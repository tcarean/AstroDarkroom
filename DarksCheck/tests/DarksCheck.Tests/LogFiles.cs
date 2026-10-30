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
using System.Text.RegularExpressions;
using DarksCheck.Core;
using NLog;
using NLog.Config;
using Xunit.Abstractions;

namespace DarksCheck.Tests;

/// <summary>
/// The file side of the diagnostic log's tests, linked into DarksCheck.Wpf.Tests too (LightsCheck's
/// LogFiles): the start of a line, the files one executable left in a folder, a private factory
/// standing for one process, an elapsed time as the log writes it, and the two-writer check of
/// R-103, which the window's tests run on the build of the atomic target the window ships.
/// </summary>
internal static partial class LogFiles
{
    /// <summary>How many lines each of the two writers writes: about 1.6 MiB together, past the roll.</summary>
    private const int WriterLines = 4000;

    /// <summary>The start of every line: the time to the tenth of a millisecond, the level padded to five, the process and thread ids, the logger.</summary>
    [GeneratedRegex(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{4} (TRACE|DEBUG|INFO |WARN |ERROR|FATAL) \[\d+:\d+\] \S+ ")]
    internal static partial Regex LinePrefix();

    /// <summary>An elapsed time as the log writes it, in either form: <c>12 ms</c>, <c>12.9 s</c>.</summary>
    [GeneratedRegex(@"\d+(\.\d+)? m?s\b")]
    internal static partial Regex ElapsedTime();

    /// <summary>The end of one writer's line: its letter in both cases, its number, its payload.</summary>
    [GeneratedRegex(@" (A|B) (a|b) (\d{5}) ([AB]+)$")]
    private static partial Regex WriterLine();

    /// <summary>A private factory on <paramref name="config"/>, standing for one process.</summary>
    internal static LogFactory Factory(LoggingConfiguration config) => new() { Configuration = config };

    /// <summary>The files one executable left in a folder, oldest first by NLog's own numbering: the bare name, then _01, _02 and so on.</summary>
    internal static string[] FilesOf(string folder, string executable) =>
        Directory.GetFiles(folder, executable + "*.log")
            .Where(f => Regex.IsMatch(Path.GetFileName(f), "^" + Regex.Escape(executable) + @"(_\d+)?\.log$", RegexOptions.IgnoreCase))
            .OrderBy(f => Path.GetFileNameWithoutExtension(f).Length)
            .ThenBy(f => f, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Two processes of one executable (two windows) write into the same file at full speed, and
    /// every line of both arrives whole, once, past the 1 MiB roll too (R-100, R-103). Two factories
    /// stand for the two processes, each opening the file on its own, each on a thread of its own,
    /// released together. NLog's plain target failed this in FlatCheck: closed between lines it lost
    /// about 1 % of them, kept open one writer lost all of its (its ADR 0070).
    /// </summary>
    internal static void TwoWritersKeepEveryLine(string folder, string executable, ITestOutputHelper output)
    {
        using LogFactory a = Factory(LogSetup.Build(executable, folder, LogLevel.Debug));
        using LogFactory b = Factory(LogSetup.Build(executable, folder, LogLevel.Debug));

        // Two threads of their own, released together: Parallel.Invoke may run the second action
        // after the first on a busy pool, and then nothing would overlap.
        using Barrier together = new(2);
        Thread ta = new(() => Write(a.GetLogger("A"), "a", 'A', together));
        Thread tb = new(() => Write(b.GetLogger("B"), "b", 'B', together));
        ta.Start();
        tb.Start();
        ta.Join();
        tb.Join();
        a.Shutdown();
        b.Shutdown();

        string[] files = FilesOf(folder, executable);
        foreach (string f in files)
        {
            output.WriteLine(Path.GetFileName(f) + " " + new FileInfo(f).Length.ToString(CultureInfo.InvariantCulture) + " bytes");
        }

        (List<int> fromA, List<int> fromB, int broken, string order) = WritersLines(folder, executable);
        output.WriteLine("whole lines: " + fromA.Count.ToString(CultureInfo.InvariantCulture) + " from A, " + fromB.Count.ToString(CultureInfo.InvariantCulture)
            + " from B; broken: " + broken.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(Directory.GetFiles(folder).Length, files.Length);
        Assert.InRange(files.Length, 2, LogSetup.FilesPerExecutable);
        Assert.Equal(0, broken);
        Assert.Equal(Enumerable.Range(0, WriterLines), fromA.Order());
        Assert.Equal(Enumerable.Range(0, WriterLines), fromB.Order());
        // The writers did overlap: their lines interleave in the file, both ways round.
        Assert.Contains("AB", order, StringComparison.Ordinal);
        Assert.Contains("BA", order, StringComparison.Ordinal);
    }

    /// <summary>One writer's lines: its tag, the line's number, then its letter as many times as the number says.</summary>
    private static void Write(Logger log, string tag, char letter, Barrier together)
    {
        together.SignalAndWait();
        for (int i = 0; i < WriterLines; i++)
        {
            log.Info(tag + " " + i.ToString("D5", CultureInfo.InvariantCulture) + " " + new string(letter, 140 + (i % 37)));
        }
    }

    /// <summary>
    /// The lines two writers left in a folder, whole, by writer: the numbers each one's lines carry,
    /// the count of lines that are neither writer's whole line, and the writers' order in the files.
    /// </summary>
    private static (List<int> A, List<int> B, int Broken, string Order) WritersLines(string folder, string executable)
    {
        List<int> a = [];
        List<int> b = [];
        StringBuilder order = new();
        int broken = 0;
        foreach (string line in FilesOf(folder, executable).SelectMany(File.ReadAllLines))
        {
            Match m = WriterLine().Match(line);
            bool ok = LinePrefix().IsMatch(line) && m.Success
                && string.Equals(m.Groups[1].Value, m.Groups[2].Value, StringComparison.OrdinalIgnoreCase);
            int n = ok ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : -1;
            // The payload is the writer's letter, as many as the line's number says.
            ok = ok && m.Groups[4].Value == new string(m.Groups[1].Value[0], 140 + (n % 37));
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
}
