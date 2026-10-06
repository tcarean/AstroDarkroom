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

// CurveAdvisor.cs - curbele de contrast sugerate (R-141, R-142, R-143; ADR 0032).
//
// CINCI ANCORE, masurate pe imagine, mereu din clasele tabului Tonuri (DustAnalyzer, pe
// calea benzii sau pe cea a cerului; ADR 0033): praful si cerul sunt cele ale verdictului;
// banda / nebuloasa, norii luminosi / nebuloasa stralucitoare si varfurile sunt PARTEA DE
// SUS a claselor lor - nu mediana, care la banda sta langa cer si ar porni portiunea
// abrupta chiar din cer. Punctele curbei poarta numele claselor caii.
//
// RETETA. Curba nu muta praful si cerul: ridicarea unui cer prea intunecat e re-stretch-ul
// pe care il recomanda Tonuri, nu treaba unei curbe (masurat: acelasi lucru, cu acelasi
// zgomot). Deasupra cerului, cele trei segmente iau pantele curbei vechi, micsorate
// impreuna cand varfurile ar trece de CurvePeakTarget. Blanda, Medie si Puternica sunt
// parti din drumul de la identitate la curba aceea; Maxima mareste pantele impreuna pana
// cand varfurile ating tinta, si exista doar cand Puternica nu ajunge acolo. Niciun punct
// nu coboara sub intrarea lui: cu varfurile deja la sau peste tinta nu mai e loc de nimic,
// si nu se sugereaza nicio curba (Problem), in loc de patru curbe care nu schimba nimic.
//
// DOVADA fiecarei curbe se REMASOARA pe subesantionul tonal cu curba aplicata, nu se
// prezice din puncte: pe o imagine colorata, o curba pe canale nu duce o dala exact unde
// duce griul ei.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>Pentru ce e curba. Schimba doar dovada (verificarile de hartie), niciodata curba.</summary>
    internal enum CurvePurpose { Print, Screen }

    /// <summary>Intrarile celor cinci puncte, in valori 0..1; NaN pentru o clasa goala.</summary>
    internal sealed record CurveAnchors
    {
        internal double Dust { get; init; }
        internal double Sky { get; init; }
        internal double Band { get; init; }
        internal double Clouds { get; init; }
        internal double Peaks { get; init; }

        internal double[] AsArray() => new[] { Dust, Sky, Band, Clouds, Peaks };
    }

    /// <summary>Cati pixeli ies din gamutul hartiei (R-144).</summary>
    internal sealed record GamutFigure
    {
        internal double PctAll { get; init; }

        /// <summary>Pe cele cinci clase, in ordinea hartii; NaN pentru o clasa goala.</summary>
        internal IReadOnlyList<double> PctByClass { get; init; }
    }

    internal sealed record CurveEvidence
    {
        /// <summary>Numele celor cinci clase, ale caii hartii (DustAnalyzer.ClassNames).</summary>
        internal IReadOnlyList<string> Labels { get; init; }

        /// <summary>L*-ul celor cinci clase (praf, cer, banda / nebuloasa, nori luminosi / nebuloasa stralucitoare, varfuri) inainte si dupa curba.</summary>
        internal IReadOnlyList<double> LstarBefore { get; init; }
        internal IReadOnlyList<double> LstarAfter { get; init; }

        /// <summary>Panta curbei la cer: de cate ori creste zgomotul din cer.</summary>
        internal double NoiseGain { get; init; }

        /// <summary>Zgomotul masurat (R-050) inmultit cu panta; NaN cand n-a fost destul cer neted.</summary>
        internal double NoiseAfterLsb { get; init; }

        /// <summary>Procentul pixelilor cu un canal la sau peste CurveClipLevel.</summary>
        internal double PctClippedBefore { get; init; }
        internal double PctClippedAfter { get; init; }

        /// <summary>Verificarile de hartie, doar pentru print; gol pentru ecran.</summary>
        internal IReadOnlyList<Recommendation> PaperChecks { get; init; } = Array.Empty<Recommendation>();

        /// <summary>Gamutul inainte de curba si dupa; null fara profil ICC sau pentru ecran.</summary>
        internal GamutFigure GamutBefore { get; init; }
        internal GamutFigure Gamut { get; init; }

        /// <summary>De ce lipseste cifra de gamut, pentru om; null cand exista sau cand nu se cere (ecran).</summary>
        internal string GamutNote { get; init; }
    }

    internal sealed record CurveSuggestion
    {
        internal string Name { get; init; }
        internal ToneCurve Curve { get; init; }

        /// <summary>Imaginea redusa, cu curba aplicata.</summary>
        internal Raster Preview { get; init; }
        internal CurveEvidence Evidence { get; init; }
    }

    internal static class CurveAdvisor
    {
        internal static readonly string[] Names = { "Blanda", "Medie", "Puternica" };
        internal const string MaximaName = "Maxima";

        static double Gray(double lstar) => double.IsNaN(lstar) ? double.NaN : ColorMath.GrayLevelOfLstar(lstar) / 255;

        static float[] ClassLstars(float[] patchLstars, DustMap map, PatchClass c)
        {
            var v = new List<float>();
            for (int i = 0; i < patchLstars.Length; i++) if (map.Classes[i] == c) v.Add(patchLstars[i]);
            return v.ToArray();
        }

        /// <summary>Percentila clasei, sau NaN cand clasa e goala.</summary>
        static double ClassLevel(float[] patchLstars, DustMap map, PatchClass c, double percentile)
        {
            float[] v = ClassLstars(patchLstars, map, c);
            return v.Length == 0 ? double.NaN : Stats.PercentileSorted(Stats.Sorted(v), percentile);
        }

        /// <summary>
        /// L*-ul celor cinci clase pe subesantionul tonal <paramref name="sub"/> (asa cum e sau
        /// cu o curba aplicata), cu aceleasi statistici ca ancorele: praful si cerul de la
        /// DustAnalyzer, restul din partea de sus a claselor; NaN pentru o clasa goala.
        /// </summary>
        internal static double[] ClassLevels(Raster sub, DustMap map, double paperBlackLstar)
        {
            float[] patches = DustAnalyzer.PatchLstars(sub);
            DustStats d = DustAnalyzer.Measure(patches, map, paperBlackLstar);
            return new[]
            {
                d.LstarDust,
                d.LstarSky,
                ClassLevel(patches, map, PatchClass.Band, Thresholds.CurveBandClassPercentile),
                ClassLevel(patches, map, PatchClass.BrightCloud, Thresholds.CurveCloudClassPercentile),
                ClassLevel(patches, map, PatchClass.Peak, 50),
            };
        }

        /// <summary>Ancorele: griul L*-ului fiecarei clase.</summary>
        internal static CurveAnchors Anchors(double[] classLstars) => new()
        {
            Dust = Gray(classLstars[0]),
            Sky = Gray(classLstars[1]),
            Band = Gray(classLstars[2]),
            Clouds = Gray(classLstars[3]),
            Peaks = Gray(classLstars[4]),
        };

        /// <summary>
        /// De ce nu se poate sugera nimic, pentru om: o clasa goala; varfurile deja la sau peste
        /// CurvePeakTarget (imaginea e la plafon, reteta n-ar avea loc); ancorele nu cresc strict
        /// (un cadru plat, o banda nu mai luminoasa decat cerul). Praful la negru nu e un motiv:
        /// punctul lui e chiar originea curbei, pe care curba oricum n-o muta (o margine neagra
        /// de stack face asta). Null cand se poate.
        /// </summary>
        internal static string Problem(CurveAnchors a, IReadOnlyList<string> labels)
        {
            double[] x = a.AsArray();
            for (int i = 0; i < 5; i++)
                if (double.IsNaN(x[i])) return CurveTexts.ClassEmpty(labels[i]);
            if (x[4] >= Thresholds.CurvePeakTarget) return CurveTexts.PeaksOverTarget(labels[4], x[4]);
            for (int i = 1; i < 5; i++)
                if (!(x[i] > x[i - 1])) return CurveTexts.AnchorsNotIncreasing(labels[i - 1], x[i - 1], labels[i], x[i]);
            return null;
        }

        /// <summary>
        /// Curbele sugerate pentru ancorele date, in ordinea Blanda, Medie, Puternica si,
        /// cand exista, Maxima, cu punctele etichetate cu <paramref name="labels"/>. Una al
        /// carei spline n-ar fi monoton nu se intoarce.
        /// </summary>
        internal static IReadOnlyList<(string Name, ToneCurve Curve)> Curves(CurveAnchors a, IReadOnlyList<string> labels)
        {
            double[] x = a.AsArray();
            double ys = x[1];
            double[] inc =
            {
                Thresholds.CurveSlopeSkyBand * (x[2] - x[1]),
                Thresholds.CurveSlopeBandCloud * (x[3] - x[2]),
                Thresholds.CurveSlopeCloudPeak * (x[4] - x[3]),
            };
            double reach = inc.Sum();
            double room = Thresholds.CurvePeakTarget - ys;
            // Varfurile nu trec de tinta: pantele se micsoreaza impreuna. Varfurile deja la sau
            // peste tinta nu ajung aici: Problem le-a refuzat, deci room > 0.
            double scale = Math.Min(room / reach, 1);

            double[] Full(double k)
            {
                var y = new double[5];
                y[0] = x[0]; y[1] = x[1];
                double acc = ys;
                for (int i = 0; i < 3; i++) { acc += k * inc[i]; y[i + 2] = Math.Max(x[i + 2], acc); }
                return y;
            }

            ToneCurve Build(double[] full, double strength)
            {
                // Praful la negru e originea insasi: punctul lui ia locul capatului (0, 0),
                // cu eticheta, ca tabelul si dovada sa aiba tot cinci puncte etichetate.
                var pts = new List<CurvePoint>();
                if (x[0] > 0) pts.Add(new CurvePoint(0, 0, null));
                for (int i = 0; i < 5; i++) pts.Add(new CurvePoint(x[i], x[i] + strength * (full[i] - x[i]), labels[i]));
                pts.Add(new CurvePoint(1, 1, null));
                return new ToneCurve(pts);
            }

            double[] full = Full(scale);
            var o = new List<(string, ToneCurve)>();
            for (int i = 0; i < Names.Length; i++)
            {
                ToneCurve c = Build(full, Thresholds.CurveStrengths[i]);
                if (c.IsMonotone) o.Add((Names[i], c));
            }
            if (Thresholds.CurvePeakTarget - full[4] > Thresholds.CurveMaximaMinGain)
            {
                ToneCurve c = Build(Full(room / reach), 1);
                if (c.IsMonotone) o.Add((MaximaName, c));
            }
            return o;
        }

        /// <summary>Capatul din dreapta al graficului umbrelor unei curbe sugerate: CurveShadowChartFactor x intrarea benzii (R-147).</summary>
        internal static double ShadowChartLimit(ToneCurve curve) => Thresholds.CurveShadowChartFactor * curve.Points[3].X;

        /// <summary>Procentul pixelilor cu un canal la sau peste CurveClipLevel.</summary>
        internal static double PctClipped(Raster r)
        {
            int n = r.W * r.H;
            long clipped = 0;
            float level = (float)Thresholds.CurveClipLevel;
            for (int i = 0; i < n; i++)
            {
                bool any = false;
                foreach (float[] plane in r.Planes) if (plane[i] >= level) { any = true; break; }
                if (any) clipped++;
            }
            return 100.0 * clipped / n;
        }

        /// <summary>
        /// Dovada masurata a unei curbe (R-143), fara verificarile de hartie si fara gamut, pe
        /// care le adauga rularea dupa scop.
        /// </summary>
        internal static CurveEvidence Measure(ToneCurve curve, Raster sub, Raster curvedSub, DustMap map,
            double[] before, double clippedBefore, double sky, NoiseResult noise, double paperBlackLstar)
        {
            double gain = curve.Slope(sky);
            return new CurveEvidence
            {
                Labels = map.ClassNames,
                LstarBefore = before,
                LstarAfter = ClassLevels(curvedSub, map, paperBlackLstar),
                NoiseGain = gain,
                NoiseAfterLsb = noise == null || noise.Insufficient ? double.NaN : noise.SigmaMedianLsb * gain,
                PctClippedBefore = clippedBefore,
                PctClippedAfter = PctClipped(curvedSub),
            };
        }
    }
}
