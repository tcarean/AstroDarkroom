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

// CurveAdvisor.cs - the suggested contrast curves (R-141, R-142, R-143; ADR 0032).
//
// FIVE ANCHORS, measured on the image, always from the classes of the Tonuri tab (DustAnalyzer,
// on the band path or on the sky path; ADR 0033): the dust and the sky are the verdict's; the
// band / nebula and the bright clouds / bright nebula are the UPPER PART of their classes - not
// the median, which for the band sits beside the sky and would start the steep stretch right at
// the sky - and the peaks the median of theirs. The curve's points carry the names of the path's
// classes.
//
// THE RECIPE. The curve does not move the dust and the sky: lifting a sky that is too dark is the
// re-stretch the Tonuri tab recommends, not a curve's job (measured: the same thing, with the
// same noise). Above the sky, the three segments take the old curve's slopes, scaled down
// together when the peaks would pass CurvePeakTarget. Blanda, Medie and Puternica are parts of
// the way from the identity to that curve; Maxima scales the slopes up together until the peaks
// reach the target, and exists only when Puternica does not get there. No point goes under its
// input: with the peaks already at or over the target there is no room for anything, and no
// curve is suggested (Problem), instead of four curves that change nothing.
//
// The EVIDENCE of each curve - the five class levels and the clipped share - is RE-MEASURED on the
// tonal subsample with the curve applied, not predicted from the points: on a colour image, a
// per-channel curve does not take a patch exactly where it takes its grey. The noise gain and the
// noise after are predicted: the curve's slope at the sky's anchor, times the noise measured on
// the image as it is.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// What the curve is for. It changes only the evidence (the paper checks and the gamut
    /// figure), never the curve.
    /// </summary>
    internal enum CurvePurpose { Print, Screen }

    /// <summary>
    /// The inputs of the five points, in values 0..1; NaN for an empty band, bright or peak class
    /// (the dust and the sky are never NaN: an empty dust class gives the darkest sky, an empty
    /// sky class all the patches).
    /// </summary>
    internal sealed record CurveAnchors
    {
        internal double Dust { get; init; }
        internal double Sky { get; init; }
        internal double Band { get; init; }
        internal double Clouds { get; init; }
        internal double Peaks { get; init; }

        /// <summary>The five inputs, in the order of the curve's points.</summary>
        /// <returns>A new array: the dust, the sky, the band, the clouds, the peaks.</returns>
        internal double[] AsArray() => new[] { Dust, Sky, Band, Clouds, Peaks };
    }

    /// <summary>How many pixels leave the paper's gamut (R-144).</summary>
    internal sealed record GamutFigure
    {
        internal double PctAll { get; init; }

        /// <summary>
        /// Over the five classes, in the map's order; on colour NaN for a class no pixel falls in,
        /// on mono 0 for every class.
        /// </summary>
        internal IReadOnlyList<double> PctByClass { get; init; }
    }

    /// <summary>
    /// The measured evidence of a suggested curve (R-143): the five class levels before and after,
    /// the noise gain and the noise after, the clipped shares, and for print the paper checks and
    /// the gamut.
    /// </summary>
    internal sealed record CurveEvidence
    {
        /// <summary>
        /// The names of the five classes, of the map's path (DustAnalyzer.ClassNames).
        /// </summary>
        internal IReadOnlyList<string> Labels { get; init; }

        /// <summary>
        /// The L* of the five classes (dust, sky, band / nebula, bright clouds / bright nebula,
        /// peaks) before and after the curve.
        /// </summary>
        internal IReadOnlyList<double> LstarBefore { get; init; }
        internal IReadOnlyList<double> LstarAfter { get; init; }

        /// <summary>The curve's slope at the sky: how many times the sky's noise grows.</summary>
        internal double NoiseGain { get; init; }

        /// <summary>
        /// The measured noise (R-050) times the slope; NaN when there was not enough smooth sky.
        /// </summary>
        internal double NoiseAfterLsb { get; init; }

        /// <summary>
        /// The percentage of the pixels with a channel at or over CurveClipLevel.
        /// </summary>
        internal double PctClippedBefore { get; init; }
        internal double PctClippedAfter { get; init; }

        /// <summary>The paper checks, only for print; empty for the screen.</summary>
        internal IReadOnlyList<Recommendation> PaperChecks { get; init; } = Array.Empty<Recommendation>();

        /// <summary>
        /// The gamut before the curve and after; null without an ICC profile, with one that cannot
        /// be used, or for the screen.
        /// </summary>
        internal GamutFigure GamutBefore { get; init; }
        internal GamutFigure Gamut { get; init; }

        /// <summary>
        /// Why the gamut figure is missing, for the user; null when it exists or when it is not
        /// asked for (screen).
        /// </summary>
        internal string GamutNote { get; init; }
    }

    /// <summary>A suggested curve: its name, the curve, its preview and its evidence.</summary>
    internal sealed record CurveSuggestion
    {
        internal string Name { get; init; }
        internal ToneCurve Curve { get; init; }

        /// <summary>The reduced image, with the curve applied.</summary>
        internal Raster Preview { get; init; }
        internal CurveEvidence Evidence { get; init; }
    }

    /// <summary>
    /// Suggests contrast curves anchored on the classes (ADR 0032) and measures each one's
    /// evidence.
    /// </summary>
    internal static class CurveAdvisor
    {
        internal static readonly string[] Names = { "Blanda", "Medie", "Puternica" };
        internal const string MaximaName = "Maxima";

        /// <summary>The grey level of an L*: the anchor of a class.</summary>
        /// <param name="lstar">The class's L*, 0..100; NaN for an empty upper class.</param>
        /// <returns>The level of a grey with that L*, 0..1; NaN for NaN.</returns>
        static double Gray(double lstar) => double.IsNaN(lstar) ? double.NaN : ColorMath.GrayLevelOfLstar(lstar) / 255;

        /// <summary>The L*s of one class's patches.</summary>
        /// <param name="patchLstars">The L* of each patch, in the map's row order.</param>
        /// <param name="map">The map, for its classes.</param>
        /// <param name="c">The class.</param>
        /// <returns>The class's L*s, in the map's row order; empty for an empty class.</returns>
        static float[] ClassLstars(float[] patchLstars, DustMap map, PatchClass c)
        {
            var v = new List<float>();
            for (int i = 0; i < patchLstars.Length; i++) if (map.Classes[i] == c) v.Add(patchLstars[i]);
            return v.ToArray();
        }

        /// <summary>The class's percentile, or NaN when the class is empty.</summary>
        /// <param name="patchLstars">The L* of each patch, in the map's row order.</param>
        /// <param name="map">The map, for its classes.</param>
        /// <param name="c">The class.</param>
        /// <param name="percentile">The percentile, 0..100.</param>
        /// <returns>The percentile, in L*; NaN for an empty class.</returns>
        static double ClassLevel(float[] patchLstars, DustMap map, PatchClass c, double percentile)
        {
            float[] v = ClassLstars(patchLstars, map, c);
            return v.Length == 0 ? double.NaN : Stats.PercentileSorted(Stats.Sorted(v), percentile);
        }

        /// <summary>
        /// The L* of the five classes on the tonal subsample <paramref name="sub"/> (as it is or
        /// with a curve applied), with the same statistics as the anchors: the dust and the sky
        /// from DustAnalyzer, the band / nebula and the bright clouds / bright nebula from the
        /// upper part of their classes, the peaks from the median of theirs; NaN for an empty one
        /// of these three (an empty dust class gives the darkest sky, an empty sky class all the
        /// patches).
        /// </summary>
        /// <param name="sub">The tonal subsample, 0..1, as it is or with a curve applied.</param>
        /// <param name="map">The map, built on the same grid.</param>
        /// <param name="paperBlackLstar">
        /// The paper's black, in L*, passed to DustAnalyzer.Measure; no level depends on it.
        /// </param>
        /// <returns>
        /// Five L*s: the dust, the sky, the band / nebula, the bright clouds / bright nebula, the
        /// peaks.
        /// </returns>
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

        /// <summary>The anchors: the grey of each class's L*.</summary>
        /// <param name="classLstars">The five class L*s, as ClassLevels gives them.</param>
        /// <returns>The anchors, 0..1; NaN where the L* is NaN.</returns>
        internal static CurveAnchors Anchors(double[] classLstars) => new()
        {
            Dust = Gray(classLstars[0]),
            Sky = Gray(classLstars[1]),
            Band = Gray(classLstars[2]),
            Clouds = Gray(classLstars[3]),
            Peaks = Gray(classLstars[4]),
        };

        /// <summary>
        /// Why nothing can be suggested, for the user: an empty upper class; the peaks already at
        /// or over CurvePeakTarget (the image is at the ceiling, the recipe would have no room);
        /// the anchors do not increase strictly (a flat frame, a band no brighter than the sky).
        /// The dust at black is not a reason: its point is the curve's origin itself, which the
        /// curve does not move anyway (a black stacking border does that). Null when it can.
        /// </summary>
        /// <param name="a">The anchors.</param>
        /// <param name="labels">The five class names of the map's path, for the sentence.</param>
        /// <returns>
        /// The reason, in Romanian, from CurveTexts; null when curves can be suggested.
        /// </returns>
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
        /// The suggested curves for the given anchors, in the order Blanda, Medie, Puternica and,
        /// when it exists, Maxima, with the points labelled with <paramref name="labels"/>. One
        /// whose clamped table would not be monotone is not returned. A spline that rises over 1
        /// and comes back is held at 1 by the clamp and kept: so is every Maxima of V-009, V-011
        /// and V-014, which maps the top fifth to two fifths of the input range to white.
        /// </summary>
        /// <param name="a">
        /// The anchors, as <see cref="Problem"/> accepts them: none NaN, strictly increasing, the
        /// peaks under CurvePeakTarget.
        /// </param>
        /// <param name="labels">The five class names of the map's path, one per anchor.</param>
        /// <returns>
        /// The name and the curve of each suggestion kept; empty when none is monotone.
        /// </returns>
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
            // The peaks do not pass the target: the slopes shrink together. Peaks already at or
            // over the target do not get here: Problem refused them, so room > 0.
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
                // The dust at black is the origin itself: its point takes the place of the (0, 0)
                // end, with its label, so that the table and the evidence still have five labelled
                // points.
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

        /// <summary>
        /// The right end of the shadow chart of a suggested curve: CurveShadowChartFactor x the
        /// input of the curve's fourth point, which is the band's (R-147) - except when the dust's
        /// point is the origin (Curves), where the fourth is the bright clouds'.
        /// </summary>
        /// <param name="curve">A curve <see cref="Curves"/> suggested.</param>
        /// <returns>The chart's right end, on the input axis, 0..1 or a little over.</returns>
        internal static double ShadowChartLimit(ToneCurve curve) => Thresholds.CurveShadowChartFactor * curve.Points[3].X;

        /// <summary>
        /// The percentage of the pixels with a channel at or over CurveClipLevel.
        /// </summary>
        /// <param name="r">The raster, 0..1.</param>
        /// <returns>The percentage, 0..100.</returns>
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
        /// The measured evidence of a curve (R-143), without the paper checks and without the
        /// gamut, which the run adds by purpose.
        /// </summary>
        /// <param name="curve">The curve; its slope at the sky is the noise gain.</param>
        /// <param name="sub">The tonal subsample as it is; not read.</param>
        /// <param name="curvedSub">The same subsample with the curve applied.</param>
        /// <param name="map">The map, for the classes and their names.</param>
        /// <param name="before">
        /// The five class L*s before the curve, as ClassLevels gave them; kept, not copied.
        /// </param>
        /// <param name="clippedBefore">
        /// The clipped percentage before the curve, from PctClipped.
        /// </param>
        /// <param name="sky">The sky's anchor, 0..1: where the slope is taken.</param>
        /// <param name="noise">
        /// The noise measurement; null, or one with too few smooth tiles, gives a NaN noise
        /// after.
        /// </param>
        /// <param name="paperBlackLstar">The paper's black, in L*, for ClassLevels.</param>
        /// <returns>The evidence, with no paper check and no gamut.</returns>
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
