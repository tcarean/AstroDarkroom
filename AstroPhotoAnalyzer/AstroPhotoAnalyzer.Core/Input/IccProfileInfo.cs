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

// IccProfileInfo.cs - the name of an ICC profile, from its 'desc' tag.
//
// It is read by hand, not through ImageSharp's objects, for a simple reason: all we want is a
// string to show ("sRGB IEC61966-2.1", "sRGB-elle-V2-srgbtrc.icc"), and the tag's structure is
// about thirty lines - textDescriptionType in the v2 profiles, mluc in v4. Nothing else in the
// profile interests us: the tool does not convert colours.
//
// Any departure from the format returns null, not an exception: an odd profile is a fact to
// report ("profil fara nume"), not a reason for the analysis to fail.

using System;
using System.Buffers.Binary;
using System.Text;

namespace AstroPhotoAnalyzer
{
    /// <summary>Reads the name of an ICC profile from its 'desc' tag, by hand.</summary>
    internal static class IccProfileInfo
    {
        /// <summary>
        /// The profile's name, from the 'desc' tag: textDescriptionType in a v2 profile, the first
        /// record of mluc in v4. A missing, malformed or nameless profile gives null rather than
        /// an exception.
        /// </summary>
        /// <param name="icc">The profile's bytes; null is allowed.</param>
        /// <returns>The name, trimmed; null when there is none to read.</returns>
        internal static string Describe(byte[] icc)
        {
            try
            {
                if (icc == null || icc.Length < 132) return null;
                if (Encoding.ASCII.GetString(icc, 36, 4) != "acsp") return null;

                uint count = BinaryPrimitives.ReadUInt32BigEndian(icc.AsSpan(128, 4));
                if (count > 1000) return null;
                for (uint t = 0; t < count; t++)
                {
                    int e = 132 + (int)t * 12;
                    if (e + 12 > icc.Length) return null;
                    string sig = Encoding.ASCII.GetString(icc, e, 4);
                    if (sig != "desc") continue;
                    int off = (int)BinaryPrimitives.ReadUInt32BigEndian(icc.AsSpan(e + 4, 4));
                    int size = (int)BinaryPrimitives.ReadUInt32BigEndian(icc.AsSpan(e + 8, 4));
                    if (off < 0 || size < 12 || off + size > icc.Length) return null;
                    return Clean(ReadTextTag(icc, off, size));
                }
                return null;
            }
            catch (ArgumentOutOfRangeException) { return null; }
            catch (DecoderFallbackException) { return null; }
        }

        /// <summary>The text of a 'desc' (v2) or 'mluc' (v4) tag; null for another type.</summary>
        /// <param name="icc">The profile's bytes.</param>
        /// <param name="off">The tag's offset in the profile, in bytes.</param>
        /// <param name="size">
        /// The tag's size, in bytes, as the tag table gives it; not read here.
        /// </param>
        /// <returns>
        /// The raw text, NULs and spaces included; null for another type, no record, or a length
        /// that runs past the profile.
        /// </returns>
        static string ReadTextTag(byte[] icc, int off, int size)
        {
            string type = Encoding.ASCII.GetString(icc, off, 4);
            switch (type)
            {
                case "desc":
                    {
                        // textDescriptionType (v2): sig, reserved, ASCII length (with the NUL),
                        // string.
                        int len = (int)BinaryPrimitives.ReadUInt32BigEndian(icc.AsSpan(off + 8, 4));
                        if (len <= 0 || off + 12 + len > icc.Length) return null;
                        return Encoding.ASCII.GetString(icc, off + 12, len);
                    }
                case "mluc":
                    {
                        // multiLocalizedUnicodeType (v4): sig, reserved, number of records, the
                        // size of a record (12), then the records: language, country, length in
                        // bytes, offset from the start of the tag. UTF-16BE.
                        uint n = BinaryPrimitives.ReadUInt32BigEndian(icc.AsSpan(off + 8, 4));
                        if (n == 0) return null;
                        int recLen = (int)BinaryPrimitives.ReadUInt32BigEndian(icc.AsSpan(off + 16, 4));
                        if (recLen < 12) return null;
                        int len = (int)BinaryPrimitives.ReadUInt32BigEndian(icc.AsSpan(off + 20, 4));
                        int rel = (int)BinaryPrimitives.ReadUInt32BigEndian(icc.AsSpan(off + 24, 4));
                        if (len <= 0 || off + rel + len > icc.Length) return null;
                        return Encoding.BigEndianUnicode.GetString(icc, off + rel, len);
                    }
                default:
                    return null;
            }
        }

        /// <summary>A tag's text without its trailing NULs and its surrounding spaces.</summary>
        /// <param name="s">The raw text; null is allowed.</param>
        /// <returns>The clean text; null when nothing is left.</returns>
        static string Clean(string s)
        {
            if (s == null) return null;
            s = s.TrimEnd('\0').Trim();
            return s.Length == 0 ? null : s;
        }
    }
}
