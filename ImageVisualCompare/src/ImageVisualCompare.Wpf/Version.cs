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

// Version.cs - the tool's version, read from the assembly.
//
// THE SOURCE OF TRUTH is <Version> (and <Copyright>) in Directory.Build.props (ADR 0004).
// Hardcode nothing here: MSBuild turns <Version> into AssemblyInformationalVersion, and it is
// read from there. That way there is a single place to change.
//
// It is bumped BY HAND, per feature or fix.

using System.Reflection;
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf;

/// <summary>
/// The program's version and copyright, read once from the assembly, and the texts the status
/// bar and the About dialog build from them.
/// </summary>
internal static class VersionInfo
{
    internal static string Value { get; } = Read();

    /// <summary>The name and the version, as they appear in the status bar and in About.</summary>
    internal static string Banner => "Image Visual Compare " + Value;

    /// <summary>
    /// The copyright notice. The program is GPLv3 (see LICENSE), and the licence wants every
    /// copy to carry it; it is shown permanently in the status bar, next to the banner. Like the
    /// version, it comes from Directory.Build.props (&lt;Copyright&gt;) - do not hardcode it here.
    /// </summary>
    internal static string Copyright { get; } = ReadCopyright();

    /// <summary>
    /// The text of the About dialog.
    ///
    /// GPLv3 section 5(d): a program with an interactive interface must also show the absence
    /// of warranty, not just the copyright. It does not fit in the status bar, so it lives
    /// here, one click away, and points on to the full text. It is a property, and not content
    /// crammed into the button's handler, so that tests can check it without opening a window;
    /// it is worded each time it is read, in the interface's language (<see cref="UiText.AboutNotice"/>).
    /// </summary>
    internal static string About => string.Join(
        Environment.NewLine,
        Banner,
        Copyright,
        string.Empty,
        UiText.Current.AboutNotice);

    /// <summary>Reads the copyright notice that MSBuild wrote into the assembly.</summary>
    /// <returns>
    /// The notice from &lt;Copyright&gt;; a fixed one naming the author when the attribute is
    /// missing or blank.
    /// </returns>
    private static string ReadCopyright()
    {
        string? copyright = typeof(VersionInfo).Assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;

        return string.IsNullOrWhiteSpace(copyright) ? "Copyright (C) 2026 Tudor Carean" : copyright;
    }

    /// <summary>Reads the version that MSBuild wrote into the assembly.</summary>
    /// <returns>
    /// The version from &lt;Version&gt;, such as "1.1.1", without any "+metadata" a build
    /// appends; the assembly version when there is none; "0.0.0" when neither can be read.
    /// </returns>
    private static string Read()
    {
        Assembly assembly = typeof(VersionInfo).Assembly;

        // InformationalVersion comes straight from <Version> and keeps the "1.0.0" form.
        // AssemblyVersion would give "1.0.0.0", which is not what should be shown.
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                          ?? assembly.GetName().Version?.ToString();

        if (string.IsNullOrWhiteSpace(version))
        {
            return "0.0.0";
        }

        // Some builds append source metadata: "1.0.0+9a8b7c6". Cut at the '+'.
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? version[..plus] : version;
    }
}
