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

// Raster.cs - the image in memory: 1 or 3 float planes, values 0..1.
//
// WHY FLOAT AND NOT DOUBLE: the typical image has 23 MP on 3 channels. In double the pixels
// alone would take 554 MB; in float, 277. Float precision (24 bits of mantissa) covers the
// source's 16 with room to spare - round(v * 65535) gives back exactly the level in the file,
// and the measurement of the real bits rests on that.
//
// WHY SEPARATE PLANES AND NOT INTERLEAVED: almost every measurement works on a single channel
// (the green, or mono) or on each channel apart; a contiguous plane is walked without a stride
// and subsampled with a single loop.
//
// The pixels do not say where they came from - that is what SourceInfo is for, a separate record
// the reader fills in and attaches as Source. The analyzers that measure pixels get a Raster
// with its SourceInfo, and some of them the run's Parameters.

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// An image in memory: <see cref="H"/> rows of <see cref="W"/> pixels, in 1 or 3 float
    /// planes, every sample 0..1.
    /// </summary>
    internal sealed class Raster
    {
        internal readonly int H, W;

        /// <summary>
        /// 1 plane (mono) or 3 (R, G, B). Each is H*W, row-major, row 0 at the TOP.
        /// </summary>
        internal readonly float[][] Planes;

        /// <summary>
        /// What the file says about itself. Null on a raster built in memory (tests).
        /// </summary>
        internal SourceInfo Source;

        /// <summary>Allocates a raster of zeros, with no <see cref="Source"/>.</summary>
        /// <param name="h">The height, in pixels.</param>
        /// <param name="w">The width, in pixels.</param>
        /// <param name="planes">The number of planes: 1 (mono) or 3 (R, G, B).</param>
        internal Raster(int h, int w, int planes)
        {
            if (planes != 1 && planes != 3)
                throw new System.ArgumentException("un raster are 1 sau 3 planuri, nu " + planes);
            H = h; W = w;
            Planes = new float[planes][];
            for (int p = 0; p < planes; p++) Planes[p] = new float[h * w];
        }

        internal bool IsColor => Planes.Length == 3;

        /// <summary>The sample of one plane at one pixel.</summary>
        /// <param name="plane">The plane: 0 on mono; 0, 1 or 2 for R, G or B.</param>
        /// <param name="y">The row, 0 at the top.</param>
        /// <param name="x">The column, 0 at the left.</param>
        /// <returns>The sample, 0..1.</returns>
        internal float At(int plane, int y, int x) => Planes[plane][y * W + x];

        /// <summary>Writes the sample of one plane at one pixel.</summary>
        /// <param name="plane">The plane: 0 on mono; 0, 1 or 2 for R, G or B.</param>
        /// <param name="y">The row, 0 at the top.</param>
        /// <param name="x">The column, 0 at the left.</param>
        /// <param name="v">The sample, 0..1; stored as it is, not clamped.</param>
        internal void Set(int plane, int y, int x, float v) => Planes[plane][y * W + x] = v;

        /// <summary>
        /// The channel the single-channel measurements are made on: the green on colour (the
        /// closest to luminance, and the one with the most photons on a Bayer sensor), or the
        /// only plane on mono. The same channel the acceptance figures were measured on.
        /// </summary>
        internal float[] Reference => IsColor ? Planes[1] : Planes[0];
    }

    /// <summary>
    /// What the file knows about itself. The fields that are missing are not errors, they are
    /// the right answer for that format: a FITS has no compression and no ICC profile, a PNG has
    /// no BITPIX.
    /// </summary>
    internal sealed record SourceInfo
    {
        internal string Path { get; init; }

        /// <summary>
        /// "TIFF", "PNG", "JPEG", "FITS": the format the reader found, which for a mis-named
        /// file can be another of ImageSharp's ("BMP", "GIF", ...), noted in Note.
        /// </summary>
        internal string Format { get; init; }

        /// <summary>
        /// The bits per sample in the FILE: 8 or 16 on most images, 1, 2 or 4 on a PNG of few
        /// levels, the TIFF's own on a TIFF; on a FITS 8, 16 or 32, and 32 or 64 with IsFloat.
        /// </summary>
        internal int BitDepth { get; init; }

        /// <summary>
        /// A FITS with a negative BITPIX: real values, with no "levels" to count.
        /// </summary>
        internal bool IsFloat { get; init; }

        /// <summary>
        /// The planes the raster keeps, 1 or 3: a FITS's own; for a TIFF, PNG or JPEG 1 on grey
        /// and 3 on colour, an alpha channel dropped.
        /// </summary>
        internal int PlanesInFile { get; init; }

        /// <summary>
        /// The compression, as the format names it ("LZW", "Deflate", "necomprimat"); null where
        /// the notion does not exist.
        /// </summary>
        internal string Compression { get; init; }

        /// <summary>The embedded ICC profile, raw; null when the file has none.</summary>
        internal byte[] Icc { get; init; }

        /// <summary>
        /// The name in the profile's 'desc' tag; null without a profile, or with a profile without
        /// a name.
        /// </summary>
        internal string IccDescription { get; init; }

        internal long FileBytes { get; init; }

        /// <summary>
        /// The WCS from the FITS header (R-018), or the one borrowed through Parameters.WcsPath
        /// (R-027); null when the file has none: the dust and the sky are then measured without
        /// the catalogue.
        /// </summary>
        internal Wcs Wcs { get; init; }

        /// <summary>
        /// A remark of the reader's that the report shows as it is: for example that float values
        /// were clipped to [0, 1], or that the rows were flipped by ROWORDER. Null when there was
        /// nothing to say.
        /// </summary>
        internal string Note { get; init; }
    }
}
