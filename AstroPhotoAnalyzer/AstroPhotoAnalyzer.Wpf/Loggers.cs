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

// Loggers.cs - the names of the window's loggers in the diagnostic log (ADR 0069), in one place,
// so every class of the window takes its logger's name from here.

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The names of the window's loggers (R-171): the application, the window and its view-model,
    /// and the two tabs that run the core after the analysis, whose computations are numbered under
    /// a name of their own so a curves run and a proof run of the same analysis are told apart.
    /// </summary>
    internal static class Loggers
    {
        /// <summary>The application's own flow: its start, its arguments, its language.</summary>
        internal const string App = "App";

        /// <summary>The main window, the main view-model, the dialogs, the settings, the title bar, the About window.</summary>
        internal const string Window = "Window";

        /// <summary>The Curves Editing tab: its computations and its saves.</summary>
        internal const string Curves = "Window.Curves";

        /// <summary>The Soft proof tab: its computations, its renders and its controls.</summary>
        internal const string SoftProof = "Window.SoftProof";
    }
}
