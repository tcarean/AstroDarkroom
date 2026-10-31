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
/// The tabs' common state, on a sample tab. What was added for the histogram tab -
/// the superseded runs - is checked here, because it belongs to the base, not to it:
/// a run over which another has started touches neither the result, nor the error,
/// nor the waiting screen.
/// </summary>
public class TabViewModelTests
{
    /// <summary>
    /// A run that waits for the heavy-run gate says so while it waits, then shows its own
    /// message: the first line of the waiting screen reads the waiting text while another run
    /// holds the gate, and the run's message once it is in. With the gate free, or no gate at
    /// all, the run's message is the only one shown.
    /// </summary>
    [Fact]
    public async Task ARunThatWaitsForTheGateSaysSoUntilItIsIn()
    {
        var tab = new StubTabViewModel();
        var gate = new HeavyRunGate();
        IDisposable held = await gate.EnterAsync(CancellationToken.None);
        var messages = new List<string>();
        tab.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(tab.BusyMessage)) messages.Add(tab.BusyMessage); };

        Task run = tab.Run("Lucrez...", async () => { using (await tab.Enter(gate)) await Task.Yield(); });
        Assert.Equal(SessionMessages.WaitingForRunningAnalysis(), tab.BusyMessage);

        held.Dispose();
        await run;

        Assert.Equal(new[] { "Lucrez...", SessionMessages.WaitingForRunningAnalysis(), "Lucrez..." }, messages);
        Assert.False(tab.IsBusy);
        Assert.False(gate.IsHeld);

        messages.Clear();
        await tab.Run("Lucrez iar...", async () => { using (await tab.Enter(gate)) { } });
        Assert.Equal(new[] { "Lucrez iar..." }, messages);
        messages.Clear();
        await tab.Run("Lucrez fara poarta...", async () => { using (await tab.Enter(null)) { } });
        Assert.Equal(new[] { "Lucrez fara poarta..." }, messages);
    }

    /// <summary>
    /// Two runs waiting for the same gate: when it opens, only the latest one puts its message
    /// back - the superseded one would otherwise write its own over the new one's.
    /// </summary>
    [Fact]
    public async Task OnlyTheLatestWaitingRunRestoresItsMessage()
    {
        var tab = new StubTabViewModel();
        var gate = new HeavyRunGate();
        IDisposable held = await gate.EnterAsync(CancellationToken.None);

        Task first = tab.Run("A", async () => { using (await tab.Enter(gate)) throw new OperationCanceledException(); });
        Task second = tab.Run("B", async () => { using (await tab.Enter(gate)) await Task.Delay(50); });
        held.Dispose();
        await Task.WhenAll(first, second);

        Assert.Equal("B", tab.BusyMessage);
        Assert.False(tab.IsBusy);
        Assert.False(gate.IsHeld);
    }

    [Fact]
    public async Task ARunThatFailsShowsAnErrorAndClearsTheResult()
    {
        var tab = new StubTabViewModel();
        tab.SetResult("vechi");

        await tab.Run("x", () => throw new InvalidOperationException("a crapat"));

        Assert.True(tab.HasError);
        Assert.Contains("a crapat", tab.ErrorText);
        Assert.False(tab.HasResult);
        Assert.False(tab.IsBusy);
    }

    [Fact]
    public async Task ACancelledRunLeavesTheTabEmptyWithoutAnError()
    {
        var tab = new StubTabViewModel();
        tab.SetResult("vechi");

        await tab.Run("x", () => throw new OperationCanceledException());

        Assert.False(tab.HasError);
        Assert.False(tab.HasResult);
        Assert.False(tab.IsBusy);
    }

    /// <summary>
    /// A starts and blocks; B starts over it and finishes; A finishes afterwards,
    /// with an error. The screen is B's: a result, no error, no waiting screen -
    /// and A switched nothing off and showed nothing.
    /// </summary>
    [Fact]
    public async Task ASupersededRunDoesNotTouchTheScreenWhenItFinishes()
    {
        var tab = new StubTabViewModel();
        var gate = new TaskCompletionSource();
        var busyChanges = new List<bool>();
        tab.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(tab.IsBusy)) busyChanges.Add(tab.IsBusy); };

        Task first = tab.Run("A", async () => { await gate.Task; throw new InvalidOperationException("veche"); });
        Task second = tab.Run("B", async () => { await Task.Yield(); tab.SetResult("nou"); });
        await second;

        Assert.True(tab.HasResult);
        Assert.False(tab.IsBusy);
        Assert.Equal("nou", tab.Report);

        gate.SetResult();
        await first;

        Assert.True(tab.HasResult);
        Assert.False(tab.HasError);
        Assert.False(tab.IsBusy);
        Assert.Equal("nou", tab.Report);
        // busy once (A), still busy at B (no change), free a single time (B)
        Assert.Equal(new[] { true, false }, busyChanges);
    }

    /// <summary>
    /// And the reverse: a superseded A is cancelled; it still does not clear B's result.
    /// </summary>
    [Fact]
    public async Task ASupersededRunThatIsCancelledDoesNotClearTheNewResult()
    {
        var tab = new StubTabViewModel();
        var gate = new TaskCompletionSource();

        Task first = tab.Run("A", async () => { await gate.Task; throw new OperationCanceledException(); });
        await tab.Run("B", async () => { await Task.Yield(); tab.SetResult("nou"); });
        gate.SetResult();
        await first;

        Assert.True(tab.HasResult);
        Assert.False(tab.IsBusy);
    }
}
