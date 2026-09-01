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
    private readonly MutableClock _clock = new(new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero));

    private ReminderCoordinator CreateCoordinator(TimeSpan? debounce = null) =>
        new(_monitor, _presenter, _clock, debounce ?? ReminderCoordinator.DefaultDebounce);

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
    public void Dispose_is_idempotent()
    {
        var coordinator = CreateCoordinator();
        coordinator.Start();

        coordinator.Dispose();
        coordinator.Dispose();

        Assert.Throws<ObjectDisposedException>(coordinator.Start);
    }

    private sealed class FakeSessionMonitor : ISessionMonitor
    {
        public bool Started { get; private set; }

        public bool Disposed { get; private set; }

        public event EventHandler? Unlocked;

        public void Start() => Started = true;

        public void RaiseUnlocked() => Unlocked?.Invoke(this, EventArgs.Empty);

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

        public void SimulateClosed() => IsShowing = false;
    }

    private sealed class MutableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void Advance(TimeSpan delta) => UtcNow += delta;
    }
}
