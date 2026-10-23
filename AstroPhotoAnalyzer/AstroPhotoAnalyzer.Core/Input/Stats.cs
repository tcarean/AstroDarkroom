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

// Stats.cs - median, percentiles, mean, deviation, and a random number generator with a fixed
// seed.
//
// THE PERCENTILE IS NUMPY'S (linear interpolation between ranks, the default method 'linear').
// The acceptance figures in docs/validation.md were measured with numpy, and a percentile
// defined otherwise (nearest, lower) would have given small differences nobody could have
// explained.
//
// A PERCENTILE OF AN UNSORTED VECTOR IS FOUND BY SELECTION, not by sorting the whole vector:
// numpy's rule reads two ranks, and a quickselect puts those two in place in linear time where
// a full sort of the 5.8 million stride-2 samples of a 23 MP image took about half a second
// (measured 2026-10-23, the code review of area 2). The two values found are the same two the
// sorted vector would hold, so every figure is identical; a vector that holds a NaN takes the
// sorted path, whose order (NaN first, as Array.Sort puts it) a selection would not reproduce.
//
// THE GENERATOR IS OUR OWN, not System.Random: the noise tiles are chosen at random, and the
// result must be the same on any runtime and in any year. System.Random with a seed is
// deterministic only within one version of .NET.

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The statistics the measurements share, on vectors of samples: numpy's percentile, the
    /// median, a robust sigma, Otsu's threshold, the mean, the standard deviation, min and max.
    /// </summary>
    internal static class Stats
    {
        /// <summary>
        /// The percentile <paramref name="p"/> of an ALREADY SORTED vector, as np.percentile.
        /// </summary>
        /// <param name="sorted">The values, sorted in ascending order; at least one.</param>
        /// <param name="p">The percentile, 0..100.</param>
        /// <returns>
        /// The value at rank p / 100 x (n - 1), interpolated linearly between the two ranks around
        /// it.
        /// </returns>
        internal static double PercentileSorted(float[] sorted, double p)
        {
            if (sorted.Length == 0) throw new ArgumentException("percentila pe un vector gol");
            if (sorted.Length == 1) return sorted[0];
            var (lo, hi, frac) = Ranks(sorted.Length, p);
            return Between(sorted[lo], sorted[hi], frac);
        }

        /// <summary>
        /// The two ranks numpy's rule reads for the percentile <paramref name="p"/> of
        /// <paramref name="n"/> values, and the fraction of the way from the first to the second.
        /// </summary>
        /// <param name="n">The count of values; at least two.</param>
        /// <param name="p">The percentile, 0..100.</param>
        /// <returns>
        /// The lower rank, floor(p / 100 x (n - 1)); the upper rank, one more, held at n - 1; and
        /// the fraction, 0..1, of the way between them.
        /// </returns>
        static (int Lo, int Hi, double Frac) Ranks(int n, double p)
        {
            double rank = p / 100.0 * (n - 1);
            int lo = (int)Math.Floor(rank);
            int hi = Math.Min(lo + 1, n - 1);
            return (lo, hi, rank - lo);
        }

        /// <summary>
        /// The value <paramref name="frac"/> of the way from <paramref name="lo"/> to
        /// <paramref name="hi"/>, in the one arithmetic every percentile goes through, so that
        /// the sorted path and the selection path give the same bits.
        /// </summary>
        /// <param name="lo">The value at the lower rank.</param>
        /// <param name="hi">The value at the upper rank.</param>
        /// <param name="frac">The fraction, 0..1.</param>
        /// <returns>lo + (hi - lo) x frac, the difference taken in float, the rest in double.</returns>
        static double Between(float lo, float hi, double frac) => lo + (hi - lo) * frac;

        /// <summary>
        /// Copies, sorts, and returns the sorted vector - for a caller that wants several
        /// percentiles of a vector it will read in order anyway (Otsu's threshold, the class
        /// sets).
        /// </summary>
        /// <param name="values">The values; left as they are.</param>
        /// <returns>A sorted copy, in ascending order.</returns>
        internal static float[] Sorted(float[] values)
        {
            var copy = (float[])values.Clone();
            Array.Sort(copy);
            return copy;
        }

        /// <summary>
        /// The percentile <paramref name="p"/> of an unsorted vector, as
        /// <see cref="PercentileSorted"/> would give it on the sorted vector, found by selection on
        /// a copy instead of a full sort.
        /// </summary>
        /// <param name="values">The values, in any order; at least one; left as they are.</param>
        /// <param name="p">The percentile, 0..100.</param>
        /// <returns>The same value PercentileSorted(Sorted(values), p) returns.</returns>
        internal static double Percentile(float[] values, double p) => Percentiles(values, new[] { p })[0];

        /// <summary>
        /// Several percentiles of an unsorted vector, each as <see cref="PercentileSorted"/> would
        /// give it on the sorted vector, found by selection on one copy: the ranks are put in
        /// place from the lowest up, each selection working on the part of the copy above the
        /// rank before it.
        /// </summary>
        /// <param name="values">The values, in any order; at least one; left as they are.</param>
        /// <param name="ps">The percentiles, 0..100 each, in any order.</param>
        /// <returns>
        /// One value per entry of <paramref name="ps"/>, in its order: the same values
        /// PercentileSorted(Sorted(values), p) returns.
        /// </returns>
        internal static double[] Percentiles(float[] values, double[] ps)
        {
            if (values.Length == 0) throw new ArgumentException("percentila pe un vector gol");
            var o = new double[ps.Length];
            int n = values.Length;
            if (n == 1)
            {
                for (int i = 0; i < ps.Length; i++) o[i] = values[0];
                return o;
            }
            if (HasNaN(values))
            {
                // Array.Sort puts NaN first; a selection on "<" would not, so the sorted path keeps
                // the one answer. The readers let no NaN through (R-013): a raster made in memory can.
                float[] sorted = Sorted(values);
                for (int i = 0; i < ps.Length; i++) o[i] = PercentileSorted(sorted, ps[i]);
                return o;
            }
            var a = (float[])values.Clone();
            int[] order = new int[ps.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (x, y) => ps[x].CompareTo(ps[y]));
            int from = 0;
            foreach (int r in order)
            {
                var (lo, hi, frac) = Ranks(n, ps[r]);
                Select(a, from, n - 1, lo);
                float vlo = a[lo];
                // The upper rank is the smallest value above the lower one: after the selection
                // everything past lo is at or above a[lo].
                float vhi = hi == lo ? vlo : MinFrom(a, lo + 1);
                o[r] = Between(vlo, vhi, frac);
                from = lo;
            }
            return o;
        }

        /// <summary>Whether any value is NaN.</summary>
        /// <param name="values">The values.</param>
        /// <returns><c>true</c> when at least one is NaN.</returns>
        static bool HasNaN(float[] values)
        {
            for (int i = 0; i < values.Length; i++) if (float.IsNaN(values[i])) return true;
            return false;
        }

        /// <summary>The smallest value from <paramref name="from"/> to the end.</summary>
        /// <param name="a">The values, without NaN.</param>
        /// <param name="from">The first index read; under the length.</param>
        /// <returns>The smallest of a[from..].</returns>
        static float MinFrom(float[] a, int from)
        {
            float m = a[from];
            for (int i = from + 1; i < a.Length; i++) if (a[i] < m) m = a[i];
            return m;
        }

        /// <summary>
        /// Puts the <paramref name="k"/>-th smallest value of a[left..right] at index k, in
        /// place, with everything before it at or under it and everything after it at or over
        /// it: Hoare's quickselect with a median-of-three pivot, which splits runs of equal
        /// values between the two sides and so stays linear on 16-bit data, where a 23 MP image
        /// has 5.8 million samples on 65 536 levels.
        /// </summary>
        /// <param name="a">The values, without NaN; reordered.</param>
        /// <param name="left">The first index of the range, inclusive.</param>
        /// <param name="right">The last index of the range, inclusive.</param>
        /// <param name="k">The index to put in place, from left to right inclusive.</param>
        static void Select(float[] a, int left, int right, int k)
        {
            while (right > left)
            {
                int mid = left + (right - left) / 2;
                if (a[mid] < a[left]) (a[mid], a[left]) = (a[left], a[mid]);
                if (a[right] < a[left]) (a[right], a[left]) = (a[left], a[right]);
                if (a[right] < a[mid]) (a[right], a[mid]) = (a[mid], a[right]);
                float pivot = a[mid];
                int i = left, j = right;
                while (i <= j)
                {
                    while (a[i] < pivot) i++;
                    while (a[j] > pivot) j--;
                    if (i <= j) { (a[i], a[j]) = (a[j], a[i]); i++; j--; }
                }
                // a[left..j] <= pivot <= a[i..right], and what lies between j and i equals it.
                if (k <= j) right = j;
                else if (k >= i) left = i;
                else return;
            }
        }

        /// <summary>The median, as the 50th percentile of <see cref="Percentile"/>.</summary>
        /// <param name="values">The values, in any order; at least one.</param>
        /// <returns>The median; for an even count, halfway between the two middle values.</returns>
        internal static double Median(float[] values) => Percentile(values, 50);

        /// <summary>
        /// Robust sigma: 1.4826 x the median of the absolute deviations from the median (MAD),
        /// equal to the standard deviation on a Gaussian and insensitive to the tail. Gives the
        /// median too.
        /// </summary>
        /// <param name="values">The values, in any order; at least one.</param>
        /// <param name="median">The median of <paramref name="values"/>.</param>
        /// <returns>The robust sigma, in the unit of the values.</returns>
        internal static double RobustSigma(float[] values, out double median)
        {
            median = Median(values);
            double m = median;
            var dev = new float[values.Length];
            for (int i = 0; i < values.Length; i++) dev[i] = (float)Math.Abs(values[i] - m);
            return 1.4826 * Median(dev);
        }

        /// <summary>
        /// Otsu's threshold on an ALREADY SORTED vector: the value at which the split into two
        /// classes has the largest variance between the classes; the values at or under the
        /// threshold are the lower class. The split leaves at least <paramref name="minAbove"/>
        /// values above it by rank: a few extreme values (stars on a faint nebula) would win any
        /// split otherwise. With values tied at the split, fewer may lie above the threshold.
        /// </summary>
        /// <param name="sorted">The values, sorted in ascending order.</param>
        /// <param name="minAbove">
        /// The fewest values the upper class may hold; under 1 is taken as 1.
        /// </param>
        /// <returns>
        /// The threshold, one of the values; NaN with fewer than minAbove + 1 values; with all
        /// the values equal, the smallest (and nothing is above it).
        /// </returns>
        internal static double OtsuThreshold(float[] sorted, int minAbove = 1)
        {
            minAbove = Math.Max(1, minAbove);
            if (sorted.Length < minAbove + 1) return double.NaN;
            double total = 0;
            foreach (float v in sorted) total += v;
            double best = -1, threshold = sorted[0], sumBelow = 0;
            for (int i = 0; i < sorted.Length - minAbove; i++)
            {
                sumBelow += sorted[i];
                int nBelow = i + 1, nAbove = sorted.Length - nBelow;
                double meanBelow = sumBelow / nBelow, meanAbove = (total - sumBelow) / nAbove;
                double between = (double)nBelow * nAbove * (meanBelow - meanAbove) * (meanBelow - meanAbove);
                if (between > best) { best = between; threshold = sorted[i]; }
            }
            return threshold;
        }

        /// <summary>The arithmetic mean.</summary>
        /// <param name="values">The values; a NaN among them makes the mean NaN.</param>
        /// <returns>The mean; NaN for an empty vector.</returns>
        internal static double Mean(float[] values)
        {
            double s = 0;
            for (int i = 0; i < values.Length; i++) s += values[i];
            return s / values.Length;
        }

        /// <summary>
        /// The population standard deviation (divided by N), as np.std by default.
        /// </summary>
        /// <param name="values">The values; a NaN among them makes the result NaN.</param>
        /// <returns>
        /// The standard deviation, in the unit of the values; NaN for an empty vector.
        /// </returns>
        internal static double StdDev(float[] values)
        {
            double m = Mean(values), s = 0;
            for (int i = 0; i < values.Length; i++) { double d = values[i] - m; s += d * d; }
            return Math.Sqrt(s / values.Length);
        }

        /// <summary>The smallest value; a NaN is never taken.</summary>
        /// <param name="values">The values; NaNs are skipped.</param>
        /// <returns>The smallest value; positive infinity for an empty vector.</returns>
        internal static float Min(float[] values)
        {
            float m = float.PositiveInfinity;
            for (int i = 0; i < values.Length; i++) if (values[i] < m) m = values[i];
            return m;
        }

        /// <summary>The largest value; a NaN is never taken.</summary>
        /// <param name="values">The values; NaNs are skipped.</param>
        /// <returns>The largest value; negative infinity for an empty vector.</returns>
        internal static float Max(float[] values)
        {
            float m = float.NegativeInfinity;
            for (int i = 0; i < values.Length; i++) if (values[i] > m) m = values[i];
            return m;
        }
    }

    /// <summary>
    /// xorshift64*, with a fixed seed. Good enough to pick tiles and to generate test noise; what
    /// matters is that it gives THE SAME numbers wherever it runs (NextGaussian goes through
    /// Math.Log and Math.Cos, which .NET does not promise bit for bit on every platform).
    /// </summary>
    internal sealed class SeededRandom
    {
        ulong _s;

        /// <summary>Starts the sequence of one seed.</summary>
        /// <param name="seed">Any value; the same seed gives the same sequence everywhere.</param>
        internal SeededRandom(int seed)
        {
            // A seed of 0 would lock xorshift on zero; it is mixed with a constant.
            _s = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)seed;
            if (_s == 0) _s = 1;
        }

        /// <summary>The next 64 bits of the sequence.</summary>
        /// <returns>A uniform 64-bit value.</returns>
        internal ulong NextUInt64()
        {
            _s ^= _s >> 12;
            _s ^= _s << 25;
            _s ^= _s >> 27;
            return _s * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>A uniform integer in [0, maxExclusive).</summary>
        /// <param name="maxExclusive">The bound, excluded; positive.</param>
        /// <returns>A value from 0 to maxExclusive - 1.</returns>
        internal int Next(int maxExclusive) => (int)(NextUInt64() % (ulong)maxExclusive);

        /// <summary>A uniform real in [0, 1).</summary>
        /// <returns>A value in [0, 1), from the top 53 bits of the next value.</returns>
        internal double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        /// <summary>A standard normal value, by Box-Muller.</summary>
        /// <returns>A normal value, of mean 0 and standard deviation 1.</returns>
        internal double NextGaussian()
        {
            double u1 = 1.0 - NextDouble(), u2 = NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }
    }
}
