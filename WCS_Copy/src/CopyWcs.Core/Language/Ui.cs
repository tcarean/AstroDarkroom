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

// Ui.cs - the language the texts are written in, and the one call that picks a wording.
// Every text the user reads is written as T("english", "romana") where it is composed, both
// wordings side by side (ADR 0004). No culture is ever set, so a figure reads the same in both
// languages. A copy of FlatCheck's Language/Ui.cs (its ADR 0062), in this project's style.

namespace CopyWcs;

/// <summary>The two languages of the interface (R-020).</summary>
internal enum UiLanguage
{
    /// <summary>English, the default when the command line names none.</summary>
    English,

    /// <summary>Romanian, written without diacritics, as the output always was.</summary>
    Romanian,
}

/// <summary>
/// The language of the run and the pick between the two wordings of a text. A run's language
/// is a scope (<see cref="Use"/>) when one is open on the current flow, and
/// <see cref="Default"/> otherwise.
/// </summary>
internal static class Ui
{
    /// <summary>The language when the command line names none: English (R-021).</summary>
    internal const UiLanguage StartupDefault = UiLanguage.English;

    // A scope rather than a plain static: the console's tests run in parallel, each run in
    // its own language, and an AsyncLocal keeps each to its own flow.
    private static readonly AsyncLocal<UiLanguage?> Scoped = new();

    /// <summary>
    /// The language outside any scope: <see cref="StartupDefault"/> until a start-up sets
    /// another, once, before the first text is written (the window from its command line, the
    /// test assembly to Romanian).
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
    /// A count as Romanian writes it before a noun (ADR 0013): followed by "de" from 20 on,
    /// unless its last two digits are 01 to 19 (<c>20 de</c>, <c>72 de</c>, <c>100 de</c>, but
    /// <c>19</c>, <c>101</c>, <c>119</c>). Digits invariant.
    /// </summary>
    /// <param name="n">The count; 0 or more.</param>
    /// <returns>The number, with " de" when the grammar asks for it.</returns>
    internal static string RoCount(int n)
    {
        string count = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return n >= 20 && n % 100 is 0 or >= 20 ? count + " de" : count;
    }

    /// <summary>
    /// Opens a scope in which <see cref="Current"/> is <paramref name="language"/>, on this
    /// flow and on the tasks it starts; disposing it restores the language before it.
    /// </summary>
    /// <param name="language">The language of the scope.</param>
    /// <returns>The scope; dispose it on the same flow that opened it.</returns>
    internal static IDisposable Use(UiLanguage language)
    {
        var before = Scoped.Value;
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
