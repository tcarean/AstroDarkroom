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
/// Raises events when the interactive session is locked and unlocked. The production
/// implementation sits on SystemEvents.SessionSwitch; keeping it behind this interface means
/// the whole reminder policy is testable, and a WTSRegisterSessionNotification
/// implementation could be swapped in without touching anything else.
/// </summary>
public interface ISessionMonitor : IDisposable
{
    /// <summary>Raised on the UI thread after the workstation is unlocked.</summary>
    event EventHandler? Unlocked;

    /// <summary>
    /// Raised on the UI thread after the workstation is locked. The periodic reminder needs
    /// it: without a lock event its deadline would pass behind the lock screen and the
    /// window would be created where nobody can see it. Nothing shows a reminder on lock.
    /// </summary>
    event EventHandler? Locked;

    void Start();
}
