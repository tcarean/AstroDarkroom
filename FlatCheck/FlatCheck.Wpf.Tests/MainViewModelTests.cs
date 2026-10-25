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
/// Deschiderea unui folder, cap-coada. Aici se verifica cerintele 3 si 4: ce porneste
/// singur, si ce se intampla cand folderul nu e bun.
/// </summary>
public class MainViewModelTests
{
    /// <summary>CERINTA 3: structura si testul end-to-end pornesc singure.</summary>
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
    /// Tabul Ratio nu calculeaza la deschidere, oricate mastere ar fi in folder, si asta
    /// e o decizie: modul cere doua stive din DOUA orientari, adica din doua sedinte,
    /// deci din doua foldere. Ce poate propune un singur folder sunt cadre individuale,
    /// din care iese un rezultat sigur si fals.
    /// </summary>
    [Fact]
    public async Task RatioWaitsForTwoExplicitChoices()
    {
        using var f = new SessionFixture()
            .WithFlatMaster().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.False(vm.Ratio.HasResult);
        Assert.Null(vm.Ratio.FileA);
        Assert.Null(vm.Ratio.FileB);
    }

    /// <summary>
    /// The chain really holds the gate the Ratio tab takes, through OpenAsync: held open at the
    /// profile catalog (the Structure run awaits it before the master), a pair picked meanwhile
    /// does not finish until the chain does. R082_APickDuringTheOpeningChainWaitsForIt holds the
    /// gate itself; this one checks the wiring.
    /// </summary>
    [Fact]
    public async Task R082_OpeningPassesTheGateToTheRatioTabSoAPickWaitsForTheChain()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF")
                                          .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        using var ws = new TempWorkspace();
        using var hold = new ManualResetEventSlim(false);
        var sources = LensProfileTestData.Sources(ws);
        var vm = new MainViewModel(null, null, null,
            locateProfiles: () => { hold.Wait(); return sources; },
            loadInfo: path => RawReader.IsRawPath(path) ? RawFrame() : ImageLoader.LoadInfo(path));

        Task open = vm.OpenAsync(f.Root);   // stops at the catalog, which the test holds
        vm.Ratio.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Ratio.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));
        Task pick = vm.Ratio.Pending;

        Assert.NotSame(pick, await Task.WhenAny(pick, Task.Delay(1000)));
        Assert.True(vm.IsRunning);
        Assert.False(vm.Ratio.HasResult);

        hold.Set();
        await open;
        await pick;

        Assert.True(vm.Structure.HasResult);
        Assert.True(vm.Ratio.HasResult);
        Assert.Contains("pp_flat_stacked_B.fit", vm.Ratio.Report);
    }

    /// <summary>
    /// The opening chain holds the gate from the structure run to the end of the series, and
    /// every tab a click runs takes the same gate: a Histogram pick, an Expo pick and a Ratio
    /// pair made while the chain is held open at the profile catalog all wait, and all finish
    /// once the chain does (ADR 0061).
    /// </summary>
    [Fact]
    public async Task R081_TheOpeningChainHoldsTheGateEveryClickRunTakes()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF")
                                          .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
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
        Task expo = vm.Exposure.RecomputeAsync();
        vm.Ratio.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Ratio.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));
        Task ratio = vm.Ratio.Pending;

        Task any = Task.WhenAny(histEq, expo, ratio);
        Assert.NotSame(any, await Task.WhenAny(any, Task.Delay(1000)));
        Assert.Equal(SessionMessages.WaitingForOpeningChain(), vm.HistEq.BusyMessage);
        Assert.Equal(SessionMessages.WaitingForOpeningChain(), vm.Exposure.BusyMessage);
        Assert.Equal(SessionMessages.WaitingForOpeningChain(), vm.Ratio.BusyMessage);

        hold.Set();
        await open;
        await Task.WhenAll(histEq, expo, ratio);

        Assert.True(vm.Structure.HasResult);
        Assert.True(vm.HistEq.HasResult);
        Assert.True(vm.Exposure.HasResult);
        Assert.True(vm.Ratio.HasResult);
    }

    /// <summary>
    /// The chain itself waits for the gate: a folder opened while another run still holds it (a
    /// Ratio run of the previous folder, which does not stop mid-computation) starts its
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
        NikonLensId = 139,
        LensMinFocalMm = 11,
        LensMaxFocalMm = 20,
        LensMaxAperture = 2.8,
    };

    /// <summary>Tabul de expunere isi ia lista, dar nu calculeaza pana nu se alege ceva.</summary>
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
    /// Tabul de histograma primeste lista - masterul intai - si nu calculeaza nimic la
    /// deschidere: masterul de 24 MP il citeste deja Structura atunci.
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

    /// <summary>Cazul A prin fereastra: un folder cu netezitul deja scris il listeaza pe loc, al doilea.</summary>
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
    /// Cazul B, cap-coada: butonul de netezire scrie fisierul si randul apare in lista
    /// tabului Histogram, fara sa porneasca nimic acolo - "nimic la deschidere" e si
    /// "nimic la o scriere".
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

    /// <summary>Alt folder, fara netezit: randul NETEZIT nu supravietuieste schimbarii.</summary>
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
    /// Sesiunea Smida din arhiva: masterul in masters/, cum il lasa OSC_Preprocessing.ssf
    /// din Siril 1.4, lights calibrate in process/. Se deschide si ruleaza amandoua.
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

    /// <summary>Fara process/ dar cu masterul in masters/: structura ruleaza, seria lipseste cu explicatie.</summary>
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

    // ------------------------------------------------------------------ folder invalid

    /// <summary>
    /// CERINTA 4: fara masterul de flat, aplicatia nu porneste pe folderul asta si spune
    /// exact ce sa faca - sa ruleze scriptul de preprocesare in Siril. Un mesaj care ar
    /// descrie doar fisierul lipsa l-ar lasa pe om sa caute singur legatura.
    ///
    /// Scriptul numit e WithoutDark, scriptul autorului; cel standard din Siril 1.4 merge
    /// la fel de bine de cand masterul se cauta si in masters/.
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

    // ------------------------------------------------------------------ rularea scriptului

    /// <summary>
    /// O instalare Siril falsa: "siril-cli" e un fisier oarecare (doar existenta conteaza
    /// aici), scripturile sunt fisiere .ssf scrise in test. Folderele de intrare cerute de
    /// WithoutDark se pun optional.
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

    /// <summary>Pe un folder fara master, cu Siril instalat si cu intrarile la loc, se poate apasa.</summary>
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
    /// Un subfolder de intrare lipsa sau gol blocheaza butonul si se numeste - altfel
    /// Siril s-ar opri la linia trei cu un "cd" esuat, si omul ar citi log-ul ca sa afle.
    /// </summary>
    [Fact]
    public async Task AMissingInputFolderBlocksTheButtonAndIsNamed()
    {
        using var f = new SessionFixture().WithProcess();
        using var ws = new TempWorkspace();
        WithInputs(f, "biases", "flats");
        Directory.CreateDirectory(Path.Combine(f.Root, "lights"));   // exista, dar gol
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

    /// <summary>Siril deschis e un avertisment, nu un blocaj: nu se stie pe ce folder lucreaza.</summary>
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

        // se reverifica la apasare: Siril s-a inchis intre timp
        running = false;
        vm.Preprocess.Refresh();
        Assert.Null(vm.Preprocess.Warning);
    }

    /// <summary>Alt script, alte cerinte: schimbarea alegerii reface blocajul.</summary>
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

    /// <summary>Pe un folder bun nu e nimic de rulat; pe unul inexistent, niciun script nu ajuta.</summary>
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

    /// <summary>Dupa rulare, acelasi folder se redeschide si trece prin Check() din nou.</summary>
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

        f.WithFlatMaster();   // ce ar fi produs scriptul
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

    /// <summary>Pe un folder invalid nu se calculeaza nimic: taburile raman goale.</summary>
    [Fact]
    public async Task NothingRunsOnAnInvalidFolder()
    {
        using var f = new SessionFixture().WithProcess();
        var vm = new MainViewModel();

        await vm.OpenAsync(f.Root);

        Assert.False(vm.Structure.HasResult);
        Assert.False(vm.Light.HasResult);
        Assert.False(vm.Ratio.HasResult);
        Assert.Empty(vm.Exposure.Files);
    }

    // ------------------------------------------------------------------ schimbarea folderului

    /// <summary>
    /// Alt folder inseamna alt inceput: rezultatele vechi dispar, in loc sa stea langa
    /// cele noi si sa se poata confunda.
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

    /// <summary>Anularea de la inchidere e idempotenta - se cheama si cand n-a rulat nimic.</summary>
    [Fact]
    public void CancellingWithNothingRunningIsHarmless()
    {
        var vm = new MainViewModel();

        vm.CancelPending();
        vm.CancelPending();
    }

    // ------------------------------------------------------------------ antet

    /// <summary>
    /// Fereastra arata acelasi banner ca prima linie a consolei. Vine din acelasi loc,
    /// deci un bump de versiune il muta in amandoua deodata.
    /// </summary>
    [Fact]
    public void TheHeaderIsTheSameVersionAndCopyrightTheConsolePrints()
    {
        var vm = new MainViewModel();

        Assert.Equal(VersionInfo.Banner, vm.Banner);
        Assert.Equal(VersionInfo.Copyright, vm.Copyright);
    }
}

/// <summary>Argumentul de linie de comanda: un folder, sau nimic.</summary>
public class AppStartupTests
{
    [Fact]
    public void AnExistingFolderInTheArgumentsIsUsed()
    {
        using var f = new SessionFixture();

        Assert.Equal(f.Root, App.FolderFromArgs(new[] { f.Root }));
    }

    /// <summary>
    /// Orice altceva se ignora. O fereastra care refuza sa porneasca din cauza unui
    /// argument gresit e mai enervanta decat una care intreaba ce folder vrei.
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
