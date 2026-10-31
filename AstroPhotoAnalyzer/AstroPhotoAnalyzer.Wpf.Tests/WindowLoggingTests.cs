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

// WindowLoggingTests.cs - the window's side of the diagnostic log (R-171, R-173, ADR 0069).
// DarksCheck's WindowLoggingTests are the model.

using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Documents;
using System.Windows.Navigation;
using AstroPhotoAnalyzer.Tests;
using NLog;
using NLog.Targets;
using Xunit.Abstractions;

namespace AstroPhotoAnalyzer.Wpf.Tests;

/// <summary>
/// A run's start and outcome, a failure with its exception while the screen keeps the message, a
/// refusal, a refused field, the profile chosen and cleared, the report saved and a save that
/// fails, a settings file that cannot be read or written, an exception the window does not handle,
/// the window's lines in both languages, a link the About window cannot open, and two writers on
/// one file through the build of the atomic target the window ships. The view-model alone, as
/// ViewModelTests runs it, except the About window, which needs the UI thread.
/// </summary>
[Collection("fereastra")]
public class WindowLoggingTests
{
    readonly StaFixture _sta;
    readonly ITestOutputHelper _output;

    /// <summary>The UI thread, for the About window, and the runner's output, for the measured file list.</summary>
    public WindowLoggingTests(StaFixture sta, ITestOutputHelper output)
    {
        _sta = sta;
        _output = output;
    }

    /// <summary>
    /// Two windows writing one log keep every line (R-173), checked here on the build of the atomic
    /// target the window ships, <c>net8.0-windows</c>, a binary of its own that the core's tests do
    /// not load (LightsCheck's review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R173_TheWindowsBuildOfTheAtomicTargetKeepsEveryLineOfTwoWindows()
    {
        Assert.Equal("Windows7.0", typeof(AtomicFileTarget).Assembly.GetCustomAttribute<TargetPlatformAttribute>()?.PlatformName);
        using TempWorkspace ws = new();

        LogFiles.TwoWritersKeepEveryLine(Path.Combine(ws.Dir, "log"), "AstroPhotoAnalyzer.Wpf", _output);
    }

    [Fact]
    public async Task R171_AnOpenLogsTheRunItsPathAndItsOutcome()
    {
        using TempWorkspace ws = new();
        string tif = SkyTiff(ws);
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        await vm.OpenAsync(tif);
        await vm.RunAsync();
        await Task.WhenAll(vm.CurvesComputation, vm.ProofComputation);

        Assert.True(vm.HasResult, vm.Error);
        string[] lines = WindowLines(capture);
        Assert.Equal(4, lines.Length);
        Assert.Equal("run 1 started: path=" + tif, lines[0]);
        Assert.Matches(@"^run 1 done in \d+ ms: verdict=(Ok|Attention|Problem)$", lines[1]);
        Assert.Equal("run 2 started: path=" + tif, lines[2]);
        Assert.Matches(@"^run 2 done in \d+ ms: verdict=(Ok|Attention|Problem)$", lines[3]);
    }

    [Fact]
    public async Task R171_AFailedRunIsLoggedWithTheExceptionAndTheScreenKeepsTheMessage()
    {
        using TempWorkspace ws = new();
        string gone = ws.PathTo("missing.tif");
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        await vm.OpenAsync(gone);

        Assert.True(vm.HasError);
        LogCapture.Captured failed = Assert.Single(capture.Events, e => e.Logger == "Window" && e.Level == LogLevel.Error);
        Assert.Matches(@"^run 1 failed after \d+ ms; the window shows the message$", failed.Message);
        Assert.IsType<FileNotFoundException>(failed.Exception);
        Assert.Equal(vm.Error, MainViewModel.Describe(failed.Exception!));
    }

    /// <summary>A linear image and a field the run cannot read are refusals at Warn, as the screen shows them, not failures (R-171).</summary>
    [Fact]
    public async Task R171_ALinearImageAndABadFieldAreRefusalsNotFailures()
    {
        using TempWorkspace ws = new();
        // 262 / 65535 is 0.004, a linear sky.
        string linear = ws.WriteFile("linear.tif", TiffTestData.Build16Rgb(64, 64, (x, y, c) => 262, IccTestData.V2("sRGB test")));
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        await vm.OpenAsync(linear);
        string refusal = vm.Error;
        vm.Parameters.Distance = "1,5";
        await vm.RunAsync();

        string[] warnings = capture.Events.Where(e => e.Logger == "Window" && e.Level == LogLevel.Warn).Select(e => e.Message).ToArray();
        Assert.Equal(2, warnings.Length);
        Assert.Matches(@"^run 1 refused after \d+ ms: ", warnings[0]);
        Assert.EndsWith(": " + refusal, warnings[0], StringComparison.Ordinal);
        Assert.Equal("refused: " + vm.Error, warnings[1]);
        Assert.DoesNotContain(capture.Events, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task R171_AProfileChosenAndClearedIsLogged()
    {
        using TempWorkspace ws = new();
        string icc = ws.WriteFile("paper.icc", IccPrinterTestData.V2Lut16("Paper test", 3.5));
        MainViewModel vm = new();
        using LogCapture capture = LogCapture.Start();

        await vm.ChooseProfile(new IccProfileRow(IccPaperBlack.Read(icc)));
        await vm.ClearProfile();

        Assert.Equal(new[] { "profile in use: " + icc, "profile cleared" }, WindowLines(capture));
    }

    [Fact]
    public async Task R171_ASavedReportIsOneLineWithItsSize_AndAFailedSaveItsException()
    {
        using TempWorkspace ws = new();
        string tif = SkyTiff(ws);
        MainViewModel vm = new();
        await vm.OpenAsync(tif);
        await Task.WhenAll(vm.CurvesComputation, vm.ProofComputation);
        string report = ws.PathTo("Report.txt");
        string nowhere = Path.Combine(ws.Dir, "no such folder", "Report.txt");
        using LogCapture capture = LogCapture.Start();

        vm.SaveReport(report);
        vm.SaveReport(nowhere);

        Assert.Contains(LogText.Wrote(report), capture.Messages);
        LogCapture.Captured failed = Assert.Single(capture.Events, e => e.Level == LogLevel.Warn);
        Assert.Equal("the report could not be written to " + nowhere + "; the window shows the message", failed.Message);
        Assert.Equal(vm.SaveError, MainViewModel.Describe(failed.Exception!));
    }

    [Fact]
    public void R171_ASettingsFileThatCannotBeReadOrWrittenIsLoggedAndTheDefaultsAreUsed()
    {
        using TempWorkspace ws = new();
        string junk = ws.WriteFile("settings.json", Encoding.UTF8.GetBytes("{ not json"));
        // A settings file whose folder is a file: it cannot be written.
        string blocked = Path.Combine(junk, "settings.json");
        using LogCapture capture = LogCapture.Start();

        WindowSettings read = WindowSettings.Read(junk);
        WindowSettings bound = WindowSettings.Read(blocked);
        bound.IccProfilePath = "paper.icc";
        bound.Write();

        Assert.Null(read.IccProfilePath);
        LogCapture.Captured[] warnings = capture.Events.Where(e => e.Level == LogLevel.Warn).ToArray();
        Assert.Equal(2, warnings.Length);
        Assert.Equal("the settings file " + junk + " cannot be read; nothing is remembered", warnings[0].Message);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(warnings[0].Exception);
        Assert.Equal("the settings could not be written to " + blocked + "; the choice holds for this run only", warnings[1].Message);
        Assert.IsAssignableFrom<IOException>(warnings[1].Exception);
    }

    /// <summary>
    /// An exception that leaves a run, whose task the window's buttons discard, is written with its
    /// stack where it leaves, through the run's exception filter, goes on as before, and the hook of
    /// the task nobody awaited names it instead of writing it again (R-171, after LightsCheck's ADR
    /// 0029). A handler of the view-model's own property change throws, outside the run's catch.
    /// </summary>
    [Fact]
    public async Task R171_AnExceptionTheWindowDoesNotHandleIsWrittenOnceAndTheHookNamesIt()
    {
        using TempWorkspace ws = new();
        string tif = SkyTiff(ws);
        MainViewModel vm = new();
        InvalidOperationException bug = new("a binding fell over");
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsBusy) && vm.IsBusy) throw bug;
        };
        using LogCapture capture = LogCapture.Start();

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => vm.OpenAsync(tif));
        LogSetup.Unobserved(new AggregateException(thrown));

        Assert.Same(bug, thrown);
        LogCapture.Captured[] errors = capture.Events.Where(e => e.Level == LogLevel.Error).ToArray();
        Assert.Equal(2, errors.Length);
        Assert.Equal("Window", errors[0].Logger);
        Assert.Equal("a run failed with an exception the window does not handle; it shows no result", errors[0].Message);
        Assert.Same(bug, errors[0].Exception);
        Assert.Equal("a task failed and nobody awaited it: the exception logged above", errors[1].Message);
        Assert.Null(errors[1].Exception);
    }

    /// <summary>
    /// The window's own lines are English whatever the language, the same in both (R-171): a run, a
    /// report that cannot be written and a field refused, the refusal's text, which is the user's,
    /// set apart.
    /// </summary>
    [Fact]
    public async Task R171_TheWindowsLinesAreTheSameInBothLanguages()
    {
        using TempWorkspace ws = new();
        string tif = SkyTiff(ws);
        string nowhere = Path.Combine(ws.Dir, "no such folder", "Report.txt");

        string[] english = await WindowMessages(tif, nowhere, UiLanguage.English);
        string[] romanian = await WindowMessages(tif, nowhere, UiLanguage.Romanian);

        Assert.Contains("refused: #", english);
        Assert.Contains("the report could not be written to " + nowhere + "; the window shows the message", english);
        Assert.Equal(english, romanian);
        Assert.Empty(RomanianWords.In(string.Join("\n", english)));
    }

    /// <summary>
    /// A link the About window cannot open is a Debug line with its exception, by its text as
    /// written, and the window stays: a relative link, whose AbsoluteUri throws, is the case
    /// (ADR 0069, after DarksCheck's review).
    /// </summary>
    [Fact]
    public void R171_ALinkThatCannotBeOpenedIsADebugLineAndTheWindowStays()
    {
        LogCapture.Captured[] lines = [];
        bool handled = false;
        _sta.Invoke(() =>
        {
            using LogCapture capture = LogCapture.Start();
            AboutWindow about = new();
            RequestNavigateEventArgs args = new(new Uri("THIRD-PARTY-NOTICES.md", UriKind.Relative), null) { RoutedEvent = Hyperlink.RequestNavigateEvent };
            about.RaiseEvent(args);
            handled = args.Handled;
            lines = capture.Events.ToArray();
            about.Close();
        });

        Assert.True(handled);
        LogCapture.Captured line = Assert.Single(lines);
        Assert.Equal(LogLevel.Debug, line.Level);
        Assert.Equal("Window", line.Logger);
        Assert.Equal("the link THIRD-PARTY-NOTICES.md could not be opened in a browser", line.Message);
        Assert.IsType<InvalidOperationException>(line.Exception);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>A 16-bit TIFF of the clean sky scene, the golden tests' way of writing one.</summary>
    static string SkyTiff(TempWorkspace ws)
    {
        Raster r = Scenes.CleanSky();
        return ws.WriteFile("sky.tif", TiffTestData.Build16Rgb(r.W, r.H, (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V2("sRGB test")));
    }

    /// <summary>The messages of the window's main logger in a capture.</summary>
    static string[] WindowLines(LogCapture capture)
        => capture.Events.Where(e => e.Logger == "Window").Select(e => e.Message).ToArray();

    /// <summary>
    /// The messages of the window's loggers in one language for an open of <paramref name="path"/>,
    /// a save of the report to <paramref name="nowhere"/>, which fails, and a run a field refuses;
    /// each elapsed time written as <c>#</c>, the refusal's text, the user's, as <c>#</c> too.
    /// </summary>
    static async Task<string[]> WindowMessages(string path, string nowhere, UiLanguage language)
    {
        using IDisposable scope = Tr.Use(language);
        using LogCapture capture = LogCapture.Start();
        MainViewModel vm = new();
        await vm.OpenAsync(path);
        await Task.WhenAll(vm.CurvesComputation, vm.ProofComputation);
        vm.SaveReport(nowhere);
        vm.Parameters.Distance = "1,5";
        await vm.RunAsync();
        return capture.Events.Where(e => e.Logger.StartsWith("Window", StringComparison.Ordinal))
            .Select(e => e.Message.StartsWith("refused: ", StringComparison.Ordinal) ? "refused: #" : LogFiles.ElapsedTime().Replace(e.Message, "#"))
            .ToArray();
    }
}
