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
/// When the next periodic reminder is due. All of the arithmetic behind the feature lives
/// here, with no timer, no events and no UI, so every rule below can be tested in
/// microseconds instead of in half an hour.
///
/// It holds a DEADLINE on the wall clock, not a count of elapsed ticks, and that is the
/// load-bearing decision. A DispatcherTimer does not tick while the machine sleeps, so
/// counting ticks would silently stretch a 30-minute interval by however long the lid was
/// shut. A deadline compared against the clock does the right thing instead: a reminder
/// that came due during sleep fires on the first tick after waking.
///
/// The deadline is null whenever nothing is being counted -- the feature is switched off,
/// or the workstation is locked -- and that same null is what the tooltip reads to know it
/// has no countdown to show.
/// </summary>
public sealed class IntervalSchedule
{
    private ReminderSettings _settings;
    private DateTimeOffset? _dueAt;
    private bool _suspended;

    /// <summary>
    /// The span the current deadline was set with -- a whole interval normally, or the retry
    /// gap after a <see cref="Postpone"/>. It exists for the backwards-clock guard in
    /// <see cref="TryConsumeDue"/>, which asks "could this class have set a deadline this far
    /// out?"; comparing against the interval instead would make any retry longer than the
    /// interval look like a clock jump and silently reset the whole schedule.
    /// </summary>
    private TimeSpan _armedSpan;

    /// <summary>
    /// A schedule with nothing counted yet: the first deadline is set by <see cref="Restart"/>.
    /// </summary>
    /// <param name="settings">The settings to schedule by; normalized before they are kept.</param>
    public IntervalSchedule(ReminderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings.Normalize();
    }

    public ReminderSettings Settings => _settings;

    /// <summary>False while the feature is off or the workstation is locked.</summary>
    public bool IsCountingDown => _dueAt is not null;

    /// <summary>True between a lock and the unlock that follows it.</summary>
    public bool IsSuspended => _suspended;

    /// <summary>
    /// Puts the full interval back on the clock. Every restart of the countdown comes through
    /// here, so "the countdown restarts" means exactly one thing everywhere it happens: at
    /// startup, on every unlock, after each reminder, after the clock jumps back, and when
    /// settings are applied. The one other place a deadline is set is <see cref="Postpone"/>,
    /// with the retry gap.
    /// </summary>
    /// <param name="now">The current time, from which the interval is counted.</param>
    public void Restart(DateTimeOffset now)
    {
        _dueAt = _settings.PeriodicEnabled && !_suspended ? now + _settings.Interval : null;
        _armedSpan = _settings.Interval;
    }

    /// <summary>
    /// Pushes the deadline out by <paramref name="by"/> instead of letting it fire, for a
    /// reminder that came due at a moment the user cannot be interrupted. The next tick past
    /// the new deadline asks again, and again after that, for as long as it takes.
    ///
    /// A no-op when nothing is being counted. Callers reach this through
    /// <see cref="TryConsumeDue"/> having just said yes, which cannot happen without a
    /// deadline -- but a postponement that quietly invented one would be a countdown running
    /// with the feature switched off.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <param name="by">
    /// The retry gap: the new deadline is <paramref name="now"/> plus this.
    /// </param>
    public void Postpone(DateTimeOffset now, TimeSpan by)
    {
        if (_dueAt is null)
        {
            return;
        }

        _dueAt = now + by;
        _armedSpan = by;
    }

    /// <summary>
    /// Stops counting for the duration of a lock. Without this the deadline would pass
    /// behind the lock screen and the reminder would be created where nobody can see it --
    /// and would then already be open, swallowing the unlock reminder that follows.
    /// </summary>
    public void Suspend()
    {
        _suspended = true;
        _dueAt = null;
    }

    /// <summary>
    /// Resumes on unlock, granting the whole interval afresh however long the lock lasted --
    /// which is the requirement, and is why no time is carried across it. Safe to call
    /// without a matching lock: an unlock always restarts the countdown.
    /// </summary>
    /// <param name="now">The current time, from which the whole interval is counted.</param>
    public void Resume(DateTimeOffset now)
    {
        _suspended = false;
        Restart(now);
    }

    /// <summary>Applies edited settings and restarts the countdown from zero.</summary>
    /// <param name="settings">The edited settings; normalized before they are kept.</param>
    /// <param name="now">The current time, from which the new interval is counted.</param>
    public void Apply(ReminderSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings.Normalize();
        Restart(now);
    }

    /// <summary>
    /// Whether the last <see cref="TryConsumeDue"/> found the clock moved back and started the
    /// countdown over. A postponement in progress ended with it, which the caller's log needs to
    /// know (R-112); the next time the reminder is held back is a first time again.
    /// </summary>
    public bool JumpRestartedCountdown { get; private set; }

    /// <summary>
    /// Asks whether the deadline has passed, and re-arms as it says yes -- so a caller that
    /// cannot act on it, because a reminder is already on screen, still leaves the schedule
    /// in a sane state rather than a deadline stuck in the past that fires on every tick.
    /// </summary>
    /// <param name="now">The current time, compared with the deadline.</param>
    /// <returns>
    /// <c>true</c> once the deadline has passed -- once per interval, or once per retry gap
    /// after <see cref="Postpone"/> -- with the next whole interval armed. <c>false</c> when
    /// nothing is being counted, when the deadline is still ahead, and when the deadline is
    /// further away than the span it was armed with, which means the clock was moved back and
    /// starts the countdown over.
    /// </returns>
    public bool TryConsumeDue(DateTimeOffset now)
    {
        JumpRestartedCountdown = false;

        if (_dueAt is not { } due)
        {
            return false;
        }

        // The clock can move backwards under us: an NTP correction, or someone setting it by
        // hand. A deadline further away than the span it was armed with cannot have been set
        // by this class, so it is a jump rather than a wait, and the countdown starts over
        // instead of the reminder being postponed by however far the clock went back.
        if (due - now > _armedSpan)
        {
            Restart(now);
            JumpRestartedCountdown = true;
            return false;
        }

        if (now < due)
        {
            return false;
        }

        Restart(now);
        return true;
    }

    /// <summary>
    /// The time left to the deadline. Never negative: a deadline in the past reads as zero,
    /// so the tooltip cannot show a minus sign in the moment between the deadline passing and
    /// the tick that acts on it.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>
    /// The time left, zero once the deadline has passed, or <c>null</c> when nothing is being
    /// counted.
    /// </returns>
    public TimeSpan? Remaining(DateTimeOffset now)
    {
        if (_dueAt is not { } due)
        {
            return null;
        }

        var left = due - now;
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }
}
