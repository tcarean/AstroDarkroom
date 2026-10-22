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
            double rank = p / 100.0 * (sorted.Length - 1);
            int lo = (int)Math.Floor(rank);
            int hi = Math.Min(lo + 1, sorted.Length - 1);
            double frac = rank - lo;
            return sorted[lo] + (sorted[hi] - sorted[lo]) * frac;
        }

        /// <summary>
        /// Copies, sorts, and returns the sorted vector - for a caller that wants several
        /// percentiles.
        /// </summary>
        /// <param name="values">The values; left as they are.</param>
        /// <returns>A sorted copy, in ascending order.</returns>
        internal static float[] Sorted(float[] values)
        {
            var copy = (float[])values.Clone();
            Array.Sort(copy);
            return copy;
        }

        /// <summary>The median, as the 50th percentile of <see cref="PercentileSorted"/>.</summary>
        /// <param name="values">The values, in any order; at least one.</param>
        /// <returns>The median; for an even count, halfway between the two middle values.</returns>
        internal static double Median(float[] values) => PercentileSorted(Sorted(values), 50);

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
        /// threshold are the lower class. The upper class has at least
        /// <paramref name="minAbove"/> values: a few extreme values (stars on a faint nebula)
        /// would win any split otherwise.
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
        /// <param name="values">The values.</param>
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
        /// <param name="values">The values.</param>
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
        /// <param name="values">The values.</param>
        /// <returns>The smallest value; positive infinity for an empty vector.</returns>
        internal static float Min(float[] values)
        {
            float m = float.PositiveInfinity;
            for (int i = 0; i < values.Length; i++) if (values[i] < m) m = values[i];
            return m;
        }

        /// <summary>The largest value; a NaN is never taken.</summary>
        /// <param name="values">The values.</param>
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
    /// matters is that it gives THE SAME numbers wherever it runs.
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
