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

// NoiseAnalyzer.cs - masuratoarea B3: cat zgomot a ramas in cer, in LSB de 8 biti.
//
// De ce conteaza pe o imagine FINITA: zgomotul e ce dither-eaza cuantizarea. Sub ~0.5
// LSB nu mai exista dither natural si un gradient lent se rupe in trepte pe 8 biti;
// peste ~2 LSB se vede la 100 % pe ecran. Iar pe hartie, la 200+ dpi, orice sub 1.5 LSB
// e invizibil - de aici tinta de denoise pentru versiunea de print.
//
// CUM: dale de 64x64 alese aleator (samanta fixa), pastrate doar cele netede (range mic),
// pe canalul de referinta. Sigma = std(diferenta intre pixeli vecini pe orizontala)/sqrt 2:
// diferenta scoate gradientul lent si lasa zgomotul, iar sqrt 2 e pentru ca diferenta a
// doua variabile independente are varianta dubla. Exact scriptul de acceptanta.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal sealed record NoiseTile(int X, int Y, double SigmaLsb);

    internal sealed record NoiseResult
    {
        internal int Candidates { get; init; }
        internal int SmoothTiles { get; init; }
        internal IReadOnlyList<NoiseTile> Tiles { get; init; }

        internal double SigmaMedianLsb { get; init; }
        internal double Sigma10thLsb { get; init; }
        internal double SigmaMinLsb { get; init; }

        /// <summary>Procentul dalelor netede cu sigma sub pragul de banding, respectiv sub 1 LSB.</summary>
        internal double PctTilesBelowBanding { get; init; }
        internal double PctTilesBelowOne { get; init; }

        /// <summary>Prea putine dale netede ca sigma sa insemne ceva: imaginea e toata structura.</summary>
        internal bool Insufficient => SmoothTiles < Thresholds.NoiseMinTiles;

        internal bool BandingRisk => !Insufficient && SigmaMedianLsb < Thresholds.NoiseBandingLsb;
        internal bool GoodForPrint => !Insufficient && SigmaMedianLsb >= Thresholds.NoisePrintLoLsb && SigmaMedianLsb <= Thresholds.NoisePrintHiLsb;
        internal bool UnderDenoised => !Insufficient && SigmaMedianLsb > Thresholds.NoiseUnderDenoisedLsb;
    }

    internal static class NoiseAnalyzer
    {
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

        /// <summary>Sigma zgomotului pe o dala, in LSB de 8 biti: std(diff orizontal) / sqrt 2 * 255.</summary>
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
