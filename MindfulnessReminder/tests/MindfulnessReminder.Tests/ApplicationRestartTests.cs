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

public class ApplicationRestartTests
{
    /// <summary>
    /// The two values handed to RegisterApplicationRestart. The command line is what the
    /// restarted copy sees (R-102); the flag keeps Windows from starting a second copy after a
    /// reboot, where the Run entry already does it (R-003).
    /// </summary>
    [Fact]
    public void R100_The_registration_passes_the_restarted_switch_and_asks_for_no_restart_after_a_reboot()
    {
        Assert.Equal("--restarted", ApplicationRestart.CommandLine);
        Assert.Equal(8, ApplicationRestart.Flags);
    }

    /// <summary>
    /// <c>--preview=x</c> is not a preview to App, which reads the message from the next
    /// argument, so that start is a tray copy and registers like one.
    /// </summary>
    [Fact]
    public void R100_A_plain_start_and_a_restarted_start_both_register()
    {
        Assert.True(ApplicationRestart.ShouldRegister(Array.Empty<string>()));
        Assert.True(ApplicationRestart.ShouldRegister(new[] { "--restarted" }));
        Assert.True(ApplicationRestart.ShouldRegister(new[] { "--preview=x" }));
    }

    /// <summary>
    /// Neither diagnostic switch lives in the tray or claims the mutex (R-002), so a restart of
    /// one would put a second, unwanted window up; the switch is matched in any case, as the
    /// switches themselves are.
    /// </summary>
    [Theory]
    [InlineData("--preview")]
    [InlineData("--PREVIEW", "Take a slow breath")]
    [InlineData("--detect")]
    [InlineData("--detect=30")]
    [InlineData("--Detect", "30")]
    [InlineData("--restarted", "--preview")]
    public void R100_Preview_and_detect_do_not_register_in_any_case(params string[] args)
    {
        Assert.False(ApplicationRestart.ShouldRegister(args));
    }

    [Theory]
    [InlineData(true, "--restarted")]
    [InlineData(true, "--RESTARTED")]
    [InlineData(true, "--restarted", "extra")]
    [InlineData(false)]
    [InlineData(false, "--preview")]
    [InlineData(false, "--restarted=1")]
    public void R102_A_start_is_a_restart_only_with_the_restarted_switch(bool expected, params string[] args)
    {
        Assert.Equal(expected, ApplicationRestart.IsRestart(args));
    }

    [Fact]
    public void R102_Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicationRestart.IsRestart(null!));
        Assert.Throws<ArgumentNullException>(() => ApplicationRestart.ShouldRegister(null!));
    }
}
