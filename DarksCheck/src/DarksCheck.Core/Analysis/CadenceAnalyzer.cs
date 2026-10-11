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

namespace DarksCheck.Core;

/// <summary>A pause longer than <see cref="Th.PauseSeconds"/> before a frame: which frame, how long.</summary>
internal sealed record LongPause(int FrameIndex, double Seconds);

/// <summary>
/// The rhythm of a group of frames (R-006): when it started and ended, how much of the span
/// the shutter was open, the typical interval between two starts, the pause before each
/// frame (the seconds between the end of the previous exposure and the start of this one, one
/// entry per position of the list given), and the pauses long enough to be gaps. Frames
/// without a time or an exposure stay out of the figures and get no pause.
/// </summary>
internal sealed record Cadence(
    DateTime? Start,
    DateTime? End,
    double SpanSeconds,
    double IntegrationSeconds,
    int TimedFrames,
    double? MedianIntervalSeconds,
    IReadOnlyList<double?> PauseSeconds,
    IReadOnlyList<LongPause> LongPauses)
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
        List<LongPause> longPauses = [];
        List<double> intervals = [];
        DateTime? start = null, end = null;
        double integration = 0;
        int timed = 0;
        DateTime? previousStart = null, previousEnd = null;
        for (int i = 0; i < frames.Count; i++)
        {
            FrameInfo f = frames[i];
            if (f.Timestamp is not DateTime t || f.ExposureSeconds is not double exposure)
            {
                previousStart = null;
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
                double pause = (t - p).TotalSeconds;
                pauses[i] = pause;
                if (pause > Th.PauseSeconds)
                {
                    longPauses.Add(new LongPause(f.Index, pause));
                }
            }

            if (previousStart is DateTime ps)
            {
                intervals.Add((t - ps).TotalSeconds);
            }

            previousStart = t;
            previousEnd = thisEnd;
        }

        double span = start is DateTime s && end is DateTime e ? (e - s).TotalSeconds : 0;
        return new Cadence(start, end, span, integration, timed, Median(intervals), pauses, longPauses);
    }

    private static double? Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        values.Sort();
        int n = values.Count;
        return n % 2 == 1 ? values[n / 2] : 0.5 * (values[n / 2 - 1] + values[n / 2]);
    }
}
