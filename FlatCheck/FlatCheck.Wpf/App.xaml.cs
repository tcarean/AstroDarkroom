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

using System;
using NLog;
using System.Globalization;
using System.IO;
using System.Windows;

namespace FlatCheck.Wpf
{
    /// <summary>The window application: fixes the number culture and the interface language,
    /// opens the main window on the folder given, if any.</summary>
    public partial class App : Application
    {
        // The value of a --lang the start-up did not understand, kept for the message the window
        // shows once it is up; null when there is nothing to say.
        readonly string _unknownLanguage;

        /// <summary>
        /// Starts the diagnostic log first, so a failure while the resources load is in it too
        /// (R-109 to R-112), then sets the interface language from the command line (R-101)
        /// before anything else: the constructor runs before App.xaml's resources and the first
        /// view load, and both may carry texts.
        /// </summary>
        public App()
        {
            string[] all = Environment.GetCommandLineArgs();
            string[] args = all.Length > 1 ? all[1..] : Array.Empty<string>();
            // The log never stops the window: without it the session opens all the same.
            try { LogSetup.Apply("FlatCheck.Wpf", args); }
            catch (Exception) { }
            DispatcherUnhandledException += (s, e) =>
                LogSetup.Fatal(e.Exception, "unhandled exception on the window's thread, the window closes");
            _unknownLanguage = ApplyCommandLine(args);
        }

        /// <summary>The diagnostic log: the start's choices.</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.App);

        /// <summary>Forces invariant numbers, shows the main window, says a --lang it did not
        /// understand, and starts the window on the command line's folder.</summary>
        /// <param name="e">The start-up arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ForceInvariantNumbers();

            var window = new MainWindow();
            MainWindow = window;
            window.Show();

            if (_unknownLanguage != null)
            {
                Log.Warn("refused: " + LanguageArgument.BadValueMessage(_unknownLanguage) + "; the window goes on in English");
                MessageBox.Show(window, LanguageArgument.BadValueMessage(_unknownLanguage), "FlatCheck",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            string folder = FolderFromArgs(e.Args);
            Log.Info("language=" + (Ui.Default == UiLanguage.Romanian ? "ro" : "en")
                     + " folder from the command line: " + (folder ?? "none, the window asks for one"));
            window.Start(folder);
        }

        /// <summary>Writes the log's last line, with the exit code, and closes it.</summary>
        /// <param name="e">The exit's arguments.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            LogSetup.Close(e.ApplicationExitCode);
        }

        /// <summary>
        /// Sets <see cref="Ui.Default"/> from the command line's <c>--lang</c>: its language when
        /// it names one, English when there is none or its value is not understood.
        /// </summary>
        /// <param name="args">The command line without the executable; not null.</param>
        /// <returns>The value of a <c>--lang</c> that names no language (an empty string for a
        /// <c>--lang</c> with none), to be said to the user; null otherwise.</returns>
        internal static string ApplyCommandLine(string[] args)
        {
            LanguageArgument lang = LanguageArgument.Extract(args);
            Ui.Default = lang.Language ?? Ui.StartupDefault;
            return lang.BadValue;
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
        /// The first argument that looks like an existing folder, a <c>--lang</c> and its value
        /// left out (R-101). Anything else is ignored silently: a window that refuses to start
        /// because of a wrong argument is more annoying than one that asks which folder you want.
        /// </summary>
        /// <param name="args">The command line's arguments; null allowed.</param>
        /// <returns>The first existing folder, or null.</returns>
        internal static string FolderFromArgs(string[] args)
        {
            if (args == null) return null;

            foreach (string a in LanguageArgument.Extract(args).Rest)
                if (!string.IsNullOrWhiteSpace(a) && Directory.Exists(a)) return a;

            return null;
        }
    }
}
