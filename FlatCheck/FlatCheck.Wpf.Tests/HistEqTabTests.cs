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
/// The "Histogram" tab. The required behaviour: the list with the master first, the smoothed
/// flat after it when there is one, and the frames from flats/ after them; a click computes,
/// another click recomputes, and a click during a computation replaces it instead of
/// waiting. The smoothed flat can also appear during the session, written by the Netezire
/// tab - the list is rebuilt without spoiling the choice. No figures are checked here - there
/// are none - but that the picture really changes with the file and with the view.
/// </summary>
public class HistEqTabTests
{
    static HistEqTabViewModel Attached(SessionFixture f)
    {
        var vm = new HistEqTabViewModel();
        vm.Attach(f.Session, CancellationToken.None);
        return vm;
    }

    // ------------------------------------------------------------------ the list

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
        Assert.True(vm.Files[0].ClosesGroup);     // without a smoothed flat, the line sits under the master
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

    /// <summary>The "where" column gives the master's REAL folder, which can be masters/.</summary>
    [Fact]
    public void AMasterInMastersIsListedWithItsRealFolder()
    {
        using var f = new SessionFixture().WithFlatMasterInMasters();

        var vm = Attached(f);

        Assert.True(vm.Files[0].IsMaster);
        Assert.Equal("masters/", vm.Files[0].Where);
    }

    /// <summary>
    /// Case A: the smoothed file exists at opening. The second row, chip NETEZIT, not
    /// master; the line under the group moves under it.
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
        Assert.Equal("pp_flat_stacked_smoothed.fit", smoothed.Name);
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

    /// <summary>Half an answer is still an answer: the master alone can be seen.</summary>
    [Fact]
    public void WithTheMasterButNoFlatsFolder_TheListHasOneRowAndNoError()
    {
        using var f = new SessionFixture().WithFlatMaster();

        var vm = Attached(f);

        Assert.Single(vm.Files);
        Assert.True(vm.Files[0].IsMaster);
        Assert.False(vm.HasError);
    }

    /// <summary>
    /// Only by attaching directly: MainViewModel refuses the folder without a master.
    /// </summary>
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

    // ------------------------------------------------------------------ the computation

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
        // a mono master: a single view, so no chips
        Assert.Single(vm.View.Options);
        Assert.False(vm.View.IsVisible);
    }

    /// <summary>
    /// The smoothed flat has its own note, not the master's; and the master keeps its own.
    /// </summary>
    [Fact]
    public async Task PickingTheSmoothedMasterShowsTheSmoothedNote()
    {
        using var f = new SessionFixture().WithFlatMaster().WithSmoothedMaster();
        var vm = Attached(f);

        vm.Selected = vm.Files[1];
        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
        Assert.Equal(HistEqReport.SmoothedVersusMaster(), vm.MasterNote);
        Assert.Contains("pp_flat_stacked_smoothed.fit", vm.Report);

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
        // the mosaic first and by default; RGB second
        Assert.Equal(new[] { "Mozaic", "RGB", "R", "G1", "G2", "B" }, vm.View.Options.Select(o => o.Label));
        Assert.Equal("Mozaic", vm.View.Selected.Label);
        Assert.True(vm.View.IsVisible);
        // a new file = zoom to fit
        Assert.True(vm.Zoom.IsFit);
    }

    /// <summary>
    /// The window shows the rasters at full resolution, directly as a bitmap: the mosaic the
    /// size of the frame, a position at half on each side, the colour composite.
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
    /// Switching the view neither rereads the file nor redoes the result: it re-encodes from
    /// the rasters kept, and the second time not even that - it comes from the cache, the
    /// same object.
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
        Assert.Same(mosaic, vm.Image);     // from the cache, the same object
        Assert.Same(report, vm.Report);
        Assert.False(vm.IsBusy);
        Assert.True(vm.Pending.IsCompleted);
        // changing the view does NOT reset the zoom
        Assert.False(vm.Zoom.IsFit);
        Assert.Equal(2, vm.Zoom.Zoom);
        Assert.Contains("pozitia R", HistEqReport.ViewCaption(HistEqAnalyzer.Analyze(TestImages.FlatCfa(8, 8, 100), null), "R"));
    }

    /// <summary>
    /// The "False colours" chip colours the one-layer views (Mosaic, a channel) through the
    /// core's table, never the RGB composite, where it is unavailable and the plain picture
    /// stays; the legend is shown while the chip is on. The coloured bitmaps are cached like the
    /// grey ones, the chip survives a new file, and Reset clears it (ADR 0063).
    /// </summary>
    [Fact]
    public async Task R104_TheFalseColoursChipColoursOneLayerViewsAndNotTheComposite()
    {
        using var f = new SessionFixture().WithFlatMaster();
        f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");
        var vm = Attached(f);
        vm.Selected = vm.Files[1];
        await vm.RecomputeAsync();
        var grey = (BitmapSource)vm.Image!;
        Assert.False(vm.View.Rainbow);
        Assert.True(vm.View.RainbowAvailable);
        Assert.Equal("", vm.Legend);

        vm.View.Rainbow = true;
        var mosaic = (BitmapSource)vm.Image!;
        Assert.NotSame(grey, mosaic);
        Assert.Equal(PixelFormats.Rgb24, mosaic.Format);
        Assert.Equal((grey.PixelWidth, grey.PixelHeight), (mosaic.PixelWidth, mosaic.PixelHeight));
        Assert.Equal(HistEqReport.RainbowLegend(), vm.Legend);
        // the first pixel is the table at the grey's first level: one channel 255, one 0
        var px = new byte[3];
        mosaic.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), px, 3, 0);
        Assert.Equal(255, px.Max());
        Assert.Equal(0, px.Min());

        vm.View.Options.First(o => o.Label == "R").IsSelected = true;
        var red = (BitmapSource)vm.Image!;
        Assert.True(vm.View.RainbowAvailable);
        Assert.Equal(PixelFormats.Rgb24, red.Format);
        Assert.Equal((grey.PixelWidth / 2, grey.PixelHeight / 2), (red.PixelWidth, red.PixelHeight));

        vm.View.Options.First(o => o.Label == "RGB").IsSelected = true;
        Assert.False(vm.View.RainbowAvailable);
        Assert.True(vm.View.Rainbow);                 // the choice is kept, only not applied
        ImageSource rgbWhileOn = vm.Image!;
        Assert.Equal(HistEqReport.RainbowLegend(), vm.Legend);
        vm.View.Rainbow = false;
        Assert.Same(rgbWhileOn, vm.Image);            // the plain composite either way
        Assert.Equal("", vm.Legend);
        vm.View.Rainbow = true;

        vm.View.Options.First(o => o.Label == "Mozaic").IsSelected = true;
        Assert.Same(mosaic, vm.Image);                // from the cache
        vm.View.Rainbow = false;
        Assert.Same(grey, vm.Image);

        // a new file keeps the chip; Reset clears it
        vm.View.Rainbow = true;
        vm.Selected = vm.Files[0];
        await vm.RecomputeAsync();
        Assert.True(vm.View.Rainbow);
        Assert.Equal(PixelFormats.Rgb24, ((BitmapSource)vm.Image!).Format);
        vm.Reset();
        Assert.False(vm.View.Rainbow);
        Assert.Equal("", vm.Legend);
    }

    /// <summary>
    /// REQUIREMENT: a click on another file during a computation does not wait - it replaces
    /// it. Whichever of the two runs ends first, the last choice stays on the screen.
    /// </summary>
    [Fact]
    public async Task ASecondPickSupersedesTheFirst()
    {
        using var f = new SessionFixture().WithFlatMaster();
        f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");
        var vm = Attached(f);

        vm.Selected = vm.Files[0];          // starts, not awaited
        Task first = vm.Pending;
        vm.Selected = vm.Files[1];          // over it
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

    // ------------------------------------------------------------------ a file written by another
    // tab

    /// <summary>
    /// Case B: the smoothed flat appears during the session. The list is rebuilt, the choice
    /// stays, nothing is recomputed - the picture is the same object.
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

    /// <summary>The file on the screen was rewritten: the picture is redone, otherwise it would
    /// show bytes that no longer exist.</summary>
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

    /// <summary>The only file the tab cares about is the session's smoothed flat; anything else
    /// moves nothing.</summary>
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
        vm.FileWritten(Path.Combine(f.ProcessDir, "altceva_smoothed.fit"));
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
