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

// RunClock.cs - a run's first and last lines in the diagnostic log and the time of each stage
// between them (ADR 0069, R-171). Taken from DarksCheck's (its ADR 0025), itself LightsCheck's
// and FlatCheck's (FlatCheck's ADR 0072).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NLog;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// A run's first and last lines in the diagnostic log, and the time of each stage between
    /// them: <c>analysis start ...</c>, then <c>analysis done verdict=Attention ... in 10312 ms
    /// (load 2911 ms, file 80 ms, ...)</c> (R-171). Held in a <c>using</c>: a run that leaves
    /// without <see cref="Done"/>, because it threw, writes <c>ended without an outcome after N
    /// ms</c> with the stages it reached; its caller logs the exception.
    /// </summary>
    internal sealed class RunClock : IDisposable
    {
        /// <summary>The run's logger.</summary>
        readonly Logger _log;

        /// <summary>The run's name, the first word of both lines.</summary>
        readonly string _run;

        /// <summary>Runs from the start line.</summary>
        readonly Stopwatch _total = Stopwatch.StartNew();

        /// <summary>The stages timed so far, as <c>name N ms</c>.</summary>
        readonly List<string> _laps = new List<string>();

        /// <summary>When the current stage began, in milliseconds since the start.</summary>
        long _stageStart;

        /// <summary>True once the last line was written, by <see cref="Done"/> or by <see cref="Dispose"/>.</summary>
        bool _ended;

        /// <summary>A clock for one run, started now.</summary>
        /// <param name="log">The run's logger.</param>
        /// <param name="run">The run's name.</param>
        RunClock(Logger log, string run)
        {
            _log = log;
            _run = run;
        }

        /// <summary>Writes the run's start line, at <c>Info</c>, and starts its clock.</summary>
        /// <param name="log">The run's logger; not null.</param>
        /// <param name="run">The run's name: <c>analysis</c>, <c>curves</c>, <c>proof</c>.</param>
        /// <param name="inputs">The file and the parameters the run was given, already formatted.</param>
        /// <returns>The clock, to hold in a <c>using</c>.</returns>
        internal static RunClock Start(Logger log, string run, string inputs)
        {
            ArgumentNullException.ThrowIfNull(log);
            log.Info(run + " start " + inputs);
            return new RunClock(log, run);
        }

        /// <summary>Ends the current stage and starts the next.</summary>
        /// <param name="stage">The stage that ended: <c>load</c>, <c>tones</c>, ...</param>
        internal void Lap(string stage)
        {
            long now = _total.ElapsedMilliseconds;
            _laps.Add(stage + " " + LogText.Ms(now - _stageStart));
            _stageStart = now;
        }

        /// <summary>
        /// Writes the run's last line, at <c>Info</c>: its outcome, its total time and the time of
        /// each stage.
        /// </summary>
        /// <param name="outcome">What the run concluded: <c>verdict=Attention recommendations=7</c>, <c>refused: ...</c>, ...</param>
        internal void Done(string outcome)
        {
            _ended = true;
            _log.Info(_run + " done " + outcome + " in " + LogText.Ms(_total.ElapsedMilliseconds) + Stages());
        }

        /// <summary>
        /// Writes the last line of a run that left without an outcome, at <c>Info</c>, with the
        /// stages it reached; nothing after <see cref="Done"/>.
        /// </summary>
        public void Dispose()
        {
            if (_ended) return;

            _ended = true;
            _log.Info(_run + " ended without an outcome after " + LogText.Ms(_total.ElapsedMilliseconds) + Stages());
        }

        /// <summary>The stages timed, in parentheses; nothing when none was.</summary>
        /// <returns>The text, with a leading space, or empty.</returns>
        string Stages() => _laps.Count == 0 ? "" : " (" + string.Join(", ", _laps) + ")";
    }
}
