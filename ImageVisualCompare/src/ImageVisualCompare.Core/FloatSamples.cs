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
/// float TIFF, into Bgra32 levels the way a float FITS is shown: as stored, through
/// <see cref="FitsScale"/> (ADR 0035).
/// </summary>
public static class FloatSamples
{
    /// <summary>
    /// Converts one row of float samples to Bgra32: each channel at the level
    /// <see cref="FitsScale.Level"/> gives the sample on the full scale, and alpha 255.
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
        double toSixteenBits = FitsScale.ToSixteenBits(fullScale);
        int perPixel = gray ? 1 : 4;
        long above = 0;

        for (int pixel = 0; pixel < samples.Length / perPixel; pixel++)
        {
            ReadOnlySpan<float> source = samples.Slice(pixel * perPixel, perPixel);
            Span<byte> target = bgra.Slice(pixel * 4, 4);
            if (gray)
            {
                byte level = FitsScale.Level(source[0] * toSixteenBits);
                target[0] = level;
                target[1] = level;
                target[2] = level;
                above += FitsScale.IsAboveThreshold(source[0]) ? 1 : 0;
            }
            else
            {
                // Bgra32 puts blue first; the samples come red first.
                for (int channel = 0; channel < 3; channel++)
                {
                    target[2 - channel] = FitsScale.Level(source[channel] * toSixteenBits);
                    above += FitsScale.IsAboveThreshold(source[channel]) ? 1 : 0;
                }
            }

            target[3] = 255;
        }

        return above;
    }
}
