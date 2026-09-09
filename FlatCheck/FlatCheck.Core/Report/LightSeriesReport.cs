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

// LightSeriesReport.cs - LightSeriesResult -> textul din consola. Doar formatare.
//
// Trei intrari in loc de una: o serie de 32 de cadre ia vreo doua minute, si a sta cu
// consola goala tot timpul ala e o purtare proasta pentru o unealta de linie de
// comanda. Deci CLI-ul scrie antetul, apoi cate un rand pe masura ce fiecare cadru e
// gata, apoi sinteza. Formatarea ramane totusi in intregime aici, ca la celelalte
// moduri - Report/ e singurul loc cu text romanesc.

using System;
using System.IO;
using System.Linq;

namespace FlatCheck
{
    internal static class LightSeriesReport
    {
        internal static void WriteHeader(
            string dir, string pattern, int count, SuitabilityResult s, TextWriter w)
        {
            w.WriteLine($"\n=== TEST END-TO-END (SERIE): {dir} ===");
            w.WriteLine("    Astept cadre CALIBRATE cu flat, FARA background extraction si NEREGISTRATE.");
            w.WriteLine($"    cadre gasite            : {count}  (tipar {pattern})");
            LightReport.WriteSuitability(s, w);
            w.WriteLine();
            w.WriteLine("    cadru                              radial   gradient cer     tile-uri");
        }

        internal static void WriteRow(string name, LightResult r, TextWriter w)
            => w.WriteLine($"    {Trim(name),-30} {LightReport.Signed(r.RadPct),8} % "
                         + $"{r.PlanePct,10:F2} %  {r.KeptTiles,6}/{r.TotalTiles}");

        internal static void WriteSummary(LightSeriesResult r, TextWriter w)
        {
            w.WriteLine("\n  SINTEZA PE SERIE");
            w.WriteLine($"    componenta radiala, medie : {LightReport.Signed(r.MeanRad),7} %");
            w.WriteLine($"    imprastiere (min .. max)  : {LightReport.Signed(r.MinRad),7} .. {LightReport.Signed(r.MaxRad)} %");
            w.WriteLine($"    eroarea mediei            : {r.Sem,7:F2} puncte"
                      + (Failed(r, "sem") ? $"   <- peste pragul de {Th.LightSeriesSemMax:F2}" : ""));

            if (r.CorrApplies)
                w.WriteLine($"    corelatie cu grad. de cer : {r.SkyCorr,7:F2}"
                          + (Failed(r, "corelatie") ? $"          <- peste pragul de {Th.LightSeriesSkyCorr:F2}" : ""));
            else
                w.WriteLine($"    corelatie cu grad. de cer : nu se aplica (gradientul variaza doar "
                          + $"{r.SkySpan:F1} puncte)");

            w.WriteLine($"\n    VERDICT: {r.Verdict}");

            if (r.Conclusive)
            {
                LightReport.WriteDiagnosis(r.MeanRad, w);
                w.WriteLine($"\n    Media pe {r.Count} cadre e estimarea erorii de flat: imprastierea e mica,");
                w.WriteLine("    deci termenul radial al cerului e stabil peste serie si nu o contamineaza.");
                return;
            }

            w.WriteLine("    Media NU se poate citi ca eroare de flat:");
            foreach (string reason in r.Reasons) WriteReason(r, reason, w);

            w.WriteLine("\n    Flat-ul e acelasi pe toate cadrele prin constructie - s-a impartit din");
            w.WriteLine("    fiecare. Deci tot ce se misca in timpul seriei e cer, nu flat.");
            w.WriteLine("    Pe date ca astea raspunsul il da testul de rotatie 180 (--ratio), care");
            w.WriteLine("    valideaza flat-ul fara sa implice cerul deloc. Vezi README, sectiunea");
            w.WriteLine("    despre cand se poate folosi --light.");
        }

        static void WriteReason(LightSeriesResult r, string reason, TextWriter w)
        {
            switch (reason)
            {
                case "sem":
                    w.WriteLine($"      - eroarea mediei ({r.Sem:F2}) depaseste chiar pragul de OK ({Th.LightRadOk:F2}),");
                    w.WriteLine("        deci seria nu rezolva flat-ul la precizia la care se pronunta");
                    w.WriteLine("        pragurile. Un verdict aici ar fi o parere, nu o masuratoare.");
                    break;
                case "corelatie":
                    w.WriteLine($"      - componenta radiala urmareste gradientul de cer (r = {r.SkyCorr:F2}");
                    w.WriteLine($"        pe un interval de {r.SkySpan:F0} puncte de gradient), semn ca ce se");
                    w.WriteLine("        masoara e cerul.");
                    break;
                default:
                    w.WriteLine($"      - {reason}.");
                    break;
            }
        }

        static bool Failed(LightSeriesResult r, string reason) => r.Reasons.Contains(reason);

        /// Numele de fisier lungi ar rupe coloanele; se taie de la stanga, pastrand
        /// coada, fiindca acolo sta numarul cadrului.
        static string Trim(string name)
            => name.Length <= 30 ? name : "..." + name.Substring(name.Length - 27);
    }
}
