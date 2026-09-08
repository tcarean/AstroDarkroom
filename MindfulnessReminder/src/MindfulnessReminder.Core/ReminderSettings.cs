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
/// What the Settings window edits: whether the periodic reminder runs at all, and how far
/// apart the reminders are.
///
/// The interval is kept as whole minutes because that is the unit the user types and the
/// unit the tray tooltip counts down in; anything finer would be a number nobody can enter.
/// The bounds are not decoration -- a zero would turn the scheduler into a window every
/// tick, and they are enforced on the way out of the registry as well as in the dialog,
/// because a hand-edited registry value must not be able to do that either.
/// </summary>
public sealed record ReminderSettings(bool PeriodicEnabled, int IntervalMinutes)
{
    public const int MinIntervalMinutes = 1;

    /// <summary>Eight hours. Past a working day the feature has stopped meaning anything.</summary>
    public const int MaxIntervalMinutes = 480;

    public const int DefaultIntervalMinutes = 30;

    /// <summary>What a machine with no saved settings gets: on, every 30 minutes.</summary>
    public static readonly ReminderSettings Default = new(true, DefaultIntervalMinutes);

    public TimeSpan Interval => TimeSpan.FromMinutes(IntervalMinutes);

    /// <summary>Clamps the interval into range. Everything that builds settings goes through this.</summary>
    public ReminderSettings Normalize() => IntervalMinutes switch
    {
        < MinIntervalMinutes => this with { IntervalMinutes = MinIntervalMinutes },
        > MaxIntervalMinutes => this with { IntervalMinutes = MaxIntervalMinutes },
        _ => this,
    };

    /// <summary>True when <paramref name="minutes"/> is something the dialog may accept.</summary>
    public static bool IsIntervalInRange(int minutes)
        => minutes is >= MinIntervalMinutes and <= MaxIntervalMinutes;
}
