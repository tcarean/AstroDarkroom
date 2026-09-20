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

// PrintStretchSimulator.cs - masuratoarea B4: la ce fundal-tinta ar deveni imaginea
// print-safe?
//
// E O SIMULARE, nu un stretch. Ridica fundalul MASURAT (mediana canalului de referinta)
// pana la fiecare tinta cu o curba gamma identica pe toate canalele, apoi refac
// statisticile L*. Raspunsul pe care il da e "re-stretch-uieste din liniar la X" - X
// fiind cea mai mica tinta la care procentul de sub negrul hartiei coboara sub prag.
// Ce va face stretch-ul adevarat al lui Siril difera in forma curbei, dar nu in ce
// conteaza aici: unde ajunge fundalul fata de negrul hartiei.

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
        internal double Gamma { get; init; }
        internal double LstarMedian { get; init; }
        internal double PctBelowPaperBlack { get; init; }
        internal double PctBelowLowSeparation { get; init; }
        internal double PctAboveHighlight { get; init; }
        internal bool PrintSafe => PctBelowPaperBlack < Thresholds.PaperBlackOkPct;
    }

    internal sealed record SimulationResult
    {
        internal double BackgroundMeasured { get; init; }
        internal double PaperBlackLstar { get; init; }

        /// <summary>Randul "asa cum e" primul, apoi tintele crescator.</summary>
        internal IReadOnlyList<SimulationRow> Rows { get; init; }

        internal bool AlreadyPrintSafe => Rows[0].PrintSafe;

        /// <summary>Cea mai mica tinta print-safe, sau null daca niciuna (ori daca nu e nevoie).</summary>
        internal double? RecommendedTarget { get; init; }

        /// <summary>Randul tintei principale din parametri - cel pe care il arata raportul in prim-plan.</summary>
        internal SimulationRow Primary { get; init; }
    }

    internal static class PrintStretchSimulator
    {
        internal static SimulationResult Simulate(Raster r, TonalResult tonal, Parameters p)
        {
            double bg = tonal.BackgroundMedian;
            Raster sub = RasterOps.Subsample(r, Thresholds.TonalStride);

            var rows = new List<SimulationRow>
            {
                new SimulationRow
                {
                    Target = bg, AsIs = true, Gamma = 1,
                    LstarMedian = tonal.LstarMedian,
                    PctBelowPaperBlack = tonal.PctBelowPaperBlack,
                    PctBelowLowSeparation = tonal.PctBelowLowSeparation,
                    PctAboveHighlight = tonal.PctAboveHighlight,
                },
            };

            var targets = p.SimulationTargets.Append(p.TargetBackground).Distinct().OrderBy(t => t).ToArray();
            foreach (double target in targets)
            {
                // Un fundal la 0 sau 1 n-are gamma; se intampla doar pe rastere sintetice
                // (negru pur), dar nu trebuie sa arunce.
                if (bg <= 0 || bg >= 1) break;
                double gamma = RasterOps.GammaFor(bg, target);
                // Subesantionul e deja la pasul tonal; LstarStats primeste pasul 1 ca sa nu-l
                // subesantioneze inca o data.
                var (median, black, low, high, _) = TonalAnalyzer.LstarStats(RasterOps.Gamma(sub, gamma), p.Dmax, stride: 1);
                rows.Add(new SimulationRow
                {
                    Target = target, Gamma = gamma,
                    LstarMedian = median, PctBelowPaperBlack = black,
                    PctBelowLowSeparation = low, PctAboveHighlight = high,
                });
            }

            bool alreadySafe = rows[0].PrintSafe;
            double? recommended = alreadySafe
                ? null
                : rows.Skip(1).Where(x => x.PrintSafe && x.Target > bg).Select(x => (double?)x.Target).FirstOrDefault();

            SimulationRow primary = rows.Skip(1).FirstOrDefault(x => Math.Abs(x.Target - p.TargetBackground) < 1e-9) ?? rows[0];

            return new SimulationResult
            {
                BackgroundMeasured = bg,
                PaperBlackLstar = tonal.PaperBlackLstar,
                Rows = rows,
                RecommendedTarget = recommended,
                Primary = primary,
            };
        }
    }
}
