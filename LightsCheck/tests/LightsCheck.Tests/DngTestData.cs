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
using System.Text;

namespace LightsCheck.Tests;

/// <summary>
/// Builds a minimal camera raw in memory: an uncompressed 16-bit DNG of one image, little-endian,
/// which libraw decodes as it decodes a camera's file. No real raw is committed to the repo
/// (the smallest NEF is 18 MB), and this is the one raw the gate decodes: every pixel comes
/// from a formula, and the black level, its repeat pattern and the colour mosaic are the test's
/// to choose.
/// </summary>
internal static class DngTestData
{
    private const ushort TypeByte = 1, TypeAscii = 2, TypeShort = 3, TypeLong = 4, TypeSRational = 10;

    /// <summary>
    /// The X-Trans mosaic of Fujifilm, 6 x 6, as colour indices (0 red, 1 green, 2 blue), row by
    /// row.
    /// </summary>
    private static readonly byte[] XTrans =
    [
        1, 1, 0, 1, 1, 2,
        1, 1, 2, 1, 1, 0,
        2, 0, 1, 0, 2, 1,
        1, 1, 2, 1, 1, 0,
        1, 1, 0, 1, 1, 2,
        0, 2, 1, 2, 0, 1,
    ];

    /// <summary>The Bayer mosaic RGGB as colour indices, row by row.</summary>
    private static readonly byte[] Rggb = [0, 1, 1, 2];

    /// <summary>
    /// The bytes of the DNG. <paramref name="pixel"/> gives each raw value, 0 to 65535, black
    /// included. <paramref name="black"/> is the <c>BlackLevel</c> tag: one value, or
    /// <paramref name="blackRows"/> x <paramref name="blackColumns"/> of them, row by row, with
    /// the <c>BlackLevelRepeatDim</c> tag written when either is given. The mosaic is RGGB, or
    /// X-Trans with <paramref name="xTrans"/>.
    /// </summary>
    internal static byte[] Build(int width, int height, Func<int, int, int> pixel, int[] black, int blackRows = 0, int blackColumns = 0,
        int white = 16383, bool xTrans = false)
    {
        byte[] pixels = new byte[width * height * 2];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan((y * width + x) * 2), (ushort)pixel(x, y));
            }
        }

        // The tags of the one image directory, in ascending order as TIFF asks; the strip's
        // offset (273) is filled in once it is known.
        List<(ushort Tag, ushort Type, int Count, byte[] Data)> tags =
        [
            (254, TypeLong, 1, Longs(0)),                 // NewSubFileType: the full image
            (256, TypeLong, 1, Longs(width)),
            (257, TypeLong, 1, Longs(height)),
            (258, TypeShort, 1, Shorts(16)),              // BitsPerSample
            (259, TypeShort, 1, Shorts(1)),               // Compression: none
            (262, TypeShort, 1, Shorts(32803)),           // PhotometricInterpretation: CFA
            Ascii(271, "Test"),
            Ascii(272, "Test DNG"),
            (273, TypeLong, 1, Longs(0)),                 // StripOffsets
            (274, TypeShort, 1, Shorts(1)),               // Orientation
            (277, TypeShort, 1, Shorts(1)),               // SamplesPerPixel
            (278, TypeLong, 1, Longs(height)),            // RowsPerStrip
            (279, TypeLong, 1, Longs(pixels.Length)),     // StripByteCounts
            (284, TypeShort, 1, Shorts(1)),               // PlanarConfiguration
            (33421, TypeShort, 2, xTrans ? Shorts(6, 6) : Shorts(2, 2)),             // CFARepeatPatternDim
            (33422, TypeByte, xTrans ? XTrans.Length : Rggb.Length, xTrans ? XTrans : Rggb),   // CFAPattern
            (50706, TypeByte, 4, [1, 4, 0, 0]),           // DNGVersion
            (50707, TypeByte, 4, [1, 1, 0, 0]),           // DNGBackwardVersion
            Ascii(50708, "Test DNG"),                     // UniqueCameraModel
        ];
        if (blackRows > 0 || blackColumns > 0)
        {
            tags.Add((50713, TypeShort, 2, Shorts(blackRows, blackColumns)));        // BlackLevelRepeatDim
        }

        tags.Add((50714, TypeShort, black.Length, Shorts(black)));                   // BlackLevel
        tags.Add((50717, TypeShort, 1, Shorts(white)));                              // WhiteLevel
        tags.Add((50721, TypeSRational, 9, SRationals(1, 0, 0, 0, 1, 0, 0, 0, 1)));  // ColorMatrix1
        tags.Add((50778, TypeShort, 1, Shorts(21)));                                 // CalibrationIlluminant1: D65

        const int directoryOffset = 8;
        int extraOffset = directoryOffset + 2 + 12 * tags.Count + 4;
        using MemoryStream extra = new();
        byte[] directory = new byte[12 * tags.Count];
        for (int i = 0; i < tags.Count; i++)
        {
            (ushort tag, ushort type, int count, byte[] data) = tags[i];
            Span<byte> entry = directory.AsSpan(12 * i, 12);
            BinaryPrimitives.WriteUInt16LittleEndian(entry, tag);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], type);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], (uint)count);
            if (data.Length <= 4)
            {
                data.CopyTo(entry[8..]);
                continue;
            }

            // A value longer than four bytes lives after the directory, on an even offset.
            if (extra.Length % 2 == 1)
            {
                extra.WriteByte(0);
            }

            BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], (uint)(extraOffset + extra.Length));
            extra.Write(data);
        }

        if (extra.Length % 2 == 1)
        {
            extra.WriteByte(0);
        }

        int strip = extraOffset + (int)extra.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(tags.FindIndex(t => t.Tag == 273) * 12 + 8), (uint)strip);

        using MemoryStream file = new();
        file.Write("II"u8);
        file.Write(Shorts(42));
        file.Write(Longs(directoryOffset));
        file.Write(Shorts(tags.Count));
        file.Write(directory);
        file.Write(Longs(0));
        extra.WriteTo(file);
        file.Write(pixels);
        return file.ToArray();
    }

    /// <summary>An ASCII tag, its text closed by a zero byte.</summary>
    private static (ushort Tag, ushort Type, int Count, byte[] Data) Ascii(ushort tag, string text)
    {
        byte[] data = [.. Encoding.ASCII.GetBytes(text), 0];
        return (tag, TypeAscii, data.Length, data);
    }

    /// <summary>The values as 16-bit little-endian numbers.</summary>
    private static byte[] Shorts(params int[] values)
    {
        byte[] data = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(i * 2), (ushort)values[i]);
        }

        return data;
    }

    /// <summary>The values as 32-bit little-endian numbers.</summary>
    private static byte[] Longs(params int[] values)
    {
        byte[] data = new byte[values.Length * 4];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i * 4), (uint)values[i]);
        }

        return data;
    }

    /// <summary>The whole numbers as signed rationals over 1.</summary>
    private static byte[] SRationals(params int[] values)
    {
        byte[] data = new byte[values.Length * 8];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * 8), values[i]);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * 8 + 4), 1);
        }

        return data;
    }
}
