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
    public void R022_AtOneHundredPercent_TheImageIsDrawnOnWholePhysicalPixels()
    {
        // CR-002 of the review of 2026-11-13: on a surface of odd height the zoom leaves the
        // image half a physical pixel off, and every screen row would be two image rows mixed.
        Viewport viewport = Create(5938, 3888, 1920, 947);
        viewport.ZoomToFit();
        viewport.ZoomToActualSize();

        Assert.Equal(2009, viewport.OffsetX, 9);
        Assert.Equal(1470.5, viewport.OffsetY, 9);
        Assert.Equal(2009, viewport.DrawnOffsetX);
        Assert.Equal(1471, viewport.DrawnOffsetY);
    }

    [Fact]
    public void R022_AtOneHundredPercent_OnASurfaceOfFractionalHeight_TheImageIsDrawnOnWholePhysicalPixels()
    {
        Viewport viewport = Create(5938, 3888, 1920, 946.37);
        viewport.ZoomToFit();
        viewport.ZoomToActualSize();

        Assert.Equal(1470.815, viewport.OffsetY, 9);
        Assert.Equal(1471, viewport.DrawnOffsetY);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    public void R022_AtAWholeScale_TheDrawnOffsetIsTheNearestWholePhysicalPixel(double scale)
    {
        Viewport viewport = Create(5938, 3888, 1920, 947);
        viewport.ZoomTo(scale, 960, 473.5);
        viewport.SetOffset(1000.3, 700.7);

        Assert.Equal(1000.3, viewport.OffsetX);
        Assert.Equal(700.7, viewport.OffsetY);
        Assert.Equal(Math.Floor((1000.3 * scale) + 0.5), viewport.DrawnOffsetX * scale);
        Assert.Equal(Math.Floor((700.7 * scale) + 0.5), viewport.DrawnOffsetY * scale);
    }

    [Theory]
    [InlineData(0.67)]
    [InlineData(1.5)]
    [InlineData(4.6)]
    public void R022_AtAScaleThatIsNotWhole_TheImageIsDrawnAtItsOffset(double scale)
    {
        Viewport viewport = Create(5938, 3888, 1920, 947);
        viewport.ZoomTo(scale, 960, 473.5);
        viewport.SetOffset(1000.3, 700.7);

        Assert.Equal(1000.3, viewport.DrawnOffsetX);
        Assert.Equal(700.7, viewport.DrawnOffsetY);
    }

    [Fact]
    public void R022_AnImageSmallerThanTheView_IsDrawnOnWholePixels_WithMarginsOnePixelApart()
    {
        var viewport = Create(contentWidth: 100, contentHeight: 100, viewportWidth: 401, viewportHeight: 401);
        viewport.ZoomTo(1.0, 200, 200);

        // 301 pixels of margin cannot be halved: the pan says 150.5 either side, the image is
        // drawn with 150 before it and 151 after.
        Assert.Equal(-150.5, viewport.OffsetX, 9);
        Assert.Equal(-150, viewport.DrawnOffsetX);
        Assert.Equal(-150, viewport.DrawnOffsetY);
    }

    [Theory]
    [InlineData(400.4, 599)]
    [InlineData(400.6, 599)]
    [InlineData(400, 600)]
    public void R022_AtTheEndOfTheRange_TheImageIsNotDrawnPastItsEdge(double viewportSize, double drawn)
    {
        var viewport = Create(viewportWidth: viewportSize, viewportHeight: viewportSize);
        viewport.ZoomTo(1.0, 0, 0);
        viewport.SetOffset(5000, 5000);

        // A view 400.4 wide ends 599.6 pixels in: the nearest whole pixel, 600, would leave a
        // strip past the image's edge, so it is drawn at 599.
        Assert.Equal(1000 - viewportSize, viewport.OffsetX, 9);
        Assert.Equal(drawn, viewport.DrawnOffsetX);
        Assert.True(viewport.DrawnOffsetX <= viewport.MaxOffsetX);
    }

    [Fact]
    public void R022_AtTheEndOfTheRange_AnEndALastBitUnderAWholePixel_IsThatPixel()
    {
        // A view the layout left an ulp over 1921 pixels ends 1078.9999999999998 pixels into a
        // 3000 pixel image: the image's last column must still be reachable.
        var viewport = Create(contentWidth: 3000, viewportWidth: 1921.0000000000002);
        viewport.ZoomTo(1.0, 0, 0);
        viewport.SetOffset(5000, 0);

        Assert.Equal(1079, viewport.OffsetX, 9);
        Assert.Equal(1079, viewport.DrawnOffsetX);
    }

    [Fact]
    public void R022_AFitALastBitUnderOneHundredPercent_StaysTheFit()
    {
        // Taken as 100% the image would be wider than the view by that last bit, and a
        // scrollbar would show at Fit.
        var viewport = Create(contentWidth: 1921, contentHeight: 1000, viewportWidth: 1920.9999999999998, viewportHeight: 2000);
        viewport.ZoomToFit();

        Assert.True(viewport.FitScale < 1);
        Assert.Equal(viewport.FitScale, viewport.Scale);
        Assert.False(viewport.CanScrollHorizontally);
    }

    [Fact]
    public void R022_AFitALastBitUnderOneHundredPercent_IsStillDrawnOnWholePixels()
    {
        // The scale stays the fit, 0.9999999999999999, and reads 100%: the 1001 pixels of
        // margin above and below must not put the image half a pixel off for that last bit.
        var viewport = Create(contentWidth: 1921, contentHeight: 1000, viewportWidth: 1920.9999999999998, viewportHeight: 2001);
        viewport.ZoomToFit();

        Assert.True(viewport.Scale < 1);
        Assert.Equal(-500.5, viewport.OffsetY, 9);
        Assert.Equal(-500, viewport.DrawnOffsetY * viewport.Scale, 9);
        Assert.Equal(0, viewport.DrawnOffsetX * viewport.Scale, 9);
    }

    [Fact]
    public void R022_AFitALastBitOverOneHundredPercent_IsOneHundredPercent()
    {
        var viewport = Create(contentWidth: 1920, contentHeight: 1000, viewportWidth: 1920.0000000000002, viewportHeight: 2000);
        viewport.ZoomToFit();

        Assert.True(viewport.FitScale > 1);
        Assert.Equal(1.0, viewport.Scale);
        Assert.False(viewport.CanScrollHorizontally);
    }

    [Fact]
    public void R027_ResizingOnePixelAtATime_AtOneHundredPercent_StillHoldsTheCentre()
    {
        // The pan keeps its half pixel, so twenty resizes do not add up to a shift (R-022).
        var viewport = Create();
        viewport.ZoomTo(1.0, 200, 200);
        viewport.CentreOn(500, 500);

        for (int width = 401; width <= 420; width++)
        {
            viewport.SetViewportSize(width, 400);
        }

        Assert.Equal(500, viewport.ScreenToContent(210, 200).X, 9);
        Assert.Equal(290, viewport.OffsetX, 9);
        Assert.Equal(290, viewport.DrawnOffsetX);
    }

    [Fact]
    public void R022_AScaleALastBitFromWhole_IsWhole()
    {
        // Four wheel notches in and four out give 1.0000000000000002 in doubles.
        var viewport = Create();
        viewport.ZoomTo(1.0, 200, 200);
        for (int notch = 0; notch < 4; notch++)
        {
            viewport.ZoomBy(1.15, 137, 291);
        }

        for (int notch = 0; notch < 4; notch++)
        {
            viewport.ZoomBy(1 / 1.15, 137, 291);
        }

        Assert.Equal(1.0, viewport.Scale);

        viewport.ZoomTo(3 - 1e-12, 200, 200);
        Assert.Equal(3.0, viewport.Scale);

        viewport.ZoomTo(1.001, 200, 200);
        Assert.Equal(1.001, viewport.Scale);
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

        // A centre exactly on the image's near edge is on the edge between the margin and
        // column 0, and an edge belongs to what is before it: the margin.
        Assert.Null(viewport.PixelAt(149.5, 200));
        Assert.Equal(new ImagePixel(0, 50), viewport.PixelAt(149.5 + (1.0 / 1024), 200));
    }

    [Fact]
    public void R028_PixelAt_AtTheFarEdge_ReadsTheLastPixel_AndNothingPastIt()
    {
        var viewport = Create(contentWidth: 100, contentHeight: 100, viewportWidth: 100, viewportHeight: 100);
        viewport.ZoomTo(1.0, 50, 50);

        // A physical pixel whose centre is exactly on the image's far edge still shows the last
        // image pixel, since an edge belongs to the pixel before it; one further out shows none.
        Assert.Equal(new ImagePixel(99, 99), viewport.PixelAt(99, 99));
        Assert.Equal(new ImagePixel(99, 99), viewport.PixelAt(99.5, 99.5));
        Assert.Null(viewport.PixelAt(99.5 + (1.0 / 1024), 99.5 + (1.0 / 1024)));
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
    public void R028_PixelAt_AfterFitThen100Percent_AtEveryOddHeightFrom901To1099_SkipsNoRow()
    {
        // An odd height leaves the pan half a physical pixel off, at 14 of the 100 heights an
        // ulp under its half. The image is drawn on whole pixels all the same (R-022), and the
        // rows read are the rows drawn, one after the other, none twice and none skipped.
        var wrong = new List<string>();
        for (int height = 901; height <= 1099; height += 2)
        {
            Viewport viewport = Create(5938, 3888, 1920, height);
            viewport.ZoomToFit();
            viewport.ZoomToActualSize();
            double half = Math.Round(viewport.OffsetY * 2) / 2;
            Assert.Equal(half, viewport.OffsetY, 9);
            Assert.Equal(Math.Floor(half) + 1, viewport.DrawnOffsetY);
            int top = (int)viewport.DrawnOffsetY;

            int rows = Enumerable.Range(0, height).Count(row => viewport.PixelAt(100, row)?.Y != top + row);
            if (rows > 0)
            {
                wrong.Add($"height {height}: {rows} rows");
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void R028_PixelAt_WithTheCentreExactlyOnAnEdge_NamesThePixelBeforeIt()
    {
        // At a scale that is not whole: at a whole one the image is drawn on whole physical
        // pixels (R-022) and no centre falls on an edge.
        Viewport viewport = Create(1200, 800, 764, 450);
        viewport.ZoomTo(4.5, 0, 0);
        viewport.SetOffset(300, 200);

        // The centre of screen column 4 is at 300 + 4.5 / 4.5 = 301, the edge between image
        // columns 300 and 301, and that of screen row 4 at 201. The display adapter paints such
        // a screen pixel from the pixel before the edge (ADR 0052).
        Assert.Equal(new ImagePixel(300, 200), viewport.PixelAt(4, 4));
        Assert.Equal(new ImagePixel(301, 201), viewport.PixelAt(5, 5));
    }

    [Theory]
    [InlineData(1.0 / 16384, 300)]
    [InlineData(1.0 / 1024, 301)]
    public void R028_PixelAt_WithTheCentreJustPastAnEdge_GoesByWhatTheAdapterPaints(double past, int column)
    {
        // Measured on screen on these columns, at 400%, 800% and 1600% (ADR 0052): a centre
        // 1/16384 of an image pixel past an edge is still painted from the pixel before it, on
        // 156 of 160 screen pixels at 400%; one 1/1024 past is painted from the pixel after it,
        // on all 160.
        Viewport viewport = Create(1200, 800, 764, 450);
        viewport.ZoomTo(4.5, 0, 0);
        viewport.SetOffset(300 + past, 200);

        Assert.Equal(column, viewport.PixelAt(4, 0)?.X);
    }

    [Fact]
    public void R028_PixelAt_AnEdgesReach_IsOneFourThousandNinetySixthOfAPixel_Inclusive()
    {
        Assert.Equal(1.0 / 4096, Viewport.EdgeReach);

        // The centre of screen column 4 is at the offset plus 4.5 / 4.5: exactly EdgeReach past
        // the edge at 301 it is still in column 300, and one ulp further in column 301.
        Viewport viewport = Create(1200, 800, 764, 450);
        viewport.ZoomTo(4.5, 0, 0);

        viewport.SetOffset(300 + Viewport.EdgeReach, 200);
        Assert.Equal(300, viewport.PixelAt(4, 0)?.X);

        viewport.SetOffset(Math.BitIncrement(300 + Viewport.EdgeReach), 200);
        Assert.Equal(301, viewport.PixelAt(4, 0)?.X);
    }

    [Fact]
    public void R028_PixelAt_OnAnOddWidth_AfterFitThen100PercentThenFourSteps_NamesThePixelDrawn()
    {
        // On a surface of odd width the 100% pan ends in .5 and the 400% one in .375. Drawn
        // there, one screen column in four would have its centre exactly on an edge; the image
        // is drawn at 504.5 instead (R-022), image column 507 starts on screen column 10, and
        // the read-out goes by where the image is drawn.
        Viewport viewport = Create(1200, 800, 765, 450);
        viewport.ZoomToFit();
        viewport.ZoomToActualSize();
        for (int step = 0; step < 4; step++)
        {
            viewport.ZoomIn(765 / 2.0, 450 / 2.0);
        }

        Assert.Equal(4, viewport.Scale);
        Assert.Equal(504.375, viewport.OffsetX);
        Assert.Equal(504.5, viewport.DrawnOffsetX);
        Assert.Equal(506, viewport.PixelAt(9, 60)?.X);
        Assert.Equal(507, viewport.PixelAt(10, 60)?.X);
        Assert.Equal(507, viewport.PixelAt(13, 60)?.X);
        Assert.Equal(508, viewport.PixelAt(14, 60)?.X);
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
        // 450%, a scale that is not whole, so the image is drawn at these very offsets (R-022).
        Viewport viewport = Create(1200, 800, 764, 450);
        viewport.ZoomTo(4.5, 0, 0);
        viewport.SetOffset(300.55, 200.45);

        // Image row 213 starts at screen y (213 - 200.45) x 4.5 = 56.475: screen row 56, whose
        // centre is at 56.5, is drawn from it, and screen row 55 from row 212.
        Assert.Equal(213, viewport.PixelAt(300, 56)?.Y);
        Assert.Equal(212, viewport.PixelAt(300, 55)?.Y);

        // Image column 302 starts at screen x (302 - 300.55) x 4.5 = 6.525, in the second half
        // of screen column 6, which is therefore still drawn from column 301.
        Assert.Equal(301, viewport.PixelAt(6, 50)?.X);
        Assert.Equal(302, viewport.PixelAt(7, 50)?.X);
    }

    [Fact]
    public void R029_SetContentSize_RejectsNegativeDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Viewport().SetContentSize(-1, 10));
    }
}
