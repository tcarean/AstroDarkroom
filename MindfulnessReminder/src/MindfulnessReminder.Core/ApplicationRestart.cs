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

namespace MindfulnessReminder.Core;

/// <summary>
/// What the application tells Windows about restarting it, and how a start reads what Windows
/// told it back (R-100, R-102; ADR 0036). The call itself, <c>RegisterApplicationRestart</c>,
/// is App's; the values and the two decisions are here, where they can be tested.
/// </summary>
public static class ApplicationRestart
{
    /// <summary>
    /// The command line Windows starts the executable with after a crash or a hang. Not empty
    /// on purpose: an empty one would remove the registration instead of making it.
    /// </summary>
    public const string CommandLine = CommandLineSwitches.Restarted;

    /// <summary>
    /// <c>RESTART_NO_REBOOT</c>: no restart after a reboot, where the Run entry starts the
    /// application (R-003). The other flags, all left clear, are <c>RESTART_NO_CRASH</c> (1),
    /// <c>RESTART_NO_HANG</c> (2) and <c>RESTART_NO_PATCH</c> (4): a crash, a hang and an
    /// installer asking the application to close all restart it.
    /// </summary>
    public const int Flags = 8;

    /// <summary>
    /// Whether this start is the tray copy, the only one worth restarting: a restarted
    /// <c>--preview</c> or <c>--detect</c> would put a second, unasked-for window up.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program's name; not null.</param>
    /// <returns><c>false</c> when the arguments carry <c>--preview</c> or <c>--detect</c>, in any case.</returns>
    public static bool ShouldRegister(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return !CommandLineSwitches.Has(args, CommandLineSwitches.Preview)
               && !CommandLineSwitches.Has(args, CommandLineSwitches.Detect);
    }

    /// <summary>
    /// Whether Windows started this copy after a crash, so the tray can say so (R-102). The
    /// switch takes no value: <c>--restarted=1</c> is not one.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program's name; not null.</param>
    /// <returns><c>true</c> when an argument is <c>--restarted</c>, in any case.</returns>
    public static bool IsRestart(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return CommandLineSwitches.HasExactly(args, CommandLineSwitches.Restarted);
    }
}
