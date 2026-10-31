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

// Mosaic.cs - the shared rule of the CFA mosaic: which pixel is in which Bayer position, when a
// frame "looks CFA", and what a position is called.
//
// It sat inline in ExposureAnalyzer until the histogram equalisation mode, which needed exactly
// the same three things. Two copies of "(y & 1) * 2 + (x & 1)" and of "1.15" would have diverged
// at the first adjustment, and the divergence would have shown only when a channel was called R
// in one tab and G in another, on the same file.
//
// ExposureAnalyzer does NOT use Split: it fills the histograms in the same pass in which it reads
// the pixels, and a second copy of the data would cost memory for nothing. It uses PositionOf,
// LooksCfa and Label - the rule, not the buffers.

namespace FlatCheck
{
    /// <summary>
    /// The 2x2 Bayer mosaic's rule: a pixel's position, the four positions as rasters,
    /// and their names.
    /// </summary>
    internal static class Mosaic
    {
        /// <summary>
        /// The Bayer position of the pixel (y, x): 0..3, in the order (y&amp;1)*2 + (x&amp;1).
        /// </summary>
        /// <param name="y">Row, 0 at the top.</param>
        /// <param name="x">Column, 0 at the left.</param>
        /// <returns>0 for [0,0], 1 for [0,1], 2 for [1,0], 3 for [1,1].</returns>
        internal static int PositionOf(int y, int x) => (y & 1) * 2 + (x & 1);

        /// <summary>
        /// The threshold from which the four medians say "mosaic intact". Below it, the channels
        /// are too close to be different colour filters: the file is mono or debayered.
        /// Empirical, like all the thresholds; a raw under very neutral light can fall below it,
        /// and is then treated as mono - which is a safe choice.
        /// </summary>
        internal const double CfaSpreadMin = 1.15;

        /// <summary>Whether four position medians are those of an intact mosaic.</summary>
        /// <param name="spread">max/min over the four position medians.</param>
        /// <returns>True above <see cref="CfaSpreadMin"/>.</returns>
        internal static bool LooksCfa(double spread) => spread > CfaSpreadMin;

        /// <summary>
        /// The four positions as four rasters of H/2 x W/2 (integer division: an extra row or
        /// column is lost, as in BlockMean). The index in the array is the position.
        /// </summary>
        /// <param name="a">The mosaic raster.</param>
        /// <returns>Four rasters, indexed by <see cref="PositionOf"/>.</returns>
        internal static Img[] Split(Img a)
        {
            int h = a.H / 2, w = a.W / 2;
            var o = new Img[4];
            for (int i = 0; i < 4; i++) o[i] = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    o[0].Set(y, x, a.At(y * 2, x * 2));
                    o[1].Set(y, x, a.At(y * 2, x * 2 + 1));
                    o[2].Set(y, x, a.At(y * 2 + 1, x * 2));
                    o[3].Set(y, x, a.At(y * 2 + 1, x * 2 + 1));
                }
            return o;
        }

        /// <summary>
        /// The name of position <paramref name="i"/>: the colour when the file gave the order,
        /// the position when it did not. It is not guessed - the order differs from camera to
        /// camera.
        /// </summary>
        /// <param name="cfa">The file's colour order; null when unknown.</param>
        /// <param name="i">The position, 0..3.</param>
        /// <returns>"R", "G1", "G2", "B", or "CFA[row,col]" when the order is unknown.</returns>
        internal static string Label(CfaPattern cfa, int i) => cfa?.Name(i) ?? $"CFA[{i / 2},{i % 2}]";

        /// <summary>
        /// 'R', 'G' or 'B' for position <paramref name="i"/>; '\0' when not known.
        /// </summary>
        /// <param name="cfa">The file's colour order; null when unknown.</param>
        /// <param name="i">The position, 0..3.</param>
        /// <returns>The colour letter, or '\0'.</returns>
        internal static char ColourOf(CfaPattern cfa, int i) => cfa?.Colours[i] ?? '\0';
    }
}
