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
/// The single source of truth for zoom and pan across the image pair. Both images are
/// drawn through this one transform, so they cannot drift apart: the synchronisation the
/// comparison depends on is structural, not something the UI has to keep up to date.
///
/// Screen space is measured in physical screen pixels from the top-left corner of the
/// viewport; the view converts from WPF's device-independent pixels before it calls in, which
/// is what lets a scale of 1 mean one image pixel per screen pixel on a scaled display.
/// Content space is measured in image pixels.
/// </summary>
public sealed class Viewport
{
    /// <summary>Beyond roughly this point a pixel is a large flat square and more zoom buys nothing.</summary>
    public const double MaxScale = 16.0;

    private double _scale = 1.0;

    public double ContentWidth { get; private set; }

    public double ContentHeight { get; private set; }

    public double ViewportWidth { get; private set; }

    public double ViewportHeight { get; private set; }

    /// <summary>Content-space coordinate drawn at the left edge of the viewport.</summary>
    public double OffsetX { get; private set; }

    /// <summary>Content-space coordinate drawn at the top edge of the viewport.</summary>
    public double OffsetY { get; private set; }

    public double Scale => _scale;

    public bool HasContent =>
        ContentWidth > 0 && ContentHeight > 0 && ViewportWidth > 0 && ViewportHeight > 0;

    /// <summary>The scale at which the whole image is visible.</summary>
    public double FitScale =>
        HasContent ? Math.Min(ViewportWidth / ContentWidth, ViewportHeight / ContentHeight) : 1.0;

    /// <summary>
    /// Zooming out past <see cref="FitScale"/> only shrinks an already fully visible image,
    /// so that is the floor. An image smaller than the viewport is the exception: there the
    /// floor is 100%, otherwise the user could never see it at its own resolution.
    /// </summary>
    public double MinScale => Math.Min(FitScale, 1.0);

    public double VisibleContentWidth => _scale > 0 ? ViewportWidth / _scale : 0;

    public double VisibleContentHeight => _scale > 0 ? ViewportHeight / _scale : 0;

    public double MaxOffsetX => Math.Max(0, ContentWidth - VisibleContentWidth);

    public double MaxOffsetY => Math.Max(0, ContentHeight - VisibleContentHeight);

    /// <summary>True when the image overflows the viewport horizontally and a scrollbar is useful.</summary>
    public bool CanScrollHorizontally => MaxOffsetX > 0;

    public bool CanScrollVertically => MaxOffsetY > 0;

    /// <summary>
    /// Sets the size of the loaded image pair. The caller normally follows with
    /// <see cref="ZoomToFit"/>.
    /// </summary>
    /// <param name="width">
    /// The canvas width, in image pixels, 0 when nothing is shown; not negative.
    /// </param>
    /// <param name="height">
    /// The canvas height, in image pixels, 0 when nothing is shown; not negative.
    /// </param>
    public void SetContentSize(double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        ContentWidth = width;
        ContentHeight = height;
        ClampScale();
        ClampOffsets();
    }

    /// <summary>
    /// Tracks a window resize. The content point at the centre of the viewport is held
    /// stationary, so growing or shrinking the window does not appear to pan the image.
    /// </summary>
    /// <param name="width">The viewport's width, in physical screen pixels; not negative.</param>
    /// <param name="height">The viewport's height, in physical screen pixels; not negative.</param>
    public void SetViewportSize(double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        bool hadViewport = ViewportWidth > 0 && ViewportHeight > 0;
        PointD centre = hadViewport
            ? ScreenToContent(ViewportWidth / 2, ViewportHeight / 2)
            : default;

        ViewportWidth = width;
        ViewportHeight = height;
        ClampScale();

        if (hadViewport && HasContent)
        {
            OffsetX = centre.X - VisibleContentWidth / 2;
            OffsetY = centre.Y - VisibleContentHeight / 2;
        }

        ClampOffsets();
    }

    /// <summary>Converts a viewport position to the image point drawn there.</summary>
    /// <param name="screenX">The position, in screen space (physical pixels).</param>
    /// <param name="screenY">The position, in screen space (physical pixels).</param>
    /// <returns>The point, in image pixels; it may lie off the image.</returns>
    public PointD ScreenToContent(double screenX, double screenY) =>
        new(screenX / _scale + OffsetX, screenY / _scale + OffsetY);

    /// <summary>Converts an image point to the viewport position it is drawn at.</summary>
    /// <param name="contentX">The point, in image pixels from the image's left edge.</param>
    /// <param name="contentY">The point, in image pixels from the image's top edge.</param>
    /// <returns>
    /// The position, in physical pixels from the viewport's top-left corner; it may lie outside
    /// the viewport.
    /// </returns>
    public PointD ContentToScreen(double contentX, double contentY) =>
        new((contentX - OffsetX) * _scale, (contentY - OffsetY) * _scale);

    /// <summary>Finds the image pixel under a viewport position.</summary>
    /// <param name="screenX">The position, in screen space (physical pixels).</param>
    /// <param name="screenY">The position, in screen space (physical pixels).</param>
    /// <returns>
    /// The point, in image pixels with their fraction; the far edge of the image belongs to no
    /// pixel. Null when the position falls on the empty margin around the image rather than on
    /// the image itself, or when there is no content.
    /// </returns>
    public PointD? ContentPointInsideAt(double screenX, double screenY)
    {
        if (!HasContent)
        {
            return null;
        }

        PointD content = ScreenToContent(screenX, screenY);

        bool inside = content.X >= 0 && content.Y >= 0
                      && content.X < ContentWidth && content.Y < ContentHeight;

        return inside ? content : null;
    }

    /// <summary>
    /// Zooms to <paramref name="scale"/> while holding the content point currently under the
    /// anchor fixed on screen. Anchoring at the mouse is what makes wheel zoom feel like the
    /// image is being pulled towards the cursor rather than towards its own centre. Does
    /// nothing while there is no content; the offsets are clamped afterwards.
    /// </summary>
    /// <param name="scale">
    /// The new scale, 1 being one image pixel per physical screen pixel; clamped to
    /// <see cref="MinScale"/> and <see cref="MaxScale"/>.
    /// </param>
    /// <param name="anchorScreenX">The anchor, in screen space (physical pixels).</param>
    /// <param name="anchorScreenY">The anchor, in screen space (physical pixels).</param>
    public void ZoomTo(double scale, double anchorScreenX, double anchorScreenY)
    {
        if (!HasContent)
        {
            return;
        }

        PointD anchor = ScreenToContent(anchorScreenX, anchorScreenY);
        _scale = Math.Clamp(scale, MinScale, MaxScale);
        OffsetX = anchor.X - anchorScreenX / _scale;
        OffsetY = anchor.Y - anchorScreenY / _scale;
        ClampOffsets();
    }

    /// <summary>
    /// Multiplies the scale by <paramref name="factor"/>, holding the anchor still as
    /// <see cref="ZoomTo"/> does.
    /// </summary>
    /// <param name="factor">
    /// The new scale over the current one: above 1 zooms in, below 1 out.
    /// </param>
    /// <param name="anchorScreenX">The anchor, in screen space (physical pixels).</param>
    /// <param name="anchorScreenY">The anchor, in screen space (physical pixels).</param>
    public void ZoomBy(double factor, double anchorScreenX, double anchorScreenY) =>
        ZoomTo(_scale * factor, anchorScreenX, anchorScreenY);

    /// <summary>
    /// Steps up to the next stop on <see cref="ZoomLadder"/>, holding the anchor still as
    /// <see cref="ZoomTo"/> does.
    /// </summary>
    /// <param name="anchorScreenX">The anchor, in screen space (physical pixels).</param>
    /// <param name="anchorScreenY">The anchor, in screen space (physical pixels).</param>
    public void ZoomIn(double anchorScreenX, double anchorScreenY) =>
        ZoomTo(ZoomLadder.Next(_scale), anchorScreenX, anchorScreenY);

    /// <summary>
    /// Steps down to the previous stop on <see cref="ZoomLadder"/>, holding the anchor still as
    /// <see cref="ZoomTo"/> does.
    /// </summary>
    /// <param name="anchorScreenX">The anchor, in screen space (physical pixels).</param>
    /// <param name="anchorScreenY">The anchor, in screen space (physical pixels).</param>
    public void ZoomOut(double anchorScreenX, double anchorScreenY) =>
        ZoomTo(ZoomLadder.Previous(_scale), anchorScreenX, anchorScreenY);

    /// <summary>Zooms so the whole image is visible and centres it.</summary>
    public void ZoomToFit()
    {
        if (!HasContent)
        {
            return;
        }

        _scale = Math.Clamp(FitScale, MinScale, MaxScale);
        ClampOffsets();
    }

    /// <summary>Zooms to one screen pixel per image pixel, anchored at the viewport centre.</summary>
    public void ZoomToActualSize() => ZoomTo(1.0, ViewportWidth / 2, ViewportHeight / 2);

    /// <summary>
    /// Pans by a screen-space drag delta. Dragging right moves the image right, which means
    /// the content offset moves left — hence the subtraction. The image stops at its edges;
    /// nothing moves while there is no content.
    /// </summary>
    /// <param name="screenDeltaX">
    /// How far the pointer moved right, in physical pixels; negative for left.
    /// </param>
    /// <param name="screenDeltaY">
    /// How far the pointer moved down, in physical pixels; negative for up.
    /// </param>
    public void PanBy(double screenDeltaX, double screenDeltaY)
    {
        if (!HasContent)
        {
            return;
        }

        OffsetX -= screenDeltaX / _scale;
        OffsetY -= screenDeltaY / _scale;
        ClampOffsets();
    }

    /// <summary>Sets the offsets directly, for scrollbar and minimap driven panning.</summary>
    /// <param name="offsetX">
    /// The image x to draw at the viewport's left edge, in image pixels; clamped like every
    /// other movement.
    /// </param>
    /// <param name="offsetY">
    /// The image y to draw at the viewport's top edge, in image pixels; clamped too.
    /// </param>
    public void SetOffset(double offsetX, double offsetY)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        ClampOffsets();
    }

    /// <summary>
    /// Centres the viewport on a content point, used when the minimap is clicked. Near an edge
    /// the offsets are clamped, so the point ends up off centre.
    /// </summary>
    /// <param name="contentX">The point's x, in image pixels.</param>
    /// <param name="contentY">The point's y, in image pixels.</param>
    public void CentreOn(double contentX, double contentY) =>
        SetOffset(contentX - VisibleContentWidth / 2, contentY - VisibleContentHeight / 2);

    /// <summary>
    /// Brings the scale back between <see cref="MinScale"/> and <see cref="MaxScale"/> after a
    /// size changed; leaves it alone while there is no content.
    /// </summary>
    private void ClampScale()
    {
        if (HasContent)
        {
            _scale = Math.Clamp(_scale, MinScale, MaxScale);
        }
    }

    /// <summary>
    /// Keeps the offsets inside the image on each axis: an axis on which the whole image fits
    /// is centred, with equal margins either side; on any other no edge of the image comes
    /// inside the view.
    /// </summary>
    private void ClampOffsets()
    {
        OffsetX = ClampAxis(OffsetX, ContentWidth, VisibleContentWidth);
        OffsetY = ClampAxis(OffsetY, ContentHeight, VisibleContentHeight);

        static double ClampAxis(double offset, double content, double visible)
        {
            // When the axis fits entirely, centre it. The resulting offset is negative,
            // which leaves equal empty margins on both sides.
            if (visible >= content)
            {
                return (content - visible) / 2;
            }

            return Math.Clamp(offset, 0, content - visible);
        }
    }
}
