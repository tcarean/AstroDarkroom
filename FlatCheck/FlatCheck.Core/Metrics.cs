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

// Metrics.cs - the geometry and the pure metrics. Each function is a deterministic transform of
// the raster; none writes anything and none has state.
//
// MIND the centre the callers use: the radial model and the asymmetry are computed around the
// centre of the SENSOR, not around the vertex fitted by FitCenter. See docs/design/pipeline.md,
// "The core idea" (ADR 0013) - centring on the vertex would absorb exactly the gradient being
// looked for.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    /// <summary>
    /// The geometry of a flat (the paraboloid fit, the radial model) and the metrics the
    /// structure and light analyzers compute on it. Every metric is a ratio or
    /// a fraction, so ISO and gain cancel.
    /// </summary>
    internal static class Metrics
    {
        // ========================================================= geometry
        /// <summary>
        /// The fit of a paraboloid on log(I). Two different things come out of the same fit:
        ///
        /// - the <b>vertex</b>, that is the centre of illumination. It is a POSITION, and so it is
        ///   NOT scale-invariant: the vertex is ~ -b/(2d), a ratio between gradient and
        ///   curvature. On a lens with almost no vignetting (small d) a modest ramp throws it
        ///   arbitrarily far; and when it comes to leave the frame, the guard below falls back
        ///   on the sensor's centre. So the metric is also NON-MONOTONIC: above a threshold, the
        ///   larger the defect, the smaller the offset reported, down to 0. Measured: with the
        ///   same 11% gradient, the offset grows from 2.4% at 2 stops of vignetting to 57% at
        ///   0.08 stops, then falls to 0 at 0.05. That is why the offset is reported only as
        ///   information, and no longer gives a verdict.
        ///
        /// - the <b>tilt</b> (<c>tiltPct</c>), that is the peak-to-peak amplitude of the LINEAR
        ///   term over the frame. It is the numerator above taken alone: a fraction, so
        ///   scale-invariant like the other metrics and directly comparable with the residual
        ///   scale. A genuinely off-centre radial field produces a linear term too (~ offset x
        ///   curvature), so the tilt catches real decentring as well - but weighted by what it
        ///   costs photometrically, which is exactly the weighting the offset lacked.
        ///
        /// The cross term f (astigmatism / saddle) is left out of the tilt on purpose: it is not
        /// a tilt, and the residual after the radial model catches it anyway. Mixed into the
        /// same figure, it would make it uninterpretable.
        /// </summary>
        /// <param name="img">The analysis raster; values above 0 (floored at 1e-12 for the
        /// log).</param>
        /// <returns>
        /// The vertex (cx, cy) in pixels, the tilt in percent (NaN on a degenerate raster), and
        /// whether the vertex is valid; when it is not, (cx, cy) is the raster's centre.
        /// </returns>
        internal static (double cx, double cy, double tiltPct, bool vertexValid) FitCenter(Img img)
        {
            int h = img.H, w = img.W;
            // normal equations for A=[1,xn,yn,xn^2,yn^2,xn*yn], 6 unknowns
            int step = Math.Max(1, Math.Min(h, w) / 200);
            var AtA = new double[6, 6];
            var Atb = new double[6];
            for (int y = 0; y < h; y += step)
                for (int x = 0; x < w; x += step)
                {
                    double xn = (x - w / 2.0) / (w / 2.0);
                    double yn = (y - h / 2.0) / (h / 2.0);
                    double val = Math.Log(Math.Max(img.At(y, x), 1e-12));
                    double[] r = { 1, xn, yn, xn * xn, yn * yn, xn * yn };
                    for (int i = 0; i < 6; i++)
                    {
                        Atb[i] += r[i] * val;
                        for (int j = 0; j < 6; j++) AtA[i, j] += r[i] * r[j];
                    }
                }
            double[] coef = Stats.Solve(AtA, Atb, 6);
            // The normal system depends only on the coordinates, not on the values, so it is
            // singular only on a degenerate raster. Then the tilt cannot be computed either:
            // NaN, which falls into the worst band, as everywhere in the tool.
            if (coef == null) return (w / 2.0, h / 2.0, double.NaN, false);
            double b = coef[1], c = coef[2], d = coef[3], e = coef[4], f = coef[5];
            // The peak-to-peak amplitude of the linear term over the frame. |b| + |c| is its
            // value in the corner (where xn = yn = 1), and sinh makes the exact conversion from
            // log to intensity, instead of the linear approximation 2*(|b| + |c|).
            double tilt = 2 * Math.Sinh(Math.Abs(b) + Math.Abs(c)) * 100;
            // M = [[2d, f],[f, 2e]]; sol = M^-1 * [-b,-c]
            double det = 2 * d * 2 * e - f * f;
            if (Math.Abs(det) < 1e-15) return (w / 2.0, h / 2.0, tilt, false);
            double sx = ((-b) * (2 * e) - f * (-c)) / det;
            double sy = ((2 * d) * (-c) - f * (-b)) / det;
            double cx = sx * (w / 2.0) + w / 2.0;
            double cy = sy * (h / 2.0) + h / 2.0;
            if (!(cx > 0 && cx < w && cy > 0 && cy < h)) return (w / 2.0, h / 2.0, tilt, false);
            return (cx, cy, tilt, true);
        }

        // A robust radial profile + a 2D model rebuilt by interpolation.
        //
        // The profile is a median per ring: immune to what is compact (a blotch, dust). Beyond
        // the inscribed circle a ring is only partial arcs, at fixed azimuths (left and right,
        // then only the corners), and any statistic on arcs inherits the field's dependence on
        // azimuth. Img.Symmetrise removes exactly the ODD part (the gradient) beforehand; the
        // non-radial EVEN part - a saddle (xy) or a steeper fall along one axis than along the
        // other (x^2 - y^2) - passes through the symmetrisation untouched, and the median on the
        // left/right arcs absorbed it into the profile: the model's slope jumped from 0.21 to
        // 0.71 % per bin right on the inscribed circle, and the map drew a ridge on it and two
        // lobes with hard edges. Measured on the sky master of the Tokina at 20 (15 + 15 frames
        // rotated by 180 degrees; the sky's saddle, -6 % at the corners, is even and the rotation
        // cannot cancel it) and reproduced on a smooth synthetic with the same coefficients: the
        // map's error against the true residual, outside the circle, 8.9 % with the median and
        // 0.5 % with the correction; the steepest step between 8 px tiles 3.58 -> 0.23 %.
        //
        // THE CORRECTION, on every ring: the profile is the median of
        //     v / (1 + r^2 (q2c cos 2t + q2s sin 2t + q4c cos 4t + q4s sin 4t)),
        // with q the RELATIVE amplitudes per r^2 (a ratio, like every metric of the tool: a sky
        // saddle multiplies, it does not add, and grows with r^2). q is estimated on every
        // identifiable ring by RingFit - on the MEDIANS of the 15-degree sectors, least squares
        // with a cut at 2.5 MAD - and a SINGLE median is taken over the rings from
        // RingQFrom * rIns outwards, whole and partial: a q constant per r^2, so a correction
        // smooth in r, which cannot draw rings; in the corners, where the arcs can no longer
        // separate the terms from the constant (a purely geometric criterion, RingFitMinDet on
        // the Gram determinant of the basis, at the variance of a whole ring), the same q. The
        // whole rings come in because on them the harmonics are exactly identifiable: from arcs
        // alone, the split between 2t and 4t is badly conditioned and the extrapolation towards
        // the unobserved azimuths put +1.3 points on the maximum tile of the Nikkor ISO400 master
        // for no reason (2.9 -> 4.2); anchored, 3.2. a0 stays a median over pixels everywhere,
        // so the profile's noise and robustness are those of before.
        //
        // Why the whole rings too (2026-11-06): until then they kept the plain median, on the
        // reading that a whole ring needs no correction - the median of a sinusoid over a full
        // turn is its mean. True for one harmonic, false for a mix. The Rosette master (Canon
        // 40D, 17 flats) carries 2t and 4t together, +1.43 / -0.60 % at the inscribed radius,
        // and the median of 1.43 cos 2t - 0.60 cos 4t is +0.60 % above its mean: the whole
        // rings' profile sat up to 0.6 % above the level, the first partial ring - corrected -
        // fell back to it (a step of -0.66 % against -0.17 / -0.22 % on its neighbours), and the
        // map drew a hard circle on the inscribed radius. Divided out everywhere, the same q
        // makes the profile continuous (-0.24 %); near the centre q r^2 is negligible and the
        // inner profile is the plain median within noise. Measured on synthetics, worst model /
        // truth error: the Rosette amplitudes 0.51 -> 0.04 %, a pure 11 % saddle and a pure 3 %
        // four-fold term unchanged. See docs/design/radial-model-even-term.md, section 8.
        //
        // Why 4t too (2026-09-23): on arcs of +-30..60 degrees around the x axis, cos 2t and
        // cos 4t are almost collinear, and a fit with 2t alone read a 4t term as 2t. A master
        // from four orientations rotated by 90 degrees has, by construction, NO 2t of the world
        // and ONLY the 4t family (the world's spots softened four times): the old correction had
        // nothing to correct and injected a q2c of +2..+4 % with the sign jumping from one ring
        // to another (LMedS chose other triplets), the profile got a -1.7 % step right on the
        // inscribed circle and +-1 % bands beyond, and the map drew a red ring and a hard circle
        // on a master whose spots are smooth. Measured on synthetics with a known truth
        // (model/truth error, rms): a 6 % 2t saddle - median 0.76 %, 2t 0.03, 2t+4t 0.03; a 3 %
        // 4t term - median 0.17, 2t 1.87 (steps of 2.6 %), 2t+4t 0.03; saddle + 4t, gradient,
        // compact spot, spot + saddle, spot + 4t: 2t+4t under 0.13 %. On the real masters, the
        // maximum step between rings of the residual's mean: rotated master 0.73 -> 0.17 %,
        // monitor 1.01 -> 0.29, sky 0.31 -> 0.31; a single orientation stack read 17.9 %
        // residual (true non-radial content 7.6 %) and reads 7.9. LMedS is no longer needed: the
        // cut at 2.5 MAD in three rounds kept the compact +10 % spot out of the estimate (0.08 %
        // error). See docs/design/radial-model-even-term.md, section 7.
        //
        // What was tried and rejected, in order:
        //  - a0 directly from the per-ring fit (LS with a cut at 3 MAD, then L1): a compact 10 %
        //    spot in a corner, symmetrised into two antipodal halves, IS a 2t signal on its
        //    rings, and the fit absorbed it with a0 moved by 2.3 % - a false ring in the map.
        //  - a global quadratic fit of the even part (astigmatism + saddle) divided out before
        //    the median: it aliases the higher-order radial terms into a false astigmatism
        //    (0.3 % on a perfectly radial field on 3:2) and moves the maximum tile of the Nikkor
        //    800 by 1.3 points for no reason.
        //  - absolute amplitudes held from the last identifiable ring: on a multiplicative
        //    saddle they are wrong by the vignetting's factor (1.4 % in the corner), and a spot
        //    in the last ring propagates into all four corners.
        //  - the 4t term with its amplitude extrapolated into the corners: it exploded (maximum
        //    tile 9.8 -> 28 %). As a relative amplitude per r^2, constant over all the rings, no.
        //  - q smoothed with a sliding median over 7 rings (version 2.2): on the masters with
        //    spots it leaves steps of 0.7-1 % between rings; a single median over the rings
        //    does not.

        // below this the 2t+4t basis can no longer be separated on a ring's arcs (Gram
        // determinant, at the variance of a whole ring)
        internal const double RingFitMinDet = 0.02;
        internal const int RingSectors = 24;          // sectors of 15 degrees; the per-ring fit is on their medians
        internal const int RingMinSectors = 6;        // below this many populated sectors the ring is not estimated
        internal const int RingFitRounds = 3;         // rounds of least squares with a cut at 2.5 MAD
        // q is the median over the rings from this fraction of the inscribed radius outwards,
        // whole and partial
        internal const double RingQFrom = 0.75;
        internal const double RingFitTie = 1e-9;      // tolerance (relative to the level) when choosing inliers, for noiseless scenes

        /// <summary>
        /// The radial profile around (<paramref name="cx"/>, <paramref name="cy"/>) and the 2D
        /// model interpolated from it: on every ring, whole or partial, the median after dividing
        /// out the even 2t and 4t terms (the correction described above, and in
        /// docs/design/radial-model-even-term.md), one relative amplitude per r^2 estimated on the
        /// rings from RingQFrom times the inscribed radius <c>min(cx, cy)</c> outwards. When no
        /// such ring is identifiable, there is no correction and every ring takes the plain
        /// median. Rings with fewer than 8 pixels are left out.
        /// </summary>
        /// <param name="img">The raster, normally symmetrised
        /// (<see cref="Img.Symmetrise"/>).</param>
        /// <param name="cx">The centre's column, in pixels; the sensor's centre, not the
        /// vertex.</param>
        /// <param name="cy">The centre's row, in pixels.</param>
        /// <param name="nbins">How many rings out to the farthest pixel.</param>
        /// <returns>
        /// The ring centres in pixels, the profile at each, and the model raster of the same
        /// size as <paramref name="img"/>.
        /// </returns>
        internal static (double[] centers, double[] prof, Img model) RadialModel(Img img, double cx, double cy, int nbins = 80)
        {
            int h = img.H, w = img.W;
            double rmax = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (r > rmax) rmax = r;
                }
            var binVals = new List<double>[nbins];
            var binCos = new List<double>[nbins];
            var binSin = new List<double>[nbins];
            var binSector = new List<int>[nbins];
            for (int i = 0; i < nbins; i++)
            {
                binVals[i] = new List<double>();
                binCos[i] = new List<double>();
                binSin[i] = new List<double>();
                binSector[i] = new List<int>();
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    int idx = Math.Min((int)(r / rmax * nbins), nbins - 1);
                    double t = Math.Atan2(y - cy, x - cx);
                    binVals[idx].Add(img.At(y, x));
                    binCos[idx].Add(Math.Cos(2 * t));
                    binSin[idx].Add(Math.Sin(2 * t));
                    binSector[idx].Add(Math.Min((int)((t + Math.PI) / (2 * Math.PI) * RingSectors), RingSectors - 1));
                }
            double rIns = Math.Min(cx, cy);
            var ringBins = new List<int>();
            for (int i = 0; i < nbins; i++)
                if (binVals[i].Count >= 8) ringBins.Add(i);
            // the relative amplitudes per r^2 on the identifiable rings from RingQFrom * rIns
            // outwards - whole (where the harmonics are exactly identifiable and anchor the
            // estimate) and partial - then their median
            var qRows = new List<double>[4];
            for (int j = 0; j < 4; j++) qRows[j] = new List<double>();
            foreach (int i in ringBins)
            {
                double center = (i + 0.5) / nbins * rmax;
                if (center < RingQFrom * rIns) continue;
                var fit = RingFit(binVals[i], binCos[i], binSin[i], binSector[i]);
                if (fit == null) continue;
                for (int j = 0; j < 4; j++) qRows[j].Add(fit[j + 1] / fit[0] / (center * center));
            }
            double[] q = null;
            if (qRows[0].Count > 0)
            {
                q = new double[4];
                for (int j = 0; j < 4; j++) q[j] = Stats.Percentile(qRows[j], 50);
            }
            var centers = new List<double>();
            var prof = new List<double>();
            foreach (int i in ringBins)
            {
                double center = (i + 0.5) / nbins * rmax;
                centers.Add(center);
                if (q == null)
                {
                    prof.Add(Stats.Percentile(binVals[i], 50));
                    continue;
                }
                var v = binVals[i]; var c = binCos[i]; var s = binSin[i];
                double r2 = center * center;
                var corrected = new List<double>(v.Count);
                for (int p = 0; p < v.Count; p++)
                {
                    double c4 = 2 * c[p] * c[p] - 1, s4 = 2 * s[p] * c[p];
                    corrected.Add(v[p] / (1 + r2 * (q[0] * c[p] + q[1] * s[p] + q[2] * c4 + q[3] * s4)));
                }
                prof.Add(Stats.Percentile(corrected, 50));
            }
            var ca = centers.ToArray();
            var pa = prof.ToArray();
            var model = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    model.Set(y, x, Stats.Interp(r, ca, pa));
                }
            return (ca, pa, model);
        }

        /// <summary>
        /// One identifiable ring: (a0, a2, b2, a4, b4) of
        /// a0 + a2 cos 2t + b2 sin 2t + a4 cos 4t + b4 sin 4t over the sectors' medians, by
        /// weighted least squares (weight = the sector's pixels), RingFitRounds rounds, the
        /// sectors with a residual above 2.5 MAD being removed between rounds - a compact spot
        /// which, symmetrised, takes 2-4 sectors of 24, is out by the second round. Null when the
        /// basis is not identifiable on the arcs (the Gram determinant of the four non-constant
        /// columns, at the variance of a whole ring, under RingFitMinDet - pure geometry, in the
        /// corners) or when there are fewer than RingMinSectors populated sectors. Numeric pair:
        /// <c>_ring_fit</c> of flatcheck.py.
        /// </summary>
        /// <param name="v">The ring's pixel values.</param>
        /// <param name="c">cos 2t of each pixel's azimuth.</param>
        /// <param name="s">sin 2t of each pixel's azimuth.</param>
        /// <param name="sector">Each pixel's sector, 0..RingSectors-1.</param>
        /// <returns>The five coefficients, a0 first; null when the ring is not
        /// identifiable.</returns>
        static double[] RingFit(List<double> v, List<double> c, List<double> s, List<int> sector)
        {
            int n = v.Count;
            var med = new List<double>(); var rows = new List<double[]>(); var wt = new List<double>();
            for (int k = 0; k < RingSectors; k++)
            {
                var vals = new List<double>();
                double sumC = 0, sumS = 0, sumC4 = 0, sumS4 = 0;
                for (int i = 0; i < n; i++)
                    if (sector[i] == k)
                    {
                        vals.Add(v[i]);
                        sumC += c[i]; sumS += s[i];
                        sumC4 += 2 * c[i] * c[i] - 1; sumS4 += 2 * s[i] * c[i];
                    }
                if (vals.Count < 8) continue;
                int cnt = vals.Count;
                med.Add(Stats.Percentile(vals, 50));
                rows.Add(new[] { 1.0, sumC / cnt, sumS / cnt, sumC4 / cnt, sumS4 / cnt });
                wt.Add(cnt);
            }
            int m = med.Count;
            if (m < RingMinSectors) return null;

            // identifiability: the determinant of the Gram matrix of the centred non-constant
            // columns, normalised to 1/2 - the variance of a harmonic on a whole ring - so 1 on a
            // whole ring and towards 0 on narrow arcs. NOT through correlation: on the arcs in the
            // corners of a square frame the correlation would give 1 however narrow they are,
            // with a meaningless fit.
            var mean = new double[4];
            for (int j = 0; j < 4; j++)
            {
                for (int p = 0; p < m; p++) mean[j] += rows[p][j + 1];
                mean[j] /= m;
            }
            var gram = new double[4, 4];
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 4; b++)
                {
                    double sum = 0;
                    for (int p = 0; p < m; p++) sum += (rows[p][a + 1] - mean[a]) * (rows[p][b + 1] - mean[b]);
                    gram[a, b] = sum / m / 0.5;
                }
            double det = Determinant(gram, 4);
            if (!(det >= RingFitMinDet)) return null;

            var inlier = new bool[m];
            for (int p = 0; p < m; p++) inlier[p] = true;
            double[] sol = null;
            var res = new double[m];
            for (int round = 0; round < RingFitRounds; round++)
            {
                var A = new double[5, 5]; var b = new double[5];
                for (int p = 0; p < m; p++)
                {
                    if (!inlier[p]) continue;
                    for (int a = 0; a < 5; a++)
                    {
                        b[a] += wt[p] * rows[p][a] * med[p];
                        for (int bb = 0; bb < 5; bb++) A[a, bb] += wt[p] * rows[p][a] * rows[p][bb];
                    }
                }
                sol = Stats.Solve(A, b, 5);
                if (sol == null) return null;
                for (int p = 0; p < m; p++)
                {
                    double e = med[p];
                    for (int a = 0; a < 5; a++) e -= rows[p][a] * sol[a];
                    res[p] = e;
                }
                double centre = Stats.Percentile((double[])res.Clone(), 50);
                var dev = new double[m];
                for (int p = 0; p < m; p++) dev[p] = Math.Abs(res[p] - centre);
                double mad = Stats.Percentile(dev, 50) * 1.4826;
                int inliers = 0;
                for (int p = 0; p < m; p++)
                {
                    inlier[p] = Math.Abs(res[p]) <= 2.5 * mad + RingFitTie * Math.Abs(sol[0]);
                    if (inlier[p]) inliers++;
                }
                if (inliers < RingMinSectors)
                    for (int p = 0; p < m; p++) inlier[p] = true;
            }
            return sol;
        }

        /// <summary>
        /// The determinant of an n x n matrix, by Gaussian elimination with partial pivoting.
        /// </summary>
        /// <param name="src">The matrix; not modified.</param>
        /// <param name="n">Its size.</param>
        /// <returns>
        /// The determinant; exactly 0 when, at some step of the elimination, the current column
        /// has no non-zero entry on or below the diagonal.
        /// </returns>
        static double Determinant(double[,] src, int n)
        {
            var a = (double[,])src.Clone();
            double det = 1;
            for (int col = 0; col < n; col++)
            {
                int piv = col;
                for (int r = col + 1; r < n; r++)
                    if (Math.Abs(a[r, col]) > Math.Abs(a[piv, col])) piv = r;
                if (a[piv, col] == 0) return 0;
                if (piv != col)
                {
                    for (int k = 0; k < n; k++) { double tmp = a[col, k]; a[col, k] = a[piv, k]; a[piv, k] = tmp; }
                    det = -det;
                }
                det *= a[col, col];
                for (int r = col + 1; r < n; r++)
                {
                    double f = a[r, col] / a[col, col];
                    for (int k = col; k < n; k++) a[r, k] -= f * a[col, k];
                }
            }
            return det;
        }

        // ========================================================= metrics
        /// <summary>
        /// The median of each corner's box and of the centre's, the boxes a fraction
        /// <paramref name="frac"/> of the frame's sides (at least 4 px); the centre's box is twice
        /// as large each way.
        /// </summary>
        /// <param name="img">The raster.</param>
        /// <param name="frac">The corner box's side, as a fraction of the frame's height and
        /// width.</param>
        /// <returns>
        /// The four corners' medians keyed by their Romanian names ("stanga-sus" ...), and the
        /// centre's median.
        /// </returns>
        internal static (Dictionary<string, double> vals, double ctr) CornerStats(Img img, double frac = 0.05)
        {
            int h = img.H, w = img.W;
            int bh = Math.Max(4, (int)(h * frac)), bw = Math.Max(4, (int)(w * frac));
            double Med(int y0, int y1, int x0, int x1)
            {
                var l = new List<double>();
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) l.Add(img.At(y, x));
                return Stats.Percentile(l, 50);
            }
            var vals = new Dictionary<string, double>
            {
                ["stanga-sus"] = Med(0, bh, 0, bw),
                ["dreapta-sus"] = Med(0, bh, w - bw, w),
                ["stanga-jos"] = Med(h - bh, h, 0, bw),
                ["dreapta-jos"] = Med(h - bh, h, w - bw, w),
            };
            double ctr = Med(h / 2 - bh, h / 2 + bh, w / 2 - bw, w / 2 + bw);
            return (vals, ctr);
        }

        // Ptp between sectors, per ring. Only within the inscribed radius (all azimuths sampled).
        /// <summary>
        /// The azimuthal asymmetry: per ring, between 0.15 and 1 of the inscribed radius, the
        /// peak-to-peak of the sector medians over their median. A ring is reported only when at
        /// most one sector has fewer than 8 pixels.
        /// </summary>
        /// <param name="img">The raster.</param>
        /// <param name="cx">The centre's column, in pixels; the sensor's centre.</param>
        /// <param name="cy">The centre's row, in pixels.</param>
        /// <param name="nsect">How many sectors per ring.</param>
        /// <param name="nrings">How many rings.</param>
        /// <returns>Per ring reported, its mid radius as a fraction of the inscribed one and the
        /// peak-to-peak in percent.</returns>
        internal static List<(double r, double ptp)> AzimuthalAsymmetry(Img img, double cx, double cy, int nsect = 12, int nrings = 10)
        {
            int h = img.H, w = img.W;
            double rIns = Math.Min(Math.Min(cx, cy), Math.Min(w - cx, h - cy));
            var edges = new double[nrings + 1];
            for (int i = 0; i <= nrings; i++) edges[i] = 0.15 * rIns + (rIns - 0.15 * rIns) * i / nrings;
            var buckets = new List<double>[nrings, nsect];
            for (int i = 0; i < nrings; i++) for (int s = 0; s < nsect; s++) buckets[i, s] = new List<double>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double dx = x - cx, dy = y - cy;
                    double r = Math.Sqrt(dx * dx + dy * dy);
                    if (r < edges[0] || r >= edges[nrings]) continue;
                    double th = Math.Atan2(dy, dx);
                    int s = Math.Min((int)((th + Math.PI) / (2 * Math.PI) * nsect), nsect - 1);
                    int ri = -1;
                    for (int i = 0; i < nrings; i++) if (r >= edges[i] && r < edges[i + 1]) { ri = i; break; }
                    if (ri >= 0) buckets[ri, s].Add(img.At(y, x));
                }
            var outl = new List<(double, double)>();
            for (int i = 0; i < nrings; i++)
            {
                var meds = new List<double>();
                for (int s = 0; s < nsect; s++) if (buckets[i, s].Count >= 8) meds.Add(Stats.Percentile(buckets[i, s], 50));
                if (meds.Count >= nsect - 1)
                {
                    double mn = meds.Min(), mx = meds.Max(), md = Stats.Percentile(meds, 50);
                    outl.Add(((edges[i] + edges[i + 1]) / 2 / rIns, (mx - mn) / md * 100));
                }
            }
            return outl;
        }

        // Large-scale structure (isolated from the noise by averaging over tiles).
        /// <summary>
        /// The tile side the large-scale metrics use. Taken out on its own because two consumers
        /// read it - <see cref="LargeScalePtp"/> and the attribution mode - and two formulas
        /// would have diverged at the first adjustment of ntiles.
        /// </summary>
        /// <param name="src">The raster to tile.</param>
        /// <param name="ntiles">How many tiles across the width.</param>
        /// <returns>The tile side in pixels, at least 2.</returns>
        internal static int TileSize(Img src, int ntiles = 40)
            => Math.Max(2, (int)(src.W / (double)ntiles));

        /// <summary>
        /// p95 - p5 of the tiles, over the WHOLE frame.
        ///
        /// At first it took only the tiles in the inscribed circle, because beyond it the radial
        /// profile came from partial arcs and could not be trusted. Symmetrisation (see
        /// <see cref="Img.Symmetrise"/>) solved exactly that, so the restriction had stayed for
        /// no reason - and it cost a lot: on a 3:2 frame the inscribed circle is 53% of the
        /// tiles, and the corners, where the light leaks live and where the gradient is largest,
        /// were not seen at all. Because of it too the same gradient read 6.5% on the long axis
        /// and 9.7% on the short one; over the whole frame it reads 10.4 / 10.6. The floor on a
        /// clean radial field stayed under 0.1% at the analysis resolution.
        /// </summary>
        /// <param name="resid">The residual raster (image / model), centred on 1.</param>
        /// <param name="ntiles">How many tiles across the width.</param>
        /// <returns>The spread as a fraction (not percent); NaN with fewer than 10 tiles.</returns>
        internal static double LargeScalePtp(Img resid, int ntiles = 40)
        {
            int k = TileSize(resid, ntiles);
            Img t = Img.BlockMean(resid, k);
            if (t.D.Length < 10) return double.NaN;
            return Stats.Percentile(t.D, 95) - Stats.Percentile(t.D, 5);
        }

        /// <summary>
        /// The largest deviation of a single tile from the model, in absolute value, over the
        /// whole frame. A fraction, not a percent.
        ///
        /// It exists because <see cref="LargeScalePtp"/> cuts 5% of the tiles at each end, so a
        /// COMPACT defect - a small light leak, a condensation spot - that covers under 5% of
        /// the frame disappears from it completely, however deep it is. Measured: a finger-sized
        /// +10% spot in a corner read 0.04% on the residual and the verdict PERFECT, with the
        /// spot perfectly visible in the map. Here it reads 9.3%.
        ///
        /// It is an upper bound too, like the others: a deep dust shadow (aperture closed down)
        /// is fixed on the camera and divides out correctly from the lights of the same session,
        /// but it shows here all the same. For that there is <see cref="LocalSpots"/> and the
        /// report's warning.
        /// </summary>
        /// <param name="resid">The residual raster (image / model), centred on 1.</param>
        /// <param name="ntiles">How many tiles across the width.</param>
        /// <returns>The largest |tile - 1|, as a fraction.</returns>
        internal static double MaxTileDeviation(Img resid, int ntiles = 40)
        {
            Img t = Img.BlockMean(resid, TileSize(resid, ntiles));
            double worst = 0;
            foreach (double v in t.D) worst = Math.Max(worst, Math.Abs(v - 1.0));
            return worst;
        }

        /// <summary>
        /// The deepest COMPACT spot: each fine tile minus the median of its neighbourhood, and
        /// the largest in absolute value is kept, with its sign and its position (in pixels of
        /// the analysis raster). A fraction, not a percent.
        ///
        /// Subtracting the local median is what sets it apart from
        /// <see cref="MaxTileDeviation"/>: a gradient is smooth, so the neighbourhood's median
        /// follows it and the difference comes out ~0 (measured: 0.46% at a 12% gradient, 0.68%
        /// at 25%), whereas a dust shadow, a ring or a small leak stay whole (4.99% on a 5%
        /// shadow, and still 4.71% when the shadow sits under a 12% gradient with noise). So it
        /// answers a question the other figures cannot ask: IS THERE SOMETHING SMALL AND FIXED
        /// ON THE CAMERA? The answer matters differently from a verdict: such a spot is right for
        /// the session whose lights have it too, and wrong for a library flat. That is why it
        /// does not vote - it gives a warning.
        ///
        /// The tiles are twice as fine as for the other two metrics (80 across the width, ~18 px
        /// at the analysis resolution), so that a 60 px dust shadow is not diluted; the
        /// neighbourhood is 7x7 tiles, that is ~250 px on the sensor, wider than any dust spot on
        /// a DSLR.
        ///
        /// ONLY the tiles with a whole neighbourhood are evaluated, so without a border of
        /// <paramref name="win"/> tiles (~55 px at the analysis resolution, ~110 on the sensor).
        /// At the edge the neighbourhood is cut on one side, its median no longer follows a
        /// gradient, and the difference comes from the gradient, not from a spot: measured on
        /// 256 px, -0.76% in the corner on a CLEAN field and -1.61% under a 60% gradient - one
        /// step from the 2% warning, for a dust that does not exist. A spot right on the edge is
        /// caught anyway by <see cref="MaxTileDeviation"/>.
        ///
        /// The floor: under 0.1% on a clean field, 1.1% at 10% noise per sensor pixel; the real
        /// masters have 0.2-0.3% noise. On a real master of the Nikkor 35 at f/2.8 the dust
        /// shadow visible in the map has -0.31%, so under any threshold - as the documentation
        /// says too, that at wide apertures the dust is under the threshold.
        /// </summary>
        /// <param name="resid">The residual raster (image / model), centred on 1.</param>
        /// <param name="ntiles">How many tiles across the width.</param>
        /// <param name="win">The neighbourhood's half side, in tiles.</param>
        /// <returns>
        /// The signed deviation as a fraction, and the spot tile's centre (x, y) in pixels of
        /// <paramref name="resid"/>.
        /// </returns>
        internal static (double dev, int x, int y) LocalSpots(Img resid, int ntiles = 80, int win = 3)
        {
            int k = TileSize(resid, ntiles);
            Img t = Img.BlockMean(resid, k);
            double worst = 0; int wx = win, wy = win;
            var nb = new List<double>((2 * win + 1) * (2 * win + 1));
            for (int y = win; y < t.H - win; y++)
                for (int x = win; x < t.W - win; x++)
                {
                    nb.Clear();
                    for (int dy = -win; dy <= win; dy++)
                        for (int dx = -win; dx <= win; dx++)
                            nb.Add(t.At(y + dy, x + dx));
                    double hp = t.At(y, x) - Stats.Percentile(nb, 50);
                    // A tolerance on equality: on a symmetric scene two tiles can come out equal
                    // to the last bit, and C# and numpy sum in a different order, so they would
                    // pick a different tile. Under 1e-12 the first in row order stays, in both.
                    if (Math.Abs(hp) > Math.Abs(worst) + 1e-12) { worst = hp; wx = x; wy = y; }
                }
            return (worst, (int)((wx + 0.5) * k), (int)((wy + 0.5) * k));
        }

        // Local background per tile (a low percentile = robust to stars/nebulosity).
        /// <summary>
        /// The local background of a light: a low percentile of each whole tile, with the tile's
        /// centre. The partial tiles at the right and bottom are dropped.
        /// </summary>
        /// <param name="img">The light's analysis raster.</param>
        /// <param name="ntiles">How many tiles across the width; a tile is at least 4 px.</param>
        /// <param name="pct">The percentile taken per tile, 0..100.</param>
        /// <returns>
        /// Per tile, row-major: the background, the centre's x and y in pixels; then the grid's
        /// height and width in tiles and the tile side in pixels.
        /// </returns>
        internal static (double[] bg, double[] bx, double[] by, int th, int tw, int k) TileBackground(Img img, int ntiles = 40, double pct = 15)
        {
            int h = img.H, w = img.W;
            int k = Math.Max(4, (int)(w / (double)ntiles));
            int hh = h / k, ww = w / k;
            var bg = new double[hh * ww];
            var bx = new double[hh * ww];
            var by = new double[hh * ww];
            for (int ty = 0; ty < hh; ty++)
                for (int tx = 0; tx < ww; tx++)
                {
                    var vals = new List<double>();
                    for (int dy = 0; dy < k; dy++)
                        for (int dx = 0; dx < k; dx++)
                            vals.Add(img.At(ty * k + dy, tx * k + dx));
                    int idx = ty * ww + tx;
                    bg[idx] = Stats.Percentile(vals, pct);
                    bx[idx] = (tx + 0.5) * k;
                    by[idx] = (ty + 0.5) * k;
                }
            return (bg, bx, by, hh, ww, k);
        }
    }
}
