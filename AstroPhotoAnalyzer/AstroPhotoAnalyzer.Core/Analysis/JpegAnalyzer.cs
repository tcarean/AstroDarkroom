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

// JpegAnalyzer.cs - the measurement C3: what the image loses at each JPEG quality, and how large
// it would be as an 8-bit TIFF.
//
// Nothing is written to disk: each quality is encoded in memory, decoded back and compared with
// the 16-bit image (on the scale 0..255, UNrounded - so the error includes the rounding to 8 bits
// too, as in the acceptance script). Besides the mean and the maximum error, the samples with an
// error over 2 levels are counted and their mean level is kept: on the reference image they were
// 3 % with ImageMagick and 1.2 % with this encoder at q100 (ADR 0019), and sat in the dark sky,
// where JPEG works worst.
//
// The second file of the core that touches ImageSharp (the first is the reader). 4:4:4, without
// chroma subsampling: the stars coloured on one pixel would not survive 4:2:0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// One JPEG quality (C3): its size and its error against the unrounded image, in levels.
    /// </summary>
    internal sealed record JpegRow
    {
        internal int Quality { get; init; }
        internal long Bytes { get; init; }
        internal double Megabytes => Bytes / 1048576.0;
        internal double MeanAbsError { get; init; }
        internal double MaxError { get; init; }
        internal double P99 { get; init; }
        internal double P999 { get; init; }

        /// <summary>
        /// The percentage of the samples with an error over Thresholds.JpegErrorCountLevels.
        /// </summary>
        internal double PctOverThreshold { get; init; }

        /// <summary>
        /// The mean level (0..255) of those samples - where the compression hits.
        /// </summary>
        internal double MeanLevelOverThreshold { get; init; }

        internal bool Acceptable => MaxError <= Thresholds.JpegMaxErrorReject;
    }

    /// <summary>
    /// What the image loses at each JPEG quality, and its size as an 8-bit TIFF LZW (C3).
    /// </summary>
    internal sealed record JpegResult
    {
        internal IReadOnlyList<JpegRow> Rows { get; init; }

        /// <summary>
        /// The highest quality under 100 whose maximum error passes
        /// <see cref="Thresholds.JpegMaxErrorReject"/>, the one the Export tab names as the floor
        /// not to go under (ADR 0061); null when every quality under 100 is acceptable.
        /// </summary>
        internal JpegRow HighestFailingBelow100 => Rows.Where(x => x.Quality < 100 && !x.Acceptable).OrderByDescending(x => x.Quality).FirstOrDefault();

        internal long Tiff8LzwBytes { get; init; }
        internal double Tiff8LzwMegabytes => Tiff8LzwBytes / 1048576.0;
    }

    /// <summary>
    /// Measures the JPEG qualities and the 8-bit TIFF size (C3), in memory, with ImageSharp's
    /// encoders.
    /// </summary>
    internal static class JpegAnalyzer
    {
        /// <summary>Measures at the qualities of <see cref="Thresholds.JpegQualities"/>.</summary>
        /// <param name="r">The image, 0..1.</param>
        /// <returns>One row per quality, and the 8-bit TIFF LZW size.</returns>
        internal static JpegResult Analyze(Raster r) => Analyze(r, Thresholds.JpegQualities);

        /// <summary>
        /// Quantises the image to 8 bits, encodes it as JPEG at each quality, in memory, decodes
        /// it back and measures the error against the unrounded image; and its size as an 8-bit
        /// TIFF LZW.
        /// </summary>
        /// <param name="r">The image, 0..1.</param>
        /// <param name="qualities">The JPEG qualities, 1..100, in the rows' order.</param>
        /// <returns>
        /// One row per quality, in the given order, and the 8-bit TIFF LZW size, in bytes.
        /// </returns>
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

        /// <summary>A colour raster quantised to 8 bits, as an ImageSharp image.</summary>
        /// <param name="r">A colour raster, 0..1.</param>
        /// <returns>
        /// A new image, every sample at <see cref="RasterOps.Level8"/>; the caller disposes of it.
        /// </returns>
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

        /// <summary>A mono raster quantised to 8 bits, as an ImageSharp image.</summary>
        /// <param name="r">A mono raster, 0..1.</param>
        /// <returns>
        /// A new image, every sample at <see cref="RasterOps.Level8"/>; the caller disposes of it.
        /// </returns>
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

        /// <summary>
        /// The size of the image saved as TIFF LZW with the horizontal predictor, in memory.
        /// </summary>
        /// <typeparam name="T">The pixel type: Rgb24 or L8.</typeparam>
        /// <param name="img">The 8-bit image.</param>
        /// <returns>The size, in bytes.</returns>
        static long TiffBytes<T>(Image<T> img) where T : unmanaged, IPixel<T>
        {
            // The horizontal predictor is what makes LZW useful on photographs: without it, on
            // the reference image ImageSharp gave 38.6 MB where ImageMagick gives 20.
            using var ms = new MemoryStream();
            img.SaveAsTiff(ms, new TiffEncoder { Compression = TiffCompression.Lzw, HorizontalPredictor = TiffPredictor.Horizontal });
            return ms.Length;
        }

        /// <summary>
        /// Encodes the 8-bit colour image as JPEG 4:4:4 at a quality, decodes it back and
        /// accumulates the error of every sample against the raster.
        /// </summary>
        /// <param name="r">The colour raster, 0..1: the reference.</param>
        /// <param name="img">The same raster quantised to 8 bits.</param>
        /// <param name="quality">The JPEG quality, 1..100.</param>
        /// <returns>The quality's row.</returns>
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

        /// <summary>
        /// Encodes the 8-bit mono image as a luminance-only JPEG at a quality, decodes it back and
        /// accumulates the error of every sample against the raster.
        /// </summary>
        /// <param name="r">The mono raster, 0..1: the reference.</param>
        /// <param name="img">The same raster quantised to 8 bits.</param>
        /// <param name="quality">The JPEG quality, 1..100.</param>
        /// <returns>The quality's row.</returns>
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
        /// The error per sample, accumulated in a histogram with a step of 0.01 level: on 69
        /// million samples a sort for the percentiles would cost seconds and hundreds of MB.
        /// </summary>
        sealed class ErrorAccumulator
        {
            const int Scale = 100, Bins = 256 * Scale + 1;
            readonly long[] _hist = new long[Bins];
            long _n, _over;
            double _sum, _max, _levelOver;

            /// <summary>Adds the error of one sample.</summary>
            /// <param name="decoded">The decoded 8-bit value, 0..255.</param>
            /// <param name="reference">
            /// The raster's sample, 0..1; clamped to that range and taken x 255, unrounded.
            /// </param>
            internal void Add(byte decoded, float reference)
            {
                double refLevel = Math.Clamp(reference, 0f, 1f) * 255;
                double e = Math.Abs(decoded - refLevel);
                _sum += e; _n++;
                if (e > _max) _max = e;
                _hist[(int)Math.Min(Bins - 1, e * Scale)]++;
                if (e > Thresholds.JpegErrorCountLevels) { _over++; _levelOver += refLevel; }
            }

            /// <summary>The row of the errors accumulated so far.</summary>
            /// <param name="quality">The JPEG quality the errors belong to.</param>
            /// <param name="bytes">The JPEG's size, in bytes.</param>
            /// <returns>
            /// The row: the mean and the maximum error, the 99th and 99.9th percentiles to 0.01
            /// level, the share of the samples over JpegErrorCountLevels and their mean level (0
            /// when there are none).
            /// </returns>
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
