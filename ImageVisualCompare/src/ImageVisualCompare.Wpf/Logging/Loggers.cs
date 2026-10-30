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

namespace ImageVisualCompare.Wpf.Logging;

/// <summary>
/// The names of the application's loggers in the diagnostic log (R-121), in one place: every
/// class takes its logger from here, the logger column of a line says which part wrote it.
/// </summary>
internal static class Loggers
{
    /// <summary>The process's own lines: the first, the last, the crashes.</summary>
    internal const string Process = "Process";

    /// <summary>The start: the language and the pair the command line chose.</summary>
    internal const string App = "App";

    /// <summary>
    /// The windows: the main window's opens, dialogs' answers, mode switches and About, and a
    /// link of the About window that would not open.
    /// </summary>
    internal const string Window = "Window";

    /// <summary>The comparison surface: the pair set on it, the zoom commands.</summary>
    internal const string Surface = "Surface";

    /// <summary>The load of a pair: the probes, the padding, the decodes, a failure.</summary>
    internal const string Load = "Load";

    /// <summary>What WIC made of a file: its decoder and pixel format, a float TIFF's range.</summary>
    internal const string Wic = "Wic";

    /// <summary>What the FITS reader made of a file: its layout, its range, its cards.</summary>
    internal const string Fits = "Fits";

    /// <summary>The window chrome: the dark caption asked of the desktop window manager.</summary>
    internal const string Chrome = "Chrome";
}
