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

using ImageVisualCompare.Wpf.Logging;

namespace ImageVisualCompare.Wpf;

/// <summary>
/// The process's entry point, in place of the one WPF generates for <see cref="App"/> (the
/// project's <c>StartupObject</c>): the diagnostic log first, then the application as the
/// generated one starts it, then the log's last line (ADR 0043).
/// </summary>
internal static class Program
{
    /// <summary>
    /// The executable's name as built, which the log's file names begin with when the process
    /// path names a host instead of it (R-120).
    /// </summary>
    private const string EntryName = "ImageVisualCompare.Wpf";

    /// <summary>
    /// Starts the diagnostic log, runs the application until its last window closes, and ends the
    /// log with the exit code. The log starts here and nowhere else: before <see cref="App"/>, its
    /// resources and its first window, so that everything after it is in it, and in no test,
    /// since no test runs this (ADR 0043). The log never stops the application: without it the
    /// pair opens all the same.
    /// </summary>
    /// <param name="args">
    /// The command line: image A's path, then image B's, with a <c>--lang</c> anywhere among them
    /// (R-002, R-100); logged verbatim.
    /// </param>
    /// <returns>The application's exit code, 0 when its window closed normally.</returns>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            LogSetup.Apply(EntryName, args);
        }
        catch (Exception)
        {
            // Swallowed on purpose: a log that cannot start is no reason not to compare a pair.
        }

        // Null until Run returns: should an exception nobody handled end the application and the
        // runtime still run the finally below, the last line says so rather than "exit 0".
        int? exitCode = null;
        try
        {
            var app = new App();
            app.DispatcherUnhandledException += (_, e) =>
                LogSetup.Fatal(e.Exception, "unhandled exception on the window's thread, the window closes");
            app.InitializeComponent();
            exitCode = app.Run();
            return exitCode.Value;
        }
        finally
        {
            // Guarded like Apply: the log never stops the application, on the way out either.
            try
            {
                LogSetup.Close(exitCode);
            }
            catch (Exception)
            {
                // Swallowed on purpose: the process is ending, and there is no one left to tell.
            }
        }
    }
}
