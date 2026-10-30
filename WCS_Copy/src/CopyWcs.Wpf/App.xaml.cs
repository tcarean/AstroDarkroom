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

// App.xaml.cs - the application's start, after DarksCheck's and FlatCheck's: the diagnostic log
// first, so a failure while the resources load is in it too (ADR 0009); the command line read
// once, in the constructor, for the language from --lang before the first view and for the two
// files (R-034), so a shortcut or a "send to" can open the window on a pair; the invariant number
// culture by hand; the log's last line on the way out.

using System.Globalization;
using System.Windows;

namespace CopyWcs.Wpf;

/// <summary>What the command line asks of the window (R-034).</summary>
/// <param name="Language">The language of the last <c>--lang</c>; English when there is none or its value is not understood.</param>
/// <param name="BadLanguage">The value of a <c>--lang</c> that names no language (an empty string for a <c>--lang</c> with none), to be said to the user; null otherwise.</param>
/// <param name="Source">The first argument that names an existing file, as given; null when there is none.</param>
/// <param name="Dest">The second such argument; null when there is none.</param>
internal sealed record StartArgs(UiLanguage Language, string? BadLanguage, string? Source, string? Dest);

/// <summary>
/// The window application: sets the interface language from <c>--lang</c>, pins the number
/// culture, opens the main window and hands it the files given on the command line.
/// </summary>
public partial class App : Application
{
    private readonly StartArgs _start;

    /// <summary>
    /// Starts the diagnostic log first (R-040 to R-046), then reads the command line and sets the
    /// interface language (R-034) before anything else: the constructor runs before App.xaml's
    /// resources and the first view load, and both carry texts.
    /// </summary>
    public App()
    {
        string[] all = Environment.GetCommandLineArgs();
        string[] args = all.Length > 1 ? all[1..] : [];

        // The log never stops the window: without it the window opens all the same (R-041).
        try { LogSetup.Apply("CopyWcs.Wpf", args); }
        catch (Exception) { }

        // Logged, not handled: a crash on the window's thread still ends the window (ADR 0009).
        DispatcherUnhandledException += (_, e) =>
            LogSetup.Fatal(e.Exception, "unhandled exception on the window's thread, the window closes");

        _start = Parse(args);
        Ui.Default = _start.Language;
    }

    /// <summary>
    /// Sets the invariant number culture, shows <see cref="CopyWcs.Wpf.MainWindow"/>, says a
    /// <c>--lang</c> value it did not understand, and fills the boxes with the files the
    /// constructor read.
    /// </summary>
    /// <param name="e">The startup arguments; its <c>Args</c> are not read again, the constructor has them.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ForceInvariantNumbers();

        MainWindow window = new();
        MainWindow = window;
        window.Show();

        // A --lang value not understood is said in English, and the window goes on in English
        // (R-034).
        if (_start.BadLanguage is not null)
        {
            MessageBox.Show(window, LanguageArgument.BadValueMessage(_start.BadLanguage), "CopyWcs", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        window.Start(_start.Source, _start.Dest);
    }

    /// <summary>Writes the diagnostic log's last line, with the exit code, and closes it.</summary>
    /// <param name="e">The exit's arguments.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        // Guarded like Apply: the log never stops the window, on the way out either.
        try { LogSetup.Close(e.ApplicationExitCode); }
        catch (Exception) { }
    }

    /// <summary>
    /// Reads a command line once: the language of its last <c>--lang</c> (English when there is
    /// none or its value is not understood; R-020 to R-022), and the first two other arguments
    /// that are existing files, in their order, a <c>--lang</c> and its value left out; anything
    /// else is ignored, so a wrong argument never stops the window from opening (R-034).
    /// </summary>
    /// <param name="args">The command line without the executable; null is allowed and treated as none.</param>
    /// <returns>The language, the value not understood if any, and the two files if any.</returns>
    internal static StartArgs Parse(string[]? args)
    {
        LanguageArgument lang = LanguageArgument.Extract(args ?? []);

        string? source = null;
        string? dest = null;
        foreach (string a in lang.Rest)
        {
            if (string.IsNullOrWhiteSpace(a) || !File.Exists(a))
            {
                continue;
            }

            if (source is null)
            {
                source = a;
            }
            else
            {
                dest = a;
                break;
            }
        }

        return new StartArgs(lang.Language ?? Ui.StartupDefault, lang.BadValue, source, dest);
    }

    /// <summary>
    /// Pins the number culture to the invariant one. The console gets this from
    /// InvariantGlobalization, which a WPF application cannot use (the window would throw
    /// while loading XAML, FlatCheck's ADR 0037); so it is done by hand, for the UI thread and,
    /// through DefaultThreadCurrentCulture, for any other thread. The header facts the window
    /// shows (`100 x 80`) must read as the console's.
    /// </summary>
    internal static void ForceInvariantNumbers()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }
}
