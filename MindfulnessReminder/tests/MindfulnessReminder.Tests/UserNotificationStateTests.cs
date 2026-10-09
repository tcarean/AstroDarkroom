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

using MindfulnessReminder.Core;

namespace MindfulnessReminder.Tests;

/// <summary>
/// The QUERY_USER_NOTIFICATION_STATE table. It is worth a test of its own because the values
/// are magic numbers out of a Windows header: an off-by-one here would not fail to compile,
/// and would surface as a reminder that either never appears or appears in the middle of a
/// film -- neither of which any other test in the suite would catch.
/// </summary>
public class UserNotificationStateTests
{
    [Theory]
    [InlineData(UserNotificationState.AcceptsNotifications, ForegroundState.Normal)]
    [InlineData(UserNotificationState.Busy, ForegroundState.FullScreen)]
    [InlineData(UserNotificationState.RunningDirect3DFullScreen, ForegroundState.FullScreen)]
    [InlineData(UserNotificationState.RunningWindowsStoreApp, ForegroundState.FullScreen)]
    [InlineData(UserNotificationState.PresentationMode, ForegroundState.Presenting)]
    [InlineData(UserNotificationState.QuietTime, ForegroundState.QuietHours)]
    [InlineData(UserNotificationState.NotPresent, ForegroundState.UserAway)]
    public void R031_Each_documented_state_maps_to_the_reading_the_app_acts_on(
        int shell,
        ForegroundState expected)
    {
        Assert.Equal(expected, UserNotificationState.FromShellState(shell));
    }

    /// <summary>
    /// Windows has added states to this enum before. An unrecognised one has to read as
    /// Unknown, which suppresses nothing: a future build of Windows must not be able to
    /// switch the reminder off by accident.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void R031_Anything_outside_the_documented_range_reads_as_unknown(int shell)
    {
        Assert.Equal(ForegroundState.Unknown, UserNotificationState.FromShellState(shell));
    }

    /// <summary>The values come from ShellAPI.h; pinning them is the point of the table above.</summary>
    [Fact]
    public void R031_The_constants_match_the_windows_header()
    {
        Assert.Equal(1, UserNotificationState.NotPresent);
        Assert.Equal(2, UserNotificationState.Busy);
        Assert.Equal(3, UserNotificationState.RunningDirect3DFullScreen);
        Assert.Equal(4, UserNotificationState.PresentationMode);
        Assert.Equal(5, UserNotificationState.AcceptsNotifications);
        Assert.Equal(6, UserNotificationState.QuietTime);
        Assert.Equal(7, UserNotificationState.RunningWindowsStoreApp);
    }
}
