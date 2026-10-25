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
/// The one gate every heavy run of the window takes (ADR 0061): one holder at a time, the
/// others in order, a cancelled waiter out of the queue, a lease released once however often
/// it is disposed.
/// </summary>
public class HeavyRunGateTests
{
    [Fact]
    public async Task OneRunHoldsItAndTheNextWaitsUntilItLetsGo()
    {
        var gate = new HeavyRunGate();
        Assert.False(gate.IsHeld);

        IDisposable first = await gate.EnterAsync(CancellationToken.None);
        Assert.True(gate.IsHeld);
        Task<IDisposable> second = gate.EnterAsync(CancellationToken.None);
        Assert.NotSame(second, await Task.WhenAny(second, Task.Delay(500)));

        first.Dispose();
        using (await second) Assert.True(gate.IsHeld);

        Assert.False(gate.IsHeld);
    }

    [Fact]
    public async Task WaitersEnterInTheOrderTheyQueued()
    {
        var gate = new HeavyRunGate();
        IDisposable held = await gate.EnterAsync(CancellationToken.None);
        var order = new List<int>();
        Task Enter(int n) => gate.EnterAsync(CancellationToken.None).ContinueWith(t => { order.Add(n); t.Result.Dispose(); });
        Task a = Enter(1), b = Enter(2), c = Enter(3);

        held.Dispose();
        await Task.WhenAll(a, b, c);

        Assert.Equal(new[] { 1, 2, 3 }, order);
        Assert.False(gate.IsHeld);
    }

    /// <summary>A waiter whose token is cancelled leaves the queue at once and never holds the
    /// gate: a superseded pick does not compute when its turn would have come.</summary>
    [Fact]
    public async Task ACancelledWaiterLeavesTheQueueWithoutEntering()
    {
        var gate = new HeavyRunGate();
        IDisposable held = await gate.EnterAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();
        Task<IDisposable> waiting = gate.EnterAsync(cts.Token);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        held.Dispose();
        Assert.False(gate.IsHeld);
        using (await gate.EnterAsync(CancellationToken.None)) Assert.True(gate.IsHeld);
    }

    [Fact]
    public async Task DisposingALeaseTwiceReleasesOnce()
    {
        var gate = new HeavyRunGate();
        IDisposable lease = await gate.EnterAsync(CancellationToken.None);

        lease.Dispose();
        lease.Dispose();

        Assert.False(gate.IsHeld);
        IDisposable again = await gate.EnterAsync(CancellationToken.None);
        Task<IDisposable> blocked = gate.EnterAsync(CancellationToken.None);
        Assert.NotSame(blocked, await Task.WhenAny(blocked, Task.Delay(300)));   // still one at a time
        again.Dispose();
        (await blocked).Dispose();
    }
}
