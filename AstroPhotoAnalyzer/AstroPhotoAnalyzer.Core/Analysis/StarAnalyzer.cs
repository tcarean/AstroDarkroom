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

// StarAnalyzer.cs - the measurements D1 and D2: how wide the stars are, over the frame and per
// region. The FWHM is the image's REAL resolution: a star of 3 px means that nothing finer than
// 3 px exists in the file, however many pixels it has. The size table starts from here, and
// here the "300 dpi" myth falls.
//
// HOW: candidate = a pixel above the plane's mean + the minimum amplitude, which is the maximum
// of its StarWindow x StarWindow window (25 x 25); among equal maxima only the first in scan
// order counts, so a flat two-pixel peak is one star, not two (ADR 0041).
// The star's base = the median of the window's border; the amplitude = peak - base,
// with a low threshold (a faint smudge has no profile) and, over it, StarMinSnr times the
// border's robust sigma, measured about a plane fitted to the border so that the local gradient
// of a nebula is not read as noise: the grain of the stretched noise peaks 3 to 4 sigmas over
// its base and passed the absolute floor by the thousand on a starless frame (ADR 0051). A high
// threshold on the peak itself leaves out a saturated star (it has no half). The width at half
// maximum is taken on the horizontal and on the vertical, with linear subpixel interpolation,
// and averaged.
// Exactly the acceptance script - with three differences: the script's candidates were above
// 3 x the mean, which rejects every star once the background is lifted (ADR 0016); it took
// 20,000 of them at random, here ALL are taken, deterministic and better sampled; and it had no
// sigma clause.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>A star kept by the measurement.</summary>
    /// <param name="X">The peak's column, in pixels.</param>
    /// <param name="Y">The peak's row, in pixels, 0 at the top.</param>
    /// <param name="Fwhm">
    /// The width at half maximum, in pixels: the mean of the horizontal and vertical widths.
    /// </param>
    /// <param name="Peak">The peak's value on the reference plane, 0..1.</param>
    internal sealed record Star(int X, int Y, double Fwhm, double Peak);

    /// <summary>A region of the frame and the median FWHM of the stars in it (D2).</summary>
    internal sealed record StarRegion
    {
        internal string Name { get; init; }
        internal int X0 { get; init; }
        internal int Y0 { get; init; }
        internal int W { get; init; }
        internal int H { get; init; }
        internal int Count { get; init; }

        /// <summary>NaN under Thresholds.RegionMinStars stars.</summary>
        internal double FwhmMedian { get; init; }
        internal bool Known => !double.IsNaN(FwhmMedian);
    }

    /// <summary>
    /// The stars and the image's real resolution, over the frame and per region (D1, D2).
    /// </summary>
    internal sealed record StarResult
    {
        internal IReadOnlyList<Star> Stars { get; init; }
        internal int Count => Stars.Count;
        internal bool Insufficient => Count < Thresholds.StarMinCount;

        /// <summary>NaN when there are too few stars.</summary>
        internal double FwhmMedian { get; init; }
        internal double FwhmQ1 { get; init; }
        internal double FwhmQ3 { get; init; }

        /// <summary>The centre first, then the four corners.</summary>
        internal IReadOnlyList<StarRegion> Regions { get; init; }

        /// <summary>
        /// The widest corner / the centre; NaN if the centre or all the corners are unknown.
        /// </summary>
        internal double CornerToCenterRatio { get; init; }
        internal bool SoftCorners => !double.IsNaN(CornerToCenterRatio) && CornerToCenterRatio > Thresholds.CornerSoftRatio;

        /// <summary>
        /// How many pixels of real detail the image has across its width: W / FWHM.
        /// </summary>
        /// <param name="w">The width, in pixels.</param>
        /// <returns>The width divided by the median FWHM; NaN with too few stars.</returns>
        internal double EffectiveDetailPx(int w) => Insufficient ? double.NaN : w / FwhmMedian;
    }

    /// <summary>Measures how wide the stars are, over the frame and per region (D1, D2).</summary>
    internal static class StarAnalyzer
    {
        /// <summary>
        /// The window's half-width, in pixels: <see cref="Thresholds.StarWindow"/> is 2 x Half + 1,
        /// and the border of Half pixels holds no candidate. (Until the review of 2026-10-23 a
        /// literal 12 stood here, and the threshold was read by no code.)
        /// </summary>
        const int Half = Thresholds.StarWindow / 2;

        /// <summary>
        /// The column and row offsets of each border pixel in the order <see cref="BorderStats"/>
        /// fills its buffer, and the sums of their squares: the plane fitted to the border is
        /// a + b x dx + c x dy, and because the border is symmetric about the centre (the sums
        /// of dx, of dy and of dx x dy vanish) its least-squares coefficients are the mean and
        /// the two ratios sum(dx x v) / sum(dx^2) and sum(dy x v) / sum(dy^2).
        /// </summary>
        static readonly int[] BorderDx, BorderDy;
        static readonly int SumDx2, SumDy2;

        /// <summary>Builds the border's offset tables once, in the buffer's fill order.</summary>
        static StarAnalyzer()
        {
            int n = 4 * (2 * Half + 1) - 4;
            BorderDx = new int[n];
            BorderDy = new int[n];
            int k = 0;
            for (int dx = -Half; dx <= Half; dx++)
            {
                BorderDx[k] = dx; BorderDy[k++] = -Half;
                BorderDx[k] = dx; BorderDy[k++] = Half;
            }
            for (int dy = -Half + 1; dy <= Half - 1; dy++)
            {
                BorderDx[k] = -Half; BorderDy[k++] = dy;
                BorderDx[k] = Half; BorderDy[k++] = dy;
            }
            for (int i = 0; i < n; i++)
            {
                SumDx2 += BorderDx[i] * BorderDx[i];
                SumDy2 += BorderDy[i] * BorderDy[i];
            }
        }

        /// <summary>
        /// Scans every pixel of the reference plane for star candidates and measures the width at
        /// half maximum of each one kept.
        /// </summary>
        /// <param name="r">
        /// The image, 0..1; the stars are looked for on its reference plane, the border of
        /// <see cref="Thresholds.StarWindow"/> / 2 pixels left out.
        /// </param>
        /// <returns>
        /// The stars, the FWHM median and quartiles (NaN under
        /// <see cref="Thresholds.StarMinCount"/> stars), the regions and the corner-to-centre
        /// ratio.
        /// </returns>
        internal static StarResult Analyze(Raster r)
        {
            float[] g = r.Reference;
            int W = r.W, H = r.H;
            float thr = (float)(Stats.Mean(g) + Thresholds.StarCandidateAboveMean16 / 65535.0);
            float minAmp = (float)(Thresholds.StarMinAmplitude16 / 65535.0);
            float maxPeak = (float)(Thresholds.StarMaxPeak16 / 65535.0);

            var stars = new List<Star>();
            var border = new float[4 * (2 * Half + 1) - 4];
            var deviations = new float[border.Length];
            var line = new double[2 * Half + 1];

            for (int y = Half; y < H - Half; y++)
            {
                int row = y * W;
                for (int x = Half; x < W - Half; x++)
                {
                    float v = g[row + x];
                    if (v <= thr || v > maxPeak) continue;
                    if (!IsWindowMax(g, W, H, x, y, v)) continue;

                    (float baseLevel, float sigma) = BorderStats(g, W, x, y, border, deviations);
                    float amp = v - baseLevel;
                    if (amp < minAmp || amp < Thresholds.StarMinSnr * sigma) continue;
                    double half = baseLevel + amp / 2.0;

                    for (int i = 0; i < line.Length; i++) line[i] = g[row + x - Half + i];
                    double wx = WidthAtHalf(line, half);
                    for (int i = 0; i < line.Length; i++) line[i] = g[(y - Half + i) * W + x];
                    double wy = WidthAtHalf(line, half);

                    if (wx > Thresholds.StarFwhmMin && wx < Thresholds.StarFwhmMax
                        && wy > Thresholds.StarFwhmMin && wy < Thresholds.StarFwhmMax)
                        stars.Add(new Star(x, y, (wx + wy) / 2, v));
                }
            }

            var regions = Regions(stars, W, H);
            double center = regions[0].FwhmMedian;
            double worstCorner = regions.Skip(1).Where(c => c.Known).Select(c => c.FwhmMedian).DefaultIfEmpty(double.NaN).Max();
            double ratio = double.IsNaN(center) || double.IsNaN(worstCorner) ? double.NaN : worstCorner / center;

            if (stars.Count < Thresholds.StarMinCount)
                return new StarResult { Stars = stars, FwhmMedian = double.NaN, FwhmQ1 = double.NaN, FwhmQ3 = double.NaN, Regions = regions, CornerToCenterRatio = ratio };

            float[] sorted = Stats.Sorted(stars.Select(s => (float)s.Fwhm).ToArray());
            return new StarResult
            {
                Stars = stars,
                FwhmMedian = Stats.PercentileSorted(sorted, 50),
                FwhmQ1 = Stats.PercentileSorted(sorted, 25),
                FwhmQ3 = Stats.PercentileSorted(sorted, 75),
                Regions = regions,
                CornerToCenterRatio = ratio,
            };
        }

        /// <summary>
        /// The maximum of the window, ties allowed, counted once: a flat peak of two pixels is
        /// still a star, and only its first pixel in scan order (a row above, or the same row to
        /// the left) passes, so it is measured once (ADR 0041; before it both passed and it was
        /// measured twice). Only a tied pixel the scan reaches counts as earlier: one in the
        /// border left out can disqualify nothing, or a flat peak straddling the border would be
        /// lost altogether.
        /// </summary>
        /// <param name="g">The reference plane, 0..1, row-major.</param>
        /// <param name="W">The plane's width, in pixels.</param>
        /// <param name="H">The plane's height, in pixels.</param>
        /// <param name="x">
        /// The candidate's column, at least <see cref="Thresholds.StarWindow"/> / 2 px from the
        /// left and right edges.
        /// </param>
        /// <param name="y">
        /// The candidate's row, at least <see cref="Thresholds.StarWindow"/> / 2 px from the top
        /// and bottom edges.
        /// </param>
        /// <param name="v">The candidate's value.</param>
        /// <returns>
        /// <c>true</c> when no pixel of the window is over <paramref name="v"/> and none equal to
        /// it comes earlier in scan order.
        /// </returns>
        static bool IsWindowMax(float[] g, int W, int H, int x, int y, float v)
        {
            for (int dy = -Half; dy <= Half; dy++)
            {
                int o = (y + dy) * W + x;
                bool rowScanned = y + dy >= Half && y + dy < H - Half;
                for (int dx = -Half; dx <= Half; dx++)
                {
                    float u = g[o + dx];
                    if (u > v) return false;
                    if (u == v && (dy < 0 || (dy == 0 && dx < 0)) && rowScanned && x + dx >= Half && x + dx < W - Half) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The median of the pixels on the border of the window around (x, y), 96 for the window
        /// of <see cref="Thresholds.StarWindow"/> (25), the star's base, and their robust sigma,
        /// 1.4826 x the median absolute deviation of the residuals of a plane fitted to the border
        /// by least squares: the noise the amplitude is judged against, the local gradient taken
        /// out so that a star on a nebula's slope is judged against the grain and not the slope
        /// (ADR 0051; the raw deviation about the median lost 14 of the 64 stars of the DarkSky
        /// scene). On a quantised flat border more than half the residuals are equal and the
        /// sigma is 0, which leaves the absolute floor alone.
        /// </summary>
        /// <param name="g">The reference plane, 0..1, row-major.</param>
        /// <param name="W">The plane's width, in pixels.</param>
        /// <param name="x">
        /// The window's centre column, at least <see cref="Thresholds.StarWindow"/> / 2 px from
        /// the left and right edges.
        /// </param>
        /// <param name="y">
        /// The window's centre row, at least <see cref="Thresholds.StarWindow"/> / 2 px from the
        /// top and bottom edges.
        /// </param>
        /// <param name="buf">A buffer of 4 x (StarWindow - 1) values (96), overwritten.</param>
        /// <param name="dev">A second buffer of the same length for the residuals, overwritten.</param>
        /// <returns>
        /// The median of the border (the mean of its two middle values) and its robust sigma.
        /// </returns>
        static (float Median, float Sigma) BorderStats(float[] g, int W, int x, int y, float[] buf, float[] dev)
        {
            int k = 0;
            for (int dx = -Half; dx <= Half; dx++)
            {
                buf[k++] = g[(y - Half) * W + x + dx];
                buf[k++] = g[(y + Half) * W + x + dx];
            }
            for (int dy = -Half + 1; dy <= Half - 1; dy++)
            {
                buf[k++] = g[(y + dy) * W + x - Half];
                buf[k++] = g[(y + dy) * W + x + Half];
            }

            // The plane through the border (see BorderDx), then the residuals about it.
            double sum = 0, sumX = 0, sumY = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                sum += buf[i];
                sumX += BorderDx[i] * (double)buf[i];
                sumY += BorderDy[i] * (double)buf[i];
            }
            double a = sum / buf.Length, b = sumX / SumDx2, c = sumY / SumDy2;
            for (int i = 0; i < buf.Length; i++) dev[i] = (float)(buf[i] - a - b * BorderDx[i] - c * BorderDy[i]);
            Array.Sort(dev);
            float residualMedian = (dev[dev.Length / 2 - 1] + dev[dev.Length / 2]) / 2;
            for (int i = 0; i < dev.Length; i++) dev[i] = Math.Abs(dev[i] - residualMedian);
            Array.Sort(dev);
            float mad = (dev[dev.Length / 2 - 1] + dev[dev.Length / 2]) / 2;

            Array.Sort(buf);
            float median = (buf[buf.Length / 2 - 1] + buf[buf.Length / 2]) / 2;
            return (median, 1.4826f * mad);
        }

        /// <summary>
        /// The width of the profile at the level <paramref name="half"/>: from the centre it
        /// descends in each direction until under the level, then interpolates linearly the exact
        /// place of the crossing. The same function as in the script, the epsilon of the division
        /// included.
        /// </summary>
        /// <param name="line">The profile through the peak, the peak at the middle.</param>
        /// <param name="half">The level: the base plus half the amplitude.</param>
        /// <returns>
        /// The width, in pixels; on a side that stays over the level to the line's end, the
        /// crossing is extrapolated from the end.
        /// </returns>
        internal static double WidthAtHalf(double[] line, double half)
        {
            int c = line.Length / 2, last = line.Length - 1;
            int l = c;
            while (l > 0 && line[l] > half) l--;
            int r = c;
            while (r < last && line[r] > half) r++;
            double fl = l + (half - line[l]) / (line[l + 1] - line[l] + 1e-6);
            double fr = r - (half - line[r]) / (line[r - 1] - line[r] + 1e-6);
            return fr - fl;
        }

        /// <summary>
        /// The centre region (<see cref="Thresholds.CenterFraction"/> of each side) and the four
        /// corners (<see cref="Thresholds.RegionFraction"/> of each side), each with the median
        /// FWHM of its stars when it holds at least <see cref="Thresholds.RegionMinStars"/>.
        /// </summary>
        /// <param name="stars">The stars kept.</param>
        /// <param name="W">The image's width, in pixels.</param>
        /// <param name="H">The image's height, in pixels.</param>
        /// <returns>
        /// The centre first, then the corners: top-left, top-right, bottom-left, bottom-right.
        /// </returns>
        static IReadOnlyList<StarRegion> Regions(List<Star> stars, int W, int H)
        {
            int cw = (int)(W * Thresholds.RegionFraction), ch = (int)(H * Thresholds.RegionFraction);
            int mw = (int)(W * Thresholds.CenterFraction), mh = (int)(H * Thresholds.CenterFraction);
            var defs = new (string Name, int X0, int Y0, int W, int H)[]
            {
                (Tr.Pick("centre", "centru"), (W - mw) / 2, (H - mh) / 2, mw, mh),
                (Tr.Pick("upper-left corner", "colt stanga-sus"), 0, 0,            cw, ch),
                (Tr.Pick("upper-right corner", "colt dreapta-sus"), W - cw, 0,            cw, ch),
                (Tr.Pick("lower-left corner", "colt stanga-jos"), 0, H - ch,       cw, ch),
                (Tr.Pick("lower-right corner", "colt dreapta-jos"), W - cw, H - ch,       cw, ch),
            };
            var list = new List<StarRegion>();
            foreach (var d in defs)
            {
                var inside = stars.Where(s => s.X >= d.X0 && s.X < d.X0 + d.W && s.Y >= d.Y0 && s.Y < d.Y0 + d.H)
                                  .Select(s => (float)s.Fwhm).ToArray();
                double med = inside.Length >= Thresholds.RegionMinStars ? Stats.Median(inside) : double.NaN;
                list.Add(new StarRegion { Name = d.Name, X0 = d.X0, Y0 = d.Y0, W = d.W, H = d.H, Count = inside.Length, FwhmMedian = med });
            }
            return list;
        }
    }
}
