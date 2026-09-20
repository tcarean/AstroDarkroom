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

// StarAnalyzer.cs - masuratorile D1 si D2: cat de late sunt stelele, global si pe
// regiuni. FWHM-ul e rezolutia REALA a imaginii: o stea de 3 px inseamna ca nimic mai
// fin de 3 px nu exista in fisier, oricati pixeli ar avea el. De aici pleaca tabelul de
// marimi, si de aici cade mitul "300 dpi".
//
// CUM: candidat = pixel peste media planului + amplitudinea minima, care e maximul
// ferestrei lui de 25x25.
// Fundalul stelei = mediana marginii ferestrei; amplitudinea = varf - fundal, cu prag
// jos (o pata slaba n-are profil) si prag sus (o stea saturata n-are jumatate). Latimea
// la jumatate se ia pe orizontala si pe verticala, cu interpolare liniara subpixel, si
// se mediaza. Exact scriptul de acceptanta - cu o diferenta: scriptul lua 20.000 de
// candidati la intamplare, aici se iau TOTI. E determinist si mai bine esantionat.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal sealed record Star(int X, int Y, double Fwhm, double Peak);

    internal sealed record StarRegion
    {
        internal string Name { get; init; }
        internal int X0 { get; init; }
        internal int Y0 { get; init; }
        internal int W { get; init; }
        internal int H { get; init; }
        internal int Count { get; init; }

        /// <summary>NaN sub Thresholds.RegionMinStars stele.</summary>
        internal double FwhmMedian { get; init; }
        internal bool Known => !double.IsNaN(FwhmMedian);
    }

    internal sealed record StarResult
    {
        internal IReadOnlyList<Star> Stars { get; init; }
        internal int Count => Stars.Count;
        internal bool Insufficient => Count < Thresholds.StarMinCount;

        /// <summary>NaN cand sunt prea putine stele.</summary>
        internal double FwhmMedian { get; init; }
        internal double FwhmQ1 { get; init; }
        internal double FwhmQ3 { get; init; }

        /// <summary>Centrul primul, apoi cele patru colturi.</summary>
        internal IReadOnlyList<StarRegion> Regions { get; init; }

        /// <summary>Cel mai lat colt / centru; NaN daca centrul sau toate colturile sunt necunoscute.</summary>
        internal double CornerToCenterRatio { get; init; }
        internal bool SoftCorners => !double.IsNaN(CornerToCenterRatio) && CornerToCenterRatio > Thresholds.CornerSoftRatio;

        /// <summary>Cati pixeli de detaliu real are imaginea pe latime: W / FWHM.</summary>
        internal double EffectiveDetailPx(int w) => Insufficient ? double.NaN : w / FwhmMedian;
    }

    internal static class StarAnalyzer
    {
        const int Half = 12;   // fereastra 25 = 2 * 12 + 1

        internal static StarResult Analyze(Raster r)
        {
            float[] g = r.Reference;
            int W = r.W, H = r.H;
            float thr = (float)(Stats.Mean(g) + Thresholds.StarCandidateAboveMean16 / 65535.0);
            float minAmp = (float)(Thresholds.StarMinAmplitude16 / 65535.0);
            float maxPeak = (float)(Thresholds.StarMaxPeak16 / 65535.0);

            var stars = new List<Star>();
            var border = new float[4 * (2 * Half + 1) - 4];
            var line = new double[2 * Half + 1];

            for (int y = Half; y < H - Half; y++)
            {
                int row = y * W;
                for (int x = Half; x < W - Half; x++)
                {
                    float v = g[row + x];
                    if (v <= thr || v > maxPeak) continue;
                    if (!IsWindowMax(g, W, x, y, v)) continue;

                    float baseLevel = BorderMedian(g, W, x, y, border);
                    float amp = v - baseLevel;
                    if (amp < minAmp) continue;
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

        /// <summary>Maxim al ferestrei, cu egalitate permisa: un varf plat de doi pixeli e tot o stea.</summary>
        static bool IsWindowMax(float[] g, int W, int x, int y, float v)
        {
            for (int dy = -Half; dy <= Half; dy++)
            {
                int o = (y + dy) * W + x;
                for (int dx = -Half; dx <= Half; dx++)
                    if (g[o + dx] > v) return false;
            }
            return true;
        }

        static float BorderMedian(float[] g, int W, int x, int y, float[] buf)
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
            Array.Sort(buf);
            return (buf[buf.Length / 2 - 1] + buf[buf.Length / 2]) / 2;
        }

        /// <summary>
        /// Latimea profilului la nivelul <paramref name="half"/>: se coboara din centru pana
        /// sub nivel in fiecare directie, apoi se interpoleaza liniar locul exact al
        /// trecerii. Aceeasi functie ca in script, inclusiv epsilonul de la impartire.
        /// </summary>
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

        static IReadOnlyList<StarRegion> Regions(List<Star> stars, int W, int H)
        {
            int cw = (int)(W * Thresholds.RegionFraction), ch = (int)(H * Thresholds.RegionFraction);
            int mw = (int)(W * Thresholds.CenterFraction), mh = (int)(H * Thresholds.CenterFraction);
            var defs = new (string Name, int X0, int Y0, int W, int H)[]
            {
                ("centru",            (W - mw) / 2, (H - mh) / 2, mw, mh),
                ("colt stanga-sus",   0,            0,            cw, ch),
                ("colt dreapta-sus",  W - cw,       0,            cw, ch),
                ("colt stanga-jos",   0,            H - ch,       cw, ch),
                ("colt dreapta-jos",  W - cw,       H - ch,       cw, ch),
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
