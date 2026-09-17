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

// FitsReader.cs - reader FITS propriu (fara dependinte).
// Header din carduri de 80 octeti in blocuri de 2880, apoi date binare big-endian.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FlatCheck
{
    internal sealed class FitsReader : IFrameReader
    {
        /// <summary>
        /// Revendica doar extensiile FITS. Fisierele pe care nu le revendica NIMENI ajung
        /// tot aici, dar decizia aia e a lui ImageLoader si e scrisa acolo - nu se ascunde
        /// intr-un "Handles care intoarce mereu true".
        /// </summary>
        public bool Handles(string path) => IsFitsPath(path);

        static readonly string[] Extensions = { ".fit", ".fits", ".fts" };

        /// <summary>
        /// Extensiile FITS, expuse din acelasi motiv ca RawReader.RawExtensions: aplicatia
        /// WPF listeaza dupa ele cadrele din flats/ pentru tabul de histograma, si o a doua
        /// lista ar fi divergat de asta.
        /// </summary>
        internal static IReadOnlyList<string> FitsExtensions => Extensions;

        /// <summary>
        /// Doar extensia, ca la raw. E o regula de LISTARE, nu de dispecerat: ce nu
        /// revendica nimeni ajunge tot aici, prin decizia lui ImageLoader.
        /// </summary>
        internal static bool IsFitsPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(Extensions, ext) >= 0;
        }

        public LoadedFrame Load(string path)
        {
            var (img, hdr, cards, planes, bitpix) = LoadFull(File.ReadAllBytes(path));

            // Ordinea culorilor, daca fisierul o poarta. Siril scrie BAYERPAT pe un cadru
            // trecut prin tab-ul Conversion, si NU o scrie pe unul debayerizat - ceea ce e
            // exact corect: acolo nu mai exista mozaic de numit. Offset-urile intra prin
            // Shift, fiindca un decupaj impar fata de senzor muta faza mozaicului. Iar
            // ROWORDER intra prin ForRowOrder: Siril scrie randurile de jos in sus, noi le
            // citim in ordinea fisierului, si pe o inaltime para asta muta faza cu un rand.
            CfaPattern cfa = CfaPattern.Parse(HStr(hdr, "BAYERPAT", ""), "BAYERPAT")
                ?.Shift(HInt(hdr, "YBAYROFF", 0), HInt(hdr, "XBAYROFF", 0))
                ?.ForRowOrder(HStr(hdr, "ROWORDER", ""), img.H);

            // Scala null, nu una inventata: un FITS chiar nu poarta nivelurile ADU, iar
            // ExposureAnalyzer trebuie sa cada pe AutoWhite, nu sa creada un numar fals.
            return new LoadedFrame
            {
                Image = img, Header = hdr, Scale = null, Cfa = cfa,
                Cards = cards, Planes = planes, Bitpix = bitpix,
            };
        }

        // ========================================================= I/O FITS
        // FITS: header din carduri de 80 octeti, in blocuri de 2880. Apoi datele binare,
        // big-endian. BITPIX: 16=short, 32=int, -32=float, -64=double. Valoarea fizica =
        // BZERO + BSCALE * valoare_stocata. Ia primul HDU cu date (NAXIS>=2).
        internal static Img Load(byte[] raw) => LoadWithHeader(raw).Image;

        /// <summary>
        /// Ca <see cref="Load(byte[])"/>, dar intoarce si cardurile HDU-ului din care a
        /// venit imaginea. Headerul era pana acum parsat si aruncat; <c>--light</c> are
        /// nevoie de el ca sa afle focala si scara, deci sa poata refuza un cadru cu camp
        /// prea larg pentru test (vezi LightSuitability).
        ///
        /// Se pastreaza limitarea cunoscuta de parsare - valoarea se taie la primul '/',
        /// ceea ce ar strica o valoare string care contine slash. Cheile citite de aici
        /// sunt toate numerice, deci nu le atinge.
        /// </summary>
        internal static (Img Image, IReadOnlyDictionary<string, string> Header) LoadWithHeader(byte[] raw)
        {
            var (img, hdr, _, _, _) = LoadFull(raw);
            return (img, hdr);
        }

        /// <summary>
        /// Ca <see cref="LoadWithHeader"/>, plus cardurile brute ale HDU-ului din care a
        /// venit imaginea (fara END), numarul de planuri si BITPIX-ul lui. Cardurile
        /// brute sunt ce copiaza FitsWriter intr-un fisier scris inapoi; dictionarul nu
        /// poate fi sursa, fiindca pierde comentariile si taie sirurile la primul '/'.
        /// </summary>
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
                    if (pos + 2880 > raw.Length) throw new Exception("FITS: header incomplet.");
                    for (int c = 0; c < 36; c++)
                    {
                        string card = System.Text.Encoding.ASCII.GetString(raw, pos + c * 80, 80);
                        string key = card.Length >= 8 ? card.Substring(0, 8).Trim() : "";
                        if (key == "END") { end = true; break; }   // nimic dupa END nu mai conteaza
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
                    // HDU fara imagine (ex: primary gol) - sar peste datele lui (0) si continui
                    if (naxis == 0) continue;
                }
                int n1 = HInt(hdr, "NAXIS1", 0);
                int n2 = HInt(hdr, "NAXIS2", 0);
                int n3 = naxis >= 3 ? HInt(hdr, "NAXIS3", 1) : 1;
                double bzero = HDbl(hdr, "BZERO", 0.0);
                double bscale = HDbl(hdr, "BSCALE", 1.0);

                // Produsul TUTUROR axelor. Inainte era n1*n2*max(1,n3), care pentru un HDU
                // 1-D dadea 0 -> pos nu avansa, iar zona de date era parsata ca header,
                // esuand cu "header incomplet" in loc de "niciun HDU cu imagine 2D".
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
                    // color (n3 planuri, Siril = plane-sequential) -> media pe planuri
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
                // HDU fara imagine 2D utila: sar peste blocurile lui de date si continui
                pos += (int)dataBlocks;
            }
            throw new Exception("FITS: niciun HDU cu imagine 2D gasit.");
        }

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

        internal static int HInt(IReadOnlyDictionary<string, string> h, string k, int def)
            => h.TryGetValue(k, out var v) && int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;
        internal static double HDbl(IReadOnlyDictionary<string, string> h, string k, double def)
            => h.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;

        /// <summary>
        /// Valoarea bruta a unui card, fara ghilimelele din jurul sirurilor FITS.
        /// Folosita pentru DATE-OBS in modul de atribuire, unde data nu se interpreteaza,
        /// doar se arata omului.
        /// </summary>
        internal static string HStr(IReadOnlyDictionary<string, string> h, string k, string def)
            => h.TryGetValue(k, out var v) ? v.Trim().Trim('\'').Trim() : def;
    }
}
