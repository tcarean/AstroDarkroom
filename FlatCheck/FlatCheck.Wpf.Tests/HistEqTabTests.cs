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

using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlatCheck.Tests;

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// Tabul "Histogram". Comportamentul cerut: lista cu masterul intai, flat-ul netezit
/// dupa el cand exista, si cadrele din flats/ dupa ei; un click calculeaza, alt click
/// recalculeaza, iar un click in timpul unui calcul il inlocuieste in loc sa astepte.
/// Netezitul poate aparea si in timpul sesiunii, scris de tabul Netezire - lista se
/// reface fara sa strice alegerea. Cifrele nu se verifica aici - nu exista cifre - ci ca
/// poza chiar se schimba cu fisierul si cu vederea.
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
        Assert.True(vm.Files[0].HasTag);
        Assert.True(vm.Files[0].ClosesGroup);     // fara netezit, linia sta sub master
        Assert.Equal("process/", vm.Files[0].Where);
        Assert.Equal(f.Session.FlatMasterPath, vm.Files[0].Path);
        Assert.All(vm.Files.Skip(1), r =>
        {
            Assert.False(r.IsMaster);
            Assert.False(r.IsSmoothed);
            Assert.Equal("", r.Tag);
            Assert.False(r.HasTag);
            Assert.False(r.ClosesGroup);
            Assert.Equal("flats/", r.Where);
        });
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

    /// <summary>
    /// Cazul A: fisierul netezit exista la deschidere. Al doilea rand, chip NETEZIT, nu
    /// master; linia de sub grup se muta sub el.
    /// </summary>
    [Fact]
    public void ASmoothedMasterPresentAtOpenIsListedSecond_TaggedNetezit()
    {
        using var f = new SessionFixture().WithFlatMaster().WithSmoothedMaster().WithRawFlats("DSC_0001.NEF");

        var vm = Attached(f);

        Assert.Equal(new[] { "MASTER", "NETEZIT", "" }, vm.Files.Select(r => r.Tag));
        HistEqFileRow smoothed = vm.Files[1];
        Assert.False(smoothed.IsMaster);
        Assert.True(smoothed.IsSmoothed);
        Assert.True(smoothed.HasTag);
        Assert.Equal("pp_flat_stacked_netezit.fit", smoothed.Name);
        Assert.Equal("process/", smoothed.Where);
        Assert.Equal(f.Session.SmoothedMasterPath, smoothed.Path);
        Assert.Equal(new[] { false, true, false }, vm.Files.Select(r => r.ClosesGroup));
    }

    [Fact]
    public void ASmoothedMasterInMastersIsListedWithItsRealFolder()
    {
        using var f = new SessionFixture().WithFlatMasterInMasters().WithSmoothedMaster();

        var vm = Attached(f);

        Assert.Equal("NETEZIT", vm.Files[1].Tag);
        Assert.Equal("masters/", vm.Files[1].Where);
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

    /// <summary>Netezitul are nota lui, nu pe a masterului; si masterul o pastreaza pe a lui.</summary>
    [Fact]
    public async Task PickingTheSmoothedMasterShowsTheSmoothedNote()
    {
        using var f = new SessionFixture().WithFlatMaster().WithSmoothedMaster();
        var vm = Attached(f);

        vm.Selected = vm.Files[1];
        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
        Assert.Equal(HistEqReport.SmoothedVersusMaster(), vm.MasterNote);
        Assert.Contains("pp_flat_stacked_netezit.fit", vm.Report);

        vm.Selected = vm.Files[0];
        await vm.RecomputeAsync();
        Assert.Equal(HistEqReport.MasterVersusFrame(), vm.MasterNote);
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

    /// <summary>
    /// A pick made while another heavy run holds the gate - the opening chain reading the very
    /// master this list offers first - waits for it instead of loading the 24 MP file beside
    /// it (R-081, ADR 0061); the waiting screen says so. A second pick while the first still
    /// waits supersedes it: the first leaves the queue without computing, and when the gate
    /// opens only the latest reaches the screen.
    /// </summary>
    [Fact]
    public async Task R085_APickDuringAHeavyRunWaitsForTheGateAndASecondPickSupersedesIt()
    {
        using var f = new SessionFixture().WithFlatMaster();
        f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");
        var gate = new HeavyRunGate();
        IDisposable chain = await gate.EnterAsync(CancellationToken.None);
        var vm = new HistEqTabViewModel();
        vm.Attach(f.Session, CancellationToken.None, gate);

        vm.Selected = vm.Files[0];          // the master: waits
        Task first = vm.Pending;
        Assert.NotSame(first, await Task.WhenAny(first, Task.Delay(1000)));
        Assert.True(vm.IsBusy);
        Assert.Equal(SessionMessages.WaitingForOpeningChain(), vm.BusyMessage);

        vm.Selected = vm.Files[1];          // over it: the frame
        Task second = vm.Pending;
        await first;                        // left the queue
        Assert.False(second.IsCompleted);
        Assert.False(vm.HasResult);

        chain.Dispose();
        await second;

        Assert.True(vm.HasResult);
        Assert.False(vm.IsBusy);
        Assert.Equal("", vm.MasterNote);
        Assert.Contains("flat.fit", vm.Report);
        Assert.False(gate.IsHeld);
    }

    // ------------------------------------------------------------------ fisier scris de alt tab

    /// <summary>
    /// Cazul B: netezitul apare in timpul sesiunii. Lista se reface, alegerea ramane,
    /// nimic nu se recalculeaza - poza e acelasi obiect.
    /// </summary>
    [Fact]
    public async Task AFileWrittenLaterAppearsInTheList_WithoutDisturbingTheSelection()
    {
        using var f = new SessionFixture().WithFlatMaster();
        f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");
        var vm = Attached(f);
        vm.Selected = vm.Files[1];
        await vm.RecomputeAsync();
        ImageSource picture = vm.Image!;
        Assert.Equal(2, vm.Files.Count);

        f.WithSmoothedMaster();
        vm.FileWritten(f.Session.SmoothedMasterPath);
        await vm.Pending;

        Assert.Equal(new[] { "MASTER", "NETEZIT", "" }, vm.Files.Select(r => r.Tag));
        Assert.Equal(new[] { false, true, false }, vm.Files.Select(r => r.ClosesGroup));
        Assert.NotNull(vm.Selected);
        Assert.Same(vm.Files[2], vm.Selected);
        Assert.Same(picture, vm.Image);
        Assert.False(vm.IsBusy);
    }

    /// <summary>Fisierul de pe ecran a fost rescris: poza se reface, altfel ar arata octeti care nu mai exista.</summary>
    [Fact]
    public async Task RewritingTheSelectedSmoothedFileRerunsIt()
    {
        using var f = new SessionFixture().WithFlatMaster().WithSmoothedMaster();
        var vm = Attached(f);
        vm.Selected = vm.Files[1];
        await vm.RecomputeAsync();
        ImageSource before = vm.Image!;

        vm.FileWritten(f.Session.SmoothedMasterPath);
        await vm.Pending;

        Assert.Same(vm.Files[1], vm.Selected);
        Assert.True(vm.Selected!.IsSmoothed);
        Assert.NotSame(before, vm.Image);
        Assert.True(vm.HasResult);
        Assert.Equal(HistEqReport.SmoothedVersusMaster(), vm.MasterNote);
    }

    /// <summary>Singurul fisier de care ii pasa tabului e netezitul sesiunii; orice altceva nu misca nimic.</summary>
    [Fact]
    public async Task AWriteOfSomeOtherPathIsIgnored()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF");
        var vm = Attached(f);
        vm.Selected = vm.Files[0];
        await vm.RecomputeAsync();
        HistEqFileRow selected = vm.Selected!;
        ImageSource picture = vm.Image!;

        vm.FileWritten(f.Session.FlatMasterPath);
        vm.FileWritten(Path.Combine(f.ProcessDir, "altceva_netezit.fit"));
        vm.FileWritten(null!);
        await vm.Pending;

        Assert.Equal(2, vm.Files.Count);
        Assert.Same(selected, vm.Selected);
        Assert.Same(picture, vm.Image);
    }

    [Fact]
    public void FileWrittenBeforeAttachOrAfterResetIsHarmless()
    {
        using var f = new SessionFixture().WithFlatMaster().WithSmoothedMaster();
        var fresh = new HistEqTabViewModel();
        fresh.FileWritten(f.Session.SmoothedMasterPath);
        Assert.Empty(fresh.Files);

        var vm = Attached(f);
        vm.Reset();
        vm.FileWritten(f.Session.SmoothedMasterPath);
        Assert.Empty(vm.Files);
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
