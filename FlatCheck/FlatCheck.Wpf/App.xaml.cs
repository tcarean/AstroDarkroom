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

// App.xaml.cs - the application's start.
//
// One thing alone deserves explaining here: the folder can come from the command line. Not so
// that it is "scriptable" - flatcheck.exe exists for that - but because that way a shortcut can
// be put on the current session's folder, and because during development it saves choosing the
// same folder fifty times.

using System.Globalization;
using System.IO;
using System.Windows;

namespace FlatCheck.Wpf
{
    /// <summary>The window application: fixes the number culture, opens the main window on
    /// the folder given, if any.</summary>
    public partial class App : Application
    {
        /// <summary>Forces invariant numbers, shows the main window and starts it on the
        /// command line's folder.</summary>
        /// <param name="e">The start-up arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ForceInvariantNumbers();

            var window = new MainWindow();
            MainWindow = window;
            window.Show();

            window.Start(FolderFromArgs(e.Args));
        }

        /// <summary>
        /// Fixes the culture on the invariant one, for numbers.
        ///
        /// The console application gets the same thing from &lt;InvariantGlobalization&gt;, but
        /// that cannot be used here: without culture data, WPF cannot bind anything and the
        /// window crashes on loading the XAML. So it is done by hand, and it is mandatory, not
        /// cosmetic - on a Romanian Windows the saved report would come out with "0,24" where
        /// the console writes "0.24", and the requirement is that the file be IDENTICAL to the
        /// console's output, not merely equivalent.
        ///
        /// DefaultThreadCurrentCulture, not only CurrentCulture: the analyzers run on pool
        /// threads through Task.Run, and those would take the system's culture.
        /// </summary>
        internal static void ForceInvariantNumbers()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        /// <summary>
        /// The first argument that looks like an existing folder. Anything else is ignored
        /// silently: a window that refuses to start because of a wrong argument is more
        /// annoying than one that asks which folder you want.
        /// </summary>
        /// <param name="args">The command line's arguments; null allowed.</param>
        /// <returns>The first existing folder, or null.</returns>
        internal static string FolderFromArgs(string[] args)
        {
            if (args == null) return null;

            foreach (string a in args)
                if (!string.IsNullOrWhiteSpace(a) && Directory.Exists(a)) return a;

            return null;
        }
    }
}
