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

#nullable disable // copied from FlatCheck 2.3.0 unchanged, which compiles without nullable annotations (ADR 0003)
// The code is the copy's; the comments are DarksCheck's own (ADR 0019).

// Img.cs - the 2D raster and the pure transforms on it.

namespace DarksCheck.Core
{
    // ---------------- simple 2D image ----------------
    /// <summary>
    /// A 2D raster of doubles, row-major: what every reader fills, in ADU for a raw and as the
    /// file's samples for a FITS. DarksCheck reads only its pixels (FrameStack turns them into
    /// 16-bit mosaics); the transforms below are pure, return a new raster or their input when
    /// there is nothing to do, and are not called by DarksCheck: the file is a copy (ADR 0003).
    /// </summary>
    sealed class Img
    {
        public int H, W;
        public double[] D; // row-major, length H*W

        /// <summary>A raster of zeros.</summary>
        /// <param name="h">Height in pixels.</param>
        /// <param name="w">Width in pixels.</param>
        public Img(int h, int w) { H = h; W = w; D = new double[h * w]; }

        /// <summary>The value at a pixel.</summary>
        /// <param name="y">Row, 0 at the top.</param>
        /// <param name="x">Column, 0 at the left.</param>
        /// <returns>The pixel's value.</returns>
        public double At(int y, int x) => D[y * W + x];

        /// <summary>Writes the value of a pixel.</summary>
        /// <param name="y">Row, 0 at the top.</param>
        /// <param name="x">Column, 0 at the left.</param>
        /// <param name="v">The new value.</param>
        public void Set(int y, int x, double v) => D[y * W + x] = v;

        // ========================================================= preprocessing
        /// <summary>
        /// The mean of each <paramref name="k"/> x <paramref name="k"/> block, anchored at the top
        /// left; the partial blocks at the right and bottom ends are dropped.
        /// </summary>
        /// <param name="a">The raster to shrink.</param>
        /// <param name="k">The block side in pixels; 1 or less returns <paramref name="a"/> itself.
        /// </param>
        /// <returns>A raster of <c>H / k</c> by <c>W / k</c> block means.</returns>
        internal static Img BlockMean(Img a, int k)
        {
            if (k <= 1) return a;
            int h = a.H / k, w = a.W / k;
            var o = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double s = 0;
                    for (int dy = 0; dy < k; dy++)
                        for (int dx = 0; dx < k; dx++)
                            s += a.At(y * k + dy, x * k + dx);
                    o.Set(y, x, s / (k * k));
                }
            return o;
        }

        /// <summary>
        /// A 2x2 block mean (each Bayer quad averaged into one value), then a downsample by the
        /// whole factor that brings the long side to ~<paramref name="targetLong"/> px.
        /// </summary>
        /// <param name="data">The frame as loaded, CFA mosaic or not.</param>
        /// <param name="targetLong">The long side to shrink towards, in pixels.</param>
        /// <returns>The shrunk raster; never larger than half the input.</returns>
        internal static Img Prepare(Img data, int targetLong = 1200)
        {
            Img img = BlockMean(data, 2);
            int k = Math.Max(1, (int)(Math.Max(img.H, img.W) / (double)targetLong));
            return BlockMean(img, k);
        }

        /// <summary>
        /// Divides two rasters of the same size, pixel by pixel, with a floor on the denominator
        /// (like np.maximum(b, 1e-12)).
        /// </summary>
        /// <param name="a">The numerator.</param>
        /// <param name="b">The denominator.</param>
        /// <returns>A new raster of <c>a / max(b, 1e-12)</c>.</returns>
        internal static Img Divide(Img a, Img b)
        {
            var o = new Img(a.H, a.W);
            for (int i = 0; i < a.D.Length; i++) o.D[i] = a.D[i] / Math.Max(b.D[i], 1e-12);
            return o;
        }

        /// <summary>
        /// Cuts a centred rectangle, dropping equally from both ends. Unlike the grid of
        /// <see cref="BlockMean"/>, which is anchored at the top left, a centred crop falls onto
        /// itself when the raster is rotated by 180 degrees.
        /// </summary>
        /// <param name="a">The raster to crop.</param>
        /// <param name="h">The height to keep, in pixels, at most <c>a.H</c>.</param>
        /// <param name="w">The width to keep, in pixels, at most <c>a.W</c>.</param>
        /// <returns>The centred crop, or <paramref name="a"/> itself when nothing is cut.</returns>
        internal static Img CropCentred(Img a, int h, int w)
        {
            if (h == a.H && w == a.W) return a;
            int oy = (a.H - h) / 2, ox = (a.W - w) / 2;
            var o = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    o.Set(y, x, a.At(oy + y, ox + x));
            return o;
        }

        /// <summary>
        /// The grid of medians over tiles of <paramref name="k"/> px, WITH the partial tiles at
        /// the end, and the real centre of each tile in pixels.
        ///
        /// Unlike <see cref="BlockMean"/>, the partial tiles are kept, so the grid reaches the
        /// last pixel, and a partial tile's centre is its real centre, not one more step. The
        /// median, not the mean: a tile with a hot pixel keeps its typical value.
        /// </summary>
        /// <param name="a">The raster to tile.</param>
        /// <param name="k">The tile side in pixels.</param>
        /// <returns>
        /// The tile medians (<c>ceil(H / k)</c> by <c>ceil(W / k)</c>), and the centre of each
        /// tile row and tile column in pixels of <paramref name="a"/>.
        /// </returns>
        internal static (Img Tiles, double[] CentreY, double[] CentreX) TileMedians(Img a, int k)
        {
            int th = (a.H + k - 1) / k, tw = (a.W + k - 1) / k;
            var t = new Img(th, tw);
            var cy = new double[th];
            var cx = new double[tw];
            var buf = new double[k * k];
            for (int i = 0; i < tw; i++)
                cx[i] = (i * k + Math.Min(a.W, (i + 1) * k)) / 2.0;
            for (int j = 0; j < th; j++)
            {
                int y0 = j * k, y1 = Math.Min(a.H, (j + 1) * k);
                cy[j] = (y0 + y1) / 2.0;
                for (int i = 0; i < tw; i++)
                {
                    int x0 = i * k, x1 = Math.Min(a.W, (i + 1) * k);
                    int n = 0;
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                            buf[n++] = a.At(y, x);
                    var vals = new double[n];
                    Array.Copy(buf, vals, n);
                    Array.Sort(vals);
                    t.Set(j, i, Stats.OfSorted(vals, 50));
                }
            }
            return (t, cy, cx);
        }

        /// <summary>The raster rotated by 180 degrees about its centre.</summary>
        /// <param name="a">The raster to rotate.</param>
        /// <returns>A new raster of the same size.</returns>
        internal static Img Rotate180(Img a)
        {
            var o = new Img(a.H, a.W);
            for (int y = 0; y < a.H; y++)
                for (int x = 0; x < a.W; x++)
                    o.Set(y, x, a.At(a.H - 1 - y, a.W - 1 - x));
            return o;
        }

        /// <summary>
        /// The mean of the raster and its 180-degree rotation: it cancels the odd part exactly
        /// (any linear gradient among it) and leaves the even part, a radial profile included,
        /// untouched.
        /// </summary>
        /// <param name="a">The raster to symmetrise.</param>
        /// <returns>A new raster of the same size, equal to its own 180-degree rotation.</returns>
        internal static Img Symmetrise(Img a)
        {
            var o = new Img(a.H, a.W);
            for (int y = 0; y < a.H; y++)
                for (int x = 0; x < a.W; x++)
                    o.Set(y, x, (a.At(y, x) + a.At(a.H - 1 - y, a.W - 1 - x)) / 2.0);
            return o;
        }
    }
}
