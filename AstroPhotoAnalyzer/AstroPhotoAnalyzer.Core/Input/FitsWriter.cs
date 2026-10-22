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

// FitsWriter.cs - the one thing the tool writes besides the report: a NEW FITS with a curve
// applied (R-145, ADR 0032). Written by hand, like the reader (ADR 0009).
//
// FROM A FITS. The source file is reopened for reading ONLY and copied as it is, byte for byte,
// except two places of the HDU with the image: the header gets HISTORY cards before its END
// (every other card stays, in order: WCS, SIP, exposure, instrument), and the data are written
// from the raster in memory passed through the curve, with the same encoding (BITPIX, BZERO,
// BSCALE) and the same row order (ROWORDER) as in the source. The HDUs before and after pass
// untouched.
//
// FROM ANYTHING ELSE (PNG, TIFF, JPEG). A 32-bit float FITS with a minimal header, the rows from
// the top down. The source's ICC profile is not carried: FITS has no standard place for it.
//
// The values are the raster's: what the reader clipped to 0..1 stays clipped.
//
// It is written to a temporary file in the target folder, moved over the name at the end: a
// failed write does not leave a half-written FITS under the target's name.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// Writes the one image the tool writes: a new FITS of an analysed raster with a curve
    /// applied (R-145, ADR 0032).
    /// </summary>
    internal static class FitsWriter
    {
        const int Block = 2880, Card = 80;

        /// <summary>
        /// Writes to <paramref name="target"/> the raster <paramref name="r"/> passed through
        /// <paramref name="curve"/>, through a temporary file in the same folder moved over the
        /// name at the end. On a failure the temporary file is deleted and the exception passes
        /// on: a source that no longer has the raster's geometry or is cut short before the
        /// image's data, or a value the source's type cannot hold. A delete refused with an
        /// IOException leaves the temporary file; one refused otherwise (access denied) throws
        /// in place of the original exception.
        /// </summary>
        /// <param name="r">The raster as it was analysed, 0..1.</param>
        /// <param name="curve">The curve applied to every sample.</param>
        /// <param name="sourceFits">
        /// The FITS the raster was read from, whose cards and other HDUs are copied; null when the
        /// source is not a FITS.
        /// </param>
        /// <param name="history">
        /// The texts of the HISTORY cards, one card each, cut to 72 characters.
        /// </param>
        /// <param name="target">The file to write; replaced when it exists.</param>
        internal static void Save(Raster r, ToneCurve curve, string sourceFits, IReadOnlyList<string> history, string target)
        {
            string temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var o = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16))
                {
                    if (sourceFits != null) FromFits(r, curve, sourceFits, history, o);
                    else Fresh(r, curve, history, o);
                }
                File.Move(temp, target, overwrite: true);
            }
            catch
            {
                // On an IOException the temporary file stays; the write error is what matters.
                try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
                throw;
            }
        }

        /// <summary>
        /// A HISTORY card: the text after "HISTORY ", cut to the card's 80 characters.
        /// </summary>
        /// <param name="text">The text, ASCII.</param>
        /// <returns>The card's 80 bytes.</returns>
        static byte[] HistoryCard(string text) => Encoding.ASCII.GetBytes(("HISTORY " + text).PadRight(Card).Substring(0, Card));

        /// <summary>
        /// A keyword = value card, the value right-aligned to column 30 as FITS writes it.
        /// </summary>
        /// <param name="key">The keyword, at most 8 characters.</param>
        /// <param name="value">The value as FITS writes it (a number, T or F).</param>
        /// <returns>The card's 80 bytes.</returns>
        static byte[] ValueCard(string key, string value) => Encoding.ASCII.GetBytes((key.PadRight(8) + "= " + value.PadLeft(20)).PadRight(Card));

        /// <summary>
        /// A keyword = 'string' card, the string padded to at least 8 characters.
        /// </summary>
        /// <param name="key">The keyword, at most 8 characters.</param>
        /// <param name="text">The string, without quotes.</param>
        /// <returns>The card's 80 bytes.</returns>
        static byte[] StringCard(string key, string text) => Encoding.ASCII.GetBytes((key.PadRight(8) + "= '" + text.PadRight(8) + "'").PadRight(Card));

        /// <summary>
        /// Writes the cards, then spaces to the end of their last 2880-byte block.
        /// </summary>
        /// <param name="o">The output.</param>
        /// <param name="cards">The cards, 80 bytes each, END included.</param>
        static void WriteCards(Stream o, List<byte[]> cards)
        {
            long n = 0;
            foreach (byte[] c in cards) { o.Write(c, 0, Card); n += Card; }
            var space = new byte[] { (byte)' ' };
            for (; n % Block != 0; n++) o.Write(space, 0, 1);
        }

        /// <summary>
        /// The FITS of a source that is not one: a minimal header (32-bit float, 2 or 3 axes,
        /// ROWORDER = 'TOP-DOWN', the HISTORY cards), then the data.
        /// </summary>
        /// <param name="r">The raster, 0..1.</param>
        /// <param name="curve">The curve applied to every sample.</param>
        /// <param name="history">The texts of the HISTORY cards.</param>
        /// <param name="o">The output, at its start.</param>
        static void Fresh(Raster r, ToneCurve curve, IReadOnlyList<string> history, Stream o)
        {
            var cards = new List<byte[]>
            {
                ValueCard("SIMPLE", "T"), ValueCard("BITPIX", "-32"),
                ValueCard("NAXIS", r.IsColor ? "3" : "2"),
                ValueCard("NAXIS1", r.W.ToString(CultureInfo.InvariantCulture)), ValueCard("NAXIS2", r.H.ToString(CultureInfo.InvariantCulture)),
            };
            if (r.IsColor) cards.Add(ValueCard("NAXIS3", "3"));
            cards.Add(StringCard("ROWORDER", "TOP-DOWN"));
            foreach (string h in history) cards.Add(HistoryCard(h));
            cards.Add(Encoding.ASCII.GetBytes("END".PadRight(Card)));
            WriteCards(o, cards);
            WriteData(o, r, curve, -32, 0, 1, bottomUp: false);
        }

        /// <summary>
        /// The FITS of a FITS source: the HDUs before the image copied as they are, the image's
        /// cards with the HISTORY cards before END, the data written from the raster through the
        /// curve in the source's encoding and row order, and what follows the image copied as it
        /// is. Throws when the source has no image, no longer has the raster's geometry, or is
        /// cut short before the image's data; a tail cut short after them is copied as it is.
        /// </summary>
        /// <param name="r">The raster read from <paramref name="sourceFits"/>, 0..1.</param>
        /// <param name="curve">The curve applied to every sample.</param>
        /// <param name="sourceFits">The source FITS, opened for reading only.</param>
        /// <param name="history">The texts of the HISTORY cards.</param>
        /// <param name="o">The output, at its start.</param>
        static void FromFits(Raster r, ToneCurve curve, string sourceFits, IReadOnlyList<string> history, Stream o)
        {
            using var s = new FileStream(sourceFits, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            var block = new byte[Block];
            while (true)
            {
                long start = s.Position;
                Dictionary<string, string> hdr = FitsReader.ReadHeader(s, block);
                if (hdr == null) throw new Exception("FITS: niciun HDU cu imagine gasit.");
                long headerEnd = s.Position;

                int bitpix = FitsReader.HInt(hdr, "BITPIX", 0);
                int naxis = FitsReader.HInt(hdr, "NAXIS", 0);
                long npix = naxis == 0 ? 0 : 1;
                for (int ax = 1; ax <= naxis; ax++) npix *= FitsReader.HInt(hdr, "NAXIS" + ax, 1);
                long dataBlocks = (npix * (Math.Abs(bitpix) / 8) + Block - 1) / Block * Block;
                int n1 = FitsReader.HInt(hdr, "NAXIS1", 0), n2 = FitsReader.HInt(hdr, "NAXIS2", 0);
                int n3 = naxis >= 3 ? FitsReader.HInt(hdr, "NAXIS3", 1) : 1;

                if (!(naxis >= 2 && n1 > 0 && n2 > 0))
                {
                    // An HDU before the image: it passes as it is.
                    Copy(s, o, start, headerEnd + dataBlocks);
                    continue;
                }
                if (n1 != r.W || n2 != r.H || n3 != r.Planes.Length)
                    throw new Exception("FITS-ul sursa nu mai are geometria imaginii analizate: " + sourceFits);

                // The header: every card up to END, then HISTORY, then END.
                var raw = new byte[headerEnd - start];
                s.Position = start;
                ReadExact(s, raw);
                var cards = new List<byte[]>();
                for (int c = 0; c * Card < raw.Length; c++)
                {
                    var card = new byte[Card];
                    Array.Copy(raw, c * Card, card, 0, Card);
                    if (Encoding.ASCII.GetString(card, 0, 8).Trim() == "END") break;
                    cards.Add(card);
                }
                foreach (string h in history) cards.Add(HistoryCard(h));
                cards.Add(Encoding.ASCII.GetBytes("END".PadRight(Card)));
                WriteCards(o, cards);

                bool bottomUp = string.Equals(FitsReader.HStr(hdr, "ROWORDER", ""), "BOTTOM-UP", StringComparison.OrdinalIgnoreCase);
                WriteData(o, r, curve, bitpix, FitsReader.HDbl(hdr, "BZERO", 0.0), FitsReader.HDbl(hdr, "BSCALE", 1.0), bottomUp);

                // What follows the image (extensions) passes as it is.
                Copy(s, o, headerEnd + dataBlocks, s.Length);
                return;
            }
        }

        /// <summary>
        /// Fills <paramref name="buf"/> from the stream; throws when the stream ends first.
        /// </summary>
        /// <param name="s">The stream.</param>
        /// <param name="buf">The buffer, filled whole.</param>
        static void ReadExact(Stream s, byte[] buf)
        {
            int got = 0;
            while (got < buf.Length)
            {
                int n = s.Read(buf, got, buf.Length - got);
                if (n <= 0) throw new Exception("FITS: fisier trunchiat.");
                got += n;
            }
        }

        /// <summary>
        /// Copies the source's bytes from <paramref name="from"/> up to <paramref name="to"/>,
        /// excluded; throws when the source ends before <paramref name="to"/>.
        /// </summary>
        /// <param name="s">The source, seekable.</param>
        /// <param name="o">The output.</param>
        /// <param name="from">The first byte, an offset in the source.</param>
        /// <param name="to">The byte after the last, an offset in the source.</param>
        static void Copy(Stream s, Stream o, long from, long to)
        {
            if (to > s.Length) throw new Exception("FITS: fisier trunchiat.");
            s.Position = from;
            var buf = new byte[1 << 16];
            long left = to - from;
            while (left > 0)
            {
                int n = s.Read(buf, 0, (int)Math.Min(buf.Length, left));
                if (n <= 0) throw new Exception("FITS: fisier trunchiat.");
                o.Write(buf, 0, n);
                left -= n;
            }
        }

        /// <summary>
        /// The data, plane after plane, in the given encoding and row order: the inverse of
        /// FitsReader.ReadData. Throws on a BITPIX it does not know or a BSCALE of 0, and, after
        /// writing, when a value fell outside the type by more than a rounding.
        /// </summary>
        /// <param name="o">The output, after the header.</param>
        /// <param name="r">The raster, 0..1.</param>
        /// <param name="curve">The curve applied to every sample.</param>
        /// <param name="bitpix">The encoding: 8, 16, 32, -32 or -64.</param>
        /// <param name="bzero">The BZERO the stored values are written for.</param>
        /// <param name="bscale">The BSCALE the stored values are written for; not 0.</param>
        /// <param name="bottomUp">Whether the rows are written from the bottom up.</param>
        static void WriteData(Stream o, Raster r, ToneCurve curve, int bitpix, double bzero, double bscale, bool bottomUp)
        {
            int bytesPer = Math.Abs(bitpix) / 8;
            if (bscale == 0 || (bitpix != 8 && bitpix != 16 && bitpix != 32 && bitpix != -32 && bitpix != -64))
                throw new Exception("BITPIX sau BSCALE neacceptat la scriere: " + bitpix + ", " + bscale.ToString(CultureInfo.InvariantCulture));
            double scale = bitpix switch { 8 => 255.0, 16 => 65535.0, 32 => 4294967295.0, _ => 1.0 };
            var row = new byte[r.W * bytesPer];
            long written = 0, outside = 0;
            (double lo, double hi) = bitpix switch
            {
                8 => (0.0, 255.0),
                16 => (short.MinValue, short.MaxValue),
                32 => (int.MinValue, int.MaxValue),
                _ => (double.NegativeInfinity, double.PositiveInfinity),
            };
            foreach (float[] plane in r.Planes)
                for (int fy = 0; fy < r.H; fy++)
                {
                    int y = bottomUp ? r.H - 1 - fy : fy;
                    for (int x = 0; x < r.W; x++)
                    {
                        double stored = (curve.MapSample(plane[y * r.W + x]) * scale - bzero) / bscale;
                        // Past the end of the type by more than a rounding: the value would no
                        // longer be the curve's.
                        if (stored < lo - 0.5 || stored > hi + 0.5) outside++;
                        var span = new Span<byte>(row, x * bytesPer, bytesPer);
                        switch (bitpix)
                        {
                            case 8: row[x] = (byte)Math.Clamp(Math.Round(stored), 0, 255); break;
                            case 16: BinaryPrimitives.WriteInt16BigEndian(span, (short)Math.Clamp(Math.Round(stored), short.MinValue, short.MaxValue)); break;
                            case 32: BinaryPrimitives.WriteInt32BigEndian(span, (int)Math.Clamp(Math.Round(stored), int.MinValue, int.MaxValue)); break;
                            case -32: BinaryPrimitives.WriteSingleBigEndian(span, (float)stored); break;
                            default: BinaryPrimitives.WriteDoubleBigEndian(span, stored); break;
                        }
                    }
                    o.Write(row, 0, row.Length);
                    written += row.Length;
                }
            // An integer FITS without BZERO (signed 16 bits: only 0..32767, half the scale) cannot
            // hold the values the curve lifts. Clipped silently they would make a wrong file: the
            // save is refused, and the temporary file is deleted (Save).
            if (outside > 0) throw new Exception(CurveTexts.ValuesDoNotFit(outside, bitpix, bzero));
            var zero = new byte[1];
            for (; written % Block != 0; written++) o.Write(zero, 0, 1);
        }
    }
}
