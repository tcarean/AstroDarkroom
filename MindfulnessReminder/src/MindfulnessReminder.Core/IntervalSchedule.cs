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
    /// Puts the full interval back on the clock. This is the only place a deadline is ever
    /// set, so "the countdown restarts" means exactly one thing everywhere it happens: at
    /// startup, on every unlock, after each reminder, and when settings are applied.
    /// </summary>
    public void Restart(DateTimeOffset now)
        => _dueAt = _settings.PeriodicEnabled && !_suspended ? now + _settings.Interval : null;

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
    public void Resume(DateTimeOffset now)
    {
        _suspended = false;
        Restart(now);
    }

    /// <summary>Applies edited settings and restarts the countdown from zero.</summary>
    public void Apply(ReminderSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings.Normalize();
        Restart(now);
    }

    /// <summary>
    /// True once per elapsed interval, and it re-arms as it says yes -- so a caller that
    /// cannot act on it, because a reminder is already on screen, still leaves the schedule
    /// in a sane state rather than a deadline stuck in the past that fires on every tick.
    /// </summary>
    public bool TryConsumeDue(DateTimeOffset now)
    {
        if (_dueAt is not { } due)
        {
            return false;
        }

        // The clock can move backwards under us: an NTP correction, or someone setting it by
        // hand. A deadline further away than a whole interval cannot have been set by this
        // class, so it is a jump rather than a wait, and the countdown starts over instead
        // of the reminder being postponed by however far the clock went back.
        if (due - now > _settings.Interval)
        {
            Restart(now);
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
    /// Time left, or null when nothing is being counted. Never negative: a deadline in the
    /// past reads as zero, so the tooltip cannot show a minus sign in the moment between the
    /// deadline passing and the tick that acts on it.
    /// </summary>
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
