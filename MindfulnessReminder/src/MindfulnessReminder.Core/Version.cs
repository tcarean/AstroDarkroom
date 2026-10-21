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

// Version.cs -- the app's name and version, read back out of the assembly.
//
// THE SOURCE OF TRUTH is <Version> in Directory.Build.props, next to <Copyright>. Nothing
// here hardcodes it: MSBuild turns <Version> into AssemblyInformationalVersion, and that is
// what gets read. One place to edit, and the same value reaches the tray tooltip, About, the
// crash dialog, and the executable's file properties.
//
// It is bumped MANUALLY, per feature or fix. See "Versioning" in README.md.

using System.Reflection;

namespace MindfulnessReminder.Core;

/// <summary>
/// The banner -- name plus version -- shown wherever the app identifies itself. A tray app
/// has no console to print it on, so it surfaces on the tray tooltip, on the first line of
/// About, and in the crash dialog: a screenshot of any of the three says on its own which
/// version produced it, which is the whole point.
/// </summary>
public static class VersionInfo
{
    /// <summary>The product name, spelled the way the user sees it everywhere.</summary>
    public const string Name = "Mindfulness Reminder";

    /// <summary>Used only if the assembly attribute is missing; the build props are the truth.</summary>
    private const string Fallback = "0.0.0";

    public static string Value { get; } = Read();

    /// <summary>Name and version, exactly as they appear on the first line of About.</summary>
    public static string Banner => Name + " " + Value;

    /// <summary>
    /// Reads the version from the assembly's informational version, or failing that its
    /// assembly version, without the "+revision" some builds append.
    /// </summary>
    /// <returns>
    /// The version, such as "1.4.0"; <see cref="Fallback"/> when the assembly carries none.
    /// </returns>
    private static string Read()
    {
        var assembly = typeof(VersionInfo).Assembly;

        // InformationalVersion comes straight from <Version> and keeps the "1.0.0" shape.
        // AssemblyVersion would give "1.0.0.0", which is not what anyone wants to read.
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString();

        if (string.IsNullOrWhiteSpace(version))
        {
            return Fallback;
        }

        // Some builds append the source revision: "1.0.0+9a8b7c6". Cut at the plus.
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }
}
