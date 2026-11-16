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
/// Where the main window's Help button and F1 send the browser: the launcher's manual, the Home
/// page of the suite's wiki, at the section of the window (R-021, ADR 0030). The wiki is English
/// only, so the address is the same in both languages. The anchor is the one GitHub makes from
/// the page's heading <c>## The window</c>: a heading renamed in the wiki is renamed here too, or
/// the browser lands at the top of the page.
/// </summary>
public static class Manual
{
    /// <summary>The Home page of the Astro Darkroom wiki, the launcher's manual.</summary>
    public const string Page = "https://github.com/tcarean/AstroDarkroom/wiki";

    /// <summary>The manual's section on the main window: its top band, rows, bottom bar and About.</summary>
    public const string Window = Page + "#the-window";
}
