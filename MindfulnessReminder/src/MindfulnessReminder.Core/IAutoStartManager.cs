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

public interface IAutoStartManager
{
    AutoStartStatus GetStatus();

    void Enable();

    void Disable();

    /// <summary>
    /// Rewrites the registered command when the executable has moved since it was registered.
    /// Returns <c>true</c> when a repair was made. Never lifts a Task Manager block.
    /// </summary>
    bool RepairPathIfNeeded();
}
