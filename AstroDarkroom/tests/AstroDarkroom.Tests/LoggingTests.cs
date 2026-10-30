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
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AstroDarkroom.Core;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit.Abstractions;

namespace AstroDarkroom.Tests;

/// <summary>
/// The diagnostic log (R-011 to R-014, ADR 0012): where the files go and how they roll, what the
/// core writes, the level, two writers on one file. The file tests run the launcher's
/// configuration (<see cref="LogSetup.Build"/>) in a private <see cref="LogFactory"/>, which
/// stands for one process, so they touch neither the test assembly's capture
/// (<see cref="LogCapture"/>) nor the tests beside them. FlatCheck's and LightsCheck's tests of
/// the same log are the model. The window, which has no tests (ADR 0001), takes the words of its
/// lines about the suite from <see cref="LogText"/>, so these tests pin them.
/// </summary>
public partial class LoggingTests
{
    private const string Exe = "AstroDarkroom";

    /// <summary>The five files 8 MiB of lines leave, as FlatCheck measured with the same NLog and the same settings (R-011).</summary>
    private static readonly string[] RolledFiles = ["AstroDarkroom_03.log", "AstroDarkroom_04.log", "AstroDarkroom_05.log", "AstroDarkroom_06.log", "AstroDarkroom_07.log"];

    /// <summary>The only file that starts and closes the log: the window's entry point.</summary>
    private static readonly string[] EntryPoints = ["src/AstroDarkroom.Wpf/App.xaml.cs"];

    /// <summary>The only two files that set NLog's configuration: the launcher's and the tests'.</summary>
    private static readonly string[] Configurers = ["src/AstroDarkroom.Core/Logging/LogSetup.cs", "tests/AstroDarkroom.Tests/LogCapture.cs"];

    private readonly ITestOutputHelper _output;

    /// <summary>The runner's output, for the measured file lists.</summary>
    public LoggingTests(ITestOutputHelper output) => _output = output;

    // ---------------------------------------------------------------- R-011: the files

    [Fact]
    public void R011_TheLogFolderIsBesideTheExecutableAndTheFileBeginsWithItsName()
    {
        using TempWorkspace ws = new();
        string fallback = ws.PathTo("fallback");

        string folder = LogSetup.ResolveFolder(ws.Root, fallback, Exe, out bool fellBack);

        Assert.False(fellBack);
        Assert.Equal(ws.PathTo("log"), folder);
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));   // the probe is gone
        Assert.False(Directory.Exists(fallback));

        using (LogFactory factory = LogFiles.Factory(LogSetup.Build(Exe, folder, LogLevel.Debug)))
        {
            factory.GetLogger("Test").Info("hello");
            factory.Shutdown();
        }

        string file = Path.Combine(folder, "AstroDarkroom.log");
        Assert.Equal(file, Assert.Single(Directory.GetFiles(folder)));
        byte[] bytes = File.ReadAllBytes(file);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no BOM");
        string line = Encoding.UTF8.GetString(bytes).TrimEnd('\r', '\n');
        Assert.Matches(LogFiles.LinePrefix(), line);
        Assert.EndsWith(" INFO  [" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ":"
            + Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture) + "] Test hello", line, StringComparison.Ordinal);
    }

    [Fact]
    public void R011_WhenTheFolderBesideCannotBeWrittenTheLogGoesToTheFallback()
    {
        using TempWorkspace ws = new();
        // A FILE named log where the folder should be: the folder cannot be created.
        ws.Touch("log");
        string fallbackRoot = ws.PathTo("LocalAppData");

        string folder = LogSetup.ResolveFolder(ws.Root, fallbackRoot, Exe, out bool fellBack);

        Assert.True(fellBack);
        Assert.Equal(Path.Combine(fallbackRoot, "AstroDarkroom", "log"), folder);
        Assert.True(Directory.Exists(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
        Assert.Contains("(fallback", LogSetup.StartupLine(Exe, [], folder, true, LogLevel.Debug), StringComparison.Ordinal);
    }

    [Fact]
    public void R011_TheLogRollsAt1MiBAndKeepsAtMost5Files()
    {
        using TempWorkspace ws = new();
        string folder = ws.PathTo("log");

        WriteThrough(folder, Exe, 8 * LogSetup.FileSizeLimit, "launcher");

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
        Assert.EndsWith("launcher last", File.ReadAllLines(files[^1])[^1], StringComparison.Ordinal);

        // The next start goes on in the newest file: the same five names, the newest grown,
        // nothing started and nothing deleted.
        long newestBefore = new FileInfo(files[^1]).Length;
        WriteThrough(folder, Exe, 1000, "next");
        string[] after = LogFiles.FilesOf(folder, Exe);
        Assert.Equal(files.Select(f => Path.GetFileName(f)), after.Select(f => Path.GetFileName(f)));
        Assert.True(new FileInfo(after[^1]).Length > newestBefore, "the newest file did not grow");
        Assert.EndsWith("next last", File.ReadAllLines(after[^1])[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// The suite folder's case: FlatCheck's window logs into the same <c>log</c> folder, under its
    /// own name, and the launcher's rolling, past five files, leaves its files alone.
    /// </summary>
    [Fact]
    public void R011_OneExecutablesRollingLeavesTheOthersFilesAlone()
    {
        using TempWorkspace ws = new();
        string folder = ws.PathTo("log");
        WriteThrough(folder, "FlatCheck.Wpf", 3 * LogSetup.FileSizeLimit / 2, "flatcheck");
        string[] flatCheck = LogFiles.FilesOf(folder, "FlatCheck.Wpf");
        Assert.Equal(2, flatCheck.Length);

        WriteThrough(folder, Exe, 8 * LogSetup.FileSizeLimit, "launcher");

        Assert.Equal(flatCheck, LogFiles.FilesOf(folder, "FlatCheck.Wpf"));
        Assert.EndsWith("flatcheck last", File.ReadAllLines(flatCheck[^1])[^1], StringComparison.Ordinal);
        Assert.Equal(LogSetup.FilesPerExecutable, LogFiles.FilesOf(folder, Exe).Length);
    }

    /// <summary>
    /// The folder's probe also opens the file NLog will append to: a read-only newest file sends
    /// the log to the fallback, where it would otherwise pass the probe and lose every line in
    /// silence (FlatCheck's review of 2026-10-27, its ADR 0072).
    /// </summary>
    [Fact]
    public void R011_AReadOnlyNewestLogFileSendsTheLogToTheFallback()
    {
        using TempWorkspace ws = new();
        string beside = ws.PathTo("suite");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        Directory.CreateDirectory(folder);
        string newest = Path.Combine(folder, "AstroDarkroom_03.log");
        File.WriteAllText(Path.Combine(folder, "AstroDarkroom.log"), "old\n");
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

    /// <summary>A file another launcher holds open (R-014) passes the probe: the second launcher logs beside the first.</summary>
    [Fact]
    public void R011_ALogFileAnotherProcessHoldsOpenStillPassesTheProbe()
    {
        using TempWorkspace ws = new();
        string beside = ws.PathTo("suite");
        string folder = Path.Combine(beside, LogSetup.FolderName);
        using LogFactory other = LogFiles.Factory(LogSetup.Build(Exe, folder, LogLevel.Debug));
        other.GetLogger("Other").Info("held open");

        string chosen = LogSetup.ResolveFolder(beside, ws.PathTo("fallback"), Exe, out bool fellBack);

        Assert.False(fellBack);
        Assert.Equal(folder, chosen);
        other.Shutdown();
    }

    [Theory]
    [InlineData("C:/Suite/AstroDarkroom.exe", "AstroDarkroom", "AstroDarkroom")]
    [InlineData("C:/Suite/AstroDarkroom-1.0.exe", "AstroDarkroom", "AstroDarkroom-1.0")]
    [InlineData("C:/Program Files/dotnet/dotnet.exe", "AstroDarkroom", "AstroDarkroom")]
    [InlineData(null, "AstroDarkroom", "AstroDarkroom")]
    public void R011_TheFilesBeginWithTheExecutablesName(string? processPath, string entry, string expected)
    {
        Assert.Equal(expected, LogSetup.ExecutableName(processPath, entry));
    }

    // ---------------------------------------------------------------- R-012: what is written

    [Fact]
    public void R012_TheFirstLineCarriesTheFacts()
    {
        string line = LogSetup.StartupLine(Exe, [@"D:\astro\veil night", "--lang=ro"], @"D:\suite\log", false, LogLevel.Debug);

        Assert.StartsWith("AstroDarkroom " + VersionInfo.Value + " starting; exe=", line, StringComparison.Ordinal);
        Assert.Contains(@"; args=[""D:\astro\veil night"", ""--lang=ro""]; cwd=", line, StringComparison.Ordinal);
        Assert.Contains(@"; log=D:\suite\log; level=Debug; os=", line, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback", line, StringComparison.Ordinal);
        string[] parts = ["; cpus=", "; memory=", " GiB; runtime=", "; culture=", "; tz=", " (UTC"];
        foreach (string part in parts)
        {
            Assert.Contains(part, line, StringComparison.Ordinal);
        }

        Assert.EndsWith("; pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture), line, StringComparison.Ordinal);
    }

    [Fact]
    public void R012_TheConfigurationHasOneFileTargetAndNoConsole()
    {
        LoggingConfiguration config = LogSetup.Build(Exe, @"C:\any\log", LogLevel.Debug);

        AtomicFileTarget file = Assert.IsType<AtomicFileTarget>(Assert.Single(config.AllTargets));
        Assert.Equal(LogSetup.FileSizeLimit, file.ArchiveAboveSize);
        Assert.Equal(LogSetup.FilesPerExecutable, file.MaxArchiveFiles);
        Assert.True(file.KeepFileOpen);
        Assert.True(file.ConcurrentWrites);
        Assert.False(file.WriteBom);
        LoggingRule rule = Assert.Single(config.LoggingRules);
        Assert.True(rule.IsLoggingEnabledForLevel(LogLevel.Debug));
        Assert.False(rule.IsLoggingEnabledForLevel(LogLevel.Trace));
    }

    /// <summary>
    /// A crash the window's dispatcher sees and then the process sees again is written once with
    /// its stack: the second Fatal line names the first (FlatCheck's ADR 0072).
    /// </summary>
    [Fact]
    public void R012_TheSameCrashSeenByTwoHooksIsWrittenOnce()
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
    /// The release.json the launcher reads is one line with the suite's and every component's
    /// version and commit, a missing key as <c>none</c>; a file that gives nothing says why, an
    /// unreadable one with its exception, at Warn.
    /// </summary>
    [Fact]
    public void R012_TheReleaseManifestIsLoggedWithItsVersions_OrWhyItGaveNothing()
    {
        using TempWorkspace ws = new();
        string missing = ws.PathTo("release.json");
        string good = ws.Write("good.json", """
            {
              "name": "AstroDarkroom", "version": "1.0.0", "date": "2026-10-28",
              "commit": "0123456789abcdef0123456789abcdef01234567",
              "components": [
                { "name": "AstroDarkroom", "version": "1.0.0", "commit": "0123456789abcdef0123456789abcdef01234567", "unreleased": true, "clean": true },
                { "name": "FlatCheck", "version": "2.3.0", "commit": "fedcba9876543210fedcba9876543210fedcba98", "unreleased": false, "clean": false },
                { "name": "WCS_Copy" }
              ]
            }
            """);
        string broken = ws.Write("broken.json", "{ not json");
        string array = ws.Write("array.json", "[1, 2]");
        string bare = ws.Write("bare.json", """{ "version": "0.1.0" }""");
        string locked = ws.Write("locked.json", "{}");
        using LogCapture capture = LogCapture.Start();

        Assert.Null(ReleaseManifest.TryRead(missing));
        Assert.NotNull(ReleaseManifest.TryRead(good));
        Assert.Null(ReleaseManifest.TryRead(broken));
        Assert.Null(ReleaseManifest.TryRead(array));
        Assert.Null(ReleaseManifest.TryRead(bare));
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Null(ReleaseManifest.TryRead(locked));
        }

        LogCapture.Captured[] lines = capture.Events.Where(e => e.Logger == "ReleaseManifest").ToArray();
        Assert.Equal(6, lines.Length);
        Assert.Equal((LogLevel.Info, "no file at " + missing + "; the versions are unknown"), (lines[0].Level, lines[0].Message));
        Assert.Equal((LogLevel.Info, "read " + good + ": name=AstroDarkroom version=1.0.0 date=2026-10-28 commit=0123456789abcdef0123456789abcdef01234567; "
            + "3 components: AstroDarkroom version=1.0.0 commit=0123456789abcdef0123456789abcdef01234567 unreleased=yes clean=yes; "
            + "FlatCheck version=2.3.0 commit=fedcba9876543210fedcba9876543210fedcba98 unreleased=no clean=no; "
            + "WCS_Copy version=none commit=none unreleased=no clean=no"), (lines[1].Level, lines[1].Message));
        Assert.Equal((LogLevel.Warn, broken + " could not be read; the versions are unknown"), (lines[2].Level, lines[2].Message));
        Assert.IsAssignableFrom<JsonException>(lines[2].Exception);
        Assert.Equal((LogLevel.Warn, array + " is not a release manifest (no JSON object); the versions are unknown"), (lines[3].Level, lines[3].Message));
        Assert.Null(lines[3].Exception);
        Assert.Equal((LogLevel.Warn, bare + " is not a release manifest (no components array); the versions are unknown"), (lines[4].Level, lines[4].Message));
        Assert.Equal((LogLevel.Warn, locked + " could not be read; the versions are unknown"), (lines[5].Level, lines[5].Message));
        Assert.IsAssignableFrom<IOException>(lines[5].Exception);
        Assert.Contains(lines[5].Exception!.Message, lines[5].Line, StringComparison.Ordinal);   // the stack is on the lines below
    }

    /// <summary>
    /// The language setting's read and write are one Debug line each, and a file that gives no
    /// language, or a write that fails, a Warn line saying why, with the exception when there is
    /// one: the write's failure, which the launcher ignores, is no longer silent.
    /// </summary>
    [Fact]
    public void R012_TheLanguageSettingIsLoggedWithWhatItGave_AndAFailedWriteWithItsException()
    {
        using TempWorkspace ws = new();
        string path = ws.PathTo(Path.Combine("AstroDarkroom", "settings.json"));
        string french = ws.Write("fr.json", """{ "language": "fr" }""");
        string array = ws.Write("array.json", "[1]");
        string broken = ws.Write("broken.json", "{ not json");
        string blocked = Path.Combine(ws.Touch("blocker"), "settings.json");
        using LogCapture capture = LogCapture.Start();

        Assert.Null(LanguageSetting.TryRead(path));
        LanguageSetting.Write(path, UiLanguage.Romanian);
        Assert.Equal(UiLanguage.Romanian, LanguageSetting.TryRead(path));
        Assert.Null(LanguageSetting.TryRead(french));
        Assert.Null(LanguageSetting.TryRead(array));
        Assert.Null(LanguageSetting.TryRead(broken));
        LanguageSetting.Write(blocked, UiLanguage.English);

        LogCapture.Captured[] lines = capture.Events.Where(e => e.Logger == "LanguageSetting").ToArray();
        Assert.Equal(7, lines.Length);
        Assert.Equal((LogLevel.Debug, "no setting file at " + path), (lines[0].Level, lines[0].Message));
        Assert.Equal((LogLevel.Debug, "setting written to " + path + ": language=ro"), (lines[1].Level, lines[1].Message));
        Assert.Equal((LogLevel.Debug, "setting read from " + path + ": language=ro"), (lines[2].Level, lines[2].Message));
        Assert.Equal((LogLevel.Warn, french + " holds language=fr, which is neither en nor ro; no language is taken from it"), (lines[3].Level, lines[3].Message));
        Assert.Equal((LogLevel.Warn, array + " is not a JSON object; no language is taken from it"), (lines[4].Level, lines[4].Message));
        Assert.Equal((LogLevel.Warn, broken + " could not be read; no language is taken from it"), (lines[5].Level, lines[5].Message));
        Assert.IsAssignableFrom<JsonException>(lines[5].Exception);
        Assert.Equal((LogLevel.Warn, "the setting could not be written to " + blocked + "; the language holds for this run"), (lines[6].Level, lines[6].Message));
        Assert.IsAssignableFrom<IOException>(lines[6].Exception);
    }

    [Fact]
    public void R012_TheSuiteFolderLineCountsTheAppsAndNamesTheMissingOnes()
    {
        using TempWorkspace ws = new();
        ws.Touch("DarksCheck.Wpf.exe");
        ws.Touch("FlatCheck.Wpf.exe");
        ws.Touch("flatcheck.exe");   // a console plays no part (R-002)
        SuiteFolder folder = new(ws.Root);

        Assert.Equal("suite folder " + ws.Root + ": 2 of 6 apps present; missing LightsCheck.Wpf.exe, ImageVisualCompare.Wpf.exe, CopyWcs.Wpf.exe, AstroPhotoAnalyzer.Wpf.exe",
            LogText.Presence(folder, AppCatalogue.Apps));

        foreach (SuiteApp app in AppCatalogue.Apps)
        {
            ws.Touch(app.WindowExe);
        }

        Assert.Equal("suite folder " + ws.Root + ": 6 of 6 apps present", LogText.Presence(folder, AppCatalogue.Apps));
    }

    [Fact]
    public void R012_TheStartLineNamesTheFileTheArgumentsAndTheFolder()
    {
        using TempWorkspace ws = new();
        string exe = ws.Touch("FlatCheck.Wpf.exe");
        SuiteApp flat = AppCatalogue.Apps.Single(a => a.Folder == "FlatCheck");

        LaunchPlan plan = Launcher.Plan(flat, new SuiteFolder(ws.Root));

        Assert.Equal("open FlatCheck: " + exe + " --lang=ro in " + ws.Root, LogText.Open(flat, plan));
    }

    /// <summary>
    /// The log's own words are English whatever the interface language (R-012): the core's lines
    /// and the words it gives the window are the same in both, but for the <c>--lang=</c> argument
    /// a start passes on, which is the language itself.
    /// </summary>
    [Fact]
    public void R012_TheLogIsTheSameInBothLanguages()
    {
        using TempWorkspace ws = new();
        ws.Touch("DarksCheck.Wpf.exe");
        string release = ws.Write("release.json", """{ "name": "AstroDarkroom", "version": "1.0.0", "components": [ { "name": "DarksCheck", "version": "1.0.0" } ] }""");
        string setting = ws.Write("settings.json", """{ "language": "fr" }""");

        string[] romanian = MessagesOf(UiLanguage.Romanian, ws, release, setting);
        string[] english = MessagesOf(UiLanguage.English, ws, release, setting);

        Assert.Equal(6, romanian.Length);
        Assert.Equal(romanian, english);
    }

    /// <summary>The log writes the elapsed time of its last line in seconds with one decimal, never whole seconds.</summary>
    [Fact]
    public void TheElapsedTimeIsInSecondsWithOneDecimal()
    {
        Assert.Equal("12.9 s", LogText.Seconds(TimeSpan.FromMilliseconds(12900)));
        Assert.Equal("0.8 s", LogText.Seconds(TimeSpan.FromMilliseconds(800)));
    }

    // ---------------------------------------------------------------- R-013: the level

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
    public void R013_TheLevelComesFromTheEnvironmentVariable(string? value, string level)
    {
        Assert.Equal(LogLevel.FromString(level), LogSetup.LevelFrom(value));
    }

    /// <summary>The variable's name, as R-013 and the README give it, written out so a renamed constant fails.</summary>
    [Fact]
    public void R013_TheVariableIsASTRODARKROOM_LOG()
    {
        Assert.Equal("ASTRODARKROOM_LOG", LogSetup.LevelVariable);
    }

    [Fact]
    public void R013_OffCreatesNeitherTheFolderNorAFile()
    {
        using TempWorkspace ws = new();

        LogPlace off = LogSetup.Plan(Exe, ws.PathTo("AstroDarkroom.exe"), ws.Root, ws.PathTo("fallback"), "off");

        Assert.Null(off.Folder);
        Assert.Empty(Directory.GetFileSystemEntries(ws.Root));
        Assert.Empty(LogSetup.Build(Exe, ws.PathTo("log"), LogLevel.Off).AllTargets);

        LogPlace trace = LogSetup.Plan(Exe, ws.PathTo("AstroDarkroom.exe"), ws.Root, ws.PathTo("fallback"), "trace");
        Assert.Equal(LogLevel.Trace, trace.Level);
        Assert.Equal(ws.PathTo("log"), trace.Folder);
    }

    // ---------------------------------------------------------------- R-014: two processes

    /// <summary>
    /// Two processes of the launcher write into the same file at full speed, and every line of
    /// both arrives whole, once, past the 1 MiB roll too (R-011, R-014).
    /// </summary>
    [Fact]
    public void R014_TwoProcessesWritingAtFullSpeedKeepEveryLine()
    {
        using TempWorkspace ws = new();

        LogFiles.TwoWritersKeepEveryLine(ws.PathTo("log"), Exe, _output);
    }

    /// <summary>
    /// The atomic target these tests load is the build the window ships: the package has a
    /// <c>net8.0</c> build and a <c>net8.0-windows</c> one, and the window, which targets
    /// <c>net8.0-windows</c>, takes the second; the test project targets it too (ADR 0012).
    /// </summary>
    [Fact]
    public void R014_TheTestsLoadTheWindowsBuildOfTheAtomicTarget()
    {
        Assert.Equal("Windows7.0", typeof(AtomicFileTarget).Assembly.GetCustomAttribute<TargetPlatformAttribute>()?.PlatformName);
    }

    // ---------------------------------------------------------------- invariants of the repository

    /// <summary>
    /// Only the window's entry point configures the log for a user's start, and nothing but
    /// LogSetup (for it) and LogCapture (for the tests) sets NLog's configuration at all: the core
    /// used as a library logs into nothing and every test into the capture (ADR 0012).
    /// </summary>
    [Fact]
    public void OnlyTheWindowsEntryPointConfiguresTheLog()
    {
        string[] callers = CsSources("src", "tests").Where(f => ApplyOrClose().IsMatch(File.ReadAllText(f))).Select(Rel).Order(StringComparer.Ordinal).ToArray();
        string[] configurers = CsSources("src", "tests").Where(f => SetsConfiguration().IsMatch(File.ReadAllText(f))).Select(Rel).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(EntryPoints, callers);
        Assert.Equal(Configurers, configurers);
    }

    /// <summary>
    /// The log's own words are English whatever the language (R-012): no log call composes its
    /// message through the T(english, romanian) pair, however many lines the call spans. And
    /// every logger is declared one way, a <c>Logger</c> field or property taken from
    /// <c>LogManager.GetLogger</c> with <c>using NLog</c>, so the first check sees every call.
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

    // ---------------------------------------------------------------- helpers

    [GeneratedRegex(@"LogSetup\.(Apply|Close)\(")]
    private static partial Regex ApplyOrClose();

    [GeneratedRegex(@"LogManager\.(Configuration\s*=[^=]|Setup\()")]
    private static partial Regex SetsConfiguration();

    /// <summary>A call that composes a log line's text.</summary>
    [GeneratedRegex(@"\b\w*[Ll]og\.(Trace|Debug|Info|Warn|Error|Fatal)\(")]
    private static partial Regex LogCall();

    [GeneratedRegex(@"\)\s*;")]
    private static partial Regex CallEnd();

    [GeneratedRegex(@"\bT\(")]
    private static partial Regex Translation();

    [GeneratedRegex(@"static readonly Logger \w+ = LogManager\.GetLogger\(|static Logger \w+ => LogManager\.GetLogger\(")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"(^|/)(bin|obj)/")]
    private static partial Regex BuildOutput();

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

    /// <summary>
    /// The messages the core writes, and the window's words it composes, for one set of files in
    /// one language: the two readers, the suite folder's line and a start's line, whose
    /// <c>--lang=</c> value is made equal.
    /// </summary>
    private static string[] MessagesOf(UiLanguage language, TempWorkspace ws, string release, string setting)
    {
        using IDisposable scope = Ui.Use(language);
        using LogCapture capture = LogCapture.Start();
        _ = ReleaseManifest.TryRead(release);
        _ = ReleaseManifest.TryRead(ws.PathTo("missing.json"));
        _ = LanguageSetting.TryRead(setting);
        _ = LanguageSetting.TryRead(ws.PathTo("missing-settings.json"));
        SuiteFolder folder = new(ws.Root);
        SuiteApp darks = AppCatalogue.Apps.Single(a => a.Folder == "DarksCheck");
        string presence = LogText.Presence(folder, AppCatalogue.Apps);
        string open = LogText.Open(darks, Launcher.Plan(darks, folder)).Replace("--lang=" + Ui.Code(language), "--lang=<code>", StringComparison.Ordinal);
        return [.. capture.Messages, presence, open];
    }

    /// <summary>The C# sources under the given folders of the project, without bin/ and obj/ (filtered on the relative path).</summary>
    private static IEnumerable<string> CsSources(params string[] folders)
    {
        string root = TestData.FindRepoRoot();
        return folders.SelectMany(f => Directory.EnumerateFiles(Path.Combine(root, f), "*.cs", SearchOption.AllDirectories))
            .Where(f => !BuildOutput().IsMatch(Rel(f)));
    }

    /// <summary>A file's path as the assertions name it: relative to the project, with forward slashes.</summary>
    private static string Rel(string f) => Path.GetRelativePath(TestData.FindRepoRoot(), f).Replace('\\', '/');
}
