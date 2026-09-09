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

// Version.cs - versiunea uneltei, citita din assembly.
//
// SURSA DE ADEVAR e <Version> (si <Copyright>) din FlatCheck.csproj. Nu hardcoda nimic aici: MSBuild
// transforma <Version> in AssemblyInformationalVersion, iar de acolo o citim. Asa exista
// un singur loc de schimbat pe partea de C#.
//
// Se incrementeaza MANUAL la fiecare feature/fix, si trebuie sa ramana identica cu
// __version__ din flatcheck.py - VersionTests verifica asta.

using System;
using System.Reflection;

namespace FlatCheck
{
    internal static class VersionInfo
    {
        internal static string Value { get; } = Read();

        /// <summary>Numele si versiunea, exact cum apar pe prima linie a fiecarei rulari.</summary>
        internal static string Banner => "flatcheck " + Value;

        /// <summary>
        /// Notita de copyright, pe a doua linie a fiecarei rulari - sub banner. Programul e
        /// GPLv3 (vezi LICENSE), iar licenta cere ca fiecare copie sa poarte notita; un
        /// raport copiat undeva spune atunci singur si a cui e unealta, nu doar ce versiune.
        /// Ca si versiunea, vine din csproj (&lt;Copyright&gt;) - nu o hardcoda aici. Spre
        /// deosebire de versiune, textul e constant intre rulari: nu se scruba nicaieri.
        /// </summary>
        internal static string Copyright { get; } = ReadCopyright();

        static string ReadCopyright()
        {
            string c = typeof(VersionInfo).Assembly
                .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
            return string.IsNullOrWhiteSpace(c) ? "Copyright (C) 2026 Tudor Carean" : c;
        }

        static string Read()
        {
            Assembly asm = typeof(VersionInfo).Assembly;

            // InformationalVersion vine direct din <Version> si pastreaza forma "1.0.1".
            // AssemblyVersion ar da "1.0.1.0", care nu e ce vrem sa aratam.
            string v = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? asm.GetName().Version?.ToString();
            if (string.IsNullOrWhiteSpace(v)) return "0.0.0";

            // Unele build-uri lipesc metadata de sursa: "1.0.1+9a8b7c6". Taie de la '+'.
            int plus = v.IndexOf('+');
            return plus >= 0 ? v.Substring(0, plus) : v;
        }
    }
}
