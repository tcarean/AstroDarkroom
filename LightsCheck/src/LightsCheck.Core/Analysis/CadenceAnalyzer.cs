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

    /// <summary>
    /// The first place where the list's order is not the order of the times (R-089): the
    /// positions in the list, from 0, of two frames with a time of capture, the second listed
    /// after the first with no other frame with a time between them, and taken before it. Null
    /// when every frame with a time was taken no earlier than the one listed before it.
    /// </summary>
    internal (int ListedBefore, int ListedAfter)? Backwards { get; init; }
}

/// <summary>
/// Reads a session's rhythm from the frames' times of capture and exposures (R-006), and
/// whether the list's order is the order of the times (R-089).
/// </summary>
internal static class CadenceAnalyzer
{
    /// <summary>
    /// Measures the span, the integration and the pause before each frame of
    /// <paramref name="frames"/>, taken in the order given.
    /// </summary>
    /// <param name="frames">
    /// The frames, in the list's order, which is taken for the order of capture; may be a
    /// filtered list. Not null.
    /// </param>
    /// <returns>
    /// The cadence, with one pause entry per position of <paramref name="frames"/>, and in
    /// <see cref="Cadence.Backwards"/> the first place where the times say the list is not in
    /// the order of capture.
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
        return new Cadence(start, end, span, integration, timed, pauses) { Backwards = FirstBackwards(frames) };
    }

    /// <summary>
    /// The first frame with a time of capture that was taken before the frame with a time listed
    /// before it (R-089). The list is in the order of the file names, which the camera's counter
    /// passing 9999, or numbers without leading zeros, set apart from the order of the night. A
    /// frame without an exposure still has its time; equal times are no disagreement, since a
    /// raw's time is in whole seconds.
    /// </summary>
    /// <param name="frames">The frames, in the list's order. Not null.</param>
    /// <returns>
    /// The positions in the list, from 0, of the frame listed before and of the one taken before
    /// it; null when the times never go back.
    /// </returns>
    private static (int ListedBefore, int ListedAfter)? FirstBackwards(IReadOnlyList<FrameInfo> frames)
    {
        int previous = -1;
        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i].Timestamp is not DateTime t)
            {
                continue;
            }

            if (previous >= 0 && t < frames[previous].Timestamp)
            {
                return (previous, i);
            }

            previous = i;
        }

        return null;
    }
}
