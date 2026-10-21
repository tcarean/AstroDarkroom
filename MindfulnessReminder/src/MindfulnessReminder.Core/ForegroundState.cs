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
/// What the desktop is doing, as far as it bears on whether a reminder may appear. These are
/// the answers Windows gives to its own "may I show a notification right now?" question,
/// reduced to the distinctions this app acts on.
/// </summary>
public enum ForegroundState
{
    /// <summary>Nothing in the way.</summary>
    Normal,

    /// <summary>A full-screen application owns the desktop: a game, a film, a shared screen.</summary>
    FullScreen,

    /// <summary>Windows presentation settings are on -- the user has asked not to be interrupted.</summary>
    Presenting,

    /// <summary>Quiet hours / Focus Assist. Read and mapped, but nothing acts on it yet.</summary>
    QuietHours,

    /// <summary>Screen saver, lock screen, or a session switched away from.</summary>
    UserAway,

    /// <summary>Windows answered with a state this build does not know about.</summary>
    Unknown,
}

/// <summary>
/// The mapping from SHQueryUserNotificationState's QUERY_USER_NOTIFICATION_STATE to
/// <see cref="ForegroundState"/>.
///
/// It takes the raw integer and lives in Core, rather than sitting in the adapter that
/// P/Invokes for it, because the mapping IS the rule -- which states of the desktop mean
/// "do not interrupt". The adapter is then one call with nothing left to get wrong, and the
/// table below is a unit test rather than a thing to be checked by hand on a real machine.
/// </summary>
public static class UserNotificationState
{
    // QUERY_USER_NOTIFICATION_STATE, from ShellAPI.h. Spelled out rather than used as bare
    // numbers so the switch below reads as the documentation it mirrors.
    public const int NotPresent = 1;
    public const int Busy = 2;
    public const int RunningDirect3DFullScreen = 3;
    public const int PresentationMode = 4;
    public const int AcceptsNotifications = 5;
    public const int QuietTime = 6;
    public const int RunningWindowsStoreApp = 7;

    /// <summary>
    /// Maps a QUERY_USER_NOTIFICATION_STATE to the state this app acts on.
    ///
    /// Anything outside the documented range maps to <see cref="ForegroundState.Unknown"/>,
    /// which suppresses nothing. Windows has added states to this enum before, and a future
    /// one must not be able to switch the reminder off by accident.
    /// </summary>
    /// <param name="state">
    /// The state SHQueryUserNotificationState wrote to its out parameter; 0 when the call
    /// failed.
    /// </param>
    /// <returns>
    /// The state it stands for; <see cref="ForegroundState.Unknown"/> for any other number.
    /// </returns>
    public static ForegroundState FromShellState(int state) => state switch
    {
        AcceptsNotifications => ForegroundState.Normal,

        // QUNS_BUSY is documented as "a full-screen application is running OR presentation
        // settings are applied", so it cannot be told apart from the two below. Both
        // suppress, so the ambiguity costs nothing; the name follows the commoner cause.
        Busy or RunningDirect3DFullScreen or RunningWindowsStoreApp => ForegroundState.FullScreen,

        PresentationMode => ForegroundState.Presenting,
        QuietTime => ForegroundState.QuietHours,
        NotPresent => ForegroundState.UserAway,
        _ => ForegroundState.Unknown,
    };
}
