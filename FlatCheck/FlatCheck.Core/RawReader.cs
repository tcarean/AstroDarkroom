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

// RawReader.cs - citirea fisierelor raw de aparat (.NEF si rudele lui), prin libraw.
//
// Singurul loc din unealta care atinge o librarie externa. FITS-ul si PNG-ul raman
// scrise de mana; aici nu se putea, fiindca un NEF nu e doar un container: pixelii sunt
// comprimati Huffman cu un predictor si trecuti printr-o curba de linearizare din
// MakerNote. Nu exista niciun decodor .NET pur managed pentru asta.
//
// Ce se cere de la libraw e strict brutul, NU o imagine developata:
//   - raw_image, decupat la zona vizibila  -> mozaicul CFA intact, exact ca
//     raw_image_visible din rawpy (asta foloseste flatcheck.py)
//   - color.maximum                        -> white
//   - color.black + color.cblack[0..3]     -> black, pe canal
//   - iparams.filters + iparams.cdesc      -> ce culoare are fiecare pozitie Bayer
// Nicio interpolare, niciun balans de alb, nicio gama: --expo masoara nivelul ADU
// original, iar orice developare l-ar distruge.
//
// DLL-urile native vin din pachetele Sdcb.LibRaw.runtime.* si stau LANGA executabil.
// Daca lipsesc (cineva a copiat doar flatcheck.exe), modurile pe FITS merg mai departe
// neschimbate - tipul asta se incarca abia cand se cere un fisier raw - iar aici se
// iese cu un mesaj care spune ce lipseste, nu cu un DllNotFoundException.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;

namespace FlatCheck
{
    internal sealed class RawReader : IFrameReader
    {
        // ATENTIE: niciun camp al clasei asteia nu are voie sa fie un tip din Sdcb.LibRaw.
        // Campurile se rezolva la incarcarea tipului, iar tipul se incarca la fiecare
        // rulare, si pe FITS - corpurile de metoda, in schimb, abia la JIT. Un camp de tip
        // valoare din libraw ar face ca o rulare pe FITS sa crape cand DLL-urile lipsesc.
        // Masurat, nu presupus; pinuit de RawReaderTests.
        static readonly IReadOnlyDictionary<string, string> NoHeader = new Dictionary<string, string>();

        // Aceleasi extensii ca in flatcheck.py. Doar NEF-ul e testat pe date reale;
        // restul merg pe cat de bine le stie libraw, care e si ce face rawpy.
        static readonly string[] Extensions =
        { ".nef", ".cr2", ".cr3", ".arw", ".dng", ".raf" };

        /// <summary>
        /// Extensiile recunoscute ca raw de aparat, expuse fiindca nu numai cititorul are
        /// nevoie de ele: aplicatia WPF filtreaza dupa ele lista din flats/, iar o a doua
        /// copie a listei ar insemna ca urmatorul aparat adaugat aici nu apare acolo.
        /// </summary>
        internal static IReadOnlyList<string> RawExtensions => Extensions;

        /// <summary>Poarta de dispecerat: se decide DOAR pe extensie, ca in flatcheck.py.</summary>
        internal static bool IsRawPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(Extensions, ext) >= 0;
        }

        public bool Handles(string path) => IsRawPath(path);

        public LoadedFrame Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"nu gasesc fisierul {path}", path);

            try
            {
                return Decode(path);
            }
            catch (DllNotFoundException e) { throw Missing(e); }
            catch (FileNotFoundException e) when (e.FileName != path) { throw Missing(e); }
            catch (BadImageFormatException e) { throw Missing(e); }
            catch (TypeInitializationException e) { throw Missing(e); }
        }

        static Exception Missing(Exception inner)
            => new InvalidOperationException(
                "nu pot incarca libraw, deci nu pot citi fisiere raw de aparat. DLL-urile "
              + "native (raw_r.dll si vecinii) trebuie sa stea langa flatcheck.exe. Modurile "
              + "pe FITS merg si fara ele; pentru raw, publica cu IncludeNativeLibrariesForSelfExtract "
              + "sau copiaza tot folderul, nu doar executabilul. (" + inner.Message + ")", inner);

        static LoadedFrame Decode(string path)
        {
            using RawContext ctx = RawContext.OpenFile(path);
            ctx.Unpack();

            LibRawData d = Marshal.PtrToStructure<LibRawData>(ctx.UnsafeGetHandle());
            LibRawImageSizes s = d.RawData.Sizes;
            IntPtr src = d.RawData.RawImage;

            // Foveon si unele formate nu produc un mozaic pe 16 biti, ci direct planuri
            // de culoare. --expo n-are ce face cu ele, deci se refuza pe fata.
            if (src == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"{path}: libraw n-a produs un mozaic Bayer pe 16 biti pentru fisierul asta "
                  + "(senzor non-Bayer sau format neacoperit).");

            int h = s.Height, w = s.Width;
            if (h <= 0 || w <= 0)
                throw new InvalidOperationException($"{path}: dimensiuni raw invalide ({w}x{h}).");

            // Ordinea culorilor, evaluata la originea zonei VIZIBILE - aceleasi margini pe
            // care le sare copierea de mai jos. Numai locale, niciun camp: un camp de tip
            // libraw ar forta incarcarea assembly-ului si pe rularile pe FITS (vezi nota
            // de sus si RawReaderTests.HoldsNoLibrawTypedFields).
            LibRawImageParams ip = d.RawData.IParams;
            CfaPattern cfa = CfaPattern.FromLibRaw(ip.Filters, ip.Cdesc, s.TopMargin, s.LeftMargin);

            // Zona VIZIBILA, ca raw_image_visible din rawpy: se sare peste marginile
            // mascate (pixelii de calibrare de sub capacul senzorului), altfel medianele
            // ar fi trase in jos de o banda neagra pe care aparatul n-o considera imagine.
            int stride = s.RawPitch > 0 ? (int)(s.RawPitch / 2) : s.RawWidth;
            var img = new Img(h, w);
            var row = new short[w];
            for (int y = 0; y < h; y++)
            {
                long offset = ((long)(y + s.TopMargin) * stride + s.LeftMargin) * sizeof(ushort);
                Marshal.Copy(src + (nint)offset, row, 0, w);
                int o = y * w;
                // libraw da ushort, Marshal.Copy stie doar short - deci se citeste ca
                // short si se reinterpreteaza. Fara cast, un pixel peste 32767 ar iesi negativ.
                for (int x = 0; x < w; x++) img.D[o + x] = (ushort)row[x];
            }

            LibRawColorData c = d.RawData.Color;
            double white = c.Maximum;

            // Nivelul de negru poate fi dat global (color.black) sau pe canal
            // (color.cblack), si libraw le imparte cum ii vine: pe NEF-urile Nikon muta
            // partea comuna in black si lasa cblack pe zero. Suma e ce citeste si rawpy,
            // iar flatcheck.py ia mediana celor patru canale - deci la fel si aici.
            var blacks = new List<double>(4);
            for (int i = 0; i < 4; i++)
                blacks.Add(c.Black + (c.CBlack != null && i < c.CBlack.Length ? c.CBlack[i] : 0));
            double black = Stats.Percentile(blacks, 50);

            // Daca scala citita e degenerata, mai bine o eroare decat o scala inversata:
            // rng negativ ar da procente absurde, iar garda de sanitate le-ar imbraca in
            // "fisierul nu pare un flat brut", care ar trimite omul pe pista gresita.
            if (!(white > black))
                throw new InvalidOperationException(
                    $"{path}: libraw da o scala imposibila (white {white:F0} <= black {black:F0}). "
                  + "Foloseste --sat si --black daca stii valorile corecte.");

            // Previzualizarea aparatului, aleasa din METADATE (lista de miniaturi e deja
            // marsalizata in d) si despachetata numai ea. Se face aici, cat fisierul e
            // deschis, ca sa nu se mai deschida o data pentru cativa MB de octeti.
            EmbeddedPreview preview = TryCapturePreview(ctx, d.ThumbnailList);

            return new LoadedFrame
            {
                Image = img,
                // Header gol, nu absent: garda de aplicabilitate a lui --light il citeste
                // ca "necunoscut" si ruleaza cu o nota, in loc sa refuze.
                Header = NoHeader,
                Scale = new ExposureScale(white, black, "libraw (exact)"),
                Cfa = cfa,
                Preview = preview,
            };
        }

        /// <summary>
        /// Latura lunga peste care o previzualizare nu mai e "de marime medie". Pe D3300
        /// alege intrarea de 1620x1080 in locul celei de 6000x4000: aceleasi mediane la
        /// 0.4 puncte, la a douasprezecea parte din timp si a treisprezecea din memorie.
        /// </summary>
        internal const int PreferredMaxLongSide = 3000;

        /// <summary>
        /// Cea mai potrivita previzualizare din fisier, sau null. Regula, in ordine:
        /// cel mai mare JPEG cu latura lunga sub <see cref="PreferredMaxLongSide"/>; altfel
        /// cel mai mare JPEG de orice marime (PreviewReader il va scala la decodare);
        /// altfel un bitmap deja decodat de libraw, oricat de mic.
        ///
        /// Alegerea se face din lista de metadate - ThumbCount intrari, fiecare cu format
        /// si dimensiuni - deci fara niciun cost pe ce nu se alege. Indecsii NU sunt
        /// stabili intre aparate, de aia se enumereaza si nu se presupun.
        ///
        /// Nu arunca niciodata: Load a mers luni de zile fara previzualizare, si o
        /// miniatura ciudata pe vreun aparat nu are voie sa-l strice acum. Lipsa ei lasa
        /// doar comutatorul din fereastra stins.
        /// </summary>
        static EmbeddedPreview TryCapturePreview(RawContext ctx, LibRawThumbnailList list)
        {
            try
            {
                int count = Math.Min(list.ThumbCount, list.ThumbList?.Length ?? 0);
                int bestMid = -1, bestAny = -1, bestOther = -1;
                long areaMid = 0, areaAny = 0, areaOther = 0;

                for (int i = 0; i < count; i++)
                {
                    LibRawThumbnailItem t = list.ThumbList[i];
                    long area = (long)t.Width * t.Height;
                    if (t.Width == 0 || t.Height == 0) continue;

                    if (t.Format == InternalThumbnailFormat.Jpeg)
                    {
                        if (Math.Max(t.Width, t.Height) <= PreferredMaxLongSide && area > areaMid) { bestMid = i; areaMid = area; }
                        if (area > areaAny) { bestAny = i; areaAny = area; }
                    }
                    else if (area > areaOther) { bestOther = i; areaOther = area; }
                }

                // Regulile 1 si 2: un JPEG, de preferat mediu.
                int jpegIdx = bestMid >= 0 ? bestMid : bestAny;
                if (jpegIdx >= 0)
                {
                    EmbeddedPreview j = Export(ctx, jpegIdx, list.ThumbList[jpegIdx].Flip);
                    if (j != null) return j;
                }

                // Regula 3: ce a mai ramas, daca libraw il da ca bitmap decodat.
                return bestOther >= 0 ? Export(ctx, bestOther, list.ThumbList[bestOther].Flip) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Despacheteaza o singura intrare si ii copiaza octetii. ProcessedImage
        /// impacheteaza memorie nativa si e IDisposable: copia se face in interiorul
        /// using-ului si niciun span nu iese de acolo.
        /// </summary>
        static EmbeddedPreview Export(RawContext ctx, int index, int flip)
        {
            ctx.UnpackThumbnail(index);
            using ProcessedImage t = ctx.ExportThumbnail(index);
            if (t.Width <= 0 || t.Height <= 0 || t.DataSize <= 0) return null;

            if (t.ImageType == ProcessedImageType.Jpeg)
            {
                // Pe intrarile JPEG libraw raporteaza Bits = 0 (masurat), deci aici nu se
                // verifica adancimea - octetii sunt fluxul comprimat, nu pixeli.
                byte[] data = t.AsSpan<byte>().ToArray();
                return new EmbeddedPreview(true, data, t.Width, t.Height, flip,
                    $"previzualizare JPEG {t.Width}x{t.Height} din fisier");
            }

            if (t.ImageType == ProcessedImageType.Bitmap && t.Bits == 8 && t.Channels == 3
                && t.DataSize == t.Width * t.Height * 3)
            {
                byte[] data = t.AsSpan<byte>().ToArray();
                return new EmbeddedPreview(false, data, t.Width, t.Height, flip,
                    $"miniatura RGB {t.Width}x{t.Height} din fisier");
            }

            // Altceva (alt bit depth, alt numar de canale) nu se reinterpreteaza.
            return null;
        }
    }
}
