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

// RasterOps.cs - transformarile pure pe un Raster: subesantionare, decupare, curba
// gamma, cuantizare, L* pe tot planul, si garda pentru date liniare.
//
// Toate intorc rastere noi; niciuna nu modifica intrarea. Analizoarele pot deci lucra
// pe acelasi raster in orice ordine.

using System;

namespace AstroPhotoAnalyzer
{
    internal static class RasterOps
    {
        /// <summary>
        /// Fiecare al <paramref name="stride"/>-lea pixel pe ambele axe, ancorat sus-stanga
        /// - exact a[::s, ::s] din numpy, cu care s-au masurat cifrele de acceptanta. Nu
        /// e o medie de bloc: distributiile tonale vor pixeli reali, nu pixeli netezi.
        /// </summary>
        internal static Raster Subsample(Raster r, int stride)
        {
            if (stride <= 1) return r;
            int h = (r.H + stride - 1) / stride, w = (r.W + stride - 1) / stride;
            var o = new Raster(h, w, r.Planes.Length) { Source = r.Source };
            for (int p = 0; p < r.Planes.Length; p++)
            {
                float[] src = r.Planes[p], dst = o.Planes[p];
                for (int y = 0; y < h; y++)
                {
                    int sy = y * stride * r.W;
                    int dy = y * w;
                    for (int x = 0; x < w; x++) dst[dy + x] = src[sy + x * stride];
                }
            }
            return o;
        }

        internal static Raster Crop(Raster r, int x0, int y0, int w, int h)
        {
            if (x0 < 0 || y0 < 0 || x0 + w > r.W || y0 + h > r.H || w <= 0 || h <= 0)
                throw new ArgumentException($"decupaj in afara rasterului: {w}x{h} la ({x0},{y0}) din {r.W}x{r.H}");
            var o = new Raster(h, w, r.Planes.Length) { Source = r.Source };
            for (int p = 0; p < r.Planes.Length; p++)
                for (int y = 0; y < h; y++)
                    Array.Copy(r.Planes[p], (y0 + y) * r.W + x0, o.Planes[p], y * w, w);
            return o;
        }

        /// <summary>
        /// v^gamma pe fiecare canal, identic pe toate - deci un gri neutru ramane neutru.
        /// E curba simularii de print: gamma = ln(tinta)/ln(fundal) duce fundalul masurat
        /// exact in tinta si ridica restul proportional. Nu e un stretch adevarat si
        /// raportul o numeste simulare.
        /// </summary>
        internal static Raster Gamma(Raster r, double gamma)
        {
            // Printr-o tabela pe cele 65536 de niveluri de 16 biti, nu prin Math.Pow pe fiecare
            // esantion: pe un raster de 4 milioane de esantioane Pow ar fi secunde. Pe o sursa
            // de 16 biti valoarea cade exact pe un nivel al tabelei si rezultatul e cel al lui
            // Pow; pe float (FITS) se interpoleaza liniar intre cele doua niveluri vecine, cu o
            // eroare sub 1e-6 pe orice gamma dintre 0.3 si 3. Simularea de print nu mai trece
            // pe aici: ridica si liniarizeaza dintr-o singura tabela (LiftTable, ADR 0035);
            // aici raman preview-ul si harta ferestrei, care au nevoie de rasterul ridicat.
            double[] lut = PowerLut(gamma);
            var o = new Raster(r.H, r.W, r.Planes.Length) { Source = r.Source };
            for (int p = 0; p < r.Planes.Length; p++)
            {
                float[] src = r.Planes[p], dst = o.Planes[p];
                for (int i = 0; i < src.Length; i++)
                {
                    // Un NaN ar da (int)NaN = int.MinValue, un indice imposibil; cititorii nu lasa
                    // NaN sa treaca, dar un raster facut in memorie poate.
                    float s = src[i];
                    double v = (float.IsNaN(s) ? 0 : Math.Clamp(s, 0f, 1f)) * 65535.0;
                    int k = (int)v;
                    double f = v - k;
                    dst[i] = (float)(f == 0 ? lut[k] : lut[k] + f * (lut[k + 1] - lut[k]));
                }
            }
            return o;
        }

        /// <summary>
        /// Tabela curbei v^gamma pe cele 65536 de niveluri de 16 biti, plus nivelul 65536 ca
        /// interpolarea sa aiba vecin si la 1.0: aceeasi pentru Gamma si pentru LiftTable.
        /// </summary>
        internal static double[] PowerLut(double gamma)
        {
            var lut = new double[65537];
            for (int k = 0; k < lut.Length; k++) lut[k] = Math.Pow(k / 65535.0, gamma);
            return lut;
        }

        /// <summary>Gamma care duce fundalul <paramref name="from"/> in <paramref name="to"/>.</summary>
        internal static double GammaFor(double from, double to)
        {
            if (from <= 0 || from >= 1 || to <= 0 || to >= 1)
                throw new ArgumentException("fundalul si tinta trebuie sa fie in (0, 1)");
            return Math.Log(to) / Math.Log(from);
        }

        /// <summary>Nivelul pe 8 biti, prin rotunjire - ce ar scrie un export 8-bit.</summary>
        internal static int Level8(float v) => (int)Math.Round(Math.Clamp(v, 0f, 1f) * 255.0);

        /// <summary>Nivelul pe 16 biti. Pe o sursa de 16 biti intoarce exact ce era in fisier.</summary>
        internal static int Level16(float v) => (int)Math.Round(Math.Clamp(v, 0f, 1f) * 65535.0);

        /// <summary>
        /// L* pentru fiecare pixel, pe rasterul subesantionat cu <paramref name="stride"/>.
        /// Pe color trece prin luminanta Rec. 709 a componentelor liniarizate; pe mono
        /// e L*-ul valorii. Vectorul e in ordinea pixelilor, bun de sortat.
        /// </summary>
        internal static float[] LstarPlane(Raster r, int stride)
            => LstarPlane(Subsample(r, stride), new ExactLinearizer());

        /// <summary>
        /// L* pentru fiecare pixel al unui raster DEJA subesantionat, cu esantioanele trecute
        /// prin <paramref name="lin"/>: exact, sau ridicate cu o gamma si liniarizate din
        /// tabela (LiftTable), cum le vrea simularea pe randurile grilei.
        /// </summary>
        internal static float[] LstarPlane<T>(Raster s, T lin) where T : struct, ISrgbLinearizer
        {
            var o = new float[s.H * s.W];
            if (s.IsColor)
            {
                float[] R = s.Planes[0], G = s.Planes[1], B = s.Planes[2];
                for (int i = 0; i < o.Length; i++)
                    o[i] = (float)ColorMath.LstarOfY(ColorMath.Luminance(lin.Linear(R[i]), lin.Linear(G[i]), lin.Linear(B[i])));
            }
            else
            {
                float[] M = s.Planes[0];
                for (int i = 0; i < o.Length; i++)
                    o[i] = (float)ColorMath.LstarOfY(lin.Linear(M[i]));
            }
            return o;
        }

        /// <summary>
        /// Pare imaginea LINIARA, nu stretch-uita? Un stack liniar are mediana la o mie de
        /// fundal, aproape totul in primele procente ale scalei; o imagine finita are
        /// fundalul la 0.1-0.25. Pragul e in Thresholds. Garda exista ca unealta sa refuze
        /// cu un mesaj, nu sa raporteze "99% sub negrul hartiei" pe un fisier pe care
        /// nimeni nu-l printeaza asa.
        /// </summary>
        internal static bool LooksLinear(Raster r, out double median)
        {
            median = Stats.Median(Subsample(r, 8).Reference);
            return median < Thresholds.LinearMedianMax;
        }
    }
}
