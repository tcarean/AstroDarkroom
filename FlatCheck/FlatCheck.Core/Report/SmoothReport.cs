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

// SmoothReport.cs - SmoothResult -> textul din consola. Doar formatare.
//
// Modul n-are verdict: fisierul se scrie oricum. Ce spune raportul e CE S-A SCOS - singura
// cifra dupa care omul poate decide daca fisierul e de incredere - si bateria de structura
// inainte si dupa, ca sa se vada ca ce era al obiectivului a ramas. Textele de ecran
// (Caveat, SirilNote, refuzurile) stau tot aici, ca valori, din regula obisnuita: nicio
// propozitie pentru utilizator nu se scrie intr-un ViewModel.

using System.IO;
using System.Linq;

namespace FlatCheck
{
    internal static class SmoothReport
    {
        internal static void Write(SmoothResult r, string path, TextWriter w)
        {
            w.WriteLine($"\n=== NETEZIRE: {path} ===");
            w.WriteLine($"    cadru              : {FrameLine(r)}");
            w.WriteLine($"    scara de netezire  : {ScaleLine(r)}");
            w.WriteLine($"    dale respinse      : {RejectedLine(r)}");

            w.WriteLine($"\n  CE S-A SCOS (original / netezit, la rezolutia de analiza {r.Width} x {r.Height})");
            foreach (string line in RemovedLines(r)) w.WriteLine("    " + line);
            string warning = Warning(r);
            if (warning != null) Indent(warning, w);

            w.WriteLine("\n  INAINTE / DUPA - cele cinci teste de structura");
            foreach (string key in StructureReport.TestKeys)
            {
                var (vb, bb) = StructureReport.Test(r.Before, key);
                var (va, ba) = StructureReport.Test(r.After, key);
                w.WriteLine($"    {StructureReport.TestLabel(key),-24}: {vb,5:F2} % [{bb}]  ->  {va,5:F2} % [{ba}]");
            }
            w.WriteLine($"    {"VERDICT",-24}: {r.Before.Verdict}  ->  {r.After.Verdict}");

            w.WriteLine();
            Indent("INFORMATIV: " + Caveat(), w);
            Indent(SirilNote(), w);
        }

        /// <summary>"6016 x 4016, CFA (4 pozitii Bayer netezite separat)" / "..., mono/procesat".</summary>
        internal static string FrameLine(SmoothResult r)
            => $"{r.SourceW} x {r.SourceH}, " + (r.IsCfa
                ? "CFA (cele 4 pozitii Bayer netezite separat)"
                : "mono/procesat (fara mozaic CFA)");

        /// <summary>"300 px pe senzor (dale de 150 px, nucleu gaussian sigma 2.0 dale)".</summary>
        internal static string ScaleLine(SmoothResult r)
            => $"{r.ScalePx:F0} px pe senzor (dale de {r.TilePx} px, nucleu gaussian sigma {r.SigmaTiles:F1} dale)";

        /// <summary>"niciuna din 1107 pe pozitie" / "66, 58, 57, 69 din 1107 pe pozitie".</summary>
        internal static string RejectedLine(SmoothResult r)
        {
            int tiles = r.Positions[0].Tiles;
            string counts = r.Positions.All(p => p.Rejected == 0)
                ? "niciuna"
                : string.Join(", ", r.Positions.Select(p => p.Rejected.ToString()));
            string per = r.IsCfa ? " pe pozitie" : "";
            string failed = r.Positions.Sum(p => p.FitFailed) > 0
                ? $"; {r.Positions.Sum(p => p.FitFailed)} dale fara fit (au ramas la mediana lor)"
                : "";
            return $"{counts} din {tiles}{per} (praf, pete, textura peste {SmoothAnalyzer.ClipFloor * 100:F0} %){failed}";
        }

        /// <summary>Cele patru cifre despre componenta scoasa, un rand fiecare.</summary>
        internal static string[] RemovedLines(SmoothResult r) => new[]
        {
            $"p1 / p99 pe pixel        : {r.RemovedP1Pct:+0.00;-0.00} % / {r.RemovedP99Pct:+0.00;-0.00} %  (zgomotul de pixel intra aici)",
            $"structura la scara mare  : {r.RemovedPtpPct,5:F2} %  (p95 - p5 pe dale)",
            $"dala cea mai abatuta     : {r.RemovedMaxTilePct,5:F2} %",
            $"pata cea mai adanca      : {StructureReport.LocalSpotValue(r.RemovedSpotPct, r.RemovedSpotX, r.RemovedSpotY)}",
        };

        /// <summary>
        /// Atentionarea cand s-a scos mai mult decat textura, sau null. Pragul se citeste
        /// din Th, nu se scrie, ca textul sa nu ramana in urma unei recalibrari.
        /// </summary>
        internal static string Warning(SmoothResult r)
            => !r.RemovedFlagged ? null
             : $"ATENTIE: dala cea mai abatuta a componentei scoase are {r.RemovedMaxTilePct:F2} %, peste {Th.SmoothKeptWarn:F0} %.\n"
             + "Netezirea a luat si ceva de scara mare, nu doar textura: praf adanc, o pata, sau o\n"
             + "structura reala mai ingusta decat scara aleasa. Uita-te in harta componentei scoase\n"
             + "inainte sa folosesti fisierul; o scara mai mica pastreaza mai mult.";

        /// <summary>Ce inseamna fisierul, si ce NU stie sa faca. Sta in raport si sub harta.</summary>
        internal static string Caveat()
            => "fisierul netezit pastreaza TOT ce e neted in master si scoate tot ce nu e. Nu stie\n"
             + "ce e al obiectivului si ce e al sursei: petele unui monitor, gradientul cerului sau\n"
             + "colturile inegale ale unui panou raman, fiindca sunt netede; doar --ratio le desparte.\n"
             + "Ce scoate - textura, praful, petele compacte - poate fi si ceva dorit: o umbra de\n"
             + "praf e corecta pentru sesiunea ale carei lights o au si ele. O structura reala mai\n"
             + "ingusta decat scara (~300 px) e luata partial; scara e singurul buton.";

        /// <summary>Cum ajunge fisierul in Siril: scripturile nu-l iau singure.</summary>
        internal static string SirilNote()
            => "Scripturile Siril numesc pp_flat_stacked explicit, deci nu iau fisierul netezit singure:\n"
             + "redenumeste-l in locul masterului (pastreaza originalul) sau da-l la calibrate cu -flat=.";

        /// <summary>Refuzul pentru un raw de aparat.</summary>
        internal static string RefusalRaw(string path)
            => $"NETEZIRE: {path} e un raw de aparat - un singur cadru, necalibrat.\n"
             + "Modul vrea masterul de flat stivuit si calibrat (pp_flat_stacked.fit). Pentru un\n"
             + "raw exista --expo.";

        /// <summary>Refuzul pentru un cub de culoare (NAXIS3 > 1).</summary>
        internal static string RefusalCube(string path, int planes)
            => $"NETEZIRE: {path} are {planes} planuri de culoare (debayerizat).\n"
             + "Cititorul le-ar media intr-un singur raster, si scris inapoi ca mono fisierul ar\n"
             + "pierde doua canale. Stivuieste flat-urile ca CFA (fara debayerizare) si reia.";

        /// <summary>"-> fisier salvat: ...", perechea lui RunReport.WriteMapSaved pentru FITS.</summary>
        internal static string SavedLine(string path) => $"    -> flat netezit salvat: {path}";

        static void Indent(string text, TextWriter w)
        {
            foreach (string line in text.Split('\n')) w.WriteLine("    " + line);
        }
    }
}
