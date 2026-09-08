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
/// Asks Windows what the user is currently in the middle of. The production implementation
/// reads the per-app microphone and camera usage Windows records for its own privacy
/// indicator, plus SHQueryUserNotificationState; both live in the App layer, and this
/// interface is what keeps the rules that act on them testable off a real machine.
///
/// It is asked ONLY at the moment a reminder is due, and once per retry while one is being
/// held back -- never on the one-second tick. A reminder every 30 minutes means two registry
/// reads an hour.
/// </summary>
public interface IActivityMonitor
{
    /// <summary>Never throws: anything unreadable reads as "nothing going on".</summary>
    ActivitySnapshot Read();
}

/// <summary>
/// Sees nothing, ever. What the coordinator uses when no real monitor is supplied, so that a
/// test about the schedule does not have to say anything about microphones.
/// </summary>
public sealed class IdleActivityMonitor : IActivityMonitor
{
    public static readonly IdleActivityMonitor Instance = new();

    public ActivitySnapshot Read() => ActivitySnapshot.Idle;
}
