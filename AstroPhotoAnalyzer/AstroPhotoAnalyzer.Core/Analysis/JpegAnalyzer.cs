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

// JpegAnalyzer.cs - masuratoarea C3: ce pierde imaginea la fiecare calitate JPEG, si
// cat ar avea ca TIFF pe 8 biti.
//
// Nu se scrie nimic pe disc: fiecare calitate se encodeaza in memorie, se decodeaza
// inapoi si se compara cu imaginea de 16 biti (in scara 0..255, NErotunjita - deci
// eroarea include si rotunjirea la 8 biti, ca in scriptul de acceptanta). Pe langa
// media si maximul erorii, se numara pixelii cu eroare peste 2 niveluri si se retine
// nivelul lor mediu: pe imaginea de referinta erau 3 % si stateau in cerul intunecat,
// unde JPEG-ul lucreaza cel mai prost.
//
// Al doilea fisier din nucleu care atinge ImageSharp (primul e cititorul). 4:4:4,
// fara subesantionare de crominanta: stelele colorate pe un pixel n-ar supravietui
// lui 4:2:0.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace AstroPhotoAnalyzer
{
    internal sealed record JpegRow
    {
        internal int Quality { get; init; }
        internal long Bytes { get; init; }
        internal double Megabytes => Bytes / 1048576.0;
        internal double MeanAbsError { get; init; }
        internal double MaxError { get; init; }
        internal double P99 { get; init; }
        internal double P999 { get; init; }

        /// <summary>Procentul esantioanelor cu eroare peste Thresholds.JpegErrorCountLevels.</summary>
        internal double PctOverThreshold { get; init; }

        /// <summary>Nivelul mediu (0..255) al acelor esantioane - unde loveste compresia.</summary>
        internal double MeanLevelOverThreshold { get; init; }

        internal bool Acceptable => MaxError <= Thresholds.JpegMaxErrorReject;
    }

    internal sealed record JpegResult
    {
        internal IReadOnlyList<JpegRow> Rows { get; init; }
        internal long Tiff8LzwBytes { get; init; }
        internal double Tiff8LzwMegabytes => Tiff8LzwBytes / 1048576.0;
    }

    internal static class JpegAnalyzer
    {
        internal static JpegResult Analyze(Raster r) => Analyze(r, Thresholds.JpegQualities);

        internal static JpegResult Analyze(Raster r, int[] qualities)
        {
            var rows = new List<JpegRow>();
            long tiffBytes;
            if (r.IsColor)
            {
                using Image<Rgb24> img = ToRgb24(r);
                foreach (int q in qualities) rows.Add(MeasureColor(r, img, q));
                tiffBytes = TiffBytes(img);
            }
            else
            {
                using Image<L8> img = ToL8(r);
                foreach (int q in qualities) rows.Add(MeasureMono(r, img, q));
                tiffBytes = TiffBytes(img);
            }
            return new JpegResult { Rows = rows, Tiff8LzwBytes = tiffBytes };
        }

        static Image<Rgb24> ToRgb24(Raster r)
        {
            var img = new Image<Rgb24>(r.W, r.H);
            img.ProcessPixelRows(acc =>
            {
                for (int y = 0; y < acc.Height; y++)
                {
                    Span<Rgb24> row = acc.GetRowSpan(y);
                    int o = y * r.W;
                    for (int x = 0; x < row.Length; x++)
                        row[x] = new Rgb24((byte)RasterOps.Level8(r.Planes[0][o + x]), (byte)RasterOps.Level8(r.Planes[1][o + x]), (byte)RasterOps.Level8(r.Planes[2][o + x]));
                }
            });
            return img;
        }

        static Image<L8> ToL8(Raster r)
        {
            var img = new Image<L8>(r.W, r.H);
            img.ProcessPixelRows(acc =>
            {
                for (int y = 0; y < acc.Height; y++)
                {
                    Span<L8> row = acc.GetRowSpan(y);
                    int o = y * r.W;
                    for (int x = 0; x < row.Length; x++) row[x] = new L8((byte)RasterOps.Level8(r.Planes[0][o + x]));
                }
            });
            return img;
        }

        static long TiffBytes<T>(Image<T> img) where T : unmanaged, IPixel<T>
        {
            using var ms = new MemoryStream();
            img.SaveAsTiff(ms, new TiffEncoder { Compression = TiffCompression.Lzw });
            return ms.Length;
        }

        static JpegRow MeasureColor(Raster r, Image<Rgb24> img, int quality)
        {
            using var ms = new MemoryStream();
            img.SaveAsJpeg(ms, new JpegEncoder { Quality = quality, ColorType = JpegEncodingColor.YCbCrRatio444 });
            ms.Position = 0;
            using Image<Rgb24> back = Image.Load<Rgb24>(ms);
            var acc = new ErrorAccumulator();
            back.ProcessPixelRows(a =>
            {
                for (int y = 0; y < a.Height; y++)
                {
                    Span<Rgb24> row = a.GetRowSpan(y);
                    int o = y * r.W;
                    for (int x = 0; x < row.Length; x++)
                    {
                        acc.Add(row[x].R, r.Planes[0][o + x]);
                        acc.Add(row[x].G, r.Planes[1][o + x]);
                        acc.Add(row[x].B, r.Planes[2][o + x]);
                    }
                }
            });
            return acc.Row(quality, ms.Length);
        }

        static JpegRow MeasureMono(Raster r, Image<L8> img, int quality)
        {
            using var ms = new MemoryStream();
            img.SaveAsJpeg(ms, new JpegEncoder { Quality = quality, ColorType = JpegEncodingColor.Luminance });
            ms.Position = 0;
            using Image<L8> back = Image.Load<L8>(ms);
            var acc = new ErrorAccumulator();
            back.ProcessPixelRows(a =>
            {
                for (int y = 0; y < a.Height; y++)
                {
                    Span<L8> row = a.GetRowSpan(y);
                    int o = y * r.W;
                    for (int x = 0; x < row.Length; x++) acc.Add(row[x].PackedValue, r.Planes[0][o + x]);
                }
            });
            return acc.Row(quality, ms.Length);
        }

        /// <summary>
        /// Eroarea pe esantion, acumulata intr-o histograma cu pas de 0.01 nivel: pe 69 de
        /// milioane de esantioane o sortare pentru percentile ar costa secunde si sute de MB.
        /// </summary>
        sealed class ErrorAccumulator
        {
            const int Scale = 100, Bins = 256 * Scale + 1;
            readonly long[] _hist = new long[Bins];
            long _n, _over;
            double _sum, _max, _levelOver;

            internal void Add(byte decoded, float reference)
            {
                double refLevel = Math.Clamp(reference, 0f, 1f) * 255;
                double e = Math.Abs(decoded - refLevel);
                _sum += e; _n++;
                if (e > _max) _max = e;
                _hist[(int)Math.Min(Bins - 1, e * Scale)]++;
                if (e > Thresholds.JpegErrorCountLevels) { _over++; _levelOver += refLevel; }
            }

            internal JpegRow Row(int quality, long bytes) => new JpegRow
            {
                Quality = quality,
                Bytes = bytes,
                MeanAbsError = _sum / _n,
                MaxError = _max,
                P99 = QuantizationAnalyzer.PercentileFromHistogram(_hist, _n, 99) / (double)Scale,
                P999 = QuantizationAnalyzer.PercentileFromHistogram(_hist, _n, 99.9) / (double)Scale,
                PctOverThreshold = 100.0 * _over / _n,
                MeanLevelOverThreshold = _over == 0 ? 0 : _levelOver / _over,
            };
        }
    }
}
