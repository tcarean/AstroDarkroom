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

// Loggers.cs - after FlatCheck's: the window's logger names, in one place. The window has one
// view-model, so FlatCheck's Window.<Tab>, Siril and Split have nothing to name here.

namespace CopyWcs.Wpf;

/// <summary>The names of the window's loggers in the diagnostic log (R-043): every class of the window takes its logger from here.</summary>
internal static class Loggers
{
    /// <summary>The application's own flow: its start, its arguments, its exit.</summary>
    internal const string App = "App";

    /// <summary>The main window, the main view-model, the About window.</summary>
    internal const string Window = "Window";
}
