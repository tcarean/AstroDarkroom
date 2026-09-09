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

// RatioReport.cs - RatioResult -> textul din consola. Doar formatare.
//
// Cinci bucati sunt expuse ca valori, nu doar scrise: Provenance, WorldValue,
// Precondition, WorldMeaning si SensorCaveat. Aceleasi propozitii apar in fereastra -
// sub verdict, langa cifra informativa, in cardul de provenienta - iar doua formulari
// ale aceleiasi avertizari ar fi divergat exact acolo unde conteaza sa nu divergem.

using System.IO;

namespace FlatCheck
{
    internal static class RatioReport
    {
        internal static void Write(RatioResult r, string pathA, string pathB, TextWriter w)
        {
            w.WriteLine("\n=== ATRIBUIRE PRIN ROTATIE 180 ===");
            w.WriteLine($"    A : {pathA}");
            w.WriteLine($"        {Provenance(r.StackCountA, r.DateObsA)}");
            w.WriteLine($"    B : {pathB}");
            w.WriteLine($"        {Provenance(r.StackCountB, r.DateObsB)}");
            Indent(Precondition(), w);

            w.WriteLine("\n  COMPONENTA FIXA IN LUME  (informativ - nu e un verdict)");
            w.WriteLine($"    gradient extern / orientare : {WorldLine(r)}");
            w.WriteLine($"    imprastiere colturi         : {r.WorldCornerPct,5:F2} %");
            Indent(WorldMeaning(), w);

            w.WriteLine("\n  STRUCTURA MASTERULUI COMBINAT (A+B)/2 - partea fixa pe aparat");
            w.WriteLine($"    dimensiune analiza : {r.Sensor.Width} x {r.Sensor.Height} (dupa 2x2 + downsample)");
            Indent(SensorCaveat(), w);

            StructureReport.WriteBody(r.Sensor, w);
        }

        /// <summary>
        /// De unde vine fiecare stiva. Singurul loc din raport in care se vede daca cele
        /// doua chiar POT fi din doua orientari diferite: doua stive care incep una
        /// imediat dupa cealalta n-au avut cand sa fie rotite. Nu e o regula automata -
        /// cadentele variaza - dar e faptul care lipsea complet.
        /// </summary>
        internal static string Provenance(int stackCount, string dateObs)
        {
            string cadre = stackCount > 0 ? $"{stackCount} cadre stivuite" : "numar de cadre necunoscut";
            string cand = string.IsNullOrEmpty(dateObs) ? "data necunoscuta" : $"prima expunere {dateObs}";
            return $"{cadre}, {cand}";
        }

        /// <summary>Ce presupune modul si nu poate verifica. Apare si in fereastra.</summary>
        internal static string Precondition()
            => "Cele doua stive trebuie sa fie din DOUA orientari la 180 de grade, cu\n"
             + "aparatul rotit in jurul axei optice si nimic altceva schimbat. Tool-ul NU\n"
             + "poate verifica asta din pixeli - verifica randurile de mai sus. Doua stive\n"
             + "din aceeasi orientare dau un rezultat increzator si fara sens.";

        /// <summary>
        /// Cifra informativa cu bara ei de eroare, fara eticheta. Fereastra o arata asa si
        /// pune eticheta separat, ca pastila colorata - de aceea sunt doua bucati si nu
        /// una: altfel verdictul ar aparea de doua ori pe acelasi rand.
        /// </summary>
        internal static string WorldValue(RatioResult r)
            => $"{r.WorldPtpPct,5:F2} % +/- {r.WorldErrPct:F2} %";

        /// <summary>Aceeasi cifra cu eticheta lipita, cum o vrea consola.</summary>
        internal static string WorldLine(RatioResult r)
            => $"{WorldValue(r)}   [{r.WorldLabel}]";

        internal static string WorldMeaning()
            => "Atat a scos rotatia din master, daca ai stivuit ambele orientari. Bara de\n"
             + "eroare e partea din A/B care NU se inverseaza la rotatie, deci zgomotul\n"
             + "masuratorii: cand cifra si eroarea ei sunt comparabile, nu s-a detectat\n"
             + "nimic peste zgomot.";

        /// <summary>
        /// Limita care conteaza cel mai mult si nu se vede din cifre: "fix pe aparat" nu
        /// inseamna "inofensiv". Apare si in fereastra, sub verdict.
        /// </summary>
        internal static string SensorCaveat()
            => "Ce urmeaza e ce a ramas dupa ce s-a scos tot ce putea scoate rotatia. Fiind\n"
             + "fix pe aparat, se imparte corect din lights - DAR numai daca era la fel si\n"
             + "cand ai fotografiat lights-urile. Vignetarea obiectivului era. O curea\n"
             + "cazuta pe lentila, condensul sau o parasolara stramba se rotesc si ele cu\n"
             + "aparatul, arata identic aici, si la lights nu mai sunt acolo.";

        static void Indent(string text, TextWriter w)
        {
            foreach (string line in text.Split('\n'))
                w.WriteLine("    " + line);
        }
    }
}
