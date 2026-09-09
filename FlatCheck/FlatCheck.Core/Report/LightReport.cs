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

// LightReport.cs - LightResult -> textul din consola. Doar formatare.
//
// Semnul componentei radiale E diagnosticul, de aceea se scrie mereu explicit:
// pozitiv = fondul cade spre margini = sub-corectie; negativ = creste = supra-corectie.

using System;
using System.IO;

namespace FlatCheck
{
    internal static class LightReport
    {
        /// <summary>
        /// Numarul cu semnul scris explicit, ca "%+.2f" din Python.
        ///
        /// NU folosi formatul cu sectiuni "+0.00;-0.00": pentru zero negativ .NET alege
        /// sectiunea POZITIVA (fiindca -0.0 nu e &lt; 0) dar tot adauga minusul lui, si iese
        /// "-+0.00". Se intampla exact pe un fond fara componenta radiala, adica pe cazul
        /// bun. Python scria "-0.00" acolo, deci era si o divergenta intre implementari.
        ///
        /// IsNegative, nu "&lt; 0", ca sa se pastreze semnul lui -0.0 - la fel ca Python.
        /// </summary>
        internal static string Signed(double v)
            => (double.IsNegative(v) ? "-" : "+") + Math.Abs(v).ToString("0.00");

        /// <summary>
        /// Linia de camp acoperit, plus avertismentul cand e la limita. Folosita si de
        /// raportul pe serie - de aceea sta aici, nu inline in Write.
        ///
        /// Cand campul nu s-a putut afla se scrie o linie care spune exact asta. E
        /// deliberat: necunoscut nu inseamna suspect, inseamna neverificat, iar tacerea
        /// ar lasa impresia ca s-a verificat.
        /// </summary>
        internal static void WriteSuitability(SuitabilityResult s, TextWriter w)
        {
            if (!double.IsNaN(s.FovDeg))
                w.WriteLine($"    camp diagonal           : {s.FovDeg,5:F1} grade  ({s.Source})");
            else if (!double.IsNaN(s.FocalMm))
                w.WriteLine($"    camp acoperit           : nedeterminat ({s.Source})");
            else
                w.WriteLine("    camp acoperit           : necunoscut (headerul nu da nici focala, nici scara)");

            if (s.Level == FovLevel.Marginal)
            {
                w.WriteLine($"\n    ATENTIE: camp la limita. Separarea plan/radial slabeste peste {Th.LightFovOk:F0}");
                w.WriteLine("    grade diagonala, iar cifra de mai jos poate contine si cer, nu doar flat.");
                w.WriteLine("    Citeste-o ca pe un indiciu, nu ca pe o masuratoare.");
            }
            else if (s.Level == FovLevel.Unknown)
            {
                w.WriteLine("    (nu pot verifica daca testul se aplica; vezi --fov si README)");
            }
        }

        /// <summary>
        /// Ce se scrie in locul unui raport cand campul e prea larg. Modul nu da un
        /// verdict aici - nu are ce masura - deci explica degenerarea si trimite la
        /// testul care chiar raspunde pe asemenea date.
        /// </summary>
        internal static void WriteTooWide(SuitabilityResult s, string path, TextWriter w)
        {
            w.WriteLine($"\n=== TEST END-TO-END: {path} ===");
            WriteSuitability(s, w);

            w.WriteLine("\n  TESTUL NU SE APLICA PE CADRELE ASTEA");
            w.WriteLine($"    Peste {Th.LightFovMax:F0} de grade diagonala, cupola de poluare luminoasa se");
            w.WriteLine("    CURBEAZA in interiorul cadrului. Partea radial simetrica a acelei curburi");
            w.WriteLine("    are exact forma vignetarii, deci fitul nu le mai poate separa: orice cifra");
            w.WriteLine("    ar iesi ar fi cerul, nu flat-ul.");
            w.WriteLine("    Nu e o limitare a uneltei, e o degenerare geometrica.");
            w.WriteLine("\n    Pentru un setup cu camp larg, testul care raspunde e rotatia la 180 de");
            w.WriteLine("    grade (--ratio): valideaza flat-ul fara sa implice cerul deloc.");
            if (double.IsNaN(s.FovDeg) && !double.IsNaN(s.FocalMm))
            {
                w.WriteLine("\n    Decizia s-a luat doar pe focala, presupunand cel mai mare senzor");
                w.WriteLine("    plauzibil. Daca al tau e mai mic, da campul real cu --fov <grade>.");
            }
        }

        internal static void Write(LightResult r, SuitabilityResult s, string path, TextWriter w)
        {
            w.WriteLine($"\n=== TEST END-TO-END: {path} ===");
            w.WriteLine("    Astept un stack CALIBRAT cu flat, dar FARA background extraction.");
            WriteSuitability(s, w);

            w.WriteLine($"\n    tile-uri folosite       : {r.KeptTiles} / {r.TotalTiles} (restul = nebulozitate/stele, respinse)");
            w.WriteLine($"    gradient de cer (plan)  : {r.PlanePct,5:F2} %  <- poluare luminoasa, IGNORA"
                      + (r.PlanePct > Th.LightPlaneWarn ? "  (mare!)" : ""));
            w.WriteLine($"    componenta radiala      : {Signed(r.RadPct),6} %   [{r.Verdict}]  <- ASTA e flat-ul");

            w.WriteLine("\n  INTERPRETARE");
            WriteDiagnosis(r.RadPct, w);

            // Un cadru singur amesteca eroarea de flat cu termenul radial al cerului din
            // acel moment, iar al doilea se schimba in timpul sesiunii. Pe o serie reala
            // cifra a baleiat 55 de puncte in 81 de minute. Media pe serie separa mult
            // mai bine, deci merita spus la fiecare rulare pe un cadru singur.
            w.WriteLine("\n    Cifra asta e dintr-un singur cadru, deci contine si cerul din acel moment.");
            w.WriteLine("    Da-i folderul in loc de fisier ca sa rulez pe toata seria si sa mediez:");
            w.WriteLine("        flatcheck process/ --light");
        }

        /// <summary>
        /// Diagnosticul din semnul componentei radiale. Sta separat fiindca il foloseste
        /// si raportul pe serie: aceeasi cifra, aceeasi citire, deci acelasi text - daca
        /// ar fi duplicat, cele doua ar diverge la prima reformulare.
        /// </summary>
        internal static void WriteDiagnosis(double radPct, TextWriter w)
        {
            foreach (string line in Diagnosis(radPct).Split('\n')) w.WriteLine("    " + line);
        }

        /// <summary>
        /// Acelasi diagnostic, ca sir. Il cere aplicatia WPF, care il pune sub cifra in
        /// loc sa-l scrie intr-o consola; scris a doua oara acolo, semnul - care E
        /// diagnosticul - ar fi putut ajunge explicat invers.
        /// </summary>
        internal static string Diagnosis(double radPct)
        {
            if (Math.Abs(radPct) < Th.LightRadOk)
                return "Fondul n-are componenta radiala. Flat-ul face treaba.\n"
                     + "Asta e testul care conteaza - toate celelalte sunt proxy pentru el.";

            if (radPct > 0)
                return "Fondul CADE spre margini => SUB-corectie. Flat-ul nu prinde toata\n"
                     + "vignetarea: alta diafragma decat la lights, sau flat contaminat.";

            return "Fondul CRESTE spre margini => SUPRA-corectie. Cel mai probabil\n"
                 + "flat-darks lipsa/gresite: imparti la un flat care contine offset.";
        }
    }
}
