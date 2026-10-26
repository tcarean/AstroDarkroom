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
/// The switches the executable understands, in one place: App parses them, and
/// <see cref="ApplicationRestart"/> decides from them, so both read the same strings.
/// </summary>
public static class CommandLineSwitches
{
    /// <summary>Shows the reminder window once and quits (R-045).</summary>
    public const string Preview = "--preview";

    /// <summary>Reports what the activity monitor sees and quits (R-080).</summary>
    public const string Detect = "--detect";

    /// <summary>What Windows passes to a copy it started again after a crash (R-100, R-102).</summary>
    public const string Restarted = "--restarted";

    /// <summary>
    /// Whether the arguments carry a switch, written as the switch alone or as the switch, an
    /// equals sign and a value (<c>--detect=30</c>); the comparison ignores case.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program's name; not null.</param>
    /// <param name="name">The switch to look for, with its leading dashes.</param>
    /// <returns><c>true</c> when any argument is the switch or starts with it and <c>=</c>.</returns>
    public static bool Has(IReadOnlyList<string> args, string name)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(name);

        foreach (var argument in args)
        {
            if (argument.Equals(name, StringComparison.OrdinalIgnoreCase)
                || argument.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the arguments carry a switch written exactly as itself, with no value attached;
    /// the comparison ignores case.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program's name; not null.</param>
    /// <param name="name">The switch to look for, with its leading dashes.</param>
    /// <returns><c>true</c> when any argument equals the switch.</returns>
    public static bool HasExactly(IReadOnlyList<string> args, string name)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(name);

        foreach (var argument in args)
        {
            if (argument.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
