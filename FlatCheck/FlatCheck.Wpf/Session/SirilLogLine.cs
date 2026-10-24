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

// SirilLogLine.cs - a line from siril-cli's stdout, read.
//
// Measured on Siril 1.4.4, with the WithoutDark script on a session of 3+3+3 frames: 1735
// lines, of which 1324 are progress updates. They come in four kinds:
//
//   log: <text>                       - what the log panel of the Siril window shows too
//   progress: <label>, 33.33%         - the start of an operation with progress
//   ESC[A ESC[2K Tprogress: 66.67%    - its update, with "go up a row, erase it" in front,
//                                       so that it overwrites itself in the console
//   <anything else>                   - debug prints (blocks, "running command" with a
//                                       number, "seqfile ... already exists")
//
// The window shows only the first kind, like Siril, and turns the second and third into a
// bar. The fourth is not shown: there are hundreds, and they tell the user nothing the log
// would not. The parsing is pure and pinned on recorded lines, not on what Siril ought to
// write.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FlatCheck.Wpf
{
    /// <summary>The kinds of siril-cli stdout line the window tells apart.</summary>
    internal enum SirilLineKind { Log, Progress, Other }

    /// <summary>One parsed line of siril-cli's stdout.</summary>
    /// <param name="Kind">Which of the kinds it is.</param>
    /// <param name="Text">A log line's text without "log: "; otherwise the whole line without
    /// its ANSI escapes.</param>
    /// <param name="Label">A progress line's label, "" when it has none or is not
    /// one.</param>
    /// <param name="Percent">A progress line's share, 0..100; NaN otherwise.</param>
    internal readonly record struct SirilLogLine(SirilLineKind Kind, string Text, string Label, double Percent)
    {
        /// <summary>The prefix with which Siril announces the next command, in the log
        /// lines.</summary>
        internal const string RunningCommand = "Running command: ";

        static readonly Regex Ansi = new(@"\x1b\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
        static readonly Regex Progress = new(@"^T?progress: (?:(?<label>.*), )?(?<pct>[0-9]+(?:\.[0-9]+)?)%$",
                                             RegexOptions.Compiled);

        /// <summary>Parses one raw stdout line: strips the ANSI escapes and the line end, then
        /// tells a log line, a progress line and anything else apart.</summary>
        /// <param name="raw">The line as read; null allowed (gives Other).</param>
        /// <returns>The parsed line; never throws.</returns>
        internal static SirilLogLine Parse(string raw)
        {
            if (raw == null) return new SirilLogLine(SirilLineKind.Other, "", "", double.NaN);

            string line = Ansi.Replace(raw, "").TrimEnd('\r', '\n');

            if (line.StartsWith("log: ", StringComparison.Ordinal))
                return new SirilLogLine(SirilLineKind.Log, line.Substring(5), "", double.NaN);

            Match m = Progress.Match(line);
            if (m.Success)
            {
                double pct = double.Parse(m.Groups["pct"].Value, CultureInfo.InvariantCulture);
                return new SirilLogLine(SirilLineKind.Progress, line, m.Groups["label"].Value, pct);
            }

            return new SirilLogLine(SirilLineKind.Other, line, "", double.NaN);
        }

        /// <summary>true when the log line announces a command; its verb is in
        /// <paramref name="verb"/>.</summary>
        /// <param name="verb">The command Siril echoes after the prefix, trimmed; null when the
        /// line is not a command start.</param>
        /// <returns>True for a log line that starts with <see cref="RunningCommand"/>.</returns>
        internal bool IsCommandStart(out string verb)
        {
            verb = null;
            if (Kind != SirilLineKind.Log || !Text.StartsWith(RunningCommand, StringComparison.Ordinal)) return false;
            verb = Text.Substring(RunningCommand.Length).Trim();
            return true;
        }
    }
}
