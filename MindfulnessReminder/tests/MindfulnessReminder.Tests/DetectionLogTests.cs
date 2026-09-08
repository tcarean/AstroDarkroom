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

using MindfulnessReminder.Core;

namespace MindfulnessReminder.Tests;

/// <summary>
/// The timeline --detect reports. It is worth testing because it is the only evidence there
/// is about the two pause settings on a real machine: neither can be exercised without a real
/// call or a real game, so a report that quietly lost the one moment the reading changed
/// would send someone hunting a bug that is not there.
/// </summary>
public class DetectionLogTests
{
    private static DetectionSample At(
        int second,
        bool microphone = false,
        ForegroundState foreground = ForegroundState.Normal,
        ReminderSuppression suppression = ReminderSuppression.None)
        => new(
            TimeSpan.FromSeconds(second),
            new ActivitySnapshot(microphone, false, foreground),
            suppression);

    [Fact]
    public void A_run_with_no_readings_says_so_rather_than_rendering_nothing()
    {
        Assert.Equal("No readings taken.", DetectionLog.Render(Array.Empty<DetectionSample>()));
    }

    /// <summary>
    /// Thirty seconds of an unchanged desktop is thirty identical readings, and thirty lines
    /// saying the same thing would bury the two moments that matter.
    /// </summary>
    [Fact]
    public void Consecutive_identical_readings_collapse_into_one_line()
    {
        var samples = Enumerable.Range(0, 30).Select(i => At(i)).ToList();

        var lines = Lines(DetectionLog.Render(samples));

        Assert.Single(lines);
        Assert.StartsWith("0:00 - 0:29", lines[0]);
    }

    /// <summary>The whole point of watching over time: catching the moment it changed.</summary>
    [Fact]
    public void A_change_in_the_reading_starts_a_new_line()
    {
        var samples = new List<DetectionSample>
        {
            At(0),
            At(1),
            At(2, foreground: ForegroundState.FullScreen, suppression: ReminderSuppression.FullScreen),
            At(3, foreground: ForegroundState.FullScreen, suppression: ReminderSuppression.FullScreen),
            At(4),
        };

        var lines = Lines(DetectionLog.Render(samples));

        Assert.Equal(3, lines.Count);
        Assert.StartsWith("0:00 - 0:01", lines[0]);
        Assert.StartsWith("0:02 - 0:03", lines[1]);
        Assert.StartsWith("0:04 - 0:04", lines[2]);

        Assert.Contains("FullScreen", lines[1]);
        Assert.EndsWith("None", lines[0]);
    }

    /// <summary>
    /// A reading that goes away and comes back is two separate runs, not one merged with the
    /// gap swallowed -- a game alt-tabbed out of and back into has to read as two.
    /// </summary>
    [Fact]
    public void A_reading_that_returns_is_a_second_run_not_a_merged_one()
    {
        var busy = ReminderSuppression.FullScreen;

        var samples = new List<DetectionSample>
        {
            At(0, foreground: ForegroundState.FullScreen, suppression: busy),
            At(1),
            At(2, foreground: ForegroundState.FullScreen, suppression: busy),
        };

        Assert.Equal(3, Lines(DetectionLog.Render(samples)).Count);
    }

    [Fact]
    public void Each_line_says_what_the_microphone_the_camera_and_the_desktop_were_doing()
    {
        var line = Lines(DetectionLog.Render(new[]
        {
            At(0, microphone: true, suppression: ReminderSuppression.MicrophoneInUse),
        }))[0];

        Assert.Contains("mic ON", line);
        Assert.Contains("camera off", line);
        Assert.Contains("Normal", line);
        Assert.EndsWith("MicrophoneInUse", line);
    }

    [Fact]
    public void The_summary_counts_the_readings_that_would_have_held_a_reminder_back()
    {
        var samples = new List<DetectionSample>
        {
            At(0),
            At(1, microphone: true, suppression: ReminderSuppression.MicrophoneInUse),
            At(2, microphone: true, suppression: ReminderSuppression.MicrophoneInUse),
        };

        Assert.Contains("held back for 2 of the 3 readings", DetectionLog.Render(samples));
    }

    /// <summary>
    /// The line that says the run proved nothing. Without it, a report of one bland line is
    /// ambiguous between "the check is broken" and "there was nothing to detect".
    /// </summary>
    [Fact]
    public void A_run_that_never_held_anything_back_says_so_in_as_many_words()
    {
        var report = DetectionLog.Render(new[] { At(0), At(1) });

        Assert.Contains("would have been shown at any point", report);
        Assert.DoesNotContain("held back", report);
    }

    [Fact]
    public void Runs_longer_than_a_minute_are_still_readable()
    {
        var samples = new List<DetectionSample> { At(0), At(95) };

        Assert.Contains("1:35", DetectionLog.Render(samples));
    }

    private static List<string> Lines(string report)
        => report
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0 && l.Contains("->"))
            .ToList();
}
