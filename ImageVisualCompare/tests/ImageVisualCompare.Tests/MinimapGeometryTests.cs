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

using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

public class MinimapGeometryTests
{
    private static Viewport Create(
        double contentWidth = 1000,
        double contentHeight = 1000,
        double viewportWidth = 200,
        double viewportHeight = 200)
    {
        var viewport = new Viewport();
        viewport.SetContentSize(contentWidth, contentHeight);
        viewport.SetViewportSize(viewportWidth, viewportHeight);
        return viewport;
    }

    [Fact]
    public void TheRectangleIsTheVisibleRegionScaledOntoTheThumbnail()
    {
        Viewport viewport = Create();
        viewport.SetOffset(400, 200);

        MinimapLayout layout = MinimapGeometry.Layout(viewport, 100, 100);

        Assert.Equal(40, layout.ViewportRect.X, 6);
        Assert.Equal(20, layout.ViewportRect.Y, 6);
        Assert.Equal(20, layout.ViewportRect.Width, 6);
        Assert.Equal(20, layout.ViewportRect.Height, 6);
        Assert.True(layout.ShowViewportRect);
    }

    [Fact]
    public void TheTwoAxesAreScaledIndependently()
    {
        // A non-square image would hide an x/y mix-up if both scales were equal.
        Viewport viewport = Create(contentWidth: 2000, contentHeight: 1000, viewportWidth: 400, viewportHeight: 200);
        viewport.SetOffset(800, 400);

        MinimapLayout layout = MinimapGeometry.Layout(viewport, 200, 100);

        Assert.Equal(80, layout.ViewportRect.X, 6);
        Assert.Equal(40, layout.ViewportRect.Y, 6);
        Assert.Equal(40, layout.ViewportRect.Width, 6);
        Assert.Equal(20, layout.ViewportRect.Height, 6);
    }

    [Fact]
    public void AtFitTheRectangleCoversTheThumbnailAndIsHidden()
    {
        Viewport viewport = Create();
        viewport.ZoomToFit();

        MinimapLayout layout = MinimapGeometry.Layout(viewport, 100, 100);

        Assert.Equal(0, layout.ViewportRect.X, 6);
        Assert.Equal(100, layout.ViewportRect.Width, 6);
        Assert.False(layout.ShowViewportRect);
    }

    [Fact]
    public void TheRectangleIsClampedWhereTheViewportOverhangsTheImage()
    {
        // A viewport larger than the image leaves a negative offset, which would otherwise
        // draw the rectangle off the edge of the thumbnail.
        Viewport viewport = Create(contentWidth: 100, contentHeight: 100, viewportWidth: 400, viewportHeight: 400);
        viewport.ZoomTo(1.0, 200, 200);

        MinimapLayout layout = MinimapGeometry.Layout(viewport, 80, 80);

        Assert.True(viewport.OffsetX < 0);
        Assert.Equal(0, layout.ViewportRect.X, 6);
        Assert.Equal(80, layout.ViewportRect.Width, 6);
    }

    [Fact]
    public void DeepZoomStillLeavesAVisibleRectangle()
    {
        Viewport viewport = Create(contentWidth: 20000, contentHeight: 20000);
        viewport.ZoomTo(16, 100, 100);

        MinimapLayout layout = MinimapGeometry.Layout(viewport, 100, 100);

        Assert.True(layout.ViewportRect.Width >= 2);
        Assert.True(layout.ViewportRect.Height >= 2);
    }

    [Fact]
    public void WithoutContentThereIsNothingToDraw()
    {
        MinimapLayout layout = MinimapGeometry.Layout(new Viewport(), 100, 100);

        Assert.False(layout.ShowViewportRect);
        Assert.Equal(0, layout.ViewportRect.Width, 6);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(100, 50, 1000, 500)]
    [InlineData(200, 100, 2000, 1000)]
    public void AClickMapsToTheContentPointUnderIt(
        double thumbnailX, double thumbnailY, double expectedX, double expectedY)
    {
        Viewport viewport = Create(contentWidth: 2000, contentHeight: 1000);

        PointD point = MinimapGeometry.ContentPointAt(viewport, thumbnailX, thumbnailY, 200, 100);

        Assert.Equal(expectedX, point.X, 6);
        Assert.Equal(expectedY, point.Y, 6);
    }

    [Fact]
    public void CentringOnAClickPutsThatPointInTheMiddleOfTheView()
    {
        Viewport viewport = Create(contentWidth: 2000, contentHeight: 1000, viewportWidth: 400, viewportHeight: 200);

        PointD target = MinimapGeometry.ContentPointAt(viewport, 100, 50, 200, 100);
        viewport.CentreOn(target.X, target.Y);

        PointD centre = viewport.ScreenToContent(200, 100);
        Assert.Equal(target.X, centre.X, 6);
        Assert.Equal(target.Y, centre.Y, 6);
    }
}
