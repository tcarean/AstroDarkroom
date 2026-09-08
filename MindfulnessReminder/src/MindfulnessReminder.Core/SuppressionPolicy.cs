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

/// <summary>Why a reminder is being held back, or <see cref="None"/> when it is not.</summary>
public enum ReminderSuppression
{
    None,

    /// <summary>The microphone or the camera is live: a call, a meeting, a recording.</summary>
    MicrophoneInUse,

    /// <summary>A full-screen application or Windows presentation mode.</summary>
    FullScreen,
}

/// <summary>
/// Whether a reminder may appear right now. The whole of the two "pause" settings is here,
/// as one pure function over the settings and a snapshot, which is what makes a feature
/// that otherwise needs a real meeting to exercise into an ordinary unit test.
///
/// The two checks are independent -- either switch works with the other off -- and they are
/// deliberately NOT tied to the periodic reminder being enabled, because the unlock reminder
/// obeys them too.
/// </summary>
public static class SuppressionPolicy
{
    public static ReminderSuppression Evaluate(ReminderSettings settings, ActivitySnapshot activity)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // The microphone is asked first so that a call being screen-shared -- both signals at
        // once, and the common case -- always gives the same reason. The tooltip shows it,
        // and a reason that flickered between the two would look like a fault.
        if (settings.PauseWhileMicrophoneInUse && activity.CaptureInUse)
        {
            return ReminderSuppression.MicrophoneInUse;
        }

        if (settings.PauseWhileFullScreen && Occupies(activity.Foreground))
        {
            return ReminderSuppression.FullScreen;
        }

        return ReminderSuppression.None;
    }

    /// <summary>
    /// Only the two states that mean a window is genuinely in the way.
    ///
    /// UserAway must NOT be one of them. It is what Windows reports behind a lock screen,
    /// and it can still be the answer for a moment after an unlock -- long enough to swallow
    /// the unlock reminder, which is the one thing this app has always done. Nothing is lost
    /// by ignoring it either: the schedule is suspended for the whole of a lock anyway.
    ///
    /// QuietHours and Unknown are left out for the same reason in reverse -- neither was
    /// asked for, and a state Windows adds in a future build must not be able to switch the
    /// reminder off by accident.
    /// </summary>
    private static bool Occupies(ForegroundState state)
        => state is ForegroundState.FullScreen or ForegroundState.Presenting;
}
