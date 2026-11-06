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

// GamutFit.cs - the paper fit (R-175, R-176, ADR 0082): the colours the paper cannot hold
// (the rule of ADR 0081) have their chroma reduced along their own hue, at their own L*, to the
// largest chroma the paper holds there; tone and hue never move. The largest chroma comes from
// a table over L* and hue (GamutFitTable), built once per profile by the exact rule at every
// node, with the exact rule deciding again for a pixel near the table's edge (the hybrid of
// ADR 0042). Three variants differ by their knee: a hard fit onto the edge, or a smooth
// compression from a share of the edge, so that a gradient of growing saturation keeps its
// steps. The recipe (GamutFit.Recipe) is what a person would do by hand instead: one tone band
// and one saturation factor.
//
// Why a table over L* and hue and not over sRGB: the largest chroma is a smooth surface over
// L* and hue, read bilinearly within 0.025 of the exact search on the measured profiles; the
// factor over sRGB jumps from 1 to under 1 at the edge, which a grid rounds inward (ADR 0042).
// Why the rule is injected: the tests know the paper they describe; the real rule is the
// compensated round trip through the profile (GamutAnalyzer), measured on the reference image
// (V-016).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The largest chroma the paper holds, over L* and hue: a table built by the exact rule at
    /// every node and read bilinearly, the rule kept for the pixels near the edge.
    /// </summary>
    internal sealed class GamutFitTable
    {
        /// <summary>The nodes over L* (0..100) and over the hue (0..360 degrees).</summary>
        internal int LNodes, HNodes;

        /// <summary>
        /// The largest chroma at each node, row by L* node then hue node; <see cref="Unbounded"/>
        /// where the search's ceiling still fits; 0 where the grey of that L* itself does not.
        /// </summary>
        internal float[] Cmax;

        /// <summary>
        /// Whether the grey of each L* node is itself out of the paper's reach: a tone no chroma
        /// can bring in.
        /// </summary>
        internal bool[] GreyOutOfReach;

        /// <summary>
        /// The whole colour difference of a Lab colour after the trip to the paper and back (the
        /// rule of ADR 0081): L*, a*, b* in, Delta E76 out; a colour is outside when it passes
        /// Thresholds.CurveGamutDeltaE. Null on a table without a rule (then the table decides
        /// alone).
        /// </summary>
        internal Func<double, double, double, double> Rule;

        /// <summary>
        /// Whether a Lab colour is outside, the way the tool's figures count it: by default the
        /// rule against Thresholds.CurveGamutDeltaE; the run sets the Soft proof tab's own judge
        /// (GamutAnalyzer.Outside on the colour's sRGB, the hybrid of ADR 0042 on its 8-bit
        /// colour), so that a pixel the fit lands on the edge is inside for that tab too. The
        /// pixels near the edge are searched with it, and the share left after the fit is
        /// counted with it. Null on a table without a rule.
        /// </summary>
        internal Func<double, double, double, bool> Judge;

        /// <summary>
        /// The exact largest chroma of the pixels that land near the edge, once per 16-bit
        /// colour, for the run: exact for the 16-bit inputs the tool reads, within a 16-bit step
        /// for a float FITS. An 8-bit colour, or a cell of L* and hue, proved too coarse: in the
        /// dark tones, where the pixels outside live, the edge moves by more than the search's
        /// tolerance within one such cell (measured 2026-11-06: half of the fitted pixels of the
        /// plate-solved FITS were still outside by the exact rule).
        /// </summary>
        internal readonly ConcurrentDictionary<long, float> EdgeCache = new();

        /// <summary>Why the table could not be built, for the user; null when it was.</summary>
        internal string Problem;

        /// <summary>The chroma the search stops at: a node that holds it holds everything.</summary>
        internal const float Unbounded = 150f;

        /// <summary>
        /// Builds the table: at every node of L* and hue a bisection of ten steps between 0 and
        /// <see cref="Unbounded"/> for the largest chroma whose colour the rule keeps within
        /// Thresholds.CurveGamutDeltaE; the rows of L* in parallel.
        /// </summary>
        /// <param name="rule">The rule: L*, a*, b* in, the whole colour difference out.</param>
        /// <param name="lNodes">The nodes over L*, at least 2; by default Thresholds.GamutFitLstarNodes.</param>
        /// <param name="hNodes">The nodes over the hue, at least 2; by default Thresholds.GamutFitHueNodes.</param>
        /// <returns>The table; with only Problem set when the rule gives NaN anywhere.</returns>
        internal static GamutFitTable Build(Func<double, double, double, double> rule, int lNodes = Thresholds.GamutFitLstarNodes, int hNodes = Thresholds.GamutFitHueNodes)
        {
            var t = new GamutFitTable { LNodes = lNodes, HNodes = hNodes, Cmax = new float[lNodes * hNodes], GreyOutOfReach = new bool[lNodes], Rule = rule };
            t.Judge = (l, a, b) => rule(l, a, b) > Thresholds.CurveGamutDeltaE;
            int failed = 0;
            Parallel.For(0, lNodes, li =>
            {
                double l = 100.0 * li / (lNodes - 1);
                double grey = rule(l, 0, 0);
                if (double.IsNaN(grey)) { System.Threading.Interlocked.Exchange(ref failed, 1); return; }
                if (grey > Thresholds.CurveGamutDeltaE)
                {
                    t.GreyOutOfReach[li] = true;
                    for (int hi = 0; hi < hNodes; hi++) t.Cmax[li * hNodes + hi] = 0;
                    return;
                }
                for (int hi = 0; hi < hNodes; hi++)
                {
                    double h = 2 * Math.PI * hi / (hNodes - 1), ca = Math.Cos(h), sb = Math.Sin(h);
                    double lo = 0, up = Unbounded;
                    double top = rule(l, ca * up, sb * up);
                    if (double.IsNaN(top)) { System.Threading.Interlocked.Exchange(ref failed, 1); return; }
                    if (top <= Thresholds.CurveGamutDeltaE) { t.Cmax[li * hNodes + hi] = Unbounded; continue; }
                    for (int k = 0; k < 10; k++)
                    {
                        double c = (lo + up) / 2;
                        if (rule(l, ca * c, sb * c) <= Thresholds.CurveGamutDeltaE) lo = c; else up = c;
                    }
                    t.Cmax[li * hNodes + hi] = (float)lo;
                }
            });
            if (failed == 1) return new GamutFitTable { Problem = Tr.Pick("the profile does not say which colours the paper holds (its tables give no answer)", "profilul nu spune ce culori tine hartia (tabelele lui nu dau raspuns)") };
            return t;
        }

        /// <summary>The table's largest chroma at an L* and a hue, read bilinearly.</summary>
        /// <param name="l">L*, clamped to 0..100.</param>
        /// <param name="hueDeg">The hue in degrees, 0..360 (a negative one is wrapped).</param>
        /// <returns>The largest chroma; <see cref="Unbounded"/> where nothing is outside.</returns>
        internal double CmaxAt(double l, double hueDeg)
        {
            double lf = Math.Clamp(l, 0, 100) / 100 * (LNodes - 1);
            if (hueDeg < 0) hueDeg += 360;
            double hf = Math.Clamp(hueDeg, 0, 360) / 360 * (HNodes - 1);
            int l0 = Math.Min((int)lf, LNodes - 2), h0 = Math.Min((int)hf, HNodes - 2);
            double tl = lf - l0, th = hf - h0;
            return (1 - tl) * ((1 - th) * Cmax[l0 * HNodes + h0] + th * Cmax[l0 * HNodes + h0 + 1])
                 + tl * ((1 - th) * Cmax[(l0 + 1) * HNodes + h0] + th * Cmax[(l0 + 1) * HNodes + h0 + 1]);
        }

    }

    /// <summary>What one fit did to a raster, with its facts when asked for (R-175, R-176).</summary>
    internal sealed record GamutFitOutcome
    {
        /// <summary>The fitted raster, the same size, three planes; the input untouched.</summary>
        internal Raster Image { get; init; }

        /// <summary>Per pixel, whether the paper could not hold its colour as it was; null without facts.</summary>
        internal bool[] OutsideBefore { get; init; }

        /// <summary>Per pixel, whether the paper still cannot hold it after the fit; null without facts.</summary>
        internal bool[] OutsideAfter { get; init; }

        internal double PctOutsideBefore { get; init; }
        internal double PctOutsideAfter { get; init; }

        /// <summary>The share of pixels whose colour the fit changed, in percent.</summary>
        internal double PctTouched { get; init; }

        /// <summary>The share of pixels whose tone itself is out of the paper's reach, in percent.</summary>
        internal double PctUnreachable { get; init; }

        /// <summary>The chroma given up by the touched pixels, at the median and at the 99 % mark; 0 with none touched.</summary>
        internal double ChromaLostMedian { get; init; }
        internal double ChromaLostP99 { get; init; }

        /// <summary>
        /// The share of the pixels outside that lie under L* Thresholds.LowSeparationLstar, in
        /// percent of them; 0 with none outside.
        /// </summary>
        internal double PctOutsideUnderLowSeparation { get; init; }
    }

    /// <summary>
    /// What a person would do by hand instead of the fit (R-176): a tone band and one uniform
    /// saturation factor over it.
    /// </summary>
    internal sealed record GamutFitRecipe
    {
        /// <summary>The band is "under this L*": the smallest whole L* holding Thresholds.GamutFitBandShare of the pixels outside.</summary>
        internal double LstarCeiling { get; init; }

        /// <summary>The saturation factor over the band, 0..1 in steps of Thresholds.GamutFitRecipeStep.</summary>
        internal double Factor { get; init; }

        /// <summary>The share of the pixels outside the factor would leave outside, in percent of them.</summary>
        internal double PctLeft { get; init; }

        /// <summary>The share of all the pixels the band holds, in percent.</summary>
        internal double PctInBand { get; init; }
    }

    /// <summary>The paper fit of a raster through a table (R-175, R-176).</summary>
    internal static class GamutFit
    {
        /// <summary>
        /// Fits <paramref name="r"/> to the paper: every pixel's chroma reduced to what the table
        /// holds at its L* and hue, with the knee's compression; the rows in parallel. A mono
        /// raster is copied, nothing outside.
        /// </summary>
        /// <param name="r">The raster, encoded sRGB 0..1; not changed.</param>
        /// <param name="t">The table, without a Problem.</param>
        /// <param name="knee">
        /// The share of the edge from which the chroma is compressed, 0 to 1; 1 is a hard fit
        /// onto the edge.
        /// </param>
        /// <param name="keepFacts">
        /// Whether the masks and the figures are measured too: the judge on every fitted
        /// colour. Off for the save of a whole image.
        /// </param>
        /// <returns>The fitted raster and, with facts, the masks and the figures.</returns>
        internal static GamutFitOutcome Apply(Raster r, GamutFitTable t, double knee, bool keepFacts)
        {
            int n = r.W * r.H;
            var image = new Raster(r.H, r.W, r.Planes.Length) { Source = r.Source };
            if (!r.IsColor)
            {
                for (int p = 0; p < r.Planes.Length; p++) Array.Copy(r.Planes[p], image.Planes[p], n);
                return new GamutFitOutcome { Image = image, OutsideBefore = keepFacts ? new bool[n] : null, OutsideAfter = keepFacts ? new bool[n] : null };
            }
            float[] R = r.Planes[0], G = r.Planes[1], B = r.Planes[2];
            float[] oR = image.Planes[0], oG = image.Planes[1], oB = image.Planes[2];
            bool[] before = keepFacts ? new bool[n] : null, after = keepFacts ? new bool[n] : null, unreachable = keepFacts ? new bool[n] : null;
            float[] lost = keepFacts ? new float[n] : null, lstar = keepFacts ? new float[n] : null;
            Parallel.For(0, r.H, y =>
            {
                for (int x = 0; x < r.W; x++)
                {
                    int i = y * r.W + x;
                    var (l, a, b) = ColorMath.LabD50OfSrgb(R[i], G[i], B[i]);
                    double c = Math.Sqrt(a * a + b * b);
                    if (keepFacts) lstar[i] = (float)l;
                    if (c < Thresholds.GamutFitMinChroma) { oR[i] = R[i]; oG[i] = G[i]; oB[i] = B[i]; continue; }
                    double cmax = Edge(t, l, a, b, c, R[i], G[i], B[i]);
                    bool outside = c > cmax;
                    if (keepFacts) before[i] = outside;
                    if (outside && cmax < 0)
                    {
                        // The tone itself is out of reach: the judge rejects even its grey (the
                        // edge search answers a negative there), so no chroma brings it in. Left
                        // as it is, and counted. The two reviews of 2026-11-06: the nearest node's
                        // answer could disagree with the judge's within one L*, and a tiny edge
                        // (under the grey floor) with a grey the judge keeps is not out of reach.
                        oR[i] = R[i]; oG[i] = G[i]; oB[i] = B[i];
                        if (keepFacts) { after[i] = true; unreachable[i] = true; }
                        continue;
                    }
                    double fitted = Knee(c, cmax, knee);
                    if (fitted >= c - 1e-9) { oR[i] = R[i]; oG[i] = G[i]; oB[i] = B[i]; continue; }
                    double f = fitted / c;
                    var (red, green, blue) = ColorMath.SrgbOfLabD50(l, a * f, b * f);
                    oR[i] = (float)red; oG[i] = (float)green; oB[i] = (float)blue;
                    if (keepFacts)
                    {
                        lost[i] = (float)(c - fitted);
                        after[i] = t.Judge != null && t.Judge(l, a * f, b * f);
                    }
                }
            });
            if (!keepFacts) return new GamutFitOutcome { Image = image };

            long outsideBefore = 0, outsideAfter = 0, touched = 0, unreach = 0, dark = 0;
            var losses = new List<float>();
            for (int i = 0; i < n; i++)
            {
                if (before[i]) { outsideBefore++; if (lstar[i] < Thresholds.LowSeparationLstar) dark++; }
                if (after[i]) outsideAfter++;
                if (unreachable[i]) unreach++;
                if (lost[i] > 0) { touched++; losses.Add(lost[i]); }
            }
            losses.Sort();
            return new GamutFitOutcome
            {
                Image = image,
                OutsideBefore = before,
                OutsideAfter = after,
                PctOutsideBefore = 100.0 * outsideBefore / n,
                PctOutsideAfter = 100.0 * outsideAfter / n,
                PctTouched = 100.0 * touched / n,
                PctUnreachable = 100.0 * unreach / n,
                ChromaLostMedian = losses.Count == 0 ? 0 : losses[losses.Count / 2],
                ChromaLostP99 = losses.Count == 0 ? 0 : losses[Math.Min(losses.Count - 1, (int)(losses.Count * 0.99))],
                PctOutsideUnderLowSeparation = outsideBefore == 0 ? 0 : 100.0 * dark / outsideBefore,
            };
        }

        /// <summary>
        /// The largest chroma for one pixel: the table's, unless the pixel's chroma reaches
        /// within Thresholds.GamutFitEdgeMargin of it or passes it (so the fit would land it on
        /// the edge) and the table has a rule, when the rule itself is searched along the pixel's
        /// own hue at its own L* (<see cref="ExactEdge"/>), once per 16-bit colour.
        /// </summary>
        /// <param name="t">The table.</param>
        /// <param name="l">The pixel's L*.</param>
        /// <param name="a">The pixel's a*.</param>
        /// <param name="b">The pixel's b*.</param>
        /// <param name="c">The pixel's chroma, over Thresholds.GamutFitMinChroma.</param>
        /// <param name="r">The pixel's encoded red, for the cache's key.</param>
        /// <param name="g">The pixel's encoded green.</param>
        /// <param name="bl">The pixel's encoded blue.</param>
        /// <returns>The largest chroma the paper holds at the pixel's L* and hue; -1 when its tone is out of reach.</returns>
        static double Edge(GamutFitTable t, double l, double a, double b, double c, float r, float g, float bl)
        {
            double hue = Math.Atan2(b, a) * 180 / Math.PI;
            double cmax = t.CmaxAt(l, hue);
            if (t.Judge == null || cmax >= GamutFitTable.Unbounded || c < cmax - Thresholds.GamutFitEdgeMargin) return cmax;
            long key = ((long)Level16(r) << 32) | ((long)Level16(g) << 16) | (long)Level16(bl);
            return t.EdgeCache.GetOrAdd(key, _ => (float)ExactEdge(t.Judge, l, a / c, b / c, cmax));
        }

        /// <summary>A sample as its 16-bit level, for the cache's key.</summary>
        /// <param name="v">The encoded value, 0..1; clamped.</param>
        /// <returns>0..65535.</returns>
        static int Level16(float v) => (int)Math.Round(Math.Clamp(v, 0f, 1f) * 65535.0);

        /// <summary>
        /// The largest chroma the judge keeps inside along one hue at one L*: the band of
        /// Thresholds.GamutFitEdgeMargin around the table's guess is bracketed (from zero when
        /// its bottom is already outside; its top kept when it is still inside), then halved
        /// <see cref="EdgeHalvings"/> times; the answer is the bracket's bottom, a chroma the
        /// judge keeps, or -1 when the judge rejects even the grey of that L*: a tone out of
        /// reach, which no chroma brings in. A bisection and not a secant, since the judge is a
        /// yes or no, and the tool's judge is a step function of the 8-bit colour.
        /// </summary>
        /// <param name="outside">The judge: L*, a*, b* in, whether the colour is outside.</param>
        /// <param name="l">The L*.</param>
        /// <param name="ca">The cosine of the hue (a* per unit of chroma).</param>
        /// <param name="sb">The sine of the hue (b* per unit of chroma).</param>
        /// <param name="guess">The table's largest chroma there.</param>
        /// <returns>The largest chroma, 0 or more; -1 when the grey itself is outside.</returns>
        internal static double ExactEdge(Func<double, double, double, bool> outside, double l, double ca, double sb, double guess)
        {
            double lo = Math.Max(0, guess - Thresholds.GamutFitEdgeMargin), up = guess + Thresholds.GamutFitEdgeMargin;
            if (outside(l, ca * lo, sb * lo))
            {
                up = lo; lo = 0;
                if (outside(l, 0, 0)) return -1;
            }
            else if (!outside(l, ca * up, sb * up)) return up;
            for (int k = 0; k < EdgeHalvings; k++)
            {
                double m = (lo + up) / 2;
                if (outside(l, ca * m, sb * m)) up = m; else lo = m;
            }
            return lo;
        }

        /// <summary>
        /// How many times the edge search halves its bracket: six, so a band of twice
        /// Thresholds.GamutFitEdgeMargin ends under a fiftieth of a chroma unit.
        /// </summary>
        internal const int EdgeHalvings = 6;

        /// <summary>
        /// The chroma after the knee: unchanged up to <paramref name="knee"/> times the edge,
        /// then compressed smoothly towards the edge, which it never reaches; a knee of 1 is a
        /// hard clip at the edge.
        /// </summary>
        /// <param name="c">The chroma.</param>
        /// <param name="cmax">The largest chroma the paper holds there.</param>
        /// <param name="knee">The share of the edge the compression starts from, 0..1.</param>
        /// <returns>The chroma to keep, at most <paramref name="cmax"/>.</returns>
        internal static double Knee(double c, double cmax, double knee)
        {
            double start = knee * cmax;
            if (c <= start) return c;
            double room = cmax - start;
            if (room <= 1e-9) return cmax;
            return start + room * (1 - Math.Exp(-(c - start) / room));
        }

        /// <summary>
        /// The recipe for a hand-made mask (R-176): the tone band holding
        /// Thresholds.GamutFitBandShare of the pixels outside, and the largest saturation factor,
        /// in steps of Thresholds.GamutFitRecipeStep, that brings Thresholds.GamutFitRecipeShare of
        /// them in when applied to every pixel of the band.
        /// </summary>
        /// <param name="r">The raster, encoded sRGB 0..1.</param>
        /// <param name="t">The table, without a Problem.</param>
        /// <returns>The recipe; with nothing outside, a ceiling of 0, a factor of 1 and 0 left.</returns>
        internal static GamutFitRecipe Recipe(Raster r, GamutFitTable t)
        {
            int n = r.W * r.H;
            if (!r.IsColor) return new GamutFitRecipe { Factor = 1 };
            float[] R = r.Planes[0], G = r.Planes[1], B = r.Planes[2];
            var lOut = new float[n]; var cOut = new float[n]; var cmaxOut = new float[n]; var isOut = new bool[n]; var lAll = new float[n];
            Parallel.For(0, r.H, y =>
            {
                for (int x = 0; x < r.W; x++)
                {
                    int i = y * r.W + x;
                    var (l, a, b) = ColorMath.LabD50OfSrgb(R[i], G[i], B[i]);
                    lAll[i] = (float)l;
                    double c = Math.Sqrt(a * a + b * b);
                    if (c < Thresholds.GamutFitMinChroma) continue;
                    double cmax = Edge(t, l, a, b, c, R[i], G[i], B[i]);
                    if (c <= cmax) continue;
                    isOut[i] = true; lOut[i] = (float)l; cOut[i] = (float)c; cmaxOut[i] = (float)cmax;
                }
            });
            var ls = new List<float>();
            for (int i = 0; i < n; i++) if (isOut[i]) ls.Add(lOut[i]);
            if (ls.Count == 0) return new GamutFitRecipe { Factor = 1 };
            ls.Sort();
            // The smallest whole L* over the band's top pixel; a hair above it, so that a tone
            // sitting on a whole number is inside its own band.
            double ceiling = Math.Ceiling(ls[Math.Min(ls.Count - 1, (int)Math.Floor(Thresholds.GamutFitBandShare * (ls.Count - 1) + 0.999999))] + 0.01);
            long inBand = 0;
            for (int i = 0; i < n; i++) if (lAll[i] < ceiling) inBand++;
            // The factor is decided on the band's own pixels outside that a factor can bring in:
            // the ones above the band, and the ones whose tone itself is out of reach (a negative
            // cmax: the judge rejects even their grey), are out of the mask's reach and are
            // counted in what is left.
            long inBandOut = 0;
            for (int i = 0; i < n; i++) if (isOut[i] && lOut[i] < ceiling && cmaxOut[i] >= 0) inBandOut++;
            double factor = 1, left = 100;
            for (double f = 1; f >= Thresholds.GamutFitRecipeStep - 1e-9; f -= Thresholds.GamutFitRecipeStep)
            {
                long stillOut = 0, stillOutInBand = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!isOut[i]) continue;
                    bool band = lOut[i] < ceiling, reachable = cmaxOut[i] >= 0;
                    double c = band ? cOut[i] * f : cOut[i];
                    if (c > cmaxOut[i]) { stillOut++; if (band && reachable) stillOutInBand++; }
                }
                factor = Math.Round(f, 2);
                left = 100.0 * stillOut / ls.Count;
                if (inBandOut == 0 || 1 - (double)stillOutInBand / inBandOut >= Thresholds.GamutFitRecipeShare - 1e-9) break;
            }
            return new GamutFitRecipe { LstarCeiling = ceiling, Factor = factor, PctLeft = left, PctInBand = 100.0 * inBand / n };
        }
    }
}
