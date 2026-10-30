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

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LightsCheck.Cli;
using LightsCheck.Core;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit.Abstractions;

namespace LightsCheck.Tests;

/// <summary>
/// The diagnostic log (R-070 to R-073, ADR 0029): where the files go and how they roll, what a
/// run writes, the level, two writers on one file. The file tests run the applications'
/// configuration (<see cref="LogSetup.Build"/>) in a private <see cref="LogFactory"/>, which
/// stands for one process, so they touch neither the test assembly's capture
/// (<see cref="LogCapture"/>) nor the tests beside them. FlatCheck's tests of the same log are the
/// model (its LoggingTests).
/// </summary>
public partial class LoggingTests
{
    private const string Exe = "lightscheck";

    /// <summary>The five files 8 MiB of lines leave, measured in FlatCheck with the same NLog and the same settings (R-070).</summary>
    private static readonly string[] RolledFiles = ["lightscheck_03.log", "lightscheck_04.log", "lightscheck_05.log", "lightscheck_06.log", "lightscheck_07.log"];

    /// <summary>The only two files that start and close the log: the two entry points.</summary>
    private static readonly string[] EntryPoints = ["src/LightsCheck.Cli/Program.cs", "src/LightsCheck.Wpf/App.xaml.cs"];

    /// <summary>The only two files that set NLog's configuration: the applications' and the tests'.</summary>
    private static readonly string[] Configurers = ["src/LightsCheck.Core/Logging/LogSetup.cs", "tests/LightsCheck.Tests/LogCapture.cs"];

    private readonly ITestOutputHelper _output;

    /// <summary>The runner's output, for the measured file lists and the captured lines.</summary>
    public LoggingTests(ITestOutputHelper output) => _output = output;

    // ---------------------------------------------------------------- R-070: the files

    [Fact]
    public void R070_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName()
    {
        using TempWorkspace ws = new();
        string fallback = Path.Combine(ws.Dir, "fallback");

        string folder = LogSetup.ResolveFolder(ws.Dir, fallback, Exe, out bool fellBack);

        Assert.False(fellBack);
        Assert.Equal(Path.Combine(ws.Dir, "log"), folder);
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));   // the probe is gone
        Assert.False(Directory.Exists(fallback));

        using (LogFactory factory = LogFiles.Factory(LogSetup.Build(Exe, folder, LogLevel.Debug)))
        {
            factory.GetLogger("Test").Info("hello");
            factory.Shutdown();
        }

        string file = Path.Combine(folder, "lightscheck.log");
        Assert.Equal(new[] { file }, Directory.GetFiles(folder));
        byte[] bytes = File.ReadAllBytes(file);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no BOM");
        string line = Encoding.UTF8.GetString(bytes).TrimEnd('\r', '\n');
        Assert.Matches(LogFiles.LinePrefix(), line);
        Assert.EndsWith(" INFO  [" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ":"
            + Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture) + "] Test hello", line, StringComparison.Ordinal);
    }

    [Fact]
    public void R070_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback()
    {
        using TempWorkspace ws = new();
        // A FILE named log where the folder should be: the folder cannot be created.
        ws.WriteFile("log", [1]);
        string fallbackRoot = Path.Combine(ws.Dir, "LocalAppData");

        string folder = LogSetup.ResolveFolder(ws.Dir, fallbackRoot, Exe, out bool fellBack);

        Assert.True(fellBack);
        Assert.Equal(Path.Combine(fallbackRoot, "LightsCheck", "log"), folder);
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
        Assert.Contains("(fallback", LogSetup.StartupLine(Exe, [], folder, true, LogLevel.Debug), StringComparison.Ordinal);
    }

    [Fact]
    public void R070_TheLogRollsAt1MiBAndKeepsAtMost5Files()
    {
        using TempWorkspace ws = new();
        string folder = Path.Combine(ws.Dir, "log");

        WriteThrough(folder, Exe, 8 * LogSetup.FileSizeLimit, "console");

        string[] files = LogFiles.FilesOf(folder, Exe);
        foreach (string f in files)
        {
            _output.WriteLine(Path.GetFileName(f) + " " + new FileInfo(f).Length.ToString(CultureInfo.InvariantCulture) + " bytes");
        }

        Assert.Equal(Directory.GetFiles(folder).Length, files.Length);   // nothing else is left
        Assert.Equal(LogSetup.FilesPerExecutable, files.Length);
        Assert.All(files, f => Assert.InRange(new FileInfo(f).Length, 1, LogSetup.FileSizeLimit + 4200));
        // The newest file is the one with the highest number, and it holds the last line.
        Assert.Equal(RolledFiles, files.Select(f => Path.GetFileName(f)));
        Assert.EndsWith("console last", File.ReadAllLines(files[^1])[^1], StringComparison.Ordinal);

        // The next process goes on in the newest file: the same five names, the newest grown,
        // nothing started and nothing deleted.
        long newestBefore = new FileInfo(files[^1]).Length;
        WriteThrough(folder, Exe, 1000, "next");
        string[] after = LogFiles.FilesOf(folder, Exe);
        foreach (string f in after)
        {
            _output.WriteLine("after a second process: " + Path.GetFileName(f) + " " + new FileInfo(f).Length.ToString(CultureInfo.InvariantCulture) + " bytes");
        }

        Assert.Equal(files.Select(f => Path.GetFileName(f)), after.Select(f => Path.GetFileName(f)));
        Assert.True(new FileInfo(after[^1]).Length > newestBefore, "the newest file did not grow");
        Assert.EndsWith("next last", File.ReadAllLines(after[^1])[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void R070_OneExecutablesRollingLeavesTheOthersFilesAlone()
    {
        using TempWorkspace ws = new();
        string folder = Path.Combine(ws.Dir, "log");
        // The window's log first, rolled once, then the console's, rolled many times, in the
        // same folder: LightsCheck.Wpf begins with the same letters as lightscheck, in another case.
        WriteThrough(folder, "LightsCheck.Wpf", 3 * LogSetup.FileSizeLimit / 2, "window");
        string[] window = LogFiles.FilesOf(folder, "LightsCheck.Wpf");
        Assert.Equal(2, window.Length);

        WriteThrough(folder, Exe, 8 * LogSetup.FileSizeLimit, "console");

        Assert.Equal(window, LogFiles.FilesOf(folder, "LightsCheck.Wpf"));
        Assert.EndsWith("window last", File.ReadAllLines(window[^1])[^1], StringComparison.Ordinal);
        Assert.Equal(LogSetup.FilesPerExecutable, LogFiles.FilesOf(folder, Exe).Length);
    }

    /// <summary>
    /// The folder's probe also opens the file NLog will append to: a read-only newest file sends
    /// the log to the fallback, where it would otherwise pass the probe and lose every line in
    /// silence (FlatCheck's review of 2026-10-27, its ADR 0072).
    /// </summary>
    [Fact]
    public void R070_AReadOnlyNewestLogFileSendsTheLogToTheFallback()
    {
        using TempWorkspace ws = new();
        string beside = Path.Combine(ws.Dir, "exe");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        Directory.CreateDirectory(folder);
        string newest = Path.Combine(folder, "lightscheck_03.log");
        File.WriteAllText(Path.Combine(folder, "lightscheck.log"), "old\n");
        File.WriteAllText(newest, "newest\n");
        File.SetAttributes(newest, FileAttributes.ReadOnly);
        try
        {
            string chosen = LogSetup.ResolveFolder(beside, ws.PathTo("fallback"), Exe, out bool fellBack);

            Assert.True(fellBack);
            Assert.Equal(Path.Combine(ws.PathTo("fallback"), LogSetup.FallbackFolder, LogSetup.FolderName), chosen);
        }
        finally
        {
            File.SetAttributes(newest, FileAttributes.Normal);
        }
    }

    /// <summary>A file another process of the same executable holds open (R-073) passes the probe: the second window logs beside the first.</summary>
    [Fact]
    public void R070_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe()
    {
        using TempWorkspace ws = new();
        string beside = Path.Combine(ws.Dir, "exe");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        using LogFactory other = LogFiles.Factory(LogSetup.Build(Exe, folder, LogLevel.Debug));
        other.GetLogger("Other").Info("held open");

        string chosen = LogSetup.ResolveFolder(beside, ws.PathTo("fallback"), Exe, out bool fellBack);

        Assert.False(fellBack);
        Assert.Equal(folder, chosen);
        other.Shutdown();
    }

    [Theory]
    // Windows paths with forward slashes, which Windows accepts and Unix can split too: the test
    // project is portable, and a backslash is not a separator there.
    [InlineData("C:/Tools/lightscheck.exe", "lightscheck", "lightscheck")]
    [InlineData("C:/Tools/lightscheck-1.0.exe", "lightscheck", "lightscheck-1.0")]
    [InlineData("C:/Program Files/dotnet/dotnet.exe", "lightscheck", "lightscheck")]
    [InlineData("/usr/share/dotnet/dotnet", "lightscheck", "lightscheck")]
    [InlineData(null, "LightsCheck.Wpf", "LightsCheck.Wpf")]
    public void R070_TheFilesBeginWithTheExecutablesName(string? processPath, string entry, string expected)
    {
        Assert.Equal(expected, LogSetup.ExecutableName(processPath, entry));
    }

    // ---------------------------------------------------------------- R-071: what is written

    [Fact]
    public void R071_TheFirstLineCarriesTheFacts()
    {
        string line = LogSetup.StartupLine(Exe, [@"D:\astro\veil night", "--lang", "ro"], @"D:\tools\log", false, LogLevel.Debug);

        Assert.StartsWith("lightscheck " + VersionInfo.Value + " starting; exe=", line, StringComparison.Ordinal);
        Assert.Contains(@"; args=[""D:\astro\veil night"", ""--lang"", ""ro""]; cwd=", line, StringComparison.Ordinal);
        Assert.Contains(@"; log=D:\tools\log; level=Debug; os=", line, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback", line, StringComparison.Ordinal);
        foreach (string part in new[] { "; cpus=", "; memory=", " GiB; runtime=", "; culture=", "; tz=", " (UTC" })
        {
            Assert.Contains(part, line, StringComparison.Ordinal);
        }

        Assert.EndsWith("; pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture), line, StringComparison.Ordinal);
    }

    /// <summary>
    /// The real console starts and closes the log itself (R-071), which only its Main does and no
    /// in-process test reaches: its first line names it, its version and its arguments, its last
    /// line the exit code. Its lines are found by its process id, after its start, in the log beside
    /// the copy of the executable in the test build folder, which other tests' processes share.
    /// </summary>
    [Fact]
    public void R071_TheRealConsoleWritesItsFirstAndItsLastLine()
    {
        (int pid, int code, DateTime started) = RunTheConsole(null, "--help", "--lang", "en");

        List<string> lines = ConsoleLinesOf(pid, started);
        Assert.Equal(0, code);
        Assert.True(lines.Count >= 3, "the console wrote " + lines.Count.ToString(CultureInfo.InvariantCulture) + " lines");
        Assert.Contains(" INFO  [" + pid.ToString(CultureInfo.InvariantCulture) + ":", lines[0], StringComparison.Ordinal);
        Assert.Contains("] Process lightscheck " + VersionInfo.Value + " starting; exe=" + ConsoleExe + "; args=[\"--help\", \"--lang\", \"en\"]; ",
            lines[0], StringComparison.Ordinal);
        Assert.Contains("; level=Debug; ", lines[0], StringComparison.Ordinal);
        Assert.Contains(lines, l => l.EndsWith("] Cli mode=Help", StringComparison.Ordinal));
        Assert.Matches(@"\] Process exit 0 after \d+\.\d s$", lines[^1]);
    }

    [Fact]
    public void R071_ASeriesRunLogsTheSessionEveryFrameTheFiguresAndTheTimings()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2);
        using LogCapture capture = LogCapture.Start();

        (int code, _, _) = Cli.Run(s.Root, "--threads", "2", "--lang", "en");

        Assert.Equal(0, code);
        string text = capture.Text;
        _output.WriteLine(text);
        Assert.Contains("Cli mode=Series input=" + s.Root + " lang=en", text, StringComparison.Ordinal);
        Assert.Contains("SeriesRun series start " + s.Root + " pixel=none threads=2 east=Right north=Up", text, StringComparison.Ordinal);
        Assert.Contains("SessionFolder session root=" + s.Root + " lights=" + s.LightsDir + " (8 frames, .fit) biases=", text, StringComparison.Ordinal);
        // Eight lights and two biases, each one line.
        Assert.Equal(10, capture.Events.Count(e => e.Logger == "ImageLoader" && e.Message.StartsWith("loaded ", StringComparison.Ordinal)));
        Assert.Matches(@"ImageLoader loaded .+DSC_0001\.fit \(\d+\.\d MiB\) reader=fits 640x480 planes=1 bitpix=16 cfa=RGGB \(BAYERPAT\) scale=none camera=NIKON D3300 focal=135 ", text);
        Assert.Contains("ImageLoader header of ", text, StringComparison.Ordinal);
        for (int i = 1; i <= 8; i++)
        {
            Assert.Matches(@"SeriesRun frame " + i.ToString(CultureInfo.InvariantCulture) + @"/8 DSC_000" + i.ToString(CultureInfo.InvariantCulture)
                + @"\.fit detected=\d+ fwhm=\S+ elongation=\S+ in \d+ ms", text);
            Assert.Matches(@"FrameRun frame " + i.ToString(CultureInfo.InvariantCulture) + @" DSC_000" + i.ToString(CultureInfo.InvariantCulture)
                + @"\.fit: 640x480 scale=65535/0 channels=\[", text);
        }

        Assert.Contains("SeriesRun registered frame 2: ok matches=", text, StringComparison.Ordinal);
        Assert.Contains("SeriesRun classified frame 3: Bad flags=[Trailed value ", text, StringComparison.Ordinal);
        foreach (string figures in new[] { "cadence: start=", "dither: registered=", "pattern: status=", "pixel size 3.92 um source=File focal=135 mm",
            "read noise: [", "wcs: none", "series: classes good=" })
        {
            Assert.Contains("SeriesRun " + figures, text, StringComparison.Ordinal);
        }

        Assert.Matches(@"SeriesRun series done frames=8 measured=8 errors=0 good=\d+ marginal=\d+ bad=\d+ unknown=\d+ walking=\w+ polar=\w+ in \d+ ms "
            + @"\(open \d+ ms, measure \d+ ms, register \d+ ms, dither \d+ ms, pattern \d+ ms, noise \d+ ms, analyze \d+ ms\)", text);
        Assert.All(capture.Events, e => Assert.Matches(LogFiles.LinePrefix(), e.Line));
    }

    [Fact]
    public void R071_AnInventoryAndAFrameRunHaveTheirStartAndDoneLines()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using LogCapture capture = LogCapture.Start();

        Assert.Equal(0, Cli.Run(s.Root, "--inventory").Code);
        Assert.Equal(0, Cli.Run(s.Root, "--frame", "1").Code);

        string text = capture.Text;
        Assert.Contains("Cli mode=Inventory input=" + s.Root, text, StringComparison.Ordinal);
        Assert.Contains("InventoryRun inventory start " + s.Root, text, StringComparison.Ordinal);
        Assert.Equal(8, capture.Events.Count(e => e.Logger == "ImageLoader" && e.Message.StartsWith("read the header of ", StringComparison.Ordinal)));
        Assert.Contains("InventoryRun cadence: start=", text, StringComparison.Ordinal);
        Assert.Matches(@"InventoryRun inventory done lights=8 errors=0 in \d+ ms \(open \d+ ms, read \d+ ms, cadence \d+ ms\)", text);
        Assert.Contains("Cli mode=Frame input=" + s.Root, text, StringComparison.Ordinal);
        Assert.Contains("FrameRun frame start " + s.Root + " index=1", text, StringComparison.Ordinal);
        Assert.Matches(@"FrameRun frame done detected=\d+ fwhm=\S+ elongation=\S+ in \d+ ms \(open \d+ ms, load \d+ ms, measure \d+ ms, crop \d+ ms\)", text);
    }

    /// <summary>
    /// A frame the series cannot measure keeps its message in its row, as R-003 and the goldens
    /// say, and the log gets the exception whole, which the row cannot hold.
    /// </summary>
    [Fact]
    public void R071_AFrameThatCannotBeMeasuredIsAWarnLineWithItsException_AndTheRowKeepsItsMessage()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        string junk = Path.Combine(s.LightsDir, "DSC_0003.fit");
        File.WriteAllText(junk, "not a FITS file, only some text");
        using LogCapture capture = LogCapture.Start();

        (int code, string stdout, _) = Cli.Run(s.Root, "--threads", "2", "--lang", "en");

        Assert.Equal(1, code);
        Assert.Contains("could not be read: FITS: the file does not begin with SIMPLE.", stdout, StringComparison.Ordinal);
        LogCapture.Captured failed = Assert.Single(capture.Events, e => e.Logger == "SeriesRun" && e.Level == LogLevel.Warn);
        Assert.IsType<InvalidDataException>(failed.Exception);
        Assert.StartsWith("frame 3/8 " + junk + " could not be measured after ", failed.Message, StringComparison.Ordinal);
        Assert.EndsWith("; its metadata could not be read either; its row carries the error", failed.Message, StringComparison.Ordinal);
        Assert.Contains("System.IO.InvalidDataException: FITS: the file does not begin with SIMPLE.", failed.Line, StringComparison.Ordinal);
        Assert.Contains("   at LightsCheck.Core.FitsReader.", failed.Line, StringComparison.Ordinal);
        // The metadata read that follows fails the same way: said in that one line, not in a
        // second stack (the review of 2026-10-30).
        Assert.DoesNotContain(capture.Events, e => e.Logger == "FrameInfo");
        Assert.Matches(@"SeriesRun series done frames=8 measured=7 errors=1 ", capture.Text);
    }

    [Fact]
    public void R071_AFailedRunLogsTheExceptionWithItsStackAndStderrKeepsOneLine()
    {
        using TempWorkspace ws = new();
        string missing = Path.Combine(ws.Dir, "missing");
        using LogCapture capture = LogCapture.Start();

        (int code, _, string stderr) = Cli.Run(missing, "--lang", "en");

        Assert.Equal(1, code);
        Assert.Equal("ERROR: cannot find the folder " + missing + "\n", stderr);
        LogCapture.Captured failed = Assert.Single(capture.Events, e => e.Level == LogLevel.Error);
        Assert.Equal("Cli", failed.Logger);
        Assert.Equal("run failed: mode=Series", failed.Message);
        Assert.IsType<DirectoryNotFoundException>(failed.Exception);
        Assert.Contains("System.IO.DirectoryNotFoundException: cannot find the folder " + missing, failed.Line, StringComparison.Ordinal);
        Assert.Contains("   at LightsCheck.Core.SessionFolder.Open(", failed.Line, StringComparison.Ordinal);
        // The run's inputs are in its start line, and the line its clock leaves says it threw.
        Assert.Contains(capture.Events, e => e.Logger == "SeriesRun" && e.Message.StartsWith("series start " + missing + " pixel=", StringComparison.Ordinal));
        Assert.Contains(capture.Events, e => e.Logger == "SeriesRun" && ThrownRun().IsMatch(e.Message));
    }

    /// <summary>
    /// A folder the run refuses after its clock started (an empty <c>lights/</c>) leaves the run's
    /// start line, with its inputs, and the line its clock writes when the run threw, beside the
    /// console's Error line with the exception.
    /// </summary>
    [Fact]
    public void R071_ARefusedFolderLeavesTheRunsStartLine()
    {
        using TempWorkspace ws = new();
        Directory.CreateDirectory(Path.Combine(ws.Dir, "lights"));
        using LogCapture capture = LogCapture.Start();

        Assert.Equal(1, Cli.Run(ws.Dir, "--inventory", "--lang", "en").Code);

        Assert.Contains(capture.Events, e => e.Logger == "InventoryRun" && e.Message == "inventory start " + ws.Dir);
        Assert.Contains(capture.Events, e => e.Logger == "InventoryRun" && InventoryThrown().IsMatch(e.Message));
        LogCapture.Captured failed = Assert.Single(capture.Events, e => e.Level == LogLevel.Error);
        Assert.Equal("run failed: mode=Inventory", failed.Message);
        Assert.IsType<InvalidOperationException>(failed.Exception);
        Assert.DoesNotContain(capture.Events, e => e.Logger == "SessionFolder");
    }

    [Fact]
    public void R071_TheArgumentsTheConsoleRefusesAreLoggedAsTheUserSawThem()
    {
        using LogCapture capture = LogCapture.Start();

        Assert.Equal(1, Cli.Run("x", "--threads", "0", "--lang", "en").Code);
        Assert.Equal(1, Cli.Run("x", "--lang", "fr").Code);
        Assert.Equal(0, Cli.Run("--help", "--lang", "en").Code);

        Assert.Contains(capture.Events, e => e.Logger == "Cli" && e.Level == LogLevel.Warn
            && e.Message == "refused the arguments: --threads expects a number of threads, at least 1");
        Assert.Contains(capture.Events, e => e.Logger == "Cli" && e.Level == LogLevel.Warn && e.Message.StartsWith("refused: ", StringComparison.Ordinal)
            && e.Message.Contains("fr", StringComparison.Ordinal));
        Assert.Contains(capture.Events, e => e.Logger == "Cli" && e.Message == "mode=Help");
    }

    /// <summary>
    /// The plate-solve candidates at the session root each leave a line: a file skipped and why,
    /// the one read; and the run's own line holds the flip of the lights the analysis uses, here
    /// none, since FITS lights are stored as the solve is (ADR 0023). Before, a skipped file left
    /// nothing and the file's line said flipped where the run did not (the review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R071_ThePlateSolveCandidatesSayWhichWasReadAndWhyTheOthersWereSkipped()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        string unsolved = Path.Combine(s.Root, "a-stack.fit");
        File.WriteAllBytes(unsolved, FitsTestData.BuildFits(8, 8, (x, y) => 1));
        string solved = Path.Combine(s.Root, "result.fit");
        string[] wcs =
        [
            FitsTestData.Card("CTYPE1", "'RA---TAN'"), FitsTestData.Card("CTYPE2", "'DEC--TAN'"),
            FitsTestData.Card("PC1_1", "0.0037"), FitsTestData.Card("PC1_2", "-0.99999"),
        ];
        File.WriteAllBytes(solved, FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: wcs));
        using LogCapture capture = LogCapture.Start();

        Assert.Equal(0, Cli.Run(s.Root, "--threads", "2").Code);

        Assert.Contains(capture.Events, e => e.Logger == "SeriesRun" && e.Message == unsolved + " skipped as a WCS source: CTYPE1=none is no RA axis");
        Assert.Contains(capture.Events, e => e.Logger == "SeriesRun" && e.Message == "wcs read from " + solved);
        Assert.Contains(capture.Events, e => e.Logger == "SeriesRun" && e.Message.StartsWith("wcs: cdelt=", StringComparison.Ordinal)
            && e.Message.Contains(" framesFlipped=no raAxis=Y", StringComparison.Ordinal));
        Assert.DoesNotContain(capture.Events, e => e.Message.Contains("framesFlipped=yes", StringComparison.Ordinal));
    }

    /// <summary>
    /// libraw's Debug line for a raw without a Bayer mosaic (an X-Trans RAF, a monochrome DNG), whose
    /// CFA order is null: the line names it <c>none</c>. A line that threw there made every such
    /// frame unreadable at the default level only (the review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R071_LibrawsLineNamesNoCfaForARawWithoutABayerMosaic()
    {
        string line = RawReader.FiguresLine(@"D:\xtrans\DSCF0001.RAF", 6000, 4000, 6048, 4032, 0, 0, 12096, 9, "RGBG", null,
            16383, 1024, [1024, 1024, 1024, 1024], 1024);

        Assert.Contains(" filters 0x00000009 cdesc RGBG cfa none maximum 16383 black 1024 ", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// A crash the window's dispatcher sees and then the process sees again is written once with
    /// its stack: the second Fatal line names the first (ADR 0029, after FlatCheck's ADR 0072).
    /// </summary>
    [Fact]
    public void R071_TheSameCrashSeenByTwoHooksIsWrittenOnce()
    {
        InvalidOperationException crash = new("the window fell over");
        using LogCapture capture = LogCapture.Start();

        LogSetup.Fatal(crash, "unhandled exception on the window's thread");
        LogSetup.Fatal(crash, "unhandled exception, the process ends");
        LogSetup.Fatal(new InvalidOperationException("another"), "unhandled exception, the process ends");

        List<LogCapture.Captured> fatal = capture.Events.Where(e => e.Level == LogLevel.Fatal && e.Logger == "Process").ToList();
        Assert.Equal(3, fatal.Count);
        Assert.Same(crash, fatal[0].Exception);
        Assert.Null(fatal[1].Exception);
        Assert.Equal("unhandled exception, the process ends: the exception logged above", fatal[1].Message);
        Assert.NotNull(fatal[2].Exception);
    }

    /// <summary>
    /// A task's exception nobody awaited, which a line already wrote with its stack (the window's
    /// unhandled one), is named by the hook, not written again; any other is written whole (the
    /// review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R071_ATaskExceptionAlreadyWrittenIsNamedNotWrittenAgain()
    {
        InvalidOperationException written = new("the window's bug");
        LogSetup.Wrote(written);
        using LogCapture capture = LogCapture.Start();

        LogSetup.Unobserved(new AggregateException(written));
        LogSetup.Unobserved(new AggregateException(new InvalidOperationException("another")));

        List<LogCapture.Captured> errors = capture.Events.Where(e => e.Logger == "Process" && e.Level == LogLevel.Error).ToList();
        Assert.Equal(2, errors.Count);
        Assert.Equal("a task failed and nobody awaited it: the exception logged above", errors[0].Message);
        Assert.Null(errors[0].Exception);
        Assert.Equal("a task failed and nobody awaited it", errors[1].Message);
        Assert.IsType<AggregateException>(errors[1].Exception);
    }

    /// <summary>
    /// The log's own words are English whatever <c>--lang</c> says (R-071): the same run in both
    /// languages writes the same messages, the times and the order of the lines the frames'
    /// threads write aside. The refused folder's message is the user's, so it is in the exception,
    /// which these messages leave out.
    /// </summary>
    /// <param name="scenario">The run: a series, an inventory, one frame, a folder that is missing.</param>
    [Theory]
    [InlineData("series")]
    [InlineData("inventory")]
    [InlineData("frame")]
    [InlineData("missing")]
    public void R071_TheLogIsTheSameInBothLanguages(string scenario)
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2);
        string[] args = scenario switch
        {
            "series" => [s.Root, "--threads", "2"],
            "inventory" => [s.Root, "--inventory"],
            "frame" => [s.Root, "--frame", "3"],
            _ => [Path.Combine(s.Root, "missing")],
        };

        string[] en = MessagesOf(args, "en"), ro = MessagesOf(args, "ro");

        Assert.NotEmpty(en);
        Assert.Equal(en, ro);
    }

    [Fact]
    public void R071_TheConfigurationHasOneFileTargetAndNoConsole()
    {
        LoggingConfiguration config = LogSetup.Build(Exe, @"C:\any\log", LogLevel.Debug);

        AtomicFileTarget file = Assert.IsType<AtomicFileTarget>(Assert.Single(config.AllTargets));
        Assert.Equal(LogSetup.FileSizeLimit, file.ArchiveAboveSize);
        Assert.Equal(LogSetup.FilesPerExecutable, file.MaxArchiveFiles);
        Assert.True(file.KeepFileOpen);
        Assert.True(file.ConcurrentWrites);
        LoggingRule rule = Assert.Single(config.LoggingRules);
        Assert.True(rule.IsLoggingEnabledForLevel(LogLevel.Debug));
        Assert.False(rule.IsLoggingEnabledForLevel(LogLevel.Trace));
    }

    [Fact]
    public void R071_EveryFileWrittenIsOneLineWithItsSize()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace ws = new();
        using LogCapture capture = LogCapture.Start();

        Assert.Equal(0, Cli.Run(s.Root, "--threads", "2", "--out", ws.Dir).Code);

        List<LogCapture.Captured> wrote = capture.Events.Where(e => e.Logger == "Cli" && e.Message.StartsWith("wrote ", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, wrote.Count);
        Assert.All(wrote, e => Assert.Matches(@"^wrote .+ \(\d+\.\d KiB\)$", e.Message));
    }

    /// <summary>
    /// An export that fails part-way names the files it did write: each file's line is written as
    /// soon as the file is, where every line used to wait for the whole export (the review of
    /// 2026-10-30). A folder takes the CSV's name, so its write fails after the report's.
    /// </summary>
    [Fact]
    public void R071_AnExportThatFailsPartWayNamesTheFilesItWrote()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace ws = new();
        Directory.CreateDirectory(Path.Combine(ws.Dir, SeriesOutputs.CsvName));
        using LogCapture capture = LogCapture.Start();

        Assert.Equal(1, Cli.Run(s.Root, "--threads", "2", "--out", ws.Dir).Code);

        Assert.Contains(capture.Events, e => e.Logger == "Cli"
            && e.Message.StartsWith("wrote " + Path.Combine(ws.Dir, SeriesOutputs.ReportName) + " (", StringComparison.Ordinal));
        Assert.DoesNotContain(capture.Events, e => e.Message.Contains(SeriesOutputs.CsvName + " (", StringComparison.Ordinal));
        Assert.Single(capture.Events, e => e.Level == LogLevel.Error && e.Message == "run failed: mode=Series");
    }

    /// <summary>The log writes an elapsed time in two forms only: milliseconds, or seconds with one decimal; never whole seconds.</summary>
    [Fact]
    public void TheElapsedTimeHasTwoForms_MillisecondsAndSecondsWithOneDecimal()
    {
        Assert.Equal("12.9 s", LogText.Seconds(TimeSpan.FromMilliseconds(12900)));
        Assert.Equal("0.8 s", LogText.Seconds(TimeSpan.FromMilliseconds(800)));
        Assert.Equal("5 ms", LogText.Ms(5));
    }

    /// <summary>
    /// The console's mode is decided once, by the dispatch's precedence: help, then an error, then
    /// <c>--frame</c>, then <c>--inventory</c>, else the series; the log's <c>mode=</c> line reads
    /// the same value.
    /// </summary>
    [Fact]
    public void TheModeIsDecidedOnceByTheDispatchsPrecedence()
    {
        Assert.Equal(RunMode.Help, Options.Parse([]).Mode);
        Assert.Equal(RunMode.Help, Options.Parse(["x", "-h", "--bad"]).Mode);
        Assert.Equal(RunMode.Error, Options.Parse(["x", "--bad", "-h"]).Mode);
        Assert.Equal(RunMode.Error, Options.Parse(["x", "--inventory", "--frame", "2"]).Mode);
        Assert.Equal(RunMode.Frame, Options.Parse(["x", "--frame", "2"]).Mode);
        Assert.Equal(RunMode.Inventory, Options.Parse(["x", "--inventory"]).Mode);
        Assert.Equal(RunMode.Series, Options.Parse(["x", "--threads", "2"]).Mode);
    }

    // ---------------------------------------------------------------- R-072: the level

    [Theory]
    [InlineData(null, "Debug")]
    [InlineData("", "Debug")]
    [InlineData("nonsense", "Debug")]
    [InlineData("OFF", "Off")]
    [InlineData(" fatal ", "Fatal")]
    [InlineData("Error", "Error")]
    [InlineData("warn", "Warn")]
    [InlineData("info", "Info")]
    [InlineData("debug", "Debug")]
    [InlineData("TRACE", "Trace")]
    public void R072_TheLevelComesFromTheEnvironmentVariable(string? value, string level)
    {
        Assert.Equal(LogLevel.FromString(level), LogSetup.LevelFrom(value));
    }

    /// <summary>
    /// The real console takes its level from <c>LIGHTSCHECK_LOG</c>, the name R-072 and the README
    /// give, written out here so a renamed constant fails: <c>off</c> writes no line, <c>trace</c>
    /// says so in the first line (the review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R072_TheRealConsoleTakesItsLevelFromLIGHTSCHECK_LOG()
    {
        (int offPid, int offCode, DateTime offStarted) = RunTheConsole("off", "--help");
        (int tracePid, int traceCode, DateTime traceStarted) = RunTheConsole("trace", "--help");

        Assert.Equal(0, offCode);
        Assert.Equal(0, traceCode);
        Assert.Empty(ConsoleLinesOf(offPid, offStarted));
        List<string> trace = ConsoleLinesOf(tracePid, traceStarted);
        Assert.NotEmpty(trace);
        Assert.Contains("; level=Trace; ", trace[0], StringComparison.Ordinal);
    }

    [Fact]
    public void R072_OffCreatesNeitherTheFolderNorAFile()
    {
        using TempWorkspace ws = new();

        LogPlace off = LogSetup.Plan(Exe, Path.Combine(ws.Dir, "lightscheck.exe"), ws.Dir, Path.Combine(ws.Dir, "fallback"), "off");

        Assert.Null(off.Folder);
        Assert.Empty(Directory.GetFileSystemEntries(ws.Dir));
        Assert.Empty(LogSetup.Build(Exe, Path.Combine(ws.Dir, "log"), LogLevel.Off).AllTargets);

        LogPlace trace = LogSetup.Plan(Exe, Path.Combine(ws.Dir, "lightscheck.exe"), ws.Dir, Path.Combine(ws.Dir, "fallback"), "trace");
        Assert.Equal(LogLevel.Trace, trace.Level);
        Assert.Equal(Path.Combine(ws.Dir, "log"), trace.Folder);
    }

    [Fact]
    public void R072_TraceHoldsEveryFitsHeaderCard()
    {
        using TempWorkspace ws = new();
        string light = ws.WriteFile("light.fit", SessionFixture.Light(SessionFixture.FirstStart, 120));
        using LogCapture capture = LogCapture.Start();

        ImageLoader.Load(light);

        string[] cards = capture.Events.Where(e => e.Level == LogLevel.Trace && e.Logger == "ImageLoader").Select(e => e.Message).ToArray();
        Assert.Contains(cards, m => m == "card BITPIX  =                   16");
        Assert.Contains(cards, m => m == "card NAXIS1  =                   32");
        Assert.Contains(cards, m => m.StartsWith("card EXPTIME = ", StringComparison.Ordinal));
        Assert.DoesNotContain(capture.Events, e => e.Level == LogLevel.Debug && e.Message.Contains("card ", StringComparison.Ordinal));
        Assert.Contains(capture.Events, e => e.Level == LogLevel.Debug && e.Message.StartsWith("header of " + light + ": ", StringComparison.Ordinal)
            && e.Message.Contains(" BITPIX=16 ", StringComparison.Ordinal) && e.Message.Contains(" EXPTIME=120 ", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- R-073: two processes

    /// <summary>
    /// Two processes of one executable (two windows) write into the same file at full speed, and
    /// every line of both arrives whole, once, past the 1 MiB roll too (R-070, R-073), on the
    /// <c>net8.0</c> build of the atomic target, the console's; the window's tests run the same
    /// check on its own build (<see cref="LogFiles.TwoWritersKeepEveryLine"/>).
    /// </summary>
    [Fact]
    public void R073_TwoProcessesWritingAtFullSpeedKeepEveryLine()
    {
        using TempWorkspace ws = new();

        LogFiles.TwoWritersKeepEveryLine(Path.Combine(ws.Dir, "log"), "LightsCheck.Wpf", _output);
    }

    // ---------------------------------------------------------------- invariants of the repository

    /// <summary>
    /// Only the two entry points configure the log for a user's run, and nothing but LogSetup (for
    /// them) and LogCapture (for the tests) sets NLog's configuration at all: a library use logs
    /// into nothing and every test into the capture (ADR 0029).
    /// </summary>
    [Fact]
    public void OnlyTheTwoEntryPointsConfigureTheLog()
    {
        string[] callers = CsSources("src", "tests").Where(f => ApplyOrClose().IsMatch(File.ReadAllText(f))).Select(Rel).Order(StringComparer.Ordinal).ToArray();
        string[] configurers = CsSources("src", "tests").Where(f => SetsConfiguration().IsMatch(File.ReadAllText(f))).Select(Rel).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(EntryPoints, callers);
        Assert.Equal(Configurers, configurers);
    }

    /// <summary>
    /// The log's own words are English whatever the language (R-071): no log call composes its
    /// message through the T(english, romanian) pair, whichever logger it goes through and however
    /// many lines the call spans. And every logger is declared one way, a <c>Logger</c> field or
    /// property taken from <c>LogManager.GetLogger</c> with <c>using NLog</c>, so the first check
    /// sees every call.
    /// </summary>
    [Fact]
    public void NoLogLineGoesThroughTheTranslation()
    {
        List<string> offenders = [];
        List<string> declarations = [];
        foreach (string f in CsSources("src"))
        {
            string text = File.ReadAllText(f);
            foreach (Match m in LogCall().Matches(text))
            {
                Match end = CallEnd().Match(text, m.Index);
                string whole = end.Success ? text[m.Index..(end.Index + end.Length)] : text[m.Index..];
                if (Translation().IsMatch(whole))
                {
                    offenders.Add(Rel(f) + ":" + (text.Take(m.Index).Count(c => c == '\n') + 1).ToString(CultureInfo.InvariantCulture));
                }
            }

            string[] lines = File.ReadAllLines(f);
            for (int i = 0; i < lines.Length; i++)
            {
                bool inline = lines[i].Contains("GetLogger(", StringComparison.Ordinal) && !Declaration().IsMatch(lines[i]);
                bool qualified = lines[i].Contains("NLog.Logger", StringComparison.Ordinal) || lines[i].Contains("NLog.LogManager", StringComparison.Ordinal);
                if (inline || qualified)
                {
                    declarations.Add(Rel(f) + ":" + (i + 1).ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        Assert.Empty(offenders);
        Assert.Empty(declarations);
    }

    /// <summary>
    /// The numbers stay pure (ADR 0029): nothing in Analysis/ or Report/, nor Thresholds.cs, nor
    /// the files of Imaging/ copied from FlatCheck, holds a logger or names NLog; the runs log what
    /// the analyzers return, and of Imaging/ only the two adapted files log.
    /// </summary>
    [Fact]
    public void NothingThatComputesOrWordsTheReportLogs()
    {
        string[] adapted = ["ImageLoader.cs", "RawReader.cs"];
        string[] files = CsSources("src/LightsCheck.Core/Analysis", "src/LightsCheck.Core/Report")
            .Concat(CsSources("src/LightsCheck.Core/Imaging").Where(f => !adapted.Contains(Path.GetFileName(f))))
            .Append(Path.Combine(TestData.FindRepoRoot(), "src", "LightsCheck.Core", "Thresholds.cs"))
            .ToArray();

        Assert.True(files.Length > 20, "the folders were not found");
        Assert.All(files, f => Assert.DoesNotMatch(NamesNLog(), File.ReadAllText(f)));
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>The line a run's clock leaves when the run threw.</summary>
    [GeneratedRegex(@"^series ended without an outcome after \d+ ms")]
    private static partial Regex ThrownRun();

    /// <summary>The line the inventory's clock leaves when it threw before any stage ended.</summary>
    [GeneratedRegex(@"^inventory ended without an outcome after \d+ ms$")]
    private static partial Regex InventoryThrown();

    [GeneratedRegex(@"LogSetup\.(Apply|Close)\(")]
    private static partial Regex ApplyOrClose();

    [GeneratedRegex(@"LogManager\.(Configuration\s*=[^=]|Setup\()")]
    private static partial Regex SetsConfiguration();

    /// <summary>A call that composes a log line's text: a logger's, or a run clock's start, lap or outcome.</summary>
    [GeneratedRegex(@"\b\w*[Ll]og\.(Trace|Debug|Info|Warn|Error|Fatal)\(|\bRunClock\.Start\(|\b\w*[Cc]lock\??\.(Lap|Done)\(")]
    private static partial Regex LogCall();

    [GeneratedRegex(@"\)\s*;")]
    private static partial Regex CallEnd();

    [GeneratedRegex(@"\bT\(")]
    private static partial Regex Translation();

    [GeneratedRegex(@"static readonly Logger \w+ = LogManager\.GetLogger\(|static Logger \w+ => LogManager\.GetLogger\(")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"\bNLog\b|\bLogger\b|\bLogManager\b")]
    private static partial Regex NamesNLog();

    /// <summary>Writes <paramref name="bytes"/> of lines through one executable's configuration and closes it.</summary>
    private static void WriteThrough(string folder, string executable, long bytes, string tag)
    {
        using LogFactory factory = LogFiles.Factory(LogSetup.Build(executable, folder, LogLevel.Debug));
        Logger log = factory.GetLogger("Rolling");
        string payload = new('x', 4000);
        long lines = (bytes / (payload.Length + 100)) + 1;
        for (long i = 0; i < lines; i++)
        {
            log.Info(tag + " " + i.ToString("D6", CultureInfo.InvariantCulture) + " " + payload);
        }

        log.Info(tag + " last");
        factory.Shutdown();
    }

    /// <summary>The messages a console run writes in one language, times and the language's own name made equal, in a fixed order.</summary>
    private static string[] MessagesOf(string[] args, string lang)
    {
        using LogCapture capture = LogCapture.Start();
        Cli.Run([.. args, "--lang", lang]);
        return capture.Messages
            .Select(m => LogFiles.ElapsedTime().Replace(m, "#"))
            .Select(m => m.Replace("lang=" + lang, "lang=<lang>", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>The console's executable as the build copies it beside the tests.</summary>
    private static string ConsoleExe => Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "lightscheck.exe" : "lightscheck");

    /// <summary>
    /// Runs the real console, without a window, its level variable as given: removed for null,
    /// whatever the developer's own environment says.
    /// </summary>
    private static (int Pid, int Code, DateTime Started) RunTheConsole(string? level, params string[] args)
    {
        ProcessStartInfo start = new(ConsoleExe) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string a in args)
        {
            start.ArgumentList.Add(a);
        }

        start.Environment.Remove("LIGHTSCHECK_LOG");
        if (level is not null)
        {
            start.Environment["LIGHTSCHECK_LOG"] = level;
        }

        DateTime started = DateTime.Now;
        using Process p = Process.Start(start)!;
        p.StandardOutput.ReadToEnd();
        Assert.True(p.WaitForExit(30_000), "lightscheck did not end within 30 s");
        return (p.Id, p.ExitCode, started);
    }

    /// <summary>
    /// The lines one console process wrote, by its process id, from its start on (a process id of
    /// an older run may come back), in the log beside the executable; the files are opened sharing
    /// every access, as other tests' processes may hold them.
    /// </summary>
    private static List<string> ConsoleLinesOf(int pid, DateTime started)
    {
        string folder = Path.Combine(AppContext.BaseDirectory, LogSetup.FolderName);
        List<string> lines = [];
        if (!Directory.Exists(folder))
        {
            return lines;
        }

        string mark = " [" + pid.ToString(CultureInfo.InvariantCulture) + ":";
        foreach (string f in LogFiles.FilesOf(folder, "lightscheck"))
        {
            using FileStream s = new(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader r = new(s);
            for (string? l = r.ReadLine(); l is not null; l = r.ReadLine())
            {
                if (l.Length > 24 && l.Contains(mark, StringComparison.Ordinal)
                    && DateTime.TryParseExact(l[..24], "yyyy-MM-dd HH:mm:ss.ffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime t)
                    && t >= started.AddSeconds(-1))
                {
                    lines.Add(l);
                }
            }
        }

        return lines;
    }

    /// <summary>The C# sources under the given folders of the project, without bin/ and obj/ (filtered on the relative path).</summary>
    private static IEnumerable<string> CsSources(params string[] folders)
    {
        string root = TestData.FindRepoRoot();
        return folders.SelectMany(f => Directory.EnumerateFiles(Path.Combine(root, f), "*.cs", SearchOption.AllDirectories))
            .Where(f => !BuildOutput().IsMatch(Rel(f)));
    }

    [GeneratedRegex(@"(^|/)(bin|obj)/")]
    private static partial Regex BuildOutput();

    /// <summary>A file's path as the assertions name it: relative to the project, with forward slashes.</summary>
    private static string Rel(string f) => Path.GetRelativePath(TestData.FindRepoRoot(), f).Replace('\\', '/');
}
