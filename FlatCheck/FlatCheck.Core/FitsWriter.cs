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

// FitsWriter.cs - our own FITS writer, the pair of FitsReader. It exists for a single
// consumer: the synthetic mode, which writes a radial flat from the lens's profile.
//
// It writes a single primary HDU, 2D, BITPIX -32 or 16, big-endian, with the input's header
// copied card by card - minus the structural ones, which it rewrites - and with a few HISTORY
// cards at the end. It is not a general writer: it knows no cubes, no extensions, and can
// format only the cards it generates itself.
//
// It writes to a Stream, rather than returning a byte[], for the same reason the report is
// written to a TextWriter: 96 MB of floats have no reason to sit in memory once more. The
// tests give it a MemoryStream.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// Writes a 2D image as the primary HDU of a FITS file, with a copied header and HISTORY
    /// lines.
    /// </summary>
    internal static class FitsWriter
    {
        const int CardSize = 80, BlockSize = 2880;

        /// <summary>
        /// The keywords the writer sets itself, so it does not copy them from the input.
        /// XTENSION/PCOUNT/GCOUNT belong to an extension HDU; the image could have come from
        /// there (FitsReader skips an empty primary), but here a primary is always written.
        /// </summary>
        static readonly HashSet<string> Structural = new(StringComparer.Ordinal)
        {
            "SIMPLE", "BITPIX", "NAXIS", "NAXIS1", "NAXIS2", "NAXIS3", "NAXIS4",
            "EXTEND", "BZERO", "BSCALE", "END", "XTENSION", "PCOUNT", "GCOUNT",
        };

        /// <summary>
        /// Writes <paramref name="img"/> as a 2D primary HDU. <paramref name="bitpix"/> is -32
        /// (IEEE float) or 16 (short with BZERO 32768, rounded and clipped to 0..65535).
        /// <paramref name="cards"/> are the raw cards to copy (the structural ones are skipped),
        /// <paramref name="history"/> are the HISTORY lines to add. Any other BITPIX throws an
        /// ArgumentException.
        /// </summary>
        /// <param name="s">Where the file's bytes go; flushed at the end, not closed.</param>
        /// <param name="img">The image; its values are written as they are (-32) or as ADU
        /// (16).</param>
        /// <param name="bitpix">-32 or 16.</param>
        /// <param name="cards">The input's raw 80-character cards; null for none.</param>
        /// <param name="history">The HISTORY lines, each wrapped to 72 characters; null for
        /// none.</param>
        internal static void Write(Stream s, Img img, int bitpix, IReadOnlyList<string> cards, IReadOnlyList<string> history)
        {
            if (bitpix != -32 && bitpix != 16)
                throw new ArgumentException(T("FitsWriter writes only BITPIX -32 or 16, not ", "FitsWriter scrie doar BITPIX -32 sau 16, nu ") + bitpix);

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

        /// <summary>
        /// All the bytes of a file, for the tests and for whoever really wants a byte[].
        /// </summary>
        /// <param name="img">The image, as for <see cref="Write"/>.</param>
        /// <param name="bitpix">-32 or 16.</param>
        /// <param name="cards">The input's raw cards; null for none.</param>
        /// <param name="history">The HISTORY lines; null for none.</param>
        /// <returns>The whole FITS file.</returns>
        internal static byte[] Encode(Img img, int bitpix, IReadOnlyList<string> cards, IReadOnlyList<string> history)
        {
            using var ms = new MemoryStream();
            Write(ms, img, bitpix, cards, history);
            return ms.ToArray();
        }

        // Fixed format: the keyword in 8, "= ", the value right-aligned up to column 30, then
        // " / comment". Only for the cards generated here.
        /// <summary>An integer card in the fixed format.</summary>
        /// <param name="key">The keyword, at most 8 characters.</param>
        /// <param name="value">The value.</param>
        /// <param name="comment">The comment; empty for none.</param>
        /// <returns>The 80-character card.</returns>
        static string Number(string key, long value, string comment)
            => Card(key, value.ToString(CultureInfo.InvariantCulture).PadLeft(20), comment);

        /// <summary>A logical card (T or F) in the fixed format.</summary>
        /// <param name="key">The keyword, at most 8 characters.</param>
        /// <param name="value">The value.</param>
        /// <param name="comment">The comment; empty for none.</param>
        /// <returns>The 80-character card.</returns>
        static string Logical(string key, bool value, string comment)
            => Card(key, (value ? "T" : "F").PadLeft(20), comment);

        /// <summary>A card from a keyword, a value already padded and a comment.</summary>
        /// <param name="key">The keyword, at most 8 characters.</param>
        /// <param name="value">The value, right-aligned in 20 characters.</param>
        /// <param name="comment">The comment; empty for none.</param>
        /// <returns>The card, padded or cut to 80 characters.</returns>
        static string Card(string key, string value, string comment)
        {
            string s = key.PadRight(8) + "= " + value;
            if (comment.Length > 0) s += " / " + comment;
            return s.PadRight(CardSize).Substring(0, CardSize);
        }

        /// <summary>Cuts a text into pieces of a fixed width, the last one shorter.</summary>
        /// <param name="text">The text; empty gives one empty piece.</param>
        /// <param name="width">The width of a piece, in characters.</param>
        /// <returns>The pieces, in order.</returns>
        static IEnumerable<string> Wrap(string text, int width)
        {
            for (int i = 0; i < text.Length; i += width)
                yield return text.Substring(i, Math.Min(width, text.Length - i));
            if (text.Length == 0) yield return "";
        }
    }
}
