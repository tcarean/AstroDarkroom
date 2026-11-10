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

// Img.cs - the 2D raster and the pure transforms on it.

using System;

namespace FlatCheck
{
    // ---------------- simple 2D image ----------------
    /// <summary>
    /// A 2D raster of doubles, row-major: the one image type every reader fills and every
    /// analyzer reads. Its values are whatever the producer put there (ADU of a raw, a FITS's
    /// samples, a ratio near 1); the transforms below are pure and return a new raster, or their
    /// input when there is nothing to do.
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
            // The block's values are summed in sorted order, so the sum does not depend on the
            // order the pixels come in: the mirror of a raster has the same four values in
            // another order, and a sum that differed in its last bit tipped the inlier cut of
            // the ring fit downstream for one sector, moving the model by 1e-4 (R-131).
            var block = new double[k * k];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = 0;
                    for (int dy = 0; dy < k; dy++)
                        for (int dx = 0; dx < k; dx++)
                            block[i++] = a.At(y * k + dy, x * k + dx);
                    Array.Sort(block);
                    double s = 0;
                    foreach (double v in block) s += v;
                    o.Set(y, x, s / (k * k));
                }
            return o;
        }

        /// <summary>
        /// The block mean on a grid laid symmetric about the raster's centre: <c>H / k</c> tiles
        /// down and <c>W / k</c> across, the leftover rows and columns split between the two
        /// edges, so that the row mirror or the column mirror of the raster gives the mirror of
        /// the tiles (R-131). With an odd leftover the grid's edge falls at half a pixel and the
        /// boundary row, or column, counts half in the edge tile: a pixel's weight is its overlap
        /// with the tile, 1/2 or 1, and the weights of a tile sum to <c>k * k</c>. With an even
        /// leftover it is <see cref="BlockMean(Img, int)"/> shifted by half the leftover.
        /// </summary>
        /// <param name="a">The raster to tile.</param>
        /// <param name="k">The tile side in pixels; 1 or less returns <paramref name="a"/> itself.</param>
        /// <returns>The tiles, <c>H / k</c> by <c>W / k</c>.</returns>
        internal static Img BlockMeanCentred(Img a, int k)
        {
            if (k <= 1) return a;
            int h = a.H / k, w = a.W / k;
            double oy = CentredOffset(a.H, k), ox = CentredOffset(a.W, k);
            var o = new Img(h, w);
            // the weighted terms are summed in sorted order, like BlockMean's values, so the tile
            // of a mirrored raster is the mirrored tile to the last bit; a tile spans at most
            // k + 1 rows and columns when its edges fall on half pixels
            var terms = new double[(k + 1) * (k + 1)];
            for (int ty = 0; ty < h; ty++)
                for (int tx = 0; tx < w; tx++)
                {
                    double y0 = oy + ty * k, y1 = y0 + k, x0 = ox + tx * k, x1 = x0 + k;
                    int ya = (int)Math.Floor(y0), yb = (int)Math.Ceiling(y1);
                    int xa = (int)Math.Floor(x0), xb = (int)Math.Ceiling(x1);
                    int i = 0;
                    for (int y = ya; y < yb; y++)
                    {
                        double wy = Math.Min(y + 1, y1) - Math.Max(y, y0);
                        for (int x = xa; x < xb; x++)
                            terms[i++] = wy * (Math.Min(x + 1, x1) - Math.Max(x, x0)) * a.At(y, x);
                    }
                    Array.Sort(terms, 0, i);
                    double s = 0;
                    for (int t = 0; t < i; t++) s += terms[t];
                    o.Set(ty, tx, s / ((double)k * k));
                }
            return o;
        }

        /// <summary>
        /// Where the centred grid of <see cref="BlockMeanCentred"/> starts along one axis: half
        /// the leftover of <paramref name="n"/> pixels tiled by <paramref name="k"/>, a whole or a
        /// half pixel.
        /// </summary>
        /// <param name="n">The raster's extent along the axis, in pixels.</param>
        /// <param name="k">The tile side in pixels.</param>
        /// <returns>The edge coordinate of the first tile, in pixels.</returns>
        internal static double CentredOffset(int n, int k) => (n - n / k * k) / 2.0;

        /// <summary>
        /// The analysis raster: a 2x2 block mean (it kills the Bayer CFA, each quad averaged into
        /// one value), then a downsample by the whole factor that brings the long side to
        /// ~<paramref name="targetLong"/> px, 1200 by default.
        /// </summary>
        /// <param name="data">The frame as loaded, CFA mosaic or not.</param>
        /// <param name="targetLong">The long side to shrink towards, in pixels.</param>
        /// <returns>The shrunk raster; never larger than half the input.</returns>
        internal static Img Prepare(Img data, int targetLong = 1200)
        {
            // The 2x2 step stays anchored at (0, 0): it must follow the Bayer phase. The further
            // downsample has no phase to keep and takes the centred grid, so a frame of even
            // width and height prepares to the mirror of its mirror's raster whatever the
            // leftover of the second step (R-131; a 8256 x 5504 frame halves to 4128 x 2752 and
            // k = 3 leaves one row over, which an anchored grid dropped at the far edge).
            Img img = BlockMean(data, 2);
            int k = Math.Max(1, (int)(Math.Max(img.H, img.W) / (double)targetLong));
            return BlockMeanCentred(img, k);
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
        /// The mean of the raster and its 180-degree rotation.
        ///
        /// It cancels the odd part EXACTLY - so any linear gradient - and leaves the even part
        /// untouched. A radial profile cannot be odd, so the radial model loses nothing it is
        /// allowed to contain, and exactly what made it jump is removed.
        ///
        /// WHY IT IS NEEDED, since it is not obvious: beyond the inscribed circle, a ring of
        /// constant radius is no longer a whole ring, it is partial arcs. On a frame with a
        /// gradient, the distribution of the pixels of those arcs is BIMODAL - one end dark, the
        /// other bright - and the median falls into the sparse gap between the two modes, so it
        /// jumps from one ring to the next with an amplitude set by the gradient. The
        /// interpolated model inherited the jumps, and img/model showed them as concentric rings
        /// plus a hard edge on the inscribed circle. Reproduced on a synthetic raster that is
        /// only smooth vignetting times an 11% gradient: the rings appear without any edge
        /// existing in the data.
        ///
        /// The verdict figures did not depend on this at the time - LargeScalePtp was restricted
        /// to the inscribed circle, where the median was already exact (measured: the residual
        /// moves by under 0.05 points) - but the MAP lied, and the map is the only thing the
        /// shape of a problem is read from. Once the model was valid out to the corner, the
        /// restriction was removed; see Metrics.LargeScalePtp.
        ///
        /// The alternative was the mean per ring instead of the median, which cures the
        /// bimodality just as well (measured, the same figures), but gives up the median's
        /// robustness against stars, hot pixels and dust - and the analyzer also gets single
        /// frames.
        ///
        /// Symmetrising removes ONLY the odd part. The non-radial even part - a saddle, a
        /// steeper fall along one axis - passes through it untouched and has the same fate on
        /// the partial arcs; that one is handled by Metrics.RadialModel, per ring, with a 2t
        /// correction. See there.
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
