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

// PrintStretchSimulator.cs - the measurement B4: at what background does the dust reach paper
// black, without going under it?
//
// IT IS A SIMULATION, not a stretch. It takes the MEASURED background (the median of the
// reference channel) to every target of the grid 0.05..0.20 with a gamma curve identical on all
// the channels, then re-measures the dust and the sky (DustAnalyzer, on the same patch map) and
// the percentages. The recommendation is the lowest row - "asa cum e" included, under or over the
// measured level - at which the dust sits at or above paper black and the sky at or above the
// visibility threshold: the minimum usable level, in both directions (ADR 0025, ADR 0029; the
// dust and the sky are the classes of ADR 0026). What Siril's real stretch will do differs in
// the shape of the curve, and not by a little: a VeraLux stretch to 0.17 kept the dust at L* 7.6,
// where the gamma curve predicted it above black (ADR 0025). The report calls it a simulation
// every time.
//
// The exact target is not searched by bisection but on a LADDER of lifts in steps of 0.01
// (Thresholds.ExactTarget*), with linear interpolation between two steps: the same ladder gives
// the exact target of the dust, of each cloud (114 rows on the plate-solved FITS, V-004 - a
// bisection on each would be 2500 lifts) and the two targets at the neighbouring Dmax, from the
// same lifts: at most 56, and fewer when every series has crossed its level and every table row
// is built before the ladder's end, since an interpolated crossing reads nothing past the first
// step at or above its level (measured 2026-10-23: 16 steps on de_printat.png, where the last
// grid row ends it, 35 on the plate-solved FITS at Dmax 2.0, where the darkest cloud row crosses
// last, and the same figures to the bit). A step builds no lifted raster: it lifts and
// linearises every sample from a single 16-bit table (LiftTable, ADR 0035) and sums the
// patches' luminances directly; the "asa cum e" row stays on the exact path.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// A row of the simulation: a target background, its gamma, and the dust, the sky and the
    /// pixel shares at it.
    /// </summary>
    internal sealed record SimulationRow
    {
        /// <summary>
        /// The target background, 0..1; for the "asa cum e" row it is the measured background.
        /// </summary>
        internal double Target { get; init; }
        internal bool AsIs { get; init; }

        /// <summary>
        /// The row of the target the user gave (Parameters.TargetBackground): a row of its own,
        /// or the grid's row when the target is on the grid. The row's target is the given one
        /// rounded to 6 decimals, and the mark needs the two within 1e-9, so a target given with
        /// more decimals (0.1234567) gets its row without the mark.
        /// </summary>
        internal bool Explicit { get; init; }
        internal double Gamma { get; init; }

        /// <summary>The sky: the median of the sky patches (DustAnalyzer.Measure).</summary>
        internal double LstarSky { get; init; }

        /// <summary>
        /// The dust: the dust class's percentile or the darkest sky, by DustMap.DustSource
        /// (DustAnalyzer.Measure).
        /// </summary>
        internal double LstarDust { get; init; }

        /// <summary>"Negru pur": the percentage of the 32 px patches under paper black.</summary>
        internal double PctPatchesBelowPaperBlack { get; init; }
        internal double PctBelowLowSeparation { get; init; }
        internal double PctAboveHighlight { get; init; }
        internal double PaperBlackLstar { get; init; }

        /// <summary>
        /// The dust at or above paper black. The tolerance is for equality in floating point.
        /// </summary>
        internal bool DustOnPaperBlack => LstarDust >= PaperBlackLstar - 1e-9;

        /// <summary>
        /// The row can be printed: the dust on paper black AND the sky visible. A row with the
        /// dust on black and the sky under the threshold is not print-safe - the table used to
        /// call it that, and the owner read "print-safe" beside a sky of L* 11 (ADR 0029).
        /// </summary>
        internal bool PrintSafe => DustOnPaperBlack && SkyVisible;

        /// <summary>
        /// The sky at or above the visibility threshold (Thresholds.LowSeparationLstar): the paper
        /// separates lower down too, but in room light a sky under the threshold looks almost
        /// black (ADR 0029).
        /// It is compared as the table prints it, to one decimal: a row that shows "15.0" passes,
        /// otherwise the table would put "cer sub L* 15" beside a sky written 15.0. The threshold
        /// is a rule of thumb; the second decimal says nothing.
        /// </summary>
        internal bool SkyVisible => Math.Round(LstarSky, 1) >= Thresholds.LowSeparationLstar;

        /// <summary>The recommended row; the simulator marks it after choosing.</summary>
        internal bool Recommended { get; set; }
    }

    /// <summary>
    /// A catalogued cloud with enough patches to have its own row, from the darkest.
    /// </summary>
    internal sealed record CloudRow
    {
        internal string Name { get; init; }
        internal int Patches { get; init; }

        /// <summary>
        /// The cloud's L* as it is: the DustCloudFraction percentile of its patches.
        /// </summary>
        internal double Lstar { get; init; }

        /// <summary>
        /// The target at which the cloud reaches paper black; null when no step of the ladder
        /// brings it there. The ladder ends at ExactTargetHi, or at a given target above it, and
        /// a crossing past ExactTargetHi is then interpolated across that one wide step.
        /// </summary>
        internal double? ExactTarget { get; init; }

        /// <summary>The cloud's index in DustMap.Clouds, for the map.</summary>
        internal int MapIndex { get; init; }
    }

    /// <summary>
    /// The print-background simulation (B4): the rows of the grid and of the given target, the
    /// recommendation, the exact targets read on the ladder, the cloud rows and the map, with the
    /// sentences that name the method for the report and the window.
    /// </summary>
    internal sealed record SimulationResult
    {
        internal double BackgroundMeasured { get; init; }
        internal double Dmax { get; init; }
        internal double PaperBlackLstar { get; init; }

        /// <summary>
        /// All the rows, in increasing target; "asa cum e" among them, at its level.
        /// </summary>
        internal IReadOnlyList<SimulationRow> Rows { get; init; }

        internal SimulationRow AsIs { get; init; }

        /// <summary>
        /// The lowest print-safe row with the sky visible, "asa cum e" included; null when none
        /// is.
        /// </summary>
        internal SimulationRow Recommended { get; init; }
        internal double? RecommendedTarget => Recommended?.Target;
        internal bool RecommendedIsAsIs => Recommended != null && Recommended.AsIs;

        /// <summary>
        /// The dust as it is is at or above paper black. Not print-safe in the sense of
        /// <see cref="SimulationRow.PrintSafe"/>, which also needs the sky visible (ADR 0029);
        /// it was called AlreadyPrintSafe until the review of 2026-10-23 (ADR 0043).
        /// </summary>
        internal bool DustOnPaperBlackAsIs => AsIs.DustOnPaperBlack;

        /// <summary>
        /// The sky itself is under paper black: the sky and the dust would print with the same
        /// black ("PROBLEMA").
        /// </summary>
        internal bool SkyBelowPaperBlack => AsIs.LstarSky < PaperBlackLstar;

        /// <summary>The dust is under paper black ("ATENTIE" when the sky is not).</summary>
        internal bool DustBelowPaperBlack => !AsIs.DustOnPaperBlack;

        /// <summary>
        /// The sky is under the visibility threshold ("ATENTIE", even with the dust above paper
        /// black).
        /// </summary>
        internal bool SkyBelowVisibility => !AsIs.SkyVisible;

        /// <summary>
        /// The target at which the sky reaches the visibility threshold, interpolated on the
        /// ladder; null when no step of the ladder reaches it (as for ExactTarget).
        /// </summary>
        internal double? VisibilityTarget { get; init; }

        /// <summary>
        /// The exact target at which the dust reaches black, interpolated on the ladder; null when
        /// no step of the ladder reaches it, and null too with a background at 0 or 1. The ladder
        /// ends at ExactTargetHi, or at a given target above it, and a crossing past ExactTargetHi
        /// is then interpolated across that one wide step.
        /// </summary>
        internal double? ExactTarget { get; init; }

        /// <summary>
        /// The same exact target at Dmax - DmaxSensitivityStep and at Dmax + DmaxSensitivityStep.
        /// </summary>
        internal double? ExactTargetDmaxLower { get; init; }
        internal double? ExactTargetDmaxHigher { get; init; }

        /// <summary>
        /// The Dmax from which the dust as it is reaches paper black: the one whose black is the
        /// dust's L*. NaN for a dust at L* 0. The sky's visibility does not enter it, so with the
        /// sky under LowSeparationLstar the image is not print-safe at this Dmax either, and the
        /// report says so beside it (ReportWriter.DustDmaxText; it was called DmaxToPassAsIs and
        /// printed "trece asa cum e" until the review of 2026-10-23, ADR 0043).
        /// </summary>
        internal double DmaxDustOnPaperBlackAsIs { get; init; }

        /// <summary>
        /// How many steps of the ladder were lifted: all of them when some series never reaches
        /// its level, fewer when every series has crossed and every table row is built before
        /// the ladder's end (Simulate). 0 with a background at 0 or 1. A figure for the tests and
        /// the timings; nothing prints it.
        /// </summary>
        internal int StepsLifted { get; init; }

        /// <summary>
        /// The primary row: the row marked Explicit for the target the user gave, otherwise the
        /// recommended one, otherwise "asa cum e".
        /// </summary>
        internal SimulationRow Primary { get; init; }

        /// <summary>
        /// The patch map and the "asa cum e" statistics, for the window and for the texts.
        /// </summary>
        internal DustMap Map { get; init; }
        internal DustStats AsIsDust { get; init; }
        internal DustMethod Method => Map.Method;

        /// <summary>
        /// The clouds with at least DustCloudMinPatches patches, from the darkest; empty without a
        /// WCS, or when no cloud has that many patches in the frame.
        /// </summary>
        internal IReadOnlyList<CloudRow> Clouds { get; init; }

        /// <summary>How many catalogued clouds have at least one patch in the frame.</summary>
        internal int CloudCount => Map.Clouds.Count;

        internal int PatchCount => Map.PatchCount;
        internal int DustPatchCount => AsIsDust.DustPatchCount;

        /// <summary>
        /// How the dust class is defined on the map's path: "cu 5 L* sub nivelul local al benzii
        /// sau sub cerul modelat" or "cu 3.8 L* sub cerul modelat (4 sigma ale cerului)"; the
        /// same sentence in the map's legend.
        /// </summary>
        internal string DustClassDefinition => Map.Method == DustMethod.Band
            ? $"cu {Fmt.F0(Thresholds.DustDepthLstar)} L* sub nivelul local al benzii sau sub cerul modelat"
            : $"cu {Fmt.F1(Map.NoiseThreshold)} L* sub cerul modelat ({Fmt.F0(Thresholds.SkyNoiseK)} sigma ale cerului)";

        /// <summary>
        /// What the dust is, by what gave its L* (ADR 0033): "percentila 25 % a dalelor de praf
        /// (cu 5 L* sub nivelul local al benzii sau sub cerul modelat; 9.3 % din cadru)" when the
        /// dust class decides; otherwise "cel mai intunecat 1 % al dalelor de cer", with why not
        /// the dust class: empty, under DustClassMinFraction of the frame, or lighter.
        /// </summary>
        internal string DustDescription
        {
            get
            {
                string dustPct = Fmt.Pct(100.0 * DustPatchCount / PatchCount);
                if (Map.DustSource == DustSource.DustClass)
                    return $"percentila {Fmt.F0(Thresholds.DustClassFraction * 100)} % a dalelor de praf ({DustClassDefinition}; {dustPct} din cadru)";
                string why = DustPatchCount == 0
                    ? "nicio dala de praf"
                    : DustPatchCount < Thresholds.DustClassMinFraction * PatchCount
                        ? $"dalele de praf, {dustPct} din cadru, sunt sub {Fmt.F0(Thresholds.DustClassMinFraction * 100)} % si nu decid"
                        : $"mai intunecat decat percentila {Fmt.F0(Thresholds.DustClassFraction * 100)} % a dalelor de praf, {dustPct} din cadru";
                return Map.SkyClassEmpty
                    ? $"cel mai intunecat {Fmt.F0(Thresholds.SkyDarkestFraction * 100)} % al tuturor dalelor (nicio dala de cer; {why})"
                    : $"cel mai intunecat {Fmt.F0(Thresholds.SkyDarkestFraction * 100)} % al dalelor de cer ({why})";
            }
        }

        /// <summary>
        /// "mediana dalelor de cer, in afara benzii (|b| peste 10 grade)", "mediana dalelor de cer
        /// (clase relative la cerul modelat)" or, without any sky patch, "mediana tuturor dalelor
        /// (nicio dala de cer)".
        /// </summary>
        internal string SkyDescription => Map.SkyClassEmpty
            ? "mediana tuturor dalelor (nicio dala de cer)"
            : Map.Method == DustMethod.Band
                ? $"mediana dalelor de cer, in afara benzii (|b| peste {Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} grade)"
                : "mediana dalelor de cer (clase relative la cerul modelat)";

        /// <summary>
        /// Why the band classes were not made: "fara WCS", "fara banda galactica in cadru",
        /// "cadrul e in intregime in banda", "camp prea ingust pentru banda: 10.9 grade de
        /// latitudine galactica, sub 20" (the patch centres); null with the band.
        /// </summary>
        internal string MethodNote => Map.SkyReason switch
        {
            SkyReason.NoWcs => "fara WCS",
            SkyReason.NoBand => "fara banda galactica in cadru",
            SkyReason.AllBand => "cadrul e in intregime in banda",
            SkyReason.NarrowField => $"camp prea ingust pentru banda: {Fmt.F1(Map.LatitudeSpanDeg)} grade de latitudine galactica, sub {Fmt.F0(Thresholds.BandMinLatitudeSpanDeg)}",
            _ => null,
        };

        /// <summary>
        /// "praf 9.3 %, cer 65.1 %, banda 22.4 %, nori luminosi 3.0 %, varfuri 0.1 %"; on the sky
        /// path, with the image's thresholds: "nebuloasa 12.3 % (peste cerul modelat cu 3.8 L*,
        /// 4 sigma ale cerului), nebuloasa stralucitoare 5.2 % (peste 16.0 L*, pragul Otsu)".
        /// </summary>
        internal string ClassFractions
        {
            get
            {
                IReadOnlyList<string> names = Map.ClassNames;
                string Of(int i, PatchClass c) => $"{names[i]} {Fmt.Pct(100 * Map.FractionOf(c))}";
                string nebula = Of(2, PatchClass.Band), bright = Of(3, PatchClass.BrightCloud);
                if (Map.Method == DustMethod.Sky)
                {
                    nebula += $" (peste cerul modelat cu {Fmt.F1(Map.NoiseThreshold)} L*, {Fmt.F0(Thresholds.SkyNoiseK)} sigma ale cerului)";
                    bright += double.IsPositiveInfinity(Map.BrightThreshold) ? " (nicio dala peste pragul Otsu)" : $" (peste {Fmt.F1(Map.BrightThreshold)} L*, pragul Otsu)";
                }
                return $"{Of(0, PatchClass.Dust)}, {Of(1, PatchClass.Sky)}, {nebula}, {bright}, {Of(4, PatchClass.Peak)}";
            }
        }
    }

    /// <summary>
    /// The measurement B4: lifts the measured background to every target of the grid and of the
    /// ladder and re-measures the dust and the sky on each.
    /// </summary>
    internal static class PrintStretchSimulator
    {
        /// <summary>
        /// Builds the patch map on the subsample at <see cref="Thresholds.TonalStride"/>,
        /// measures the "asa cum e" row and, when the measured background is strictly inside
        /// (0, 1), lifts the subsample once per step of the ladder (the exact steps, the grid and
        /// the given target), from the lowest up: every step feeds the dust, sky and cloud series
        /// the exact targets are read on, and the steps of the grid and of the given target
        /// become rows. The ladder stops early once every series has a step at or above the
        /// level it is read against and every row is built; the targets read are then the same
        /// as from the whole ladder.
        /// </summary>
        /// <param name="r">
        /// The image, 0..1; its <see cref="Raster.Source"/> gives the WCS, when there is one.
        /// </param>
        /// <param name="tonal">
        /// The image's tonal facts: the measured background, the paper's black, the Dmax and the
        /// "asa cum e" pixel shares.
        /// </param>
        /// <param name="p">
        /// The run's parameters: the grid, the given target and the Dmax of the two neighbouring
        /// exact targets. The given target, rounded to 6 decimals, must lie strictly inside
        /// (0, 1), or RasterOps.GammaFor throws (an ArgumentException that ends the run); NaN
        /// makes the LiftTable throw.
        /// </param>
        /// <returns>
        /// The simulation; with a background at 0 or 1, the "asa cum e" row alone, no exact
        /// target, and cloud rows without one.
        /// </returns>
        internal static SimulationResult Simulate(Raster r, TonalResult tonal, Parameters p)
        {
            double bg = tonal.BackgroundMedian;
            double black = tonal.PaperBlackLstar;
            Raster sub = RasterOps.Subsample(r, Thresholds.TonalStride);
            float[] asIsPatches = DustAnalyzer.PatchLstars(sub);
            DustMap map = DustAnalyzer.Build(r.W, r.H, r.Source?.Wcs, asIsPatches);
            DustStats asIsDust = DustAnalyzer.Measure(asIsPatches, map, black);
            var asIs = new SimulationRow
            {
                Target = bg,
                AsIs = true,
                Gamma = 1,
                LstarSky = asIsDust.LstarSky,
                LstarDust = asIsDust.LstarDust,
                PctPatchesBelowPaperBlack = asIsDust.PctPatchesBelowPaperBlack,
                PctBelowLowSeparation = tonal.PctBelowLowSeparation,
                PctAboveHighlight = tonal.PctAboveHighlight,
                PaperBlackLstar = black,
            };

            var rows = new List<SimulationRow> { asIs };
            double? exact = null, exactLo = null, exactHi = null, visible = null;
            int steps = 0;
            var cloudRows = new List<CloudRow>();
            int[] rowClouds = Enumerable.Range(0, map.Clouds.Count).Where(c => map.Clouds[c].Patches.Length >= Thresholds.DustCloudMinPatches).ToArray();

            // A background at 0 or 1 has no gamma; it happens only on synthetic rasters (pure
            // black), but it must not throw.
            if (bg > 0 && bg < 1)
            {
                // The ladder: the exact steps, the grid and the given target, a single lift for
                // each, in increasing order.
                var ladder = new SortedSet<double>();
                for (double t = Thresholds.ExactTargetLo; t <= Thresholds.ExactTargetHi + 1e-9; t += Thresholds.ExactTargetStep) ladder.Add(Math.Round(t, 6));
                foreach (double t in p.SimulationTargets) ladder.Add(Math.Round(t, 6));
                if (p.TargetBackground.HasValue) ladder.Add(Math.Round(p.TargetBackground.Value, 6));
                double[] targets = ladder.ToArray();

                var tableRows = new HashSet<double>(p.SimulationTargets.Select(t => Math.Round(t, 6)));
                if (p.TargetBackground.HasValue) tableRows.Add(Math.Round(p.TargetBackground.Value, 6));
                int rowsLeft = targets.Count(tableRows.Contains);

                // The series the exact targets are read on, one value per step lifted, and the
                // levels each one is read against: the dust at the three paper blacks, the sky
                // at the visibility threshold, each row cloud at paper black. Crossing reads a
                // series up to its first step at or above the level and no further, so the
                // ladder stops once every series has such a step and every table row is built:
                // the results are the ones the whole ladder would give, from fewer lifts.
                double blackLo = ColorMath.PaperBlackLstar(p.Dmax - Thresholds.DmaxSensitivityStep);
                double blackHi = ColorMath.PaperBlackLstar(p.Dmax + Thresholds.DmaxSensitivityStep);
                var seriesTargets = new List<double>();
                var dustAt = new List<double>();
                var skyAt = new List<double>();
                var cloudAt = new List<double[]>();
                bool dustCrossed = false, dustLoCrossed = false, dustHiCrossed = false, skyCrossed = false;
                var cloudCrossed = new bool[rowClouds.Length];
                int cloudsLeft = rowClouds.Length;
                for (int i = 0; i < targets.Length; i++)
                {
                    double target = targets[i];
                    double gamma = RasterOps.GammaFor(bg, target);
                    // The lift and the linearisation from a single table, without a lifted raster
                    // (ADR 0035); the patches are re-measured on the same map.
                    var lift = new LiftTable(gamma);
                    DustStats d = DustAnalyzer.Measure(DustAnalyzer.PatchLstars(sub, lift), map, black);
                    steps++;
                    seriesTargets.Add(target);
                    dustAt.Add(d.LstarDust);
                    skyAt.Add(d.LstarSky);
                    cloudAt.Add(d.CloudLstars);
                    dustCrossed |= d.LstarDust >= black;
                    dustLoCrossed |= d.LstarDust >= blackLo;
                    dustHiCrossed |= d.LstarDust >= blackHi;
                    skyCrossed |= d.LstarSky >= Thresholds.LowSeparationLstar;
                    for (int k = 0; k < rowClouds.Length; k++)
                        if (!cloudCrossed[k] && d.CloudLstars[rowClouds[k]] >= black) { cloudCrossed[k] = true; cloudsLeft--; }
                    if (tableRows.Contains(target))
                    {
                        // The row's pixel percentages, on the L* plane of the subsample lifted
                        // through the same table.
                        var (low, high) = TonalAnalyzer.LstarShares(RasterOps.LstarPlane(sub, lift));
                        rows.Add(new SimulationRow
                        {
                            Target = target,
                            Explicit = p.TargetBackground.HasValue && Math.Abs(target - p.TargetBackground.Value) < 1e-9,
                            Gamma = gamma,
                            LstarSky = d.LstarSky,
                            LstarDust = d.LstarDust,
                            PctPatchesBelowPaperBlack = d.PctPatchesBelowPaperBlack,
                            PctBelowLowSeparation = low,
                            PctAboveHighlight = high,
                            PaperBlackLstar = black,
                        });
                        rowsLeft--;
                    }
                    if (rowsLeft == 0 && dustCrossed && dustLoCrossed && dustHiCrossed && skyCrossed && cloudsLeft == 0) break;
                }
                rows = rows.OrderBy(x => x.Target).ToList();

                double[] st = seriesTargets.ToArray(), dust = dustAt.ToArray();
                exact = Crossing(st, dust, black);
                visible = Crossing(st, skyAt.ToArray(), Thresholds.LowSeparationLstar);
                exactLo = Crossing(st, dust, blackLo);
                exactHi = Crossing(st, dust, blackHi);

                foreach (int c in rowClouds)
                {
                    var series = new double[st.Length];
                    for (int i = 0; i < st.Length; i++) series[i] = cloudAt[i][c];
                    cloudRows.Add(new CloudRow
                    {
                        Name = map.Clouds[c].Name,
                        Patches = map.Clouds[c].Patches.Length,
                        Lstar = asIsDust.CloudLstars[c],
                        ExactTarget = Crossing(st, series, black),
                        MapIndex = c,
                    });
                }
            }
            else
            {
                foreach (int c in rowClouds)
                    cloudRows.Add(new CloudRow { Name = map.Clouds[c].Name, Patches = map.Clouds[c].Patches.Length, Lstar = asIsDust.CloudLstars[c], MapIndex = c });
            }

            SimulationRow recommended = rows.FirstOrDefault(x => x.PrintSafe);
            if (recommended != null) recommended.Recommended = true;

            SimulationRow primary = rows.FirstOrDefault(x => x.Explicit) ?? recommended ?? asIs;

            return new SimulationResult
            {
                BackgroundMeasured = bg,
                Dmax = tonal.Dmax,
                PaperBlackLstar = black,
                Rows = rows,
                AsIs = asIs,
                Recommended = recommended,
                ExactTarget = exact,
                ExactTargetDmaxLower = exactLo,
                ExactTargetDmaxHigher = exactHi,
                VisibilityTarget = visible,
                DmaxDustOnPaperBlackAsIs = asIs.LstarDust > 0 ? -Math.Log10(ColorMath.YOfLstar(asIs.LstarDust)) : double.NaN,
                StepsLifted = steps,
                Primary = primary,
                Map = map,
                AsIsDust = asIsDust,
                Clouds = cloudRows.OrderBy(c => c.Lstar).ThenBy(c => c.Name, StringComparer.Ordinal).ToList(),
            };
        }

        /// <summary>
        /// The target at which the statistic (increasing with the target) reaches
        /// <paramref name="level"/>: the first step at or above the level, interpolated linearly
        /// with the step before. The first step when it already passes; null when not even the
        /// last one reaches it.
        /// </summary>
        /// <param name="targets">The ladder's targets, 0..1, in increasing order.</param>
        /// <param name="values">The statistic at each target, in L*: one per target.</param>
        /// <param name="level">The level to reach, in L*.</param>
        /// <returns>
        /// The target at the crossing, between the first step at or above the level and the step
        /// before it (whose value is under the level, so the interpolation never divides by zero).
        /// </returns>
        internal static double? Crossing(double[] targets, double[] values, double level)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (values[i] < level) continue;
                if (i == 0) return targets[0];
                double span = values[i] - values[i - 1];
                double f = span <= 0 ? 1 : (level - values[i - 1]) / span;
                return targets[i - 1] + f * (targets[i] - targets[i - 1]);
            }
            return null;
        }
    }
}
