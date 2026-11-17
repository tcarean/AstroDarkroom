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

    /// <summary>
    /// The same file with one more directory after its last, holding no image: only the tag
    /// 52545 with <paramref name="payload"/>. That is what Lightroom Classic 16 writes after the
    /// image on export for its Content Credentials (measured on the export of 2026-11-17, ADR
    /// 0111). The file must be a little-endian classic TIFF; the payload longer than 4 bytes,
    /// so that it sits outside its entry.
    /// </summary>
    internal static byte[] WithADirectoryWithoutAnImage(byte[] tiff, byte[] payload)
    {
        if (payload.Length <= 4) throw new ArgumentException("the payload must be longer than 4 bytes", nameof(payload));
        int payloadOffset = tiff.Length + tiff.Length % 2;
        int ifd = payloadOffset + payload.Length + payload.Length % 2;
        var b = new byte[ifd + 2 + 12 + 4];
        tiff.CopyTo(b, 0);
        payload.CopyTo(b, payloadOffset);

        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(ifd, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(ifd + 2, 2), 52545);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(ifd + 4, 2), Undefined);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(ifd + 6, 4), (uint)payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(ifd + 10, 4), (uint)payloadOffset);
        // The four bytes after the entry stay 0: no other IFD.
        ChainAfterTheLast(b, ifd);
        return b;
    }

    /// <summary>
    /// The same file with a second page: a copy of the first directory, <paramref name="rows"/>
    /// high, over the first rows of the first page's strip. For a file of
    /// <see cref="Build16Rgb"/> (one strip, the sizes as LONG), with fewer rows than the first
    /// page, so that the raster's height says which page was read.
    /// </summary>
    internal static byte[] WithASecondPage(byte[] tiff, int rows)
    {
        int first = (int)BinaryPrimitives.ReadUInt32LittleEndian(tiff.AsSpan(4, 4));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan(first, 2));
        int len = 2 + count * 12 + 4;
        int ifd = tiff.Length + tiff.Length % 2;
        var b = new byte[ifd + len];
        tiff.CopyTo(b, 0);
        Array.Copy(tiff, first, b, ifd, len);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(ifd + len - 4, 4), 0);

        uint height = 0, stripBytes = 0;
        for (int i = 0; i < count; i++)
        {
            int e = ifd + 2 + i * 12;
            ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(e, 2));
            if (tag == 257) height = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(e + 8, 4));
            if (tag == 279) stripBytes = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(e + 8, 4));
        }
        if (rows < 1 || rows >= height) throw new ArgumentException("the second page must have fewer rows than the first", nameof(rows));
        for (int i = 0; i < count; i++)
        {
            int e = ifd + 2 + i * 12;
            ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(e, 2));
            if (tag == 257 || tag == 278) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(e + 8, 4), (uint)rows);
            if (tag == 279) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(e + 8, 4), stripBytes / height * (uint)rows);
        }
        ChainAfterTheLast(b, ifd);
        return b;
    }

    /// <summary>
    /// Makes the directory at <paramref name="ifd"/> the one after the file's last: follows the
    /// "next" pointers from the header and writes the offset into the first that is 0.
    /// </summary>
    static void ChainAfterTheLast(byte[] b, int ifd)
    {
        if (b[0] != (byte)'I' || b[1] != (byte)'I' || BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(2, 2)) != 42)
            throw new ArgumentException("a little-endian classic TIFF is expected");
        int at = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4, 4));
        while (true)
        {
            int next = at + 2 + BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at, 2)) * 12;
            int following = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(next, 4));
            if (following == 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(next, 4), (uint)ifd);
                return;
            }
            at = following;
        }
    }
}
