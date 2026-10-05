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
/// How a FITS value becomes one of the 256 levels on screen: linearly, with no stretch, and by
/// the rule WIC applies to a 16-bit PNG, so that a FITS and its own export show the same
/// levels on both sides of the divider (ADR 0029).
/// </summary>
public static class FitsScale
{
    /// <summary>
    /// A float sample above this cannot belong to an image in 0 to 1. The largest sample of
    /// five Siril files measured on 2026-10-05 was 1.0005.
    /// </summary>
    public const double RescaleThreshold = 2.0;

    /// <summary>
    /// The share of a float file's samples that has to be above
    /// <see cref="RescaleThreshold"/> for the file to be taken to count in 0 to 65535. A share,
    /// not one sample: a hot pixel or a stray overshoot in a file in 0 to 1 must not turn the
    /// whole image black, and a file in 0 to 65535 has nearly every sample above 2.
    /// </summary>
    public const double RescaleFraction = 0.01;

    /// <summary>What <see cref="ImageInfo.ValueRange"/> says of a float file read in 0 to 65535.</summary>
    public const string SixteenBitRange = "0..65535";

    private const double SixteenBitMax = 65535;

    /// <summary>The value that is white, for a sample type; for floats, by what <see cref="IsRescaled"/> found.</summary>
    public static double FullScale(int bitPix, bool rescaled = false) => bitPix switch
    {
        8 => 255,
        16 => SixteenBitMax,
        32 => 4294967295,
        _ => rescaled ? SixteenBitMax : 1,
    };

    /// <param name="samplesAbove">How many finite samples of the file are above <see cref="RescaleThreshold"/>.</param>
    /// <param name="samples">How many samples the file has, all planes.</param>
    public static bool IsRescaled(int bitPix, long samplesAbove, long samples) =>
        bitPix < 0 && samplesAbove > samples * RescaleFraction;

    /// <summary>What a value is multiplied by to count in 0 to 65535, given the value that is white.</summary>
    public static double ToSixteenBits(double fullScale) => SixteenBitMax / fullScale;

    /// <summary>The level of a value on a scale whose white is <paramref name="fullScale"/>.</summary>
    public static byte ToByte(double value, double fullScale) => Level(value * ToSixteenBits(fullScale));

    /// <summary>
    /// The level of a value already brought to 0 to 65535: rounded to a 16-bit sample, then the
    /// high byte, which is what WIC keeps of one (measured on all 65536 values). NaN, which
    /// Siril leaves behind after some float operations, is black.
    /// </summary>
    public static byte Level(double sixteenBit)
    {
        if (!(sixteenBit > 0))
        {
            return 0;
        }

        if (sixteenBit >= SixteenBitMax)
        {
            return 255;
        }

        return (byte)((int)(sixteenBit + 0.5) >> 8);
    }
}
