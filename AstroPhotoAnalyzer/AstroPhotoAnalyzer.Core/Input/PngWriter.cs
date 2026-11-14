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

// PngWriter.cs - the PNG of a crop (R-181, ADR 0093), through ImageSharp's encoder: 16 bits per
// sample or 8, RGB or grey, with the input's ICC profile.
//
// Why the encoder and not a writer by hand, as the TIFF's: measured on 2026-11-14 on the crop
// of de_printat.png (ADR 0093), ImageSharp 3.1.11 writes a 16-bit RGB PNG sample for sample,
// with the iCCP chunk when the image's metadata hold a profile, as small as a hand-written one
// and as fast. The TIFF writer exists only because the TIFF encoder cannot write 16 bits.
// The encoder also writes a pHYs chunk (96 dpi) the input may not have had; it changes no pixel.
//
// Beside ImageSharpReader and JpegAnalyzer, this is the third file of the core that touches
// ImageSharp. The file is written whole or not at all (AtomicFile).

using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace AstroPhotoAnalyzer
{
    /// <summary>Writes a raster as a PNG of 16 or 8 bits per sample with its ICC profile (R-181).</summary>
    internal static class PngWriter
    {
        /// <summary>
        /// Writes <paramref name="r"/> to <paramref name="target"/>: <paramref name="bits"/>
        /// bits per sample, RGB for a colour raster and grey for a mono one, every sample
        /// rounded to 0..65535 (0..255 at 8 bits), whole or not at all.
        /// </summary>
        /// <param name="r">The raster, 0..1 per sample.</param>
        /// <param name="icc">The ICC profile to embed; null for none.</param>
        /// <param name="target">The file to write; replaced when it exists.</param>
        /// <param name="bits">The bits per sample: 16 or 8; anything else throws.</param>
        internal static void Save(Raster r, byte[] icc, string target, int bits)
        {
            if (bits != 8 && bits != 16) throw new ArgumentException("a PNG is written at 8 or 16 bits per sample, not " + bits, nameof(bits));
            var encoder = new PngEncoder
            {
                BitDepth = bits == 16 ? PngBitDepth.Bit16 : PngBitDepth.Bit8,
                ColorType = r.IsColor ? PngColorType.Rgb : PngColorType.Grayscale,
            };
            AtomicFile.Write(target, o =>
            {
                using Image image = (r.IsColor, bits) switch
                {
                    (true, 16) => Pixels<Rgb48>(r, (v, p) => new Rgb48((ushort)RasterOps.Level16(v[0][p]), (ushort)RasterOps.Level16(v[1][p]), (ushort)RasterOps.Level16(v[2][p]))),
                    (true, _) => Pixels<Rgb24>(r, (v, p) => new Rgb24((byte)RasterOps.Level8(v[0][p]), (byte)RasterOps.Level8(v[1][p]), (byte)RasterOps.Level8(v[2][p]))),
                    (false, 16) => Pixels<L16>(r, (v, p) => new L16((ushort)RasterOps.Level16(v[0][p]))),
                    _ => Pixels<L8>(r, (v, p) => new L8((byte)RasterOps.Level8(v[0][p]))),
                };
                if (icc != null) image.Metadata.IccProfile = new IccProfile(icc);
                image.Save(o, encoder);
            });
        }

        /// <summary>The raster as an ImageSharp image of one pixel type.</summary>
        /// <typeparam name="TPixel">The pixel type the file is written from.</typeparam>
        /// <param name="r">The raster.</param>
        /// <param name="pixel">The pixel made from the raster's planes at an index of theirs.</param>
        /// <returns>The image, the caller's to dispose.</returns>
        static Image<TPixel> Pixels<TPixel>(Raster r, Func<float[][], int, TPixel> pixel) where TPixel : unmanaged, IPixel<TPixel>
        {
            var image = new Image<TPixel>(r.W, r.H);
            float[][] planes = r.Planes;
            int w = r.W;
            try
            {
                image.ProcessPixelRows(acc =>
                {
                    for (int y = 0; y < acc.Height; y++)
                    {
                        Span<TPixel> row = acc.GetRowSpan(y);
                        int at = y * w;
                        for (int x = 0; x < row.Length; x++) row[x] = pixel(planes, at + x);
                    }
                });
            }
            catch
            {
                // The image holds pooled memory: it goes back even when the filling fails.
                image.Dispose();
                throw;
            }
            return image;
        }
    }
}
