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

// TonalAnalyzer.cs - the measurements B1 and B2: where the tones sit, and how many of them fall
// under paper black.
//
// Two scales, on purpose. B1 is in LEVELS (0..255), the language of the histogram and of any
// editor - "96 % of the samples under 64". B2 is in L*, the language of paper: a medium's black
// is an L* (from its Dmax), and what lies under it prints with the same black. On grey the
// conversion is a fixed curve (ColorMath.GrayLevelOfLstar, the LstarLadder table); on colour the
// same level gives another L* by hue, and B1 counts samples over the channels, B2 pixels - which
// is why both are reported. They are FACTS of the image, not the verdict: the print verdict is
// given on the dust and the sky, in DustAnalyzer and PrintStretchSimulator (ADR 0025).
//
// The subsampling steps are those of the acceptance scripts (Thresholds.*Stride).

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The tonal facts of an image: B1 (the means and the percentiles in 8-bit levels, the dark
    /// shares in percent of the samples, and the background level the simulation lifts, on
    /// 0..1) and B2 in L* (the shares under paper black and the two L* thresholds, the
    /// histogram).
    /// </summary>
    internal sealed record TonalResult
    {
        /// <summary>The mean of each plane, in levels 0..255, over all the pixels.</summary>
        internal IReadOnlyList<double> MeanLevels { get; init; }

        /// <summary>
        /// The percentage of the samples (all planes) whose 8-bit level, the one an 8-bit export
        /// would write (<see cref="RasterOps.Level8"/>), is under <see cref="Thresholds.DarkLevelA"/>
        /// and under <see cref="Thresholds.DarkLevelB"/>, on every format (ADR 0043).
        /// </summary>
        internal double PctBelowDarkA { get; init; }
        internal double PctBelowDarkB { get; init; }

        /// <summary>
        /// The percentiles of the reference channel, in levels, at <see cref="PercentileRanks"/>.
        /// </summary>
        internal IReadOnlyList<double> ReferencePercentiles { get; init; }
        internal static readonly double[] PercentileRanks = { 0.1, 1, 50, 90, 99, 99.9, 100 };

        /// <summary>
        /// The median of the reference channel, 0..1 - the "background" the simulation lifts.
        /// </summary>
        internal double BackgroundMedian { get; init; }

        internal double Dmax { get; init; }
        internal double PaperBlackLstar { get; init; }

        internal double LstarMedian { get; init; }
        internal double PctBelowPaperBlack { get; init; }
        internal double PctBelowLowSeparation { get; init; }
        internal double PctAboveHighlight { get; init; }

        /// <summary>The L* histogram, 101 bins (0..100), in percent - for the window.</summary>
        internal IReadOnlyList<double> LstarHistogramPct { get; init; }
    }

    /// <summary>
    /// A row of the level - L* table: a reference mark of the tab, with its L* and the 8-bit level
    /// of a grey with the same L*.
    /// </summary>
    /// <param name="Lstar">The mark's L*, 0..100.</param>
    /// <param name="GrayLevel">
    /// The 8-bit level of a grey (R = G = B) with that L*, 0..255, not rounded.
    /// </param>
    /// <param name="Meaning">
    /// What the mark is, in the interface language as the window shows it ("negrul hartiei: baryta (Dmax
    /// 2.4)", "separare slaba").
    /// </param>
    /// <param name="Chosen">
    /// The black of the medium the image is judged on: the chosen medium's, or the explicit
    /// Dmax's.
    /// </param>
    internal sealed record LstarLadderRow(double Lstar, double GrayLevel, string Meaning, bool Chosen = false);

    /// <summary>
    /// Measures where the tones sit (B1, B2) and builds the level - L* table of the Tonuri tab.
    /// </summary>
    internal static class TonalAnalyzer
    {
        /// <summary>
        /// The level - L* correspondence table for the tab's reference marks: the black of the
        /// three media (and of an explicit Dmax, R-042), the thresholds in levels (32/64), the
        /// thresholds in L* (15/50) and the simulation's primary target (the given one, otherwise
        /// the recommended one; no row when "asa cum e" is the primary row). It measures nothing
        /// of the image: the window shows it so that a threshold in L* can be read on Siril's
        /// histogram. Valid on grey; on colour the same level gives another L* by hue.
        /// </summary>
        /// <param name="p">
        /// The run's parameters: the medium, the Dmax and whether it is explicit (and read from a
        /// profile), the given target background.
        /// </param>
        /// <param name="s">
        /// The simulation, for its primary row; null without one, and then a given target
        /// background has its row.
        /// </param>
        /// <returns>The rows, in increasing L*.</returns>
        internal static IReadOnlyList<LstarLadderRow> LstarLadder(Parameters p, SimulationResult s = null)
        {
            var rows = new List<LstarLadderRow>();
            // The marked row is the paper the analysis uses: the chosen medium when the Dmax is
            // its own, otherwise the explicit Dmax's row - even when another medium has the same
            // Dmax (R-042).
            foreach (PrintMedium m in Enum.GetValues(typeof(PrintMedium)))
            {
                double dmax = Parameters.DmaxOf(m);
                bool chosen = m == p.Medium && !p.DmaxIsExplicit;
                rows.Add(FromLstar(ColorMath.PaperBlackLstar(dmax), Tr.Pick($"paper black: {Parameters.NameOf(m)} (Dmax {Fmt.Dmax(dmax)})", $"negrul hartiei: {Parameters.NameOf(m)} (Dmax {Fmt.Dmax(dmax)})"), chosen));
            }
            if (p.DmaxIsExplicit)
                rows.Add(FromLstar(ColorMath.PaperBlackLstar(p.Dmax), Tr.Pick($"paper black: {(p.DmaxProfile != null ? "ICC profile" : "explicit")} Dmax {Fmt.Dmax(p.Dmax)}", $"negrul hartiei: Dmax {(p.DmaxProfile != null ? "din profilul ICC" : "explicit")} {Fmt.Dmax(p.Dmax)}"), true));
            rows.Add(FromLevel(Thresholds.DarkLevelA, Tr.Pick($"the threshold \"under level {Thresholds.DarkLevelA}\"", $"pragul \"sub nivelul {Thresholds.DarkLevelA}\"")));
            rows.Add(FromLevel(Thresholds.DarkLevelB, Tr.Pick($"the threshold \"under level {Thresholds.DarkLevelB}\"", $"pragul \"sub nivelul {Thresholds.DarkLevelB}\"")));
            rows.Add(FromLstar(Thresholds.LowSeparationLstar, Tr.Pick("the eye barely tells apart", "ochiul abia deosebeste")));
            rows.Add(FromLstar(Thresholds.HighlightLstar, Tr.Pick("highlights", "lumini")));
            SimulationRow primary = s?.Primary;
            if (primary != null && !primary.AsIs)
                rows.Add(FromLevel(primary.Target * 255, primary.Explicit ? Tr.Pick($"the target background given {Fmt.Level(primary.Target)}", $"fundalul-tinta dat {Fmt.Level(primary.Target)}") : Tr.Pick($"the recommended background {Fmt.Level(primary.Target)}", $"fundalul recomandat {Fmt.Level(primary.Target)}")));
            else if (primary == null && p.TargetBackground.HasValue)
                rows.Add(FromLevel(p.TargetBackground.Value * 255, Tr.Pick($"the target background given {Fmt.Level(p.TargetBackground.Value)}", $"fundalul-tinta dat {Fmt.Level(p.TargetBackground.Value)}")));
            return rows.OrderBy(r => r.Lstar).ToList();

            static LstarLadderRow FromLstar(double l, string meaning, bool chosen = false)
                => new(l, ColorMath.GrayLevelOfLstar(l), meaning, chosen);
            static LstarLadderRow FromLevel(double level, string meaning)
                => new(ColorMath.LstarOfGrayLevel(level), level, meaning);
        }

        /// <summary>
        /// Measures B1 (the means and the dark shares on every sample, the percentiles at
        /// <see cref="Thresholds.PercentileStride"/>, the background level at
        /// <see cref="Thresholds.TonalStride"/>) and B2 (the L* facts at
        /// <see cref="Thresholds.TonalStride"/>).
        /// </summary>
        /// <param name="r">The image, 0..1.</param>
        /// <param name="p">The run's parameters; only the Dmax is read.</param>
        /// <returns>The tonal facts.</returns>
        internal static TonalResult Analyze(Raster r, Parameters p)
        {
            // B1: on every pixel - means and counts, a single pass per plane.
            var means = new double[r.Planes.Length];
            long belowA = 0, belowB = 0, total = 0;
            // "sub nivelul N" (N = Thresholds.DarkLevelA or DarkLevelB) is a sample whose 8-bit
            // level, the one an export writes (RasterOps.Level8), is under N: Level8Under, on the
            // same product Level8 forms, without a Math.Round per sample. Until the review of
            // 2026-10-23 the float sample was compared with N / 255, which counted the level N
            // itself on an 8-bit TIFF, PNG or JPEG, read as n x 257 / 65535 (ADR 0043; the script
            // tested the high byte, hi < N).
            for (int pl = 0; pl < r.Planes.Length; pl++)
            {
                float[] plane = r.Planes[pl];
                double s = 0;
                for (int i = 0; i < plane.Length; i++)
                {
                    float v = plane[i];
                    s += v;
                    double level = v * 255.0;
                    if (Level8Under(level, Thresholds.DarkLevelA)) belowA++;
                    if (Level8Under(level, Thresholds.DarkLevelB)) belowB++;
                }
                means[pl] = s / plane.Length * 255;
                total += plane.Length;
            }

            // The seven percentiles by selection on one copy, not by sorting the 5.8 million
            // samples of a 23 MP image (Stats.Percentiles; the same values, half a second less).
            double[] pct = Stats.Percentiles(RasterOps.Subsample(r, Thresholds.PercentileStride).Reference, TonalResult.PercentileRanks);
            for (int i = 0; i < pct.Length; i++) pct[i] *= 255;

            double bg = Stats.Median(RasterOps.Subsample(r, Thresholds.TonalStride).Reference);

            // B2: L* on the subsample.
            var (median, belowBlack, belowLow, aboveHigh, hist) = LstarStats(r, p.Dmax);

            return new TonalResult
            {
                MeanLevels = means,
                PctBelowDarkA = 100.0 * belowA / total,
                PctBelowDarkB = 100.0 * belowB / total,
                ReferencePercentiles = pct,
                BackgroundMedian = bg,
                Dmax = p.Dmax,
                PaperBlackLstar = ColorMath.PaperBlackLstar(p.Dmax),
                LstarMedian = median,
                PctBelowPaperBlack = belowBlack,
                PctBelowLowSeparation = belowLow,
                PctAboveHighlight = aboveHigh,
                LstarHistogramPct = hist,
            };
        }

        /// <summary>
        /// Whether the 8-bit level of a sample is under <paramref name="n"/>, from the product
        /// <see cref="RasterOps.Level8"/> rounds: round(x) &lt; n is x &lt; n - 0.5 when n is even
        /// (the half rounds up to n, the even value) and x &lt;= n - 0.5 when n is odd (the half
        /// rounds down to n - 1). The same answer as Level8 for every value, whatever the
        /// threshold's parity.
        /// </summary>
        /// <param name="level255">The sample times 255, in double, as Level8 forms it before rounding.</param>
        /// <param name="n">The level compared with, 0..255.</param>
        /// <returns><c>true</c> when Level8 of the sample is under <paramref name="n"/>.</returns>
        internal static bool Level8Under(double level255, int n)
            => (n & 1) == 0 ? level255 < n - 0.5 : level255 <= n - 0.5;

        /// <summary>
        /// The L* statistics of a raster, at the given step (by default the tonal one).
        /// </summary>
        /// <param name="r">The raster, 0..1.</param>
        /// <param name="dmax">
        /// The paper's Dmax, positive; its black is <see cref="ColorMath.PaperBlackLstar"/>.
        /// </param>
        /// <param name="stride">
        /// The subsampling step, as in <see cref="RasterOps.Subsample"/>.
        /// </param>
        /// <returns>
        /// As <see cref="LstarStats(float[], double)"/> returns them, on one L* per pixel of the
        /// subsample.
        /// </returns>
        internal static (double Median, double PctBelowPaperBlack, double PctBelowLow, double PctAboveHigh, double[] HistPct)
            LstarStats(Raster r, double dmax, int stride = Thresholds.TonalStride)
            => LstarStats(RasterOps.LstarPlane(r, stride), dmax);

        /// <summary>
        /// The same statistics on an L* plane already made (RasterOps.LstarPlane).
        /// </summary>
        /// <param name="L">One L* (0..100) per pixel; at least one value.</param>
        /// <param name="dmax">
        /// The paper's Dmax, positive; its black is <see cref="ColorMath.PaperBlackLstar"/>.
        /// </param>
        /// <returns>
        /// The median L*; the percentages (0..100) of the values under paper black, under
        /// <see cref="Thresholds.LowSeparationLstar"/> and over
        /// <see cref="Thresholds.HighlightLstar"/>; and the histogram of the L* rounded to an
        /// integer, 101 bins (0..100), in percent.
        /// </returns>
        internal static (double Median, double PctBelowPaperBlack, double PctBelowLow, double PctAboveHigh, double[] HistPct)
            LstarStats(float[] L, double dmax)
        {
            double black = ColorMath.PaperBlackLstar(dmax);
            long nBlack = 0;
            var hist = new double[101];
            for (int i = 0; i < L.Length; i++)
            {
                float l = L[i];
                if (l < black) nBlack++;
                hist[Math.Clamp((int)Math.Round(l), 0, 100)]++;
            }
            for (int i = 0; i < hist.Length; i++) hist[i] = 100.0 * hist[i] / L.Length;
            double median = Stats.Median(L);
            var (low, high) = LstarShares(L);
            return (median, 100.0 * nBlack / L.Length, low, high, hist);
        }

        /// <summary>
        /// Only the percentages of the pixels under L* LowSeparationLstar and over HighlightLstar
        /// of an L* plane: what a row of the simulation carries. Exposed for the simulator, which
        /// recomputes them on the grid's rows from the L* plane of the subsample lifted through
        /// the table (RasterOps.LstarPlane with LiftTable), without a lifted raster and without
        /// the median and the histogram, which the row does not show and whose sort would cost
        /// about as much as all the rest of the row (ADR 0035).
        /// </summary>
        /// <param name="L">One L* (0..100) per pixel; an empty plane gives NaN for both.</param>
        /// <returns>
        /// The percentages (0..100) of the values under <see cref="Thresholds.LowSeparationLstar"/>
        /// and over <see cref="Thresholds.HighlightLstar"/>.
        /// </returns>
        internal static (double PctBelowLow, double PctAboveHigh) LstarShares(float[] L)
        {
            long nLow = 0, nHigh = 0;
            for (int i = 0; i < L.Length; i++)
            {
                float l = L[i];
                if (l < Thresholds.LowSeparationLstar) nLow++;
                if (l > Thresholds.HighlightLstar) nHigh++;
            }
            return (100.0 * nLow / L.Length, 100.0 * nHigh / L.Length);
        }
    }
}
