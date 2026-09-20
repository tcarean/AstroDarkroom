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

// FitsReader.cs - cititor FITS propriu (fara dependinte), pornit din cel al FlatCheck.
// Header din carduri de 80 octeti in blocuri de 2880, apoi date binare big-endian.
//
// CE S-A PASTRAT din FlatCheck: parsarea headerului, BZERO/BSCALE, sarirea peste HDU-uri
// fara imagine, toate tipurile BITPIX. CE S-A SCHIMBAT:
//   - se citeste in flux, nu prin File.ReadAllBytes: un FITS float RGB de 23 MP are 277
//     MB, si a-l tine de doua ori in memorie (octeti + float) doar ca sa fie parsat e o
//     risipa care se simte;
//   - planurile se PASTREAZA (1 sau 3), nu se mediaza intr-unul singur - aici culoarea
//     e obiect de masura, nu zgomot;
//   - valorile se normalizeaza in 0..1 dupa tipul din fisier, ca TIFF-ul si PNG-ul;
//   - randurile se rastoarna pe ROWORDER = 'BOTTOM-UP' (asa scrie Siril), ca "coltul
//     dreapta-jos" din raport sa fie acelasi colt ca intr-un TIFF.
// CE S-A TAIAT: CFA, focala, aparat, obiectiv - o imagine finita n-are mozaic si nu
// se compara cu profile de vignetare.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AstroPhotoAnalyzer
{
    internal sealed class FitsReader : IFrameReader
    {
        static readonly string[] Exts = { ".fit", ".fits", ".fts" };

        public IReadOnlyList<string> Extensions => Exts;

        public bool Handles(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(Exts, ext) >= 0;
        }

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
                Note = note,
            };
            return raster;
        }

        /// <summary>
        /// Primul HDU cu o imagine 2D sau 3D. Intoarce si headerul lui, BITPIX-ul,
        /// numarul de planuri din fisier si o nota pentru raport (sau null).
        /// </summary>
        internal static (Raster Raster, IReadOnlyDictionary<string, string> Header, int Bitpix, int Planes, string Note)
            LoadFrom(Stream s)
        {
            var block = new byte[2880];
            while (true)
            {
                var hdr = ReadHeader(s, block);
                if (hdr == null) throw new Exception("FITS: niciun HDU cu imagine gasit.");

                int bitpix = HInt(hdr, "BITPIX", 0);
                int naxis = HInt(hdr, "NAXIS", 0);

                // Un HDU cu NAXIS = 0 (primarul gol al unui fisier cu extensii) n-are date
                // DELOC - nu "un pixel". Produsul gol ar da 1 si s-ar sari peste un bloc
                // de 2880 care nu exista, si urmatorul header ar fi citit din mijlocul
                // imaginii.
                long npix = naxis == 0 ? 0 : 1;
                for (int ax = 1; ax <= naxis; ax++) npix *= HInt(hdr, "NAXIS" + ax, 1);
                int bytesPer = Math.Abs(bitpix) / 8;
                if (bytesPer == 0 && naxis > 0) throw new Exception("BITPIX neacceptat: " + bitpix);
                long dataBytes = npix * bytesPer;
                long dataBlocks = (dataBytes + 2879) / 2880 * 2880;

                int n1 = HInt(hdr, "NAXIS1", 0);
                int n2 = HInt(hdr, "NAXIS2", 0);
                int n3 = naxis >= 3 ? HInt(hdr, "NAXIS3", 1) : 1;

                if (naxis >= 2 && n1 > 0 && n2 > 0)
                {
                    if (naxis > 3) throw new Exception($"FITS cu {naxis} axe; se accepta imagini 2D sau cu 3 planuri.");
                    if (n3 != 1 && n3 != 3) throw new Exception($"FITS cu {n3} planuri; se accepta 1 sau 3.");

                    double bzero = HDbl(hdr, "BZERO", 0.0);
                    double bscale = HDbl(hdr, "BSCALE", 1.0);
                    bool bottomUp = string.Equals(HStr(hdr, "ROWORDER", ""), "BOTTOM-UP", StringComparison.OrdinalIgnoreCase);

                    var raster = new Raster(n2, n1, n3);
                    long clipped = ReadData(s, raster, bitpix, bzero, bscale, bottomUp);

                    string note = null;
                    if (clipped > 0)
                        note = $"{clipped} valori in afara [0, 1] au fost taiate la citire";
                    if (bottomUp)
                        note = (note == null ? "" : note + "; ") + "randurile au fost rasturnate dupa ROWORDER = BOTTOM-UP";

                    return (raster, hdr, bitpix, n3, note);
                }

                // HDU fara imagine utila: sar peste blocurile lui de date si continui.
                if (!SkipExact(s, dataBlocks)) throw new Exception("FITS: fisier trunchiat.");
            }
        }

        /// <summary>Cardurile pana la END, ca dictionar; null la sfarsit de fisier.</summary>
        static Dictionary<string, string> ReadHeader(Stream s, byte[] block)
        {
            var hdr = new Dictionary<string, string>(StringComparer.Ordinal);
            bool any = false;
            while (true)
            {
                if (!ReadExact(s, block, block.Length))
                {
                    if (!any) return null;
                    throw new Exception("FITS: header incomplet.");
                }
                any = true;
                for (int c = 0; c < 36; c++)
                {
                    string card = Encoding.ASCII.GetString(block, c * 80, 80);
                    string key = card.Substring(0, 8).Trim();
                    if (key == "END") return hdr;
                    if (key.Length == 0 || card[8] != '=') continue;

                    // Valoarea: pana la '/', dar un '/' dintr-un sir intre ghilimele nu e
                    // comentariu. Sirurile se dau fara ghilimele si fara spatiile din
                    // interior de la sfarsit (FITS le umple cu spatii).
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
        /// Datele, plan dupa plan (FITS e plane-sequential), decodate in bucati de 1 MB.
        /// Intoarce cate valori au iesit din [0, 1] si au fost taiate - pe int nu se
        /// intampla, pe float se poate.
        /// </summary>
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
                if (!ReadExact(s, buf, want * bytesPer)) throw new Exception("FITS: date trunchiate.");
                for (int k = 0; k < want; k++, i++)
                {
                    double v = (bzero + bscale * ReadSample(buf, k * bytesPer, bitpix)) / scale;
                    if (v < 0) { v = 0; clipped++; }
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

        internal static double ReadSample(byte[] b, int p, int bitpix)
        {
            switch (bitpix)
            {
                case 8: return b[p];
                case 16: return BinaryPrimitives.ReadInt16BigEndian(new ReadOnlySpan<byte>(b, p, 2));
                case 32: return BinaryPrimitives.ReadInt32BigEndian(new ReadOnlySpan<byte>(b, p, 4));
                case -32: return BinaryPrimitives.ReadSingleBigEndian(new ReadOnlySpan<byte>(b, p, 4));
                case -64: return BinaryPrimitives.ReadDoubleBigEndian(new ReadOnlySpan<byte>(b, p, 8));
                default: throw new Exception("BITPIX neacceptat: " + bitpix);
            }
        }

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

        internal static int HInt(IReadOnlyDictionary<string, string> h, string k, int def)
            => h.TryGetValue(k, out var v) && int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;
        internal static double HDbl(IReadOnlyDictionary<string, string> h, string k, double def)
            => h.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;
        internal static string HStr(IReadOnlyDictionary<string, string> h, string k, string def)
            => h.TryGetValue(k, out var v) ? v : def;
    }
}
