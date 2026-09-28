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

// SeriesFinder.cs - ce fisiere intra intr-o rulare pe serie.
//
// Sta separat de LightSeriesRun fiindca lista se cere si INAINTE de rulare: fereastra
// vrea sa arate cate cadre a gasit si sa spuna "niciunul" ca pe o stare a interfetei,
// nu ca pe o exceptie prinsa la mijlocul unui calcul.

using System.Collections.Generic;
using System.IO;

namespace FlatCheck
{
    internal static class SeriesFinder
    {
        /// <summary>
        /// Tiparul implicit: conventia Siril pentru cadre CALIBRATE dar NEREGISTRATE.
        ///
        /// "r_pp_light_*" nu se potriveste, fiindca tiparul e ancorat la inceput, si asta
        /// e corect, nu intamplator: registrarea transforma geometric cadrul, deci muta
        /// eroarea de flat - care e fixa pe senzor - fata de centrul in jurul caruia se
        /// fiteaza modelul radial. Cadrele registrate ar corupe masuratoarea tacut.
        /// </summary>
        internal const string DefaultPattern = "pp_light_*.fit";

        /// <summary>
        /// Fisierele din folder care se potrivesc cu tiparul, sortate.
        ///
        /// Enumerare completa + potrivire in proces, NU Directory.GetFiles(dir, pattern):
        /// potrivirea Win32 mai prinde si extensii mai lungi din mostenirea 8.3, deci
        /// "pp_light_*.fit" ar inghiti si ".fits". Aici tiparul inseamna acelasi lucru pe
        /// orice sistem, si acelasi lucru ca in flatcheck.py.
        ///
        /// Sortarea e ordinala pe numele fisierului, deci nu depinde de cultura masinii:
        /// consola ruleaza fara date de cultura, fereastra cu ele, si amandoua listeaza
        /// seria in aceeasi ordine. Verdictul nu depinde de ordine, doar randurile tabelului
        /// (ADR 0021).
        /// </summary>
        internal static List<string> Find(string dir, string pattern)
        {
            var hits = new List<string>();
            foreach (string p in Directory.EnumerateFiles(dir))
                if (GlobMatch(Path.GetFileName(p), pattern)) hits.Add(p);
            hits.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
            return hits;
        }

        /// <summary>
        /// Potrivire de tipar cu '*' si '?', fara distinctie de litere mari/mici.
        /// Clasele de caractere ('[abc]') NU sunt suportate - sunt tratate literal, in
        /// ambele implementari, ca sa nu apara o divergenta tacuta.
        /// </summary>
        internal static bool GlobMatch(string name, string pattern)
        {
            int n = 0, p = 0, starP = -1, starN = 0;
            while (n < name.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || Eq(pattern[p], name[n])))
                { p++; n++; }
                else if (p < pattern.Length && pattern[p] == '*')
                { starP = p++; starN = n; }
                else if (starP >= 0)
                { p = starP + 1; n = ++starN; }
                else return false;
            }
            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }

        static bool Eq(char a, char b) => char.ToLowerInvariant(a) == char.ToLowerInvariant(b);
    }
}
