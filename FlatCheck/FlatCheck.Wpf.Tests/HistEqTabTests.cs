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

using System.Threading;
using FlatCheck.Tests;

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// Tabul "Histogram". Comportamentul cerut: lista cu masterul intai si cadrele din
/// flats/ dupa el; un click calculeaza, alt click recalculeaza, iar un click in timpul
/// unui calcul il inlocuieste in loc sa astepte. Cifrele nu se verifica aici - nu exista
/// cifre - ci ca poza chiar se schimba cu fisierul si cu vederea.
/// </summary>
public class HistEqTabTests
{
    static HistEqTabViewModel Attached(SessionFixture f)
    {
        var vm = new HistEqTabViewModel();
        vm.Attach(f.Session, CancellationToken.None);
        return vm;
    }

    // ------------------------------------------------------------------ lista

    [Fact]
    public void TheListHasTheMasterFirst_ThenTheReadableFramesFromFlats()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("DSC_0001.NEF", "note.txt");
        f.WriteAnalysableRawFlat("conv.fit");

        var vm = Attached(f);

        Assert.Equal(new[] { SirilSession.FlatMasterName, "DSC_0001.NEF", "conv.fit" },
                     vm.Files.Select(r => r.Name));
        Assert.True(vm.Files[0].IsMaster);
        Assert.Equal("MASTER", vm.Files[0].Tag);
        Assert.Equal("process/", vm.Files[0].Where);
        Assert.Equal(f.Session.FlatMasterPath, vm.Files[0].Path);
        Assert.All(vm.Files.Skip(1), r => { Assert.False(r.IsMaster); Assert.Equal("", r.Tag); Assert.Equal("flats/", r.Where); });
        Assert.False(vm.HasError);
    }

    /// <summary>Jumatate de raspuns e tot un raspuns: masterul singur se poate vedea.</summary>
    [Fact]
    public void WithTheMasterButNoFlatsFolder_TheListHasOneRowAndNoError()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = Attached(f);

        Assert.Single(vm.Files);
        Assert.True(vm.Files[0].IsMaster);
        Assert.False(vm.HasError);
    }

    /// <summary>Numai prin atasare directa: MainViewModel refuza folderul fara master.</summary>
    [Fact]
    public void WithoutMasterAndWithoutFrames_ItExplains()
    {
        using var f = new SessionFixture().WithProcess().WithFlats();

        var vm = Attached(f);

        Assert.Empty(vm.Files);
        Assert.True(vm.HasError);
        Assert.Contains("flats", vm.ErrorText);
        Assert.Contains(".nef", vm.ErrorText);
        Assert.Contains(".fit", vm.ErrorText);
    }

    [Fact]
    public void NothingIsSelectedUntilTheUserPicks()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = Attached(f);

        Assert.Null(vm.Selected);
        Assert.False(vm.HasResult);
        Assert.Null(vm.Image);
    }

    // ------------------------------------------------------------------ calcul

    [Fact]
    public async Task PickingTheMasterEqualisesTheMasterFile()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);

        vm.Selected = vm.Files[0];
        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
        Assert.NotNull(vm.Image);
        Assert.Contains("128 x 128", vm.Frame);
        Assert.NotEmpty(vm.MasterNote);
        Assert.Contains("Masterul e stiva", vm.MasterNote);
        Assert.Contains("EGALIZARE DE HISTOGRAMA", vm.Report);
        Assert.Contains(SirilSession.FlatMasterName, vm.Report);
        Assert.NotEmpty(vm.Caveat);
        // un master mono: o singura vedere, deci fara chipuri
        Assert.Single(vm.View.Options);
        Assert.False(vm.View.IsVisible);
    }

    [Fact]
    public async Task PickingAnotherFileRegeneratesThePicture_AndAFrameHasNoMasterNote()
    {
        using var f = new SessionFixture().WithFlatMaster();
        f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");
        var vm = Attached(f);

        vm.Selected = vm.Files[0];
        await vm.RecomputeAsync();
        byte[] master = vm.Image!;

        vm.Selected = vm.Files[1];
        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
        Assert.NotEqual(master, vm.Image);
        Assert.Equal("", vm.MasterNote);
        Assert.Contains("CFA (ordine RGGB", vm.Frame);
        Assert.Equal(new[] { "RGB", "R", "G1", "G2", "B", "Mozaic" }, vm.View.Options.Select(o => o.Label));
        Assert.True(vm.View.IsVisible);
    }

    /// <summary>
    /// Comutarea vederii nu reciteste fisierul si nu reface rezultatul: reencodeaza din
    /// rasterele tinute, si a doua oara nici atat - vine din cache, acelasi obiect.
    /// </summary>
    [Fact]
    public async Task SwitchingTheViewChangesThePictureWithoutRerunning()
    {
        using var f = new SessionFixture().WithFlatMaster();
        f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");
        var vm = Attached(f);
        vm.Selected = vm.Files[1];
        await vm.RecomputeAsync();
        byte[] rgb = vm.Image!;
        string report = vm.Report;

        vm.View.Options.First(o => o.Label == "R").IsSelected = true;
        byte[] red = vm.Image!;
        vm.View.Options.First(o => o.Label == "Mozaic").IsSelected = true;
        byte[] mosaic = vm.Image!;
        vm.View.Options.First(o => o.Label == "RGB").IsSelected = true;

        Assert.NotEqual(rgb, red);
        Assert.NotEqual(red, mosaic);
        Assert.Same(rgb, vm.Image);
        Assert.Same(report, vm.Report);
        Assert.False(vm.IsBusy);
        Assert.True(vm.Pending.IsCompleted);
        Assert.Contains("pozitia R", HistEqReport.ViewCaption(HistEqAnalyzer.Analyze(TestImages.FlatCfa(8, 8, 100), null), "R"));
    }

    /// <summary>
    /// CERINTA: un click pe alt fisier in timpul unui calcul nu asteapta - il inlocuieste.
    /// Oricare din cele doua rulari termina prima, pe ecran ramane ultima alegere.
    /// </summary>
    [Fact]
    public async Task ASecondPickSupersedesTheFirst()
    {
        using var f = new SessionFixture().WithFlatMaster();
        f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");
        var vm = Attached(f);

        vm.Selected = vm.Files[0];          // porneste, nu se asteapta
        Task first = vm.Pending;
        vm.Selected = vm.Files[1];          // peste ea
        Task second = vm.Pending;
        await Task.WhenAll(first, second);

        Assert.True(vm.HasResult);
        Assert.False(vm.IsBusy);
        Assert.False(vm.HasError);
        Assert.Equal("", vm.MasterNote);
        Assert.Contains("CFA", vm.Frame);
        Assert.Contains("flat.fit", vm.Report);
    }

    [Fact]
    public async Task ResetClearsEverything()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);
        vm.Selected = vm.Files[0];
        await vm.RecomputeAsync();

        vm.Reset();

        Assert.Empty(vm.Files);
        Assert.Null(vm.Selected);
        Assert.Null(vm.Image);
        Assert.False(vm.HasResult);
        Assert.Equal("", vm.Frame);
        Assert.Equal("", vm.MasterNote);
        Assert.Equal("", vm.Report);
    }
}
