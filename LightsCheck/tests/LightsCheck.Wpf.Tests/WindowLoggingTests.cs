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

using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using LightsCheck.Core;
using LightsCheck.Tests;
using NLog;
using NLog.Targets;
using Xunit.Abstractions;

namespace LightsCheck.Wpf.Tests;

/// <summary>
/// The window's side of the diagnostic log (R-071, R-073, ADR 0029): a session run's start and
/// outcome, the refused folder with its exception while the card keeps the message, a stop, the
/// files the export writes, a superseded run kept at Debug, the window's lines in both languages,
/// and two writers on one file through the build of the atomic target the window ships. The
/// view-model alone, without a window, as <c>MainViewModelTests</c> runs it.
/// </summary>
public class WindowLoggingTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>The runner's output, for the measured file list.</summary>
    public WindowLoggingTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Two windows writing one log keep every line (R-073), checked here on the build of the atomic
    /// target the window ships, <c>net8.0-windows</c>, a binary of its own that the core's tests do
    /// not load (the review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R073_TheWindowsBuildOfTheAtomicTargetKeepsEveryLineOfTwoWindows()
    {
        Assert.Equal("Windows7.0", typeof(AtomicFileTarget).Assembly.GetCustomAttribute<TargetPlatformAttribute>()?.PlatformName);
        using TempWorkspace ws = new();

        LogFiles.TwoWritersKeepEveryLine(Path.Combine(ws.Dir, "log"), "LightsCheck.Wpf", _output);
    }

    /// <summary>
    /// The window's own lines are English whatever the interface's language (R-071): a session
    /// opened, a frame selected for its cut-outs and the files exported write the same messages in
    /// English and in Romanian, the times, the export folder, the order of the frames' lines and the
    /// exported files' sizes aside, since the report in them is in the run's language (the review
    /// of 2026-10-30).
    /// </summary>
    [Fact]
    public async Task R071_TheWindowsLinesAreTheSameInBothLanguages()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);

        string[] en = await MessagesOf(f.Root, UiLanguage.English);
        string[] ro = await MessagesOf(f.Root, UiLanguage.Romanian);

        Assert.Contains(en, m => m.StartsWith("cut-outs of frame 1 ", StringComparison.Ordinal));
        Assert.Contains(en, m => m.StartsWith("wrote <out>", StringComparison.Ordinal));
        Assert.Equal(en, ro);
    }

    /// <summary>The window's messages for one session in one language, in a fixed order, the times, the export folder and the files' sizes made equal.</summary>
    private static async Task<string[]> MessagesOf(string root, UiLanguage language)
    {
        using TempWorkspace ws = new();
        using IDisposable scope = Ui.Use(language);
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        await vm.OpenAsync(root);
        vm.SelectedFrame = vm.Frames[0];
        await vm.CropsPending;
        vm.Export(ws.Dir);

        return capture.Messages
            .Select(m => LogFiles.ElapsedTime().Replace(m, "#"))
            .Select(m => m.Replace(ws.Dir, "<out>", StringComparison.Ordinal))
            .Select(m => Regex.Replace(m, @"\(\d+\.\d KiB\)$", "(# KiB)"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    [Fact]
    public async Task R071_ASessionRunLogsItsStartAndItsOutcome()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasResult);
        // The arrows come from the settings file other tests may have changed, so they are not
        // pinned here.
        Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Level == LogLevel.Info
            && e.Message.StartsWith("session " + f.Root + ": run started pixel=none threads=default east=", StringComparison.Ordinal));
        Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Level == LogLevel.Info
            && Regex.IsMatch(e.Message, @"^session .+: done in \d+\.\d s; the tabs show the result$"));
        Assert.Contains(capture.Events, e => e.Logger == "SeriesRun" && e.Message.StartsWith("series done frames=8 ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task R071_ARefusedFolderIsAWarnLineWithTheExceptionAndTheCardKeepsTheMessage()
    {
        using TempWorkspace ws = new();
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        await vm.OpenAsync(Path.Combine(ws.Dir, "missing"));

        Assert.True(vm.HasProblem);
        Assert.Contains("missing", vm.ProblemText, StringComparison.Ordinal);
        LogCapture.Captured refused = Assert.Single(capture.Events, e => e.Level == LogLevel.Warn);
        Assert.Equal("Window", refused.Logger);
        Assert.IsType<DirectoryNotFoundException>(refused.Exception);
        Assert.Matches(@"^session .+: refused after \d+\.\d s; the card shows the message$", refused.Message);
        Assert.Contains("   at LightsCheck.Core.SessionFolder.Open(", refused.Line, StringComparison.Ordinal);
    }

    /// <summary>A stop the user asked for is a line of its own, no exception: R-071 excepts it.</summary>
    [Fact]
    public async Task R071_AStoppedRunSaysSoAndLogsNoException()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        Task run = vm.OpenAsync(f.Root);
        vm.CancelPending();
        await run;

        Assert.False(vm.HasResult);
        Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Level == LogLevel.Info
            && Regex.IsMatch(e.Message, @"^session .+: stopped after \d+\.\d s$"));
        Assert.DoesNotContain(capture.Events, e => e.Level >= LogLevel.Warn);
    }

    [Fact]
    public async Task R071_EveryFileTheWindowWritesIsOneLineWithItsSize()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace ws = new();
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);
        using LogCapture capture = LogCapture.Start();

        vm.Export(ws.Dir);

        List<LogCapture.Captured> wrote = capture.Events.Where(e => e.Logger == "Window" && e.Message.StartsWith("wrote ", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, wrote.Count);
        Assert.All(wrote, e => Assert.Matches(@"^wrote .+ \(\d+\.\d KiB\)$", e.Message));
    }

    /// <summary>
    /// A run that a newer one replaced shows nothing, and its end is still in the log, at Debug,
    /// so the log of a user who opened two folders in a row says what became of the first.
    /// </summary>
    [Fact]
    public async Task R071_ASupersededRunsOutcomeIsKeptAtDebugThoughTheScreenShowsNothing()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace ws = new();
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        Task first = vm.OpenAsync(f.Root);
        Task second = vm.OpenAsync(Path.Combine(ws.Dir, "missing"));
        await Task.WhenAll(first, second);

        Assert.True(vm.HasProblem);
        Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Level == LogLevel.Debug
            && e.Message.StartsWith("session " + f.Root + ": stopped after ", StringComparison.Ordinal)
            && e.Message.EndsWith(" by a newer run; nothing is shown", StringComparison.Ordinal));
    }
}
