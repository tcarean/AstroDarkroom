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

namespace LightsCheck.Wpf;

/// <summary>
/// The window application: pins the number culture, opens the main window and starts it on the
/// folder given on the command line, if any.
/// </summary>
public partial class App : Application
{
    // The command line without --lang, and the --lang value not understood (null when none),
    // read in the constructor.
    private readonly string[] _args;
    private readonly string? _badLanguage;

    /// <summary>
    /// Reads the language from the command line before anything else (R-060, R-063): App.xaml's
    /// resources (the theme) and the first view load after this, and both carry texts.
    /// </summary>
    public App()
    {
        (_args, _badLanguage) = ApplyCommandLine(Environment.GetCommandLineArgs().Skip(1).ToArray());
    }

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
            MessageBox.Show(window, LanguageArgument.BadValueMessage(_badLanguage), WindowTexts.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        window.Start(FolderFromArgs(_args));
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
