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

// Detached.cs - a task the window starts and does not await.
//
// The window fires the opening of a folder from a click or from the start, with no one to await
// it. A task that faults there reaches no handler: App's handler sees only what is thrown on
// the dispatcher. Watch is that handler: the exception goes to the log, whole.

using System;
using System.Threading.Tasks;
using NLog;

namespace FlatCheck.Wpf
{
    /// <summary>The tasks the window starts without awaiting them.</summary>
    internal static class Detached
    {
        /// <summary>
        /// Follows a task nobody awaits to its end: a fault is written to the log at
        /// <c>Error</c>, with the exception and its stack (R-110); a cancellation, which the
        /// user asked for, is not.
        /// </summary>
        /// <param name="task">The task started; not null.</param>
        /// <param name="log">The logger of whoever started it.</param>
        /// <param name="what">What the task was doing, in the log's English, to start the line:
        /// "opening E:\session".</param>
        /// <returns>A task that completes when <paramref name="task"/> has; it never
        /// faults.</returns>
        internal static async Task Watch(Task task, Logger log, string what)
        {
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception e) { log.Error(e, what + " failed in a task nobody awaits; nothing on screen says so"); }
        }
    }
}
