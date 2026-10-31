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
using DarksCheck.Core;
using NLog;

namespace DarksCheck.Wpf;

/// <summary>
/// The window application: starts and closes the diagnostic log, sets the interface language from
/// <c>--lang</c>, pins the number culture, opens the main window and hands it the folder given on
/// the command line.
/// </summary>
public partial class App : Application
{
    // The value of a --lang the start-up did not understand, kept for the message the window shows
    // once it is up; null when there is nothing to say.
    private readonly string? _unknownLanguage;

    /// <summary>
    /// Starts the diagnostic log first, so a failure while the resources load is in it too (R-100
    /// to R-103), and hooks the window thread's unhandled exceptions to it; then sets the interface
    /// language from the command line (R-094) before anything else: the constructor runs before
    /// App.xaml's resources and the first view load, and both carry texts.
    /// </summary>
    public App()
    {
        string[] all = Environment.GetCommandLineArgs();
        string[] args = all.Length > 1 ? all[1..] : [];

        // A log that cannot be set up never stops the window: the session opens all the same.
        try
        {
            LogSetup.Apply("DarksCheck.Wpf", args);
        }
        catch (Exception)
        {
            // the window goes on without a log
        }

        // Logged, not handled: the window closes on such an exception as it did (ADR 0025).
        DispatcherUnhandledException += (_, e) =>
            LogSetup.Fatal(e.Exception, "unhandled exception on the window's thread, the window closes");
        Ui.Default = LanguageFromArgs(args, out _unknownLanguage);
    }

    /// <summary>
    /// The diagnostic log: the start's choices. Fetched on use, not a static field: this is the
    /// type whose constructor configures the log, and a logger its type initialiser created first
    /// could make NLog load a stray <c>NLog.config</c> (ADR 0025, after FlatCheck's ADR 0072).
    /// </summary>
    private static Logger Log => LogManager.GetLogger(Loggers.App);

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

        // A --lang value not understood is said in English, and the window goes on in English
        // (R-092).
        if (_unknownLanguage is not null)
        {
            Log.Warn("refused: " + LanguageArgument.BadValueMessage(_unknownLanguage) + "; the window goes on in English");
            MessageBox.Show(window, LanguageArgument.BadValueMessage(_unknownLanguage), WindowTexts.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        string? folder = FolderFromArgs(e.Args);
        Log.Info("language=" + (Ui.Default == UiLanguage.Romanian ? "ro" : "en") + " folder from the command line: "
            + (folder ?? "none, the window asks for one"));
        window.Start(folder);
    }

    /// <summary>Writes the diagnostic log's last line, with the exit code, and closes the log.</summary>
    /// <param name="e">The exit's arguments, whose exit code the line reports.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        // Guarded like Apply: the log never stops the window, on the way out either.
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
    /// The interface language a command line asks for: the language of its last <c>--lang</c>,
    /// English when there is none or its value is not understood (R-090 to R-092).
    /// </summary>
    /// <param name="args">The command line without the executable; null is allowed and treated as
    /// none.</param>
    /// <param name="badValue">The value of a <c>--lang</c> that names no language (an empty string
    /// for a <c>--lang</c> with none), to be said to the user; null otherwise.</param>
    /// <returns>The language to set as <see cref="Ui.Default"/>.</returns>
    internal static UiLanguage LanguageFromArgs(string[]? args, out string? badValue)
    {
        LanguageArgument lang = LanguageArgument.Extract(args ?? []);
        badValue = lang.BadValue;
        return lang.Language ?? Ui.StartupDefault;
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

    /// <summary>
    /// The first argument that is an existing folder, a <c>--lang</c> and its value left out
    /// (R-094); anything else is ignored.
    /// </summary>
    /// <param name="args">The command-line arguments; null is allowed and treated as none.</param>
    /// <returns>The first non-blank argument naming an existing folder, as given; null when none
    /// does.</returns>
    internal static string? FolderFromArgs(string[]? args)
    {
        if (args is null)
        {
            return null;
        }

        foreach (string a in LanguageArgument.Extract(args).Rest)
        {
            if (!string.IsNullOrWhiteSpace(a) && Directory.Exists(a))
            {
                return a;
            }
        }

        return null;
    }
}
