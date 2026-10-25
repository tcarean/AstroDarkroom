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
// The image can come from the command line: a shortcut, or "Open with" from Explorer. It is not
// for scripting - the console exists for that. So can the interface language, --lang en|ro,
// English when it is missing (ADR 0048).

using System.Globalization;
using System.IO;
using System.Windows;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The WPF application: pins the culture, sets the interface language, shows the main window,
    /// opens the file given.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Starts the application: the invariant culture first, then the interface language,
        /// before any XAML loads, then the main window, shown, the --lang error said if there is
        /// one (R-152), and started with the first existing file among the arguments, or with
        /// none.
        /// </summary>
        /// <param name="e">WPF's startup arguments; <c>e.Args</c> is read.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ForceInvariantNumbers();
            Tr.SetDefault(LanguageFromArgs(e.Args, out string languageError));

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            if (languageError != null)
                MessageBox.Show(window, languageError, "AstroPhotoAnalyzer", MessageBoxButton.OK, MessageBoxImage.Warning);

            window.Start(FileFromArgs(e.Args));
        }

        /// <summary>
        /// The interface language the arguments ask for (R-150): the last --lang, English when
        /// there is none or when one is wrong (R-151, R-152).
        /// </summary>
        /// <param name="args">The command-line arguments; null reads as none.</param>
        /// <param name="error">
        /// What is wrong with --lang, in English, as the message shows it; null when nothing is.
        /// </param>
        /// <returns>The language to set as the process default.</returns>
        internal static UiLanguage LanguageFromArgs(string[] args, out string error)
        {
            if (UiLanguageArgs.Find(args, out UiLanguage? language, out string problem))
            {
                error = null;
                return language ?? UiLanguage.English;
            }
            error = problem + ". The interface is in English.";
            return UiLanguage.English;
        }

        /// <summary>
        /// Pins the culture to the invariant one, for numbers.
        ///
        /// The console gets the same from InvariantGlobalization, but that cannot be used here:
        /// without culture data WPF can bind nothing and the window crashes while loading the
        /// XAML. The report goes through Fmt with an explicit invariant culture anyway; this is
        /// the second net, for any text that would not.
        ///
        /// DefaultThreadCurrentCulture, not only CurrentCulture: the analysis runs on a pool
        /// thread through Task.Run, and that one would take the system's culture.
        /// </summary>
        internal static void ForceInvariantNumbers()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        /// <summary>
        /// The first argument that is an existing file, --lang and its value apart (R-150);
        /// anything else is silently ignored.
        /// </summary>
        /// <param name="args">The command-line arguments; null or empty gives null.</param>
        /// <returns>
        /// The first argument that names an existing file, as given; null when none does.
        /// </returns>
        internal static string FileFromArgs(string[] args)
        {
            if (args == null) return null;
            foreach (string a in UiLanguageArgs.Strip(args))
                if (!string.IsNullOrWhiteSpace(a) && File.Exists(a)) return a;
            return null;
        }
    }
}
