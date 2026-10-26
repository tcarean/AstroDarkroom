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

// LicenseInfo.cs - notita de copyright, citita din assembly.
//
// SURSA DE ADEVAR e <Copyright> din CopyWcs.csproj. Nu hardcoda nimic aici: MSBuild
// transforma <Copyright> in AssemblyCopyrightAttribute, iar de acolo o citim. Asa exista
// un singur loc de schimbat.

using System.Reflection;

namespace CopyWcs;

internal static class LicenseInfo
{
    /// <summary>
    /// Notita de copyright, pe linia a doua a fiecarei rulari - sub banner-ul de versiune
    /// (vezi <see cref="VersionInfo"/>). Programul e GPLv3 (vezi
    /// LICENSE), iar licenta cere ca fiecare copie sa poarte notita; o iesire copiata
    /// undeva spune atunci singura a cui e unealta. Vine din csproj (&lt;Copyright&gt;) -
    /// nu o hardcoda aici; literalul de mai jos e doar plasa de siguranta pentru cazul
    /// in care atributul lipseste din assembly.
    /// </summary>
    internal static string Copyright { get; } = Read();

    private static string Read()
    {
        var c = typeof(LicenseInfo).Assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        return string.IsNullOrWhiteSpace(c) ? "Copyright (C) 2026 Tudor Carean" : c;
    }
}
