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

namespace ImageVisualCompare.Core;

/// <param name="Maximum">Highest offset the bar can scroll to; the minimum is always zero.</param>
/// <param name="ViewportSize">Visible extent, which is what sets the thumb's proportion.</param>
/// <param name="Value">Current offset, clamped into range.</param>
/// <param name="IsVisible">False when the image fits and there is nothing to scroll.</param>
public readonly record struct ScrollBarState(
    double Maximum,
    double ViewportSize,
    double Value,
    bool IsVisible);

public static class ScrollBarStates
{
    public static ScrollBarState Horizontal(Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        return new ScrollBarState(
            viewport.MaxOffsetX,
            viewport.VisibleContentWidth,
            Math.Clamp(viewport.OffsetX, 0, viewport.MaxOffsetX),
            viewport.CanScrollHorizontally);
    }

    public static ScrollBarState Vertical(Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        return new ScrollBarState(
            viewport.MaxOffsetY,
            viewport.VisibleContentHeight,
            Math.Clamp(viewport.OffsetY, 0, viewport.MaxOffsetY),
            viewport.CanScrollVertically);
    }
}
