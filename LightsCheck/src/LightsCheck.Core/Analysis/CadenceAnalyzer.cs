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

namespace LightsCheck.Core;

/// <summary>
/// The rhythm of a session (R-006): when it started and ended, how much of the span the
/// shutter was open, and the pause before each frame (the seconds between the end of the
/// previous exposure and the start of this one), one entry per position of the list given.
/// Frames without a time or an exposure stay out of the figures and get no pause.
/// </summary>
/// <param name="Start">
/// The time of capture of the first timed frame of the list; null when none is timed.
/// </param>
/// <param name="End">
/// The latest end of exposure (time of capture plus exposure); null when none is timed.
/// </param>
/// <param name="SpanSeconds">
/// From <paramref name="Start"/> to <paramref name="End"/>, in seconds; 0 when nothing is timed.
/// </param>
/// <param name="IntegrationSeconds">The sum of the timed frames' exposures, in seconds.</param>
/// <param name="TimedFrames">How many frames carry both a time of capture and an exposure.</param>
/// <param name="PauseSeconds">
/// Per position of the list, the seconds from the previous frame's end of exposure to this
/// frame's capture; null for the first timed frame, for an untimed frame and for the frame right
/// after an untimed one. Negative when the times overlap.
/// </param>
internal sealed record Cadence(
    DateTime? Start,
    DateTime? End,
    double SpanSeconds,
    double IntegrationSeconds,
    int TimedFrames,
    IReadOnlyList<double?> PauseSeconds)
{
    /// <summary>Integration over span, in percent; 0 when nothing is timed.</summary>
    internal double DutyPct => SpanSeconds > 0 ? 100.0 * IntegrationSeconds / SpanSeconds : 0;
}

/// <summary>
/// Reads a session's rhythm from the frames' times of capture and exposures (R-006).
/// </summary>
internal static class CadenceAnalyzer
{
    /// <summary>
    /// Measures the span, the integration and the pause before each frame of
    /// <paramref name="frames"/>, taken in the order given.
    /// </summary>
    /// <param name="frames">The frames, in capture order; may be a filtered list. Not null.</param>
    /// <returns>
    /// The cadence, with one pause entry per position of <paramref name="frames"/>.
    /// </returns>
    internal static Cadence Analyze(IReadOnlyList<FrameInfo> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        // One pause per position of the list, whatever the frames' indices: a filtered list
        // (only the good frames) must work too.
        double?[] pauses = new double?[frames.Count];
        DateTime? start = null, end = null;
        double integration = 0;
        int timed = 0;
        DateTime? previousEnd = null;
        for (int i = 0; i < frames.Count; i++)
        {
            FrameInfo f = frames[i];
            if (f.Timestamp is not DateTime t || f.ExposureSeconds is not double exposure)
            {
                previousEnd = null;
                continue;
            }

            DateTime thisEnd = t.AddSeconds(exposure);
            start ??= t;
            if (end is null || thisEnd > end)
            {
                end = thisEnd;
            }

            integration += exposure;
            timed++;
            if (previousEnd is DateTime p)
            {
                pauses[i] = (t - p).TotalSeconds;
            }

            previousEnd = thisEnd;
        }

        double span = start is DateTime s && end is DateTime e ? (e - s).TotalSeconds : 0;
        return new Cadence(start, end, span, integration, timed, pauses);
    }
}
