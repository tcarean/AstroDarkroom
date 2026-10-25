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

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightsCheck.Wpf;

/// <summary>Turns the core's byte rasters into bitmaps the window can show.</summary>
internal static class RasterBitmaps
{
    /// <summary>
    /// A grey raster of w x h bytes, row by row; frozen, so usable from any thread.
    /// </summary>
    /// <param name="grey">The pixels, w*h bytes, 0 black to 255 white, rows top down.</param>
    /// <param name="w">The width in pixels.</param>
    /// <param name="h">The height in pixels.</param>
    /// <returns>An 8-bit grey bitmap at 96 DPI.</returns>
    internal static BitmapSource Grey(byte[] grey, int w, int h)
    {
        BitmapSource b = BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, grey, w);
        b.Freeze();
        return b;
    }
}
