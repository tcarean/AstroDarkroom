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

namespace AstroDarkroom.Core;

/// <summary>
/// The licence notice the "Despre" window shows: GPLv3 section 5(d) asks a program that
/// displays notices to display the copyright and the disclaimer of warranty. The paragraph is
/// in both languages, the Romanian without diacritics as the components' windows word it
/// (ADR 0003, ADR 0006).
/// </summary>
public static class LicenseInfo
{
    /// <summary>Where the full licence text is on the web; LICENSE beside the executable is the same text.</summary>
    public const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";

    /// <summary>The disclaimer of warranty and the terms, one paragraph, in the current language.</summary>
    public static string Warranty => Ui.T(
        "This program comes with ABSOLUTELY NO WARRANTY, not even the implied warranty of "
        + "merchantability or fitness for a particular purpose. It is free software: you may "
        + "redistribute and modify it under the terms of the GPLv3 (or, at your option, any later "
        + "version). The full text is in the LICENSE file beside the program, or at <" + LicenseUrl + ">.",
        "Acest program vine FARA NICIO GARANTIE, nici macar cea implicita de vandabilitate sau "
        + "potrivire pentru un scop anume. E software liber: il poti redistribui si modifica in "
        + "termenii GPLv3 (sau, la alegerea ta, orice versiune ulterioara). Textul complet e in "
        + "fisierul LICENSE de langa program, sau la <" + LicenseUrl + ">.");
}
