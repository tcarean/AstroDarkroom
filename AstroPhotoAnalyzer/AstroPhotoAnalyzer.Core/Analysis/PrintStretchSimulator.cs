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

// PrintStretchSimulator.cs - masuratoarea B4: la ce fundal ajunge praful pe negrul
// hartiei, fara sa coboare sub el?
//
// E O SIMULARE, nu un stretch. Duce fundalul MASURAT (mediana canalului de referinta)
// la fiecare tinta a grilei 0.10..0.20 cu o curba gamma identica pe toate canalele, apoi
// remasoara praful si cerul (DustAnalyzer, pe aceeasi harta de dale) si procentele.
// Recomandarea e cel mai mic rand - "asa cum e" inclus, sub sau peste nivelul masurat -
// la care praful sta la sau peste negrul hartiei: nivelul minim folosibil, in ambele
// sensuri (ADR 0025; praful si cerul sunt clasele din ADR 0026). Ce va face stretch-ul adevarat al lui Siril difera in forma curbei,
// si nu putin: un stretch VeraLux la 0.17 a tinut praful la L* 7.6, unde curba gamma il
// prezicea peste negru (ADR 0025). Raportul o numeste simulare de fiecare data.
//
// Tinta exacta nu se cauta prin bisectie, ci pe o SCARA de ridicari din 0.01 in 0.01
// (Thresholds.ExactTarget*), cu interpolare liniara intre doua trepte: aceeasi scara da
// tinta exacta a prafului, a fiecarui nor (116 pe imaginea de referinta - o bisectie pe
// fiecare ar fi 2500 de ridicari) si cele doua tinte la Dmax vecin, din aceleasi 56 de
// ridicari. Ridicarea unei trepte trece printr-o tabela pe 16 biti, nu prin Math.Pow pe
// fiecare esantion: pe o sursa de 16 biti e identic, pe float difera sub 1e-5.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal sealed record SimulationRow
    {
        /// <summary>Fundalul-tinta; pentru randul "asa cum e" e fundalul masurat.</summary>
        internal double Target { get; init; }
        internal bool AsIs { get; init; }

        /// <summary>Randul tintei date de om (Parameters.TargetBackground), cand nu e pe grila.</summary>
        internal bool Explicit { get; init; }
        internal double Gamma { get; init; }

        /// <summary>Cerul: mediana dalelor din afara norilor, sau a tuturor (DustAnalyzer).</summary>
        internal double LstarSky { get; init; }

        /// <summary>Praful: percentila dalelor norilor, sau a tuturor (DustAnalyzer).</summary>
        internal double LstarDust { get; init; }

        /// <summary>"Negru pur": procentul dalelor de 32 px sub negrul hartiei.</summary>
        internal double PctPatchesBelowPaperBlack { get; init; }
        internal double PctBelowLowSeparation { get; init; }
        internal double PctAboveHighlight { get; init; }
        internal double PaperBlackLstar { get; init; }

        /// <summary>Praful la sau peste negrul hartiei. Toleranta e pentru egalitatea in virgula mobila.</summary>
        internal bool DustOnPaperBlack => LstarDust >= PaperBlackLstar - 1e-9;

        /// <summary>
        /// Randul se poate tipari: praful pe negrul hartiei SI cerul vizibil. Un rand cu praful
        /// pe negru si cerul sub prag nu e print-safe - asa il numea tabelul, si omul citea
        /// "print-safe" langa un cer de L* 11 (ADR 0029).
        /// </summary>
        internal bool PrintSafe => DustOnPaperBlack && SkyVisible;

        /// <summary>
        /// Cerul la sau peste pragul de vizibilitate (Thresholds.LowSeparationLstar): hartia
        /// separa si mai jos, dar in lumina de camera un cer sub prag se vede aproape negru
        /// (ADR 0029).
        /// Se compara cum se tipareste in tabel, la o zecimala: un rand care arata "15.0" trece,
        /// altfel tabelul ar pune "cer sub L* 15" langa un cer scris 15.0. Pragul e o regula de
        /// degetul mare; a doua zecimala nu spune nimic.
        /// </summary>
        internal bool SkyVisible => Math.Round(LstarSky, 1) >= Thresholds.LowSeparationLstar;

        /// <summary>Randul recomandat; il marcheaza simulatorul dupa ce a ales.</summary>
        internal bool Recommended { get; set; }
    }

    /// <summary>Un nor catalogat cu destule dale ca sa aiba randul lui, de la cel mai intunecat.</summary>
    internal sealed record CloudRow
    {
        internal string Name { get; init; }
        internal int Patches { get; init; }

        /// <summary>L*-ul norului asa cum e: percentila DustCloudFraction a dalelor lui.</summary>
        internal double Lstar { get; init; }

        /// <summary>Tinta la care norul ajunge pe negrul hartiei; null cand nici ExactTargetHi nu-l aduce.</summary>
        internal double? ExactTarget { get; init; }

        /// <summary>Indicele norului in DustMap.Clouds, pentru harta.</summary>
        internal int MapIndex { get; init; }
    }

    internal sealed record SimulationResult
    {
        internal double BackgroundMeasured { get; init; }
        internal double Dmax { get; init; }
        internal double PaperBlackLstar { get; init; }

        /// <summary>Toate randurile, crescator dupa tinta; "asa cum e" printre ele, la nivelul lui.</summary>
        internal IReadOnlyList<SimulationRow> Rows { get; init; }

        internal SimulationRow AsIs { get; init; }

        /// <summary>Cel mai mic rand print-safe si cu cerul vizibil, "asa cum e" inclus; null cand niciunul nu e.</summary>
        internal SimulationRow Recommended { get; init; }
        internal double? RecommendedTarget => Recommended?.Target;
        internal bool RecommendedIsAsIs => Recommended != null && Recommended.AsIs;

        internal bool AlreadyPrintSafe => AsIs.DustOnPaperBlack;

        /// <summary>Cerul insusi e sub negrul hartiei: cerul si praful s-ar tipari cu acelasi negru (Problema).</summary>
        internal bool SkyBelowPaperBlack => AsIs.LstarSky < PaperBlackLstar;

        /// <summary>Praful e sub negrul hartiei (Atentie cand cerul nu e).</summary>
        internal bool DustBelowPaperBlack => !AsIs.DustOnPaperBlack;

        /// <summary>Cerul e sub pragul de vizibilitate (Atentie, chiar cu praful peste negrul hartiei).</summary>
        internal bool SkyBelowVisibility => !AsIs.SkyVisible;

        /// <summary>Tinta la care cerul ajunge la pragul de vizibilitate, interpolata pe scara; null cand nici ExactTargetHi nu ajunge.</summary>
        internal double? VisibilityTarget { get; init; }

        /// <summary>Tinta exacta la care praful ajunge pe negru, interpolata pe scara; null cand nici ExactTargetHi nu ajunge.</summary>
        internal double? ExactTarget { get; init; }

        /// <summary>Aceeasi tinta exacta la Dmax - DmaxSensitivityStep si la Dmax + DmaxSensitivityStep.</summary>
        internal double? ExactTargetDmaxLower { get; init; }
        internal double? ExactTargetDmaxHigher { get; init; }

        /// <summary>Dmax-ul de la care imaginea trece asa cum e: cel al carui negru e L*-ul prafului. NaN pe praf la L* 0.</summary>
        internal double DmaxToPassAsIs { get; init; }

        /// <summary>Randul principal: tinta data de om, altfel cel recomandat, altfel "asa cum e".</summary>
        internal SimulationRow Primary { get; init; }

        /// <summary>Harta dalelor si statisticile "asa cum e", pentru fereastra si pentru texte.</summary>
        internal DustMap Map { get; init; }
        internal DustStats AsIsDust { get; init; }
        internal DustMethod Method => Map.Method;

        /// <summary>Norii cu cel putin DustCloudMinPatches dale, de la cel mai intunecat; gol fara catalog.</summary>
        internal IReadOnlyList<CloudRow> Clouds { get; init; }

        /// <summary>Cati nori catalogati au macar o dala in cadru.</summary>
        internal int CloudCount => Map.Clouds.Count;

        internal int PatchCount => Map.PatchCount;
        internal int DustPatchCount => AsIsDust.DustPatchCount;

        /// <summary>Cum e definita clasa praf pe calea hartii: "cu 5 L* sub nivelul local al benzii sau sub cerul modelat" sau "cu 3.8 L* sub cerul modelat".</summary>
        string DustClassDefinition => Map.Method == DustMethod.Band
            ? $"cu {Fmt.F0(Thresholds.DustDepthLstar)} L* sub nivelul local al benzii sau sub cerul modelat"
            : $"cu {Fmt.F1(Map.DustThreshold)} L* sub cerul modelat";

        /// <summary>
        /// Ce e praful, dupa cine a dat L*-ul lui (ADR 0033): "percentila 25 % a dalelor de praf
        /// (cu 5 L* sub nivelul local al benzii sau sub cerul modelat; 9.3 % din cadru)" cand
        /// clasa praf decide; altfel "cel mai intunecat 1 % al dalelor de cer", cu de ce nu
        /// clasa praf: goala, sub DustClassMinFraction din cadru, sau mai luminoasa.
        /// </summary>
        internal string DustDescription
        {
            get
            {
                string dustPct = Fmt.Pct(100.0 * DustPatchCount / PatchCount);
                if (AsIsDust.DustSource == DustSource.DustClass)
                    return $"percentila {Fmt.F0(Thresholds.DustClassFraction * 100)} % a dalelor de praf ({DustClassDefinition}; {dustPct} din cadru)";
                string why = DustPatchCount == 0
                    ? "nicio dala de praf"
                    : DustPatchCount < Thresholds.DustClassMinFraction * PatchCount
                        ? $"dalele de praf, {dustPct} din cadru, sunt sub {Fmt.F0(Thresholds.DustClassMinFraction * 100)} % si nu decid"
                        : $"mai intunecat decat percentila {Fmt.F0(Thresholds.DustClassFraction * 100)} % a dalelor de praf, {dustPct} din cadru";
                return $"cel mai intunecat {Fmt.F0(Thresholds.SkyDarkestFraction * 100)} % al dalelor de cer ({why})";
            }
        }

        /// <summary>"mediana dalelor de cer, in afara benzii (|b| peste 10 grade)" sau "mediana dalelor de cer (clase relative la cerul modelat)".</summary>
        internal string SkyDescription => Map.Method == DustMethod.Band
            ? $"mediana dalelor de cer, in afara benzii (|b| peste {Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} grade)"
            : "mediana dalelor de cer (clase relative la cerul modelat)";

        /// <summary>De ce nu s-au facut clasele benzii: "fara WCS", "fara banda galactica in cadru", "cadrul e in intregime in banda"; null cu banda.</summary>
        internal string MethodNote => Map.SkyReason switch
        {
            SkyReason.NoWcs => "fara WCS",
            SkyReason.NoBand => "fara banda galactica in cadru",
            SkyReason.AllBand => "cadrul e in intregime in banda",
            _ => null,
        };

        /// <summary>
        /// "praf 9.3 %, cer 65.1 %, banda 22.4 %, nori luminosi 3.0 %, varfuri 0.1 %"; pe calea
        /// cerului, cu pragurile imaginii: "nebuloasa 12.3 % (peste cerul modelat cu 3.8 L*,
        /// 4 sigma ale cerului), nebuloasa stralucitoare 5.2 % (peste 16.0 L*)".
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
                    nebula += $" (peste cerul modelat cu {Fmt.F1(Map.NebulaThreshold)} L*, {Fmt.F0(Thresholds.SkyNoiseK)} sigma ale cerului)";
                    bright += double.IsPositiveInfinity(Map.BrightThreshold) ? " (nicio dala peste pragul Otsu)" : $" (peste {Fmt.F1(Map.BrightThreshold)} L*, pragul Otsu)";
                }
                return $"{Of(0, PatchClass.Dust)}, {Of(1, PatchClass.Sky)}, {nebula}, {bright}, {Of(4, PatchClass.Peak)}";
            }
        }
    }

    internal static class PrintStretchSimulator
    {
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
            var cloudRows = new List<CloudRow>();
            int[] rowClouds = Enumerable.Range(0, map.Clouds.Count).Where(c => map.Clouds[c].Patches.Length >= Thresholds.DustCloudMinPatches).ToArray();

            // Un fundal la 0 sau 1 n-are gamma; se intampla doar pe rastere sintetice (negru
            // pur), dar nu trebuie sa arunce.
            if (bg > 0 && bg < 1)
            {
                // Scara: treptele exacte, grila si tinta data, o singura ridicare pentru fiecare.
                var ladder = new SortedSet<double>();
                for (double t = Thresholds.ExactTargetLo; t <= Thresholds.ExactTargetHi + 1e-9; t += Thresholds.ExactTargetStep) ladder.Add(Math.Round(t, 6));
                foreach (double t in p.SimulationTargets) ladder.Add(Math.Round(t, 6));
                if (p.TargetBackground.HasValue) ladder.Add(Math.Round(p.TargetBackground.Value, 6));
                double[] targets = ladder.ToArray();

                var dustAt = new double[targets.Length];
                var skyAt = new double[targets.Length];
                var cloudAt = new double[targets.Length][];
                var tableRows = new HashSet<double>(p.SimulationTargets.Select(t => Math.Round(t, 6)));
                if (p.TargetBackground.HasValue) tableRows.Add(Math.Round(p.TargetBackground.Value, 6));
                for (int i = 0; i < targets.Length; i++)
                {
                    double target = targets[i];
                    double gamma = RasterOps.GammaFor(bg, target);
                    Raster lifted = RasterOps.Gamma(sub, gamma);
                    DustStats d = DustAnalyzer.Measure(DustAnalyzer.PatchLstars(lifted), map, black);
                    dustAt[i] = d.LstarDust;
                    skyAt[i] = d.LstarSky;
                    cloudAt[i] = d.CloudLstars;
                    if (!tableRows.Contains(target)) continue;
                    // Subesantionul e deja la pasul tonal; LstarStats primeste pasul 1 ca sa nu-l
                    // subesantioneze inca o data.
                    var (_, _, low, high, _) = TonalAnalyzer.LstarStats(lifted, p.Dmax, stride: 1);
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
                }
                rows = rows.OrderBy(x => x.Target).ToList();

                exact = Crossing(targets, dustAt, black);
                visible = Crossing(targets, skyAt, Thresholds.LowSeparationLstar);
                exactLo = Crossing(targets, dustAt, ColorMath.PaperBlackLstar(p.Dmax - Thresholds.DmaxSensitivityStep));
                exactHi = Crossing(targets, dustAt, ColorMath.PaperBlackLstar(p.Dmax + Thresholds.DmaxSensitivityStep));

                foreach (int c in rowClouds)
                {
                    var series = new double[targets.Length];
                    for (int i = 0; i < targets.Length; i++) series[i] = cloudAt[i][c];
                    cloudRows.Add(new CloudRow
                    {
                        Name = map.Clouds[c].Name,
                        Patches = map.Clouds[c].Patches.Length,
                        Lstar = asIsDust.CloudLstars[c],
                        ExactTarget = Crossing(targets, series, black),
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
                DmaxToPassAsIs = asIs.LstarDust > 0 ? -Math.Log10(ColorMath.YOfLstar(asIs.LstarDust)) : double.NaN,
                Primary = primary,
                Map = map,
                AsIsDust = asIsDust,
                Clouds = cloudRows.OrderBy(c => c.Lstar).ThenBy(c => c.Name, StringComparer.Ordinal).ToList(),
            };
        }

        /// <summary>
        /// Tinta la care statistica (crescatoare cu tinta) atinge <paramref name="level"/>:
        /// prima treapta la sau peste nivel, interpolata liniar cu treapta de dinainte. Prima
        /// treapta cand si ea trece; null cand nici ultima nu ajunge.
        /// </summary>
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
