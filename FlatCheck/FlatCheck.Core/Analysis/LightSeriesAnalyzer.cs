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

// LightSeriesAnalyzer.cs - mode 3 on a whole series of frames.
//
// WHY A SINGLE FRAME IS NOT ENOUGH. The radial component reported by --light is the sum of
// two things: the flat error, which is THE SAME on every frame by construction (the same master
// flat divided each of them), and a radial term of the sky, which changes during the session.
// On a real series (32 frames, 81 minutes, the Milky Way at 20 mm) the figure swept smoothly
// from +3.13 % through -19.20 % to +35.73 %, with only 1.36 points between consecutive frames.
// Everything that moves like that is sky.
//
// The mean over the series estimates the flat error ONLY if the sky term averages out to
// zero. That is not guaranteed, so it is not assumed - it is checked, by three conditions, and
// if any one fails the verdict is NEDETERMINAT instead of a number that looks confident.
//
// The second condition is the interesting one: if the "flat error" FOLLOWS the sky gradient
// over the series, then what is measured is the sky, because the flat has no way to change from
// one frame to the next. On the series above the correlation came out -0.94.
//
// WHAT IT DOES NOT DO: it does not try to extrapolate to zero sky gradient. The regression
// radPct ~ planePct on the real series gives an intercept of +122 %, an extrapolation far
// outside the data (the observed gradient lies between 49 and 88 %). The correlation is good as
// a diagnostic and useless as an estimator; do not turn it into one.
//
// The combining is pure: it takes results already computed, it does not touch the disk. The
// loading of the files sits in Runs/LightSeriesRun.cs, as every mode's loading sits in Runs/.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    /// <summary>
    /// A series of lights combined: every frame's result, the statistics of the radial
    /// component over them, the sky correlation, and the verdict or the reasons it is
    /// undetermined.
    /// </summary>
    internal sealed class LightSeriesResult
    {
        internal string[] Names;
        internal LightResult[] Frames;

        internal double MeanRad, MinRad, MaxRad, StdDev, Sem;
        internal double SkyCorr = double.NaN;   // NaN when it cannot be computed
        internal double SkySpan;                // the range of the sky gradient, points
        internal bool CorrApplies;              // the gradient varies enough to matter

        internal Img RadialMap;                 // the mean radial map over the frames
        internal string Verdict;
        internal string[] Reasons;              // empty when the verdict is conclusive

        internal int Count => Frames.Length;
        internal bool Conclusive => Reasons.Length == 0;
    }

    /// <summary>
    /// Mode 3 on a series: combines the <see cref="LightResult"/>s of many lights calibrated
    /// with the same master and gives a verdict only when the sky term can be shown to cancel.
    /// </summary>
    internal static class LightSeriesAnalyzer
    {
        internal const string Undetermined = "NEDETERMINAT";

        /// <summary>
        /// Combines the frames' results: the mean radial component and its standard error,
        /// the correlation with the sky gradient, the mean map, and the verdict on |mean|
        /// against <see cref="Th.LightRadOk"/> / <see cref="Th.LightRadBad"/> - or
        /// <see cref="Undetermined"/> when fewer than <see cref="Th.LightSeriesMinFrames"/>
        /// frames, a standard error not below <see cref="Th.LightSeriesSemMax"/>, or an absolute
        /// correlation not below <see cref="Th.LightSeriesSkyCorr"/> (when the sky span exceeds
        /// <see cref="Th.LightSeriesSkySpanMin"/>). Throws on an empty series and on frames of
        /// different sizes.
        /// </summary>
        /// <param name="frames">Each frame's name and its <see cref="LightAnalyzer"/> result, in
        /// series order; at least one.</param>
        /// <returns>The combined result; never null.</returns>
        internal static LightSeriesResult Combine(IReadOnlyList<(string Name, LightResult R)> frames)
        {
            if (frames == null || frames.Count == 0)
                throw new Exception("Serie goala: niciun cadru de analizat.");

            var names = frames.Select(f => f.Name).ToArray();
            var results = frames.Select(f => f.R).ToArray();
            var rad = results.Select(r => r.RadPct).ToArray();
            var plane = results.Select(r => r.PlanePct).ToArray();
            int n = rad.Length;

            double mean = rad.Average();
            double std = StdDev(rad, mean);
            double sem = n >= 2 ? std / Math.Sqrt(n) : double.NaN;

            double skySpan = plane.Max() - plane.Min();
            bool corrApplies = skySpan > Th.LightSeriesSkySpanMin;
            double corr = Pearson(rad, plane);

            // The verdict is given only if ALL the conditions pass. The comparisons are written
            // as !(x < threshold) and not as (x >= threshold) on purpose: that way NaN falls on
            // the bad side, as in Stats.Verdict. A sem that comes out NaN means the spread
            // could not be computed, which is a reason for NEDETERMINAT, not for a silent
            // pass.
            var reasons = new List<string>();
            if (n < Th.LightSeriesMinFrames)
                reasons.Add($"prea putine cadre ({n}, minim {Th.LightSeriesMinFrames})");
            if (!(sem < Th.LightSeriesSemMax))
                reasons.Add("sem");
            if (corrApplies && !(Math.Abs(corr) < Th.LightSeriesSkyCorr))
                reasons.Add("corelatie");

            return new LightSeriesResult
            {
                Names = names,
                Frames = results,
                MeanRad = mean,
                MinRad = rad.Min(),
                MaxRad = rad.Max(),
                StdDev = std,
                Sem = sem,
                SkyCorr = corr,
                SkySpan = skySpan,
                CorrApplies = corrApplies,
                RadialMap = MeanMap(results),
                Reasons = reasons.ToArray(),
                Verdict = reasons.Count > 0
                    ? Undetermined
                    : Stats.Verdict(Math.Abs(mean), Th.LightRadOk, Th.LightRadBad),
            };
        }

        /// <summary>The sample standard deviation (ddof = 1), like numpy.std(ddof=1).</summary>
        /// <param name="v">The values.</param>
        /// <param name="mean">Their mean.</param>
        /// <returns>The standard deviation; NaN for fewer than two values.</returns>
        internal static double StdDev(double[] v, double mean)
        {
            if (v.Length < 2) return double.NaN;
            double s = 0;
            foreach (double x in v) s += (x - mean) * (x - mean);
            return Math.Sqrt(s / (v.Length - 1));
        }

        /// <summary>
        /// The Pearson correlation. NaN if either series is constant - a case in which the
        /// question "do they move together?" has no answer, not the answer "no".
        /// </summary>
        /// <param name="a">The first series.</param>
        /// <param name="b">The second series, as long as the first.</param>
        /// <returns>The correlation, -1..1; NaN for fewer than two values, different lengths or
        /// a constant series.</returns>
        internal static double Pearson(double[] a, double[] b)
        {
            int n = a.Length;
            if (n < 2 || b.Length != n) return double.NaN;
            double ma = a.Average(), mb = b.Average();
            double sab = 0, saa = 0, sbb = 0;
            for (int i = 0; i < n; i++)
            {
                double da = a[i] - ma, db = b[i] - mb;
                sab += da * db; saa += da * da; sbb += db * db;
            }
            double den = Math.Sqrt(saa * sbb);
            return den <= 0 ? double.NaN : sab / den;
        }

        /// <summary>
        /// The mean radial map over the frames. All must have the same tile grid; otherwise the
        /// mean makes no sense and it is better to stop here (it throws) than to produce a mixed
        /// map.
        /// </summary>
        /// <param name="results">The frames' results; at least one.</param>
        /// <returns>The pixel-wise mean of their radial maps.</returns>
        static Img MeanMap(LightResult[] results)
        {
            var first = results[0].RadialMap;
            foreach (var r in results)
                if (r.RadialMap.H != first.H || r.RadialMap.W != first.W)
                    throw new Exception(
                        "Cadrele din serie nu au aceeasi dimensiune - nu pot fi mediate. "
                        + "Foloseste doar cadre din aceeasi sesiune, necropate.");

            var o = new Img(first.H, first.W);
            foreach (var r in results)
                for (int i = 0; i < o.D.Length; i++) o.D[i] += r.RadialMap.D[i];
            for (int i = 0; i < o.D.Length; i++) o.D[i] /= results.Length;
            return o;
        }
    }
}
