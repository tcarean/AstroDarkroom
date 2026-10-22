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

    /// <summary>Gives the value shown as white for a sample type.</summary>
    /// <param name="bitPix">
    /// The FITS sample type: 8, 16 or 32 for integers, -32 or -64 for floats.
    /// </param>
    /// <param name="rescaled">
    /// For a float file, what <see cref="IsRescaled"/> found: true when it counts in 0 to 65535;
    /// not read for integers.
    /// </param>
    /// <returns>
    /// The value that is white, for a sample type: 255, 65535 or 4294967295 for 8, 16 or 32-bit
    /// integers; for floats, 1, or 65535 when <paramref name="rescaled"/>.
    /// </returns>
    public static double FullScale(int bitPix, bool rescaled = false) => bitPix switch
    {
        8 => 255,
        16 => SixteenBitMax,
        32 => 4294967295,
        _ => rescaled ? SixteenBitMax : 1,
    };

    /// <summary>
    /// Decides, once every sample of a float file has been read, whether the file counts in 0
    /// to 65535 rather than in 0 to 1.
    /// </summary>
    /// <param name="bitPix">
    /// The FITS sample type; only a float type, -32 or -64, can be rescaled.
    /// </param>
    /// <param name="samplesAbove">How many finite samples of the file are above <see cref="RescaleThreshold"/>.</param>
    /// <param name="samples">How many samples the file has, all planes.</param>
    /// <returns>
    /// True for a float file in which more than <see cref="RescaleFraction"/> of the samples are
    /// above the threshold.
    /// </returns>
    public static bool IsRescaled(int bitPix, long samplesAbove, long samples) =>
        bitPix < 0 && samplesAbove > samples * RescaleFraction;

    // An infinity says nothing about the scale of the file; NaN fails the first comparison.
    /// <summary>
    /// Tells whether a float sample counts towards <see cref="IsRescaled"/>, for a FITS and for
    /// a float TIFF alike.
    /// </summary>
    /// <param name="value">The sample; for a FITS, after BZERO and BSCALE.</param>
    /// <returns>True when it is finite and above <see cref="RescaleThreshold"/>.</returns>
    public static bool IsAboveThreshold(double value) =>
        value > RescaleThreshold && value <= double.MaxValue;

    /// <summary>Gives the factor that brings a value to 0 to 65535.</summary>
    /// <param name="fullScale">The value that is white (<see cref="FullScale"/>); above 0.</param>
    /// <returns>
    /// What a value is multiplied by to count in 0 to 65535, given the value that is white.
    /// </returns>
    public static double ToSixteenBits(double fullScale) => SixteenBitMax / fullScale;

    /// <summary>
    /// Converts a value to the level it is shown at, by the rule of <see cref="Level"/>.
    /// </summary>
    /// <param name="value">A sample after BZERO and BSCALE, on the file's own scale.</param>
    /// <param name="fullScale">The value that is white (<see cref="FullScale"/>); above 0.</param>
    /// <returns>
    /// The level of a value on a scale whose white is <paramref name="fullScale"/>, 0 to 255.
    /// </returns>
    public static byte ToByte(double value, double fullScale) => Level(value * ToSixteenBits(fullScale));

    /// <summary>
    /// Converts a value already brought to 0 to 65535 into a level: rounded to a 16-bit sample,
    /// then the high byte, which is what WIC keeps of one (measured on all 65536 values). NaN,
    /// which Siril leaves behind after some float operations, is black.
    /// </summary>
    /// <param name="sixteenBit">
    /// The value on the 0 to 65535 scale; anything below 0, and NaN, is black, anything from
    /// 65535 up is white.
    /// </param>
    /// <returns>The level, 0 to 255.</returns>
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
