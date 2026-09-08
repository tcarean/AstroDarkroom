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
/// The single place that decides when a reminder appears. Two things can ask for one -- an
/// unlock, and the periodic schedule -- and both go through here, so the rules about not
/// stacking windows are written once.
///
/// The unlock side is less trivial than it sounds. Windows can raise SessionUnlock twice for
/// a single unlock, so bursts are collapsed by a short debounce window. And if a reminder
/// from earlier is still on screen, a second window would be noise -- the existing one is
/// raised instead.
///
/// The periodic side is driven by ONE timer (see <see cref="IPeriodicTimer"/>), ticking at a
/// short fixed cadence rather than being armed for the interval, with
/// <see cref="IntervalSchedule"/> holding the deadline. That arrangement is what lets the
/// tooltip count down off the same tick, and what makes the reset-on-unlock requirement one
/// line instead of a timer torn down and rebuilt.
/// </summary>
public sealed class ReminderCoordinator : IDisposable
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How often the one timer ticks. One second is not the reminder interval -- it is the
    /// resolution of the countdown in the tray tooltip, which is what has to look alive.
    /// </summary>
    public static readonly TimeSpan DefaultTickInterval = TimeSpan.FromSeconds(1);

    private readonly ISessionMonitor _monitor;
    private readonly IReminderPresenter _presenter;
    private readonly IPeriodicTimer _timer;
    private readonly IntervalSchedule _schedule;
    private readonly IClock _clock;
    private readonly TimeSpan _debounce;

    private DateTimeOffset? _lastShownAt;
    private bool _started;
    private bool _disposed;

    public ReminderCoordinator(
        ISessionMonitor monitor,
        IReminderPresenter presenter,
        IPeriodicTimer timer,
        ReminderSettings settings,
        IClock? clock = null,
        TimeSpan? debounce = null)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(settings);

        _monitor = monitor;
        _presenter = presenter;
        _timer = timer;
        _clock = clock ?? SystemClock.Instance;
        _debounce = debounce ?? DefaultDebounce;
        _schedule = new IntervalSchedule(settings);
    }

    /// <summary>
    /// Raised whenever the time to the next reminder may have changed -- on every tick, and
    /// on anything that restarts or stops the countdown. The tray host turns it into the
    /// tooltip; it carries no text, because the coordinator has no opinion on wording.
    /// </summary>
    public event EventHandler? CountdownChanged;

    /// <summary>The settings currently in force. The dialog is seeded from this.</summary>
    public ReminderSettings Settings => _schedule.Settings;

    /// <summary>
    /// Time until the next periodic reminder, or null when there is no countdown -- the
    /// feature is off, or the workstation is locked.
    /// </summary>
    public TimeSpan? Remaining => _schedule.Remaining(_clock.UtcNow);

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_started)
        {
            return;
        }

        _monitor.Unlocked += OnUnlocked;
        _monitor.Locked += OnLocked;
        _monitor.Start();

        _timer.Tick += OnTick;

        // The countdown starts with the app, which on a machine that launches this from the
        // Run key is as soon after boot as the requirement can mean.
        _schedule.Restart(_clock.UtcNow);
        SyncTimer();

        _started = true;
        CountdownChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Takes settings edited in the dialog. The countdown restarts from zero rather than
    /// keeping the time already elapsed: changing 30 minutes to 10 while 25 remain would
    /// otherwise either fire at once or leave the tooltip showing a number the new setting
    /// cannot explain.
    /// </summary>
    public void ApplySettings(ReminderSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(settings);

        _schedule.Apply(settings, _clock.UtcNow);
        SyncTimer();
        CountdownChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnUnlocked(object? sender, EventArgs e) => HandleUnlock();

    private void OnLocked(object? sender, EventArgs e) => HandleLock();

    private void OnTick(object? sender, EventArgs e) => HandleTick();

    internal void HandleUnlock()
    {
        // The countdown restarts on EVERY unlock, before anything decides whether a window
        // appears. The debounce and the already-showing check below are about windows, not
        // about the schedule, and letting either of them skip this would leave a deadline
        // set before a lock that may have lasted hours.
        _schedule.Resume(_clock.UtcNow);
        SyncTimer();
        CountdownChanged?.Invoke(this, EventArgs.Empty);

        if (_presenter.IsShowing)
        {
            _presenter.BringToFront();
            return;
        }

        var now = _clock.UtcNow;
        if (_lastShownAt is { } last && now - last < _debounce)
        {
            return;
        }

        _lastShownAt = now;
        _presenter.Show();
    }

    internal void HandleLock()
    {
        _schedule.Suspend();
        SyncTimer();
        CountdownChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void HandleTick()
    {
        var now = _clock.UtcNow;

        if (_schedule.TryConsumeDue(now))
        {
            // The deadline is spent whether or not a window actually appears. A reminder
            // already on screen means the moment has been served; re-arming anyway is what
            // keeps a due deadline from firing again on the very next tick.
            _lastShownAt = now;

            if (_presenter.IsShowing)
            {
                _presenter.BringToFront();
            }
            else
            {
                _presenter.Show();
            }
        }

        CountdownChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The timer runs only while there is something to count. With the feature switched off,
    /// or the workstation locked, a tick would have nothing to decide and nothing to redraw.
    /// </summary>
    private void SyncTimer()
    {
        if (_schedule.IsCountingDown)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_started)
        {
            _monitor.Unlocked -= OnUnlocked;
            _monitor.Locked -= OnLocked;
            _timer.Tick -= OnTick;
        }

        _timer.Stop();
        _timer.Dispose();
        _monitor.Dispose();
        _disposed = true;
    }
}
