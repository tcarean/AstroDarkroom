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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>The raster primitives against the prototype's numpy and scipy conventions (R-013).</summary>
public class RasterTests
{
    private static Raster Of(int h, int w, Func<int, int, float> f)
    {
        Raster r = new(h, w);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                r.D[y * w + x] = f(y, x);
            }
        }

        return r;
    }

    [Fact]
    public void R013_GreenInterpolationKeepsGreensAndAveragesTheFourNeighboursElsewhere()
    {
        // RGGB: greens at (0,1) and (1,0). Set every green to 10 + x + y, others to 999.
        Raster a = Of(4, 4, (y, x) => (y & 1) != (x & 1) ? 10 + x + y : 999);
        Raster g = Raster.InterpolateGreen(a, CfaPattern.Parse("RGGB", "t"));

        Assert.Equal(11, g.At(0, 1));
        Assert.Equal(13, g.At(2, 1));
        // (1,1) is blue: neighbours (0,1)=11, (2,1)=13, (1,0)=11, (1,2)=13 -> 12
        Assert.Equal(12, g.At(1, 1));
        // (0,0) is red at a corner: neighbours (1,0)=11 and (0,1)=11 -> 11
        Assert.Equal(11, g.At(0, 0));
        Assert.Same(a, Raster.InterpolateGreen(a, null));
    }

    [Fact]
    public void R013_TileMediansReadEveryFourthPixelAndDropPartialTiles()
    {
        Raster a = Of(20, 30, (y, x) => x < 8 ? 5 : 7);
        Raster t = Raster.TileMedians(a, 8, 4);
        Assert.Equal(2, t.H);
        Assert.Equal(3, t.W);
        Assert.Equal(5, t.At(0, 0));
        Assert.Equal(7, t.At(0, 1));
        Assert.Equal(7, t.At(1, 2));
    }

    [Fact]
    public void R013_BilinearResizeMapsTheGridCornersOntoTheRasterCorners()
    {
        Raster t = Of(2, 2, (y, x) => y == 0 ? (x == 0 ? 0 : 10) : (x == 0 ? 20 : 30));
        Raster o = Raster.BilinearResize(t, 5, 5);
        Assert.Equal(0, o.At(0, 0));
        Assert.Equal(10, o.At(0, 4));
        Assert.Equal(20, o.At(4, 0));
        Assert.Equal(30, o.At(4, 4));
        Assert.Equal(15, o.At(2, 2), 1e-5);
        Assert.Equal(5, o.At(0, 2), 1e-5);
    }

    [Fact]
    public void R013_GaussianIsNormalisedAndSymmetric()
    {
        Raster flat = Of(20, 20, (y, x) => 3);
        Raster g = Raster.Gaussian(flat, 1.0);
        Assert.All(g.D, v => Assert.Equal(3, v, 1e-4));

        Raster spike = Of(21, 21, (y, x) => y == 10 && x == 10 ? 100 : 0);
        Raster s = Raster.Gaussian(spike, 1.0);
        Assert.Equal(100, s.D.Sum(), 0.01);
        Assert.Equal(s.At(10, 12), s.At(10, 8), 1e-5);
        Assert.Equal(s.At(12, 10), s.At(10, 12), 1e-5);
        // scipy, sigma 1, truncate 4: the centre weight of the 9-tap kernel is 0.39894 (normalised), squared in 2D.
        Assert.Equal(100 * 0.39894 * 0.39894, s.At(10, 10), 0.05);
    }

    [Theory]
    [InlineData(-1, 5, 0)]
    [InlineData(-2, 5, 1)]
    [InlineData(5, 5, 4)]
    [InlineData(6, 5, 3)]
    [InlineData(2, 5, 2)]
    public void R013_ReflectBorderMatchesScipy(int i, int n, int expected) => Assert.Equal(expected, Raster.Reflect(i, n));

    [Fact]
    public void R013_CentralMadSigmaReadsTheMiddleWindow()
    {
        // The window [100, 200) x [100, 200) holds +-1 alternating (median 0, MAD 1 -> sigma 1.4826); the rest is a huge constant.
        Raster a = Of(300, 300, (y, x) => y >= 100 && y < 200 && x >= 100 && x < 200 ? ((x + y) % 2 == 0 ? 1 : -1) : 1000);
        Assert.Equal(1.4826, Raster.CentralMadSigma(a, 50, 1), 1e-4);
    }

    [Theory]
    [InlineData(50, 2.5)]
    [InlineData(90, 3.7)]
    [InlineData(0, 1)]
    [InlineData(100, 4)]
    public void R013_PercentileInterpolatesLikeNumpy(double p, double expected)
        => Assert.Equal(expected, Raster.Percentile([4, 1, 3, 2], p), 1e-9);

    [Fact]
    public void R013_MedianOfEvenAndOddCounts()
    {
        Assert.Equal(2.5f, Raster.Median([4, 1, 3, 2], 4));
        Assert.Equal(3f, Raster.Median([4, 1, 3, 2, 9], 5));
        Assert.True(float.IsNaN(Raster.Median([], 0)));
    }
}
