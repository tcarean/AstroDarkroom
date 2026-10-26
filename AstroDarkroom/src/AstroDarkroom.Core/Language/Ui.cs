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

namespace AstroDarkroom.Core;

/// <summary>The two languages of the interface (R-006).</summary>
public enum UiLanguage
{
    /// <summary>English, the language of a first start without a saved setting.</summary>
    English,

    /// <summary>Romanian, written without diacritics, as the launcher always spoke.</summary>
    Romanian,
}

/// <summary>
/// The language the texts are written in, and the one call that picks a wording. Every text
/// the user reads is written as <c>T("english", "romana")</c> where it is composed, both
/// wordings side by side (ADR 0006). No culture is ever set, so a figure reads the same in
/// both languages. The shape is FlatCheck's <c>Ui</c>; here the window switches
/// <see cref="Default"/> at run time and relabels itself.
/// </summary>
public static class Ui
{
    /// <summary>The language of a first start, before any setting is read: English.</summary>
    public const UiLanguage StartupDefault = UiLanguage.English;

    // A scope rather than a plain static alone: the tests run in parallel, each class on its
    // own flow, and an AsyncLocal keeps a test's language to that flow.
    private static readonly AsyncLocal<UiLanguage?> Scoped = new();

    /// <summary>
    /// The language outside any scope: <see cref="StartupDefault"/> until the start-up sets it
    /// from the saved setting, and the window's selector after that; the test assembly sets it
    /// to Romanian once.
    /// </summary>
    public static UiLanguage Default { get; set; } = StartupDefault;

    /// <summary>The language the texts are written in now: the innermost open scope's, or <see cref="Default"/>.</summary>
    public static UiLanguage Current => Scoped.Value ?? Default;

    /// <summary>Picks the wording of the current language.</summary>
    /// <param name="en">The English wording; ASCII.</param>
    /// <param name="ro">The Romanian wording; ASCII, without diacritics.</param>
    /// <returns><paramref name="ro"/> in Romanian, <paramref name="en"/> otherwise.</returns>
    public static string T(string en, string ro) => Current == UiLanguage.Romanian ? ro : en;

    /// <summary>The code of a language, as the setting file and the tools' <c>--lang</c> write it.</summary>
    /// <param name="language">The language.</param>
    /// <returns><c>ro</c> for Romanian, <c>en</c> otherwise.</returns>
    public static string Code(UiLanguage language) => language == UiLanguage.Romanian ? "ro" : "en";

    /// <summary>The language of a code, read back from the setting file.</summary>
    /// <param name="code">The code as written, case-insensitive; null allowed.</param>
    /// <returns>The language of <c>en</c> or <c>ro</c>; null for anything else.</returns>
    public static UiLanguage? TryParse(string? code) => code?.ToUpperInvariant() switch
    {
        "EN" => UiLanguage.English,
        "RO" => UiLanguage.Romanian,
        _ => null,
    };

    /// <summary>
    /// Opens a scope in which <see cref="Current"/> is <paramref name="language"/>, on this
    /// flow and on the tasks it starts; disposing it restores the language before it.
    /// </summary>
    /// <param name="language">The language of the scope.</param>
    /// <returns>The scope; dispose it on the same flow that opened it.</returns>
    public static IDisposable Use(UiLanguage language)
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
