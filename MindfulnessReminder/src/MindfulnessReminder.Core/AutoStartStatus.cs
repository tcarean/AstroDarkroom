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

public enum AutoStartStatus
{
    /// <summary>No Run entry exists: the app will not start with Windows.</summary>
    Disabled,

    /// <summary>A Run entry exists and Windows will honour it.</summary>
    Enabled,

    /// <summary>
    /// A Run entry exists but the user switched it off from Task Manager's Startup tab,
    /// so Windows ignores it. The tray checkbox must show this as "off".
    /// </summary>
    BlockedByWindows,
}
