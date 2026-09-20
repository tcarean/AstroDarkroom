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

// IccProfileInfo.cs - numele unui profil ICC, din tagul 'desc'.
//
// Se citeste de mana, nu prin obiectele ImageSharp, dintr-un motiv simplu: tot ce vrem
// e un sir de afisat ("sRGB IEC61966-2.1", "sRGB-elle-V2-srgbtrc.icc"), iar structura
// tagului e de vreo treizeci de linii - textDescriptionType in profilele v2, mluc in v4.
// Orice altceva din profil nu ne intereseaza: unealta nu converteste culori.
//
// Orice abatere de la format intoarce null, nu exceptie: un profil ciudat e un fapt de
// raportat ("profil fara nume"), nu un motiv sa cada analiza.

using System;
using System.Buffers.Binary;
using System.Text;

namespace AstroPhotoAnalyzer
{
    internal static class IccProfileInfo
    {
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

        static string ReadTextTag(byte[] icc, int off, int size)
        {
            string type = Encoding.ASCII.GetString(icc, off, 4);
            switch (type)
            {
                case "desc":
                {
                    // textDescriptionType (v2): sig, rezervat, lungime ASCII (cu NUL), sir.
                    int len = (int)BinaryPrimitives.ReadUInt32BigEndian(icc.AsSpan(off + 8, 4));
                    if (len <= 0 || off + 12 + len > icc.Length) return null;
                    return Encoding.ASCII.GetString(icc, off + 12, len);
                }
                case "mluc":
                {
                    // multiLocalizedUnicodeType (v4): sig, rezervat, nr. inregistrari,
                    // marimea unei inregistrari (12), apoi inregistrari: limba, tara,
                    // lungime in octeti, offset fata de inceputul tagului. UTF-16BE.
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

        static string Clean(string s)
        {
            if (s == null) return null;
            s = s.TrimEnd('\0').Trim();
            return s.Length == 0 ? null : s;
        }
    }
}
