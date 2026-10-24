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

// Stats.cs - pure numeric primitives: percentile, interpolation, solver, verdicts.

using System;
using System.Collections.Generic;

namespace FlatCheck
{
    /// <summary>
    /// The numeric primitives every analyzer shares (percentiles as numpy computes them,
    /// linear interpolation, a small linear solver) and the verdict words of the two scales,
    /// with the severity that orders them.
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

        /// <summary>
        /// The three-word verdict of a metric against two thresholds: "OK" below
        /// <paramref name="ok"/>, "SUSPECT" below <paramref name="bad"/>, "PROBLEMA" otherwise.
        /// NaN fails both comparisons, so it comes out "PROBLEMA".
        /// </summary>
        /// <param name="val">The metric, in the unit of the thresholds.</param>
        /// <param name="ok">The upper bound of OK, from <see cref="Th"/>.</param>
        /// <param name="bad">The upper bound of SUSPECT, from <see cref="Th"/>.</param>
        /// <returns>"OK", "SUSPECT" or "PROBLEMA".</returns>
        internal static string Verdict(double val, double ok, double bad)
            => val < ok ? "OK" : (val < bad ? "SUSPECT" : "PROBLEMA");

        /// <summary>The names of the four residual bands, in order of severity.</summary>
        internal static readonly string[] ResidualBands = { "PERFECT", "BUN", "ACCEPTABIL", "REFA" };

        /// <summary>
        /// Not a verdict, a refusal: the input is not suited to the test asked for (--light on
        /// too wide a field). It differs from NEDETERMINAT, which means "it ran, but does not
        /// conclude".
        ///
        /// It sits here, beside the other verdict names, because both applications use it: the
        /// console maps it to exit code 1, the window colours it neutral. Two copies of the
        /// string would have lasted until the first rewording.
        /// </summary>
        internal const string NotApplicable = "INAPLICABIL";

        /// <summary>
        /// The residual scale of the structure mode. Like <see cref="Verdict"/>, NaN falls into
        /// the worst band: every comparison comes out false, so a degenerate input comes out
        /// "REFA".
        /// </summary>
        /// <param name="val">The residual, peak to peak in percent.</param>
        /// <returns>
        /// "PERFECT", "BUN", "ACCEPTABIL" or "REFA", by <see cref="Th.ResidPerfect"/>,
        /// <see cref="Th.ResidUsable"/> and <see cref="Th.ResidCheck"/>.
        /// </returns>
        internal static string ResidualVerdict(double val)
            => val < Th.ResidPerfect ? "PERFECT"
             : val < Th.ResidUsable ? "BUN"
             : val < Th.ResidCheck ? "ACCEPTABIL"
             : "REFA";

        /// <summary>
        /// The error in the corner of a light calibrated with a flat whose vignetting differs
        /// from the true one by <paramref name="stops"/>: 2^|stops| - 1, in percent. 0.1 stops =
        /// 7 %, 0.42 = 34 %. NaN stays NaN and falls into REFA, as everywhere. It is what turns
        /// a difference of DEPTH into a fractional amplitude like all the others, so onto the
        /// same residual scale, with no new threshold; --against and the comparison with the
        /// lens's profile in the structure mode use it.
        /// </summary>
        /// <param name="stops">The depth difference, in stops; its sign is ignored.</param>
        /// <returns>The corner error, in percent.</returns>
        internal static double DepthErrorPct(double stops) => (Math.Pow(2, Math.Abs(stops)) - 1) * 100;

        /// <summary>
        /// The verdict of a depth difference, on the residual scale applied to the corner error.
        /// </summary>
        /// <param name="stops">The depth difference, in stops.</param>
        /// <returns>A word of <see cref="ResidualBands"/>.</returns>
        internal static string DepthVerdict(double stops) => ResidualVerdict(DepthErrorPct(stops));

        /// <summary>
        /// The same corner error, read on the scale of the --light mode (Th.LightRadOk /
        /// LightRadBad, through the same <see cref="Verdict"/> LightAnalyzer uses, so that the
        /// two cannot diverge). Informative: the depth's verdict stays on the residual scale,
        /// because its reference - the published profile - is uncertain by
        /// Th.ProfileDatabaseSpreadStops between databases, that is more than the PROBLEMA line
        /// of the --light scale; --light measures the same quantity directly in the light and
        /// can afford the 5 % line. The report shows both.
        /// </summary>
        /// <param name="stops">The depth difference, in stops.</param>
        /// <returns>"OK", "SUSPECT" or "PROBLEMA".</returns>
        internal static string DepthOnLightScale(double stops)
            => Verdict(DepthErrorPct(stops), Th.LightRadOk, Th.LightRadBad);

        /// <summary>
        /// The inverse of <see cref="DepthErrorPct"/>: the stops at which the corner error
        /// reaches a threshold.
        /// </summary>
        /// <param name="errorPct">The corner error, in percent, 0 or more.</param>
        /// <returns>The depth difference, in stops.</returns>
        internal static double StopsForError(double errorPct) => Math.Log2(1 + errorPct / 100);

        /// <summary>
        /// Severity 0-3, unified over both scales, so that the "worst" of a mix of verdicts can
        /// be taken. Anything unknown is treated as the worst.
        /// </summary>
        /// <param name="verdict">A verdict word of either scale, or anything else.</param>
        /// <returns>0 for OK or PERFECT, 1 for BUN, 2 for SUSPECT, ACCEPTABIL or NEDETERMINAT,
        /// 3 otherwise.</returns>
        internal static int Severity(string verdict) => verdict switch
        {
            "OK" or "PERFECT" => 0,
            "BUN" => 1,
            // NEDETERMINAT is not a quality verdict - it means "it ran, but cannot conclude".
            // It sits at 2 because the rule of the exit codes is "anything != 0 means not
            // clean, look", and "I do not know" asks for exactly the same reaction.
            "SUSPECT" or "ACCEPTABIL" or LightSeriesAnalyzer.Undetermined => 2,
            _ => 3,
        };
    }
}
