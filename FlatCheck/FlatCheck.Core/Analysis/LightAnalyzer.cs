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

// LightAnalyzer.cs - mode 3: the end-to-end test, the only one that closes the loop.
//
// The local background (a low percentile per tile) is fitted simultaneously with a plane and two
// radial terms:
//
//     bg = a + b*X + c*Y + d*R2 + e*R2^2        X, Y normalised to the half-diagonal
//
// The plane absorbs the light-pollution gradient; the radial terms capture the residual
// vignetting, that is the flat error. The simultaneous fit is what makes the test work on a
// polluted sky too: the two have different shapes and separate.
//
// BUT ONLY ON A NARROW FIELD. The separation above holds as long as the pollution dome is
// quasi-linear across the frame. On a wide field it curves inside the frame, and the
// radially symmetric part of the curvature has exactly the shape of vignetting - and then
// nothing written here can tell them apart any more. See LightSuitability, which stops the mode
// before it gets here, and the section "The validity limit" in docs/design/modes-light.md.
//
// WARNING: do NOT replace with percentiles on circular rings - p20 on a ring catches the dark
// part of the sky gradient and reports it as false vignetting.

using System;
using System.Collections.Generic;
using System.Linq;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// The background fit of one calibrated light: the five coefficients, the radial component
    /// at the corner (the flat error) and the plane (the sky gradient), and the verdict.
    /// </summary>
    internal sealed class LightResult
    {
        internal double A, B, C, D, E;       // the fit's coefficients
        internal double RadPct;              // -(d+e)/a*100; the SIGN is the diagnosis
        internal double PlanePct;            // the sky gradient, to be ignored
        internal int KeptTiles, TotalTiles;
        internal int TileRows, TileCols;
        internal Img RadialMap;              // 1 + (d*R2 + e*R2^2)/a, for the map
        internal string Verdict;
    }

    /// <summary>
    /// Mode 3, <c>--light</c>: does the flat correct a real light? Fits the light's background
    /// with a plane and two radial terms and reads the radial ones as the flat error.
    /// </summary>
    internal static class LightAnalyzer
    {
        /// <summary>
        /// Fits plane + radial terms to the tile backgrounds of a calibrated light, with a
        /// one-sided sigma clip, and gives the verdict on |RadPct| against
        /// <see cref="Th.LightRadOk"/> and <see cref="Th.LightRadBad"/>. Throws when the fit is
        /// singular already at the first iteration (too few usable tiles). Run
        /// <see cref="LightSuitability.Check"/> first: on a wide field the answer means nothing.
        /// </summary>
        /// <param name="raw">The calibrated light, as loaded; it is prepared here.</param>
        /// <returns>The coefficients, the radial and plane components in percent, the radial map
        /// and the verdict.</returns>
        internal static LightResult Analyze(Img raw)
        {
            Img img = Img.Prepare(raw);
            int h = img.H, w = img.W;
            double cx = Metrics.Centre(w), cy = Metrics.Centre(h);
            double rCorner = Math.Sqrt(cx * cx + cy * cy);

            var (bg, bx, by, th, tw, _) = Metrics.TileBackground(img);
            int n = bg.Length;

            var X = new double[n]; var Y = new double[n]; var R2 = new double[n]; var V = bg;
            for (int i = 0; i < n; i++)
            {
                X[i] = (bx[i] - cx) / rCorner;
                Y[i] = (by[i] - cy) / rCorner;
                R2[i] = X[i] * X[i] + Y[i] * Y[i];
            }

            var keep = new bool[n];
            for (int i = 0; i < n; i++) keep[i] = true;
            double[] coef = null;

            // Iterative sigma clip, ONE-SIDED (res < threshold): the contaminants - stars and
            // nebulosity - add only positive signal, so only the top is cut.
            for (int iter = 0; iter < 4; iter++)
            {
                var AtA = new double[5, 5]; var Atb = new double[5];
                for (int i = 0; i < n; i++)
                {
                    if (!keep[i]) continue;
                    double[] r = { 1, X[i], Y[i], R2[i], R2[i] * R2[i] };
                    for (int a = 0; a < 5; a++)
                    {
                        Atb[a] += r[a] * V[i];
                        for (int b = 0; b < 5; b++) AtA[a, b] += r[a] * r[b];
                    }
                }
                coef = Stats.Solve(AtA, Atb, 5);
                if (coef == null) break;
                var res = new double[n];
                var kept = new List<double>();
                for (int i = 0; i < n; i++)
                {
                    res[i] = V[i] - (coef[0] + coef[1] * X[i] + coef[2] * Y[i]
                                   + coef[3] * R2[i] + coef[4] * R2[i] * R2[i]);
                    if (keep[i]) kept.Add(res[i]);
                }
                double medr = Stats.Percentile(kept, 50);
                var absdev = kept.Select(x => Math.Abs(x - medr)).ToList();
                double s = 1.4826 * Stats.Percentile(absdev, 50);
                double thr = 2.5 * Math.Max(s, 1e-9);
                for (int i = 0; i < n; i++) keep[i] = res[i] < thr;
            }

            // If Solve failed right at the first iteration, coef is still null. Before
            // phase 8 the next line threw NullReferenceException; now it says why.
            if (coef == null)
                throw new Exception(
                    T("The background fit failed (singular system) - too few usable tiles.",
                      "Fitul fondului a esuat (sistem singular) - prea putine tile-uri utilizabile."));

            double a0 = coef[0], b0 = coef[1], c0 = coef[2], d0 = coef[3], e0 = coef[4];
            double radAtCorner = d0 + e0;                       // R2 = 1 at the corner
            double radPct = -radAtCorner / a0 * 100;            // + = the background falls toward the edges
            double planePct = Math.Sqrt(b0 * b0 + c0 * c0) / a0 * 100;

            var radMap = new Img(th, tw);
            for (int i = 0; i < n; i++)
                radMap.D[i] = 1.0 + (d0 * R2[i] + e0 * R2[i] * R2[i]) / a0;

            return new LightResult
            {
                A = a0,
                B = b0,
                C = c0,
                D = d0,
                E = e0,
                RadPct = radPct,
                PlanePct = planePct,
                KeptTiles = keep.Count(x => x),
                TotalTiles = n,
                TileRows = th,
                TileCols = tw,
                RadialMap = radMap,
                Verdict = Stats.Verdict(Math.Abs(radPct), Th.LightRadOk, Th.LightRadBad),
            };
        }
    }
}
