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
/// Where <see cref="ReminderSettings"/> live between runs. Behind an interface for the same
/// reason as <see cref="IRegistryStore"/>: the loading rules -- what a missing value means,
/// what a corrupt one means -- are worth testing off a real machine.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Never throws and never returns null; anything unreadable falls back to the defaults.</summary>
    ReminderSettings Load();

    void Save(ReminderSettings settings);
}
