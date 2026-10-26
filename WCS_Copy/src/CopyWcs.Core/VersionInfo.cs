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

// VersionInfo.cs - the tool's version, read from the assembly.
//
// THE SOURCE OF TRUTH is <Version> in Directory.Build.props (ADR 0003). Hardcode nothing
// here: MSBuild turns <Version> into AssemblyInformationalVersion, and that is read back. So
// there is one place to change.
//
// The owner bumps it by hand, per feature or fix (the kit's rule "Version and year are the
// owner's").

using System.Reflection;

namespace CopyWcs;

/// <summary>The version and the banner, both read out of the assembly.</summary>
internal static class VersionInfo
{
    /// <summary>The version as <c>Directory.Build.props</c> states it, "1.0.0"; "0.0.0" when the assembly carries none.</summary>
    internal static string Value { get; } = Read();

    /// <summary>The name and the version, exactly as they appear on the first line of every run (R-001).</summary>
    internal static string Banner => "copywcs " + Value;

    /// <summary>Reads the version from the assembly's attributes.</summary>
    /// <returns>The informational version without any source suffix, or "0.0.0".</returns>
    private static string Read()
    {
        var asm = typeof(VersionInfo).Assembly;

        // InformationalVersion comes straight from <Version> and keeps the form "1.0.0".
        // AssemblyVersion would give "1.0.0.0", which is not what is shown.
        var v = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString();
        if (string.IsNullOrWhiteSpace(v))
        {
            return "0.0.0";
        }

        // Some builds append the source metadata: "1.0.0+9a8b7c6". Cut at the '+'.
        var plus = v.IndexOf('+');
        return plus >= 0 ? v[..plus] : v;
    }
}
