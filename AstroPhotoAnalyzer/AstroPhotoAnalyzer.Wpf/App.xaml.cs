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

// App.xaml.cs - the application's start and end.
//
// The image can come from the command line: a shortcut, or "Open with" from Explorer. It is not
// for scripting - the console exists for that. So can the interface language, --lang en|ro,
// English when it is missing (ADR 0048). The diagnostic log is started first, in the
// constructor, and closed last, in OnExit (ADR 0069).

using System;
using System.Globalization;
using System.IO;
using System.Windows;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The WPF application: pins the culture, sets the interface language, shows the main window,
    /// opens the file given.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Starts the diagnostic log first, so a failure while the resources load is in it too
        /// (R-170 to R-173), and hooks the window thread's unhandled exceptions to it. The window's
        /// tests build a plain Application, not this one, so they never start a log of their own.
        /// </summary>
        public App()
        {
            string[] all = Environment.GetCommandLineArgs();
            string[] args = all.Length > 1 ? all[1..] : Array.Empty<string>();

            // Whatever Apply throws, the window goes on and the image opens all the same; NLog's
            // own assembly is no such case: like the core's, it must be there.
            try
            {
                LogSetup.Apply("AstroPhotoAnalyzer.Wpf", args);
            }
            catch (Exception)
            {
                // the window goes on without a log
            }

            // Logged, not handled: the window closes on such an exception as it did (ADR 0069).
            DispatcherUnhandledException += (_, e) =>
                LogSetup.Fatal(e.Exception, "unhandled exception on the window's thread, the window closes");
        }

        /// <summary>
        /// The diagnostic log: the start's choices. Fetched on use, not a static field: this is the
        /// type whose constructor configures the log, and a logger its type initialiser created
        /// first could make NLog load a stray <c>NLog.config</c> (ADR 0069, after FlatCheck's ADR
        /// 0072).
        /// </summary>
        static Logger Log => LogManager.GetLogger(Loggers.App);

        /// <summary>
        /// Starts the application: the invariant culture first, then the interface language,
        /// before any XAML loads, then the main window over the user's remembered settings
        /// (R-156), shown, the --lang error said if there is one (R-152), and started with the
        /// first existing file among the arguments, or with none.
        /// </summary>
        /// <param name="e">WPF's startup arguments; <c>e.Args</c> is read.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ForceInvariantNumbers();
            Tr.SetDefault(LanguageFromArgs(e.Args, out string languageError));

            var window = new MainWindow(WindowSettings.InLocalAppData());
            MainWindow = window;
            window.Show();
            if (languageError != null)
            {
                Log.Warn("refused: " + languageError);
                MessageBox.Show(window, languageError, "AstroPhotoAnalyzer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            string file = FileFromArgs(e.Args);
            Log.Info("language=" + (Tr.Current == UiLanguage.Romanian ? "ro" : "en") + " file from the command line: " + (file ?? "none, the window asks for one"));
            window.Start(file);
        }

        /// <summary>Writes the diagnostic log's last line, with the exit code, and closes the log.</summary>
        /// <param name="e">The exit's arguments, whose exit code the line reports.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            // Guarded like Apply: whatever Close throws, the window still exits.
            try
            {
                LogSetup.Close(e.ApplicationExitCode);
            }
            catch (Exception)
            {
                // the log's last line is lost, the exit is not
            }
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
