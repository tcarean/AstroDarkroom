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

// RasterBitmaps.cs - o bucata de Raster, direct ca bitmap WPF, fara fisier.
//
// Previzualizarile sunt decupaje la 100 % sau 200 % si micsorari ale intregului: toate
// se fac cu un pas intreg (stride) si o marire intreaga (zoom, vecinul cel mai apropiat),
// ca pixelii aratati sa fie pixeli din fisier, nu medii. BitmapSource.Create ia octetii
// asa cum sunt; inghetat, deci bun de legat de pe orice fir.

using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    internal static class RasterBitmaps
    {
        /// <summary>
        /// Dreptunghiul (x0, y0, w, h) din raster, luat din <paramref name="stride"/> in
        /// stride pixeli si marit de <paramref name="zoom"/> ori. Pe mono da acelasi gri pe
        /// toate cele trei canale.
        /// </summary>
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

        /// <summary>Intreaga imagine, micsorata cu pasul care o aduce sub <paramref name="maxSide"/> px.</summary>
        internal static BitmapSource Whole(Raster r, int maxSide)
            => Region(r, 0, 0, r.W, r.H, StrideFor(r, maxSide));

        /// <summary>Pasul intreg care aduce latura mare a rasterului sub <paramref name="maxSide"/>.</summary>
        internal static int StrideFor(Raster r, int maxSide)
            => Math.Max(1, (int)Math.Ceiling(Math.Max(r.W, r.H) / (double)maxSide));

        /// <summary>
        /// Un strat de culoare semitransparenta de <paramref name="w"/> x <paramref name="h"/>
        /// pixeli, vopsit acolo unde <paramref name="on"/>(x, y) e adevarat si transparent in
        /// rest: cum arata ferestrele de harta si de preview ce a decis nucleul. Nu decide
        /// nimic: primeste clasa fiecarui pixel sau a fiecarei dale.
        /// </summary>
        internal static BitmapSource Overlay(int w, int h, Func<int, int, bool> on, Color colour)
        {
            var bgra = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!on(x, y)) continue;
                    int o = (y * w + x) * 4;
                    // Pbgra32: canalele premultiplicate cu alpha.
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
