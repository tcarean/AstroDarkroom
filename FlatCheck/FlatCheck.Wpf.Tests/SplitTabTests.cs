// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using FlatCheck.Tests;

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// The Split tab (R-091 to R-093): the flats of one session divided into 2 or 4 temp
/// sessions, the Siril script run in each, the part masters compared with the whole.
/// Siril is replaced by a runner that writes a generated master where the script would;
/// the temp root is redirected into the test workspace so nothing touches %TEMP%.
/// </summary>
public class SplitTabTests
{
    const int Size = 128;
    static readonly DateTime When = new(2026, 9, 28, 10, 31, 0);

    // ------------------------------------------------------------------ fixtures

    static SirilInstall FakeInstall(TempWorkspace ws, bool cli = true, string? scriptText = null)
    {
        string cliPath = Path.Combine(ws.Dir, "siril-cli.exe");
        File.WriteAllText(cliPath, "nu conteaza");
        string script = Path.Combine(ws.Dir, "OSC_Preprocessing_WithoutDark.ssf");
        File.WriteAllText(script, scriptText ?? SirilInstallTests.WithoutDark);
        return new SirilInstall(cli ? cliPath : null, new[] { script }, Path.Combine(ws.Dir, "config.ini"));
    }

    /// <summary>
    /// Every input frame is one MiB of nothing, so the copied figure of the estimate can be pinned.
    /// </summary>
    static readonly byte[] OneMiB = new byte[1 << 20];

    internal static SessionFixture Session(int flats, int biases = 3, int lights = 3, bool readme = true)
    {
        var f = new SessionFixture().WithFlatMaster().WithFlats();
        for (int i = 1; i <= flats; i++) File.WriteAllBytes(Path.Combine(f.FlatsDir, $"DSC_{i:D4}.NEF"), OneMiB);
        if (readme) File.WriteAllText(Path.Combine(f.FlatsDir, "readme.txt"), "nu e cadru");
        Inputs(f.Root, "biases", "bias", biases);
        Inputs(f.Root, "lights", "light", lights);
        return f;
    }

    static void Inputs(string root, string folder, string stem, int count)
    {
        Directory.CreateDirectory(Path.Combine(root, folder));
        for (int i = 1; i <= count; i++) File.WriteAllBytes(Path.Combine(root, folder, $"{stem}_{i:D2}.NEF"), OneMiB);
    }

    /// <summary>What the fake Siril saw in a part folder when it was started.</summary>
    internal sealed record Seen(string Folder, string[] Biases, string[] Lights, string[] Flats, string[] Darks, bool HadProcess);

    static string[] Names(string dir) => Directory.Exists(dir)
        ? Directory.EnumerateFiles(dir).Select(p => Path.GetFileName(p)!).OrderBy(n => n, StringComparer.Ordinal).ToArray()
        : Array.Empty<string>();

    /// <summary>
    /// A runner that behaves like the script on success: looks at the folder, writes the
    /// master into process/, prints a few lines. <paramref name="exitCodeFor"/> decides the
    /// exit code per part (1-based); a non-zero one writes no master.
    /// </summary>
    internal static Func<string, string, SirilRunner> FakeSiril(List<Seen> seen, Func<int, int>? exitCodeFor = null, bool writeMaster = true,
                                                                 Func<int, Func<int, int, double>>? scene = null)
        => (folder, script) => (onLine, ct) =>
        {
            seen.Add(new Seen(folder,
                Names(Path.Combine(folder, "biases")), Names(Path.Combine(folder, "lights")), Names(Path.Combine(folder, "flats")),
                Names(Path.Combine(folder, "darks")), Directory.Exists(Path.Combine(folder, "process"))));
            int part = seen.Count;
            int code = exitCodeFor?.Invoke(part) ?? 0;
            onLine("log: Running command: cd");
            onLine("progress: Converting files, 50.00%");
            if (code == 0 && writeMaster)
            {
                Directory.CreateDirectory(Path.Combine(folder, "process"));
                File.WriteAllBytes(Path.Combine(folder, "process", SirilSession.FlatMasterName),
                    FitsTestData.BuildFits(Size, Size, scene?.Invoke(part) ?? FitsTestData.Radial(Size, Size, k: 0.40 + 0.05 * part)));
                // a read-only file left by the script itself: Delete() must still remove the folder
                string locked = Path.Combine(folder, "process", "flat_.seq");
                File.WriteAllText(locked, "x");
                File.SetAttributes(locked, FileAttributes.ReadOnly);
                onLine("log: Script execution finished successfully.");
            }
            else onLine("log: Error: nu conteaza");
            return Task.FromResult(code);
        };

    internal static SplitTabViewModel Vm(SessionFixture f, TempWorkspace ws, Func<string, string, SirilRunner> runner,
                                         bool cli = true, Func<bool>? gui = null, string? scriptText = null)
    {
        var vm = new SplitTabViewModel(() => FakeInstall(ws, cli, scriptText), gui ?? (() => false), runner,
                                       clock: () => When, tempRootFor: _ => Path.Combine(ws.Dir, "temp"));
        vm.Attach(f.Session, CancellationToken.None, new HeavyRunGate());
        return vm;
    }

    // ------------------------------------------------------------------ R-091

    [Fact]
    public void R091_TheTabCountsOnlyTheFramesAndOffersTwoOrFourParts()
    {
        using var f = Session(flats: 8);
        using var ws = new TempWorkspace();

        var vm = Vm(f, ws, FakeSiril(new List<Seen>()));

        Assert.Equal(8, vm.FrameCount);                   // readme.txt is not a frame
        Assert.Equal(new[] { 2, 4 }, vm.PartOptions.Select(o => o.Parts));
        Assert.Equal(2, vm.Parts);
        Assert.Contains("8", vm.FramesLine);
        Assert.Contains("2 x 4", vm.FramesLine);
        // the disk estimate: 2 parts x (3 biases + 2 lights) + 8 flats, one MiB each
        Assert.Contains("18 MB copiate", vm.EstimateLine);
        Assert.Contains("scrise de Siril", vm.EstimateLine);
        Assert.Contains("liber pe discul temporar", vm.EstimateLine);
        Assert.Equal("OSC_Preprocessing_WithoutDark.ssf", vm.Script.Selected?.Name);
        Assert.Null(vm.Blocker);
        Assert.True(vm.CanStart);
        Assert.True(vm.Start.CanExecute(null));
        Assert.False(vm.IsRunning);
        Assert.False(vm.HasResult);

        vm.SelectParts(4);
        Assert.Equal(4, vm.Parts);
        Assert.Contains("4 x 2", vm.FramesLine);
        Assert.True(vm.CanStart);
        Assert.Contains("28 MB copiate", vm.EstimateLine);       // 4 x (3 + 2) + 8: the biases and lights four times, the flats once
    }

    /// <summary>
    /// process/ and masters/ are what Siril writes; a script that enters one never gets the
    /// original's copied.
    /// </summary>
    [Fact]
    public async Task R092_TheSessionsOutputFoldersAreNeverCopied()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var seen = new List<Seen>();
        string intoProcess = SirilInstallTests.WithoutDark.Replace("cd biases\n", "cd process\ncd ..\ncd biases\n");
        var vm = Vm(f, ws, FakeSiril(seen), scriptText: intoProcess);

        Assert.True(vm.CanStart, vm.Blocker);
        await vm.StartAsync();

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Equal(2, seen.Count);
        Assert.All(seen, s => Assert.False(s.HadProcess));      // the original's process/ (with its master) was not copied
        Assert.All(seen, s => Assert.Equal(3, s.Biases.Length));
        var plan = new SplitPlan(f.Session, 2, Path.Combine(ws.Dir, "plan"), new[] { "process", "Masters", "biases", "flats", "lights" });
        Assert.Equal(new[] { "biases" }, plan.WholeFolders);
    }

    [Fact]
    public void R091_NoEstimateBesideABlocker()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var vm = Vm(f, ws, FakeSiril(new List<Seen>()), cli: false);

        Assert.NotNull(vm.Blocker);
        Assert.Equal("", vm.EstimateLine);
    }

    [Fact]
    public void R091_ACountThatDoesNotDivideBlocksTheButtonAndSaysWhy()
    {
        using var f = Session(flats: 6);
        using var ws = new TempWorkspace();
        var vm = Vm(f, ws, FakeSiril(new List<Seen>()));

        vm.SelectParts(4);

        Assert.False(vm.CanStart);
        Assert.False(vm.Start.CanExecute(null));
        Assert.Contains("6", vm.Blocker);
        Assert.Contains("4", vm.Blocker);

        vm.SelectParts(2);
        Assert.True(vm.CanStart);
        Assert.Null(vm.Blocker);
    }

    [Fact]
    public void R091_WithoutFramesOrWithoutSirilTheButtonIsBlocked()
    {
        using var ws = new TempWorkspace();
        using var empty = Session(flats: 0, readme: false);
        var noFrames = Vm(empty, ws, FakeSiril(new List<Seen>()));
        Assert.False(noFrames.CanStart);
        Assert.Equal(SessionMessages.SplitNoFrames(empty.Session), noFrames.Blocker);

        using var f = Session(flats: 4);
        var noCli = Vm(f, ws, FakeSiril(new List<Seen>()), cli: false);
        Assert.False(noCli.CanStart);
        Assert.Equal(SessionMessages.SirilCliNotFound(), noCli.Blocker);

        var guiOpen = Vm(f, ws, FakeSiril(new List<Seen>()), gui: () => true);
        Assert.True(guiOpen.CanStart);                    // a warning, not a refusal (ADR 0035)
        Assert.Equal(SessionMessages.SirilGuiRunning(), guiOpen.Script.Warning);
    }

    [Fact]
    public async Task R091_TheMainViewModelAttachesTheTabOnOpenAndResetsItOnAnotherFolder()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var vm = new MainViewModel(() => FakeInstall(ws), () => false, runScript: FakeSiril(new List<Seen>()));

        await vm.OpenAsync(f.Root);
        Assert.Equal(4, vm.Split.FrameCount);
        Assert.True(vm.Split.CanStart);

        await vm.OpenAsync(Path.Combine(ws.Dir, "nu-exista"));
        Assert.Equal(0, vm.Split.FrameCount);
        Assert.False(vm.Split.CanStart);
    }

    // ------------------------------------------------------------------ R-092

    [Fact]
    public async Task R092_StartCopiesTheInputsRunsTheScriptPerPartAnalysesAndDeletesTheTemp()
    {
        using var f = Session(flats: 4, biases: 3, lights: 3);
        using var ws = new TempWorkspace();
        var seen = new List<Seen>();
        var vm = Vm(f, ws, FakeSiril(seen));

        await vm.StartAsync();

        Assert.False(vm.HasError, vm.ErrorText);
        Assert.True(vm.HasResult);
        Assert.False(vm.IsRunning);
        Assert.Equal(2, seen.Count);

        string temp = Path.Combine(ws.Dir, "temp");
        Assert.Equal(Path.Combine(temp, "part-1"), seen[0].Folder);
        Assert.Equal(Path.Combine(temp, "part-2"), seen[1].Folder);
        Assert.All(seen, s => Assert.Equal(new[] { "bias_01.NEF", "bias_02.NEF", "bias_03.NEF" }, s.Biases));
        Assert.All(seen, s => Assert.Equal(new[] { "light_01.NEF", "light_02.NEF" }, s.Lights));   // at most MaxLights, the first by name
        Assert.Equal(new[] { "DSC_0001.NEF", "DSC_0002.NEF" }, seen[0].Flats);
        Assert.Equal(new[] { "DSC_0003.NEF", "DSC_0004.NEF" }, seen[1].Flats);

        Assert.False(Directory.Exists(temp));             // deleted after the analysis
        Assert.Equal("", vm.TempNote);
    }

    [Fact]
    public async Task R092_TheOriginalSessionIsOnlyRead()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var vm = Vm(f, ws, FakeSiril(new List<Seen>()));
        var before = Snapshot(f.Root);

        await vm.StartAsync();

        Assert.True(vm.HasResult, vm.ErrorText);
        var after = Snapshot(f.Root);
        var added = after.Keys.Except(before.Keys).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { f.Session.SplitPngPath, f.Session.SplitResultTextPath }.OrderBy(p => p, StringComparer.Ordinal), added);
        foreach (var (path, stamp) in before) Assert.Equal(stamp, after[path]);
    }

    static Dictionary<string, (long, DateTime)> Snapshot(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(p => p, p => (new FileInfo(p).Length, File.GetLastWriteTimeUtc(p)), StringComparer.Ordinal);

    [Fact]
    public async Task R092_AFailedScriptKeepsTheTempFolderAndNamesIt()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var seen = new List<Seen>();
        var vm = Vm(f, ws, FakeSiril(seen, exitCodeFor: part => part == 2 ? 1 : 0));

        await vm.StartAsync();

        Assert.True(vm.HasError);
        Assert.False(vm.HasResult);
        Assert.Equal(2, seen.Count);
        string temp = Path.Combine(ws.Dir, "temp");
        Assert.True(Directory.Exists(Path.Combine(temp, "part-2")));
        Assert.Contains(temp, vm.ErrorText);
        Assert.Contains("2", vm.ErrorText);
        Assert.NotNull(vm.Run);
        Assert.True(vm.Run.IsFailure);
        Assert.Contains("nu conteaza", vm.Run.Log);
        Assert.False(File.Exists(f.Session.SplitResultTextPath));
    }

    [Fact]
    public async Task R092_AScriptThatLeavesNoMasterIsAFailureToo()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var vm = Vm(f, ws, FakeSiril(new List<Seen>(), writeMaster: false));

        await vm.StartAsync();

        Assert.True(vm.HasError);
        Assert.Contains(Path.Combine(ws.Dir, "temp", "part-1"), vm.ErrorText);
        Assert.True(Directory.Exists(Path.Combine(ws.Dir, "temp", "part-1")));
    }

    [Fact]
    public async Task R092_StopKillsTheRunAndDeletesTheTemp()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var started = new TaskCompletionSource();
        SirilRunner blocking = async (onLine, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);     // Siril, until killed
            return 1;
        };
        var vm = Vm(f, ws, (_, _) => blocking);

        Task run = vm.StartAsync();
        await started.Task;
        Assert.True(vm.IsRunning);
        Assert.False(vm.CanStart);                        // R-091: blocked while a split is running
        Assert.False(vm.Start.CanExecute(null));
        vm.StopRun();
        await run;

        Assert.False(vm.IsRunning);
        Assert.False(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.False(Directory.Exists(Path.Combine(ws.Dir, "temp")));
        Assert.True(vm.CanStart);
    }

    /// <summary>
    /// A script that enters darks/ (the stock OSC_Preprocessing.ssf) gets darks/ copied whole, like
    /// biases/.
    /// </summary>
    [Fact]
    public async Task R092_TheFoldersTheScriptEntersAreCopiedWhole_DarksIncluded()
    {
        using var f = Session(flats: 4);
        Inputs(f.Root, "darks", "dark", 2);
        using var ws = new TempWorkspace();
        var seen = new List<Seen>();
        string withDarks = SirilInstallTests.WithoutDark.Replace("cd biases\n", "cd darks\nconvert dark -out=../process\ncd ..\ncd biases\n");
        var vm = Vm(f, ws, FakeSiril(seen), scriptText: withDarks);

        Assert.True(vm.CanStart, vm.Blocker);
        await vm.StartAsync();

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.All(seen, s => Assert.Equal(new[] { "dark_01.NEF", "dark_02.NEF" }, s.Darks));
        Assert.All(seen, s => Assert.Equal(3, s.Biases.Length));
    }

    /// <summary>
    /// A required folder the original lacks blocks the button, as on the problem card - not a
    /// failure on part 1.
    /// </summary>
    [Fact]
    public void R091_ARequiredFolderMissingFromTheSessionBlocksTheButton()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        string withDarks = SirilInstallTests.WithoutDark.Replace("cd biases\n", "cd darks\nconvert dark -out=../process\ncd ..\ncd biases\n");
        var vm = Vm(f, ws, FakeSiril(new List<Seen>()), scriptText: withDarks);

        Assert.False(vm.CanStart);
        Assert.Contains("darks/", vm.Blocker);
    }

    /// <summary>
    /// A raw copied from a card is often read-only; the copy must still be deletable at the end.
    /// </summary>
    [Fact]
    public async Task R092_ReadOnlySourcesDoNotKeepTheTempFolder()
    {
        using var f = Session(flats: 4);
        foreach (string p in Directory.EnumerateFiles(Path.Combine(f.Root, "biases")))
            File.SetAttributes(p, FileAttributes.ReadOnly);
        using var ws = new TempWorkspace();
        var vm = Vm(f, ws, FakeSiril(new List<Seen>()));

        try
        {
            await vm.StartAsync();

            Assert.True(vm.HasResult, vm.ErrorText);
            Assert.Equal("", vm.TempNote);
            Assert.False(Directory.Exists(Path.Combine(ws.Dir, "temp")));
        }
        finally
        {
            foreach (string p in Directory.EnumerateFiles(Path.Combine(f.Root, "biases")))
                File.SetAttributes(p, FileAttributes.Normal);
        }
    }

    /// <summary>
    /// flats/ can change between opening the folder and pressing the button: the count is re-read
    /// then.
    /// </summary>
    [Fact]
    public async Task R092_FramesAddedAfterOpeningAreCountedWhenTheButtonIsPressed()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var seen = new List<Seen>();
        var vm = Vm(f, ws, FakeSiril(seen));
        vm.SelectParts(4);
        Assert.True(vm.CanStart);
        File.WriteAllText(Path.Combine(f.FlatsDir, "DSC_0005.NEF"), "x");

        await vm.StartAsync();

        Assert.Empty(seen);                              // nothing was copied or run
        Assert.Equal(5, vm.FrameCount);
        Assert.False(vm.CanStart);
        Assert.Contains("5", vm.Blocker);
        Assert.False(vm.HasError);
    }

    /// <summary>
    /// Choosing another folder mid-split kills Siril and deletes the temp folder, like Opreste.
    /// </summary>
    [Fact]
    public async Task R092_OpeningAnotherFolderMidSplitStopsItAndDeletesTheTemp()
    {
        using var f = Session(flats: 4);
        using var other = new SessionFixture().WithFlatMaster();
        using var ws = new TempWorkspace();
        var started = new TaskCompletionSource();
        SirilRunner blocking = async (onLine, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return 1;
        };
        var vm = new MainViewModel(() => FakeInstall(ws), () => false, runScript: (_, _) => blocking,
                                   splitTempRootFor: _ => Path.Combine(ws.Dir, "temp"));
        await vm.OpenAsync(f.Root);
        vm.Split.Start.Execute(null);
        await started.Task;
        Assert.True(vm.Split.IsRunning);

        await vm.OpenAsync(other.Root);
        await vm.Split.Pending;

        Assert.False(vm.Split.IsRunning);
        Assert.False(vm.Split.HasError);
        Assert.False(Directory.Exists(Path.Combine(ws.Dir, "temp")));
        Assert.Equal(0, vm.Split.FrameCount);            // the other folder has no flats/
    }

    // ------------------------------------------------------------------ R-093

    /// <summary>R-138: the split's report and mosaic replace the earlier files and are never
    /// written into them; each earlier file's second name keeps its bytes.</summary>
    [Fact]
    public async Task R138_TheSavedReportAndMosaicReplaceTheEarlierFilesAndNeverWriteIntoThem()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        byte[] old = Enumerable.Repeat((byte)0x55, 5000).ToArray();
        File.WriteAllBytes(f.Session.SplitResultTextPath, old);
        File.WriteAllBytes(f.Session.SplitPngPath, old);
        string oldReport = SecondName.Of(f.Session.SplitResultTextPath), oldMosaic = SecondName.Of(f.Session.SplitPngPath);
        var vm = Vm(f, ws, FakeSiril(new List<Seen>()));

        await vm.StartAsync();

        Assert.Equal(f.Session.SplitResultTextPath, vm.SavedTo);
        Assert.Equal(old, File.ReadAllBytes(oldReport));
        Assert.Equal(old, File.ReadAllBytes(oldMosaic));
        Assert.Equal(vm.Report, File.ReadAllText(f.Session.SplitResultTextPath));
        Assert.NotEqual(old, File.ReadAllBytes(f.Session.SplitPngPath));
    }

    [Fact]
    public async Task R093_TheTableHasAVerdictOnEveryTestCell_AndTheSavedFilesAreTheTabs()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var vm = Vm(f, ws, FakeSiril(new List<Seen>()));

        await vm.StartAsync();

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Equal(new[] { "partea 1 din 2", "partea 2 din 2", "intreg" }, vm.Headers);   // no raw the loader can read: no profile column
        Assert.Equal(7, vm.Rows.Count);
        foreach (SplitTableRow test in vm.Rows.Take(5))
        {
            Assert.Equal(3, test.Cells.Count);
            Assert.All(test.Cells, c => Assert.True(c.HasVerdict));
        }
        Assert.Contains(vm.Rows, r => r.IsKey);
        Assert.Equal("intreg", vm.Maps[2].Label);
        Assert.Equal(3, vm.Maps.Count);
        Assert.All(vm.Maps, m => Assert.NotNull(m.Png));
        Assert.NotEmpty(vm.MapLegend);

        Assert.Equal(f.Session.SplitResultTextPath, vm.SavedTo);
        Assert.Equal(vm.Report, File.ReadAllText(f.Session.SplitResultTextPath));
        Assert.StartsWith(VersionInfo.Banner, vm.Report);
        Assert.Contains("2026-09-28 10:31", vm.Report);
        byte[] png = File.ReadAllBytes(f.Session.SplitPngPath);
        int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];   // IHDR, big-endian
        Assert.Equal(3 * vm.Maps[0].Raster.W + 2 * PngWriter.MosaicGap, width);      // three tiles side by side
    }

    /// <summary>The tab on the catalog with the two 135 f/2 lenses a raw's range alone finds
    /// (R-121's case), the range raw in place of the files in flats/, attached with the
    /// folder's switches.</summary>
    static SplitTabViewModel VmWithRangePair(SessionFixture f, TempWorkspace ws, SessionSettings? settings, out ProfileSources sources)
    {
        ProfileSources found = sources = LensProfileTestData.Sources(ws, rangePair: true);
        var vm = new SplitTabViewModel(() => FakeInstall(ws), () => false, FakeSiril(new List<Seen>()),
                                       new ProfileCatalog(() => found),
                                       path => RawReader.IsRawPath(path) ? LensProfileTestData.Range135Raw() : ImageLoader.LoadInfo(path),
                                       clock: () => When, tempRootFor: _ => Path.Combine(ws.Dir, "temp"));
        vm.Attach(f.Session, CancellationToken.None, new HeavyRunGate(), settings);
        return vm;
    }

    /// <summary>
    /// R-093: the lens chosen on the Structure tab (R-121) is the Split's too. Before, the tab
    /// compared every lens of the range and let the nearest decide, so the two saved reports
    /// could read the same master against two profiles.
    /// </summary>
    [Fact]
    public async Task R093_TheLensChosenOnTheStructureTabIsTheOnlyOneCompared()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();

        // nothing chosen: both lenses of the range are rows
        var none = VmWithRangePair(f, ws, SessionSettings.None, out _);
        await none.StartAsync();
        Assert.True(none.HasResult, none.ErrorText);
        Assert.Contains(LensProfileTestData.Nikkor135Dc, none.Report);
        Assert.Contains(LensProfileTestData.Samyang135, none.Report);

        var chosen = SessionSettings.None;
        var vm = VmWithRangePair(f, ws, chosen, out ProfileSources sources);
        chosen.Lens = LensProfileTestData.KeyOf(sources, LensProfileTestData.Samyang135);
        await vm.StartAsync();

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Equal(4, vm.Headers.Count);                                   // two parts, the whole, the profile
        Assert.Contains(LensProfileTestData.Samyang135, vm.Report);
        Assert.DoesNotContain(LensProfileTestData.Nikkor135Dc, vm.Report);
        SplitTableRow reference = Assert.Single(vm.Rows, r => r.IsReference);
        Assert.Contains(LensProfileTestData.Samyang135, reference.Label);
        Assert.Contains("obiectivul ales de tine", reference.Note);              // not "the nearest": it was chosen
        Assert.Contains("obiectivul ales de tine", vm.ReferenceLine);
        Assert.Contains("cel mai apropiat de intreg", none.ReferenceLine);
        Assert.Equal(none.Rows.Count - 1, vm.Rows.Count);                    // one profile row fewer
    }

    /// <summary>
    /// R-093: with the box "no filters" off (R-118) the Structure tab compares nothing, and
    /// neither does the Split: no profile row, no profile column, the five tests' verdict.
    /// The box is read at the analysis, so a flip made after the folder opened counts.
    /// </summary>
    [Fact]
    public async Task R093_WithAFilterOnTheLensNothingIsComparedWithAProfile()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var settings = SessionSettings.None;
        var vm = VmWithRangePair(f, ws, settings, out _);
        settings.NoFilters = false;                                         // unticked on the Structure tab, after the opening

        await vm.StartAsync();

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Equal(new[] { "partea 1 din 2", "partea 2 din 2", "intreg" }, vm.Headers);
        Assert.Equal(7, vm.Rows.Count);                                     // the five tests, the depth, the verdict
        Assert.DoesNotContain(vm.Rows, r => r.IsReference);
        Assert.Contains("filtru pe obiectiv", vm.ReferenceLine);                // why, in place of "no raw"
        Assert.Contains("filtru pe obiectiv", vm.Report);
        Assert.DoesNotContain(LensProfileTestData.Samyang135, vm.Report);
        Assert.DoesNotContain(LensProfileTestData.Nikkor135Dc, vm.Report);
    }

    /// <summary>
    /// R-093: the switches are read when the parts' masters are analysed, not when the button
    /// is pressed: the other tabs stay usable through the Siril runs, and the two saved reports
    /// must speak of the same comparison. Here the box is unticked while part 1 runs.
    /// </summary>
    [Fact]
    public async Task R093_TheSwitchesAreReadAtTheAnalysis_SoAFlipDuringTheSirilRunsCounts()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var settings = SessionSettings.None;
        ProfileSources sources = LensProfileTestData.Sources(ws, rangePair: true);
        var vm = new SplitTabViewModel(() => FakeInstall(ws), () => false,
                                       FakeSiril(new List<Seen>(), exitCodeFor: part => { if (part == 1) settings.NoFilters = false; return 0; }),
                                       new ProfileCatalog(() => sources),
                                       path => RawReader.IsRawPath(path) ? LensProfileTestData.Range135Raw() : ImageLoader.LoadInfo(path),
                                       clock: () => When, tempRootFor: _ => Path.Combine(ws.Dir, "temp"));
        vm.Attach(f.Session, CancellationToken.None, new HeavyRunGate(), settings);
        Assert.True(settings.NoFilters);                                    // ticked at the press

        await vm.StartAsync();

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Equal(3, vm.Headers.Count);
        Assert.Contains("filtru pe obiectiv", vm.ReferenceLine);
    }

    /// <summary>
    /// R-093: the window hands the Split tab the folder's switches. A folder remembered with
    /// the box off (R-119) splits without a profile; the same folder without the file compares.
    /// </summary>
    [Fact]
    public async Task R093_TheWindowHandsTheTabTheFoldersSwitches()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        ProfileSources sources = LensProfileTestData.Sources(ws, rangePair: true);
        MainViewModel Window() => new MainViewModel(() => FakeInstall(ws), () => false, locateProfiles: () => sources,
            loadInfo: path => RawReader.IsRawPath(path) ? LensProfileTestData.Range135Raw() : ImageLoader.LoadInfo(path),
            runScript: FakeSiril(new List<Seen>()), clock: () => When, splitTempRootFor: _ => Path.Combine(ws.Dir, "temp"));

        var compared = Window();
        await compared.OpenAsync(f.Root);
        await compared.Split.StartAsync();
        Assert.True(compared.Split.HasResult, compared.Split.ErrorText);
        Assert.Equal(4, compared.Split.Headers.Count);

        File.WriteAllText(f.Session.SettingsPath, "{\"noFilters\":false,\"skyFlat\":false}");
        var filtered = Window();
        await filtered.OpenAsync(f.Root);
        await filtered.Split.StartAsync();
        Assert.True(filtered.Split.HasResult, filtered.Split.ErrorText);
        Assert.Equal(3, filtered.Split.Headers.Count);
        Assert.Contains("filtru pe obiectiv", filtered.Split.ReferenceLine);
    }

    [Fact]
    public async Task R093_ChangingTheScaleReencodesTheMapsWithoutRerunning()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var seen = new List<Seen>();
        // the parts carry gradients of 2 and 4 %, so their residual ranges differ from each other
        // and from the whole master's: the shared range is then no map's own (on the clean radial
        // parts the three ranges coincided once the geometry was centred, ADR 0095)
        var vm = Vm(f, ws, FakeSiril(seen, scene: part => FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size, k: 0.40 + 0.05 * part), Size, 0.02 * part)));
        await vm.StartAsync();
        byte[] before = vm.Maps[0].Png;
        Assert.Equal(RunReport.MapLegend(PngWriter.FixedRange(PngWriter.DefaultCapPct)), vm.MapLegend);

        vm.MapScale.Options[0].IsSelected = true;        // auto: the file's shared range, not each map's own

        Assert.Equal(2, seen.Count);
        Assert.NotEqual(before, vm.Maps[0].Png);
        Assert.NotEqual(RunReport.MapLegend(PngWriter.FixedRange(PngWriter.DefaultCapPct)), vm.MapLegend);
        Assert.NotEqual(RunReport.MapLegend(PngWriter.AutoRange(vm.Maps[0].Raster)), vm.MapLegend);
    }
}
