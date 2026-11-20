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

using System.Buffers.Binary;

namespace ImageVisualCompare.Core;

/// <summary>
/// Turns the samples of a FITS image into sixteen-bit rows, one row at a time, and through a
/// <see cref="DisplayRenderer"/> into Bgra32. FITS stores one whole plane after another, so a
/// row of the image lies in as many places as there are planes. Each plane is read a band of
/// consecutive rows at a time, so the file is read in a few large pieces and not a row from
/// each place in turn; the image is never held whole, only a band of each plane is.
/// </summary>
/// <param name="layout">Where the image's samples are in the file and how they are stored.</param>
/// <param name="stream">Seekable, and left where the last band read ended.</param>
/// <param name="fullScale">The value shown as white (<see cref="FitsScale.FullScale"/>).</param>
/// <param name="bandBytes">
/// The most bytes of one plane held at a time, at least 1; a band is as many whole rows as fit
/// in them, and one row when a row is longer. <see cref="DefaultBandBytes"/> unless a test wants
/// a band's edge inside a small image.
/// </param>
public sealed class FitsRowDecoder(
    FitsImageLayout layout, Stream stream, double fullScale, int bandBytes = FitsRowDecoder.DefaultBandBytes)
{
    /// <summary>
    /// The bytes of one plane read at a time, 4 MiB. Measured on a 277 MB file of three float
    /// planes out of the file cache, on a hard disk: the reads alone took 3.3 s a row at a time,
    /// 2.3 s in bands of 1 MiB, 1.7 s at 4 MiB and 1.6 s at 8 MiB, where one read from start to
    /// end took 1.5 s (<c>docs/design/imaging.md</c>).
    /// </summary>
    public const int DefaultBandBytes = 4 * 1024 * 1024;

    private readonly int _rowBytes = layout.Width * layout.BytesPerSample;
    private readonly int _bandRows = BandRows(layout, bandBytes);

    /// <summary>The band of each plane, made when the plane is first read.</summary>
    private readonly byte[]?[] _bands = new byte[layout.Planes][];

    /// <summary>The row of the file each plane's band starts at.</summary>
    private readonly int[] _bandStart = new int[layout.Planes];

    /// <summary>
    /// How many whole rows of each plane's band the file gave: fewer than the band's when the
    /// file ends inside it.
    /// </summary>
    private readonly int[] _bandRowsRead = new int[layout.Planes];
    private readonly double _toSixteenBits = FitsScale.ToSixteenBits(fullScale);

    /// <summary>What brings a float sample to 0 to 1, in single precision; read for floats only.</summary>
    private readonly float _toUnit = FitsScale.ToUnitSingle(fullScale);
    private ushort[]? _planesRow;
    private DisplayRenderer? _linear;

    /// <summary>
    /// How many float samples among the rows read so far are finite and above
    /// <see cref="FitsScale.RescaleThreshold"/>, after BZERO and BSCALE: what tells a file in 0
    /// to 65535 from one in 0 to 1. Integer samples are not counted; their scale is their type.
    /// </summary>
    public long SamplesAboveThreshold { get; private set; }

    /// <summary>How many samples a row of every plane holds: the planes times the width.</summary>
    public int RowSamples => layout.Planes * layout.Width;

    /// <summary>
    /// Reads one row of every plane, as the file numbers the rows, 0 first in the file, into
    /// <paramref name="planesRow"/>: the planes one after another in the file's order, red,
    /// green, blue, the width in samples each, as <see cref="DisplayRenderer"/> takes them.
    /// Where the row belongs in the image is <see cref="FitsImageLayout.DisplayRow"/>.
    /// </summary>
    /// <param name="fileRow">
    /// The row's place in the file, from 0 to the layout's height less one.
    /// </param>
    /// <param name="planesRow">Receives <see cref="RowSamples"/> sixteen-bit samples.</param>
    /// <exception cref="EndOfStreamException">The file ends before the row does.</exception>
    public void ReadFileRow(int fileRow, Span<ushort> planesRow)
    {
        for (int plane = 0; plane < layout.Planes; plane++)
        {
            ReadPlaneRow(fileRow, plane, planesRow.Slice(plane * layout.Width, layout.Width));
        }
    }

    /// <summary>
    /// Reads one row as the file numbers them, 0 first in the file, into
    /// <paramref name="bgra"/>, four bytes a pixel, in the linear display. Where the row belongs
    /// in the image is <see cref="FitsImageLayout.DisplayRow"/>.
    /// </summary>
    /// <param name="fileRow">
    /// The row's place in the file, from 0 to the layout's height less one.
    /// </param>
    /// <param name="bgra">
    /// Receives the row: the layout's width in pixels, blue, green, red, alpha, with alpha at
    /// 255; a single plane is written to all three colours.
    /// </param>
    /// <exception cref="EndOfStreamException">The file ends before the row does.</exception>
    public void ReadFileRow(int fileRow, Span<byte> bgra)
    {
        _planesRow ??= new ushort[RowSamples];
        _linear ??= new DisplayRenderer(DisplayMode.Linear, layout.Width, layout.Planes);
        ReadFileRow(fileRow, _planesRow);
        _linear.Render(_planesRow, bgra);
    }

    /// <summary>
    /// Reads one row of one plane into sixteen-bit samples: each value after BZERO and BSCALE,
    /// brought to 0 to 65535 by the full scale and rounded (<see cref="FitsScale.SixteenBit"/>
    /// for an integer sample, <see cref="FitsScale.SixteenBitOfFloat"/> for a float one). The
    /// row comes from the plane's band, which is read from the file when the row is outside the
    /// one in hand.
    /// </summary>
    /// <param name="fileRow">
    /// The row's place in the file, from 0 to the layout's height less one.
    /// </param>
    /// <param name="plane">The plane, 0 to the layout's planes less one; 0 is red.</param>
    /// <param name="sixteenBit">Receives the layout's width in samples.</param>
    /// <exception cref="EndOfStreamException">
    /// The file ends before the row does; a row the file does hold is read even when the file
    /// ends later in its band.
    /// </exception>
    public void ReadPlaneRow(int fileRow, int plane, Span<ushort> sixteenBit)
    {
        // The band that holds the row starts at a multiple of the band's rows, so rows asked
        // for backwards cost a read a band too, not a read a row.
        int start = fileRow / _bandRows * _bandRows;
        if (_bands[plane] is not { } band || _bandStart[plane] != start)
        {
            band = FillBand(plane, start);
        }

        int index = fileRow - start;
        if (index >= _bandRowsRead[plane])
        {
            throw new EndOfStreamException(UiText.Current.FitsDataEndsEarly);
        }

        Convert(band.AsSpan(index * _rowBytes, _rowBytes), sixteenBit);
    }

    /// <summary>
    /// Works out how many rows a band holds: as many whole rows as fit in the bytes allowed,
    /// at least one, and no more than the image has.
    /// </summary>
    /// <param name="layout">The image; its width and sample size give the bytes of a row.</param>
    /// <param name="bandBytes">The most bytes of one plane held at a time, at least 1.</param>
    /// <returns>The rows of a band, from 1 to the image's height.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bandBytes"/> is below 1.
    /// </exception>
    private static int BandRows(FitsImageLayout layout, int bandBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bandBytes, 1);

        return Math.Clamp(bandBytes / (layout.Width * layout.BytesPerSample), 1, layout.Height);
    }

    /// <summary>
    /// Reads one band of one plane from the file, in one read: the rows from
    /// <paramref name="start"/> on, as many as a band holds or as the plane has left.
    /// </summary>
    /// <param name="plane">The plane, 0 to the layout's planes less one.</param>
    /// <param name="start">The first row of the band, as the file numbers the rows.</param>
    /// <returns>
    /// The plane's band, holding the rows read; how many whole rows the file gave is kept, so a
    /// row the file does not reach is refused when it is asked for, not here.
    /// </returns>
    private byte[] FillBand(int plane, int start)
    {
        byte[] band = _bands[plane] ??= new byte[_bandRows * _rowBytes];
        int wanted = Math.Max(0, Math.Min(_bandRows, layout.Height - start)) * _rowBytes;

        stream.Position = layout.DataOffset + (plane * layout.PlaneBytes) + ((long)start * _rowBytes);
        int read = stream.ReadAtLeast(band.AsSpan(0, wanted), wanted, throwOnEndOfStream: false);

        _bandStart[plane] = start;
        _bandRowsRead[plane] = read / _rowBytes;
        return band;
    }

    /// <summary>
    /// One row of one plane into sixteen-bit samples. One loop per sample type, so that nothing
    /// is decided per sample: an image of 23 megapixels in three planes passes here 69 million
    /// times. An integer sample is scaled in double precision, which is exact for it; a float
    /// one, after BZERO and BSCALE, is held as a <see cref="float"/>, as Siril holds it, and
    /// brought to sixteen bits in single precision (<see cref="FitsScale.SixteenBitOfFloat"/>).
    /// </summary>
    /// <param name="row">The row's bytes as the file stores them, big-endian.</param>
    /// <param name="sixteenBit">Receives the layout's width in samples.</param>
    private void Convert(ReadOnlySpan<byte> row, Span<ushort> sixteenBit)
    {
        int width = layout.Width;
        double zero = layout.BZero;
        double scale = layout.BScale;
        double toSixteenBits = _toSixteenBits;
        float toUnit = _toUnit;
        long above = 0;

        switch (layout.BitPix)
        {
            case 8:
                for (int x = 0; x < width; x++)
                {
                    sixteenBit[x] = FitsScale.SixteenBit((zero + (scale * row[x])) * toSixteenBits);
                }

                break;

            case 16:
                for (int x = 0; x < width; x++)
                {
                    short stored = BinaryPrimitives.ReadInt16BigEndian(row.Slice(x * 2, 2));
                    sixteenBit[x] = FitsScale.SixteenBit((zero + (scale * stored)) * toSixteenBits);
                }

                break;

            case 32:
                for (int x = 0; x < width; x++)
                {
                    int stored = BinaryPrimitives.ReadInt32BigEndian(row.Slice(x * 4, 4));
                    sixteenBit[x] = FitsScale.SixteenBit((zero + (scale * stored)) * toSixteenBits);
                }

                break;

            case -32:
                for (int x = 0; x < width; x++)
                {
                    double value = zero + (scale * BinaryPrimitives.ReadSingleBigEndian(row.Slice(x * 4, 4)));
                    above += FitsScale.IsAboveThreshold(value) ? 1 : 0;
                    sixteenBit[x] = FitsScale.SixteenBitOfFloat((float)value, toUnit);
                }

                break;

            default:
                for (int x = 0; x < width; x++)
                {
                    // Narrowed to a float first: Siril has cfitsio hand it a double image as
                    // floats, and quantises those.
                    double value = zero + (scale * BinaryPrimitives.ReadDoubleBigEndian(row.Slice(x * 8, 8)));
                    above += FitsScale.IsAboveThreshold(value) ? 1 : 0;
                    sixteenBit[x] = FitsScale.SixteenBitOfFloat((float)value, toUnit);
                }

                break;
        }

        SamplesAboveThreshold += above;
    }
}
