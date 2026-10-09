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
/// stacking windows are written once. Each has its own switch and its own message in
/// <see cref="ReminderSettings"/>, and the two are independent: turning the unlock reminder
/// off does not stop the schedule restarting on unlock, and turning the periodic one off
/// does not stop the unlock reminder.
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
///
/// Both sources are also gated on what the user is in the middle of, through
/// <see cref="IActivityMonitor"/> and <see cref="SuppressionPolicy"/>. The two react
/// differently, and deliberately: a periodic reminder is POSTPONED, because there is a
/// deadline to push and a countdown to keep honest, while an unlock reminder is simply
/// SKIPPED -- the unlock also restarted the schedule, so a reminder is coming anyway, and a
/// one-shot retry beside the periodic one would be a second deadline to keep in step with
/// the first.
/// </summary>
public sealed class ReminderCoordinator : IDisposable
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How often the one timer ticks. One second is not the reminder interval -- it is the
    /// resolution of the countdown in the tray tooltip, which is what has to look alive.
    /// </summary>
    public static readonly TimeSpan DefaultTickInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How often the activity monitor is re-read purely to keep the tooltip honest, when no
    /// reminder is due.
    ///
    /// It is not the tick interval, and that is the whole point. A read walks a few hundred
    /// registry keys -- about five milliseconds -- which is nothing every ten seconds and
    /// wasteful every one. Ten seconds is also faster than anyone can hover the tray icon
    /// after joining a call, which is the moment this exists for.
    /// </summary>
    public static readonly TimeSpan DefaultActivityRefresh = TimeSpan.FromSeconds(10);

    private readonly ISessionMonitor _monitor;
    private readonly IReminderPresenter _presenter;
    private readonly IPeriodicTimer _timer;
    private readonly IActivityMonitor _activity;
    private readonly IntervalSchedule _schedule;
    private readonly IClock _clock;
    private readonly TimeSpan _debounce;
    private readonly TimeSpan _activityRefresh;

    private DateTimeOffset? _lastShownAt;
    private DateTimeOffset? _lastActivityReadAt;
    private bool _started;
    private bool _disposed;

    public ReminderCoordinator(
        ISessionMonitor monitor,
        IReminderPresenter presenter,
        IPeriodicTimer timer,
        ReminderSettings settings,
        IActivityMonitor? activity = null,
        IClock? clock = null,
        TimeSpan? debounce = null,
        TimeSpan? activityRefresh = null)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(settings);

        _monitor = monitor;
        _presenter = presenter;
        _timer = timer;
        _activity = activity ?? IdleActivityMonitor.Instance;
        _clock = clock ?? SystemClock.Instance;
        _debounce = debounce ?? DefaultDebounce;
        _activityRefresh = activityRefresh ?? DefaultActivityRefresh;
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

    /// <summary>
    /// Why reminders are paused right now, or None. It is what the last reading of the
    /// activity monitor said, which is NOT the same as "a reminder was held back a moment
    /// ago": it is kept current between reminders too, every
    /// <see cref="DefaultActivityRefresh"/>, so that hovering the tray icon during a call
    /// says so instead of counting down to a reminder that is not going to arrive.
    ///
    /// That refresh is the difference between a feature the user can see working and one
    /// they have to wait half an hour to catch in the act.
    /// </summary>
    public ReminderSuppression Suppression { get; private set; }

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

        // A reminder held back under the old settings is let go of along with them: the new
        // interval starts from zero, so the postponement it was waiting on no longer exists.
        // The read stamp goes with it, so that switching a pause ON is reflected in the
        // tooltip on the next tick rather than up to a refresh interval later.
        Suppression = ReminderSuppression.None;
        _lastActivityReadAt = null;

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

        // Cleared here rather than further down, because every path out of this method has
        // just been given a whole fresh interval: whatever was being waited on is gone, and
        // the branches below return early often enough that clearing it in each would be a
        // rule kept in four places. It is set again a few lines down if the user is busy.
        Suppression = ReminderSuppression.None;

        SyncTimer();
        CountdownChanged?.Invoke(this, EventArgs.Empty);

        // The unlock reminder's own switch, and it sits HERE: after the schedule has been
        // restarted, because that belongs to the periodic reminder, and before anything to
        // do with windows, because everything from this line down IS the unlock reminder.
        // That includes raising a window left open from before the lock, and it includes
        // the activity read -- a reminder that is not going to appear has no reason to walk
        // the registry to find out whether it would have been held back.
        if (!Settings.UnlockEnabled)
        {
            return;
        }

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

        // Asked last, after the two cheap checks, because this is the one that reads the
        // registry. You can lock the workstation with a call still running -- the meeting
        // carries on without you -- and without this the reminder would land on top of it the
        // moment you came back, which is the exact thing the setting exists to prevent.
        if (Evaluate() is not ReminderSuppression.None)
        {
            // Skipped, not postponed. With the periodic reminder off there is no countdown,
            // so no tick reads the monitor again: a reason kept here would stay in the
            // tooltip after the call, for a reminder that is not being held back.
            if (!_schedule.IsCountingDown)
            {
                Suppression = ReminderSuppression.None;
            }

            // The reason changed, so the tooltip has something new to say.
            CountdownChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _lastShownAt = now;
        _presenter.Show(Settings.UnlockMessage);
    }

    internal void HandleLock()
    {
        _schedule.Suspend();

        // Nothing is read behind a lock -- the timer is stopped -- so the stamp is cleared to
        // make the first tick after the unlock take a fresh reading rather than trust one
        // from before the lock.
        Suppression = ReminderSuppression.None;
        _lastActivityReadAt = null;
        SyncTimer();
        CountdownChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void HandleTick()
    {
        var now = _clock.UtcNow;

        if (_schedule.TryConsumeDue(now))
        {
            // The activity monitor is asked HERE and nowhere else on the tick path: once per
            // elapsed interval, and once per retry while a reminder is held back. Asking on
            // every tick would read the registry once a second to answer a question that only
            // matters in the instant a window would otherwise open.
            if (Evaluate() is not ReminderSuppression.None)
            {
                // TryConsumeDue re-armed a whole interval as it said yes; this overwrites it
                // with the much shorter retry gap, so the next look comes in seconds rather
                // than in half an hour.
                _schedule.Postpone(now, Settings.RetryInterval);
                CountdownChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            // The deadline is spent whether or not a window actually appears. A reminder
            // already on screen means the moment has been served; re-arming anyway is what
            // keeps a due deadline from firing again on the very next tick. It is raised
            // with whatever it already says -- possibly the unlock message -- rather than
            // having its text swapped under the user's eyes.
            _lastShownAt = now;

            if (_presenter.IsShowing)
            {
                _presenter.BringToFront();
            }
            else
            {
                _presenter.Show(Settings.PeriodicMessage);
            }
        }
        else
        {
            RefreshSuppression(now);
        }

        CountdownChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Reads the activity monitor and records why a reminder may not appear, so the tooltip
    /// can say so. Nothing is read when both switches are off: an answer nobody would look at
    /// is not worth a walk through the registry.
    /// </summary>
    private ReminderSuppression Evaluate()
    {
        if (!Settings.PausesForAnything)
        {
            return Suppression = ReminderSuppression.None;
        }

        _lastActivityReadAt = _clock.UtcNow;
        return Suppression = SuppressionPolicy.Evaluate(Settings, _activity.Read());
    }

    /// <summary>
    /// Keeps <see cref="Suppression"/> current on ticks where nothing is due, so that the
    /// tooltip tells the truth while a call is in progress rather than only in the minute
    /// after a reminder was held back.
    ///
    /// Rate-limited to <see cref="DefaultActivityRefresh"/> rather than run on every tick.
    /// The read is cheap but not free, and a tray application that walks the registry once a
    /// second forever has stopped being unobtrusive.
    /// </summary>
    private void RefreshSuppression(DateTimeOffset now)
    {
        if (!Settings.PausesForAnything)
        {
            Suppression = ReminderSuppression.None;
            return;
        }

        if (_lastActivityReadAt is { } last && now - last < _activityRefresh)
        {
            return;
        }

        Evaluate();
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
