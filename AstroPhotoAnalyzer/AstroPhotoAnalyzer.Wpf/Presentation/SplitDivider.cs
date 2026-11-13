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

// SplitDivider.cs - the vertical line of a Before / After comparison (R-179, ADR 0090):
// ImageVisualCompare's DividerModel, copied (the suite has no shared library).
//
// The line's place is a fraction of the viewport's width, screen space and not image space, so
// it stays where it was put while the images zoom and pan under it: the line belongs to the
// view, not to the image. Nothing here knows of WPF's elements; BeforeAfterViewer draws what
// this places and asks it what a press grabs, so the two cannot disagree.

using System;
using System.Windows;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The vertical line between the two images of a comparison: where it is, where a pointer
    /// moves it, and what a press grabs (R-179).
    /// </summary>
    public sealed class SplitDivider
    {
        /// <summary>The line's width, in device-independent pixels.</summary>
        public const double LineWidth = 2;

        /// <summary>The width of the handle at mid height, in device-independent pixels.</summary>
        public const double HandleWidth = 26;

        /// <summary>The height of the handle at mid height, in device-independent pixels.</summary>
        public const double HandleHeight = 56;

        /// <summary>How far from the line a press still grabs it, in device-independent pixels.</summary>
        public const double LineGrabTolerance = 8;

        /// <summary>
        /// The distance between the line and each of the two labels, and between a label and the
        /// top of the viewport, in device-independent pixels.
        /// </summary>
        public const double LabelGap = 10;

        double _position = 0.5;

        /// <summary>
        /// The line's place as a fraction of the viewport's width: 0 the left edge, 1 the right
        /// one; a value outside is clamped.
        /// </summary>
        public double Position
        {
            get => _position;
            set => _position = Math.Clamp(value, 0, 1);
        }

        /// <summary>Places the line in a viewport of a given width.</summary>
        /// <param name="viewportWidth">The viewport's width, in device-independent pixels.</param>
        /// <returns>The line's x, in device-independent pixels from the viewport's left edge.</returns>
        public double LineX(double viewportWidth) => _position * viewportWidth;

        /// <summary>Puts the line back in the middle, as for a new image.</summary>
        public void Reset() => _position = 0.5;

        /// <summary>Moves the line under the pointer, as a drag does.</summary>
        /// <param name="x">
        /// The pointer's x, in device-independent pixels from the viewport's left edge; past
        /// either edge the line stops on that edge.
        /// </param>
        /// <param name="viewportWidth">
        /// The viewport's width, in device-independent pixels; at 0 or less the line goes to the
        /// middle.
        /// </param>
        public void MoveTo(double x, double viewportWidth)
            => Position = viewportWidth > 0 ? x / viewportWidth : 0.5;

        /// <summary>Places the handle: centred on the line and on the viewport's height.</summary>
        /// <param name="viewportWidth">The viewport's width, in device-independent pixels.</param>
        /// <param name="viewportHeight">The viewport's height, in device-independent pixels.</param>
        /// <returns>The handle's rectangle, in device-independent pixels from the viewport's top left corner.</returns>
        public Rect Handle(double viewportWidth, double viewportHeight)
            => new Rect(LineX(viewportWidth) - HandleWidth / 2, (viewportHeight - HandleHeight) / 2, HandleWidth, HandleHeight);

        /// <summary>
        /// Whether a press at a point grabs the line instead of the image: anywhere along the
        /// line within <see cref="LineGrabTolerance"/>, or on the handle, which is a wider target.
        /// </summary>
        /// <param name="x">The press, in device-independent pixels from the viewport's left edge.</param>
        /// <param name="y">The press, in device-independent pixels from the viewport's top edge.</param>
        /// <param name="viewportWidth">The viewport's width, in device-independent pixels.</param>
        /// <param name="viewportHeight">The viewport's height, in device-independent pixels.</param>
        /// <returns>True when the press starts a drag of the line; false when it is the image's.</returns>
        public bool HitTest(double x, double y, double viewportWidth, double viewportHeight)
        {
            double fromLine = Math.Abs(x - LineX(viewportWidth));
            if (fromLine <= LineGrabTolerance) return true;
            return fromLine <= HandleWidth / 2 && Math.Abs(y - viewportHeight / 2) <= HandleHeight / 2;
        }

        /// <summary>
        /// Whether a label has room on its side of the line: its own width with
        /// <see cref="LabelGap"/> on both its sides.
        /// </summary>
        /// <param name="sideWidth">The width of the side, from the viewport's edge to the line, in device-independent pixels.</param>
        /// <param name="labelWidth">The label's width, in device-independent pixels.</param>
        /// <returns>True when the label is shown; false when it is hidden.</returns>
        public static bool LabelFits(double sideWidth, double labelWidth) => sideWidth >= labelWidth + 2 * LabelGap;
    }
}
