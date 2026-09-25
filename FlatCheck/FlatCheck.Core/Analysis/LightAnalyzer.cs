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

// LightAnalyzer.cs - modul 3: testul end-to-end, singurul care inchide bucla.
//
// Fondul local (percentila mica per tile) e fitat simultan cu un plan si doi termeni
// radiali:
//
//     bg = a + b*X + c*Y + d*R2 + e*R2^2        X, Y normalizate la semidiagonala
//
// Planul absoarbe gradientul de poluare luminoasa; termenii radiali capteaza vignetarea
// reziduala, adica eroarea de flat. Fitul simultan e ce face testul sa mearga si pe cer
// poluat: cele doua au forme diferite si se separa.
//
// DAR NUMAI PE CAMP INGUST. Separarea de mai sus tine cat timp cupola de poluare e
// cvasi-liniara peste cadru. Pe camp larg ea se curbeaza in interiorul cadrului, iar
// partea radial simetrica a curburii are exact forma vignetarii - si atunci nimic din
// ce e scris aici nu le mai poate deosebi. Vezi LightSuitability, care opreste modul
// inainte sa ajunga aici, si sectiunea "The validity limit" din CLAUDE.md.
//
// ATENTIE: NU inlocui cu percentile pe inele circulare - p20 pe un inel prinde partea
// intunecata a gradientului de cer si o raporteaza ca falsa vignetare.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    internal sealed class LightResult
    {
        internal double A, B, C, D, E;       // coeficientii fitului
        internal double RadPct;              // -(d+e)/a*100; SEMNUL e diagnosticul
        internal double PlanePct;            // gradientul de cer, de ignorat
        internal int KeptTiles, TotalTiles;
        internal int TileRows, TileCols;
        internal Img RadialMap;              // 1 + (d*R2 + e*R2^2)/a, pentru harta
        internal string Verdict;
    }

    internal static class LightAnalyzer
    {
        internal static LightResult Analyze(Img raw)
        {
            Img img = Img.Prepare(raw);
            int h = img.H, w = img.W;
            double cx = w / 2.0, cy = h / 2.0;
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

            // Sigma-clip iterativ, UNILATERAL (res < prag): contaminantii - stele si
            // nebulozitate - adauga doar semnal pozitiv, deci se taie doar in sus.
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

            // Daca Solve a esuat chiar la prima iteratie, coef e inca null. Inainte de
            // faza 8 linia urmatoare arunca NullReferenceException; acum spune de ce.
            if (coef == null)
                throw new Exception(
                    "Fitul fondului a esuat (sistem singular) - prea putine tile-uri utilizabile.");

            double a0 = coef[0], b0 = coef[1], c0 = coef[2], d0 = coef[3], e0 = coef[4];
            double radAtCorner = d0 + e0;                       // R2 = 1 in colt
            double radPct = -radAtCorner / a0 * 100;            // + = fondul cade spre margini
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
