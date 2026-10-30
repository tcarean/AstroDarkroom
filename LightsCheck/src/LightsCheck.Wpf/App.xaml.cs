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
using LightsCheck.Core;
using NLog;

namespace LightsCheck.Wpf;

/// <summary>
/// The window application: starts and closes the diagnostic log, pins the number culture, opens
/// the main window and starts it on the folder given on the command line, if any.
/// </summary>
public partial class App : Application
{
    // The command line without --lang, and the --lang value not understood (null when none),
    // read in the constructor.
    private readonly string[] _args;
    private readonly string? _badLanguage;

    /// <summary>
    /// Starts the diagnostic log first, so a failure while the resources load is in it too
    /// (R-070 to R-073), and hooks the window thread's unhandled exceptions to it; then reads the
    /// language from the command line before anything else (R-060, R-063): App.xaml's resources
    /// (the theme) and the first view load after this, and both carry texts.
    /// </summary>
    public App()
    {
        string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        // The log never stops the window: without it the session opens all the same.
        try
        {
            LogSetup.Apply("LightsCheck.Wpf", args);
        }
        catch (Exception)
        {
            // the window goes on without a log
        }

        // Logged, not handled: the window closes on such an exception as it did (ADR 0029).
        DispatcherUnhandledException += (_, e) =>
            LogSetup.Fatal(e.Exception, "unhandled exception on the window's thread, the window closes");
        (_args, _badLanguage) = ApplyCommandLine(args);
    }

    /// <summary>
    /// The diagnostic log: the start's choices. Fetched on use, not a static field: this is the
    /// type whose constructor configures the log, and a logger its type initialiser created first
    /// could make NLog load a stray <c>NLog.config</c> (ADR 0029, after FlatCheck's ADR 0072).
    /// </summary>
    private static Logger Log => LogManager.GetLogger(Loggers.App);

    /// <summary>
    /// Takes <c>--lang</c> out of the command line and sets <see cref="Ui.Default"/> to the
    /// language it names, English when it names none or a value not understood (R-061).
    /// </summary>
    /// <param name="args">The command line without the program's name; not null.</param>
    /// <returns>
    /// The other arguments, in their order, and the <c>--lang</c> value not understood (an
    /// empty string for a <c>--lang</c> with none), null when there is no such value.
    /// </returns>
    internal static (string[] Args, string? BadValue) ApplyCommandLine(string[] args)
    {
        LanguageArgument lang = LanguageArgument.Extract(args);
        Ui.Default = lang.Language ?? Ui.StartupDefault;
        return (lang.Rest, lang.BadValue);
    }

    /// <summary>
    /// Runs at start: pins the number culture to the invariant one before any window exists, then
    /// shows the main window and starts it on the first command-line argument that is an existing
    /// folder, or on the folder dialog when there is none; a <c>--lang</c> value not understood
    /// is said in a message once the window is up.
    /// </summary>
    /// <param name="e">The start arguments; not read, the constructor read the command line.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ForceInvariantNumbers();

        MainWindow window = new();
        MainWindow = window;
        window.Show();
        if (_badLanguage is not null)
        {
            Log.Warn("refused: " + LanguageArgument.BadValueMessage(_badLanguage) + "; the window goes on in English");
            MessageBox.Show(window, LanguageArgument.BadValueMessage(_badLanguage), WindowTexts.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        string? folder = FolderFromArgs(_args);
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
    /// <param name="args">
    /// The command-line arguments without <c>--lang</c> (<see cref="ApplyCommandLine"/>), so a
    /// language value is never taken for the folder; null is allowed and gives null.
    /// </param>
    /// <returns>
    /// The first argument, as given, that is not blank and names an existing folder; null when
    /// none does.
    /// </returns>
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
