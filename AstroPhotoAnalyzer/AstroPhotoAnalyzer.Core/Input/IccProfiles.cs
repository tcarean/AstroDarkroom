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

// IccProfiles.cs - the sRGB profile the tool embeds in a TIFF whose input had none (R-177,
// ADR 0083): a v2 display profile built here from the sRGB primaries adapted to D50 and the
// sRGB curve, nothing copied from anyone's file. The tool assumes every input is sRGB (R-096),
// so a file it writes says so.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace AstroPhotoAnalyzer
{
    /// <summary>The ICC profiles the tool writes: the standard sRGB, built once.</summary>
    internal static class IccProfiles
    {
        /// <summary>The sRGB profile: a v2 display profile, matrix and curves.</summary>
        internal static byte[] Srgb { get; } = BuildSrgb();

        /// <summary>
        /// Builds the sRGB profile: 'desc' "sRGB", 'cprt', 'wtpt' D50, the three colorants as the
        /// columns of the Bradford-adapted sRGB matrix (ColorMath), the three curves as 1024
        /// points of the sRGB transfer function.
        /// </summary>
        /// <returns>The profile's bytes.</returns>
        static byte[] BuildSrgb()
        {
            var tags = new List<(string Sig, byte[] Data)>
            {
                ("desc", TextDescription("sRGB")),
                ("cprt", Text("Public domain: the sRGB colour space, as a profile written by AstroPhotoAnalyzer")),
                ("wtpt", Xyz(ColorMath.D50X, ColorMath.D50Y, ColorMath.D50Z)),
                ("rXYZ", Xyz(0.4360747, 0.2225045, 0.0139322)),
                ("gXYZ", Xyz(0.3850649, 0.7168786, 0.0971045)),
                ("bXYZ", Xyz(0.1430804, 0.0606169, 0.7141733)),
                ("rTRC", Curve()),
                ("gTRC", Curve()),
                ("bTRC", Curve()),
            };
            int tableLen = 4 + 12 * tags.Count;
            int at = 128 + tableLen;
            var offsets = new int[tags.Count];
            int total = at;
            for (int i = 0; i < tags.Count; i++) { offsets[i] = total; total += (tags[i].Data.Length + 3) & ~3; }
            var p = new byte[total];
            BinaryPrimitives.WriteUInt32BigEndian(p.AsSpan(0, 4), (uint)total);
            BinaryPrimitives.WriteUInt32BigEndian(p.AsSpan(8, 4), 0x02100000);   // version 2.1
            Encoding.ASCII.GetBytes("mntr").CopyTo(p, 12);
            Encoding.ASCII.GetBytes("RGB ").CopyTo(p, 16);
            Encoding.ASCII.GetBytes("XYZ ").CopyTo(p, 20);
            Encoding.ASCII.GetBytes("acsp").CopyTo(p, 36);
            // The illuminant, D50, at 68: the PCS's.
            Xyz(ColorMath.D50X, ColorMath.D50Y, ColorMath.D50Z).AsSpan(8, 12).CopyTo(p.AsSpan(68, 12));
            BinaryPrimitives.WriteUInt32BigEndian(p.AsSpan(128, 4), (uint)tags.Count);
            for (int i = 0; i < tags.Count; i++)
            {
                Encoding.ASCII.GetBytes(tags[i].Sig).CopyTo(p, 132 + 12 * i);
                BinaryPrimitives.WriteUInt32BigEndian(p.AsSpan(136 + 12 * i, 4), (uint)offsets[i]);
                BinaryPrimitives.WriteUInt32BigEndian(p.AsSpan(140 + 12 * i, 4), (uint)tags[i].Data.Length);
                tags[i].Data.CopyTo(p, offsets[i]);
            }
            return p;
        }

        /// <summary>A textDescriptionType tag: the ASCII text, no Unicode, no ScriptCode.</summary>
        /// <param name="text">The text, ASCII.</param>
        /// <returns>The tag's bytes.</returns>
        static byte[] TextDescription(string text)
        {
            byte[] ascii = Encoding.ASCII.GetBytes(text + "\0");
            var t = new byte[12 + ascii.Length + 4 + 4 + 2 + 1 + 67];
            Encoding.ASCII.GetBytes("desc").CopyTo(t, 0);
            BinaryPrimitives.WriteUInt32BigEndian(t.AsSpan(8, 4), (uint)ascii.Length);
            ascii.CopyTo(t, 12);
            return t;
        }

        /// <summary>A textType tag.</summary>
        /// <param name="text">The text, ASCII.</param>
        /// <returns>The tag's bytes.</returns>
        static byte[] Text(string text)
        {
            byte[] ascii = Encoding.ASCII.GetBytes(text + "\0");
            var t = new byte[8 + ascii.Length];
            Encoding.ASCII.GetBytes("text").CopyTo(t, 0);
            ascii.CopyTo(t, 8);
            return t;
        }

        /// <summary>An XYZType tag with one number.</summary>
        /// <param name="x">X.</param>
        /// <param name="y">Y.</param>
        /// <param name="z">Z.</param>
        /// <returns>The tag's bytes, s15Fixed16 each.</returns>
        static byte[] Xyz(double x, double y, double z)
        {
            var t = new byte[20];
            Encoding.ASCII.GetBytes("XYZ ").CopyTo(t, 0);
            BinaryPrimitives.WriteInt32BigEndian(t.AsSpan(8, 4), (int)Math.Round(x * 65536));
            BinaryPrimitives.WriteInt32BigEndian(t.AsSpan(12, 4), (int)Math.Round(y * 65536));
            BinaryPrimitives.WriteInt32BigEndian(t.AsSpan(16, 4), (int)Math.Round(z * 65536));
            return t;
        }

        /// <summary>A curveType tag of 1024 points: the sRGB transfer function, encoded to linear.</summary>
        /// <returns>The tag's bytes.</returns>
        static byte[] Curve()
        {
            const int n = 1024;
            var t = new byte[12 + 2 * n];
            Encoding.ASCII.GetBytes("curv").CopyTo(t, 0);
            BinaryPrimitives.WriteUInt32BigEndian(t.AsSpan(8, 4), n);
            for (int i = 0; i < n; i++)
                BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(12 + 2 * i, 2), (ushort)Math.Round(ColorMath.SrgbToLinear(i / (n - 1.0)) * 65535));
            return t;
        }
    }
}
