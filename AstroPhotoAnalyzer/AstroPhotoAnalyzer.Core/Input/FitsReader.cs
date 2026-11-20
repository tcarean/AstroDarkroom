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

// FitsReader.cs - our own FITS reader (no dependencies), started from FlatCheck's.
// The header as 80-byte cards in 2880-byte blocks, then big-endian binary data.
//
// WHAT WAS KEPT from FlatCheck: the header parsing, BZERO/BSCALE, skipping the HDUs without an
// image, the BITPIX types it read (8, 16, 32, -32, -64; not 64). WHAT CHANGED:
//   - it reads as a stream, not through File.ReadAllBytes: a 23 MP RGB float FITS is 277 MB,
//     and holding it twice in memory (bytes + float) just to parse it is a waste that shows;
//   - the planes are KEPT (1 or 3), not averaged into one - here colour is an object of
//     measurement, not noise;
//   - the values are normalised to 0..1 by the type in the file, like the TIFF and the PNG;
//   - the rows are always flipped, the file's first row last, which is how Siril shows every
//     FITS whatever its ROWORDER says (ADR 0049), so that the report's "colt dreapta-jos" is
//     the same corner as in Siril and as in a TIFF exported from it.
// WHAT WAS CUT: CFA, focal length, camera, lens - a finished image has no mosaic and is not
// compared with vignetting profiles.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NLog;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The hand-written FITS reader (.fit, .fits, .fts): the first HDU with NAXIS 2 or more, which
    /// must be 2D or have 3 planes; BITPIX 8, 16, 32, -32 and -64; BZERO and BSCALE; the rows
    /// flipped as Siril shows them; the WCS. XTENSION is not checked, so a table HDU with two
    /// axes is taken for an image.
    /// </summary>
    internal sealed class FitsReader : IFrameReader
    {
        static readonly string[] Exts = { ".fit", ".fits", ".fts" };

        /// <summary>
        /// The diagnostic log: the keywords that decide the reading at Debug, every card at Trace
        /// (R-172, ADR 0069).
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger("FitsReader");

        public IReadOnlyList<string> Extensions => Exts;

        /// <inheritdoc/>
        public bool Handles(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(Exts, ext) >= 0;
        }

        /// <summary>
        /// Reads the first HDU with NAXIS 2 or more: its samples normalised to 0..1 by the BITPIX
        /// after BZERO and BSCALE, the values outside clipped and counted in the note, the rows
        /// flipped so that the file's last row is row 0 (as Siril shows every FITS, whatever
        /// ROWORDER says; a TOP-DOWN keyword is noted as ignored), and the WCS attached when the
        /// header has one.
        /// Throws, with a message for the user, on a file with no image, cut short, with a
        /// BITPIX it does not know, with more than 3 axes or planes other than 1 or 3, with
        /// a size, a BZERO or a BSCALE that cannot be read as a number, or with a size that is
        /// impossible (<see cref="Shape"/>) or larger than one array holds; a file that does not
        /// open throws the platform's exception.
        /// </summary>
        /// <param name="path">The FITS file.</param>
        /// <returns>The raster, with its <see cref="SourceInfo"/>.</returns>
        public Raster Load(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            var (raster, hdr, bitpix, planes, note) = LoadFrom(fs);
            if (Log.IsDebugEnabled) Log.Debug("header of " + path + ": " + LogText.FitsKeys(hdr));
            raster.Source = new SourceInfo
            {
                Path = path,
                Format = "FITS",
                BitDepth = Math.Abs(bitpix),
                IsFloat = bitpix < 0,
                PlanesInFile = planes,
                Compression = null,
                Icc = null,
                IccDescription = null,
                FileBytes = fs.Length,
                Wcs = Wcs.TryFrom(hdr, raster.W, raster.H, rowsFlipped: true),
                Note = note,
            };
            return raster;
        }

        /// <summary>
        /// Only the header of the first HDU with an image, without its data: for a FITS whose WCS
        /// is lent to another file (R-027). Also returns the width and the height, so that the
        /// geometry can be checked. Throws on a file with no image, cut short, or with a size
        /// that cannot be read as a number or is impossible (<see cref="Shape"/>).
        /// </summary>
        /// <param name="path">The FITS file.</param>
        /// <returns>The header (keyword -> value) and the width and height in pixels.</returns>
        internal static (IReadOnlyDictionary<string, string> Header, int W, int H) ReadImageHeader(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            var block = new byte[2880];
            while (true)
            {
                var hdr = ReadHeader(fs, block);
                if (hdr == null) throw new Exception(Tr.Pick("FITS: no HDU with an image found.", "FITS: niciun HDU cu imagine gasit."));
                HduShape shape = Shape(hdr);
                if (shape.HasImage)
                {
                    if (Log.IsDebugEnabled) Log.Debug("header of " + path + ": " + LogText.FitsKeys(hdr));
                    return (hdr, shape.N1, shape.N2);
                }

                if (!SkipExact(fs, shape.DataBlocks)) throw new Exception(Tr.Pick("FITS: truncated file.", "FITS: fisier trunchiat."));
            }
        }

        /// <summary>
        /// The first HDU with NAXIS 2 or more. Also returns its header, its BITPIX, the number of
        /// planes in the file and a note for the report (or null). Throws as
        /// <see cref="Load"/> does.
        /// </summary>
        /// <param name="s">The stream, at the start of the file; read forward only.</param>
        /// <returns>
        /// The raster without its <see cref="Raster.Source"/>, the image HDU's header, its BITPIX,
        /// its planes (1 or 3) and the note: the values clipped, a TOP-DOWN row order ignored,
        /// or null.
        /// </returns>
        internal static (Raster Raster, IReadOnlyDictionary<string, string> Header, int Bitpix, int Planes, string Note)
            LoadFrom(Stream s)
        {
            var block = new byte[2880];
            while (true)
            {
                var hdr = ReadHeader(s, block);
                if (hdr == null) throw new Exception(Tr.Pick("FITS: no HDU with an image found.", "FITS: niciun HDU cu imagine gasit."));

                HduShape shape = Shape(hdr);
                int bitpix = shape.Bitpix, naxis = shape.Naxis, n1 = shape.N1, n2 = shape.N2, n3 = shape.N3;

                if (shape.HasImage)
                {
                    if (naxis > 3) throw new Exception(Tr.Pick($"a FITS with {naxis} axes; 2D images or images with 3 planes are accepted.", $"FITS cu {naxis} axe; se accepta imagini 2D sau cu 3 planuri."));
                    if (n3 != 1 && n3 != 3) throw new Exception(Tr.Pick($"a FITS with {n3} planes; 1 or 3 are accepted.", $"FITS cu {n3} planuri; se accepta 1 sau 3."));

                    // The image is checked before memory is taken for it (ADR 0114): a BITPIX the
                    // reader decodes (64 is one an HDU may have and this reader does not read),
                    // pixels that fit one array, and the data in the file. A header of 40000 x
                    // 40000 x 3 in a file of 3 KB took 19 GB before the first read failed.
                    if (bitpix == 64) throw new Exception(Tr.Pick("unsupported BITPIX: ", "BITPIX neacceptat: ") + bitpix);
                    if ((long)n1 * n2 > Array.MaxLength)
                        throw new Exception(Tr.Pick($"FITS: the image is too large to read: {Fmt.Dim(n1, n2)} px.", $"FITS: imaginea e prea mare ca sa fie citita: {Fmt.Dim(n1, n2)} px."));
                    if (s.CanSeek && s.Length - s.Position < shape.DataBytes) throw new Exception(Tr.Pick("FITS: truncated data.", "FITS: date trunchiate."));

                    double bzero = HDbl(hdr, "BZERO", 0.0);
                    double bscale = HDbl(hdr, "BSCALE", 1.0);

                    var raster = new Raster(n2, n1, n3);
                    OutOfRange outside = ReadData(s, raster, bitpix, bzero, bscale);

                    // Most samples over 1: the file is on another scale (a 32-bit float FITS on
                    // 0..65535, as other stackers write). Clipped, it would be a white frame with
                    // a median of 1, which the linear refusal does not catch, and the whole report
                    // would describe it (ADR 0096).
                    long samples = (long)n1 * n2 * n3;
                    if (outside.OverOne > samples * Thresholds.OffScaleShare)
                        throw new RefusedImageException(OffScaleRefusal(100.0 * outside.OverOne / samples, outside.Largest));

                    long clipped = outside.Clipped;
                    string note = null;
                    if (clipped > 0)
                        note = Tr.Pick($"{clipped} values outside [0, 1] were clipped on reading", $"{clipped} valori in afara [0, 1] au fost taiate la citire");
                    // Siril ignores ROWORDER on display and so does the reader (ADR 0049); a file
                    // that says TOP-DOWN over bottom-up data exists, and the reader says what it did.
                    if (string.Equals(HStr(hdr, "ROWORDER", ""), "TOP-DOWN", StringComparison.OrdinalIgnoreCase))
                        note = (note == null ? "" : note + "; ") + Tr.Pick("ROWORDER = TOP-DOWN was ignored: the rows are shown as Siril shows them, the file's first row at the bottom",
                                                                           "ROWORDER = TOP-DOWN a fost ignorat: randurile sunt aratate cum le arata Siril, prima linie din fisier jos");

                    return (raster, hdr, bitpix, n3, note);
                }

                // An HDU without a usable image: skip its data blocks and go on.
                if (!SkipExact(s, shape.DataBlocks)) throw new Exception(Tr.Pick("FITS: truncated file.", "FITS: fisier trunchiat."));
            }
        }

        /// <summary>
        /// The sizes of one HDU, as <see cref="Shape"/> read and checked them.
        /// </summary>
        /// <param name="Bitpix">BITPIX: 8, 16, 32, 64, -32 or -64 when the HDU has axes; as written (0 when absent) when NAXIS is 0.</param>
        /// <param name="Naxis">NAXIS, 0 to 999.</param>
        /// <param name="N1">NAXIS1, 0 when absent; not negative when NAXIS is 1 or more (a card beyond NAXIS is as written).</param>
        /// <param name="N2">NAXIS2, 0 when absent; not negative when NAXIS is 2 or more (a card beyond NAXIS is as written).</param>
        /// <param name="N3">NAXIS3 when NAXIS is 3 or more (1 when absent; not negative), else 1.</param>
        /// <param name="DataBytes">The bytes of the HDU's data, before the padding to a block; 0 when NAXIS is 0.</param>
        internal readonly record struct HduShape(int Bitpix, int Naxis, int N1, int N2, int N3, long DataBytes)
        {
            /// <summary>Has the HDU an image the tool may read: two axes or more, the first two longer than 0?</summary>
            internal bool HasImage => Naxis >= 2 && N1 > 0 && N2 > 0;

            /// <summary>
            /// The bytes of one sample; 0 when NAXIS is 0: such an HDU has no samples, its BITPIX
            /// is read and its value not checked, so it may be any whole number, the one Math.Abs
            /// throws on included.
            /// </summary>
            internal int BytesPer => Naxis == 0 ? 0 : Math.Abs(Bitpix) / 8;

            /// <summary>The bytes the data take in the file, padded to whole blocks of 2880.</summary>
            internal long DataBlocks => (DataBytes + 2879) / 2880 * 2880;
        }

        /// <summary>The largest NAXIS the FITS standard allows.</summary>
        const int MaxAxes = 999;

        /// <summary>
        /// The sizes of an HDU from its header, checked before any walk of the file uses them
        /// (ADR 0114): the reader, the --wcs header walk and the writer's two walks all skip or
        /// read by these numbers, and an impossible one made them walk for ever (a negative
        /// data size seeks back to the header just read), loop two billion times, or overflow.
        /// Throws, with a message for the user, on a NAXIS outside 0 to 999, a negative NAXISn,
        /// a BITPIX that is none of 8, 16, 32, 64, -32 and -64 in an HDU with axes, a data size
        /// no 64-bit count holds, and on a number that cannot be read (<see cref="HInt"/>).
        /// </summary>
        /// <param name="hdr">The HDU's header, keyword -> value.</param>
        /// <returns>The sizes; the data's bytes are not negative.</returns>
        internal static HduShape Shape(IReadOnlyDictionary<string, string> hdr)
        {
            int bitpix = HInt(hdr, "BITPIX", 0);
            int naxis = HInt(hdr, "NAXIS", 0);
            if (naxis < 0 || naxis > MaxAxes)
                throw new Exception(Tr.Pick($"FITS: the header's NAXIS is not between 0 and {MaxAxes}: {naxis}.", $"FITS: NAXIS din header nu e intre 0 si {MaxAxes}: {naxis}."));
            // Asked by value, not as Math.Abs(bitpix) / 8, which throws the platform's overflow
            // on int.MinValue and takes 12 for one byte a sample.
            if (naxis > 0 && bitpix != 8 && bitpix != 16 && bitpix != 32 && bitpix != 64 && bitpix != -32 && bitpix != -64)
                throw new Exception(Tr.Pick("unsupported BITPIX: ", "BITPIX neacceptat: ") + bitpix);

            // An HDU with NAXIS = 0 (the empty primary of a file with extensions) has NO data
            // AT ALL - not "one pixel". The empty product would give 1, a block of 2880 that
            // does not exist would be skipped, and the next header would be read from the
            // middle of the image.
            long bytes = naxis == 0 ? 0 : Math.Abs(bitpix) / 8;
            for (int ax = 1; ax <= naxis; ax++)
            {
                string key = "NAXIS" + ax;
                int n = HInt(hdr, key, 1);
                if (n < 0) throw new Exception(Tr.Pick($"FITS: the header's {key} is negative: {n}.", $"FITS: {key} din header e negativ: {n}."));
                // More bytes than a 64-bit count holds are in no file: told as the cut-short
                // file it is, before the product wraps to a small or a negative number.
                if (n != 0 && bytes > (long.MaxValue - 2879) / n) throw new Exception(Tr.Pick("FITS: truncated file.", "FITS: fisier trunchiat."));
                bytes *= n;
            }
            return new HduShape(bitpix, naxis, HInt(hdr, "NAXIS1", 0), HInt(hdr, "NAXIS2", 0), naxis >= 3 ? HInt(hdr, "NAXIS3", 1) : 1, bytes);
        }

        /// <summary>
        /// The sentence that refuses a FITS whose values are not on a 0 to 1 scale (R-013, ADR
        /// 0096): what was found and what to do, in the interface language.
        /// </summary>
        /// <param name="pctOverOne">The share of the samples over 1, in percent.</param>
        /// <param name="largest">The largest sample, on the scale the reader reads (1 = white).</param>
        /// <returns>The reason and the way out, without the console's error prefix.</returns>
        internal static string OffScaleRefusal(double pctOverOne, double largest)
            => Tr.Pick(
                $"the file's values are not between 0 and 1: {Fmt.F1(pctOverOne)} % of them are over 1 (the largest is {Fmt.Short(largest)}). "
                + "The tool reads a FITS on a scale from 0 to 1. Save it from Siril as 32-bit on a 0 to 1 scale, or as 16-bit, and analyse that file.",
                $"valorile din fisier nu sunt intre 0 si 1: {Fmt.F1(pctOverOne)} % din ele trec de 1 (cea mai mare e {Fmt.Short(largest)}). "
                + "Unealta citeste un FITS pe o scara de la 0 la 1. Salveaza-l din Siril pe 32 de biti pe scara 0..1, sau pe 16 biti, si analizeaza fisierul acela.");

        /// <summary>What <see cref="ReadData"/> met outside [0, 1].</summary>
        /// <param name="Clipped">The samples clipped to 0 or to 1, the NaN ones counted at 0.</param>
        /// <param name="OverOne">Of them, the samples over 1.</param>
        /// <param name="Largest">The largest sample over 1, on the reader's scale; 0 when none is over 1.</param>
        internal readonly record struct OutOfRange(long Clipped, long OverOne, double Largest);

        /// <summary>
        /// Writes one header card to the diagnostic log at Trace, as the file holds it (R-172):
        /// HISTORY and COMMENT included, the blank cards left out, a control character written
        /// <c>?</c> so a damaged header puts no control byte in the log.
        /// </summary>
        /// <param name="card">The card's 80 characters, as ASCII decoded them.</param>
        static void TraceCard(string card)
        {
            string text = card.TrimEnd();
            if (text.Length == 0) return;
            Log.Trace("card " + LogText.Printable(text));
        }

        /// <summary>
        /// The cards up to END, as a dictionary; null at the end of the file. Cards without a
        /// value (HISTORY, COMMENT, blank) are skipped, and a keyword met twice keeps its last
        /// value. Throws when the file ends inside a header after its first block; one that ends
        /// inside the first block is taken as the end of the file.
        /// </summary>
        /// <param name="s">The stream, at the first block of a header.</param>
        /// <param name="block">A buffer of 2880 bytes, overwritten.</param>
        /// <returns>
        /// Keyword -> value, a string without its quotes and trailing spaces, a number or a
        /// logical as written; null when the stream ends before a whole first block.
        /// </returns>
        internal static Dictionary<string, string> ReadHeader(Stream s, byte[] block)
        {
            var hdr = new Dictionary<string, string>(StringComparer.Ordinal);
            bool any = false;
            while (true)
            {
                if (!ReadExact(s, block, block.Length))
                {
                    if (!any) return null;
                    throw new Exception(Tr.Pick("FITS: incomplete header.", "FITS: header incomplet."));
                }
                any = true;
                for (int c = 0; c < 36; c++)
                {
                    string card = Encoding.ASCII.GetString(block, c * 80, 80);
                    if (Log.IsTraceEnabled) TraceCard(card);
                    string key = card.Substring(0, 8).Trim();
                    if (key == "END") return hdr;
                    if (key.Length == 0 || card[8] != '=') continue;

                    // The value: up to '/', but a '/' inside a quoted string is not a comment.
                    // Strings are given without their quotes and without the trailing spaces
                    // inside them (FITS pads them with spaces).
                    string value = card.Substring(10);
                    string t = value.TrimStart();
                    if (t.StartsWith("'"))
                    {
                        int close = t.IndexOf('\'', 1);
                        value = close > 0 ? t.Substring(1, close - 1).TrimEnd() : t.Substring(1).TrimEnd();
                    }
                    else
                    {
                        int slash = value.IndexOf('/');
                        if (slash >= 0) value = value.Substring(0, slash);
                        value = value.Trim();
                    }
                    hdr[key] = value;
                }
            }
        }

        /// <summary>
        /// The data, plane after plane (FITS is plane-sequential), decoded in chunks of 1 MB.
        /// The rows are flipped: the file's first row lands last in the raster (ADR 0049).
        /// Returns how many values fell outside [0, 1] and were clipped - it does not happen on
        /// integers with the usual BZERO, it can on float and on signed integers stored without
        /// it, whose positive half is also read on the same scale (0..32767 gives 0..0.5, with no
        /// note). Throws when the data are cut short.
        /// </summary>
        /// <param name="s">The stream, at the first byte of the data.</param>
        /// <param name="r">The raster to fill, already of the header's size.</param>
        /// <param name="bitpix">The header's BITPIX: 8, 16, 32, -32 or -64.</param>
        /// <param name="bzero">The header's BZERO, 0 when absent.</param>
        /// <param name="bscale">The header's BSCALE, 1 when absent.</param>
        /// <returns>
        /// The samples clipped to 0 or 1, NaN ones counted at 0, and of them how many were over 1
        /// and the largest, by which <see cref="LoadFrom"/> tells a file on another scale.
        /// </returns>
        static OutOfRange ReadData(Stream s, Raster r, int bitpix, double bzero, double bscale)
        {
            int bytesPer = Math.Abs(bitpix) / 8;
            long plane = (long)r.W * r.H;
            long total = plane * r.Planes.Length;
            double scale = bitpix switch
            {
                8 => 255.0,
                16 => 65535.0,
                32 => 4294967295.0,
                _ => 1.0,
            };

            int chunkSamples = (1 << 20) / bytesPer;
            var buf = new byte[chunkSamples * bytesPer];
            long clipped = 0, overOne = 0;
            double largest = 0;
            long i = 0;
            while (i < total)
            {
                int want = (int)Math.Min(chunkSamples, total - i);
                if (!ReadExact(s, buf, want * bytesPer)) throw new Exception(Tr.Pick("FITS: truncated data.", "FITS: date trunchiate."));
                for (int k = 0; k < want; k++, i++)
                {
                    double v = (bzero + bscale * ReadSample(buf, k * bytesPer, bitpix)) / scale;
                    // A NaN (Siril leaves one after some operations on float) is neither under 0
                    // nor over 1: without this line it would pass into the raster and break any
                    // table indexed by level (RasterOps.Gamma). It is treated as a clipped value,
                    // at 0.
                    if (double.IsNaN(v)) { v = 0; clipped++; }
                    else if (v < 0) { v = 0; clipped++; }
                    else if (v > 1)
                    {
                        if (v > largest) largest = v;
                        v = 1; clipped++; overOne++;
                    }
                    int p = (int)(i / plane);
                    long idx = i % plane;
                    // The file's first row is the image's bottom row, as Siril shows it (ADR 0049).
                    long y = idx / r.W, x = idx % r.W;
                    r.Planes[p][(r.H - 1 - y) * r.W + x] = (float)v;
                }
            }
            return new OutOfRange(clipped, overOne, largest);
        }

        /// <summary>
        /// One big-endian sample of the given BITPIX, before BZERO and BSCALE. Throws on a BITPIX
        /// other than 8, 16, 32, -32 and -64.
        /// </summary>
        /// <param name="b">The bytes.</param>
        /// <param name="p">The sample's first byte in <paramref name="b"/>.</param>
        /// <param name="bitpix">8 unsigned; 16 and 32 signed; -32 and -64 float.</param>
        /// <returns>The stored value, as a double.</returns>
        internal static double ReadSample(byte[] b, int p, int bitpix)
        {
            switch (bitpix)
            {
                case 8: return b[p];
                case 16: return BinaryPrimitives.ReadInt16BigEndian(new ReadOnlySpan<byte>(b, p, 2));
                case 32: return BinaryPrimitives.ReadInt32BigEndian(new ReadOnlySpan<byte>(b, p, 4));
                case -32: return BinaryPrimitives.ReadSingleBigEndian(new ReadOnlySpan<byte>(b, p, 4));
                case -64: return BinaryPrimitives.ReadDoubleBigEndian(new ReadOnlySpan<byte>(b, p, 8));
                default: throw new Exception(Tr.Pick("unsupported BITPIX: ", "BITPIX neacceptat: ") + bitpix);
            }
        }

        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes, over as many reads as it takes.
        /// </summary>
        /// <param name="s">The stream.</param>
        /// <param name="buf">The buffer, filled from its start.</param>
        /// <param name="count">The bytes to read, at most the buffer's length.</param>
        /// <returns><c>false</c> when the stream ends first.</returns>
        static bool ReadExact(Stream s, byte[] buf, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = s.Read(buf, got, count - got);
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }

        /// <summary>
        /// Skips <paramref name="count"/> bytes: by seeking where the stream can, by reading
        /// elsewhere.
        /// </summary>
        /// <param name="s">The stream.</param>
        /// <param name="count">The bytes to skip, not negative.</param>
        /// <returns>
        /// <c>false</c> when the stream ends first; a stream that can seek is then left where it
        /// was.
        /// </returns>
        static bool SkipExact(Stream s, long count)
        {
            if (count == 0) return true;
            if (s.CanSeek)
            {
                // Written as a difference: the sum with a large count wraps and passes.
                if (count > s.Length - s.Position) return false;
                s.Seek(count, SeekOrigin.Current);
                return true;
            }
            var buf = new byte[Math.Min(count, 1 << 16)];
            while (count > 0)
            {
                int n = s.Read(buf, 0, (int)Math.Min(buf.Length, count));
                if (n <= 0) return false;
                count -= n;
            }
            return true;
        }

        /// <summary>
        /// A header number as FITS writes one: an optional sign, digits with an optional point,
        /// and an optional exponent after E or D ("3.2768D+04"), which .NET does not read by
        /// itself; the letter is taken in either case. Nothing else is a number: no thousands
        /// separator, no brackets, no currency sign (NumberStyles.Any read "0,5" as 5 and "(5)"
        /// as -5), and no NaN or infinity.
        /// </summary>
        /// <param name="text">The card's value, as <see cref="ReadHeader"/> gives it; not null.</param>
        /// <param name="value">The number; 0 when the text is not one.</param>
        /// <returns><c>true</c> when the text is a finite number.</returns>
        internal static bool TryNumber(string text, out double value)
        {
            string t = text.Trim().Replace('D', 'E').Replace('d', 'e');
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value))
                return true;
            value = 0;
            return false;
        }

        /// <summary>
        /// A header value as an integer: digits with an optional sign, or a real whose value is
        /// whole, as some writers put one ("16.0"). Throws, with a message for the user, when
        /// the keyword is present and its value is not a whole number: a size read as its
        /// default would have the reader measure another image (ADR 0104).
        /// </summary>
        /// <param name="h">The header, keyword -> value.</param>
        /// <param name="k">The keyword.</param>
        /// <param name="def">The value when the keyword is absent.</param>
        /// <returns>The integer, or <paramref name="def"/> when the keyword is absent.</returns>
        internal static int HInt(IReadOnlyDictionary<string, string> h, string k, int def)
        {
            if (!h.TryGetValue(k, out var v)) return def;
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)) return r;
            if (TryNumber(v, out double d) && d == Math.Floor(d) && d >= int.MinValue && d <= int.MaxValue) return (int)d;
            throw new Exception(Tr.Pick($"FITS: the header's {k} cannot be read as a whole number: '{LogText.Printable(v)}'.",
                                        $"FITS: {k} din header nu se poate citi ca numar intreg: '{LogText.Printable(v)}'."));
        }

        /// <summary>
        /// A header value as a real number (<see cref="TryNumber"/>). Throws, with a message for
        /// the user, when the keyword is present and its value is not a number: a BZERO or a
        /// BSCALE read as its default would have the reader measure another image (ADR 0104).
        /// </summary>
        /// <param name="h">The header, keyword -> value.</param>
        /// <param name="k">The keyword.</param>
        /// <param name="def">The value when the keyword is absent.</param>
        /// <returns>The number, or <paramref name="def"/> when the keyword is absent.</returns>
        internal static double HDbl(IReadOnlyDictionary<string, string> h, string k, double def)
        {
            if (TryHDbl(h, k, def, out double value)) return value;
            throw new Exception(Tr.Pick($"FITS: the header's {k} cannot be read as a number: '{LogText.Printable(h[k])}'.",
                                        $"FITS: {k} din header nu se poate citi ca numar: '{LogText.Printable(h[k])}'."));
        }

        /// <summary>
        /// A header value as a real number, for a caller that takes an unreadable one as an
        /// answer of its own (the WCS: no WCS) rather than as an error.
        /// </summary>
        /// <param name="h">The header, keyword -> value.</param>
        /// <param name="k">The keyword.</param>
        /// <param name="def">The value when the keyword is absent.</param>
        /// <param name="value">
        /// The number; <paramref name="def"/> when the keyword is absent, 0 when it is present
        /// and not a number.
        /// </param>
        /// <returns><c>false</c> when the keyword is present and its value is not a number.</returns>
        internal static bool TryHDbl(IReadOnlyDictionary<string, string> h, string k, double def, out double value)
        {
            if (!h.TryGetValue(k, out var v)) { value = def; return true; }
            return TryNumber(v, out value);
        }

        /// <summary>A header value as it was read: a string without its quotes.</summary>
        /// <param name="h">The header, keyword -> value.</param>
        /// <param name="k">The keyword.</param>
        /// <param name="def">The value when the keyword is absent.</param>
        /// <returns>The value, or <paramref name="def"/>.</returns>
        internal static string HStr(IReadOnlyDictionary<string, string> h, string k, string def)
            => h.TryGetValue(k, out var v) ? v : def;
    }
}
