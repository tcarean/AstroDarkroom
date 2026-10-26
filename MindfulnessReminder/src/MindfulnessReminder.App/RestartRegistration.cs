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

using System.Runtime.InteropServices;
using MindfulnessReminder.Core;

namespace MindfulnessReminder.App;

/// <summary>
/// The one Windows call behind R-100: asks Windows Error Reporting to start the executable
/// again, with <see cref="ApplicationRestart.CommandLine"/>, after a crash or a hang, or when an
/// installer running as the same user asks it to close. Windows honours it only for a process
/// that has run for 60 seconds, and only while Windows Error Reporting is on (ADR 0036).
/// </summary>
internal static class RestartRegistration
{
    /// <summary>
    /// Registers the running executable for restart with Windows. A refused registration, a
    /// failed HRESULT, changes nothing but the start line of the decision log (R-111): the
    /// application runs as it did before the registration existed.
    /// </summary>
    /// <returns>The HRESULT Windows returned: 0 when registered.</returns>
    public static int Register()
        => RegisterApplicationRestart(ApplicationRestart.CommandLine, ApplicationRestart.Flags);

    /// <summary>
    /// kernel32's RegisterApplicationRestart: the command line Windows restarts the process
    /// with, and the RESTART_NO_* flags saying which events do not restart it.
    /// </summary>
    /// <param name="commandLine">
    /// The arguments for the restarted copy, at most 1024 characters; an empty string removes
    /// the registration.
    /// </param>
    /// <param name="flags">The RESTART_NO_* flags, combined.</param>
    /// <returns>An HRESULT: 0 when registered.</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterApplicationRestart(string commandLine, int flags);
}
