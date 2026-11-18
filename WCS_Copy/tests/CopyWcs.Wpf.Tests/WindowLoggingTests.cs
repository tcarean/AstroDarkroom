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
using System.Windows.Documents;
using System.Windows.Navigation;
using CopyWcs.Tests;
using NLog;
using NLog.Targets;
using Xunit.Abstractions;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The window's side of the diagnostic log (R-043, R-045, ADR 0009): its start line, a header whose
/// facts could not be read, a copy that writes what the console's copy writes, a link the About
/// window cannot open, and two windows into one file. Without a window, like
/// <see cref="MainViewModelTests"/>, except the About window's link, which needs the UI thread.
/// </summary>
[Collection("window")]
public class WindowLoggingTests
{
    private readonly StaFixture _sta;
    private readonly ITestOutputHelper _output;

    public WindowLoggingTests(StaFixture sta, ITestOutputHelper output)
    {
        _sta = sta;
        _output = output;
    }

    /// <summary>
    /// A link the About window cannot open is a Debug line with the reason, and the window stays:
    /// a relative link has no absolute address to start, and the line must not ask for one again
    /// (the second review of 2026-10-30: it did, and the exception escaped the handler).
    /// </summary>
    [Fact]
    public void R043_ALinkThatCannotBeOpenedIsADebugLineAndTheWindowStays()
        => _sta.Invoke(() =>
        {
            AboutWindow about = new();
            RequestNavigateEventArgs args = new(new Uri("THIRD-PARTY-NOTICES.md", UriKind.Relative), null)
            {
                RoutedEvent = Hyperlink.RequestNavigateEvent,
            };
            using LogCapture capture = LogCapture.Start();

            about.RaiseEvent(args);

            Assert.True(args.Handled);
            LogCapture.Captured line = Assert.Single(capture.Events, e => e.Logger == "Window");
            Assert.Equal(LogLevel.Debug, line.Level);
            Assert.Equal("the link THIRD-PARTY-NOTICES.md could not be opened", line.Message);
            Assert.IsType<InvalidOperationException>(line.Exception);
        });

    /// <summary>
    /// Each press of F1 or Help is an Info line with the address it opens (R-039), whether the
    /// browser starts or not; here the window's opener is replaced, so none does.
    /// </summary>
    [Fact]
    public void R043_HelpWritesTheAddressItOpens()
        => _sta.Invoke(() =>
        {
            MainWindow w = new() { OpenAddress = _ => { } };
            using LogCapture capture = LogCapture.Start();

            Assert.True(HelpTests.PressF1(w));

            LogCapture.Captured line = Assert.Single(capture.Events, e => e.Logger == "Window");
            Assert.Equal(LogLevel.Info, line.Level);
            Assert.Equal("Help opened: https://github.com/tcarean/AstroDarkroom/wiki/CopyWcs#the-window", line.Message);
        });

    /// <summary>
    /// A click of About is an Info line, whether the window opens or not; here the window is
    /// replaced, so no modal window blocks the test (R-035).
    /// </summary>
    [Fact]
    public void R043_AboutWritesItsLine()
        => _sta.Invoke(() =>
        {
            MainWindow w = new() { OpenAbout = _ => { } };
            using LogCapture capture = LogCapture.Start();

            HelpTests.Click(w.AboutButton);

            LogCapture.Captured line = Assert.Single(capture.Events, e => e.Logger == "Window");
            Assert.Equal(LogLevel.Info, line.Level);
            Assert.Equal("About opened", line.Message);
        });

    /// <summary>
    /// A close held during a run is two Info lines of the window, around the run's own done line,
    /// and a second close adds none (R-043, R-050): the log says the run ended before the window
    /// closed, where it used to end at the process's exit line (CR-002).
    /// </summary>
    [Fact]
    public Task R043_ACloseHeldDuringARunIsLoggedAroundTheRunsDoneLine()
        => _sta.InvokeAsync(async () =>
        {
            using WindowFixture f = new();
            using ManualResetEventSlim release = new();
            MainViewModel vm = new() { Source = f.Source, Dest = f.Dest, RunCore = WindowCloseTests.HeldRun(release) };
            MainWindow w = new(vm);
            using LogCapture capture = LogCapture.Start();

            Task preview = vm.Preview();
            w.Close();
            w.Close();
            release.Set();
            await preview;

            LogCapture.Captured[] lines = capture.Events
                .Where(e => e.Logger == "Window" || e.Message.StartsWith("copy done", StringComparison.Ordinal))
                .ToArray();
            Assert.Equal(3, lines.Length);
            Assert.Equal("close asked while a run is in flight: the window closes when it ends", lines[0].Message);
            Assert.StartsWith("copy done previewed 15 keys ", lines[1].Message, StringComparison.Ordinal);
            Assert.Equal("the run ended: the window closes", lines[2].Message);
            Assert.All([lines[0], lines[2]], l => Assert.Equal(LogLevel.Info, l.Level));
        });

    /// <summary>
    /// A window closed at the end of a held run starts no look after it has closed (R-050, R-054):
    /// the run's look again at its end would write its read lines after the window's last line,
    /// and in the application race the process's exit line (the review of the branch on
    /// 2026-11-18).
    /// </summary>
    [Fact]
    public Task R054_AClosedWindowLooksAtNoFile()
        => _sta.InvokeAsync(async () =>
        {
            using WindowFixture f = new();
            using ManualResetEventSlim release = new();
            MainViewModel vm = new() { Source = f.Source, Dest = f.Dest, RunCore = WindowCloseTests.HeldRun(release) };
            await vm.FactsRead;
            MainWindow w = new(vm);
            using LogCapture capture = LogCapture.Start();

            Task preview = vm.Preview();
            w.Close();
            release.Set();
            await preview;
            await vm.FactsRead;

            List<string> lines = capture.Events.Where(e => e.Logger is "Window" or "FitsHeader").Select(e => e.Message).ToList();
            int closed = lines.IndexOf("the run ended: the window closes");
            Assert.True(closed > 0, "no close line");
            Assert.DoesNotContain(lines[(closed + 1)..], m => m.StartsWith("read ", StringComparison.Ordinal));
        });

    [Fact]
    public void R043_TheStartLineNamesTheLanguageAndTheFilesFromTheCommandLine()
    {
        using WindowFixture f = new();

        Assert.Equal("language=ro source from the command line: " + f.Source + " dest: " + f.Dest,
                     App.StartLine(App.Parse(["--lang", "ro", f.Source, f.Dest])));
        Assert.Equal("language=en source from the command line: none dest: none", App.StartLine(App.Parse([])));
    }

    /// <summary>The facts' quiet catch is a Debug line with the reader's exception, written by the look off the window's thread (R-052); the box stays empty.</summary>
    [Fact]
    public async Task R043_AFileWhoseFactsCannotBeReadIsADebugLineWithTheException()
    {
        using WindowFixture f = new();
        string notFits = f.FilePath("notes.fit");
        File.WriteAllText(notFits, "not a FITS file, only some text");
        using var capture = LogCapture.Start();

        MainViewModel vm = new() { Source = notFits };
        await vm.FactsRead;

        Assert.Equal("", vm.SourceFacts);
        LogCapture.Captured line = Assert.Single(capture.Events, e => e.Logger == "Window");
        Assert.Equal(LogLevel.Debug, line.Level);
        Assert.IsType<InvalidDataException>(line.Exception);
        Assert.Equal("no facts under the source box: the header of " + notFits + " could not be read", line.Message);
    }

    /// <summary>The window's copy goes through the core's run, so its lines are the console's for the same request.</summary>
    [Fact]
    public async Task R043_ACopyFromTheWindowWritesTheLinesOfTheConsolesCopy()
    {
        using WindowFixture window = new();
        using WindowFixture console = new();
        MainViewModel vm = new() { Source = window.Source, Dest = window.Dest };

        IReadOnlyList<string> fromWindow;
        using (var capture = LogCapture.Start())
        {
            await vm.Copy();
            fromWindow = OfTheRun(capture, window.Dir);
        }

        IReadOnlyList<string> fromConsole;
        using (var capture = LogCapture.Start())
        {
            Assert.Equal(0, CopyWcsCli.Run([console.Source, console.Dest], new StringWriter(), new StringWriter()));
            fromConsole = OfTheRun(capture, console.Dir);
        }

        Assert.NotEmpty(fromWindow);
        Assert.Equal(fromConsole, fromWindow);
    }

    /// <summary>
    /// Two windows writing one file at full speed keep every line, through the build of
    /// <c>NLog.Targets.AtomicFile</c> the window ships, its <c>net8.0-windows7.0</c> one, which
    /// is not the console's (R-045; the review of 2026-10-30 found it untested).
    /// </summary>
    [Fact]
    public void R045_TwoWindowsWritingAtFullSpeedKeepEveryLine()
    {
        Assert.Equal("Windows7.0", typeof(AtomicFileTarget).Assembly.GetCustomAttribute<TargetPlatformAttribute>()?.PlatformName);
        using TempWorkspace ws = new();
        string folder = ws.FilePath("log");
        const int Lines = 4000;   // about 1.6 MiB together: the writers cross the roll

        LogFiles.TwoWriters result = LogFiles.WriteTwoAtFullSpeed(folder, "CopyWcs.Wpf", Lines);

        foreach (string f in result.Files)
        {
            _output.WriteLine($"{Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        }

        _output.WriteLine($"whole lines: {result.FromA.Count} from A, {result.FromB.Count} from B; broken: {result.Broken}");
        LogFiles.AssertEveryLineKept(folder, result, Lines);
    }

    /// <summary>A run's messages from <c>copy start</c> to <c>copy done</c>, the folder, the times and the random part of the write's temporary file (R-012) made neutral.</summary>
    private static List<string> OfTheRun(LogCapture capture, string dir)
    {
        List<string> lines = capture.Events.Where(e => e.Logger is "WcsCopy" or "FitsHeader").Select(e => e.Message).ToList();
        int start = lines.FindIndex(m => m.StartsWith("copy start ", StringComparison.Ordinal));
        int done = lines.FindIndex(m => m.StartsWith("copy done ", StringComparison.Ordinal));
        Assert.True(start >= 0 && done > start, "no run between copy start and copy done");
        return lines.GetRange(start, done - start + 1)
                    .Select(m => Regex.Replace(m.Replace(dir, "<dir>", StringComparison.Ordinal), @"\d+ ms\b", "# ms"))
                    .Select(m => Regex.Replace(m, @"\.copywcs-[0-9a-f]{8}\.tmp", ".copywcs-#.tmp"))
                    .ToList();
    }
}
