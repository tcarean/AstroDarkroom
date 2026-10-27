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

using System.Text;
using System.Text.RegularExpressions;
using NLog;
using NLog.Targets;
using Xunit.Abstractions;

namespace FlatCheck.Tests;

/// <summary>
/// The diagnostic log (R-109 to R-111, ADR 0070): where the files go and how they roll, what a
/// run writes, the level. The file tests run the applications' configuration
/// (<see cref="LogSetup.Build"/>) in a private <see cref="LogFactory"/>, so they touch neither
/// the test assembly's log (<see cref="LogCapture"/>) nor the tests beside them.
/// </summary>
public class LoggingTests
{
    readonly ITestOutputHelper output;

    /// <summary>The runner's output, for the measured file lists.</summary>
    public LoggingTests(ITestOutputHelper output) => this.output = output;

    const int Size = 256;

    static readonly string Root = TestData.RepoDir();

    static readonly Regex LinePrefix = new(
        @"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{4} (TRACE|DEBUG|INFO |WARN |ERROR|FATAL) \[\d+:\d+\] \S+ ",
        RegexOptions.Compiled);

    /// <summary>The files one executable left in a folder, oldest first by NLog's own
    /// numbering: the bare name, then _01, _02 and so on.</summary>
    static string[] FilesOf(string folder, string executable) =>
        Directory.GetFiles(folder, executable + "*.log")
                 .Where(f => Regex.IsMatch(Path.GetFileName(f), "^" + Regex.Escape(executable) + @"(_\d+)?\.log$", RegexOptions.IgnoreCase))
                 .OrderBy(f => Path.GetFileNameWithoutExtension(f).Length)
                 .ThenBy(f => f, StringComparer.Ordinal)
                 .ToArray();

    /// <summary>A private factory on <paramref name="config"/>, standing for one process.</summary>
    static LogFactory Factory(NLog.Config.LoggingConfiguration config)
    {
        var factory = new LogFactory();
        factory.Configuration = config;
        return factory;
    }

    /// <summary>Writes <paramref name="bytes"/> of lines through one executable's
    /// configuration and closes it.</summary>
    static void WriteThrough(string folder, string executable, long bytes, string tag)
    {
        using var factory = Factory(LogSetup.Build(executable, folder, LogLevel.Debug));
        Logger log = factory.GetLogger("Rolling");
        string payload = new('x', 4000);
        long lines = bytes / (payload.Length + 100) + 1;
        for (long i = 0; i < lines; i++) log.Info($"{tag} {i:D6} {payload}");
        log.Info($"{tag} last");
        factory.Shutdown();
    }

    // ---------------------------------------------------------------- R-109: the files

    [Fact]
    public void R109_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName()
    {
        using var ws = new TempWorkspace();
        string fallback = Path.Combine(ws.Dir, "fallback");

        string folder = LogSetup.ResolveFolder(ws.Dir, fallback, "flatcheck", out bool fellBack);

        Assert.False(fellBack);
        Assert.Equal(Path.Combine(ws.Dir, "log"), folder);
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));   // the probe is gone
        Assert.False(Directory.Exists(fallback));

        using (var factory = Factory(LogSetup.Build("flatcheck", folder, LogLevel.Debug)))
        {
            factory.GetLogger("Test").Info("hello");
            factory.Shutdown();
        }
        string file = Path.Combine(folder, "flatcheck.log");
        Assert.Equal(new[] { file }, Directory.GetFiles(folder));
        byte[] bytes = File.ReadAllBytes(file);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no BOM");
        string line = Encoding.UTF8.GetString(bytes).TrimEnd('\r', '\n');
        Assert.Matches(LinePrefix, line);
        Assert.EndsWith(" INFO  [" + Environment.ProcessId + ":" + Environment.CurrentManagedThreadId + "] Test hello", line);
    }

    [Fact]
    public void R109_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback()
    {
        using var ws = new TempWorkspace();
        // A FILE named log where the folder should be: the folder cannot be created.
        ws.WriteFile("log", new byte[] { 1 });
        string fallbackRoot = Path.Combine(ws.Dir, "LocalAppData");

        string folder = LogSetup.ResolveFolder(ws.Dir, fallbackRoot, "flatcheck", out bool fellBack);

        Assert.True(fellBack);
        Assert.Equal(Path.Combine(fallbackRoot, "FlatCheck", "log"), folder);
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
        Assert.Contains("(fallback", LogSetup.StartupLine("flatcheck", Array.Empty<string>(), folder, true, LogLevel.Debug));
    }

    [Fact]
    public void R109_TheLogRollsAt1MiBAndKeepsAtMost5Files()
    {
        using var ws = new TempWorkspace();
        string folder = Path.Combine(ws.Dir, "log");

        WriteThrough(folder, "flatcheck", 8 * LogSetup.FileSizeLimit, "console");

        string[] files = FilesOf(folder, "flatcheck");
        foreach (string f in files)
            output.WriteLine($"{Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        Assert.Equal(Directory.GetFiles(folder).Length, files.Length);   // nothing else is left
        Assert.Equal(LogSetup.FilesPerExecutable, files.Length);
        Assert.All(files, f => Assert.InRange(new FileInfo(f).Length, 1, LogSetup.FileSizeLimit + 4200));
        // The newest file is the one with the highest number, and it holds the last line.
        Assert.Equal(new[] { "flatcheck_03.log", "flatcheck_04.log", "flatcheck_05.log", "flatcheck_06.log", "flatcheck_07.log" },
                     files.Select(Path.GetFileName));
        Assert.EndsWith("console last", File.ReadAllLines(files[^1])[^1]);

        // The next process goes on in the newest file: the same five names, the newest grown,
        // nothing started and nothing deleted (ADR 0070 rejected one file per process).
        long newestBefore = new FileInfo(files[^1]).Length;
        WriteThrough(folder, "flatcheck", 1000, "next");
        string[] after = FilesOf(folder, "flatcheck");
        foreach (string f in after)
            output.WriteLine($"after a second process: {Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        Assert.Equal(files.Select(Path.GetFileName), after.Select(Path.GetFileName));
        Assert.True(new FileInfo(after[^1]).Length > newestBefore, "the newest file did not grow");
        Assert.EndsWith("next last", File.ReadAllLines(after[^1])[^1]);
    }

    [Fact]
    public void R109_OneExecutablesRollingLeavesTheOthersFilesAlone()
    {
        using var ws = new TempWorkspace();
        string folder = Path.Combine(ws.Dir, "log");
        // The window's log first, rolled once, then the console's, rolled many times, in the
        // same folder: FlatCheck.Wpf begins with the same letters as flatcheck, in another case.
        WriteThrough(folder, "FlatCheck.Wpf", 3 * LogSetup.FileSizeLimit / 2, "window");
        string[] window = FilesOf(folder, "FlatCheck.Wpf");
        Assert.Equal(2, window.Length);

        WriteThrough(folder, "flatcheck", 8 * LogSetup.FileSizeLimit, "console");

        Assert.Equal(window, FilesOf(folder, "FlatCheck.Wpf"));
        Assert.EndsWith("window last", File.ReadAllLines(window[^1])[^1]);
        Assert.Equal(LogSetup.FilesPerExecutable, FilesOf(folder, "flatcheck").Length);
    }

    /// <summary>The lines two writers left in a folder, whole, by writer: the numbers each
    /// one's lines carry, and the count of lines that are neither writer's whole line.</summary>
    static (List<int> A, List<int> B, int Broken, string Order) TwoWritersLines(string folder, string executable)
    {
        var a = new List<int>();
        var b = new List<int>();
        var order = new StringBuilder();
        int broken = 0;
        var whole = new Regex(@" (A|B) (a|b) (\d{5}) ([AB]+)$");
        foreach (string line in FilesOf(folder, executable).SelectMany(File.ReadAllLines))
        {
            Match m = whole.Match(line);
            bool ok = LinePrefix.IsMatch(line) && m.Success && m.Groups[1].Value.ToLowerInvariant() == m.Groups[2].Value;
            int n = ok ? int.Parse(m.Groups[3].Value) : -1;
            // the payload is the writer's letter, as many as the line's number says
            ok = ok && m.Groups[4].Value == new string(m.Groups[1].Value[0], 140 + n % 37);
            if (!ok) { broken++; continue; }
            (m.Groups[1].Value == "A" ? a : b).Add(n);
            order.Append(m.Groups[1].Value);
        }
        return (a, b, broken, order.ToString());
    }

    /// <summary>
    /// Two processes of one executable (two windows) write into the same file at full speed, and
    /// every line of both arrives whole, once, past the 1 MiB roll too (R-109, R-112). Two
    /// factories stand for the two processes; each opens the file on its own. NLog's plain
    /// target failed this: closed between lines it lost about 1 % of them, kept open one writer
    /// lost all of its (ADR 0070).
    /// </summary>
    [Fact]
    public void R112_TwoProcessesWritingAtFullSpeedKeepEveryLine()
    {
        using var ws = new TempWorkspace();
        string folder = Path.Combine(ws.Dir, "log");
        using var a = Factory(LogSetup.Build("FlatCheck.Wpf", folder, LogLevel.Debug));
        using var b = Factory(LogSetup.Build("FlatCheck.Wpf", folder, LogLevel.Debug));
        const int Lines = 4000;   // about 1.6 MiB together: the writers cross the roll

        // Two threads of their own, released together: Parallel.Invoke may run the second
        // action after the first on a busy pool, and then nothing would overlap.
        using var together = new Barrier(2);
        var ta = new Thread(() => { var log = a.GetLogger("A"); together.SignalAndWait(); for (int i = 0; i < Lines; i++) log.Info($"a {i:D5} " + new string('A', 140 + i % 37)); });
        var tb = new Thread(() => { var log = b.GetLogger("B"); together.SignalAndWait(); for (int i = 0; i < Lines; i++) log.Info($"b {i:D5} " + new string('B', 140 + i % 37)); });
        ta.Start();
        tb.Start();
        ta.Join();
        tb.Join();
        a.Shutdown();
        b.Shutdown();

        string[] files = FilesOf(folder, "FlatCheck.Wpf");
        foreach (string f in files)
            output.WriteLine($"{Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        var (fromA, fromB, broken, order) = TwoWritersLines(folder, "FlatCheck.Wpf");
        output.WriteLine($"whole lines: {fromA.Count} from A, {fromB.Count} from B; broken: {broken}");
        Assert.Equal(Directory.GetFiles(folder).Length, files.Length);
        Assert.InRange(files.Length, 2, LogSetup.FilesPerExecutable);
        Assert.Equal(0, broken);
        Assert.Equal(Enumerable.Range(0, Lines), fromA.OrderBy(n => n));
        Assert.Equal(Enumerable.Range(0, Lines), fromB.OrderBy(n => n));
        // The writers did overlap: their lines interleave in the file, both ways round.
        Assert.Contains("AB", order);
        Assert.Contains("BA", order);
    }

    [Theory]
    // Windows paths with forward slashes, which Windows accepts and Unix can split too: the
    // test project is portable, and a backslash is not a separator there.
    [InlineData("C:/Tools/flatcheck.exe", "flatcheck", "flatcheck")]
    [InlineData("C:/Tools/flatcheck-2.3.exe", "flatcheck", "flatcheck-2.3")]
    [InlineData("C:/Program Files/dotnet/dotnet.exe", "flatcheck", "flatcheck")]
    [InlineData("/usr/share/dotnet/dotnet", "flatcheck", "flatcheck")]
    [InlineData(null, "FlatCheck.Wpf", "FlatCheck.Wpf")]
    public void R109_TheFilesBeginWithTheExecutablesName(string? processPath, string entry, string expected)
    {
        Assert.Equal(expected, LogSetup.ExecutableName(processPath, entry));
    }

    // ---------------------------------------------------------------- R-110: what is written

    [Fact]
    public void R110_TheFirstLineCarriesTheFacts()
    {
        string line = LogSetup.StartupLine("flatcheck", new[] { @"D:\flats\pp flat.fit", "--lang", "ro" },
                                           @"D:\tools\log", false, LogLevel.Debug);

        Assert.StartsWith("flatcheck " + VersionInfo.Value + " starting; exe=", line);
        Assert.Contains(@"; args=[""D:\flats\pp flat.fit"", ""--lang"", ""ro""]; cwd=", line);
        Assert.Contains(@"; log=D:\tools\log; level=Debug; os=", line);
        Assert.DoesNotContain("fallback", line);
        foreach (string part in new[] { "; cpus=", "; memory=", " GiB; runtime=", "; culture=", "; tz=", " (UTC" })
            Assert.Contains(part, line);
        Assert.EndsWith("; pid=" + Environment.ProcessId, line);
    }

    [Fact]
    public void R110_AStructureRunLogsTheFileTheVerdictAndTheTimings()
    {
        using var ws = new TempWorkspace();
        string flat = GoldenOutputTests.Flat(ws, "flat.fit", FitsTestData.Radial(Size, Size), Size);
        using var capture = LogCapture.Start();

        int exit = FlatCheckCli.Run(new[] { flat, "-o", ws.PathTo("map.png") }, new StringWriter(), new StringWriter());

        Assert.Equal(0, exit);
        string text = capture.Text;
        output.WriteLine(text);
        Assert.Contains("Cli mode=structure input=" + flat, text);
        Assert.Matches(@"ImageLoader loaded .+flat\.fit \(0\.1 MiB\) reader=fits 256x256 planes=1 bitpix=16 ", text);
        Assert.Matches(@"StructureRun structure done verdict=PERFECT in \d+ ms \(load \d+ ms, analyze \d+ ms, report \d+ ms, map \d+ ms\)", text);
        Assert.Contains("residualPtpPct=", text);
        Assert.Matches(@"Cli wrote .+map\.png \(\d+(\.\d)? KiB\)", text);
        Assert.All(capture.Events, e => Assert.Matches(LinePrefix, e.Line));
    }

    /// <summary>
    /// A channel without a colour (a CFA file without <c>BAYERPAT</c>, or a mono frame) is
    /// named in the log by its position, as the report names it; before, the analyzer's
    /// <c>'\0'</c> went into the file and <c>grep</c> took the whole log for binary (the review
    /// of 2026-10-27, A4).
    /// </summary>
    [Fact]
    public void R110_AChannelWithoutAColourIsNamedByItsPosition_NoNulByteInTheLog()
    {
        using var ws = new TempWorkspace();
        string raw = GoldenOutputTests.Flat(ws, "raw.fit", FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000)), Size);
        using var capture = LogCapture.Start();

        FlatCheckCli.Run(new[] { raw, "--expo" }, new StringWriter(), new StringWriter());

        Assert.DoesNotContain('\0', capture.Text);
        Assert.Contains("channels=[0:CFA[0,0]=", capture.Text);
    }

    /// <summary>
    /// A refusal decided after the load has the run's start and done lines, with the load
    /// stage: before, the path overloads of <c>--smooth</c> and <c>--synthetic</c> loaded the
    /// file before the clock started, and a refusal left one bare line (the review of
    /// 2026-10-27, A9).
    /// </summary>
    [Fact]
    public void R110_ASmoothRefusalHasItsStartAndDoneLinesWithTheLoadStage()
    {
        using var ws = new TempWorkspace();
        string cube = ws.WriteFile("cube.fit", FitsTestData.BuildFitsCube(Size, Size, 3, (x, y, p) => 30000));
        using var capture = LogCapture.Start();

        int exit = FlatCheckCli.Run(new[] { cube, "--smooth", ws.PathTo("smooth.fit") }, new StringWriter(), new StringWriter());

        Assert.Equal(1, exit);
        Assert.Contains(capture.Events, e => e.Logger == "SmoothRun" && e.Message.StartsWith("smooth start " + cube, StringComparison.Ordinal));
        Assert.Matches(@"SmoothRun smooth done refused: a cube of 3 planes in \d+ ms \(load \d+ ms\)", capture.Text);
    }

    /// <summary>
    /// A run that throws leaves its start line, so its inputs are in the log, and a line with
    /// the time and the stages it reached before the exception (the review of 2026-10-27, A9).
    /// </summary>
    [Fact]
    public void R110_ARunThatThrowsLeavesItsStartLineAndTheStagesItReached()
    {
        using var ws = new TempWorkspace();
        string notFits = ws.WriteFile("notes.fit", Encoding.ASCII.GetBytes("not a FITS file, only some text"));
        using var capture = LogCapture.Start();

        int exit = FlatCheckCli.Run(new[] { notFits, "--synthetic", ws.PathTo("s.fit"), "--lens", "x", "--profiles", ws.PathTo("none") },
                                    new StringWriter(), new StringWriter());

        Assert.Equal(1, exit);
        Assert.Contains(capture.Events, e => e.Logger == "SyntheticRun" && e.Message.StartsWith("synthetic start " + notFits, StringComparison.Ordinal));
        Assert.Matches(@"SyntheticRun synthetic ended without an outcome after \d+ ms(\r?\n|$)", capture.Text);
        Assert.Single(capture.Events, e => e.Level == LogLevel.Error);
    }

    /// <summary>The log writes an elapsed time in two forms only: milliseconds, or seconds
    /// with one decimal; never whole seconds (the review of 2026-10-27, A10 and D8).</summary>
    [Fact]
    public void TheElapsedTimeHasTwoForms_MillisecondsAndSecondsWithOneDecimal()
    {
        Assert.Equal("12.9 s", LogText.Seconds(TimeSpan.FromMilliseconds(12900)));
        Assert.Equal("0.8 s", LogText.Seconds(TimeSpan.FromMilliseconds(800)));
        Assert.Equal("5 ms", LogText.Ms(5));
    }

    /// <summary>
    /// A light series finds its files and loads its first frame before the frames stage:
    /// both are stages of their own, so the first frame's time is not shorter than the others'
    /// by its load (the review of 2026-10-27, A11).
    /// </summary>
    [Fact]
    public void R110_ALightSeriesTimesTheFindAndTheFirstLoadAsStagesOfTheirOwn()
    {
        using var ws = new TempWorkspace();
        string dir = GoldenOutputTests.SeriesDir(ws, "lights", FitsTestData.Radial(Size, Size), FitsTestData.Radial(Size, Size));
        using var capture = LogCapture.Start();

        FlatCheckCli.Run(new[] { dir, "--light", "--fov", "10", "-o", ws.PathTo("out.png") }, new StringWriter(), new StringWriter());

        Assert.Matches(@"LightSeriesRun light series done .* in \d+ ms \(find \d+ ms, load \d+ ms, frames \d+ ms", capture.Text);
    }

    /// <summary>
    /// The folder probe also opens the file NLog will append to: a read-only newest file sends
    /// the log to the fallback, where before it passed the probe and every line was dropped
    /// in silence (the review of 2026-10-27, A16).
    /// </summary>
    [Fact]
    public void R109_AReadOnlyNewestLogFileSendsTheLogToTheFallback()
    {
        using var ws = new TempWorkspace();
        string beside = Path.Combine(ws.Dir, "exe");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        Directory.CreateDirectory(folder);
        string newest = Path.Combine(folder, "flatcheck_03.log");
        File.WriteAllText(Path.Combine(folder, "flatcheck.log"), "old\n");
        File.WriteAllText(newest, "newest\n");
        File.SetAttributes(newest, FileAttributes.ReadOnly);
        try
        {
            string chosen = LogSetup.ResolveFolder(beside, ws.PathTo("fallback"), "flatcheck", out bool fellBack);

            Assert.True(fellBack);
            Assert.Equal(Path.Combine(ws.PathTo("fallback"), LogSetup.FallbackFolder, LogSetup.FolderName), chosen);
        }
        finally
        {
            File.SetAttributes(newest, FileAttributes.Normal);
        }
    }

    /// <summary>A file another process of the same executable holds open (R-112) passes the
    /// probe: the second window logs beside the first.</summary>
    [Fact]
    public void R109_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe()
    {
        using var ws = new TempWorkspace();
        string beside = Path.Combine(ws.Dir, "exe");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        using var other = Factory(LogSetup.Build("flatcheck", folder, LogLevel.Debug));
        other.GetLogger("Other").Info("held open");

        string chosen = LogSetup.ResolveFolder(beside, ws.PathTo("fallback"), "flatcheck", out bool fellBack);

        Assert.False(fellBack);
        Assert.Equal(folder, chosen);
    }

    [Fact]
    public void R110_AFailedRunLogsTheExceptionWithItsStackAndStderrKeepsOneLine()
    {
        using var ws = new TempWorkspace();
        string notFits = ws.WriteFile("notes.fit", Encoding.ASCII.GetBytes("not a FITS file, only some text"));
        var stderr = new StringWriter();
        using var capture = LogCapture.Start();

        int exit = FlatCheckCli.Run(new[] { notFits, "--lang", "en" }, new StringWriter(), stderr);

        Assert.Equal(1, exit);
        Assert.Equal("ERROR: FITS: the file does not begin with SIMPLE." + Environment.NewLine, stderr.ToString());
        var failed = Assert.Single(capture.Events, e => e.Level == LogLevel.Error);
        Assert.IsType<InvalidDataException>(failed.Exception);
        Assert.Contains("run failed", failed.Message);
        Assert.Contains("System.IO.InvalidDataException: FITS: the file does not begin with SIMPLE.", failed.Line);
        Assert.Contains("   at FlatCheck.FitsReader.", failed.Line);
    }

    [Theory]
    [InlineData("structure")]
    [InlineData("expo")]
    [InlineData("light")]
    [InlineData("synthetic")]
    [InlineData("smooth")]
    [InlineData("against")]
    public void R110_TheLogIsTheSameInBothLanguages(string scenario)
    {
        using var ws = new TempWorkspace();
        string flat = GoldenOutputTests.Flat(ws, "flat.fit", FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.12), Size);
        string raw = GoldenOutputTests.Flat(ws, "raw.fit", FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000)), Size);
        string cube = ws.WriteFile("cube.fit", FitsTestData.BuildFitsCube(Size, Size, 3, (x, y, p) => 30000));
        string lights = GoldenOutputTests.SeriesDir(ws, "lights", FitsTestData.Radial(Size, Size), FitsTestData.Radial(Size, Size));

        // Every mode, the refusals included (synthetic: no lens matches; smooth: a cube).
        string[] Args(string lang) => scenario switch
        {
            "structure" => new[] { flat, "-o", ws.PathTo(lang + ".png") },
            "expo" => new[] { raw, "--expo" },
            "light" => new[] { lights, "--light", "--fov", "10", "-o", ws.PathTo(lang + ".png") },
            "synthetic" => new[] { flat, "--synthetic", ws.PathTo(lang + ".fit"), "--lens", "nothing matches this", "--profiles", ws.PathTo("none") },
            "smooth" => new[] { cube, "--smooth", ws.PathTo(lang + ".fit") },
            "against" => new[] { flat, "--against", flat, "-o", ws.PathTo(lang + ".png") },
            _ => throw new ArgumentException(scenario),
        };

        IReadOnlyList<string> Run(string lang)
        {
            using var capture = LogCapture.Start();
            FlatCheckCli.Run(Args(lang).Concat(new[] { "--lang", lang }).ToArray(), new StringWriter(), new StringWriter());
            // Times and the output's name are the only things allowed to differ.
            return capture.Messages
                .Select(m => Regex.Replace(m, @"\d+(\.\d+)? m?s\b", "# ms"))
                .Select(m => m.Replace(lang + ".png", "<out>.png").Replace(lang + ".fit", "<out>.fit").Replace("lang=" + lang, "lang=<lang>"))
                .ToArray();
        }

        IReadOnlyList<string> en = Run("en"), ro = Run("ro");

        Assert.NotEmpty(en);
        Assert.Equal(en, ro);
    }

    [Fact]
    public void R110_TheConfigurationHasOneFileTargetAndNoConsole()
    {
        var config = LogSetup.Build("flatcheck", @"C:\any\log", LogLevel.Debug);

        var file = Assert.IsType<AtomicFileTarget>(Assert.Single(config.AllTargets));
        Assert.Equal(LogSetup.FileSizeLimit, file.ArchiveAboveSize);
        Assert.Equal(LogSetup.FilesPerExecutable, file.MaxArchiveFiles);
        Assert.True(file.KeepFileOpen);
        Assert.True(file.ConcurrentWrites);
        var rule = Assert.Single(config.LoggingRules);
        Assert.True(rule.IsLoggingEnabledForLevel(LogLevel.Debug));
        Assert.False(rule.IsLoggingEnabledForLevel(LogLevel.Trace));
    }

    // ---------------------------------------------------------------- R-111: the level

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
    public void R111_TheLevelComesFromTheEnvironmentVariable(string? value, string level)
    {
        Assert.Equal(LogLevel.FromString(level), LogSetup.LevelFrom(value));
    }

    [Fact]
    public void R111_OffCreatesNeitherTheFolderNorAFile()
    {
        using var ws = new TempWorkspace();

        LogPlace off = LogSetup.Plan("flatcheck", Path.Combine(ws.Dir, "flatcheck.exe"), ws.Dir, Path.Combine(ws.Dir, "fallback"), "off");

        Assert.Null(off.Folder);
        Assert.Empty(Directory.GetFileSystemEntries(ws.Dir));
        Assert.Empty(LogSetup.Build("flatcheck", Path.Combine(ws.Dir, "log"), LogLevel.Off).AllTargets);

        LogPlace trace = LogSetup.Plan("flatcheck", Path.Combine(ws.Dir, "flatcheck.exe"), ws.Dir, Path.Combine(ws.Dir, "fallback"), "trace");
        Assert.Equal(LogLevel.Trace, trace.Level);
        Assert.Equal(Path.Combine(ws.Dir, "log"), trace.Folder);
    }

    [Fact]
    public void R111_TraceHoldsEveryFitsHeaderCard()
    {
        using var ws = new TempWorkspace();
        string flat = GoldenOutputTests.Flat(ws, "flat.fit", FitsTestData.Radial(Size, Size), Size);
        using var capture = LogCapture.Start();

        ImageLoader.Load(flat);

        var cards = capture.Events.Where(e => e.Level == LogLevel.Trace).Select(e => e.Message).ToArray();
        Assert.Contains(cards, m => m.Contains("card BITPIX  =                   16"));
        Assert.Contains(cards, m => m.Contains("card NAXIS1  =                  256"));
        Assert.DoesNotContain(capture.Events, e => e.Level == LogLevel.Debug && e.Message.Contains("card "));
    }

    // ---------------------------------------------------------------- invariants of the repository

    /// <summary>The C# sources under the given repository folders, without bin/ and obj/
    /// (filtered on the relative path, so a checkout under a folder named bin still yields
    /// every file).</summary>
    static IEnumerable<string> CodeFiles(params string[] folders) => RepoInvariantTests.CsSources(folders);

    /// <summary>A file's path as the assertions name it: relative to the repository, with
    /// forward slashes.</summary>
    static string Rel(string f) => Path.GetRelativePath(Root, f).Replace('\\', '/');

    /// <summary>Only the two entry points configure the log for a user's run, and nothing but
    /// LogSetup (for them) and LogCapture (for the tests) sets NLog's configuration at all;
    /// so a library use logs into nothing and every test into the capture (ADR 0070).</summary>
    [Fact]
    public void OnlyTheTwoEntryPointsConfigureTheLog()
    {
        string[] all = { "FlatCheck.Core", "FlatCheck", "FlatCheck.Wpf", "FlatCheck.Tests", "FlatCheck.Wpf.Tests" };
        var callers = CodeFiles(all)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"LogSetup\.(Apply|Close)\("))
            .Select(Rel).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var configurers = CodeFiles(all)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"LogManager\.(Configuration\s*=[^=]|Setup\()"))
            .Select(Rel).OrderBy(f => f, StringComparer.Ordinal).ToArray();

        Assert.Equal(new[] { "FlatCheck.Wpf/App.xaml.cs", "FlatCheck/Program.cs" }, callers);
        Assert.Equal(new[] { "FlatCheck.Core/Logging/LogSetup.cs", "FlatCheck.Tests/LogCapture.cs" }, configurers);
    }

    /// <summary>
    /// The log's own words are English whatever the language (R-110): no log call composes its
    /// message through the T(english, romanian) pair, whichever logger it goes through and
    /// however many lines the call spans. And every logger is declared the one way, a
    /// <c>Logger</c> field taken from <c>LogManager.GetLogger</c> with <c>using NLog</c>, so the
    /// first check sees every call (a tab's logger is an instance named after the tab, and
    /// LogSetup's is fetched on use, on purpose).
    /// </summary>
    [Fact]
    public void NoLogLineGoesThroughTheTranslation()
    {
        var call = new Regex(@"\b\w*[Ll]og\.(Trace|Debug|Info|Warn|Error|Fatal)\(");
        var callEnd = new Regex(@"\)\s*;");
        var declaration = new Regex(@"static readonly Logger \w+ = LogManager\.GetLogger\(|static Logger \w+ => LogManager\.GetLogger\(|^\s*Log = LogManager\.GetLogger\(", RegexOptions.Multiline);
        var offenders = new List<string>();
        var declarations = new List<string>();
        foreach (string f in CodeFiles("FlatCheck.Core", "FlatCheck", "FlatCheck.Wpf"))
        {
            string text = File.ReadAllText(f);
            foreach (Match m in call.Matches(text))
            {
                Match end = callEnd.Match(text, m.Index);
                string whole = end.Success ? text.Substring(m.Index, end.Index + end.Length - m.Index) : text.Substring(m.Index);
                if (Regex.IsMatch(whole, @"\bT\(")) offenders.Add(Rel(f) + ":" + (text.Take(m.Index).Count(c => c == '\n') + 1));
            }
            string[] lines = File.ReadAllLines(f);
            for (int i = 0; i < lines.Length; i++)
            {
                bool inline = lines[i].Contains("GetLogger(") && !declaration.IsMatch(lines[i]);
                bool qualified = lines[i].Contains("NLog.Logger") || lines[i].Contains("NLog.LogManager");
                if (inline || qualified) declarations.Add(Rel(f) + ":" + (i + 1));
            }
        }

        Assert.Empty(offenders);
        Assert.Empty(declarations);
    }

    /// <summary>The maths stay pure (ADR 0070): nothing in Analysis/ or Report/, nor Metrics,
    /// Stats or Img, holds a logger or names NLog; the runs log what the analyzers return. (A
    /// catalog they call may still warn about an Adobe file it cannot read: the ADR's restated
    /// note of 2026-10-27.)</summary>
    [Fact]
    public void NothingThatComputesOrWordsTheReportLogs()
    {
        var files = CodeFiles("FlatCheck.Core/Analysis", "FlatCheck.Core/Report")
            .Concat(new[] { "Metrics.cs", "Stats.cs", "Img.cs", "Thresholds.cs" }.Select(n => Path.Combine(Root, "FlatCheck.Core", n)));

        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.DoesNotMatch(@"\bNLog\b|\bLogger\b|\bLogManager\b", File.ReadAllText(f)));
    }
}
