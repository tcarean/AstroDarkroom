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
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf.Controls;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The minimap on the WPF test thread (ADR 0032), laid out at the size it gives itself: the
/// thumbnail sits inside a 1-DIP border, so the minimap's own size and position are not the
/// thumbnail's.
/// </summary>
public class MinimapViewTests
{
    [Fact]
    public async Task R042_AClickOnTheThumbnailsCorner_CentresOnTheImagesCorner()
    {
        PointD? centre = null;

        await WpfThread.RunAsync(() =>
        {
            MinimapView minimap = Shown(Fitted(5938, 3888));
            minimap.CentreRequested += (_, point) => centre = point;

            // The minimap's own coordinates, as its mouse handlers read them: (1, 1) is the
            // thumbnail's top-left corner, just inside the border.
            minimap.RequestCentre(new Point(1, 1));
        });

        Assert.NotNull(centre);
        Assert.Equal(0, centre.Value.X, 6);
        Assert.Equal(0, centre.Value.Y, 6);
    }

    [Fact]
    public async Task R041_TheRectangleIsDrawnInsideTheBorder()
    {
        double right = 0;
        double bottom = 0;
        double width = 0;
        double height = 0;

        await WpfThread.RunAsync(() =>
        {
            Viewport viewport = Fitted(5938, 3888);
            viewport.ZoomTo(16, 1000, 600);
            viewport.SetOffset(viewport.MaxOffsetX, viewport.MaxOffsetY);
            MinimapView minimap = Shown(viewport);

            right = Canvas.GetLeft(minimap.ViewportRect) + minimap.ViewportRect.Width;
            bottom = Canvas.GetTop(minimap.ViewportRect) + minimap.ViewportRect.Height;
            width = minimap.Overlay.ActualWidth;
            height = minimap.Overlay.ActualHeight;
        });

        Assert.Equal(188, width, 6);
        Assert.Equal(width, right, 6);
        Assert.Equal(height, bottom, 6);
    }

    /// <summary>A view of an image of this size, 2000 x 1200 physical pixels, at fit.</summary>
    private static Viewport Fitted(double width, double height)
    {
        var viewport = new Viewport();
        viewport.SetContentSize(width, height);
        viewport.SetViewportSize(2000, 1200);
        viewport.ZoomToFit();
        return viewport;
    }

    /// <summary>A minimap showing the view, laid out at the size it gives itself.</summary>
    private static MinimapView Shown(Viewport viewport)
    {
        var minimap = new MinimapView();
        BitmapSource thumbnail = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, new byte[64], 16);
        minimap.Show(thumbnail, viewport.ContentWidth, viewport.ContentHeight, viewport);
        minimap.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        minimap.Arrange(new Rect(minimap.DesiredSize));
        minimap.UpdateLayout();
        return minimap;
    }
}
