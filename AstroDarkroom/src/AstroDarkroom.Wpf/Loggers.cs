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

namespace AstroDarkroom.Wpf;

/// <summary>
/// The names of the window's loggers in the diagnostic log (R-012, ADR 0012), in one place, as
/// LightsCheck's: two names say every line's source.
/// </summary>
internal static class Loggers
{
    /// <summary>The application's own flow: its start, its language, its folders.</summary>
    internal const string App = "App";

    /// <summary>The main window, the about window and the title bar.</summary>
    internal const string Window = "Window";
}
