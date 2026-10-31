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
using AstroDarkroom.Core;
using NLog;

namespace AstroDarkroom.Wpf;

/// <summary>
/// The application: it starts and closes the diagnostic log, sets the interface language from
/// the remembered setting, then opens the main window on the folder the executable runs from.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Starts the diagnostic log first, so a failure while the resources load is in it too
    /// (R-011 to R-014), and hooks the window thread's unhandled exceptions to it.
    /// </summary>
    public App()
    {
        string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        // The log never stops the launcher: without it the window opens all the same.
        try
        {
            LogSetup.Apply("AstroDarkroom", args);
        }
        catch (Exception)
        {
            // the launcher goes on without a log
        }

        // Logged, not handled: the window closes on such an exception as it did (ADR 0012).
        DispatcherUnhandledException += (_, e) =>
            LogSetup.Fatal(e.Exception, "unhandled exception on the window's thread, the window closes");
    }

    /// <summary>
    /// The diagnostic log: the start's choices. Fetched on use, not a static field: this is the
    /// type whose constructor configures the log, and a logger its type initialiser created first
    /// could make NLog load a stray <c>NLog.config</c> (ADR 0012, after FlatCheck's ADR 0072).
    /// </summary>
    private static Logger Log => LogManager.GetLogger(Loggers.App);

    /// <summary>
    /// Reads the language the last run chose (R-007), English when there is none, asks Windows
    /// for the Visual C++ runtime LibRaw's DLLs need and logs the answer (R-018), then opens the
    /// main window on the suite folder, the launcher's own base directory (R-002), with that answer.
    /// </summary>
    /// <param name="e">The start-up arguments; not read, the launcher takes none.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroDarkroom", "settings.json");
        UiLanguage? remembered = LanguageSetting.TryRead(settingsPath);
        Ui.Default = remembered ?? Ui.StartupDefault;
        string suiteFolder = AppContext.BaseDirectory;
        Log.Info("language=" + Ui.Code(Ui.Default) + (remembered is null ? " (the start-up default)" : " (the setting)")
            + "; settings=" + settingsPath + "; suite folder=" + suiteFolder);
        IReadOnlyList<string> missingRuntime = NativeProbe.MissingRuntime();
        if (missingRuntime.Count == 0)
        {
            Log.Info(LogText.VcRuntime(missingRuntime));
        }
        else
        {
            Log.Warn(LogText.VcRuntime(missingRuntime));
        }

        MainWindow window = new(new SuiteFolder(suiteFolder), settingsPath, missingRuntime);
        MainWindow = window;
        window.Show();
    }

    /// <summary>Writes the diagnostic log's last line, with the exit code, and closes the log.</summary>
    /// <param name="e">The exit's arguments, whose exit code the line reports.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        // Guarded like Apply: the log never stops the launcher, on the way out either.
        try
        {
            LogSetup.Close(e.ApplicationExitCode);
        }
        catch (Exception)
        {
            // the log's last line is lost, the exit is not
        }
    }
}
