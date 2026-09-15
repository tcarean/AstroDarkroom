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
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    /// <summary>Coloana "unde" spune folderul REAL al masterului, care poate fi masters/.</summary>
    [Fact]
    public void AMasterInMastersIsListedWithItsRealFolder()
    {
        using var f = new SessionFixture().WithFlatMasterInMasters();

        var vm = Attached(f);

        Assert.True(vm.Files[0].IsMaster);
        Assert.Equal("masters/", vm.Files[0].Where);
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
        ImageSource master = vm.Image!;
        vm.Zoom.Manual(3);

        vm.Selected = vm.Files[1];
        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
        Assert.NotSame(master, vm.Image);
        Assert.Equal("", vm.MasterNote);
        Assert.Contains("CFA (ordine RGGB", vm.Frame);
        // mozaicul primul si implicit; RGB al doilea
        Assert.Equal(new[] { "Mozaic", "RGB", "R", "G1", "G2", "B" }, vm.View.Options.Select(o => o.Label));
        Assert.Equal("Mozaic", vm.View.Selected.Label);
        Assert.True(vm.View.IsVisible);
        // fisier nou = zoom la potrivit
        Assert.True(vm.Zoom.IsFit);
    }

    /// <summary>
    /// Fereastra arata rasterele la rezolutie intreaga, direct ca bitmap: mozaicul cat
    /// cadrul, o pozitie la jumatate pe fiecare latura, compozitul color.
    /// </summary>
    [Fact]
    public async Task ThePictureIsTheFullResolutionRaster_NotAShrunkPng()
    {
        using var f = new SessionFixture().WithFlatMaster();
        f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");
        var vm = Attached(f);
        vm.Selected = vm.Files[1];
        await vm.RecomputeAsync();

        var mosaic = (BitmapSource)vm.Image!;
        Assert.Equal((128, 128), (mosaic.PixelWidth, mosaic.PixelHeight));
        Assert.Equal(PixelFormats.Gray8, mosaic.Format);
        Assert.Contains("128 x 128 px", vm.Caption);

        vm.View.Options.First(o => o.Label == "RGB").IsSelected = true;
        var rgb = (BitmapSource)vm.Image!;
        Assert.Equal((64, 64), (rgb.PixelWidth, rgb.PixelHeight));
        Assert.Equal(PixelFormats.Rgb24, rgb.Format);
        Assert.Contains("64 x 64 px", vm.Caption);
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
        ImageSource mosaic = vm.Image!;
        string report = vm.Report;
        vm.Zoom.Manual(2);

        vm.View.Options.First(o => o.Label == "R").IsSelected = true;
        ImageSource red = vm.Image!;
        vm.View.Options.First(o => o.Label == "RGB").IsSelected = true;
        ImageSource rgb = vm.Image!;
        vm.View.Options.First(o => o.Label == "Mozaic").IsSelected = true;

        Assert.NotSame(mosaic, red);
        Assert.NotSame(red, rgb);
        Assert.Same(mosaic, vm.Image);     // din cache, acelasi obiect
        Assert.Same(report, vm.Report);
        Assert.False(vm.IsBusy);
        Assert.True(vm.Pending.IsCompleted);
        // schimbarea vederii NU reseteaza zoom-ul
        Assert.False(vm.Zoom.IsFit);
        Assert.Equal(2, vm.Zoom.Zoom);
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

        vm.Zoom.Manual(2);
        vm.Reset();

        Assert.Empty(vm.Files);
        Assert.Null(vm.Selected);
        Assert.Null(vm.Image);
        Assert.True(vm.Zoom.IsFit);
        Assert.False(vm.HasResult);
        Assert.Equal("", vm.Frame);
        Assert.Equal("", vm.MasterNote);
        Assert.Equal("", vm.Report);
    }
}
