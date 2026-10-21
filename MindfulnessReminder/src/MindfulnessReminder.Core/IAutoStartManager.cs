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
/// Whether the app starts with Windows: what the tray checkbox reads and sets, and what each
/// launch repairs. Each member may throw when Windows refuses the access (a policy, security
/// software).
/// </summary>
public interface IAutoStartManager
{
    /// <summary>Reads whether Windows will start the app at sign-in.</summary>
    /// <returns>
    /// <see cref="AutoStartStatus.Disabled"/> when there is no entry,
    /// <see cref="AutoStartStatus.BlockedByWindows"/> when there is one but Task Manager has
    /// switched it off, and <see cref="AutoStartStatus.Enabled"/> otherwise.
    /// </returns>
    AutoStartStatus GetStatus();

    /// <summary>
    /// Registers the app to start with Windows, lifting a Task Manager block too, so that
    /// <see cref="GetStatus"/> reads <see cref="AutoStartStatus.Enabled"/> afterwards.
    /// </summary>
    void Enable();

    /// <summary>Removes the entry, so the app no longer starts with Windows.</summary>
    void Disable();

    /// <summary>
    /// Rewrites the registered command when the executable has moved since it was registered.
    /// Never lifts a Task Manager block.
    /// </summary>
    /// <returns>
    /// <c>true</c> when a repair was made; <c>false</c> when there is no entry, or it already
    /// points at this executable.
    /// </returns>
    bool RepairPathIfNeeded();
}
