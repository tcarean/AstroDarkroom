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

    /// <summary>
    /// How far past the edge between two image pixels, in image pixels, the centre of a screen
    /// pixel may be and the screen pixel still be painted from the pixel before the edge.
    /// Measured on screen with nearest-neighbour drawing at 400%, 800% and 1600% (ADR 0052): a
    /// centre exactly on an edge, or 1/16384 past it, is painted from the pixel before on nearly
    /// every screen pixel (435 of 438 on the edge); 1/4096 past, on some and not on others; and
    /// 1/1024 past, from the pixel after, on every one near the image's origin and on half of
    /// them near column 3800. The value sits inside that band. It also keeps a centre that
    /// rounding left an ulp to either side of an edge on one side of it.
    /// </summary>
    public const double EdgeReach = 1.0 / 4096;

    /// <summary>
    /// How near a whole number a scale, or the half of a physical pixel an offset is rounded at,
    /// has to be to count as it: the arithmetic of a zoom leaves both a last bit away.
    /// </summary>
    public const double WholeTolerance = 1e-9;

    private double _scale = 1.0;

    public double ContentWidth { get; private set; }

    public double ContentHeight { get; private set; }

    public double ViewportWidth { get; private set; }

    public double ViewportHeight { get; private set; }

    /// <summary>
    /// The pan: the content-space coordinate at the left edge of the viewport, with its
    /// fraction. The image is drawn with <see cref="DrawnOffsetX"/>.
    /// </summary>
    public double OffsetX { get; private set; }

    /// <summary>The pan's content-space coordinate at the top edge of the viewport.</summary>
    public double OffsetY { get; private set; }

    /// <summary>
    /// The content-space coordinate the image is drawn with at the left edge of the viewport:
    /// <see cref="OffsetX"/>, but at a whole scale the nearest one that puts the image on whole
    /// physical pixels (R-022).
    /// </summary>
    public double DrawnOffsetX => Drawn(OffsetX, ContentWidth, ViewportWidth);

    /// <summary>The same for the top edge: <see cref="OffsetY"/> as it is drawn.</summary>
    public double DrawnOffsetY => Drawn(OffsetY, ContentHeight, ViewportHeight);

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

    /// <summary>
    /// Converts a viewport position to the image point the pan puts there. At a whole scale the
    /// image is drawn up to half a physical pixel from it (<see cref="DrawnOffsetX"/>); the
    /// pixel drawn at a position is <see cref="PixelAt"/>'s to say.
    /// </summary>
    /// <param name="screenX">The position, in screen space (physical pixels).</param>
    /// <param name="screenY">The position, in screen space (physical pixels).</param>
    /// <returns>The point, in image pixels; it may lie off the image.</returns>
    public PointD ScreenToContent(double screenX, double screenY) =>
        new(screenX / _scale + OffsetX, screenY / _scale + OffsetY);

    /// <summary>
    /// Converts an image point to the viewport position the pan puts it at; at a whole scale it
    /// is drawn up to half a physical pixel from there (<see cref="DrawnOffsetX"/>).
    /// </summary>
    /// <param name="contentX">The point, in image pixels from the image's left edge.</param>
    /// <param name="contentY">The point, in image pixels from the image's top edge.</param>
    /// <returns>
    /// The position, in physical pixels from the viewport's top-left corner; it may lie outside
    /// the viewport.
    /// </returns>
    public PointD ContentToScreen(double contentX, double contentY) =>
        new((contentX - OffsetX) * _scale, (contentY - OffsetY) * _scale);

    /// <summary>
    /// Finds the image pixel drawn on the physical screen pixel the pointer is on (R-028).
    /// </summary>
    /// <param name="screenX">
    /// The pointer's position, in screen space (physical pixels): the left edge of the physical
    /// pixel it is on. It need not be whole, since the viewport's own origin may sit on a fraction
    /// of a physical pixel.
    /// </param>
    /// <param name="screenY">The top edge of that physical pixel, in the same space.</param>
    /// <returns>
    /// The pixel that holds the image point at the centre of that physical pixel, by its whole
    /// coordinates; a centre on the edge between two pixels, or within <see cref="EdgeReach"/>
    /// past it, belongs to the pixel before the edge. Null when the physical pixel shows the
    /// empty margin around the image or lies past its far edge, and when there is no content.
    /// </returns>
    public ImagePixel? PixelAt(double screenX, double screenY)
    {
        if (!HasContent)
        {
            return null;
        }

        // The image point at the centre of the physical pixel, not at its corner: that is the
        // point the screen pixel is painted from (ADR 0052).
        // Through the offsets the image is drawn with, which at a whole scale are not the pan's.
        var centre = new PointD(
            ((screenX + 0.5) / _scale) + DrawnOffsetX,
            ((screenY + 0.5) / _scale) + DrawnOffsetY);

        // Pixel k holds the points from just past k + EdgeReach up to k + 1 + EdgeReach: rounding
        // up and stepping back one gives an edge, and the little past it, to the pixel before.
        double x = Math.Ceiling(centre.X - EdgeReach) - 1;
        double y = Math.Ceiling(centre.Y - EdgeReach) - 1;

        // The content's size is a whole number of pixels, so "less than" ends at the last one.
        bool inside = x >= 0 && y >= 0 && x < ContentWidth && y < ContentHeight;

        return inside ? new ImagePixel((int)x, (int)y) : null;
    }

    /// <summary>
    /// Zooms to <paramref name="scale"/> while holding the content point currently under the
    /// anchor fixed on screen. Anchoring at the mouse is what makes wheel zoom feel like the
    /// image is being pulled towards the cursor rather than towards its own centre. Does
    /// nothing while there is no content; the offsets are clamped afterwards.
    /// </summary>
    /// <param name="scale">
    /// The new scale, 1 being one image pixel per physical screen pixel; clamped to
    /// <see cref="MinScale"/> and <see cref="MaxScale"/>, and taken as a whole number when it
    /// is a last bit from one (<see cref="Settled"/>).
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
        _scale = Settled(Math.Clamp(scale, MinScale, MaxScale));
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
    /// Zooms to <see cref="ZoomLadder.Next"/> of the current scale, clamped and anchored as
    /// <see cref="ZoomTo"/> does: the next stop up, or no change at the highest.
    /// </summary>
    /// <param name="anchorScreenX">The anchor, in screen space (physical pixels).</param>
    /// <param name="anchorScreenY">The anchor, in screen space (physical pixels).</param>
    public void ZoomIn(double anchorScreenX, double anchorScreenY) =>
        ZoomTo(ZoomLadder.Next(_scale), anchorScreenX, anchorScreenY);

    /// <summary>
    /// Zooms to <see cref="ZoomLadder.Previous"/> of the current scale, clamped and anchored as
    /// <see cref="ZoomTo"/> does: normally the next stop down, and just above
    /// <see cref="MinScale"/> MinScale itself. When no stop is below the scale, which happens
    /// only when the fit is under 2%, it zooms to MinScale instead, so it never zooms in; at
    /// MinScale it changes nothing.
    /// </summary>
    /// <param name="anchorScreenX">The anchor, in screen space (physical pixels).</param>
    /// <param name="anchorScreenY">The anchor, in screen space (physical pixels).</param>
    public void ZoomOut(double anchorScreenX, double anchorScreenY)
    {
        // Under the lowest stop the ladder answers with that stop, which is above the scale.
        double previous = ZoomLadder.Previous(_scale);
        ZoomTo(previous < _scale ? previous : MinScale, anchorScreenX, anchorScreenY);
    }

    /// <summary>Zooms so the whole image is visible and centres it.</summary>
    public void ZoomToFit()
    {
        if (!HasContent)
        {
            return;
        }

        _scale = Settled(Math.Clamp(FitScale, MinScale, MaxScale));
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
    /// The image x the pan puts at the viewport's left edge, in image pixels; clamped like
    /// every other movement.
    /// </param>
    /// <param name="offsetY">
    /// The image y the pan puts at the viewport's top edge, in image pixels; clamped too.
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
    /// size changed, a last bit from a whole number taken as that number
    /// (<see cref="Settled"/>); leaves it alone while there is no content.
    /// </summary>
    private void ClampScale()
    {
        if (HasContent)
        {
            _scale = Settled(Math.Clamp(_scale, MinScale, MaxScale));
        }
    }

    /// <summary>
    /// Takes a scale a last bit away from a whole number as that number: four wheel notches in
    /// and four out give 1.0000000000000002, which is meant as 100% and has to be drawn as it
    /// (R-022).
    /// </summary>
    /// <param name="scale">The scale, already between the floor and the ceiling.</param>
    /// <returns>
    /// The whole number within <see cref="WholeTolerance"/> of the scale, from 1 up; otherwise
    /// the scale as given, and also when the scale shows the whole image and the whole number
    /// would not.
    /// </returns>
    private double Settled(double scale)
    {
        double whole = Math.Round(scale);
        if (whole < 1 || Math.Abs(scale - whole) >= WholeTolerance)
        {
            return scale;
        }

        // A fit a last bit under a whole number stays the fit: at the whole number the image
        // would be larger than the view by that bit, and a scrollbar would show at Fit.
        double fit = FitScale;
        return scale <= fit && whole > fit ? scale : whole;
    }

    /// <summary>
    /// The offset the image is drawn with on one axis: the pan's, but at a whole scale the
    /// nearest that puts the image on whole physical pixels (R-022).
    /// </summary>
    /// <param name="offset">The pan on the axis, in image pixels; negative when the image is centred.</param>
    /// <param name="content">The image's size on the axis, in image pixels.</param>
    /// <param name="viewport">The view's size on the axis, in physical pixels; it need not be whole.</param>
    /// <returns>
    /// The offset itself at a scale that is not a whole number; at a whole one, the offset whose
    /// product with the scale is the nearest whole number of physical pixels, a half going up,
    /// and on an axis the image overflows never one that would leave the view past the image's
    /// far edge. A scale within <see cref="WholeTolerance"/> of a whole number counts as whole
    /// here: a fit a last bit under one is kept as it is (<see cref="Settled"/>) and is still
    /// drawn on whole pixels.
    /// </returns>
    private double Drawn(double offset, double content, double viewport)
    {
        double whole = Math.Round(_scale);
        if (!HasContent || whole < 1 || Math.Abs(_scale - whole) >= WholeTolerance)
        {
            return offset;
        }

        double pixels = Math.Floor((offset * _scale) + 0.5 + WholeTolerance);

        // On a view that does not end on a whole pixel the nearest pixel can be past the end of
        // the range; the last whole one inside it is drawn instead.
        double last = (content * _scale) - viewport;
        if (last > 0)
        {
            pixels = Math.Min(pixels, Math.Floor(last + WholeTolerance));
        }

        return pixels / _scale;
    }

    /// <summary>
    /// Brings the offsets back to where the view may be, axis by axis: an axis on which the
    /// whole image fits is centred, with equal margins either side, which makes its offset
    /// negative; on any other, no edge of the image comes inside the view.
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
