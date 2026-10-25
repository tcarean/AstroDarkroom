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

// ImageSharpReader.cs - TIFF, PNG and JPEG, through ImageSharp.
//
// THE ONLY file of the core that reads image files through ImageSharp (JpegAnalyzer encodes and
// decodes JPEG in memory). If the package's licence ever moves, the seam is here.
//
// TWO PASSES THROUGH THE FILE, on purpose. The first, Identify, reads the metadata without
// decoding the pixels and says what is in the file - bits per sample, channels, compression
// (on a PNG it still checks the chunks' CRCs, so a damaged IDAT fails here). The second decodes
// the pixels into the fitting type: L16 on mono, Rgb48 on colour - and only it gives the ICC
// profile too (see IccOf). Decoding into a single type (Rgb48 for everything) would have tripled
// the memory of a mono image, and inferring the bits from the type I ASKED the decode for would
// have been a lie: ImageSharp gives 16 bits to whoever asks for them, even out of 8 bits scaled
// by 257.
//
// 3.1.x never applies the ICC profile when decoding - the values are those in the file. Exactly
// what we want: we measure what Siril wrote, not a colour conversion.

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
    /// <summary>The reader of TIFF, PNG and JPEG files, through ImageSharp 3.1.11.</summary>
    internal sealed class ImageSharpReader : IFrameReader
    {
        static readonly string[] Exts = { ".tif", ".tiff", ".png", ".jpg", ".jpeg" };

        public IReadOnlyList<string> Extensions => Exts;

        /// <inheritdoc/>
        public bool Handles(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(Exts, ext) >= 0;
        }

        /// <summary>
        /// Reads the file in two passes: what the file holds from its metadata (Identify), then
        /// the pixels as L16 (mono) or Rgb48 (colour), each sample / 65535, with the ICC profile.
        /// Throws, with a message for the user, when Identify does not know the format or finds
        /// the content damaged. Other failures pass with ImageSharp's own exception and message
        /// (damage only the decode meets, a TIFF variant ImageSharp does not decode, a TIFF cut
        /// short that Identify trips on), and some damage goes unnoticed: the decoder may read
        /// a TIFF's missing strip rows as black.
        /// </summary>
        /// <param name="path">The TIFF, PNG or JPEG file.</param>
        /// <returns>The raster, with its <see cref="SourceInfo"/>.</returns>
        public Raster Load(string path)
        {
            ImageInfo info;
            try { info = Image.Identify(path); }
            catch (UnknownImageFormatException e) { throw new Exception(Tr.Pick("the file is not an image format the tool knows: ", "fisierul nu e o imagine cunoscuta: ") + e.Message); }
            catch (InvalidImageContentException e) { throw new Exception(Tr.Pick("damaged image: ", "imagine stricata: ") + e.Message); }

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

        /// <summary>
        /// What a file's metadata say about it, as <see cref="Facts"/> reads them.
        /// </summary>
        sealed class FileFacts
        {
            internal string Format;
            internal int Bits;
            internal bool Color;
            internal string Compression;
            internal string Note;
        }

        /// <summary>
        /// What is in the file, from each format's metadata - not from the pixel type it would be
        /// decoded into. PNG and TIFF state the bits and the channels explicitly; JPEG is always
        /// 8 bits.
        /// </summary>
        /// <param name="info">What Identify read from the file.</param>
        /// <returns>
        /// The format's name in capitals, the bits per sample, colour or not, the compression as
        /// the report names it, and a note for the report (or null).
        /// </returns>
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
                if (png.ColorType == PngColorType.Palette) f.Note = Tr.Pick("a palette PNG: the colours are indexed, not measured directly", "PNG cu paleta: culorile sunt indexate, nu masurate direct");
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
                        TiffCompression.None => Tr.Pick("uncompressed", "necomprimat"),
                        TiffCompression.Lzw => "LZW",
                        TiffCompression.Deflate => "Deflate",
                        TiffCompression.OldDeflate => "Deflate",
                        TiffCompression.PackBits => "PackBits",
                        TiffCompression.Jpeg => "JPEG",
                        null => Tr.Pick("uncompressed", "necomprimat"),
                        _ => tf.Compression.ToString(),
                    };
                    if (tf.PhotometricInterpretation == TiffPhotometricInterpretation.PaletteColor)
                        f.Note = Tr.Pick("a palette TIFF: the colours are indexed, not measured directly", "TIFF cu paleta: culorile sunt indexate, nu masurate direct");
                    if (info.FrameMetadataCollection.Count > 1)
                        f.Note = Tr.Pick($"a TIFF with {info.FrameMetadataCollection.Count} pages; the first was read", $"TIFF cu {info.FrameMetadataCollection.Count} pagini; s-a citit prima");
                }
                if (f.Bits != 8 && f.Bits != 16)
                    f.Note = Tr.Pick($"a TIFF with {f.Bits} bits per sample, read through ImageSharp's conversion", $"TIFF cu {f.Bits} biti pe esantion, citit prin conversia ImageSharp");
            }
            else if (f.Format == "JPEG")
            {
                f.Bits = 8;
                f.Color = info.PixelType.BitsPerPixel > 8;
                f.Compression = Tr.Pick("JPEG (lossy)", "JPEG (cu pierderi)");
            }
            else
            {
                f.Note = Tr.Pick("a format the extension did not suggest: ", "format neasteptat dupa extensie: ") + name;
            }
            return f;
        }

        /// <summary>
        /// The ICC profile, from the DECODED image - not from Identify. Measured on 3.1.11:
        /// Identify does not return a PNG's iCCP chunk, and on TIFF the
        /// decoder leaves the profile on the first frame, beside the IFD's tags, not on the
        /// image. Both places are searched, so that a PNG from Siril or a TIFF with a profile does
        /// not show as "fara profil".
        /// </summary>
        /// <param name="img">The decoded image.</param>
        /// <returns>The profile's bytes, or null when the file carries none.</returns>
        static byte[] IccOf(Image img)
            => img.Metadata.IccProfile?.ToByteArray() ?? img.Frames[0].Metadata.IccProfile?.ToByteArray();

        /// <summary>
        /// Decodes the file as Rgb48 into a raster of 3 planes, each sample / 65535.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="icc">The embedded ICC profile, or null.</param>
        /// <returns>The raster, without its <see cref="Raster.Source"/>.</returns>
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

        /// <summary>
        /// Decodes the file as L16 into a raster of 1 plane, each sample / 65535.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="icc">The embedded ICC profile, or null.</param>
        /// <returns>The raster, without its <see cref="Raster.Source"/>.</returns>
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
