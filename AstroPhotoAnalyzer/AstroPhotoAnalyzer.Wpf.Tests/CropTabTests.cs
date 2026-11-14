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

// CropTabTests - the Crop with aspect ratio tab (R-183, ADR 0093): the two sizes, the rectangle
// the core makes of them, its drags through the viewer, the sentences and the save.

using System.Windows;
using System.Windows.Media.Imaging;
using AstroPhotoAnalyzer.Tests;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("fereastra")]
public class CropTabTests
{
    readonly StaFixture _sta;

    public CropTabTests(StaFixture sta) => _sta = sta;

    /// <summary>A frame 400 wide and 300 tall, brighter to the right, so no part of it looks linear.</summary>
    static Raster Frame(int h = 300, int w = 400)
        => SyntheticRasters.From(h, w, 3, (x, y, p) => 0.25f + 0.0008f * x + 0.0002f * y + 0.02f * p);

    /// <summary>The frame as a JPEG, written by WPF's own encoder.</summary>
    static string SaveJpeg(Raster f, string path)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 95 };
        encoder.Frames.Add(BitmapFrame.Create(RasterBitmaps.Region(f, 0, 0, f.W, f.H)));
        using FileStream file = File.Create(path);
        encoder.Save(file);
        return path;
    }

    static AnalysisResult Result(TempWorkspace ws, string? inputName = null)
    {
        Raster f = Frame();
        if (inputName == null) return AnalysisRun.Execute(f, "memorie", Scenes.OnBuiltForPaper());
        string input = inputName.EndsWith(".jpg")
            ? SaveJpeg(f, ws.PathTo(inputName))
            : ws.WriteFile(inputName, TiffTestData.Build16Rgb(f.W, f.H, (x, y, c) => RasterOps.Level16(f.Planes[c][y * f.W + x]), IccTestData.V2("sRGB test")));
        return AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
    }

    static (int, int, int, int) Rect(CropTabViewModel tab) => (tab.CropX, tab.CropY, tab.CropWidth, tab.CropHeight);

    [Fact]
    public void R183_TheTabAsksForTheTwoSizesThenShowsTheRectangleAndItsSentences()
    {
        using var ws = new TempWorkspace();
        var tab = new CropTabViewModel();
        Assert.False(tab.HasCrop);
        Assert.Contains("Scrie latimea si inaltimea ramei", tab.Message);
        Assert.False(tab.IsProblem);
        Assert.False(tab.SaveAs.CanExecute(null));
        Assert.False(tab.Largest.CanExecute(null));

        tab.Populate(Result(ws, "calea-lactee.tif"));
        Assert.NotNull(tab.Image);
        Assert.Equal((400, 300), (tab.ImageWidth, tab.ImageHeight));
        // The image alone, the save off, the sizes still asked for.
        Assert.False(tab.HasCrop);
        Assert.Null(tab.KeptText);
        Assert.Contains("Scrie latimea si inaltimea ramei", tab.Message);
        Assert.False(tab.SaveAs.CanExecute(null));
        Assert.Contains("TIFF pe 16 biti", tab.OutputText);
        Assert.False(tab.OutputIsProblem);

        // One size is not enough; a field that is not a number above 0 is named, in red.
        tab.FrameWidth = "70";
        Assert.False(tab.HasCrop);
        Assert.False(tab.IsProblem);
        tab.FrameHeight = "abc";
        Assert.False(tab.HasCrop);
        Assert.True(tab.IsProblem);
        Assert.Contains("Inaltimea ramei", tab.Message);
        Assert.Contains("'abc'", tab.Message);

        // Both sizes: the largest rectangle of 70 x 54.62 on 400 x 300, centred, with the
        // core's sentences.
        tab.FrameHeight = "54.62";
        Assert.True(tab.HasCrop);
        Assert.Null(tab.Message);
        Assert.False(tab.IsProblem);
        Assert.Equal((8, 0, 384, 300), Rect(tab));
        Assert.Equal("ramane 384 x 300 px, 96.0 % din poza; se taie 8 px in stanga si 8 px in dreapta", tab.KeptText);
        Assert.Equal("pe o rama de 70 x 54.62 cm: 14 pixeli pe inch (ppi)", tab.FrameText);
        Assert.True(tab.SaveAs.CanExecute(null));
        Assert.True(tab.Largest.CanExecute(null));
        Assert.Contains("Tragi din interiorul dreptunghiului", tab.Help);
        Assert.DoesNotContain("raport de aspect", tab.Help + tab.KeptText + tab.FrameText + tab.OutputText);

        // The sizes as typed: the same two numbers the other way round stand taller than wide.
        tab.FrameWidth = "54.62";
        tab.FrameHeight = "70";
        Assert.Equal((83, 0, 234, 300), Rect(tab));

        // A width of 0 takes the rectangle away and names the field.
        tab.FrameWidth = "0";
        Assert.False(tab.HasCrop);
        Assert.Contains("Latimea ramei", tab.Message);
        Assert.Null(tab.KeptText);
        Assert.False(tab.SaveAs.CanExecute(null));
    }

    [Fact]
    public void R183_ADragMovesTheRectangleACornerResizesItAndLargestPutsItBack()
    {
        using var ws = new TempWorkspace();
        var tab = new CropTabViewModel { FrameWidth = "2", FrameHeight = "1" };
        tab.Populate(Result(ws));
        Assert.Equal((0, 50, 400, 200), Rect(tab));

        // Resized from the bottom right corner to half its width: the top left corner stays.
        tab.BeginDrag();
        tab.ResizeTo(CropCorner.BottomRight, 200, 150);
        tab.EndDrag();
        Assert.Equal((0, 50, 200, 100), Rect(tab));
        Assert.StartsWith("ramane 200 x 100 px", tab.KeptText);

        // Moved: the distance is counted from where the drag started, and the image's edges stop it.
        tab.BeginDrag();
        tab.MoveBy(30, 20);
        Assert.Equal((30, 70, 200, 100), Rect(tab));
        tab.MoveBy(45, -10);
        Assert.Equal((45, 40, 200, 100), Rect(tab));
        tab.MoveBy(5000, 5000);
        Assert.Equal((200, 200, 200, 100), Rect(tab));
        tab.EndDrag();
        Assert.Contains("se taie 200 px in stanga si 200 px sus", tab.KeptText);

        // A corner pushed through the opposite one stops at the smallest rectangle.
        tab.BeginDrag();
        tab.ResizeTo(CropCorner.TopLeft, 5000, 5000);
        Assert.Equal((400 - 128, 300 - 64, 128, 64), Rect(tab));

        tab.Largest.Execute(null);
        Assert.Equal((0, 50, 400, 200), Rect(tab));
    }

    [Fact]
    public void R183_TheViewerDrawsTheRectangleAndTurnsThePointerIntoPixelsOfTheImage()
        => _sta.Invoke(() =>
        {
            using var ws = new TempWorkspace();
            var tab = new CropTabViewModel { FrameWidth = "2", FrameHeight = "1" };
            tab.Populate(Result(ws));
            var viewer = new CropViewer
            {
                Source = tab.Image,
                ImageWidth = 400,
                ImageHeight = 300,
                HasCrop = true,
                CropX = 100,
                CropY = 50,
                CropWidth = 200,
                CropHeight = 100,
            };
            // An area of 1000 x 600 over 400 x 300: two DIPs per pixel, the image centred.
            viewer.Measure(new Size(1000, 600));
            viewer.Arrange(new Rect(0, 0, 1000, 600));
            Assert.Equal(new Rect(100, 0, 800, 600), viewer.ImageArea);
            Assert.Equal(2, viewer.PixelSize);
            Assert.Equal(new Rect(300, 100, 400, 200), viewer.CropArea);

            // What a press grabs: a corner near it, the rectangle inside it, nothing outside.
            Assert.Equal(CropViewer.Grip.TopLeft, viewer.HitTestGrip(new Point(300 + CropViewer.HandleGrab, 100 - CropViewer.HandleGrab)));
            Assert.Equal(CropViewer.Grip.TopRight, viewer.HitTestGrip(new Point(700, 100)));
            Assert.Equal(CropViewer.Grip.BottomLeft, viewer.HitTestGrip(new Point(295, 305)));
            Assert.Equal(CropViewer.Grip.BottomRight, viewer.HitTestGrip(new Point(705, 295)));
            Assert.Equal(CropViewer.Grip.Move, viewer.HitTestGrip(new Point(500, 200)));
            Assert.Equal(CropViewer.Grip.None, viewer.HitTestGrip(new Point(300 - CropViewer.HandleGrab - 1, 200)));
            Assert.Equal(CropViewer.Grip.None, viewer.HitTestGrip(new Point(50, 50)));

            int started = 0, ended = 0;
            (int, int)? moved = null;
            (CropCorner, double, double)? pulled = null;
            viewer.DragStarted += () => started++;
            viewer.DragEnded += () => ended++;
            viewer.MoveDragged += (dx, dy) => moved = (dx, dy);
            viewer.CornerDragged += (c, x, y) => pulled = (c, x, y);

            // A press outside starts nothing.
            Assert.False(viewer.BeginDrag(new Point(50, 50)));
            viewer.DragTo(new Point(60, 60));
            Assert.Equal(0, started);
            Assert.Null(moved);

            // A drag inside: the distance, in pixels of the image.
            Assert.True(viewer.BeginDrag(new Point(500, 200)));
            viewer.DragTo(new Point(560, 170));
            Assert.Equal((30, -15), moved);
            Assert.Null(pulled);
            viewer.EndDrag();
            Assert.Equal((1, 1), (started, ended));

            // A drag of a corner grabbed 4 DIPs off it: the corner keeps that distance from the
            // pointer, so a pointer moved by (-60, +40) pulls the corner to (100 - 30, 50 + 20).
            Assert.True(viewer.BeginDrag(new Point(304, 96)));
            viewer.DragTo(new Point(244, 136));
            Assert.Equal((CropCorner.TopLeft, 70.0, 70.0), pulled);
            viewer.EndDrag();
            viewer.DragTo(new Point(0, 0));   // nothing after the end
            Assert.Equal((CropCorner.TopLeft, 70.0, 70.0), pulled);
            Assert.Equal((2, 2), (started, ended));

            // No rectangle: nothing to grab.
            viewer.HasCrop = false;
            Assert.True(viewer.CropArea.IsEmpty);
            Assert.Equal(CropViewer.Grip.None, viewer.HitTestGrip(new Point(500, 200)));
        });

    [Fact]
    public void R183_TheTabRendersAndADragOnItsImageReachesTheCore()
        => _sta.Invoke(() =>
        {
            using var ws = new TempWorkspace();
            using var errors = new WindowSmokeTests.BindingErrors();
            var tab = new CropTabViewModel();
            var view = new CropView { DataContext = tab };
            WindowSmokeTests.Render(view, name: "tab-decupare-fara-marimi");
            tab.Populate(Result(ws, "calea-lactee.tif"));
            tab.FrameWidth = "2";
            tab.FrameHeight = "1";
            WindowSmokeTests.Render(view, name: "tab-decupare");
            Assert.Equal("", errors.Text);

            CropViewer viewer = view.Viewer;
            Assert.Same(tab.Image, viewer.Source);
            Assert.True(viewer.HasCrop);
            Assert.Equal((400, 300), (viewer.ImageWidth, viewer.ImageHeight));
            Assert.Equal((0, 50, 400, 200), (viewer.CropX, viewer.CropY, viewer.CropWidth, viewer.CropHeight));
            // The image fills the area it is given: one of its two sides is the area's.
            Rect image = viewer.ImageArea;
            Assert.True(Math.Abs(image.Width - viewer.RenderSize.Width) < 0.01 || Math.Abs(image.Height - viewer.RenderSize.Height) < 0.01, image.ToString());
            Assert.True(viewer.RenderSize.Width > 1000 && viewer.RenderSize.Height > 500, viewer.RenderSize.ToString());

            // A drag of the bottom right corner to the middle of the rectangle, through the
            // viewer: the core halves it, the top left corner staying.
            Rect crop = viewer.CropArea;
            Assert.True(viewer.BeginDrag(crop.BottomRight));
            viewer.DragTo(new Point(crop.X + crop.Width / 2, crop.Y + crop.Height / 2));
            viewer.EndDrag();
            Assert.Equal((0, 50, 200, 100), Rect(tab));
            view.UpdateLayout();
            Assert.Equal((0, 50, 200, 100), (viewer.CropX, viewer.CropY, viewer.CropWidth, viewer.CropHeight));

            // A drag inside moves it by the pointer's distance, in pixels of the image.
            crop = viewer.CropArea;
            var middle = new Point(crop.X + crop.Width / 2, crop.Y + crop.Height / 2);
            Assert.True(viewer.BeginDrag(middle));
            viewer.DragTo(new Point(middle.X + 40 * viewer.PixelSize, middle.Y + 20 * viewer.PixelSize));
            viewer.EndDrag();
            Assert.Equal((40, 70, 200, 100), Rect(tab));
            WindowSmokeTests.Render(view, name: "tab-decupare-mutat");
            Assert.Equal("", errors.Text);
        });

    [Fact]
    public async Task R183_TheSaveWritesTheCropUnderTheProposedNameAndTheSizesSurviveAnotherImage()
    {
        using var ws = new TempWorkspace();
        var tab = new CropTabViewModel { FrameWidth = "70", FrameHeight = "54.62" };
        tab.Populate(Result(ws, "calea-lactee.tif"));
        Assert.Equal(ws.Dir, tab.InputFolder);
        Assert.Equal(".tif", tab.SaveExtension);
        Assert.Equal("TIFF|*.tif", tab.SaveFilter);

        string? proposed = null;
        tab.SaveRequested += name => proposed = name;
        tab.SaveAs.Execute(null);
        Assert.Equal("calea-lactee_cropped_70x54.62.tif", proposed);

        // On a pool thread, as the window saves: busy meanwhile, the save button off.
        string target = ws.PathTo(proposed!);
        Task saving = tab.SaveAsync(target);
        Assert.Same(saving, tab.Saving);
        await saving;
        Assert.False(tab.IsBusy);
        Assert.True(tab.SaveAs.CanExecute(null));
        Assert.Null(tab.SaveError);
        Assert.Equal("salvat: " + target, tab.SaveMessage);
        Raster back = ImageLoader.Load(target);
        Assert.Equal((384, 300, 16), (back.W, back.H, back.Source.BitDepth));
        Assert.Equal(Frame().Planes[1][8], back.Planes[1][0], 4);   // the rectangle starts 8 columns in

        // Over the input: the core's refusal, in red, the input untouched.
        byte[] before = File.ReadAllBytes(ws.PathTo("calea-lactee.tif"));
        tab.Save(ws.PathTo("calea-lactee.tif"));
        Assert.Null(tab.SaveMessage);
        Assert.Contains("imaginea de intrare", tab.SaveError);
        Assert.Equal(before, File.ReadAllBytes(ws.PathTo("calea-lactee.tif")));

        // Another image: the sizes stay as typed, the rectangle is the new image's largest,
        // the messages of the last save are gone; a JPEG says why it becomes a TIFF.
        tab.BeginDrag();
        tab.MoveBy(3, 0);
        Assert.Equal((11, 0, 384, 300), Rect(tab));
        tab.Populate(Result(ws, "poza.jpg"));
        Assert.Equal(("70", "54.62"), (tab.FrameWidth, tab.FrameHeight));
        Assert.Equal((8, 0, 384, 300), Rect(tab));
        Assert.Null(tab.SaveError);
        Assert.Null(tab.SaveMessage);
        Assert.Contains("JPEG", tab.OutputText);
        Assert.Contains("TIFF pe 8 biti", tab.OutputText);
        Assert.Equal("TIFF|*.tif", tab.SaveFilter);
        tab.SaveAs.Execute(null);
        Assert.Equal("poza_cropped_70x54.62.tif", proposed);
        tab.Save(ws.PathTo(proposed!));
        Assert.Equal(8, ImageLoader.Load(ws.PathTo(proposed!)).Source.BitDepth);

        // No image: nothing to crop, the sizes still there.
        tab.Clear();
        Assert.False(tab.HasCrop);
        Assert.Null(tab.Image);
        Assert.Null(tab.OutputText);
        Assert.False(tab.SaveAs.CanExecute(null));
        Assert.Equal("70", tab.FrameWidth);
    }

    [Fact]
    public void R183_AnInputThatCannotBeCroppedWithoutLossSaysSoAndCannotBeSaved()
    {
        Raster r = Frame();
        r.Source = new SourceInfo { Format = "TIFF", BitDepth = 32, PlanesInFile = 3 };
        var tab = new CropTabViewModel { FrameWidth = "3", FrameHeight = "2" };
        tab.Populate(AnalysisRun.Execute(r, "float.tif", Scenes.OnBuiltForPaper()));
        Assert.True(tab.HasCrop);
        Assert.True(tab.OutputIsProblem);
        Assert.Contains("nu poate fi decupat fara pierderi", tab.OutputText);
        Assert.False(tab.SaveAs.CanExecute(null));
    }

    [Fact]
    public void R183_AFinerCopyIsMadeWhenTheAreaAllowsItAndTheRectangleStaysInImagePixels()
    {
        using var ws = new TempWorkspace();
        var tab = new CropTabViewModel { FrameWidth = "2", FrameHeight = "1" };
        tab.SetArea(100, 75);
        tab.Populate(Result(ws));
        Assert.Equal(100, tab.Image.PixelWidth);   // every fourth pixel covers an area of 100 x 75
        (int, int, int, int) rect = Rect(tab);
        Assert.Equal((0, 50, 400, 200), rect);

        tab.SetArea(50, 40);                        // a smaller area asks for nothing finer
        Assert.Equal(100, tab.Image.PixelWidth);
        tab.SetArea(400, 300);
        Assert.Equal(400, tab.Image.PixelWidth);
        Assert.Equal(rect, Rect(tab));
        Assert.Equal((400, 300), (tab.ImageWidth, tab.ImageHeight));
    }
}
