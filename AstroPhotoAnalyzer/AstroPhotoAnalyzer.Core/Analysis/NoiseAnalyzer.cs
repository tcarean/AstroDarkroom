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

// NoiseAnalyzer.cs - the measurement B3: how much noise is left in the sky, in 8-bit LSB.
//
// Why it matters on a FINISHED image: the noise is what dithers the quantization. Under ~0.5 LSB
// there is no natural dither left and a slow gradient breaks into steps on 8 bits; over ~2 LSB it
// shows at 100 % on screen. And on paper, at 200+ dpi, anything under 1.5 LSB is invisible -
// hence the denoise target for the print version.
//
// HOW: 64x64 tiles chosen at random (a fixed seed), only the smooth ones kept (a small range), on
// the reference channel. Sigma = std(difference between horizontally neighbouring pixels)/sqrt 2:
// the difference removes the slow gradient and leaves the noise, and sqrt 2 is because the
// difference of two independent variables has twice the variance. Exactly the acceptance script.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>A smooth tile the noise was measured on.</summary>
    /// <param name="X">The tile's left column, in pixels.</param>
    /// <param name="Y">The tile's top row, in pixels, 0 at the top.</param>
    /// <param name="SigmaLsb">The tile's noise sigma, in 8-bit LSB.</param>
    internal sealed record NoiseTile(int X, int Y, double SigmaLsb);

    /// <summary>
    /// The noise left in the sky (B3): the smooth tiles and their sigmas, in 8-bit LSB, with the
    /// flags the report reads.
    /// </summary>
    internal sealed record NoiseResult
    {
        internal int Candidates { get; init; }
        internal int SmoothTiles { get; init; }
        internal IReadOnlyList<NoiseTile> Tiles { get; init; }

        internal double SigmaMedianLsb { get; init; }
        internal double Sigma10thLsb { get; init; }
        internal double SigmaMinLsb { get; init; }

        /// <summary>
        /// The percentage of the smooth tiles with a sigma under the banding threshold, and under
        /// 1 LSB respectively.
        /// </summary>
        internal double PctTilesBelowBanding { get; init; }
        internal double PctTilesBelowOne { get; init; }

        /// <summary>
        /// Too few smooth tiles for the sigma to mean anything: the image is all structure.
        /// </summary>
        internal bool Insufficient => SmoothTiles < Thresholds.NoiseMinTiles;

        internal bool BandingRisk => !Insufficient && SigmaMedianLsb < Thresholds.NoiseBandingLsb;
        internal bool GoodForPrint => !Insufficient && SigmaMedianLsb >= Thresholds.NoisePrintLoLsb && SigmaMedianLsb <= Thresholds.NoisePrintHiLsb;
        internal bool UnderDenoised => !Insufficient && SigmaMedianLsb > Thresholds.NoiseUnderDenoisedLsb;
    }

    /// <summary>Measures the noise left in the sky (B3).</summary>
    internal static class NoiseAnalyzer
    {
        /// <summary>
        /// Draws <see cref="Thresholds.NoiseTileCandidates"/> random tiles of
        /// <see cref="Thresholds.NoiseTileSize"/> pixels on the reference plane, with the run's
        /// seed, keeps the smooth ones and measures the noise sigma on each.
        /// </summary>
        /// <param name="r">
        /// The image, 0..1; with a side not larger than a tile there are no candidates.
        /// </param>
        /// <param name="p">The run's parameters; only the seed is read.</param>
        /// <returns>The smooth tiles and the sigmas; NaN sigmas without a smooth tile.</returns>
        internal static NoiseResult Analyze(Raster r, Parameters p)
        {
            int size = Thresholds.NoiseTileSize;
            var rng = new SeededRandom(p.Seed);
            float[] plane = r.Reference;
            float maxRange = (float)(Thresholds.NoiseTileMaxRangeLevels / 255.0);
            var tiles = new List<NoiseTile>();

            int candidates = r.H > size && r.W > size ? Thresholds.NoiseTileCandidates : 0;
            for (int t = 0; t < candidates; t++)
            {
                int y = rng.Next(r.H - size), x = rng.Next(r.W - size);
                if (!IsSmooth(plane, r.W, x, y, size, maxRange)) continue;
                tiles.Add(new NoiseTile(x, y, SigmaLsb(plane, r.W, x, y, size)));
            }

            if (tiles.Count == 0)
                return new NoiseResult
                {
                    Candidates = candidates,
                    SmoothTiles = 0,
                    Tiles = tiles,
                    SigmaMedianLsb = double.NaN,
                    Sigma10thLsb = double.NaN,
                    SigmaMinLsb = double.NaN
                };

            float[] sigmas = Stats.Sorted(tiles.Select(t => (float)t.SigmaLsb).ToArray());
            return new NoiseResult
            {
                Candidates = candidates,
                SmoothTiles = tiles.Count,
                Tiles = tiles,
                SigmaMedianLsb = Stats.PercentileSorted(sigmas, 50),
                Sigma10thLsb = Stats.PercentileSorted(sigmas, 10),
                SigmaMinLsb = sigmas[0],
                PctTilesBelowBanding = 100.0 * sigmas.Count(s => s < Thresholds.NoiseBandingLsb) / sigmas.Length,
                PctTilesBelowOne = 100.0 * sigmas.Count(s => s < 1.0) / sigmas.Length,
            };
        }

        /// <summary>Whether a tile is smooth: its range, max - min, is at most the limit.</summary>
        /// <param name="plane">The plane, 0..1, row-major.</param>
        /// <param name="w">The plane's width, in pixels.</param>
        /// <param name="x0">The tile's left column.</param>
        /// <param name="y0">The tile's top row, 0 at the top.</param>
        /// <param name="size">The tile's side, in pixels; the tile lies inside the plane.</param>
        /// <param name="maxRange">The largest range allowed, on the scale 0..1.</param>
        /// <returns><c>true</c> when max - min is at most <paramref name="maxRange"/>.</returns>
        internal static bool IsSmooth(float[] plane, int w, int x0, int y0, int size, float maxRange)
        {
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            for (int y = y0; y < y0 + size; y++)
            {
                int o = y * w;
                for (int x = x0; x < x0 + size; x++)
                {
                    float v = plane[o + x];
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                }
            }
            return hi - lo <= maxRange;
        }

        /// <summary>
        /// The noise sigma on a tile, in 8-bit LSB: std(horizontal diff) / sqrt 2 * 255.
        /// </summary>
        /// <param name="plane">The plane, 0..1, row-major.</param>
        /// <param name="w">The plane's width, in pixels.</param>
        /// <param name="x0">The tile's left column.</param>
        /// <param name="y0">The tile's top row, 0 at the top.</param>
        /// <param name="size">
        /// The tile's side, in pixels, at least 2; the tile lies inside the plane.
        /// </param>
        /// <returns>
        /// The sigma, in 8-bit LSB: the population standard deviation of the differences, divided
        /// by sqrt 2, times 255.
        /// </returns>
        internal static double SigmaLsb(float[] plane, int w, int x0, int y0, int size)
        {
            int n = size * (size - 1);
            double sum = 0, sumSq = 0;
            for (int y = y0; y < y0 + size; y++)
            {
                int o = y * w;
                for (int x = x0; x < x0 + size - 1; x++)
                {
                    double d = plane[o + x + 1] - plane[o + x];
                    sum += d; sumSq += d * d;
                }
            }
            double mean = sum / n;
            double var = sumSq / n - mean * mean;
            return Math.Sqrt(Math.Max(0, var)) / Math.Sqrt(2) * 255;
        }
    }
}
