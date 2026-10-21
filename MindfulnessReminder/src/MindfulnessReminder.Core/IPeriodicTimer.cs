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
/// The one timer the reminder schedule runs on; the reminder window's show delay and
/// <c>--detect</c> have timers of their own. It ticks at a short, fixed cadence -- it is *not*
/// set to the reminder interval -- because two separate things need it: deciding whether
/// the deadline has passed, and keeping the tray tooltip's countdown current. A one-shot
/// timer armed for the interval would need a second timer for the tooltip.
///
/// The production implementation is a DispatcherTimer, which lives in the App layer;
/// keeping it behind this interface is what lets the whole schedule be tested instantly.
/// </summary>
public interface IPeriodicTimer : IDisposable
{
    event EventHandler? Tick;

    bool IsRunning { get; }

    /// <summary>Idempotent: starting an already-running timer must not restart its period.</summary>
    void Start();

    /// <summary>Stops the ticks. Does nothing when the timer is not running.</summary>
    void Stop();
}
