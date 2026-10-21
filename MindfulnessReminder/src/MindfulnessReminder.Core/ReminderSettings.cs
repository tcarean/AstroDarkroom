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
/// What the Settings window edits: whether each of the two reminders runs at all, what each
/// of them says, how far apart the periodic ones are, and the two conditions under which
/// any reminder is held back.
///
/// The two reminders -- the one on unlock and the periodic one -- are independent. Each has
/// its own switch and its own message, and neither switch has any say over the other: the
/// periodic schedule still restarts on every unlock with the unlock reminder turned off,
/// and the unlock reminder still appears with the periodic one turned off.
///
/// The interval is kept as whole minutes because that is the unit the user types and the
/// unit the tray tooltip counts down in; anything finer would be a number nobody can enter.
/// The bounds are not decoration -- a zero would turn the scheduler into a window every
/// tick, and they are enforced on the way out of the registry as well as in the dialog,
/// because a hand-edited registry value must not be able to do that either.
///
/// The pause settings, the unlock switch and the two messages are init-only properties
/// rather than more positional parameters. That is not tidiness: every one of them has a
/// default that is the same on a fresh machine and in a hand-written test, so writing it
/// out at every call site would be repetition with nothing to say, and
/// "new ReminderSettings(true, 30)" goes on meaning exactly what it meant before they
/// existed.
/// </summary>
/// <param name="PeriodicEnabled">Whether the periodic reminder runs.</param>
/// <param name="IntervalMinutes">
/// The minutes between periodic reminders; <see cref="Normalize"/> clamps it to
/// <see cref="MinIntervalMinutes"/> to <see cref="MaxIntervalMinutes"/>.
/// </param>
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

    /// <summary>What both reminders said before the text became a setting.</summary>
    public const string DefaultMessage = "Ground yourself";

    /// <summary>
    /// Fifty characters. At the reminder window's 30 px type they can wrap to three lines, one
    /// more than fits between the dot and the Close button. The window is a fixed size on
    /// purpose -- it is placed by hand on the primary monitor -- so such a message is scaled
    /// down to fit rather than growing the window.
    /// </summary>
    public const int MaxMessageLength = 50;

    /// <summary>
    /// What a machine with no saved settings gets: both reminders on, every 30 minutes, both
    /// pauses on, and the same message on each. Everything but the two positional values
    /// comes from the property initializers below rather than from here, so each default is
    /// written once.
    /// </summary>
    public static readonly ReminderSettings Default = new(true, DefaultIntervalMinutes);

    /// <summary>
    /// Whether a reminder appears when the workstation is unlocked. Independent of
    /// <see cref="PeriodicEnabled"/>, and it has no bearing on the periodic schedule, which
    /// restarts on every unlock either way.
    /// </summary>
    public bool UnlockEnabled { get; init; } = true;

    /// <summary>What the reminder shown on unlock says. Never empty after <see cref="Normalize"/>.</summary>
    public string UnlockMessage { get; init; } = DefaultMessage;

    /// <summary>What the periodic reminder says. Never empty after <see cref="Normalize"/>.</summary>
    public string PeriodicMessage { get; init; } = DefaultMessage;

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

    /// <summary>
    /// Clamps both numbers into range and both messages into shape. Everything that builds
    /// settings goes through this.
    /// </summary>
    /// <returns>
    /// These settings with both numbers clamped and both messages normalized; this same
    /// instance when nothing needed changing.
    /// </returns>
    public ReminderSettings Normalize()
    {
        var normalized = IntervalMinutes switch
        {
            < MinIntervalMinutes => this with { IntervalMinutes = MinIntervalMinutes },
            > MaxIntervalMinutes => this with { IntervalMinutes = MaxIntervalMinutes },
            _ => this,
        };

        normalized = normalized.RetrySeconds switch
        {
            < MinRetrySeconds => normalized with { RetrySeconds = MinRetrySeconds },
            > MaxRetrySeconds => normalized with { RetrySeconds = MaxRetrySeconds },
            _ => normalized,
        };

        var unlockMessage = NormalizeMessage(normalized.UnlockMessage);
        var periodicMessage = NormalizeMessage(normalized.PeriodicMessage);

        // A "with" allocates, so it is skipped when there is nothing to change: Normalize
        // runs on every load and every save, and most of them find the messages as written.
        return ReferenceEquals(unlockMessage, normalized.UnlockMessage)
               && ReferenceEquals(periodicMessage, normalized.PeriodicMessage)
            ? normalized
            : normalized with { UnlockMessage = unlockMessage, PeriodicMessage = periodicMessage };
    }

    /// <summary>
    /// The message rule, in the same spirit as the clamps above: whatever was written is
    /// read as generously as possible rather than refused. Line breaks and tabs become
    /// spaces, because the box has one line of input and the window wraps for itself;
    /// surrounding space is dropped; nothing at all -- a wiped registry value, or a box the
    /// user cleared with that reminder switched off -- means the default; and anything past
    /// the limit is cut at it, so a hand-edited registry cannot hand the window a longer text
    /// than the dialog lets anyone type.
    /// </summary>
    /// <param name="message">The text as written; <c>null</c> reads as the default.</param>
    /// <returns>
    /// The text the rule makes of it. The SAME instance when nothing needed doing, which is
    /// what lets <see cref="Normalize"/> avoid copying the record for nothing.
    /// </returns>
    public static string NormalizeMessage(string? message)
    {
        if (message is null)
        {
            return DefaultMessage;
        }

        var text = message;

        if (text.AsSpan().IndexOfAny('\r', '\n', '\t') >= 0)
        {
            text = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        }

        var trimmed = text.AsSpan().Trim();

        if (trimmed.IsEmpty)
        {
            return DefaultMessage;
        }

        if (trimmed.Length > MaxMessageLength)
        {
            trimmed = trimmed[..MaxMessageLength].TrimEnd();
        }

        return trimmed.Length == message.Length && ReferenceEquals(text, message)
            ? message
            : trimmed.ToString();
    }

    /// <summary>Checks an interval typed in the dialog against the bounds.</summary>
    /// <param name="minutes">The interval, in minutes.</param>
    /// <returns>
    /// <c>true</c> when <paramref name="minutes"/> is something the dialog may accept: from
    /// <see cref="MinIntervalMinutes"/> to <see cref="MaxIntervalMinutes"/>.
    /// </returns>
    public static bool IsIntervalInRange(int minutes)
        => minutes is >= MinIntervalMinutes and <= MaxIntervalMinutes;

    /// <summary>Checks a retry gap typed in the dialog against the bounds.</summary>
    /// <param name="seconds">The retry gap, in seconds.</param>
    /// <returns>
    /// <c>true</c> when <paramref name="seconds"/> is something the dialog may accept: from
    /// <see cref="MinRetrySeconds"/> to <see cref="MaxRetrySeconds"/>.
    /// </returns>
    public static bool IsRetryInRange(int seconds)
        => seconds is >= MinRetrySeconds and <= MaxRetrySeconds;

    /// <summary>
    /// Checks a message typed in the dialog. For a reminder that is switched on, the dialog
    /// refuses an unacceptable message rather than falling back, because there is a user
    /// standing there to tell; for one that is off it saves the default instead. The fallback
    /// in <see cref="NormalizeMessage"/> is for the registry, where there is nobody to tell.
    /// </summary>
    /// <param name="message">The text as typed; <c>null</c> is refused.</param>
    /// <returns>
    /// <c>true</c> when <paramref name="message"/> is something the dialog may accept:
    /// something other than whitespace, and at most <see cref="MaxMessageLength"/> characters
    /// once trimmed.
    /// </returns>
    public static bool IsMessageAcceptable(string? message)
    {
        if (message is null)
        {
            return false;
        }

        var trimmed = message.AsSpan().Trim();
        return !trimmed.IsEmpty && trimmed.Length <= MaxMessageLength;
    }
}
