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
using System.Globalization;
using System.Text;

namespace ImageVisualCompare.Tests;

/// <summary>
/// Builds FITS files in memory, card by card, so the tests say in code what the file holds
/// and no binary fixture enters the repository (ADR 0026). Samples are the stored numbers,
/// before BZERO and BSCALE, one whole plane after another, the first row of the file first.
/// </summary>
internal static class FitsFixture
{
    private const int BlockSize = 2880;

    /// <summary>A primary unit holding one image, with any further cards after the axes.</summary>
    public static byte[] Image(int bitPix, int width, int height, int planes, double[] samples, params string[] cards)
    {
        var all = new List<string> { Card("SIMPLE", "T") };
        all.AddRange(ImageCards(bitPix, width, height, planes));
        all.AddRange(cards);
        return [.. Header(all), .. Data(bitPix, samples)];
    }

    public static IEnumerable<string> ImageCards(int bitPix, int width, int height, int planes)
    {
        yield return Card("BITPIX", bitPix);
        yield return Card("NAXIS", planes == 1 ? 2 : 3);
        yield return Card("NAXIS1", width);
        yield return Card("NAXIS2", height);
        if (planes != 1)
        {
            yield return Card("NAXIS3", planes);
        }
    }

    public static string Card(string key, string value) => (key.PadRight(8) + "= " + value.PadLeft(20)).PadRight(80);

    public static string Card(string key, double value) => Card(key, value.ToString(CultureInfo.InvariantCulture));

    public static string Text(string key, string value) => (key.PadRight(8) + "= '" + value.PadRight(8) + "'").PadRight(80);

    /// <summary>The cards, END, and spaces to the end of the 2880-byte block.</summary>
    public static byte[] Header(IEnumerable<string> cards)
    {
        string text = string.Concat(cards) + "END".PadRight(80);
        int padded = (text.Length + BlockSize - 1) / BlockSize * BlockSize;
        return Encoding.ASCII.GetBytes(text.PadRight(padded));
    }

    /// <summary>The samples, big-endian, and zeros to the end of the block.</summary>
    public static byte[] Data(int bitPix, double[] samples)
    {
        int size = Math.Abs(bitPix) / 8;
        byte[] data = new byte[(samples.Length * size + BlockSize - 1) / BlockSize * BlockSize];
        for (int i = 0; i < samples.Length; i++)
        {
            Span<byte> at = data.AsSpan(i * size, size);
            switch (bitPix)
            {
                case 8: at[0] = (byte)samples[i]; break;
                case 16: BinaryPrimitives.WriteInt16BigEndian(at, (short)samples[i]); break;
                case 32: BinaryPrimitives.WriteInt32BigEndian(at, (int)samples[i]); break;
                case -32: BinaryPrimitives.WriteSingleBigEndian(at, (float)samples[i]); break;
                default: BinaryPrimitives.WriteDoubleBigEndian(at, samples[i]); break;
            }
        }

        return data;
    }
}
