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

// RasterBitmaps.cs - a raster of bytes, straight as a WPF bitmap, without PNG.
//
// The maps go through PngWriter because the PNG IS the file the console writes, and the window
// wants to show exactly those bytes. The equalised views have no file on the window's side, and
// at the sensor's resolution a PNG of equalised noise would be 15-20 MB and would take seconds
// to encode per view - for nothing: BitmapSource.Create takes the bytes as they are. Without
// this the zoom would magnify block means, not sensor pixels.

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlatCheck.Wpf
{
    /// <summary>Wraps byte rasters as frozen WPF bitmaps, without encoding them.</summary>
    internal static class RasterBitmaps
    {
        /// <summary>A grey raster of w*h bytes, row by row. Frozen, so good on any
        /// thread.</summary>
        /// <param name="grey">The pixels, one byte each, top row first.</param>
        /// <param name="w">The width, pixels.</param>
        /// <param name="h">The height, pixels.</param>
        /// <returns>The bitmap, at 96 dpi.</returns>
        internal static BitmapSource Grey(byte[] grey, int w, int h)
        {
            var b = BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, grey, w);
            b.Freeze();
            return b;
        }

        /// <summary>An RGB raster of w*h*3 bytes, in the order R, G, B per pixel.</summary>
        /// <param name="rgb">The pixels, three bytes each, top row first.</param>
        /// <param name="w">The width, pixels.</param>
        /// <param name="h">The height, pixels.</param>
        /// <returns>The bitmap, frozen, at 96 dpi.</returns>
        internal static BitmapSource Rgb(byte[] rgb, int w, int h)
        {
            var b = BitmapSource.Create(w, h, 96, 96, PixelFormats.Rgb24, null, rgb, w * 3);
            b.Freeze();
            return b;
        }
    }
}
