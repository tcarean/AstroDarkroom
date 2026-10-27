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

// Program.cs - the entry point. Main is a shim over FlatCheckCli.Run, which is also where
// everything can be tested, because it receives the output writers; around it, the diagnostic
// log's configuration and its last line, which only the real process has (ADR 0070).


using System;

namespace FlatCheck
{
    /// <summary>The console's entry point, <c>flatcheck.exe</c>.</summary>
    static class Program
    {
        /// <summary>
        /// Runs the console on the process's own stdout and stderr, between the diagnostic
        /// log's first and last lines (<see cref="LogSetup.Apply"/>, <see cref="LogSetup.Close"/>;
        /// R-109 to R-111). The log is configured here and not in <see cref="FlatCheckCli.Run"/>,
        /// which the tests call in-process.
        /// </summary>
        /// <param name="args">The command line, flags before or after the path
        /// (R-003).</param>
        /// <returns>The process's exit code, <see cref="FlatCheckCli.Run"/>'s.</returns>
        internal static int Main(string[] args)
        {
            LogSetup.Apply("flatcheck", args);
            int code = 1;   // what an exception escaping the run leaves the process with
            try
            {
                code = FlatCheckCli.Run(args, Console.Out, Console.Error);
                return code;
            }
            finally
            {
                LogSetup.Close(code);
            }
        }
    }
}
