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

using System.Globalization;

namespace DarksCheck.Core;

/// <summary>The two languages of the interface (R-090).</summary>
internal enum UiLanguage
{
    /// <summary>English, the default when the command line names none.</summary>
    English,

    /// <summary>Romanian, written without diacritics, as the output always was.</summary>
    Romanian,
}

/// <summary>
/// The language of the run and the pick between the two wordings of a text (ADR 0021). A run's
/// language is a scope (<see cref="Use"/>) when one is open on the current flow, and
/// <see cref="Default"/> otherwise.
/// </summary>
internal static class Ui
{
    /// <summary>The language when the command line names none: English.</summary>
    internal const UiLanguage StartupDefault = UiLanguage.English;

    // A scope rather than a plain static: the console's tests run in parallel, each run in its
    // own language, and an AsyncLocal keeps each to its own flow; the tasks and the Parallel.For
    // workers started inside the scope inherit it.
    private static readonly AsyncLocal<UiLanguage?> Scoped = new();

    /// <summary>
    /// The language outside any scope: <see cref="StartupDefault"/> until a start-up sets another,
    /// once, before the first text is written (the window from its command line, the test
    /// assemblies to Romanian).
    /// </summary>
    internal static UiLanguage Default { get; set; } = StartupDefault;

    /// <summary>The language the texts are written in now: the innermost open scope's, or
    /// <see cref="Default"/>.</summary>
    internal static UiLanguage Current => Scoped.Value ?? Default;

    /// <summary>Picks the wording of the current language.</summary>
    /// <param name="en">The English wording; ASCII.</param>
    /// <param name="ro">The Romanian wording; ASCII, without diacritics.</param>
    /// <returns><paramref name="ro"/> in Romanian, <paramref name="en"/> otherwise.</returns>
    internal static string T(string en, string ro) => Current == UiLanguage.Romanian ? ro : en;

    /// <summary>
    /// A count as a Romanian wording writes it before the word it counts (R-095, ADR 0029): the
    /// figure, a space, and "de " from 20 up unless the last two digits are 01 to 19, so
    /// "20 de cadre", "101 cadre", "120 de cadre". Only Romanian wordings call it.
    /// </summary>
    /// <param name="n">The count; not negative.</param>
    /// <returns>The figure and a space, with "de " after it when the count takes one.</returns>
    internal static string RoCount(long n) => RoCount(n.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// A figure as a Romanian wording writes it before the word it counts: a whole number takes
    /// "de " as <see cref="RoCount(long)"/> says, by its digits as shown; a figure with decimals or
    /// any other text takes none (R-095).
    /// </summary>
    /// <param name="figure">The figure exactly as the text shows it, such as "6", "-20" or "2.0";
    /// not null.</param>
    /// <returns>The figure and a space, with "de " after it when the count takes one.</returns>
    internal static string RoCount(string figure)
    {
        ArgumentNullException.ThrowIfNull(figure);
        if (!long.TryParse(figure, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long n))
        {
            return figure + " ";
        }

        long lastTwo = Math.Abs(n % 100);
        return figure + (n != 0 && (lastTwo == 0 || lastTwo >= 20) ? " de " : " ");
    }

    /// <summary>
    /// Opens a scope in which <see cref="Current"/> is <paramref name="language"/>, on this flow and
    /// on the tasks it starts; disposing it restores the language before it.
    /// </summary>
    /// <param name="language">The language of the scope.</param>
    /// <returns>The scope; dispose it on the same flow that opened it.</returns>
    internal static IDisposable Use(UiLanguage language)
    {
        UiLanguage? before = Scoped.Value;
        Scoped.Value = language;
        return new Scope(before);
    }

    /// <summary>The open scope of <see cref="Use"/>.</summary>
    /// <param name="before">The scoped language to restore, null for none.</param>
    private sealed class Scope(UiLanguage? before) : IDisposable
    {
        /// <summary>Restores the language that was scoped before this one.</summary>
        public void Dispose() => Scoped.Value = before;
    }
}
