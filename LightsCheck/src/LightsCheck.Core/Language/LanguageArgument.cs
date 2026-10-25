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

namespace LightsCheck.Core;

/// <summary>
/// The <c>--lang</c> option taken out of a command line (R-060, R-061), before anything else
/// reads it: the console needs the language before it can word a usage or a parse error, the
/// window before it loads its resources and its first view.
/// </summary>
/// <param name="Language">
/// The language the last <c>--lang</c> names; null when there was none or its value names no
/// language.
/// </param>
/// <param name="BadValue">
/// The value of the last <c>--lang</c> when it names no language (an empty string for a
/// <c>--lang</c> with nothing after it); null otherwise.
/// </param>
/// <param name="Rest">Every other argument, in its order.</param>
internal sealed record LanguageArgument(UiLanguage? Language, string? BadValue, string[] Rest)
{
    /// <summary>The option's name.</summary>
    internal const string Option = "--lang";

    /// <summary>
    /// Takes every <c>--lang &lt;code&gt;</c> and <c>--lang=&lt;code&gt;</c> out of the
    /// arguments, wherever they are; the last one counts. The name and the code are
    /// case-insensitive: <c>en</c> is English, <c>ro</c> Romanian. In the spaced form the next
    /// argument is the value whatever it is, so a <c>--lang</c> and its value are never taken
    /// for a folder or an option.
    /// </summary>
    /// <param name="args">The command line as the process received it; not null.</param>
    /// <returns>The language or the value not understood, and the other arguments.</returns>
    internal static LanguageArgument Extract(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        List<string> rest = [];
        string? value = null;
        for (int i = 0; i < args.Count; i++)
        {
            string a = args[i];
            if (a.Equals(Option, StringComparison.OrdinalIgnoreCase))
            {
                // The last argument has nothing after it to be its value.
                value = i + 1 < args.Count ? args[++i] : string.Empty;
            }
            else if (a.StartsWith(Option + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = a[(Option.Length + 1)..];
            }
            else
            {
                rest.Add(a);
            }
        }

        UiLanguage? language = Parse(value);
        return new LanguageArgument(language, value is not null && language is null ? value : null, [.. rest]);
    }

    /// <summary>The language a code names.</summary>
    /// <param name="code">The value of a <c>--lang</c>; null when there was none.</param>
    /// <returns>English for <c>en</c>, Romanian for <c>ro</c>, in any case; null otherwise.</returns>
    private static UiLanguage? Parse(string? code) => code?.ToUpperInvariant() switch
    {
        "EN" => UiLanguage.English,
        "RO" => UiLanguage.Romanian,
        _ => null,
    };

    /// <summary>
    /// The message for a value that names no language. English only: the language asked for is
    /// not known, so the default one says it.
    /// </summary>
    /// <param name="value">The value given; an empty string for a <c>--lang</c> with none.</param>
    /// <returns>One line, without the error prefix.</returns>
    internal static string BadValueMessage(string value)
        => value.Length == 0
            ? "--lang expects en or ro, and got nothing"
            : $"--lang expects en or ro, got '{value}'";
}
