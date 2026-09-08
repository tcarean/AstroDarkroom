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

public class ViewportTests
{
    private static Viewport Create(
        double contentWidth = 1000,
        double contentHeight = 1000,
        double viewportWidth = 400,
        double viewportHeight = 400)
    {
        var viewport = new Viewport();
        viewport.SetContentSize(contentWidth, contentHeight);
        viewport.SetViewportSize(viewportWidth, viewportHeight);
        return viewport;
    }

    [Fact]
    public void WithoutContent_ZoomAndPanAreNoOps()
    {
        var viewport = new Viewport();

        viewport.ZoomTo(4, 10, 10);
        viewport.PanBy(50, 50);
        viewport.ZoomToFit();

        Assert.False(viewport.HasContent);
        Assert.Equal(1.0, viewport.Scale, 6);
        Assert.Equal(0.0, viewport.OffsetX, 6);
    }

    [Fact]
    public void ScreenAndContentSpaceRoundTrip()
    {
        var viewport = Create();
        viewport.ZoomTo(2.5, 200, 200);

        PointD content = viewport.ScreenToContent(137, 291);
        PointD screen = viewport.ContentToScreen(content.X, content.Y);

        Assert.Equal(137, screen.X, 6);
        Assert.Equal(291, screen.Y, 6);
    }

    [Theory]
    [InlineData(2.0, 100, 100)]
    [InlineData(4.0, 350, 20)]
    [InlineData(0.6, 200, 200)]
    public void ZoomTo_HoldsTheAnchoredContentPointUnderTheCursor(double scale, double anchorX, double anchorY)
    {
        var viewport = Create();
        viewport.CentreOn(500, 500);
        PointD before = viewport.ScreenToContent(anchorX, anchorY);

        viewport.ZoomTo(scale, anchorX, anchorY);

        PointD after = viewport.ScreenToContent(anchorX, anchorY);
        Assert.Equal(before.X, after.X, 6);
        Assert.Equal(before.Y, after.Y, 6);
    }

    [Fact]
    public void ZoomTo_ClampsAtTheCeiling()
    {
        var viewport = Create();

        viewport.ZoomTo(100, 200, 200);

        Assert.Equal(Viewport.MaxScale, viewport.Scale, 6);
    }

    [Fact]
    public void ZoomTo_WillNotShrinkAnImageBelowTheFitScale()
    {
        var viewport = Create();

        viewport.ZoomTo(0.01, 200, 200);

        Assert.Equal(0.4, viewport.FitScale, 6);
        Assert.Equal(0.4, viewport.Scale, 6);
    }

    [Fact]
    public void ForAnImageSmallerThanTheViewport_TheFloorIsActualSize()
    {
        var viewport = Create(contentWidth: 100, contentHeight: 100);

        Assert.Equal(4.0, viewport.FitScale, 6);
        Assert.Equal(1.0, viewport.MinScale, 6);

        viewport.ZoomTo(0.1, 200, 200);
        Assert.Equal(1.0, viewport.Scale, 6);
    }

    [Fact]
    public void ZoomToFit_ShowsTheWholeImage()
    {
        var viewport = Create(contentWidth: 5794, contentHeight: 3637, viewportWidth: 1200, viewportHeight: 800);

        viewport.ZoomToFit();

        Assert.Equal(1200.0 / 5794, viewport.Scale, 6);
        Assert.True(viewport.VisibleContentWidth >= 5794 - 1e-6);
        Assert.True(viewport.VisibleContentHeight >= 3637 - 1e-6);
    }

    [Fact]
    public void ZoomToFit_CentresTheImageOnTheAxisWithSpareRoom()
    {
        var viewport = Create(contentWidth: 5794, contentHeight: 3637, viewportWidth: 1200, viewportHeight: 800);

        viewport.ZoomToFit();

        PointD topLeft = viewport.ContentToScreen(0, 0);
        PointD bottomRight = viewport.ContentToScreen(5794, 3637);
        Assert.Equal(0, topLeft.X, 6);
        Assert.Equal(1200, bottomRight.X, 6);
        Assert.Equal(800 - bottomRight.Y, topLeft.Y, 6);
    }

    [Fact]
    public void ZoomToActualSize_GivesOneScreenPixelPerImagePixel()
    {
        var viewport = Create();
        viewport.ZoomToFit();

        viewport.ZoomToActualSize();

        Assert.Equal(1.0, viewport.Scale, 6);
    }

    [Fact]
    public void AnImageSmallerThanTheViewportIsCentred()
    {
        var viewport = Create(contentWidth: 100, contentHeight: 100);
        viewport.ZoomTo(1.0, 200, 200);

        PointD topLeft = viewport.ContentToScreen(0, 0);

        Assert.Equal(150, topLeft.X, 6);
        Assert.Equal(150, topLeft.Y, 6);
    }

    [Fact]
    public void PanBy_MovesTheImageWithTheDrag()
    {
        var viewport = Create();
        viewport.CentreOn(500, 500);
        double before = viewport.OffsetX;

        viewport.PanBy(40, 0);

        Assert.Equal(before - 40, viewport.OffsetX, 6);
    }

    [Fact]
    public void PanBy_StopsAtTheImageEdges()
    {
        var viewport = Create(viewportWidth: 200, viewportHeight: 200);

        viewport.PanBy(10_000, 10_000);
        Assert.Equal(0, viewport.OffsetX, 6);
        Assert.Equal(0, viewport.OffsetY, 6);

        viewport.PanBy(-10_000, -10_000);
        Assert.Equal(800, viewport.OffsetX, 6);
        Assert.Equal(800, viewport.OffsetY, 6);
    }

    [Fact]
    public void MaxOffsets_DescribeTheScrollableRange()
    {
        var viewport = Create(viewportWidth: 200, viewportHeight: 200);

        Assert.Equal(800, viewport.MaxOffsetX, 6);
        Assert.True(viewport.CanScrollHorizontally);

        viewport.ZoomToFit();

        Assert.Equal(0, viewport.MaxOffsetX, 6);
        Assert.False(viewport.CanScrollHorizontally);
    }

    [Fact]
    public void SetOffset_IsClampedLikeEveryOtherMovement()
    {
        var viewport = Create(viewportWidth: 200, viewportHeight: 200);

        viewport.SetOffset(5000, -5000);

        Assert.Equal(800, viewport.OffsetX, 6);
        Assert.Equal(0, viewport.OffsetY, 6);
    }

    [Fact]
    public void CentreOn_PutsTheRequestedPointInTheMiddle()
    {
        var viewport = Create();
        viewport.ZoomTo(1.0, 200, 200);

        viewport.CentreOn(500, 500);

        PointD centre = viewport.ScreenToContent(200, 200);
        Assert.Equal(500, centre.X, 6);
        Assert.Equal(500, centre.Y, 6);
    }

    [Fact]
    public void ResizingTheWindowHoldsTheCentreOfTheViewStill()
    {
        var viewport = Create();
        viewport.ZoomTo(1.0, 200, 200);
        viewport.CentreOn(300, 300);

        viewport.SetViewportSize(200, 200);

        PointD centre = viewport.ScreenToContent(100, 100);
        Assert.Equal(300, centre.X, 6);
        Assert.Equal(300, centre.Y, 6);
    }

    [Fact]
    public void GrowingTheWindowRaisesTheFitScaleAndPullsTheZoomUpWithIt()
    {
        var viewport = Create();
        viewport.ZoomToFit();
        Assert.Equal(0.4, viewport.Scale, 6);

        viewport.SetViewportSize(800, 800);

        Assert.Equal(0.8, viewport.FitScale, 6);
        Assert.Equal(0.8, viewport.Scale, 6);
    }

    [Fact]
    public void ContentPointInsideAt_ReturnsTheImagePixelUnderTheCursor()
    {
        var viewport = Create();
        viewport.ZoomTo(1.0, 200, 200);
        viewport.CentreOn(500, 500);

        PointD? point = viewport.ContentPointInsideAt(200, 200);

        Assert.NotNull(point);
        Assert.Equal(500, point.Value.X, 6);
        Assert.Equal(500, point.Value.Y, 6);
    }

    [Fact]
    public void ContentPointInsideAt_ReturnsNullOnTheEmptyMarginAroundTheImage()
    {
        var viewport = Create(contentWidth: 100, contentHeight: 100);
        viewport.ZoomTo(1.0, 200, 200);

        // The image occupies 150..250 in a 400 wide viewport, so 10 is on the margin.
        Assert.Null(viewport.ContentPointInsideAt(10, 10));
        Assert.NotNull(viewport.ContentPointInsideAt(200, 200));
    }

    [Fact]
    public void ContentPointInsideAt_ExcludesTheFarEdgeBecauseThereIsNoPixelThere()
    {
        var viewport = Create(contentWidth: 100, contentHeight: 100, viewportWidth: 100, viewportHeight: 100);
        viewport.ZoomTo(1.0, 50, 50);

        Assert.NotNull(viewport.ContentPointInsideAt(99.5, 99.5));
        Assert.Null(viewport.ContentPointInsideAt(100, 100));
    }

    [Fact]
    public void ContentPointInsideAt_WithoutContentIsNull()
    {
        Assert.Null(new Viewport().ContentPointInsideAt(10, 10));
    }

    [Fact]
    public void SetContentSize_RejectsNegativeDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Viewport().SetContentSize(-1, 10));
    }
}
