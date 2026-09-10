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

// RasterBitmaps.cs - un raster de octeti, direct ca bitmap WPF, fara PNG.
//
// Hartile trec prin PngWriter fiindca PNG-ul E fisierul pe care il scrie consola, si
// fereastra vrea sa arate exact acei octeti. Vederile egalizate n-au fisier pe partea
// ferestrei, iar la rezolutia senzorului un PNG de zgomot egalizat ar avea 15-20 MB si
// ar lua secunde de encodat pe vedere - pentru nimic: BitmapSource.Create ia octetii
// asa cum sunt. Fara asta zoom-ul ar mari medii de blocuri, nu pixeli de senzor.

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlatCheck.Wpf
{
    internal static class RasterBitmaps
    {
        /// <summary>Un raster gri de w*h octeti, rand cu rand. Inghetat, deci bun pe orice fir.</summary>
        internal static BitmapSource Grey(byte[] grey, int w, int h)
        {
            var b = BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, grey, w);
            b.Freeze();
            return b;
        }

        /// <summary>Un raster RGB de w*h*3 octeti, in ordinea R, G, B pe pixel.</summary>
        internal static BitmapSource Rgb(byte[] rgb, int w, int h)
        {
            var b = BitmapSource.Create(w, h, 96, 96, PixelFormats.Rgb24, null, rgb, w * 3);
            b.Freeze();
            return b;
        }
    }
}
