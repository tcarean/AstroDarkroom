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

// FitsWriter.cs - singurul lucru pe care unealta il scrie in afara de raport: un FITS NOU
// cu o curba aplicata (R-145, ADR 0032). Scris de mana, ca si cititorul (ADR 0009).
//
// DINTR-UN FITS. Fisierul sursa se redeschide DOAR pentru citire si se copiaza asa cum e,
// octet cu octet, in afara de doua locuri ale HDU-ului cu imaginea: antetul primeste
// carduri HISTORY inaintea lui END (toate celelalte carduri raman, in ordine: WCS, SIP,
// expunere, instrument), iar datele se scriu din rasterul din memorie trecut prin curba,
// cu aceeasi codare (BITPIX, BZERO, BSCALE) si aceeasi ordine a randurilor (ROWORDER) ca
// in sursa. HDU-urile de dinainte si de dupa trec neatinse.
//
// DIN ALTCEVA (PNG, TIFF, JPEG). Un FITS float pe 32 de biti cu un antet minim, randurile
// de sus in jos. Profilul ICC al sursei nu se duce: FITS n-are un loc standard pentru el.
//
// Valorile sunt cele ale rasterului: ce cititorul a taiat la 0..1 ramane taiat.
//
// Se scrie intr-un fisier temporar din folderul tinta, mutat la sfarsit peste nume: o
// scriere esuata nu lasa un FITS pe jumatate.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AstroPhotoAnalyzer
{
    internal static class FitsWriter
    {
        const int Block = 2880, Card = 80;

        /// <summary>
        /// Scrie la <paramref name="target"/> rasterul <paramref name="r"/> trecut prin
        /// <paramref name="curve"/>. <paramref name="sourceFits"/> e FITS-ul din care a fost
        /// citit rasterul, sau null cand sursa nu e un FITS.
        /// </summary>
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
                try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { /* fisierul temporar ramane; eroarea de scriere e cea care conteaza */ }
                throw;
            }
        }

        static byte[] HistoryCard(string text) => Encoding.ASCII.GetBytes(("HISTORY " + text).PadRight(Card).Substring(0, Card));

        static byte[] ValueCard(string key, string value) => Encoding.ASCII.GetBytes((key.PadRight(8) + "= " + value.PadLeft(20)).PadRight(Card));

        static byte[] StringCard(string key, string text) => Encoding.ASCII.GetBytes((key.PadRight(8) + "= '" + text.PadRight(8) + "'").PadRight(Card));

        static void WriteCards(Stream o, List<byte[]> cards)
        {
            long n = 0;
            foreach (byte[] c in cards) { o.Write(c, 0, Card); n += Card; }
            var space = new byte[] { (byte)' ' };
            for (; n % Block != 0; n++) o.Write(space, 0, 1);
        }

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
                    // Un HDU de dinaintea imaginii: trece asa cum e.
                    Copy(s, o, start, headerEnd + dataBlocks);
                    continue;
                }
                if (n1 != r.W || n2 != r.H || n3 != r.Planes.Length)
                    throw new Exception("FITS-ul sursa nu mai are geometria imaginii analizate: " + sourceFits);

                // Antetul: toate cardurile pana la END, apoi HISTORY, apoi END.
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

                // Ce urmeaza dupa imagine (extensii) trece asa cum e.
                Copy(s, o, headerEnd + dataBlocks, s.Length);
                return;
            }
        }

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

        /// <summary>Datele, plan dupa plan, in codarea si ordinea randurilor date: inversa lui FitsReader.ReadData.</summary>
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
                        // Peste capatul tipului cu mai mult de o rotunjire: valoarea n-ar mai fi cea a curbei.
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
            // Un FITS intreg fara BZERO (16 biti cu semn: doar 0..32767, jumatate de scara) nu
            // poate tine valorile ridicate de curba. Taiate pe tacute ar da un fisier gresit:
            // salvarea se refuza, iar fisierul temporar se sterge (Save).
            if (outside > 0) throw new Exception(CurveTexts.ValuesDoNotFit(outside, bitpix, bzero));
            var zero = new byte[1];
            for (; written % Block != 0; written++) o.Write(zero, 0, 1);
        }
    }
}
