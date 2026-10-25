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
// wordings side by side (ADR 0062). Numbers go into both through the same format strings, and
// no culture is ever set, so a figure reads the same in both languages.

using System;
using System.Threading;

namespace FlatCheck
{
    /// <summary>The two languages of the interface (R-100).</summary>
    internal enum UiLanguage
    {
        /// <summary>English, the default when the command line names none.</summary>
        English,

        /// <summary>Romanian, written without diacritics, as the output always was.</summary>
        Romanian,
    }

    /// <summary>
    /// The language of the run and the pick between the two wordings of a text. A run's
    /// language is a scope (<see cref="Use"/>) when one is open on the current flow, and
    /// <see cref="Default"/> otherwise.
    /// </summary>
    internal static class Ui
    {
        /// <summary>The language when the command line names none: English.</summary>
        internal const UiLanguage StartupDefault = UiLanguage.English;

        // A scope rather than a plain static: the console's tests run in parallel, each run
        // in its own language, and an AsyncLocal keeps each to its own flow - a task started
        // inside the scope inherits it.
        static readonly AsyncLocal<UiLanguage?> scoped = new();

        /// <summary>
        /// The language outside any scope: <see cref="StartupDefault"/> until a start-up sets
        /// another, once, before the first text is written (the window from its command line,
        /// the test assemblies to Romanian).
        /// </summary>
        internal static UiLanguage Default { get; set; } = StartupDefault;

        /// <summary>The language the texts are written in now: the innermost open scope's,
        /// or <see cref="Default"/>.</summary>
        internal static UiLanguage Current => scoped.Value ?? Default;

        /// <summary>Picks the wording of the current language.</summary>
        /// <param name="en">The English wording; ASCII.</param>
        /// <param name="ro">The Romanian wording; ASCII, without diacritics.</param>
        /// <returns><paramref name="ro"/> in Romanian, <paramref name="en"/> otherwise.</returns>
        internal static string T(string en, string ro) => Current == UiLanguage.Romanian ? ro : en;

        /// <summary>
        /// Opens a scope in which <see cref="Current"/> is <paramref name="language"/>, on this
        /// flow and on the tasks it starts; disposing it restores the language before it.
        /// </summary>
        /// <param name="language">The language of the scope.</param>
        /// <returns>The scope; dispose it on the same flow that opened it.</returns>
        internal static IDisposable Use(UiLanguage language)
        {
            var before = scoped.Value;
            scoped.Value = language;
            return new Scope(before);
        }

        /// <summary>The open scope of <see cref="Use"/>.</summary>
        /// <param name="before">The scoped language to restore, null for none.</param>
        sealed class Scope(UiLanguage? before) : IDisposable
        {
            /// <summary>Restores the language that was scoped before this one.</summary>
            public void Dispose() => scoped.Value = before;
        }
    }
}
