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
/// The vertical split line between the two images.
///
/// Its position is stored as a fraction of the viewport width — screen space, not content
/// space — so it stays where the user put it while they zoom and pan underneath. That is
/// the behaviour of a before/after slider: the line belongs to the window, not to the image.
/// </summary>
public sealed class DividerModel
{
    /// <summary>Width of the centred drag handle, in device-independent pixels.</summary>
    public const double HandleWidth = 26;

    /// <summary>Height of the centred drag handle, in device-independent pixels.</summary>
    public const double HandleHeight = 56;

    /// <summary>How far from the line a press still counts as grabbing it.</summary>
    public const double LineGrabTolerance = 8;

    private double _position = 0.5;

    /// <summary>Position as a fraction of the viewport width, 0 at the left edge and 1 at the right.</summary>
    public double Position
    {
        get => _position;
        set => _position = Math.Clamp(value, 0, 1);
    }

    /// <summary>Places the line on a surface of this width.</summary>
    /// <param name="viewportWidth">The surface's width, in device-independent pixels.</param>
    /// <returns>The line's x, in device-independent pixels from the surface's left edge.</returns>
    public double ScreenX(double viewportWidth) => _position * viewportWidth;

    /// <summary>Places the handle's centre on a surface of this height: half way down.</summary>
    /// <param name="viewportHeight">The surface's height, in device-independent pixels.</param>
    /// <returns>
    /// The handle's centre, in device-independent pixels from the surface's top edge.
    /// </returns>
    public double HandleCentreY(double viewportHeight) => viewportHeight / 2;

    /// <summary>Puts the line back in the middle, as for a newly opened pair.</summary>
    public void Reset() => _position = 0.5;

    /// <summary>Moves the line to a screen position, used while dragging.</summary>
    /// <param name="screenX">
    /// Where the pointer is, in device-independent pixels from the surface's left edge; a
    /// position past either edge puts the line on that edge.
    /// </param>
    /// <param name="viewportWidth">
    /// The surface's width, in device-independent pixels; at 0 or below the line goes to the
    /// middle.
    /// </param>
    public void MoveTo(double screenX, double viewportWidth)
    {
        Position = viewportWidth > 0 ? screenX / viewportWidth : 0.5;
    }

    /// <summary>Shifts the line by a screen-space amount, used by the arrow keys.</summary>
    /// <param name="screenDelta">
    /// How far to move, in device-independent pixels; negative moves left. The line stops at
    /// the edges.
    /// </param>
    /// <param name="viewportWidth">
    /// The surface's width, in device-independent pixels; at 0 or below nothing moves.
    /// </param>
    public void NudgeBy(double screenDelta, double viewportWidth)
    {
        if (viewportWidth > 0)
        {
            Position = _position + screenDelta / viewportWidth;
        }
    }

    /// <summary>
    /// Places the line, the clip region for image A, and the handle, for a viewport of this
    /// size.
    /// </summary>
    /// <param name="viewportWidth">The surface's width, in device-independent pixels.</param>
    /// <param name="viewportHeight">The surface's height, in device-independent pixels.</param>
    /// <returns>
    /// The three, in device-independent pixels from the surface's top-left corner.
    /// </returns>
    public DividerLayout Layout(double viewportWidth, double viewportHeight)
    {
        double x = ScreenX(viewportWidth);

        return new DividerLayout(
            LineX: x,
            ClipRect: new RectD(0, 0, x, viewportHeight),
            Handle: new RectD(
                x - (HandleWidth / 2),
                (viewportHeight - HandleHeight) / 2,
                HandleWidth,
                HandleHeight));
    }

    /// <summary>
    /// Decides whether a press at this point grabs the divider rather than starting a pan.
    /// The line can be grabbed anywhere along its length within <see cref="LineGrabTolerance"/>,
    /// and the handle offers a wider target for anyone aiming at the visible grip.
    /// </summary>
    /// <param name="screenX">
    /// The press, in device-independent pixels from the surface's left edge.
    /// </param>
    /// <param name="screenY">
    /// The press, in device-independent pixels from the surface's top edge.
    /// </param>
    /// <param name="viewportWidth">The surface's width, in device-independent pixels.</param>
    /// <param name="viewportHeight">The surface's height, in device-independent pixels.</param>
    /// <returns>
    /// True when a press at this point should start a divider drag rather than a pan.
    /// </returns>
    public bool HitTest(double screenX, double screenY, double viewportWidth, double viewportHeight)
    {
        double distanceFromLine = Math.Abs(screenX - ScreenX(viewportWidth));

        if (distanceFromLine <= LineGrabTolerance)
        {
            return true;
        }

        return distanceFromLine <= HandleWidth / 2
               && Math.Abs(screenY - HandleCentreY(viewportHeight)) <= HandleHeight / 2;
    }
}
