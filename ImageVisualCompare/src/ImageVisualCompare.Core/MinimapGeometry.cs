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
/// Where the minimap draws the visible region on its thumbnail, and whether it does.
/// </summary>
/// <param name="ViewportRect">The visible region, in thumbnail coordinates.</param>
/// <param name="ShowViewportRect">False when the whole image is on screen and the rectangle would say nothing.</param>
public readonly record struct MinimapLayout(RectD ViewportRect, bool ShowViewportRect);

/// <summary>
/// Maps between the minimap's thumbnail and the image: where the visible region is drawn on
/// the thumbnail, and which image point a click on it means.
/// </summary>
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
    /// <param name="viewport">The view whose visible region is drawn.</param>
    /// <param name="thumbnailWidth">
    /// The width of the area the whole image is mapped onto, in the unit the rectangle is
    /// wanted in (the minimap passes its own width, in device-independent pixels).
    /// </param>
    /// <param name="thumbnailHeight">The height of that area, in the same unit.</param>
    /// <returns>
    /// The rectangle, clamped to the area and then widened to at least 2 units on each side,
    /// so at deep zoom by the right or bottom edge it can reach up to 2 units past the area;
    /// shown only while part of the image is off screen. The default, hidden, when the viewport
    /// has no content or the area no size.
    /// </returns>
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

    /// <summary>
    /// Maps a position on the thumbnail to the image point it stands for, for a click or a drag
    /// that recentres the view.
    /// </summary>
    /// <param name="viewport">The view, whose content size the thumbnail is scaled from.</param>
    /// <param name="thumbnailX">
    /// The position, from the left edge of the area the image is mapped onto.
    /// </param>
    /// <param name="thumbnailY">The position, from the top edge of that area.</param>
    /// <param name="thumbnailWidth">The width of that area, in the unit of the position.</param>
    /// <param name="thumbnailHeight">The height of that area, in the same unit.</param>
    /// <returns>
    /// The content point a click at this thumbnail position is pointing at, in image pixels.
    /// It is not clamped: a drag past the thumbnail's edge gives a point off the image, which
    /// <see cref="Viewport.CentreOn"/> clamps. The origin when the thumbnail has no size.
    /// </returns>
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
