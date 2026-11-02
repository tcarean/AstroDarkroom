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

namespace DarksCheck.Tests;

/// <summary>
/// The words of the method the Cadre, Harti and Pixeli fierbinti tabs' texts must not use (ADR 0028,
/// R-083, R-084, R-085), shared by the core's tests and the window's, which link this file; one list
/// per language, whole words in any case. The earlier tabs' lists stay beside their tests in
/// DarkSeriesTests.
/// </summary>
internal static partial class PlainWords
{
    /// <summary>R-083's, R-084's and R-085's words of the method in the three tabs' Romanian texts.</summary>
    /// <returns>The pattern; a match is a word the text must lose.</returns>
    [GeneratedRegex(@"\b(sigma|sqrt|rezid\w*|deviat\w*|abater\w*|offset\w*|amplit\w*|median\w*|metadat\w*|decod\w*|outlier\w*|normaliz\w*|cuantiz\w*|cuadratur\w*|radacin\w*|castig\w*|brut\w*|percentil\w*|temporal\w*|candidat\w*|planuri\w*)\b|(?<![\w-])e-(?!\w)|(?<![\w-])n ?=|zgomot\w* alb|planul\w* de referinta|culoarea de atentie", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    internal static partial Regex RomanianTabWords();

    /// <summary>R-083's, R-084's and R-085's words of the method in the three tabs' English texts.</summary>
    /// <returns>The pattern; a match is a word the text must lose.</returns>
    [GeneratedRegex(@"\b(sigma|sqrt|resid\w*|deviation\w*|offset\w*|amplitude\w*|ampl|median\w*|metadata|decod\w*|outlier\w*|normali[sz]\w*|quanti[sz]\w*|quadrature|gain\w*|percentile\w*|temporal\w*|candidate\w*|planes)\b|raw ADU|(?<![\w-])e-(?!\w)|(?<![\w-])n ?=|square root|white noise|reference plane|attention colou?r", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    internal static partial Regex EnglishTabWords();
}
