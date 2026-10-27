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

using System.Text.RegularExpressions;
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
    /// <summary>Every tab logs under its own name, <c>Window.&lt;Tab&gt;</c>, so the logger
    /// column says which tab a line belongs to and the messages carry no prefix.</summary>
    [Fact]
    public async Task R110_ATabsFailureGoesToTheLogWithItsExceptionAndTheScreenKeepsTheMessage()
    {
        var tab = new StubTabViewModel();
        using var capture = LogCapture.Start();

        await tab.Run("Lucrez...", () => Task.FromException(new InvalidDataException("FITS: header incomplet.")));

        Assert.Equal("FITS: header incomplet.", tab.ErrorText);
        var failed = Assert.Single(capture.Events, e => e.Level == LogLevel.Error);
        Assert.IsType<InvalidDataException>(failed.Exception);
        Assert.Equal("Window.Stub", failed.Logger);
        Assert.Matches(@"^run 1 failed after \d+ ms; the tab shows the message$", failed.Message);
        Assert.Contains("System.IO.InvalidDataException: FITS: header incomplet.", failed.Line);
        Assert.Contains(capture.Events, e => e.Level == LogLevel.Info && e.Logger == "Window.Stub" && e.Message == "run 1 started");
    }

    /// <summary>
    /// A refusal a tab shows instead of a result is in the log with its reason, in English,
    /// and the run that ended with it says <c>refused</c>, not <c>done</c> (the review of
    /// 2026-10-27, A6).
    /// </summary>
    [Fact]
    public async Task R110_ARefusalATabShowsIsLoggedWithItsReason_AndTheRunSaysRefusedNotDone()
    {
        var tab = new StubTabViewModel();
        using var capture = LogCapture.Start();

        await tab.Run("Lucrez...", () => { tab.RefuseWith("Nimic de rulat", "explicatia", "nothing to run on"); return Task.CompletedTask; });

        Assert.True(tab.HasError);
        Assert.Equal("Nimic de rulat", tab.ErrorTitle);
        Assert.Contains(capture.Events, e => e.Level == LogLevel.Info && e.Logger == "Window.Stub"
                                              && e.Message == "refused: nothing to run on; the tab shows \"Nimic de rulat\"");
        Assert.Contains(capture.Events, e => Regex.IsMatch(e.Message, @"^run 1 refused in \d+ ms$"));
        Assert.DoesNotContain(capture.Events, e => e.Message.StartsWith("run 1 done", StringComparison.Ordinal));
    }

    /// <summary>The Exposure tab's refusal at <c>Attach</c>, outside any run, is logged too.</summary>
    [Fact]
    public void R110_ARefusalAtAttachIsLoggedUnderTheTabsName()
    {
        using var f = new SessionFixture().WithFlatMaster().WithFlats();
        var tab = new ExposureTabViewModel();
        using var capture = LogCapture.Start();

        tab.Attach(f.Session, CancellationToken.None);

        Assert.True(tab.HasError);
        var refused = Assert.Single(capture.Events, e => e.Message.StartsWith("refused: ", StringComparison.Ordinal));
        Assert.Equal("Window.Exposure", refused.Logger);
        Assert.StartsWith("refused: no raw flat to check", refused.Message);
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
        Assert.Equal("run 1 failed after a newer run started; nothing is shown", lost.Message);
        Assert.Contains(capture.Events, e => e.Logger == "Window.Stub" && e.Message.StartsWith("run 2 done in ", StringComparison.Ordinal));
    }
}
