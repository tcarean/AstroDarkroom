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

internal static class CadenceAnalyzer
{
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
