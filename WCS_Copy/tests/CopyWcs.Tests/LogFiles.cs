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
using NLog.Config;

namespace CopyWcs.Tests;

/// <summary>
/// The log's files as the tests read them, linked into CopyWcs.Wpf.Tests too: the line prefix,
/// one executable's files in NLog's order, a private factory standing for one process, and two
/// writers at full speed into one file. Shared so that R-045 runs in both test assemblies, each
/// loading the build of <c>NLog.Targets.AtomicFile</c> its application ships: the console's
/// <c>net8.0</c> one here, the window's <c>net8.0-windows7.0</c> one there (the review of
/// 2026-10-30).
/// </summary>
internal static class LogFiles
{
    /// <summary>The start of every line the applications' layout writes: time, level, ids, logger.</summary>
    internal static readonly Regex LinePrefix = new(
        @"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{4} (TRACE|DEBUG|INFO |WARN |ERROR|FATAL) \[\d+:\d+\] \S+ ",
        RegexOptions.Compiled);

    /// <summary>The files one executable left in a folder, oldest first by NLog's own numbering:
    /// the bare name, then _01, _02 and so on.</summary>
    internal static string[] FilesOf(string folder, string executable) =>
        Directory.GetFiles(folder, executable + "*.log")
                 .Where(f => Regex.IsMatch(Path.GetFileName(f), "^" + Regex.Escape(executable) + @"(_\d+)?\.log$", RegexOptions.IgnoreCase))
                 .OrderBy(f => Path.GetFileNameWithoutExtension(f).Length)
                 .ThenBy(f => f, StringComparer.Ordinal)
                 .ToArray();

    /// <summary>A private factory on <paramref name="config"/>, standing for one process.</summary>
    internal static LogFactory Factory(LoggingConfiguration config) => new() { Configuration = config };

    /// <summary>What two writers left in one executable's files.</summary>
    /// <param name="Files">The files, oldest first.</param>
    /// <param name="FromA">The numbers of writer A's whole lines, in file order.</param>
    /// <param name="FromB">The numbers of writer B's whole lines, in file order.</param>
    /// <param name="Broken">The lines that are neither writer's whole line.</param>
    /// <param name="Order">The writers' letters in file order, one per whole line.</param>
    internal sealed record TwoWriters(string[] Files, List<int> FromA, List<int> FromB, int Broken, string Order);

    /// <summary>
    /// Two factories through the applications' configuration of <paramref name="executable"/>,
    /// standing for two processes, each on a thread of its own released together by a barrier
    /// (Parallel.Invoke may run the second after the first on a busy pool, and then nothing
    /// would overlap), write <paramref name="lines"/> lines each at full speed into the same
    /// file; then their files are read back.
    /// </summary>
    internal static TwoWriters WriteTwoAtFullSpeed(string folder, string executable, int lines)
    {
        using LogFactory a = Factory(LogSetup.Build(executable, folder, LogLevel.Debug));
        using LogFactory b = Factory(LogSetup.Build(executable, folder, LogLevel.Debug));
        using var together = new Barrier(2);
        var ta = new Thread(() =>
        {
            Logger log = a.GetLogger("A");
            together.SignalAndWait();
            for (int i = 0; i < lines; i++)
            {
                log.Info($"a {i:D5} " + new string('A', 140 + i % 37));
            }
        });
        var tb = new Thread(() =>
        {
            Logger log = b.GetLogger("B");
            together.SignalAndWait();
            for (int i = 0; i < lines; i++)
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

        var fromA = new List<int>();
        var fromB = new List<int>();
        var order = new StringBuilder();
        int broken = 0;
        var whole = new Regex(@" (A|B) (a|b) (\d{5}) ([AB]+)$");
        string[] files = FilesOf(folder, executable);
        foreach (string line in files.SelectMany(File.ReadAllLines))
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

            (m.Groups[1].Value == "A" ? fromA : fromB).Add(n);
            order.Append(m.Groups[1].Value);
        }

        return new TwoWriters(files, fromA, fromB, broken, order.ToString());
    }

    /// <summary>
    /// Asserts R-045 on what <see cref="WriteTwoAtFullSpeed"/> left: every line of both writers
    /// whole and once, the roll crossed within the five files, and the writers interleaved both
    /// ways round, so they did overlap.
    /// </summary>
    internal static void AssertEveryLineKept(string folder, TwoWriters result, int lines)
    {
        Assert.Equal(Directory.GetFiles(folder).Length, result.Files.Length);
        Assert.InRange(result.Files.Length, 2, LogSetup.FilesPerExecutable);
        Assert.Equal(0, result.Broken);
        Assert.Equal(Enumerable.Range(0, lines), result.FromA.OrderBy(n => n));
        Assert.Equal(Enumerable.Range(0, lines), result.FromB.OrderBy(n => n));
        Assert.Contains("AB", result.Order, StringComparison.Ordinal);
        Assert.Contains("BA", result.Order, StringComparison.Ordinal);
    }
}
