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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// Minimal ICC profiles, built by hand: only the header, the table with a single tag and
/// the 'desc' tag - in the v2 form (textDescriptionType) or the v4 one (mluc). They are not
/// valid profiles for a colour conversion; they are exactly as much as IccProfileInfo reads.
/// </summary>
internal static class IccTestData
{
    static byte[] Assemble(byte[] tag)
    {
        const int headerLen = 128, tableLen = 4 + 12;
        int tagOff = headerLen + tableLen;
        int total = tagOff + tag.Length;
        var icc = new byte[total];
        BinaryPrimitives.WriteUInt32BigEndian(icc.AsSpan(0, 4), (uint)total);
        Encoding.ASCII.GetBytes("acsp").CopyTo(icc, 36);
        BinaryPrimitives.WriteUInt32BigEndian(icc.AsSpan(128, 4), 1);
        Encoding.ASCII.GetBytes("desc").CopyTo(icc, 132);
        BinaryPrimitives.WriteUInt32BigEndian(icc.AsSpan(136, 4), (uint)tagOff);
        BinaryPrimitives.WriteUInt32BigEndian(icc.AsSpan(140, 4), (uint)tag.Length);
        tag.CopyTo(icc, tagOff);
        return icc;
    }

    /// <summary>
    /// A v2 profile: 'desc' of type textDescriptionType, with the ASCII string and its NUL.
    /// </summary>
    internal static byte[] V2(string description)
    {
        byte[] text = Encoding.ASCII.GetBytes(description + "\0");
        int len = 12 + text.Length;
        int padded = (len + 3) / 4 * 4;
        var tag = new byte[padded];
        Encoding.ASCII.GetBytes("desc").CopyTo(tag, 0);
        BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(8, 4), (uint)text.Length);
        text.CopyTo(tag, 12);
        return Assemble(tag);
    }

    /// <summary>A v4 profile: 'desc' of type mluc, a single en-US record in UTF-16BE.</summary>
    internal static byte[] V4(string description)
    {
        byte[] text = Encoding.BigEndianUnicode.GetBytes(description);
        const int recOff = 28;
        int len = recOff + text.Length;
        int padded = (len + 3) / 4 * 4;
        var tag = new byte[padded];
        Encoding.ASCII.GetBytes("mluc").CopyTo(tag, 0);
        BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(8, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(12, 4), 12);
        Encoding.ASCII.GetBytes("enUS").CopyTo(tag, 16);
        BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(20, 4), (uint)text.Length);
        BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(24, 4), recOff);
        text.CopyTo(tag, recOff);
        return Assemble(tag);
    }

    /// <summary>A profile without the 'desc' tag - it has a 'wtpt' instead.</summary>
    internal static byte[] WithoutDescription()
    {
        byte[] icc = V2("x");
        Encoding.ASCII.GetBytes("wtpt").CopyTo(icc, 132);
        return icc;
    }
}
