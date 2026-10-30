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

/// <summary>
/// The widths of the two names in the toolbar's pair label (R-065): the buttons and "vs", the
/// fixed part, are 260 DIPs in every case here.
/// </summary>
public class PairLabelLayoutTests
{
    [Fact]
    public void R065_NamesThatFit_KeepTheirOwnWidths()
    {
        Assert.Equal(new PairLabelLayout(150, 200), PairLabelLayout.For(1000, 150, 200, 260));
        Assert.Equal(new PairLabelLayout(150, 200), PairLabelLayout.For(double.PositiveInfinity, 150, 200, 260));
    }

    [Fact]
    public void R065_ANameIsNeverWiderThan360()
    {
        Assert.Equal(new PairLabelLayout(360, 100), PairLabelLayout.For(double.PositiveInfinity, 500, 100, 260));
        Assert.Equal(new PairLabelLayout(360, 360), PairLabelLayout.For(2000, 400, 361, 260));
    }

    [Fact]
    public void R065_TheLongerNameGivesFirst_TheShorterKeepsItsWidth()
    {
        // 340 of room for the names: 300 + 100 do not fit, and the longer is cut to the 240 left.
        Assert.Equal(new PairLabelLayout(240, 100), PairLabelLayout.For(600, 300, 100, 260));
        Assert.Equal(new PairLabelLayout(100, 240), PairLabelLayout.For(600, 100, 300, 260));
    }

    [Fact]
    public void R065_BothGiveEqually_WhenCuttingTheLongerIsNotEnough()
    {
        // 140 of room: the longer cut to what the shorter leaves would be shorter than it, so
        // each takes half.
        Assert.Equal(new PairLabelLayout(70, 70), PairLabelLayout.For(400, 300, 200, 260));
    }

    [Fact]
    public void R065_ANameStopsAt48_OrAtItsOwnWidthWhenThatIsLess()
    {
        // 40 of room, 20 a name: the long one stops at 48, the 30-wide one at its own 30; the
        // label is then wider than the room, and is clipped.
        Assert.Equal(new PairLabelLayout(48, 30), PairLabelLayout.For(300, 300, 30, 260));
        Assert.Equal(new PairLabelLayout(48, 48), PairLabelLayout.For(0, 300, 300, 260));
    }

    [Theory]
    [InlineData(-1, 10, 10)]
    [InlineData(10, -1, 10)]
    [InlineData(10, 10, -1)]
    [InlineData(double.NaN, 10, 10)]
    public void R065_ANegativeOrNaNWidth_IsRejected(double nameA, double nameB, double fixedWidth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PairLabelLayout.For(500, nameA, nameB, fixedWidth));
    }
}
