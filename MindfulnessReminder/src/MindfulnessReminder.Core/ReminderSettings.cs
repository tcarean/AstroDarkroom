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
/// What the Settings window edits: whether the periodic reminder runs at all, how far apart
/// the reminders are, and the two conditions under which one is held back.
///
/// The interval is kept as whole minutes because that is the unit the user types and the
/// unit the tray tooltip counts down in; anything finer would be a number nobody can enter.
/// The bounds are not decoration -- a zero would turn the scheduler into a window every
/// tick, and they are enforced on the way out of the registry as well as in the dialog,
/// because a hand-edited registry value must not be able to do that either.
///
/// The two pause settings are init-only properties rather than more positional parameters.
/// That is not tidiness: every one of them has a default that is the same on a fresh machine
/// and in a hand-written test, so writing it out at every call site would be repetition with
/// nothing to say, and "new ReminderSettings(true, 30)" goes on meaning exactly what it
/// meant before they existed.
/// </summary>
public sealed record ReminderSettings(bool PeriodicEnabled, int IntervalMinutes)
{
    public const int MinIntervalMinutes = 1;

    /// <summary>Eight hours. Past a working day the feature has stopped meaning anything.</summary>
    public const int MaxIntervalMinutes = 480;

    public const int DefaultIntervalMinutes = 30;

    /// <summary>
    /// Five seconds. Below that the check costs more than the wait: a reminder held back
    /// through an hour-long call would read the registry hundreds of times for nothing.
    /// </summary>
    public const int MinRetrySeconds = 5;

    /// <summary>
    /// Five minutes. Past that a call that ended is a break not taken for longer than the
    /// shortest interval the app allows.
    /// </summary>
    public const int MaxRetrySeconds = 300;

    public const int DefaultRetrySeconds = 20;

    /// <summary>
    /// What a machine with no saved settings gets: on, every 30 minutes, both pauses on.
    /// The two pauses come from the property initializers below rather than from here, so
    /// each default is written once.
    /// </summary>
    public static readonly ReminderSettings Default = new(true, DefaultIntervalMinutes);

    /// <summary>
    /// Hold a reminder back while the microphone or the camera is live -- a call, a meeting,
    /// a recording. Independent of <see cref="PeriodicEnabled"/>: it governs the unlock
    /// reminder too.
    /// </summary>
    public bool PauseWhileMicrophoneInUse { get; init; } = true;

    /// <summary>Hold a reminder back while a full-screen app or presentation mode has the desktop.</summary>
    public bool PauseWhileFullScreen { get; init; } = true;

    /// <summary>How long a held-back reminder waits before the conditions are looked at again.</summary>
    public int RetrySeconds { get; init; } = DefaultRetrySeconds;

    public TimeSpan Interval => TimeSpan.FromMinutes(IntervalMinutes);

    public TimeSpan RetryInterval => TimeSpan.FromSeconds(RetrySeconds);

    /// <summary>True when either pause is switched on and the activity monitor is worth asking.</summary>
    public bool PausesForAnything => PauseWhileMicrophoneInUse || PauseWhileFullScreen;

    /// <summary>Clamps both numbers into range. Everything that builds settings goes through this.</summary>
    public ReminderSettings Normalize()
    {
        var normalized = IntervalMinutes switch
        {
            < MinIntervalMinutes => this with { IntervalMinutes = MinIntervalMinutes },
            > MaxIntervalMinutes => this with { IntervalMinutes = MaxIntervalMinutes },
            _ => this,
        };

        return normalized.RetrySeconds switch
        {
            < MinRetrySeconds => normalized with { RetrySeconds = MinRetrySeconds },
            > MaxRetrySeconds => normalized with { RetrySeconds = MaxRetrySeconds },
            _ => normalized,
        };
    }

    /// <summary>True when <paramref name="minutes"/> is something the dialog may accept.</summary>
    public static bool IsIntervalInRange(int minutes)
        => minutes is >= MinIntervalMinutes and <= MaxIntervalMinutes;

    /// <summary>True when <paramref name="seconds"/> is something the dialog may accept.</summary>
    public static bool IsRetryInRange(int seconds)
        => seconds is >= MinRetrySeconds and <= MaxRetrySeconds;
}
