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
// CINCI ANCORE, masurate pe imagine: praful si cerul sunt cele ale tabului Tonuri
// (DustAnalyzer); banda, norii luminosi si varfurile sunt PARTEA DE SUS a claselor lor -
// nu mediana, care la banda sta langa cer si ar porni portiunea abrupta chiar din cer.
// Fara clase (fara WCS, fara banda, fara praf) cele trei de sus sunt percentile ale
// histogramei cadrului, acolo unde stateau intrarile curbei vechi a proprietarului.
//
// RETETA. Curba nu muta praful si cerul: ridicarea unui cer prea intunecat e re-stretch-ul
// pe care il recomanda Tonuri, nu treaba unei curbe (masurat: acelasi lucru, cu acelasi
// zgomot). Deasupra cerului, cele trei segmente iau pantele curbei vechi, micsorate
// impreuna cand varfurile ar trece de CurvePeakTarget. Blanda, Medie si Puternica sunt
// parti din drumul de la identitate la curba aceea; Maxima mareste pantele impreuna pana
// cand varfurile ating tinta, si exista doar cand Puternica nu ajunge acolo.
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

    /// <summary>De unde vin ancorele de sus.</summary>
    internal enum CurveAnchorSource
    {
        /// <summary>Clasele tabului Tonuri (ADR 0026).</summary>
        Classes,

        /// <summary>Percentilele histogramei cadrului: fara WCS, fara banda sau fara vreuna din clase.</summary>
        FrameHistogram,
    }

    /// <summary>Intrarile celor cinci puncte, in valori 0..1.</summary>
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

        /// <summary>Pe clasele praf, cer, banda, nori luminosi, varfuri; null fara clase, NaN pentru o clasa goala.</summary>
        internal IReadOnlyList<double> PctByClass { get; init; }
    }

    internal sealed record CurveEvidence
    {
        /// <summary>L*-ul celor cinci clase (praf, cer, banda, nori luminosi, varfuri) inainte si dupa curba.</summary>
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
        internal static readonly string[] Labels = { "praf", "cer", "banda", "nori luminosi", "varfuri" };
        internal static readonly string[] Names = { "Blanda", "Medie", "Puternica" };
        internal const string MaximaName = "Maxima";

        static double Gray(double lstar) => ColorMath.GrayLevelOfLstar(lstar) / 255;

        /// <summary>Griul luminantei fiecarui pixel al subesantionului, sortat.</summary>
        static float[] SortedLuminanceGray(Raster sub)
        {
            int n = sub.W * sub.H;
            var g = new float[n];
            bool color = sub.IsColor;
            float[] R = sub.Planes[0], G = color ? sub.Planes[1] : null, B = color ? sub.Planes[2] : null;
            for (int i = 0; i < n; i++)
                g[i] = color
                    ? (float)ColorMath.LinearToSrgb(ColorMath.Luminance(ColorMath.SrgbToLinear(R[i]), ColorMath.SrgbToLinear(G[i]), ColorMath.SrgbToLinear(B[i])))
                    : R[i];
            Array.Sort(g);
            return g;
        }

        static float[] ClassLstars(float[] patchLstars, DustMap map, PatchClass c)
        {
            var v = new List<float>();
            for (int i = 0; i < patchLstars.Length; i++) if (map.Classes[i] == c) v.Add(patchLstars[i]);
            return v.ToArray();
        }

        /// <summary>Ancorele de sus vin din clase doar cand praful si cerul vin din ele si niciuna din cele trei clase nu e goala.</summary>
        internal static CurveAnchorSource SourceOf(DustMap map)
            => map.UsesClasses
               && Array.IndexOf(map.Classes, PatchClass.Band) >= 0
               && Array.IndexOf(map.Classes, PatchClass.BrightCloud) >= 0
               && Array.IndexOf(map.Classes, PatchClass.Peak) >= 0
                ? CurveAnchorSource.Classes : CurveAnchorSource.FrameHistogram;

        /// <summary>
        /// L*-ul celor cinci clase pe subesantionul tonal <paramref name="sub"/> (asa cum e sau
        /// cu o curba aplicata), cu aceleasi statistici ca ancorele: praful si cerul de la
        /// DustAnalyzer, restul din partea de sus a claselor sau din histograma cadrului.
        /// </summary>
        internal static double[] ClassLevels(Raster sub, DustMap map, CurveAnchorSource source, double paperBlackLstar)
        {
            float[] patches = DustAnalyzer.PatchLstars(sub);
            DustStats d = DustAnalyzer.Measure(patches, map, paperBlackLstar);
            var o = new double[5];
            o[0] = d.LstarDust;
            o[1] = d.LstarSky;
            if (source == CurveAnchorSource.Classes)
            {
                o[2] = Stats.PercentileSorted(Stats.Sorted(ClassLstars(patches, map, PatchClass.Band)), Thresholds.CurveBandClassPercentile);
                o[3] = Stats.PercentileSorted(Stats.Sorted(ClassLstars(patches, map, PatchClass.BrightCloud)), Thresholds.CurveCloudClassPercentile);
                o[4] = Stats.Median(ClassLstars(patches, map, PatchClass.Peak));
            }
            else
            {
                float[] g = SortedLuminanceGray(sub);
                o[2] = ColorMath.LstarOfY(ColorMath.SrgbToLinear(Stats.PercentileSorted(g, Thresholds.CurveBandPercentile)));
                o[3] = ColorMath.LstarOfY(ColorMath.SrgbToLinear(Stats.PercentileSorted(g, Thresholds.CurveCloudPercentile)));
                o[4] = ColorMath.LstarOfY(ColorMath.SrgbToLinear(Stats.PercentileSorted(g, Thresholds.CurvePeakPercentile)));
            }
            return o;
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
        /// De ce nu se poate sugera nimic, pentru om: ancorele nu cresc strict intre 0 si 1
        /// (un cadru plat, varfuri arse, o banda nu mai luminoasa decat cerul). Null cand cresc.
        /// </summary>
        internal static string Problem(CurveAnchors a)
        {
            double[] x = a.AsArray();
            if (!(x[0] > 0)) return CurveTexts.AnchorAtEnd(Labels[0], x[0], atBlack: true);
            for (int i = 1; i < 5; i++)
                if (!(x[i] > x[i - 1])) return CurveTexts.AnchorsNotIncreasing(Labels[i - 1], x[i - 1], Labels[i], x[i]);
            if (!(x[4] < 1)) return CurveTexts.AnchorAtEnd(Labels[4], x[4], atBlack: false);
            return null;
        }

        /// <summary>
        /// Curbele sugerate pentru ancorele date, in ordinea Blanda, Medie, Puternica si,
        /// cand exista, Maxima. Una al carei spline n-ar fi monoton nu se intoarce.
        /// </summary>
        internal static IReadOnlyList<(string Name, ToneCurve Curve)> Curves(CurveAnchors a)
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
            // Varfurile nu trec de tinta: pantele se micsoreaza impreuna. Cu varfurile deja
            // peste tinta nu mai ramane contrast de dat, iar curba ramane identitatea.
            double scale = Math.Clamp(room / reach, 0, 1);

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
                var pts = new List<CurvePoint> { new(0, 0, null) };
                for (int i = 0; i < 5; i++) pts.Add(new CurvePoint(x[i], x[i] + strength * (full[i] - x[i]), Labels[i]));
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
        internal static CurveEvidence Measure(ToneCurve curve, Raster sub, Raster curvedSub, DustMap map, CurveAnchorSource source,
            double[] before, double clippedBefore, double sky, NoiseResult noise, double paperBlackLstar)
        {
            double gain = curve.Slope(sky);
            return new CurveEvidence
            {
                LstarBefore = before,
                LstarAfter = ClassLevels(curvedSub, map, source, paperBlackLstar),
                NoiseGain = gain,
                NoiseAfterLsb = noise == null || noise.Insufficient ? double.NaN : noise.SigmaMedianLsb * gain,
                PctClippedBefore = clippedBefore,
                PctClippedAfter = PctClipped(curvedSub),
            };
        }
    }
}
