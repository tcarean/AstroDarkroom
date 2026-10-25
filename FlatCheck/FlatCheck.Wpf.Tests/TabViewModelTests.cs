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
/// Starea comuna a taburilor, pe un tab de proba. Ce s-a adaugat pentru tabul de
/// histograma - rularile depasite - se verifica aici, fiindca e a bazei, nu a lui:
/// o rulare peste care a pornit alta nu mai atinge nici rezultatul, nici eroarea,
/// nici ecranul de asteptare.
/// </summary>
public class TabViewModelTests
{
    sealed class StubTab : TabViewModel
    {
        internal Task Run(string message, Func<Task> work) => RunGuardedAsync(message, work);
        internal void SetResult(string report) { Report = report; HasResult = true; }
        internal Task WaitForChain(Func<Task> waitIdle) => WaitForChainAsync(waitIdle);
    }

    /// <summary>
    /// A run that awaits the opening chain says so while it waits, then shows its own message:
    /// the first line of the waiting screen reads the waiting text while the chain still runs,
    /// and the run's message once it ended. With the chain already over, or no chain to wait
    /// for, the run's message is the only one shown.
    /// </summary>
    [Fact]
    public async Task ARunThatWaitsForTheChainSaysSoUntilItEnds()
    {
        var tab = new StubTab();
        var chain = new TaskCompletionSource();
        var messages = new List<string>();
        tab.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(tab.BusyMessage)) messages.Add(tab.BusyMessage); };

        Task run = tab.Run("Lucrez...", async () => { await tab.WaitForChain(() => chain.Task); await Task.Yield(); });
        Assert.Equal(SessionMessages.WaitingForOpeningChain(), tab.BusyMessage);

        chain.SetResult();
        await run;

        Assert.Equal(new[] { "Lucrez...", SessionMessages.WaitingForOpeningChain(), "Lucrez..." }, messages);
        Assert.False(tab.IsBusy);

        messages.Clear();
        await tab.Run("Lucrez iar...", () => tab.WaitForChain(() => Task.CompletedTask));
        Assert.Equal(new[] { "Lucrez iar..." }, messages);
        messages.Clear();
        await tab.Run("Lucrez fara lant...", () => tab.WaitForChain(null));
        Assert.Equal(new[] { "Lucrez fara lant..." }, messages);
    }

    /// <summary>
    /// Two runs waiting for the same chain: when it ends, only the latest one puts its message
    /// back - the superseded one would otherwise write its own over the new one's.
    /// </summary>
    [Fact]
    public async Task OnlyTheLatestWaitingRunRestoresItsMessage()
    {
        var tab = new StubTab();
        var chain = new TaskCompletionSource();

        Task first = tab.Run("A", async () => { await tab.WaitForChain(() => chain.Task); throw new OperationCanceledException(); });
        Task second = tab.Run("B", async () => { await tab.WaitForChain(() => chain.Task); await Task.Delay(50); });
        chain.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal("B", tab.BusyMessage);
        Assert.False(tab.IsBusy);
    }

    [Fact]
    public async Task ARunThatFailsShowsAnErrorAndClearsTheResult()
    {
        var tab = new StubTab();
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
        var tab = new StubTab();
        tab.SetResult("vechi");

        await tab.Run("x", () => throw new OperationCanceledException());

        Assert.False(tab.HasError);
        Assert.False(tab.HasResult);
        Assert.False(tab.IsBusy);
    }

    /// <summary>
    /// A porneste si se blocheaza; B porneste peste ea si termina; A termina pe urma,
    /// cu eroare. Ecranul e al lui B: rezultat, fara eroare, fara ecran de asteptare -
    /// iar A n-a stins nimic si n-a aratat nimic.
    /// </summary>
    [Fact]
    public async Task ASupersededRunDoesNotTouchTheScreenWhenItFinishes()
    {
        var tab = new StubTab();
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
        // ocupat o data (A), ramas ocupat la B (nicio schimbare), liber o singura data (B)
        Assert.Equal(new[] { true, false }, busyChanges);
    }

    /// <summary>Si invers: A depasita se anuleaza; tot nu goleste rezultatul lui B.</summary>
    [Fact]
    public async Task ASupersededRunThatIsCancelledDoesNotClearTheNewResult()
    {
        var tab = new StubTab();
        var gate = new TaskCompletionSource();

        Task first = tab.Run("A", async () => { await gate.Task; throw new OperationCanceledException(); });
        await tab.Run("B", async () => { await Task.Yield(); tab.SetResult("nou"); });
        gate.SetResult();
        await first;

        Assert.True(tab.HasResult);
        Assert.False(tab.IsBusy);
    }
}
