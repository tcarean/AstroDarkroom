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

// Language.cs - the interface language: which one is in use, and the --lang parser both
// applications share (ADR 0048).
//
// Every text the user sees is written where it is used as a pair, Tr.Pick(english, romanian),
// so the two wordings are read and changed together. The language is ambient rather than a
// parameter: the texts are written deep in the analysis (a class's name, a method's note), and
// passing it through every signature would change most of the code and of the tests for a value
// set once per run. The console sets it for one run with Tr.Use, an AsyncLocal scope, so two
// tests running in parallel in two languages do not see each other's; the window sets the
// process default once, before its first window.

using System;
using System.Collections.Generic;
using System.Threading;

namespace AstroPhotoAnalyzer
{
    /// <summary>The two languages of the interface.</summary>
    internal enum UiLanguage { English = 0, Romanian = 1 }

    /// <summary>
    /// The interface language in use, and the choice of a text's wording by it. English unless
    /// the application says otherwise. Numbers do not change with it: <see cref="Fmt"/> and the
    /// invariant culture stay the same in both languages.
    /// </summary>
    internal static class Tr
    {
        static readonly AsyncLocal<UiLanguage?> Scoped = new();
        static UiLanguage processDefault = UiLanguage.English;

        /// <summary>
        /// The language in use: the one of the innermost <see cref="Use"/> scope on this flow of
        /// execution, otherwise the process default.
        /// </summary>
        internal static UiLanguage Current => Scoped.Value ?? processDefault;

        /// <summary>The process default, English until <see cref="SetDefault"/> changes it.</summary>
        internal static UiLanguage Default => processDefault;

        /// <summary>
        /// Sets the process default: the window at start-up, and the test assemblies, which run
        /// in Romanian so that the tests written before ADR 0048 assert what they asserted.
        /// </summary>
        /// <param name="language">The language every flow without a scope uses from now on.</param>
        internal static void SetDefault(UiLanguage language) => processDefault = language;

        /// <summary>
        /// Makes <paramref name="language"/> the language of this flow of execution until the
        /// returned scope is disposed, which restores the one before it.
        /// </summary>
        /// <param name="language">The language of the scope.</param>
        /// <returns>The scope, to dispose when the run ends.</returns>
        internal static IDisposable Use(UiLanguage language)
        {
            var scope = new Scope(Scoped.Value);
            Scoped.Value = language;
            return scope;
        }

        /// <summary>One of a text's two wordings, the one of the language in use.</summary>
        /// <param name="en">The English wording.</param>
        /// <param name="ro">The Romanian wording, without diacritics.</param>
        /// <returns><paramref name="en"/> or <paramref name="ro"/>.</returns>
        internal static string Pick(string en, string ro) => Current == UiLanguage.Romanian ? ro : en;

        /// <summary>The <see cref="Use"/> scope: it puts back the language it found.</summary>
        sealed class Scope : IDisposable
        {
            readonly UiLanguage? previous;
            bool disposed;

            /// <summary>A scope that restores <paramref name="previous"/> when disposed.</summary>
            /// <param name="previous">The scoped language before this scope; null for none.</param>
            internal Scope(UiLanguage? previous) => this.previous = previous;

            /// <summary>Restores the language before the scope, once.</summary>
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Scoped.Value = previous;
            }
        }
    }

    /// <summary>
    /// The --lang argument, as both applications read it: <c>--lang=ro</c> or <c>--lang ro</c>,
    /// anywhere among the arguments, the flag and the value in any case, the last one counting
    /// (R-150).
    /// </summary>
    internal static class UiLanguageArgs
    {
        /// <summary>The flag's name.</summary>
        internal const string Flag = "--lang";

        /// <summary>The codes --lang takes, in the order the messages list them.</summary>
        internal static readonly string[] Codes = { "en", "ro" };

        /// <summary>
        /// Finds the language among the arguments. A missing or unknown value is reported in
        /// English, since no language was chosen (R-152).
        /// </summary>
        /// <param name="args">The command-line arguments; null reads as none.</param>
        /// <param name="language">
        /// The language of the last --lang; null when there is none or when one is wrong.
        /// </param>
        /// <param name="error">
        /// The problem with a --lang, an English sentence without the "ERROR: " prefix; null when
        /// every --lang has a known value.
        /// </param>
        /// <returns><c>true</c> when no --lang is wrong.</returns>
        internal static bool Find(string[] args, out UiLanguage? language, out string error)
        {
            language = null;
            error = null;
            if (args == null) return true;
            UiLanguage? found = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (!IsFlag(args[i], out string value)) continue;
                if (value == null)
                {
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("-"))
                    {
                        error = $"{Flag} expects {string.Join(" or ", Codes)}, but no value was given";
                        return false;
                    }
                    value = args[++i];
                }
                UiLanguage? lang = FromCode(value);
                if (lang == null)
                {
                    error = $"{Flag} expects {string.Join(" or ", Codes)}, got '{value}'";
                    return false;
                }
                found = lang;
            }
            language = found;
            return true;
        }

        /// <summary>
        /// The arguments without every --lang and its value, for the parsers of the rest: the
        /// console's options and the window's file. Meant for arguments <see cref="Find"/>
        /// accepted; a trailing "--lang" alone is dropped too.
        /// </summary>
        /// <param name="args">The command-line arguments; null reads as none.</param>
        /// <returns>A new array, in the same order; empty for null.</returns>
        internal static string[] Strip(string[] args)
        {
            var rest = new List<string>();
            if (args == null) return rest.ToArray();
            for (int i = 0; i < args.Length; i++)
            {
                if (!IsFlag(args[i], out string value))
                {
                    rest.Add(args[i]);
                    continue;
                }
                if (value == null && i + 1 < args.Length && !args[i + 1].StartsWith("-")) i++;
            }
            return rest.ToArray();
        }

        /// <summary>
        /// Whether an argument is the --lang flag, alone or with its value after an '='.
        /// </summary>
        /// <param name="arg">One argument; null is not the flag.</param>
        /// <param name="value">The value after '=', null for the flag alone.</param>
        /// <returns><c>true</c> for "--lang" and "--lang=..." in any case.</returns>
        internal static bool IsFlag(string arg, out string value)
        {
            value = null;
            if (arg == null) return false;
            if (arg.Equals(Flag, StringComparison.OrdinalIgnoreCase)) return true;
            if (arg.StartsWith(Flag + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = arg.Substring(Flag.Length + 1);
                return true;
            }
            return false;
        }

        /// <summary>The language of a code.</summary>
        /// <param name="code">"en" or "ro", in any case, spaces around ignored; null allowed.</param>
        /// <returns>The language, or null for any other text.</returns>
        static UiLanguage? FromCode(string code) => code?.Trim().ToLowerInvariant() switch
        {
            "en" => UiLanguage.English,
            "ro" => UiLanguage.Romanian,
            _ => null,
        };
    }
}
