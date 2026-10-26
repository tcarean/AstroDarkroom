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

// App.xaml.cs - the application's start, after DarksCheck's and FlatCheck's: the language from
// --lang before the first view, the invariant number culture by hand, and the two files from the
// command line (R-034), so a shortcut or a "send to" can open the window on a pair.

using System.Globalization;
using System.Windows;

namespace CopyWcs.Wpf;

/// <summary>
/// The window application: sets the interface language from <c>--lang</c>, pins the number
/// culture, opens the main window and hands it the files given on the command line.
/// </summary>
public partial class App : Application
{
    // The value of a --lang the start-up did not understand, kept for the message the window shows
    // once it is up; null when there is nothing to say.
    private readonly string? _unknownLanguage;

    /// <summary>
    /// Sets the interface language from the command line (R-034) before anything else: the
    /// constructor runs before App.xaml's resources and the first view load, and both carry texts.
    /// </summary>
    public App()
    {
        string[] all = Environment.GetCommandLineArgs();
        Ui.Default = LanguageFromArgs(all.Length > 1 ? all[1..] : [], out _unknownLanguage);
    }

    /// <summary>
    /// Sets the invariant number culture, shows <see cref="CopyWcs.Wpf.MainWindow"/>, says a
    /// <c>--lang</c> value it did not understand, and fills the boxes from the first two
    /// command-line arguments that are existing files.
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
        // (R-034).
        if (_unknownLanguage is not null)
        {
            MessageBox.Show(window, LanguageArgument.BadValueMessage(_unknownLanguage), "CopyWcs", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        (string? source, string? dest) = FilesFromArgs(e.Args);
        window.Start(source, dest);
    }

    /// <summary>
    /// The interface language a command line asks for: the language of its last <c>--lang</c>,
    /// English when there is none or its value is not understood (R-034, R-020 to R-022).
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
    /// while loading XAML, FlatCheck's ADR 0037); so it is done by hand, for the UI thread and,
    /// through DefaultThreadCurrentCulture, for any other thread. The header facts the window
    /// shows (`100 x 80`) must read as the console's.
    /// </summary>
    internal static void ForceInvariantNumbers()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// The first two arguments that are existing files, in their order, a <c>--lang</c> and its
    /// value left out (R-034); anything else is ignored, so a wrong argument never stops the
    /// window from opening.
    /// </summary>
    /// <param name="args">The command-line arguments; null is allowed and treated as none.</param>
    /// <returns>The source and the destination, each as given, or null when no argument names
    /// an existing file for it.</returns>
    internal static (string? Source, string? Dest) FilesFromArgs(string[]? args)
    {
        if (args is null)
        {
            return (null, null);
        }

        string? source = null;
        foreach (string a in LanguageArgument.Extract(args).Rest)
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
                return (source, a);
            }
        }

        return (source, null);
    }
}
