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

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// Scrie un Raster pe disc in formatele pe care le citeste unealta, cu encoderul
/// ImageSharp. Testele cititorului trec deci prin acelasi pachet la scriere si la
/// citire - ceea ce verifica ca NOI il folosim corect, nu ca el e corect; pentru asta
/// exista testele lui.
/// </summary>
internal static class ImageFiles
{
    static ushort U16(float v) => (ushort)RasterOps.Level16(v);
    static byte U8(float v) => (byte)RasterOps.Level8(v);

    static Image<Rgb48> ToRgb48(Raster r)
    {
        var img = new Image<Rgb48>(r.W, r.H);
        for (int y = 0; y < r.H; y++)
            for (int x = 0; x < r.W; x++)
                img[x, y] = new Rgb48(U16(r.At(0, y, x)), U16(r.At(r.IsColor ? 1 : 0, y, x)), U16(r.At(r.IsColor ? 2 : 0, y, x)));
        return img;
    }

    static Image<L16> ToL16(Raster r)
    {
        var img = new Image<L16>(r.W, r.H);
        for (int y = 0; y < r.H; y++)
            for (int x = 0; x < r.W; x++)
                img[x, y] = new L16(U16(r.At(0, y, x)));
        return img;
    }

    static Image<L8> ToL8(Raster r)
    {
        var img = new Image<L8>(r.W, r.H);
        for (int y = 0; y < r.H; y++)
            for (int x = 0; x < r.W; x++)
                img[x, y] = new L8(U8(r.At(0, y, x)));
        return img;
    }

    /// <summary>
    /// TIFF RGB pe 8 biti. Encoderul TIFF din ImageSharp 3.1 scrie 8 biti pe esantion
    /// si cand i se cere Bit48 (masurat), deci 16 biti RGB nu se pot produce prin el -
    /// pentru asta exista TiffTestData, care scrie fisierul de mana.
    /// </summary>
    internal static string SaveTiff8Rgb(Raster r, string path, TiffCompression compression = TiffCompression.Lzw)
    {
        using var img = ToRgb48(r);
        img.SaveAsTiff(path, new TiffEncoder { Compression = compression });
        return path;
    }

    internal static string SaveTiff16Gray(Raster r, string path)
    {
        using var img = ToL16(r);
        img.SaveAsTiff(path, new TiffEncoder { Compression = TiffCompression.Deflate, BitsPerPixel = TiffBitsPerPixel.Bit16 });
        return path;
    }

    internal static string SavePng16Rgb(Raster r, string path)
    {
        using var img = ToRgb48(r);
        img.SaveAsPng(path, new PngEncoder { BitDepth = PngBitDepth.Bit16, ColorType = PngColorType.Rgb });
        return path;
    }

    internal static string SavePng8Gray(Raster r, string path)
    {
        using var img = ToL8(r);
        img.SaveAsPng(path, new PngEncoder { BitDepth = PngBitDepth.Bit8, ColorType = PngColorType.Grayscale });
        return path;
    }

    internal static string SaveJpeg(Raster r, string path, int quality = 95)
    {
        using var img = ToRgb48(r);
        img.SaveAsJpeg(path, new JpegEncoder { Quality = quality });
        return path;
    }
}
