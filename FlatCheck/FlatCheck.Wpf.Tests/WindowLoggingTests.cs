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

using FlatCheck.Tests;
using NLog;

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// The window's side of the diagnostic log (R-110, ADR 0070): what a tab shows the user stays
/// as it was, and the log gets the exception whole, the one of a run nobody waits for any
/// more included.
/// </summary>
public class WindowLoggingTests
{
    sealed class StubTabViewModel : TabViewModel
    {
        internal Task Run(string message, Func<Task> work) => RunGuardedAsync(message, work);
        internal void SetResult(string report) { Report = report; HasResult = true; }
    }

    [Fact]
    public async Task R110_ATabsFailureGoesToTheLogWithItsExceptionAndTheScreenKeepsTheMessage()
    {
        var tab = new StubTabViewModel();
        using var capture = LogCapture.Start();

        await tab.Run("Lucrez...", () => Task.FromException(new InvalidDataException("FITS: header incomplet.")));

        Assert.Equal("FITS: header incomplet.", tab.ErrorText);
        var failed = Assert.Single(capture.Events, e => e.Level == LogLevel.Error);
        Assert.IsType<InvalidDataException>(failed.Exception);
        Assert.Matches(@"^Stub tab, run 1 failed after \d+ ms; the tab shows the message$", failed.Message);
        Assert.Contains("System.IO.InvalidDataException: FITS: header incomplet.", failed.Line);
        Assert.Contains(capture.Events, e => e.Level == LogLevel.Info && e.Message == "Stub tab, run 1 started");
    }

    [Fact]
    public async Task R110_ASupersededRunsFailureIsKeptInTheLogThoughTheScreenShowsNothing()
    {
        var tab = new StubTabViewModel();
        var gate = new TaskCompletionSource();
        using var capture = LogCapture.Start();

        Task first = tab.Run("A", async () => { await gate.Task; throw new InvalidOperationException("veche"); });
        await tab.Run("B", async () => { await Task.Yield(); tab.SetResult("nou"); });
        gate.SetResult();
        await first;

        Assert.False(tab.HasError);
        var lost = Assert.Single(capture.Events, e => e.Exception is InvalidOperationException);
        Assert.Equal(LogLevel.Debug, lost.Level);
        Assert.Equal("Stub tab, run 1 failed after a newer run started; nothing is shown", lost.Message);
        Assert.Contains(capture.Events, e => e.Message.StartsWith("Stub tab, run 2 done in ", StringComparison.Ordinal));
    }
}
