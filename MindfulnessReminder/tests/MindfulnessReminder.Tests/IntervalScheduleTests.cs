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
/// The deadline arithmetic, which is where the whole periodic feature actually lives. The
/// interesting cases are the ones a manual test cannot reach in reasonable time: a lock
/// longer than the interval, a clock that moves backwards, and the promise that a reminder
/// is granted the full interval after every unlock no matter how many there were.
/// </summary>
public class IntervalScheduleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 8, 9, 0, 0, TimeSpan.Zero);

    private static IntervalSchedule Create(int minutes = 30, bool enabled = true)
        => new(new ReminderSettings(enabled, minutes));

    [Fact]
    public void A_fresh_schedule_is_not_counting_until_it_is_started()
    {
        var schedule = Create();

        Assert.False(schedule.IsCountingDown);
        Assert.Null(schedule.Remaining(T0));
    }

    [Fact]
    public void Restart_puts_the_whole_interval_on_the_clock()
    {
        var schedule = Create(30);

        schedule.Restart(T0);

        Assert.True(schedule.IsCountingDown);
        Assert.Equal(TimeSpan.FromMinutes(30), schedule.Remaining(T0));
    }

    [Fact]
    public void Nothing_is_due_before_the_interval_has_passed()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        Assert.False(schedule.TryConsumeDue(T0 + TimeSpan.FromMinutes(29.9)));
    }

    [Fact]
    public void The_reminder_is_due_exactly_at_the_interval()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        Assert.True(schedule.TryConsumeDue(T0 + TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void Becoming_due_re_arms_for_another_full_interval()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        var due = T0 + TimeSpan.FromMinutes(30);
        schedule.TryConsumeDue(due);

        Assert.False(schedule.TryConsumeDue(due + TimeSpan.FromSeconds(1)));
        Assert.True(schedule.TryConsumeDue(due + TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void A_disabled_schedule_never_counts_and_never_fires()
    {
        var schedule = Create(30, enabled: false);

        schedule.Restart(T0);

        Assert.False(schedule.IsCountingDown);
        Assert.Null(schedule.Remaining(T0));
        Assert.False(schedule.TryConsumeDue(T0 + TimeSpan.FromHours(10)));
    }

    [Fact]
    public void A_lock_stops_the_countdown()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        schedule.Suspend();

        Assert.True(schedule.IsSuspended);
        Assert.False(schedule.IsCountingDown);
        Assert.Null(schedule.Remaining(T0 + TimeSpan.FromMinutes(5)));
    }

    /// <summary>
    /// The case the requirement calls out by name: 10 minutes left, the machine is locked,
    /// the user is away for longer than the whole interval, and on their return the reminder
    /// is due 30 minutes after the unlock rather than immediately.
    /// </summary>
    [Fact]
    public void A_lock_longer_than_the_interval_still_grants_the_full_interval_after_unlock()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        var locked = T0 + TimeSpan.FromMinutes(20);   // 10 minutes were left
        schedule.Suspend();

        var unlocked = locked + TimeSpan.FromHours(3);
        schedule.Resume(unlocked);

        Assert.Equal(TimeSpan.FromMinutes(30), schedule.Remaining(unlocked));
        Assert.False(schedule.TryConsumeDue(unlocked + TimeSpan.FromMinutes(29)));
        Assert.True(schedule.TryConsumeDue(unlocked + TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void Any_number_of_lock_unlock_cycles_leaves_the_full_interval_after_the_last_one()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        var now = T0;
        for (var i = 0; i < 25; i++)
        {
            now += TimeSpan.FromMinutes(7);
            schedule.Suspend();

            now += TimeSpan.FromMinutes(i * 13);   // away for wildly different lengths
            schedule.Resume(now);
        }

        Assert.Equal(TimeSpan.FromMinutes(30), schedule.Remaining(now));
        Assert.False(schedule.TryConsumeDue(now + TimeSpan.FromMinutes(29)));
        Assert.True(schedule.TryConsumeDue(now + TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void An_unlock_without_a_lock_still_restarts_the_countdown()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        var later = T0 + TimeSpan.FromMinutes(25);
        schedule.Resume(later);

        Assert.Equal(TimeSpan.FromMinutes(30), schedule.Remaining(later));
    }

    [Fact]
    public void An_unlock_does_not_restart_a_disabled_schedule()
    {
        var schedule = Create(30, enabled: false);

        schedule.Resume(T0);

        Assert.False(schedule.IsCountingDown);
    }

    [Fact]
    public void Applying_settings_restarts_the_countdown_from_zero()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        var later = T0 + TimeSpan.FromMinutes(25);
        schedule.Apply(new ReminderSettings(true, 10), later);

        Assert.Equal(TimeSpan.FromMinutes(10), schedule.Remaining(later));
    }

    [Fact]
    public void Switching_the_feature_off_stops_the_countdown_immediately()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        schedule.Apply(new ReminderSettings(false, 30), T0 + TimeSpan.FromMinutes(5));

        Assert.False(schedule.IsCountingDown);
    }

    [Fact]
    public void Switching_the_feature_back_on_starts_a_whole_interval()
    {
        var schedule = Create(30, enabled: false);
        schedule.Restart(T0);

        var later = T0 + TimeSpan.FromHours(2);
        schedule.Apply(new ReminderSettings(true, 45), later);

        Assert.Equal(TimeSpan.FromMinutes(45), schedule.Remaining(later));
    }

    [Fact]
    public void An_out_of_range_interval_is_clamped_rather_than_taken_literally()
    {
        var schedule = Create();

        schedule.Apply(new ReminderSettings(true, 0), T0);

        Assert.Equal(ReminderSettings.MinIntervalMinutes, schedule.Settings.IntervalMinutes);
        Assert.Equal(TimeSpan.FromMinutes(ReminderSettings.MinIntervalMinutes), schedule.Remaining(T0));
    }

    /// <summary>
    /// An NTP correction or a hand-set clock can move time backwards under the deadline. The
    /// naive reading postpones the reminder by however far it jumped; the guard notices that
    /// the deadline is further out than a whole interval could put it, and starts over.
    /// </summary>
    [Fact]
    public void A_clock_that_jumps_backwards_restarts_instead_of_postponing_the_reminder()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        var jumped = T0 - TimeSpan.FromHours(1);

        Assert.False(schedule.TryConsumeDue(jumped));
        Assert.Equal(TimeSpan.FromMinutes(30), schedule.Remaining(jumped));
    }

    /// <summary>
    /// The other direction needs no guard and must not get one: a DispatcherTimer does not
    /// tick while the machine sleeps, so a deadline that passed during sleep is a reminder
    /// that is genuinely owed, and it fires on the first tick after waking.
    /// </summary>
    [Fact]
    public void A_deadline_that_passed_while_the_machine_slept_fires_on_the_first_tick_after()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        Assert.True(schedule.TryConsumeDue(T0 + TimeSpan.FromHours(4)));
    }

    [Fact]
    public void Remaining_never_goes_negative()
    {
        var schedule = Create(30);
        schedule.Restart(T0);

        Assert.Equal(TimeSpan.Zero, schedule.Remaining(T0 + TimeSpan.FromHours(4)));
    }

    // ------------------------------------------------------------------------- postponing

    [Fact]
    public void Postpone_moves_the_deadline_to_the_gap_it_is_given()
    {
        var schedule = new IntervalSchedule(ReminderSettings.Default);
        var now = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);

        schedule.Restart(now);
        schedule.Postpone(now, TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(20), schedule.Remaining(now));
    }

    [Fact]
    public void A_postponed_deadline_comes_due_when_the_gap_has_passed()
    {
        var schedule = new IntervalSchedule(ReminderSettings.Default);
        var now = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);

        schedule.Restart(now);
        schedule.Postpone(now, TimeSpan.FromSeconds(20));

        Assert.False(schedule.TryConsumeDue(now + TimeSpan.FromSeconds(19)));
        Assert.True(schedule.TryConsumeDue(now + TimeSpan.FromSeconds(20)));
    }

    /// <summary>
    /// THE regression. The backwards-clock guard asks whether a deadline is further out than
    /// this class could have set it; comparing that against the interval rather than against
    /// the span actually armed made every retry gap longer than the interval look like a
    /// clock jump, which reset the schedule and threw the postponement away.
    /// </summary>
    [Fact]
    public void A_postponement_longer_than_the_interval_is_not_read_as_a_clock_jump()
    {
        var settings = new ReminderSettings(true, 1);
        var schedule = new IntervalSchedule(settings);
        var now = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);

        schedule.Restart(now);
        schedule.Postpone(now, TimeSpan.FromMinutes(5));

        Assert.False(schedule.TryConsumeDue(now + TimeSpan.FromMinutes(1)));
        Assert.Equal(TimeSpan.FromMinutes(4), schedule.Remaining(now + TimeSpan.FromMinutes(1)));
    }

    /// <summary>The guard still has to work while a postponement is standing.</summary>
    [Fact]
    public void A_clock_jumped_backwards_during_a_postponement_still_restarts_the_countdown()
    {
        var schedule = new IntervalSchedule(ReminderSettings.Default);
        var now = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);

        schedule.Restart(now);
        schedule.Postpone(now, TimeSpan.FromSeconds(20));

        var jumped = now - TimeSpan.FromHours(2);

        Assert.False(schedule.TryConsumeDue(jumped));
        Assert.Equal(TimeSpan.FromMinutes(30), schedule.Remaining(jumped));
    }

    /// <summary>
    /// Nothing to postpone means nothing invented. A postponement that quietly created a
    /// deadline would be a countdown running with the feature switched off.
    /// </summary>
    [Fact]
    public void Postpone_does_nothing_while_the_feature_is_off()
    {
        var schedule = new IntervalSchedule(new ReminderSettings(false, 30));
        var now = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);

        schedule.Restart(now);
        schedule.Postpone(now, TimeSpan.FromSeconds(20));

        Assert.False(schedule.IsCountingDown);
        Assert.Null(schedule.Remaining(now));
    }

    [Fact]
    public void Postpone_does_nothing_while_the_workstation_is_locked()
    {
        var schedule = new IntervalSchedule(ReminderSettings.Default);
        var now = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);

        schedule.Restart(now);
        schedule.Suspend();
        schedule.Postpone(now, TimeSpan.FromSeconds(20));

        Assert.False(schedule.IsCountingDown);
    }

    /// <summary>A postponement lasts until the next thing that sets a deadline, and no longer.</summary>
    [Fact]
    public void A_restart_puts_the_whole_interval_back_over_a_postponement()
    {
        var schedule = new IntervalSchedule(ReminderSettings.Default);
        var now = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);

        schedule.Restart(now);
        schedule.Postpone(now, TimeSpan.FromSeconds(20));
        schedule.Restart(now);

        Assert.Equal(TimeSpan.FromMinutes(30), schedule.Remaining(now));
    }
}
