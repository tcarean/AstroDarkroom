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

public class ReminderCoordinatorTests
{
    private readonly FakeSessionMonitor _monitor = new();
    private readonly FakePresenter _presenter = new();
    private readonly FakeTimer _timer = new();
    private readonly FakeActivityMonitor _activity = new();
    private readonly MutableClock _clock = new(new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero));

    private ReminderCoordinator CreateCoordinator(
        TimeSpan? debounce = null,
        ReminderSettings? settings = null) =>
        new(
            _monitor,
            _presenter,
            _timer,
            settings ?? ReminderSettings.Default,
            _activity,
            _clock,
            debounce ?? ReminderCoordinator.DefaultDebounce,
            ReminderCoordinator.DefaultActivityRefresh);

    [Fact]
    public void R010_Start_starts_the_monitor()
    {
        using var coordinator = CreateCoordinator();

        coordinator.Start();

        Assert.True(_monitor.Started);
    }

    [Fact]
    public void R010_Start_twice_subscribes_only_once()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();
        coordinator.Start();

        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
    }

    [Fact]
    public void R010_An_unlock_shows_the_reminder()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
    }

    [Fact]
    public void R011_A_duplicate_unlock_within_the_debounce_window_is_ignored()
    {
        using var coordinator = CreateCoordinator(TimeSpan.FromSeconds(2));
        coordinator.Start();

        _monitor.RaiseUnlocked();
        _presenter.SimulateClosed();
        _clock.Advance(TimeSpan.FromMilliseconds(900));
        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
    }

    [Fact]
    public void R011_A_later_unlock_shows_the_reminder_again()
    {
        using var coordinator = CreateCoordinator(TimeSpan.FromSeconds(2));
        coordinator.Start();

        _monitor.RaiseUnlocked();
        _presenter.SimulateClosed();
        _clock.Advance(TimeSpan.FromSeconds(2));
        _monitor.RaiseUnlocked();

        Assert.Equal(2, _presenter.ShowCount);
    }

    [Fact]
    public void R012_An_unlock_while_a_reminder_is_open_raises_it_instead_of_opening_a_second()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _monitor.RaiseUnlocked();
        _clock.Advance(TimeSpan.FromMinutes(5));
        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
        Assert.Equal(1, _presenter.BringToFrontCount);
    }

    [Fact]
    public void R010_Unlocks_are_ignored_before_Start()
    {
        using var coordinator = CreateCoordinator();

        _monitor.RaiseUnlocked();

        Assert.Equal(0, _presenter.ShowCount);
    }

    [Fact]
    public void R010_Dispose_unsubscribes_and_disposes_the_monitor()
    {
        var coordinator = CreateCoordinator();
        coordinator.Start();
        coordinator.Dispose();

        _monitor.RaiseUnlocked();

        Assert.Equal(0, _presenter.ShowCount);
        Assert.True(_monitor.Disposed);
    }

    [Fact]
    public void R021_Dispose_stops_and_disposes_the_timer()
    {
        var coordinator = CreateCoordinator();
        coordinator.Start();
        coordinator.Dispose();

        _timer.RaiseTick();

        Assert.True(_timer.Disposed);
        Assert.False(_timer.IsRunning);
        Assert.Equal(0, _presenter.ShowCount);
    }

    [Fact]
    public void R010_Dispose_is_idempotent()
    {
        var coordinator = CreateCoordinator();
        coordinator.Start();

        coordinator.Dispose();
        coordinator.Dispose();

        Assert.Throws<ObjectDisposedException>(coordinator.Start);
    }

    // ---------------------------------------------------------------- periodic reminders

    [Fact]
    public void R020_The_countdown_starts_with_the_app()
    {
        using var coordinator = CreateCoordinator();

        coordinator.Start();

        Assert.True(_timer.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(30), coordinator.Remaining);
    }

    [Fact]
    public void R021_A_tick_before_the_interval_shows_nothing()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(29));
        _timer.RaiseTick();

        Assert.Equal(0, _presenter.ShowCount);
    }

    [Fact]
    public void R021_The_reminder_appears_once_the_interval_has_passed()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        Assert.Equal(1, _presenter.ShowCount);
    }

    [Fact]
    public void R021_The_periodic_reminder_repeats_every_interval()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        for (var i = 0; i < 3; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(30));
            _timer.RaiseTick();
            _presenter.SimulateClosed();
        }

        Assert.Equal(3, _presenter.ShowCount);
    }

    /// <summary>
    /// The deadline is spent even when no window can be opened. Without that, every
    /// subsequent tick would find a deadline still in the past and hammer BringToFront.
    /// </summary>
    [Fact]
    public void R023_A_due_reminder_over_an_open_one_raises_it_and_still_re_arms()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _presenter.SimulateShown();

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();
        _timer.RaiseTick();
        _timer.RaiseTick();

        Assert.Equal(1, _presenter.BringToFrontCount);
        Assert.Equal(TimeSpan.FromMinutes(30), coordinator.Remaining);
    }

    [Fact]
    public void R024_An_unlock_restarts_the_countdown()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(25));
        _monitor.RaiseUnlocked();

        Assert.Equal(TimeSpan.FromMinutes(30), coordinator.Remaining);
    }

    /// <summary>
    /// The requirement, end to end through the coordinator: 10 minutes left, a long lock,
    /// and the periodic reminder is due a full interval after the unlock. The one window
    /// shown along the way is the unlock reminder, not a periodic one.
    /// </summary>
    [Fact]
    public void R024_A_lock_longer_than_the_interval_postpones_the_reminder_to_a_full_interval_after_unlock()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(20));
        _monitor.RaiseLocked();

        // Nothing may be shown behind the lock screen, however long the machine sits there.
        _clock.Advance(TimeSpan.FromHours(3));
        _timer.RaiseTick();
        Assert.Equal(0, _presenter.ShowCount);

        _monitor.RaiseUnlocked();
        _presenter.SimulateClosed();
        Assert.Equal(1, _presenter.ShowCount);   // the unlock reminder

        _clock.Advance(TimeSpan.FromMinutes(29));
        _timer.RaiseTick();
        Assert.Equal(1, _presenter.ShowCount);

        _clock.Advance(TimeSpan.FromMinutes(1));
        _timer.RaiseTick();
        Assert.Equal(2, _presenter.ShowCount);   // the periodic one, 30 min after the unlock
    }

    [Fact]
    public void R024_Many_lock_unlock_cycles_leave_a_full_interval_after_the_last_unlock()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        for (var i = 0; i < 10; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(11));
            _monitor.RaiseLocked();

            _clock.Advance(TimeSpan.FromMinutes(i * 17));
            _monitor.RaiseUnlocked();
            _presenter.SimulateClosed();
        }

        Assert.Equal(TimeSpan.FromMinutes(30), coordinator.Remaining);
    }

    [Fact]
    public void R024_A_lock_stops_the_timer_and_clears_the_countdown()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _monitor.RaiseLocked();

        Assert.False(_timer.IsRunning);
        Assert.Null(coordinator.Remaining);
    }

    [Fact]
    public void R016_A_lock_never_shows_a_reminder_by_itself()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _monitor.RaiseLocked();

        Assert.Equal(0, _presenter.ShowCount);
    }

    [Fact]
    public void R027_A_disabled_feature_never_shows_a_periodic_reminder_and_runs_no_timer()
    {
        using var coordinator = CreateCoordinator(settings: new ReminderSettings(false, 30));
        coordinator.Start();

        _clock.Advance(TimeSpan.FromHours(6));
        _timer.RaiseTick();

        Assert.Equal(0, _presenter.ShowCount);
        Assert.False(_timer.IsRunning);
        Assert.Null(coordinator.Remaining);
    }

    /// <summary>The two switches are independent; this is one half of that, the other is below.</summary>
    [Fact]
    public void R027_Switching_the_periodic_reminder_off_leaves_the_unlock_reminder_alone()
    {
        using var coordinator = CreateCoordinator(settings: new ReminderSettings(false, 30));
        coordinator.Start();

        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
        Assert.Null(coordinator.Remaining);
    }

    [Fact]
    public void R026_Applying_settings_restarts_the_countdown_from_zero()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(25));
        coordinator.ApplySettings(new ReminderSettings(true, 10));

        Assert.Equal(TimeSpan.FromMinutes(10), coordinator.Remaining);
        Assert.Equal(new ReminderSettings(true, 10), coordinator.Settings);
    }

    [Fact]
    public void R026_Switching_the_feature_off_stops_the_timer()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        coordinator.ApplySettings(new ReminderSettings(false, 30));

        Assert.False(_timer.IsRunning);
    }

    [Fact]
    public void R026_Switching_the_feature_back_on_starts_the_timer_again()
    {
        using var coordinator = CreateCoordinator(settings: new ReminderSettings(false, 30));
        coordinator.Start();

        coordinator.ApplySettings(new ReminderSettings(true, 45));

        Assert.True(_timer.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(45), coordinator.Remaining);
    }

    [Fact]
    public void R021_The_countdown_change_event_fires_on_every_tick_and_on_every_reset()
    {
        using var coordinator = CreateCoordinator();

        var changes = 0;
        coordinator.CountdownChanged += (_, _) => changes++;

        coordinator.Start();          // 1
        _timer.RaiseTick();           // 2
        _monitor.RaiseLocked();       // 3
        _monitor.RaiseUnlocked();     // 4

        Assert.Equal(4, changes);
    }

    /// <summary>A periodic reminder counts as shown, so an unlock right after it does not stack.</summary>
    [Fact]
    public void R011_An_unlock_just_after_a_periodic_reminder_is_debounced()
    {
        using var coordinator = CreateCoordinator(TimeSpan.FromSeconds(2));
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();
        _presenter.SimulateClosed();

        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
    }

    // ------------------------------------------------- the two switches and the two messages

    private static readonly ReminderSettings TwoMessages = ReminderSettings.Default with
    {
        UnlockMessage = "Welcome back",
        PeriodicMessage = "Look away from the screen",
    };

    [Fact]
    public void R010_The_unlock_reminder_says_the_unlock_message()
    {
        using var coordinator = CreateCoordinator(settings: TwoMessages);
        coordinator.Start();

        _monitor.RaiseUnlocked();

        Assert.Equal(new[] { "Welcome back" }, _presenter.Messages);
    }

    [Fact]
    public void R021_The_periodic_reminder_says_the_periodic_message()
    {
        using var coordinator = CreateCoordinator(settings: TwoMessages);
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        Assert.Equal(new[] { "Look away from the screen" }, _presenter.Messages);
    }

    /// <summary>The other half of independence: the unlock switch alone silences the unlock.</summary>
    [Fact]
    public void R013_Switching_the_unlock_reminder_off_shows_nothing_on_unlock()
    {
        using var coordinator = CreateCoordinator(
            settings: ReminderSettings.Default with { UnlockEnabled = false });
        coordinator.Start();

        _monitor.RaiseUnlocked();

        Assert.Equal(0, _presenter.ShowCount);
    }

    /// <summary>
    /// The restart on unlock belongs to the periodic reminder, not to the unlock one, so the
    /// unlock switch must not be allowed to skip it: with 10 minutes left before a lock, the
    /// next reminder is still a whole interval after the unlock.
    /// </summary>
    [Fact]
    public void R013_Switching_the_unlock_reminder_off_still_restarts_the_countdown_on_unlock()
    {
        using var coordinator = CreateCoordinator(
            settings: ReminderSettings.Default with { UnlockEnabled = false });
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(20));
        _monitor.RaiseLocked();
        _clock.Advance(TimeSpan.FromHours(3));
        _monitor.RaiseUnlocked();

        Assert.Equal(TimeSpan.FromMinutes(30), coordinator.Remaining);
        Assert.True(_timer.IsRunning);

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        Assert.Equal(1, _presenter.ShowCount);
    }

    /// <summary>A reminder that is not going to appear has no reason to walk the registry.</summary>
    [Fact]
    public void R013_An_unlock_with_the_unlock_reminder_off_does_not_read_the_activity_monitor()
    {
        using var coordinator = CreateCoordinator(
            settings: ReminderSettings.Default with { UnlockEnabled = false });
        coordinator.Start();

        _monitor.RaiseUnlocked();

        Assert.Equal(0, _activity.ReadCount);
        Assert.Equal(ReminderSuppression.None, coordinator.Suppression);
    }

    /// <summary>
    /// Raising a window left open from before the lock is the unlock reminder acting, and
    /// the switch is off. The window is still there; it is simply not touched.
    /// </summary>
    [Fact]
    public void R013_An_unlock_with_the_unlock_reminder_off_leaves_an_open_reminder_where_it_is()
    {
        using var coordinator = CreateCoordinator(
            settings: ReminderSettings.Default with { UnlockEnabled = false });
        coordinator.Start();
        _presenter.SimulateShown();

        _monitor.RaiseUnlocked();

        Assert.Equal(0, _presenter.BringToFrontCount);
        Assert.Equal(0, _presenter.ShowCount);
    }

    [Fact]
    public void R027_With_both_reminders_off_nothing_ever_appears_and_no_timer_runs()
    {
        using var coordinator = CreateCoordinator(
            settings: new ReminderSettings(false, 30) { UnlockEnabled = false });
        coordinator.Start();

        _monitor.RaiseUnlocked();
        _clock.Advance(TimeSpan.FromHours(6));
        _timer.RaiseTick();
        _monitor.RaiseLocked();
        _monitor.RaiseUnlocked();

        Assert.Equal(0, _presenter.ShowCount);
        Assert.Equal(0, _presenter.BringToFrontCount);
        Assert.False(_timer.IsRunning);
        Assert.Null(coordinator.Remaining);
    }

    /// <summary>
    /// The window on screen keeps the text it opened with. Swapping it for the periodic
    /// message would change a reminder under the user's eyes, and the deadline is served
    /// either way.
    /// </summary>
    [Fact]
    public void R023_A_periodic_reminder_due_over_the_unlock_reminder_raises_it_without_changing_its_text()
    {
        using var coordinator = CreateCoordinator(settings: TwoMessages);
        coordinator.Start();

        _monitor.RaiseUnlocked();
        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        Assert.Equal(new[] { "Welcome back" }, _presenter.Messages);
        Assert.Equal(1, _presenter.BringToFrontCount);
    }

    [Fact]
    public void R013_Applying_settings_that_switch_the_unlock_reminder_off_takes_effect_on_the_next_unlock()
    {
        using var coordinator = CreateCoordinator(TimeSpan.Zero);
        coordinator.Start();

        _monitor.RaiseUnlocked();
        _presenter.SimulateClosed();
        Assert.Equal(1, _presenter.ShowCount);

        coordinator.ApplySettings(ReminderSettings.Default with { UnlockEnabled = false });
        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
    }

    [Fact]
    public void R044_Applying_new_messages_takes_effect_on_the_next_reminder()
    {
        using var coordinator = CreateCoordinator(TimeSpan.Zero);
        coordinator.Start();

        _monitor.RaiseUnlocked();
        _presenter.SimulateClosed();

        coordinator.ApplySettings(TwoMessages);
        _monitor.RaiseUnlocked();
        _presenter.SimulateClosed();
        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        Assert.Equal(
            new[] { ReminderSettings.DefaultMessage, "Welcome back", "Look away from the screen" },
            _presenter.Messages);
    }

    // ------------------------------------------------------- pausing for what the user is doing

    [Fact]
    public void R030_A_due_reminder_is_held_back_while_the_microphone_is_live()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        Assert.Equal(0, _presenter.ShowCount);
        Assert.Equal(ReminderSuppression.MicrophoneInUse, coordinator.Suppression);
    }

    /// <summary>The retry gap, not another whole interval: the point is to come back soon.</summary>
    [Fact]
    public void R033_A_held_back_reminder_is_looked_at_again_after_the_retry_gap()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        Assert.Equal(TimeSpan.FromSeconds(20), coordinator.Remaining);
    }

    [Fact]
    public void R033_A_reminder_stays_held_back_for_as_long_as_the_call_lasts()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        // An hour of retries, twenty seconds apart, and not one window.
        for (var i = 0; i < 180; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(20));
            _timer.RaiseTick();
        }

        Assert.Equal(0, _presenter.ShowCount);
        Assert.Equal(180, _activity.ReadCount - 1);
    }

    [Fact]
    public void R033_The_reminder_appears_on_the_first_retry_after_the_call_ends()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();
        Assert.Equal(0, _presenter.ShowCount);

        _activity.Snapshot = ActivitySnapshot.Idle;

        _clock.Advance(TimeSpan.FromSeconds(20));
        _timer.RaiseTick();

        Assert.Equal(1, _presenter.ShowCount);
        Assert.Equal(ReminderSuppression.None, coordinator.Suppression);
    }

    /// <summary>
    /// A whole interval, not the retry gap. Once the reminder has been shown the schedule is
    /// back to normal, and a countdown left on 20 seconds would fire again immediately.
    /// </summary>
    [Fact]
    public void R033_The_full_interval_returns_once_a_held_back_reminder_has_been_shown()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };
        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        _activity.Snapshot = ActivitySnapshot.Idle;
        _clock.Advance(TimeSpan.FromSeconds(20));
        _timer.RaiseTick();

        Assert.Equal(TimeSpan.FromMinutes(30), coordinator.Remaining);
    }

    [Fact]
    public void R031_A_due_reminder_is_held_back_by_a_full_screen_application()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { Foreground = ForegroundState.FullScreen };

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        Assert.Equal(0, _presenter.ShowCount);
        Assert.Equal(ReminderSuppression.FullScreen, coordinator.Suppression);
    }

    /// <summary>
    /// The regression behind the guard in IntervalSchedule. A retry gap longer than the
    /// interval used to look like a clock jumped backwards, which reset the schedule and lost
    /// the postponement -- so the reminder appeared in the middle of the call after all.
    /// </summary>
    [Fact]
    public void R025_A_retry_gap_longer_than_the_interval_is_not_mistaken_for_a_clock_jump()
    {
        var settings = new ReminderSettings(true, ReminderSettings.MinIntervalMinutes)
        {
            RetrySeconds = ReminderSettings.MaxRetrySeconds,
        };

        using var coordinator = CreateCoordinator(settings: settings);
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        _clock.Advance(TimeSpan.FromMinutes(1));
        _timer.RaiseTick();

        Assert.Equal(TimeSpan.FromSeconds(300), coordinator.Remaining);

        // Well inside the retry gap: the postponement has to still be standing.
        _clock.Advance(TimeSpan.FromSeconds(90));
        _timer.RaiseTick();

        Assert.Equal(0, _presenter.ShowCount);
        Assert.Equal(TimeSpan.FromSeconds(210), coordinator.Remaining);
    }

    [Fact]
    public void R014_The_unlock_reminder_is_skipped_while_the_microphone_is_live()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        _monitor.RaiseUnlocked();

        Assert.Equal(0, _presenter.ShowCount);
        Assert.Equal(ReminderSuppression.MicrophoneInUse, coordinator.Suppression);
    }

    /// <summary>
    /// Skipped, not postponed: the unlock restarted the schedule on its way past, so a
    /// reminder is already coming, and a one-shot retry beside it would be a second deadline.
    /// </summary>
    [Fact]
    public void R014_A_skipped_unlock_reminder_still_leaves_a_full_interval_running()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        _clock.Advance(TimeSpan.FromMinutes(25));
        _monitor.RaiseUnlocked();

        Assert.Equal(TimeSpan.FromMinutes(30), coordinator.Remaining);
    }

    /// <summary>
    /// With the periodic reminder off there is no countdown, so no tick reads the monitor
    /// again, and a reason kept from a skipped unlock would sit in the tooltip long after
    /// the call ended. Nothing is held back either: the unlock reminder was skipped.
    /// </summary>
    [Fact]
    public void R060_With_the_periodic_reminder_off_a_skipped_unlock_leaves_no_reason_behind()
    {
        using var coordinator = CreateCoordinator(
            settings: ReminderSettings.Default with { PeriodicEnabled = false });
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        _monitor.RaiseUnlocked();

        Assert.Equal(0, _presenter.ShowCount);
        Assert.Null(coordinator.Remaining);
        Assert.Equal(ReminderSuppression.None, coordinator.Suppression);
    }

    /// <summary>
    /// UserAway is what Windows reports behind a lock screen, and it can still be the answer
    /// in the instant after an unlock. If it suppressed, the unlock reminder -- the oldest
    /// behaviour this app has -- would go missing on exactly the event it exists for.
    /// </summary>
    [Fact]
    public void R015_An_unlock_still_shows_its_reminder_when_windows_says_the_user_is_away()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { Foreground = ForegroundState.UserAway };

        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
    }

    /// <summary>
    /// An unlock grants a whole fresh interval, so whatever was being waited on is gone --
    /// including down the early-return path taken when a reminder is already on screen, where
    /// a stale reason would sit in the tooltip beside a countdown that is actually running.
    /// </summary>
    [Fact]
    public void R035_An_unlock_forgets_that_a_reminder_was_being_held_back()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };
        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();
        Assert.Equal(ReminderSuppression.MicrophoneInUse, coordinator.Suppression);

        _presenter.SimulateShown();
        _monitor.RaiseUnlocked();

        Assert.Equal(ReminderSuppression.None, coordinator.Suppression);
        Assert.Equal(TimeSpan.FromMinutes(30), coordinator.Remaining);
    }

    [Fact]
    public void R035_A_lock_forgets_that_a_reminder_was_being_held_back()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };
        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        _monitor.RaiseLocked();

        Assert.Equal(ReminderSuppression.None, coordinator.Suppression);
        Assert.Null(coordinator.Remaining);
    }

    [Fact]
    public void R035_Applying_settings_forgets_that_a_reminder_was_being_held_back()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };
        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        coordinator.ApplySettings(new ReminderSettings(true, 10));

        Assert.Equal(ReminderSuppression.None, coordinator.Suppression);
        Assert.Equal(TimeSpan.FromMinutes(10), coordinator.Remaining);
    }

    [Fact]
    public void R032_With_both_pauses_switched_off_nothing_is_ever_held_back()
    {
        var settings = ReminderSettings.Default with
        {
            PauseWhileMicrophoneInUse = false,
            PauseWhileFullScreen = false,
        };

        using var coordinator = CreateCoordinator(settings: settings);
        coordinator.Start();

        _activity.Snapshot = new ActivitySnapshot(true, true, ForegroundState.FullScreen);

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        Assert.Equal(1, _presenter.ShowCount);
    }

    /// <summary>
    /// With both switches off the monitor is not asked at all -- an answer nobody would look
    /// at is not worth a walk through the registry.
    /// </summary>
    [Fact]
    public void R032_With_both_pauses_switched_off_the_monitor_is_never_read()
    {
        var settings = ReminderSettings.Default with
        {
            PauseWhileMicrophoneInUse = false,
            PauseWhileFullScreen = false,
        };

        using var coordinator = CreateCoordinator(settings: settings);
        coordinator.Start();

        for (var i = 0; i < 120; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(30));
            _timer.RaiseTick();
        }

        _monitor.RaiseUnlocked();

        Assert.Equal(0, _activity.ReadCount);
    }

    /// <summary>
    /// Ticks where nothing is due still refresh the reading, so the tooltip can say "paused"
    /// during a call rather than only in the minute after a reminder was held back -- but at
    /// the refresh interval, not at the tick interval. At one tick a second, reading on each
    /// would be 1800 registry walks per reminder.
    /// </summary>
    [Fact]
    public void R034_Idle_ticks_refresh_the_reading_at_the_refresh_interval_not_on_every_tick()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        for (var i = 0; i < 100; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            _timer.RaiseTick();
        }

        // 100 seconds at one read per ten, give or take the first tick.
        Assert.InRange(_activity.ReadCount, 10, 11);
    }

    /// <summary>
    /// The reason this refresh exists. Joining a call does not make a reminder due, so
    /// without it the tray icon would go on counting down to a reminder that is not going to
    /// arrive, and the feature would be invisible until the interval happened to elapse.
    /// </summary>
    [Fact]
    public void R034_Joining_a_call_shows_up_in_the_tooltip_without_waiting_for_a_reminder()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        _clock.Advance(ReminderCoordinator.DefaultActivityRefresh);
        _timer.RaiseTick();

        Assert.Equal(ReminderSuppression.MicrophoneInUse, coordinator.Suppression);
        Assert.Equal(0, _presenter.ShowCount);
    }

    [Fact]
    public void R034_Leaving_a_call_clears_the_tooltip_without_waiting_for_a_reminder()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };
        _clock.Advance(ReminderCoordinator.DefaultActivityRefresh);
        _timer.RaiseTick();
        Assert.Equal(ReminderSuppression.MicrophoneInUse, coordinator.Suppression);

        _activity.Snapshot = ActivitySnapshot.Idle;
        _clock.Advance(ReminderCoordinator.DefaultActivityRefresh);
        _timer.RaiseTick();

        Assert.Equal(ReminderSuppression.None, coordinator.Suppression);
    }

    /// <summary>Switching a pause on has to show in the tooltip at once, not a refresh later.</summary>
    [Fact]
    public void R035_Applying_settings_takes_a_fresh_reading_on_the_next_tick()
    {
        var off = ReminderSettings.Default with
        {
            PauseWhileMicrophoneInUse = false,
            PauseWhileFullScreen = false,
        };

        using var coordinator = CreateCoordinator(settings: off);
        coordinator.Start();

        _activity.Snapshot = ActivitySnapshot.Idle with { MicrophoneInUse = true };

        _clock.Advance(TimeSpan.FromSeconds(1));
        _timer.RaiseTick();
        Assert.Equal(ReminderSuppression.None, coordinator.Suppression);

        coordinator.ApplySettings(ReminderSettings.Default);

        _clock.Advance(TimeSpan.FromSeconds(1));
        _timer.RaiseTick();

        Assert.Equal(ReminderSuppression.MicrophoneInUse, coordinator.Suppression);
    }

    private sealed class FakeSessionMonitor : ISessionMonitor
    {
        public bool Started { get; private set; }

        public bool Disposed { get; private set; }

        public event EventHandler? Unlocked;

        public event EventHandler? Locked;

        public void Start() => Started = true;

        public void RaiseUnlocked() => Unlocked?.Invoke(this, EventArgs.Empty);

        public void RaiseLocked() => Locked?.Invoke(this, EventArgs.Empty);

        public void Dispose() => Disposed = true;
    }

    private sealed class FakePresenter : IReminderPresenter
    {
        public int ShowCount { get; private set; }

        public int BringToFrontCount { get; private set; }

        public bool IsShowing { get; private set; }

        /// <summary>Every message handed to Show, in order, so a test can say which reminder it was.</summary>
        public List<string> Messages { get; } = [];

        public void Show(string message)
        {
            ShowCount++;
            Messages.Add(message);
            IsShowing = true;
        }

        public void BringToFront() => BringToFrontCount++;

        public void SimulateShown() => IsShowing = true;

        public void SimulateClosed() => IsShowing = false;
    }

    /// <summary>Ticks only when the test says so, so half an hour costs nothing.</summary>
    private sealed class FakeTimer : IPeriodicTimer
    {
        public bool IsRunning { get; private set; }

        public bool Disposed { get; private set; }

        public event EventHandler? Tick;

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void RaiseTick() => Tick?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            IsRunning = false;
            Disposed = true;
        }
    }

    /// <summary>Sees whatever the test says, and counts how often it was asked.</summary>
    private sealed class FakeActivityMonitor : IActivityMonitor
    {
        public ActivitySnapshot Snapshot { get; set; } = ActivitySnapshot.Idle;

        public int ReadCount { get; private set; }

        public ActivitySnapshot Read()
        {
            ReadCount++;
            return Snapshot;
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void Advance(TimeSpan delta) => UtcNow += delta;
    }
}
