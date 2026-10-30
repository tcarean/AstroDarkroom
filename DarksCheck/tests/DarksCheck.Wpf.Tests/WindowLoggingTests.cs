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
using System.Text.Json;
using System.Text.RegularExpressions;
using DarksCheck.Core;
using DarksCheck.Tests;
using NLog;
using NLog.Targets;
using Xunit.Abstractions;

namespace DarksCheck.Wpf.Tests;

/// <summary>
/// The window's side of the diagnostic log (R-101, R-103, ADR 0025): a run's start and outcome,
/// the problem card with its exception while the card keeps the message, a stop, a run a newer one
/// replaced, the files the export writes and an export that fails, a settings file that cannot be
/// read, an exception the window does not handle, the window's lines in both languages, and two
/// writers on one file through the build of the atomic target the window ships. The view-model
/// alone, without a window, as <c>MainViewModelTests</c> runs it; LightsCheck's WindowLoggingTests
/// are the model.
/// </summary>
public class WindowLoggingTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>The runner's output, for the measured file list.</summary>
    public WindowLoggingTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Two windows writing one log keep every line (R-103), checked here on the build of the atomic
    /// target the window ships, <c>net8.0-windows</c>, a binary of its own that the core's tests do
    /// not load (LightsCheck's review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R103_TheWindowsBuildOfTheAtomicTargetKeepsEveryLineOfTwoWindows()
    {
        Assert.Equal("Windows7.0", typeof(AtomicFileTarget).Assembly.GetCustomAttribute<TargetPlatformAttribute>()?.PlatformName);
        using TempWorkspace ws = new();

        LogFiles.TwoWritersKeepEveryLine(Path.Combine(ws.Dir, "log"), "DarksCheck.Wpf", _output);
    }

    [Fact]
    public async Task R101_AnOpenLogsTheRunItsOptionsAndItsOutcome()
    {
        using TestSettings.Own own = new();
        using SessionFixture f = WindowFixture.Session();
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasResult);
        Assert.Contains("run 1 started: folder=" + f.Root + " threads=auto maxFrames=60 isoFactor=2 doubling=6 against=none", capture.Messages);
        Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Level == LogLevel.Info
            && Regex.IsMatch(e.Message, @"^run 1 done in \d+\.\d s; the tabs show the result$"));
        Assert.Contains(capture.Events, e => e.Logger == "DarkSeriesRun" && e.Message.StartsWith("darks done ", StringComparison.Ordinal));
        Assert.DoesNotContain(capture.Events, e => e.Level >= LogLevel.Warn);
    }

    [Fact]
    public async Task R101_AProblemCardIsLoggedWithTheExceptionAndTheScreenKeepsTheMessage()
    {
        using TestSettings.Own own = new();
        using TempWorkspace ws = new();
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        await vm.OpenAsync(Path.Combine(ws.Dir, "missing"));

        Assert.True(vm.HasProblem);
        LogCapture.Captured failed = Assert.Single(capture.Events, e => e.Level >= LogLevel.Warn);
        Assert.Equal("Window", failed.Logger);
        Assert.Equal(LogLevel.Error, failed.Level);
        Assert.Matches(@"^run 1 failed after \d+\.\d s; the problem card shows the message$", failed.Message);
        DirectoryNotFoundException e = Assert.IsType<DirectoryNotFoundException>(failed.Exception);
        Assert.Equal(e.Message, vm.ProblemText);
        Assert.Contains("   at DarksCheck.Core.SessionFolder.Open(", failed.Line, StringComparison.Ordinal);
    }

    /// <summary>A stop the user asked for is a line of its own, no exception: R-101 excepts it.</summary>
    [Fact]
    public async Task R101_ACancelledRunIsLoggedAsCancelledNotAsAFailure()
    {
        using TestSettings.Own own = new();
        using SessionFixture f = WindowFixture.Session();
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        Task run = vm.OpenAsync(f.Root);
        vm.CancelPending();
        await run;

        Assert.False(vm.HasResult);
        Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Level == LogLevel.Info && Regex.IsMatch(e.Message, @"^run 1 stopped after \d+\.\d s$"));
        Assert.DoesNotContain(capture.Events, e => e.Level >= LogLevel.Warn);
    }

    /// <summary>
    /// A run that a newer one replaced shows nothing, and its end is still in the log, at Debug, so
    /// the log of a user who opened two folders in a row says what became of the first.
    /// </summary>
    [Fact]
    public async Task R101_ASupersededRunIsKeptInTheLogThoughTheScreenShowsNothing()
    {
        using TestSettings.Own own = new();
        using SessionFixture f = WindowFixture.Session();
        using TempWorkspace ws = new();
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        Task first = vm.OpenAsync(f.Root);
        Task second = vm.OpenAsync(Path.Combine(ws.Dir, "missing"));
        await Task.WhenAll(first, second);

        Assert.True(vm.HasProblem);
        Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Level == LogLevel.Debug
            && Regex.IsMatch(e.Message, @"^run 1 stopped after \d+\.\d s by a newer run; nothing is shown$"));
        Assert.Contains(capture.Events, e => e.Logger == "Window" && e.Level == LogLevel.Error && e.Message.StartsWith("run 2 failed after ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task R101_AnExportLogsEveryFileAndAFailedExportItsException()
    {
        using TestSettings.Own own = new();
        using SessionFixture f = WindowFixture.Session();
        using TempWorkspace ws = new();
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);
        using LogCapture capture = LogCapture.Start();

        vm.Export(ws.Dir);
        vm.Export(f.Root);

        string[] wrote = capture.Events.Where(e => e.Logger == "Window" && e.Message.StartsWith("wrote ", StringComparison.Ordinal)).Select(e => e.Message).ToArray();
        Assert.Equal(Directory.GetFiles(ws.Dir).Length, wrote.Length);
        Assert.All(wrote, m => Assert.Matches(@"^wrote .+ \(\d+\.\d KiB\)$", m));
        LogCapture.Captured failed = Assert.Single(capture.Events, e => e.Level >= LogLevel.Warn);
        Assert.Equal("the export into " + f.Root + " failed; the status line shows the message", failed.Message);
        Assert.IsType<InvalidOperationException>(failed.Exception);
    }

    [Fact]
    public void R101_ASettingsFileThatCannotBeReadIsLoggedAndTheDefaultsAreUsed()
    {
        using TestSettings.Own own = new();
        Directory.CreateDirectory(Path.GetDirectoryName(own.FilePath)!);
        File.WriteAllText(own.FilePath, "{ not json");
        using LogCapture capture = LogCapture.Start();

        StoredSettings s = UserSettings.Load();

        Assert.Null(s.Partner);
        LogCapture.Captured line = Assert.Single(capture.Events);
        Assert.Equal("Window", line.Logger);
        Assert.Equal(LogLevel.Debug, line.Level);
        Assert.Equal("the settings " + own.FilePath + " could not be read; the defaults are used", line.Message);
        Assert.IsAssignableFrom<JsonException>(line.Exception);
    }

    /// <summary>
    /// An exception the window does not handle is written at Error with its stack by the run's
    /// exception filter, which returns false, so the exception leaves as before; the filter records
    /// it, so the hook that meets its task later names it instead of writing it again
    /// (LightsCheck's second review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R101_AnExceptionTheWindowDoesNotHandleIsWrittenOnceAndTheHookNamesIt()
    {
        FormatException bug = new("the view-model's bug");
        using LogCapture capture = LogCapture.Start();

        Assert.False(MainViewModel.LogUnhandled(3, bug));
        LogSetup.Unobserved(new AggregateException(bug));

        LogCapture.Captured written = Assert.Single(capture.Events, e => e.Logger == "Window" && e.Level == LogLevel.Error);
        Assert.Same(bug, written.Exception);
        Assert.Equal("run 3 failed with an exception the window does not handle; it shows no result", written.Message);
        LogCapture.Captured named = Assert.Single(capture.Events, e => e.Logger == "Process" && e.Level == LogLevel.Error);
        Assert.Equal("a task failed and nobody awaited it: the exception logged above", named.Message);
        Assert.Null(named.Exception);
    }

    /// <summary>
    /// The window's own lines are English whatever the interface's language (R-101): a run opened and
    /// its files exported write the same messages in English and in Romanian, the times, the export
    /// folder, the working set and the exported files' sizes aside, since the report in them is in
    /// the run's language.
    /// </summary>
    [Fact]
    public async Task R101_TheWindowsLinesAreTheSameInBothLanguages()
    {
        using TestSettings.Own own = new();
        using SessionFixture f = WindowFixture.Session();

        string[] en = await MessagesOf(f.Root, UiLanguage.English);
        string[] ro = await MessagesOf(f.Root, UiLanguage.Romanian);

        Assert.Contains(en, m => m.StartsWith("wrote <out>", StringComparison.Ordinal));
        Assert.Equal(en, ro);
    }

    /// <summary>The window's messages for one run in one language, in a fixed order, the times, the export folder, the working sets and the files' sizes made equal.</summary>
    private static async Task<string[]> MessagesOf(string root, UiLanguage language)
    {
        using TempWorkspace ws = new();
        using IDisposable scope = Ui.Use(language);
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        await vm.OpenAsync(root);
        vm.Export(ws.Dir);

        return capture.Messages
            .Select(m => LogFiles.ElapsedTime().Replace(m, "#"))
            .Select(m => m.Replace(ws.Dir, "<out>", StringComparison.Ordinal))
            .Select(m => Regex.Replace(m, @"working set \d+\.\d MiB", "working set #"))
            .Select(m => Regex.Replace(m, @"\(\d+\.\d KiB\)$", "(# KiB)"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
