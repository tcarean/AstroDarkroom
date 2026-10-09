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

using System.IO.Compression;

namespace LightsCheck.Core
{
    internal static class PngWriter
    {
        internal static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>
        /// Harta divergenta: albastru (sub mid) -> alb (mid) -> rosu (peste mid), cu capetele
        /// lo si hi date de apelant.
        /// </summary>
        internal static (byte r, byte g, byte b) Colormap(double v, double lo, double mid, double hi)
        {
            double t;
            byte r, g, b;
            if (v < mid)
            {
                t = (v - lo) / Math.Max(mid - lo, 1e-12); t = Math.Clamp(t, 0, 1);
                // albastru -> alb
                r = (byte)(255 * t); g = (byte)(255 * t); b = 255;
            }
            else
            {
                t = (v - mid) / Math.Max(hi - mid, 1e-12); t = Math.Clamp(t, 0, 1);
                // alb -> rosu
                r = 255; g = (byte)(255 * (1 - t)); b = (byte)(255 * (1 - t));
            }
            return (r, g, b);
        }

        internal static void Write(string path, byte[] rgb, int w, int h)
            => File.WriteAllBytes(path, Encode(rgb, w, h));

        /// <summary>
        /// Un raster gri, scris ca RGB cu cele trei componente egale. Encoderul ramane cu
        /// un singur tip de culoare; a adauga tipul 0 pentru vederile egalizate ar fi
        /// insemnat o a doua cale prin IHDR si prin decoderul din teste, pentru octeti pe
        /// care nimeni nu-i numara.
        /// </summary>
        internal static byte[] EncodeGrey(byte[] grey, int w, int h)
        {
            var rgb = new byte[grey.Length * 3];
            for (int i = 0; i < grey.Length; i++)
            {
                rgb[i * 3] = grey[i]; rgb[i * 3 + 1] = grey[i]; rgb[i * 3 + 2] = grey[i];
            }
            return Encode(rgb, w, h);
        }

        /// <summary>Encodeaza in memorie - punctul de intrare real, ca sa fie testabil fara disc.</summary>
        internal static byte[] Encode(byte[] rgb, int w, int h)
        {
            using var outp = new MemoryStream();
            outp.Write(Signature, 0, Signature.Length);

            var ihdr = new byte[13];
            WriteBE(ihdr, 0, w); WriteBE(ihdr, 4, h);
            ihdr[8] = 8;   // bit depth
            ihdr[9] = 2;   // color type RGB
            ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;   // deflate, filtrare adaptiva, neintretesut
            WriteChunk(outp, "IHDR", ihdr);

            // IDAT: fiecare rand precedat de filter byte 0 (None), apoi zlib
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

        static void WriteBE(byte[] b, int o, int v)
        { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        static void WriteChunk(MemoryStream s, string type, byte[] data)
        {
            var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len, 0, 4);
            var tb = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(tb, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = Crc32(tb, data);
            var cb = new byte[4]; WriteBE(cb, 0, (int)crc); s.Write(cb, 0, 4);
        }

        // Construita o singura data, la initializarea tipului, si publicata gata umpluta.
        // A fost un camp initializat lenes ("if null, umple"), ceea ce era o cursa reala,
        // nu teoretica: campul se atribuia INAINTE de umplere, deci un al doilea fir care
        // intra in acel moment vedea tabela ne-nula, o citea cu zerouri si scria un CRC
        // gresit - adica un PNG corupt. Reprodus: 8 fire, 2000 de runde, 5261 de CRC-uri
        // gresite. In fereastra e chiar posibil, fiindca tabul Ratio poate encoda in timp
        // ce Structura inca ruleaza. Un initializator static are garantia CLR-ului ca
        // nimeni nu vede tabela pana nu e gata.
        static readonly uint[] _crcTable = BuildCrcTable();

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

        internal static uint Crc32(byte[] type, byte[] data)
        {
            uint crc = 0xffffffff;
            foreach (byte bb in type) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            foreach (byte bb in data) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            return crc ^ 0xffffffff;
        }
    }
}
