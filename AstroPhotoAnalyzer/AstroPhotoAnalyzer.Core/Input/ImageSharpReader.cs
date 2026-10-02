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

// ImageSharpReader.cs - TIFF, PNG si JPEG, prin ImageSharp.
//
// SINGURUL fisier din nucleu care atinge ImageSharp la citire (JpegAnalyzer il atinge la
// encodare). Daca licenta pachetului se muta vreodata, cusatura e aici.
//
// DOUA TRECERI PRIN FISIER, deliberat. Prima, Identify, citeste doar headerul si spune
// ce e in fisier - biti pe esantion, canale, compresie. A doua decodeaza pixelii in
// tipul potrivit: L16 pe mono, Rgb48 pe color - si abia ea da si profilul ICC (vezi
// IccOf). Decodarea intr-un singur tip (Rgb48 pentru tot) ar fi triplat memoria pe o
// imagine mono, iar a deduce bitii din tipul in care AM CERUT decodarea ar fi fost o
// minciuna: ImageSharp da 16 biti oricui ii cere, si din 8 biti scalati cu 257.
//
// 3.1.x nu aplica niciodata profilul ICC la decodare - valorile sunt cele din fisier.
// Exact ce vrem: masuram ce a scris Siril, nu o conversie de culoare.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace AstroPhotoAnalyzer
{
    internal sealed class ImageSharpReader : IFrameReader
    {
        static readonly string[] Exts = { ".tif", ".tiff", ".png", ".jpg", ".jpeg" };

        public IReadOnlyList<string> Extensions => Exts;

        public bool Handles(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(Exts, ext) >= 0;
        }

        public Raster Load(string path)
        {
            ImageInfo info;
            try { info = Image.Identify(path); }
            catch (UnknownImageFormatException e) { throw new Exception("fisierul nu e o imagine cunoscuta: " + e.Message); }
            catch (InvalidImageContentException e) { throw new Exception("imagine stricata: " + e.Message); }

            var facts = Facts(info);
            var raster = facts.Color ? LoadColor(path, out byte[] icc) : LoadMono(path, out icc);

            raster.Source = new SourceInfo
            {
                Path = path,
                Format = facts.Format,
                BitDepth = facts.Bits,
                IsFloat = false,
                PlanesInFile = facts.Color ? 3 : 1,
                Compression = facts.Compression,
                Icc = icc,
                IccDescription = IccProfileInfo.Describe(icc),
                FileBytes = new FileInfo(path).Length,
                Note = facts.Note,
            };
            return raster;
        }

        sealed class FileFacts
        {
            internal string Format;
            internal int Bits;
            internal bool Color;
            internal string Compression;
            internal string Note;
        }

        /// <summary>
        /// Ce e in fisier, din metadatele fiecarui format - nu din tipul de pixel in care
        /// s-ar decoda. PNG si TIFF spun explicit bitii si canalele; JPEG e mereu 8 biti.
        /// </summary>
        static FileFacts Facts(ImageInfo info)
        {
            string name = info.Metadata.DecodedImageFormat?.Name ?? "";
            var f = new FileFacts { Format = name.ToUpperInvariant(), Bits = 8, Color = true };

            if (f.Format == "PNG")
            {
                PngMetadata png = info.Metadata.GetPngMetadata();
                f.Bits = png.BitDepth switch
                {
                    PngBitDepth.Bit16 => 16,
                    PngBitDepth.Bit8 => 8,
                    PngBitDepth.Bit4 => 4,
                    PngBitDepth.Bit2 => 2,
                    PngBitDepth.Bit1 => 1,
                    _ => 8,
                };
                f.Color = png.ColorType != PngColorType.Grayscale && png.ColorType != PngColorType.GrayscaleWithAlpha;
                f.Compression = "Deflate";
                if (png.ColorType == PngColorType.Palette) f.Note = "PNG cu paleta: culorile sunt indexate, nu masurate direct";
            }
            else if (f.Format == "TIFF")
            {
                TiffFrameMetadata tf = info.FrameMetadataCollection.Count > 0
                    ? info.FrameMetadataCollection[0].GetTiffMetadata()
                    : null;
                if (tf != null)
                {
                    if (tf.BitsPerSample.HasValue) f.Bits = tf.BitsPerSample.Value.Channel0;
                    f.Color = tf.PhotometricInterpretation != TiffPhotometricInterpretation.BlackIsZero
                           && tf.PhotometricInterpretation != TiffPhotometricInterpretation.WhiteIsZero;
                    f.Compression = tf.Compression switch
                    {
                        TiffCompression.None => "necomprimat",
                        TiffCompression.Lzw => "LZW",
                        TiffCompression.Deflate => "Deflate",
                        TiffCompression.OldDeflate => "Deflate",
                        TiffCompression.PackBits => "PackBits",
                        TiffCompression.Jpeg => "JPEG",
                        null => "necomprimat",
                        _ => tf.Compression.ToString(),
                    };
                    if (tf.PhotometricInterpretation == TiffPhotometricInterpretation.PaletteColor)
                        f.Note = "TIFF cu paleta: culorile sunt indexate, nu masurate direct";
                    if (info.FrameMetadataCollection.Count > 1)
                        f.Note = $"TIFF cu {info.FrameMetadataCollection.Count} pagini; s-a citit prima";
                }
                if (f.Bits != 8 && f.Bits != 16)
                    f.Note = $"TIFF cu {f.Bits} biti pe esantion, citit prin conversia ImageSharp";
            }
            else if (f.Format == "JPEG")
            {
                f.Bits = 8;
                f.Color = info.PixelType.BitsPerPixel > 8;
                f.Compression = "JPEG (cu pierderi)";
            }
            else
            {
                f.Note = "format neasteptat dupa extensie: " + name;
            }
            return f;
        }

        /// <summary>
        /// Profilul ICC, din imaginea DECODATA - nu din Identify. Masurat pe 3.1.11:
        /// Identify se opreste la header si nu vede chunk-ul iCCP al unui PNG, iar la
        /// TIFF decoderul lasa profilul pe primul cadru, langa tagurile IFD-ului, nu pe
        /// imagine. Se cauta in ambele locuri, ca un PNG de la Siril sau un TIFF cu
        /// profil sa nu apara "fara profil".
        /// </summary>
        static byte[] IccOf(Image img)
            => img.Metadata.IccProfile?.ToByteArray() ?? img.Frames[0].Metadata.IccProfile?.ToByteArray();

        static Raster LoadColor(string path, out byte[] icc)
        {
            using Image<Rgb48> img = Image.Load<Rgb48>(path);
            icc = IccOf(img);
            var r = new Raster(img.Height, img.Width, 3);
            const float k = 1f / 65535f;
            img.ProcessPixelRows(acc =>
            {
                for (int y = 0; y < acc.Height; y++)
                {
                    Span<Rgb48> row = acc.GetRowSpan(y);
                    int o = y * r.W;
                    for (int x = 0; x < row.Length; x++)
                    {
                        r.Planes[0][o + x] = row[x].R * k;
                        r.Planes[1][o + x] = row[x].G * k;
                        r.Planes[2][o + x] = row[x].B * k;
                    }
                }
            });
            return r;
        }

        static Raster LoadMono(string path, out byte[] icc)
        {
            using Image<L16> img = Image.Load<L16>(path);
            icc = IccOf(img);
            var r = new Raster(img.Height, img.Width, 1);
            const float k = 1f / 65535f;
            img.ProcessPixelRows(acc =>
            {
                for (int y = 0; y < acc.Height; y++)
                {
                    Span<L16> row = acc.GetRowSpan(y);
                    int o = y * r.W;
                    for (int x = 0; x < row.Length; x++)
                        r.Planes[0][o + x] = row[x].PackedValue * k;
                }
            });
            return r;
        }
    }
}
