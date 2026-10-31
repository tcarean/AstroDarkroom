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
/// Opening a folder, end to end. R-081 and R-080 (with R-088) are checked here: what starts
/// by itself, and what happens when the folder is not good.
/// </summary>
public class MainViewModelTests
{
    /// <summary>R-081: the structure and the end-to-end test start by themselves.</summary>
    [Fact]
    public async Task OpeningAFolderRunsStructureAndEndToEndByItself()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(6);
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.Structure.HasResult);
        Assert.True(vm.Light.HasResult);
        Assert.False(vm.IsRunning);
    }

    /// <summary>
    /// The opening chain holds the gate from the structure run to the end of the series, and
    /// every tab a click runs takes the same gate: a Histogram pick and an Expo pick made while
    /// the chain is held open at the profile catalog both wait, and both finish once the chain
    /// does (ADR 0061).
    /// </summary>
    [Fact]
    public async Task R081_TheOpeningChainHoldsTheGateEveryClickRunTakes()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF");
        string raw = f.WriteAnalysableRawFlat("flat.fit");
        using var ws = new TempWorkspace();
        using var hold = new ManualResetEventSlim(false);
        var sources = LensProfileTestData.Sources(ws);
        var vm = new MainViewModel(null, null, null,
            locateProfiles: () => { hold.Wait(); return sources; },
            loadInfo: path => RawReader.IsRawPath(path) ? RawFrame() : ImageLoader.LoadInfo(path));

        Task open = vm.OpenAsync(f.Root);   // stops at the catalog, which the test holds
        vm.HistEq.Selected = vm.HistEq.Files[0];
        Task histEq = vm.HistEq.Pending;
        vm.Exposure.Selected = raw;
        Task expo = vm.Exposure.Pending;

        Task any = Task.WhenAny(histEq, expo);
        Assert.NotSame(any, await Task.WhenAny(any, Task.Delay(1000)));
        Assert.Equal(SessionMessages.WaitingForRunningAnalysis(), vm.HistEq.BusyMessage);
        Assert.Equal(SessionMessages.WaitingForRunningAnalysis(), vm.Exposure.BusyMessage);

        hold.Set();
        await open;
        await Task.WhenAll(histEq, expo);

        Assert.True(vm.Structure.HasResult);
        Assert.True(vm.HistEq.HasResult);
        Assert.True(vm.Exposure.HasResult);
    }

    /// <summary>
    /// The chain itself waits for the gate: a folder opened while another run still holds it (a
    /// click-run of the previous folder, which does not stop mid-computation) starts its
    /// structure run only when that run lets go.
    /// </summary>
    [Fact]
    public async Task R081_TheOpeningChainWaitsForARunThatStillHoldsTheGate()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(6);
        var gate = new HeavyRunGate();
        IDisposable other = await gate.EnterAsync(CancellationToken.None);
        var vm = new MainViewModel(null, null, gate: gate);

        Task open = vm.OpenAsync(f.Root);

        Assert.NotSame(open, await Task.WhenAny(open, Task.Delay(1000)));
        Assert.True(vm.IsRunning);
        Assert.False(vm.Structure.HasResult);

        other.Dispose();
        await open;

        Assert.True(vm.Structure.HasResult);
        Assert.True(vm.Light.HasResult);
        Assert.False(vm.IsRunning);
        Assert.False(gate.IsHeld);
    }

    /// <summary>Another folder while the chain still waits for the gate: the wait is cancelled
    /// and the new folder's chain takes its place, without a fault reaching the caller.</summary>
    [Fact]
    public async Task R081_AnotherFolderCancelsAChainStillWaitingForTheGate()
    {
        using var a = new SessionFixture().WithFlatMaster();
        using var b = new SessionFixture().WithFlatMaster();
        var gate = new HeavyRunGate();
        IDisposable other = await gate.EnterAsync(CancellationToken.None);
        var vm = new MainViewModel(null, null, gate: gate);

        Task first = vm.OpenAsync(a.Root);
        Task second = vm.OpenAsync(b.Root);
        await first;
        Assert.False(second.IsCompleted);

        other.Dispose();
        await second;

        Assert.Equal(b.Root, vm.Folder);
        Assert.True(vm.Structure.HasResult);
        Assert.False(gate.IsHeld);
    }

    /// <summary>What the metadata reader answers for the fabricated raw in flats/.</summary>
    static LoadedFrame RawFrame() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 20,
        FNumber = 2.8,
        Camera = "NIKON D3300",
        LensId = 139,
        LensMinFocalMm = 11,
        LensMaxFocalMm = 20,
        LensMaxAperture = 2.8,
    };

    /// <summary>
    /// The exposure tab takes its list, but computes nothing until something is chosen.
    /// </summary>
    [Fact]
    public async Task ExposureGetsItsListButWaitsForAChoice()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF", "b.NEF");
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.Equal(2, vm.Exposure.Files.Count);
        Assert.False(vm.Exposure.HasResult);
    }

    /// <summary>
    /// The histogram tab gets the list - the master first - and computes nothing at opening:
    /// the 24 MP master is already being read by Structura then.
    /// </summary>
    [Fact]
    public async Task HistEqGetsItsListWithTheMasterFirstButWaitsForAChoice()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF", "b.NEF");
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.Equal(3, vm.HistEq.Files.Count);
        Assert.True(vm.HistEq.Files[0].IsMaster);
        Assert.False(vm.HistEq.HasResult);
        Assert.Null(vm.HistEq.Selected);
    }

    /// <summary>Case A through the window: a folder with the smoothed master already written lists
    /// it at once, second.</summary>
    [Fact]
    public async Task AFolderOpenedWithASmoothedMasterListsItAtOnce()
    {
        using var f = new SessionFixture().WithFlatMaster().WithSmoothedMaster().WithRawFlats("a.NEF");
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.Equal(new[] { "MASTER", "NETEZIT", "" }, vm.HistEq.Files.Select(r => r.Tag));
        Assert.Null(vm.HistEq.Selected);
    }

    /// <summary>
    /// Case B, end to end: the smoothing button writes the file and the row appears in the
    /// Histogram tab's list, without starting anything there - "nothing at opening" is also
    /// "nothing at a write".
    /// </summary>
    [Fact]
    public async Task SmoothingFromTheWindowPutsTheSmoothedMasterIntoTheHistogramList()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF");
        var vm = new MainViewModel(null, null, _ => true);

        await vm.OpenAsync(f.Root);
        Assert.Equal(new[] { "MASTER", "" }, vm.HistEq.Files.Select(r => r.Tag));

        await vm.Smooth.RunAsync();

        Assert.Equal(f.Session.SmoothedMasterPath, vm.Smooth.SavedTo);
        Assert.Equal(new[] { "MASTER", "NETEZIT", "" }, vm.HistEq.Files.Select(r => r.Tag));
        Assert.Equal(vm.Smooth.SavedTo, vm.HistEq.Files[1].Path);
        Assert.Null(vm.HistEq.Selected);
        Assert.False(vm.HistEq.HasResult);
    }

    /// <summary>Another folder, without a smoothed master: the NETEZIT row does not survive the
    /// change.</summary>
    [Fact]
    public async Task OpeningAnotherFolderDropsTheSmoothedRow()
    {
        using var a = new SessionFixture().WithFlatMaster().WithSmoothedMaster();
        using var b = new SessionFixture().WithFlatMaster();
        var vm = new MainViewModel();

        await vm.OpenAsync(a.Root);
        Assert.Contains(vm.HistEq.Files, r => r.IsSmoothed);

        await vm.OpenAsync(b.Root);

        Assert.DoesNotContain(vm.HistEq.Files, r => r.IsSmoothed);
        Assert.Single(vm.HistEq.Files);
    }

    /// <summary>
    /// The Smida session from the archive: the master in masters/, as OSC_Preprocessing.ssf
    /// from Siril 1.4 leaves it, calibrated lights in process/. It opens and runs both.
    /// </summary>
    [Fact]
    public async Task AFolderWithTheMasterInMastersOpensAndRuns()
    {
        using var f = new SessionFixture().WithFlatMasterInMasters().WithLightSeries(6);
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.False(vm.HasProblem);
        Assert.True(vm.Structure.HasResult);
        Assert.True(vm.Light.HasResult);
        Assert.True(vm.HistEq.Files[0].IsMaster);
    }

    /// <summary>No process/ but the master in masters/: the structure runs, the series is missing
    /// with an explanation.</summary>
    [Fact]
    public async Task WithoutProcessButWithTheMasterInMastersStructureStillRuns()
    {
        using var f = new SessionFixture().WithFlatMasterInMasters();
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.False(vm.HasProblem);
        Assert.True(vm.Structure.HasResult);
        Assert.False(vm.Light.HasResult);
        Assert.True(vm.Light.HasError);
    }

    // ------------------------------------------------------------------ invalid folder

    /// <summary>
    /// R-080 with R-088: without the flat master, the application does not start on this folder
    /// and says exactly what to do - run the preprocessing script in Siril. A message that only
    /// described the missing file would leave the user to find the connection alone.
    ///
    /// The script named is WithoutDark, the author's script; the standard one from Siril 1.4
    /// works just as well since the master is also looked for in masters/.
    /// </summary>
    [Fact]
    public async Task WithoutTheFlatMasterItNamesTheSirilScriptToRun()
    {
        using var f = new SessionFixture().WithProcess();
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasProblem);
        Assert.Contains("OSC_Preprocessing_WithoutDark.ssf", vm.ProblemText);
        Assert.Contains("pp_flat_stacked.fit", vm.ProblemText);
        Assert.Contains("Siril", vm.ProblemText);
    }

    /// <summary>
    /// A folder the window cannot list fully keeps its card: the log's Debug line about the
    /// folder, which counts its files, must not take the "flat master is missing" card down
    /// with it (the review of 2026-10-27, A1). The folder is made unlistable with a deny rule.
    /// </summary>
    [Fact]
    public async Task R080_AnUnlistableProcessFolderStillGetsTheMissingMasterCard()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new SessionFixture().WithFlats().WithProcess();
        using var deny = UnlistableFolder.Deny(f.ProcessDir);
        var vm = new MainViewModel();
        using var capture = LogCapture.Start();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasProblem);
        Assert.Contains("pp_flat_stacked.fit", vm.ProblemText);
        Assert.Contains("OSC_Preprocessing_WithoutDark.ssf", vm.ProblemText);
        Assert.Contains(capture.Events, e => e.Message.Contains("lights=unreadable (UnauthorizedAccessException)"));
    }

    /// <summary>
    /// R-110: an exception while a folder is opened, here a <c>flats</c> folder the user may
    /// not list, is shown as a card and logged with its stack; before, the task fired by the
    /// window swallowed it and the tabs stayed empty (the review of 2026-10-27, A2).
    /// </summary>
    [Fact]
    public async Task R080_AnUnlistableFlatsFolderGivesACardAndAnErrorLine_NotEmptyTabs()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new SessionFixture().WithFlatMaster().WithFlats();
        using var deny = UnlistableFolder.Deny(f.FlatsDir);
        var vm = new MainViewModel();
        using var capture = LogCapture.Start();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasProblem);
        Assert.Contains("flats", vm.ProblemText);
        Assert.False(vm.IsRunning);
        Assert.False(vm.Exposure.IsBusy);
        var failed = Assert.Single(capture.Events, e => e.Level == NLog.LogLevel.Error);
        Assert.IsType<UnauthorizedAccessException>(failed.Exception);
        Assert.Contains("   at ", failed.Line);
    }

    [Fact]
    public async Task WithoutProcessAtAllItAlsoNamesTheScript()
    {
        using var f = new SessionFixture();
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasProblem);
        Assert.Contains("OSC_Preprocessing_WithoutDark.ssf", vm.ProblemText);
        Assert.Contains("process/", vm.ProblemText);
    }

    // ------------------------------------------------------------------ running the script

    /// <summary>
    /// A fake Siril install: "siril-cli" is an arbitrary file (only its existence matters
    /// here), the scripts are .ssf files written in the test. The input folders WithoutDark
    /// asks for are added optionally.
    /// </summary>
    static SirilInstall FakeInstall(TempWorkspace ws, bool cli = true, params string[] scripts)
    {
        string cliPath = Path.Combine(ws.Dir, "siril-cli.exe");
        File.WriteAllText(cliPath, "nu conteaza");

        var paths = new List<string>();
        foreach (string name in scripts)
        {
            string p = Path.Combine(ws.Dir, name);
            File.WriteAllText(p, SirilInstallTests.WithoutDark);
            paths.Add(p);
        }
        return new SirilInstall(cli ? cliPath : null, paths, Path.Combine(ws.Dir, "config.ini"));
    }

    static void WithInputs(SessionFixture f, params string[] folders)
    {
        foreach (string name in folders)
        {
            Directory.CreateDirectory(Path.Combine(f.Root, name));
            File.WriteAllText(Path.Combine(f.Root, name, "DSC_0001.NEF"), "x");
        }
    }

    /// <summary>On a folder without a master, with Siril installed and the inputs in place, the
    /// button can be pressed.</summary>
    [Fact]
    public async Task WithoutTheMasterTheScriptCanBeRunFromHere_WithoutDarkProposed()
    {
        using var f = new SessionFixture().WithProcess();
        using var ws = new TempWorkspace();
        WithInputs(f, "biases", "flats", "lights");
        var install = FakeInstall(ws, true, "Z_alt.ssf", "OSC_Preprocessing_WithoutDark.ssf", "A_alt.ssf");
        var vm = new MainViewModel(() => install, () => false);

        await vm.OpenAsync(f.Root);

        var p = vm.Preprocess;
        Assert.True(p.IsOffered);
        Assert.Equal(3, p.Scripts.Count);
        Assert.Equal("OSC_Preprocessing_WithoutDark.ssf", p.Selected?.Name);
        Assert.Null(p.Blocker);
        Assert.Null(p.Warning);
        Assert.True(p.CanRun);
        Assert.Equal(install.CliPath, p.CliPath);
        Assert.EndsWith("OSC_Preprocessing_WithoutDark.ssf", p.SelectedPath);
    }

    [Fact]
    public async Task WithoutProcessAtAllTheScriptIsOfferedToo()
    {
        using var f = new SessionFixture();
        using var ws = new TempWorkspace();
        WithInputs(f, "biases", "flats", "lights");
        var vm = new MainViewModel(() => FakeInstall(ws, true, "OSC_Preprocessing_WithoutDark.ssf"), () => false);

        await vm.OpenAsync(f.Root);

        Assert.True(vm.Preprocess.IsOffered);
        Assert.True(vm.Preprocess.CanRun);
    }

    /// <summary>
    /// A missing or empty input subfolder blocks the button and is named - otherwise Siril
    /// would stop at line three with a failed "cd", and the user would read the log to find out.
    /// </summary>
    [Fact]
    public async Task AMissingInputFolderBlocksTheButtonAndIsNamed()
    {
        using var f = new SessionFixture().WithProcess();
        using var ws = new TempWorkspace();
        WithInputs(f, "biases", "flats");
        Directory.CreateDirectory(Path.Combine(f.Root, "lights"));   // exists, but empty
        var vm = new MainViewModel(() => FakeInstall(ws, true, "OSC_Preprocessing_WithoutDark.ssf"), () => false);

        await vm.OpenAsync(f.Root);

        var p = vm.Preprocess;
        Assert.True(p.IsOffered);
        Assert.False(p.CanRun);
        Assert.Contains("lights/", p.Blocker);
        Assert.DoesNotContain("goale: biases", p.Blocker);
        Assert.Contains("OSC_Preprocessing_WithoutDark.ssf", p.Blocker);
    }

    [Fact]
    public async Task WithoutSirilCliTheButtonIsBlockedAndSaysSo()
    {
        using var f = new SessionFixture().WithProcess();
        using var ws = new TempWorkspace();
        WithInputs(f, "biases", "flats", "lights");
        var vm = new MainViewModel(() => FakeInstall(ws, false, "OSC_Preprocessing_WithoutDark.ssf"), () => false);

        await vm.OpenAsync(f.Root);

        Assert.True(vm.Preprocess.IsOffered);
        Assert.False(vm.Preprocess.CanRun);
        Assert.Equal(SessionMessages.SirilCliNotFound(), vm.Preprocess.Blocker);
    }

    [Fact]
    public async Task WithoutAnyScriptTheButtonIsBlockedAndNamesTheConfig()
    {
        using var f = new SessionFixture().WithProcess();
        using var ws = new TempWorkspace();
        var vm = new MainViewModel(() => FakeInstall(ws, true), () => false);

        await vm.OpenAsync(f.Root);

        Assert.False(vm.Preprocess.CanRun);
        Assert.Contains("config.ini", vm.Preprocess.Blocker);
        Assert.Null(vm.Preprocess.Selected);
    }

    /// <summary>Siril open is a warning, not a block: there is no knowing which folder it works
    /// on.</summary>
    [Fact]
    public async Task SirilRunningIsAWarningNotABlocker()
    {
        using var f = new SessionFixture().WithProcess();
        using var ws = new TempWorkspace();
        WithInputs(f, "biases", "flats", "lights");
        bool running = true;
        var vm = new MainViewModel(() => FakeInstall(ws, true, "OSC_Preprocessing_WithoutDark.ssf"), () => running);

        await vm.OpenAsync(f.Root);

        Assert.True(vm.Preprocess.CanRun);
        Assert.Equal(SessionMessages.SirilGuiRunning(), vm.Preprocess.Warning);

        // checked again at the press: Siril was closed meanwhile
        running = false;
        vm.Preprocess.Refresh();
        Assert.Null(vm.Preprocess.Warning);
    }

    /// <summary>Another script, other requirements: changing the choice redoes the block.</summary>
    [Fact]
    public async Task ChangingTheScriptRechecksTheInputs()
    {
        using var f = new SessionFixture().WithProcess();
        using var ws = new TempWorkspace();
        WithInputs(f, "lights");
        var install = FakeInstall(ws, true, "OSC_Preprocessing_WithoutDark.ssf");
        string onlyLights = Path.Combine(ws.Dir, "Doar_lights.ssf");
        File.WriteAllText(onlyLights, "requires 1.2.0\ncd lights\nconvert light -out=../process\ncd ..\nclose\n");
        install = new SirilInstall(install.CliPath, new[] { install.Scripts[0], onlyLights });
        var vm = new MainViewModel(() => install, () => false);

        await vm.OpenAsync(f.Root);
        Assert.False(vm.Preprocess.CanRun);

        vm.Preprocess.Selected = vm.Preprocess.Scripts.First(s => s.Name == "Doar_lights.ssf");

        Assert.True(vm.Preprocess.CanRun);
        Assert.Null(vm.Preprocess.Blocker);
    }

    /// <summary>
    /// On a good folder there is nothing to run; on a missing one, no script helps.
    /// </summary>
    [Fact]
    public async Task TheScriptIsNotOfferedOnAGoodFolderOrOnAMissingOne()
    {
        using var good = new SessionFixture().WithFlatMaster();
        using var ws = new TempWorkspace();
        var vm = new MainViewModel(() => FakeInstall(ws, true, "OSC_Preprocessing_WithoutDark.ssf"), () => false);

        await vm.OpenAsync(good.Root);
        Assert.False(vm.Preprocess.IsOffered);
        Assert.False(vm.Preprocess.CanRun);

        await vm.OpenAsync(Path.Combine(Path.GetTempPath(), "nu-exista-" + Guid.NewGuid()));
        Assert.False(vm.Preprocess.IsOffered);
        Assert.Empty(vm.Preprocess.Scripts);
    }

    /// <summary>
    /// After the run, the same folder is opened again and goes through Check() again.
    /// </summary>
    [Fact]
    public async Task ReopeningAfterTheScriptPicksUpTheNewMaster()
    {
        using var f = new SessionFixture().WithProcess();
        using var ws = new TempWorkspace();
        WithInputs(f, "biases", "flats", "lights");
        var vm = new MainViewModel(() => FakeInstall(ws, true, "OSC_Preprocessing_WithoutDark.ssf"), () => false);

        await vm.OpenAsync(f.Root);
        Assert.True(vm.HasProblem);
        Assert.True(vm.Preprocess.IsOffered);

        f.WithFlatMaster();   // what the script would have produced
        await vm.ReopenAsync();

        Assert.False(vm.HasProblem);
        Assert.False(vm.Preprocess.IsOffered);
        Assert.True(vm.Structure.HasResult);
    }

    [Fact]
    public async Task AMissingFolderIsReportedAsSuch()
    {
        var vm = new MainViewModel();

        await vm.OpenAsync(Path.Combine(Path.GetTempPath(), "nu-exista-" + Guid.NewGuid()));

        Assert.True(vm.HasProblem);
        Assert.Equal(SessionMessages.Title(SessionProblem.NoFolder), vm.ProblemTitle);
    }

    /// <summary>On an invalid folder nothing is computed: the tabs stay empty.</summary>
    [Fact]
    public async Task NothingRunsOnAnInvalidFolder()
    {
        using var f = new SessionFixture().WithProcess();
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.False(vm.Structure.HasResult);
        Assert.False(vm.Light.HasResult);
        Assert.Empty(vm.Exposure.Files);
    }

    // ------------------------------------------------------------------ changing the folder

    /// <summary>
    /// Another folder means another start: the old results disappear, instead of staying
    /// beside the new ones where they could be mistaken for them.
    /// </summary>
    [Fact]
    public async Task OpeningAnotherFolderClearsTheOldResults()
    {
        using var good = new SessionFixture().WithFlatMaster().WithLightSeries(6);
        using var bad = new SessionFixture().WithProcess();
        var vm = new MainViewModel();

        await vm.OpenAsync(good.Root);
        Assert.True(vm.Structure.HasResult);
        vm.HistEq.Selected = vm.HistEq.Files[0];
        await vm.HistEq.RecomputeAsync();
        Assert.True(vm.HistEq.HasResult);

        await vm.OpenAsync(bad.Root);

        Assert.False(vm.Structure.HasResult);
        Assert.False(vm.Light.HasResult);
        Assert.Empty(vm.Light.Frames);
        Assert.False(vm.HistEq.HasResult);
        Assert.Empty(vm.HistEq.Files);
        Assert.Null(vm.HistEq.Image);
    }

    [Fact]
    public async Task TheFolderIsRememberedAndReported()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = new MainViewModel();

        Assert.False(vm.HasFolder);

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasFolder);
        Assert.Equal(f.Root, vm.Folder);
    }

    [Fact]
    public async Task AnEmptyFolderNameLeavesTheAppInItsStartingState()
    {
        var vm = new MainViewModel();

        await vm.OpenAsync("");

        Assert.False(vm.HasFolder);
        Assert.False(vm.HasProblem);
    }

    /// <summary>
    /// The cancellation at closing is idempotent - it is called also when nothing ran.
    /// </summary>
    [Fact]
    public void CancellingWithNothingRunningIsHarmless()
    {
        var vm = new MainViewModel();

        vm.CancelPending();
        vm.CancelPending();
    }

    // ------------------------------------------------------------------ banner

    /// <summary>
    /// The window shows the same banner as the console's first line. It comes from the same
    /// place, so a version bump moves it in both at once.
    /// </summary>
    [Fact]
    public void TheHeaderIsTheSameVersionAndCopyrightTheConsolePrints()
    {
        var vm = new MainViewModel();

        Assert.Equal(VersionInfo.Banner, vm.Banner);
        Assert.Equal(VersionInfo.Copyright, vm.Copyright);
    }

    // ------------------------------------------------ the switches remembered per folder (R-119)

    /// <summary>
    /// R-119: a folder whose flatcheck.json says so opens with the filter box unchecked - so
    /// the Structure run compares nothing, even with a raw and a catalog - and the sky box
    /// checked, before any run.
    /// </summary>
    [Fact]
    public async Task R119_TheSwitchesAreRestoredFromTheFolderWhenItOpens()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        File.WriteAllText(Path.Combine(f.Root, "flatcheck.json"), "{\"noFilters\":false,\"skyFlat\":true}");
        var vm = new MainViewModel(null, null, null, locateProfiles: () => sources,
            loadInfo: path => RawReader.IsRawPath(path) ? RawFrame() : ImageLoader.LoadInfo(path));

        await vm.OpenAsync(f.Root);

        Assert.True(vm.Structure.HasResult, vm.Structure.ErrorText);
        Assert.False(vm.Structure.NoFilters);
        Assert.False(vm.Structure.HasProfileRows);
        Assert.NotEmpty(vm.Structure.FilteredNote);
        Assert.True(vm.Exposure.Sky);
    }

    /// <summary>
    /// R-119: without a file both switches are on their defaults, nothing is written by
    /// opening the folder, and each change writes the file with both keys.
    /// </summary>
    [Fact]
    public async Task R119_NothingIsWrittenUntilASwitchChanges()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string json = Path.Combine(f.Root, "flatcheck.json");
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.Structure.NoFilters);
        Assert.False(vm.Exposure.Sky);
        Assert.False(File.Exists(json));

        vm.Exposure.Sky = true;
        Assert.True(File.Exists(json));
        Assert.Contains("\"skyFlat\":true", File.ReadAllText(json));
        Assert.Contains("\"noFilters\":true", File.ReadAllText(json));

        vm.Structure.NoFilters = false;
        await vm.Structure.Pending;
        Assert.Contains("\"noFilters\":false", File.ReadAllText(json));
        Assert.Contains("\"skyFlat\":true", File.ReadAllText(json));
    }

    /// <summary>
    /// R-119: the switches belong to the folder: the next folder, without a file, opens on the
    /// defaults, whatever the previous one had.
    /// </summary>
    [Fact]
    public async Task R119_TheNextFolderStartsFromItsOwnFile()
    {
        using var a = new SessionFixture().WithFlatMaster();
        using var b = new SessionFixture().WithFlatMaster();
        File.WriteAllText(Path.Combine(a.Root, "flatcheck.json"), "{\"noFilters\":false,\"skyFlat\":true}");
        var vm = new MainViewModel();

        await vm.OpenAsync(a.Root);
        Assert.False(vm.Structure.NoFilters);
        Assert.True(vm.Exposure.Sky);

        await vm.OpenAsync(b.Root);

        Assert.True(vm.Structure.NoFilters);
        Assert.False(vm.Exposure.Sky);
        Assert.False(File.Exists(Path.Combine(b.Root, "flatcheck.json")));
    }

    /// <summary>
    /// R-121: a folder whose flatcheck.json names the chosen lens opens with the comparison
    /// narrowed to it - one row, the picker on that lens - and writes nothing by opening.
    /// </summary>
    [Fact]
    public async Task R121_TheChosenLensIsRestoredFromTheFolderWhenItOpens()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF");
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, rangePair: true);
        string key = LensProfileTestData.KeyOf(sources, LensProfileTestData.Samyang135);
        LoadedFrame raw = LensProfileTestData.Range135Raw();
        string json = Path.Combine(f.Root, "flatcheck.json");
        File.WriteAllText(json, "{\"noFilters\":true,\"skyFlat\":false,\"lens\":\"" + key + "\"}");
        DateTime written = File.GetLastWriteTimeUtc(json);
        var vm = new MainViewModel(null, null, null, locateProfiles: () => sources,
            loadInfo: path => RawReader.IsRawPath(path) ? raw : ImageLoader.LoadInfo(path));

        await vm.OpenAsync(f.Root);

        Assert.True(vm.Structure.HasResult, vm.Structure.ErrorText);
        LensProfileRow row = Assert.Single(vm.Structure.ProfileRows);
        Assert.Equal(LensProfileTestData.Samyang135, row.Lens);
        Assert.True(vm.Structure.OffersLensChoice);
        Assert.Equal(key, vm.Structure.SelectedLens.Key);
        Assert.Equal(written, File.GetLastWriteTimeUtc(json));
    }

    // ------------------------------------------------------------ the opening panel (R-120)

    /// <summary>
    /// R-120: while the opening runs, the three rows say where it stands - the structure and
    /// the prefill running (both wait for the held catalog), the series waiting - and the
    /// window is running; once everything has ended every row is done, the series' row with
    /// the tab's own refusal (no lights in the fixture), and the window is not running any
    /// more. The tabs follow IsRunning in the XAML.
    /// </summary>
    [Fact]
    public async Task R120_TheRowsFollowTheRunsAndTheWindowRunsUntilThePrefillEnds()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF");
        using var ws = new TempWorkspace();
        using var hold = new ManualResetEventSlim(false);
        var sources = LensProfileTestData.Sources(ws);
        var vm = new MainViewModel(null, null, null,
            locateProfiles: () => { hold.Wait(); return sources; },
            loadInfo: path => RawReader.IsRawPath(path) ? RawFrame() : ImageLoader.LoadInfo(path));
        Assert.Equal(3, vm.OpeningSteps.Count);
        Assert.All(vm.OpeningSteps, s => Assert.True(s.IsWaiting));

        Task open = vm.OpenAsync(f.Root);   // stops at the catalog, which the test holds

        Assert.True(vm.IsRunning);
        Assert.True(vm.OpeningSteps[0].IsRunning);
        Assert.Equal("ruleaza...", vm.OpeningSteps[0].StatusWord);
        Assert.True(vm.OpeningSteps[1].IsWaiting);
        Assert.Equal("asteapta", vm.OpeningSteps[1].StatusWord);
        Assert.True(vm.OpeningSteps[2].IsRunning);
        Assert.Same(vm.Structure, vm.OpeningSteps[0].Tab);
        Assert.NotEmpty(vm.Structure.BusyMessage);

        hold.Set();
        await open;

        Assert.False(vm.IsRunning);
        Assert.All(vm.OpeningSteps, s => Assert.True(s.IsDone));
        Assert.Equal("gata", vm.OpeningSteps[0].StatusWord);
        Assert.Equal(vm.Light.ErrorTitle, vm.OpeningSteps[1].StatusWord);   // the tab's refusal, not a word
        Assert.Contains("end-to-end", vm.OpeningSteps[1].StatusWord);
        Assert.Equal("gata", vm.OpeningSteps[2].StatusWord);
        Assert.NotEmpty(vm.OpeningSteps[0].Label);
        Assert.NotEqual(vm.OpeningSteps[0].Label, vm.OpeningSteps[1].Label);
    }

    /// <summary>
    /// R-120: a folder opened over a running one keeps the panel until its own opening ends.
    /// The gate is held by the test, as a click's run of a previous folder would hold it: A
    /// waits for it, B cancels A and waits in its turn; A's opening ends under B's, and the
    /// window is still running - before the fix A's finally dropped the flag and the tabs
    /// showed while B had not even started.
    /// </summary>
    [Fact]
    public async Task R120_TheOlderOpeningEndingDoesNotDropTheNewerOnesPanel()
    {
        using var a = new SessionFixture().WithFlatMaster();
        using var b = new SessionFixture().WithFlatMaster();
        var gate = new HeavyRunGate();
        IDisposable other = await gate.EnterAsync(CancellationToken.None);
        var vm = new MainViewModel(null, null, gate: gate);

        Task openA = vm.OpenAsync(a.Root);   // waits for the gate
        Task openB = vm.OpenAsync(b.Root);   // cancels A's wait and waits in its turn
        Assert.True(vm.IsRunning);

        await openA;                          // A's opening ends under B's
        Assert.False(openB.IsCompleted);
        Assert.True(vm.IsRunning);            // the panel follows B, not A's ending
        Assert.False(vm.OpeningSteps[0].IsDone);
        Assert.False(vm.OpeningSteps[1].IsDone);

        // B's prefill needs no gate: its row turns done when its own task ends, while the chain
        // still waits (the watcher's continuation lands right after the task, so a short wait).
        await vm.Synthetic.Pending;
        for (int i = 0; i < 300 && !vm.OpeningSteps[2].IsDone; i++) await Task.Delay(10);
        Assert.True(vm.OpeningSteps[2].IsDone);
        Assert.True(vm.IsRunning);

        other.Dispose();
        await openB;
        Assert.False(vm.IsRunning);
        Assert.Equal(b.Root, vm.Folder);
        Assert.All(vm.OpeningSteps, s => Assert.True(s.IsDone));
    }

    /// <summary>R-120: an opening that stops at the problem card, over a running one, drops the
    /// panel at once: the card must not stay covered.</summary>
    [Fact]
    public async Task R120_AnOpeningThatStopsAtTheCardDropsThePanel()
    {
        using var a = new SessionFixture().WithFlatMaster();
        using var b = new SessionFixture().WithProcess();   // no master: the card
        var gate = new HeavyRunGate();
        IDisposable other = await gate.EnterAsync(CancellationToken.None);
        var vm = new MainViewModel(null, null, gate: gate);

        Task openA = vm.OpenAsync(a.Root);
        Assert.True(vm.IsRunning);
        await vm.OpenAsync(b.Root);

        Assert.True(vm.HasProblem);
        Assert.False(vm.IsRunning);

        other.Dispose();
        await openA;
        Assert.False(vm.IsRunning);           // A's ending changes nothing
        Assert.True(vm.HasProblem);
    }

    /// <summary>R-120: a folder with lights ends with every row done, in a word.</summary>
    [Fact]
    public async Task R120_WithASeriesEveryRowEndsDone()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(3);
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.False(vm.IsRunning);
        Assert.All(vm.OpeningSteps, s => Assert.Equal("gata", s.StatusWord));
        Assert.True(vm.Light.ProgressKnown);  // the series row's bar was determinate
    }

    /// <summary>
    /// R-117: the folder button is locked while the Smoothing or the Synthetic tab is busy, as
    /// it is during the opening chain: their main runs take longer than a folder change does
    /// (measured 2026-10-30 on a 24 MP master, 6.4-6.7 s and about 8 s), and their file writes
    /// have no business landing on another folder's tabs. The gate the test holds keeps each
    /// run busy.
    /// </summary>
    [Fact]
    public async Task R117_TheFolderButtonIsLockedDuringASmoothingOrSyntheticRun()
    {
        using var f = new SessionFixture().WithFlatMaster();
        using var ws = new TempWorkspace();
        var gate = new HeavyRunGate();
        var vm = new MainViewModel(null, null, _ => true,
            locateProfiles: () => LensProfileTestData.Sources(ws), gate: gate);
        var seen = new List<bool>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.CanChooseFolder)) seen.Add(vm.CanChooseFolder); };

        await vm.OpenAsync(f.Root);
        await vm.Synthetic.Pending;
        Assert.True(vm.CanChooseFolder);

        IDisposable held = await gate.EnterAsync(CancellationToken.None);
        Task smooth = vm.Smooth.RunAsync();
        Assert.False(vm.CanChooseFolder);
        held.Dispose();
        await smooth;
        Assert.True(vm.CanChooseFolder);

        held = await gate.EnterAsync(CancellationToken.None);
        Task synthetic = vm.Synthetic.RunAsync();
        Assert.False(vm.CanChooseFolder);
        held.Dispose();
        await synthetic;
        Assert.True(vm.CanChooseFolder);

        // the binding hears every change: false at each run, true at its end
        Assert.Equal(new[] { false, true, false, true }, seen.Skip(seen.Count - 4));
    }
}

/// <summary>The command-line argument: a folder, or nothing.</summary>
public class AppStartupTests
{
    [Fact]
    public void AnExistingFolderInTheArgumentsIsUsed()
    {
        using var f = new SessionFixture();

        Assert.Equal(f.Root, App.FolderFromArgs(new[] { f.Root }));
    }

    /// <summary>
    /// Anything else is ignored. A window that refuses to start because of a wrong argument is
    /// more annoying than one that asks which folder you want.
    /// </summary>
    [Fact]
    public void AnythingElseIsIgnoredRatherThanFatal()
    {
        Assert.Null(App.FolderFromArgs(null));
        Assert.Null(App.FolderFromArgs(Array.Empty<string>()));
        Assert.Null(App.FolderFromArgs(new[] { "", "  ", "--ceva", @"C:\nu\exista\aici" }));
    }

    [Fact]
    public void TheFirstExistingFolderWins()
    {
        using var f = new SessionFixture();

        Assert.Equal(f.Root, App.FolderFromArgs(new[] { "--flag", f.Root }));
    }
}
