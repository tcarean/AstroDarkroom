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
// THE SOURCE OF TRUTH is <Version> (and <Copyright>) in FlatCheck.csproj. Hardcode nothing
// here: MSBuild turns <Version> into AssemblyInformationalVersion, and we read it from there.
// That way there is a single place to change on the C# side.
//
// It is bumped BY HAND at every feature/fix. The flatcheck.py prototype is frozen at 2.2.0 and
// no longer synchronised (ADR 0047); VersionTests checks only the plumbing.

using System.Reflection;

namespace FlatCheck
{
    /// <summary>The tool's version and copyright notice, read from the core's assembly.</summary>
    internal static class VersionInfo
    {
        internal static string Value { get; } = Read();

        /// <summary>
        /// The name and version, exactly as they appear on the first line of every run.
        /// </summary>
        internal static string Banner => "flatcheck " + Value;

        /// <summary>
        /// The copyright notice, on the second line of every run - under the banner. The program
        /// is GPLv3 (see LICENSE), and the licence asks every copy to carry the notice; a report
        /// copied somewhere then says by itself whose the tool is, not only which version. Like
        /// the version, it comes from the csproj (&lt;Copyright&gt;) - do not hardcode it here.
        /// Unlike the version, the text is constant between runs: it is scrubbed nowhere.
        /// </summary>
        internal static string Copyright { get; } = ReadCopyright();

        /// <summary>
        /// The assembly's copyright attribute, or the notice itself when it is missing.
        /// </summary>
        /// <returns>The copyright line.</returns>
        static string ReadCopyright()
        {
            string c = typeof(VersionInfo).Assembly
                .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
            return string.IsNullOrWhiteSpace(c) ? "Copyright (C) 2026 Tudor Carean" : c;
        }

        /// <summary>
        /// The assembly's informational version without source metadata, or "0.0.0" when there
        /// is none.
        /// </summary>
        /// <returns>The version, such as "2.3.0".</returns>
        static string Read()
        {
            Assembly asm = typeof(VersionInfo).Assembly;

            // InformationalVersion comes straight from <Version> and keeps the form "1.0.1".
            // AssemblyVersion would give "1.0.1.0", which is not what we want to show.
            string v = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? asm.GetName().Version?.ToString();
            if (string.IsNullOrWhiteSpace(v)) return "0.0.0";

            // Some builds append source metadata: "1.0.1+9a8b7c6". Cut from the '+'.
            int plus = v.IndexOf('+');
            return plus >= 0 ? v.Substring(0, plus) : v;
        }
    }
}
