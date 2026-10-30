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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf;
using ImageVisualCompare.Wpf.Imaging;
using ImageVisualCompare.Wpf.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit.Abstractions;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The diagnostic log (R-120 to R-123, ADR 0043): where the files go and how they roll, what a
/// load writes, the level, two writers on one file, and the repository's rules about the log. The
/// file tests run the application's configuration (<see cref="LogSetup.Build"/>) in a private
/// <see cref="LogFactory"/>, which stands for one process, so they touch neither the test
/// assembly's capture (<see cref="LogCapture"/>) nor the tests beside them.
/// </summary>
public sealed class LoggingTests : IDisposable
{
    private const string Exe = "ImageVisualCompare.Wpf";

    private static readonly Regex LinePrefix = new(
        @"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{4} (TRACE|DEBUG|INFO |WARN |ERROR|FATAL) \[\d+:\d+\] \S+ ",
        RegexOptions.Compiled);

    private readonly ITestOutputHelper _output;
    private readonly TempImages _images = new();
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ivc-log-tests-" + Guid.NewGuid().ToString("N"));

    public LoggingTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        _images.Dispose();
        foreach (string file in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_dir, recursive: true);
    }

    // ---------------------------------------------------------------- R-120: the files

    [Fact]
    public void R120_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName()
    {
        string fallback = Path.Combine(_dir, "fallback");

        string folder = LogSetup.ResolveFolder(_dir, fallback, Exe, out bool fellBack);

        Assert.False(fellBack);
        Assert.Equal(Path.Combine(_dir, "log"), folder);
        Assert.Empty(Directory.GetFileSystemEntries(folder)); // the probe is gone
        Assert.False(Directory.Exists(fallback));

        using (LogFactory factory = Factory(LogSetup.Build(Exe, folder, LogLevel.Debug)))
        {
            factory.GetLogger("Test").Info("hello");
            factory.Shutdown();
        }

        string file = Path.Combine(folder, Exe + ".log");
        Assert.Equal([file], Directory.GetFiles(folder));
        byte[] bytes = File.ReadAllBytes(file);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "no byte order mark");
        string line = Encoding.UTF8.GetString(bytes).TrimEnd('\r', '\n');
        Assert.Matches(LinePrefix, line);
        Assert.EndsWith($" INFO  [{Environment.ProcessId}:{Environment.CurrentManagedThreadId}] Test hello", line);
    }

    [Fact]
    public void R120_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback()
    {
        // A file named log where the folder should be: the folder cannot be created.
        File.WriteAllBytes(Path.Combine(_dir, "log"), [1]);
        string fallbackRoot = Path.Combine(_dir, "LocalAppData");

        string folder = LogSetup.ResolveFolder(_dir, fallbackRoot, Exe, out bool fellBack);

        Assert.True(fellBack);
        Assert.Equal(Path.Combine(fallbackRoot, "ImageVisualCompare", "log"), folder);
        Assert.Empty(Directory.GetFileSystemEntries(folder));
        Assert.Contains(
            "; log=" + folder + " (fallback: the folder beside the executable cannot be written); level=Debug",
            LogSetup.StartupLine(Exe, [], folder, fellBack: true, LogLevel.Debug));
    }

    [Fact]
    public void R120_TheLogRollsAt1MiBAndKeepsAtMost5Files()
    {
        string folder = Path.Combine(_dir, "log");

        WriteThrough(folder, 8 * LogSetup.FileSizeLimit, "first");

        string[] files = FilesOf(folder);
        foreach (string f in files)
        {
            _output.WriteLine($"{Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        }

        Assert.Equal(Directory.GetFiles(folder).Length, files.Length); // nothing else is left
        Assert.Equal(
            ["ImageVisualCompare.Wpf_03.log", "ImageVisualCompare.Wpf_04.log", "ImageVisualCompare.Wpf_05.log", "ImageVisualCompare.Wpf_06.log", "ImageVisualCompare.Wpf_07.log"],
            files.Select(Path.GetFileName));
        // A file ends one line past the limit: no line is cut, and a line here is about 4 KB.
        Assert.All(files, f => Assert.InRange(new FileInfo(f).Length, 1, LogSetup.FileSizeLimit + 4200));
        Assert.EndsWith("first last", File.ReadAllLines(files[^1])[^1]);

        // The next process goes on in the newest file: the same five names, the newest grown.
        long newestBefore = new FileInfo(files[^1]).Length;
        WriteThrough(folder, 1000, "next");
        string[] after = FilesOf(folder);
        Assert.Equal(files.Select(Path.GetFileName), after.Select(Path.GetFileName));
        Assert.True(new FileInfo(after[^1]).Length > newestBefore, "the newest file did not grow");
        Assert.EndsWith("next last", File.ReadAllLines(after[^1])[^1]);
    }

    [Fact]
    public void R120_AReadOnlyNewestLogFileSendsTheLogToTheFallback()
    {
        string beside = Path.Combine(_dir, "exe");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, Exe + ".log"), "old\n");
        string newest = Path.Combine(folder, Exe + "_03.log");
        File.WriteAllText(newest, "newest\n");
        File.SetAttributes(newest, FileAttributes.ReadOnly);

        string chosen = LogSetup.ResolveFolder(beside, Path.Combine(_dir, "fallback"), Exe, out bool fellBack);

        Assert.True(fellBack);
        Assert.Equal(Path.Combine(_dir, "fallback", LogSetup.FallbackFolder, LogSetup.FolderName), chosen);
    }

    [Fact]
    public void R120_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe()
    {
        string beside = Path.Combine(_dir, "exe");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        using LogFactory other = Factory(LogSetup.Build(Exe, folder, LogLevel.Debug));
        other.GetLogger("Other").Info("held open");

        string chosen = LogSetup.ResolveFolder(beside, Path.Combine(_dir, "fallback"), Exe, out bool fellBack);

        Assert.False(fellBack);
        Assert.Equal(folder, chosen);
        other.Shutdown();
    }

    [Theory]
    [InlineData(@"C:\Tools\ImageVisualCompare.Wpf.exe", "ImageVisualCompare.Wpf")]
    [InlineData(@"C:\Tools\ivc-1.2.exe", "ivc-1.2")]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", "ImageVisualCompare.Wpf")]
    [InlineData(null, "ImageVisualCompare.Wpf")]
    public void R120_TheFilesBeginWithTheExecutablesName(string? processPath, string expected)
    {
        Assert.Equal(expected, LogSetup.ExecutableName(processPath, Exe));
    }

    [Fact]
    public void R120_TheConfigurationHasOneFileTargetAndNoOther()
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

    // ---------------------------------------------------------------- R-121: what is written

    [Fact]
    public void R121_TheFirstLineCarriesTheFacts()
    {
        string line = LogSetup.StartupLine(Exe, [@"D:\astro\a b.fit", "--lang=ro"], @"D:\tools\log", fellBack: false, LogLevel.Debug);

        Assert.StartsWith(Exe + " " + VersionInfo.Value + " starting; exe=", line);
        Assert.Contains(@"; args=[""D:\astro\a b.fit"", ""--lang=ro""]; cwd=", line);
        Assert.Contains(@"; log=D:\tools\log; level=Debug; os=", line);
        Assert.DoesNotContain("fallback", line);
        foreach (string part in new[] { "; cpus=", "; memory=", " GiB; runtime=", "; culture=", "; tz=", " (UTC" })
        {
            Assert.Contains(part, line);
        }

        Assert.EndsWith("; pid=" + Environment.ProcessId, line);
    }

    [Fact]
    public void R121_TheSameCrashSeenByTwoHooksIsWrittenOnce()
    {
        var crash = new InvalidOperationException("the window fell over");
        using LogCapture capture = LogCapture.Start();

        LogSetup.Fatal(crash, "unhandled exception on the window's thread, the window closes");
        LogSetup.Fatal(crash, "unhandled exception, the process ends");
        LogSetup.Fatal(new InvalidOperationException("another"), "unhandled exception, the process ends");

        var fatal = capture.Events.Where(e => e.Level == LogLevel.Fatal && e.Logger == Loggers.Process).ToList();
        Assert.Equal(3, fatal.Count);
        Assert.Same(crash, fatal[0].Exception);
        Assert.Contains("System.InvalidOperationException: the window fell over", fatal[0].Line);
        Assert.Null(fatal[1].Exception);
        Assert.Equal("unhandled exception, the process ends: the exception logged above", fatal[1].Message);
        Assert.NotNull(fatal[2].Exception);
    }

    [Fact]
    public async Task R121_ALoadLogsBothFilesWhatTheDecoderMadeOfThemAndTheTimeOfEachStage()
    {
        string before = _images.WritePng("before.png", PixelFormats.Rgb48, 64, 48);
        string after = _images.WritePng("after.png", PixelFormats.Gray8, 64, 48);
        using LogCapture capture = LogCapture.Start();

        ImagePairLoadResult result = await new ImagePairLoader(new RoutingImageLoader()).LoadAsync(before, after);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                $"load start A={before} B={after} mode=Linear",
                $"probed A: {before} ({LogText.FileMiB(before)}) 64x48 bits=16 channels=Rgb float=no asStored=no",
                $"probed B: {after} ({LogText.FileMiB(after)}) 64x48 bits=8 channels=Gray float=no asStored=no",
                "decoded A: range=none mode=Linear",
                "decoded B: range=none mode=Linear",
                "load done mode=Linear padded=no canvas=64x48 buffers=0.0 MiB in # ms (probe A # ms, probe B # ms, decode A # ms, decode B # ms)",
            ],
            WithoutTimes(capture.MessagesOf(Loggers.Load)));
        Assert.Equal(
            [
                $"probed {before}: decoder=PngBitmapDecoder format=Rgb48 bpp=48 64x48 profiles=0",
                $"probed {after}: decoder=PngBitmapDecoder format=Gray8 bpp=8 64x48 profiles=0",
                $"decoding {before}: decoder=PngBitmapDecoder format=Rgb48 bpp=48 64x48 profiles=0",
                $"decoding {after}: decoder=PngBitmapDecoder format=Gray8 bpp=8 64x48 profiles=0",
            ],
            capture.MessagesOf(Loggers.Wic));
        Assert.All(capture.Events, e => Assert.Matches(LinePrefix, e.Line));
    }

    [Fact]
    public async Task R121_AFailedLoadLogsTheExceptionWithItsStackBesideTheMessageShown()
    {
        string before = _images.WritePng("before.png", PixelFormats.Bgr24, 32, 32);
        string corrupt = _images.WriteCorrupt("broken.png");
        using LogCapture capture = LogCapture.Start();

        ImagePairLoadResult result = await new ImagePairLoader(new RoutingImageLoader()).LoadAsync(before, corrupt);

        Assert.False(result.IsSuccess);
        LogCapture.Captured failed = Assert.Single(capture.Events, e => e.Level == LogLevel.Error);
        Assert.Equal(Loggers.Load, failed.Logger);
        Assert.NotNull(failed.Exception);
        Assert.Equal($"could not read {corrupt}; the user is told: {LogText.OneLine(result.Error!)}", failed.Message);
        Assert.Contains(failed.Exception!.GetType().FullName!, failed.Line);
        Assert.Contains("   at ", failed.Line);
        Assert.Equal($"load done failed on {corrupt} in # ms (probe A # ms)", WithoutTimes(capture.MessagesOf(Loggers.Load))[^1]);
    }

    [Fact]
    public async Task R121_APaddingQuestionIsLoggedWithTheSizesAndTheAnswer()
    {
        var loader = new FakeLoader(path => new ImageInfo(path, path == "a.png" ? 100 : 200, 100, 8, ColorChannels.Rgb));
        using LogCapture capture = LogCapture.Start();

        ImagePairLoadResult declined = await new ImagePairLoader(loader).LoadAsync("a.png", "b.png", confirmPadding: _ => Task.FromResult(false));
        ImagePairLoadResult padded = await new ImagePairLoader(loader).LoadAsync("a.png", "b.png", confirmPadding: _ => Task.FromResult(true));

        Assert.True(declined.WasDeclined);
        Assert.True(padded.IsSuccess);
        IReadOnlyList<string> lines = WithoutTimes(capture.MessagesOf(Loggers.Load));
        Assert.Equal(2, lines.Count(l => l == "the sizes differ: A 100x100, B 200x100, canvas 200x100; asking whether to pad"));
        Assert.Contains("load done declined: the padding was answered no in # ms (probe A # ms, probe B # ms, question # ms)", lines);
        Assert.Contains("the padding was answered yes", lines);
        Assert.Contains(lines, l => l.StartsWith("load done mode=Linear padded=yes canvas=200x100 buffers=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task R121_AMismatchWithNoOneToAskIsLoggedAsRefused()
    {
        var loader = new FakeLoader(path => new ImageInfo(path, path == "a.png" ? 100 : 200, 100, 8, ColorChannels.Rgb));
        using LogCapture capture = LogCapture.Start();

        ImagePairLoadResult result = await new ImagePairLoader(loader).LoadAsync("a.png", "b.png");

        Assert.False(result.IsSuccess);
        Assert.Equal(
            [
                "load start A=a.png B=b.png mode=Linear",
                "probed A: a.png (?) 100x100 bits=8 channels=Rgb float=no asStored=no",
                "probed B: b.png (?) 200x100 bits=8 channels=Rgb float=no asStored=no",
                "the sizes differ: A 100x100, B 200x100, canvas 200x100; no one to ask, so the pair is refused",
                "load done refused: the sizes differ in # ms (probe A # ms, probe B # ms)",
            ],
            WithoutTimes(capture.MessagesOf(Loggers.Load)));
    }

    [Fact]
    public async Task R121_AModeThatFallsBackToLinearIsLoggedWithWhy()
    {
        var loader = new FakeLoader(path => new ImageInfo(path, 2, 2, 8, ColorChannels.Rgb));
        using LogCapture capture = LogCapture.Start();

        await new ImagePairLoader(loader).LoadAsync("a.png", "b.png", mode: DisplayMode.Histogram);

        Assert.Contains("mode Histogram asked, Linear used: not both files are shown as stored", capture.MessagesOf(Loggers.Load));
    }

    [Fact]
    public async Task R121_AFloatFitsLogsItsLayoutAndTheRangeItsSamplesWereFoundIn()
    {
        string fits = _images.WriteBytes("float.fit", FitsFixture.Image(-32, 4, 3, 1, [.. Enumerable.Repeat(0.25, 12)]));
        using LogCapture capture = LogCapture.Start();

        LoadedImage image = await new FitsImageLoader().LoadAsync(fits);

        Assert.Null(image.Info.ValueRange);
        Assert.Equal(
            [
                $"decoding {fits}: 4x3 planes=1 bitpix=-32 bzero=0 bscale=1 data at 2880",
                $"{fits}: float samples counted in 0..1: 0 of 12 above 2",
            ],
            capture.Events.Where(e => e.Logger == Loggers.Fits && e.Level == LogLevel.Debug).Select(e => e.Message));
    }

    [Fact]
    public void R121_AFitsLayoutWritesBzeroAndBscaleExactly()
    {
        // An unsigned 32-bit FITS, as its BZERO says: six significant digits would write it 2.14748E+09.
        string fits = _images.WriteBytes(
            "unsigned.fit",
            FitsFixture.Image(32, 4, 3, 1, new double[12], FitsFixture.Card("BZERO", 2147483648), FitsFixture.Card("BSCALE", 1.0000001)));
        using LogCapture capture = LogCapture.Start();

        new FitsImageLoader().Probe(fits);

        Assert.Equal(
            $"probed {fits}: 4x3 planes=1 bitpix=32 bzero=2147483648 bscale=1.0000001 data at 2880",
            Assert.Single(capture.MessagesOf(Loggers.Fits), m => m.StartsWith("probed ", StringComparison.Ordinal)));
    }

    [Fact]
    public void R121_AFloatTiffsLineSaysWhatItsSamplesAre_ThoughWpfHasNoNameForItsFormat()
    {
        // The 96 bpp float RGB of Siril's and GraXpert's exports: WPF's name for it is its Default.
        string tiff = _images.WriteFloatTiff("float.tif", 4, 2, 3, new float[24]);
        using LogCapture capture = LogCapture.Start();

        new WicImageLoader().Probe(tiff);

        Assert.Equal(
            $"probed {tiff}: decoder=TiffBitmapDecoder format=unnamed bpp=96 4x2 SampleFormat=3 BitsPerSample=32 SamplesPerPixel=3 profiles=0",
            Assert.Single(capture.MessagesOf(Loggers.Wic)));
    }

    [Fact]
    public void R121_TheLastLineSaysTheExitCodeOrThatAnUnhandledExceptionEndedTheProcess()
    {
        Assert.Equal("exit 0 after 12.9 s", LogSetup.ExitLine(0, TimeSpan.FromMilliseconds(12900)));
        Assert.Equal("exit 3 after 0.8 s", LogSetup.ExitLine(3, TimeSpan.FromMilliseconds(800)));
        Assert.Equal("ended by an unhandled exception after 0.8 s", LogSetup.ExitLine(null, TimeSpan.FromMilliseconds(800)));
    }

    [Fact]
    public async Task R121_ACancelledLoadIsOneLineWithoutAStack()
    {
        var loader = new FakeLoader(path => new ImageInfo(path, 2, 2, 8, ColorChannels.Rgb));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using LogCapture capture = LogCapture.Start();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ImagePairLoader(loader).LoadAsync("a.png", "b.png", cancellationToken: cancelled.Token));

        Assert.Equal("load done cancelled during a.png in # ms", WithoutTimes(capture.MessagesOf(Loggers.Load))[^1]);
        Assert.DoesNotContain(capture.Events, e => e.Exception is not null || e.Level >= LogLevel.Warn);
    }

    [Fact]
    public async Task R121_ALoadThatThrowsPastItsCatchesLeavesTheStagesItReached()
    {
        var loader = new FakeLoader(
            path => new ImageInfo(path, 2, 2, 8, ColorChannels.Rgb),
            _ => throw new InvalidOperationException("a defect"));
        using LogCapture capture = LogCapture.Start();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ImagePairLoader(loader).LoadAsync("a.png", "b.png"));

        Assert.Equal(
            "load ended without an outcome after # ms (probe A # ms, probe B # ms)",
            WithoutTimes(capture.MessagesOf(Loggers.Load))[^1]);
    }

    [Fact]
    public void TheElapsedTimeHasTwoForms_MillisecondsAndSecondsWithOneDecimal()
    {
        Assert.Equal("12.9 s", LogText.Seconds(TimeSpan.FromMilliseconds(12900)));
        Assert.Equal("0.8 s", LogText.Seconds(TimeSpan.FromMilliseconds(800)));
        Assert.Equal("5 ms", LogText.Ms(5));
    }

    // ---------------------------------------------------------------- R-122: the level

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
    public void R122_TheLevelComesFromTheEnvironmentVariable(string? value, string level)
    {
        Assert.Equal(LogLevel.FromString(level), LogSetup.LevelFrom(value));
    }

    [Fact]
    public void R122_OffCreatesNeitherTheFolderNorAFile()
    {
        string exe = Path.Combine(_dir, Exe + ".exe");

        LogPlace off = LogSetup.Plan(Exe, exe, _dir, Path.Combine(_dir, "fallback"), "off");

        Assert.Null(off.Folder);
        Assert.Empty(Directory.GetFileSystemEntries(_dir));
        Assert.Empty(LogSetup.Build(Exe, Path.Combine(_dir, "log"), LogLevel.Off).AllTargets);
        Assert.Empty(LogSetup.Build(Exe, null, LogLevel.Debug).AllTargets);

        LogPlace trace = LogSetup.Plan(Exe, exe, _dir, Path.Combine(_dir, "fallback"), "trace");
        Assert.Equal(LogLevel.Trace, trace.Level);
        Assert.Equal(Path.Combine(_dir, "log"), trace.Folder);
    }

    [Fact]
    public void R122_TraceHoldsEveryFitsHeaderCard()
    {
        string fits = _images.WriteBytes("cards.fit", FitsFixture.Image(16, 4, 3, 1, new double[12], FitsFixture.Text("OBJECT", "Veil")));
        using LogCapture capture = LogCapture.Start();

        new FitsImageLoader().Probe(fits);

        string[] cards = [.. capture.Events.Where(e => e.Level == LogLevel.Trace).Select(e => e.Message)];
        string[] written = [FitsFixture.Card("SIMPLE", "T"), .. FitsFixture.ImageCards(16, 4, 3, 1), FitsFixture.Text("OBJECT", "Veil"), "END"];
        Assert.Equal(written.Select(card => "card " + card.TrimEnd()), cards);
        Assert.Contains("card BITPIX  =                   16", cards);
        Assert.DoesNotContain(capture.Events, e => e.Level > LogLevel.Trace && e.Message.Contains("card ", StringComparison.Ordinal));
    }

    [Fact]
    public void R122_AFitsTheReaderRefusesStillHasItsCardsWrittenAtTrace()
    {
        // BITPIX 12 is no depth the reader knows: the probe refuses the file, and the header is
        // what says why.
        string[] header =
        [
            FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 12), FitsFixture.Card("NAXIS", 2),
            FitsFixture.Card("NAXIS1", 4), FitsFixture.Card("NAXIS2", 3),
        ];
        string fits = _images.WriteBytes("refused.fit", [.. FitsFixture.Header(header), .. new byte[2880]]);
        using LogCapture capture = LogCapture.Start();

        Assert.Throws<NotSupportedException>(() => new FitsImageLoader().Probe(fits));

        Assert.Equal(
            [.. header.Select(card => "card " + card.TrimEnd()), "card END"],
            capture.Events.Where(e => e.Level == LogLevel.Trace).Select(e => e.Message));
    }

    [Fact]
    public void R122_TheCardsOfAnImageInAnExtensionFollowThePrimaryHeadersOnes()
    {
        string[] primary = [FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 0), FitsFixture.Card("EXTEND", "T")];
        string[] extension =
        [
            FitsFixture.Text("XTENSION", "IMAGE"), .. FitsFixture.ImageCards(16, 4, 3, 1),
            FitsFixture.Card("PCOUNT", 0), FitsFixture.Card("GCOUNT", 1),
        ];
        string fits = _images.WriteBytes(
            "extension.fit", [.. FitsFixture.Header(primary), .. FitsFixture.Header(extension), .. FitsFixture.Data(16, new double[12])]);
        using LogCapture capture = LogCapture.Start();

        ImageInfo info = new FitsImageLoader().Probe(fits);

        Assert.Equal(4, info.PixelWidth);
        Assert.Equal(
            [.. primary.Select(card => "card " + card.TrimEnd()), "card END", .. extension.Select(card => "card " + card.TrimEnd()), "card END"],
            capture.Events.Where(e => e.Level == LogLevel.Trace).Select(e => e.Message));
    }

    [Fact]
    public void R122_TheCardsOfAnImageBehindATablesDataAreWrittenToo()
    {
        // An empty primary header, a binary table of three 10-byte rows and its block of data,
        // then the image: the reader steps over the table's data, and so does the walk.
        string[] primary = [FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 0), FitsFixture.Card("EXTEND", "T")];
        string[] table =
        [
            FitsFixture.Text("XTENSION", "BINTABLE"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 2),
            FitsFixture.Card("NAXIS1", 10), FitsFixture.Card("NAXIS2", 3), FitsFixture.Card("PCOUNT", 0), FitsFixture.Card("GCOUNT", 1),
        ];
        string[] image = [FitsFixture.Text("XTENSION", "IMAGE"), .. FitsFixture.ImageCards(8, 2, 2, 1)];
        byte[] tableData = new byte[2880];
        new Random(5).NextBytes(tableData.AsSpan(0, 30));
        string fits = _images.WriteBytes(
            "table.fit",
            [.. FitsFixture.Header(primary), .. FitsFixture.Header(table), .. tableData, .. FitsFixture.Header(image), .. FitsFixture.Data(8, [1, 2, 3, 4])]);
        using LogCapture capture = LogCapture.Start();

        ImageInfo info = new FitsImageLoader().Probe(fits);

        Assert.Equal(2, info.PixelWidth);
        Assert.Equal(
            [
                .. primary.Select(card => "card " + card.TrimEnd()), "card END",
                .. table.Select(card => "card " + card.TrimEnd()), "card END",
                .. image.Select(card => "card " + card.TrimEnd()), "card END",
            ],
            capture.Events.Where(e => e.Level == LogLevel.Trace).Select(e => e.Message));
    }

    [Fact]
    public void R122_ACardWithBytesThatAreNotAsciiIsWrittenWithThemAsQuestionMarks_AndTheWalkGoesOn()
    {
        // A tab and a UTF-8 letter in a value, as a hand-typed OBJECT can carry: the log writes
        // each of their bytes as a question mark, and the cards after them still matter.
        string exposure = FitsFixture.Card("EXPTIME", 120);
        byte[] file = FitsFixture.Image(16, 4, 3, 1, new double[12], FitsFixture.Text("OBJECT", "Voal_NGC##"), exposure);
        int at = Encoding.ASCII.GetString(file).IndexOf("Voal_NGC##", StringComparison.Ordinal);
        file[at + 4] = 0x09; // the underscore becomes a tab
        file[at + 8] = 0xC3; // the two marks become an é in UTF-8
        file[at + 9] = 0xA9;
        string fits = _images.WriteBytes("odd.fit", file);
        using LogCapture capture = LogCapture.Start();

        new FitsImageLoader().Probe(fits);

        string[] cards = [.. capture.Events.Where(e => e.Level == LogLevel.Trace).Select(e => e.Message)];
        Assert.Contains("card OBJECT  = 'Voal?NGC??'", cards);
        Assert.Equal(["card " + exposure.TrimEnd(), "card END"], cards[^2..]);
        Assert.All(cards, card => Assert.DoesNotMatch(@"[^\x20-\x7E]", card));
    }

    // ---------------------------------------------------------------- R-123: two writers

    /// <summary>
    /// Two processes (two windows) write into the same file at full speed, and every line of both
    /// arrives whole, once, past the 1 MiB roll too. Two factories stand for the two processes;
    /// each opens the file on its own, on a thread of its own released by a barrier so that the
    /// two do overlap. NLog's plain target failed this in FlatCheck: closed between lines it lost
    /// about 1 % of them, kept open one writer lost all of its (its ADR 0070).
    /// </summary>
    [Fact]
    public void R123_TwoProcessesWritingAtFullSpeedKeepEveryLine()
    {
        string folder = Path.Combine(_dir, "log");
        const int Lines = 4000; // about 1.8 MiB together: the writers cross the roll
        using (LogFactory a = Factory(LogSetup.Build(Exe, folder, LogLevel.Debug)))
        using (LogFactory b = Factory(LogSetup.Build(Exe, folder, LogLevel.Debug)))
        {
            using var together = new Barrier(2);
            var ta = new Thread(() => Write(a, "A"));
            var tb = new Thread(() => Write(b, "B"));
            ta.Start();
            tb.Start();
            ta.Join();
            tb.Join();
            a.Shutdown();
            b.Shutdown();

            void Write(LogFactory factory, string writer)
            {
                Logger log = factory.GetLogger(writer);
                together.SignalAndWait();
                for (int i = 0; i < Lines; i++)
                {
                    log.Info($"{writer.ToLowerInvariant()} {i:D5} " + new string(writer[0], 140 + (i % 37)));
                }
            }
        }

        string[] files = FilesOf(folder);
        foreach (string f in files)
        {
            _output.WriteLine($"{Path.GetFileName(f)} {new FileInfo(f).Length} bytes");
        }

        var whole = new Regex(@" (A|B) (a|b) (\d{5}) ([AB]+)$");
        var fromA = new List<int>();
        var fromB = new List<int>();
        var order = new StringBuilder();
        int broken = 0;
        foreach (string line in files.SelectMany(File.ReadAllLines))
        {
            Match m = whole.Match(line);
            int n = m.Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : -1;
            bool ok = LinePrefix.IsMatch(line) && m.Success
                && m.Groups[1].Value.ToLowerInvariant() == m.Groups[2].Value
                && m.Groups[4].Value == new string(m.Groups[1].Value[0], 140 + (n % 37));
            if (!ok)
            {
                broken++;
                continue;
            }

            (m.Groups[1].Value == "A" ? fromA : fromB).Add(n);
            order.Append(m.Groups[1].Value);
        }

        _output.WriteLine($"whole lines: {fromA.Count} from A, {fromB.Count} from B; broken: {broken}");
        Assert.Equal(Directory.GetFiles(folder).Length, files.Length);
        Assert.InRange(files.Length, 2, LogSetup.FilesPerExecutable);
        Assert.Equal(0, broken);
        Assert.Equal(Enumerable.Range(0, Lines), fromA.Order());
        Assert.Equal(Enumerable.Range(0, Lines), fromB.Order());
        // The writers did overlap: their lines interleave in the file, both ways round.
        Assert.Contains("AB", order.ToString(), StringComparison.Ordinal);
        Assert.Contains("BA", order.ToString(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- the repository's rules

    /// <summary>
    /// Only the application's entry point configures the log for a user's run, and nothing but
    /// LogSetup (for it) and LogCapture (for the tests) sets NLog's configuration at all (ADR
    /// 0043). The entry point is Program.Main, not App: the WPF test thread builds an App, and
    /// WPF runs its start there.
    /// </summary>
    [Fact]
    public void OnlyTheEntryPointConfiguresTheLog()
    {
        string[] callers = [.. CodeFiles("src", "tests").Where(f => Regex.IsMatch(File.ReadAllText(f), @"LogSetup\.(Apply|Close)\(")).Select(Rel).Order(StringComparer.Ordinal)];
        string[] configurers = [.. CodeFiles("src", "tests").Where(f => Regex.IsMatch(File.ReadAllText(f), @"LogManager\.(Configuration\s*=[^=]|Setup\()")).Select(Rel).Order(StringComparer.Ordinal)];

        Assert.Equal(["src/ImageVisualCompare.Wpf/Program.cs"], callers);
        Assert.Contains(
            "<StartupObject>ImageVisualCompare.Wpf.Program</StartupObject>",
            File.ReadAllText(Path.Combine(TestData.RepoDir(), "src", "ImageVisualCompare.Wpf", "ImageVisualCompare.Wpf.csproj")),
            StringComparison.Ordinal);
        Assert.Equal(["src/ImageVisualCompare.Wpf/Logging/LogSetup.cs", "tests/ImageVisualCompare.Tests/LogCapture.cs"], configurers);
    }

    /// <summary>
    /// The entry point starts the log before the application exists, hooks the window thread's
    /// crashes and closes the log after the application's run; <c>LogSetup.Apply</c> hooks the
    /// process's own crashes (R-121). No test runs <c>Main</c> or <c>Apply</c>, so their order
    /// and their hooks are read in their source.
    /// </summary>
    [Fact]
    public void TheEntryPointStartsTheLogBeforeTheApplication_AndEveryCrashIsHooked()
    {
        string program = File.ReadAllText(Path.Combine(TestData.RepoDir(), "src", "ImageVisualCompare.Wpf", "Program.cs"));
        string setup = File.ReadAllText(Path.Combine(TestData.RepoDir(), "src", "ImageVisualCompare.Wpf", "Logging", "LogSetup.cs"));

        // The two calls are spelled in pieces, so that OnlyTheEntryPointConfiguresTheLog does not
        // take this test for a caller.
        int main = program.IndexOf("int Main(", StringComparison.Ordinal);
        int apply = program.IndexOf("LogSetup" + ".Apply(", StringComparison.Ordinal);
        int app = program.IndexOf("var app = new App()", StringComparison.Ordinal);
        int run = program.IndexOf("app.Run()", StringComparison.Ordinal);
        int close = program.IndexOf("LogSetup" + ".Close(", StringComparison.Ordinal);
        int closing = run < 0 ? -1 : program.IndexOf("finally", run, StringComparison.Ordinal);
        Assert.True(main >= 0 && apply > main && app > apply, "Program.Main must start the log before it builds the App");
        Assert.True(program.IndexOf("try", main, StringComparison.Ordinal) is int guard && guard > main && guard < apply, "Program.Main must start the log in a try");
        Assert.True(run > app && closing > run && close > closing, "Program.Main must close the log in a finally after the App's run");
        Assert.Contains("app.DispatcherUnhandledException +=", program, StringComparison.Ordinal);
        Assert.Contains("AppDomain.CurrentDomain.UnhandledException +=", setup, StringComparison.Ordinal);
        Assert.Contains("TaskScheduler.UnobservedTaskException +=", setup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The log's words are English whatever the language (R-121): no log call composes its message
    /// through <see cref="UiText"/>, however many lines the call spans; a message the user was
    /// shown is put in a variable first, and logged as shown. Every logger is declared one way, a
    /// <c>static readonly Logger Log</c> field taken from <c>LogManager.GetLogger</c> with a name of
    /// <see cref="Loggers"/>, or, in <c>LogSetup</c>, which runs before the log is configured, a
    /// property fetched on use; so the first check sees every call.
    /// </summary>
    [Fact]
    public void NoLogLineGoesThroughTheInterfaceTexts()
    {
        var call = new Regex(@"\bLog\.(Trace|Debug|Info|Warn|Error|Fatal)\(");
        var declaration = new Regex(@"^\s*private static (readonly Logger Log = |Logger Log => )LogManager\.GetLogger\(Loggers\.\w+\);$");
        var offenders = new List<string>();
        var declarations = new List<string>();
        foreach (string file in CodeFiles("src"))
        {
            string text = File.ReadAllText(file);
            foreach (Match m in call.Matches(text))
            {
                Match end = Regex.Match(text[m.Index..], @"\)\s*;");
                string whole = end.Success ? text.Substring(m.Index, end.Index + end.Length) : text[m.Index..];
                if (Regex.IsMatch(whole, @"\bUiText\b"))
                {
                    offenders.Add(Rel(file) + ":" + LineOf(text, m.Index));
                }
            }

            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if ((lines[i].Contains("GetLogger(", StringComparison.Ordinal) && !declaration.IsMatch(lines[i]))
                    || lines[i].Contains("NLog.Logger", StringComparison.Ordinal)
                    || lines[i].Contains("NLog.LogManager", StringComparison.Ordinal))
                {
                    declarations.Add(Rel(file) + ":" + (i + 1));
                }
            }
        }

        Assert.Empty(offenders);
        Assert.Empty(declarations);
    }

    /// <summary>
    /// The core stays without a package and without a logger (ADR 0043): every fact it computes
    /// comes back to a loader of the application, which logs it.
    /// </summary>
    [Fact]
    public void TheCoreHoldsNoLoggerAndNoPackage()
    {
        string[] files = [.. CodeFiles(Path.Combine("src", "ImageVisualCompare.Core"))];

        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.DoesNotMatch(@"\bNLog\b|\bLogger\b|\bLogManager\b", File.ReadAllText(f)));
        Assert.DoesNotContain(
            "PackageReference",
            File.ReadAllText(Path.Combine(TestData.RepoDir(), "src", "ImageVisualCompare.Core", "ImageVisualCompare.Core.csproj")),
            StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>A private factory on a configuration, standing for one process.</summary>
    private static LogFactory Factory(LoggingConfiguration config) => new() { Configuration = config };

    /// <summary>The executable's files in a folder, oldest first by NLog's numbering: the bare name, then _01, _02...</summary>
    private static string[] FilesOf(string folder) =>
        [.. Directory.GetFiles(folder, Exe + "*.log")
            .Where(f => Regex.IsMatch(Path.GetFileName(f), "^" + Regex.Escape(Exe) + @"(_\d+)?\.log$"))
            .OrderBy(f => Path.GetFileNameWithoutExtension(f).Length)
            .ThenBy(f => f, StringComparer.Ordinal)];

    /// <summary>Writes about this many bytes of 4 KB lines through the application's configuration, then a last line, and closes it.</summary>
    private static void WriteThrough(string folder, long bytes, string tag)
    {
        using LogFactory factory = Factory(LogSetup.Build(Exe, folder, LogLevel.Debug));
        Logger log = factory.GetLogger("Rolling");
        string payload = new('x', 4000);
        long lines = (bytes / (payload.Length + 100)) + 1;
        for (long i = 0; i < lines; i++)
        {
            log.Info($"{tag} {i:D6} {payload}");
        }

        log.Info(tag + " last");
        factory.Shutdown();
    }

    /// <summary>The messages with every time written as <c># ms</c>, the one thing that differs between two runs.</summary>
    internal static IReadOnlyList<string> WithoutTimes(IEnumerable<string> messages) =>
        [.. messages.Select(m => Regex.Replace(m, @"\b\d+ ms\b", "# ms"))];

    /// <summary>The C# sources under folders of the project, without bin/ and obj/.</summary>
    private static IEnumerable<string> CodeFiles(params string[] folders) =>
        folders.SelectMany(folder => Directory.EnumerateFiles(Path.Combine(TestData.RepoDir(), folder), "*.cs", SearchOption.AllDirectories))
            .Where(f => !Regex.IsMatch(Rel(f), @"/(bin|obj)/"));

    /// <summary>A file's path from the project's root, with forward slashes.</summary>
    private static string Rel(string file) => Path.GetRelativePath(TestData.RepoDir(), file).Replace('\\', '/');

    /// <summary>The line number of a position in a text.</summary>
    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    /// <summary>
    /// Probes a file as its function says, and decodes it as a 2 x 2 frozen bitmap at once, or as
    /// its second function says.
    /// </summary>
    internal sealed class FakeLoader(Func<string, ImageInfo> probe, Func<string, LoadedImage>? load = null) : IImageLoader
    {
        public ImageInfo Probe(string path) => probe(path);

        public Task<LoadedImage> LoadAsync(string path, DisplayMode mode = DisplayMode.Linear, CancellationToken cancellationToken = default)
        {
            if (load is not null)
            {
                return Task.FromResult(load(path));
            }

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8);
            bitmap.Freeze();
            return Task.FromResult(new LoadedImage(probe(path), bitmap, mode));
        }
    }
}

/// <summary>
/// The log's words in both interface languages and under another culture (R-121), and what the
/// start writes about its command line. These switch <see cref="UiText.Current"/>, one value for
/// the whole process, so they run with the other tests of the language, alone (ADR 0038).
/// </summary>
[Collection(UiLanguageCollection.Name)]
public sealed class LanguageLoggingTests : IDisposable
{
    private readonly TempImages _images = new();

    public void Dispose() => _images.Dispose();

    /// <summary>
    /// The same loads in English on an English culture and in Romanian on a Romanian culture,
    /// where a number written with the culture's comma would show, write the same messages: the
    /// log's words are its own, and its numbers invariant. The loads take every line a load
    /// writes but a failure, whose message is the user's, as shown (a padded pair, a float FITS,
    /// a fallback to Linear).
    /// </summary>
    [Fact]
    public async Task R121_TheLogIsTheSameWhateverTheLanguageAndTheCulture()
    {
        string a = _images.WritePng("a.png", PixelFormats.Rgb48, 64, 48);
        string b = _images.WritePng("b.png", PixelFormats.Bgr24, 60, 48);
        string fitsA = _images.WriteBytes("a.fit", FitsFixture.Image(-32, 4, 3, 1, [.. Enumerable.Repeat(0.25, 12)]));
        string fitsB = _images.WriteBytes("b.fit", FitsFixture.Image(-32, 4, 3, 1, [.. Enumerable.Repeat(300.5, 12)]));

        async Task<IReadOnlyList<string>> Run(UiText text, string culture)
        {
            UiText previousText = UiText.Current;
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            UiText.Current = text;
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            try
            {
                using LogCapture capture = LogCapture.Start();
                var loader = new ImagePairLoader(new RoutingImageLoader());
                await loader.LoadAsync(a, b, confirmPadding: _ => Task.FromResult(true), mode: DisplayMode.Histogram);
                await loader.LoadAsync(fitsA, fitsB, mode: DisplayMode.FalseColours);
                return LoggingTests.WithoutTimes(capture.Messages);
            }
            finally
            {
                UiText.Current = previousText;
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        IReadOnlyList<string> english = await Run(UiText.English, "en-US");
        IReadOnlyList<string> romanian = await Run(UiText.Romanian, "ro-RO");

        Assert.Contains(english, m => m.Contains(" MiB", StringComparison.Ordinal));
        Assert.Contains(english, m => m.EndsWith("float samples counted in 0..65535: 12 of 12 above 2", StringComparison.Ordinal));
        Assert.Equal(english, romanian);
    }

    [Fact]
    public void R121_TheStartLogsTheLanguageARefusedLangAndThePair()
    {
        string a = _images.WritePng("a.png", PixelFormats.Bgr24, 4, 4);
        string b = _images.WritePng("b.png", PixelFormats.Bgr24, 4, 4);
        UiText previous = UiText.Current;
        try
        {
            using LogCapture capture = LogCapture.Start();

            App.ApplyCommandLine(["--lang=de", a, b]);
            App.ApplyCommandLine(["--lang=", a, @"C:\not-here.png"]);
            App.ApplyCommandLine(["--lang", "ro"]);

            Assert.Equal(
                [
                    "language=en",
                    "--lang \"de\" names no language this program has: the interface is English, and the window says so",
                    $"the command line names the pair A={a} B={b}",
                    "language=en",
                    "--lang was given without a language: the interface is English, and the window says so",
                    $"the command line names no pair of existing files (\"{a}\", \"C:\\not-here.png\" missing): the dialogs ask",
                    "language=ro",
                    "the command line names no pair: the dialogs ask",
                ],
                capture.MessagesOf(Loggers.App));
        }
        finally
        {
            UiText.Current = previous;
            App.TakeStartupSelection();
            App.TakeUnknownLanguage();
        }
    }
}
