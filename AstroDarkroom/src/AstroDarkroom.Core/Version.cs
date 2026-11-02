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

using System.Reflection;

namespace AstroDarkroom.Core;

/// <summary>
/// The suite's name, version and copyright, read from the assembly. The source of truth is
/// <c>&lt;Version&gt;</c> and <c>&lt;Copyright&gt;</c> in Directory.Build.props (the kit's ADR
/// 0029: the owner bumps them); nothing is hard-coded here, and VersionTests checks the plumbing.
/// </summary>
public static class VersionInfo
{
    /// <summary>The product name, spelled the way the user sees it everywhere.</summary>
    public const string Name = "Astro Darkroom";

    /// <summary>The version, such as "0.1.0", as Directory.Build.props sets it.</summary>
    public static string Value { get; } = Read();

    /// <summary>The name and the version, exactly as the "Despre" window's first line shows them.</summary>
    public static string Banner => Name + " " + Value;

    /// <summary>
    /// The copyright notice, the same in both languages: the left of the main window's bottom bar
    /// (R-019) and the second line of "Despre" (GPLv3 asks every copy to carry it).
    /// </summary>
    public static string Copyright { get; } = ReadCopyright();

    /// <summary>
    /// Reads the copyright from the assembly's <see cref="AssemblyCopyrightAttribute"/>.
    /// </summary>
    /// <returns>The notice, or the owner's line when the attribute is missing or blank.</returns>
    private static string ReadCopyright()
    {
        string? c = typeof(VersionInfo).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        return string.IsNullOrWhiteSpace(c) ? "Copyright (C) 2026 Tudor Carean" : c;
    }

    /// <summary>
    /// Reads the version from the assembly's informational version, or failing that its
    /// assembly version, without the "+revision" some builds append.
    /// </summary>
    /// <returns>The version in the "1.0.1" form; "0.0.0" when the assembly carries none.</returns>
    private static string Read()
    {
        Assembly asm = typeof(VersionInfo).Assembly;

        // InformationalVersion comes straight from <Version> and keeps the "1.0.1" form;
        // AssemblyVersion would give "1.0.1.0".
        string? v = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? asm.GetName().Version?.ToString();
        if (string.IsNullOrWhiteSpace(v))
        {
            return "0.0.0";
        }

        // Some builds append source metadata: "1.0.1+9a8b7c6". Cut at '+'.
        int plus = v.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? v[..plus] : v;
    }
}
