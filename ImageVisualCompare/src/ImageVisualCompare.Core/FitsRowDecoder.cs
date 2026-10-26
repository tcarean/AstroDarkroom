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
/// row of the image is read from as many places as there are planes; the image is never held
/// as floats, only the row in hand is.
/// </summary>
/// <param name="layout">Where the image's samples are in the file and how they are stored.</param>
/// <param name="stream">Seekable, and left where the last row read ended.</param>
/// <param name="fullScale">The value shown as white (<see cref="FitsScale.FullScale"/>).</param>
public sealed class FitsRowDecoder(FitsImageLayout layout, Stream stream, double fullScale)
{
    private readonly byte[] _row = new byte[layout.Width * layout.BytesPerSample];
    private readonly double _toSixteenBits = FitsScale.ToSixteenBits(fullScale);
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
    /// brought to 0 to 65535 by the full scale and rounded (<see cref="FitsScale.SixteenBit"/>).
    /// </summary>
    /// <param name="fileRow">
    /// The row's place in the file, from 0 to the layout's height less one.
    /// </param>
    /// <param name="plane">The plane, 0 to the layout's planes less one; 0 is red.</param>
    /// <param name="sixteenBit">Receives the layout's width in samples.</param>
    /// <exception cref="EndOfStreamException">The file ends before the row does.</exception>
    public void ReadPlaneRow(int fileRow, int plane, Span<ushort> sixteenBit)
    {
        stream.Position = layout.DataOffset + (plane * layout.PlaneBytes) + ((long)fileRow * _row.Length);
        if (stream.ReadAtLeast(_row, _row.Length, throwOnEndOfStream: false) < _row.Length)
        {
            throw new EndOfStreamException(UiText.Current.FitsDataEndsEarly);
        }

        Convert(sixteenBit);
    }

    /// <summary>
    /// The row in hand into sixteen-bit samples. One loop per sample type, so that nothing is
    /// decided per sample: an image of 23 megapixels in three planes passes here 69 million
    /// times.
    /// </summary>
    /// <param name="sixteenBit">Receives the layout's width in samples.</param>
    private void Convert(Span<ushort> sixteenBit)
    {
        ReadOnlySpan<byte> row = _row;
        int width = layout.Width;
        double zero = layout.BZero;
        double scale = layout.BScale;
        double toSixteenBits = _toSixteenBits;
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
                    sixteenBit[x] = FitsScale.SixteenBit(value * toSixteenBits);
                }

                break;

            default:
                for (int x = 0; x < width; x++)
                {
                    double value = zero + (scale * BinaryPrimitives.ReadDoubleBigEndian(row.Slice(x * 8, 8)));
                    above += FitsScale.IsAboveThreshold(value) ? 1 : 0;
                    sixteenBit[x] = FitsScale.SixteenBit(value * toSixteenBits);
                }

                break;
        }

        SamplesAboveThreshold += above;
    }
}
