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

namespace CopyWcs;

/// <summary>
/// The entry point of <c>copywcs.exe</c>: copies the WCS (plate solving) metadata and the
/// optical data from one FITS into another. A shim: the diagnostic log's first line, one call to
/// <see cref="CopyWcsCli.Run"/>, which the tests drive in process, and the log's last line (ADR
/// 0009). The log is configured here and not in <see cref="CopyWcsCli.Run"/>, so the tests never
/// configure it.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The exit code Windows gives a process an unhandled .NET exception ended, 0xE0434352. The
    /// log's last line carries it when a crash still reaches <see cref="Main"/>'s <c>finally</c>,
    /// so that line does not read as a refusal's exit 1 (the review of 2026-10-30).
    /// </summary>
    private const int CrashExitCode = unchecked((int)0xE0434352);

    /// <summary>
    /// Runs the console on the real standard streams, between the diagnostic log's first and last
    /// lines (<see cref="LogSetup.Apply"/>, <see cref="LogSetup.Close"/>; R-040 to R-046).
    /// </summary>
    /// <param name="args">The command line without the executable.</param>
    /// <returns>The exit code: 0 on success, 1 when the copy was refused, 2 on a usage error.</returns>
    private static int Main(string[] args)
    {
        // The log never stops the tool: a folder that cannot be probed, a broken NLog on this
        // machine, and the run goes on without a log, stdout and stderr untouched (R-041).
        try { LogSetup.Apply("copywcs", args); }
        catch (Exception) { }

        // What the log's last line reports when the run throws: the code the crashed process
        // gets. Whether that line is written depends on the error mode the process inherits from
        // what started it: with the crash dialog off (a child of Git Bash) Windows unwinds the
        // stack and this finally runs; otherwise Windows Error Reporting takes the crash and the
        // log ends at the FATAL line (docs/design/logging.md).
        int code = CrashExitCode;
        try
        {
            code = CopyWcsCli.Run(args, Console.Out, Console.Error);
            return code;
        }
        finally
        {
            try { LogSetup.Close(code); }
            catch (Exception) { }
        }
    }
}
