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

public class TrayTooltipTests
{
    [Fact]
    public void Without_a_countdown_the_tooltip_is_just_the_banner()
    {
        Assert.Equal(VersionInfo.Banner, TrayTooltip.Format(VersionInfo.Banner, null));
    }

    [Fact]
    public void With_a_countdown_the_tooltip_says_how_many_minutes_are_left()
    {
        var text = TrayTooltip.Format(VersionInfo.Banner, TimeSpan.FromMinutes(12));

        Assert.Equal(VersionInfo.Banner + " - next in 12 min", text);
    }

    /// <summary>
    /// Rounded up, so the tooltip reads "30 min" the instant a 30-minute countdown restarts.
    /// Rounding down would show 29 for the first 59 seconds of every interval.
    /// </summary>
    [Fact]
    public void Part_minutes_round_up()
    {
        Assert.Contains("30 min", TrayTooltip.Format("x", TimeSpan.FromSeconds(1799)));
        Assert.Contains("29 min", TrayTooltip.Format("x", TimeSpan.FromSeconds(1740)));
    }

    [Fact]
    public void The_last_minute_reads_as_one_rather_than_zero()
    {
        Assert.Contains("1 min", TrayTooltip.Format("x", TimeSpan.FromSeconds(4)));
        Assert.Contains("1 min", TrayTooltip.Format("x", TimeSpan.Zero));
    }

    /// <summary>
    /// The reason this is a testable function at all: NotifyIcon.Text throws above 63
    /// characters, so the longest string the app can produce has to be shown to fit.
    /// </summary>
    [Fact]
    public void The_longest_tooltip_the_app_can_produce_fits_the_tray_limit()
    {
        var longest = TrayTooltip.Format(
            VersionInfo.Banner,
            TimeSpan.FromMinutes(ReminderSettings.MaxIntervalMinutes));

        Assert.True(
            longest.Length <= TrayTooltip.MaxLength,
            $"tooltip is {longest.Length} characters: {longest}");
    }

    [Fact]
    public void An_over_long_banner_is_truncated_rather_than_allowed_to_throw()
    {
        var text = TrayTooltip.Format(new string('x', 200), TimeSpan.FromMinutes(5));

        Assert.Equal(TrayTooltip.MaxLength, text.Length);
    }
}
