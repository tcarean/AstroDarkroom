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

/// <param name="ViewportRect">The visible region, in thumbnail coordinates.</param>
/// <param name="ShowViewportRect">False when the whole image is on screen and the rectangle would say nothing.</param>
public readonly record struct MinimapLayout(RectD ViewportRect, bool ShowViewportRect);

public static class MinimapGeometry
{
    /// <summary>Below this the rectangle is a dot nobody can see; at deep zoom it would be.</summary>
    private const double MinimumRectSize = 2;

    /// <summary>
    /// Places the viewport rectangle on the thumbnail.
    ///
    /// The rectangle is clamped to the thumbnail rather than drawn where the maths puts it,
    /// because the viewport legitimately extends past the image whenever the image is smaller
    /// than the window — at fit, on the axis with spare room, the offset is negative.
    /// </summary>
    public static MinimapLayout Layout(Viewport viewport, double thumbnailWidth, double thumbnailHeight)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        if (!viewport.HasContent || thumbnailWidth <= 0 || thumbnailHeight <= 0)
        {
            return default;
        }

        double scaleX = thumbnailWidth / viewport.ContentWidth;
        double scaleY = thumbnailHeight / viewport.ContentHeight;

        double left = Math.Max(0, viewport.OffsetX * scaleX);
        double top = Math.Max(0, viewport.OffsetY * scaleY);
        double right = Math.Min(thumbnailWidth, (viewport.OffsetX + viewport.VisibleContentWidth) * scaleX);
        double bottom = Math.Min(thumbnailHeight, (viewport.OffsetY + viewport.VisibleContentHeight) * scaleY);

        return new MinimapLayout(
            new RectD(
                left,
                top,
                Math.Max(MinimumRectSize, right - left),
                Math.Max(MinimumRectSize, bottom - top)),
            viewport.CanScrollHorizontally || viewport.CanScrollVertically);
    }

    /// <summary>The content point a click at this thumbnail position is pointing at.</summary>
    public static PointD ContentPointAt(
        Viewport viewport,
        double thumbnailX,
        double thumbnailY,
        double thumbnailWidth,
        double thumbnailHeight)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        if (thumbnailWidth <= 0 || thumbnailHeight <= 0)
        {
            return default;
        }

        return new PointD(
            thumbnailX / thumbnailWidth * viewport.ContentWidth,
            thumbnailY / thumbnailHeight * viewport.ContentHeight);
    }
}
