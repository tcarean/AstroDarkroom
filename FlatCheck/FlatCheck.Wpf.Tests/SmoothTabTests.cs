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
/// Tabul "Netezire". Ce se verifica: ca nu ruleaza singur, ca butonul scrie fisierul
/// acolo unde spune sesiunea si ca intreaba inainte sa suprascrie, ca cifrele sunt
/// cele ale nucleului, si ca un refuz (cub de culoare) ajunge pe ecran ca explicatie.
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
        Assert.Equal(Path.Combine(f.ProcessDir, "pp_flat_stacked_netezit.fit"), vm.Target);
        Assert.False(File.Exists(vm.Target));
        Assert.True(vm.Guide.IsExpanded);
        Assert.Equal(Th.ResidPerfect, vm.MapScale.Selected.CapPct);
    }

    [Fact]
    public async Task TheButtonWritesTheFileWhereTheSessionSays_AndFillsBothBatteries()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);

        await vm.RunAsync();

        Assert.True(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.Equal(vm.Target, vm.SavedTo);
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
    /// Fisierul netezit NU devine masterul sesiunii: numele lui nu e printre candidati,
    /// deci tabul Structura masoara in continuare masterul fotografiat.
    /// </summary>
    [Fact]
    public async Task TheSmoothedFileDoesNotBecomeTheSessionMaster()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);

        await vm.RunAsync();

        Assert.Equal(Path.Combine(f.ProcessDir, SirilSession.FlatMasterName), f.Session.FlatMasterPath);
        Assert.False(f.Session.FindFlatMaster().Ambiguous);
        // ...but the Ratio dialog may offer it: a smoothed stack is a stack
        Assert.True(SirilSession.IsFlatMasterName(vm.Target));
    }

    [Fact]
    public async Task WhenTheFileExists_ItAsksBeforeOverwriting_AndANoKeepsTheOldFile()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string target = f.Session.SmoothedMasterPath;
        File.WriteAllBytes(target, new byte[] { 1, 2, 3 });
        string? asked = null;
        var vm = Attached(f, q => { asked = q; return false; });

        await vm.RunAsync();

        Assert.NotNull(asked);
        Assert.Contains(target, asked);
        Assert.True(vm.HasResult);                       // the numbers stay on screen
        Assert.Equal("", vm.SavedTo);
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

        await vm.RunAsync();

        Assert.Equal(target, vm.SavedTo);
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

        await vm.RunAsync();

        Assert.False(vm.HasResult);
        Assert.True(vm.HasError);
        Assert.Contains("3 planuri", vm.ErrorText);
        Assert.False(File.Exists(vm.Target));
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

    /// <summary>Butonul asteapta lantul de la deschidere: apasat in timpul structurii, nu se suprapune peste ea.</summary>
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

    /// <summary>Folder nou = tabul se goleste, ca toate celelalte.</summary>
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
