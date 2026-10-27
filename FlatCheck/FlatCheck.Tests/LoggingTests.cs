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

        // The next process goes on in the newest file, and the count stays at five.
        WriteThrough(folder, "flatcheck", 1000, "next");
        string[] after = FilesOf(folder, "flatcheck");
        foreach (string f in after)
            output.WriteLine($"after a second process: {Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        Assert.True(after.Length <= LogSetup.FilesPerExecutable);
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
    static (List<int> A, List<int> B, int Broken) TwoWritersLines(string folder, string executable)
    {
        var a = new List<int>();
        var b = new List<int>();
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
        }
        return (a, b, broken);
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

        Parallel.Invoke(
            () => { var log = a.GetLogger("A"); for (int i = 0; i < Lines; i++) log.Info($"a {i:D5} " + new string('A', 140 + i % 37)); },
            () => { var log = b.GetLogger("B"); for (int i = 0; i < Lines; i++) log.Info($"b {i:D5} " + new string('B', 140 + i % 37)); });
        a.Shutdown();
        b.Shutdown();

        string[] files = FilesOf(folder, "FlatCheck.Wpf");
        foreach (string f in files)
            output.WriteLine($"{Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        var (fromA, fromB, broken) = TwoWritersLines(folder, "FlatCheck.Wpf");
        output.WriteLine($"whole lines: {fromA.Count} from A, {fromB.Count} from B; broken: {broken}");
        Assert.Equal(Directory.GetFiles(folder).Length, files.Length);
        Assert.InRange(files.Length, 2, LogSetup.FilesPerExecutable);
        Assert.Equal(0, broken);
        Assert.Equal(Enumerable.Range(0, Lines), fromA.OrderBy(n => n));
        Assert.Equal(Enumerable.Range(0, Lines), fromB.OrderBy(n => n));
    }

    [Theory]
    [InlineData(@"C:\Tools\flatcheck.exe", "flatcheck", "flatcheck")]
    [InlineData(@"C:\Tools\flatcheck-2.3.exe", "flatcheck", "flatcheck-2.3")]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", "flatcheck", "flatcheck")]
    [InlineData(@"/usr/share/dotnet/dotnet", "flatcheck", "flatcheck")]
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

    [Fact]
    public void R110_TheLogIsTheSameInBothLanguages()
    {
        using var ws = new TempWorkspace();
        string flat = GoldenOutputTests.Flat(ws, "flat.fit", FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.12), Size);

        IReadOnlyList<string> Run(string lang)
        {
            using var capture = LogCapture.Start();
            FlatCheckCli.Run(new[] { flat, "--lang", lang, "-o", ws.PathTo(lang + ".png") }, new StringWriter(), new StringWriter());
            // Times and the map's name are the only things allowed to differ.
            return capture.Messages
                .Select(m => Regex.Replace(m, @"\d+(\.\d+)? m?s\b", "# ms"))
                .Select(m => m.Replace(lang + ".png", "<map>.png").Replace("lang=" + lang, "lang=<lang>"))
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

    static IEnumerable<string> CodeFiles(params string[] folders) =>
        folders.SelectMany(f => Directory.GetFiles(Path.Combine(Root, f), "*.cs", SearchOption.AllDirectories))
               .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));

    /// <summary>Only the two entry points configure the log; the core and the tests never
    /// do, so a library use and every test log into nothing but the capture.</summary>
    [Fact]
    public void OnlyTheTwoEntryPointsConfigureTheLog()
    {
        var callers = CodeFiles("FlatCheck.Core", "FlatCheck", "FlatCheck.Wpf", "FlatCheck.Tests", "FlatCheck.Wpf.Tests")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"LogSetup\.(Apply|Close)\("))
            .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "FlatCheck.Wpf/App.xaml.cs", "FlatCheck/Program.cs" }, callers);
    }

    /// <summary>The log is English whatever the language (R-110): no log call composes its
    /// message through the T(english, romanian) pair.</summary>
    [Fact]
    public void NoLogLineGoesThroughTheTranslation()
    {
        var offenders = CodeFiles("FlatCheck.Core", "FlatCheck", "FlatCheck.Wpf")
            .SelectMany(f => File.ReadAllLines(f).Select((l, i) => (f, i, l)))
            .Where(x => Regex.IsMatch(x.l, @"\bLog\.(Trace|Debug|Info|Warn|Error|Fatal)\(") && Regex.IsMatch(x.l, @"\bT\("))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}")
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>The maths stay pure (ADR 0070): nothing in Analysis/ or Report/, nor Metrics,
    /// Stats or Img, names NLog; the runs log what the analyzers return.</summary>
    [Fact]
    public void NothingThatComputesOrWordsTheReportLogs()
    {
        var files = CodeFiles(Path.Combine("FlatCheck.Core", "Analysis"), Path.Combine("FlatCheck.Core", "Report"))
            .Concat(new[] { "Metrics.cs", "Stats.cs", "Img.cs", "Thresholds.cs" }.Select(n => Path.Combine(Root, "FlatCheck.Core", n)));

        Assert.All(files, f => Assert.DoesNotContain("NLog", File.ReadAllText(f)));
    }
}
