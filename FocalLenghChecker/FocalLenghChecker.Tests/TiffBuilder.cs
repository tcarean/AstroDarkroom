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

namespace FocalLenghChecker.Tests;

/// <summary>
/// Builds the smallest little-endian TIFF that still carries an EXIF sub-IFD.
/// A .NEF is a TIFF underneath, so this exercises the real parsing path without
/// needing a 25 MB binary fixture checked in beside the tests.
/// </summary>
internal static class TiffBuilder
{
    private const ushort TypeShort = 3;
    private const ushort TypeLong = 4;
    private const ushort TypeRational = 5;

    private const ushort TagImageWidth = 0x0100;
    private const ushort TagImageHeight = 0x0101;
    private const ushort TagExifIfdPointer = 0x8769;
    private const ushort TagFocalLength = 0x920A;

    private const uint Ifd0Offset = 8;
    private const int Ifd0EntryCount = 3;

    /// <summary>A TIFF whose EXIF sub-IFD records FocalLength as the given rational.</summary>
    public static byte[] WithFocalLength(uint numerator, uint denominator) =>
        Build(numerator, denominator, includeFocalLength: true);

    /// <summary>A TIFF with a valid EXIF sub-IFD that simply has no FocalLength tag.</summary>
    public static byte[] WithoutFocalLength() =>
        Build(0, 0, includeFocalLength: false);

    private static byte[] Build(uint numerator, uint denominator, bool includeFocalLength)
    {
        var exifEntryCount = includeFocalLength ? 1 : 0;
        var exifOffset = Ifd0Offset + (uint)IfdSize(Ifd0EntryCount);
        var rationalOffset = exifOffset + (uint)IfdSize(exifEntryCount);

        using var buffer = new MemoryStream();

        // BinaryWriter always writes little-endian, which matches the "II" header below.
        using var writer = new BinaryWriter(buffer);

        writer.Write((byte)'I');
        writer.Write((byte)'I');
        writer.Write((ushort)42);
        writer.Write(Ifd0Offset);

        writer.Write((ushort)Ifd0EntryCount);
        WriteEntry(writer, TagImageWidth, TypeShort, 1, 4);
        WriteEntry(writer, TagImageHeight, TypeShort, 1, 4);
        WriteEntry(writer, TagExifIfdPointer, TypeLong, 1, exifOffset);
        writer.Write(0u);   // no IFD1

        writer.Write((ushort)exifEntryCount);

        if (includeFocalLength)
            WriteEntry(writer, TagFocalLength, TypeRational, 1, rationalOffset);

        writer.Write(0u);   // no IFD after the EXIF one

        if (includeFocalLength)
        {
            writer.Write(numerator);
            writer.Write(denominator);
        }

        writer.Flush();

        return buffer.ToArray();
    }

    /// <summary>Entry count, then 12 bytes per entry, then the offset of the next IFD.</summary>
    private static int IfdSize(int entryCount) => 2 + 12 * entryCount + 4;

    private static void WriteEntry(BinaryWriter writer, ushort tag, ushort type, uint count, uint valueOrOffset)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(count);
        writer.Write(valueOrOffset);
    }
}
