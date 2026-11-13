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

// SplitDividerTests - the line of the Before / After comparison (R-179, ADR 0090): where it
// starts, how it moves, what a press grabs, when a label has room.

using System.Windows;

namespace AstroPhotoAnalyzer.Wpf.Tests;

public class SplitDividerTests
{
    [Fact]
    public void R179_TheLineStartsInTheMiddleMovesWithThePointerAndStopsAtTheEdges()
    {
        var d = new SplitDivider();
        Assert.Equal(0.5, d.Position);
        Assert.Equal(200, d.LineX(400));

        d.MoveTo(100, 400);
        Assert.Equal(0.25, d.Position);
        // A fraction of the width, not a distance: a wider area keeps the fraction.
        Assert.Equal(200, d.LineX(800));

        d.MoveTo(-50, 400);
        Assert.Equal(0, d.Position);
        d.MoveTo(900, 400);
        Assert.Equal(1, d.Position);
        Assert.Equal(400, d.LineX(400));

        // No area to speak of: the middle.
        d.MoveTo(100, 0);
        Assert.Equal(0.5, d.Position);

        d.MoveTo(100, 400);
        d.Reset();
        Assert.Equal(0.5, d.Position);
    }

    [Fact]
    public void R179_APressNearTheLineOrOnTheHandleGrabsItAndOneElsewhereDoesNot()
    {
        var d = new SplitDivider();   // the line at x = 200 of an area of 400 x 300
        Assert.Equal(8, SplitDivider.LineGrabTolerance);
        Assert.Equal(2, SplitDivider.LineWidth);

        // The whole height of the line, within the tolerance on either side.
        Assert.True(d.HitTest(200, 0, 400, 300));
        Assert.True(d.HitTest(208, 299, 400, 300));
        Assert.True(d.HitTest(192, 10, 400, 300));
        Assert.False(d.HitTest(208.5, 10, 400, 300));
        Assert.False(d.HitTest(191.5, 10, 400, 300));

        // The handle is wider than the tolerance, at mid height only.
        Assert.Equal(new Rect(187, 122, 26, 56), d.Handle(400, 300));
        Assert.True(d.HitTest(213, 150, 400, 300));
        Assert.True(d.HitTest(187, 178, 400, 300));
        Assert.False(d.HitTest(213, 178.5, 400, 300));
        Assert.False(d.HitTest(213.5, 150, 400, 300));

        // Anywhere else is the image's: a pan.
        Assert.False(d.HitTest(100, 150, 400, 300));

        // The hit test follows the line.
        d.MoveTo(100, 400);
        Assert.True(d.HitTest(100, 150, 400, 300));
        Assert.False(d.HitTest(200, 150, 400, 300));
    }

    [Fact]
    public void R179_ALabelHasRoomOnlyWhenItsSideHoldsItWithTheGapOnBothItsSides()
    {
        Assert.Equal(10, SplitDivider.LabelGap);
        Assert.True(SplitDivider.LabelFits(80, 60));
        Assert.False(SplitDivider.LabelFits(79.5, 60));
        Assert.False(SplitDivider.LabelFits(0, 60));
    }
}
