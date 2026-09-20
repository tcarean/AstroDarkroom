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

namespace AstroPhotoAnalyzer.Tests;

public class StatsTests
{
    /// <summary>Semantica numpy: np.percentile([1,2,3,4], 50) = 2.5, 25 -> 1.75, 100 -> 4.</summary>
    [Theory]
    [InlineData(50, 2.5)]
    [InlineData(25, 1.75)]
    [InlineData(0, 1.0)]
    [InlineData(100, 4.0)]
    [InlineData(99.9, 3.997)]
    public void PercentileInterpolatesLikeNumpy(double p, double expected)
    {
        float[] v = { 4, 1, 3, 2 };

        Assert.Equal(expected, Stats.PercentileSorted(Stats.Sorted(v), p), 3);
    }

    [Fact]
    public void MedianOfAnOddCountIsTheMiddleValue()
    {
        Assert.Equal(3.0, Stats.Median(new float[] { 5, 1, 3, 2, 4 }));
    }

    [Fact]
    public void SingleElementPercentileIsThatElement()
    {
        Assert.Equal(7.0, Stats.PercentileSorted(new float[] { 7 }, 99));
    }

    [Fact]
    public void StdDevIsThePopulationOne()
    {
        // np.std([2, 4, 4, 4, 5, 5, 7, 9]) = 2.0
        Assert.Equal(2.0, Stats.StdDev(new float[] { 2, 4, 4, 4, 5, 5, 7, 9 }), 9);
        Assert.Equal(5.0, Stats.Mean(new float[] { 2, 4, 4, 4, 5, 5, 7, 9 }), 9);
    }

    [Fact]
    public void MinAndMax()
    {
        float[] v = { 0.3f, -1f, 2.5f };
        Assert.Equal(-1f, Stats.Min(v));
        Assert.Equal(2.5f, Stats.Max(v));
    }

    [Fact]
    public void TheSeededGeneratorIsDeterministic()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);
        var c = new SeededRandom(43);

        var sa = Enumerable.Range(0, 20).Select(_ => a.NextUInt64()).ToArray();
        var sb = Enumerable.Range(0, 20).Select(_ => b.NextUInt64()).ToArray();
        var sc = Enumerable.Range(0, 20).Select(_ => c.NextUInt64()).ToArray();

        Assert.Equal(sa, sb);
        Assert.NotEqual(sa, sc);
    }

    [Fact]
    public void SeedZeroDoesNotStickOnZero()
    {
        var r = new SeededRandom(0);
        Assert.NotEqual(0UL, r.NextUInt64());
        Assert.NotEqual(r.NextUInt64(), r.NextUInt64());
    }

    [Fact]
    public void NextIsUniformInRangeAndNextDoubleInUnitInterval()
    {
        var r = new SeededRandom(7);
        var counts = new int[5];
        for (int i = 0; i < 50000; i++)
        {
            int n = r.Next(5);
            Assert.InRange(n, 0, 4);
            counts[n]++;
            double d = r.NextDouble();
            Assert.True(d >= 0 && d < 1);
        }
        foreach (int c in counts) Assert.InRange(c, 9000, 11000);
    }

    [Fact]
    public void GaussianHasZeroMeanAndUnitVariance()
    {
        var r = new SeededRandom(1);
        var v = new float[200000];
        for (int i = 0; i < v.Length; i++) v[i] = (float)r.NextGaussian();

        Assert.Equal(0.0, Stats.Mean(v), 2);
        Assert.Equal(1.0, Stats.StdDev(v), 2);
    }
}
