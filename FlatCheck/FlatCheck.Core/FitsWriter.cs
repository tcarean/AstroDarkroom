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

// FitsWriter.cs - scriitor FITS propriu, perechea lui FitsReader. Exista pentru un
// singur consumator: modul de netezire, care scrie un al doilea master flat.
//
// Scrie un singur HDU primar, 2D, BITPIX -32 sau 16, big-endian, cu headerul intrarii
// copiat card cu card - minus cele structurale, pe care le rescrie - si cu cateva
// HISTORY la sfarsit. Nu e un scriitor general: nu stie cuburi, nu stie extensii, nu
// stie sa formateze decat cardurile pe care le genereaza el.
//
// Scrie intr-un Stream, nu intoarce byte[], din acelasi motiv pentru care raportul se
// scrie intr-un TextWriter: 96 MB de float n-au de ce sa mai stea o data in memorie.
// Testele ii dau un MemoryStream.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FlatCheck
{
    internal static class FitsWriter
    {
        const int CardSize = 80, BlockSize = 2880;

        /// <summary>
        /// Cuvintele-cheie pe care scriitorul le pune el, deci nu le copiaza din intrare.
        /// XTENSION/PCOUNT/GCOUNT sunt ale unui HDU de extensie; imaginea putea veni de
        /// acolo (FitsReader sare peste un primar gol), dar aici se scrie mereu un primar.
        /// </summary>
        static readonly HashSet<string> Structural = new(StringComparer.Ordinal)
        {
            "SIMPLE", "BITPIX", "NAXIS", "NAXIS1", "NAXIS2", "NAXIS3", "NAXIS4",
            "EXTEND", "BZERO", "BSCALE", "END", "XTENSION", "PCOUNT", "GCOUNT",
        };

        /// <summary>
        /// Scrie <paramref name="img"/> ca HDU primar 2D. <paramref name="bitpix"/> e -32
        /// (float IEEE) sau 16 (short cu BZERO 32768, rotunjit si taiat la 0..65535).
        /// <paramref name="cards"/> sunt cardurile brute de copiat (cele structurale se
        /// sar), <paramref name="history"/> sunt liniile HISTORY de adaugat.
        /// </summary>
        internal static void Write(Stream s, Img img, int bitpix, IReadOnlyList<string> cards, IReadOnlyList<string> history)
        {
            if (bitpix != -32 && bitpix != 16)
                throw new ArgumentException("FitsWriter scrie doar BITPIX -32 sau 16, nu " + bitpix);

            var header = new List<string>
            {
                Logical("SIMPLE", true, "conforms to FITS standard"),
                Number("BITPIX", bitpix, bitpix == 16 ? "16-bit unsigned via BZERO" : "IEEE single float"),
                Number("NAXIS", 2, "number of array dimensions"),
                Number("NAXIS1", img.W, "length of data axis 1"),
                Number("NAXIS2", img.H, "length of data axis 2"),
                Logical("EXTEND", true, ""),
            };
            if (bitpix == 16)
            {
                header.Add(Number("BZERO", 32768, "offset data range to that of unsigned short"));
                header.Add(Number("BSCALE", 1, "default scaling factor"));
            }
            if (cards != null)
                foreach (string card in cards)
                {
                    string key = card.Length >= 8 ? card.Substring(0, 8).Trim() : card.Trim();
                    if (Structural.Contains(key)) continue;
                    header.Add(card.PadRight(CardSize).Substring(0, CardSize));
                }
            if (history != null)
                foreach (string line in history)
                    foreach (string piece in Wrap(line, CardSize - 8))
                        header.Add(("HISTORY " + piece).PadRight(CardSize));
            header.Add("END".PadRight(CardSize));
            while (header.Count % (BlockSize / CardSize) != 0) header.Add(new string(' ', CardSize));

            foreach (string card in header)
                s.Write(Encoding.ASCII.GetBytes(card), 0, CardSize);

            int bytesPer = Math.Abs(bitpix) / 8;
            long dataBytes = (long)img.D.Length * bytesPer;
            var buf = new byte[Math.Min(dataBytes, 1 << 20)];
            int bp = 0;
            foreach (double v in img.D)
            {
                if (bitpix == -32)
                {
                    int bits = BitConverter.SingleToInt32Bits((float)v);
                    buf[bp++] = (byte)(bits >> 24); buf[bp++] = (byte)(bits >> 16);
                    buf[bp++] = (byte)(bits >> 8); buf[bp++] = (byte)bits;
                }
                else
                {
                    int u = (int)Math.Clamp(Math.Round(v), 0, 65535) - 32768;
                    buf[bp++] = (byte)((u >> 8) & 0xFF); buf[bp++] = (byte)(u & 0xFF);
                }
                if (bp == buf.Length) { s.Write(buf, 0, bp); bp = 0; }
            }
            if (bp > 0) s.Write(buf, 0, bp);

            long pad = (BlockSize - dataBytes % BlockSize) % BlockSize;
            if (pad > 0) s.Write(new byte[pad], 0, (int)pad);
            s.Flush();
        }

        /// <summary>Toate octetii unui fisier, pentru teste si pentru cine chiar vrea un byte[].</summary>
        internal static byte[] Encode(Img img, int bitpix, IReadOnlyList<string> cards, IReadOnlyList<string> history)
        {
            using var ms = new MemoryStream();
            Write(ms, img, bitpix, cards, history);
            return ms.ToArray();
        }

        // Format fix: cuvant in 8, "= ", valoarea aliniata la dreapta pana la coloana 30,
        // apoi " / comentariu". Doar pentru cardurile generate aici.
        static string Number(string key, long value, string comment)
            => Card(key, value.ToString(CultureInfo.InvariantCulture).PadLeft(20), comment);

        static string Logical(string key, bool value, string comment)
            => Card(key, (value ? "T" : "F").PadLeft(20), comment);

        static string Card(string key, string value, string comment)
        {
            string s = key.PadRight(8) + "= " + value;
            if (comment.Length > 0) s += " / " + comment;
            return s.PadRight(CardSize).Substring(0, CardSize);
        }

        static IEnumerable<string> Wrap(string text, int width)
        {
            for (int i = 0; i < text.Length; i += width)
                yield return text.Substring(i, Math.Min(width, text.Length - i));
            if (text.Length == 0) yield return "";
        }
    }
}
