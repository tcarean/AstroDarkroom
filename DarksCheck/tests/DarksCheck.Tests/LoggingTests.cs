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
using DarksCheck.Core;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit.Abstractions;

namespace DarksCheck.Tests;

/// <summary>
/// The diagnostic log (R-100 to R-103, ADR 0025): where the files go and how they roll, what a
/// run writes, the level, two writers on one file. The file tests run the applications'
/// configuration (<see cref="LogSetup.Build"/>) in a private <see cref="LogFactory"/>, which
/// stands for one process, so they touch neither the test assembly's capture
/// (<see cref="LogCapture"/>) nor the tests beside them. LightsCheck's and FlatCheck's tests of the
/// same log are the model (their LoggingTests).
/// </summary>
public partial class LoggingTests
{
    private const string Exe = "darkscheck";

    /// <summary>The only two files that start and close the log: the two entry points.</summary>
    private static readonly string[] EntryPoints = ["src/DarksCheck.Cli/Program.cs", "src/DarksCheck.Wpf/App.xaml.cs"];

    /// <summary>The only two files that set NLog's configuration: the applications' and the tests'.</summary>
    private static readonly string[] Configurers = ["src/DarksCheck.Core/Logging/LogSetup.cs", "tests/DarksCheck.Tests/LogCapture.cs"];

    /// <summary>The five files 8 MiB of lines leave, measured in FlatCheck with the same NLog and the same settings (R-100).</summary>
    private static readonly string[] RolledFiles = ["darkscheck_03.log", "darkscheck_04.log", "darkscheck_05.log", "darkscheck_06.log", "darkscheck_07.log"];

    private readonly ITestOutputHelper _output;

    /// <summary>The runner's output, for the measured file lists and the captured lines.</summary>
    public LoggingTests(ITestOutputHelper output) => _output = output;

    // ---------------------------------------------------------------- R-100: the files

    [Fact]
    public void R100_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName()
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

        string file = Path.Combine(folder, "darkscheck.log");
        Assert.Equal(new[] { file }, Directory.GetFiles(folder));
        byte[] bytes = File.ReadAllBytes(file);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no BOM");
        string line = Encoding.UTF8.GetString(bytes).TrimEnd('\r', '\n');
        Assert.Matches(LogFiles.LinePrefix(), line);
        Assert.EndsWith(" INFO  [" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ":"
            + Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture) + "] Test hello", line, StringComparison.Ordinal);
    }

    [Fact]
    public void R100_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback()
    {
        using TempWorkspace ws = new();
        // A FILE named log where the folder should be: the folder cannot be created.
        ws.WriteFile("log", [1]);
        string fallbackRoot = Path.Combine(ws.Dir, "LocalAppData");

        string folder = LogSetup.ResolveFolder(ws.Dir, fallbackRoot, Exe, out bool fellBack);

        Assert.True(fellBack);
        Assert.Equal(Path.Combine(fallbackRoot, "DarksCheck", "log"), folder);
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
        Assert.Contains("(fallback", LogSetup.StartupLine(Exe, [], folder, true, LogLevel.Debug), StringComparison.Ordinal);
    }

    [Fact]
    public void R100_TheLogRollsAt1MiBAndKeepsAtMost5Files()
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
    public void R100_OneExecutablesRollingLeavesTheOthersFilesAlone()
    {
        using TempWorkspace ws = new();
        string folder = Path.Combine(ws.Dir, "log");
        // The window's log first, rolled once, then the console's, rolled many times, in the
        // same folder: DarksCheck.Wpf begins with the same letters as darkscheck, in another case.
        WriteThrough(folder, "DarksCheck.Wpf", 3 * LogSetup.FileSizeLimit / 2, "window");
        string[] window = LogFiles.FilesOf(folder, "DarksCheck.Wpf");
        Assert.Equal(2, window.Length);

        WriteThrough(folder, Exe, 8 * LogSetup.FileSizeLimit, "console");

        Assert.Equal(window, LogFiles.FilesOf(folder, "DarksCheck.Wpf"));
        Assert.EndsWith("window last", File.ReadAllLines(window[^1])[^1], StringComparison.Ordinal);
        Assert.Equal(LogSetup.FilesPerExecutable, LogFiles.FilesOf(folder, Exe).Length);
    }

    /// <summary>
    /// The folder's probe also opens the file NLog will append to: a read-only newest file sends
    /// the log to the fallback, where it would otherwise pass the probe and lose every line in
    /// silence (FlatCheck's review of 2026-10-27, its ADR 0072).
    /// </summary>
    [Fact]
    public void R100_AReadOnlyNewestLogFileSendsTheLogToTheFallback()
    {
        using TempWorkspace ws = new();
        string beside = Path.Combine(ws.Dir, "exe");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        Directory.CreateDirectory(folder);
        string newest = Path.Combine(folder, "darkscheck_03.log");
        File.WriteAllText(Path.Combine(folder, "darkscheck.log"), "old\n");
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

    /// <summary>A file another process of the same executable holds open (R-103) passes the probe: the second window logs beside the first.</summary>
    [Fact]
    public void R100_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe()
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
    [InlineData("C:/Tools/darkscheck.exe", "darkscheck", "darkscheck")]
    [InlineData("C:/Tools/darkscheck-1.0.exe", "darkscheck", "darkscheck-1.0")]
    [InlineData("C:/Program Files/dotnet/dotnet.exe", "darkscheck", "darkscheck")]
    [InlineData("/usr/share/dotnet/dotnet", "darkscheck", "darkscheck")]
    [InlineData(null, "DarksCheck.Wpf", "DarksCheck.Wpf")]
    public void R100_TheFilesBeginWithTheExecutablesName(string? processPath, string entry, string expected)
    {
        Assert.Equal(expected, LogSetup.ExecutableName(processPath, entry));
    }

    // ---------------------------------------------------------------- R-101: what is written

    [Fact]
    public void R101_TheFirstLineCarriesTheFacts()
    {
        string line = LogSetup.StartupLine(Exe, [@"D:\astro\smida darks", "--lang", "ro"], @"D:\tools\log", false, LogLevel.Debug);

        Assert.StartsWith("darkscheck " + VersionInfo.Value + " starting; exe=", line, StringComparison.Ordinal);
        Assert.Contains(@"; args=[""D:\astro\smida darks"", ""--lang"", ""ro""]; cwd=", line, StringComparison.Ordinal);
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
    /// its stack: the second Fatal line names the first (ADR 0025, after FlatCheck's ADR 0072).
    /// </summary>
    [Fact]
    public void R101_TheSameCrashSeenByTwoHooksIsWrittenOnce()
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
    /// unhandled one), is named by the hook, not written again; any other is written whole
    /// (LightsCheck's review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R101_ATaskExceptionAlreadyWrittenIsNamedNotWrittenAgain()
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
    public void R101_TheConfigurationHasOneFileTargetAndNoConsole()
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
    /// or, when the run left without one because it threw, the stages it reached (R-101).
    /// </summary>
    [Fact]
    public void R101_ARunsClockWritesItsStagesWithItsOutcomeOrWithoutOne()
    {
        Logger log = LogManager.GetLogger("ClockTest");
        using LogCapture capture = LogCapture.Start();

        using (RunClock done = RunClock.Start(log, "darks", "folder=X"))
        {
            done.Lap("inventory");
            done.Lap("darks");
            done.Done("leak=Ok");
        }

        using (RunClock thrown = RunClock.Start(log, "inventory", "folder=Y"))
        {
            thrown.Lap("open");
        }

        string[] lines = capture.Events.Where(e => e.Logger == "ClockTest").Select(e => LogFiles.ElapsedTime().Replace(e.Message, "#")).ToArray();
        string[] expected =
        [
            "darks start folder=X",
            "darks done leak=Ok in # (inventory #, darks #)",
            "inventory start folder=Y",
            "inventory ended without an outcome after # (open #)",
        ];
        Assert.Equal(expected, lines);
    }

    // ---------------------------------------------------------------- R-101: the core's seams

    /// <summary>
    /// A darks run with biases, lights and a partner writes its start line with its options, each
    /// folder opened, each file read and each frame decoded with the process's working set, each
    /// stack, the figures of every dark, bias and light, the series, the verdicts, the index, the
    /// lights and the partner, and its done line with the verdicts and the time of every stage
    /// (R-101, D4 and D8 of ADR 0025).
    /// </summary>
    [Fact]
    public void R101_ADarksRunLogsEveryFrameTheVerdictsTheIndexAndTheStageTimes()
    {
        using SessionFixture p = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 2 }, inSubfolder: false);
        using SessionFixture s = new SessionFixture()
            .WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice }, biases: 2)
            .WithLights(3, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, Stars = SyntheticLight.ThreeStars });
        using LogCapture capture = LogCapture.Start();

        DarkSeriesResult r = DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2, Against: p.Root));

        string[] m = Timeless(capture);
        Assert.Equal("darks start folder=" + s.Root + " threads=2 maxFrames=60 against=" + p.Root + " lightsFrom=none isoFactor=2 doubling=6", m[0]);
        Assert.Contains("opened root=" + s.Root + " kind=session darks=" + s.DarksDir + " (4 frames, .fit) biases=" + s.BiasesDir + " (2) lights=" + s.LightsDir + " (3) chosenLights=none", m);
        Assert.Contains("opened root=" + p.Root + " kind=library darks=" + p.Root + " (3 frames, .fit) biases=none (0) lights=none (0) chosenLights=none", m);
        Assert.Contains("inventory done darks=4 biases=2 lights=3 errors=0 in # (open #, darks #, biases #, lights #, cadence #)", m);
        // every file read whole has its line: the darks, the biases, the lights and the partner's darks
        Assert.Equal(12, m.Count(x => x.StartsWith("loaded ", StringComparison.Ordinal)));
        // every frame a stack decoded has its line with the working set: the darks, the biases and the partner's
        Assert.Equal(9, m.Count(x => DecodedLine().IsMatch(x)));
        Assert.Contains("stack of 2 files: 2 decoded, 0 failed, 0 skipped; 256x192 black=200 (the median of frame 1) white=65535 (from BITPIX) cfa=RGGB norm=1.66667E-06 in #", m);
        Assert.Contains("stack of 4 files: 4 decoded, 0 failed, 0 skipped; 256x192 black=200 (the biases') white=65535 (from BITPIX) cfa=RGGB norm=1 in #", m);
        Assert.Equal(4, m.Count(x => DarkLine().IsMatch(x)));
        Assert.Equal(2, m.Count(x => x.StartsWith("bias ", StringComparison.Ordinal) && x.Contains(": planes=[(name=R ", StringComparison.Ordinal)));
        Assert.Equal(3, m.Count(x => LightLine().IsMatch(x)));
        Assert.Single(m, x => x.StartsWith("series: width=256 height=192 black=200 white=65535 cfaLetters=RGGB norm=1 ", StringComparison.Ordinal));
        Assert.Single(m, x => x.StartsWith("verdicts: leak=(verdict=" + r.Verdicts.Leak.Verdict + " offsetAdu=", StringComparison.Ordinal));
        Assert.Contains("index: " + LogText.Figures(r.Index) + " label=" + r.Label + " partnerLabel=none", m);
        Assert.Single(m, x => x.StartsWith("lights: used=3 ", StringComparison.Ordinal));
        Assert.Single(m, x => x.StartsWith("partner: folder=" + p.Root + " frames=3 used=3 ", StringComparison.Ordinal));
        Assert.Equal("darks done " + DarkSeriesRun.Outcome(r) + " in # (inventory #, biases #, darks #, measure #, verdicts #, lights #, partner #)", m[^1]);
        Assert.StartsWith("darks done leak=Ok drift=Ok noise=Ok pattern=Ok deadSat=Ok enough=Warning index=", m[^1], StringComparison.Ordinal);
        Assert.Contains(" frames darks=4/4 biases=2/2 lights=3/3 partner=3/3 errors=no in ", m[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(capture.Events, e => e.Level >= LogLevel.Warn);
    }

    [Fact]
    public void R101_AnInventoryRunLogsEveryFrameReadAndTheCadence()
    {
        using SessionFixture s = new SessionFixture().WithDarks(3, biases: 2, lights: 2);
        using LogCapture capture = LogCapture.Start();

        InventoryRun.Run(s.Root);

        string[] m = Timeless(capture);
        string first = Path.Combine(s.DarksDir, "DSC_0001.fit");
        Assert.Equal("inventory start folder=" + s.Root + " lightsFrom=none", m[0]);
        Assert.Equal(7, m.Count(x => x.StartsWith("read the header of ", StringComparison.Ordinal)));
        Assert.Contains(m, x => x.StartsWith("read the header of " + first + " (0.0 MiB) reader=fits no pixels planes=1 bitpix=16 cfa=none scale=none camera=NIKON D3300 ", StringComparison.Ordinal)
            && x.Contains(" exposure=119.2 iso=400 time=2026-08-13 00:48:53 ", StringComparison.Ordinal));
        Assert.Contains(m, x => x.StartsWith("header of " + first + ": XTENSION=none NAXIS=2 NAXIS1=32 NAXIS2=24 ", StringComparison.Ordinal)
            && x.Contains(" EXPTIME=119.2 ISOSPEED=400 DATE-OBS=2026-08-13T00:48:53 ", StringComparison.Ordinal));
        Assert.Contains("cadence of the darks: start=2026-08-13 00:48:53 end=2026-08-13 00:54:52 spanSeconds=359.2 integrationSeconds=357.6 timedFrames=3 medianIntervalSeconds=120 pauseSeconds=[none, 0.8, 0.8] longPauses=[]", m);
        Assert.Contains("match: darks=(exposureSeconds=119.2 iso=400 frames=3 readable=3 uniform=yes) biases=(exposureSeconds=0.0002 iso=400 frames=2 readable=2 uniform=yes) "
            + "lights=(exposureSeconds=119.2 iso=400 frames=2 readable=2 uniform=yes) darksVsLights=Ok bias=Ok", m);
        Assert.Equal("inventory done darks=3 biases=2 lights=2 errors=0 in # (open #, darks #, biases #, lights #, cadence #)", m[^1]);
    }

    /// <summary>
    /// A file whose metadata cannot be read is a Warn line with the exception, which only the log
    /// keeps: the row carries the message alone. A file that is no FITS at all is a Warn line
    /// without one, since nothing threw. The inventory goes on and counts both (R-101).
    /// </summary>
    [Fact]
    public void R101_AFrameThatCannotBeReadIsLoggedWithItsExceptionAndTheRunGoesOn()
    {
        using SessionFixture raw = new SessionFixture().WithDarks(1);
        string nef = Path.Combine(raw.DarksDir, "DSC_0002.NEF");
        File.WriteAllBytes(nef, new byte[4096]);
        using SessionFixture fits = new SessionFixture().WithDarks(2);
        string junk = Path.Combine(fits.DarksDir, "DSC_0003.fit");
        File.WriteAllBytes(junk, new byte[100]);
        using LogCapture capture = LogCapture.Start();

        InventoryRun.Run(raw.DarksDir);
        InventoryRun.Run(fits.Root);

        LogCapture.Captured unreadable = Assert.Single(capture.Events, e => e.Level == LogLevel.Warn && e.Exception is not null);
        Assert.Equal("FrameInfo", unreadable.Logger);
        Assert.Equal("frame 1 " + nef + " could not be read; its row carries the error", unreadable.Message);
        Assert.Contains("   at DarksCheck.Core.RawReader.", unreadable.Line, StringComparison.Ordinal);
        LogCapture.Captured notFits = Assert.Single(capture.Events, e => e.Level == LogLevel.Warn && e.Exception is null);
        Assert.Equal("frame 3 " + junk + " is no FITS file (it does not begin with SIMPLE); its row says so", notFits.Message);
        string[] m = Timeless(capture);
        Assert.Contains(m, x => x.StartsWith("inventory done darks=1 biases=0 lights=0 errors=1 in ", StringComparison.Ordinal));
        Assert.Contains(m, x => x.StartsWith("inventory done darks=3 biases=0 lights=0 errors=1 in ", StringComparison.Ordinal));
    }

    /// <summary>
    /// A frame that cannot be decoded is a Warn line with its exception where the loop catches it,
    /// the only place its stack exists: the darks' loop in FrameStack and the lights' loop in
    /// LightsAnalyzer. The line says whether the metadata was read, and the run goes on (R-101, D4
    /// of ADR 0025).
    /// </summary>
    [Fact]
    public void R101_AFrameThatCannotBeDecodedIsLoggedWhereItIsCaught()
    {
        using SessionFixture s = new SessionFixture()
            .WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice })
            .WithLights(3, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, Stars = SyntheticLight.ThreeStars });
        string dark = Path.Combine(s.DarksDir, "DSC_0005.fit");
        File.WriteAllBytes(dark, File.ReadAllBytes(Path.Combine(s.DarksDir, "DSC_0001.fit")).Take(FitsTestData.BlockSize + 100).ToArray());
        string light = Path.Combine(s.LightsDir, "light_0004.fit");
        File.WriteAllBytes(light, File.ReadAllBytes(Path.Combine(s.LightsDir, "light_0001.fit")).Take(FitsTestData.BlockSize + 100).ToArray());
        using LogCapture capture = LogCapture.Start();

        DarkSeriesResult r = DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2));

        LogCapture.Captured darkLine = Assert.Single(capture.Events, e => e.Level == LogLevel.Warn && e.Logger == "FrameStack");
        Assert.IsType<InvalidDataException>(darkLine.Exception);
        Assert.Equal("frame 5/5 " + dark + " could not be decoded after #; its metadata was read; its row carries the error", LogFiles.ElapsedTime().Replace(darkLine.Message, "#"));
        Assert.Contains("   at DarksCheck.Core.FitsReader.LoadFull(", darkLine.Line, StringComparison.Ordinal);
        LogCapture.Captured lightLine = Assert.Single(capture.Events, e => e.Level == LogLevel.Warn && e.Logger == "LightsAnalyzer");
        Assert.IsType<InvalidDataException>(lightLine.Exception);
        Assert.Equal("frame 4/4 " + light + " could not be decoded after #; its metadata was read; its row carries the error", LogFiles.ElapsedTime().Replace(lightLine.Message, "#"));
        Assert.Contains(" frames darks=4/5 biases=none lights=3/4 partner=none errors=yes in ", Timeless(capture)[^1], StringComparison.Ordinal);
        Assert.Equal(4, r.Darks.Used);
    }

    /// <summary>
    /// A frame left out of a stack for its size or its settings, and a light of another size, are
    /// Warn lines in the log's own words, with the figures that kept them out; the rows keep the
    /// report's words (R-101).
    /// </summary>
    [Fact]
    public void R101_AFrameLeftOutForItsSizeOrSettingsIsAWarnLineInTheLogsWords()
    {
        using SessionFixture s = new SessionFixture()
            .WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice })
            .WithLights(2, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice });
        string small = Path.Combine(s.DarksDir, "DSC_0004.fit");
        File.WriteAllBytes(small, new SyntheticDark { Seed = 1, Width = 128, Height = 96 }.Build(SessionFixture.FirstStart.AddSeconds(480)));
        string iso = Path.Combine(s.DarksDir, "DSC_0005.fit");
        File.WriteAllBytes(iso, new SyntheticDark { Seed = 2 }.Build(SessionFixture.FirstStart.AddSeconds(600), 120, 800));
        string light = Path.Combine(s.LightsDir, "light_0003.fit");
        File.WriteAllBytes(light, new SyntheticLight { Seed = 3, Width = 128, Height = 96 }.Build(SessionFixture.FirstStart));
        using LogCapture capture = LogCapture.Start();

        DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2));

        string[] warned = capture.Events.Where(e => e.Level == LogLevel.Warn).Select(e => e.Logger + ": " + e.Message).Order(StringComparer.Ordinal).ToArray();
        string[] expected =
        [
            "FrameStack: frame 3 " + light + " left out: 128x96 instead of 256x192; its row says so",
            "FrameStack: frame 4/5 " + small + " left out: 128x96 instead of 256x192; its row says so",
            "FrameStack: frame 5/5 " + iso + " left out: ISO 800 and 120 s, the first frame ISO 400 and 120 s; its row says so",
        ];
        Assert.Equal(expected, warned);
        Assert.DoesNotContain(capture.Events, e => e.Level == LogLevel.Warn && e.Exception is not null);
    }

    /// <summary>
    /// A run that throws leaves its start line and, from its clock, the stages it reached; the
    /// exception is its caller's line (R-101). No dark decodes, so the run throws after the darks'
    /// stage.
    /// </summary>
    [Fact]
    public void R101_ARunThatThrowsLeavesItsStartLineAndTheStagesItReached()
    {
        using SessionFixture s = new SessionFixture().WithDarks(1);
        for (int i = 1; i <= 3; i++)
        {
            File.WriteAllBytes(Path.Combine(s.DarksDir, $"DSC_000{i}.fit"), new byte[100]);
        }

        using LogCapture capture = LogCapture.Start();

        Assert.Throws<InvalidOperationException>(() => DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2)));

        string[] m = Timeless(capture);
        Assert.StartsWith("darks start folder=" + s.Root + " ", m[0], StringComparison.Ordinal);
        Assert.Contains("stack of 3 files: none decoded; 0 decoded, 3 failed, 0 skipped in #", m);
        Assert.Equal("darks ended without an outcome after # (inventory #, darks #)", m[^1]);
    }

    /// <summary>
    /// A group that fails is a Warn line with its exception where the run catches it, and the run
    /// goes on to its done line: biases of another size leave the dark current out, a partner that
    /// does not exist leaves the partner out (R-101).
    /// </summary>
    [Fact]
    public void R101_AGroupThatFailsIsLoggedWithItsExceptionAndTheRunGoesOn()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i }, biases: 2,
            bias: i => new SyntheticDark { Seed = 500 + i, Width = 128, Height = 96 });
        using TempWorkspace ws = new();
        string missing = Path.Combine(ws.Dir, "missing");
        using LogCapture capture = LogCapture.Start();

        DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2, Against: missing));

        List<LogCapture.Captured> warned = capture.Events.Where(e => e.Level == LogLevel.Warn && e.Logger == "DarkSeriesRun").ToList();
        Assert.Equal(2, warned.Count);
        Assert.Equal("the dark current could not be measured; the biases are left out of the run", warned[0].Message);
        Assert.IsType<InvalidOperationException>(warned[0].Exception);
        Assert.Equal("the partner " + missing + " could not be used; the report says why", warned[1].Message);
        Assert.IsType<DirectoryNotFoundException>(warned[1].Exception);
        Assert.Contains(" frames darks=3/3 biases=none lights=none partner=none errors=yes in ", Timeless(capture)[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// The core's lines are English whatever the language of the run: the same run writes the same
    /// messages under English and under Romanian, the times and the working set aside (R-101). The
    /// exceptions' own messages, in the run's language as the user saw them, are on the lines below
    /// a message, not in it.
    /// </summary>
    [Fact]
    public void R101_TheLogIsTheSameInBothLanguages()
    {
        using SessionFixture s = new SessionFixture()
            .WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice }, biases: 2)
            .WithLights(2, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, Stars = SyntheticLight.ThreeStars });
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0005.fit"), new byte[100]);

        string[] en = CoreMessages(s.Root, UiLanguage.English);
        string[] ro = CoreMessages(s.Root, UiLanguage.Romanian);

        Assert.Contains(en, x => x.StartsWith("frame 5/5 ", StringComparison.Ordinal));
        Assert.Equal(en, ro);
    }

    /// <summary>
    /// The export tells its caller each file as soon as that file is written, so the console and
    /// the window write one line per file with its size, and an export that fails part-way names
    /// the files it did write (R-101).
    /// </summary>
    [Fact]
    public void R101_TheExportTellsEachFileAsSoonAsItIsWritten()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i }, biases: 2);
        using TempWorkspace ws = new();
        DarkSeriesResult r = DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2));
        List<string> told = [];

        IReadOnlyList<string> written = DarkOutputs.WriteAll(r, ws.Dir, path =>
        {
            Assert.True(File.Exists(path), path + " was told before it was written");
            told.Add(path);
        });

        Assert.Equal(written, told);
        Assert.Matches(@"^wrote .+darkscheck-report\.txt \(\d+\.\d KiB\)$", LogText.Wrote(written[0]));
    }

    /// <summary>
    /// The figures of a record follow one rule: every public property by its name in camelCase, a
    /// number with six significant digits, a flag as yes or no, an enum by its name, a missing value
    /// as none, a record in parentheses, a list whole up to twelve items, else its first and last
    /// and its count (ADR 0025).
    /// </summary>
    [Fact]
    public void R101_TheFiguresOfARecordFollowOneRule()
    {
        Probe probe = new("x", null, true, Verdict.Warning, new Pair(0.123456789, 3), [1, 2], Enumerable.Range(0, 20).Select(i => (double)i).ToArray(),
            new DateTime(2026, 8, 13, 0, 48, 53, DateTimeKind.Utc));

        Assert.Equal("name=x missing=none flag=yes verdict=Warning pair=(a=0.123457 b=3) few=[1, 2] many=[0, ..., 19] (20 items) when=2026-08-13 00:48:53",
            LogText.Figures(probe));
        Assert.Equal("flag=yes when=2026-08-13 00:48:53", LogText.Figures(probe, "Name", "Missing", "Verdict", "Pair", "Few", "Many"));
        Assert.Equal("none", LogText.Figures(null));
    }

    /// <summary>
    /// A raw without a Bayer mosaic (mono, Leaf, X-Trans) reaches libraw's Debug line with a null
    /// CFA order: the line names it <c>none</c> (LightsCheck's review of 2026-10-30, whose line threw
    /// there and made every such frame unreadable at the default level).
    /// </summary>
    [Fact]
    public void R101_LibrawsLineNamesNoCfaForARawWithoutABayerMosaic()
    {
        string line = RawReader.FiguresLine(@"D:\xtrans\DSCF0001.RAF", 6000, 4000, 6048, 4032, 0, 0, 12096, 9, "RGBG", null,
            16383, 1024, [1024, 1024, 1024, 1024], 1024);

        Assert.Contains(" filters 0x00000009 cdesc RGBG cfa none maximum 16383 black 1024 ", line, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- R-101: the console

    /// <summary>
    /// A console run that fails keeps its one line on stderr, as before the log, and writes the
    /// failure at Error with the exception's type and stack, after the mode and the options; the
    /// run's clocks say where it stopped (R-101).
    /// </summary>
    [Fact]
    public void R101_TheConsoleLogsTheModeAndAFailureWithItsStack_AndStderrKeepsOneLine()
    {
        using TempWorkspace ws = new();
        string missing = Path.Combine(ws.Dir, "missing");
        using LogCapture capture = LogCapture.Start();

        (int code, _, string stderr) = Cli.Run(missing, "--threads", "2");

        Assert.Equal(1, code);
        Assert.Equal("EROARE: nu gasesc folderul " + missing + "\n", stderr);
        Assert.Contains("mode=darks input=" + missing + " against=none lights=none out=none threads=2 maxFrames=60 lang=ro", capture.Messages);
        LogCapture.Captured failed = Assert.Single(capture.Events, e => e.Level == LogLevel.Error);
        Assert.Equal("Cli", failed.Logger);
        Assert.Equal("run failed: mode=darks", failed.Message);
        Assert.IsType<DirectoryNotFoundException>(failed.Exception);
        Assert.Contains("   at DarksCheck.Core.SessionFolder.Open(", failed.Line, StringComparison.Ordinal);
        Assert.Contains("darks ended without an outcome after #", Timeless(capture));
    }

    /// <summary>A refused argument is a Warn line with the message the user was shown, in the language it was shown in (R-101).</summary>
    [Fact]
    public void R101_ARefusedOptionIsLoggedAtWarnAsShown()
    {
        using LogCapture capture = LogCapture.Start();

        (int threadsCode, _, string threadsError) = Cli.Run("C:/any", "--threads", "x");
        (int langCode, _, string langError) = Cli.Run("C:/any", "--lang", "fr");

        Assert.Equal(1, threadsCode);
        Assert.Equal(1, langCode);
        string[] warned = capture.Events.Where(e => e.Level == LogLevel.Warn && e.Logger == "Cli").Select(e => e.Message).ToArray();
        Assert.Equal(2, warned.Length);
        Assert.StartsWith("refused the arguments: ", warned[0], StringComparison.Ordinal);
        Assert.Equal("EROARE: " + warned[0]["refused the arguments: ".Length..] + "\n", threadsError);
        Assert.Equal("refused: --lang expects en or ro, got 'fr'", warned[1]);
        Assert.Equal("ERROR: --lang expects en or ro, got 'fr'\n", langError);
    }

    /// <summary>
    /// The log adds nothing to the console's streams: with the capture open, a darks run prints the
    /// golden text on stdout and only the progress on stderr, while the log holds the run (R-101).
    /// </summary>
    [Fact]
    public void R101_TheConsoleAddsNothingToStdoutOrStderr()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticDark.OnePerLevel, HotFactor = 0.9 + 0.2 * i / 3 }, biases: 3);
        using LogCapture capture = LogCapture.Start();

        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--threads", "2");

        Assert.True(capture.Events.Count > 50, "the log holds " + capture.Events.Count.ToString(CultureInfo.InvariantCulture) + " lines");
        Assert.Equal(File.ReadAllText(Path.Combine(GoldenOutputTests.GoldenDir, "darks.txt")).ReplaceLineEndings("\n"),
            GoldenOutputTests.Scrub(stdout, s.Root) + "--- stderr ---\n(progresul, omis)\n--- exit " + code.ToString(CultureInfo.InvariantCulture) + " ---\n");
        Assert.All(stderr.Split('\n').Where(l => l.Trim().Length > 0), l => Assert.Matches(@"^    .+: \d+ / \d+$", l));
    }

    [Fact]
    public void R101_EveryFileWrittenIsOneLineWithItsSize()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i }, biases: 2);
        using TempWorkspace ws = new();
        using LogCapture capture = LogCapture.Start();

        Assert.Equal(0, Cli.Run(s.Root, "--threads", "2", "--out", ws.Dir).Code);

        string[] wrote = capture.Events.Where(e => e.Logger == "Cli" && e.Message.StartsWith("wrote ", StringComparison.Ordinal)).Select(e => e.Message).ToArray();
        string[] files = Directory.GetFiles(ws.Dir);
        Assert.Equal(files.Length, wrote.Length);
        Assert.All(wrote, m => Assert.Matches(@"^wrote .+ \(\d+\.\d KiB\)$", m));
        Assert.All(files, f => Assert.Contains(wrote, m => m.StartsWith("wrote " + f + " (", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The real console starts and closes the log itself (R-101), which only its Main does and no
    /// in-process test reaches: its first line names it, its version and its arguments, its last
    /// line the exit code. Its lines are found by its process id from its first line, which names a
    /// working folder of its own, in the log beside the copy of the executable in the test build
    /// folder, which other tests' processes share (LightsCheck's test).
    /// </summary>
    [Fact]
    public void R101_TheRealConsoleWritesItsFirstAndItsLastLine()
    {
        (int pid, int code, List<string> lines) = RunTheConsole(null, "--help", "--lang", "en");

        Assert.Equal(0, code);
        Assert.True(lines.Count >= 3, "the console wrote " + lines.Count.ToString(CultureInfo.InvariantCulture) + " lines");
        Assert.Contains(" INFO  [" + pid.ToString(CultureInfo.InvariantCulture) + ":", lines[0], StringComparison.Ordinal);
        Assert.Contains("] Process darkscheck " + VersionInfo.Value + " starting; exe=" + ConsoleExe + "; args=[\"--help\", \"--lang\", \"en\"]; ",
            lines[0], StringComparison.Ordinal);
        Assert.Contains("; level=Debug; ", lines[0], StringComparison.Ordinal);
        Assert.Contains(lines, l => l.EndsWith("] Cli mode=help", StringComparison.Ordinal));
        Assert.Matches(@"\] Process exit 0 after \d+\.\d s$", lines[^1]);
    }

    /// <summary>The log writes an elapsed time in two forms only: milliseconds, or seconds with one decimal; never whole seconds.</summary>
    [Fact]
    public void TheElapsedTimeHasTwoForms_MillisecondsAndSecondsWithOneDecimal()
    {
        Assert.Equal("12.9 s", LogText.Seconds(TimeSpan.FromMilliseconds(12900)));
        Assert.Equal("0.8 s", LogText.Seconds(TimeSpan.FromMilliseconds(800)));
        Assert.Equal("5 ms", LogText.Ms(5));
    }

    // ---------------------------------------------------------------- R-102: the level

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
    public void R102_TheLevelComesFromTheEnvironmentVariable(string? value, string level)
    {
        Assert.Equal(LogLevel.FromString(level), LogSetup.LevelFrom(value));
    }

    /// <summary>
    /// The real console takes its level from <c>DARKSCHECK_LOG</c>, the name R-102 and the README
    /// give, written out here so a renamed constant fails: <c>off</c> writes no line, <c>trace</c>
    /// says so in the first line (LightsCheck's review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R102_TheRealConsoleTakesItsLevelFromDARKSCHECK_LOG()
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
    public void R102_OffCreatesNeitherTheFolderNorAFile()
    {
        using TempWorkspace ws = new();

        LogPlace off = LogSetup.Plan(Exe, Path.Combine(ws.Dir, "darkscheck.exe"), ws.Dir, Path.Combine(ws.Dir, "fallback"), "off");

        Assert.Null(off.Folder);
        Assert.Empty(Directory.GetFileSystemEntries(ws.Dir));
        Assert.Empty(LogSetup.Build(Exe, Path.Combine(ws.Dir, "log"), LogLevel.Off).AllTargets);

        LogPlace trace = LogSetup.Plan(Exe, Path.Combine(ws.Dir, "darkscheck.exe"), ws.Dir, Path.Combine(ws.Dir, "fallback"), "trace");
        Assert.Equal(LogLevel.Trace, trace.Level);
        Assert.Equal(Path.Combine(ws.Dir, "log"), trace.Folder);
    }

    /// <summary>
    /// Trace adds every card of a FITS frame read whole, below the keywords that decide the reading,
    /// which are at Debug; a metadata read keeps no cards, so it adds none (R-102).
    /// </summary>
    [Fact]
    public void R102_TraceHoldsEveryFitsHeaderCard()
    {
        using TempWorkspace ws = new();
        string dark = ws.WriteFile("dark.fit", SessionFixture.Frame(SessionFixture.FirstStart, 120, 400));
        using (LogCapture capture = LogCapture.Start())
        {
            ImageLoader.Load(dark);

            string[] cards = capture.Events.Where(e => e.Level == LogLevel.Trace && e.Logger == "ImageLoader").Select(e => e.Message).ToArray();
            Assert.Contains("card BITPIX  =                   16", cards);
            Assert.Contains(cards, c => c.StartsWith("card EXPTIME = ", StringComparison.Ordinal));
            Assert.DoesNotContain(capture.Events, e => e.Level == LogLevel.Debug && e.Message.StartsWith("card ", StringComparison.Ordinal));
            Assert.Contains(capture.Events, e => e.Level == LogLevel.Debug && e.Message.StartsWith("header of " + dark + ": ", StringComparison.Ordinal)
                && e.Message.Contains(" BITPIX=16 ", StringComparison.Ordinal) && e.Message.Contains(" EXPTIME=120 ", StringComparison.Ordinal));
        }

        using (LogCapture capture = LogCapture.Start())
        {
            ImageLoader.LoadInfo(dark);

            Assert.DoesNotContain(capture.Events, e => e.Level == LogLevel.Trace);
            Assert.Contains(capture.Events, e => e.Level == LogLevel.Debug && e.Message.StartsWith("header of " + dark + ": ", StringComparison.Ordinal));
        }
    }

    // ---------------------------------------------------------------- R-103: two processes

    /// <summary>
    /// Two processes of one executable (two windows) write into the same file at full speed, and
    /// every line of both arrives whole, once, past the 1 MiB roll too (R-100, R-103), on the
    /// <c>net8.0</c> build of the atomic target, the console's; the window's tests run the same
    /// check on its own build (<see cref="LogFiles.TwoWritersKeepEveryLine"/>).
    /// </summary>
    [Fact]
    public void R103_TwoProcessesWritingAtFullSpeedKeepEveryLine()
    {
        using TempWorkspace ws = new();

        LogFiles.TwoWritersKeepEveryLine(Path.Combine(ws.Dir, "log"), "DarksCheck.Wpf", _output);
    }

    // ---------------------------------------------------------------- invariants of the repository

    /// <summary>
    /// Only the two entry points configure the log for a user's run, and nothing but LogSetup (for
    /// them) and LogCapture (for the tests) sets NLog's configuration at all: a library use logs
    /// into nothing and every test into the capture (ADR 0025).
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
    /// The log's own words are English whatever the language (R-101): no log call composes its
    /// message through the T(english, romanian) pair, whichever logger it goes through and however
    /// many lines the call spans. And every logger is declared one way, a <c>Logger</c> field or
    /// property named <c>Log</c> taken from <c>LogManager.GetLogger</c> with <c>using NLog</c>: a
    /// <c>LogManager.Get...</c> call of any other shape, a <c>Logger</c> variable of another name
    /// and a qualified NLog name are refused, so the first check, which scans the calls of names
    /// ending in <c>log</c>, <c>Log(level, ...)</c> among them, sees every call (the branch's two
    /// reviews: before them a logger of another name, or one from <c>GetCurrentClassLogger</c>,
    /// passed unseen).
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
                bool inline = lines[i].Contains("LogManager.Get", StringComparison.Ordinal) && !Declaration().IsMatch(lines[i]);
                bool qualified = lines[i].Contains("NLog.Logger", StringComparison.Ordinal) || lines[i].Contains("NLog.LogManager", StringComparison.Ordinal);
                bool renamed = LoggerVariable().Match(lines[i]) is { Success: true } v && v.Groups[1].Value != "Log";
                if (inline || qualified || renamed)
                {
                    declarations.Add(Rel(f) + ":" + (i + 1).ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        Assert.Empty(offenders);
        Assert.Empty(declarations);
    }

    /// <summary>
    /// The numbers and the report's words stay pure (ADR 0025): of Analysis/ only the two decoding
    /// loops' files hold a logger (D4), of Imaging/ only the two files adapted from LightsCheck
    /// (D3: FitsReader.cs and the other copies take no line), and nothing in Report/, Language/ or
    /// Thresholds.cs names NLog; the runs log what the analyzers return.
    /// </summary>
    [Fact]
    public void NothingThatComputesOrWordsTheReportLogs()
    {
        string[] loops = ["FrameStack.cs", "LightsAnalyzer.cs"];
        string[] adapted = ["ImageLoader.cs", "RawReader.cs"];
        string[] analysis = CsSources("src/DarksCheck.Core/Analysis").ToArray();
        string[] imaging = CsSources("src/DarksCheck.Core/Imaging").ToArray();
        string[] pure = analysis.Where(f => !loops.Contains(Path.GetFileName(f)))
            .Concat(imaging.Where(f => !adapted.Contains(Path.GetFileName(f))))
            .Concat(CsSources("src/DarksCheck.Core/Report", "src/DarksCheck.Core/Language"))
            .Append(Path.Combine(TestData.FindRepoRoot(), "src", "DarksCheck.Core", "Thresholds.cs"))
            .ToArray();

        Assert.True(pure.Length > 25, "the folders were not found");
        Assert.All(pure, f => Assert.DoesNotMatch(NamesNLog(), File.ReadAllText(f)));
        // and the two exceptions of each folder are the files that do log, no more
        Assert.Equal(loops, analysis.Where(f => NamesNLog().IsMatch(File.ReadAllText(f))).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal));
        Assert.Equal(adapted, imaging.Where(f => NamesNLog().IsMatch(File.ReadAllText(f))).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal));
    }

    // ---------------------------------------------------------------- helpers

    [GeneratedRegex(@"LogSetup\.(Apply|Close)\(")]
    private static partial Regex ApplyOrClose();

    [GeneratedRegex(@"LogManager\.(Configuration\s*=[^=]|Setup\()")]
    private static partial Regex SetsConfiguration();

    /// <summary>A call that composes a log line's text: a logger's, or a run clock's start, lap or outcome.</summary>
    [GeneratedRegex(@"\b\w*[Ll]og\.(Trace|Debug|Info|Warn|Error|Fatal|Log)\(|\bRunClock\.Start\(|\b\w*[Cc]lock\??\.(Lap|Done)\(")]
    private static partial Regex LogCall();

    /// <summary>A variable, field or property of type <c>Logger</c> given a value, and its name.</summary>
    [GeneratedRegex(@"\bLogger\s+(\w+)\s*=")]
    private static partial Regex LoggerVariable();

    [GeneratedRegex(@"\)\s*;")]
    private static partial Regex CallEnd();

    [GeneratedRegex(@"\bT\(")]
    private static partial Regex Translation();

    [GeneratedRegex(@"static readonly Logger Log = LogManager\.GetLogger\(|static Logger Log => LogManager\.GetLogger\(")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"\bNLog\b|\bLogger\b|\bLogManager\b")]
    private static partial Regex NamesNLog();

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

    /// <summary>A record for the figures' rule: every kind of value it writes.</summary>
    private sealed record Probe(string? Name, double? Missing, bool Flag, Verdict Verdict, Pair Pair, IReadOnlyList<int> Few, IReadOnlyList<double> Many, DateTime When);

    /// <summary>A record held by <see cref="Probe"/>.</summary>
    private sealed record Pair(double A, int B);

    /// <summary>A frame a stack decoded, its time and its working set.</summary>
    [GeneratedRegex(@"^decoded frame \d/\d .+\.fit: 256x192 black=none white=65535 norm=\S+ in #; working set \d+\.\d MiB$")]
    private static partial Regex DecodedLine();

    /// <summary>A dark's figures, its four planes first.</summary>
    [GeneratedRegex(@"^dark \d DSC_000\d\.fit: planes=\[\(name=R median=")]
    private static partial Regex DarkLine();

    /// <summary>A light measured, its time, its working set and its figures.</summary>
    [GeneratedRegex(@"^light \d/3 .+light_000\d\.fit measured in #; working set \d+\.\d MiB; assumedSettings=no skyMedian=\[")]
    private static partial Regex LightLine();

    /// <summary>The working set as the log writes it.</summary>
    [GeneratedRegex(@"working set \d+\.\d MiB")]
    private static partial Regex WorkingSet();

    /// <summary>The console's executable as the build copies it beside the tests.</summary>
    private static string ConsoleExe => Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "darkscheck.exe" : "darkscheck");

    /// <summary>
    /// Runs the real console, without a window, its level variable as given: removed for null,
    /// whatever the developer's own environment says. It runs in a working folder of its own, which
    /// its first line names, and its lines are read while this test still holds its handle, so no
    /// other process can be given its id meanwhile and an older process's lines under the same id
    /// stay out (LightsCheck's second review of 2026-10-30).
    /// </summary>
    private static (int Pid, int Code, List<string> Lines) RunTheConsole(string? level, params string[] args)
    {
        using TempWorkspace cwd = new();
        ProcessStartInfo start = new(ConsoleExe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = cwd.Dir };
        foreach (string a in args)
        {
            start.ArgumentList.Add(a);
        }

        start.Environment.Remove("DARKSCHECK_LOG");
        if (level is not null)
        {
            start.Environment["DARKSCHECK_LOG"] = level;
        }

        using Process p = Process.Start(start)!;
        Task<string> errors = p.StandardError.ReadToEndAsync();
        p.StandardOutput.ReadToEnd();
        Assert.True(p.WaitForExit(30_000), "darkscheck did not end within 30 s");
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
        foreach (string f in LogFiles.FilesOf(folder, "darkscheck"))
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

    /// <summary>A capture's messages, each elapsed time written as <c>#</c>.</summary>
    private static string[] Timeless(LogCapture capture) => capture.Messages.Select(x => LogFiles.ElapsedTime().Replace(x, "#")).ToArray();

    /// <summary>The messages one darks run writes in one language, times and working sets made equal, in a fixed order.</summary>
    private static string[] CoreMessages(string root, UiLanguage language)
    {
        using IDisposable scope = Ui.Use(language);
        using LogCapture capture = LogCapture.Start();
        DarkSeriesRun.Run(root, new DarkOptions(Threads: 2));
        return Timeless(capture)
            .Select(x => WorkingSet().Replace(x, "working set #"))
            .Order(StringComparer.Ordinal)
            .ToArray();
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
