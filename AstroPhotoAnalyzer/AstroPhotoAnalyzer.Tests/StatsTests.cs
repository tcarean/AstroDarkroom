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
    public void R017_PercentileInterpolatesLikeNumpy(double p, double expected)
    {
        float[] v = { 4, 1, 3, 2 };

        Assert.Equal(expected, Stats.PercentileSorted(Stats.Sorted(v), p), 3);
        Assert.Equal(expected, Stats.Percentile(v, p), 3);
    }

    /// <summary>
    /// The selection path gives the same bits as the sorted path: on random vectors with many
    /// duplicates (16-bit levels), on all-equal vectors, on the smallest counts, for every rank
    /// the tonal analyzer reads and in any order of the ranks; a vector with a NaN takes the
    /// sorted path and agrees too.
    /// </summary>
    [Fact]
    public void R017_PercentilesBySelectionEqualTheSortedRuleBitForBit()
    {
        var rng = new SeededRandom(17);
        double[] ranks = TonalResult.PercentileRanks;
        double[] shuffled = { 99.9, 0.1, 50, 100, 1, 99, 90 };
        foreach (int n in new[] { 1, 2, 3, 4, 7, 100, 10_007, 250_000 })
        {
            var v = new float[n];
            for (int i = 0; i < n; i++) v[i] = rng.Next(256) / 255f;   // heavy duplicates, as 8-bit levels
            float[] sorted = Stats.Sorted(v);
            double[] bySort = ranks.Select(p => Stats.PercentileSorted(sorted, p)).ToArray();
            Assert.Equal(bySort, Stats.Percentiles(v, ranks));
            double[] byShuffled = Stats.Percentiles(v, shuffled);
            for (int i = 0; i < shuffled.Length; i++) Assert.Equal(Stats.PercentileSorted(sorted, shuffled[i]), byShuffled[i]);
            Assert.Equal(Stats.PercentileSorted(sorted, 50), Stats.Median(v));
            Assert.Equal(Stats.PercentileSorted(sorted, 25), Stats.Percentile(v, 25));

            var w = new float[n];
            for (int i = 0; i < n; i++) w[i] = (float)rng.NextDouble();   // no duplicates
            float[] ws = Stats.Sorted(w);
            Assert.Equal(ranks.Select(p => Stats.PercentileSorted(ws, p)).ToArray(), Stats.Percentiles(w, ranks));
        }

        float[] flat = Enumerable.Repeat(0.25f, 1000).ToArray();
        Assert.Equal(0.25, Stats.Percentile(flat, 99.9));
        Assert.Equal(0.25, Stats.Median(flat));

        float[] withNaN = { 3, float.NaN, 1, 2 };
        Assert.Equal(Stats.PercentileSorted(Stats.Sorted(withNaN), 50), Stats.Median(withNaN));
        Assert.Throws<ArgumentException>(() => Stats.Percentile(Array.Empty<float>(), 50));
    }

    [Fact]
    public void RobustSigmaIsTheScaledMadAndIgnoresATail()
    {
        // 1..9 plus two far outliers: the median is 6, the absolute deviations 0..5 and the two
        // outliers give a MAD of 3, and the tail does not count.
        float[] v = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 1000, 2000 };
        double sigma = Stats.RobustSigma(v, out double median);
        Assert.Equal(6, median, 6);
        Assert.Equal(1.4826 * 3, sigma, 6);
    }

    [Fact]
    public void OtsuSplitsABimodalSampleBetweenItsModes()
    {
        // Two clusters: the threshold is the top of the lower one; values at or under it are the lower class.
        float[] v = Stats.Sorted(new float[] { 1, 2, 2, 3, 3, 3, 4, 20, 21, 22, 23 });
        Assert.Equal(4, Stats.OtsuThreshold(v));
        Assert.Equal(double.NaN, Stats.OtsuThreshold(new float[] { 7 }));
        Assert.Equal(5, Stats.OtsuThreshold(new float[] { 5, 5, 5 }));   // all equal: nothing is above it

        // A floor on the upper class: two far outliers over a hundred close values win every
        // split (the threshold sits at the top of the hundred, the outliers alone above it);
        // with at least ten values required above, the split is inside the hundred.
        float[] w = Stats.Sorted(Enumerable.Range(0, 100).Select(i => (float)(4 + 4.0 * i / 99)).Concat(new float[] { 60, 61 }).ToArray());
        Assert.Equal(8, Stats.OtsuThreshold(w), 3);                // without the floor: only the outliers above
        Assert.True(Stats.OtsuThreshold(w, 10) < 8);               // with it: inside the ramp
        Assert.Equal(double.NaN, Stats.OtsuThreshold(w, 102));     // not enough values to leave 102 above
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
    public void R017_TheSeededGeneratorIsDeterministic()
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
