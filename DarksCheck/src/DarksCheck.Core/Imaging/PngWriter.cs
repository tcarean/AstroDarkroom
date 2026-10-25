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

#nullable disable // copied from FlatCheck 2.3.0 unchanged, which compiles without nullable annotations (ADR 0003)
// The code is the copy's; the comments are DarksCheck's own (ADR 0019).

// PngWriter.cs - our own PNG encoder (no dependencies). DarksCheck writes its binned grey maps
// with EncodeGrey (`--out`, the window's export); the diverging colour scale and Write are not
// called here: the file is a copy (ADR 0003).
//
// The encoder writes the signature + IHDR (8 bits, color type 2 = RGB) + a single IDAT (each row
// preceded by filter byte 0, deflated through ZLibStream) + IEND, with the CRC-32 computed by
// hand.

using System.IO.Compression;

namespace DarksCheck.Core
{
    /// <summary>
    /// Encodes RGB and grey rasters as PNG, and gives the colour of a value on the
    /// blue-white-red diverging scale.
    /// </summary>
    internal static class PngWriter
    {
        internal static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>
        /// The colour of one value on the diverging scale: blue at <paramref name="lo"/>, white
        /// at <paramref name="mid"/>, red at <paramref name="hi"/>, linear in between and clipped
        /// outside.
        /// </summary>
        /// <param name="v">The value.</param>
        /// <param name="lo">The value drawn full blue.</param>
        /// <param name="mid">The value drawn white.</param>
        /// <param name="hi">The value drawn full red.</param>
        /// <returns>The colour's components, 0..255.</returns>
        internal static (byte r, byte g, byte b) Colormap(double v, double lo, double mid, double hi)
        {
            double t;
            byte r, g, b;
            if (v < mid)
            {
                t = (v - lo) / Math.Max(mid - lo, 1e-12); t = Math.Clamp(t, 0, 1);
                // blue -> white
                r = (byte)(255 * t); g = (byte)(255 * t); b = 255;
            }
            else
            {
                t = (v - mid) / Math.Max(hi - mid, 1e-12); t = Math.Clamp(t, 0, 1);
                // white -> red
                r = 255; g = (byte)(255 * (1 - t)); b = (byte)(255 * (1 - t));
            }
            return (r, g, b);
        }

        /// <summary>Writes an RGB raster to disk as a PNG.</summary>
        /// <param name="path">The PNG file to write; overwritten if it exists.</param>
        /// <param name="rgb">The pixels, w*h*3 bytes, interleaved R,G,B, rows top down.</param>
        /// <param name="w">The width in pixels.</param>
        /// <param name="h">The height in pixels.</param>
        internal static void Write(string path, byte[] rgb, int w, int h)
            => File.WriteAllBytes(path, Encode(rgb, w, h));

        /// <summary>
        /// A grey raster, written as RGB with the three components equal: the encoder keeps a
        /// single colour type rather than a second path through IHDR for grey (type 0). The
        /// maps of <c>--out</c> are written this way.
        /// </summary>
        /// <param name="grey">The pixels, w*h bytes, rows top down.</param>
        /// <param name="w">The width in pixels.</param>
        /// <param name="h">The height in pixels.</param>
        /// <returns>The PNG file's bytes.</returns>
        internal static byte[] EncodeGrey(byte[] grey, int w, int h)
        {
            var rgb = new byte[grey.Length * 3];
            for (int i = 0; i < grey.Length; i++)
            {
                rgb[i * 3] = grey[i]; rgb[i * 3 + 1] = grey[i]; rgb[i * 3 + 2] = grey[i];
            }
            return Encode(rgb, w, h);
        }

        /// <summary>
        /// Encodes in memory - the real entry point, so that it can be tested without a disk.
        /// </summary>
        /// <param name="rgb">The pixels, w*h*3 bytes, interleaved R,G,B, rows top down.</param>
        /// <param name="w">The width in pixels.</param>
        /// <param name="h">The height in pixels.</param>
        /// <returns>The PNG file's bytes: 8-bit RGB, no filtering, one IDAT.</returns>
        internal static byte[] Encode(byte[] rgb, int w, int h)
        {
            using var outp = new MemoryStream();
            outp.Write(Signature, 0, Signature.Length);

            var ihdr = new byte[13];
            WriteBE(ihdr, 0, w); WriteBE(ihdr, 4, h);
            ihdr[8] = 8;   // bit depth
            ihdr[9] = 2;   // color type RGB
            ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;   // deflate, adaptive filtering, not interlaced
            WriteChunk(outp, "IHDR", ihdr);

            // IDAT: each row preceded by filter byte 0 (None), then zlib
            using var raw = new MemoryStream();
            for (int y = 0; y < h; y++)
            {
                raw.WriteByte(0);
                raw.Write(rgb, y * w * 3, w * 3);
            }
            byte[] rawBytes = raw.ToArray();
            byte[] comp;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                    z.Write(rawBytes, 0, rawBytes.Length);
                comp = ms.ToArray();
            }
            WriteChunk(outp, "IDAT", comp);
            WriteChunk(outp, "IEND", Array.Empty<byte>());
            return outp.ToArray();
        }

        /// <summary>Writes a 32-bit integer big-endian into a buffer.</summary>
        /// <param name="b">The buffer.</param>
        /// <param name="o">The offset of the first byte.</param>
        /// <param name="v">The value.</param>
        static void WriteBE(byte[] b, int o, int v)
        { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        /// <summary>Writes one chunk: its length, its type, its data and its CRC.</summary>
        /// <param name="s">The PNG being written.</param>
        /// <param name="type">The chunk type, four ASCII letters.</param>
        /// <param name="data">The chunk's data.</param>
        static void WriteChunk(MemoryStream s, string type, byte[] data)
        {
            var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len, 0, 4);
            var tb = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(tb, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = Crc32(tb, data);
            var cb = new byte[4]; WriteBE(cb, 0, (int)crc); s.Write(cb, 0, 4);
        }

        // Built once, when the type initialises, and published already filled. It used to be a
        // lazily initialised field ("if null, fill it"), which was a real race, not a theoretical
        // one: the field was assigned BEFORE it was filled, so a second thread coming in at that
        // moment saw the table non-null, read it with zeros and wrote a wrong CRC - that is, a
        // corrupt PNG. Reproduced: 8 threads, 2000 rounds, 5261 wrong CRCs. A static
        // initialiser has the CLR's guarantee that no one sees the table until it is ready, so
        // any caller may encode on any thread.
        static readonly uint[] _crcTable = BuildCrcTable();

        /// <summary>The 256-entry table of the PNG CRC-32 (polynomial 0xedb88320).</summary>
        /// <returns>The table.</returns>
        static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        /// <summary>The CRC-32 of a chunk, over its type and its data, as PNG defines it.</summary>
        /// <param name="type">The chunk type's four bytes.</param>
        /// <param name="data">The chunk's data.</param>
        /// <returns>The CRC.</returns>
        internal static uint Crc32(byte[] type, byte[] data)
        {
            uint crc = 0xffffffff;
            foreach (byte bb in type) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            foreach (byte bb in data) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            return crc ^ 0xffffffff;
        }
    }
}
