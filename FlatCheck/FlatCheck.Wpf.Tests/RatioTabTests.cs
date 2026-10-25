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
namespace FlatCheck.Wpf.Tests;

/// <summary>
/// The attribution tab, by 180-degree rotation.
///
/// The tab NO longer searches the folder, and that is why there are no list tests here any
/// more. The mode needs two MASTERS, one for each orientation, and the two orientations are
/// two sittings, so two session folders. What a single folder could offer were the
/// individual frames, from which a confident and false result comes out - a frame's noise
/// is of the same order as the signal sought. So two files are asked for explicitly, and
/// what is checked here is the validation of the choice and the resulting attribution.
/// </summary>
public class RatioTabTests
{
    static RatioTabViewModel Attached(SessionFixture f)
    {
        var vm = new RatioTabViewModel();
        vm.Attach(f.Session, CancellationToken.None);
        return vm;
    }

    // ------------------------------------------------------------------ start

    /// <summary>
    /// The dialog for set A starts in the folder of the open session's master - the stacks
    /// are there, wherever the script put them - and in process/ when there is no master.
    /// </summary>
    [Fact]
    public void TheDialogStartsWhereTheSessionKeepsItsMaster()
    {
        using var inMasters = new SessionFixture().WithFlatMasterInMasters().WithProcess();
        using var inProcess = new SessionFixture().WithFlatMaster();
        using var none = new SessionFixture().WithProcess();

        Assert.Equal(inMasters.MastersDir, Attached(inMasters).InitialDirectory);
        Assert.Equal(inProcess.ProcessDir, Attached(inProcess).InitialDirectory);
        Assert.Equal(none.ProcessDir, Attached(none).InitialDirectory);
    }

    /// <summary>
    /// The tab starts empty: it offers nothing, because a session folder cannot know which
    /// the two orientations are. Instead it explains what it needs, before anything else.
    /// </summary>
    [Fact]
    public void TheTabStartsEmptyAndExplainsWhatItNeeds()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");

        var vm = Attached(f);

        Assert.Null(vm.FileA);
        Assert.Null(vm.FileB);
        Assert.False(vm.HasResult);

        Assert.Contains("DOUA MASTERE", vm.Documentation);
        Assert.Contains("DOUA SEDINTE", vm.Documentation);
        Assert.Contains("180", vm.Documentation);
    }

    /// <summary>
    /// The documentation must also say what the test CANNOT tell: that the camera-fixed part
    /// can be optical (harmless) or something on the lens (not). Without that, a good verdict
    /// on a contaminated master reads as a guarantee.
    /// </summary>
    [Fact]
    public void TheDocumentationStatesWhatTheTestCannotTell()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit");
        var vm = Attached(f);

        Assert.Contains("CE NU ITI SPUNE", vm.Documentation);
        Assert.Contains("obiectiv", vm.Documentation);
    }

    // ------------------------------------------------------------------ validation

    /// <summary>
    /// A file that does not start with "pp_flat" is not refused curtly - it explains what
    /// these files are in Siril's output and why the test needs one, including why an
    /// UNCALIBRATED flat does not do.
    /// </summary>
    [Fact]
    public void ChoosingSomethingElseExplainsWhatAFlatMasterIs()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);

        vm.Choose(isA: true, path: @"C:\undeva\poza.jpg");

        Assert.True(vm.HasError);
        Assert.Contains("pp_flat", vm.ErrorText);
        Assert.Contains("process/", vm.ErrorText);
        Assert.Contains("preprocesat", vm.ErrorText);
        Assert.Contains("180", vm.ErrorText);
    }

    /// <summary>
    /// The individual calibrated frame is the tab's real trap: it sits in the same process/,
    /// has the same prefix and looks the same as the master. Until the pattern was fixed the
    /// dialog even offered it, and the pair came out with a confident and false verdict. The
    /// refusal says why explicitly, otherwise it looks like a whim.
    /// </summary>
    [Fact]
    public void ChoosingAnIndividualCalibratedFlatIsRefusedAndExplained()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);

        vm.Choose(isA: true, path: f.MasterPath("pp_flat_00001.fit"));

        Assert.True(vm.HasError);
        Assert.Null(vm.FileA);
        Assert.Contains("pp_flat_00001.fit", vm.ErrorText);
        Assert.Contains("zgomotul unui singur cadru", vm.ErrorText);
    }

    [Fact]
    public void ARejectedChoiceDoesNotReplaceTheCurrentSelection()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        string before = vm.FileA;

        vm.Choose(isA: true, path: @"C:\undeva\flat_necalibrat.fit");

        Assert.Equal(before, vm.FileA);
    }

    /// <summary>
    /// The NORMAL case, not the exception: the two masters come from two different session
    /// folders, one for each orientation.
    /// </summary>
    [Fact]
    public void AFlatMasterFromAnotherSessionIsAccepted()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit");
        var vm = Attached(f);

        vm.Choose(isA: false, path: @"C:\alta-sesiune\process\pp_flat_stacked.fit");

        Assert.False(vm.HasError);
        Assert.Equal(@"C:\alta-sesiune\process\pp_flat_stacked.fit", vm.FileB);
    }

    /// <summary>
    /// A session's overall master is exactly what the mode needs, not something to refuse:
    /// in the normal flow, both files are "pp_flat_stacked.fit", from two folders.
    /// </summary>
    [Fact]
    public void TheOverallMasterOfAnotherSessionIsAccepted()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit");
        var vm = Attached(f);

        vm.Choose(isA: true, path: @"C:\sesiune-0deg\process\pp_flat_stacked.fit");
        vm.Choose(isA: false, path: @"C:\sesiune-180deg\process\pp_flat_stacked.fit");

        Assert.False(vm.HasError);
        Assert.EndsWith("pp_flat_stacked.fit", vm.FileA);
        Assert.EndsWith("pp_flat_stacked.fit", vm.FileB);
    }

    /// <summary>
    /// The same file in both slots gives an external component of exactly zero - which does
    /// not mean "clean setup", it means "nothing was measured". Caught at the CHOICE, so that
    /// the good selection from before is not lost.
    /// </summary>
    [Fact]
    public void TheSameFileInBothSlotsIsRefusedAtChoiceTime()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));

        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_A.fit"));

        Assert.True(vm.HasError);
        Assert.Contains("DIFERITE", vm.ErrorText);
        Assert.Null(vm.FileB);
    }

    // ------------------------------------------------------------------ attribution

    [Fact]
    public async Task ItAttributesTheStructureOfTheCombinedMaster()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
        Assert.NotEmpty(vm.Verdict);
        Assert.Equal(2, vm.WorldRows.Count);    // the world-fixed component, with its error bar
        Assert.Equal(5, vm.Tests.Count);    // the structure battery on (A+B)/2
        Assert.All(vm.Tests, t => Assert.NotEmpty(t.Value));
        Assert.All(vm.Tests, t => Assert.True(t.HasVerdict));
        Assert.NotEmpty(vm.ProvenanceA);    // how many frames and from when
        Assert.NotNull(vm.Map);
    }

    /// <summary>
    /// Every row says what it measures, as in tab 1. Here all five were missing, because the
    /// rows were Grid blocks written by hand in XAML, with no room for a description.
    /// </summary>
    [Fact]
    public async Task EveryTestRowCarriesItsOwnDescription()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        Assert.All(vm.Tests, t => Assert.True(t.HasNote, "rand fara descriere: " + t.Label));
    }

    /// <summary>
    /// The two rows of the world-fixed component. The first has the figure with its error
    /// bar and the informative label as its pill; the second, only the figure. Both with the
    /// core's description - the second had no word at all, the first did not say what the
    /// pill is.
    /// </summary>
    [Fact]
    public async Task TheWorldRowsCarryTheirDescriptionsAndOnlyTheFirstHasAChip()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        Assert.Contains("gradient extern", vm.WorldRows[0].Label);
        Assert.Contains("+/-", vm.WorldRows[0].Value);
        Assert.True(vm.WorldRows[0].HasVerdict);
        Assert.Equal(RatioReport.WorldNote(), vm.WorldRows[0].Note);

        Assert.Contains("colturi", vm.WorldRows[1].Label);
        Assert.False(vm.WorldRows[1].HasVerdict);
        Assert.Equal(RatioReport.WorldCornerNote(), vm.WorldRows[1].Note);
    }

    /// <summary>
    /// The rule for the whole screen, not for one card: every row with a figure on it also
    /// has a line of text under it. This is the test that would have caught from the start
    /// that two rows in Ratio had nothing.
    /// </summary>
    [Fact]
    public async Task EveryRowOnTheScreenCarriesADescription()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        var all = vm.WorldRows.Concat(vm.Tests).Concat(vm.Informative).ToList();
        Assert.Equal(2 + 5 + 3, all.Count);
        Assert.All(all, r => Assert.True(r.HasNote, "rand fara descriere: " + r.Label));
        Assert.All(all, r => Assert.True(r.Note!.Length > 30, "descriere prea scurta: " + r.Label));
    }

    /// <summary>
    /// The informative quantities too, as in the console: the --ratio report prints all of
    /// WriteBody, including the combined master's vignetting, illumination centre and local
    /// spots. Without a verdict, because they are not tests; and without the ADU levels line,
    /// which the console does not print in --ratio either, because the combined master is
    /// normalised to 1.
    /// </summary>
    [Fact]
    public async Task TheInformativeRowsAreShownWithoutVerdict()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        Assert.Equal(3, vm.Informative.Count);
        Assert.All(vm.Informative, r => Assert.False(r.HasVerdict));
        Assert.All(vm.Informative, r => Assert.True(r.HasNote, "rand fara descriere: " + r.Label));
        Assert.Contains(vm.Informative, r => r.Label.Contains("vignetare"));
        Assert.Contains(vm.Informative, r => r.Label.Contains("offset"));
        Assert.Contains(vm.Informative, r => r.Label.Contains("pete locale"));
        Assert.DoesNotContain(vm.Informative, r => r.Label.Contains("ADU"));
        Assert.Empty(vm.LocalSpotWarning);       // a clean radial field: nothing to warn about
    }

    /// <summary>
    /// The two tabs show the SAME battery - Ratio runs it on the combined master, but the
    /// analyzer is the same - so the labels, the order and the descriptions must be
    /// identical. They were two separate compositions, and they diverged: the fifth test had
    /// to be added twice, and the descriptions reached only one tab.
    /// </summary>
    [Fact]
    public async Task TheRowsAreTheSameOnesTheStructureTabShows()
    {
        using var rf = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var ratio = Attached(rf);
        ratio.Choose(isA: true, path: rf.MasterPath("pp_flat_stacked_A.fit"));
        ratio.Choose(isA: false, path: rf.MasterPath("pp_flat_stacked_B.fit"));
        await ratio.RecomputeAsync();

        using var sf = new SessionFixture().WithFlatMaster();
        var structure = new StructureTabViewModel();
        await structure.RunAsync(sf.Session, CancellationToken.None);

        Assert.Equal(structure.Tests.Select(t => t.Label), ratio.Tests.Select(t => t.Label));
        Assert.Equal(structure.Tests.Select(t => t.Note), ratio.Tests.Select(t => t.Note));
        Assert.Equal(structure.Tests.Select(t => t.IsKey), ratio.Tests.Select(t => t.IsKey));
        Assert.Equal(structure.Informative.Select(t => t.Label), ratio.Informative.Select(t => t.Label));
    }

    /// <summary>
    /// The tab's verdict is the combined master's STRUCTURE verdict, on the same scale as
    /// tab 1 - so the advice too must be the same text, not a rewritten one.
    /// </summary>
    [Fact]
    public async Task TheAdviceComesFromTheSharedResidualScale()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();

        Assert.Equal(StructureReport.Advice(vm.Verdict), vm.Advice);
    }

    /// <summary>
    /// Two maps, because the mode splits the master into two components. The toggle changes
    /// what is seen, and the two really are different.
    /// </summary>
    [Fact]
    public async Task BothMapsAreAvailableAndTheToggleSwitchesThem()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        await vm.RecomputeAsync();
        byte[] sensor = vm.Map;

        vm.ShowWorldMap = true;

        Assert.NotNull(vm.Map);
        Assert.NotEqual(sensor, vm.Map);
    }

    /// <summary>Choosing the second file triggers the computation by itself.</summary>
    [Fact]
    public async Task ChoosingTheSecondFileComputes()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);

        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        Assert.False(vm.HasResult);

        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));
        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
    }

    /// <summary>
    /// A new choice during the computation neither waits nor competes: it supersedes it. The
    /// pair chosen last stays on the screen, whichever of the two runs ends first. Before,
    /// the two continuations wrote into the same cache from two threads and the test above
    /// failed now and then.
    /// </summary>
    [Fact]
    public async Task ASecondPickSupersedesTheFirst()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit", "pp_flat_stacked_C.fit");
        var vm = Attached(f);

        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));   // starts A+B
        Task first = vm.Pending;
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_C.fit"));    // over it: C+B
        Task second = vm.Pending;
        await Task.WhenAll(first, second);

        Assert.True(vm.HasResult);
        Assert.False(vm.IsBusy);
        Assert.False(vm.HasError);
        Assert.Contains("pp_flat_stacked_C.fit", vm.Report);
        Assert.DoesNotContain("pp_flat_stacked_A.fit", vm.Report);
        Assert.NotNull(vm.Map);
    }

    /// <summary>
    /// Both masters picked while another heavy run holds the gate (the opening chain, say): the
    /// comparison waits for it, as the smoothing does, instead of loading two more 24 MP masters
    /// beside it (the "sequential, not parallel" rule of MainViewModel, ADR 0061). Released, it
    /// computes as usual and lets the gate go.
    /// </summary>
    [Fact]
    public async Task R082_APickDuringTheOpeningChainWaitsForIt()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var gate = new HeavyRunGate();
        IDisposable chain = await gate.EnterAsync(CancellationToken.None);
        var vm = new RatioTabViewModel();
        vm.Attach(f.Session, CancellationToken.None, gate);

        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));
        Task run = vm.Pending;

        Assert.NotSame(run, await Task.WhenAny(run, Task.Delay(1000)));
        Assert.False(vm.HasResult);

        chain.Dispose();
        await run;

        Assert.True(vm.HasResult);
        Assert.Contains("pp_flat_stacked_B.fit", vm.Report);
        Assert.False(gate.IsHeld);
    }

    /// <summary>
    /// A pair superseded while it still waits for the gate leaves the queue without computing:
    /// only the latest pair runs when the gate opens, and the screen shows it alone.
    /// </summary>
    [Fact]
    public async Task R082_APickSupersededWhileWaitingLeavesTheQueue()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit", "pp_flat_stacked_C.fit");
        var gate = new HeavyRunGate();
        IDisposable chain = await gate.EnterAsync(CancellationToken.None);
        var vm = new RatioTabViewModel();
        vm.Attach(f.Session, CancellationToken.None, gate);

        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));   // waits: A+B
        Task first = vm.Pending;
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_C.fit"));    // over it: C+B
        Task second = vm.Pending;
        await first;                                                            // left the queue
        Assert.False(second.IsCompleted);

        chain.Dispose();
        await second;

        Assert.True(vm.HasResult);
        Assert.Contains("pp_flat_stacked_C.fit", vm.Report);
        Assert.DoesNotContain("pp_flat_stacked_A.fit", vm.Report);
        Assert.False(gate.IsHeld);
    }

    /// <summary>
    /// While the pair waits for the opening chain (up to two minutes on a long series) the
    /// waiting screen says that, not that it compares: nothing is compared yet. Once the chain
    /// ends, the comparison's own message takes over. With no chain running, the comparison's
    /// message is the first one shown.
    /// </summary>
    [Fact]
    public async Task R082_WhileAPickWaitsForTheChainTheWaitingScreenSaysSo()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var gate = new HeavyRunGate();
        IDisposable chain = await gate.EnterAsync(CancellationToken.None);
        var vm = new RatioTabViewModel();
        vm.Attach(f.Session, CancellationToken.None, gate);
        var messages = new List<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.BusyMessage)) messages.Add(vm.BusyMessage); };

        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));

        Assert.True(vm.IsBusy);
        Assert.Equal(SessionMessages.WaitingForOpeningChain(), vm.BusyMessage);

        chain.Dispose();
        await vm.Pending;

        Assert.Equal(SessionMessages.RatioComparing(), messages.Last());
        Assert.True(vm.HasResult);

        var idle = Attached(f);
        idle.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        idle.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));
        Assert.Equal(SessionMessages.RatioComparing(), idle.BusyMessage);
        await idle.Pending;
    }

    [Fact]
    public async Task WithoutTwoFilesNothingIsComputed()
    {
        using var f = new SessionFixture().WithFlatMasters("pp_flat_stacked_A.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));

        await vm.RecomputeAsync();

        Assert.False(vm.HasResult);
    }

    [Fact]
    public void ResetClearsBothChoices()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));

        vm.Reset();

        Assert.Null(vm.FileA);
        Assert.Null(vm.FileB);
        Assert.False(vm.ShowWorldMap);
        Assert.Null(vm.Map);
        Assert.Empty(vm.MapLegend);
    }

    // -------------------------------------------------------- the map scale

    static async Task<RatioTabViewModel> Computed(SessionFixture f)
    {
        var vm = Attached(f);
        vm.Choose(isA: true, path: f.MasterPath("pp_flat_stacked_A.fit"));
        vm.Choose(isA: false, path: f.MasterPath("pp_flat_stacked_B.fit"));
        await vm.RecomputeAsync();
        return vm;
    }

    static MapScaleOption Cap(RatioTabViewModel vm, double? capPct)
        => vm.MapScale.Options.Single(o => o.Label == RunReport.MapScaleLabel(capPct));

    /// <summary>
    /// The mode has two maps, and the scale applies to the one shown - whichever of the two
    /// it is.
    /// </summary>
    [Fact]
    public async Task TheScaleAppliesToBothMaps()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = await Computed(f);

        byte[] sensorAtTen = vm.Map;
        vm.ShowWorldMap = true;
        byte[] worldAtTen = vm.Map;

        Assert.NotEqual(sensorAtTen, worldAtTen);

        Cap(vm, Th.ResidPerfect).IsSelected = true;
        byte[] worldAtTwo = vm.Map;
        vm.ShowWorldMap = false;
        byte[] sensorAtTwo = vm.Map;

        Assert.NotEqual(worldAtTen, worldAtTwo);
        Assert.NotEqual(sensorAtTen, sensorAtTwo);
    }

    /// <summary>
    /// On the automatic scale the two components have different ranges, so the legend must
    /// follow the map shown - otherwise it would describe an image other than the one on
    /// the screen.
    /// </summary>
    [Fact]
    public async Task TheLegendFollowsTheDisplayedMap()
    {
        using var f = new SessionFixture()
            .WithFlatMasters("pp_flat_stacked_A.fit", "pp_flat_stacked_B.fit");
        var vm = await Computed(f);
        Cap(vm, null).IsSelected = true;

        string sensor = vm.MapLegend;
        vm.ShowWorldMap = true;

        Assert.NotEmpty(sensor);
        Assert.NotEqual(sensor, vm.MapLegend);
    }

    /// <summary>The same guide as on the structure tab, and also collapsed by default.</summary>
    [Fact]
    public void TheGuideIsThereBeforeAnythingIsChosen()
    {
        var vm = new RatioTabViewModel();

        Assert.NotEmpty(vm.MapGuide.Intro);
        Assert.NotEmpty(vm.MapGuide.Shapes);
        Assert.False(vm.MapGuide.IsExpanded);
    }
}
