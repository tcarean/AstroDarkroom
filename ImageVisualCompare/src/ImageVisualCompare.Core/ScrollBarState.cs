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

/// <summary>
/// What one floating scrollbar shows, ready to assign: its range, the thumb, its position and
/// whether it is there at all. The numbers are the viewport's offsets, in image pixels.
/// </summary>
/// <param name="Maximum">Highest offset the bar can scroll to; the minimum is always zero.</param>
/// <param name="ViewportSize">Visible extent, which is what sets the thumb's proportion.</param>
/// <param name="Value">Current offset, clamped into range.</param>
/// <param name="IsVisible">False when the image fits and there is nothing to scroll.</param>
public readonly record struct ScrollBarState(
    double Maximum,
    double ViewportSize,
    double Value,
    bool IsVisible);

/// <summary>
/// Reads the state of the horizontal and the vertical scrollbar off the viewport.
/// </summary>
public static class ScrollBarStates
{
    /// <summary>Reads the horizontal scrollbar's state off the viewport.</summary>
    /// <param name="viewport">
    /// The view whose horizontal offset and visible width the bar shows.
    /// </param>
    /// <returns>
    /// The range 0 to <see cref="Viewport.MaxOffsetX"/>, the visible width as the thumb, the
    /// offset clamped into that range, and visible only while the image is wider than the view.
    /// </returns>
    public static ScrollBarState Horizontal(Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        return new ScrollBarState(
            viewport.MaxOffsetX,
            viewport.VisibleContentWidth,
            Math.Clamp(viewport.OffsetX, 0, viewport.MaxOffsetX),
            viewport.CanScrollHorizontally);
    }

    /// <summary>Reads the vertical scrollbar's state off the viewport.</summary>
    /// <param name="viewport">
    /// The view whose vertical offset and visible height the bar shows.
    /// </param>
    /// <returns>
    /// The range 0 to <see cref="Viewport.MaxOffsetY"/>, the visible height as the thumb, the
    /// offset clamped into that range, and visible only while the image is taller than the view.
    /// </returns>
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
