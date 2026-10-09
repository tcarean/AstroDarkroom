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

public class WindowPlacementTests
{
    [Fact]
    public void R070_Centers_the_window_in_the_work_area()
    {
        var (left, top) = WindowPlacement.CenterIn(new WorkArea(0, 0, 1920, 1080), 440, 260);

        Assert.Equal(740, left);
        Assert.Equal(410, top);
    }

    [Fact]
    public void R070_Centers_on_a_secondary_monitor_using_its_virtual_desktop_offset()
    {
        var (left, top) = WindowPlacement.CenterIn(new WorkArea(1920, 0, 1920, 1040), 440, 260);

        Assert.Equal(2660, left);
        Assert.Equal(390, top);
    }

    [Fact]
    public void R070_Keeps_the_window_on_screen_when_it_is_wider_than_the_work_area()
    {
        var (left, top) = WindowPlacement.CenterIn(new WorkArea(100, 50, 300, 200), 440, 260);

        Assert.Equal(100, left);
        Assert.Equal(50, top);
    }
}
