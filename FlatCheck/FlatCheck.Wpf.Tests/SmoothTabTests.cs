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
/// The "Netezire" tab. What is checked: that it does not run by itself, that the button
/// writes the file where the session says and asks before overwriting, that the figures
/// are the core's, and that a refusal (colour cube) reaches the screen as an explanation.
/// </summary>
public class SmoothTabTests
{
    static SmoothTabViewModel Attached(SessionFixture f, Func<string, bool>? confirm = null)
    {
        var vm = new SmoothTabViewModel(confirm);
        vm.Attach(f.Session, CancellationToken.None);
        return vm;
    }

    [Fact]
    public void AttachRunsNothing_AndNamesTheTargetBesideTheMaster()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = Attached(f);

        Assert.False(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.True(vm.CanRun);
        Assert.Equal(f.Session.FlatMasterPath, vm.Source);
        Assert.Equal(Path.Combine(f.ProcessDir, "pp_flat_stacked_smoothed.fit"), vm.Target);
        Assert.False(File.Exists(vm.Target));
        Assert.True(vm.Guide.IsExpanded);
        Assert.Equal(Th.ResidPerfect, vm.MapScale.Selected.CapPct);
    }

    [Fact]
    public async Task TheButtonWritesTheFileWhereTheSessionSays_AndFillsBothBatteries()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);
        var saved = new List<string>();
        vm.Saved += saved.Add;

        await vm.RunAsync();

        Assert.True(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.Equal(vm.Target, vm.SavedTo);
        Assert.Equal(new[] { vm.Target }, saved);      // once, with the path
        Assert.True(File.Exists(vm.Target));
        Assert.Equal("", vm.SaveError);
        Assert.Equal(SmoothAnalyzer.DefaultScalePx, vm.LastScalePx);

        Assert.Equal(5, vm.Before.Count);
        Assert.Equal(5, vm.After.Count);
        Assert.Equal(vm.Before.Select(r => r.Label), vm.After.Select(r => r.Label));
        Assert.Contains(vm.Before, r => r.IsKey);
        Assert.NotEmpty(vm.BeforeVerdict);
        Assert.NotEmpty(vm.AfterVerdict);
        Assert.Equal(4, vm.Removed.Count);
        Assert.All(vm.Removed, r => Assert.False(r.HasVerdict));
        Assert.NotNull(vm.Map);
        Assert.NotEmpty(vm.MapLegend);
        Assert.Contains("NETEZIRE", vm.Report);
        Assert.Contains(vm.Target, vm.Report);
        Assert.StartsWith(VersionInfo.Banner, vm.Report);

        // the file re-loads to the analyzer's raster, with the master's shape
        var (back, _, _, _, _) = FitsReader.LoadFull(File.ReadAllBytes(vm.Target));
        Assert.Equal((128, 128), (back.W, back.H));
    }

    /// <summary>
    /// The smoothed file does NOT become the session's master: its name is not among the
    /// candidates, so the Structura tab still measures the photographed master.
    /// </summary>
    [Fact]
    public async Task TheSmoothedFileDoesNotBecomeTheSessionMaster()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);

        await vm.RunAsync();

        Assert.Equal(Path.Combine(f.ProcessDir, SirilSession.FlatMasterName), f.Session.FlatMasterPath);
        Assert.False(f.Session.FindFlatMaster().Ambiguous);
    }

    [Fact]
    public async Task WhenTheFileExists_ItAsksBeforeOverwriting_AndANoKeepsTheOldFile()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string target = f.Session.SmoothedMasterPath;
        File.WriteAllBytes(target, new byte[] { 1, 2, 3 });
        string? asked = null;
        var vm = Attached(f, q => { asked = q; return false; });
        bool fired = false;
        vm.Saved += _ => fired = true;

        await vm.RunAsync();

        Assert.NotNull(asked);
        Assert.Contains(target, asked);
        Assert.True(vm.HasResult);                       // the numbers stay on screen
        Assert.Equal("", vm.SavedTo);
        Assert.False(fired);                             // nothing on disk changed
        Assert.Contains(target, vm.SaveError);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(target));
        Assert.DoesNotContain("salvat:", vm.Report);
    }

    [Fact]
    public async Task WhenTheFileExists_AYesOverwritesIt()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string target = f.Session.SmoothedMasterPath;
        File.WriteAllBytes(target, new byte[] { 1, 2, 3 });
        var vm = Attached(f, _ => true);
        string? saved = null;
        vm.Saved += p => saved = p;

        await vm.RunAsync();

        Assert.Equal(target, vm.SavedTo);
        Assert.Equal(target, saved);
        Assert.True(new FileInfo(target).Length > 2880);
    }

    [Fact]
    public async Task TheScaleChipsChangeTheScaleOfTheNextRun()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);
        vm.Scale.Options[0].IsSelected = true;

        await vm.RunAsync();

        Assert.Equal(SmoothAnalyzer.DefaultScalePx / 2, vm.LastScalePx);
        Assert.Contains($"{SmoothAnalyzer.DefaultScalePx / 2:F0} px pe senzor", vm.ScaleLine);
        Assert.Single(vm.Scale.Options, o => o.IsSelected);
    }

    [Fact]
    public async Task ACubeMasterIsRefused_WithTheExplanationOnScreen()
    {
        using var f = new SessionFixture().WithProcess();
        File.WriteAllBytes(Path.Combine(f.ProcessDir, SirilSession.FlatMasterName),
            FitsTestData.BuildFitsCube(32, 32, 3, (x, y, p) => 1000 + p));
        var vm = Attached(f);
        bool fired = false;
        vm.Saved += _ => fired = true;

        await vm.RunAsync();

        Assert.False(vm.HasResult);
        Assert.True(vm.HasError);
        Assert.Contains("3 planuri", vm.ErrorText);
        Assert.False(File.Exists(vm.Target));
        Assert.False(fired);
    }

    [Fact]
    public async Task TheTwoMapsAreDifferentPictures_AndTheCapReencodes()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);
        await vm.RunAsync();

        byte[] removed = vm.Map!;
        vm.ShowAfterMap = true;
        byte[] after = vm.Map!;
        vm.MapScale.Options[0].IsSelected = true;   // auto
        byte[] afterAuto = vm.Map!;

        Assert.NotEqual(removed, after);
        Assert.NotEqual(after, afterAuto);
        Assert.Contains("auto", vm.MapScale.Selected.Label);
    }

    [Fact]
    public async Task ResetLeavesTheTabEmpty_AndTheButtonDisabled()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);
        await vm.RunAsync();

        vm.Reset();

        Assert.False(vm.HasResult);
        Assert.False(vm.CanRun);
        Assert.Equal("", vm.Source);
        Assert.Equal("", vm.Target);
        Assert.Empty(vm.Before);
        Assert.Null(vm.Map);
        Assert.Equal("", vm.SavedTo);
        Assert.False(vm.Run.CanExecute(null));
    }

    // ---------------------------------------------------------------- the scale proposal

    /// <summary>A master without texture: the proposal says "nothing to smooth", writes nothing
    /// and leaves the chip.</summary>
    [Fact]
    public async Task ProposingOnACleanMasterSaysNothingToSmooth_AndLeavesTheChip()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);
        var chip = vm.Scale.Selected;

        await vm.AdviseAsync();

        Assert.True(vm.HasAdvice);
        Assert.StartsWith("NIMIC DE NETEZIT", vm.AdviceVerdict);
        Assert.Empty(vm.AdviceRows);
        Assert.Contains($"sub {Th.SmoothTextureMin:F2} %", vm.AdviceTexture);
        Assert.Same(chip, vm.Scale.Selected);
        Assert.True(double.IsNaN(vm.ProposedScalePx));
        Assert.False(vm.HasResult);
        Assert.False(File.Exists(vm.Target));
        Assert.Contains("propunere de scara", vm.Report);
    }

    /// <summary>
    /// A master with texture: the three steps, a proposal, and the proposed chip ticked - with
    /// no file written; the smoothing stays its button's.
    /// </summary>
    [Fact]
    public async Task ProposingOnATexturedMasterSelectsTheChip_AndWritesNoFile()
    {
        // The same 512-px master with a 13-px weave as the --scale auto golden: on the
        // 128-px fixture master there is no texture period that is both fine enough to
        // average out inside the azimuthal sectors and coarse enough not to count as pixel
        // noise in the texture metric, so the tab is exercised at the golden's size.
        using var f = new SessionFixture().WithProcess();
        var radial = FitsTestData.Radial(512, 512);
        File.WriteAllBytes(Path.Combine(f.ProcessDir, SirilSession.FlatMasterName),
            FitsTestData.BuildFits(512, 512, (x, y) => radial(x, y) * (1 + 0.03 * Math.Sin(x / 2.0) * Math.Cos(y / 2.3))));
        var vm = Attached(f);

        await vm.AdviseAsync();

        Assert.True(vm.HasAdvice);
        Assert.StartsWith("SCARA PROPUSA", vm.AdviceVerdict);
        Assert.Equal(3, vm.AdviceRows.Count);
        Assert.False(double.IsNaN(vm.ProposedScalePx));
        Assert.Equal(vm.ProposedScalePx, vm.Scale.ScalePx);
        Assert.Single(vm.Scale.Options, o => o.IsSelected);
        Assert.False(File.Exists(vm.Target));

        // and the smoothing that follows uses the proposed chip
        await vm.RunAsync();
        Assert.Equal(vm.ProposedScalePx, vm.LastScalePx);
        Assert.True(File.Exists(vm.Target));
    }

    [Fact]
    public async Task ResetClearsTheAdvice()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);
        await vm.AdviseAsync();

        vm.Reset();

        Assert.False(vm.HasAdvice);
        Assert.Empty(vm.AdviceRows);
        Assert.False(vm.Advise.CanExecute(null));
    }

    /// <summary>The button waits for the opening chain: pressed during the structure, it does not
    /// overlap it.</summary>
    [Fact]
    public async Task TheButtonWaitsForTheOpeningChain()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(5);
        var vm = new MainViewModel(null, null, _ => true);

        Task opening = vm.OpenAsync(f.Root);
        Task smoothing = vm.Smooth.RunAsync();
        await opening;
        await smoothing;

        Assert.True(vm.Structure.HasResult);
        Assert.True(vm.Smooth.HasResult);
        Assert.True(File.Exists(vm.Smooth.Target));
    }

    /// <summary>New folder = the tab empties, like all the others.</summary>
    [Fact]
    public async Task ChangingTheFolderResetsTheTab()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = new MainViewModel(null, null, _ => true);
        await vm.OpenAsync(f.Root);
        await vm.Smooth.RunAsync();
        Assert.True(vm.Smooth.HasResult);

        await vm.OpenAsync("");

        Assert.False(vm.Smooth.HasResult);
        Assert.Equal("", vm.Smooth.Source);
    }
}
