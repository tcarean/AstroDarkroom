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

// FrameCrop.cs - the rectangle of the Crop with aspect ratio tab (R-180, ADR 0093): the part of
// the image that goes on a frame of a given width and height.
//
// A computation, like CropAnalyzer, whose centred rectangle is where this one starts. The
// window moves and resizes it with the mouse, but every rectangle it shows comes from here: in
// whole pixels of the full image, inside the image, of the frame's proportions within half a
// pixel. The window only says where the pointer is.

using System;
using System.Globalization;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// A rectangle of the image, in whole pixels: the columns <see cref="X"/> to X + W - 1 and
    /// the rows <see cref="Y"/> to Y + H - 1, the row 0 at the top as in a <see cref="Raster"/>.
    /// </summary>
    /// <param name="X">The first column kept.</param>
    /// <param name="Y">The first row kept, from the top.</param>
    /// <param name="W">The width, in pixels.</param>
    /// <param name="H">The height, in pixels.</param>
    internal readonly record struct CropRect(int X, int Y, int W, int H)
    {
        /// <summary>The column after the last one kept.</summary>
        internal int Right => X + W;

        /// <summary>The row after the last one kept.</summary>
        internal int Bottom => Y + H;

        /// <summary>Whether the rectangle has a pixel and lies inside an image of the given size.</summary>
        /// <param name="imageW">The image's width, in pixels.</param>
        /// <param name="imageH">The image's height, in pixels.</param>
        /// <returns>True when it does.</returns>
        internal bool FitsIn(int imageW, int imageH) => W > 0 && H > 0 && X >= 0 && Y >= 0 && Right <= imageW && Bottom <= imageH;
    }

    /// <summary>A corner of the crop rectangle, the one a resize drags.</summary>
    internal enum CropCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    /// <summary>The figures of a crop rectangle the tab shows (R-180).</summary>
    internal sealed record FrameCropFacts
    {
        /// <summary>The rectangle's size, in pixels.</summary>
        internal int W { get; init; }
        internal int H { get; init; }

        /// <summary>The image's pixels cut on each side.</summary>
        internal int CutLeft { get; init; }
        internal int CutRight { get; init; }
        internal int CutTop { get; init; }
        internal int CutBottom { get; init; }

        /// <summary>The share of the image's pixels the rectangle keeps, in percent.</summary>
        internal double PctKept { get; init; }

        /// <summary>
        /// The pixels per inch of the kept width printed over the frame's width.
        /// </summary>
        internal double Ppi { get; init; }
    }

    /// <summary>Computes the crop rectangle for a frame and its moves (R-180).</summary>
    internal static class FrameCrop
    {
        /// <summary>
        /// A frame's size as it was typed: a number above 0, written with a point (R-118), with
        /// spaces around it allowed.
        /// </summary>
        /// <param name="typed">The field's text; null counts as empty.</param>
        /// <returns>The size, or null when the text is not a finite number above 0.</returns>
        internal static double? ParseSize(string typed)
        {
            if (!double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return null;
            return v > 0 && !double.IsInfinity(v) ? v : null;
        }

        /// <summary>
        /// The largest rectangle of the frame's proportions that fits the image, centred: the
        /// rule of R-032 (<see cref="CropAnalyzer"/>), the sizes as typed, so a frame taller than
        /// wide gives a rectangle taller than wide.
        /// </summary>
        /// <param name="imageW">The image's width, in pixels.</param>
        /// <param name="imageH">The image's height, in pixels.</param>
        /// <param name="frameW">The frame's width, above 0, in any unit.</param>
        /// <param name="frameH">The frame's height, above 0, in the same unit.</param>
        /// <returns>The rectangle; the whole image when it has the frame's proportions.</returns>
        internal static CropRect Largest(int imageW, int imageH, double frameW, double frameH)
        {
            CropResult c = CropAnalyzer.Analyze(imageW, imageH, frameW / frameH);
            return new CropRect(c.X0, c.Y0, c.W, c.H);
        }

        /// <summary>Moves a rectangle by a distance, stopping at the image's edges.</summary>
        /// <param name="r">The rectangle, inside the image.</param>
        /// <param name="dx">The move to the right, in pixels; negative to the left.</param>
        /// <param name="dy">The move down, in pixels; negative up.</param>
        /// <param name="imageW">The image's width, in pixels.</param>
        /// <param name="imageH">The image's height, in pixels.</param>
        /// <returns>The rectangle of the same size at its new place.</returns>
        internal static CropRect Move(CropRect r, int dx, int dy, int imageW, int imageH)
            => r with { X = Math.Clamp(r.X + dx, 0, Math.Max(0, imageW - r.W)), Y = Math.Clamp(r.Y + dy, 0, Math.Max(0, imageH - r.H)) };

        /// <summary>
        /// Resizes a rectangle from one corner, towards a point: the opposite corner stays, the
        /// proportions are the frame's, and of the two sizes the point asks for (by its distance
        /// from the opposite corner along x, and along y) the larger one is taken, so the corner
        /// follows the pointer along the axis it was pulled further on. The size stops at the
        /// image's edges and at <see cref="Thresholds.CropMinSide"/> on the short side (or at the
        /// largest size the place allows, when that is smaller); a point past the opposite corner
        /// gives the smallest size, never a rectangle turned inside out.
        /// </summary>
        /// <param name="r">The rectangle, inside the image.</param>
        /// <param name="corner">The corner dragged.</param>
        /// <param name="x">Where the corner is pulled to: a column edge, 0 the image's left edge; may be outside the image.</param>
        /// <param name="y">Where the corner is pulled to: a row edge, 0 the image's top edge; may be outside the image.</param>
        /// <param name="imageW">The image's width, in pixels.</param>
        /// <param name="imageH">The image's height, in pixels.</param>
        /// <param name="frameW">The frame's width, above 0.</param>
        /// <param name="frameH">The frame's height, above 0, in the same unit.</param>
        /// <returns>The resized rectangle.</returns>
        internal static CropRect Resize(CropRect r, CropCorner corner, double x, double y, int imageW, int imageH, double frameW, double frameH)
        {
            double ratio = frameW / frameH;
            bool left = corner is CropCorner.TopLeft or CropCorner.BottomLeft;
            bool top = corner is CropCorner.TopLeft or CropCorner.TopRight;
            // The corner that stays, and the room from it to the image's edge the drag goes to.
            int anchorX = left ? r.Right : r.X, anchorY = top ? r.Bottom : r.Y;
            int roomW = left ? anchorX : imageW - anchorX, roomH = top ? anchorY : imageH - anchorY;
            double askedW = left ? anchorX - x : x - anchorX, askedH = top ? anchorY - y : y - anchorY;

            // The largest width the room holds, with its height, and the smallest allowed.
            (int maxW, int maxH) = Fit(roomW, roomH, ratio);
            int minW = Math.Min(maxW, (int)Math.Ceiling(ratio >= 1 ? Thresholds.CropMinSide * ratio : Thresholds.CropMinSide));

            double asked = Math.Max(askedW, askedH * ratio);
            int w = (int)Math.Clamp(Math.Round(double.IsNaN(asked) ? minW : asked), minW, maxW);
            int h = w == maxW ? maxH : Math.Clamp((int)Math.Round(w / ratio), 1, roomH);
            return new CropRect(left ? anchorX - w : anchorX, top ? anchorY - h : anchorY, w, h);
        }

        /// <summary>
        /// The largest size of a given width over height that fits a room, in whole pixels: the
        /// full width with its rounded height when that height fits, otherwise the full height
        /// with its rounded width.
        /// </summary>
        /// <param name="roomW">The room's width, in pixels, at least 1.</param>
        /// <param name="roomH">The room's height, in pixels, at least 1.</param>
        /// <param name="ratio">The width over the height, above 0.</param>
        /// <returns>The width and the height, each at least 1.</returns>
        static (int W, int H) Fit(int roomW, int roomH, double ratio)
        {
            int h = (int)Math.Round(roomW / ratio);
            if (h <= roomH) return (roomW, Math.Max(1, h));
            return (Math.Clamp((int)Math.Round(roomH * ratio), 1, roomW), roomH);
        }

        /// <summary>The figures of a rectangle for the tab (R-180).</summary>
        /// <param name="r">The rectangle, inside the image.</param>
        /// <param name="imageW">The image's width, in pixels.</param>
        /// <param name="imageH">The image's height, in pixels.</param>
        /// <param name="frameWcm">The frame's width, in cm, above 0.</param>
        /// <returns>The size, the cut on each side, the share kept and the pixels per inch.</returns>
        internal static FrameCropFacts Facts(CropRect r, int imageW, int imageH, double frameWcm)
            => new FrameCropFacts
            {
                W = r.W,
                H = r.H,
                CutLeft = r.X,
                CutRight = imageW - r.Right,
                CutTop = r.Y,
                CutBottom = imageH - r.Bottom,
                PctKept = 100.0 * ((double)r.W * r.H) / ((double)imageW * imageH),
                Ppi = r.W / (frameWcm / 2.54),
            };
    }
}
