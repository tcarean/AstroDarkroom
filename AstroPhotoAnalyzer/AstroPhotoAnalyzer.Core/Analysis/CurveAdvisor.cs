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
// THE RECIPE. The curve does not move the sky, and the dust only by the dust move below: lifting
// a sky that is too dark is the re-stretch the Tonuri tab recommends, not a curve's job (measured:
// the same thing, with the same noise). Above the sky, the three segments take the old curve's slopes, scaled down
// together when the peaks would pass CurvePeakTarget. Blanda, Medie and Puternica are parts of
// the way from the identity to that curve; Maxima scales the slopes up together towards the
// target, and exists only when its peaks gain more than CurveMaximaMinGain over Puternica's. No
// point goes under its input: with the peaks already at or over the target there is no room for
// anything, and no curve is suggested (Problem), instead of four curves that change nothing.
//
// THE CAP (ADR 0039). A factor's curve must be suggestible: its spline monotone and within 0..1
// (ToneCurve). The factor is therefore capped at the largest value, up to the recipe's own,
// whose spline is; the full curve's factor needs no cap on the four reference images (its
// spline peaks at exactly 1, at the (1, 1) end), Maxima's does on three of them: the natural
// spline from peaks at 0.92 to (1, 1) overshoots 1 (1.010 on de_printat.png, 1.007 on Rosetta,
// 1.051 on the Veil, measured 2026-10-23), and the clamp used to hold it at white over 21 to
// 41 % of the input range. Capped, their peaks land at 0.892, 0.899 and 0.828.
//
// The EVIDENCE of each curve - the five class levels and the clipped share - is RE-MEASURED on the
// tonal subsample with the curve applied, not predicted from the points: on a colour image, a
// per-channel curve does not take a patch exactly where it takes its grey. The noise gain and the
// noise after are predicted: the curve's slope at the sky's anchor (for a curve with the dust
// move, the larger of it and the mean slope over the sky's wide range, MovedSkyNoiseGain), times
// the noise measured on the image as it is.
//
// THE DUST MOVE (R-184, ADR 0101). For print and only when asked, the dust's point is put so that
// the dust, measured again on the curved subsample, is the paper's black, with the sky's dark end
// held: DustMoveProblem says when it cannot be had, AimDust finds the point, Curves builds the set
// with it, DustFigures gives the evidence its figures.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// What the curve is for. It changes the evidence (the paper checks and the gamut figure),
    /// and the dust move (R-184) exists for print alone; the curves without that move are the
    /// same for both.
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

    /// <summary>
    /// The dust move of a set of suggestions (R-184, ADR 0101): the points every curve of the set
    /// gets under the sky, so that the dust lands on paper black while the sky stays.
    /// </summary>
    internal sealed record DustMove
    {
        /// <summary>The output of the dust's point, 0..1; its input is the dust's anchor.</summary>
        internal double DustOutput { get; init; }

        /// <summary>
        /// The grey of the sky's dark end, 0..1: a point that maps to itself, between the dust's
        /// and the sky's. NaN when the sky's dark end is the sky's own anchor (a sky of one
        /// level): the sky's point then holds, and none is added.
        /// </summary>
        internal double Hold { get; init; } = double.NaN;

        /// <summary>
        /// The curve has the helper point, at CurveDustHelperFraction of the dust's input and of
        /// its output: the spline is not suggestible without it.
        /// </summary>
        internal bool Helper { get; init; }

        /// <summary>
        /// The dust's point stopped before paper black, the dust ending more than
        /// CurveDustAimTolerance from it: further on no spline is suggestible.
        /// </summary>
        internal bool StoppedShort { get; init; }
    }

    /// <summary>What the dust move did, measured on the curved subsample (R-184).</summary>
    internal sealed record DustMoveFigures
    {
        /// <summary>The dust's L* before and after the curve.</summary>
        internal double LstarBefore { get; init; }
        internal double LstarAfter { get; init; }

        /// <summary>The paper's black, in L*, and the Dmax it comes from.</summary>
        internal double PaperBlackLstar { get; init; }
        internal double Dmax { get; init; }

        /// <summary>
        /// The change of the difference between the sky and the dust on paper, in percent, where
        /// on paper the dust is never darker than paper black; NaN when the difference before is
        /// not above 0.
        /// </summary>
        internal double PaperContrastChangePct { get; init; }

        /// <summary>
        /// The change of the noise in the dust, in percent: how much wider the stretch from the
        /// dust's point to the held point becomes.
        /// </summary>
        internal double DustNoiseChangePct { get; init; }

        /// <summary>The percentage of the patches under paper black before and after.</summary>
        internal double PctUnderBlackBefore { get; init; }
        internal double PctUnderBlackAfter { get; init; }

        /// <summary>The dust stopped before paper black (DustMove.StoppedShort).</summary>
        internal bool StoppedShort { get; init; }
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

        /// <summary>The dust move's figures (R-184); null on a curve without the move.</summary>
        internal DustMoveFigures Dust { get; init; }
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
        internal static string[] Names => new[] { Tr.Pick("Gentle", "Blanda"), Tr.Pick("Moderate", "Medie"), Tr.Pick("Strong", "Puternica") };
        internal static string MaximaName => Tr.Pick("Maximum", "Maxima");

        // The labels of the two points the dust move adds (R-184): no class has them.
        internal static string DarkSkyLabel => Tr.Pick("dark sky", "cer inchis");
        internal static string UnderDustLabel => Tr.Pick("under the dust", "sub praf");

        /// <summary>The grey level of an L*: the anchor of a class.</summary>
        /// <param name="lstar">The class's L*, 0..100; NaN for an empty upper class.</param>
        /// <returns>The level of a grey with that L*, 0..1; NaN for NaN.</returns>
        internal static double Gray(double lstar) => double.IsNaN(lstar) ? double.NaN : ColorMath.GrayLevelOfLstar(lstar) / 255;

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
        internal static double[] ClassLevels(Raster sub, DustMap map, double paperBlackLstar) => ClassLevels(sub, map, paperBlackLstar, out _);

        /// <summary>
        /// <see cref="ClassLevels(Raster, DustMap, double)"/>, with the dust's and the sky's whole
        /// measurement on the same patches: their ranges and the share under paper black, which
        /// the dust move reads (R-184).
        /// </summary>
        /// <param name="sub">The tonal subsample, 0..1, as it is or with a curve applied.</param>
        /// <param name="map">The map, built on the same grid.</param>
        /// <param name="paperBlackLstar">The paper's black, in L*; a patch under it counts as under paper black.</param>
        /// <param name="stats">The dust and the sky measured on the subsample's patches.</param>
        /// <returns>The five L*s, as the other overload gives them.</returns>
        internal static double[] ClassLevels(Raster sub, DustMap map, double paperBlackLstar, out DustStats stats)
        {
            float[] patches = DustAnalyzer.PatchLstars(sub);
            DustStats d = stats = DustAnalyzer.Measure(patches, map, paperBlackLstar);
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
        /// The reason, in the interface language, from CurveTexts; null when curves can be suggested.
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
        /// when it exists, Maxima, with the points labelled with <paramref name="labels"/>. The
        /// slope factor of the full curve, and Maxima's, is capped at the largest value up to the
        /// recipe's own whose spline is monotone and stays within 0..1 (ADR 0039); a curve that
        /// still is not (<see cref="ToneCurve.IsSuggestible"/>) is not returned. Maxima exists only
        /// when its peaks gain more than CurveMaximaMinGain over Puternica's.
        /// </summary>
        /// <param name="a">
        /// The anchors, as <see cref="Problem"/> accepts them: none NaN, strictly increasing, the
        /// peaks under CurvePeakTarget.
        /// </param>
        /// <param name="labels">The five class names of the map's path, one per anchor.</param>
        /// <param name="move">
        /// The dust move every curve of the set gets (R-184), from <see cref="AimDust"/>; null
        /// for none, and then the dust's point maps to itself.
        /// </param>
        /// <returns>
        /// The name and the curve of each suggestion kept; empty when none is suggestible.
        /// </returns>
        internal static IReadOnlyList<(string Name, ToneCurve Curve)> Curves(CurveAnchors a, IReadOnlyList<string> labels, DustMove move = null)
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

            List<CurvePoint> PointsOf(double[] full, double strength) => Points(x, full, strength, labels, move);

            ToneCurve Build(double[] full, double strength) => new ToneCurve(PointsOf(full, strength));

            // The largest factor up to kMax whose full curve is suggestible, by bisection from the
            // identity (factor 0, always suggestible) to kMax: 40 halvings put it within 1e-12.
            // Each probe judges the spline without building a table (ToneCurve.WouldBeSuggestible).
            double Cap(double kMax)
            {
                if (ToneCurve.WouldBeSuggestible(PointsOf(Full(kMax), 1))) return kMax;
                double lo = 0, hi = kMax;
                for (int i = 0; i < 40; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (ToneCurve.WouldBeSuggestible(PointsOf(Full(mid), 1))) lo = mid; else hi = mid;
                }
                return lo;
            }

            double[] full = Full(Cap(scale));
            var o = new List<(string, ToneCurve)>();
            for (int i = 0; i < Names.Length; i++)
            {
                ToneCurve c = Build(full, Thresholds.CurveStrengths[i]);
                if (c.IsSuggestible) o.Add((Names[i], c));
            }
            double[] maxima = Full(Cap(room / reach));
            if (maxima[4] - full[4] > Thresholds.CurveMaximaMinGain)
            {
                ToneCurve c = Build(maxima, 1);
                if (c.IsSuggestible) o.Add((MaximaName, c));
            }
            return o;
        }

        /// <summary>
        /// The control points of one curve of a set: the (0, 0) end, the five anchors with their
        /// outputs, the (1, 1) end, and with a dust move its points under the sky.
        /// </summary>
        /// <param name="x">The five anchors, as CurveAnchors.AsArray gives them.</param>
        /// <param name="full">The five outputs of the full curve, one per anchor.</param>
        /// <param name="strength">
        /// How much of the way from the identity to the full curve, 0..1; the dust's point of a
        /// move does not follow it.
        /// </param>
        /// <param name="labels">The five class names, one per anchor.</param>
        /// <param name="move">The dust move, or null; with one the dust's anchor is above 0.</param>
        /// <returns>The points, in increasing input.</returns>
        static List<CurvePoint> Points(double[] x, double[] full, double strength, IReadOnlyList<string> labels, DustMove move)
        {
            // The dust at black is the origin itself: its point takes the place of the (0, 0)
            // end, with its label, so that the table and the evidence still have five labelled
            // points.
            var pts = new List<CurvePoint>();
            if (x[0] > 0) pts.Add(new CurvePoint(0, 0, null));
            if (move != null && move.Helper)
                pts.Add(new CurvePoint(x[0] * Thresholds.CurveDustHelperFraction, move.DustOutput * Thresholds.CurveDustHelperFraction, UnderDustLabel, true));
            for (int i = 0; i < 5; i++)
            {
                // The move is the same at every strength: only the points above the sky follow it.
                pts.Add(new CurvePoint(x[i], i == 0 && move != null ? move.DustOutput : x[i] + strength * (full[i] - x[i]), labels[i]));
                if (i == 0 && move != null && !double.IsNaN(move.Hold)) pts.Add(new CurvePoint(move.Hold, move.Hold, DarkSkyLabel, true));
            }
            pts.Add(new CurvePoint(1, 1, null));
            return pts;
        }

        /// <summary>
        /// Why the dust move cannot be had (R-184), for the user: the screen has no paper black;
        /// the dust's figure is the darkest sky, not a dust zone; the dust is the origin; the
        /// sky's dark end is not above the dust, or the paper's black is not under it; the dust
        /// is already within CurveDustAimTolerance of the paper's black. Null when it can.
        /// </summary>
        /// <param name="purpose">What the curves are for.</param>
        /// <param name="noDustZone">
        /// The sentence for an image whose dust's L* comes from the darkest sky
        /// (CurveTexts.DustMoveNoDustZone); null when the dust zone gives it.
        /// </param>
        /// <param name="a">The anchors.</param>
        /// <param name="hold">The grey of the sky's dark end, 0..1.</param>
        /// <param name="dustLstar">The dust's L* on the image as it is.</param>
        /// <param name="paperBlackLstar">The paper's black, in L*.</param>
        /// <returns>The reason, in the interface language, from CurveTexts; null when the move can be had.</returns>
        internal static string DustMoveProblem(CurvePurpose purpose, string noDustZone, CurveAnchors a, double hold, double dustLstar, double paperBlackLstar)
        {
            if (purpose != CurvePurpose.Print) return CurveTexts.DustMoveOnlyForPrint;
            if (noDustZone != null) return noDustZone;
            if (!(a.Dust > 0)) return CurveTexts.DustMoveDustAtBlack;
            if (!(hold > a.Dust)) return CurveTexts.DustMoveDustNotUnderSky;
            if (!(Gray(paperBlackLstar) < hold)) return CurveTexts.DustMoveNoRoom(ColorMath.LstarOfGrayLevel(hold * 255), paperBlackLstar);
            if (Math.Abs(dustLstar - paperBlackLstar) <= Thresholds.CurveDustAimTolerance) return CurveTexts.DustMoveAlreadyThere(paperBlackLstar);
            return null;
        }

        // The aim of the dust move. A held point this close under the sky's anchor is the sky's
        // own (a sky of one level); the bisection's steps put the dust's output within 2e-6 of
        // the scale; a lifted dust stops this far under the point above it, so the inputs and
        // the outputs still increase.
        const double HoldGap = 1e-6;
        const int AimSteps = 16;
        const double AimMargin = 1e-4;

        /// <summary>
        /// Aims the dust move (R-184): the output of the dust's point at which the dust, measured
        /// again on the curved image, is the paper's black, with the sky's dark end held.
        /// </summary>
        /// <param name="a">The anchors, as <see cref="Problem"/> accepts them, the dust above 0.</param>
        /// <param name="labels">The five class names of the map's path, one per anchor.</param>
        /// <param name="hold">
        /// The grey of the sky's dark end, 0..1, above the dust's anchor and above the grey of
        /// paper black, at or under the sky's anchor.
        /// </param>
        /// <param name="dustLstar">The dust's L* on the image as it is.</param>
        /// <param name="paperBlackLstar">The paper's black, in L*.</param>
        /// <param name="dustLstarAfter">
        /// The dust's L* on the image with a curve applied: the measurement each probe makes.
        /// </param>
        /// <returns>The move; null when no part of it leaves a suggestible spline.</returns>
        internal static DustMove AimDust(CurveAnchors a, IReadOnlyList<string> labels, double hold, double dustLstar, double paperBlackLstar, Func<ToneCurve, double> dustLstarAfter)
        {
            // The aim is made on the strongest curve of the plain recipe; the other strengths
            // differ above the sky only, where the dust hardly feels them.
            IReadOnlyList<(string Name, ToneCurve Curve)> plain = Curves(a, labels);
            if (plain.Count == 0) return null;
            ToneCurve strongest = plain.Where(c => c.Name != MaximaName).DefaultIfEmpty(plain[^1]).Last().Curve;
            double[] x = a.AsArray();
            double[] full = strongest.Points.Where(p => p.Label != null).Select(p => p.Y).ToArray();

            double held = a.Sky - hold > HoldGap ? hold : double.NaN;
            double above = double.IsNaN(held) ? a.Sky : held;
            bool lower = dustLstar > paperBlackLstar;

            DustMove Move(double output, bool helper) => new() { DustOutput = output, Hold = held, Helper = helper };
            // Measured on the gentlest curve, where the dust lands lowest (on the reference FITS
            // 0.01 to 0.03 L* under the strongest's, 2026-11-16): aimed there, no strength leaves
            // the dust under paper black.
            double gentlest = Thresholds.CurveStrengths.Min();
            double Measure(double output, bool helper) => dustLstarAfter(new ToneCurve(Points(x, full, gentlest, labels, Move(output, helper))));
            // A move is good when the curve of every strength is suggestible with it.
            bool Valid(double output, bool helper) => Thresholds.CurveStrengths.All(s => ToneCurve.WouldBeSuggestible(Points(x, full, s, labels, Move(output, helper))));

            // The output at which the measured dust is paper black: the dust rises with its
            // point's output, so a bisection finds it; the end kept is the one at or above black.
            (double Output, bool Reached) Aim(bool helper)
            {
                double lo = lower ? 0 : x[0], hi = lower ? x[0] : above - AimMargin;
                if (lower && Measure(lo, helper) >= paperBlackLstar) return (lo, false);
                if (!lower && Measure(hi, helper) <= paperBlackLstar) return (hi, false);
                for (int i = 0; i < AimSteps; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (Measure(mid, helper) >= paperBlackLstar) hi = mid; else lo = mid;
                }
                return (hi, true);
            }

            // The largest part of the way to the aimed output whose curves are suggestible; NaN
            // when not even the dust left in place is.
            double Cap(double aimed, bool helper)
            {
                if (!Valid(x[0], helper)) return double.NaN;
                double lo = 0, hi = 1;
                for (int i = 0; i < 40; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (Valid(x[0] + mid * (aimed - x[0]), helper)) lo = mid; else hi = mid;
                }
                return x[0] + lo * (aimed - x[0]);
            }

            // First without the helper point, then with it (R-184).
            var (plainOutput, plainReached) = Aim(false);
            if (Valid(plainOutput, false)) return Move(plainOutput, false) with { StoppedShort = !plainReached };
            var (helpedOutput, helpedReached) = Aim(true);
            if (Valid(helpedOutput, true)) return Move(helpedOutput, true) with { StoppedShort = !helpedReached };

            // Neither goes all the way: of the two capped moves, the one that leaves the dust
            // nearer paper black; the plain one when they tie, for its shorter table.
            DustMove best = null;
            double bestMiss = double.MaxValue;
            foreach (bool helper in new[] { false, true })
            {
                double output = Cap(helper ? helpedOutput : plainOutput, helper);
                if (double.IsNaN(output)) continue;
                double after = Measure(output, helper), miss = Math.Abs(after - paperBlackLstar);
                // A move that leaves the dust where it was is no move.
                if (Math.Abs(after - dustLstar) <= Thresholds.CurveDustAimTolerance) continue;
                // Cut by a hair, the dust is on paper black all the same: it stopped short only
                // when it ends further from it than the tolerance.
                if (miss < bestMiss) { bestMiss = miss; best = Move(output, helper) with { StoppedShort = miss > Thresholds.CurveDustAimTolerance }; }
            }
            return best;
        }

        /// <summary>
        /// How many times the sky's noise grows under a curve with the dust move: the larger of
        /// the slope at the sky's anchor and the mean slope over the sky's wide range. A lowered
        /// dust bends the curve right at the sky's anchor, where the slope alone then understates
        /// the sky: on the reference FITS at Dmax 2.4 it reads 0.76 to 0.82 where the sky's 35
        /// noise tiles measure 0.95 to 1.04 and the range's mean slope 0.96 to 1.01; on a lifted
        /// dust and on a curve without the move the slope at the anchor is the closer of the two
        /// and the larger (measured 2026-11-16, ADR 0101).
        /// </summary>
        /// <param name="curve">The curve.</param>
        /// <param name="sky">The sky's anchor, 0..1.</param>
        /// <param name="skyWide">The sky's wide range on the image as it is, in L*.</param>
        /// <returns>The gain; the slope at the anchor when the range has no width.</returns>
        internal static double MovedSkyNoiseGain(ToneCurve curve, double sky, LstarRange skyWide)
        {
            double lo = Gray(skyWide.Low), hi = Gray(skyWide.High), slope = curve.Slope(sky);
            return hi > lo ? Math.Max(slope, (curve.Map(hi) - curve.Map(lo)) / (hi - lo)) : slope;
        }

        /// <summary>What a dust move did (R-184), from the dust and the sky measured before and after.</summary>
        /// <param name="move">The move.</param>
        /// <param name="a">The anchors the move was aimed with.</param>
        /// <param name="before">The dust and the sky on the image as it is.</param>
        /// <param name="after">The same on the curved subsample.</param>
        /// <param name="paperBlackLstar">The paper's black, in L*.</param>
        /// <param name="dmax">The Dmax that black comes from.</param>
        /// <returns>The figures the evidence shows.</returns>
        internal static DustMoveFigures DustFigures(DustMove move, CurveAnchors a, DustStats before, DustStats after, double paperBlackLstar, double dmax)
        {
            // On paper nothing prints darker than the paper's black.
            double apartBefore = before.LstarSky - Math.Max(before.LstarDust, paperBlackLstar);
            double apartAfter = after.LstarSky - Math.Max(after.LstarDust, paperBlackLstar);
            double above = double.IsNaN(move.Hold) ? a.Sky : move.Hold;
            return new DustMoveFigures
            {
                LstarBefore = before.LstarDust,
                LstarAfter = after.LstarDust,
                PaperBlackLstar = paperBlackLstar,
                Dmax = dmax,
                PaperContrastChangePct = apartBefore > 0 ? 100 * (apartAfter / apartBefore - 1) : double.NaN,
                DustNoiseChangePct = 100 * ((above - move.DustOutput) / (above - a.Dust) - 1),
                PctUnderBlackBefore = before.PctPatchesBelowPaperBlack,
                PctUnderBlackAfter = after.PctPatchesBelowPaperBlack,
                StoppedShort = move.StoppedShort,
            };
        }

        /// <summary>
        /// The right end of the shadow chart of a suggested curve: CurveShadowChartFactor x the
        /// input of the curve's fourth point that the dust move did not add, which is the band's
        /// (R-147) - except when the dust's point is the origin (Curves), where the fourth is the
        /// bright clouds'.
        /// </summary>
        /// <param name="curve">A curve <see cref="Curves"/> suggested.</param>
        /// <returns>The chart's right end, on the input axis, 0..1 or a little over.</returns>
        internal static double ShadowChartLimit(ToneCurve curve) => Thresholds.CurveShadowChartFactor * curve.Points.Where(p => !p.Extra).ElementAt(3).X;

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
        /// <param name="movedSkyRange">
        /// For a curve with the dust move (R-184), the sky's wide range on the image as it is; the
        /// noise gain is then <see cref="MovedSkyNoiseGain"/>. Null for a curve without the move.
        /// </param>
        /// <returns>The evidence, with no paper check and no gamut.</returns>
        internal static CurveEvidence Measure(ToneCurve curve, Raster sub, Raster curvedSub, DustMap map,
            double[] before, double clippedBefore, double sky, NoiseResult noise, double paperBlackLstar, LstarRange? movedSkyRange = null)
        {
            double gain = movedSkyRange is LstarRange range ? MovedSkyNoiseGain(curve, sky, range) : curve.Slope(sky);
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
