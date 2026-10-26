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

namespace ImageVisualCompare.Core;

/// <summary>
/// Turns the floating-point samples of an image that is not a FITS, as WIC hands them over for a
/// float TIFF, into the sixteen-bit rows a FITS's samples become, and from there into Bgra32
/// levels the way a float FITS is shown: as stored, through <see cref="FitsScale"/> (ADR 0035,
/// ADR 0040).
/// </summary>
public static class FloatSamples
{
    /// <summary>
    /// Converts one row of float samples to sixteen-bit samples laid out as
    /// <see cref="DisplayRenderer"/> takes them: the planes one after another, red, green,
    /// blue, or the single gray plane, each sample <see cref="FitsScale.SixteenBit"/> of its
    /// value on the full scale.
    /// </summary>
    /// <param name="samples">
    /// The row: one float a pixel when <paramref name="gray"/>, otherwise four, red, green, blue
    /// and a fourth that is not read (WIC's alpha or padding).
    /// </param>
    /// <param name="gray">True for one sample a pixel.</param>
    /// <param name="planesRow">
    /// Receives the width in samples when <paramref name="gray"/>, otherwise three times the
    /// width, the red plane first.
    /// </param>
    /// <param name="fullScale">
    /// The value shown as white, above 0: 1, or 65535 for an image read as 0 to 65535
    /// (<see cref="FitsScale.FullScale"/>).
    /// </param>
    /// <returns>
    /// How many of the row's red, green and blue samples, or gray ones, count towards the
    /// rescale (<see cref="FitsScale.IsAboveThreshold"/>).
    /// </returns>
    public static long ToSixteenBit(ReadOnlySpan<float> samples, bool gray, Span<ushort> planesRow, double fullScale)
    {
        double toSixteenBits = FitsScale.ToSixteenBits(fullScale);
        long above = 0;

        if (gray)
        {
            for (int x = 0; x < samples.Length; x++)
            {
                planesRow[x] = FitsScale.SixteenBit(samples[x] * toSixteenBits);
                above += FitsScale.IsAboveThreshold(samples[x]) ? 1 : 0;
            }

            return above;
        }

        int width = samples.Length / 4;
        for (int x = 0; x < width; x++)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                float value = samples[(x * 4) + channel];
                planesRow[(channel * width) + x] = FitsScale.SixteenBit(value * toSixteenBits);
                above += FitsScale.IsAboveThreshold(value) ? 1 : 0;
            }
        }

        return above;
    }

    /// <summary>
    /// Converts one row of float samples to Bgra32 in the linear display: each channel at the
    /// level <see cref="FitsScale.Level"/> gives the sample on the full scale, and alpha 255.
    /// </summary>
    /// <param name="samples">
    /// The row: one float a pixel when <paramref name="gray"/>, otherwise four, red, green, blue
    /// and a fourth that is not read (WIC's alpha or padding).
    /// </param>
    /// <param name="gray">True for one sample a pixel, shown on all three channels.</param>
    /// <param name="bgra">
    /// Receives four bytes a pixel, blue, green, red and alpha, for every pixel of the row.
    /// </param>
    /// <param name="fullScale">
    /// The value shown as white, above 0: 1, or 65535 for an image read as 0 to 65535
    /// (<see cref="FitsScale.FullScale"/>).
    /// </param>
    /// <returns>
    /// How many of the row's red, green and blue samples, or gray ones, count towards the
    /// rescale (<see cref="FitsScale.IsAboveThreshold"/>).
    /// </returns>
    public static long ToBgra32(ReadOnlySpan<float> samples, bool gray, Span<byte> bgra, double fullScale)
    {
        int width = gray ? samples.Length : samples.Length / 4;
        int planes = gray ? 1 : 3;
        var planesRow = new ushort[width * planes];
        long above = ToSixteenBit(samples, gray, planesRow, fullScale);
        new DisplayRenderer(DisplayMode.Linear, width, planes).Render(planesRow, bgra);
        return above;
    }
}
