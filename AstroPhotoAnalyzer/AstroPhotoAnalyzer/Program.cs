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

// Program.cs - the entry point. Main is a one-line shim over AnalyzerCli.Run, which is also the
// place where everything can be tested, because it takes the output writers.

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>The console's entry point.</summary>
    static class Program
    {
        /// <summary>Runs the console on the real stdout and stderr.</summary>
        /// <param name="args">The command-line arguments, without the program's name.</param>
        /// <returns>The exit code of <see cref="AnalyzerCli.Run"/>: 0, 1, 2 or 3.</returns>
        internal static int Main(string[] args)
            => AnalyzerCli.Run(args, Console.Out, Console.Error);
    }
}
