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

// SmoothAnalyzer.cs - mode 6: the SMOOTHED flat. It keeps everything that is smooth in the
// master and throws away everything that is not - the diffuser's texture, the dust shadows,
// the spots - and returns a second master, at full resolution, plus the measure of what was
// removed.
//
// Pure: Img -> SmoothResult. It writes nothing. The text is in Report/SmoothReport.cs, the file
// is written by Runs/SmoothRun.cs through FitsWriter.
//
// WHAT IT DOES, on each Bayer position separately (on mono: on the whole frame):
//   1. the median over tiles of k px (Metrics.TileSize: ~150 px on the sensor), with the partial
//      tiles at the end kept and with their real centre (Img.TileMedians);
//   2. in each tile, a local QUADRATIC fit - 1, x, y, x2, xy, y2 - over the neighbouring tiles,
//      Gaussian-weighted with sigma_t tiles, with the real offsets between the centres;
//   3. robust: 3 rounds of two-sided clipping at max(2.5 * MAD, 2 % of the level) against the
//      local fit; a rejected tile no longer weighs in the fit of any neighbour;
//   4. on each pixel, the bilinear mean of the predictions of the four surrounding tiles, each
//      evaluated at the pixel's real offset from that tile's centre.
//
// WHY THIS WAY, and not otherwise - measured on five real masters (2026-09-16, see
// docs/design/smooth-validation.md for the figures), because two more obvious variants failed:
//
//   - A global polynomial over the residual of a radial model does NOT follow the mid-scale
//     structure every real master has (the monitor's spots at 1000-1500 px and +-3 %,
//     a Nikkor's off-centre ring, an LCD's blobs): the residual over tiles falls up to
//     degree 10 with no floor, and from degree 6 on the polynomial oscillates in the corners.
//   - A radial model on rings (the median per ring, as the structure mode has) PUTS RINGS
//     INTO THE FILE: beyond the inscribed circle the rings are partial arcs, any even
//     non-radial structure makes the distribution on a ring bimodal and the median jumps from
//     one ring to the next. A local field of 150 px cannot make up for rings of 22 px, so they
//     reach the output - visible as concentric circles in the ratio of two smoothed masters.
//     The structure mode suffers from the same thing only on the map; here it would enter the
//     flat. The conclusion: the split into "radial" and "rest" is arbitrary, and the only
//     requirement is that the output be smooth and follow the data. Without a radial model,
//     nothing here knows of a centre, so there are no rings by construction.
//
// A local quadratic fit over a window of +-450 px follows the curvature of vignetting (scale
// ~1800 px) into the corner exactly to the third order, and extrapolates correctly the half
// tile beyond the last centre. Two lessons that cost a round each:
//   - clipping relative to the MAD alone rejected 8 % of the tiles on a very clean master
//     (Samyang, MAD 0.06 %): any structure of 0.15 % became an "outlier" and was no longer
//     followed at all. Hence the absolute floor of 2 % of the level (ClipFloor);
//   - the partial tile of 8 px on the right edge, fitted at one tile's distance from its
//     neighbour, put a band of 0.8 % error along the edge. Hence the real offsets.
//
// THE SCALE. ScalePx is the SIGMA of the Gaussian kernel, in sensor pixels (the full width at
// half maximum is ~2.35 sigma), 300 px by default: the weave cells measured on the Tokina master
// are 290-340 px, and at sigma 300 a random field with that correlation length passes at
// ~30 % in RMS. Wider takes more out of the cells and eats more of the real structure
// of 300 px (the Nikkor's ring, 0.3-0.5 %, is taken partly at 300 and stays at
// 225). It is the only knob.
//
// RESULTS on the five real masters, at 300 px: the verdicts and the vignetting depth
// unchanged; the removed component 0.12-0.39 % ptp on the four clean ones (pixel noise
// plus the dust), 3.7 % on the one with the weave, where the compact spot dropped from +6.19 % to
// +0.62 % and the texture in the file from +-3 % to +-0.3 %. No rings: +-0.02 %. The CFA mosaic
// intact: the levels of the positions move under 0.1 %. On a synthetic one with a known truth
// (vignetting to 55 % in the corner, gradient 6 %, saddle) the maximum error over the whole
// frame, corners included, is 0.19 %.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    /// <summary>What the fit did on one Bayer position (or on the whole frame, on mono).</summary>
    internal sealed class SmoothPositionFit
    {
        internal int Tiles;
        internal int Rejected;
        /// <summary>
        /// The RMS of the kept tiles against the local fit, as a fraction of the level.
        /// </summary>
        internal double TileRms;
        /// <summary>
        /// How many tiles had a singular system and fell back on their own median.
        /// </summary>
        internal int FitFailed;
    }

    /// <summary>
    /// What <see cref="SmoothAnalyzer.Analyze"/> produced: the smoothed master, how each
    /// position was fitted, the measure of the removed component, and the structure battery
    /// before and after.
    /// </summary>
    internal sealed class SmoothResult
    {
        /// <summary>
        /// The smoothed flat, at full resolution, the shape of the input. Never null.
        /// </summary>
        internal Img Smoothed;

        internal bool IsCfa;
        internal double CfaSpread;
        internal int SourceH, SourceW;

        /// <summary>The smoothing scale used: the kernel's sigma, in sensor pixels.</summary>
        internal double ScalePx;
        /// <summary>The tile side in sensor pixels, and the kernel's sigma in tiles.</summary>
        internal int TilePx;
        internal double SigmaTiles;

        /// <summary>4 on CFA, 1 on mono.</summary>
        internal SmoothPositionFit[] Positions;

        /// <summary>
        /// The analysis raster of the removed component: Prepare(input) / Prepare(smoothed).
        /// </summary>
        internal Img Removed;
        internal int Width, Height;
        internal double RemovedP1Pct, RemovedP99Pct;
        internal double RemovedPtpPct;      // p95 - p5 over tiles
        internal double RemovedMaxTilePct;  // the most deviant tile
        internal double RemovedSpotPct;     // the deepest compact spot, signed
        internal int RemovedSpotX, RemovedSpotY;
        /// <summary>
        /// RemovedMaxTilePct at or above Th.SmoothKeptWarn: more than the texture was removed.
        /// </summary>
        internal bool RemovedFlagged;

        /// <summary>
        /// The fine texture of the input and of the output (Metrics.FineTexture on the analysis
        /// rasters), in percent. The first says whether there was anything to smooth, the second
        /// whether it went; the scale proposal rests on them (SmoothScaleAdvisor).
        /// </summary>
        internal double InputTexturePct, OutputTexturePct;

        /// <summary>
        /// The structure battery on the input and on the output, on the analysis rasters.
        /// </summary>
        internal StructureResult Before, After;
    }

    /// <summary>
    /// Mode 6, <c>--smooth</c>: builds a second master with the diffuser texture removed, by a
    /// robust local quadratic fit over tile medians on each Bayer position.
    /// </summary>
    internal static class SmoothAnalyzer
    {
        /// <summary>The default scale, in sensor pixels. Calibrated on a 6016 px sensor.</summary>
        internal const double DefaultScalePx = 300;

        /// <summary>
        /// Clipping: k sigma of the MAD, the absolute floor as a fraction of the level, and the
        /// rounds.
        /// </summary>
        internal const double ClipSigma = 2.5;
        internal const double ClipFloor = 0.02;
        internal const int ClipRounds = 3;

        /// <summary>
        /// The kernel's sigma in tiles: the scale is the sigma in SENSOR pixels,
        /// <paramref name="tilePx"/> the tile side in sensor pixels too (on CFA, twice the side
        /// in the position's raster). On the D3300 at 300 px: tiles of 150, sigma 2 tiles.
        /// </summary>
        /// <param name="scalePx">The kernel's sigma, sensor pixels.</param>
        /// <param name="tilePx">The tile side, sensor pixels.</param>
        /// <returns>The sigma in tiles.</returns>
        internal static double SigmaTiles(double scalePx, int tilePx) => scalePx / tilePx;

        /// <summary>
        /// Smooths a master: decides CFA or mono by the rule of <c>--expo</c>, smooths each
        /// Bayer position (or the whole frame) and merges them back, then measures what was
        /// removed and runs the structure battery on the input and on the output.
        /// </summary>
        /// <param name="input">The master, at full resolution, as loaded; not modified.</param>
        /// <param name="scalePx">The kernel's sigma, sensor pixels.</param>
        /// <returns>The smoothed master and the figures; never null.</returns>
        internal static SmoothResult Analyze(Img input, double scalePx = DefaultScalePx)
        {
            int h = input.H, w = input.W;

            // The CFA rule of --expo and --histeq: the four position medians, on a sparse
            // sample - enough to decide, and it does not cost a sort of 6 M values.
            double[] meds = Enumerable.Range(0, 4)
                .Select(p => PositionMedian(input, p))
                .ToArray();
            double spread = meds.Max() / Math.Max(meds.Min(), 1e-12);
            bool cfa = Mosaic.LooksCfa(spread) && h >= 2 && w >= 2;

            int pitch = cfa ? 2 : 1;
            var fits = new List<SmoothPositionFit>();
            Img smoothed;
            int tilePx;
            double sigmaT;
            if (cfa)
            {
                var outputs = new Img[4];
                Img probe = Mosaic.Extract(input, 0);
                tilePx = Metrics.TileSize(probe) * pitch;
                sigmaT = SigmaTiles(scalePx, tilePx);
                for (int p = 0; p < 4; p++)
                {
                    Img pos = p == 0 ? probe : Mosaic.Extract(input, p);
                    outputs[p] = SmoothPosition(pos, sigmaT, out SmoothPositionFit fit);
                    fits.Add(fit);
                }
                smoothed = Mosaic.Merge(outputs, h, w);
            }
            else
            {
                tilePx = Metrics.TileSize(input);
                sigmaT = SigmaTiles(scalePx, tilePx);
                smoothed = SmoothPosition(input, sigmaT, out SmoothPositionFit fit);
                fits.Add(fit);
            }

            // What was removed, at the analysis resolution, at a sixteenth of the memory. Prepare
            // is linear but a ratio is not: the ratio of the prepared rasters equals the prepared
            // ratio only where the smoothed flat is constant over a block, which it nearly is.
            Img prepIn = Img.Prepare(input);
            Img prepOut = Img.Prepare(smoothed);
            Img removed = Img.Divide(prepIn, prepOut);
            double[] sorted = Stats.Sorted(removed.D);
            double ptp = Metrics.LargeScalePtp(removed) * 100;
            double maxTile = Metrics.MaxTileDeviation(removed) * 100;
            var (spot, sx, sy) = Metrics.LocalSpots(removed);

            return new SmoothResult
            {
                Smoothed = smoothed,
                IsCfa = cfa,
                CfaSpread = spread,
                SourceH = h,
                SourceW = w,
                ScalePx = scalePx,
                TilePx = tilePx,
                SigmaTiles = sigmaT,
                Positions = fits.ToArray(),
                Removed = removed,
                Width = removed.W,
                Height = removed.H,
                RemovedP1Pct = (Stats.OfSorted(sorted, 1) - 1) * 100,
                RemovedP99Pct = (Stats.OfSorted(sorted, 99) - 1) * 100,
                RemovedPtpPct = ptp,
                RemovedMaxTilePct = maxTile,
                RemovedSpotPct = spot * 100,
                RemovedSpotX = sx,
                RemovedSpotY = sy,
                // !(x
                // < threshold), so that NaN falls on the warned side - the Stats.Verdict convention
                RemovedFlagged = !(maxTile < Th.SmoothKeptWarn),
                InputTexturePct = Metrics.FineTexture(prepIn),
                OutputTexturePct = Metrics.FineTexture(prepOut),
                Before = StructureAnalyzer.AnalyzePrepared(prepIn),
                After = StructureAnalyzer.AnalyzePrepared(prepOut),
            };
        }

        /// <summary>
        /// The median of a Bayer position on a sample of one pixel in 8 of the position (one in
        /// 16 of the sensor), on both axes.
        /// </summary>
        /// <param name="a">The frame, CFA mosaic or not.</param>
        /// <param name="p">The Bayer position, 0..3, as <see cref="Mosaic.PositionOf"/> numbers
        /// it.</param>
        /// <returns>The sample's median; 0 when the sample is empty.</returns>
        static double PositionMedian(Img a, int p)
        {
            int oy = p / 2, ox = p % 2;
            var vals = new List<double>();
            for (int y = oy; y < a.H; y += 16)
                for (int x = ox; x < a.W; x += 16)
                    vals.Add(a.At(y, x));
            return vals.Count == 0 ? 0 : Stats.Percentile(vals, 50);
        }

        /// <summary>
        /// The smoothing of one raster (a position or the whole frame), at a scale of
        /// <paramref name="sigmaT"/> tiles.
        /// </summary>
        /// <param name="p">The raster; not modified.</param>
        /// <param name="sigmaT">The kernel's sigma, in tiles.</param>
        /// <param name="fit">Set to what the fit did on this raster.</param>
        /// <returns>The smoothed raster, the size of <paramref name="p"/>.</returns>
        internal static Img SmoothPosition(Img p, double sigmaT, out SmoothPositionFit fit)
        {
            int k = Metrics.TileSize(p);
            var (t, cy, cx) = Img.TileMedians(p, k);
            var (coef, keep, failed) = LocalQuadratic(t, cy, cx, k, sigmaT);

            int rejected = keep.Cast<bool>().Count(v => !v);
            double level = Stats.Percentile(t.D, 50);
            double ss = 0; int n = 0;
            for (int j = 0; j < t.H; j++)
                for (int i = 0; i < t.W; i++)
                    if (keep[j, i]) { double r = (t.At(j, i) - coef[j, i, 0]) / level; ss += r * r; n++; }
            fit = new SmoothPositionFit
            {
                Tiles = t.D.Length,
                Rejected = rejected,
                TileRms = n > 0 ? Math.Sqrt(ss / n) : 0,
                FitFailed = failed,
            };
            return Evaluate(coef, cy, cx, k, p.H, p.W);
        }

        // The monomials of the local fit: 1, x, y, x2, xy, y2.
        static readonly (int a, int b)[] Mono = { (0, 0), (1, 0), (0, 1), (2, 0), (1, 1), (0, 2) };

        /// <summary>
        /// The local quadratic fit on the tile grid <paramref name="t"/>, with robust clipping
        /// (<see cref="ClipRounds"/> rounds at max(<see cref="ClipSigma"/> MAD,
        /// <see cref="ClipFloor"/>)).
        /// </summary>
        /// <param name="t">The tile medians.</param>
        /// <param name="cy">The real centre of each tile row, raster pixels.</param>
        /// <param name="cx">The real centre of each tile column, raster pixels.</param>
        /// <param name="k">The tile side, raster pixels; offsets are measured in units of
        /// it.</param>
        /// <param name="sigmaT">The Gaussian weight's sigma, in tiles; neighbours out to 3 sigma
        /// count.</param>
        /// <returns>
        /// The 6 coefficients per tile (in tile units, relative to its centre), the mask of the
        /// kept tiles, and how many systems were singular in the last round.
        /// </returns>
        internal static (double[,,] Coef, bool[,] Keep, int Failed) LocalQuadratic(
            Img t, double[] cy, double[] cx, int k, double sigmaT)
        {
            int th = t.H, tw = t.W;
            int rad = (int)Math.Ceiling(3 * sigmaT);
            double radSq = (3 * sigmaT) * (3 * sigmaT) + 1;
            var keep = new bool[th, tw];
            for (int j = 0; j < th; j++) for (int i = 0; i < tw; i++) keep[j, i] = true;
            var coef = new double[th, tw, 6];
            double level = Stats.Percentile(t.D, 50);
            int failed = 0;

            var M = new double[6, 6];
            var rhs = new double[6];
            var m = new double[6];
            var res = new List<double>(th * tw);

            for (int round = 0; round <= ClipRounds; round++)
            {
                failed = 0;
                for (int j = 0; j < th; j++)
                    for (int i = 0; i < tw; i++)
                    {
                        Array.Clear(M, 0, M.Length);
                        Array.Clear(rhs, 0, rhs.Length);
                        for (int dy = -rad; dy <= rad; dy++)
                        {
                            int jj = j + dy;
                            if (jj < 0 || jj >= th) continue;
                            double oy = (cy[jj] - cy[j]) / k;
                            for (int dx = -rad; dx <= rad; dx++)
                            {
                                int ii = i + dx;
                                if (ii < 0 || ii >= tw) continue;
                                if (dx * dx + dy * dy > radSq) continue;
                                if (!keep[jj, ii]) continue;
                                double ox = (cx[ii] - cx[i]) / k;
                                double g = Math.Exp(-(ox * ox + oy * oy) / (2 * sigmaT * sigmaT));
                                for (int q = 0; q < 6; q++)
                                    m[q] = Math.Pow(ox, Mono[q].a) * Math.Pow(oy, Mono[q].b);
                                double z = t.At(jj, ii);
                                for (int q = 0; q < 6; q++)
                                {
                                    rhs[q] += g * z * m[q];
                                    for (int r = 0; r < 6; r++) M[q, r] += g * m[q] * m[r];
                                }
                            }
                        }
                        double[] sol = Stats.Solve(M, rhs, 6);
                        if (sol == null)
                        {
                            // all neighbours rejected, or a 1x1 grid: the tile keeps its median
                            failed++;
                            sol = new double[6];
                            sol[0] = t.At(j, i);
                        }
                        for (int q = 0; q < 6; q++) coef[j, i, q] = sol[q];
                    }

                // The residual against the fit's local level, relative to the frame's level.
                res.Clear();
                for (int j = 0; j < th; j++)
                    for (int i = 0; i < tw; i++)
                        if (keep[j, i]) res.Add((t.At(j, i) - coef[j, i, 0]) / level);
                double med = Stats.Percentile(res, 50);
                var dev = res.Select(r => Math.Abs(r - med)).ToList();
                double mad = Stats.Percentile(dev, 50) * 1.4826;
                if (mad <= 0) break;
                double cut = Math.Max(ClipSigma * mad, ClipFloor);
                for (int j = 0; j < th; j++)
                    for (int i = 0; i < tw; i++)
                        keep[j, i] = Math.Abs((t.At(j, i) - coef[j, i, 0]) / level - med) < cut;
            }
            return (coef, keep, failed);
        }

        /// <summary>
        /// The bilinear mean of the predictions of the four tiles around each pixel, each
        /// evaluated at the pixel's real offset from that tile's centre. Beyond the last centre
        /// the weight is the edge tile's and its prediction is extrapolated.
        /// </summary>
        /// <param name="coef">The 6 coefficients per tile, from
        /// <see cref="LocalQuadratic"/>.</param>
        /// <param name="cy">The real centre of each tile row, raster pixels.</param>
        /// <param name="cx">The real centre of each tile column, raster pixels.</param>
        /// <param name="k">The tile side, raster pixels.</param>
        /// <param name="h">The output's height, pixels.</param>
        /// <param name="w">The output's width, pixels.</param>
        /// <returns>The smooth raster, h x w.</returns>
        internal static Img Evaluate(double[,,] coef, double[] cy, double[] cx, int k, int h, int w)
        {
            int th = cy.Length, tw = cx.Length;
            var o = new Img(h, w);

            // per column: the index of the tile on the left, the fraction, and the offsets from the
            // two tiles
            var i0 = new int[w]; var fu = new double[w]; var dx0 = new double[w]; var dx1 = new double[w];
            for (int x = 0; x < w; x++)
            {
                double px = x + 0.5;
                double u = FracIndex(px, cx);
                int i = Math.Clamp((int)Math.Floor(u), 0, Math.Max(0, tw - 2));
                i0[x] = i;
                fu[x] = tw < 2 ? 0 : Math.Clamp(u - i, 0, 1);
                dx0[x] = (px - cx[i]) / k;
                dx1[x] = tw < 2 ? 0 : (px - cx[i + 1]) / k;
            }
            for (int y = 0; y < h; y++)
            {
                double py = y + 0.5;
                double v = FracIndex(py, cy);
                int j = Math.Clamp((int)Math.Floor(v), 0, Math.Max(0, th - 2));
                double fv = th < 2 ? 0 : Math.Clamp(v - j, 0, 1);
                double dy0 = (py - cy[j]) / k;
                double dy1 = th < 2 ? 0 : (py - cy[j + 1]) / k;
                int j1 = Math.Min(j + 1, th - 1);
                for (int x = 0; x < w; x++)
                {
                    int i = i0[x], i1 = Math.Min(i + 1, tw - 1);
                    double a = Quad(coef, j, i, dx0[x], dy0);
                    double b = Quad(coef, j, i1, dx1[x], dy0);
                    double c = Quad(coef, j1, i, dx0[x], dy1);
                    double d = Quad(coef, j1, i1, dx1[x], dy1);
                    double top = a * (1 - fu[x]) + b * fu[x];
                    double bot = c * (1 - fu[x]) + d * fu[x];
                    o.Set(y, x, top * (1 - fv) + bot * fv);
                }
            }
            return o;
        }

        /// <summary>One tile's quadratic evaluated at an offset from its centre.</summary>
        /// <param name="c">The coefficients, per tile.</param>
        /// <param name="j">The tile's row.</param>
        /// <param name="i">The tile's column.</param>
        /// <param name="dx">The horizontal offset, in tiles.</param>
        /// <param name="dy">The vertical offset, in tiles.</param>
        /// <returns>The predicted level, in the raster's units.</returns>
        static double Quad(double[,,] c, int j, int i, double dx, double dy)
            => c[j, i, 0] + c[j, i, 1] * dx + c[j, i, 2] * dy
             + c[j, i, 3] * dx * dx + c[j, i, 4] * dx * dy + c[j, i, 5] * dy * dy;

        /// <summary>
        /// The fractional index of the position <paramref name="p"/> in the array of centres: like
        /// np.interp over (centres, 0..n-1), clamped at the ends.
        /// </summary>
        /// <param name="p">The position, raster pixels.</param>
        /// <param name="centres">The tile centres, ascending; at least one.</param>
        /// <returns>The fractional index, 0..n-1.</returns>
        static double FracIndex(double p, double[] centres)
        {
            int n = centres.Length;
            if (n == 1 || p <= centres[0]) return 0;
            if (p >= centres[n - 1]) return n - 1;
            int lo = 0, hi = n - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (centres[mid] <= p) lo = mid; else hi = mid; }
            return lo + (p - centres[lo]) / (centres[hi] - centres[lo]);
        }
    }
}
