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

// QuantizationAnalyzer.cs - masuratorile C1, C2 si C4: ce se intampla la trecerea pe
// 8 biti.
//
// C1, palierul: pe cerul cuantizat la 8 biti, cat de lungi sunt sirurile de pixeli cu
// aceeasi valoare. Pe hartie un palier devine un contur; sub ~1 mm nu se vede. Dalele
// sunt cele stricte (Thresholds.Plateau*): cer curat, fara nucleu.
//
// C2, eroarea de rotunjire: 16 -> 8 biti e |v*255 - round(v*255)|, si raportul o spune
// ca sa se vada ca e pura rotunjire (max 0.5), nu o pierdere.
//
// C4, testul de stres: acelasi shadow-lift aplicat pe datele de 16 biti si pe cele
// deja cuantizate la 8, apoi numarul de niveluri lipsa din umbre. Cifra care spune
// "pastreaza master-ul de 16 biti" nu e o parere, e diferenta dintre cele doua.

using System;
using System.Collections.Generic;

namespace AstroPhotoAnalyzer
{
    internal sealed record QuantizationResult
    {
        internal int PlateauTiles { get; init; }

        /// <summary>Lungimea medie a sirurilor de valori identice, in pixeli; NaN fara dale.</summary>
        internal double PlateauPx { get; init; }
        internal bool PlateauKnown => !double.IsNaN(PlateauPx);
        internal double PlateauMmAt(double dpi) => PlateauPx * 25.4 / dpi;
        internal bool PlateauVisibleAt(double dpi) => PlateauKnown && PlateauMmAt(dpi) > Thresholds.PlateauVisibleMm;

        /// <summary>C2 si C4 au sens doar pe o sursa cu mai mult de 8 biti.</summary>
        internal bool Applicable { get; init; }

        internal double RoundingMeanAbs { get; init; }
        internal double RoundingMax { get; init; }
        internal double RoundingP999 { get; init; }

        internal int LevelsUsedFrom16 { get; init; }
        internal int MissingFrom16 { get; init; }
        internal int LevelsUsedFrom8 { get; init; }
        internal int MissingFrom8 { get; init; }
        internal bool Keep16BitMaster => Applicable && MissingFrom8 > Thresholds.MissingLevelsWarn;
    }

    internal static class QuantizationAnalyzer
    {
        internal static QuantizationResult Analyze(Raster r)
        {
            var (tiles, plateau) = Plateau(r);
            bool applicable = r.Source == null || r.Source.IsFloat || r.Source.BitDepth > 8;
            var (mean, max, p999) = applicable ? Rounding(r) : (0, 0, 0);
            var (u16, m16, u8, m8) = applicable ? Stress(r) : (0, 0, 0, 0);
            return new QuantizationResult
            {
                PlateauTiles = tiles, PlateauPx = plateau,
                Applicable = applicable,
                RoundingMeanAbs = mean, RoundingMax = max, RoundingP999 = p999,
                LevelsUsedFrom16 = u16, MissingFrom16 = m16, LevelsUsedFrom8 = u8, MissingFrom8 = m8,
            };
        }

        static (int Tiles, double PlateauPx) Plateau(Raster r)
        {
            int size = Thresholds.NoiseTileSize;
            if (r.H <= size || r.W <= size) return (0, double.NaN);
            float[] g = r.Reference;
            var rng = new SeededRandom(Thresholds.PlateauSeed);
            float maxRange = (float)(Thresholds.PlateauTileMaxRangeLevels / 255.0);
            float maxMean = (float)(Thresholds.PlateauTileMaxMeanLevels / 255.0);

            double sum = 0; int tiles = 0;
            for (int attempt = 0; attempt < Thresholds.PlateauMaxAttempts && tiles < Thresholds.PlateauTileCount; attempt++)
            {
                int y0 = rng.Next(r.H - size), x0 = rng.Next(r.W - size);
                if (!Accept(g, r.W, x0, y0, size, maxRange, maxMean)) continue;

                // Schimbari de valoare pe fiecare rand, pe cerul cuantizat; medie pe randuri.
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
            return (tiles, tiles == 0 ? double.NaN : sum / tiles);
        }

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

        /// <summary>Eroarea de rotunjire pe toate planurile, la pasul percentilelor, in niveluri.</summary>
        static (double Mean, double Max, double P999) Rounding(Raster full)
        {
            Raster r = RasterOps.Subsample(full, Thresholds.PercentileStride);
            const int bins = 1000;   // 0..1 nivel, in miimi
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

        /// <summary>Nivelurile folosite intre ShadowLiftLevelLo si Hi (exclusiv) si cate lipsesc intre primul si ultimul folosit.</summary>
        static int Count(bool[] seen, out int missing)
        {
            int used = 0, first = -1, last = -1;
            for (int l = Thresholds.ShadowLiftLevelLo + 1; l < Thresholds.ShadowLiftLevelHi; l++)
                if (seen[l]) { used++; if (first < 0) first = l; last = l; }
            missing = used == 0 ? 0 : (last - first + 1) - used;
            return used;
        }

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
