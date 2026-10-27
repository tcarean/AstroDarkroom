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

// LogSetup.cs - the one place that knows how the diagnostic log is configured (ADR 0070,
// R-109 to R-111): the folder, the file names, the rolling, the level, the line layout, the
// first and the last line of a process. The two applications call Apply first and Close last;
// nothing else configures NLog, so the core and the tests, which never call Apply, log into
// nothing. docs/design/logging.md holds how the parts fit and what was measured.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace FlatCheck
{
    /// <summary>Where a process's log goes, as <see cref="LogSetup.Plan"/> decided it.</summary>
    /// <param name="ExecutableName">The start of every file's name.</param>
    /// <param name="Folder">The folder of the files; null when the level is <c>off</c>.</param>
    /// <param name="FellBack">True when the folder beside the executable could not be written
    /// and the fallback was taken.</param>
    /// <param name="Level">The lowest level written.</param>
    internal sealed record LogPlace(string ExecutableName, string Folder, bool FellBack, LogLevel Level);

    /// <summary>
    /// The diagnostic log's configuration: a UTF-8 text file per executable in the folder
    /// <c>log</c> beside it, rolled at <see cref="FileSizeLimit"/>, at most
    /// <see cref="FilesPerExecutable"/> files kept, at the level <see cref="LevelVariable"/>
    /// names.
    /// </summary>
    internal static class LogSetup
    {
        /// <summary>The size above which the file being written is left for the next one,
        /// in bytes: 1 MiB (R-109).</summary>
        internal const long FileSizeLimit = 1024 * 1024;

        /// <summary>The most files one executable keeps in its folder, the one being written
        /// included (R-109). NLog 6.2.2's <c>MaxArchiveFiles</c> counts that file too: set to
        /// 4, it left 4 files (measured by
        /// <c>R109_TheLogRollsAt1MiBAndKeepsAtMost5Files</c>), so it takes this value.</summary>
        internal const int FilesPerExecutable = 5;

        /// <summary>The environment variable that sets the level (R-111).</summary>
        internal const string LevelVariable = "FLATCHECK_LOG";

        /// <summary>The folder's name, beside the executable and in the fallback.</summary>
        internal const string FolderName = "log";

        /// <summary>The folder under <c>%LOCALAPPDATA%</c> that holds the fallback
        /// <see cref="FolderName"/>.</summary>
        internal const string FallbackFolder = "FlatCheck";

        /// <summary>The level when <see cref="LevelVariable"/> is unset or names no
        /// level.</summary>
        internal static readonly LogLevel DefaultLevel = LogLevel.Debug;

        /// <summary>
        /// One line per event: local time to the tenth of a millisecond, the level padded to
        /// five, the process and thread ids, the logger, the message; an exception follows on
        /// the next lines as <see cref="Exception.ToString"/> writes it (type, message, stack,
        /// inner exceptions).
        /// </summary>
        internal const string LineLayout =
            "${longdate} ${level:uppercase=true:padding=-5} [${processid}:${threadid}] ${logger} ${message}"
            + "${onexception:inner=${newline}${exception:format=ToString}}";

        /// <summary>The logger of the process's own lines: the first, the last, the crashes.</summary>
        private static readonly Logger Log = LogManager.GetLogger("Process");

        /// <summary>1 once <see cref="Apply"/> ran in this process; a second call does
        /// nothing.</summary>
        private static int applied;

        /// <summary>Runs from <see cref="Apply"/>, for the elapsed time of the last
        /// line.</summary>
        private static Stopwatch clock;

        /// <summary>The last exception logged as fatal, so the same crash seen by two handlers
        /// is written once with its stack.</summary>
        private static Exception lastFatal;

        /// <summary>
        /// Configures the process's log, writes its first line and hooks the unhandled
        /// exceptions. Called first by each application's entry point, and only there; a
        /// second call does nothing. With <see cref="LevelVariable"/> at <c>off</c> nothing is
        /// created.
        /// </summary>
        /// <param name="entryName">The executable's name as built (<c>flatcheck</c>,
        /// <c>FlatCheck.Wpf</c>), used when the process path names a host instead
        /// (<c>dotnet</c>).</param>
        /// <param name="args">The command line, logged verbatim; never null.</param>
        internal static void Apply(string entryName, IReadOnlyList<string> args)
        {
            if (Interlocked.Exchange(ref applied, 1) == 1) return;
            clock = Stopwatch.StartNew();

            string fallbackRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(fallbackRoot)) fallbackRoot = Path.GetTempPath();
            LogPlace place = Plan(entryName, Environment.ProcessPath, AppContext.BaseDirectory, fallbackRoot,
                                  Environment.GetEnvironmentVariable(LevelVariable));
            if (place.Folder == null)
            {
                // Off: an empty configuration, so NLog does not go looking for an NLog.config.
                LogManager.Configuration = new LoggingConfiguration();
                return;
            }

            LogManager.ThrowExceptions = false;
            LogManager.Configuration = Build(place.ExecutableName, place.Folder, place.Level);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Fatal(e.ExceptionObject as Exception, "unhandled exception, the process ends"
                      + (e.ExceptionObject is Exception ? "" : ": " + e.ExceptionObject));
            TaskScheduler.UnobservedTaskException += (s, e) =>
                Log.Error(e.Exception, "a task failed and nobody awaited it");

            Log.Info(StartupLine(place.ExecutableName, args, place.Folder, place.FellBack, place.Level));
        }

        /// <summary>
        /// Decides where the log goes and at which level, without configuring anything: the
        /// executable's name, the folder (created, unless the level is <c>off</c>), whether it
        /// is the fallback. <see cref="Apply"/> runs it on the process's own values.
        /// </summary>
        /// <param name="entryName">The executable's name as built.</param>
        /// <param name="processPath">The process's path; null when unknown.</param>
        /// <param name="baseDirectory">The executable's folder.</param>
        /// <param name="fallbackRoot">The folder for the fallback.</param>
        /// <param name="levelValue">The value of <see cref="LevelVariable"/>; null when
        /// unset.</param>
        /// <returns>The place; its folder is null when the level is <c>off</c>, and then
        /// nothing was created.</returns>
        internal static LogPlace Plan(string entryName, string processPath, string baseDirectory, string fallbackRoot, string levelValue)
        {
            LogLevel level = LevelFrom(levelValue);
            string name = ExecutableName(processPath, entryName);
            if (level == LogLevel.Off) return new LogPlace(name, null, false, level);
            string folder = ResolveFolder(baseDirectory, fallbackRoot, name, out bool fellBack);
            return new LogPlace(name, folder, fellBack, level);
        }

        /// <summary>
        /// Writes the process's last line, the exit code and the time since
        /// <see cref="Apply"/>, and closes the log. Called last by each application; does
        /// nothing when <see cref="Apply"/> did not run.
        /// </summary>
        /// <param name="exitCode">The process's exit code.</param>
        internal static void Close(int exitCode)
        {
            if (Volatile.Read(ref applied) == 0) return;
            Log.Info(FormattableString.Invariant($"exit {exitCode} after {clock.Elapsed.TotalSeconds:F1} s"));
            LogManager.Shutdown();
        }

        /// <summary>
        /// Logs an exception nobody handled, at <c>Fatal</c>, and flushes. The same exception
        /// seen a second time (the window's dispatcher, then the process) is named, not
        /// written again.
        /// </summary>
        /// <param name="e">The exception; null when the runtime handed something else.</param>
        /// <param name="where">Where it was caught, in English.</param>
        internal static void Fatal(Exception e, string where)
        {
            if (e != null && ReferenceEquals(e, Interlocked.Exchange(ref lastFatal, e)))
                Log.Fatal(where + ": the exception logged above");
            else
                Log.Fatal(e, where);
            LogManager.Flush();
        }

        /// <summary>
        /// The configuration of one executable's log: one atomic-append file target, every
        /// logger from <paramref name="minimum"/> up; no console target, ever (stdout and stderr
        /// are the console's contract, R-001). <c>Off</c> gives a configuration without targets.
        /// </summary>
        /// <param name="executableName">The start of every file's name.</param>
        /// <param name="folder">The folder of the files; created on the first line when
        /// missing.</param>
        /// <param name="minimum">The lowest level written.</param>
        /// <returns>The configuration, not yet applied.</returns>
        internal static LoggingConfiguration Build(string executableName, string folder, LogLevel minimum)
        {
            var config = new LoggingConfiguration { DefaultCultureInfo = CultureInfo.InvariantCulture };
            if (minimum == LogLevel.Off) return config;

            // The atomic target, so a second process of the same executable (two windows)
            // appends to the same file without a line lost: the operating system places every
            // write at the end. NLog's plain target lost lines either way, closed between lines
            // (and 4.8 ms a line) or kept open (where the second of two windows wrote over the
            // first's whole log); ADR 0070 has the measurements, R-112 the requirement.
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
        /// The folder the log goes to: <see cref="FolderName"/> beside the executable when a
        /// file can be created there, otherwise <see cref="FolderName"/> under
        /// <see cref="FallbackFolder"/> in <paramref name="fallbackRoot"/>. The check creates
        /// the folder and a probe file deleted on close; no log file is touched.
        /// </summary>
        /// <param name="baseDirectory">The executable's folder.</param>
        /// <param name="fallbackRoot">The folder for the fallback, <c>%LOCALAPPDATA%</c> in the
        /// applications.</param>
        /// <param name="executableName">The start of the probe file's name.</param>
        /// <param name="fellBack">True when the fallback was taken.</param>
        /// <returns>The folder's full path.</returns>
        internal static string ResolveFolder(string baseDirectory, string fallbackRoot, string executableName, out bool fellBack)
        {
            string beside = Path.Combine(baseDirectory, FolderName);
            fellBack = !CanWrite(beside, executableName);
            if (!fellBack) return beside;
            string fallback = Path.Combine(fallbackRoot, FallbackFolder, FolderName);
            CanWrite(fallback, executableName);   // if even this fails, NLog drops the lines
            return fallback;
        }

        /// <summary>Creates <paramref name="folder"/> and a file in it, deleted when
        /// closed.</summary>
        /// <param name="folder">The folder to check.</param>
        /// <param name="executableName">The start of the probe file's name.</param>
        /// <returns>True when both could be created.</returns>
        private static bool CanWrite(string folder, string executableName)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string probe = Path.Combine(folder, FormattableString.Invariant(
                    $"{executableName}.probe-{Environment.ProcessId}-{Guid.NewGuid():N}.tmp"));
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                      || e is NotSupportedException || e is ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// The level <see cref="LevelVariable"/> names: <c>off</c>, <c>fatal</c>,
        /// <c>error</c>, <c>warn</c>, <c>info</c>, <c>debug</c> or <c>trace</c>, in any case,
        /// spaces around ignored; <see cref="DefaultLevel"/> for anything else (R-111).
        /// </summary>
        /// <param name="value">The variable's value; null when unset.</param>
        /// <returns>The level.</returns>
        internal static LogLevel LevelFrom(string value)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "off": return LogLevel.Off;
                case "fatal": return LogLevel.Fatal;
                case "error": return LogLevel.Error;
                case "warn": return LogLevel.Warn;
                case "info": return LogLevel.Info;
                case "debug": return LogLevel.Debug;
                case "trace": return LogLevel.Trace;
                default: return DefaultLevel;
            }
        }

        /// <summary>
        /// The executable's name without its extension, from the process's path; the entry
        /// name when there is no path or the path names the <c>dotnet</c> host
        /// (<c>dotnet flatcheck.dll</c>).
        /// </summary>
        /// <param name="processPath">The process's path; null when unknown.</param>
        /// <param name="entryName">The name as built.</param>
        /// <returns>The name the log files begin with.</returns>
        internal static string ExecutableName(string processPath, string entryName)
        {
            string name = string.IsNullOrEmpty(processPath) ? null : Path.GetFileNameWithoutExtension(processPath);
            return string.IsNullOrEmpty(name) || name.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? entryName : name;
        }

        /// <summary>
        /// The process's first line: the executable and its version, the path, the arguments
        /// verbatim, the working folder, the log folder, the level, the machine (OS,
        /// processors, memory), the runtime, the culture, the time zone and the process id
        /// (R-110).
        /// </summary>
        /// <param name="executableName">The executable's name.</param>
        /// <param name="args">The command line; never null.</param>
        /// <param name="folder">The log folder.</param>
        /// <param name="fellBack">True when the folder is the fallback.</param>
        /// <param name="level">The level written.</param>
        /// <returns>The line, in English.</returns>
        internal static string StartupLine(string executableName, IReadOnlyList<string> args, string folder, bool fellBack, LogLevel level)
        {
            var inv = CultureInfo.InvariantCulture;
            TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
            var sb = new StringBuilder();
            sb.Append(executableName).Append(' ').Append(VersionInfo.Value).Append(" starting");
            sb.Append("; exe=").Append(Environment.ProcessPath ?? "?");
            sb.Append("; args=[").Append(string.Join(", ", args.Select(a => "\"" + a + "\""))).Append(']');
            sb.Append("; cwd=").Append(CurrentDirectory());
            sb.Append("; log=").Append(folder);
            if (fellBack) sb.Append(" (fallback: the folder beside the executable cannot be written)");
            sb.Append("; level=").Append(level.Name);
            sb.Append("; os=").Append(RuntimeInformation.OSDescription).Append(' ').Append(RuntimeInformation.OSArchitecture);
            sb.Append("; cpus=").Append(Environment.ProcessorCount.ToString(inv));
            sb.Append("; memory=").Append((GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024)).ToString("F1", inv)).Append(" GiB");
            sb.Append("; runtime=").Append(RuntimeInformation.FrameworkDescription).Append(' ').Append(RuntimeInformation.ProcessArchitecture);
            sb.Append("; culture=").Append(CultureName(CultureInfo.CurrentCulture)).Append('/').Append(CultureName(CultureInfo.CurrentUICulture));
            sb.Append("; tz=").Append(TimeZoneInfo.Local.Id).Append(" (UTC")
              .Append(offset < TimeSpan.Zero ? '-' : '+').Append(offset.Duration().ToString(@"hh\:mm", inv)).Append(')');
            sb.Append("; pid=").Append(Environment.ProcessId.ToString(inv));
            return sb.ToString();
        }

        /// <summary>The working folder, or a question mark when it cannot be read.</summary>
        /// <returns>The path.</returns>
        private static string CurrentDirectory()
        {
            try { return Environment.CurrentDirectory; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return "?"; }
        }

        /// <summary>A culture's name, <c>invariant</c> for the invariant one.</summary>
        /// <param name="c">The culture.</param>
        /// <returns>The name.</returns>
        private static string CultureName(CultureInfo c) => string.IsNullOrEmpty(c.Name) ? "invariant" : c.Name;
    }
}
