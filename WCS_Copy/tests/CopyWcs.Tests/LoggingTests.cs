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

namespace CopyWcs.Tests;

/// <summary>
/// The diagnostic log (R-040 to R-046, ADR 0009), after FlatCheck's LoggingTests: where the files
/// go and how they roll, the lines of a process, what a copy writes, the level, the two writers,
/// the language. The file tests run the applications' configuration (<see cref="LogSetup.Build"/>)
/// in a private <see cref="LogFactory"/>, so they touch neither the test assembly's log
/// (<see cref="LogCapture"/>) nor the tests beside them.
/// </summary>
public class LoggingTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>The runner's output, for the measured file lists.</summary>
    public LoggingTests(ITestOutputHelper output) => _output = output;

    private static readonly Regex LinePrefix = new(
        @"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{4} (TRACE|DEBUG|INFO |WARN |ERROR|FATAL) \[\d+:\d+\] \S+ ",
        RegexOptions.Compiled);

    /// <summary>The files one executable left in a folder, oldest first by NLog's own numbering:
    /// the bare name, then _01, _02 and so on.</summary>
    private static string[] FilesOf(string folder, string executable) =>
        Directory.GetFiles(folder, executable + "*.log")
                 .Where(f => Regex.IsMatch(Path.GetFileName(f), "^" + Regex.Escape(executable) + @"(_\d+)?\.log$", RegexOptions.IgnoreCase))
                 .OrderBy(f => Path.GetFileNameWithoutExtension(f).Length)
                 .ThenBy(f => f, StringComparer.Ordinal)
                 .ToArray();

    /// <summary>A private factory on <paramref name="config"/>, standing for one process.</summary>
    private static LogFactory Factory(NLog.Config.LoggingConfiguration config) => new() { Configuration = config };

    /// <summary>Writes <paramref name="bytes"/> of lines through one executable's configuration and closes it.</summary>
    private static void WriteThrough(string folder, string executable, long bytes, string tag)
    {
        using LogFactory factory = Factory(LogSetup.Build(executable, folder, LogLevel.Debug));
        Logger log = factory.GetLogger("Rolling");
        string payload = new('x', 4000);
        long lines = bytes / (payload.Length + 100) + 1;
        for (long i = 0; i < lines; i++)
        {
            log.Info($"{tag} {i:D6} {payload}");
        }

        log.Info($"{tag} last");
        factory.Shutdown();
    }

    // ---------------------------------------------------------------- R-040: the files

    [Fact]
    public void R040_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName()
    {
        using var ws = new TempWorkspace();
        string fallback = ws.FilePath("fallback");

        string folder = LogSetup.ResolveFolder(ws.Dir, fallback, "copywcs", out bool fellBack);

        Assert.False(fellBack);
        Assert.Equal(Path.Combine(ws.Dir, "log"), folder);
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));   // the probe is gone
        Assert.False(Directory.Exists(fallback));

        using (LogFactory factory = Factory(LogSetup.Build("copywcs", folder, LogLevel.Debug)))
        {
            factory.GetLogger("Test").Info("hello");
            factory.Shutdown();
        }

        string file = Path.Combine(folder, "copywcs.log");
        Assert.Equal([file], Directory.GetFiles(folder));
        byte[] bytes = File.ReadAllBytes(file);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no BOM");
        string line = Encoding.UTF8.GetString(bytes).TrimEnd('\r', '\n');
        Assert.Matches(LinePrefix, line);
        Assert.EndsWith(" INFO  [" + Environment.ProcessId + ":" + Environment.CurrentManagedThreadId + "] Test hello", line, StringComparison.Ordinal);
    }

    [Fact]
    public void R040_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback()
    {
        using var ws = new TempWorkspace();
        // A FILE named log where the folder should be: the folder cannot be created.
        ws.WriteFile("log", [1]);
        string fallbackRoot = ws.FilePath("LocalAppData");

        string folder = LogSetup.ResolveFolder(ws.Dir, fallbackRoot, "copywcs", out bool fellBack);

        Assert.True(fellBack);
        Assert.Equal(Path.Combine(fallbackRoot, "CopyWcs", "log"), folder);
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
        Assert.Contains("(fallback", LogSetup.StartupLine("copywcs", [], folder, true, LogLevel.Debug), StringComparison.Ordinal);
    }

    /// <summary>
    /// The folder probe also opens the file NLog will append to: a read-only newest file sends
    /// the log to the fallback, where it would otherwise pass the probe and lose every line
    /// (FlatCheck's review, A16).
    /// </summary>
    [Fact]
    public void R040_AReadOnlyNewestLogFileSendsTheLogToTheFallback()
    {
        using var ws = new TempWorkspace();
        string beside = ws.FilePath("exe");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        Directory.CreateDirectory(folder);
        string newest = Path.Combine(folder, "copywcs_03.log");
        File.WriteAllText(Path.Combine(folder, "copywcs.log"), "old\n");
        File.WriteAllText(newest, "newest\n");
        File.SetAttributes(newest, FileAttributes.ReadOnly);
        try
        {
            string chosen = LogSetup.ResolveFolder(beside, ws.FilePath("fallback"), "copywcs", out bool fellBack);

            Assert.True(fellBack);
            Assert.Equal(Path.Combine(ws.FilePath("fallback"), LogSetup.FallbackFolder, LogSetup.FolderName), chosen);
        }
        finally
        {
            File.SetAttributes(newest, FileAttributes.Normal);
        }
    }

    /// <summary>A file another process of the same executable holds open (R-045) passes the
    /// probe: the second window logs beside the first.</summary>
    [Fact]
    public void R040_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe()
    {
        using var ws = new TempWorkspace();
        string beside = ws.FilePath("exe");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        using LogFactory other = Factory(LogSetup.Build("copywcs", folder, LogLevel.Debug));
        other.GetLogger("Other").Info("held open");

        string chosen = LogSetup.ResolveFolder(beside, ws.FilePath("fallback"), "copywcs", out bool fellBack);

        Assert.False(fellBack);
        Assert.Equal(folder, chosen);
        other.Shutdown();
    }

    [Fact]
    public void R040_TheLogRollsAt1MiBAndKeepsAtMost5Files()
    {
        using var ws = new TempWorkspace();
        string folder = ws.FilePath("log");

        WriteThrough(folder, "copywcs", 8 * LogSetup.FileSizeLimit, "console");

        string[] files = FilesOf(folder, "copywcs");
        foreach (string f in files)
        {
            _output.WriteLine($"{Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        }

        Assert.Equal(Directory.GetFiles(folder).Length, files.Length);   // nothing else is left
        Assert.Equal(LogSetup.FilesPerExecutable, files.Length);
        Assert.All(files, f => Assert.InRange(new FileInfo(f).Length, 1, LogSetup.FileSizeLimit + 4200));
        // The newest file is the one with the highest number, and it holds the last line.
        Assert.Equal(["copywcs_03.log", "copywcs_04.log", "copywcs_05.log", "copywcs_06.log", "copywcs_07.log"],
                     files.Select(Path.GetFileName));
        Assert.EndsWith("console last", File.ReadAllLines(files[^1])[^1], StringComparison.Ordinal);

        // The next process goes on in the newest file: the same five names, the newest grown,
        // nothing started and nothing deleted (one file per process was rejected, ADR 0009).
        long newestBefore = new FileInfo(files[^1]).Length;
        WriteThrough(folder, "copywcs", 1000, "next");
        string[] after = FilesOf(folder, "copywcs");
        foreach (string f in after)
        {
            _output.WriteLine($"after a second process: {Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        }

        Assert.Equal(files.Select(Path.GetFileName), after.Select(Path.GetFileName));
        Assert.True(new FileInfo(after[^1]).Length > newestBefore, "the newest file did not grow");
        Assert.EndsWith("next last", File.ReadAllLines(after[^1])[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void R040_OneExecutablesRollingLeavesTheOthersFilesAlone()
    {
        using var ws = new TempWorkspace();
        string folder = ws.FilePath("log");
        // The window's log first, rolled once, then the console's, rolled many times, in the
        // same folder: CopyWcs.Wpf begins with the same letters as copywcs, in another case.
        WriteThrough(folder, "CopyWcs.Wpf", 3 * LogSetup.FileSizeLimit / 2, "window");
        string[] window = FilesOf(folder, "CopyWcs.Wpf");
        Assert.Equal(2, window.Length);

        WriteThrough(folder, "copywcs", 8 * LogSetup.FileSizeLimit, "console");

        Assert.Equal(window, FilesOf(folder, "CopyWcs.Wpf"));
        Assert.EndsWith("window last", File.ReadAllLines(window[^1])[^1], StringComparison.Ordinal);
        Assert.Equal(LogSetup.FilesPerExecutable, FilesOf(folder, "copywcs").Length);
    }

    [Theory]
    // Windows paths with forward slashes, which Windows accepts and Unix can split too: the test
    // project is portable, and a backslash is not a separator there.
    [InlineData("C:/Tools/copywcs.exe", "copywcs", "copywcs")]
    [InlineData("C:/Tools/copywcs-1.1.exe", "copywcs", "copywcs-1.1")]
    [InlineData("C:/Program Files/dotnet/dotnet.exe", "copywcs", "copywcs")]
    [InlineData("/usr/share/dotnet/dotnet", "copywcs", "copywcs")]
    [InlineData(null, "CopyWcs.Wpf", "CopyWcs.Wpf")]
    public void R040_TheFilesBeginWithTheExecutablesName(string? processPath, string entry, string expected)
    {
        Assert.Equal(expected, LogSetup.ExecutableName(processPath, entry));
    }

    // ---------------------------------------------------------------- R-041: the lines of a process

    [Fact]
    public void R041_TheFirstLineCarriesTheFacts()
    {
        string line = LogSetup.StartupLine("copywcs", [@"D:\stacks\solved one.fit", "--lang", "ro"],
                                           @"D:\tools\log", false, LogLevel.Debug);

        Assert.StartsWith("copywcs " + VersionInfo.Value + " starting; exe=", line, StringComparison.Ordinal);
        Assert.Contains(@"; args=[""D:\stacks\solved one.fit"", ""--lang"", ""ro""]; cwd=", line, StringComparison.Ordinal);
        Assert.Contains(@"; log=D:\tools\log; level=Debug; os=", line, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback", line, StringComparison.Ordinal);
        foreach (string part in new[] { "; cpus=", "; memory=", " GiB; runtime=", "; culture=", "; tz=", " (UTC" })
        {
            Assert.Contains(part, line, StringComparison.Ordinal);
        }

        Assert.EndsWith("; pid=" + Environment.ProcessId, line, StringComparison.Ordinal);
    }

    [Fact]
    public void R041_TheConfigurationHasOneFileTargetAndNoConsole()
    {
        var config = LogSetup.Build("copywcs", @"C:\any\log", LogLevel.Debug);

        var file = Assert.IsType<AtomicFileTarget>(Assert.Single(config.AllTargets));
        Assert.Equal(LogSetup.FileSizeLimit, file.ArchiveAboveSize);
        Assert.Equal(LogSetup.FilesPerExecutable, file.MaxArchiveFiles);
        Assert.True(file.KeepFileOpen);
        Assert.True(file.ConcurrentWrites);
        var rule = Assert.Single(config.LoggingRules);
        Assert.True(rule.IsLoggingEnabledForLevel(LogLevel.Debug));
        Assert.False(rule.IsLoggingEnabledForLevel(LogLevel.Trace));
    }

    /// <summary>A crash the window's dispatcher sees and then the process sees again is written
    /// once with its stack: the second Fatal line names the first (FlatCheck's ADR 0072).</summary>
    [Fact]
    public void R041_TheSameCrashSeenByTwoHooksIsWrittenOnce()
    {
        var crash = new InvalidOperationException("the window fell over");
        using var capture = LogCapture.Start();

        LogSetup.Fatal(crash, "unhandled exception on the window's thread");
        LogSetup.Fatal(crash, "unhandled exception, the process ends");
        LogSetup.Fatal(new InvalidOperationException("another"), "unhandled exception, the process ends");

        var fatal = capture.Events.Where(e => e.Level == LogLevel.Fatal && e.Logger == "Process").ToList();
        Assert.Equal(3, fatal.Count);
        Assert.Same(crash, fatal[0].Exception);
        Assert.Null(fatal[1].Exception);
        Assert.Equal("unhandled exception, the process ends: the exception logged above", fatal[1].Message);
        Assert.NotNull(fatal[2].Exception);
    }

    /// <summary>The log writes an elapsed time in two forms only: milliseconds, or seconds with
    /// one decimal, as the last line does; never whole seconds.</summary>
    [Fact]
    public void R041_TheLastLineGivesTheSecondsWithOneDecimal()
    {
        Assert.Equal("12.9 s", LogText.Seconds(TimeSpan.FromMilliseconds(12900)));
        Assert.Equal("0.8 s", LogText.Seconds(TimeSpan.FromMilliseconds(800)));
        Assert.Equal("5 ms", LogText.Ms(5));
    }

    // ---------------------------------------------------------------- R-044: the level

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
    public void R044_TheLevelComesFromTheEnvironmentVariable(string? value, string level)
    {
        Assert.Equal(LogLevel.FromString(level), LogSetup.LevelFrom(value));
    }

    [Fact]
    public void R044_OffCreatesNeitherTheFolderNorAFile()
    {
        using var ws = new TempWorkspace();

        LogPlace off = LogSetup.Plan("copywcs", ws.FilePath("copywcs.exe"), ws.Dir, ws.FilePath("fallback"), "off");

        Assert.Null(off.Folder);
        Assert.Empty(Directory.GetFileSystemEntries(ws.Dir));
        Assert.Empty(LogSetup.Build("copywcs", ws.FilePath("log"), LogLevel.Off).AllTargets);
        Assert.Empty(LogSetup.Build("copywcs", null, LogLevel.Debug).AllTargets);

        LogPlace trace = LogSetup.Plan("copywcs", ws.FilePath("copywcs.exe"), ws.Dir, ws.FilePath("fallback"), "trace");
        Assert.Equal(LogLevel.Trace, trace.Level);
        Assert.Equal(ws.FilePath("log"), trace.Folder);
    }

    // ---------------------------------------------------------------- R-045: two writers

    /// <summary>The lines two writers left in a folder, whole, by writer: the numbers each one's
    /// lines carry, and the count of lines that are neither writer's whole line.</summary>
    private static (List<int> A, List<int> B, int Broken, string Order) TwoWritersLines(string folder, string executable)
    {
        var a = new List<int>();
        var b = new List<int>();
        var order = new StringBuilder();
        int broken = 0;
        var whole = new Regex(@" (A|B) (a|b) (\d{5}) ([AB]+)$");
        foreach (string line in FilesOf(folder, executable).SelectMany(File.ReadAllLines))
        {
            Match m = whole.Match(line);
            bool ok = LinePrefix.IsMatch(line) && m.Success && m.Groups[1].Value.Equals(m.Groups[2].Value, StringComparison.OrdinalIgnoreCase);
            int n = ok ? int.Parse(m.Groups[3].Value) : -1;
            // the payload is the writer's letter, as many as the line's number says
            ok = ok && m.Groups[4].Value == new string(m.Groups[1].Value[0], 140 + n % 37);
            if (!ok)
            {
                broken++;
                continue;
            }

            (m.Groups[1].Value == "A" ? a : b).Add(n);
            order.Append(m.Groups[1].Value);
        }

        return (a, b, broken, order.ToString());
    }

    /// <summary>
    /// Two processes of one executable (two windows) write into the same file at full speed, and
    /// every line of both arrives whole, once, past the 1 MiB roll too (R-040, R-045). Two
    /// factories stand for the two processes; each opens the file on its own. NLog's plain target
    /// failed this in FlatCheck: closed between lines it lost about 1 % of them, kept open one
    /// writer lost all of its (FlatCheck's ADR 0070).
    /// </summary>
    [Fact]
    public void R045_TwoProcessesWritingAtFullSpeedKeepEveryLine()
    {
        using var ws = new TempWorkspace();
        string folder = ws.FilePath("log");
        using LogFactory a = Factory(LogSetup.Build("CopyWcs.Wpf", folder, LogLevel.Debug));
        using LogFactory b = Factory(LogSetup.Build("CopyWcs.Wpf", folder, LogLevel.Debug));
        const int Lines = 4000;   // about 1.6 MiB together: the writers cross the roll

        // Two threads of their own, released together: Parallel.Invoke may run the second action
        // after the first on a busy pool, and then nothing would overlap.
        using var together = new Barrier(2);
        var ta = new Thread(() =>
        {
            Logger log = a.GetLogger("A");
            together.SignalAndWait();
            for (int i = 0; i < Lines; i++)
            {
                log.Info($"a {i:D5} " + new string('A', 140 + i % 37));
            }
        });
        var tb = new Thread(() =>
        {
            Logger log = b.GetLogger("B");
            together.SignalAndWait();
            for (int i = 0; i < Lines; i++)
            {
                log.Info($"b {i:D5} " + new string('B', 140 + i % 37));
            }
        });
        ta.Start();
        tb.Start();
        ta.Join();
        tb.Join();
        a.Shutdown();
        b.Shutdown();

        string[] files = FilesOf(folder, "CopyWcs.Wpf");
        foreach (string f in files)
        {
            _output.WriteLine($"{Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        }

        var (fromA, fromB, broken, order) = TwoWritersLines(folder, "CopyWcs.Wpf");
        _output.WriteLine($"whole lines: {fromA.Count} from A, {fromB.Count} from B; broken: {broken}");
        Assert.Equal(Directory.GetFiles(folder).Length, files.Length);
        Assert.InRange(files.Length, 2, LogSetup.FilesPerExecutable);
        Assert.Equal(0, broken);
        Assert.Equal(Enumerable.Range(0, Lines), fromA.OrderBy(n => n));
        Assert.Equal(Enumerable.Range(0, Lines), fromB.OrderBy(n => n));
        // The writers did overlap: their lines interleave in the file, both ways round.
        Assert.Contains("AB", order, StringComparison.Ordinal);
        Assert.Contains("BA", order, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- invariants of the repository

    /// <summary>The C# sources under the given folders of the repository, without bin/ and obj/
    /// (filtered on the relative path, the walk LicenseHeaderTests does).</summary>
    private static IEnumerable<string> CsSources(params string[] folders)
    {
        string root = TestData.FindRepoRoot();
        foreach (string folder in folders)
        {
            foreach (string f in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            {
                string rel = Rel(f);
                if (!rel.Contains("/bin/", StringComparison.Ordinal) && !rel.Contains("/obj/", StringComparison.Ordinal))
                {
                    yield return f;
                }
            }
        }
    }

    /// <summary>A file's path as the assertions name it: relative to the repository, with forward slashes.</summary>
    private static string Rel(string f) => Path.GetRelativePath(TestData.FindRepoRoot(), f).Replace('\\', '/');

    /// <summary>Only the two entry points configure the log for a user's run, and nothing but
    /// LogSetup (for them) and LogCapture (for the tests) sets NLog's configuration at all; so a
    /// library use logs into nothing and every test into the capture (ADR 0009).</summary>
    [Fact]
    public void OnlyTheTwoEntryPointsConfigureTheLog()
    {
        var callers = CsSources("src", "tests")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"LogSetup\.(Apply|Close)\("))
            .Select(Rel).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var configurers = CsSources("src", "tests")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"LogManager\.(Configuration\s*=[^=]|Setup\()"))
            .Select(Rel).OrderBy(f => f, StringComparer.Ordinal).ToArray();

        Assert.Equal(["src/CopyWcs.Cli/Program.cs", "src/CopyWcs.Wpf/App.xaml.cs"], callers);
        Assert.Equal(["src/CopyWcs.Core/Logging/LogSetup.cs", "tests/CopyWcs.Tests/LogCapture.cs"], configurers);
    }

    /// <summary>
    /// The log's own words are English whatever the language (R-046): no log call composes its
    /// message through the T(english, romanian) pair, however many lines the call spans. And every
    /// logger is declared the one way, a <c>Logger</c> field taken from <c>LogManager.GetLogger</c>
    /// with <c>using NLog</c> (LogSetup's and App's fetched on use, on purpose), so the first check
    /// sees every call.
    /// </summary>
    [Fact]
    public void NoLogLineGoesThroughTheTranslation()
    {
        var call = new Regex(@"\b\w*[Ll]og\.(Trace|Debug|Info|Warn|Error|Fatal)\(");
        var callEnd = new Regex(@"\)\s*;");
        var declaration = new Regex(@"static readonly Logger \w+ = LogManager\.GetLogger\(|static Logger \w+ => LogManager\.GetLogger\(");
        var offenders = new List<string>();
        var declarations = new List<string>();
        foreach (string f in CsSources("src"))
        {
            string text = File.ReadAllText(f);
            foreach (Match m in call.Matches(text))
            {
                Match end = callEnd.Match(text, m.Index);
                string whole = end.Success ? text.Substring(m.Index, end.Index + end.Length - m.Index) : text[m.Index..];
                if (Regex.IsMatch(whole, @"\bT\("))
                {
                    offenders.Add(Rel(f) + ":" + (text.Take(m.Index).Count(c => c == '\n') + 1));
                }
            }

            string[] lines = File.ReadAllLines(f);
            for (int i = 0; i < lines.Length; i++)
            {
                bool inline = lines[i].Contains("GetLogger(", StringComparison.Ordinal) && !declaration.IsMatch(lines[i]);
                bool qualified = lines[i].Contains("NLog.Logger", StringComparison.Ordinal) || lines[i].Contains("NLog.LogManager", StringComparison.Ordinal);
                if (inline || qualified)
                {
                    declarations.Add(Rel(f) + ":" + (i + 1));
                }
            }
        }

        Assert.Empty(offenders);
        Assert.Empty(declarations);
    }
}
