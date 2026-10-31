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

// LoggingTests.cs - the diagnostic log (R-170 to R-173, ADR 0069): where the files go and how
// they roll, what the core and the console write, the level, two writers on one file, and where
// the log may be configured and written. DarksCheck's tests of the same log are the model.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit.Abstractions;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// The file tests run the applications' configuration (<see cref="LogSetup.Build"/>) in a private
/// <see cref="LogFactory"/>, which stands for one process, so they touch neither the test
/// assembly's capture (<see cref="LogCapture"/>) nor the tests beside them.
/// </summary>
public partial class LoggingTests
{
    private const string Exe = "AstroPhotoAnalyzer";
    private const string WindowExe = "AstroPhotoAnalyzer.Wpf";

    /// <summary>The five files 8 MiB of lines leave, measured in FlatCheck with the same NLog and the same settings (R-170).</summary>
    private static readonly string[] RolledFiles =
    [
        "AstroPhotoAnalyzer_03.log", "AstroPhotoAnalyzer_04.log", "AstroPhotoAnalyzer_05.log", "AstroPhotoAnalyzer_06.log", "AstroPhotoAnalyzer_07.log",
    ];

    private readonly ITestOutputHelper _output;

    /// <summary>The runner's output, for the measured file lists and the captured lines.</summary>
    public LoggingTests(ITestOutputHelper output) => _output = output;

    // ---------------------------------------------------------------- R-170: the files

    [Fact]
    public void R170_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName()
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

        string file = Path.Combine(folder, "AstroPhotoAnalyzer.log");
        Assert.Equal(new[] { file }, Directory.GetFiles(folder));
        byte[] bytes = File.ReadAllBytes(file);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no BOM");
        string line = Encoding.UTF8.GetString(bytes).TrimEnd('\r', '\n');
        Assert.Matches(LogFiles.LinePrefix(), line);
        Assert.EndsWith(" INFO  [" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ":"
            + Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture) + "] Test hello", line, StringComparison.Ordinal);
    }

    [Fact]
    public void R170_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback()
    {
        using TempWorkspace ws = new();
        // A FILE named log where the folder should be: the folder cannot be created.
        ws.WriteFile("log", [1]);
        string fallbackRoot = Path.Combine(ws.Dir, "LocalAppData");

        string folder = LogSetup.ResolveFolder(ws.Dir, fallbackRoot, Exe, out bool fellBack);

        Assert.True(fellBack);
        Assert.Equal(Path.Combine(fallbackRoot, "AstroPhotoAnalyzer", "log"), folder);
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
        Assert.Contains("(fallback", LogSetup.StartupLine(Exe, [], folder, true, LogLevel.Debug), StringComparison.Ordinal);
    }

    [Fact]
    public void R170_TheLogRollsAt1MiBAndKeepsAtMost5Files()
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
        Assert.Equal(files[^1], LogSetup.NewestLog(folder, Exe));

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

    /// <summary>
    /// The console's name begins the window's (<c>AstroPhotoAnalyzer</c>,
    /// <c>AstroPhotoAnalyzer.Wpf</c>), and in one folder neither executable's rolling deletes or
    /// takes the other's files, either way round; the probe's newest file is each one's own.
    /// </summary>
    [Fact]
    public void R170_OneExecutablesRollingLeavesTheOthersFilesAlone()
    {
        using TempWorkspace ws = new();
        string folder = Path.Combine(ws.Dir, "log");

        // The window's log first, rolled once, then the console's, rolled many times.
        WriteThrough(folder, WindowExe, 3 * LogSetup.FileSizeLimit / 2, "window");
        string[] window = LogFiles.FilesOf(folder, WindowExe);
        Assert.Equal(2, window.Length);

        WriteThrough(folder, Exe, 8 * LogSetup.FileSizeLimit, "console");

        Assert.Equal(window, LogFiles.FilesOf(folder, WindowExe));
        Assert.EndsWith("window last", File.ReadAllLines(window[^1])[^1], StringComparison.Ordinal);
        string[] console = LogFiles.FilesOf(folder, Exe);
        Assert.Equal(RolledFiles, console.Select(f => Path.GetFileName(f)));
        Assert.Equal(window[^1], LogSetup.NewestLog(folder, WindowExe));
        Assert.Equal(console[^1], LogSetup.NewestLog(folder, Exe));

        // And the other way round: the window rolled many times leaves the console's five.
        WriteThrough(folder, WindowExe, 8 * LogSetup.FileSizeLimit, "window again");

        Assert.Equal(console, LogFiles.FilesOf(folder, Exe));
        Assert.EndsWith("console last", File.ReadAllLines(console[^1])[^1], StringComparison.Ordinal);
        Assert.Equal(LogSetup.FilesPerExecutable, LogFiles.FilesOf(folder, WindowExe).Length);
        Assert.Equal(2 * LogSetup.FilesPerExecutable, Directory.GetFiles(folder).Length);
    }

    /// <summary>
    /// The folder's probe also opens the file NLog will append to: a read-only newest file sends
    /// the log to the fallback, where it would otherwise pass the probe and lose every line in
    /// silence (FlatCheck's review of 2026-10-27, its ADR 0072).
    /// </summary>
    [Fact]
    public void R170_AReadOnlyNewestLogFileSendsTheLogToTheFallback()
    {
        using TempWorkspace ws = new();
        string beside = Path.Combine(ws.Dir, "exe");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        Directory.CreateDirectory(folder);
        string newest = Path.Combine(folder, "AstroPhotoAnalyzer_03.log");
        File.WriteAllText(Path.Combine(folder, "AstroPhotoAnalyzer.log"), "old\n");
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

    /// <summary>A file another process of the same executable holds open (R-173) passes the probe: the second window logs beside the first.</summary>
    [Fact]
    public void R170_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe()
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
    [InlineData("C:/Tools/AstroPhotoAnalyzer.exe", "AstroPhotoAnalyzer", "AstroPhotoAnalyzer")]
    [InlineData("C:/Tools/AstroPhotoAnalyzer.Wpf.exe", "AstroPhotoAnalyzer.Wpf", "AstroPhotoAnalyzer.Wpf")]
    [InlineData("C:/Tools/AstroPhotoAnalyzer-1.0.exe", "AstroPhotoAnalyzer", "AstroPhotoAnalyzer-1.0")]
    [InlineData("C:/Program Files/dotnet/dotnet.exe", "AstroPhotoAnalyzer", "AstroPhotoAnalyzer")]
    [InlineData("/usr/share/dotnet/dotnet", "AstroPhotoAnalyzer", "AstroPhotoAnalyzer")]
    [InlineData(null, "AstroPhotoAnalyzer.Wpf", "AstroPhotoAnalyzer.Wpf")]
    public void R170_TheFilesBeginWithTheExecutablesName(string? processPath, string entry, string expected)
    {
        Assert.Equal(expected, LogSetup.ExecutableName(processPath, entry));
    }

    // ---------------------------------------------------------------- R-171: what is written

    [Fact]
    public void R171_TheFirstLineCarriesTheFacts()
    {
        string line = LogSetup.StartupLine(Exe, [@"D:\astro\de printat.png", "--lang", "ro"], @"D:\tools\log", false, LogLevel.Debug);

        Assert.StartsWith("AstroPhotoAnalyzer " + VersionInfo.Value + " starting; exe=", line, StringComparison.Ordinal);
        Assert.Contains(@"; args=[""D:\astro\de printat.png"", ""--lang"", ""ro""]; cwd=", line, StringComparison.Ordinal);
        Assert.Contains(@"; log=D:\tools\log; level=Debug; os=", line, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback", line, StringComparison.Ordinal);
        foreach (string part in new[] { "; cpus=", "; memory=", " GiB; runtime=", "; culture=", "; tz=", " (UTC" })
        {
            Assert.Contains(part, line, StringComparison.Ordinal);
        }

        Assert.EndsWith("; pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture), line, StringComparison.Ordinal);
    }

    /// <summary>
    /// A crash the window's dispatcher sees and then the process sees again is written once with
    /// its stack: the second Fatal line names the first (ADR 0069, after FlatCheck's ADR 0072).
    /// </summary>
    [Fact]
    public void R171_TheSameCrashSeenByTwoHooksIsWrittenOnce()
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
    /// run), is named by the hook, not written again; any other is written whole (LightsCheck's
    /// review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R171_ATaskExceptionAlreadyWrittenIsNamedNotWrittenAgain()
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
        Assert.NotNull(errors[1].Exception);
    }

    [Fact]
    public void R171_TheConfigurationHasOneFileTargetAndNoConsole()
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

    /// <summary>
    /// A run's clock writes its start line, then either its outcome with the time of every stage,
    /// or, when the run left without one because it threw, the stages it reached (R-171).
    /// </summary>
    [Fact]
    public void R171_ARunsClockWritesItsStagesWithItsOutcomeOrWithoutOne()
    {
        Logger log = LogManager.GetLogger("ClockTest");
        using LogCapture capture = LogCapture.Start();

        using (RunClock done = RunClock.Start(log, "analysis", "path=X"))
        {
            done.Lap("load");
            done.Lap("tones");
            done.Done("verdict=Ok");
        }

        using (RunClock thrown = RunClock.Start(log, "curves", "purpose=Print"))
        {
            thrown.Lap("anchors");
        }

        string[] lines = capture.Events.Where(e => e.Logger == "ClockTest").Select(e => LogFiles.ElapsedTime().Replace(e.Message, "#")).ToArray();
        string[] expected =
        [
            "analysis start path=X",
            "analysis done verdict=Ok in # (load #, tones #)",
            "curves start purpose=Print",
            "curves ended without an outcome after # (anchors #)",
        ];
        Assert.Equal(expected, lines);
    }

    /// <summary>
    /// A record's figures follow one rule (ADR 0069): every member that is not private, by its
    /// name in camelCase; numbers with six significant digits, a missing one as <c>none</c>, a flag
    /// as yes or no, an enum by its name, a tuple and a held record in parentheses, a short list
    /// whole and a long one by its ends and count; a getter written by hand that gives a text is
    /// left out (on this core, a sentence through Tr.Pick), one that gives a number is written; a
    /// delegate is left out; a getter that throws is <c>?</c>; another assembly's object is its
    /// type's name; a skipped name is left out at every depth.
    /// </summary>
    [Fact]
    public void R171_TheFiguresOfARecordFollowOneRule()
    {
        Probe probe = new("x", null, true, Verdict.Attention, new Pair(0.1234567, 3, "region name"), [1, 2],
            Enumerable.Range(0, 30).Select(i => (double)i).ToArray(), new DateTime(2026, 8, 13, 0, 48, 53));

        Assert.Equal(
            "missing=none flag=yes verdict=Attention pair=(a=0.123457 b=3) few=[1, 2] many=[0, ..., 29] (30 items) "
            + "when=2026-08-13 00:48:53 inner=0.25 twice=4 broken=? lab=(50, -1.5) foreign=<Version> field=7",
            LogText.Figures(probe, "Name"));
        Assert.StartsWith("name=x missing=none ", LogText.Figures(probe), StringComparison.Ordinal);
        Assert.Contains(" pair=(a=0.123457 b=3 name=region name) ", LogText.Figures(probe), StringComparison.Ordinal);
        Assert.Equal("none", LogText.Figures(null));
    }

    /// <summary>The log writes an elapsed time in two forms only: milliseconds, or seconds with one decimal; never whole seconds.</summary>
    [Fact]
    public void TheElapsedTimeHasTwoForms_MillisecondsAndSecondsWithOneDecimal()
    {
        Assert.Equal("12.9 s", LogText.Seconds(TimeSpan.FromMilliseconds(12900)));
        Assert.Equal("0.8 s", LogText.Seconds(TimeSpan.FromMilliseconds(800)));
        Assert.Equal("5 ms", LogText.Ms(5));
    }

    // ---------------------------------------------------------------- R-171: the core's seams

    /// <summary>
    /// An analysis of a file writes its start line with the path and every parameter, the file
    /// loaded, one figures line per result in the chain's order, one line per recommendation, and
    /// its done line with the verdict and the time of every stage (R-171).
    /// </summary>
    [Fact]
    public void R171_AnAnalysisLogsItsStartEveryResultTheRecommendationsAndTheStageTimes()
    {
        using TempWorkspace ws = new();
        string png = ImageFiles.SavePng16Rgb(Scenes.DarkSky(), ws.PathTo("dark-sky.png"));
        using LogCapture capture = LogCapture.Start();

        AnalysisResult result = AnalysisRun.Execute(png, Scenes.OnBuiltForPaper());

        string[] run = capture.Events.Where(e => e.Logger == "AnalysisRun").Select(e => e.Message).ToArray();
        Assert.StartsWith("analysis start path=" + png + " medium=Baryta dmaxOverride=2 ", run[0], StringComparison.Ordinal);
        Assert.Contains(" passepartoutCm=7 frameCm=3 ", run[0], StringComparison.Ordinal);
        string[] figures = run.Where(m => FigureLine().IsMatch(m)).Select(m => m[..m.IndexOf(':')]).ToArray();
        Assert.Equal(new[] { "file", "bitDepth", "crop", "tones", "simulation", "noise", "background", "stars", "size", "quantization", "jpeg" }, figures);
        Assert.Equal(result.Recommendations.Count, run.Count(m => m.StartsWith("recommendation topic=", StringComparison.Ordinal)));
        Assert.Matches(@"^analysis done verdict=" + result.Overall + @" recommendations=\d+ 512x512 workingSet=\d+\.\d MiB in \d+ ms \(load \d+ ms, file \d+ ms, "
            + @"tones \d+ ms, simulation \d+ ms, noise \d+ ms, background \d+ ms, stars \d+ ms, size \d+ ms, quantization \d+ ms, jpeg \d+ ms, "
            + @"recommendations \d+ ms, report \d+ ms\)$", run[^1]);
        Assert.Single(capture.Events, e => e.Logger == "ImageLoader" && e.Message.StartsWith("loaded " + png + " (", StringComparison.Ordinal));
        Assert.All(capture.Events.Where(e => e.Logger == "AnalysisRun"), e => Assert.True(e.Level == LogLevel.Info || e.Level == LogLevel.Debug, e.Line));
        _output.WriteLine(capture.Text);
    }

    /// <summary>
    /// Every image loaded is one line with its size, its reader and what the reader recorded; a
    /// FITS adds the keywords that decide the reading, an ImageSharp file what Identify found, both
    /// at Debug (R-171).
    /// </summary>
    [Fact]
    public void R171_ALoadedImageIsOneLineWithItsReadersFacts()
    {
        using TempWorkspace ws = new();
        string fits = ws.WriteFile("frame.fit", FitsTestData.Build(64, 48, 3, (x, y, p) => 0.3, extraCards: [FitsTestData.StringCard("OBJECT", "M42")]));
        // Flat takes the height first: a raster 64 wide and 48 high, as the FITS.
        string png = ImageFiles.SavePng16Rgb(SyntheticRasters.Flat(48, 64, 0.3f), ws.PathTo("frame.png"));
        using LogCapture capture = LogCapture.Start();

        ImageLoader.Load(fits);
        ImageLoader.Load(png);

        string[] loaded = Timeless(capture, "ImageLoader");
        Assert.Equal(2, loaded.Length);
        Assert.Equal("loaded " + fits + " (0.0 MiB) reader=FitsReader 64x48 planes=3 format=FITS bitDepth=16 float=no planesInFile=3 compression=none icc=none wcs=no note=none in #",
            loaded[0]);
        Assert.StartsWith("loaded " + png + " (0.0 MiB) reader=ImageSharpReader 64x48 planes=3 format=PNG bitDepth=16 float=no planesInFile=3 compression=Deflate icc=",
            loaded[1], StringComparison.Ordinal);
        Assert.Contains(capture.Events, e => e.Logger == "FitsReader" && e.Level == LogLevel.Debug
            && e.Message.StartsWith("header of " + fits + ": SIMPLE=T BITPIX=16 NAXIS=3 NAXIS1=64 NAXIS2=48 NAXIS3=3 ", StringComparison.Ordinal)
            && e.Message.Contains(" OBJECT=M42 ", StringComparison.Ordinal));
        Assert.Contains(capture.Events, e => e.Logger == "ImageSharpReader" && e.Level == LogLevel.Debug
            && e.Message == "identified " + png + ": format=PNG 64x48 bitsPerPixel=48 frames=0 bits=16 color=yes compression=Deflate");
    }

    /// <summary>A linear image is the run's outcome, a refusal with its figures, not a run that failed (R-171, R-015).</summary>
    [Fact]
    public void R171_ALinearImageIsARefusedDoneLineNotAFailure()
    {
        Raster linear = SyntheticRasters.Flat(64, 64, 0.004f);
        using LogCapture capture = LogCapture.Start();

        Assert.Throws<RefusedImageException>(() => AnalysisRun.Execute(linear, "linear", Scenes.OnBuiltForPaper()));

        string[] run = Timeless(capture, "AnalysisRun");
        Assert.Equal(2, run.Length);
        Assert.StartsWith("analysis start path=linear medium=Baryta ", run[0], StringComparison.Ordinal);
        Assert.EndsWith(" raster=64x64", run[0], StringComparison.Ordinal);
        Assert.Equal("analysis done refused: the image looks linear, median 0.004 under 0.02 in #", run[1]);
    }

    /// <summary>
    /// A run that throws leaves its start line and the stages it reached, and the exception keeps
    /// the reader's own as its inner one, so the caller's line writes both stacks (R-171).
    /// </summary>
    [Fact]
    public void R171_ARunThatThrowsLeavesItsStartLineAndTheStagesItReached()
    {
        using TempWorkspace ws = new();
        string png = ImageFiles.SavePng16Rgb(Scenes.CleanSky(), ws.PathTo("clean-sky.png"));
        byte[] whole = FitsTestData.Build(64, 64, 3, (x, y, p) => 0.3, bitpix: -32);
        string cutHeader = ws.WriteFile("cut-header.fit", whole[..1000]);
        string cutData = ws.WriteFile("cut-data.fit", whole[..(whole.Length / 2)]);

        using (LogCapture capture = LogCapture.Start())
        {
            Exception e = Assert.ThrowsAny<Exception>(() => AnalysisRun.Execute(png, Scenes.OnBuiltForPaper() with { WcsPath = cutHeader }));

            Assert.StartsWith("--wcs: ", e.Message, StringComparison.Ordinal);
            Assert.NotNull(e.InnerException);
            string[] run = Timeless(capture, "AnalysisRun");
            Assert.Equal(2, run.Length);
            Assert.StartsWith("analysis start path=" + png + " ", run[0], StringComparison.Ordinal);
            Assert.Contains(" wcsPath=" + cutHeader + " ", run[0], StringComparison.Ordinal);
            Assert.Equal("analysis ended without an outcome after # (load #)", run[1]);
        }

        using (LogCapture capture = LogCapture.Start())
        {
            Assert.ThrowsAny<Exception>(() => AnalysisRun.Execute(cutData, Scenes.OnBuiltForPaper()));

            string[] run = Timeless(capture, "AnalysisRun");
            Assert.Equal(2, run.Length);
            Assert.Equal("analysis ended without an outcome after #", run[1]);
            Assert.DoesNotContain(capture.Events, x => x.Logger == "ImageLoader");
        }
    }

    /// <summary>
    /// Every profile read is one line with its Dmax or its problem; a file that cannot be read is
    /// a warning with its exception; a folder of profiles is listed with its counts (R-171).
    /// </summary>
    [Fact]
    public void R171_AProfileThatCannotBeReadIsLoggedWithItsException()
    {
        using TempWorkspace ws = new();
        string gone = ws.PathTo("gone.icc");
        string junk = ws.WriteFile("junk.icc", [1, 2, 3]);
        string folder = Path.Combine(ws.Dir, "profiles");
        Directory.CreateDirectory(folder);
        string baryta = Path.Combine(folder, "baryta.icc");
        File.WriteAllBytes(baryta, IccPrinterTestData.V2Lut16("Baryta test", 3.5));
        File.WriteAllBytes(Path.Combine(folder, "junk.icm"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a profile");
        string missingFolder = Path.Combine(ws.Dir, "no such folder");
        using LogCapture capture = LogCapture.Start();

        IccPaperBlack.Read(gone);
        IccPaperBlack.Read(junk);
        IReadOnlyList<IccBlack> installed = IccPaperBlack.Installed(folder);
        IccPaperBlack.Installed(missingFolder);

        LogCapture.Captured missing = Assert.Single(capture.Events, e => e.Level == LogLevel.Warn);
        Assert.Equal("profile " + gone + " cannot be read", missing.Message);
        Assert.IsType<FileNotFoundException>(missing.Exception);
        Assert.Contains(capture.Messages, m => m.StartsWith("profile " + junk + " (3 bytes): description=junk.icc problem=", StringComparison.Ordinal)
            && m.Contains(" readable=no ", StringComparison.Ordinal));
        Assert.Contains(capture.Messages, m => m.StartsWith("profile " + baryta + " (", StringComparison.Ordinal)
            && m.Contains(" bytes): description=Baryta test problem=none readable=yes note=none blackLstar=", StringComparison.Ordinal));
        Assert.Single(installed);
        Assert.Contains("profiles in " + folder + ": 1 printer profiles of 3 files in #", Timeless(capture, "IccPaperBlack"));
        Assert.Contains("profiles folder " + missingFolder + " does not exist", capture.Messages);
    }

    /// <summary>
    /// The curves' run writes its start line, its anchors, a gamut table that cannot be built as a
    /// warning with its exception, every suggestion's curve and evidence, and its done line with
    /// the time of each stage (R-171).
    /// </summary>
    [Fact]
    public void R171_TheCurvesRunLogsItsAnchorsEachSuggestionAndAGamutThatCannotBeBuilt()
    {
        using TempWorkspace ws = new();
        string gone = ws.PathTo("gone.icc");
        Parameters p = Scenes.OnBuiltForPaper() with { DmaxProfile = "gone", DmaxProfilePath = gone };
        AnalysisResult a = AnalysisRun.Execute(CurveTests.StripeScene(), "stripes", p);
        using LogCapture capture = LogCapture.Start();

        CurveSet set = CurveRun.Execute(a, CurvePurpose.Print, 256, 256);

        Assert.NotEmpty(set.Suggestions);
        string[] run = Timeless(capture, "CurveRun");
        Assert.Equal("curves start path=stripes purpose=Print preview=256x256", run[0]);
        Assert.StartsWith("anchors: dust=", run[1], StringComparison.Ordinal);
        Assert.Contains(" method=Sky classLevels=[", run[1], StringComparison.Ordinal);
        LogCapture.Captured gamut = Assert.Single(capture.Events, e => e.Logger == "CurveRun" && e.Level == LogLevel.Warn);
        Assert.StartsWith("the gamut table of " + gone + " cannot be built: ", gamut.Message, StringComparison.Ordinal);
        Assert.IsType<FileNotFoundException>(gamut.Exception);
        string[] curves = run.Where(m => m.StartsWith("curve ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(set.Suggestions.Count, curves.Length);
        Assert.All(curves, c => Assert.Contains(": points=[(x=", c, StringComparison.Ordinal));
        Assert.All(curves, c => Assert.Contains(" noiseGain=", c, StringComparison.Ordinal));
        Assert.Equal("curves done suggestions=" + set.Suggestions.Count.ToString(CultureInfo.InvariantCulture) + " in # (anchors #, gamut #, suggestions #)", run[^1]);
    }

    /// <summary>
    /// The soft proof's preparation writes its start line with the profile, a refusal or its
    /// figures as its done line, and each render at Debug (R-171).
    /// </summary>
    [Fact]
    public void R171_TheProofRunLogsItsProfileAndItsOutcome()
    {
        using TempWorkspace ws = new();
        string icc = ws.WriteFile("paper.icc", IccPrinterTestData.V2Lut16("Paper test", 3.5));
        Raster scene = SyntheticRasters.From(64, 64, 3, (x, y, pl) => x < 32 ? (pl == 0 ? 1f : 0f) : 0.3f);
        Parameters withProfile = Scenes.OnBuiltForPaper().WithDmaxFromProfile(icc, out string error) ?? throw new InvalidOperationException(error);
        AnalysisResult none = AnalysisRun.Execute(scene, "memory", Scenes.OnBuiltForPaper());
        AnalysisResult a = AnalysisRun.Execute(scene, "memory", withProfile);
        using LogCapture capture = LogCapture.Start();

        ProofSet refused = ProofRun.Prepare(none);
        ProofSet set = ProofRun.Prepare(a);
        ProofRun.Render(set, ProofIntent.Relative, simulate: true);

        Assert.Null(set.Problem);
        string[] expected =
        [
            "proof start path=memory profile=none stride=1",
            "proof done refused: " + refused.Problem + " in #",
            "proof start path=memory profile=" + icc + " stride=1",
            "proof done outside=" + LogText.Num(set.PctPaper) + " % reduced=64x64 description=Paper test dmax=" + LogText.Num(set.Profile.Dmax)
                + " paperBlackLstar=" + LogText.Num(set.Profile.PaperBlackLstar) + " in # (profile #, gamut #, mask #)",
            "proof rendered intent=Relative simulate=yes in #",
        ];
        Assert.Equal(expected, Timeless(capture, "ProofRun"));
    }

    /// <summary>A FITS written with a suggested curve is one line with its size (R-171, R-145).</summary>
    [Fact]
    public void R171_ASavedCurveIsOneLineWithItsSize()
    {
        using TempWorkspace ws = new();
        AnalysisResult a = AnalysisRun.Execute(CurveTests.StripeScene(), "stripes", Scenes.OnBuiltForPaper());
        CurveSet set = CurveRun.Execute(a, CurvePurpose.Print, 256, 256);
        string target = ws.PathTo("curved.fit");
        using LogCapture capture = LogCapture.Start();

        CurveExport.Save(a, set.Suggestions[0], target);

        LogCapture.Captured wrote = Assert.Single(capture.Events, e => e.Logger == "CurveExport");
        Assert.Equal(LogLevel.Info, wrote.Level);
        Assert.Equal("wrote " + target + " (" + LogText.FileMiB(target) + ") curve=" + set.Suggestions[0].Name + " header from=none", wrote.Message);
    }

    /// <summary>
    /// The log's own words are English whatever the language (R-171): the same analysis in English
    /// and in Romanian writes the same lines, but for the recommendations, whose text and because
    /// are written as the user reads them; and the English run holds no Romanian word.
    /// </summary>
    [Fact]
    public void R171_TheLogIsTheSameInBothLanguages()
    {
        using TempWorkspace ws = new();
        string png = ImageFiles.SavePng16Rgb(Scenes.DarkSky(), ws.PathTo("dark-sky.png"));

        string[] english = AnalysisMessages(png, UiLanguage.English);
        string[] romanian = AnalysisMessages(png, UiLanguage.Romanian);

        // the start, the loaded and the identified lines, eleven results, the done line
        Assert.True(english.Length >= 15, "the run wrote " + english.Length.ToString(CultureInfo.InvariantCulture) + " lines");
        Assert.Equal(english, romanian);
        Assert.Empty(RomanianWords.In(string.Join("\n", english)));
    }

    // ---------------------------------------------------------------- R-171: the console

    /// <summary>
    /// A run that fails is one line on stderr, as before, and in the log an Error line with the
    /// exception's type, message and stack (R-171, R-002).
    /// </summary>
    [Fact]
    public void R171_TheConsoleLogsAFailureWithItsStack_AndStderrKeepsOneLine()
    {
        using TempWorkspace ws = new();
        byte[] whole = FitsTestData.Build(64, 64, 3, (x, y, p) => 0.3, bitpix: -32);
        string cut = ws.WriteFile("cut-data.fit", whole[..(whole.Length / 2)]);
        StringWriter stdout = new();
        StringWriter stderr = new();
        using LogCapture capture = LogCapture.Start();

        int code = AnalyzerCli.Run([cut, "--lang", "en"], stdout, stderr);

        Assert.Equal(1, code);
        string error = Assert.Single(stderr.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)).TrimEnd('\r');
        Assert.StartsWith("ERROR: ", error, StringComparison.Ordinal);
        LogCapture.Captured failed = Assert.Single(capture.Events, e => e.Level == LogLevel.Error);
        Assert.Equal("Cli", failed.Logger);
        Assert.Equal("run failed: mode=analysis", failed.Message);
        Assert.Equal(error["ERROR: ".Length..], failed.Exception?.Message);
        Assert.Contains("FitsReader", failed.Line, StringComparison.Ordinal);   // the stack, under the line
        Assert.Contains("mode=analysis image=" + cut + " out=none lang=en", capture.Messages);
    }

    /// <summary>
    /// Every refusal of the console is a Warn line holding the message stderr shows, without an
    /// exception: a bad option, a bad --lang, a folder, a file that does not exist (R-171, R-003).
    /// </summary>
    [Fact]
    public void R171_ARefusedOptionIsLoggedAtWarnAsShown()
    {
        using TempWorkspace ws = new();
        string[][] runs =
        [
            ["x.tif", "--medium", "paper", "--lang", "en"],
            ["x.tif", "--lang", "fr"],
            [ws.Dir, "--lang", "en"],
            [ws.PathTo("missing.tif"), "--lang", "en"],
        ];
        foreach (string[] args in runs)
        {
            StringWriter stderr = new();
            using LogCapture capture = LogCapture.Start();

            Assert.Equal(1, AnalyzerCli.Run(args, new StringWriter(), stderr));

            string shown = stderr.ToString().Trim();
            Assert.StartsWith("ERROR: ", shown, StringComparison.Ordinal);
            LogCapture.Captured warn = Assert.Single(capture.Events, e => e.Level == LogLevel.Warn);
            Assert.Equal("Cli", warn.Logger);
            Assert.Null(warn.Exception);
            Assert.StartsWith("refused", warn.Message, StringComparison.Ordinal);
            Assert.EndsWith(": " + shown["ERROR: ".Length..], warn.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The log adds nothing to the console's streams: with the capture open, a golden scene's
    /// stdout and stderr are its golden file, byte for byte, while the log holds the run (R-171,
    /// R-001).
    /// </summary>
    [Fact]
    public void R171_TheConsoleAddsNothingToStdoutOrStderr()
    {
        using TempWorkspace ws = new();
        string png = ImageFiles.SavePng8Gray(Scenes.MonoSky(), ws.PathTo("mono.png"));
        using LogCapture capture = LogCapture.Start();

        string actual = GoldenOutputTests.RunCli(ws, "en", png);

        Assert.True(capture.Events.Count > 10, "the log holds " + capture.Events.Count.ToString(CultureInfo.InvariantCulture) + " lines");
        Assert.Equal(File.ReadAllText(Path.Combine(GoldenOutputTests.GoldenDir, "mono-png8.en.txt")).Replace("\r\n", "\n"), actual);
    }

    /// <summary>The report the console writes with -o is one line with its size (R-171).</summary>
    [Fact]
    public void R171_TheReportTheConsoleWritesIsOneLineWithItsSize()
    {
        using TempWorkspace ws = new();
        string png = ImageFiles.SavePng8Gray(Scenes.MonoSky(), ws.PathTo("mono.png"));
        string report = ws.PathTo("report.txt");
        using LogCapture capture = LogCapture.Start();

        AnalyzerCli.Run([png, "-o", report, "--lang", "en"], new StringWriter(), new StringWriter());

        Assert.True(File.Exists(report));
        Assert.Contains(LogText.Wrote(report), capture.Messages);
    }

    /// <summary>
    /// The real console, as the user starts it, writes the process's first line with its facts,
    /// its own lines, and the last line with the exit code and the elapsed time into the log beside
    /// the executable, which here is the test's output folder, which other tests' processes share.
    /// </summary>
    [Fact]
    public void R171_TheRealConsoleWritesItsFirstAndItsLastLine()
    {
        (int pid, int code, List<string> lines) = RunTheConsole(null, "--help", "--lang", "en");

        Assert.Equal(0, code);
        Assert.True(lines.Count >= 3, "the console wrote " + lines.Count.ToString(CultureInfo.InvariantCulture) + " lines");
        Assert.Contains(" INFO  [" + pid.ToString(CultureInfo.InvariantCulture) + ":", lines[0], StringComparison.Ordinal);
        Assert.Contains("] Process AstroPhotoAnalyzer " + VersionInfo.Value + " starting; exe=" + ConsoleExe + "; args=[\"--help\", \"--lang\", \"en\"]; ",
            lines[0], StringComparison.Ordinal);
        Assert.Contains("; level=Debug; ", lines[0], StringComparison.Ordinal);
        Assert.Contains(lines, l => l.EndsWith("] Cli mode=help", StringComparison.Ordinal));
        Assert.Matches(@"\] Process exit 0 after \d+\.\d s$", lines[^1]);
    }

    // ---------------------------------------------------------------- R-172: the level

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
    public void R172_TheLevelComesFromTheEnvironmentVariable(string? value, string level)
    {
        Assert.Equal(LogLevel.FromString(level), LogSetup.LevelFrom(value));
    }

    /// <summary>
    /// The real console takes its level from <c>APA_LOG</c>, the name R-172 and the README give,
    /// written out here so a renamed constant fails: <c>off</c> writes no line, <c>trace</c> says so
    /// in the first line (LightsCheck's review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R172_TheRealConsoleTakesItsLevelFromAPA_LOG()
    {
        (_, int offCode, List<string> off) = RunTheConsole("off", "--help");
        (_, int traceCode, List<string> trace) = RunTheConsole("trace", "--help");

        Assert.Equal(0, offCode);
        Assert.Equal(0, traceCode);
        Assert.Empty(off);
        Assert.NotEmpty(trace);
        Assert.Contains("; level=Trace; ", trace[0], StringComparison.Ordinal);
    }

    [Fact]
    public void R172_OffCreatesNeitherTheFolderNorAFile()
    {
        using TempWorkspace ws = new();

        LogPlace off = LogSetup.Plan(Exe, Path.Combine(ws.Dir, "AstroPhotoAnalyzer.exe"), ws.Dir, Path.Combine(ws.Dir, "fallback"), "off");

        Assert.Null(off.Folder);
        Assert.Empty(Directory.GetFileSystemEntries(ws.Dir));
        Assert.Empty(LogSetup.Build(Exe, Path.Combine(ws.Dir, "log"), LogLevel.Off).AllTargets);

        LogPlace trace = LogSetup.Plan(Exe, Path.Combine(ws.Dir, "AstroPhotoAnalyzer.exe"), ws.Dir, Path.Combine(ws.Dir, "fallback"), "trace");
        Assert.Equal(LogLevel.Trace, trace.Level);
        Assert.Equal(Path.Combine(ws.Dir, "log"), trace.Folder);
    }

    /// <summary>
    /// Trace adds every card of a FITS header, HISTORY included, below the keywords that decide the
    /// reading, which are at Debug, for the analysed image and for a <c>--wcs</c> file (R-172).
    /// </summary>
    [Fact]
    public void R172_TraceHoldsEveryFitsHeaderCard()
    {
        using TempWorkspace ws = new();
        string history = "HISTORY made by a test".PadRight(FitsTestData.CardSize);
        string fits = ws.WriteFile("frame.fit", FitsTestData.Build(64, 48, 3, (x, y, p) => 0.3,
            extraCards: [FitsTestData.StringCard("OBJECT", "M42"), history]));
        using LogCapture capture = LogCapture.Start();

        ImageLoader.Load(fits);
        FitsReader.ReadImageHeader(fits);

        string[] cards = capture.Events.Where(e => e.Level == LogLevel.Trace && e.Logger == "FitsReader").Select(e => e.Message).ToArray();
        Assert.Equal(2, cards.Count(c => c == "card " + FitsTestData.Card("BITPIX", "16").TrimEnd()));
        Assert.Contains("card " + FitsTestData.StringCard("OBJECT", "M42").TrimEnd(), cards);
        Assert.Contains("card HISTORY made by a test", cards);
        Assert.Contains("card END", cards);
        Assert.DoesNotContain(capture.Events, e => e.Level != LogLevel.Trace && e.Message.StartsWith("card ", StringComparison.Ordinal));
        Assert.Equal(2, capture.Events.Count(e => e.Level == LogLevel.Debug && e.Message.StartsWith("header of " + fits + ": SIMPLE=T BITPIX=16 ", StringComparison.Ordinal)));
    }

    // ---------------------------------------------------------------- R-173: two processes

    /// <summary>
    /// Two processes of one executable (two windows) write into the same file at full speed, and
    /// every line of both arrives whole, once, past the 1 MiB roll too (R-170, R-173), on the
    /// <c>net8.0</c> build of the atomic target, the console's; the window's tests run the same
    /// check on its own build (<see cref="LogFiles.TwoWritersKeepEveryLine"/>).
    /// </summary>
    [Fact]
    public void R173_TwoProcessesWritingAtFullSpeedKeepEveryLine()
    {
        using TempWorkspace ws = new();

        LogFiles.TwoWritersKeepEveryLine(Path.Combine(ws.Dir, "log"), WindowExe, _output);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>A result's figures line: its name, a colon, its figures.</summary>
    [GeneratedRegex(@"^\w+: ")]
    private static partial Regex FigureLine();

    /// <summary>The working set as the log writes it.</summary>
    [GeneratedRegex(@"workingSet=\d+\.\d MiB")]
    private static partial Regex WorkingSet();

    /// <summary>One logger's messages in a capture, each elapsed time written as <c>#</c>.</summary>
    private static string[] Timeless(LogCapture capture, string logger)
        => capture.Events.Where(e => e.Logger == logger).Select(e => LogFiles.ElapsedTime().Replace(e.Message, "#")).ToArray();

    /// <summary>
    /// The messages one analysis of <paramref name="path"/> writes in one language, times and the
    /// working set made equal, without the recommendations' lines, whose text is the user's.
    /// </summary>
    private static string[] AnalysisMessages(string path, UiLanguage language)
    {
        using IDisposable scope = Tr.Use(language);
        using LogCapture capture = LogCapture.Start();
        AnalysisRun.Execute(path, Scenes.OnBuiltForPaper());
        return capture.Messages
            .Where(m => !m.StartsWith("recommendation ", StringComparison.Ordinal))
            .Select(m => WorkingSet().Replace(LogFiles.ElapsedTime().Replace(m, "#"), "workingSet=#"))
            .ToArray();
    }

    /// <summary>A record for the figures' rule: every kind of member and value it writes or leaves out.</summary>
    private sealed record Probe(string? Name, double? Missing, bool Flag, Verdict Verdict, Pair Pair, IReadOnlyList<int> Few, IReadOnlyList<double> Many, DateTime When)
    {
        internal double Inner { get; init; } = 0.25;

        internal string Sentence => "a sentence through Tr.Pick";

        internal int Twice => Few.Count * 2;

        internal Func<int> Callback { get; init; } = () => 1;

        internal double Broken => throw new InvalidOperationException("no figure");

        internal (double L, double A) Lab { get; init; } = (50, -1.5);

        internal Version Foreign { get; init; } = new(1, 2);

        internal int Field = 7;
    }

    /// <summary>A record held by <see cref="Probe"/>.</summary>
    private sealed record Pair(double A, int B, string Name);

    /// <summary>The console's executable as the build copies it beside the tests.</summary>
    private static string ConsoleExe => Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "AstroPhotoAnalyzer.exe" : "AstroPhotoAnalyzer");

    /// <summary>
    /// Runs the real console, without a window, its level variable as given: removed for null,
    /// whatever the developer's own environment says. It runs in a working folder of its own,
    /// which its first line names, and its lines are read while this test still holds its
    /// handle, so no other process can be given its id meanwhile and an older process's lines
    /// under the same id stay out (LightsCheck's second review of 2026-10-30).
    /// </summary>
    private static (int Pid, int Code, List<string> Lines) RunTheConsole(string? level, params string[] args)
    {
        using TempWorkspace cwd = new();
        ProcessStartInfo start = new(ConsoleExe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = cwd.Dir,
        };
        foreach (string a in args)
        {
            start.ArgumentList.Add(a);
        }

        start.Environment.Remove(LogSetup.LevelVariable);
        if (level is not null)
        {
            start.Environment["APA_LOG"] = level;
        }

        using Process p = Process.Start(start)!;
        Task<string> errors = p.StandardError.ReadToEndAsync();
        p.StandardOutput.ReadToEnd();
        Assert.True(p.WaitForExit(30_000), "AstroPhotoAnalyzer did not end within 30 s");
        errors.Wait();
        return (p.Id, p.ExitCode, ConsoleLinesOf(p.Id, Path.GetFileName(cwd.Dir)));
    }

    /// <summary>
    /// The lines one console process wrote, in the log beside the executable: those of its process
    /// id from its first line on, the one that names its working folder; none when it wrote no
    /// first line. The files are opened sharing every access, as other tests' processes may hold
    /// them.
    /// </summary>
    private static List<string> ConsoleLinesOf(int pid, string folderName)
    {
        string folder = Path.Combine(AppContext.BaseDirectory, LogSetup.FolderName);
        List<string> lines = [];
        if (!Directory.Exists(folder))
        {
            return lines;
        }

        string mark = " [" + pid.ToString(CultureInfo.InvariantCulture) + ":";
        foreach (string f in LogFiles.FilesOf(folder, Exe))
        {
            using FileStream s = new(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader r = new(s);
            for (string? l = r.ReadLine(); l is not null; l = r.ReadLine())
            {
                if (l.Contains(mark, StringComparison.Ordinal) && (lines.Count > 0
                    || (l.Contains(" starting; ", StringComparison.Ordinal) && l.Contains(folderName, StringComparison.Ordinal))))
                {
                    lines.Add(l);
                }
            }
        }

        return lines;
    }

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
}
