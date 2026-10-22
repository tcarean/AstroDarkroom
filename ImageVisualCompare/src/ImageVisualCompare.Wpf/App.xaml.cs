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

using System.IO;
using System.Windows;
using ImageVisualCompare.Wpf.Services;

namespace ImageVisualCompare.Wpf;

/// <summary>
/// The application. It keeps a pair named on the command line for the main window, which opens
/// it in place of the dialogs.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// A pair named on the command line, so the tool can be launched straight from a folder
    /// without walking through both dialogs. Null unless two existing files were given.
    /// </summary>
    private static ImagePairSelection? _startupSelection;

    /// <summary>Hands over the command line pair, once. Later opens go through the dialogs.</summary>
    /// <returns>
    /// The pair the command line named, the first time; null after that, and when the command
    /// line named no two existing files.
    /// </returns>
    public static ImagePairSelection? TakeStartupSelection()
    {
        ImagePairSelection? selection = _startupSelection;
        _startupSelection = null;
        return selection;
    }

    /// <summary>
    /// Keeps the first two arguments as the pair to open when both are existing files; anything
    /// else leaves the choice to the dialogs.
    /// </summary>
    /// <param name="e">The startup event; its arguments are image A's path, then image B's.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Anything less than two existing files falls through to the dialogs rather than
        // failing: a mistyped path is not worth refusing to start over.
        if (e.Args.Length >= 2 && File.Exists(e.Args[0]) && File.Exists(e.Args[1]))
        {
            _startupSelection = new ImagePairSelection(e.Args[0], e.Args[1]);
        }
    }
}
