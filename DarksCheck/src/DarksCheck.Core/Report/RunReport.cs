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
using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// The lines every run shares: the banner, the copyright, the warranty of --help (R-001, R-004).
/// </summary>
internal static class RunReport
{
    /// <summary>
    /// The first two lines of every run, before anything else, including errors and --help.
    /// </summary>
    /// <param name="w">The writer; not null.</param>
    internal static void WriteBanner(TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.WriteLine(VersionInfo.Banner);
        w.WriteLine(VersionInfo.Copyright);
    }

    internal const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";

    /// <summary>
    /// GPLv3 §5(d): a program that shows legal notices shows the warranty disclaimer too; here, in
    /// --help.
    /// </summary>
    internal static IReadOnlyList<string> WarrantyLines => Ui.Current == UiLanguage.Romanian
        ?
        [
            "Acest program vine FARA NICIO GARANTIE, nici macar cea implicita de",
            "vandabilitate sau potrivire pentru un scop anume. E software liber:",
            "il poti redistribui si modifica in termenii GPLv3 (sau, la alegerea",
            "ta, orice versiune ulterioara). Textul complet e in fisierul LICENSE,",
            "sau la <" + LicenseUrl + ">.",
        ]
        :
        [
            "This program comes with ABSOLUTELY NO WARRANTY, not even the implied",
            "warranty of merchantability or fitness for a particular purpose. It is",
            "free software: you can redistribute it and modify it under the terms of",
            "GPLv3 (or, at your option, any later version). The full text is in the",
            "LICENSE file, or at <" + LicenseUrl + ">.",
        ];

    /// <summary>"1 frame", "20 frames"; "1 cadru", "20 cadre".</summary>
    /// <param name="n">The frame count.</param>
    /// <returns>The count with the word for frames.</returns>
    internal static string Frames(int n) => n == 1 ? T("1 frame", "1 cadru") : n.ToString(CultureInfo.InvariantCulture) + T(" frames", " cadre");

    /// <summary>
    /// The refusal of a session with lights and no darks run without a partner (R-002, R-054): what
    /// to give, in both applications' words.
    /// </summary>
    /// <param name="root">The session's folder.</param>
    /// <returns>The message, in the run's language.</returns>
    internal static string LightsOnlyNeedsPartner(string root) => T(
        $"the session {root} has lights\\ but no darks\\; give a partner of darks (a library) to compare the lights with: --against DIR in the console, \"Choose partner...\" in the window",
        $"sesiunea {root} are lights\\ dar nu are darks\\; da un partener de dark-uri (o biblioteca) ca sa compari lights-urile cu el: --against DIR in consola, \"Alege partener...\" in fereastra");

    /// <summary>A number with the invariant culture and a fixed number of decimals.</summary>
    /// <param name="v">The number.</param>
    /// <param name="decimals">The number of decimals.</param>
    /// <returns>The formatted number; NaN prints as "NaN".</returns>
    internal static string F(double v, int decimals) => v.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>A duration as "3 h 22 min", "54 min" or "48 s".</summary>
    /// <param name="seconds">The duration in seconds, rounded to the nearest second.</param>
    /// <returns>Seconds under a minute, whole minutes under an hour, else hours and
    /// minutes.</returns>
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
