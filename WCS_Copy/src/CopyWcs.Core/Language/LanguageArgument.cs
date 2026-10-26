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

// LanguageArgument.cs - the --lang option, read before anything else on the command line, by
// both applications: the console needs the language before it can word a usage or a parse
// error, the window before it loads its first view. A copy of FlatCheck's.

namespace CopyWcs;

/// <summary>The <c>--lang</c> option taken out of a command line (R-020, R-022).</summary>
/// <param name="Language">The language named by the last <c>--lang</c>, or null when there
/// was none or its value is not a language this program has.</param>
/// <param name="Given">True when the command line holds at least one <c>--lang</c>.</param>
/// <param name="BadValue">The value of the last <c>--lang</c> when it names no language (an
/// empty string when that <c>--lang</c> came last, with nothing after it); null otherwise.</param>
/// <param name="Rest">Every other argument, in its order.</param>
internal sealed record LanguageArgument(UiLanguage? Language, bool Given, string? BadValue, string[] Rest)
{
    /// <summary>The option's name.</summary>
    internal const string Option = "--lang";

    /// <summary>
    /// Takes every <c>--lang &lt;code&gt;</c> and <c>--lang=&lt;code&gt;</c> out of the
    /// arguments, wherever they are; the last one counts. The name and the code are
    /// case-insensitive: <c>en</c> is English, <c>ro</c> Romanian. In the spaced form the next
    /// argument is the value whatever it is, so a <c>--lang</c> and its value are never taken
    /// for a path or a flag.
    /// </summary>
    /// <param name="args">The command line as the process received it; not null.</param>
    /// <returns>The language, or the value not understood, and the other arguments.</returns>
    internal static LanguageArgument Extract(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var rest = new List<string>();
        string? value = null;
        var given = false;
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a.Equals(Option, StringComparison.OrdinalIgnoreCase))
            {
                given = true;
                // The last argument has nothing after it to be its value.
                value = i + 1 < args.Count ? args[++i] : string.Empty;
            }
            else if (a.StartsWith(Option + "=", StringComparison.OrdinalIgnoreCase))
            {
                given = true;
                value = a[(Option.Length + 1)..];
            }
            else
            {
                rest.Add(a);
            }
        }

        var language = Parse(value);
        var bad = given && language == null ? value : null;
        return new LanguageArgument(language, given, bad, rest.ToArray());
    }

    /// <summary>The language a code names.</summary>
    /// <param name="code">The value of a <c>--lang</c>, or null.</param>
    /// <returns>English for <c>en</c>, Romanian for <c>ro</c>, in any case; null for anything else.</returns>
    private static UiLanguage? Parse(string? code) => code?.ToLowerInvariant() switch
    {
        "en" => UiLanguage.English,
        "ro" => UiLanguage.Romanian,
        _ => null,
    };

    /// <summary>
    /// The message for a value that names no language. English only: the language asked for
    /// is not known, so the default one says it.
    /// </summary>
    /// <param name="value">The value given; an empty string for a <c>--lang</c> with none.</param>
    /// <returns>One line, without a prefix.</returns>
    internal static string BadValueMessage(string value)
        => value.Length == 0
            ? "--lang expects a language, en or ro, and got nothing"
            : $"--lang expects en or ro, got '{value}'";
}
