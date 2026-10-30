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

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// A tab with no work of its own, for the tests of what every tab shares: the guarded run,
/// the gate, the refusal. Its name matters: <c>TabViewModel.TabName</c> strips the suffix, so
/// it is <c>Stub</c> in the log.
/// </summary>
sealed class StubTabViewModel : TabViewModel
{
    internal Task Run(string message, Func<Task> work) => RunGuardedAsync(message, work);
    internal void SetResult(string report) { Report = report; HasResult = true; }
    internal Task<IDisposable> Enter(HeavyRunGate? gate) => EnterGateAsync(gate, CancellationToken.None);
    internal void RefuseWith(string title, string text, string why) => Refuse(title, text, why);
}
