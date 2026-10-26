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
    private readonly IDecisionLog _log;

    private DateTimeOffset? _lastShownAt;
    private DateTimeOffset? _lastActivityReadAt;

    /// <summary>
    /// When the periodic reminder now due was first held back, and for what; <c>null</c> while
    /// nothing is held. Only for the log: one line when the hold starts or its reason changes,
    /// one with the total when the reminder finally appears (R-112).
    /// </summary>
    private DateTimeOffset? _heldSince;
    private ReminderSuppression _heldReason;

    private bool _started;
    private bool _disposed;

    /// <summary>
    /// A coordinator that hears no lock, unlock or tick until <see cref="Start"/>; an
    /// <see cref="ApplySettings"/> before it already restarts the countdown and starts the
    /// timer. It owns <paramref name="monitor"/> and <paramref name="timer"/>:
    /// <see cref="Dispose"/> disposes both.
    /// </summary>
    /// <param name="monitor">Raises the lock and the unlock.</param>
    /// <param name="presenter">Shows the reminder window.</param>
    /// <param name="timer">
    /// Ticks the schedule; the app's ticks every <see cref="DefaultTickInterval"/>.
    /// </param>
    /// <param name="settings">The settings to start with; normalized before they are kept.</param>
    /// <param name="activity">
    /// What the user is in the middle of; <c>null</c> sees nothing, ever.
    /// </param>
    /// <param name="clock">The time; <c>null</c> is the system clock.</param>
    /// <param name="debounce">
    /// How soon after a reminder was shown an unlock shows none, which collapses the two
    /// unlock events Windows can raise for one unlock; <c>null</c> is
    /// <see cref="DefaultDebounce"/>.
    /// </param>
    /// <param name="activityRefresh">
    /// How often the activity monitor is read on ticks when no reminder is due; <c>null</c> is
    /// <see cref="DefaultActivityRefresh"/>.
    /// </param>
    /// <param name="log">
    /// Where each decision is written, one line each (R-112); <c>null</c> keeps none.
    /// </param>
    public ReminderCoordinator(
        ISessionMonitor monitor,
        IReminderPresenter presenter,
        IPeriodicTimer timer,
        ReminderSettings settings,
        IActivityMonitor? activity = null,
        IClock? clock = null,
        TimeSpan? debounce = null,
        TimeSpan? activityRefresh = null,
        IDecisionLog? log = null)
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
        _log = log ?? NullDecisionLog.Instance;
        _schedule = new IntervalSchedule(settings);
    }

    /// <summary>
    /// Raised whenever the time to the next reminder may have changed -- on every tick, and
    /// on anything that restarts or stops the countdown. The App turns it into the tray
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

    /// <summary>
    /// Subscribes to and starts the session monitor, subscribes to the timer, and starts the
    /// countdown, which raises <see cref="CountdownChanged"/>. Calling it again does nothing;
    /// calling it after <see cref="Dispose"/> throws <see cref="ObjectDisposedException"/>.
    /// </summary>
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
    /// cannot explain. Throws <see cref="ObjectDisposedException"/> after <see cref="Dispose"/>.
    /// </summary>
    /// <param name="settings">The settings to apply; normalized before they are kept.</param>
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
        ClearHold();

        SyncTimer();
        CountdownChanged?.Invoke(this, EventArgs.Empty);
        _log.Write(DecisionLog.SettingsApplied(Settings));
    }

    /// <summary>
    /// Hands the monitor's <see cref="ISessionMonitor.Unlocked"/> to <see cref="HandleUnlock"/>.
    /// </summary>
    /// <param name="sender">The session monitor; not read.</param>
    /// <param name="e">Empty; not read.</param>
    private void OnUnlocked(object? sender, EventArgs e) => HandleUnlock();

    /// <summary>
    /// Hands the monitor's <see cref="ISessionMonitor.Locked"/> to <see cref="HandleLock"/>.
    /// </summary>
    /// <param name="sender">The session monitor; not read.</param>
    /// <param name="e">Empty; not read.</param>
    private void OnLocked(object? sender, EventArgs e) => HandleLock();

    /// <summary>
    /// Hands the timer's <see cref="IPeriodicTimer.Tick"/> to <see cref="HandleTick"/>.
    /// </summary>
    /// <param name="sender">The timer; not read.</param>
    /// <param name="e">Empty; not read.</param>
    private void OnTick(object? sender, EventArgs e) => HandleTick();

    /// <summary>
    /// What an unlock does. The countdown restarts in full first. Then, with the unlock
    /// reminder switched on, a reminder already open is raised, whatever the debounce and the
    /// activity monitor would say. Otherwise one is shown with the unlock message, unless a
    /// reminder was shown within the debounce or what the user is in the middle of holds it
    /// back.
    /// </summary>
    internal void HandleUnlock()
    {
        // The countdown restarts on EVERY unlock, before anything decides whether a window
        // appears. The debounce and the already-showing check below are about windows, not
        // about the schedule, and letting either of them skip this would leave a deadline
        // set before a lock that may have lasted hours.
        _schedule.Resume(_clock.UtcNow);
        _log.Write(DecisionLog.Unlocked);

        // Cleared here rather than further down, because every path out of this method has
        // just been given a whole fresh interval: whatever was being waited on is gone, and
        // the branches below return early often enough that clearing it in each would be a
        // rule kept in four places. It is set again a few lines down if the user is busy.
        Suppression = ReminderSuppression.None;
        ClearHold();

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
            _log.Write(DecisionLog.UnlockSkippedOff());
            return;
        }

        if (_presenter.IsShowing)
        {
            _presenter.BringToFront();
            _log.Write(DecisionLog.BroughtToFront(ReminderKind.Unlock));
            return;
        }

        var now = _clock.UtcNow;
        if (_lastShownAt is { } last && now - last < _debounce)
        {
            _log.Write(DecisionLog.UnlockSkippedRecent(now - last));
            return;
        }

        // Asked last, after the two cheap checks, because this is the one that reads the
        // registry. You can lock the workstation with a call still running -- the meeting
        // carries on without you -- and without this the reminder would land on top of it the
        // moment you came back, which is the exact thing the setting exists to prevent.
        if (Evaluate() is var reason and not ReminderSuppression.None)
        {
            _log.Write(DecisionLog.UnlockSkipped(reason));

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
        _log.Write(DecisionLog.ReminderShown(ReminderKind.Unlock, Settings.UnlockMessage));
    }

    /// <summary>
    /// What a lock does: the countdown is suspended and the timer stopped until the unlock.
    /// Nothing is shown on a lock.
    /// </summary>
    internal void HandleLock()
    {
        _schedule.Suspend();
        _log.Write(DecisionLog.Locked);

        // Nothing is read behind a lock -- the timer is stopped -- so the stamp is cleared to
        // make the first tick after the unlock take a fresh reading rather than trust one
        // from before the lock.
        Suppression = ReminderSuppression.None;
        _lastActivityReadAt = null;
        ClearHold();
        SyncTimer();
        CountdownChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// What a tick does. Once the deadline has passed, the periodic reminder is shown, or the
    /// one already open is raised; if what the user is in the middle of holds it back, it is
    /// postponed by the retry gap instead. On any other tick, <see cref="Suppression"/> is
    /// refreshed when the last reading is older than the refresh interval. Either way
    /// <see cref="CountdownChanged"/> is raised.
    /// </summary>
    internal void HandleTick()
    {
        var now = _clock.UtcNow;

        if (_schedule.TryConsumeDue(now))
        {
            // The activity monitor is asked HERE and nowhere else on the tick path: once per
            // elapsed interval, and once per retry while a reminder is held back. Asking on
            // every tick would read the registry once a second to answer a question that only
            // matters in the instant a window would otherwise open.
            if (Evaluate() is var reason and not ReminderSuppression.None)
            {
                // TryConsumeDue re-armed a whole interval as it said yes; this overwrites it
                // with the much shorter retry gap, so the next look comes in seconds rather
                // than in half an hour.
                _schedule.Postpone(now, Settings.RetryInterval);

                // One line when the hold starts and one per change of reason, not one per
                // retry: a long call would otherwise write the same line every 20 seconds.
                if (_heldSince is null || reason != _heldReason)
                {
                    _log.Write(DecisionLog.HeldBack(reason, Settings.RetryInterval));
                }

                _heldSince ??= now;
                _heldReason = reason;

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
                _log.Write(_heldSince is { } heldSinceRaise
                    ? DecisionLog.BroughtToFrontAfterHold(now - heldSinceRaise)
                    : DecisionLog.BroughtToFront(ReminderKind.Periodic));
            }
            else
            {
                _presenter.Show(Settings.PeriodicMessage);
                _log.Write(_heldSince is { } heldSince
                    ? DecisionLog.ShownAfterHold(now - heldSince, Settings.PeriodicMessage)
                    : DecisionLog.ReminderShown(ReminderKind.Periodic, Settings.PeriodicMessage));
            }

            ClearHold();
        }
        else
        {
            // A clock moved back restarted the countdown inside TryConsumeDue (R-025): the
            // postponement that was running is over, so the hold the log tracks is too.
            if (_schedule.JumpRestartedCountdown)
            {
                ClearHold();
            }

            RefreshSuppression(now);
        }

        CountdownChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forgets a hold the log was tracking: the reminder appeared, or the countdown it belonged
    /// to was restarted or suspended.
    /// </summary>
    private void ClearHold()
    {
        _heldSince = null;
        _heldReason = ReminderSuppression.None;
    }

    /// <summary>
    /// Reads the activity monitor and records why a reminder may not appear, so the tooltip
    /// can say so. Nothing is read when both switches are off: an answer nobody would look at
    /// is not worth a walk through the registry.
    /// </summary>
    /// <returns>
    /// Why a reminder may not appear now, as also kept in <see cref="Suppression"/>;
    /// <see cref="ReminderSuppression.None"/> when it may, or when both pauses are off.
    /// </returns>
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
    /// <param name="now">The current time, compared with the time of the last reading.</param>
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

    /// <summary>
    /// Unsubscribes, stops and disposes the timer, and disposes the session monitor. The
    /// presenter and the activity monitor are not disposed. Calling it again does nothing.
    /// </summary>
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
