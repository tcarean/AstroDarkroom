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

using System.Diagnostics;
using NLog;

namespace ImageVisualCompare.Wpf.Logging;

/// <summary>
/// A run's first and last lines in the diagnostic log, and the time of each stage between them:
/// <c>load start ...</c>, then <c>load done mode=Linear padded=no ... in 2731 ms (probe A 3 ms,
/// probe B 2 ms, decode A 1360 ms, decode B 1366 ms)</c> (R-121). Held in a <c>using</c>: a run
/// that leaves without <see cref="Done"/>, because it threw, writes <c>ended without an outcome
/// after N ms</c> with the stages it reached, beside the exception the crash hook logs.
/// </summary>
internal sealed class RunClock : IDisposable
{
    /// <summary>The run's logger.</summary>
    private readonly Logger _log;

    /// <summary>The run's name, the first word of both lines.</summary>
    private readonly string _run;

    /// <summary>Runs from the start line.</summary>
    private readonly Stopwatch _total = Stopwatch.StartNew();

    /// <summary>The stages timed so far, as <c>name N ms</c>.</summary>
    private readonly List<string> _laps = [];

    /// <summary>When the current stage began, in milliseconds since the start.</summary>
    private long _stageStart;

    /// <summary>True once the last line was written, by <see cref="Done"/> or <see cref="Dispose"/>.</summary>
    private bool _ended;

    /// <summary>Builds a clock for one run, started now.</summary>
    /// <param name="log">The run's logger.</param>
    /// <param name="run">The run's name.</param>
    private RunClock(Logger log, string run)
    {
        _log = log;
        _run = run;
    }

    /// <summary>Writes the run's start line, at <c>Info</c>, and starts its clock.</summary>
    /// <param name="log">The run's logger.</param>
    /// <param name="run">The run's name: <c>load</c>.</param>
    /// <param name="inputs">The files and options the run was given, already written for the log.</param>
    /// <returns>The clock, to hold in a <c>using</c>.</returns>
    internal static RunClock Start(Logger log, string run, string inputs)
    {
        log.Info(run + " start " + inputs);
        return new RunClock(log, run);
    }

    /// <summary>Ends the current stage and starts the next.</summary>
    /// <param name="stage">The stage that ended: <c>probe A</c>, <c>decode B</c>, ...</param>
    internal void Lap(string stage)
    {
        long now = _total.ElapsedMilliseconds;
        _laps.Add(stage + " " + LogText.Ms(now - _stageStart));
        _stageStart = now;
    }

    /// <summary>
    /// Writes the run's last line, at <c>Info</c>: its outcome, its total time and the time of each
    /// stage.
    /// </summary>
    /// <param name="outcome">What the run came to: <c>mode=Linear padded=no ...</c>, <c>declined</c>, <c>failed ...</c>.</param>
    internal void Done(string outcome)
    {
        _ended = true;
        _log.Info(_run + " done " + outcome + " in " + LogText.Ms(_total.ElapsedMilliseconds) + Stages());
    }

    /// <summary>
    /// Writes the last line of a run that left without an outcome, at <c>Info</c>, with the stages
    /// it reached; nothing after <see cref="Done"/>.
    /// </summary>
    public void Dispose()
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        _log.Info(_run + " ended without an outcome after " + LogText.Ms(_total.ElapsedMilliseconds) + Stages());
    }

    /// <summary>Writes the stages timed, in parentheses.</summary>
    /// <returns>The text with a leading space, or empty when no stage was timed.</returns>
    private string Stages() => _laps.Count == 0 ? string.Empty : " (" + string.Join(", ", _laps) + ")";
}
