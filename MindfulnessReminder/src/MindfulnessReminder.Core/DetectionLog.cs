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

namespace MindfulnessReminder.Core;

/// <summary>One reading, and how long after the start of the run it was taken.</summary>
/// <param name="Elapsed">The time from the start of the run to this reading.</param>
/// <param name="Activity">What the activity monitor read.</param>
/// <param name="Suppression">
/// What <see cref="SuppressionPolicy"/> made of it, with the settings of the run.
/// </param>
public readonly record struct DetectionSample(
    TimeSpan Elapsed,
    ActivitySnapshot Activity,
    ReminderSuppression Suppression);

/// <summary>
/// Renders what <c>--detect</c> watched into something readable.
///
/// It exists because of how the full-screen check has to be tested. A reading taken the
/// instant the switch is run is no use there: getting to a terminal to run it means leaving
/// full screen, which destroys the state being measured. So the switch samples for a while
/// instead, and what comes back is a timeline to read afterwards.
///
/// Consecutive identical readings are collapsed into one line. Thirty seconds of sampling is
/// thirty lines that all say the same thing, and the two or three moments where the answer
/// CHANGED are the entire content of the report.
/// </summary>
public static class DetectionLog
{
    /// <summary>
    /// Writes the readings as a timeline: one line per run of identical readings, then a line
    /// saying how many of them would have held a reminder back.
    /// </summary>
    /// <param name="samples">The readings, in the order they were taken; may be empty.</param>
    /// <returns>The report, ready to show; "No readings taken." when there are none.</returns>
    public static string Render(IReadOnlyList<DetectionSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            return "No readings taken.";
        }

        var report = new StringBuilder();
        var runStart = 0;

        for (var i = 1; i <= samples.Count; i++)
        {
            // A run ends at the last sample, or where the reading changes.
            if (i < samples.Count && SameReading(samples[i], samples[runStart]))
            {
                continue;
            }

            report.AppendLine(Line(samples[runStart], samples[i - 1]));
            runStart = i;
        }

        var heldBack = samples.Count(s => s.Suppression is not ReminderSuppression.None);

        report.AppendLine();
        report.Append(
            heldBack == 0
                ? "A reminder would have been shown at any point in this run."
                : "A reminder would have been held back for "
                  + heldBack
                  + " of the "
                  + samples.Count
                  + " readings.");

        return report.ToString();
    }

    /// <summary>Whether two readings belong on the same line of the timeline.</summary>
    /// <param name="a">One reading.</param>
    /// <param name="b">The other reading.</param>
    /// <returns>
    /// <c>true</c> when the activity and the verdict are both the same; the times are not
    /// compared.
    /// </returns>
    private static bool SameReading(DetectionSample a, DetectionSample b)
        => a.Activity == b.Activity && a.Suppression == b.Suppression;

    /// <summary>
    /// One line of the timeline: the span a run of identical readings covers, what they read,
    /// and the verdict.
    /// </summary>
    /// <param name="first">The first reading of the run.</param>
    /// <param name="last">
    /// The last reading of the run; <paramref name="first"/> again for a run of one.
    /// </param>
    /// <returns>A line such as "0:01 - 0:05   mic off, camera off, Normal   ->  None".</returns>
    private static string Line(DetectionSample first, DetectionSample last)
        => Clock(first.Elapsed)
           + " - "
           + Clock(last.Elapsed)
           + "   "
           + Describe(first.Activity)
           + "   ->  "
           + first.Suppression;

    /// <summary>The middle of a line: the two devices, then the desktop state.</summary>
    /// <param name="activity">The reading to describe.</param>
    /// <returns>
    /// Text such as "mic off, camera off, Normal"; "ON" is padded to the width of "off", so
    /// the two device readings always take the same width. The desktop state is not padded.
    /// </returns>
    private static string Describe(ActivitySnapshot activity)
        => (activity.MicrophoneInUse ? "mic ON " : "mic off")
           + ", "
           + (activity.CameraInUse ? "camera ON " : "camera off")
           + ", "
           + activity.Foreground;

    /// <summary>
    /// Minutes and seconds from the start of the run. Wall-clock times would be worse: what
    /// the reader wants to line up against is what they were doing at the time, and they were
    /// counting from when they started the switch.
    /// </summary>
    /// <param name="elapsed">The time from the start of the run.</param>
    /// <returns>Whole minutes, a colon and two-digit seconds, such as "1:05".</returns>
    private static string Clock(TimeSpan elapsed)
        => ((int)elapsed.TotalMinutes).ToString(CultureInfo.InvariantCulture)
           + ":"
           + elapsed.Seconds.ToString("00", CultureInfo.InvariantCulture);
}
