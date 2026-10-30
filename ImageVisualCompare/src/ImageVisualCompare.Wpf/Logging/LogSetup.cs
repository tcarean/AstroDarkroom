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

// LogSetup.cs - the one place that knows how the diagnostic log is configured (R-120 to R-123,
// ADR 0043): the folder, the file names, the rolling, the level, the line layout, the first and
// the last line of a process, the crash hooks. Program.Main calls Apply first and Close last;
// nothing else configures NLog for a user's run. The test assembly configures a capture at Trace
// instead (LogCapture), so every test runs every log line on its path. The port follows
// FlatCheck's LogSetup, whose measurements it relies on; docs/design/logging.md holds how the
// parts fit.

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace ImageVisualCompare.Wpf.Logging;

/// <summary>Where a process's log goes, as <see cref="LogSetup.Plan"/> decided it.</summary>
/// <param name="ExecutableName">The start of every file's name.</param>
/// <param name="Folder">The folder of the files; null when the level is <c>off</c>.</param>
/// <param name="FellBack">
/// True when the folder beside the executable could not be written and the fallback was taken.
/// </param>
/// <param name="Level">The lowest level written.</param>
internal sealed record LogPlace(string ExecutableName, string? Folder, bool FellBack, LogLevel Level);

/// <summary>
/// The diagnostic log's configuration: a UTF-8 text file per executable in the folder
/// <see cref="FolderName"/> beside it, rolled at <see cref="FileSizeLimit"/>, at most
/// <see cref="FilesPerExecutable"/> files kept, at the level <see cref="LevelVariable"/> names.
/// </summary>
internal static class LogSetup
{
    /// <summary>The size above which the file being written is left for the next one, in bytes: 1 MiB (R-120).</summary>
    internal const long FileSizeLimit = 1024 * 1024;

    /// <summary>
    /// The most files the executable keeps in its folder, the one being written included
    /// (R-120). NLog 6.2.2's <c>MaxArchiveFiles</c> counts that file too: FlatCheck measured that
    /// 4 left 4 files, and <c>R120_TheLogRollsAt1MiBAndKeepsAtMost5Files</c> holds this value.
    /// </summary>
    internal const int FilesPerExecutable = 5;

    /// <summary>The environment variable that sets the level (R-122).</summary>
    internal const string LevelVariable = "IMAGEVISUALCOMPARE_LOG";

    /// <summary>The folder's name, beside the executable and in the fallback.</summary>
    internal const string FolderName = "log";

    /// <summary>The folder under <c>%LOCALAPPDATA%</c> that holds the fallback <see cref="FolderName"/>.</summary>
    internal const string FallbackFolder = "ImageVisualCompare";

    /// <summary>
    /// One line per event: local time to the tenth of a millisecond, the level padded to five, the
    /// process and thread ids, the logger, the message; an exception follows on the next lines as
    /// <see cref="Exception.ToString"/> writes it (type, message, stack, inner exceptions).
    /// </summary>
    internal const string LineLayout =
        "${longdate} ${level:uppercase=true:padding=-5} [${processid}:${threadid}] ${logger} ${message}"
        + "${onexception:inner=${newline}${exception:format=ToString}}";

    /// <summary>The level when <see cref="LevelVariable"/> is unset or names no level.</summary>
    internal static readonly LogLevel DefaultLevel = LogLevel.Debug;

    /// <summary>
    /// Runs from <see cref="Apply"/>, for the elapsed time of the last line; null until then,
    /// which is how a second <see cref="Apply"/> and a <see cref="Close"/> without
    /// <see cref="Apply"/> know to do nothing.
    /// </summary>
    private static Stopwatch? _clock;

    /// <summary>The last exception logged as fatal, so the same crash seen by two hooks is written once.</summary>
    private static Exception? _lastFatal;

    /// <summary>
    /// The logger of the process's own lines. Fetched on use, not held in a static field: creating
    /// a logger makes NLog load an <c>NLog.config</c> it finds beside the executable, and
    /// <see cref="Apply"/> must have set the configuration before that (FlatCheck measured it on
    /// 2026-10-27: a stray file that throws took the process down before its first line). NLog
    /// caches the logger, so this costs a lookup.
    /// </summary>
    private static Logger Log => LogManager.GetLogger(Loggers.Process);

    /// <summary>
    /// Configures the process's log, writes its first line and hooks the unhandled exceptions.
    /// Called first by <see cref="Program"/>'s entry point, and only there; a second call does nothing. With
    /// <see cref="LevelVariable"/> at <c>off</c> nothing is created, hooked or written.
    /// </summary>
    /// <param name="entryName">
    /// The executable's name as built, <c>ImageVisualCompare.Wpf</c>, used when the process path
    /// names a host instead (<c>dotnet</c>).
    /// </param>
    /// <param name="args">The command line without the executable, logged verbatim; not null.</param>
    internal static void Apply(string entryName, IReadOnlyList<string> args)
    {
        if (Interlocked.CompareExchange(ref _clock, Stopwatch.StartNew(), null) is not null)
        {
            return;
        }

        // An empty configuration before anything else, and before any logger exists: should
        // something below throw, the process runs without a log, not with an NLog.config that
        // NLog found beside the executable (the caller swallows what this throws).
        LogManager.ThrowExceptions = false;
        LogManager.Configuration = new LoggingConfiguration();

        string fallbackRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(fallbackRoot))
        {
            fallbackRoot = Path.GetTempPath();
        }

        LogPlace place = Plan(entryName, Environment.ProcessPath, AppContext.BaseDirectory, fallbackRoot,
            Environment.GetEnvironmentVariable(LevelVariable));
        LogManager.Configuration = Build(place.ExecutableName, place.Folder, place.Level);
        if (place.Folder is null)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Fatal(e.ExceptionObject as Exception, "unhandled exception, the process ends"
                + (e.ExceptionObject is Exception ? string.Empty : ": " + e.ExceptionObject));
        TaskScheduler.UnobservedTaskException += (_, e) =>
            Log.Error(e.Exception, "a task failed and nobody awaited it");

        Log.Info(StartupLine(place.ExecutableName, args, place.Folder, place.FellBack, place.Level));
    }

    /// <summary>
    /// Decides where the log goes and at which level, without configuring anything: the
    /// executable's name, the folder (created, unless the level is <c>off</c>), whether it is the
    /// fallback. <see cref="Apply"/> runs it on the process's own values.
    /// </summary>
    /// <param name="entryName">The executable's name as built.</param>
    /// <param name="processPath">The process's path; null when unknown.</param>
    /// <param name="baseDirectory">The executable's folder.</param>
    /// <param name="fallbackRoot">The folder that holds the fallback, <c>%LOCALAPPDATA%</c> in the application.</param>
    /// <param name="levelValue">The value of <see cref="LevelVariable"/>; null when unset.</param>
    /// <returns>The place; its folder is null when the level is <c>off</c>, and then nothing was created.</returns>
    internal static LogPlace Plan(string entryName, string? processPath, string baseDirectory, string fallbackRoot, string? levelValue)
    {
        LogLevel level = LevelFrom(levelValue);
        string name = ExecutableName(processPath, entryName);
        if (level == LogLevel.Off)
        {
            return new LogPlace(name, null, false, level);
        }

        string folder = ResolveFolder(baseDirectory, fallbackRoot, name, out bool fellBack);
        return new LogPlace(name, folder, fellBack, level);
    }

    /// <summary>
    /// Writes the process's last line, <see cref="ExitLine"/>, and closes the log. Called last by
    /// the application; does nothing when <see cref="Apply"/> did not run.
    /// </summary>
    /// <param name="exitCode">
    /// The process's exit code; null when the application did not return one, because an
    /// exception nobody handled ended it and the runtime still ran the caller's <c>finally</c>.
    /// </param>
    internal static void Close(int? exitCode)
    {
        Stopwatch? started = Volatile.Read(ref _clock);
        if (started is null)
        {
            return;
        }

        Log.Info(ExitLine(exitCode, started.Elapsed));
        LogManager.Shutdown();
    }

    /// <summary>
    /// The process's last line: the exit code, or that an exception nobody handled ended it, and
    /// the time since the log started (R-121).
    /// </summary>
    /// <param name="exitCode">The exit code; null when the application ended by an exception.</param>
    /// <param name="elapsed">The time since <see cref="Apply"/>.</param>
    /// <returns>The line, in English: <c>exit 0 after 12.9 s</c>.</returns>
    internal static string ExitLine(int? exitCode, TimeSpan elapsed) =>
        (exitCode is { } code ? "exit " + LogText.Int(code) : "ended by an unhandled exception")
        + " after " + LogText.Seconds(elapsed);

    /// <summary>
    /// Logs an exception nobody handled, at <c>Fatal</c>, and flushes. The same exception seen a
    /// second time (the window's dispatcher, then the process) is named, not written again.
    /// </summary>
    /// <param name="exception">The exception; null when the runtime handed something else.</param>
    /// <param name="where">Where it was caught, in English.</param>
    internal static void Fatal(Exception? exception, string where)
    {
        if (exception is not null && ReferenceEquals(exception, Interlocked.Exchange(ref _lastFatal, exception)))
        {
            Log.Fatal(where + ": the exception logged above");
        }
        else
        {
            Log.Fatal(exception, where);
        }

        LogManager.Flush();
    }

    /// <summary>
    /// The configuration of the executable's log: one atomic-append file target and every logger
    /// from <paramref name="minimum"/> up. <c>Off</c>, or no folder, gives a configuration without
    /// targets, in which every log call writes nothing.
    /// </summary>
    /// <param name="executableName">The start of every file's name.</param>
    /// <param name="folder">The folder of the files, created on the first line when missing; null for no file.</param>
    /// <param name="minimum">The lowest level written.</param>
    /// <returns>The configuration, not yet applied.</returns>
    internal static LoggingConfiguration Build(string executableName, string? folder, LogLevel minimum)
    {
        var config = new LoggingConfiguration { DefaultCultureInfo = CultureInfo.InvariantCulture };
        if (minimum == LogLevel.Off || folder is null)
        {
            return config;
        }

        // The atomic target, so a second process (two windows) appends to the same file without
        // a line lost: the operating system places every write at the end. NLog's plain target
        // lost lines either way, closed between lines or kept open, where the second of two
        // windows wrote over the first's whole log (FlatCheck's ADR 0070, R-123).
        var file = new AtomicFileTarget
        {
            Name = "file",
            FileName = Layout.FromLiteral(Path.Combine(folder, executableName + ".log")),
            Layout = LineLayout,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            WriteBom = false,
            ArchiveAboveSize = FileSizeLimit,
            MaxArchiveFiles = FilesPerExecutable,
            ConcurrentWrites = true,
            KeepFileOpen = true,
            AutoFlush = true,
            CreateDirs = true,
        };
        config.AddRule(minimum, LogLevel.Fatal, file, "*");
        return config;
    }

    /// <summary>
    /// The folder the log goes to: <see cref="FolderName"/> beside the executable when a file can
    /// be written there, otherwise <see cref="FolderName"/> under <see cref="FallbackFolder"/> in
    /// <paramref name="fallbackRoot"/>. The check creates the folder, a probe file deleted on
    /// close, and opens for append the file NLog would write next; no line is written.
    /// </summary>
    /// <param name="baseDirectory">The executable's folder.</param>
    /// <param name="fallbackRoot">The folder that holds the fallback.</param>
    /// <param name="executableName">The start of the files' names.</param>
    /// <param name="fellBack">True when the fallback was taken.</param>
    /// <returns>The folder's full path.</returns>
    internal static string ResolveFolder(string baseDirectory, string fallbackRoot, string executableName, out bool fellBack)
    {
        string beside = Path.Combine(baseDirectory, FolderName);
        fellBack = !CanWrite(beside, executableName);
        if (!fellBack)
        {
            return beside;
        }

        string fallback = Path.Combine(fallbackRoot, FallbackFolder, FolderName);
        CanWrite(fallback, executableName); // if even this fails, NLog drops the lines
        return fallback;
    }

    /// <summary>
    /// Creates a folder and a file in it, deleted when closed, and opens for append the file NLog
    /// will write next, when there is one: a read-only or locked newest file would pass the
    /// folder's probe and lose every line in silence (FlatCheck's review of 2026-10-27).
    /// </summary>
    /// <param name="folder">The folder to check.</param>
    /// <param name="executableName">The start of the probe file's name.</param>
    /// <returns>True when the folder, the probe and the newest file can all be written.</returns>
    private static bool CanWrite(string folder, string executableName)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string probe = Path.Combine(folder, FormattableString.Invariant(
                $"{executableName}.probe-{Environment.ProcessId}-{Guid.NewGuid():N}.tmp"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }

            string? newest = NewestLog(folder, executableName);
            if (newest is not null)
            {
                // The share mode the atomic target leaves: another process of the same
                // executable may hold the file open (R-123), and that is no reason to fall back.
                using (new FileStream(newest, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// The file NLog appends to next, by its own rolling (R-120): the highest-numbered
    /// <c>&lt;name&gt;_NN.log</c> when the log has rolled, else <c>&lt;name&gt;.log</c>.
    /// </summary>
    /// <param name="folder">The log folder; must exist.</param>
    /// <param name="executableName">The start of the files' names.</param>
    /// <returns>The file's path, or null when the executable has no file there yet.</returns>
    internal static string? NewestLog(string folder, string executableName)
    {
        string? best = null;
        int bestNumber = -1;
        foreach (string file in Directory.EnumerateFiles(folder, executableName + "_*.log"))
        {
            string stem = Path.GetFileNameWithoutExtension(file);
            if (stem.Length > executableName.Length + 1
                && int.TryParse(stem.AsSpan(executableName.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                && number > bestNumber)
            {
                bestNumber = number;
                best = file;
            }
        }

        if (best is not null)
        {
            return best;
        }

        string plain = Path.Combine(folder, executableName + ".log");
        return File.Exists(plain) ? plain : null;
    }

    /// <summary>
    /// The level <see cref="LevelVariable"/> names: <c>off</c>, <c>fatal</c>, <c>error</c>,
    /// <c>warn</c>, <c>info</c>, <c>debug</c> or <c>trace</c>, in any case, spaces around it
    /// ignored; <see cref="DefaultLevel"/> for anything else (R-122).
    /// </summary>
    /// <param name="value">The variable's value; null when unset.</param>
    /// <returns>The level.</returns>
    internal static LogLevel LevelFrom(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "off" => LogLevel.Off,
        "fatal" => LogLevel.Fatal,
        "error" => LogLevel.Error,
        "warn" => LogLevel.Warn,
        "info" => LogLevel.Info,
        "debug" => LogLevel.Debug,
        "trace" => LogLevel.Trace,
        _ => DefaultLevel,
    };

    /// <summary>
    /// The executable's name without its extension, from the process's path; the entry name when
    /// there is no path or the path names the <c>dotnet</c> host.
    /// </summary>
    /// <param name="processPath">The process's path; null when unknown.</param>
    /// <param name="entryName">The name as built.</param>
    /// <returns>The name the log files begin with.</returns>
    internal static string ExecutableName(string? processPath, string entryName)
    {
        string? name = string.IsNullOrEmpty(processPath) ? null : Path.GetFileNameWithoutExtension(processPath);
        return string.IsNullOrEmpty(name) || name.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? entryName : name;
    }

    /// <summary>
    /// The process's first line: the executable and its version, the path, the arguments
    /// verbatim, the working folder, the log folder, the level, the machine (OS, processors,
    /// memory), the runtime, the culture, the time zone and the process id (R-121).
    /// </summary>
    /// <param name="executableName">The executable's name.</param>
    /// <param name="args">The command line without the executable; not null.</param>
    /// <param name="folder">The log folder.</param>
    /// <param name="fellBack">True when the folder is the fallback.</param>
    /// <param name="level">The level written.</param>
    /// <returns>The line, in English.</returns>
    internal static string StartupLine(string executableName, IReadOnlyList<string> args, string folder, bool fellBack, LogLevel level)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
        var line = new StringBuilder();
        line.Append(executableName).Append(' ').Append(VersionInfo.Value).Append(" starting");
        line.Append("; exe=").Append(Environment.ProcessPath ?? "?");
        line.Append("; args=[").Append(string.Join(", ", args.Select(LogText.Quoted))).Append(']');
        line.Append("; cwd=").Append(CurrentDirectory());
        line.Append("; log=").Append(folder);
        if (fellBack)
        {
            line.Append(" (fallback: the folder beside the executable cannot be written)");
        }

        line.Append("; level=").Append(level.Name);
        line.Append("; os=").Append(RuntimeInformation.OSDescription).Append(' ').Append(RuntimeInformation.OSArchitecture);
        line.Append("; cpus=").Append(LogText.Int(Environment.ProcessorCount));
        line.Append("; memory=").Append((GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024)).ToString("F1", inv)).Append(" GiB");
        line.Append("; runtime=").Append(RuntimeInformation.FrameworkDescription).Append(' ').Append(RuntimeInformation.ProcessArchitecture);
        line.Append("; culture=").Append(CultureName(CultureInfo.CurrentCulture)).Append('/').Append(CultureName(CultureInfo.CurrentUICulture));
        line.Append("; tz=").Append(TimeZoneInfo.Local.Id).Append(" (UTC")
            .Append(offset < TimeSpan.Zero ? '-' : '+').Append(offset.Duration().ToString(@"hh\:mm", inv)).Append(')');
        line.Append("; pid=").Append(LogText.Int(Environment.ProcessId));
        return line.ToString();
    }

    /// <summary>Reads the working folder.</summary>
    /// <returns>The path, or a question mark when it cannot be read.</returns>
    private static string CurrentDirectory()
    {
        try
        {
            return Environment.CurrentDirectory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "?";
        }
    }

    /// <summary>Names a culture, the invariant one included.</summary>
    /// <param name="culture">The culture.</param>
    /// <returns>Its name, such as <c>ro-RO</c>, or <c>invariant</c>.</returns>
    private static string CultureName(CultureInfo culture) => string.IsNullOrEmpty(culture.Name) ? "invariant" : culture.Name;
}
