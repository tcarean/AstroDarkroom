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
