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

// LicenseInfo.cs - the copyright notice the tool prints, read from the assembly.
//
// THE SOURCE OF TRUTH is <Copyright> in FocalLenghChecker.csproj. Nothing here is
// hardcoded except the fallback: MSBuild turns <Copyright> into
// AssemblyCopyrightAttribute, and this reads it back, so there is exactly one place
// to edit - the same arrangement VersionInfo uses for <Version>. Unlike the version,
// though, this string is constant between runs.

using System.Reflection;

namespace FocalLenghChecker;

public static class LicenseInfo
{
    /// <summary>
    /// The copyright notice, printed on the second line of every run - under the version
    /// banner. The tool is GPLv3 (see LICENSE), and the licence asks every copy to carry
    /// the notice; it also means a report pasted somewhere else says on its own whose
    /// tool produced it, not just which version.
    /// </summary>
    public static string Copyright { get; } = ReadCopyright();

    /// <summary>
    /// GPLv3 section 5(d) - the warranty disclaimer. It cannot sit under the banner
    /// without padding every report, so it lives in the help text, where the reader came
    /// to read anyway, and points at the full licence from there.
    /// </summary>
    public const string WarrantyNotice = """
        This program comes with ABSOLUTELY NO WARRANTY, not even the implied warranty of
        merchantability or fitness for a particular purpose. It is free software: you may
        redistribute and modify it under the terms of the GPLv3 (or, at your option, any
        later version). The full text is in the LICENSE file, or at
        <https://www.gnu.org/licenses/gpl-3.0.html>.
        """;

    private static string ReadCopyright()
    {
        var copyright = typeof(LicenseInfo).Assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;

        return string.IsNullOrWhiteSpace(copyright) ? "Copyright (C) 2026 Tudor Carean" : copyright;
    }
}
