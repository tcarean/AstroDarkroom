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

// FitsReader.cs - our own FITS reader (no dependencies).
// Header of 80-byte cards in 2880-byte blocks, then big-endian binary data.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FlatCheck
{
    /// <summary>
    /// Reads a FITS file: the first HDU with a 2D image, its planes averaged into one raster,
    /// the header's cards as a dictionary and as they stand, and the CFA order from BAYERPAT.
    /// </summary>
    internal sealed class FitsReader : IFrameReader
    {
        /// <summary>
        /// Claims only the FITS extensions. The files NO ONE claims still end up here, but that
        /// decision is ImageLoader's and is written there - it does not hide in a "Handles that
        /// always returns true".
        /// </summary>
        /// <param name="path">The file's path; only its extension is looked at.</param>
        /// <returns>True for .fit, .fits or .fts, any case.</returns>
        public bool Handles(string path) => IsFitsPath(path);

        static readonly string[] Extensions = { ".fit", ".fits", ".fts" };

        /// <summary>
        /// The FITS extensions, exposed for the same reason as RawReader.RawExtensions: the WPF
        /// application lists the frames of flats/ by them for the histogram tab, and a second
        /// list would have diverged from this one.
        /// </summary>
        internal static IReadOnlyList<string> FitsExtensions => Extensions;

        /// <summary>
        /// The extension only, as for raws. It is a LISTING rule, not a dispatch rule: what no
        /// one claims still ends up here, by ImageLoader's decision.
        /// </summary>
        /// <param name="path">The file's path; null or empty allowed.</param>
        /// <returns>True when the extension is a FITS one.</returns>
        internal static bool IsFitsPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(Extensions, ext) >= 0;
        }

        const string NotSimple = "FITS: fisierul nu incepe cu SIMPLE.";
        const string HeaderIncomplete = "FITS: header incomplet.";

        /// <summary>
        /// A card's keyword: the first 8 bytes, without blanks. One definition for Load and
        /// LoadHeader, so that the two cannot read a card differently.
        /// </summary>
        /// <param name="card">The card's text, normally 80 characters.</param>
        /// <returns>The keyword; empty for a card shorter than 8 characters.</returns>
        static string KeyOf(string card) => card.Length >= 8 ? card.Substring(0, 8).Trim() : "";

        /// <summary>
        /// The file's magic: the first card of the primary HDU is SIMPLE, otherwise it is not a
        /// FITS and both reads refuse the file with the same message (ADR 0057).
        /// <paramref name="length"/> is how much was actually read into
        /// <paramref name="bytes"/>: a file that is empty or shorter than a keyword is refused
        /// the same way. Throws InvalidDataException.
        /// </summary>
        /// <param name="bytes">The start of the file.</param>
        /// <param name="length">How many of <paramref name="bytes"/> hold file data.</param>
        static void RequireSimple(byte[] bytes, int length)
        {
            if (length < 8 || KeyOf(Encoding.ASCII.GetString(bytes, 0, Math.Min(length, 80))) != "SIMPLE")
                throw new InvalidDataException(NotSimple);
        }

        /// <summary>
        /// Only the header of the primary HDU, without pixels: the 2880 blocks up to END. For
        /// whoever wants FOCALLEN or INSTRUME from a 96 MB master without reading it (the
        /// Synthetic tab, when the session opens). It refuses, with InvalidDataException,
        /// exactly what the full load refuses too: a file that does not begin with the SIMPLE
        /// card (empty, shorter than a keyword, or with something else in the first card) and a
        /// header cut before END. An empty header would be an answer, not an error, and every
        /// caller would have to recognise it by the number of cards (ADR 0057).
        /// </summary>
        /// <param name="path">The FITS file to read.</param>
        /// <returns>The keywords and their values, string quotes and comments removed.</returns>
        internal static IReadOnlyDictionary<string, string> LoadHeader(string path)
        {
            var hdr = new Dictionary<string, string>(StringComparer.Ordinal);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var block = new byte[2880];
            for (int b = 0; b < 1000; b++)
            {
                int got = 0;
                while (got < block.Length)
                {
                    int n = fs.Read(block, got, block.Length - got);
                    if (n <= 0) break;
                    got += n;
                }
                if (b == 0) RequireSimple(block, got);
                if (got < block.Length) throw new InvalidDataException(HeaderIncomplete);
                for (int c = 0; c < 36; c++)
                {
                    string card = Encoding.ASCII.GetString(block, c * 80, 80);
                    string key = KeyOf(card);
                    if (key == "END") return hdr;
                    if (key.Length == 0 || card.Length < 10 || card[8] != '=') continue;
                    string value = card.Substring(10);
                    int slash = value.IndexOf('/');
                    if (slash >= 0 && !value.TrimStart().StartsWith("'")) value = value.Substring(0, slash);
                    else if (slash >= 0)
                    {
                        int close = value.IndexOf('\'', value.IndexOf('\'') + 1);
                        if (close >= 0) value = value.Substring(0, close + 1);
                    }
                    value = value.Trim();
                    if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'') value = value.Substring(1, value.Length - 2).Trim();
                    hdr[key] = value;
                }
            }
            return hdr;
        }

        /// <summary>
        /// Reads the whole file: the first HDU with a 2D image, its planes averaged, the header
        /// as a dictionary and as raw cards, the CFA order from BAYERPAT, and the focal length
        /// and camera. The ADU scale stays null: a FITS does not carry it. Throws
        /// InvalidDataException on a file that is not a FITS or whose header is cut.
        /// </summary>
        /// <param name="path">The FITS file to read.</param>
        /// <returns>The frame, its image filled.</returns>
        public LoadedFrame Load(string path)
        {
            var (img, hdr, cards, planes, bitpix) = LoadFull(File.ReadAllBytes(path));

            // The colour order, if the file carries it. Siril writes BAYERPAT on a frame passed
            // through the Conversion tab, and does NOT write it on a debayered one - which is
            // exactly right: there is no mosaic left to name there. The offsets come in through
            // Shift, because an odd crop relative to the sensor shifts the mosaic's phase. And
            // ROWORDER comes in through ForRowOrder: Siril writes the rows bottom up, we read them
            // in the file's order, and on an even height that shifts the phase by one row.
            CfaPattern cfa = CfaPattern.Parse(HStr(hdr, "BAYERPAT", ""), "BAYERPAT")
                ?.Shift(HInt(hdr, "YBAYROFF", 0), HInt(hdr, "XBAYROFF", 0))
                ?.ForRowOrder(HStr(hdr, "ROWORDER", ""), img.H);

            // A null scale, not an invented one: a FITS really does not carry the ADU levels,
            // and ExposureAnalyzer has to fall back on AutoWhite, not believe a false number.
            var f = new LoadedFrame
            {
                Image = img,
                Header = hdr,
                Scale = null,
                Cfa = cfa,
                Cards = cards,
                Planes = planes,
                Bitpix = bitpix,
            };
            FillShootingInfo(f, hdr);
            return f;
        }

        /// <summary>
        /// Only what the header says: focal length, camera, planes, BITPIX, CFA order. No pixels
        /// and no raw cards (LoadHeader does not keep them; whoever writes a file back loads
        /// everything anyway). What LoadHeader refuses (no SIMPLE, a cut header) throws
        /// InvalidDataException with the same message as Load on the same file (ADR 0057).
        /// </summary>
        /// <param name="path">The FITS file to read.</param>
        /// <returns>The frame's metadata, its image null.</returns>
        public LoadedFrame LoadInfo(string path)
        {
            IReadOnlyDictionary<string, string> hdr = LoadHeader(path);
            int h = HInt(hdr, "NAXIS2", 0);
            var f = new LoadedFrame
            {
                Image = null,
                Header = hdr,
                Scale = null,
                Cfa = CfaPattern.Parse(HStr(hdr, "BAYERPAT", ""), "BAYERPAT")
                    ?.Shift(HInt(hdr, "YBAYROFF", 0), HInt(hdr, "XBAYROFF", 0))
                    ?.ForRowOrder(HStr(hdr, "ROWORDER", ""), h),
                Planes = HInt(hdr, "NAXIS", 2) >= 3 ? Math.Max(1, HInt(hdr, "NAXIS3", 1)) : 1,
                Bitpix = HInt(hdr, "BITPIX", 0),
            };
            FillShootingInfo(f, hdr);
            return f;
        }

        /// <summary>
        /// What a FITS header knows about the shot: FOCALLEN and INSTRUME. No f-number - Siril
        /// does not write it - and no lens. One place for Load and LoadInfo, so that the two
        /// cannot read the header differently.
        /// </summary>
        /// <param name="f">The frame to fill; its FocalMm and Camera are set.</param>
        /// <param name="hdr">The header's keywords.</param>
        static void FillShootingInfo(LoadedFrame f, IReadOnlyDictionary<string, string> hdr)
        {
            double focal = HDbl(hdr, "FOCALLEN", 0.0);
            string camera = HStr(hdr, "INSTRUME", null);
            f.FocalMm = focal > 0 ? focal : null;
            f.Camera = string.IsNullOrWhiteSpace(camera) ? null : camera.Trim();
        }

        // ========================================================= FITS I/O
        // FITS: a header of 80-byte cards, in 2880-byte blocks. Then the binary data,
        // big-endian. BITPIX: 16=short, 32=int, -32=float, -64=double. Physical value =
        // BZERO + BSCALE * stored_value. Takes the first HDU with data (NAXIS>=2).
        /// <summary>
        /// The image of a FITS already in memory, as <see cref="LoadFull"/> reads it.
        /// </summary>
        /// <param name="raw">The whole file's bytes.</param>
        /// <returns>The image, its planes averaged.</returns>
        internal static Img Load(byte[] raw) => LoadWithHeader(raw).Image;

        /// <summary>
        /// Like <see cref="Load(byte[])"/>, but also returns the cards of the HDU the image came
        /// from. The header used to be parsed and thrown away; <c>--light</c> needs it to find
        /// the focal length and the scale, so that it can refuse a frame with too wide a field
        /// for the test (see LightSuitability).
        ///
        /// The known parsing limitation is kept - the value is cut at the first '/', which
        /// would break a string value that contains a slash. The keys read from here are all
        /// numeric, so it does not touch them.
        /// </summary>
        /// <param name="raw">The whole file's bytes.</param>
        /// <returns>The image and the keywords of its HDU.</returns>
        internal static (Img Image, IReadOnlyDictionary<string, string> Header) LoadWithHeader(byte[] raw)
        {
            var (img, hdr, _, _, _) = LoadFull(raw);
            return (img, hdr);
        }

        /// <summary>
        /// Like <see cref="LoadWithHeader"/>, plus the raw cards of the HDU the image came from
        /// (without END), its number of planes and its BITPIX. The raw cards are what FitsWriter
        /// copies into a file written back; the dictionary cannot be the source, because it
        /// loses the comments and cuts the strings at the first '/'. Throws
        /// InvalidDataException on a file that is not a FITS or whose header is cut, and an
        /// Exception when no HDU holds a 2D image or BITPIX is not one of the five.
        /// </summary>
        /// <param name="raw">The whole file's bytes.</param>
        /// <returns>
        /// The image (planes averaged, values BZERO + BSCALE * stored), the keywords, the raw
        /// cards, NAXIS3 (1 for a 2D image) and BITPIX.
        /// </returns>
        internal static (Img Image, IReadOnlyDictionary<string, string> Header, IReadOnlyList<string> Cards, int Planes, int Bitpix)
            LoadFull(byte[] raw)
        {
            RequireSimple(raw, raw.Length);
            int pos = 0;
            while (pos < raw.Length)
            {
                var hdr = new Dictionary<string, string>();
                var cards = new List<string>();
                bool end = false;
                while (!end)
                {
                    if (pos + 2880 > raw.Length) throw new InvalidDataException(HeaderIncomplete);
                    for (int c = 0; c < 36; c++)
                    {
                        string card = System.Text.Encoding.ASCII.GetString(raw, pos + c * 80, 80);
                        string key = KeyOf(card);
                        if (key == "END") { end = true; break; }   // nothing after END matters
                        cards.Add(card);
                        if (card.Length > 10 && card[8] == '=')
                        {
                            string val = card.Substring(9);
                            int slash = val.IndexOf('/');
                            if (slash >= 0) val = val.Substring(0, slash);
                            hdr[key] = val.Trim().Trim('\'').Trim();
                        }
                    }
                    pos += 2880;
                }

                int bitpix = HInt(hdr, "BITPIX", 0);
                int naxis = HInt(hdr, "NAXIS", 0);
                if (naxis < 2)
                {
                    // an HDU without an image (e.g. an empty primary) - skip its data (0) and go on
                    if (naxis == 0) continue;
                }
                int n1 = HInt(hdr, "NAXIS1", 0);
                int n2 = HInt(hdr, "NAXIS2", 0);
                int n3 = naxis >= 3 ? HInt(hdr, "NAXIS3", 1) : 1;
                double bzero = HDbl(hdr, "BZERO", 0.0);
                double bscale = HDbl(hdr, "BSCALE", 1.0);

                // The product of ALL the axes. It used to be n1*n2*max(1,n3), which for a 1-D
                // HDU gave 0 -> pos did not advance, and the data area was parsed as a header,
                // failing with "header incomplet" instead of "niciun HDU cu imagine 2D".
                long npix = 1;
                for (int ax = 1; ax <= naxis; ax++) npix *= HInt(hdr, "NAXIS" + ax, 1);
                int bytesPer = Math.Abs(bitpix) / 8;
                long dataBytes = npix * bytesPer;
                long dataBlocks = (dataBytes + 2879) / 2880 * 2880;

                if (n1 > 0 && n2 > 0)
                {
                    var vals = new double[npix];
                    int p = pos;
                    for (long i = 0; i < npix; i++)
                    {
                        double v = ReadSample(raw, p, bitpix);
                        vals[i] = bzero + bscale * v;
                        p += bytesPer;
                    }
                    // colour (n3 planes, Siril = plane-sequential) -> the mean over the planes
                    var img = new Img(n2, n1);
                    if (n3 <= 1)
                        Array.Copy(vals, img.D, n1 * n2);
                    else
                    {
                        long plane = (long)n1 * n2;
                        for (long i = 0; i < plane; i++)
                        {
                            double s = 0;
                            for (int c = 0; c < n3; c++) s += vals[c * plane + i];
                            img.D[i] = s / n3;
                        }
                    }
                    return (img, hdr, cards, n3, bitpix);
                }
                // an HDU without a usable 2D image: skip its data blocks and go on
                pos += (int)dataBlocks;
            }
            throw new Exception("FITS: niciun HDU cu imagine 2D gasit.");
        }

        /// <summary>One big-endian sample, as stored (before BZERO and BSCALE).</summary>
        /// <param name="b">The file's bytes.</param>
        /// <param name="p">The sample's first byte.</param>
        /// <param name="bitpix">8, 16, 32, -32 or -64; anything else throws an Exception.</param>
        /// <returns>The stored value.</returns>
        internal static double ReadSample(byte[] b, int p, int bitpix)
        {
            switch (bitpix)
            {
                case 8: return b[p];
                case 16: return (short)((b[p] << 8) | b[p + 1]);
                case 32: return (int)((b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]);
                case -32:
                    {
                        int bits = (b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3];
                        return BitConverter.Int32BitsToSingle(bits);
                    }
                case -64:
                    {
                        long bits = 0;
                        for (int k = 0; k < 8; k++) bits = (bits << 8) | b[p + k];
                        return BitConverter.Int64BitsToDouble(bits);
                    }
                default: throw new Exception("BITPIX neacceptat: " + bitpix);
            }
        }

        /// <summary>A header value as an integer.</summary>
        /// <param name="h">The header's keywords.</param>
        /// <param name="k">The keyword.</param>
        /// <param name="def">What to return when the keyword is missing or not an integer.</param>
        /// <returns>The value, or <paramref name="def"/>.</returns>
        internal static int HInt(IReadOnlyDictionary<string, string> h, string k, int def)
            => h.TryGetValue(k, out var v) && int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;
        /// <summary>A header value as a number, read with the invariant culture.</summary>
        /// <param name="h">The header's keywords.</param>
        /// <param name="k">The keyword.</param>
        /// <param name="def">What to return when the keyword is missing or not a number.</param>
        /// <returns>The value, or <paramref name="def"/>.</returns>
        internal static double HDbl(IReadOnlyDictionary<string, string> h, string k, double def)
            => h.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;

        /// <summary>
        /// The raw value of a card, without the quotes around FITS strings. Used for DATE-OBS in
        /// the attribution mode, where the date is not interpreted, only shown to the person.
        /// </summary>
        /// <param name="h">The header's keywords.</param>
        /// <param name="k">The keyword.</param>
        /// <param name="def">What to return when the keyword is missing; null allowed.</param>
        /// <returns>The value, trimmed and unquoted, or <paramref name="def"/>.</returns>
        internal static string HStr(IReadOnlyDictionary<string, string> h, string k, string def)
            => h.TryGetValue(k, out var v) ? v.Trim().Trim('\'').Trim() : def;
    }
}
