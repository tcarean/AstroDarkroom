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
// THE SOURCE OF TRUTH is <Version> (and <Copyright>) in Directory.Build.props. Hard-code nothing
// here: MSBuild turns <Version> into AssemblyInformationalVersion, and we read it from there.
// That way there is a single place to change. It is incremented BY HAND, by the owner.

using System.Reflection;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The tool's version and copyright notice, read once from the assembly's attributes, which
    /// Directory.Build.props sets.
    /// </summary>
    internal static class VersionInfo
    {
        internal static string Value { get; } = Read();

        /// <summary>
        /// The name and the version, exactly as they appear on the first line of every run.
        /// </summary>
        internal static string Banner => "AstroPhotoAnalyzer " + Value;

        /// <summary>
        /// The copyright notice, on the second line of every run - under the banner. The program
        /// is GPLv3 (see LICENSE), and the licence requires every copy to carry the notice; a
        /// report copied somewhere then says by itself whose tool it is, not only which version.
        /// Like the version, it comes from Directory.Build.props - do not hard-code it here. The
        /// literal in ReadCopyright is only a fallback.
        /// </summary>
        internal static string Copyright { get; } = ReadCopyright();

        /// <summary>
        /// The assembly's copyright attribute, which Directory.Build.props's &lt;Copyright&gt;
        /// sets.
        /// </summary>
        /// <returns>
        /// The attribute's text, or "Copyright (C) 2026 Tudor Carean" when it is missing or blank.
        /// </returns>
        static string ReadCopyright()
        {
            string c = typeof(VersionInfo).Assembly
                .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
            return string.IsNullOrWhiteSpace(c) ? "Copyright (C) 2026 Tudor Carean" : c;
        }

        /// <summary>
        /// The assembly's informational version, which Directory.Build.props's &lt;Version&gt;
        /// sets, without the source metadata some builds append.
        /// </summary>
        /// <returns>
        /// The version as written there ("1.0.0"); the assembly version when the attribute is
        /// missing; "0.0.0" when neither gives anything.
        /// </returns>
        static string Read()
        {
            Assembly asm = typeof(VersionInfo).Assembly;

            // InformationalVersion comes straight from <Version> and keeps the form "1.0.0".
            // AssemblyVersion would give "1.0.0.0", which is not what we want to show.
            string v = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? asm.GetName().Version?.ToString();
            if (string.IsNullOrWhiteSpace(v)) return "0.0.0";

            // Some builds append source metadata: "1.0.0+9a8b7c6". Cut at the '+'.
            int plus = v.IndexOf('+');
            return plus >= 0 ? v.Substring(0, plus) : v;
        }
    }
}
