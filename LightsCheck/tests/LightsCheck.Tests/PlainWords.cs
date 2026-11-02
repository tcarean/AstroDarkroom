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

namespace LightsCheck.Tests;

/// <summary>
/// The method's words a text in plain words never holds (R-077, R-078, R-079), English and Romanian,
/// whole words in any case; one list for every tab, the core's tests and the window's, which
/// link this file.
/// </summary>
internal static class PlainWords
{
    private static readonly Regex MethodWords = new(
        @"\b(median|medians|mean|sigma|mpx|registered|unregistered|correlated|distinct|isolated|coverage|radius|consecutive|sign|span|threshold"
        + @"|penalty|coherent|integration|arcmin|decoding"
        + @"|mediana|mediane|medie|inregistrat|inregistrate|neinregistrat|neinregistrate|corelat|distincte|izolate|acoperire|raza|fierbinti|semn|intindere|pragul"
        + @"|penalizare|coerent|coerenta|integrare|prag|consecutiv|decodeaza)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Fails when the text holds one of the method's words.</summary>
    /// <param name="text">The text the user reads.</param>
    /// <param name="where">Where it was read, for the failure message.</param>
    internal static void AssertPlain(string text, string where)
    {
        Match m = MethodWords.Match(text);
        Assert.False(m.Success, $"'{m.Value}' in {where}: {text}");
    }
}
