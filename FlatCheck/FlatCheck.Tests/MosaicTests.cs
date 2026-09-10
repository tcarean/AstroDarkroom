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

namespace FlatCheck.Tests;

/// <summary>
/// The CFA rule shared by --expo and --histeq. Small on purpose: what matters is that
/// there is exactly one copy of it, and that the split puts each pixel where the
/// position formula says.
/// </summary>
public class MosaicTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 2)]
    [InlineData(1, 1, 3)]
    [InlineData(6, 9, 1)]
    [InlineData(7, 8, 2)]
    public void PositionOf_IsRowParityTimesTwoPlusColumnParity(int y, int x, int expected)
        => Assert.Equal(expected, Mosaic.PositionOf(y, x));

    [Fact]
    public void Split_PutsEveryPixelAtItsPosition()
    {
        var img = TestImages.From(8, 6, (x, y) => y * 100 + x);

        Img[] p = Mosaic.Split(img);

        Assert.All(p, c => { Assert.Equal(3, c.H); Assert.Equal(4, c.W); });
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 8; x++)
                Assert.Equal(img.At(y, x), p[Mosaic.PositionOf(y, x)].At(y / 2, x / 2));
    }

    [Fact]
    public void Split_DropsTheOddRowAndColumn()
    {
        Img[] p = Mosaic.Split(TestImages.From(7, 5, (x, y) => 1));

        Assert.All(p, c => { Assert.Equal(2, c.H); Assert.Equal(3, c.W); });
    }

    [Theory]
    [InlineData(1.15, false)]
    [InlineData(1.1501, true)]
    [InlineData(1.0, false)]
    [InlineData(2.0, true)]
    public void LooksCfa_IsStrictlyAboveTheSpreadThreshold(double spread, bool expected)
        => Assert.Equal(expected, Mosaic.LooksCfa(spread));

    [Fact]
    public void Label_UsesTheColourWhenThePatternIsKnown_AndThePositionOtherwise()
    {
        var gbrg = CfaPattern.Parse("GBRG", "test");

        Assert.Equal(new[] { "G1", "B", "R", "G2" }, Enumerable.Range(0, 4).Select(i => Mosaic.Label(gbrg, i)));
        Assert.Equal(new[] { "CFA[0,0]", "CFA[0,1]", "CFA[1,0]", "CFA[1,1]" },
                     Enumerable.Range(0, 4).Select(i => Mosaic.Label(null, i)));
        Assert.Equal('R', Mosaic.ColourOf(gbrg, 2));
        Assert.Equal('\0', Mosaic.ColourOf(null, 2));
    }
}
