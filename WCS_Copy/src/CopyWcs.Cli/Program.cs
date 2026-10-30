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

        // What the log's last line reports when the run throws; the process itself then exits
        // with the runtime's crash code, not with this.
        int code = 1;
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
