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

using System.Buffers.Binary;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// A 16-bit RGB TIFF written BY HAND, uncompressed, little-endian, with a single strip and
/// optionally an ICC tag.
///
/// Why not through ImageSharp: its TIFF encoder in 3.1 writes 8 bits per sample even when
/// asked for Bit48 (measured), so it cannot produce the file Siril writes and the tool must
/// read. A file built from a formula is also the more honest test: it checks the decoding,
/// not the encoder-decoder symmetry of one and the same package.
/// </summary>
internal static class TiffTestData
{
    const ushort Short = 3, Long = 4, Undefined = 7;

    internal static byte[] Build16Rgb(int width, int height, Func<int, int, int, int> level16, byte[]? icc = null)
    {
        var entries = new List<(ushort Tag, ushort Type, uint Count, uint ValueOrOffset)>();
        int entryCount = icc == null ? 10 : 11;
        int ifdOffset = 8;
        int ifdLen = 2 + entryCount * 12 + 4;
        int bpsOffset = ifdOffset + ifdLen;          // 3 x SHORT = 6 bytes, they do not fit inline
        int iccOffset = bpsOffset + 6;
        int iccLen = icc?.Length ?? 0;
        int pixelsOffset = iccOffset + iccLen;
        if (pixelsOffset % 2 == 1) pixelsOffset++;    // TIFF offsets are even
        int pixelsLen = width * height * 3 * 2;

        entries.Add((256, Long, 1, (uint)width));
        entries.Add((257, Long, 1, (uint)height));
        entries.Add((258, Short, 3, (uint)bpsOffset));
        entries.Add((259, Short, 1, 1));              // Compression = none
        entries.Add((262, Short, 1, 2));              // Photometric = RGB
        entries.Add((273, Long, 1, (uint)pixelsOffset));
        entries.Add((277, Short, 1, 3));              // SamplesPerPixel
        entries.Add((278, Long, 1, (uint)height));    // RowsPerStrip
        entries.Add((279, Long, 1, (uint)pixelsLen));
        entries.Add((284, Short, 1, 1));              // PlanarConfiguration = chunky
        if (icc != null) entries.Add((34675, Undefined, (uint)iccLen, (uint)iccOffset));

        var b = new byte[pixelsOffset + pixelsLen];
        b[0] = (byte)'I'; b[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(2, 2), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4, 4), (uint)ifdOffset);

        int p = ifdOffset;
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(p, 2), (ushort)entryCount); p += 2;
        foreach (var e in entries)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(p, 2), e.Tag);
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(p + 2, 2), e.Type);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(p + 4, 4), e.Count);
            // A single SHORT sits inline, in the first two bytes of the value field.
            if (e.Type == Short && e.Count == 1)
                BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(p + 8, 2), (ushort)e.ValueOrOffset);
            else
                BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(p + 8, 4), e.ValueOrOffset);
            p += 12;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(p, 4), 0);   // no other IFD

        for (int c = 0; c < 3; c++)
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(bpsOffset + c * 2, 2), 16);
        icc?.CopyTo(b, iccOffset);

        p = pixelsOffset;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                for (int c = 0; c < 3; c++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(p, 2), (ushort)level16(x, y, c));
                    p += 2;
                }
        return b;
    }
}
