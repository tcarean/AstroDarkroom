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

namespace ImageVisualCompare.Core;

/// <summary>
/// The command line, read once at start-up: the interface language and the paths of a pair
/// (R-100, R-102, R-002).
/// </summary>
/// <param name="Language">
/// The language to show the interface in: that of the last <c>--lang</c> when its value is
/// known, English otherwise.
/// </param>
/// <param name="UnknownLanguage">
/// The value of the last <c>--lang</c> when it names no language this program has, an empty
/// string when that <c>--lang</c> has no value, and null when it was valid or there was none.
/// </param>
/// <param name="Paths">Every other argument, in its order: the candidates for the pair.</param>
public sealed record StartupArguments(UiLanguage Language, string? UnknownLanguage, IReadOnlyList<string> Paths)
{
    private const string Option = "--lang";

    /// <summary>
    /// Reads the arguments: <c>--lang=&lt;code&gt;</c>, or <c>--lang &lt;code&gt;</c> with the
    /// next argument as its value whatever it is, anywhere and case-insensitive, the last one
    /// counting; <c>en</c> is English and <c>ro</c> Romanian. A <c>--lang</c> and its value are
    /// never taken for a path.
    /// </summary>
    /// <param name="args">The arguments as the process received them; not null.</param>
    /// <returns>The language, the value to report if it was not understood, and the paths.</returns>
    public static StartupArguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var paths = new List<string>();
        string? value = null;

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg.Equals(Option, StringComparison.OrdinalIgnoreCase))
            {
                // The last argument has nothing after it to be its value.
                value = i + 1 < args.Count ? args[++i] : string.Empty;
            }
            else if (arg.StartsWith(Option + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = arg[(Option.Length + 1)..];
            }
            else
            {
                paths.Add(arg);
            }
        }

        UiLanguage? language = value?.ToLowerInvariant() switch
        {
            null or "en" => UiLanguage.English,
            "ro" => UiLanguage.Romanian,
            _ => null,
        };

        return language is { } known
            ? new StartupArguments(known, null, paths)
            : new StartupArguments(UiLanguage.English, value, paths);
    }
}
