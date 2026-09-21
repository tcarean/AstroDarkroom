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

// BackgroundAnalyzer.cs - masuratorile E1 si E2: ce culoare are cerul, si daca vreun
// colt sau vreo latura se abate de la el.
//
// E1. "Fundalul" e cel mai intunecat sfert al pixelilor (dupa luminanta medie a
// canalelor), fara banda de jos a imaginii - acolo sta orizontul si poluarea lui, care
// nu e "fundal". Pe el se iau medianele R, G, B. O tenta de 1-2 niveluri e invizibila la
// fundal 0.10 si evidenta la 0.19: de asta se masoara acum, inainte de re-stretch.
//
// E2. Opt regiuni pe margine (4 colturi, 4 laturi), fiecare cu medianele ei R, G, B fata
// de fundal. Nu se compara luminanta, ci FIECARE canal: coltul de orizont al imaginii de
// acceptanta avea aceeasi luminanta ca cerul (R +9, B -10), si o comparatie pe luminanta
// nu l-ar fi vazut.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal sealed record EdgeRegion
    {
        /// <summary>"colt stanga-sus", "latura dreapta" etc. - numele din raport.</summary>
        internal string Name { get; init; }
        internal int X0 { get; init; }
        internal int Y0 { get; init; }
        internal int W { get; init; }
        internal int H { get; init; }

        /// <summary>Medianele regiunii, in niveluri 0..255, pe fiecare plan.</summary>
        internal IReadOnlyList<double> Levels { get; init; }

        /// <summary>Regiune minus fundal, pe fiecare plan, in niveluri.</summary>
        internal IReadOnlyList<double> Deltas { get; init; }

        internal double MaxAbsDelta => Deltas.Max(d => Math.Abs(d));
        internal bool Flagged => MaxAbsDelta > Thresholds.CornerGradientLevels;

        /// <summary>
        /// Abaterea are CULOARE (canalele se misca diferit), nu doar luminozitate? Pe mono
        /// nu exista notiunea. Vezi Thresholds.CornerColourSpreadRatio.
        /// </summary>
        internal bool Coloured
        {
            get
            {
                if (Deltas.Count < 3) return false;
                double spread = Deltas.Max() - Deltas.Min();
                double mean = Deltas.Average(d => Math.Abs(d));
                return spread > Thresholds.CornerGradientLevels && spread > Thresholds.CornerColourSpreadRatio * mean;
            }
        }
    }

    internal sealed record BackgroundResult
    {
        /// <summary>Medianele fundalului, in niveluri, pe fiecare plan (unul singur pe mono).</summary>
        internal IReadOnlyList<double> Levels { get; init; }

        /// <summary>Metricile de tenta au sens doar pe color.</summary>
        internal bool ColorApplicable { get; init; }

        /// <summary>G - (R + B) / 2: pozitiv = verde, negativ = magenta.</summary>
        internal double GreenExcess { get; init; }

        /// <summary>R - B: pozitiv = cald, negativ = rece.</summary>
        internal double RedMinusBlue { get; init; }

        internal bool HasCast => ColorApplicable
            && (Math.Abs(GreenExcess) > Thresholds.CastWarnLevels || Math.Abs(RedMinusBlue) > Thresholds.CastWarnLevels);

        internal IReadOnlyList<EdgeRegion> Regions { get; init; }
        internal IEnumerable<EdgeRegion> FlaggedRegions => Regions.Where(x => x.Flagged);
    }

    internal static class BackgroundAnalyzer
    {
        internal static BackgroundResult Analyze(Raster full)
        {
            Raster s = RasterOps.Subsample(full, Thresholds.BackgroundStride);
            int n = s.H * s.W, planes = s.Planes.Length;

            // Luminanta simpla, media planurilor - ca in script. Pragul se ia pe TOT
            // subesantionul, apoi se scoate banda de jos: asa era scriptul, si asa
            // fundalul ramane "cel mai intunecat sfert al imaginii", nu al partii de sus.
            var lum = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = 0;
                for (int p = 0; p < planes; p++) t += s.Planes[p][i];
                lum[i] = t / planes;
            }
            double cut = Stats.PercentileSorted(Stats.Sorted(lum), 100 * Thresholds.BackgroundDarkestFraction);
            int rowLimit = (int)(s.H * (1 - Thresholds.BackgroundExcludeBottomFraction));

            // "<=", nu "<": pe un cer plat multi pixeli sunt EXACT la percentila, si un
            // "<" strict n-ar alege niciunul. Pe date reale, cu valori continue, cele
            // doua dau acelasi lucru.
            var picked = new List<int>();
            for (int y = 0; y < rowLimit; y++)
                for (int x = 0; x < s.W; x++)
                    if (lum[y * s.W + x] <= cut) picked.Add(y * s.W + x);

            var bg = new double[planes];
            for (int p = 0; p < planes; p++)
            {
                var vals = new float[picked.Count];
                for (int k = 0; k < picked.Count; k++) vals[k] = s.Planes[p][picked[k]];
                bg[p] = vals.Length == 0 ? 0 : Stats.Median(vals) * 255;
            }

            bool color = planes == 3;
            return new BackgroundResult
            {
                Levels = bg,
                ColorApplicable = color,
                GreenExcess = color ? bg[1] - (bg[0] + bg[2]) / 2 : 0,
                RedMinusBlue = color ? bg[0] - bg[2] : 0,
                Regions = Regions(s, bg),
            };
        }

        static IReadOnlyList<EdgeRegion> Regions(Raster s, double[] bg)
        {
            // Coltul: 15 % din inaltime x 20 % din latime (regiunea din script). Latura: o
            // banda de aceeasi grosime, pe mijlocul laturii, intre colturi.
            int ch = (int)(s.H * Thresholds.EdgeRegionHeightFraction), cw = (int)(s.W * Thresholds.EdgeRegionWidthFraction);
            var defs = new (string Name, int X0, int Y0, int W, int H)[]
            {
                ("colt stanga-sus",    0,         0,         cw,           ch),
                ("colt dreapta-sus",   s.W - cw,  0,         cw,           ch),
                ("colt stanga-jos",    0,         s.H - ch,  cw,           ch),
                ("colt dreapta-jos",   s.W - cw,  s.H - ch,  cw,           ch),
                ("latura de sus",      cw,        0,         s.W - 2 * cw, ch),
                ("latura de jos",      cw,        s.H - ch,  s.W - 2 * cw, ch),
                ("latura stanga",      0,         ch,        cw,           s.H - 2 * ch),
                ("latura dreapta",     s.W - cw,  ch,        cw,           s.H - 2 * ch),
            };

            var list = new List<EdgeRegion>();
            foreach (var d in defs)
            {
                if (d.W <= 0 || d.H <= 0) continue;
                var levels = new double[s.Planes.Length];
                var deltas = new double[s.Planes.Length];
                var buf = new float[d.W * d.H];
                for (int p = 0; p < s.Planes.Length; p++)
                {
                    int k = 0;
                    for (int y = d.Y0; y < d.Y0 + d.H; y++)
                        for (int x = d.X0; x < d.X0 + d.W; x++)
                            buf[k++] = s.Planes[p][y * s.W + x];
                    levels[p] = Stats.Median(buf) * 255;
                    deltas[p] = levels[p] - bg[p];
                }
                list.Add(new EdgeRegion { Name = d.Name, X0 = d.X0, Y0 = d.Y0, W = d.W, H = d.H, Levels = levels, Deltas = deltas });
            }
            return list;
        }
    }
}
