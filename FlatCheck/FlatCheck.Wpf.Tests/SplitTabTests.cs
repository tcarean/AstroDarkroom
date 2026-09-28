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

    static SirilInstall FakeInstall(TempWorkspace ws, bool cli = true)
    {
        string cliPath = Path.Combine(ws.Dir, "siril-cli.exe");
        File.WriteAllText(cliPath, "nu conteaza");
        string script = Path.Combine(ws.Dir, "OSC_Preprocessing_WithoutDark.ssf");
        File.WriteAllText(script, SirilInstallTests.WithoutDark);
        return new SirilInstall(cli ? cliPath : null, new[] { script }, Path.Combine(ws.Dir, "config.ini"));
    }

    static SessionFixture Session(int flats, int biases = 3, int lights = 3, bool readme = true)
    {
        var f = new SessionFixture().WithFlatMaster().WithFlats();
        for (int i = 1; i <= flats; i++) File.WriteAllText(Path.Combine(f.FlatsDir, $"DSC_{i:D4}.NEF"), "x");
        if (readme) File.WriteAllText(Path.Combine(f.FlatsDir, "readme.txt"), "nu e cadru");
        Inputs(f.Root, "biases", "bias", biases);
        Inputs(f.Root, "lights", "light", lights);
        return f;
    }

    static void Inputs(string root, string folder, string stem, int count)
    {
        Directory.CreateDirectory(Path.Combine(root, folder));
        for (int i = 1; i <= count; i++) File.WriteAllText(Path.Combine(root, folder, $"{stem}_{i:D2}.NEF"), "x");
    }

    /// <summary>What the fake Siril saw in a part folder when it was started.</summary>
    sealed record Seen(string Folder, string[] Biases, string[] Lights, string[] Flats);

    static string[] Names(string dir) => Directory.Exists(dir)
        ? Directory.EnumerateFiles(dir).Select(p => Path.GetFileName(p)!).OrderBy(n => n, StringComparer.Ordinal).ToArray()
        : Array.Empty<string>();

    /// <summary>
    /// A runner that behaves like the script on success: looks at the folder, writes the
    /// master into process/, prints a few lines. <paramref name="exitCodeFor"/> decides the
    /// exit code per part (1-based); a non-zero one writes no master.
    /// </summary>
    static Func<string, string, SirilRunner> FakeSiril(List<Seen> seen, Func<int, int>? exitCodeFor = null, bool writeMaster = true)
        => (folder, script) => (onLine, ct) =>
        {
            seen.Add(new Seen(folder,
                Names(Path.Combine(folder, "biases")), Names(Path.Combine(folder, "lights")), Names(Path.Combine(folder, "flats"))));
            int part = seen.Count;
            int code = exitCodeFor?.Invoke(part) ?? 0;
            onLine("log: Running command: cd");
            onLine("progress: Converting files, 50.00%");
            if (code == 0 && writeMaster)
            {
                Directory.CreateDirectory(Path.Combine(folder, "process"));
                File.WriteAllBytes(Path.Combine(folder, "process", SirilSession.FlatMasterName),
                    FitsTestData.BuildFits(Size, Size, FitsTestData.Radial(Size, Size, k: 0.40 + 0.05 * part)));
                onLine("log: Script execution finished successfully.");
            }
            else onLine("log: Error: nu conteaza");
            return Task.FromResult(code);
        };

    static SplitTabViewModel Vm(SessionFixture f, TempWorkspace ws, Func<string, string, SirilRunner> runner,
                                bool cli = true, Func<bool>? gui = null)
    {
        var vm = new SplitTabViewModel(() => FakeInstall(ws, cli), gui ?? (() => false), runner,
                                       clock: () => When, tempRootFor: _ => Path.Combine(ws.Dir, "temp"));
        vm.Attach(f.Session, CancellationToken.None, () => Task.CompletedTask);
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
        Assert.Equal(Path.Combine(temp, "parte-1"), seen[0].Folder);
        Assert.Equal(Path.Combine(temp, "parte-2"), seen[1].Folder);
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
        Assert.True(Directory.Exists(Path.Combine(temp, "parte-2")));
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
        Assert.Contains(Path.Combine(ws.Dir, "temp", "parte-1"), vm.ErrorText);
        Assert.True(Directory.Exists(Path.Combine(ws.Dir, "temp", "parte-1")));
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
        vm.StopRun();
        await run;

        Assert.False(vm.IsRunning);
        Assert.False(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.False(Directory.Exists(Path.Combine(ws.Dir, "temp")));
        Assert.True(vm.CanStart);
    }

    [Fact]
    public async Task R092_TheProgressCoversEveryPartAndReachesTheEnd()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var phases = new List<string>();
        var vm = Vm(f, ws, FakeSiril(new List<Seen>()));
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Phase) && vm.Phase.Length > 0) phases.Add(vm.Phase); };

        await vm.StartAsync();

        Assert.True(vm.HasResult, vm.ErrorText);
        Assert.Contains(phases, p => p.Contains("1 din 2"));
        Assert.Contains(phases, p => p.Contains("2 din 2"));
        Assert.Equal(1.0, vm.Progress, 6);
        Assert.True(vm.ProgressKnown);
    }

    // ------------------------------------------------------------------ R-093

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
        Assert.True(width > 3 * 32, $"latimea mozaicului: {width}");                 // three tiles side by side
    }

    [Fact]
    public async Task R093_ChangingTheScaleReencodesTheMapsWithoutRerunning()
    {
        using var f = Session(flats: 4);
        using var ws = new TempWorkspace();
        var seen = new List<Seen>();
        var vm = Vm(f, ws, FakeSiril(seen));
        await vm.StartAsync();
        byte[] before = vm.Maps[0].Png;

        vm.MapScale.Options[0].IsSelected = true;        // auto

        Assert.Equal(2, seen.Count);
        Assert.NotEqual(before, vm.Maps[0].Png);
        Assert.Contains("albastru", vm.MapLegend);
    }
}
