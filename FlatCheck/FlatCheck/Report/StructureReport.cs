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
        /// Scara comuna a celor patru teste de abatere de la simetria radiala. Nu se
        /// aplica offsetului centrului de iluminare, care masoara o pozitie.
        /// </summary>
        static void WriteScale(TextWriter w)
        {
            w.WriteLine("\n  SCARA celor patru teste de abatere de mai sus");
            Band(w, $"sub {Th.ResidPerfect:F0} %", "calitate perfecta");
            Band(w, $"{Th.ResidPerfect:F0} - {Th.ResidUsable:F0} %", "utilizabil; background extraction scoate ce ramane");
            Band(w, $"{Th.ResidUsable:F0} - {Th.ResidCheck:F0} %", "utilizabil, dar confirma cu --light inainte de un stretch dur");
            Band(w, $"peste {Th.ResidCheck:F0} %", "refa flat-urile");
            w.WriteLine("    Cel mai important e \"reziduu dupa model rad.\": masoara direct cat din");
            w.WriteLine("    cadru NU se explica printr-un model pur radial, adica exact eroarea de");
            w.WriteLine("    flat. Imprastierea colturilor, asimetria azimutala si inclinarea sunt");
            w.WriteLine("    vederi partiale ale aceleiasi abateri.");
        }

        static void Band(TextWriter w, string range, string meaning)
            => w.WriteLine($"    {range,-13}{meaning}");

        static void WriteAdvice(string verdict, TextWriter w)
        {
            switch (verdict)
            {
                case "PERFECT":
                    w.WriteLine("    Abaterea de la simetria radiala e sub zgomot. Flat-ul e curat.");
                    break;
                case "BUN":
                    w.WriteLine("    Utilizabil ca atare. Ce ramane sta in intervalul pe care background");
                    w.WriteLine("    extraction il scoate fara efort.");
                    break;
                case "ACCEPTABIL":
                    w.WriteLine("    Utilizabil, dar ruleaza modul --light pe un light calibrat inainte de");
                    w.WriteLine("    un stretch dur, ca sa confirmi ca flat-ul chiar corecteaza.");
                    break;
                default:
                    w.WriteLine("    Abatere prea mare de la simetria radiala => sursa neuniforma, light");
                    w.WriteLine("    leak, camera inclinata, sau ambient. NU e vignetare. Refa flat-urile.");
                    w.WriteLine("    Vezi harta reziduala pentru forma problemei.");
                    break;
            }
        }
    }
}
