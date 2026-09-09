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

// StructureReport.cs - StructureResult -> textul din consola. Doar formatare.

using System.Collections.Generic;
using System.IO;

namespace FlatCheck
{
    internal static class StructureReport
    {
        internal static void Write(StructureResult r, string path, TextWriter w)
        {
            w.WriteLine($"\n=== STRUCTURA: {path} ===");
            w.WriteLine($"    dimensiune analiza : {r.Width} x {r.Height} (dupa 2x2 + downsample)");
            w.WriteLine($"    median / max ADU   : {r.RawMedian:F0} / {r.RawMax:F0}");
            WriteBody(r, w);
        }

        /// <summary>
        /// Tot ce urmeaza dupa antet: informativele, cele patru teste, scara si
        /// verdictul. Scos separat fiindca modul de atribuire ruleaza aceeasi baterie
        /// pe masterul combinat (A+B)/2 si vrea exact acest corp sub antetul lui - dar
        /// nu si randul de ADU, care pe un raster normalizat la mediana 1 n-ar insemna
        /// nimic.
        /// </summary>
        internal static void WriteBody(StructureResult r, TextWriter w)
        {
            WriteInformative(r, w);

            w.WriteLine("\n  TESTE");
            w.WriteLine($"    imprastiere 4 colturi   : {r.CornerSpreadPct,5:F2} %   [{r.CornerVerdict}]");
            foreach (var kv in r.Corners)
                w.WriteLine($"        {kv.Key,-14}: {kv.Value / r.CornerMean * 100,6:F2} % din media colturilor");
            w.WriteLine($"    asimetrie azimutala max : {r.AzimuthalMaxPct,5:F2} %   [{r.AzimuthalVerdict}]");
            w.WriteLine($"    reziduu dupa model rad. : {r.ResidualPtpPct,5:F2} %   [{r.ResidualVerdict}]  <- CEL MAI IMPORTANT");
            w.WriteLine($"    inclinare (gradient)    : {r.TiltPct,5:F2} %   [{r.TiltVerdict}]");

            WriteScale(w);

            w.WriteLine($"\n  VERDICT: {r.Verdict}");
            WriteAdvice(r.Verdict, w);
        }

        /// <summary>
        /// Cele doua marimi care NU sunt teste. Vignetarea e o proprietate a
        /// obiectivului, nu o eroare. Offsetul centrului de iluminare a fost si el un
        /// test pana in 1.2.0 si nu mai e: fiind ~ -b/(2d), adica raportul dintre
        /// gradient si curbura, nu e scale-invariant - iar peste pragul la care varful
        /// iese din cadru, nici macar monoton. Vezi Metrics.FitCenter.
        /// </summary>
        static void WriteInformative(StructureResult r, TextWriter w)
        {
            w.WriteLine("\n  INFORMATIV (nu sunt teste)");
            w.WriteLine($"    vignetare centru->colt  : {r.VignetteStops:F2} stopuri (colt = {r.CornerMean / r.CentreMedian * 100:F0}% din centru)");
            if (r.CentroidValid)
            {
                w.WriteLine($"    offset centru iluminare : {r.CentroidOffsetPct,5:F2} %");
                w.WriteLine($"        varf iluminare = ({r.FitCx:F0}, {r.FitCy:F0})  vs  centru senzor = ({r.SensorCx:F0}, {r.SensorCy:F0})");
            }
            else
            {
                w.WriteLine("    offset centru iluminare : nedeterminat (varf in afara cadrului sau fit degenerat)");
            }
            w.WriteLine("        E raportul dintre gradient si curbura, deci nu e scale-invariant: pe un");
            w.WriteLine("        obiectiv aproape fara vignetare creste mult fara ca eroarea sa creasca,");
            w.WriteLine("        iar peste un prag cade inapoi spre 0. Amplitudinea o da \"inclinare\".");
        }

        /// <summary>
        /// Scara comuna a celor patru teste de abatere de la simetria radiala: numele
        /// benzii, intervalul si intelesul. Nu se aplica offsetului centrului de
        /// iluminare, care masoara o pozitie.
        ///
        /// Se intoarce ca date, nu ca text deja scris, fiindca o citesc doi consumatori:
        /// raportul de mai jos, care o insira in coloane, si aplicatia WPF, care o pune
        /// intr-un tabel cu banda curenta evidentiata. Pragurile raman singurele din Th;
        /// o a doua lista de benzi ar fi divergat la prima recalibrare.
        /// </summary>
        internal static IReadOnlyList<(string Band, string Range, string Meaning)> Scale()
            => new[]
            {
                ("PERFECT", $"sub {Th.ResidPerfect:F0} %",
                            "calitate perfecta"),
                ("BUN", $"{Th.ResidPerfect:F0} - {Th.ResidUsable:F0} %",
                        "utilizabil; background extraction scoate ce ramane"),
                ("ACCEPTABIL", $"{Th.ResidUsable:F0} - {Th.ResidCheck:F0} %",
                               "utilizabil, dar confirma cu --light inainte de un stretch dur"),
                ("REFA", $"peste {Th.ResidCheck:F0} %",
                         "refa flat-urile"),
            };

        /// <summary>
        /// De ce reziduul dupa modelul radial e cel care conteaza. Scos separat din
        /// acelasi motiv ca <see cref="Scale"/>: il arata si fereastra, sub tabel.
        /// </summary>
        internal static string WhyResidualMatters()
            => "Cel mai important e \"reziduu dupa model rad.\": masoara direct cat din\n"
             + "cadru NU se explica printr-un model pur radial. E o LIMITA SUPERIOARA a\n"
             + "erorii de flat, nu eroarea insasi - cuprinde si partea fixa pe aparat\n"
             + "(optica; se imparte corect din lights) si partea fixa in lume (iluminarea;\n"
             + "nu se reproduce). Modul --ratio le desparte. Imprastierea colturilor,\n"
             + "asimetria azimutala si inclinarea sunt vederi partiale ale aceleiasi abateri.";

        static void WriteScale(TextWriter w)
        {
            w.WriteLine("\n  SCARA celor patru teste de abatere de mai sus");
            foreach (var (_, range, meaning) in Scale())
                w.WriteLine($"    {range,-13}{meaning}");
            Indent(WhyResidualMatters(), w);
        }

        /// <summary>
        /// Ce sa faca omul cu verdictul asta. Intors ca sir, nu scris direct, fiindca il
        /// arata si fereastra, sub verdictul colorat - iar doua formulari ale aceluiasi
        /// sfat ar diverge exact acolo unde conteaza cel mai mult sa nu divergem.
        /// </summary>
        internal static string Advice(string verdict) => verdict switch
        {
            "PERFECT" =>
                "Abaterea de la simetria radiala e sub zgomot. Flat-ul e curat.",

            "BUN" =>
                "Utilizabil ca atare. Ce ramane sta in intervalul pe care background\n"
              + "extraction il scoate fara efort.",

            "ACCEPTABIL" =>
                "Utilizabil, dar ruleaza modul --light pe un light calibrat inainte de\n"
              + "un stretch dur, ca sa confirmi ca flat-ul chiar corecteaza.",

            _ =>
                "Abatere prea mare de la simetria radiala => sursa neuniforma, light\n"
              + "leak, camera inclinata, sau ambient. NU e vignetare. Refa flat-urile.\n"
              + "Vezi harta reziduala pentru forma problemei.",
        };

        static void WriteAdvice(string verdict, TextWriter w) => Indent(Advice(verdict), w);

        static void Indent(string text, TextWriter w)
        {
            foreach (string line in text.Split('\n')) w.WriteLine("    " + line);
        }
    }
}
