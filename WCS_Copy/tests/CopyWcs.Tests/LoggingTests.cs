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

using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using NLog.Targets;
using Xunit.Abstractions;
using static CopyWcs.Tests.LogFiles;

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

    /// <summary>The keys of the test pair's copy, in the order the selection takes them (R-007).</summary>
    private static readonly string[] PairKeys =
        ["CTYPE1", "CTYPE2", "CRPIX1", "CRPIX2", "CRVAL1", "CRVAL2", "CDELT1", "CDELT2", "PLTSOLVD", "A_0_2", "BP_1_1", "FOCALLEN", "XPIXSZ", "OBJECT", "TELESCOP"];

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

    // ---------------------------------------------------------------- R-042: what a copy writes

    /// <summary>The console's run in process: the exit code and stderr.</summary>
    private static (int Code, string Stderr) Cli(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int code = CopyWcsCli.Run(args, stdout, stderr);
        return (code, stderr.ToString());
    }

    /// <summary>The test pair: a plate-solved <c>source.fit</c> and a processed <c>dest.fit</c>, 100 x 80, with the same 400 bytes of data.</summary>
    private static (string Source, string Dest) Pair(TempWorkspace ws, List<string>? source = null, List<string>? dest = null)
    {
        byte[] data = FitsTestData.DeterministicData(400);
        return (ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(source ?? FitsTestData.PlateSolvedCards(), data)),
                ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(dest ?? FitsTestData.ProcessedCards(), data)));
    }

    /// <summary>The done line of the one run a capture saw.</summary>
    private static string DoneLine(LogCapture capture) =>
        Assert.Single(capture.Messages, m => m.StartsWith("copy done ", StringComparison.Ordinal));

    [Fact]
    public void R042_ACopyLogsTheRequestBothHeadersEveryCardAndTheStages()
    {
        using var ws = new TempWorkspace();
        List<string> destCards = FitsTestData.ProcessedCards();
        destCards[5] = FitsTestData.Card("OBJECT", "'M43'");
        var (src, dst) = Pair(ws, dest: destCards);
        using var capture = LogCapture.Start();

        var (code, stderr) = Cli(src, dst);

        Assert.Equal(0, code);
        Assert.Equal("", stderr);
        IReadOnlyList<string> messages = capture.Messages;
        _output.WriteLine(capture.Text);
        Assert.Equal("copy start source=" + src + " dest=" + dst + " output=in place optical=yes force=no dryRun=no", messages[0]);
        Assert.Contains("read " + src + " (3.2 KiB) bytes=3280 cards=20 blocks=1 dataOffset=2880 data=400 BITPIX=16 NAXIS=2 NAXIS1=100"
                        + " NAXIS2=80 NAXIS3=none EXTEND=none CTYPE1='RA---TAN-SIP' CTYPE2='DEC--TAN-SIP' ROWORDER=none A_ORDER=none PLTSOLVD=T", messages);
        Assert.Contains("read " + dst + " (3.2 KiB) bytes=3280 cards=6 blocks=1 dataOffset=2880 data=400 BITPIX=16 NAXIS=2 NAXIS1=100"
                        + " NAXIS2=80 NAXIS3=none EXTEND=none CTYPE1=none CTYPE2=none ROWORDER=none A_ORDER=none PLTSOLVD=none", messages);
        Assert.Contains("selected 15 keys: 9 WCS, 2 SIP, 4 optical (optical keys asked: yes)", messages);
        Assert.Contains("card CTYPE1: CTYPE1  = 'RA---TAN-SIP' / TAN + SIP | dest: none", messages);
        Assert.Contains("card OBJECT: OBJECT  = 'M42' | dest: OBJECT  = 'M43'", messages);
        // One card line per key selected, in the selection's order, the other 14 new to the destination.
        string[] cardLines = messages.Where(m => m.StartsWith("card ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(PairKeys, cardLines.Select(m => m[5..m.IndexOf(':', StringComparison.Ordinal)]));
        Assert.Equal(14, cardLines.Count(m => m.EndsWith(" | dest: none", StringComparison.Ordinal)));
        Assert.Contains("appended HISTORY WCS copied from source.fit", messages);
        Assert.Contains("write " + dst + " in place through " + dst + ".tmp, the data from byte 2880 of " + dst, messages);
        Assert.Contains("wrote " + dst + " (3.2 KiB) bytes=3280 cards=21 blocks=1 data=400", messages);
        Assert.Matches("^copy done copied 15 keys into " + Regex.Escape(dst) + @" \(new 14, replaced 1, identical 0\) in \d+ ms"
                       + @" \(source \d+ ms, dest \d+ ms, select \d+ ms, write \d+ ms\)$", DoneLine(capture));
        Assert.Contains(capture.Events, e => e.Logger == "FitsHeader" && e.Message.StartsWith("read " + src, StringComparison.Ordinal));
        Assert.Contains(capture.Events, e => e.Logger == "WcsCopy" && e.Message.StartsWith("card OBJECT:", StringComparison.Ordinal));
        Assert.All(capture.Events, e => Assert.Matches(LinePrefix, e.Line));
    }

    [Fact]
    public void R042_ADryRunLogsTheCardsItWouldCopyAndWritesNoFile()
    {
        using var ws = new TempWorkspace();
        var (src, dst) = Pair(ws);
        using var capture = LogCapture.Start();

        Assert.Equal(0, Cli(src, dst, "--dry-run").Code);

        Assert.Contains("card OBJECT: OBJECT  = 'M42' | dest: same", capture.Messages);
        Assert.DoesNotContain(capture.Messages, m => m.StartsWith("write ", StringComparison.Ordinal) || m.StartsWith("wrote ", StringComparison.Ordinal));
        Assert.Matches(@"^copy done previewed 15 keys \(new 14, replaced 0, identical 1\) in \d+ ms \(source \d+ ms, dest \d+ ms, select \d+ ms\)$",
                       DoneLine(capture));
    }

    [Fact]
    public void R042_AFileThatCannotBeReadIsAWarningWithTheExceptionAndItsStack()
    {
        using var ws = new TempWorkspace();
        var (_, dst) = Pair(ws);
        string notFits = ws.WriteFile("notes.fit", Encoding.ASCII.GetBytes("not a FITS file, only some text"));
        using var capture = LogCapture.Start();

        var (code, stderr) = Cli(notFits, dst);

        Assert.Equal(1, code);
        Assert.Single(stderr.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'));
        LogCapture.Captured warn = Assert.Single(capture.Events, e => e.Level == LogLevel.Warn);
        Assert.Equal("WcsCopy", warn.Logger);
        Assert.Equal("cannot read the source " + notFits, warn.Message);
        Assert.IsType<InvalidDataException>(warn.Exception);
        Assert.Contains("System.IO.InvalidDataException: ", warn.Line, StringComparison.Ordinal);
        Assert.Contains("   at CopyWcs.FitsHeader.ReadFrom", warn.Line, StringComparison.Ordinal);
        Assert.DoesNotContain(capture.Events, e => e.Level >= LogLevel.Error);
        Assert.Matches(@"^copy done refused: cannot read the source in \d+ ms \(source \d+ ms\)$", DoneLine(capture));
    }

    [Theory]
    [InlineData("missingdest", @"cannot read the destination in \d+ ms \(source \d+ ms, dest \d+ ms\)")]
    [InlineData("dims", @"dimensions differ \(source 100x80, destination 200x80\) in \d+ ms \(source \d+ ms, dest \d+ ms, select \d+ ms\)")]
    [InlineData("nokey", @"no relevant key in the source in \d+ ms \(source \d+ ms, dest \d+ ms, select \d+ ms\)")]
    public void R042_EveryRefusalSaysWhyInTheDoneLine(string scenario, string reason)
    {
        using var ws = new TempWorkspace();
        var (src, dst) = Pair(ws);
        string[] args = scenario switch
        {
            "missingdest" => [src, ws.FilePath("missing.fit")],
            "dims" => [src, ws.WriteFile("wide.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(naxis1: 200)))],
            "nokey" => [ws.WriteFile("unsolved.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards())), dst, "--no-optical"],
            _ => throw new ArgumentException(scenario),
        };
        using var capture = LogCapture.Start();

        Assert.Equal(1, Cli(args).Code);

        Assert.Matches("^copy done refused: " + reason + "$", DoneLine(capture));
        Assert.Contains(capture.Events, e => e.Level == LogLevel.Warn && e.Logger == "WcsCopy");
    }

    [Fact]
    public void R042_AWriteThatFailsIsAnErrorWithTheException()
    {
        using var ws = new TempWorkspace();
        var (src, dst) = Pair(ws);
        string output = ws.FilePath(Path.Combine("missing", "out.fit"));
        using var capture = LogCapture.Start();

        Assert.Equal(1, Cli(src, dst, "-o", output).Code);

        LogCapture.Captured error = Assert.Single(capture.Events, e => e.Level == LogLevel.Error);
        Assert.Equal("the write of " + output + " failed", error.Message);
        Assert.IsType<DirectoryNotFoundException>(error.Exception);
        Assert.Matches(@"^copy done refused: the write failed in \d+ ms \(source \d+ ms, dest \d+ ms, select \d+ ms, write \d+ ms\)$", DoneLine(capture));
    }

    [Fact]
    public void R042_TheTwoWarningsAreLogged()
    {
        using var ws = new TempWorkspace();
        var (src, _) = Pair(ws);
        string wide = ws.WriteFile("wide.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(naxis1: 200)));
        string unsolved = ws.WriteFile("unsolved.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));
        string plain = ws.WriteFile("plain.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards()));

        using (var capture = LogCapture.Start())
        {
            Assert.Equal(0, Cli(src, wide, "--force").Code);
            Assert.Contains("dimensions differ: source 100x80, destination 200x80; forced, the copy goes on", capture.Messages);
        }

        using (var capture = LogCapture.Start())
        {
            Assert.Equal(0, Cli(unsolved, plain).Code);
            Assert.Contains("the source has no CTYPE1, it does not look plate-solved; the copy goes on", capture.Messages);
        }
    }

    /// <summary>A control character in a card (here a NUL in a comment) is written as <c>\x00</c>:
    /// a raw NUL in the file would make grep take the whole log for binary (FlatCheck's review, A4).</summary>
    [Fact]
    public void R042_AControlCharacterInACardIsEscaped_NoNulByteInTheLog()
    {
        using var ws = new TempWorkspace();
        List<string> cards = FitsTestData.PlateSolvedCards();
        int obj = cards.FindIndex(c => c.StartsWith("OBJECT", StringComparison.Ordinal));
        cards[obj] = FitsTestData.Card("OBJECT", "'M42'", "a\0b");
        var (src, dst) = Pair(ws, source: cards);
        using var capture = LogCapture.Start();

        Assert.Equal(0, Cli(src, dst, "--dry-run").Code);

        Assert.DoesNotContain('\0', capture.Text);
        Assert.Contains(@"card OBJECT: OBJECT  = 'M42' / a\x00b | dest: OBJECT  = 'M42'", capture.Messages);
    }

    /// <summary>
    /// A header read from a stream that cannot seek (a named pipe) has no length to tell: the
    /// read line says so instead of throwing, and the run goes on as it did before the log
    /// (R-041, R-042; the review of 2026-10-30 found <c>FileStream.Length</c> ending such a run
    /// with an unhandled exception). Windows only: the test returns elsewhere.
    /// </summary>
    [Fact]
    public async Task R042_AHeaderReadFromAPipeIsLoggedWithoutItsSize()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var ws = new TempWorkspace();
        var (_, dst) = Pair(ws);
        string name = "copywcs-tests-" + Guid.NewGuid().ToString("N");
        byte[] header = FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards());
        var done = new TaskCompletionSource();
        // A run that never opens the source must fail the test, not hang the gate: the wait for
        // the client gives up after 30 s.
        using var connect = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var server = new NamedPipeServerStream(name, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        Task serve = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(connect.Token);
            await server.WriteAsync(header);
            await done.Task;
        });
        string pipe = @"\\.\pipe\" + name;
        using var capture = LogCapture.Start();

        int code;
        try
        {
            // The run is bounded too: one that read past the header would wait on the pipe for
            // ever; after 30 s the test fails here, and disposing the server ends that read.
            code = (await Task.Run(() => Cli(pipe, dst, "--dry-run")).WaitAsync(TimeSpan.FromSeconds(30))).Code;
        }
        finally
        {
            done.SetResult();
            await serve;
        }

        Assert.Equal(0, code);
        Assert.Contains(capture.Messages, m => m.StartsWith(
            "read " + pipe + " (size unknown) bytes=none cards=20 blocks=1 dataOffset=2880 data=none BITPIX=16 ", StringComparison.Ordinal));
    }

    /// <summary>
    /// A stream that says it can seek and then throws when asked its length (a share that drops
    /// after the header was read, a query the system denies or aborts) gives the read line no
    /// length, whatever it throws: a value the log reads never stops a run (R-041; the third
    /// review of 2026-10-30 found only an <see cref="IOException"/> caught, and nothing making the
    /// query throw). The pipe test above pins the line a missing length gives.
    /// </summary>
    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(NotSupportedException))]
    [InlineData(typeof(OperationCanceledException))]
    public void R042_ALengthThatCannotBeReadIsNone_WhateverTheQueryThrows(Type thrown)
    {
        using var stream = new LengthThrowingStream((Exception)Activator.CreateInstance(thrown)!);

        Assert.Null(FitsHeader.LengthOf(stream));
    }

    /// <summary>A stream that can seek gives its length to the read line.</summary>
    [Fact]
    public void R042_ASeekableStreamGivesItsLength()
    {
        using var stream = new MemoryStream(new byte[2880]);

        Assert.Equal(2880, FitsHeader.LengthOf(stream));
    }

    /// <summary>
    /// A card is written as the file holds it: a HISTORY card built from a source named with a
    /// letter outside ASCII is logged with the question mark the file gets for it, not with the
    /// letter (the review of 2026-10-30).
    /// </summary>
    [Fact]
    public void R042_ACardIsLoggedAsTheFileHoldsIt_ALetterOutsideAsciiAsAQuestionMark()
    {
        using var ws = new TempWorkspace();
        byte[] data = FitsTestData.DeterministicData(400);
        // A-ring, one letter outside ASCII, by its code point so this file stays ASCII.
        string src = ws.WriteFile("Nebula-" + (char)0xC5 + ".fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        string dst = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));
        using (var capture = LogCapture.Start())
        {
            Assert.Equal(0, Cli(src, dst).Code);
            Assert.Contains("appended HISTORY WCS copied from Nebula-?.fit", capture.Messages);
        }

        // The file's own bytes, not the reader's decoding, which turns any byte above 0x7F into a
        // question mark itself: Latin-1 keeps each byte as it is. The whole 80-byte card, so
        // nothing may follow the name.
        Assert.Contains("HISTORY WCS copied from Nebula-?.fit".PadRight(80), Encoding.Latin1.GetString(File.ReadAllBytes(dst)), StringComparison.Ordinal);
    }

    [Fact]
    public void R042_ARunThatEndsWithoutAnOutcomeSaysSoWithItsStages()
    {
        Logger log = LogManager.GetLogger("RunClockTest");
        using var capture = LogCapture.Start();

        using (RunClock clock = RunClock.Start(log, "copy", "source=a.fit dest=b.fit"))
        {
            clock.Lap("source");
        }

        Assert.Equal("copy start source=a.fit dest=b.fit", capture.Messages[0]);
        Assert.Matches(@"^copy ended without an outcome after \d+ ms \(source \d+ ms\)$", capture.Messages[1]);
    }

    // ---------------------------------------------------------------- R-043: the console's own lines

    [Fact]
    public void R043_TheConsoleLogsAUsageErrorHelpAndARefusedLang()
    {
        using (var capture = LogCapture.Start())
        {
            Assert.Equal(2, Cli("--lang", "en", "one.fit").Code);
            Assert.Contains(capture.Events, e => e.Logger == "Cli" && e.Level == LogLevel.Warn
                && e.Message == "usage error (exit 2): Exactly two files are needed: the source and the destination.");
        }

        using (var capture = LogCapture.Start())
        {
            Assert.Equal(0, Cli("--help").Code);
            Assert.Contains(capture.Events, e => e.Logger == "Cli" && e.Message == "help shown (exit 0)");
        }

        using (var capture = LogCapture.Start())
        {
            Assert.Equal(1, Cli("--lang", "xx").Code);
            Assert.Contains(capture.Events, e => e.Logger == "Cli" && e.Level == LogLevel.Warn
                && e.Message == "refused (exit 1): --lang expects en or ro, got 'xx'");
        }
    }

    // ---------------------------------------------------------------- R-044: the level

    [Fact]
    public void R044_TraceHoldsEveryCardOfEveryHeaderReadAndWritten()
    {
        using var ws = new TempWorkspace();
        var (src, dst) = Pair(ws);
        string output = ws.FilePath("out.fit");
        using var capture = LogCapture.Start();

        Assert.Equal(0, Cli(src, dst, "-o", output).Code);

        var trace = capture.Events.Where(e => e.Level == LogLevel.Trace).Select(e => e.Message).ToList();
        Assert.Contains(src + " card 1: SIMPLE  = T", trace);
        Assert.Contains(dst + " card 1: SIMPLE  = T", trace);
        Assert.Contains(output + " card 1: SIMPLE  = T", trace);
        Assert.Equal(20, trace.Count(m => m.StartsWith(src + " card ", StringComparison.Ordinal)));
        Assert.Equal(21, trace.Count(m => m.StartsWith(output + " card ", StringComparison.Ordinal)));
        Assert.DoesNotContain(capture.Events, e => e.Level != LogLevel.Trace && e.Message.Contains(" card 1: ", StringComparison.Ordinal));
    }

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

    /// <summary>The variable is the one the README tells the user to set: a typo in the constant
    /// would leave every other test green and <c>set COPYWCS_LOG=trace</c> doing nothing (the
    /// review of 2026-10-30).</summary>
    [Fact]
    public void R044_TheVariableIsCOPYWCS_LOG()
    {
        Assert.Equal("COPYWCS_LOG", LogSetup.LevelVariable);
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

    /// <summary>
    /// Two processes of one executable write into the same file at full speed, and every line of
    /// both arrives whole, once, past the 1 MiB roll too (R-040, R-045), through the build of
    /// <c>NLog.Targets.AtomicFile</c> the console ships, its <c>net8.0</c> one; the window's
    /// build has its own test in CopyWcs.Wpf.Tests. NLog's plain target failed this in FlatCheck:
    /// closed between lines it lost about 1 % of them, kept open one writer lost all of its
    /// (FlatCheck's ADR 0070).
    /// </summary>
    [Fact]
    public void R045_TwoProcessesWritingAtFullSpeedKeepEveryLine()
    {
        Assert.Null(typeof(AtomicFileTarget).Assembly.GetCustomAttribute<TargetPlatformAttribute>());
        using var ws = new TempWorkspace();
        string folder = ws.FilePath("log");
        const int Lines = 4000;   // about 1.6 MiB together: the writers cross the roll

        TwoWriters result = WriteTwoAtFullSpeed(folder, "copywcs", Lines);

        foreach (string f in result.Files)
        {
            _output.WriteLine($"{Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        }

        _output.WriteLine($"whole lines: {result.FromA.Count} from A, {result.FromB.Count} from B; broken: {result.Broken}");
        AssertEveryLineKept(folder, result, Lines);
    }

    // ---------------------------------------------------------------- R-046: the language

    /// <summary>The arguments of one console scenario, every file written into <paramref name="ws"/>.</summary>
    private static string[] Scenario(TempWorkspace ws, string scenario)
    {
        byte[] data = FitsTestData.DeterministicData(400);
        string solved = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        string processed = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));
        string wide = ws.WriteFile("wide.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(naxis1: 200), data));
        string unsolved = ws.WriteFile("unsolved.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));
        string notFits = ws.WriteFile("notes.fit", Encoding.ASCII.GetBytes("not a FITS file, only some text"));
        return scenario switch
        {
            "copy" => [solved, processed],
            "preview" => [solved, processed, "--dry-run"],
            "newfile" => [solved, processed, "-o", ws.FilePath("out.fit")],
            "forced" => [solved, wide, "--force"],
            "nosolve" => [unsolved, processed],
            "dims" => [solved, wide],
            "nokey" => [unsolved, processed, "--no-optical"],
            "unreadable" => [notFits, processed],
            "writeerror" => [solved, processed, "-o", ws.FilePath(Path.Combine("missing", "out.fit"))],
            _ => throw new ArgumentException(scenario),
        };
    }

    /// <summary>
    /// The same run in English and in Romanian writes the same messages, the times aside: the
    /// log's words never go through the translation (R-046). The exception of a file that cannot
    /// be read keeps its message as the user saw it, but under the line, not in the message.
    /// </summary>
    [Theory]
    [InlineData("copy")]
    [InlineData("preview")]
    [InlineData("newfile")]
    [InlineData("forced")]
    [InlineData("nosolve")]
    [InlineData("dims")]
    [InlineData("nokey")]
    [InlineData("unreadable")]
    [InlineData("writeerror")]
    public void R046_TheLogIsTheSameInBothLanguages(string scenario)
    {
        IReadOnlyList<string> Run(string lang)
        {
            using var ws = new TempWorkspace();
            string[] args = Scenario(ws, scenario);
            using var capture = LogCapture.Start();
            CopyWcsCli.Run([.. args, "--lang", lang], new StringWriter(), new StringWriter());
            return capture.Messages.Select(m => Regex.Replace(m.Replace(ws.Dir, "<ws>", StringComparison.Ordinal), @"\d+ ms\b", "# ms")).ToArray();
        }

        IReadOnlyList<string> en = Run("en");
        IReadOnlyList<string> ro = Run("ro");

        Assert.NotEmpty(en);
        Assert.Equal(en, ro);
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

    /// <summary>A stream that says it can seek, and throws the given exception when asked its length.</summary>
    private sealed class LengthThrowingStream : Stream
    {
        private readonly Exception _thrown;

        public LengthThrowingStream(Exception thrown)
        {
            _thrown = thrown;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => throw _thrown;

        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
