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

using System.Globalization;
using System.Text;
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

    // ---------------------------------------------------------------- helpers

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
