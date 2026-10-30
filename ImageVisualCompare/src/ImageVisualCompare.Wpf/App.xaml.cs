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

using System.IO;
using System.Windows;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf.Logging;
using ImageVisualCompare.Wpf.Services;
using NLog;

namespace ImageVisualCompare.Wpf;

/// <summary>
/// The application. It chooses the interface language from the command line before the first
/// window, and keeps a pair named there, and a <c>--lang</c> it did not understand, for the main
/// window, which opens the pair in place of the dialogs and reports the language. The process
/// starts in <see cref="Program"/>, which starts the diagnostic log before this.
/// </summary>
public partial class App : Application
{
    /// <summary>The start's choices in the diagnostic log (R-121).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.App);

    /// <summary>
    /// A pair named on the command line, so the tool can be launched straight from a folder
    /// without walking through both dialogs. Null unless two existing files were given.
    /// </summary>
    private static ImagePairSelection? _startupSelection;

    /// <summary>
    /// The value of a <c>--lang</c> that named no language this program has, empty when it had
    /// none, for the main window to report (R-102); null when there is nothing to report.
    /// </summary>
    private static string? _unknownLanguage;

    /// <summary>Hands over the command line pair, once. Later opens go through the dialogs.</summary>
    /// <returns>
    /// The pair the command line named, the first time; null after that, and when the command
    /// line named no two existing files.
    /// </returns>
    public static ImagePairSelection? TakeStartupSelection()
    {
        ImagePairSelection? selection = _startupSelection;
        _startupSelection = null;
        return selection;
    }

    /// <summary>Hands over a <c>--lang</c> that was not understood, once.</summary>
    /// <returns>
    /// The value given, empty when <c>--lang</c> had none, the first time; null after that, and
    /// when the language was understood or not given.
    /// </returns>
    public static string? TakeUnknownLanguage()
    {
        string? value = _unknownLanguage;
        _unknownLanguage = null;
        return value;
    }

    /// <summary>
    /// Reads the command line before the first window: <see cref="ApplyCommandLine"/>.
    /// </summary>
    /// <param name="e">
    /// The startup event; its arguments are image A's path, then image B's, with a
    /// <c>--lang</c> anywhere among them.
    /// </param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Before the first window, so that everything it writes is in the language chosen.
        ApplyCommandLine(e.Args);
    }

    /// <summary>
    /// Sets the interface language from <c>--lang</c> (R-100), keeps a value it did not
    /// understand for the main window to report (R-102), and keeps the first two of the other
    /// arguments as the pair to open when both are existing files; anything else leaves the
    /// choice to the dialogs. Each of the three is a line of the diagnostic log (R-121).
    /// Separate from <see cref="OnStartup"/> so a test can run it without an application.
    /// </summary>
    /// <param name="args">The command line's arguments, as the process received them.</param>
    internal static void ApplyCommandLine(IReadOnlyList<string> args)
    {
        StartupArguments arguments = StartupArguments.Parse(args);
        UiText.Current = UiText.For(arguments.Language);
        _unknownLanguage = arguments.UnknownLanguage;

        Log.Info("language=" + (arguments.Language == UiLanguage.Romanian ? "ro" : "en"));
        if (arguments.UnknownLanguage is { } unknown)
        {
            Log.Warn((unknown.Length == 0 ? "--lang was given without a language" : "--lang " + LogText.Quoted(unknown) + " names no language this program has")
                + ": the interface is English, and the window says so");
        }

        // Anything less than two existing files falls through to the dialogs rather than
        // failing: a mistyped path is not worth refusing to start over.
        IReadOnlyList<string> paths = arguments.Paths;
        if (paths.Count >= 2 && File.Exists(paths[0]) && File.Exists(paths[1]))
        {
            _startupSelection = new ImagePairSelection(paths[0], paths[1]);
            Log.Info("the command line names the pair A=" + paths[0] + " B=" + paths[1]
                + (paths.Count > 2 ? "; " + LogText.Int(paths.Count - 2) + " more argument(s) ignored" : string.Empty));
        }
        else if (paths.Count > 0)
        {
            Log.Info("the command line names no pair of existing files ("
                + string.Join(", ", paths.Select(p => LogText.Quoted(p) + (File.Exists(p) ? string.Empty : " missing")))
                + "): the dialogs ask");
        }
        else
        {
            Log.Info("the command line names no pair: the dialogs ask");
        }
    }
}
