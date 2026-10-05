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

// GamutAnalyzer.cs - cati pixeli ies din gamutul hartiei (R-144, ADR 0032).
//
// O CIFRA, nu o corectie: nu intra in nicio curba. O culoare e "in afara" cand drumul
// sRGB -> imprimanta -> inapoi, prin tabelele colorimetrice relative ale profilului, ii
// muta a*, b* cu mai mult de CurveGamutDeltaAb. Doar croma: ce cade sub negrul hartiei e
// treaba tabului Tonuri, iar fara compensarea punctului negru L*-ul umbrelor s-ar numara
// de doua ori (pe canvas, jumatate din imagine).
//
// Pachetul (Wacton.Unicolour) face drumul in ~28 de microsecunde pe culoare (masurat pe
// 2026-10-05, pe profilele laboratorului): un subesantion de 1.4 MP ar dura 40 s. De
// aceea se trece o data o grila de CurveGamutGrid^3 culori, iar pixelii iau deplasarea
// (da, db) prin interpolare triliniara. Langa marginea gamutului interpolarea rotunjeste
// coltul; cifra e o estimare buna la zecimi de procent, nu o masuratoare la pixel.

using System;
using System.IO;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;

namespace AstroPhotoAnalyzer
{
    /// <summary>Deplasarea (da, db) a fiecarui nod al grilei sRGB dupa drumul prin profil.</summary>
    internal sealed class GamutTable
    {
        internal int N;
        internal float[] Da, Db;

        /// <summary>De ce nu s-a putut construi, pentru om; null cand s-a putut.</summary>
        internal string Problem;
    }

    internal static class GamutAnalyzer
    {
        /// <summary>Tabela dus-intors a profilului de la <paramref name="path"/>; cu Problem setat cand profilul nu se poate folosi.</summary>
        internal static GamutTable Build(string path, int n = Thresholds.CurveGamutGrid)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var icc = new IccConfiguration(new Profile(bytes, path), Intent.RelativeColorimetric, path);
                if (!string.IsNullOrEmpty(icc.Error)) return new GamutTable { Problem = "profilul nu se poate citi: " + icc.Error };
                var config = new Configuration(xyzConfig: XyzConfiguration.D50, iccConfig: icc);
                var t = new GamutTable { N = n, Da = new float[n * n * n], Db = new float[n * n * n] };
                for (int r = 0; r < n; r++)
                    for (int g = 0; g < n; g++)
                        for (int b = 0; b < n; b++)
                        {
                            var colour = new Unicolour(config, ColourSpace.Rgb, r / (n - 1.0), g / (n - 1.0), b / (n - 1.0));
                            var back = new Unicolour(config, colour.Icc);
                            int i = (r * n + g) * n + b;
                            t.Da[i] = (float)(back.Lab.A - colour.Lab.A);
                            t.Db[i] = (float)(back.Lab.B - colour.Lab.B);
                        }
                foreach (float v in t.Da) if (float.IsNaN(v)) return new GamutTable { Problem = "profilul nu are tabela spre imprimanta (B2A)" };
                return t;
            }
            catch (Exception e)
            {
                return new GamutTable { Problem = "profilul nu se poate citi: " + e.Message };
            }
        }

        /// <summary>Pixelul (r, g, b) iese din gamut: deplasarea interpolata trece de prag.</summary>
        internal static bool Outside(GamutTable t, float r, float g, float b)
        {
            int n = t.N;
            float fr = Math.Clamp(r, 0f, 1f) * (n - 1), fg = Math.Clamp(g, 0f, 1f) * (n - 1), fb = Math.Clamp(b, 0f, 1f) * (n - 1);
            int ir = Math.Min((int)fr, n - 2), ig = Math.Min((int)fg, n - 2), ib = Math.Min((int)fb, n - 2);
            float tr = fr - ir, tg = fg - ig, tb = fb - ib;
            float da = 0, db = 0;
            for (int k = 0; k < 8; k++)
            {
                int dr = k & 1, dg = (k >> 1) & 1, dbb = (k >> 2) & 1;
                float w = (dr == 1 ? tr : 1 - tr) * (dg == 1 ? tg : 1 - tg) * (dbb == 1 ? tb : 1 - tb);
                int i = ((ir + dr) * n + ig + dg) * n + ib + dbb;
                da += w * t.Da[i];
                db += w * t.Db[i];
            }
            return da * da + db * db > Thresholds.CurveGamutDeltaAb * Thresholds.CurveGamutDeltaAb;
        }

        /// <summary>
        /// Procentul pixelilor subesantionului tonal <paramref name="sub"/> din afara gamutului,
        /// in total si pe clasele hartii (praf, cer, banda, nori luminosi, varfuri) cand
        /// <paramref name="byClass"/>; o imagine mono n-are croma si da 0.
        /// </summary>
        internal static GamutFigure Measure(GamutTable t, Raster sub, DustMap map, bool byClass)
        {
            long all = 0, outAll = 0;
            var n = new long[6]; var o = new long[6];
            if (sub.IsColor)
            {
                float[] R = sub.Planes[0], G = sub.Planes[1], B = sub.Planes[2];
                for (int y = 0; y < sub.H; y++)
                {
                    int by = y / map.Side;
                    for (int x = 0; x < sub.W; x++)
                    {
                        int i = y * sub.W + x;
                        bool outside = Outside(t, R[i], G[i], B[i]);
                        all++; if (outside) outAll++;
                        int bx = x / map.Side;
                        if (!byClass || bx >= map.Cols || by >= map.Rows) continue;
                        int c = (int)map.Classes[by * map.Cols + bx];
                        n[c]++; if (outside) o[c]++;
                    }
                }
            }
            double[] per = null;
            if (byClass)
            {
                per = new double[5];
                for (int c = 1; c <= 5; c++) per[c - 1] = n[c] == 0 ? (sub.IsColor ? double.NaN : 0) : 100.0 * o[c] / n[c];
            }
            return new GamutFigure { PctAll = all == 0 ? 0 : 100.0 * outAll / all, PctByClass = per };
        }
    }
}
