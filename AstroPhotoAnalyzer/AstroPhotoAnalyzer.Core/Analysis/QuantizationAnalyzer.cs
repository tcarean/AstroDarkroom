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

// QuantizationAnalyzer.cs - the measurements C1, C2 and C4: what happens on the way to 8 bits.
//
// C1, the plateau: on the sky quantised to 8 bits, how long the runs of pixels with the same
// value are. On paper a plateau becomes a contour; under ~1 mm it does not show. The tiles are
// the strict ones (Thresholds.Plateau*): clean sky, no core.
//
// C2, the rounding error: 16 -> 8 bits is |v*255 - round(v*255)|, and the report states it so
// that it can be seen to be pure rounding (max 0.5), not a loss.
//
// C4, the stress test: the same shadow lift applied to the 16-bit data and to the data already
// quantised to 8, then the number of levels missing from the shadows. The figure that says
// "Pastreaza master-ul de 16 biti" is not an opinion: it is the levels missing on the 8-bit path
// over those missing on the 16-bit one, which the report sets side by side. The rule
// (Keep16BitMaster) reads the difference, so an 8-bit file saved as 16 bits, missing as many
// either way, gets the upscale warning alone (ADR 0043; until the review of 2026-10-23 the rule
// read the 8-bit path alone and gave both).

using System;
using System.Collections.Generic;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// What the step to 8 bits does (C1, C2, C4): the plateau on the quantised sky, the rounding
    /// error, and the levels a shadow lift loses.
    /// </summary>
    internal sealed record QuantizationResult
    {
        internal int PlateauTiles { get; init; }

        /// <summary>
        /// The share of the frame the plateau's tiles stand for, in percent (0..100, as
        /// <see cref="NoiseResult.PctTilesBelowBanding"/>): the tiles accepted over the random
        /// positions tried (the draws are uniform over the frame), so a frame whose clean sky is a
        /// few wiped patches reads as a few tenths of a percent. 0 without tiles.
        /// </summary>
        internal double PlateauFramePct { get; init; }

        /// <summary>
        /// The residual noise on the plateau's tiles, the median of their sigmas in 8-bit LSB
        /// (<see cref="NoiseAnalyzer.SigmaLsb"/>): what the denoise left where the plateau is
        /// measured, usually far under the noise section's median, since a tile with real noise
        /// does not pass the plateau's range criterion. NaN without tiles.
        /// </summary>
        internal double PlateauSigmaLsb { get; init; }

        /// <summary>
        /// The mean length of the runs of identical values, in pixels; NaN without tiles.
        /// </summary>
        internal double PlateauPx { get; init; }
        internal bool PlateauKnown => !double.IsNaN(PlateauPx);

        /// <summary>The plateau's length on paper at a dpi.</summary>
        /// <param name="dpi">The print's dots per inch, positive.</param>
        /// <returns>PlateauPx x 25.4 / dpi, in mm; NaN without tiles.</returns>
        internal double PlateauMmAt(double dpi) => PlateauPx * 25.4 / dpi;

        /// <summary>
        /// Whether the plateau shows on paper at a dpi: longer than
        /// <see cref="Thresholds.PlateauVisibleMm"/>.
        /// </summary>
        /// <param name="dpi">The print's dots per inch, positive.</param>
        /// <returns>
        /// <c>true</c> when the plateau is known and its length at that dpi is over the limit.
        /// </returns>
        internal bool PlateauVisibleAt(double dpi) => PlateauKnown && PlateauMmAt(dpi) > Thresholds.PlateauVisibleMm;

        /// <summary>C2 and C4 make sense only on a source with more than 8 bits.</summary>
        internal bool Applicable { get; init; }

        internal double RoundingMeanAbs { get; init; }
        internal double RoundingMax { get; init; }
        internal double RoundingP999 { get; init; }

        internal int LevelsUsedFrom16 { get; init; }
        internal int MissingFrom16 { get; init; }
        internal int LevelsUsedFrom8 { get; init; }
        internal int MissingFrom8 { get; init; }

        /// <summary>
        /// Keep the 16-bit master: more than <see cref="Thresholds.MissingLevelsWarn"/> levels are
        /// missing on the 8-bit path over the 16-bit one (R-082, R-096). False where C4 does not
        /// apply, and on an 8-bit upscale, whose two paths miss the same levels.
        /// </summary>
        internal bool Keep16BitMaster => Applicable && MissingFrom8 - MissingFrom16 > Thresholds.MissingLevelsWarn;
    }

    /// <summary>Measures what happens on the way to 8 bits (C1, C2, C4).</summary>
    internal static class QuantizationAnalyzer
    {
        /// <summary>
        /// Measures the plateau (C1) and, on a source with more than 8 bits, a float one or one
        /// without a Source, the rounding error (C2) and the shadow-lift stress (C4).
        /// </summary>
        /// <param name="r">The image, 0..1.</param>
        /// <returns>The result; C2 and C4 at 0 where they do not apply.</returns>
        internal static QuantizationResult Analyze(Raster r)
        {
            var (tiles, pct, plateau, sigma) = Plateau(r);
            bool applicable = r.Source == null || r.Source.IsFloat || r.Source.BitDepth > 8;
            var (mean, max, p999) = applicable ? Rounding(r) : (0, 0, 0);
            var (u16, m16, u8, m8) = applicable ? Stress(r) : (0, 0, 0, 0);
            return new QuantizationResult
            {
                PlateauTiles = tiles,
                PlateauFramePct = pct,
                PlateauPx = plateau,
                PlateauSigmaLsb = sigma,
                Applicable = applicable,
                RoundingMeanAbs = mean,
                RoundingMax = max,
                RoundingP999 = p999,
                LevelsUsedFrom16 = u16,
                MissingFrom16 = m16,
                LevelsUsedFrom8 = u8,
                MissingFrom8 = m8,
            };
        }

        /// <summary>
        /// The plateau (C1): up to <see cref="Thresholds.PlateauTileCount"/> strict tiles drawn
        /// from <see cref="Thresholds.PlateauSeed"/> within
        /// <see cref="Thresholds.PlateauMaxAttempts"/> draws, and on each the mean run length of
        /// the values quantised to 8 bits along its rows.
        /// </summary>
        /// <param name="r">The image, 0..1; its reference plane is read.</param>
        /// <returns>
        /// The tiles accepted, their share of the frame in percent (accepted over tried), the mean
        /// plateau in pixels and the median of the tiles' noise sigmas in LSB; NaN without a tile, and 0 and NaN
        /// on a raster not larger than a tile.
        /// </returns>
        static (int Tiles, double FramePct, double PlateauPx, double SigmaLsb) Plateau(Raster r)
        {
            int size = Thresholds.NoiseTileSize;
            if (r.H <= size || r.W <= size) return (0, 0, double.NaN, double.NaN);
            float[] g = r.Reference;
            var rng = new SeededRandom(Thresholds.PlateauSeed);
            float maxRange = (float)(Thresholds.PlateauTileMaxRangeLevels / 255.0);
            float maxMean = (float)(Thresholds.PlateauTileMaxMeanLevels / 255.0);

            double sum = 0; int tiles = 0, attempts = 0;
            var sigmas = new List<float>();
            for (; attempts < Thresholds.PlateauMaxAttempts && tiles < Thresholds.PlateauTileCount; attempts++)
            {
                int y0 = rng.Next(r.H - size), x0 = rng.Next(r.W - size);
                if (!Accept(g, r.W, x0, y0, size, maxRange, maxMean)) continue;
                sigmas.Add((float)NoiseAnalyzer.SigmaLsb(g, r.W, x0, y0, size));

                // Changes of value along each row, on the quantised sky; averaged over the rows.
                double changes = 0;
                for (int y = y0; y < y0 + size; y++)
                {
                    int o = y * r.W;
                    int prev = RasterOps.Level8(g[o + x0]);
                    for (int x = x0 + 1; x < x0 + size; x++)
                    {
                        int q = RasterOps.Level8(g[o + x]);
                        if (q != prev) { changes++; prev = q; }
                    }
                }
                sum += size / (1 + changes / size);
                tiles++;
            }
            return (tiles, attempts == 0 ? 0 : 100.0 * tiles / attempts, tiles == 0 ? double.NaN : sum / tiles, tiles == 0 ? double.NaN : Stats.Median(sigmas.ToArray()));
        }

        /// <summary>
        /// Whether a tile is clean sky for the plateau: its range under
        /// <paramref name="maxRange"/> and its mean under <paramref name="maxMean"/>.
        /// </summary>
        /// <param name="g">The reference plane, 0..1, row-major.</param>
        /// <param name="w">The plane's width, in pixels.</param>
        /// <param name="x0">The tile's left column.</param>
        /// <param name="y0">The tile's top row, 0 at the top.</param>
        /// <param name="size">The tile's side, in pixels; the tile lies inside the plane.</param>
        /// <param name="maxRange">
        /// The range, max - min, to stay strictly under, on the scale 0..1.
        /// </param>
        /// <param name="maxMean">The mean to stay strictly under, on the scale 0..1.</param>
        /// <returns><c>true</c> when both hold.</returns>
        static bool Accept(float[] g, int w, int x0, int y0, int size, float maxRange, float maxMean)
        {
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity; double s = 0;
            for (int y = y0; y < y0 + size; y++)
            {
                int o = y * w;
                for (int x = x0; x < x0 + size; x++)
                {
                    float v = g[o + x];
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                    s += v;
                }
            }
            return hi - lo < maxRange && s / (size * size) < maxMean;
        }

        /// <summary>
        /// The rounding error on all the planes, at the percentiles' step, in levels.
        /// </summary>
        /// <param name="full">The image, 0..1.</param>
        /// <returns>
        /// The mean, the maximum and the 99.9th percentile of |v x 255 - round(v x 255)|, in
        /// levels (0..0.5); the percentile to a thousandth of a level.
        /// </returns>
        static (double Mean, double Max, double P999) Rounding(Raster full)
        {
            Raster r = RasterOps.Subsample(full, Thresholds.PercentileStride);
            const int bins = 1000;   // 0..1 level, in thousandths
            var hist = new long[bins + 1];
            double sum = 0, max = 0; long n = 0;
            foreach (float[] plane in r.Planes)
                for (int i = 0; i < plane.Length; i++)
                {
                    double v = Math.Clamp(plane[i], 0f, 1f) * 255;
                    double e = Math.Abs(v - Math.Round(v));
                    sum += e; n++;
                    if (e > max) max = e;
                    hist[(int)Math.Min(bins, e * bins)]++;
                }
            return (sum / n, max, PercentileFromHistogram(hist, n, 99.9) / bins);
        }

        /// <summary>
        /// The shadow-lift stress test (C4): the reference plane at
        /// <see cref="Thresholds.PercentileStride"/>, lifted with x^(1 / ShadowLiftGamma) and
        /// quantised to 8 bits, once from the data and once from the data first rounded to 8 bits.
        /// </summary>
        /// <param name="full">The image, 0..1.</param>
        /// <returns>
        /// The levels used and the levels missing between ShadowLiftLevelLo and ShadowLiftLevelHi,
        /// from the data and from the data rounded to 8 bits.
        /// </returns>
        static (int Used16, int Missing16, int Used8, int Missing8) Stress(Raster full)
        {
            float[] g = RasterOps.Subsample(full, Thresholds.PercentileStride).Reference;
            double exp = 1.0 / Thresholds.ShadowLiftGamma;
            var seen16 = new bool[256]; var seen8 = new bool[256];
            for (int i = 0; i < g.Length; i++)
            {
                double v = Math.Clamp(g[i], 0f, 1f);
                seen16[RasterOps.Level8((float)Math.Pow(v, exp))] = true;
                double v8 = Math.Round(v * 255) / 255;
                seen8[RasterOps.Level8((float)Math.Pow(v8, exp))] = true;
            }
            return (Count(seen16, out int m16), m16, Count(seen8, out int m8), m8);
        }

        /// <summary>
        /// The levels used between ShadowLiftLevelLo and Hi (exclusive) and how many are missing
        /// between the first and the last used.
        /// </summary>
        /// <param name="seen">For each 8-bit level, 256 entries, whether it was used.</param>
        /// <param name="missing">
        /// The levels missing between the first and the last used; 0 when none is used.
        /// </param>
        /// <returns>The levels used, strictly between the two bounds.</returns>
        static int Count(bool[] seen, out int missing)
        {
            int used = 0, first = -1, last = -1;
            for (int l = Thresholds.ShadowLiftLevelLo + 1; l < Thresholds.ShadowLiftLevelHi; l++)
                if (seen[l]) { used++; if (first < 0) first = l; last = l; }
            missing = used == 0 ? 0 : (last - first + 1) - used;
            return used;
        }

        /// <summary>
        /// The percentile of the values a histogram counts, to the bin: the first bin at which the
        /// cumulative count reaches <paramref name="p"/> % of <paramref name="n"/>, rounded up.
        /// In floating point 99.9 / 100 is a hair over 0.999, so when 99.9 % of n is whole the
        /// count asked for is one sample more.
        /// </summary>
        /// <param name="hist">The count in each bin.</param>
        /// <param name="n">The total count, the sum of <paramref name="hist"/>.</param>
        /// <param name="p">The percentile, 0..100.</param>
        /// <returns>The bin's index; the last bin when the count never gets there.</returns>
        internal static double PercentileFromHistogram(long[] hist, long n, double p)
        {
            long target = (long)Math.Ceiling(p / 100.0 * n);
            long cum = 0;
            for (int b = 0; b < hist.Length; b++)
            {
                cum += hist[b];
                if (cum >= target) return b;
            }
            return hist.Length - 1;
        }
    }
}
