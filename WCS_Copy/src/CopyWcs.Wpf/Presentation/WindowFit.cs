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

// Copied from AstroDarkroom's core (its CR-005 fix); a fix goes there first (ADR 0007). It
// places a window and says nothing about a copy, so here it lives beside the window.

namespace CopyWcs.Wpf;

/// <summary>
/// Where a window goes so that it stays on its screen (R-055). Plain numbers, in the
/// device-independent pixels the window measures in.
/// </summary>
internal static class WindowFit
{
    /// <summary>
    /// The top of a window of the given height kept inside a work area: its own top when it fits
    /// there, moved down to the area's top or up until its bottom is the area's when it runs past
    /// one edge, and the area's top when it is as tall as the area or taller, so its title bar
    /// stays on the screen.
    /// </summary>
    /// <param name="top">Where the window's top is before the fit.</param>
    /// <param name="height">The window's height, title bar included; not negative.</param>
    /// <param name="areaTop">The work area's top.</param>
    /// <param name="areaHeight">The work area's height; not negative.</param>
    /// <returns>The window's top inside the area.</returns>
    internal static double Top(double top, double height, double areaTop, double areaHeight)
        => height >= areaHeight ? areaTop : Math.Clamp(top, areaTop, areaTop + areaHeight - height);
}
