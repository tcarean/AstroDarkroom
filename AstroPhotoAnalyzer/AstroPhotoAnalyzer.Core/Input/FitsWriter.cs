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
// the bottom up under ROWORDER = 'BOTTOM-UP': Siril puts a file's first row at the bottom
// whatever the keyword says (ADR 0049), so this is the file it shows the way up it was
// analysed. The source's ICC profile is not carried: FITS has no standard place for it.
//
// Either way the raster, whose row 0 is the top, is written from its last row to its first:
// the inverse of the reader's flip.
//
// The values are the raster's: what the reader clipped to 0..1 stays clipped.
//
// It is written whole or not at all, through AtomicFile (a temporary file in the target folder,
// moved over the name at the end): a failed write does not leave a half-written FITS under the
// target's name.
//
// A CROP (R-181, ADR 0098) is the second thing written, and the only one that goes around the
// raster: SaveCrop copies the rectangle's bytes from the source file as they are, so that every
// BITPIX keeps every bit (the raster is 32-bit float clipped to 0..1 and could not), under the
// source's header with the new size and the reference pixel of a plate solution moved.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// Writes the tool's FITS files: a new FITS of an analysed raster with a curve applied
    /// (R-145, ADR 0032), and the crop of a FITS, copied from its bytes (R-181, ADR 0098).
    /// </summary>
    internal static class FitsWriter
    {
        const int Block = 2880, Card = 80;

        /// <summary>
        /// Writes to <paramref name="target"/> the raster <paramref name="r"/> passed through
        /// <paramref name="curve"/>, whole or not at all (<see cref="AtomicFile"/>: a temporary
        /// file in the same folder, moved over the name at the end, deleted on a failure). The
        /// exception passes on: a target held open or refused, found before anything is written;
        /// a source that no longer has the raster's geometry or is cut short before the image's
        /// data; a value the source's type cannot hold.
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
            => AtomicFile.Write(target, o =>
            {
                if (sourceFits != null) FromFits(r, curve, sourceFits, history, o);
                else Fresh(r, curve, history, o);
            });

        /// <summary>
        /// Writes to <paramref name="target"/> the crop of a FITS file, whole or not at all
        /// (<see cref="AtomicFile"/>): the rectangle's bytes copied from
        /// <paramref name="sourceFits"/> as they are, in the file's own row order, so every
        /// sample keeps its encoding and every bit; the image's header with NAXIS1 and NAXIS2
        /// set to the rectangle's size, CRPIX1 and CRPIX2 (and their lettered alternates), when
        /// present, moved by the columns and the file's rows cut before the first kept, every
        /// other card as it is, then the HISTORY cards; the HDUs before and after copied.
        /// Throws when the source has no image, no longer has the given geometry or is cut short
        /// before the image's data ends, or when the rectangle is not inside the image.
        /// </summary>
        /// <param name="sourceFits">The FITS the image was read from, opened for reading only.</param>
        /// <param name="rect">
        /// The rectangle to keep, in the raster's pixels: its row 0 is the top, which is the
        /// file's last row (the reader flips every FITS, ADR 0049).
        /// </param>
        /// <param name="imageW">The analysed image's width, in pixels.</param>
        /// <param name="imageH">The analysed image's height, in pixels.</param>
        /// <param name="planes">The analysed image's planes, 1 or 3.</param>
        /// <param name="history">The texts of the HISTORY cards, one card each, cut to 72 characters.</param>
        /// <param name="target">The file to write; replaced when it exists.</param>
        internal static void SaveCrop(string sourceFits, CropRect rect, int imageW, int imageH, int planes, IReadOnlyList<string> history, string target)
        {
            if (!rect.FitsIn(imageW, imageH)) throw new ArgumentException("the crop rectangle " + rect + " is not inside the image " + imageW + " x " + imageH, nameof(rect));
            AtomicFile.Write(target, o => CropFromFits(sourceFits, rect, imageW, imageH, planes, history, o));
        }

        /// <summary>The body of <see cref="SaveCrop"/>, to an open stream at its start.</summary>
        /// <param name="sourceFits">The source FITS, opened for reading only.</param>
        /// <param name="rect">The rectangle, in the raster's pixels, inside the image.</param>
        /// <param name="imageW">The analysed image's width, in pixels.</param>
        /// <param name="imageH">The analysed image's height, in pixels.</param>
        /// <param name="planes">The analysed image's planes.</param>
        /// <param name="history">The texts of the HISTORY cards.</param>
        /// <param name="o">The output, at its start.</param>
        static void CropFromFits(string sourceFits, CropRect rect, int imageW, int imageH, int planes, IReadOnlyList<string> history, Stream o)
        {
            using var s = new FileStream(sourceFits, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            var block = new byte[Block];
            while (true)
            {
                long start = s.Position;
                Dictionary<string, string> hdr = FitsReader.ReadHeader(s, block);
                if (hdr == null) throw new Exception(Tr.Pick("FITS: no HDU with an image found.", "FITS: niciun HDU cu imagine gasit."));
                long headerEnd = s.Position;

                int bitpix = FitsReader.HInt(hdr, "BITPIX", 0);
                int naxis = FitsReader.HInt(hdr, "NAXIS", 0);
                long npix = naxis == 0 ? 0 : 1;
                for (int ax = 1; ax <= naxis; ax++) npix *= FitsReader.HInt(hdr, "NAXIS" + ax, 1);
                int bytesPer = Math.Abs(bitpix) / 8;
                long dataBlocks = (npix * bytesPer + Block - 1) / Block * Block;
                int n1 = FitsReader.HInt(hdr, "NAXIS1", 0), n2 = FitsReader.HInt(hdr, "NAXIS2", 0);
                int n3 = naxis >= 3 ? FitsReader.HInt(hdr, "NAXIS3", 1) : 1;

                if (!(naxis >= 2 && n1 > 0 && n2 > 0))
                {
                    // An HDU before the image: it passes as it is.
                    Copy(s, o, start, headerEnd + dataBlocks);
                    continue;
                }
                if (n1 != imageW || n2 != imageH || n3 != planes)
                    throw new Exception(Tr.Pick("the source FITS no longer has the geometry of the image analysed: ", "FITS-ul sursa nu mai are geometria imaginii analizate: ") + sourceFits);
                if (bytesPer == 0 || headerEnd + npix * bytesPer > s.Length) throw new Exception(Tr.Pick("FITS: truncated file.", "FITS: fisier trunchiat."));

                // The raster's row y is the file's row imageH - 1 - y, so the rectangle's rows
                // are the file's rows from firstRow on, and firstRow file rows are cut before
                // them: what CRPIX2, which counts the file's rows, moves by.
                int firstRow = imageH - rect.Bottom;

                // The header: every card up to END, four of them with a new value, then
                // HISTORY, then END.
                var raw = new byte[headerEnd - start];
                s.Position = start;
                ReadExact(s, raw);
                var cards = new List<byte[]>();
                for (int c = 0; c * Card < raw.Length; c++)
                {
                    var card = new byte[Card];
                    Array.Copy(raw, c * Card, card, 0, Card);
                    string key = Encoding.ASCII.GetString(card, 0, 8).Trim();
                    if (key == "END") break;
                    if (key == "NAXIS1") card = WithValue(card, rect.W.ToString(CultureInfo.InvariantCulture));
                    else if (key == "NAXIS2") card = WithValue(card, rect.H.ToString(CultureInfo.InvariantCulture));
                    else if (IsReferencePixel(key, '1')) card = Moved(card, rect.X);
                    else if (IsReferencePixel(key, '2')) card = Moved(card, firstRow);
                    cards.Add(card);
                }
                foreach (string h in history) cards.Add(HistoryCard(h));
                cards.Add(Encoding.ASCII.GetBytes("END".PadRight(Card)));
                WriteCards(o, cards);

                // The data: plane after plane, the rectangle's stretch of each kept file row.
                var row = new byte[rect.W * bytesPer];
                long planeBytes = (long)imageW * imageH * bytesPer, written = 0;
                for (int p = 0; p < planes; p++)
                    for (int fr = firstRow; fr < firstRow + rect.H; fr++)
                    {
                        s.Position = headerEnd + p * planeBytes + ((long)fr * imageW + rect.X) * bytesPer;
                        ReadExact(s, row);
                        o.Write(row, 0, row.Length);
                        written += row.Length;
                    }
                var zero = new byte[1];
                for (; written % Block != 0; written++) o.Write(zero, 0, 1);

                // What follows the image (extensions) passes as it is.
                Copy(s, o, headerEnd + dataBlocks, s.Length);
                return;
            }
        }

        /// <summary>
        /// Whether a keyword is the reference pixel of an axis: CRPIX1 or CRPIX2, or the same
        /// with the letter of an alternate plate solution (CRPIX1A).
        /// </summary>
        /// <param name="key">The card's keyword, trimmed.</param>
        /// <param name="axis">'1' or '2'.</param>
        /// <returns>True when it is.</returns>
        static bool IsReferencePixel(string key, char axis)
            => key.StartsWith("CRPIX", StringComparison.Ordinal) && key.Length >= 6 && key[5] == axis
               && (key.Length == 6 || (key.Length == 7 && key[6] >= 'A' && key[6] <= 'Z'));

        /// <summary>
        /// A value card with another value: the keyword and the comment kept, the value
        /// right-aligned to column 30 as FITS writes it. A card that is not "KEY = value" comes
        /// back as it is.
        /// </summary>
        /// <param name="card">The card's 80 bytes.</param>
        /// <param name="value">The new value, as FITS writes it.</param>
        /// <returns>The new card's 80 bytes.</returns>
        static byte[] WithValue(byte[] card, string value)
        {
            string text = Encoding.ASCII.GetString(card);
            if (text[8] != '=') return card;
            // A number holds no slash, so the first one after the value starts the comment.
            int slash = text.IndexOf('/', 10);
            string comment = slash < 0 ? "" : " " + text.Substring(slash).TrimEnd();
            string made = text.Substring(0, 8) + "= " + value.PadLeft(20) + comment;
            return Encoding.ASCII.GetBytes(made.PadRight(Card).Substring(0, Card));
        }

        /// <summary>
        /// A reference-pixel card moved by the pixels cut before it, in decimal arithmetic, so
        /// that 2919.1 less 833 is written 2086.1 and the card keeps its comment; a value some
        /// writers put between quotes is read as the number it holds and written as one. A
        /// card whose value is not a number comes back as it is.
        /// </summary>
        /// <param name="card">The card's 80 bytes.</param>
        /// <param name="cut">The pixels cut before the first kept one, along the card's axis.</param>
        /// <returns>The new card's 80 bytes.</returns>
        static byte[] Moved(byte[] card, int cut)
        {
            string text = Encoding.ASCII.GetString(card);
            if (text[8] != '=') return card;
            int slash = text.IndexOf('/', 10);
            // The reader takes a quoted number for the number (ReadHeader strips the quotes), so
            // the plate solution it reads from the crop must have moved too. A FITS real may
            // write its exponent with a D, and the reader takes a lower-case d as well
            // (FitsReader.TryNumber): what it reads as a number in a well-formed card is moved
            // as one. A card with broken quoting is taken apart differently here and there
            // (CR-077 of the code review of 2026-11-13).
            string value = (slash < 0 ? text.Substring(10) : text.Substring(10, slash - 10)).Trim().Trim('\'').Trim().Replace('D', 'E').Replace('d', 'e');
            string now;
            // Within the cut of the decimal's own end the difference does not fit one: such a
            // value goes the way of those a decimal cannot hold at all, through a double.
            if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal exact) && exact > decimal.MinValue + cut)
                now = (exact - cut).ToString(CultureInfo.InvariantCulture);
            else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double was) && !double.IsNaN(was) && !double.IsInfinity(was))
                now = (was - cut).ToString("R", CultureInfo.InvariantCulture);
            else return card;
            // A real keeps its point, as the writers of plate solutions put it ("1811.").
            if (now.IndexOfAny(new[] { '.', 'E' }) < 0) now += ".";
            return WithValue(card, now);
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
        /// ROWORDER = 'BOTTOM-UP', the HISTORY cards), then the data, bottom row first.
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
            cards.Add(StringCard("ROWORDER", "BOTTOM-UP"));
            foreach (string h in history) cards.Add(HistoryCard(h));
            cards.Add(Encoding.ASCII.GetBytes("END".PadRight(Card)));
            WriteCards(o, cards);
            WriteData(o, r, curve, -32, 0, 1);
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
                if (hdr == null) throw new Exception(Tr.Pick("FITS: no HDU with an image found.", "FITS: niciun HDU cu imagine gasit."));
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
                    throw new Exception(Tr.Pick("the source FITS no longer has the geometry of the image analysed: ", "FITS-ul sursa nu mai are geometria imaginii analizate: ") + sourceFits);

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

                // The raster was flipped on reading whatever the source's ROWORDER said, so
                // writing it flipped back keeps the file's own order and its keyword true.
                WriteData(o, r, curve, bitpix, FitsReader.HDbl(hdr, "BZERO", 0.0), FitsReader.HDbl(hdr, "BSCALE", 1.0));

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
                if (n <= 0) throw new Exception(Tr.Pick("FITS: truncated file.", "FITS: fisier trunchiat."));
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
            if (to > s.Length) throw new Exception(Tr.Pick("FITS: truncated file.", "FITS: fisier trunchiat."));
            s.Position = from;
            var buf = new byte[1 << 16];
            long left = to - from;
            while (left > 0)
            {
                int n = s.Read(buf, 0, (int)Math.Min(buf.Length, left));
                if (n <= 0) throw new Exception(Tr.Pick("FITS: truncated file.", "FITS: fisier trunchiat."));
                o.Write(buf, 0, n);
                left -= n;
            }
        }

        /// <summary>
        /// The data, plane after plane, in the given encoding, the raster's last row first: the
        /// inverse of FitsReader.ReadData. Throws on a BITPIX it does not know or a BSCALE of 0,
        /// and, after writing, when a value fell outside the type by more than a rounding.
        /// </summary>
        /// <param name="o">The output, after the header.</param>
        /// <param name="r">The raster, 0..1.</param>
        /// <param name="curve">The curve applied to every sample.</param>
        /// <param name="bitpix">The encoding: 8, 16, 32, -32 or -64.</param>
        /// <param name="bzero">The BZERO the stored values are written for.</param>
        /// <param name="bscale">The BSCALE the stored values are written for; not 0.</param>
        static void WriteData(Stream o, Raster r, ToneCurve curve, int bitpix, double bzero, double bscale)
        {
            int bytesPer = Math.Abs(bitpix) / 8;
            if (bscale == 0 || (bitpix != 8 && bitpix != 16 && bitpix != 32 && bitpix != -32 && bitpix != -64))
                throw new Exception(Tr.Pick("BITPIX or BSCALE not supported for writing: ", "BITPIX sau BSCALE neacceptat la scriere: ") + bitpix + ", " + bscale.ToString(CultureInfo.InvariantCulture));
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
                    int y = r.H - 1 - fy;
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
