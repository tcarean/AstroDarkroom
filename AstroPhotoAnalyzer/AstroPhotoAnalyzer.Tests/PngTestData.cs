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
using System.IO.Compression;
using System.Text;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// Un PNG 16 biti RGB scris DE MANA, cu un chunk iCCP - exact forma in care era
/// imaginea de acceptanta (de_printat.png, cu profilul sRGB-elle in iCCP). Encoderul
/// ImageSharp nu scrie iCCP in 3.1 (masurat), deci citirea profilului din PNG nu se
/// poate testa prin el.
/// </summary>
internal static class PngTestData
{
    static readonly uint[] CrcTable = MakeCrcTable();

    static uint[] MakeCrcTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    static uint Crc32(byte[] data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    static byte[] Zlib(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw, 0, raw.Length);
        return ms.ToArray();
    }

    static void Chunk(List<byte> into, string type, byte[] data)
    {
        var len = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
        into.AddRange(len);
        byte[] typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        into.AddRange(typeAndData);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeAndData));
        into.AddRange(crc);
    }

    internal static byte[] Build16Rgb(int width, int height, Func<int, int, int, int> level16, byte[]? icc = null, string iccName = "test")
    {
        var png = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
        ihdr[8] = 16;   // biti pe esantion
        ihdr[9] = 2;    // RGB
        Chunk(png, "IHDR", ihdr);

        if (icc != null)
        {
            // iCCP: numele profilului, NUL, metoda de compresie (0 = zlib), profilul comprimat.
            byte[] name = Encoding.ASCII.GetBytes(iccName);
            byte[] body = name.Concat(new byte[] { 0, 0 }).Concat(Zlib(icc)).ToArray();
            Chunk(png, "iCCP", body);
        }

        var raw = new byte[height * (1 + width * 6)];
        int p = 0;
        for (int y = 0; y < height; y++)
        {
            raw[p++] = 0;   // filtru None pe fiecare rand
            for (int x = 0; x < width; x++)
                for (int c = 0; c < 3; c++)
                {
                    BinaryPrimitives.WriteUInt16BigEndian(raw.AsSpan(p, 2), (ushort)level16(x, y, c));
                    p += 2;
                }
        }
        Chunk(png, "IDAT", Zlib(raw));
        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }
}
