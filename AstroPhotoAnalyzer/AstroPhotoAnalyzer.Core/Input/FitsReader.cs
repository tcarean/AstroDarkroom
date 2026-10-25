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
//   - the rows are flipped on ROWORDER = 'BOTTOM-UP' (that is how Siril writes), so that the
//     report's "colt dreapta-jos" is the same corner as in a TIFF.
// WHAT WAS CUT: CFA, focal length, camera, lens - a finished image has no mosaic and is not
// compared with vignetting profiles.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The hand-written FITS reader (.fit, .fits, .fts): the first HDU with NAXIS 2 or more, which
    /// must be 2D or have 3 planes; BITPIX 8, 16, 32, -32 and -64; BZERO and BSCALE; ROWORDER;
    /// the WCS. XTENSION is not checked, so a table HDU with two axes is taken for an image.
    /// </summary>
    internal sealed class FitsReader : IFrameReader
    {
        static readonly string[] Exts = { ".fit", ".fits", ".fts" };

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
        /// flipped when ROWORDER is BOTTOM-UP, and the WCS attached when the header has one.
        /// Throws, with a message for the user, on a file with no image, cut short, with a
        /// BITPIX it does not know, or with more than 3 axes or planes other than 1 or 3; a file
        /// that does not open throws the platform's exception.
        /// </summary>
        /// <param name="path">The FITS file.</param>
        /// <returns>The raster, with its <see cref="SourceInfo"/>.</returns>
        public Raster Load(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            var (raster, hdr, bitpix, planes, note) = LoadFrom(fs);
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
                Wcs = Wcs.TryFrom(hdr, raster.W, raster.H, IsBottomUp(hdr)),
                Note = note,
            };
            return raster;
        }

        /// <summary>
        /// Only the header of the first HDU with an image, without its data: for a FITS whose WCS
        /// is lent to another file (R-027). Also returns the width, the height and the flip, so
        /// that the geometry can be checked. Throws on a file with no image or cut short.
        /// </summary>
        /// <param name="path">The FITS file.</param>
        /// <returns>
        /// The header (keyword -> value), the width and height in pixels, and whether ROWORDER
        /// is BOTTOM-UP.
        /// </returns>
        internal static (IReadOnlyDictionary<string, string> Header, int W, int H, bool BottomUp) ReadImageHeader(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            var block = new byte[2880];
            while (true)
            {
                var hdr = ReadHeader(fs, block);
                if (hdr == null) throw new Exception(Tr.Pick("FITS: no HDU with an image found.", "FITS: niciun HDU cu imagine gasit."));
                int naxis = HInt(hdr, "NAXIS", 0);
                int n1 = HInt(hdr, "NAXIS1", 0), n2 = HInt(hdr, "NAXIS2", 0);
                if (naxis >= 2 && n1 > 0 && n2 > 0) return (hdr, n1, n2, IsBottomUp(hdr));

                long npix = naxis == 0 ? 0 : 1;
                for (int ax = 1; ax <= naxis; ax++) npix *= HInt(hdr, "NAXIS" + ax, 1);
                long dataBytes = npix * (Math.Abs(HInt(hdr, "BITPIX", 0)) / 8);
                if (!SkipExact(fs, (dataBytes + 2879) / 2880 * 2880)) throw new Exception(Tr.Pick("FITS: truncated file.", "FITS: fisier trunchiat."));
            }
        }

        /// <summary>Whether the header's ROWORDER is 'BOTTOM-UP', in any case.</summary>
        /// <param name="hdr">The header, keyword -> value.</param>
        /// <returns><c>true</c> when the rows are stored from the bottom up.</returns>
        static bool IsBottomUp(IReadOnlyDictionary<string, string> hdr)
            => string.Equals(HStr(hdr, "ROWORDER", ""), "BOTTOM-UP", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The first HDU with NAXIS 2 or more. Also returns its header, its BITPIX, the number of
        /// planes in the file and a note for the report (or null). Throws as
        /// <see cref="Load"/> does.
        /// </summary>
        /// <param name="s">The stream, at the start of the file; read forward only.</param>
        /// <returns>
        /// The raster without its <see cref="Raster.Source"/>, the image HDU's header, its BITPIX,
        /// its planes (1 or 3) and the note: the values clipped, the rows flipped, or null.
        /// </returns>
        internal static (Raster Raster, IReadOnlyDictionary<string, string> Header, int Bitpix, int Planes, string Note)
            LoadFrom(Stream s)
        {
            var block = new byte[2880];
            while (true)
            {
                var hdr = ReadHeader(s, block);
                if (hdr == null) throw new Exception(Tr.Pick("FITS: no HDU with an image found.", "FITS: niciun HDU cu imagine gasit."));

                int bitpix = HInt(hdr, "BITPIX", 0);
                int naxis = HInt(hdr, "NAXIS", 0);

                // An HDU with NAXIS = 0 (the empty primary of a file with extensions) has NO data
                // AT ALL - not "one pixel". The empty product would give 1, a block of 2880 that
                // does not exist would be skipped, and the next header would be read from the
                // middle of the image.
                long npix = naxis == 0 ? 0 : 1;
                for (int ax = 1; ax <= naxis; ax++) npix *= HInt(hdr, "NAXIS" + ax, 1);
                int bytesPer = Math.Abs(bitpix) / 8;
                if (bytesPer == 0 && naxis > 0) throw new Exception(Tr.Pick("unsupported BITPIX: ", "BITPIX neacceptat: ") + bitpix);
                long dataBytes = npix * bytesPer;
                long dataBlocks = (dataBytes + 2879) / 2880 * 2880;

                int n1 = HInt(hdr, "NAXIS1", 0);
                int n2 = HInt(hdr, "NAXIS2", 0);
                int n3 = naxis >= 3 ? HInt(hdr, "NAXIS3", 1) : 1;

                if (naxis >= 2 && n1 > 0 && n2 > 0)
                {
                    if (naxis > 3) throw new Exception(Tr.Pick($"a FITS with {naxis} axes; 2D images or images with 3 planes are accepted.", $"FITS cu {naxis} axe; se accepta imagini 2D sau cu 3 planuri."));
                    if (n3 != 1 && n3 != 3) throw new Exception(Tr.Pick($"a FITS with {n3} planes; 1 or 3 are accepted.", $"FITS cu {n3} planuri; se accepta 1 sau 3."));

                    double bzero = HDbl(hdr, "BZERO", 0.0);
                    double bscale = HDbl(hdr, "BSCALE", 1.0);
                    bool bottomUp = string.Equals(HStr(hdr, "ROWORDER", ""), "BOTTOM-UP", StringComparison.OrdinalIgnoreCase);

                    var raster = new Raster(n2, n1, n3);
                    long clipped = ReadData(s, raster, bitpix, bzero, bscale, bottomUp);

                    string note = null;
                    if (clipped > 0)
                        note = Tr.Pick($"{clipped} values outside [0, 1] were clipped on reading", $"{clipped} valori in afara [0, 1] au fost taiate la citire");
                    if (bottomUp)
                        note = (note == null ? "" : note + "; ") + Tr.Pick("the rows were flipped after ROWORDER = BOTTOM-UP", "randurile au fost rasturnate dupa ROWORDER = BOTTOM-UP");

                    return (raster, hdr, bitpix, n3, note);
                }

                // An HDU without a usable image: skip its data blocks and go on.
                if (!SkipExact(s, dataBlocks)) throw new Exception(Tr.Pick("FITS: truncated file.", "FITS: fisier trunchiat."));
            }
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
        /// <param name="bottomUp">Whether the file's rows run from the bottom up.</param>
        /// <returns>The number of samples clipped to 0 or 1, NaN ones counted at 0.</returns>
        static long ReadData(Stream s, Raster r, int bitpix, double bzero, double bscale, bool bottomUp)
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
            long clipped = 0;
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
                    else if (v > 1) { v = 1; clipped++; }
                    int p = (int)(i / plane);
                    long idx = i % plane;
                    if (bottomUp)
                    {
                        long y = idx / r.W, x = idx % r.W;
                        idx = (r.H - 1 - y) * r.W + x;
                    }
                    r.Planes[p][idx] = (float)v;
                }
            }
            return clipped;
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
                if (s.Position + count > s.Length) return false;
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

        /// <summary>A header value as an integer, read in the invariant culture.</summary>
        /// <param name="h">The header, keyword -> value.</param>
        /// <param name="k">The keyword.</param>
        /// <param name="def">
        /// The value when the keyword is absent or does not parse as an integer with
        /// NumberStyles.Any, which also accepts forms FITS does not ("1,024", "16.0").
        /// </param>
        /// <returns>The integer, or <paramref name="def"/>.</returns>
        internal static int HInt(IReadOnlyDictionary<string, string> h, string k, int def)
            => h.TryGetValue(k, out var v) && int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;

        /// <summary>A header value as a real number, read in the invariant culture.</summary>
        /// <param name="h">The header, keyword -> value.</param>
        /// <param name="k">The keyword.</param>
        /// <param name="def">
        /// The value when the keyword is absent or does not parse with NumberStyles.Any: also a
        /// FITS real with a D exponent ("1.5D-3"), while "0,5" parses as 5.
        /// </param>
        /// <returns>The number, or <paramref name="def"/>.</returns>
        internal static double HDbl(IReadOnlyDictionary<string, string> h, string k, double def)
            => h.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;

        /// <summary>A header value as it was read: a string without its quotes.</summary>
        /// <param name="h">The header, keyword -> value.</param>
        /// <param name="k">The keyword.</param>
        /// <param name="def">The value when the keyword is absent.</param>
        /// <returns>The value, or <paramref name="def"/>.</returns>
        internal static string HStr(IReadOnlyDictionary<string, string> h, string k, string def)
            => h.TryGetValue(k, out var v) ? v : def;
    }
}
