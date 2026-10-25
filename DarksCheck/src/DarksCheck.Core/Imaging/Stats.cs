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

#nullable disable // copied from FlatCheck 2.3.0 unchanged, which compiles without nullable annotations (ADR 0003)
// The code is the copy's; the comments are DarksCheck's own (ADR 0019).

// Stats.cs - pure numeric primitives: percentile, interpolation, solver.

namespace DarksCheck.Core
{
    /// <summary>
    /// Numeric primitives: percentiles as numpy computes them, linear interpolation and a small
    /// linear solver. In DarksCheck only the readers use them (RawReader's median of the four
    /// blacks, Img's tile medians); the analyzers have their own in Planes.
    /// </summary>
    internal static class Stats
    {
        /// <summary>
        /// The <paramref name="p"/>-th percentile of a list, as <see cref="OfSorted"/> computes it.
        /// </summary>
        /// <param name="data">The values; not modified.</param>
        /// <param name="p">The percentile, 0..100.</param>
        /// <returns>The percentile; 0 for an empty list.</returns>
        internal static double Percentile(List<double> data, double p)
        {
            var s = data.ToArray();
            Array.Sort(s);
            return OfSorted(s, p);
        }

        /// <summary>
        /// The overload on an array: it avoids the extra copy of a .ToList() at the caller.
        /// On a 24 MP full-res raster that means ~190 MB less per call.
        /// </summary>
        /// <param name="data">The values; not modified (a sorted copy is made).</param>
        /// <param name="p">The percentile, 0..100.</param>
        /// <returns>The percentile; 0 for an empty array.</returns>
        internal static double Percentile(double[] data, double p)
        {
            var s = (double[])data.Clone();
            Array.Sort(s);
            return OfSorted(s, p);
        }

        /// <summary>
        /// For whoever needs several percentiles of the same set: sort once, then call
        /// <see cref="OfSorted"/> several times.
        /// </summary>
        /// <param name="data">The values; not modified.</param>
        /// <returns>A sorted copy of <paramref name="data"/>.</returns>
        internal static double[] Sorted(double[] data)
        {
            var s = (double[])data.Clone();
            Array.Sort(s);
            return s;
        }

        /// <summary>
        /// The <paramref name="p"/>-th percentile of values already sorted ascending, by linear
        /// interpolation between the two neighbouring order statistics, like numpy's default.
        /// </summary>
        /// <param name="s">The values, sorted ascending.</param>
        /// <param name="p">The percentile, 0..100.</param>
        /// <returns>The percentile; 0 for an empty array, where numpy would throw.</returns>
        internal static double OfSorted(double[] s, double p)
        {
            if (s.Length == 0) return 0;      // numpy would throw; a deliberate divergence
            if (s.Length == 1) return s[0];
            double idx = p / 100.0 * (s.Length - 1);
            int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
            double frac = idx - lo;
            return s[lo] * (1 - frac) + s[hi] * frac; // linear interpolation, like numpy
        }

        /// <summary>
        /// Linear interpolation of <paramref name="x"/> in a table, held at the end values
        /// outside it (like np.interp).
        /// </summary>
        /// <param name="x">The abscissa to look up.</param>
        /// <param name="xs">The table's abscissas, ascending.</param>
        /// <param name="ys">The table's values, as many as <paramref name="xs"/>.</param>
        /// <returns>The interpolated value; 0 for an empty table.</returns>
        internal static double Interp(double x, double[] xs, double[] ys)
        {
            if (xs.Length == 0) return 0;
            if (x <= xs[0]) return ys[0];
            if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
            int lo = 0, hi = xs.Length - 1;
            while (hi - lo > 1) { int m = (lo + hi) / 2; if (xs[m] <= x) lo = m; else hi = m; }
            double t = (x - xs[lo]) / (xs[hi] - xs[lo]);
            return ys[lo] + t * (ys[hi] - ys[lo]);
        }

        /// <summary>
        /// Solves <c>A x = b</c> by Gauss with partial pivoting; null if singular.
        /// </summary>
        /// <param name="A">The n x n matrix; not modified.</param>
        /// <param name="b">The right-hand side, n values; not modified.</param>
        /// <param name="n">The size of the system.</param>
        /// <returns>The solution, n values; null when a pivot is below 1e-15 (singular).</returns>
        internal static double[] Solve(double[,] A, double[] b, int n)
        {
            var M = new double[n, n + 1];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) M[i, j] = A[i, j]; M[i, n] = b[i]; }
            for (int col = 0; col < n; col++)
            {
                int piv = col; double best = Math.Abs(M[col, col]);
                for (int r = col + 1; r < n; r++) if (Math.Abs(M[r, col]) > best) { best = Math.Abs(M[r, col]); piv = r; }
                if (best < 1e-15) return null;
                if (piv != col) for (int j = 0; j <= n; j++) { var t = M[col, j]; M[col, j] = M[piv, j]; M[piv, j] = t; }
                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double factor = M[r, col] / M[col, col];
                    for (int j = col; j <= n; j++) M[r, j] -= factor * M[col, j];
                }
            }
            var x = new double[n];
            for (int i = 0; i < n; i++) x[i] = M[i, n] / M[i, i];
            return x;
        }
    }
}
