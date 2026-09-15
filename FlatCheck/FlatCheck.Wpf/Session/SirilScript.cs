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

// SirilScript.cs - ce se poate citi dintr-un .ssf fara sa-l rulezi.
//
// Doua lucruri, amandoua pure: comenzile (o linie negoala care nu incepe cu #), ca
// fereastra sa poata spune "comanda 7 din 20" si sa arate comanda intreaga cand Siril
// tipareste doar verbul; si folderele de intrare, ca butonul sa poata refuza INAINTE o
// rulare care ar muri la linia trei cu "cd biases" pe un folder fara biases/.
//
// Folderele se deduc din comenzile "cd <nume>" cu un nume simplu - fara "..", fara "/" -
// adica exact cum sunt scrise scripturile de preprocesare: "cd biases", "cd flats",
// "cd lights". "cd ../process" e iesirea, nu o intrare, si nu se numara. E o regula de
// forma, nu o intelegere a scriptului, si e suficienta pentru toate scripturile din
// depozitul Siril; un script care intra in foldere altfel pur si simplu nu e verificat
// dinainte, si Siril spune el ce lipseste.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck.Wpf
{
    internal static class SirilScript
    {
        /// <summary>Liniile care sunt comenzi, in ordinea din fisier, fara comentarii si goluri.</summary>
        internal static IReadOnlyList<string> Commands(string scriptText)
        {
            if (scriptText == null) return Array.Empty<string>();
            return scriptText.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && l[0] != '#')
                .ToList();
        }

        /// <summary>
        /// Subfolderele in care scriptul intra direct din folderul sesiunii, in ordinea
        /// primei aparitii. Vezi nota din antet pentru ce inseamna "direct".
        /// </summary>
        internal static IReadOnlyList<string> RequiredFolders(string scriptText)
        {
            var found = new List<string>();
            foreach (string cmd in Commands(scriptText))
            {
                string[] parts = cmd.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || parts[0] != "cd") continue;

                string target = parts[1];
                if (target.Contains('/') || target.Contains('\\') || target.Contains("..")) continue;
                if (!found.Contains(target, StringComparer.OrdinalIgnoreCase)) found.Add(target);
            }
            return found;
        }
    }
}
