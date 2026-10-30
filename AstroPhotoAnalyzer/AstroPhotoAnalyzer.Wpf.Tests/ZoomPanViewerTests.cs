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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf.Tests;

/// <summary>
/// R-163: the soft proof's viewer fits its image, zooms around the cursor between the fit and
/// 8:1, pans without letting the image leave the viewport, and fits again on a reset.
/// </summary>
[Collection("fereastra")]
public class ZoomPanViewerTests
{
    readonly StaFixture _sta;

    public ZoomPanViewerTests(StaFixture sta) => _sta = sta;

    /// <summary>A viewer of 400 x 300 over a child of 800 x 600: the fit is 0.5.</summary>
    static ZoomPanViewer Viewer(double childW = 800, double childH = 600)
    {
        var v = new ZoomPanViewer { Child = new Border { Width = childW, Height = childH, Background = Brushes.Gray } };
        v.Measure(new Size(400, 300));
        v.Arrange(new Rect(0, 0, 400, 300));
        v.UpdateLayout();
        return v;
    }

    [Fact]
    public void R163_TheViewerFitsTheImageAndZoomsAroundTheCursor()
        => _sta.Invoke(() =>
        {
            ZoomPanViewer v = Viewer();
            Assert.Equal(0.5, v.FitScale, 9);
            Assert.Equal(0.5, v.Scale, 9);
            Assert.Equal(1, v.Zoom);
            Assert.Equal(new Point(400, 300), v.Centre);
            // At the fit the child's corner is the viewport's corner.
            Assert.Equal(new Point(0, 0), v.ToChild(new Point(0, 0)));
            Assert.Equal(new Point(800, 600), v.ToChild(new Point(400, 300)));

            // Zooming twice around the viewport's corner keeps the child's corner there.
            v.ZoomAt(new Point(0, 0), 2);
            Assert.Equal(2, v.Zoom);
            Assert.Equal(1.0, v.Scale, 9);
            Assert.Equal(new Point(0, 0), v.ToChild(new Point(0, 0)));
            Assert.Equal(new Point(200, 150), v.Centre);

            // Zooming around a point in the middle keeps the child under it.
            Point under = v.ToChild(new Point(100, 100));
            v.ZoomAt(new Point(100, 100), 2);
            Assert.Equal(under.X, v.ToChild(new Point(100, 100)).X, 9);
            Assert.Equal(under.Y, v.ToChild(new Point(100, 100)).Y, 9);

            // Out below the fit is the fit; in above 8:1 is 8:1.
            v.ZoomAt(new Point(200, 150), 0.01);
            Assert.Equal(1, v.Zoom);
            Assert.Equal(new Point(400, 300), v.Centre);
            v.ZoomAt(new Point(200, 150), 1000);
            Assert.Equal(ZoomPanViewer.MaxScale, v.Scale, 9);
            Assert.Equal(ZoomPanViewer.MaxScale / 0.5, v.Zoom, 9);
        });

    [Fact]
    public void R163_TheViewerPansWithoutLettingTheImageLeaveAndResetsToTheFit()
        => _sta.Invoke(() =>
        {
            ZoomPanViewer v = Viewer();
            // At the fit there is nowhere to pan: the image stays centred.
            v.PanBy(50, 50);
            Assert.Equal(new Point(400, 300), v.Centre);

            v.ZoomAt(new Point(200, 150), 4);   // scale 2: the child is 1600 x 1200 on screen
            Assert.Equal(new Point(400, 300), v.Centre);
            v.PanBy(-100, 0);                   // dragging left shows what is to the right
            Assert.Equal(new Point(450, 300), v.Centre);
            v.PanBy(-10000, -10000);            // clamped: the right and bottom edges stay on the viewport's
            Assert.Equal(new Point(800 - 100, 600 - 75), v.Centre);
            v.PanBy(10000, 10000);
            Assert.Equal(new Point(100, 75), v.Centre);

            v.Reset();
            Assert.Equal(1, v.Zoom);
            Assert.Equal(new Point(400, 300), v.Centre);

            // A child smaller than the viewport is enlarged to the fit, and a child of another
            // size is a new image: the whole of it again.
            ZoomPanViewer small = Viewer(100, 100);
            Assert.Equal(3, small.FitScale, 9);
            small.ZoomAt(new Point(0, 0), 2);
            Assert.Equal(2, small.Zoom);
            ((Border)small.Child).Width = 200;
            small.Measure(new Size(400, 300));
            small.Arrange(new Rect(0, 0, 400, 300));
            small.UpdateLayout();
            Assert.Equal(1, small.Zoom);
            Assert.Equal(new Point(100, 50), small.Centre);
        });
}
