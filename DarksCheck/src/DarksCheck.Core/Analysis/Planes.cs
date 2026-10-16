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

namespace DarksCheck.Core;

/// <summary>
/// The arithmetic the dark checks share, on one CFA position (a "plane") of a mosaic: the
/// four positions are p = (y &amp; 1) * 2 + (x &amp; 1), as <see cref="Mosaic.PositionOf"/>
/// numbers them. The figures reproduce the Python prototype (`prototype/analyze_darks_lib.py`):
/// the clipped sigma with its subsampling, numpy's percentile and median, block means.
/// </summary>
internal static class Planes
{
    /// <summary>How many values the clipped sigma reads at most; above it every k-th value of the plane is taken, as the prototype did.</summary>
    internal const int SigmaSampleMax = 3_000_000;

    internal static int PlaneHeight(int h, int p) => (h - (p >> 1) + 1) / 2;

    internal static int PlaneWidth(int w, int p) => (w - (p & 1) + 1) / 2;

    /// <summary>The values of one CFA position, row-major, as floats.</summary>
    internal static float[] Extract(ushort[] a, int w, int h, int p)
    {
        ArgumentNullException.ThrowIfNull(a);
        int ph = PlaneHeight(h, p), pw = PlaneWidth(w, p);
        float[] r = new float[ph * pw];
        int y0 = p >> 1, x0 = p & 1;
        for (int j = 0; j < ph; j++)
        {
            int row = (y0 + 2 * j) * w + x0;
            int o = j * pw;
            for (int i = 0; i < pw; i++)
            {
                r[o + i] = a[row + 2 * i];
            }
        }

        return r;
    }

    internal static float[] Extract(float[] a, int w, int h, int p)
    {
        ArgumentNullException.ThrowIfNull(a);
        int ph = PlaneHeight(h, p), pw = PlaneWidth(w, p);
        float[] r = new float[ph * pw];
        int y0 = p >> 1, x0 = p & 1;
        for (int j = 0; j < ph; j++)
        {
            int row = (y0 + 2 * j) * w + x0;
            int o = j * pw;
            for (int i = 0; i < pw; i++)
            {
                r[o + i] = a[row + 2 * i];
            }
        }

        return r;
    }

    /// <summary>
    /// The noise of a plane as a clipped standard deviation: the raw of a D3300 is quantised
    /// in steps of 2 to 4 ADU, so a median absolute deviation reads 0 or one step. Every k-th
    /// value (k = n / 3 000 000, at least 1), the population std, then three passes that drop
    /// what lies more than 5 sigma (at least 5 ADU) from the running centre. Returns the sigma
    /// and the median of the sampled values.
    /// </summary>
    internal static (double Sigma, double Median) ClippedSigma(float[] plane)
    {
        ArgumentNullException.ThrowIfNull(plane);
        int k = Math.Max(1, plane.Length / SigmaSampleMax);
        int n = (plane.Length + k - 1) / k;
        float[] x = new float[n];
        for (int i = 0, j = 0; i < plane.Length; i += k, j++)
        {
            x[j] = plane[i];
        }

        double med = Median(x);
        double s = Std(x, null, 0, out _);
        double centre = med;
        for (int pass = 0; pass < 3; pass++)
        {
            double limit = 5 * Math.Max(s, 1.0);
            s = Std(x, centre, limit, out double mean);
            centre = mean;
        }

        return (s, med);
    }

    /// <summary>Population standard deviation of the values within <paramref name="limit"/> of <paramref name="centre"/> (all of them when the centre is null); the mean of the kept values comes out too.</summary>
    private static double Std(float[] x, double? centre, double limit, out double mean)
    {
        double sum = 0, sum2 = 0;
        long count = 0;
        foreach (float v in x)
        {
            if (centre is double c && Math.Abs(v - c) >= limit)
            {
                continue;
            }

            sum += v;
            sum2 += (double)v * v;
            count++;
        }

        if (count == 0)
        {
            mean = centre ?? 0;
            return 0;
        }

        mean = sum / count;
        double var = sum2 / count - mean * mean;
        return Math.Sqrt(Math.Max(var, 0));
    }

    /// <summary>numpy's median: the middle value, or the mean of the two middle values; the array is copied.</summary>
    internal static double Median(float[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
        {
            return double.NaN;
        }

        float[] c = (float[])values.Clone();
        int n = c.Length;
        if (n % 2 == 1)
        {
            return Select(c, n / 2);
        }

        double hi = Select(c, n / 2);
        double lo = Select(c, n / 2 - 1);
        return 0.5 * (lo + hi);
    }

    /// <summary>numpy's percentile with linear interpolation: rank p / 100 * (n - 1); the array is copied.</summary>
    internal static double Percentile(float[] values, double p)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
        {
            return double.NaN;
        }

        float[] c = (float[])values.Clone();
        double rank = p / 100.0 * (c.Length - 1);
        int lo = (int)Math.Floor(rank);
        int hi = Math.Min(lo + 1, c.Length - 1);
        double a = Select(c, lo);
        if (hi == lo)
        {
            return a;
        }

        double b = Select(c, hi);
        return a + (b - a) * (rank - lo);
    }

    /// <summary>The k-th smallest value (0-based), by quickselect; reorders the array.</summary>
    internal static float Select(float[] a, int k)
    {
        ArgumentNullException.ThrowIfNull(a);
        int lo = 0, hi = a.Length - 1;
        while (hi > lo)
        {
            // median of three as the pivot: sorted and reversed inputs stay linear
            int mid = lo + (hi - lo) / 2;
            float pivot = a[mid];
            if (a[lo] > pivot)
            {
                pivot = a[lo] < a[hi] ? a[lo] : (a[hi] > a[mid] ? a[hi] : a[mid]);
            }
            else if (a[hi] < pivot)
            {
                pivot = a[hi] > a[lo] ? a[hi] : a[lo];
            }

            int i = lo, j = hi;
            while (i <= j)
            {
                while (a[i] < pivot)
                {
                    i++;
                }

                while (a[j] > pivot)
                {
                    j--;
                }

                if (i <= j)
                {
                    (a[i], a[j]) = (a[j], a[i]);
                    i++;
                    j--;
                }
            }

            if (k <= j)
            {
                hi = j;
            }
            else if (k >= i)
            {
                lo = i;
            }
            else
            {
                return a[k];
            }
        }

        return a[k];
    }

    /// <summary>The mean of each b x b block of a plane (the remainder rows and columns dropped), as a smaller map.</summary>
    internal static float[] BlockMean(float[] plane, int pw, int ph, int b, out int bw, out int bh)
    {
        ArgumentNullException.ThrowIfNull(plane);
        bw = pw / b;
        bh = ph / b;
        float[] m = new float[bw * bh];
        double inv = 1.0 / (b * b);
        for (int by = 0; by < bh; by++)
        {
            for (int bx = 0; bx < bw; bx++)
            {
                double s = 0;
                for (int y = by * b; y < (by + 1) * b; y++)
                {
                    int row = y * pw + bx * b;
                    for (int x = 0; x < b; x++)
                    {
                        s += plane[row + x];
                    }
                }

                m[by * bw + bx] = (float)(s * inv);
            }
        }

        return m;
    }

    /// <summary>The mean of a rectangle of a map, rows [y0, y1) and columns [x0, x1).</summary>
    internal static double MeanOf(float[] map, int w, int y0, int y1, int x0, int x1)
    {
        ArgumentNullException.ThrowIfNull(map);
        double s = 0;
        long n = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                s += map[y * w + x];
                n++;
            }
        }

        return n == 0 ? double.NaN : s / n;
    }

    /// <summary>The mean of each row of a plane, and of each column.</summary>
    internal static (double[] Rows, double[] Cols) Profiles(float[] plane, int pw, int ph)
    {
        ArgumentNullException.ThrowIfNull(plane);
        double[] rows = new double[ph];
        double[] cols = new double[pw];
        for (int y = 0; y < ph; y++)
        {
            double s = 0;
            int row = y * pw;
            for (int x = 0; x < pw; x++)
            {
                float v = plane[row + x];
                s += v;
                cols[x] += v;
            }

            rows[y] = s / pw;
        }

        for (int x = 0; x < pw; x++)
        {
            cols[x] /= ph;
        }

        return (rows, cols);
    }

    /// <summary>Population standard deviation of a sequence.</summary>
    internal static double Std(IReadOnlyList<double> v)
    {
        ArgumentNullException.ThrowIfNull(v);
        if (v.Count == 0)
        {
            return 0;
        }

        double mean = v.Average();
        double s = 0;
        foreach (double x in v)
        {
            s += (x - mean) * (x - mean);
        }

        return Math.Sqrt(s / v.Count);
    }

    /// <summary>A running median over a window of <paramref name="window"/> values, the window clipped at the ends (scipy's `mode='nearest'` on a profile is the same up to the edges).</summary>
    internal static double[] MedianFilter(double[] v, int window)
    {
        ArgumentNullException.ThrowIfNull(v);
        int half = window / 2;
        double[] r = new double[v.Length];
        List<double> buf = new(window);
        for (int i = 0; i < v.Length; i++)
        {
            buf.Clear();
            for (int j = i - half; j <= i + half; j++)
            {
                buf.Add(v[Math.Clamp(j, 0, v.Length - 1)]);
            }

            buf.Sort();
            r[i] = buf.Count % 2 == 1 ? buf[buf.Count / 2] : 0.5 * (buf[buf.Count / 2 - 1] + buf[buf.Count / 2]);
        }

        return r;
    }
}
