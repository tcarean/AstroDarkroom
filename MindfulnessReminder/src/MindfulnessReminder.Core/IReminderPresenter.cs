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

/// <summary>The reminder window, as far as the coordinator needs to know about it.</summary>
public interface IReminderPresenter
{
    /// <summary>True while a reminder is on screen, or scheduled to appear shortly.</summary>
    bool IsShowing { get; }

    /// <summary>
    /// Shows a reminder that says <paramref name="message"/>. The coordinator chooses the
    /// text, because the coordinator is what knows which of the two reminders this is;
    /// the presenter only draws what it is handed.
    /// </summary>
    /// <param name="message">What the reminder says, drawn as given.</param>
    void Show(string message);

    /// <summary>
    /// Raises the reminder that is already showing. It keeps the text it opened with.
    /// </summary>
    void BringToFront();
}
