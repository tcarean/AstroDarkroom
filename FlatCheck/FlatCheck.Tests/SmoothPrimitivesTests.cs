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
/// The two raster primitives the smoothing mode added: <see cref="Mosaic.Extract"/> /
/// <see cref="Mosaic.Merge"/> (one position out, four positions back in, at the input's
/// size) and <see cref="Img.TileMedians"/> (the grid that keeps its partial tiles).
/// </summary>
public class SmoothPrimitivesTests
{
    // ---------------------------------------------------------------- Extract / Merge

    [Fact]
    public void Extract_IsTheSamePositionSplitWouldGive()
    {
        var img = TestImages.From(8, 6, (x, y) => y * 100 + x);
        Img[] split = Mosaic.Split(img);

        for (int p = 0; p < 4; p++)
            Assert.Equal(split[p].D, Mosaic.Extract(img, p).D);
    }

    [Fact]
    public void Merge_UndoesSplit_OnAnEvenFrame()
    {
        var img = TestImages.From(8, 6, (x, y) => y * 100 + x);

        Img back = Mosaic.Merge(Mosaic.Split(img), 6, 8);

        Assert.Equal(img.D, back.D);
    }

    /// <summary>
    /// Split drops the odd last row and column; Merge has to give them back, and gives
    /// them the nearest even line of the same parity - not zero, which in a flat would
    /// divide the lights by nothing along that line.
    /// </summary>
    [Fact]
    public void Merge_OnAnOddFrame_FillsTheLastRowAndColumnFromTheNearestEvenOnes()
    {
        var img = TestImages.From(7, 5, (x, y) => y * 100 + x + 1);

        Img back = Mosaic.Merge(Mosaic.Split(img), 5, 7);

        Assert.Equal(5, back.H);
        Assert.Equal(7, back.W);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 6; x++)
                Assert.Equal(img.At(y, x), back.At(y, x), 9);
        // last row (y = 4, even parity) copies row 2 (the same parity, nearest); last column likewise
        Assert.Equal(img.At(2, 0), back.At(4, 0), 9);
        Assert.Equal(img.At(0, 4), back.At(0, 6), 9);
        Assert.Equal(img.At(2, 4), back.At(4, 6), 9);
        Assert.All(back.D, v => Assert.NotEqual(0, v));
    }

    // ---------------------------------------------------------------- TileMedians

    [Fact]
    public void TileMedians_KeepsThePartialLastRowAndColumn()
    {
        var img = TestImages.From(23, 17, (x, y) => 1);

        var (t, cy, cx) = Img.TileMedians(img, 10);

        Assert.Equal(2, t.H);   // 17 rows: 10 + 7
        Assert.Equal(3, t.W);   // 23 cols: 10 + 10 + 3
        Assert.Equal(new[] { 5.0, 13.5 }, cy);          // the partial tile's centre is its own
        Assert.Equal(new[] { 5.0, 15.0, 21.5 }, cx);
    }

    [Fact]
    public void TileMedians_TakeTheMedianOfEachTile()
    {
        // left tile all 2, right tile all 8 except one hot pixel at 1000
        var img = TestImages.From(8, 4, (x, y) => x < 4 ? 2 : (x == 6 && y == 1 ? 1000 : 8));

        var (t, _, _) = Img.TileMedians(img, 4);

        Assert.Equal(2, t.At(0, 0), 9);
        Assert.Equal(8, t.At(0, 1), 9);
    }

    /// <summary>The grid must reach the last pixel: a 4017-row sensor has 27 tile rows of 150, not 26.</summary>
    [Fact]
    public void TileMedians_CoverEveryPixel()
    {
        var img = TestImages.From(3008, 2008, (x, y) => 1);

        var (t, cy, cx) = Img.TileMedians(img, 75);

        Assert.Equal(27, t.H);
        Assert.Equal(41, t.W);
        Assert.Equal((1950 + 2008) / 2.0, cy[26], 9);
        Assert.Equal((3000 + 3008) / 2.0, cx[40], 9);
    }
}
