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
/// The "Histogram" tab. The required behaviour: the list with the master first and the frames
/// from flats/ after it; a click computes, another click recomputes, and a click during a
/// computation replaces it instead of waiting. No figures are checked here - there are none -
/// but that the picture really changes with the file and with the view.
/// </summary>
public class HistEqTabTests
{
    // Every test that picks a file awaits Pending, the run the pick started (ADR 0087). The tests
    // used to start a second run with RecomputeAsync, now private, which superseded the first;
    // a superseded run that had already begun computing goes on until HistEqRun.Execute
    // returns, and a test that looked at Pending failed 16 times in 150 in a loop (2026-11-02).

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
        Assert.Equal("process/", vm.Files[0].Where);
        Assert.Equal(f.Session.FlatMasterPath, vm.Files[0].Path);
        Assert.All(vm.Files.Skip(1), r =>
        {
            Assert.False(r.IsMaster);
            Assert.Equal("", r.Tag);
            Assert.False(r.HasTag);
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
        await vm.Pending;

        Assert.True(vm.HasResult);
        Assert.NotNull(vm.Image);
        Assert.Contains("128 x 128", vm.Frame);
        Assert.NotEmpty(vm.MasterNote);
        Assert.Contains("Masterul e media", vm.MasterNote);
        Assert.Contains("EGALIZARE DE HISTOGRAMA", vm.Report);
        Assert.Contains(SirilSession.FlatMasterName, vm.Report);
        Assert.NotEmpty(vm.Caveat);
        // a mono master: a single view, so no chips
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
        await vm.Pending;
        ImageSource master = vm.Image!;
        vm.Zoom.Manual(3);

        vm.Selected = vm.Files[1];
        await vm.Pending;

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
        await vm.Pending;

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
        await vm.Pending;
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
        Assert.Contains("canalul R", HistEqReport.ViewCaption(HistEqAnalyzer.Analyze(TestImages.FlatCfa(8, 8, 100), null), "R"));
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
        await vm.Pending;
        var grey = (BitmapSource)vm.Image!;
        string report = vm.Report;
        Assert.False(vm.View.Rainbow);
        Assert.True(vm.View.RainbowAvailable);
        Assert.Equal("", vm.Legend);
        Assert.DoesNotContain("culori false", report);

        vm.View.Rainbow = true;
        var mosaic = (BitmapSource)vm.Image!;
        Assert.NotSame(grey, mosaic);
        Assert.Equal(PixelFormats.Rgb24, mosaic.Format);
        Assert.Equal((grey.PixelWidth, grey.PixelHeight), (mosaic.PixelWidth, mosaic.PixelHeight));
        Assert.Equal(HistEqReport.RainbowLegend(), vm.Legend);
        // the report panel says what is on screen: the console's --rainbow text, with the
        // file's name, the display line and the legend, over the same banner
        Assert.Contains("culori false", vm.Report);
        Assert.Contains("LEGENDA: " + HistEqReport.RainbowLegend(), vm.Report);
        Assert.Contains("flat.fit", vm.Report);
        Assert.StartsWith(report.Split('\n')[0], vm.Report);
        string colouredReport = vm.Report;
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
        Assert.Equal(HistEqReport.RainbowNotOnRgb(), vm.Legend);   // not the table's legend: nothing is coloured
        Assert.Same(report, vm.Report);               // and the report is the plain run's
        vm.View.Rainbow = false;
        Assert.Same(rgbWhileOn, vm.Image);            // the plain composite either way
        Assert.Equal("", vm.Legend);
        vm.View.Rainbow = true;

        vm.View.Options.First(o => o.Label == "Mozaic").IsSelected = true;
        Assert.Same(mosaic, vm.Image);                // from the cache
        Assert.Same(colouredReport, vm.Report);       // built once
        vm.View.Rainbow = false;
        Assert.Same(grey, vm.Image);
        Assert.Same(report, vm.Report);

        // a new file keeps the chip; Reset clears it
        vm.View.Rainbow = true;
        vm.Selected = vm.Files[0];
        await vm.Pending;
        Assert.True(vm.View.Rainbow);
        Assert.Equal(PixelFormats.Rgb24, ((BitmapSource)vm.Image!).Format);
        vm.Reset();
        Assert.False(vm.View.Rainbow);
        Assert.Equal("", vm.Legend);
    }

    /// <summary>
    /// On a mono frame the single view is one layer: the chip is available, colours the frame
    /// through the core's table (the channel, not the grey-three-times composite) and shows
    /// the legend; the report rebuilt for it names the file (ADR 0063).
    /// </summary>
    [Fact]
    public void R104_OnAMonoFrameTheChipColoursTheSingleView()
    {
        var mono = HistEqAnalyzer.Analyze(TestImages.From(64, 48, FitsTestData.Radial(64, 48)), null);
        var vm = new HistEqTabViewModel();
        vm.Populate(mono, HistEqKind.Frame, "mono.fit");
        Assert.True(vm.View.RainbowAvailable);
        Assert.False(vm.View.IsVisible);              // one chip, no view row; the checkbox stays

        vm.View.Rainbow = true;
        var coloured = (BitmapSource)vm.Image!;
        Assert.Equal(PixelFormats.Rgb24, coloured.Format);
        Assert.Equal((64, 48), (coloured.PixelWidth, coloured.PixelHeight));
        byte[] expected = RainbowLut.Colour(mono.Channels[0].Full);
        var px = new byte[64 * 48 * 3];
        coloured.CopyPixels(px, 64 * 3, 0);
        Assert.Equal(expected, px);
        Assert.Equal(HistEqReport.RainbowLegend(), vm.Legend);
        Assert.Contains("fisier:     mono.fit", vm.Report);
        Assert.Contains("culori false", vm.Report);

        vm.View.Rainbow = false;
        Assert.Equal(PixelFormats.Rgb24, ((BitmapSource)vm.Image!).Format);   // the plain composite, grey three times
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
    /// A pick made while another heavy run holds the gate - another tab's run reading the very
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
        IDisposable other = await gate.EnterAsync(CancellationToken.None);
        var vm = new HistEqTabViewModel();
        vm.Attach(f.Session, CancellationToken.None, gate);

        vm.Selected = vm.Files[0];          // the master: waits
        Task first = vm.Pending;
        Assert.NotSame(first, await Task.WhenAny(first, Task.Delay(1000)));
        Assert.True(vm.IsBusy);
        Assert.Equal(SessionMessages.WaitingForRunningAnalysis(), vm.BusyMessage);

        vm.Selected = vm.Files[1];          // over it: the frame
        Task second = vm.Pending;
        await first;                        // left the queue
        Assert.False(second.IsCompleted);
        Assert.False(vm.HasResult);

        other.Dispose();
        await second;

        Assert.True(vm.HasResult);
        Assert.False(vm.IsBusy);
        Assert.Equal("", vm.MasterNote);
        Assert.Contains("flat.fit", vm.Report);
        Assert.False(gate.IsHeld);
    }

    [Fact]
    public async Task ResetClearsEverything()
    {
        using var f = new SessionFixture().WithFlatMaster();
        var vm = Attached(f);
        vm.Selected = vm.Files[0];
        await vm.Pending;

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
