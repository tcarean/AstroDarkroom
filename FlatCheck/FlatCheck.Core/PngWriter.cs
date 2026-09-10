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

// PngWriter.cs - encoder PNG propriu (fara dependinte) pentru harta reziduala.
//
// Harta e divergenta: albastru (sub) -> alb -> rosu (peste), cu albul FIXAT la 1.0.
// Fixarea la 1.0 e valida pentru toti apelantii de azi - reziduul de structura e
// img/model ~ 1, raportul A/B e normalizat la mediana 1, iar harta din --light e
// 1 + radial/a. Daca adaugi un apelant ale carui date NU sunt centrate pe 1, harta
// va minti.
//
// CAPETELE intervalului vin din afara, ca MapRange, si sunt de doua feluri:
//
//   - AutoRange - percentilele 1/99 plus 10% padding. Purtarea din totdeauna, si
//     singura pe care o folosesc consola si fisierul salvat de fereastra.
//   - FixedRange - simetric, +/- un plafon procentual in jurul lui 1.0.
//
// De ce a aparut a doua: scara automata intinde culoarea pe intervalul propriu al
// fiecarei imagini, deci rosu plin nu inseamna nimic anume - masurat pe doua mastere
// ale aceluiasi obiectiv, rosu plin era +3% pe unul si +10% pe altul, iar nimic de pe
// ecran nu spunea asta. Doua hartii pe scara automata se pot compara doar prin forma.
//
// Plafoanele oferite (FixedCapsPct) NU sunt numere noi: sunt granitele scarii de
// reziduu, Th.Resid*. Nu le duplica; daca scara se recalibreaza, se recalibreaza si ele.
//
// Dar plafonul si banda NU sunt acelasi lucru, si merita spus exact: plafonul e abaterea
// unui PIXEL fata de model, banda e o amplitudine varf-la-varf. O pata unilaterala de
// +10% atinge plafonul de 10% chiar la granita benzii BUN; un gradient de 10% (care
// merge de -5% la +5%) ajunge doar la jumatatea lui. Masurat: la un gradient de 10%
// harta are p1/p99 la -4.9/+4.8%. Deci pe chipul implicit de +/-10% saturatia inseamna
// "iese din BUN" pentru o pata si "a ajuns la REFA" pentru un gradient. Nu exista un
// plafon care sa fie exact pentru amandoua, asa ca se explica in legenda si in README,
// nu se schimba cifrele.
//
// Encoderul scrie semnatura + IHDR (8 biti, color type 2 = RGB) + un singur IDAT
// (fiecare rand precedat de filter byte 0, deflatat prin ZLibStream) + IEND, cu CRC-32
// calculat de mana.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace FlatCheck
{
    internal static class PngWriter
    {
        /// <summary>Albul hartii. Vezi nota de sus inainte sa-l schimbi.</summary>
        internal const double Mid = 1.0;

        internal static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>Capetele hartii, in unitatile rasterului (deci in jurul lui <see cref="Mid"/> = 1.0).</summary>
        internal readonly record struct MapRange(double Lo, double Hi);

        /// <summary>
        /// Plafoanele oferite ca scara fixa: granitele scarii de reziduu, in procente.
        /// Vezi nota de sus - nu sunt praguri noi, sunt Th.Resid* refolosite.
        /// </summary>
        internal static readonly double[] FixedCapsPct = { Th.ResidPerfect, Th.ResidUsable, Th.ResidCheck };

        /// <summary>
        /// Plafonul implicit al ferestrei: banda in care se asteapta sa traiasca un flat
        /// normal, deci doua sesiuni se compara dintr-o privire fara sa apesi nimic.
        /// </summary>
        internal const double DefaultCapPct = Th.ResidUsable;

        /// <summary>
        /// Scara automata: percentilele 1/99 plus 10% padding. Purtarea din totdeauna -
        /// consola si fisierul salvat folosesc numai asta.
        /// </summary>
        internal static MapRange AutoRange(Img resid)
        {
            // o singura sortare pentru ambele percentile
            var sorted = Stats.Sorted(resid.D);
            double lo = Stats.OfSorted(sorted, 1), hi = Stats.OfSorted(sorted, 99);
            double pad = Math.Max(hi - lo, 1e-4) * 0.1;
            return new MapRange(lo - pad, hi + pad);
        }

        /// <summary>Scara fixa, simetrica: +/- <paramref name="capPct"/> procente in jurul lui 1.0.</summary>
        internal static MapRange FixedRange(double capPct)
            => new MapRange(Mid - capPct / 100.0, Mid + capPct / 100.0);

        /// <summary>Scaleaza reziduul si-l converteste in RGB. Intoarce si capetele, ca sa fie verificabile.</summary>
        internal static (byte[] Rgb, double Lo, double Hi) MapResidual(Img resid)
            => MapResidual(resid, AutoRange(resid));

        /// <inheritdoc cref="MapResidual(Img)"/>
        internal static (byte[] Rgb, double Lo, double Hi) MapResidual(Img resid, MapRange range)
        {
            double lo = range.Lo, hi = range.Hi;

            int h = resid.H, w = resid.W;
            var rgb = new byte[h * w * 3];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var (r, g, b) = Colormap(resid.At(y, x), lo, Mid, hi);
                    int p = (y * w + x) * 3;
                    rgb[p] = r; rgb[p + 1] = g; rgb[p + 2] = b;
                }
            return (rgb, lo, hi);
        }

        /// <summary>
        /// Harta reziduala gata encodata PNG, in memorie, pe scara automata.
        ///
        /// Asta e forma pe care o vor amandoua aplicatiile: consola o scrie pe disc,
        /// fereastra o da direct unui BitmapImage. Un fisier temporar doar ca fereastra
        /// sa poata arata o imagine ar fi fost gunoi pe disc si o cale de esec in plus.
        /// </summary>
        internal static byte[] EncodeResidualMap(Img resid)
            => EncodeResidualMap(resid, AutoRange(resid));

        /// <summary>
        /// Aceeasi harta, pe capete date de apelant. Fereastra o refoloseste ca sa arate
        /// acelasi reziduu pe scara fixa, fara sa reia analiza.
        /// </summary>
        internal static byte[] EncodeResidualMap(Img resid, MapRange range)
        {
            var (rgb, _, _) = MapResidual(resid, range);
            return Encode(rgb, resid.W, resid.H);
        }

        /// <summary>Scrie harta reziduala pe disc. Mesajul pentru utilizator e treaba apelantului.</summary>
        internal static void WriteResidualMap(string path, Img resid)
            => File.WriteAllBytes(path, EncodeResidualMap(resid));

        internal static (byte r, byte g, byte b) Colormap(double v, double lo, double mid, double hi)
        {
            double t;
            byte r, g, b;
            if (v < mid)
            {
                t = (v - lo) / Math.Max(mid - lo, 1e-12); t = Math.Clamp(t, 0, 1);
                // albastru -> alb
                r = (byte)(255 * t); g = (byte)(255 * t); b = 255;
            }
            else
            {
                t = (v - mid) / Math.Max(hi - mid, 1e-12); t = Math.Clamp(t, 0, 1);
                // alb -> rosu
                r = 255; g = (byte)(255 * (1 - t)); b = (byte)(255 * (1 - t));
            }
            return (r, g, b);
        }

        internal static void Write(string path, byte[] rgb, int w, int h)
            => File.WriteAllBytes(path, Encode(rgb, w, h));

        /// <summary>
        /// Un raster gri, scris ca RGB cu cele trei componente egale. Encoderul ramane cu
        /// un singur tip de culoare; a adauga tipul 0 pentru vederile egalizate ar fi
        /// insemnat o a doua cale prin IHDR si prin decoderul din teste, pentru octeti pe
        /// care nimeni nu-i numara.
        /// </summary>
        internal static byte[] EncodeGrey(byte[] grey, int w, int h)
        {
            var rgb = new byte[grey.Length * 3];
            for (int i = 0; i < grey.Length; i++)
            {
                rgb[i * 3] = grey[i]; rgb[i * 3 + 1] = grey[i]; rgb[i * 3 + 2] = grey[i];
            }
            return Encode(rgb, w, h);
        }

        /// <summary>Encodeaza in memorie - punctul de intrare real, ca sa fie testabil fara disc.</summary>
        internal static byte[] Encode(byte[] rgb, int w, int h)
        {
            using var outp = new MemoryStream();
            outp.Write(Signature, 0, Signature.Length);

            var ihdr = new byte[13];
            WriteBE(ihdr, 0, w); WriteBE(ihdr, 4, h);
            ihdr[8] = 8;   // bit depth
            ihdr[9] = 2;   // color type RGB
            ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;   // deflate, filtrare adaptiva, neintretesut
            WriteChunk(outp, "IHDR", ihdr);

            // IDAT: fiecare rand precedat de filter byte 0 (None), apoi zlib
            using var raw = new MemoryStream();
            for (int y = 0; y < h; y++)
            {
                raw.WriteByte(0);
                raw.Write(rgb, y * w * 3, w * 3);
            }
            byte[] rawBytes = raw.ToArray();
            byte[] comp;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                    z.Write(rawBytes, 0, rawBytes.Length);
                comp = ms.ToArray();
            }
            WriteChunk(outp, "IDAT", comp);
            WriteChunk(outp, "IEND", Array.Empty<byte>());
            return outp.ToArray();
        }

        static void WriteBE(byte[] b, int o, int v)
        { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        static void WriteChunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len, 0, 4);
            var tb = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(tb, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = Crc32(tb, data);
            var cb = new byte[4]; WriteBE(cb, 0, (int)crc); s.Write(cb, 0, 4);
        }

        // Construita o singura data, la initializarea tipului, si publicata gata umpluta.
        // A fost un camp initializat lenes ("if null, umple"), ceea ce era o cursa reala,
        // nu teoretica: campul se atribuia INAINTE de umplere, deci un al doilea fir care
        // intra in acel moment vedea tabela ne-nula, o citea cu zerouri si scria un CRC
        // gresit - adica un PNG corupt. Reprodus: 8 fire, 2000 de runde, 5261 de CRC-uri
        // gresite. In fereastra e chiar posibil, fiindca tabul Ratio poate encoda in timp
        // ce Structura inca ruleaza. Un initializator static are garantia CLR-ului ca
        // nimeni nu vede tabela pana nu e gata.
        static readonly uint[] _crcTable = BuildCrcTable();

        static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        internal static uint Crc32(byte[] type, byte[] data)
        {
            uint crc = 0xffffffff;
            foreach (byte bb in type) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            foreach (byte bb in data) crc = _crcTable[(crc ^ bb) & 0xff] ^ (crc >> 8);
            return crc ^ 0xffffffff;
        }
    }
}
