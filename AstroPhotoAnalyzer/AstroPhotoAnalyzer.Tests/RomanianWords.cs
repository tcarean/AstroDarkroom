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

using System.Text.RegularExpressions;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// The guard of R-153, shared by the console's English goldens (LanguageTests) and the window's
/// English pass (WindowEnglishTests, which links this file).
/// </summary>
public static class RomanianWords
{
    /// <summary>
    /// Words that are Romanian and never English: one in an English text means a wording left
    /// unpaired (ADR 0048). Short function words catch most sentences; the domain words catch a
    /// label or a class name.
    /// </summary>
    public static readonly string[] Words =
    {
        "fiindca", "pentru", "si", "sau", "nu", "cu", "din", "mai", "fara", "este", "sunt", "la", "pe",
        "care", "sub", "peste", "dupa", "inainte", "praf", "praful", "cer", "cerul", "banda", "nori",
        "varfuri", "fundal", "fundalul", "hartie", "hartiei", "negrul", "tinta", "dale", "dalele",
        "zgomot", "zgomotul", "raport", "raportul", "recomandat", "marime", "latime", "imagine",
        "imaginea", "fisier", "fisierul", "eroare", "atentie", "problema", "nivel", "niveluri", "curba",
        "citire", "stele", "grila", "nativ", "naturala", "dat", "lumini", "memorie", "salvat", "deschide",
        "calculeaza", "analizeaza", "harta", "curbe", "tonuri", "culoare", "rezolutie", "despre", "ancora",
        "ancorele", "tenta", "colt", "colturi", "centru", "latura", "rama", "mediu", "distanta",
        "numar", "trebuie", "asteapta", "primit", "lipseste", "necunoscut", "necunoscuta", "gol",
    };

    /// <summary>The Romanian words of a text, its file paths scrubbed (the fixtures' names are Romanian).</summary>
    public static List<string> In(string text)
    {
        string scrubbed = Regex.Replace(text, @"<TEMP>\S*|\S+\.(tif|png|fit)\b", "");
        return Regex.Matches(scrubbed, @"[A-Za-z]+").Select(m => m.Value)
            .Where(w => Words.Contains(w.ToLowerInvariant())).Distinct().ToList();
    }

}
