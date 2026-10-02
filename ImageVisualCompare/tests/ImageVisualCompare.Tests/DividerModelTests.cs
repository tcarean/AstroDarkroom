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

public class DividerModelTests
{
    private const double ViewportWidth = 800;
    private const double ViewportHeight = 600;

    [Fact]
    public void R030_StartsCentred()
    {
        Assert.Equal(0.5, new DividerModel().Position, 6);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(0.25, 0.25)]
    [InlineData(1.0, 1.0)]
    [InlineData(3.0, 1.0)]
    public void R030_Position_IsClampedToTheViewport(double assigned, double expected)
    {
        var divider = new DividerModel { Position = assigned };

        Assert.Equal(expected, divider.Position, 6);
    }

    [Fact]
    public void R030_MoveTo_ConvertsScreenPositionToAFraction()
    {
        var divider = new DividerModel();

        divider.MoveTo(200, ViewportWidth);

        Assert.Equal(0.25, divider.Position, 6);
        Assert.Equal(200, divider.ScreenX(ViewportWidth), 6);
    }

    [Fact]
    public void R030_MoveTo_BeyondTheEdgeClampsInsteadOfEscaping()
    {
        var divider = new DividerModel();

        divider.MoveTo(-100, ViewportWidth);
        Assert.Equal(0.0, divider.Position, 6);

        divider.MoveTo(ViewportWidth + 100, ViewportWidth);
        Assert.Equal(1.0, divider.Position, 6);
    }

    [Fact]
    public void R031_NudgeBy_ShiftsByAScreenAmount()
    {
        var divider = new DividerModel();

        divider.NudgeBy(80, ViewportWidth);

        Assert.Equal(480, divider.ScreenX(ViewportWidth), 6);
    }

    [Fact]
    public void R030_Reset_ReturnsToTheCentre()
    {
        var divider = new DividerModel { Position = 0.9 };

        divider.Reset();

        Assert.Equal(0.5, divider.Position, 6);
    }

    [Theory]
    [InlineData(400, 50)]    // on the line, far from the handle
    [InlineData(407, 500)]   // within grab tolerance, near the bottom
    [InlineData(393, 10)]    // within grab tolerance, near the top
    public void R032_HitTest_GrabsTheLineAnywhereAlongItsLength(double x, double y)
    {
        Assert.True(new DividerModel().HitTest(x, y, ViewportWidth, ViewportHeight));
    }

    [Theory]
    [InlineData(410, 50)]    // past the tolerance, and away from the handle
    [InlineData(300, 300)]   // well clear of the line
    public void R032_HitTest_IgnoresPressesThatShouldPanInstead(double x, double y)
    {
        Assert.False(new DividerModel().HitTest(x, y, ViewportWidth, ViewportHeight));
    }

    [Fact]
    public void R032_HitTest_AcceptsTheWiderHandleTargetAtTheCentre()
    {
        var divider = new DividerModel();

        // 12 px from the line is past the line tolerance but still inside the handle.
        Assert.True(divider.HitTest(412, ViewportHeight / 2, ViewportWidth, ViewportHeight));
        Assert.False(divider.HitTest(412, ViewportHeight / 2 + 40, ViewportWidth, ViewportHeight));
    }
}
