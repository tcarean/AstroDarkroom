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

// PreviewReader.cs - decodarea previzualizarii JPEG, prin ImageSharp.
//
// SINGURUL fisier care numeste un tip din SixLabors.ImageSharp, exact cum RawReader.cs e
// singurul care numeste unul din Sdcb.LibRaw. Aceeasi izolare, din acelasi motiv - si
// aici e in plus punctul de schimb daca licenta pachetului se muta vreodata: alternativa
// masurata (StbImageSharp, Unlicense/MIT) a dat mediane identice pe aceleasi
// previzualizari, deci inlocuirea ar fi de un fisier. Vezi THIRD-PARTY-NOTICES.md.
//
// Patru lucruri pe care decodarea trebuie sa le nimereasca, verificate in sursa
// ImageSharp, nu in documentatie:
//   - Profilul ICC inglobat NU se aplica pixelilor. O previzualizare convertita nu mai e
//     ce a stat pe ecran, iar diferenta n-ar aparea in niciun test. Pe 3.1.x asta e
//     singurul comportament posibil - decodorul pastreaza profilul ca metadata si atat.
//     Daca pachetul se urca vreodata la 4.x, acolo exista DecoderOptions.ColorProfileHandling
//     cu un membru Convert, si trebuie setat EXPLICIT pe Preserve. (4.x aduce si un task
//     MSBuild care cere cheie de licenta pe Release; vezi FlatCheck.Core.csproj.)
//   - TargetSize scaleaza in timpul IDCT-ului, nu dupa. Se foloseste doar cand fisierul
//     ofera numai o previzualizare mare (vezi RawReader.TryCapturePreview); pe cea de
//     marime medie se decodeaza la dimensiunea nativa, ca niciun reesantionaj sa nu atinga
//     pixelii.
//   - Se decodeaza in Rgb24, ca un JPEG CMYK/YCCK sa ajunga tot RGB.
//   - Orice esec intoarce null. O previzualizare care nu se decodeaza nu e un motiv sa
//     pice rularea - inseamna doar ca comutatorul din fereastra ramane stins.
//
// Masurat pe D3300, cu ImageSharp 4.1.1, previzualizarea din DSC_0091.NEF:
//   1620x1080, nativ            16 ms,  5 MB alocati
//   6000x4000, nativ           460 ms, 69 MB
//   6000x4000, TargetSize 2000 485 ms,  9 MB
// Deci TargetSize taie MEMORIA, nu timpul - entropia se decodeaza oricum, doar IDCT-ul
// iese mai mic. Ce face decodarea ieftina e alegerea previzualizarii medii, in RawReader;
// TargetSize e plasa de siguranta pentru aparatele care n-au decat una mare.

using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace FlatCheck
{
    internal static class PreviewReader
    {
        // ATENTIE: niciun camp de tip ImageSharp aici, din acelasi motiv ca la RawReader -
        // campurile se rezolva la incarcarea tipului, corpurile de metoda abia la JIT.
        // Pinuit de PreviewReaderTests.HoldsNoImageSharpTypedFields.

        /// <summary>
        /// Latura lunga la care se aduce o previzualizare prea mare. Peste asta se plateste
        /// timp si memorie pentru pixeli pe care o histograma nu-i deosebeste.
        /// </summary>
        internal const int TargetLongSide = 2000;

        /// <summary>
        /// Previzualizarea ca pixeli. Un bitmap trece direct; un JPEG trece prin ImageSharp.
        /// Null la orice esec, inclusiv cand assembly-ul ImageSharp lipseste de langa
        /// executabil - atunci se degradeaza la "fara previzualizare", nu crapa tabul.
        /// </summary>
        internal static PreviewImage Decode(EmbeddedPreview p)
        {
            if (p == null || p.Data == null || p.Data.Length == 0) return null;

            if (!p.IsJpeg)
            {
                if (p.W <= 0 || p.H <= 0 || p.Data.Length != p.W * p.H * 3) return null;
                return new PreviewImage(p.H, p.W, p.Data, p.Source);
            }

            try
            {
                return DecodeJpeg(p);
            }
            catch (ImageFormatException) { return null; }
            catch (DllNotFoundException) { return null; }
            catch (FileNotFoundException) { return null; }
            catch (BadImageFormatException) { return null; }
            catch (TypeInitializationException) { return null; }
            catch (Exception) { return null; }
        }

        static PreviewImage DecodeJpeg(EmbeddedPreview p)
        {
            // Numai cand e chiar prea mare: pe o previzualizare de marime medie nu se
            // reesantioneaza nimic. Fereastra de 20% si medianele nu au nevoie de mai
            // mult de ~2000 px pe latura lunga.
            Size? target = null;
            int longSide = Math.Max(p.W, p.H);
            if (longSide > TargetLongSide * 3 / 2 && p.W > 0 && p.H > 0)
            {
                double k = TargetLongSide / (double)longSide;
                target = new Size(Math.Max(1, (int)(p.W * k)), Math.Max(1, (int)(p.H * k)));
            }

            // SkipMetadata: EXIF si ICC nu se citesc deloc - nu ne trebuie, si asa nici nu
            // exista tentatia (sau, pe 4.x, optiunea) de a aplica profilul pixelilor.
            var options = new DecoderOptions
            {
                SkipMetadata = true,
                TargetSize = target,
            };

            using Image<Rgb24> img = Image.Load<Rgb24>(options, p.Data);
            var rgb = new byte[img.Width * img.Height * 3];
            img.CopyPixelDataTo(rgb);
            return new PreviewImage(img.Height, img.Width, rgb, p.Source);
        }
    }
}
