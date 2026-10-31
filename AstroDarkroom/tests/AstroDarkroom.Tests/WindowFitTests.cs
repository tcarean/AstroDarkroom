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

using AstroDarkroom.Core;

namespace AstroDarkroom.Tests;

/// <summary>
/// R-016: where "Despre" goes so that it stays on its screen. The window caps its height at the
/// work area's and asks the core where its top goes; these are the core's answers.
/// </summary>
public class WindowFitTests
{
    [Theory]
    [InlineData(100.0, 600.0, 0.0, 800.0, 100.0)]   // it fits where it opens: unchanged
    [InlineData(300.0, 600.0, 0.0, 800.0, 200.0)]   // past the bottom: up until its bottom is the area's (R-016's example)
    [InlineData(-60.0, 600.0, 0.0, 800.0, 0.0)]     // past the top: down to the area's top
    [InlineData(-60.0, 946.0, 0.0, 800.0, 0.0)]     // taller than the area: at its top (the window is cut to the area's height)
    [InlineData(-60.0, 800.0, 0.0, 800.0, 0.0)]     // exactly as tall as the area: at its top
    [InlineData(500.0, 600.0, 40.0, 800.0, 240.0)]  // an area that does not start at 0, a taskbar above it
    [InlineData(100.0, 600.0, -1080.0, 1040.0, -640.0)]   // a screen above the primary, at negative coordinates
    public void R016_TheWindowsTopKeepsItInsideTheWorkArea(double top, double height, double areaTop, double areaHeight, double expected)
    {
        Assert.Equal(expected, WindowFit.Top(top, height, areaTop, areaHeight));
    }
}
