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
    public void R024_ZoomOut_StepsDownTheLadder()
    {
        Viewport viewport = Create(4000, 4000, 400, 400);
        viewport.ZoomTo(0.5, 200, 200);

        viewport.ZoomOut(200, 200);

        Assert.Equal(0.33, viewport.Scale, 9);
    }

    [Fact]
    public void R024_ZoomOut_UnderTheLowestStop_GoesToTheFloor_NeverIn()
    {
        // 5938 x 3888 in 91 x 60 physical pixels fits at 0.0153, under the ladder's 0.02.
        Viewport viewport = Create(5938, 3888, 91, 60);
        viewport.ZoomTo(0.018, 45, 30);

        viewport.ZoomOut(45, 30);
        double fromBetween = viewport.Scale;
        viewport.ZoomOut(45, 30);

        Assert.True(viewport.FitScale < ZoomLadder.Steps[0]);
        Assert.Equal(viewport.FitScale, fromBetween, 9);
        Assert.Equal(viewport.FitScale, viewport.Scale, 9);
    }

    [Fact]
    public void R029_WithoutContent_ZoomAndPanAreNoOps()
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
    public void R029_ScreenAndContentSpaceRoundTrip()
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
    public void R023_ZoomTo_HoldsTheAnchoredContentPointUnderTheCursor(double scale, double anchorX, double anchorY)
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
    public void R020_ZoomTo_ClampsAtTheCeiling()
    {
        var viewport = Create();

        viewport.ZoomTo(100, 200, 200);

        Assert.Equal(Viewport.MaxScale, viewport.Scale, 6);
    }

    [Fact]
    public void R020_ZoomTo_WillNotShrinkAnImageBelowTheFitScale()
    {
        var viewport = Create();

        viewport.ZoomTo(0.01, 200, 200);

        Assert.Equal(0.4, viewport.FitScale, 6);
        Assert.Equal(0.4, viewport.Scale, 6);
    }

    [Fact]
    public void R020_ForAnImageSmallerThanTheViewport_TheFloorIsActualSize()
    {
        var viewport = Create(contentWidth: 100, contentHeight: 100);

        Assert.Equal(4.0, viewport.FitScale, 6);
        Assert.Equal(1.0, viewport.MinScale, 6);

        viewport.ZoomTo(0.1, 200, 200);
        Assert.Equal(1.0, viewport.Scale, 6);
    }

    [Fact]
    public void R021_ZoomToFit_ShowsTheWholeImage()
    {
        var viewport = Create(contentWidth: 5794, contentHeight: 3637, viewportWidth: 1200, viewportHeight: 800);

        viewport.ZoomToFit();

        Assert.Equal(1200.0 / 5794, viewport.Scale, 6);
        Assert.True(viewport.VisibleContentWidth >= 5794 - 1e-6);
        Assert.True(viewport.VisibleContentHeight >= 3637 - 1e-6);
    }

    [Fact]
    public void R021_ZoomToFit_CentresTheImageOnTheAxisWithSpareRoom()
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
    public void R022_ZoomToActualSize_GivesOneScreenPixelPerImagePixel()
    {
        var viewport = Create();
        viewport.ZoomToFit();

        viewport.ZoomToActualSize();

        Assert.Equal(1.0, viewport.Scale, 6);
    }

    [Fact]
    public void R021_AnImageSmallerThanTheViewportIsCentred()
    {
        var viewport = Create(contentWidth: 100, contentHeight: 100);
        viewport.ZoomTo(1.0, 200, 200);

        PointD topLeft = viewport.ContentToScreen(0, 0);

        Assert.Equal(150, topLeft.X, 6);
        Assert.Equal(150, topLeft.Y, 6);
    }

    [Fact]
    public void R026_PanBy_MovesTheImageWithTheDrag()
    {
        var viewport = Create();
        viewport.CentreOn(500, 500);
        double before = viewport.OffsetX;

        viewport.PanBy(40, 0);

        Assert.Equal(before - 40, viewport.OffsetX, 6);
    }

    [Fact]
    public void R026_PanBy_StopsAtTheImageEdges()
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
    public void R026_MaxOffsets_DescribeTheScrollableRange()
    {
        var viewport = Create(viewportWidth: 200, viewportHeight: 200);

        Assert.Equal(800, viewport.MaxOffsetX, 6);
        Assert.True(viewport.CanScrollHorizontally);

        viewport.ZoomToFit();

        Assert.Equal(0, viewport.MaxOffsetX, 6);
        Assert.False(viewport.CanScrollHorizontally);
    }

    [Fact]
    public void R026_SetOffset_IsClampedLikeEveryOtherMovement()
    {
        var viewport = Create(viewportWidth: 200, viewportHeight: 200);

        viewport.SetOffset(5000, -5000);

        Assert.Equal(800, viewport.OffsetX, 6);
        Assert.Equal(0, viewport.OffsetY, 6);
    }

    [Fact]
    public void R026_CentreOn_PutsTheRequestedPointInTheMiddle()
    {
        var viewport = Create();
        viewport.ZoomTo(1.0, 200, 200);

        viewport.CentreOn(500, 500);

        PointD centre = viewport.ScreenToContent(200, 200);
        Assert.Equal(500, centre.X, 6);
        Assert.Equal(500, centre.Y, 6);
    }

    [Fact]
    public void R027_ResizingTheWindowHoldsTheCentreOfTheViewStill()
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
    public void R027_GrowingTheWindowRaisesTheFitScaleAndPullsTheZoomUpWithIt()
    {
        var viewport = Create();
        viewport.ZoomToFit();
        Assert.Equal(0.4, viewport.Scale, 6);

        viewport.SetViewportSize(800, 800);

        Assert.Equal(0.8, viewport.FitScale, 6);
        Assert.Equal(0.8, viewport.Scale, 6);
    }

    [Fact]
    public void R028_PixelAt_ReturnsTheImagePixelUnderTheCursor()
    {
        var viewport = Create();
        viewport.ZoomTo(1.0, 200, 200);
        viewport.CentreOn(500, 500);

        Assert.Equal(new ImagePixel(500, 500), viewport.PixelAt(200, 200));
    }

    [Fact]
    public void R028_PixelAt_ReturnsNullOnTheEmptyMarginAroundTheImage()
    {
        var viewport = Create(contentWidth: 100, contentHeight: 100);
        viewport.ZoomTo(1.0, 200, 200);

        // The image occupies 150..250 in a 400 wide viewport, so 10 is on the margin.
        Assert.Null(viewport.PixelAt(10, 10));
        Assert.NotNull(viewport.PixelAt(200, 200));
    }

    [Fact]
    public void R028_PixelAt_AtTheNearEdge_GoesByThePhysicalPixelsCentre()
    {
        var viewport = Create(contentWidth: 100, contentHeight: 100);
        viewport.ZoomTo(1.0, 200, 200);

        // The image starts at 150. A physical pixel whose left edge is at 149.4 has its centre at
        // 149.9, still on the margin; one at 149.6 has its centre at 150.1, on image column 0.
        Assert.Null(viewport.PixelAt(149.4, 200));
        Assert.Equal(new ImagePixel(0, 50), viewport.PixelAt(149.6, 200));
    }

    [Fact]
    public void R028_PixelAt_ExcludesTheFarEdgeBecauseThereIsNoPixelThere()
    {
        var viewport = Create(contentWidth: 100, contentHeight: 100, viewportWidth: 100, viewportHeight: 100);
        viewport.ZoomTo(1.0, 50, 50);

        // The last physical pixel shows the last image pixel; one whose centre is at the image's
        // far edge, or past it, shows none.
        Assert.Equal(new ImagePixel(99, 99), viewport.PixelAt(99, 99));
        Assert.Null(viewport.PixelAt(99.5, 99.5));
        Assert.Null(viewport.PixelAt(100, 100));
    }

    [Fact]
    public void R028_PixelAt_WithoutContentIsNull()
    {
        Assert.Null(new Viewport().PixelAt(10, 10));
    }

    [Fact]
    public void R028_PixelAt_AfterFitThen100Percent_NamesTheRowDrawnOnEveryScreenRow()
    {
        // CR-001 of the review of 2026-11-13: the zoom leaves the vertical offset an ulp under
        // 1456, and a read-out that cut the fraction off named the row above on 593 rows.
        Viewport viewport = Create(5938, 3888, 1920, 976);
        viewport.ZoomToFit();
        viewport.ZoomToActualSize();
        Assert.Equal(2009, viewport.OffsetX, 9);
        Assert.Equal(1456, viewport.OffsetY, 9);

        Assert.Equal(new ImagePixel(2109, 1556), viewport.PixelAt(100, 100));
        int[] wrong = [.. Enumerable.Range(0, 976).Where(row => viewport.PixelAt(100, row) != new ImagePixel(2109, 1456 + row))];
        Assert.Empty(wrong);
    }

    [Fact]
    public void R028_PixelAt_AfterFitThen100Percent_NamesTheRowDrawn_AtEveryEvenHeightFrom900To1100()
    {
        // An even height is one at which the offset is meant to be whole; 15 of the 101 leave it
        // an ulp under. An odd one puts the image half a physical pixel off, which is CR-002's.
        var wrong = new List<string>();
        for (int height = 900; height <= 1100; height += 2)
        {
            Viewport viewport = Create(5938, 3888, 1920, height);
            viewport.ZoomToFit();
            viewport.ZoomToActualSize();
            int top = (int)Math.Round(viewport.OffsetY);
            Assert.Equal(top, viewport.OffsetY, 9);

            int rows = Enumerable.Range(0, height).Count(row => viewport.PixelAt(100, row)?.Y != top + row);
            if (rows > 0)
            {
                wrong.Add($"height {height}: {rows} rows");
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void R028_PixelAt_WithAnOffsetAnUlpUnderAWholeNumber_NamesThePixelDrawn()
    {
        Viewport viewport = Create(5938, 3888, 1920, 976);
        viewport.ZoomTo(1.0, 0, 0);
        viewport.SetOffset(Math.BitDecrement(2009.0), Math.BitDecrement(1456.0));

        Assert.Equal(new ImagePixel(2109, 1556), viewport.PixelAt(100, 100));
    }

    [Fact]
    public void R028_PixelAt_FromFourHundredPercent_NamesThePixelAtThePhysicalPixelsCentre()
    {
        Viewport viewport = Create(1200, 800, 764, 450);
        viewport.ZoomTo(4, 0, 0);
        viewport.SetOffset(300.55, 200.45);

        // Image row 213 starts at screen y (213 - 200.45) x 4 = 50.2: screen row 50, whose centre
        // is at 50.5, is drawn from it, and screen row 49 from row 212.
        Assert.Equal(213, viewport.PixelAt(300, 50)?.Y);
        Assert.Equal(212, viewport.PixelAt(300, 49)?.Y);

        // Image column 301 starts at screen x (301 - 300.55) x 4 = 1.8, in the second half of
        // screen column 1, which is therefore still drawn from column 300.
        Assert.Equal(300, viewport.PixelAt(1, 50)?.X);
        Assert.Equal(301, viewport.PixelAt(2, 50)?.X);
    }

    [Fact]
    public void R029_SetContentSize_RejectsNegativeDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Viewport().SetContentSize(-1, 10));
    }
}
