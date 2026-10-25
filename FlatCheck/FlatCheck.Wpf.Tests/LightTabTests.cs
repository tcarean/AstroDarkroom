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
/// The end-to-end tab. The tests that matter here are not about figures, but about the
/// three ways it can end - verdict, NEDETERMINAT, INAPLICABIL - and above all about the
/// fact that the last one is not an error.
/// </summary>
public class LightTabTests
{
    static async Task<LightTabViewModel> Run(SessionFixture f)
    {
        var vm = new LightTabViewModel();
        await vm.RunAsync(f.Session, CancellationToken.None);
        return vm;
    }

    [Fact]
    public async Task ItRunsOverTheWholeSeriesInProcess()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(6);

        var vm = await Run(f);

        Assert.True(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.Equal(6, vm.Frames.Count);
        Assert.NotEmpty(vm.Verdict);
        Assert.NotEmpty(vm.MeanRadial);
    }

    /// <summary>
    /// The table fills as the frames are ready, in order. On 32 frames the run takes two
    /// minutes, and a table that appeared all at once at the end would look stuck.
    /// </summary>
    [Fact]
    public async Task TheTableFillsUpInFileOrder()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(5);

        var vm = await Run(f);

        Assert.Equal(
            new[] { "pp_light_00000.fit", "pp_light_00001.fit", "pp_light_00002.fit",
                    "pp_light_00003.fit", "pp_light_00004.fit" },
            vm.Frames.Select(r => r.Name).ToArray());
    }

    /// <summary>Every row carries the explicit sign: the sign IS the diagnosis.</summary>
    [Fact]
    public async Task EveryRowShowsTheRadialComponentWithAnExplicitSign()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(5);

        var vm = await Run(f);

        Assert.All(vm.Frames, r =>
            Assert.True(r.Radial.StartsWith('+') || r.Radial.StartsWith('-'),
                        "randul nu poarta semn: " + r.Radial));
    }

    /// <summary>
    /// The progress bar is determinate on a series: the number of frames is known.
    /// </summary>
    [Fact]
    public async Task ProgressIsKnownAndReachesTheEnd()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(5);

        var vm = await Run(f);

        Assert.True(vm.ProgressKnown);
        Assert.Equal(1.0, vm.Progress, 6);
    }

    // ------------------------------------------------------------------ the refusal

    /// <summary>
    /// The implicit REQUIREMENT of the guard since 1.2.0: on a field too wide the test refuses.
    /// The refusal is NOT an error of the application - it is shown as a result, with an
    /// explanation, and the tab stays usable. Were it to appear as an error, the user would
    /// think the tool is broken exactly when it has just spared them a false conclusion.
    /// </summary>
    [Fact]
    public async Task ATooWideFieldIsRefusedAsAResultNotAsAnError()
    {
        using var f = new SessionFixture().WithFlatMaster()
                                          .WithLightSeries(6, SessionFixture.WideFieldCards);

        var vm = await Run(f);

        Assert.True(vm.HasResult);          // it answered
        Assert.False(vm.HasError);          // but it did not fail
        Assert.True(vm.Refused);
        Assert.Equal(Stats.NotApplicable, vm.Verdict);
    }

    /// <summary>When it refuses, no figure and no map is produced.</summary>
    [Fact]
    public async Task ARefusalProducesNoNumbersAtAll()
    {
        using var f = new SessionFixture().WithFlatMaster()
                                          .WithLightSeries(6, SessionFixture.WideFieldCards);

        var vm = await Run(f);

        Assert.False(vm.HasNumbers);
        Assert.Empty(vm.MeanRadial);
        Assert.Empty(vm.Spread);
        Assert.Empty(vm.Sem);
        Assert.Null(vm.Map);
    }

    /// <summary>The refusal explains the geometric degeneracy and points to the test that
    /// answers.</summary>
    [Fact]
    public async Task ARefusalExplainsWhyAndPointsAtTheRotationTest()
    {
        using var f = new SessionFixture().WithFlatMaster()
                                          .WithLightSeries(6, SessionFixture.WideFieldCards);

        var vm = await Run(f);

        Assert.Contains("degenerare geometrica", vm.Reasons);
        Assert.Contains("Ratio", vm.Reasons);
        Assert.NotEmpty(vm.Field);
    }

    /// <summary>
    /// A series whose header gives the focal length and no pixel size - a DSLR through Siril -
    /// is judged on the full-frame assumption, and the refusal says so, as the console does; the
    /// console's --fov way out is left off, the window has no control for it.
    /// </summary>
    [Fact]
    public async Task R083_ARefusalJudgedOnTheFocalLengthAloneSaysSo()
    {
        using var f = new SessionFixture().WithFlatMaster()
                                          .WithLightSeries(6, FitsTestData.OpticsCards(20));

        var vm = await Run(f);

        Assert.True(vm.Refused);
        Assert.Equal("nedeterminat (FOCALLEN=20mm, fara marimea pixelului)", vm.Field);
        Assert.Contains("Decizia s-a luat doar pe focala", vm.Reasons);
        Assert.DoesNotContain("--fov", vm.Reasons);
    }

    /// <summary>The refusal verdict is coloured neutral, not red.</summary>
    [Fact]
    public async Task ARefusalIsColouredNeutrally()
    {
        using var f = new SessionFixture().WithFlatMaster()
                                          .WithLightSeries(6, SessionFixture.WideFieldCards);

        var vm = await Run(f);

        Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For(vm.Verdict));
    }

    // ------------------------------------------------------------------ inconclusive

    /// <summary>
    /// A series too short cannot conclude - under five frames the mean is not a
    /// measurement. The verdict is NEDETERMINAT and it says why, but the figures exist.
    /// </summary>
    [Fact]
    public async Task AShortSeriesIsUndeterminedButStillShowsItsNumbers()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(3);

        var vm = await Run(f);

        Assert.Equal(LightSeriesAnalyzer.Undetermined, vm.Verdict);
        Assert.True(vm.HasNumbers);
        Assert.NotEmpty(vm.MeanRadial);
        Assert.NotEmpty(vm.Reasons);
        Assert.Empty(vm.Diagnosis);        // without a verdict no diagnosis is given
    }

    // ------------------------------------------------------------------ no frames

    /// <summary>
    /// Without calibrated frames, the tab explains what it asks for and why the registered
    /// ones do not fit - instead of throwing an exception from the middle of a computation.
    /// </summary>
    [Fact]
    public async Task WithoutCalibratedFramesItExplainsWhatItNeeds()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = await Run(f);

        Assert.False(vm.HasResult);
        Assert.True(vm.HasError);
        Assert.Contains("pp_light_*.fit", vm.ErrorText);
        Assert.Contains("r_pp_light_", vm.ErrorText);
    }

    [Fact]
    public async Task RegisteredFramesAloneStillCountAsNoSeries()
    {
        using var f = new SessionFixture().WithFlatMaster();
        File.Copy(Path.Combine(f.ProcessDir, SirilSession.FlatMasterName),
                  Path.Combine(f.ProcessDir, "r_pp_light_00001.fit"));

        var vm = await Run(f);

        Assert.True(vm.HasError);
    }

    // ------------------------------------------------------------------ cancellation

    /// <summary>
    /// Cancellation is not an error: it means the user changed the folder or closed the
    /// window. The tab stays empty, without a red panel.
    /// </summary>
    [Fact]
    public async Task CancellingLeavesTheTabEmptyWithoutAnError()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(6);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var vm = new LightTabViewModel();
        await vm.RunAsync(f.Session, cts.Token);

        Assert.False(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ResetClearsEverything()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(5);
        var vm = await Run(f);

        vm.Reset();

        Assert.False(vm.HasResult);
        Assert.False(vm.Refused);
        Assert.Empty(vm.Frames);
        Assert.Empty(vm.Verdict);
        Assert.Null(vm.Map);
        Assert.Empty(vm.MapLegend);
    }

    // ------------------------------------------------------------------ legend

    /// <summary>
    /// The legend gives the ends of the automatic scale, which here are usually very
    /// asymmetric - the map is 1 + radial/a, so it goes far to one side. Without the legend, the
    /// colour says nothing.
    /// </summary>
    [Fact]
    public async Task AConclusiveRunHasALegend()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(6);

        var vm = await Run(f);

        Assert.Contains("albastru", vm.MapLegend);
        Assert.Contains("rosu", vm.MapLegend);
    }

    /// <summary>
    /// On a refusal there is no map, so the legend has nothing to describe either.
    /// </summary>
    [Fact]
    public async Task ARefusedRunHasNoLegend()
    {
        using var f = new SessionFixture().WithFlatMaster()
                                          .WithLightSeries(6, SessionFixture.WideFieldCards);

        var vm = await Run(f);

        Assert.Empty(vm.MapLegend);
    }
}
