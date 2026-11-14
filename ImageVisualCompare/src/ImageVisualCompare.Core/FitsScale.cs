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

    private const float SixteenBitMaxSingle = 65535f;

    /// <summary>
    /// What Siril multiplies the samples of a float FITS that counts to 65535 by when it reads
    /// it, to bring them to 0 to 1: its <c>INV_USHRT_MAX_SINGLE</c>, written here as its source
    /// writes it (<c>core/siril.h</c> of 1.4, read on 2026-11-14).
    /// </summary>
    public const float SirilSixteenBitToUnit = .000015259022f;

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
    /// Converts a value already brought to 0 to 65535 into a level: rounded to a 16-bit sample
    /// (<see cref="SixteenBit"/>), then the high byte, which is what WIC keeps of one (measured
    /// on all 65536 values). NaN, which Siril leaves behind after some float operations, is
    /// black.
    /// </summary>
    /// <param name="sixteenBit">
    /// The value on the 0 to 65535 scale; anything below 0, and NaN, is black, anything from
    /// 65535 up is white.
    /// </param>
    /// <returns>The level, 0 to 255.</returns>
    public static byte Level(double sixteenBit) => (byte)(SixteenBit(sixteenBit) >> 8);

    /// <summary>
    /// Gives the factor that brings a float sample to 0 to 1, in single precision, for
    /// <see cref="SixteenBitOfFloat"/>.
    /// </summary>
    /// <param name="fullScale">
    /// The value that is white for a float file (<see cref="FullScale"/>): 1, or 65535 for a
    /// file read as 0 to 65535.
    /// </param>
    /// <returns>
    /// 1 for a file in 0 to 1, which leaves the sample as it is; for a file read as 0 to 65535
    /// the factor Siril multiplies such a file by when it reads it, <see cref="SirilSixteenBitToUnit"/>;
    /// for any other scale its inverse, rounded to a <see cref="float"/>.
    /// </returns>
    public static float ToUnitSingle(double fullScale) =>
        fullScale == 1 ? 1f
        : fullScale == SixteenBitMax ? SirilSixteenBitToUnit
        : (float)(1 / fullScale);

    /// <summary>
    /// Brings a float sample to the 16-bit sample it is, in single precision, as Siril 1.4 does
    /// when it shows a float image and when it exports one to a 16-bit PNG or TIFF
    /// (<c>float_to_ushort_range</c>: <c>(WORD)(f * 65535f + 0.5f)</c>): the product and the sum
    /// are rounded to a <see cref="float"/> each, so a product just under a half becomes the
    /// half and goes up, where double precision keeps it under and goes down. That is one
    /// 16-bit value up for 32,768 of the floats below 1, and one level up for 127 of them, all
    /// from 0.5 on (ADR 0055). A float FITS and Siril's own 16-bit export of it then hold the
    /// same samples (R-015).
    /// </summary>
    /// <param name="sample">
    /// The sample on the file's own scale, after BZERO and BSCALE, as a <see cref="float"/>.
    /// Anything that comes out below 0, and NaN, is 0; anything from 65535 up is 65535.
    /// </param>
    /// <param name="toUnit">
    /// What brings the sample to 0 to 1 (<see cref="ToUnitSingle"/>); 1 leaves it as it is.
    /// </param>
    /// <returns>The sample, 0 to 65535.</returns>
    public static ushort SixteenBitOfFloat(float sample, float toUnit)
    {
        // Each step cast to float, so that it is rounded to one as Siril's is, whatever
        // precision the runtime might otherwise carry the expression in.
        float unit = (float)(sample * toUnit);
        float sixteenBit = (float)((float)(unit * SixteenBitMaxSingle) + 0.5f);
        if (!(sixteenBit > 0f))
        {
            return 0;
        }

        if (sixteenBit >= SixteenBitMaxSingle)
        {
            return (ushort)SixteenBitMax;
        }

        return (ushort)sixteenBit;
    }

    /// <summary>
    /// Rounds a value already brought to 0 to 65535 to the 16-bit sample it is: the value a
    /// 16-bit PNG of the same image would hold, which the linear level is the high byte of and
    /// the histogram of R-111 is counted on. In double precision, which is exact for the
    /// integer samples it is used for; a float sample goes through
    /// <see cref="SixteenBitOfFloat"/>, in single precision as Siril computes it.
    /// </summary>
    /// <param name="sixteenBit">
    /// The value on the 0 to 65535 scale; anything below 0, and NaN, is 0, anything from 65535
    /// up is 65535.
    /// </param>
    /// <returns>The sample, 0 to 65535.</returns>
    public static ushort SixteenBit(double sixteenBit)
    {
        if (!(sixteenBit > 0))
        {
            return 0;
        }

        if (sixteenBit >= SixteenBitMax)
        {
            return (ushort)SixteenBitMax;
        }

        return (ushort)(int)(sixteenBit + 0.5);
    }
}
