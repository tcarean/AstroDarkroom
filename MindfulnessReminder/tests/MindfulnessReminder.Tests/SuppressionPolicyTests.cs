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
/// The whole of the two "pause" settings. Neither can be exercised on a real machine without
/// a real call or a real game, which is the reason the decision is a pure function over a
/// snapshot rather than a few ifs next to the code that reads the registry.
/// </summary>
public class SuppressionPolicyTests
{
    private static ReminderSettings Settings(bool microphone, bool fullScreen)
        => ReminderSettings.Default with
        {
            PauseWhileMicrophoneInUse = microphone,
            PauseWhileFullScreen = fullScreen,
        };

    [Fact]
    public void An_idle_desktop_suppresses_nothing()
    {
        Assert.Equal(
            ReminderSuppression.None,
            SuppressionPolicy.Evaluate(ReminderSettings.Default, ActivitySnapshot.Idle));
    }

    [Fact]
    public void A_live_microphone_holds_the_reminder_back()
    {
        var activity = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        Assert.Equal(
            ReminderSuppression.MicrophoneInUse,
            SuppressionPolicy.Evaluate(ReminderSettings.Default, activity));
    }

    /// <summary>A camera on with the microphone muted in hardware is still a call.</summary>
    [Fact]
    public void A_live_camera_counts_the_same_as_a_microphone()
    {
        var activity = ActivitySnapshot.Idle with { CameraInUse = true };

        Assert.Equal(
            ReminderSuppression.MicrophoneInUse,
            SuppressionPolicy.Evaluate(ReminderSettings.Default, activity));
    }

    [Theory]
    [InlineData(ForegroundState.FullScreen)]
    [InlineData(ForegroundState.Presenting)]
    public void A_full_screen_or_presenting_desktop_holds_the_reminder_back(ForegroundState state)
    {
        var activity = ActivitySnapshot.Idle with { Foreground = state };

        Assert.Equal(
            ReminderSuppression.FullScreen,
            SuppressionPolicy.Evaluate(ReminderSettings.Default, activity));
    }

    /// <summary>
    /// UserAway is what Windows reports behind a lock screen, and it can still be the answer
    /// for a moment after an unlock -- long enough to swallow the unlock reminder, which is
    /// the one thing this app has always done. QuietHours and Unknown were never asked for.
    /// </summary>
    [Theory]
    [InlineData(ForegroundState.UserAway)]
    [InlineData(ForegroundState.QuietHours)]
    [InlineData(ForegroundState.Unknown)]
    [InlineData(ForegroundState.Normal)]
    public void No_other_foreground_state_holds_anything_back(ForegroundState state)
    {
        var activity = ActivitySnapshot.Idle with { Foreground = state };

        Assert.Equal(
            ReminderSuppression.None,
            SuppressionPolicy.Evaluate(ReminderSettings.Default, activity));
    }

    [Fact]
    public void The_microphone_switch_turns_off_only_the_microphone_check()
    {
        var settings = Settings(microphone: false, fullScreen: true);
        var activity = new ActivitySnapshot(true, true, ForegroundState.FullScreen);

        Assert.Equal(ReminderSuppression.FullScreen, SuppressionPolicy.Evaluate(settings, activity));
    }

    [Fact]
    public void The_full_screen_switch_turns_off_only_the_full_screen_check()
    {
        var settings = Settings(microphone: true, fullScreen: false);
        var activity = new ActivitySnapshot(true, false, ForegroundState.FullScreen);

        Assert.Equal(ReminderSuppression.MicrophoneInUse, SuppressionPolicy.Evaluate(settings, activity));
    }

    [Fact]
    public void With_both_switches_off_nothing_is_ever_held_back()
    {
        var settings = Settings(microphone: false, fullScreen: false);
        var activity = new ActivitySnapshot(true, true, ForegroundState.Presenting);

        Assert.Equal(ReminderSuppression.None, SuppressionPolicy.Evaluate(settings, activity));
    }

    /// <summary>
    /// A call being screen-shared trips both. The reason reaches the tooltip, so it has to be
    /// the same one every time rather than flickering between the two.
    /// </summary>
    [Fact]
    public void When_both_apply_the_microphone_is_the_reason_given()
    {
        var activity = new ActivitySnapshot(true, false, ForegroundState.FullScreen);

        Assert.Equal(
            ReminderSuppression.MicrophoneInUse,
            SuppressionPolicy.Evaluate(ReminderSettings.Default, activity));
    }

    /// <summary>The pauses are not part of the periodic reminder: the unlock one obeys them too.</summary>
    [Fact]
    public void The_pauses_apply_even_with_the_periodic_reminder_switched_off()
    {
        var settings = ReminderSettings.Default with { PeriodicEnabled = false };
        var activity = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        Assert.Equal(ReminderSuppression.MicrophoneInUse, SuppressionPolicy.Evaluate(settings, activity));
    }

    [Fact]
    public void Both_pauses_ship_switched_on()
    {
        Assert.True(ReminderSettings.Default.PauseWhileMicrophoneInUse);
        Assert.True(ReminderSettings.Default.PauseWhileFullScreen);
        Assert.True(ReminderSettings.Default.PausesForAnything);
    }

    [Fact]
    public void With_both_pauses_off_the_monitor_is_not_worth_asking()
    {
        Assert.False(Settings(microphone: false, fullScreen: false).PausesForAnything);
    }
}
