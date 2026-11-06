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

// TiffWriter.cs - the tool's own 16-bit TIFF writer (R-177, ADR 0083): classic little-endian
// TIFF, uncompressed, 16 bits per sample, RGB chunky or grey, the ICC profile embedded, a
// description and the program's name in their tags. Written by hand because ImageSharp 3.1
// writes a TIFF at 8 bits whatever is asked (measured 2026-11-06; docs/design/dependencies.md),
// and the owner's inputs are 16-bit. Uncompressed on purpose: Deflate with the predictor saved
// 20 % on the measured image at 6 s and a compressor to maintain, and Siril writes the inputs
// uncompressed too. The file is written whole or not at all (AtomicFile).

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AstroPhotoAnalyzer
{
    /// <summary>Writes a raster as a 16-bit uncompressed TIFF with its ICC profile (R-177).</summary>
    internal static class TiffWriter
    {
        /// <summary>The rows of one strip; the last strip is shorter.</summary>
        internal const int RowsPerStrip = 64;

        /// <summary>
        /// Writes <paramref name="r"/> to <paramref name="target"/>: 16 bits per sample, three
        /// samples per pixel for a colour raster, one for a mono raster, every sample rounded to
        /// 0..65535, whole or not at all.
        /// </summary>
        /// <param name="r">The raster, 0..1 per sample.</param>
        /// <param name="icc">The ICC profile to embed; null for none.</param>
        /// <param name="description">The ImageDescription tag's text, ASCII; null for none.</param>
        /// <param name="target">The file to write; replaced when it exists.</param>
        internal static void Save(Raster r, byte[] icc, string description, string target)
            => AtomicFile.Write(target, o => Write(r, icc, description, o));

        /// <summary>The body of <see cref="Save"/>, to an open stream at its start.</summary>
        /// <param name="r">The raster.</param>
        /// <param name="icc">The ICC profile; null for none.</param>
        /// <param name="description">The description; null for none.</param>
        /// <param name="o">The output.</param>
        static void Write(Raster r, byte[] icc, string description, Stream o)
        {
            int spp = r.IsColor ? 3 : 1;
            int strips = (r.H + RowsPerStrip - 1) / RowsPerStrip;
            long bytesPerRow = (long)r.W * spp * 2;
            byte[] software = Ascii(VersionInfo.Banner);
            byte[] desc = description == null ? null : Ascii(description);

            // The entries, sorted by tag as the format requires; the data too large for an
            // entry's four bytes goes in a heap after the directory, the strips after the heap.
            var entries = new List<(ushort Tag, ushort Type, uint Count, byte[] Data)>
            {
                (256, 4, 1, U32((uint)r.W)),
                (257, 4, 1, U32((uint)r.H)),
                (258, 3, (uint)spp, Shorts(spp, 16)),
                (259, 3, 1, U16(1)),
                (262, 3, 1, U16((ushort)(r.IsColor ? 2 : 1))),
            };
            if (desc != null) entries.Add((270, 2, (uint)desc.Length, desc));
            entries.Add((273, 4, (uint)strips, null));   // the strip offsets, filled once the heap is laid out
            entries.Add((277, 3, 1, U16((ushort)spp)));
            entries.Add((278, 4, 1, U32(RowsPerStrip)));
            var counts = new byte[4 * strips];
            for (int s = 0; s < strips; s++)
            {
                int rows = Math.Min(RowsPerStrip, r.H - s * RowsPerStrip);
                BinaryPrimitives.WriteUInt32LittleEndian(counts.AsSpan(4 * s, 4), (uint)(rows * bytesPerRow));
            }
            entries.Add((279, 4, (uint)strips, counts));
            entries.Add((282, 5, 1, Rational(72, 1)));
            entries.Add((283, 5, 1, Rational(72, 1)));
            entries.Add((284, 3, 1, U16(1)));
            entries.Add((296, 3, 1, U16(2)));
            entries.Add((305, 2, (uint)software.Length, software));
            entries.Add((339, 3, (uint)spp, Shorts(spp, 1)));
            if (icc != null) entries.Add((34675, 7, (uint)icc.Length, icc));

            // The layout: the header (8), the directory (2 + 12 n + 4), the heap, the strips.
            int ifdAt = 8, ifdLen = 2 + 12 * entries.Count + 4;
            long heapAt = ifdAt + ifdLen;
            long heapLen = 0;
            foreach (var e in entries)
            {
                int len = e.Data?.Length ?? 4 * strips;
                if (len > 4) heapLen += (len + 1) & ~1L;   // word-aligned, as readers expect
            }
            long stripsAt = heapAt + heapLen;
            var offsets = new byte[4 * strips];
            for (int s = 0; s < strips; s++)
                BinaryPrimitives.WriteUInt32LittleEndian(offsets.AsSpan(4 * s, 4), (uint)(stripsAt + s * RowsPerStrip * bytesPerRow));
            for (int i = 0; i < entries.Count; i++) if (entries[i].Tag == 273) entries[i] = (273, 4, (uint)strips, offsets);
            if (stripsAt + (long)r.H * bytesPerRow > uint.MaxValue) throw new Exception(Tr.Pick("the image is too large for a TIFF (over 4 GB)", "imaginea e prea mare pentru un TIFF (peste 4 GB)"));

            var header = new byte[8];
            header[0] = (byte)'I'; header[1] = (byte)'I'; header[2] = 42; header[3] = 0;
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), (uint)ifdAt);
            o.Write(header, 0, 8);

            var ifd = new byte[ifdLen];
            BinaryPrimitives.WriteUInt16LittleEndian(ifd.AsSpan(0, 2), (ushort)entries.Count);
            var heap = new MemoryStream();
            for (int i = 0; i < entries.Count; i++)
            {
                var (tag, type, count, data) = entries[i];
                Span<byte> e = ifd.AsSpan(2 + 12 * i, 12);
                BinaryPrimitives.WriteUInt16LittleEndian(e[..2], tag);
                BinaryPrimitives.WriteUInt16LittleEndian(e.Slice(2, 2), type);
                BinaryPrimitives.WriteUInt32LittleEndian(e.Slice(4, 4), count);
                if (data.Length <= 4) data.CopyTo(e.Slice(8, data.Length));
                else
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(e.Slice(8, 4), (uint)(heapAt + heap.Length));
                    heap.Write(data, 0, data.Length);
                    if ((data.Length & 1) == 1) heap.WriteByte(0);
                }
            }
            o.Write(ifd, 0, ifdLen);
            heap.Position = 0;
            heap.CopyTo(o);

            var row = new byte[bytesPerRow];
            for (int y = 0; y < r.H; y++)
            {
                for (int x = 0; x < r.W; x++)
                    for (int p = 0; p < spp; p++)
                    {
                        float v = r.Planes[p][y * r.W + x];
                        ushort level = (ushort)Math.Round(Math.Clamp(v, 0f, 1f) * 65535.0);
                        BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan((x * spp + p) * 2, 2), level);
                    }
                o.Write(row, 0, row.Length);
            }
        }

        /// <summary>An ASCII tag's bytes: the text and its NUL.</summary>
        /// <param name="text">The text; characters outside ASCII become '?'.</param>
        /// <returns>The bytes.</returns>
        static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text + "\0");

        /// <summary>A LONG's four bytes, little-endian.</summary>
        /// <param name="v">The value.</param>
        /// <returns>The bytes.</returns>
        static byte[] U32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return b; }

        /// <summary>A SHORT's two bytes, little-endian.</summary>
        /// <param name="v">The value.</param>
        /// <returns>The bytes.</returns>
        static byte[] U16(ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, v); return b; }

        /// <summary>Several equal SHORTs.</summary>
        /// <param name="n">How many.</param>
        /// <param name="v">The value of each.</param>
        /// <returns>The bytes.</returns>
        static byte[] Shorts(int n, ushort v)
        {
            var b = new byte[2 * n];
            for (int i = 0; i < n; i++) BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(2 * i, 2), v);
            return b;
        }

        /// <summary>A RATIONAL's eight bytes: numerator, denominator.</summary>
        /// <param name="num">The numerator.</param>
        /// <param name="den">The denominator.</param>
        /// <returns>The bytes.</returns>
        static byte[] Rational(uint num, uint den)
        {
            var b = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0, 4), num);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4, 4), den);
            return b;
        }
    }
}
