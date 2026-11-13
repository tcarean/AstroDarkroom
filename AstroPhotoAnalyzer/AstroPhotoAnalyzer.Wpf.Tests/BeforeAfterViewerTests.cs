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

// BeforeAfterViewerTests - the comparison of the Culori pentru hartie tab (R-179, ADR 0090):
// the original left of the line and the variant right of it, whatever the zoom and the pan; the
// labels beside the line; the line back in the middle with a new image.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("fereastra")]
public class BeforeAfterViewerTests
{
    const byte BeforeLevel = 40, AfterLevel = 200;

    readonly StaFixture _sta;

    public BeforeAfterViewerTests(StaFixture sta) => _sta = sta;

    /// <summary>A flat grey image.</summary>
    static BitmapSource Image(int w, int h, byte level)
    {
        var px = new byte[w * h * 3];
        Array.Fill(px, level);
        var b = BitmapSource.Create(w, h, 96, 96, PixelFormats.Rgb24, null, px, w * 3);
        b.Freeze();
        return b;
    }

    static void Layout(FrameworkElement e, double w, double h)
    {
        e.Measure(new Size(w, h));
        e.Arrange(new Rect(0, 0, w, h));
        e.UpdateLayout();
    }

    /// <summary>An area of 400 x 300 over two images of 800 x 600: the fit is 0.5.</summary>
    static BeforeAfterViewer Comparison(int imageW = 800, int imageH = 600)
    {
        var v = new BeforeAfterViewer
        {
            Before = Image(imageW, imageH, BeforeLevel),
            After = Image(imageW, imageH, AfterLevel),
            ContentWidth = imageW,
            ContentHeight = imageH,
            BeforeText = "Before",
            AfterText = "After",
        };
        Layout(v, 400, 300);
        return v;
    }

    /// <summary>
    /// The red sample of the rendered control at a point of its area; the tests sample at
    /// y = 100, clear of the labels at the top and of the handle at mid height.
    /// </summary>
    static byte Seen(BeforeAfterViewer v, int x, int y)
    {
        // What a window does by itself before it draws: the layout a moved line asked for.
        v.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)v.ActualWidth, (int)v.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(v);
        var px = new byte[4];
        bmp.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return px[2];
    }

    static double ClipRight(BeforeAfterViewer v) => ((RectangleGeometry)v.BeforeLayer.Clip).Rect.Right;

    [Fact]
    public void R179_TheOriginalIsCutAtTheLineWhateverTheZoomAndThePan()
        => _sta.Invoke(() =>
        {
            BeforeAfterViewer v = Comparison();
            Assert.Equal(0.5, v.DividerPosition);
            // The line at x = 200 of the area: the copy's columns 0 to 400 are the original's.
            Assert.Equal(400, ClipRight(v), 9);
            Assert.Equal(BeforeLevel, Seen(v, 50, 100));
            Assert.Equal(AfterLevel, Seen(v, 350, 100));

            // A zoom of 2 around the area's corner: the line stays, the columns under it change.
            v.Viewer.ZoomAt(new Point(0, 0), 2);
            Assert.Equal(0.5, v.DividerPosition);
            Assert.Equal(200, ClipRight(v), 9);
            Assert.Equal(BeforeLevel, Seen(v, 190, 100));
            Assert.Equal(AfterLevel, Seen(v, 210, 100));

            // The line dragged: the cut follows it and the view does not move.
            Point centre = v.Viewer.Centre;
            v.MoveDividerTo(100);
            Assert.Equal(0.25, v.DividerPosition);
            Assert.Equal(100, ClipRight(v), 9);
            Assert.Equal(centre, v.Viewer.Centre);
            Assert.Equal(BeforeLevel, Seen(v, 90, 100));
            Assert.Equal(AfterLevel, Seen(v, 110, 100));

            // A pan moves both images under the line.
            v.Viewer.PanBy(-50, 0);
            Assert.Equal(0.25, v.DividerPosition);
            Assert.Equal(150, ClipRight(v), 9);

            // Back at the fit, and on a larger area: the line keeps its fraction of the width.
            v.Viewer.Reset();
            Assert.Equal(200, ClipRight(v), 9);
            Layout(v, 800, 600);
            Assert.Equal(0.25, v.DividerPosition);
            Assert.Equal(200, ClipRight(v), 9);
            Assert.Equal(BeforeLevel, Seen(v, 190, 100));
            Assert.Equal(AfterLevel, Seen(v, 210, 100));

            // An area the image does not fill (800 x 300: the image is 400 wide, in the middle):
            // the line at a quarter of the width is on the image's left edge, and in the middle
            // of the width it cuts the image in two.
            Layout(v, 800, 300);
            Assert.Equal(0, ClipRight(v), 9);
            Assert.Equal(AfterLevel, Seen(v, 220, 100));
            v.MoveDividerTo(400);
            Assert.Equal(400, ClipRight(v), 9);
            Assert.Equal(BeforeLevel, Seen(v, 380, 100));
            Assert.Equal(AfterLevel, Seen(v, 420, 100));
            v.MoveDividerTo(200);
            Layout(v, 800, 600);

            // At the edges only one of the two is seen.
            v.MoveDividerTo(-30);
            Assert.Equal(0, v.DividerPosition);
            Assert.Equal(AfterLevel, Seen(v, 5, 100));
            v.MoveDividerTo(5000);
            Assert.Equal(1, v.DividerPosition);
            Assert.Equal(BeforeLevel, Seen(v, 795, 100));
        });

    /// <summary>Where an element of the control is drawn: its top left corner, in the control's area.</summary>
    static Point At(FrameworkElement e, BeforeAfterViewer v)
    {
        v.UpdateLayout();
        return e.TranslatePoint(new Point(0, 0), v);
    }

    [Fact]
    public void R179_TheLabelsStayBesideTheLineAndHideWhereThereIsNoRoom()
        => _sta.Invoke(() =>
        {
            BeforeAfterViewer v = Comparison();
            Assert.Equal("Before", ((TextBlock)v.BeforeLabel.Child).Text);
            Assert.Equal("After", ((TextBlock)v.AfterLabel.Child).Text);
            Assert.Equal(Visibility.Visible, v.BeforeLabel.Visibility);
            Assert.Equal(Visibility.Visible, v.AfterLabel.Visibility);
            Assert.True(v.BeforeLabel.ActualWidth > 20);

            // The line at 200: Before ends the gap left of it, After starts the gap right of it,
            // both the gap under the top.
            Assert.Equal(200 - SplitDivider.LabelGap, At(v.BeforeLabel, v).X + v.BeforeLabel.ActualWidth, 6);
            Assert.Equal(200 + SplitDivider.LabelGap, At(v.AfterLabel, v).X, 6);
            Assert.Equal(SplitDivider.LabelGap, At(v.BeforeLabel, v).Y, 6);
            Assert.Equal(SplitDivider.LabelGap, At(v.AfterLabel, v).Y, 6);

            // They move with it.
            v.MoveDividerTo(260);
            Assert.Equal(270, At(v.AfterLabel, v).X, 6);
            Assert.Equal(250, At(v.BeforeLabel, v).X + v.BeforeLabel.ActualWidth, 6);

            // A side too narrow for its label shows none.
            v.MoveDividerTo(15);
            Assert.NotEqual(Visibility.Visible, v.BeforeLabel.Visibility);
            Assert.Equal(Visibility.Visible, v.AfterLabel.Visibility);
            v.MoveDividerTo(390);
            Assert.Equal(Visibility.Visible, v.BeforeLabel.Visibility);
            Assert.NotEqual(Visibility.Visible, v.AfterLabel.Visibility);
        });

    [Fact]
    public void R179_TheLineRunsTheWholeHeightWithItsHandleInTheMiddleWhateverTheAreaBecomes()
        => _sta.Invoke(() =>
        {
            BeforeAfterViewer v = Comparison();
            Assert.Equal(new Point(200 - SplitDivider.LineWidth / 2, 0), At(v.Line, v));
            Assert.Equal(SplitDivider.LineWidth, v.Line.ActualWidth, 6);
            Assert.Equal(300, v.Line.ActualHeight, 6);
            Assert.Equal(new Point(187, 122), At(v.Handle, v));
            Assert.Equal(SplitDivider.HandleWidth, v.Handle.ActualWidth, 6);
            Assert.Equal(SplitDivider.HandleHeight, v.Handle.ActualHeight, 6);

            // An area that changes size after the first layout, as a tab's does when the texts
            // under it settle: the line still starts at the top and ends at the bottom.
            Layout(v, 400, 200);
            Assert.Equal(new Point(199, 0), At(v.Line, v));
            Assert.Equal(200, v.Line.ActualHeight, 6);
            Assert.Equal(new Point(187, 72), At(v.Handle, v));
            Layout(v, 600, 500);
            Assert.Equal(new Point(299, 0), At(v.Line, v));
            Assert.Equal(500, v.Line.ActualHeight, 6);
            Assert.Equal(new Point(287, 222), At(v.Handle, v));
            Assert.Equal(SplitDivider.LabelGap, At(v.AfterLabel, v).Y, 6);

            // And it follows a drag.
            v.MoveDividerTo(150);
            Assert.Equal(new Point(149, 0), At(v.Line, v));
            Assert.Equal(new Point(137, 222), At(v.Handle, v));
        });

    [Fact]
    public void R179_ANewImagePutsTheLineBackInTheMiddleAndNoImageShowsNoLine()
        => _sta.Invoke(() =>
        {
            BeforeAfterViewer v = Comparison();
            Assert.Equal(Visibility.Visible, v.Overlay.Visibility);
            v.MoveDividerTo(100);

            // Another variant of the same image keeps the line: only After changed.
            v.After = Image(800, 600, 120);
            Layout(v, 400, 300);
            Assert.Equal(0.25, v.DividerPosition);

            // The gallery emptied (another image opened): nothing to compare, the line goes home.
            v.Before = null;
            v.After = null;
            Layout(v, 400, 300);
            Assert.NotEqual(Visibility.Visible, v.Overlay.Visibility);
            Assert.Equal(0.5, v.DividerPosition);

            v.Before = Image(800, 600, BeforeLevel);
            v.After = Image(800, 600, AfterLevel);
            Layout(v, 400, 300);
            Assert.Equal(Visibility.Visible, v.Overlay.Visibility);
            Assert.Equal(0.5, v.DividerPosition);
            Assert.Equal(400, ClipRight(v), 9);
        });

    [Fact]
    public void R179_APressOnTheLineDragsItFromWhereItWasGrabbedAndOneElsewhereIsTheImages()
        => _sta.Invoke(() =>
        {
            BeforeAfterViewer v = Comparison();   // the line at x = 200

            // A press on the image is not the line's: nothing is dragged.
            Assert.False(v.BeginDividerDrag(new Point(100, 150)));
            v.DragDividerTo(300);
            Assert.Equal(0.5, v.DividerPosition);

            // A press 6 DIPs right of the line grabs it there: the line keeps that distance
            // from the pointer instead of jumping under it.
            Assert.True(v.BeginDividerDrag(new Point(206, 20)));
            Assert.Equal(0.5, v.DividerPosition);
            v.DragDividerTo(306);
            Assert.Equal(0.75, v.DividerPosition, 9);
            Assert.Equal(600, ClipRight(v), 9);
            v.DragDividerTo(5000);
            Assert.Equal(1, v.DividerPosition);
            v.DragDividerTo(106);
            Assert.Equal(0.25, v.DividerPosition, 9);

            // Released, the pointer moves the line no more.
            v.EndDividerDrag();
            v.DragDividerTo(306);
            Assert.Equal(0.25, v.DividerPosition, 9);

            // The handle is a wider grip, at mid height.
            Assert.True(v.BeginDividerDrag(new Point(100 - 12, 150)));
            v.DragDividerTo(200 - 12);
            Assert.Equal(0.5, v.DividerPosition, 9);
            v.EndDividerDrag();

            // With nothing to compare there is no line to grab.
            v.After = null;
            Assert.False(v.BeginDividerDrag(new Point(200, 150)));
        });

    [Fact]
    public void R179_EachLayerIsCutAtTheLineWithTheImageUnderIt()
        => _sta.Invoke(() =>
        {
            const byte beforeLayer = 250, afterLayer = 100;
            BeforeAfterViewer v = Comparison();
            v.BeforeOverlay = Image(800, 600, beforeLayer);
            v.AfterOverlay = Image(800, 600, afterLayer);
            // Each side shows its own layer, over its own image, up to the line.
            Assert.Equal(beforeLayer, Seen(v, 150, 100));
            Assert.Equal(afterLayer, Seen(v, 250, 100));
            v.MoveDividerTo(300);
            Assert.Equal(beforeLayer, Seen(v, 250, 100));
            Assert.Equal(afterLayer, Seen(v, 350, 100));

            // A layer taken away on one side leaves the other side's alone.
            v.BeforeOverlay = null;
            Assert.Equal(BeforeLevel, Seen(v, 250, 100));
            Assert.Equal(afterLayer, Seen(v, 350, 100));
            v.AfterOverlay = null;
            Assert.Equal(AfterLevel, Seen(v, 350, 100));
        });

    [Fact]
    public void R179_AnImageSmallerThanTheAreaKeepsItsSizeUnlessToldToFill()
        => _sta.Invoke(() =>
        {
            BeforeAfterViewer v = Comparison(200, 150);
            Assert.True(v.FitEnlarges);   // the default is the viewer's
            Assert.Equal(2, v.Viewer.Scale, 9);

            v.FitEnlarges = false;
            Layout(v, 400, 300);
            Assert.Equal(1, v.Viewer.Scale, 9);
            // Centred at its own size: the line in the middle cuts the image in the middle.
            Assert.Equal(100, ClipRight(v), 9);
            Assert.Equal(BeforeLevel, Seen(v, 150, 150));
            Assert.Equal(AfterLevel, Seen(v, 250, 150));
        });
}
