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
// SURSA DE ADEVAR e <Version> (si <Copyright>) din ImageVisualCompare.Wpf.csproj. Nu hardcoda
// nimic aici: MSBuild transforma <Version> in AssemblyInformationalVersion, iar de acolo o
// citim. Asa exista un singur loc de schimbat.
//
// Se incrementeaza MANUAL la fiecare feature/fix.

using System.Reflection;

namespace ImageVisualCompare.Wpf;

internal static class VersionInfo
{
    internal static string Value { get; } = Read();

    /// <summary>Numele si versiunea, asa cum apar in bara de stare si in About.</summary>
    internal static string Banner => "Image Visual Compare " + Value;

    /// <summary>
    /// Notita de copyright. Programul e GPLv3 (vezi LICENSE), iar licenta cere ca fiecare
    /// copie sa o poarte; aici e afisata permanent in bara de stare, langa banner. Ca si
    /// versiunea, vine din csproj (&lt;Copyright&gt;) - nu o hardcoda aici.
    /// </summary>
    internal static string Copyright { get; } = ReadCopyright();

    /// <summary>
    /// Textul din dialogul About.
    ///
    /// GPLv3 sectiunea 5(d): un program cu interfata interactiva trebuie sa arate si lipsa
    /// garantiei, nu doar copyright-ul. Nu incape in bara de stare, deci sta aici, la un
    /// click distanta, si trimite mai departe la textul complet. E o proprietate, si nu
    /// continut inghesuit in handler-ul butonului, ca sa poata fi verificata de teste fara
    /// sa deschida o fereastra.
    /// </summary>
    internal static string About { get; } = string.Join(
        Environment.NewLine,
        Banner,
        Copyright,
        string.Empty,
        "This program comes with ABSOLUTELY NO WARRANTY.",
        "This is free software, and you are welcome to redistribute it under the terms",
        "of the GNU General Public License, version 3 or later.",
        string.Empty,
        "The full licence text is in the LICENSE file next to this program, and at",
        "https://www.gnu.org/licenses/gpl-3.0.html");

    private static string ReadCopyright()
    {
        string? copyright = typeof(VersionInfo).Assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;

        return string.IsNullOrWhiteSpace(copyright) ? "Copyright (C) 2026 Tudor Carean" : copyright;
    }

    private static string Read()
    {
        Assembly assembly = typeof(VersionInfo).Assembly;

        // InformationalVersion vine direct din <Version> si pastreaza forma "1.0.0".
        // AssemblyVersion ar da "1.0.0.0", care nu e ce vrem sa aratam.
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                          ?? assembly.GetName().Version?.ToString();

        if (string.IsNullOrWhiteSpace(version))
        {
            return "0.0.0";
        }

        // Unele build-uri lipesc metadata de sursa: "1.0.0+9a8b7c6". Taie de la '+'.
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? version[..plus] : version;
    }
}
