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

// RasterBitmaps.cs - a piece of a Raster, directly as a WPF bitmap, with no file.
//
// The previews are crops at 100 % or 200 % and reductions of the whole: all are made with an
// integer step (stride) and an integer magnification (zoom, nearest neighbour), so that the
// pixels shown are pixels of the file, not averages. BitmapSource.Create takes the bytes as
// they are; frozen, so good to bind from any thread.

using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// Frozen WPF bitmaps from a <see cref="Raster"/>: a region at an integer step and zoom,
    /// the whole frame reduced, and a semi-transparent overlay from a predicate per pixel.
    /// </summary>
    internal static class RasterBitmaps
    {
        /// <summary>
        /// The rectangle (x0, y0, w, h) of the raster, taken every <paramref name="stride"/>
        /// pixels and magnified <paramref name="zoom"/> times. On mono the same grey goes on all
        /// three channels.
        /// </summary>
        /// <param name="r">The raster, 0..1 levels; at least one pixel.</param>
        /// <param name="x0">The left column, in pixels; clamped into the image.</param>
        /// <param name="y0">The top row, in pixels, 0 at the top; clamped into the image.</param>
        /// <param name="w">The width in source pixels; clamped to 1..the pixels left of x0.</param>
        /// <param name="h">The height in source pixels; clamped to 1..the rows under y0.</param>
        /// <param name="stride">Take every n-th pixel on both axes, 1 or more.</param>
        /// <param name="zoom">
        /// Repeat each taken pixel this many times on both axes, 1 or more.
        /// </param>
        /// <returns>
        /// A frozen Rgb24 bitmap at 96 dpi, (w / stride) x zoom by (h / stride) x zoom pixels, at
        /// least 1 x 1, each sample the 8-bit level of <see cref="RasterOps.Level8"/>.
        /// </returns>
        internal static BitmapSource Region(Raster r, int x0, int y0, int w, int h, int stride = 1, int zoom = 1)
        {
            x0 = Math.Clamp(x0, 0, Math.Max(0, r.W - 1));
            y0 = Math.Clamp(y0, 0, Math.Max(0, r.H - 1));
            w = Math.Clamp(w, 1, r.W - x0);
            h = Math.Clamp(h, 1, r.H - y0);
            int ow = Math.Max(1, w / stride) * zoom, oh = Math.Max(1, h / stride) * zoom;
            var rgb = new byte[ow * oh * 3];
            float[] R = r.Planes[0], G = r.IsColor ? r.Planes[1] : r.Planes[0], B = r.IsColor ? r.Planes[2] : r.Planes[0];
            for (int y = 0; y < oh; y++)
            {
                int sy = y0 + (y / zoom) * stride;
                int so = sy * r.W;
                int o = y * ow * 3;
                for (int x = 0; x < ow; x++)
                {
                    int sx = x0 + (x / zoom) * stride;
                    rgb[o + x * 3] = (byte)RasterOps.Level8(R[so + sx]);
                    rgb[o + x * 3 + 1] = (byte)RasterOps.Level8(G[so + sx]);
                    rgb[o + x * 3 + 2] = (byte)RasterOps.Level8(B[so + sx]);
                }
            }
            var b = BitmapSource.Create(ow, oh, 96, 96, PixelFormats.Rgb24, null, rgb, ow * 3);
            b.Freeze();
            return b;
        }

        /// <summary>
        /// The whole image, reduced by the smallest step that brings its long side to at most
        /// <paramref name="maxSide"/> px (3200 px at 1600 gives 1600, not under).
        /// </summary>
        /// <param name="r">The raster, 0..1 levels.</param>
        /// <param name="maxSide">The limit of the long side, in pixels, 1 or more.</param>
        /// <returns>A frozen Rgb24 bitmap, as <see cref="Region"/> gives it.</returns>
        internal static BitmapSource Whole(Raster r, int maxSide)
            => Region(r, 0, 0, r.W, r.H, Math.Max(1, (int)Math.Ceiling(Math.Max(r.W, r.H) / (double)maxSide)));

        /// <summary>
        /// A layer of one semi-transparent colour, <paramref name="w"/> x <paramref name="h"/>
        /// pixels, painted where <paramref name="on"/>(x, y) is true and transparent elsewhere:
        /// how the map and preview windows show what the core decided. Decides nothing: it is
        /// given the class of each pixel or of each patch.
        /// </summary>
        /// <param name="w">The width in pixels, 1 or more.</param>
        /// <param name="h">The height in pixels, 1 or more.</param>
        /// <param name="on">Whether the pixel at column x, row y (0 at the top) is painted.</param>
        /// <param name="colour">
        /// The paint, with its alpha; the bytes stored are premultiplied.
        /// </param>
        /// <returns>A frozen Pbgra32 bitmap at 96 dpi.</returns>
        internal static BitmapSource Overlay(int w, int h, Func<int, int, bool> on, Color colour)
        {
            var bgra = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!on(x, y)) continue;
                    int o = (y * w + x) * 4;
                    // Pbgra32: the channels premultiplied with the alpha.
                    bgra[o] = (byte)(colour.B * colour.A / 255);
                    bgra[o + 1] = (byte)(colour.G * colour.A / 255);
                    bgra[o + 2] = (byte)(colour.R * colour.A / 255);
                    bgra[o + 3] = colour.A;
                }
            var b = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, bgra, w * 4);
            b.Freeze();
            return b;
        }
    }
}
