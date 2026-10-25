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
// The code is the copy's, with FlatCheck's cut-data check of 469da9b (ADR 0020); the comments
// are DarksCheck's own (ADR 0019).

// FitsReader.cs - our own FITS reader (no dependencies).
// Header of 80-byte cards in 2880-byte blocks, then big-endian binary data.

using System.Globalization;
using System.Text;

namespace DarksCheck.Core
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
        /// The FITS extensions, as RawReader.RawExtensions exposes the raw ones. DarksCheck lists
        /// a folder's FITS frames through <see cref="IsFitsPath"/> and does not read this list.
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

        /// <summary>
        /// Only the header of the primary HDU, without pixels: the 2880 blocks up to END, for
        /// <see cref="LoadInfo"/>, so the inventory reads a FITS frame's settings without its
        /// pixels. It checks no SIMPLE card and throws nothing
        /// for the content: a file that ends before END gives the keywords read so far, and a
        /// file that is not a FITS gives whatever 80-byte pieces happen to read as cards,
        /// usually none. (FlatCheck's ADR 0057 makes the source refuse both; this copy is older.)
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
                    if (n <= 0) return hdr;
                    got += n;
                }
                for (int c = 0; c < 36; c++)
                {
                    string card = Encoding.ASCII.GetString(block, c * 80, 80);
                    string key = card.Substring(0, 8).Trim();
                    if (key == "END") return hdr;
                    if (key.Length == 0 || card.Length < 10 || card[8] != '=') continue;
                    string value = card.Substring(10);
                    int slash = value.IndexOf('/');
                    if (slash >= 0 && !value.TrimStart().StartsWith('\'')) value = value.Substring(0, slash);
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
        /// as a dictionary and as raw cards, the CFA order from BAYERPAT, and what
        /// <see cref="FillShootingInfo"/> reads. The ADU scale stays null: a FITS does not carry
        /// it. Throws InvalidDataException as <see cref="LoadFull"/> does.
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

            // A null scale, not an invented one: a FITS really does not carry the ADU levels;
            // FrameStack then takes the white from BITPIX and estimates the black, and says so.
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
        /// Only what the header says: focal length, camera, planes, BITPIX, CFA order and what
        /// <see cref="FillShootingInfo"/> reads. No pixels and no raw cards (LoadHeader does not
        /// keep them). A file that is not a FITS gives what LoadHeader gives for it,
        /// usually no keywords, not an error.
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
        /// What a FITS header knows about the shot: FOCALLEN and INSTRUME, plus - unlike
        /// FlatCheck, where the file is copied from - EXPTIME, ISOSPEED, DATE-OBS and XPIXSZ,
        /// which Siril writes on a light converted from a raw. No f-number - Siril does not
        /// write it - and no lens. One place for Load and LoadInfo, so that the two cannot read
        /// the header differently. DATE-OBS is in UTC in Siril, whereas libraw gives the
        /// camera's clock: the two do not mix within a session, so they are not converted.
        /// </summary>
        /// <param name="f">
        /// The frame to fill: its FocalMm, Camera, ExposureSeconds, Iso, PixelSizeUm and
        /// Timestamp are set, each null when the card is missing, not positive or not a date.
        /// </param>
        /// <param name="hdr">The header's keywords.</param>
        static void FillShootingInfo(LoadedFrame f, IReadOnlyDictionary<string, string> hdr)
        {
            double focal = HDbl(hdr, "FOCALLEN", 0.0);
            string camera = HStr(hdr, "INSTRUME", null);
            f.FocalMm = focal > 0 ? focal : null;
            f.Camera = string.IsNullOrWhiteSpace(camera) ? null : camera.Trim();

            double exposure = HDbl(hdr, "EXPTIME", 0.0);
            f.ExposureSeconds = exposure > 0 ? exposure : null;
            double iso = HDbl(hdr, "ISOSPEED", 0.0);
            f.Iso = iso > 0 ? iso : null;
            double pixel = HDbl(hdr, "XPIXSZ", 0.0);
            f.PixelSizeUm = pixel > 0 ? pixel : null;
            string date = HStr(hdr, "DATE-OBS", null);
            f.Timestamp = DateTime.TryParse(date, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime t)
                ? t : null;
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
        /// Like <see cref="Load(byte[])"/>, but also returns the keywords of the HDU the image
        /// came from. Only the tests call it in DarksCheck.
        ///
        /// The known parsing limitation is kept - the value is cut at the first '/', which
        /// would break a string value that contains a slash. The keys read from here are numeric,
        /// or strings that hold no slash (BAYERPAT, ROWORDER, INSTRUME, DATE-OBS), so it does not
        /// touch them.
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
        /// (without END), its number of planes and its BITPIX: the raw cards keep the comments
        /// and the strings the dictionary cuts at the first '/'. DarksCheck reads BITPIX from it
        /// (for a FITS's white level); nothing here writes a FITS back. It checks no SIMPLE card.
        /// Throws InvalidDataException when a header runs past the end of the file (what a file
        /// that is not a FITS usually does), when no HDU holds a 2D image (an empty file
        /// included), when BITPIX is not one of the five, and when the data area is shorter
        /// than the header declares (checked for every HDU before a sample is read).
        /// </summary>
        /// <param name="raw">The whole file's bytes.</param>
        /// <returns>
        /// The image (planes averaged, values BZERO + BSCALE * stored), the keywords, the raw
        /// cards, NAXIS3 (1 for a 2D image) and BITPIX.
        /// </returns>
        internal static (Img Image, IReadOnlyDictionary<string, string> Header, IReadOnlyList<string> Cards, int Planes, int Bitpix)
            LoadFull(byte[] raw)
        {

            int pos = 0;
            while (pos < raw.Length)
            {
                var hdr = new Dictionary<string, string>();
                var cards = new List<string>();
                bool end = false;
                while (!end)
                {
                    if (pos + 2880 > raw.Length) throw new InvalidDataException("FITS: header incomplet.");
                    for (int c = 0; c < 36; c++)
                    {
                        string card = System.Text.Encoding.ASCII.GetString(raw, pos + c * 80, 80);
                        string key = card.Length >= 8 ? card.Substring(0, 8).Trim() : "";
                        if (key == "END") { end = true; break; }   // nothing after END matters any more
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

                // A file cut mid-transfer: the header promises more pixels than the file holds.
                // Checked for every HDU, before a sample is read, so ReadSample never indexes
                // past the end (it used to throw IndexOutOfRangeException). The data alone
                // counts, not the padding to 2880 bytes, which a last HDU may lack.
                if (pos + dataBytes > raw.Length)
                    throw new InvalidDataException(DataCut(dataBytes, raw.Length - pos));

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
            throw new InvalidDataException("FITS: niciun HDU cu imagine 2D gasit.");
        }

        /// <summary>
        /// The refusal of a data area shorter than its header declares, a copy cut
        /// mid-transfer (FlatCheck's R-009). Both counts are in the message so the reader of the
        /// error sees how much is missing.
        /// </summary>
        /// <param name="announced">The bytes of pixels the header's NAXISn and BITPIX promise.</param>
        /// <param name="held">The bytes the file holds after that header, fewer than announced.</param>
        /// <returns>The message, Romanian.</returns>
        static string DataCut(long announced, long held)
        {
            string n = announced.ToString(CultureInfo.InvariantCulture);
            string m = held.ToString(CultureInfo.InvariantCulture);
            // a label form, so the Romanian numeral needs no "de" agreement for any count
            return "FITS: date trunchiate: octeti de pixeli anuntati de header: " + n + ", in fisier doar " + m + ".";
        }

        /// <summary>One big-endian sample, as stored (before BZERO and BSCALE).</summary>
        /// <param name="b">The file's bytes.</param>
        /// <param name="p">The sample's first byte.</param>
        /// <param name="bitpix">8, 16, 32, -32 or -64; anything else throws
        /// InvalidDataException.</param>
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
                default: throw new InvalidDataException("BITPIX neacceptat: " + bitpix);
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
        /// The raw value of a card, without the quotes around FITS strings. Used for BAYERPAT,
        /// ROWORDER, INSTRUME and DATE-OBS, which <see cref="FillShootingInfo"/> then parses as a
        /// UTC time.
        /// </summary>
        /// <param name="h">The header's keywords.</param>
        /// <param name="k">The keyword.</param>
        /// <param name="def">What to return when the keyword is missing; null allowed.</param>
        /// <returns>The value, trimmed and unquoted, or <paramref name="def"/>.</returns>
        internal static string HStr(IReadOnlyDictionary<string, string> h, string k, string def)
            => h.TryGetValue(k, out var v) ? v.Trim().Trim('\'').Trim() : def;
    }
}
