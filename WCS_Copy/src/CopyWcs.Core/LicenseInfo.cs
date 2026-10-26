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

// LicenseInfo.cs - the copyright notice, read from the assembly.
//
// THE SOURCE OF TRUTH is <Copyright> in Directory.Build.props (ADR 0003). Hardcode nothing
// here: MSBuild turns <Copyright> into AssemblyCopyrightAttribute, and that is read back. So
// there is one place to change.

using System.Reflection;

namespace CopyWcs;

/// <summary>The copyright line, read out of the assembly.</summary>
internal static class LicenseInfo
{
    /// <summary>
    /// The copyright notice, on the second line of every run, under the version banner (see
    /// <see cref="VersionInfo"/>; R-001). The program is GPLv3 (see LICENSE), and the licence
    /// asks that every copy carry the notice; a pasted output then says on its own whose tool
    /// it is. It comes from <c>Directory.Build.props</c>; the literal in <see cref="Read"/> is
    /// only a safety net for an assembly built without the attribute.
    /// </summary>
    internal static string Copyright { get; } = Read();

    /// <summary>Reads the copyright from the assembly's attribute.</summary>
    /// <returns>The attribute's text, or the fallback literal when the assembly has none.</returns>
    private static string Read()
    {
        var c = typeof(LicenseInfo).Assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        return string.IsNullOrWhiteSpace(c) ? "Copyright (C) 2026 Tudor Carean" : c;
    }
}
