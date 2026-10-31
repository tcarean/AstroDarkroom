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

// Program.cs - the entry point. Main opens the diagnostic log, runs AnalyzerCli.Run once and
// closes the log; AnalyzerCli.Run is the place where everything can be tested, because it takes
// the output writers, and it configures no log, so the tests that call it in-process keep their
// capture (ADR 0069).

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>The console's entry point.</summary>
    static class Program
    {
        /// <summary>
        /// Runs the console on the real stdout and stderr, between the diagnostic log's first and
        /// last lines (R-170 to R-173).
        /// </summary>
        /// <param name="args">The command-line arguments, without the program's name.</param>
        /// <returns>The exit code of <see cref="AnalyzerCli.Run"/>: 0, 1, 2 or 3.</returns>
        internal static int Main(string[] args)
        {
            // Whatever Apply throws, the run goes on, with the log as far as it was set up, stdout
            // and stderr untouched (R-001, R-171). A folder that cannot be written does not end
            // here (Apply falls back to %LOCALAPPDATA% itself), and NLog's own assembly is no such
            // case: like the core's it must be there, since without it the core's first logger
            // throws.
            try
            {
                LogSetup.Apply("AstroPhotoAnalyzer", args);
            }
            catch (Exception)
            {
                // the run goes on without a log
            }

            // Run's exit code, for the log's last line. A run that throws past AnalyzerCli's
            // catches crashes: the hook's Fatal line, written first, is then the crash's line.
            int code = 1;
            try
            {
                code = AnalyzerCli.Run(args, Console.Out, Console.Error);
                return code;
            }
            finally
            {
                // Guarded like Apply: whatever Close throws, the exit code is kept.
                try
                {
                    LogSetup.Close(code);
                }
                catch (Exception)
                {
                    // the log's last line is lost, the exit code is not
                }
            }
        }
    }
}
