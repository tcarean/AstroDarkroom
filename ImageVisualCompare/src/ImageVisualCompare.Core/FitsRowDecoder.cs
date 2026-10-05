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
/// Turns the samples of a FITS image into Bgra32, one row at a time. FITS stores one whole
/// plane after another, so a row of the image is read from as many places as there are
/// planes; the image is never held as floats, only the row in hand is.
/// </summary>
/// <param name="stream">Seekable, and left where the last row read ended.</param>
/// <param name="fullScale">The value shown as white (<see cref="FitsScale.FullScale"/>).</param>
public sealed class FitsRowDecoder(FitsImageLayout layout, Stream stream, double fullScale)
{
    private readonly byte[] _row = new byte[layout.Width * layout.BytesPerSample];

    /// <summary>
    /// The largest finite value among the rows read so far, after BZERO and BSCALE: what tells
    /// a float file in 0 to 65535 from one in 0 to 1.
    /// </summary>
    public double LargestSample { get; private set; } = double.NegativeInfinity;

    /// <summary>
    /// Reads one row as the file numbers them, 0 first in the file, into
    /// <paramref name="bgra"/>, four bytes a pixel. Where the row belongs in the image is
    /// <see cref="FitsImageLayout.DisplayRow"/>.
    /// </summary>
    public void ReadFileRow(int fileRow, Span<byte> bgra)
    {
        double largest = LargestSample;
        for (int plane = 0; plane < layout.Planes; plane++)
        {
            stream.Position = layout.DataOffset + (plane * layout.PlaneBytes) + ((long)fileRow * _row.Length);
            if (stream.ReadAtLeast(_row, _row.Length, throwOnEndOfStream: false) < _row.Length)
            {
                throw new EndOfStreamException("FITS: the file ends before the image data does.");
            }

            // Red, green, blue in the file; blue, green, red in the bitmap.
            int channel = layout.Planes == 3 ? 2 - plane : 0;
            for (int x = 0; x < layout.Width; x++)
            {
                double value = layout.BZero + (layout.BScale * Sample(x));
                if (value > largest && double.IsFinite(value))
                {
                    largest = value;
                }

                bgra[(x * 4) + channel] = FitsScale.ToByte(value, fullScale);
            }
        }

        LargestSample = largest;

        for (int x = 0; x < layout.Width; x++)
        {
            if (layout.Planes == 1)
            {
                bgra[(x * 4) + 1] = bgra[x * 4];
                bgra[(x * 4) + 2] = bgra[x * 4];
            }

            bgra[(x * 4) + 3] = 255;
        }
    }

    private double Sample(int x)
    {
        ReadOnlySpan<byte> bytes = _row.AsSpan(x * layout.BytesPerSample, layout.BytesPerSample);
        return layout.BitPix switch
        {
            8 => bytes[0],
            16 => BinaryPrimitives.ReadInt16BigEndian(bytes),
            32 => BinaryPrimitives.ReadInt32BigEndian(bytes),
            -32 => BinaryPrimitives.ReadSingleBigEndian(bytes),
            _ => BinaryPrimitives.ReadDoubleBigEndian(bytes),
        };
    }
}
