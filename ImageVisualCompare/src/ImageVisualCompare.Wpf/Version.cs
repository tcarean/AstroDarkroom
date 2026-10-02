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

namespace ImageVisualCompare.Wpf;

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
    /// crammed into the button's handler, so that tests can check it without opening a window.
    /// </summary>
    internal static string About { get; } = string.Join(
        Environment.NewLine,
        Banner,
        Copyright,
        string.Empty,
        "This program comes with ABSOLUTELY NO WARRANTY.",
        "This is free software, and you are welcome to redistribute it under the terms",
        "of the GNU General Public License, version 3 or later.",
        string.Empty,
        "The full licence text is in the LICENSE file next to this program, and at",
        "https://www.gnu.org/licenses/gpl-3.0.html");

    private static string ReadCopyright()
    {
        string? copyright = typeof(VersionInfo).Assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;

        return string.IsNullOrWhiteSpace(copyright) ? "Copyright (C) 2026 Tudor Carean" : copyright;
    }

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
