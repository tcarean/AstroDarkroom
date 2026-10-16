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

using System.Globalization;

namespace DarksCheck.Core;

/// <summary>The lines every run shares: the banner, the copyright, the warranty of --help (R-001, R-004).</summary>
internal static class RunReport
{
    /// <summary>The first two lines of every run, before anything else, including errors and --help.</summary>
    internal static void WriteBanner(TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.WriteLine(VersionInfo.Banner);
        w.WriteLine(VersionInfo.Copyright);
    }

    internal const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";

    /// <summary>GPLv3 §5(d): a program that shows legal notices shows the warranty disclaimer too; here, in --help.</summary>
    internal static readonly string[] WarrantyLines =
    [
        "Acest program vine FARA NICIO GARANTIE, nici macar cea implicita de",
        "vandabilitate sau potrivire pentru un scop anume. E software liber:",
        "il poti redistribui si modifica in termenii GPLv3 (sau, la alegerea",
        "ta, orice versiune ulterioara). Textul complet e in fisierul LICENSE,",
        "sau la <" + LicenseUrl + ">.",
    ];

    /// <summary>"1 cadru", "20 cadre".</summary>
    internal static string Frames(int n) => n == 1 ? "1 cadru" : n.ToString(CultureInfo.InvariantCulture) + " cadre";

    /// <summary>A number with the invariant culture and a fixed number of decimals.</summary>
    internal static string F(double v, int decimals) => v.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>A duration as "3 h 22 min", "54 min" or "48 s".</summary>
    internal static string Duration(double seconds)
    {
        int s = (int)Math.Round(seconds);
        if (s < 60)
        {
            return s.ToString(CultureInfo.InvariantCulture) + " s";
        }

        int minutes = s / 60;
        return minutes < 60
            ? minutes.ToString(CultureInfo.InvariantCulture) + " min"
            : (minutes / 60).ToString(CultureInfo.InvariantCulture) + " h " + (minutes % 60).ToString(CultureInfo.InvariantCulture) + " min";
    }
}
