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
using System.Windows;

namespace DarksCheck.Wpf;

/// <summary>
/// The window application: pins the number culture, opens the main window and hands it the
/// folder given on the command line.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Sets the invariant number culture, shows <see cref="DarksCheck.Wpf.MainWindow"/> and starts
    /// it on the first command-line argument that is an existing folder (or on the folder dialog
    /// when there is none).
    /// </summary>
    /// <param name="e">The startup arguments; <c>e.Args</c> are the command-line arguments.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ForceInvariantNumbers();

        MainWindow window = new();
        MainWindow = window;
        window.Show();
        window.Start(FolderFromArgs(e.Args));
    }

    /// <summary>
    /// Pins the number culture to the invariant one. The console gets this from
    /// InvariantGlobalization, which a WPF application cannot use (the window would throw
    /// while loading XAML); so it is done by hand, for the UI thread and, through
    /// DefaultThreadCurrentCulture, for the pool threads the analysis runs on. A Romanian
    /// Windows would otherwise write "0,24" into a report meant to be identical to the console's.
    /// </summary>
    internal static void ForceInvariantNumbers()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    /// <summary>The first argument that is an existing folder; anything else is ignored.</summary>
    /// <param name="args">The command-line arguments; null is allowed and treated as none.</param>
    /// <returns>The first non-blank argument naming an existing folder, as given; null when none
    /// does.</returns>
    internal static string? FolderFromArgs(string[]? args)
    {
        if (args is null)
        {
            return null;
        }

        foreach (string a in args)
        {
            if (!string.IsNullOrWhiteSpace(a) && Directory.Exists(a))
            {
                return a;
            }
        }

        return null;
    }
}
