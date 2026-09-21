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

// TonalAnalyzer.cs - masuratorile B1 si B2: unde stau tonurile, si cate din ele cad
// sub negrul hartiei.
//
// Doua scari, deliberat. B1 e in NIVELURI (0..255), limbajul histogramei si al oricarui
// editor - "96 % din pixeli sub 64". B2 e in L*, limbajul hartiei: negrul unui mediu e
// un L* (din Dmax), si ce e sub el se printeaza cu acelasi negru. Pe gri trecerea e o
// curba fixa (ColorMath.GrayLevelOfLstar, tabela LstarLadder); pe culoare acelasi nivel
// da alt L* dupa nuanta, iar B1 numara esantioane pe canale, B2 pixeli - de asta se
// raporteaza amandoua.
//
// Pasii de subesantionare sunt cei ai scripturilor de acceptanta (Thresholds.*Stride).

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal sealed record TonalResult
    {
        /// <summary>Media fiecarui plan, in niveluri 0..255, pe toti pixelii.</summary>
        internal IReadOnlyList<double> MeanLevels { get; init; }

        /// <summary>Procentul esantioanelor (toate planurile) sub Thresholds.DarkLevelA si DarkLevelB.</summary>
        internal double PctBelowDarkA { get; init; }
        internal double PctBelowDarkB { get; init; }

        /// <summary>Percentilele canalului de referinta, in niveluri, la PercentileRanks.</summary>
        internal IReadOnlyList<double> ReferencePercentiles { get; init; }
        internal static readonly double[] PercentileRanks = { 0.1, 1, 50, 90, 99, 99.9, 100 };

        /// <summary>Mediana canalului de referinta, 0..1 - "fundalul" pe care il ridica simularea.</summary>
        internal double BackgroundMedian { get; init; }

        internal double Dmax { get; init; }
        internal double PaperBlackLstar { get; init; }

        internal double LstarMedian { get; init; }
        internal double PctBelowPaperBlack { get; init; }
        internal double PctBelowLowSeparation { get; init; }
        internal double PctAboveHighlight { get; init; }

        /// <summary>Histograma L*, 101 cosuri (0..100), procente - pentru fereastra.</summary>
        internal IReadOnlyList<double> LstarHistogramPct { get; init; }

        internal bool BackgroundTooLowForPaper => PctBelowPaperBlack > Thresholds.PaperBlackWarnPct;
        internal bool PrintSafe => PctBelowPaperBlack < Thresholds.PaperBlackOkPct;
    }

    /// <summary>
    /// Un rand al tabelei nivel - L*: un reper al tabului, cu L*-ul lui si nivelul de 8 biti
    /// al unui gri cu acelasi L*. Chosen = negrul mediului pe care se judeca imaginea.
    /// </summary>
    internal sealed record LstarLadderRow(double Lstar, double GrayLevel, string Meaning, bool Chosen = false);

    internal static class TonalAnalyzer
    {
        /// <summary>
        /// Tabela de corespondenta nivel - L* pentru reperele tabului: negrul celor trei
        /// medii, pragurile in niveluri (32/64), pragurile in L* (15/50) si fundalul-tinta.
        /// Nu masoara nimic din imagine - e fereastra care o arata, ca sa se poata citi un
        /// prag in L* pe histograma din Siril. Valabila pe gri; pe culoare acelasi nivel da
        /// alt L* dupa nuanta. Crescator dupa L*.
        /// </summary>
        internal static IReadOnlyList<LstarLadderRow> LstarLadder(Parameters p)
        {
            var rows = new List<LstarLadderRow>();
            bool chosenIsMedium = false;
            foreach (PrintMedium m in Enum.GetValues(typeof(PrintMedium)))
            {
                double dmax = Parameters.DmaxOf(m);
                bool chosen = Math.Abs(dmax - p.Dmax) < 1e-9;
                chosenIsMedium |= chosen;
                rows.Add(FromLstar(ColorMath.PaperBlackLstar(dmax), $"negrul hartiei: {Parameters.NameOf(m)} (Dmax {Fmt.F1(dmax)})", chosen));
            }
            if (!chosenIsMedium)
                rows.Add(FromLstar(ColorMath.PaperBlackLstar(p.Dmax), $"negrul hartiei: Dmax explicit {Fmt.F2(p.Dmax)}", true));
            rows.Add(FromLevel(Thresholds.DarkLevelA, $"pragul \"sub nivelul {Thresholds.DarkLevelA}\""));
            rows.Add(FromLevel(Thresholds.DarkLevelB, $"pragul \"sub nivelul {Thresholds.DarkLevelB}\""));
            rows.Add(FromLstar(Thresholds.LowSeparationLstar, "separare slaba"));
            rows.Add(FromLstar(Thresholds.HighlightLstar, "lumini"));
            rows.Add(FromLevel(p.TargetBackground * 255, $"fundalul-tinta {Fmt.F2(p.TargetBackground)}"));
            return rows.OrderBy(r => r.Lstar).ToList();

            static LstarLadderRow FromLstar(double l, string meaning, bool chosen = false)
                => new(l, ColorMath.GrayLevelOfLstar(l), meaning, chosen);
            static LstarLadderRow FromLevel(double level, string meaning)
                => new(ColorMath.LstarOfGrayLevel(level), level, meaning);
        }

        internal static TonalResult Analyze(Raster r, Parameters p)
        {
            // B1: pe toti pixelii - medii si numaratori, o singura trecere pe plan.
            var means = new double[r.Planes.Length];
            long belowA = 0, belowB = 0, total = 0;
            float a = Thresholds.DarkLevelA / 255f, b = Thresholds.DarkLevelB / 255f;
            for (int pl = 0; pl < r.Planes.Length; pl++)
            {
                float[] plane = r.Planes[pl];
                double s = 0;
                for (int i = 0; i < plane.Length; i++)
                {
                    float v = plane[i];
                    s += v;
                    // "sub nivelul 32" inseamna octetul inalt < 32, ca in script (hi < 32).
                    if (v < a) belowA++;
                    if (v < b) belowB++;
                }
                means[pl] = s / plane.Length * 255;
                total += plane.Length;
            }

            float[] refSorted = Stats.Sorted(RasterOps.Subsample(r, Thresholds.PercentileStride).Reference);
            var pct = new double[TonalResult.PercentileRanks.Length];
            for (int i = 0; i < pct.Length; i++)
                pct[i] = Stats.PercentileSorted(refSorted, TonalResult.PercentileRanks[i]) * 255;

            double bg = Stats.Median(RasterOps.Subsample(r, Thresholds.TonalStride).Reference);

            // B2: L* pe subesantion.
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
        /// Statisticile L* ale unui raster, la pasul dat (implicit cel tonal). Publica
        /// pentru simulator, care le recalculeaza pe un raster DEJA subesantionat si
        /// ridicat - de aceea pasul e parametru: altfel s-ar subesantiona de doua ori.
        /// </summary>
        internal static (double Median, double PctBelowPaperBlack, double PctBelowLow, double PctAboveHigh, double[] HistPct)
            LstarStats(Raster r, double dmax, int stride = Thresholds.TonalStride)
        {
            float[] L = RasterOps.LstarPlane(r, stride);
            double black = ColorMath.PaperBlackLstar(dmax);
            long nBlack = 0, nLow = 0, nHigh = 0;
            var hist = new double[101];
            for (int i = 0; i < L.Length; i++)
            {
                float l = L[i];
                if (l < black) nBlack++;
                if (l < Thresholds.LowSeparationLstar) nLow++;
                if (l > Thresholds.HighlightLstar) nHigh++;
                hist[Math.Clamp((int)Math.Round(l), 0, 100)]++;
            }
            for (int i = 0; i < hist.Length; i++) hist[i] = 100.0 * hist[i] / L.Length;
            double median = Stats.Median(L);
            return (median, 100.0 * nBlack / L.Length, 100.0 * nLow / L.Length, 100.0 * nHigh / L.Length, hist);
        }
    }
}
