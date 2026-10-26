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

// HeavyRunGate.cs - the one gate every heavy run of the window takes (ADR 0061).
//
// Measured on 2026-10-25 on the 24 MP masters: one run alone peaks at 0.7-1.25 GB of working
// set, and two at once add up (Light series + the two-master run of the time, 2.2 GB). The "sequential, not parallel" rule
// of MainViewModel used to be kept per tab, through a seam that waited for the opening chain, so
// two click-runs could still overlap, and the Expo and Histogram tabs did not wait at all. This
// gate is the rule itself: the opening chain takes it once, every click-run takes it around its
// computation, and whoever holds it is the only one loading frames.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    /// <summary>A gate one heavy run holds at a time; the others queue in order and leave the
    /// queue when their token is cancelled.</summary>
    internal sealed class HeavyRunGate
    {
        readonly SemaphoreSlim _one = new(1, 1);

        /// <summary>True while a run holds the gate. Read before entering, so a waiting screen
        /// can say it waits; a run that enters when nobody holds it waits for nothing.</summary>
        internal bool IsHeld => _one.CurrentCount == 0;

        /// <summary>Enters the gate, waiting for the run that holds it, if any.</summary>
        /// <param name="ct">The run's token; a cancellation while queued leaves the queue with an
        /// <see cref="OperationCanceledException"/> and takes nothing.</param>
        /// <returns>The lease; disposing it lets the next run in. Dispose it exactly once, in a
        /// <c>using</c>.</returns>
        internal async Task<IDisposable> EnterAsync(CancellationToken ct)
        {
            await _one.WaitAsync(ct).ConfigureAwait(false);
            return new Lease(_one);
        }

        /// <summary>The hold on the gate; disposing releases it, once.</summary>
        sealed class Lease : IDisposable
        {
            SemaphoreSlim _held;

            /// <summary>A lease on a semaphore just entered.</summary>
            /// <param name="held">The semaphore to release on dispose.</param>
            internal Lease(SemaphoreSlim held) { _held = held; }

            /// <inheritdoc/>
            public void Dispose() => Interlocked.Exchange(ref _held, null)?.Release();
        }
    }
}
