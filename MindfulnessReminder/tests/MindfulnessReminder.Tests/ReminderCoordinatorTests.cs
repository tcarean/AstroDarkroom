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
    private readonly MutableClock _clock = new(new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero));

    private ReminderCoordinator CreateCoordinator(
        TimeSpan? debounce = null,
        ReminderSettings? settings = null) =>
        new(
            _monitor,
            _presenter,
            _timer,
            settings ?? ReminderSettings.Default,
            _clock,
            debounce ?? ReminderCoordinator.DefaultDebounce);

    [Fact]
    public void Start_starts_the_monitor()
    {
        using var coordinator = CreateCoordinator();

        coordinator.Start();

        Assert.True(_monitor.Started);
    }

    [Fact]
    public void Start_twice_subscribes_only_once()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();
        coordinator.Start();

        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
    }

    [Fact]
    public void An_unlock_shows_the_reminder()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
    }

    [Fact]
    public void A_duplicate_unlock_within_the_debounce_window_is_ignored()
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
    public void A_later_unlock_shows_the_reminder_again()
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
    public void An_unlock_while_a_reminder_is_open_raises_it_instead_of_opening_a_second()
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
    public void Unlocks_are_ignored_before_Start()
    {
        using var coordinator = CreateCoordinator();

        _monitor.RaiseUnlocked();

        Assert.Equal(0, _presenter.ShowCount);
    }

    [Fact]
    public void Dispose_unsubscribes_and_disposes_the_monitor()
    {
        var coordinator = CreateCoordinator();
        coordinator.Start();
        coordinator.Dispose();

        _monitor.RaiseUnlocked();

        Assert.Equal(0, _presenter.ShowCount);
        Assert.True(_monitor.Disposed);
    }

    [Fact]
    public void Dispose_stops_and_disposes_the_timer()
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
    public void Dispose_is_idempotent()
    {
        var coordinator = CreateCoordinator();
        coordinator.Start();

        coordinator.Dispose();
        coordinator.Dispose();

        Assert.Throws<ObjectDisposedException>(coordinator.Start);
    }

    // ---------------------------------------------------------------- periodic reminders

    [Fact]
    public void The_countdown_starts_with_the_app()
    {
        using var coordinator = CreateCoordinator();

        coordinator.Start();

        Assert.True(_timer.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(30), coordinator.Remaining);
    }

    [Fact]
    public void A_tick_before_the_interval_shows_nothing()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(29));
        _timer.RaiseTick();

        Assert.Equal(0, _presenter.ShowCount);
    }

    [Fact]
    public void The_reminder_appears_once_the_interval_has_passed()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();

        Assert.Equal(1, _presenter.ShowCount);
    }

    [Fact]
    public void The_periodic_reminder_repeats_every_interval()
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
    public void A_due_reminder_over_an_open_one_raises_it_and_still_re_arms()
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
    public void An_unlock_restarts_the_countdown()
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
    public void A_lock_longer_than_the_interval_postpones_the_reminder_to_a_full_interval_after_unlock()
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
    public void Many_lock_unlock_cycles_leave_a_full_interval_after_the_last_unlock()
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
    public void A_lock_stops_the_timer_and_clears_the_countdown()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _monitor.RaiseLocked();

        Assert.False(_timer.IsRunning);
        Assert.Null(coordinator.Remaining);
    }

    [Fact]
    public void A_lock_never_shows_a_reminder_by_itself()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _monitor.RaiseLocked();

        Assert.Equal(0, _presenter.ShowCount);
    }

    [Fact]
    public void A_disabled_feature_never_shows_a_periodic_reminder_and_runs_no_timer()
    {
        using var coordinator = CreateCoordinator(settings: new ReminderSettings(false, 30));
        coordinator.Start();

        _clock.Advance(TimeSpan.FromHours(6));
        _timer.RaiseTick();

        Assert.Equal(0, _presenter.ShowCount);
        Assert.False(_timer.IsRunning);
        Assert.Null(coordinator.Remaining);
    }

    [Fact]
    public void A_disabled_feature_still_shows_the_reminder_on_unlock()
    {
        using var coordinator = CreateCoordinator(settings: new ReminderSettings(false, 30));
        coordinator.Start();

        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
        Assert.Null(coordinator.Remaining);
    }

    [Fact]
    public void Applying_settings_restarts_the_countdown_from_zero()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(25));
        coordinator.ApplySettings(new ReminderSettings(true, 10));

        Assert.Equal(TimeSpan.FromMinutes(10), coordinator.Remaining);
        Assert.Equal(new ReminderSettings(true, 10), coordinator.Settings);
    }

    [Fact]
    public void Switching_the_feature_off_stops_the_timer()
    {
        using var coordinator = CreateCoordinator();
        coordinator.Start();

        coordinator.ApplySettings(new ReminderSettings(false, 30));

        Assert.False(_timer.IsRunning);
    }

    [Fact]
    public void Switching_the_feature_back_on_starts_the_timer_again()
    {
        using var coordinator = CreateCoordinator(settings: new ReminderSettings(false, 30));
        coordinator.Start();

        coordinator.ApplySettings(new ReminderSettings(true, 45));

        Assert.True(_timer.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(45), coordinator.Remaining);
    }

    [Fact]
    public void The_countdown_change_event_fires_on_every_tick_and_on_every_reset()
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
    public void An_unlock_just_after_a_periodic_reminder_is_debounced()
    {
        using var coordinator = CreateCoordinator(TimeSpan.FromSeconds(2));
        coordinator.Start();

        _clock.Advance(TimeSpan.FromMinutes(30));
        _timer.RaiseTick();
        _presenter.SimulateClosed();

        _monitor.RaiseUnlocked();

        Assert.Equal(1, _presenter.ShowCount);
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

        public void Show()
        {
            ShowCount++;
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

    private sealed class MutableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void Advance(TimeSpan delta) => UtcNow += delta;
    }
}
