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
// THE SOURCE OF TRUTH is <Version> in FocalLenghChecker.csproj. Nothing is hardcoded
// here: MSBuild turns <Version> into AssemblyInformationalVersion, and this reads it
// back, so there is exactly one place to edit.
//
// It is bumped MANUALLY, per feature or fix, and that is the user's call - see
// "Versioning" in CLAUDE.md.

using System.Reflection;

namespace FocalLenghChecker;

public static class VersionInfo
{
    /// <summary>The version alone, in the "1.0.0" form written in the csproj.</summary>
    public static string Value { get; } = Read();

    /// <summary>The name and the version, exactly as they appear on the first line of a run.</summary>
    public static string Banner => "FocalLenghChecker " + Value;

    private static string Read()
    {
        var assembly = typeof(VersionInfo).Assembly;

        // InformationalVersion comes straight from <Version> and keeps the "1.0.0" shape.
        // AssemblyVersion would give "1.0.0.0", which is not what we want to show.
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString();

        if (string.IsNullOrWhiteSpace(version))
            return "0.0.0";

        // Some builds glue the source revision on: "1.0.0+9a8b7c6". Cut at the '+'.
        var plus = version.IndexOf('+');

        return plus >= 0 ? version[..plus] : version;
    }
}
