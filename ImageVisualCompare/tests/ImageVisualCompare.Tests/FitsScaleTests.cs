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

using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

public class FitsScaleTests
{
    [Theory]
    [InlineData(0.0, 1.0, 0)]
    [InlineData(255.0 / 65535, 1.0, 0)]
    [InlineData(256.0 / 65535, 1.0, 1)]
    [InlineData(0.5, 1.0, 128)]
    [InlineData(1.0, 1.0, 255)]
    [InlineData(32767.0, 65535.0, 127)]
    [InlineData(32768.0, 65535.0, 128)]
    [InlineData(65279.0, 65535.0, 254)]
    [InlineData(65280.0, 65535.0, 255)]
    [InlineData(200.0, 255.0, 200)]
    [InlineData(2147483648.0, 4294967295.0, 128)]
    public void R015_ToByte_IsTheHighByteOfTheSixteenBitValue(double value, double fullScale, byte expected)
    {
        Assert.Equal(expected, FitsScale.ToByte(value, fullScale));
    }

    [Fact]
    public void R015_ToByte_AnEightBitSampleKeepsItsValue()
    {
        for (int value = 0; value <= 255; value++)
        {
            Assert.Equal(value, FitsScale.ToByte(value, 255));
        }
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(-0.25, 0)]
    [InlineData(double.NegativeInfinity, 0)]
    [InlineData(1.0005, 255)]
    [InlineData(double.PositiveInfinity, 255)]
    public void R016_ToByte_NaNAndBelowZeroAreBlack_AboveTheScaleIsWhite(double value, byte expected)
    {
        Assert.Equal(expected, FitsScale.ToByte(value, 1));
    }

    [Fact]
    public void R015_ToByte_GivesEverySixteenBitValueItsHighByte_OnEachScaleAFileCanHave()
    {
        for (int value = 0; value <= 65535; value++)
        {
            byte highByte = (byte)(value >> 8);

            // As a 16-bit integer, as a float in 0 to 65535, and as the float32 a program
            // writes when it divides that value by 65535.
            Assert.Equal(highByte, FitsScale.ToByte(value, 65535));
            Assert.Equal(highByte, FitsScale.ToByte((float)value, FitsScale.FullScale(-32, rescaled: true)));
            Assert.Equal(highByte, FitsScale.ToByte(value / 65535f, FitsScale.FullScale(-32)));
        }
    }

    /// <summary>
    /// CR-007 of the review of 2026-11-13. Siril turns a float into 16 bits in single precision,
    /// <c>(WORD)(f * 65535f + 0.5f)</c> (its <c>float_to_ushort_range</c>, read in the source of
    /// 1.4 on 2026-11-14), for its display and its 16-bit exports. A product just under a half
    /// becomes the half in a <c>float</c> and rounds up; in double precision it rounds down.
    /// </summary>
    [Theory]
    [InlineData(0x3F560056u, 54784)] // 0.8359426259994507: level 214; in double 54783, level 213
    [InlineData(0x3F720072u, 61952)] // 0.9453192949295044: level 242; in double 61951, level 241
    [InlineData(0x3F000000u, 32768)] // 0.5
    [InlineData(0x3F800000u, 65535)] // 1
    [InlineData(0x3F801062u, 65535)] // 1.0005, above the scale
    [InlineData(0x00000000u, 0)]
    [InlineData(0xBF800000u, 0)] // -1
    [InlineData(0x7FC00000u, 0)] // NaN
    [InlineData(0x7F800000u, 65535)] // +infinity
    [InlineData(0xFF800000u, 0)] // -infinity
    public void R015_AFloatSample_IsBroughtToSixteenBitsInSinglePrecision_AsSirilDoes(uint bits, int expected)
    {
        float sample = BitConverter.UInt32BitsToSingle(bits);

        Assert.Equal(expected, FitsScale.SixteenBitOfFloat(sample, FitsScale.ToUnitSingle(1)));
    }

    [Fact]
    public void R015_SinglePrecision_DiffersFromDouble_OnlyByOneUp_AndByALevelFor127Floats()
    {
        // Every float from 0.5 up to 1, the binade where the product is rounded to 1/256: the
        // figures measured on 2026-11-14 (ADR 0055).
        uint first = BitConverter.SingleToUInt32Bits(0.5f);
        uint last = BitConverter.SingleToUInt32Bits(1f);
        float toUnit = FitsScale.ToUnitSingle(1);
        int sixteenBitDiffers = 0;
        int levelDiffers = 0;
        int notOneUp = 0;
        for (uint bits = first; bits < last; bits++)
        {
            float sample = BitConverter.UInt32BitsToSingle(bits);
            int inSingle = FitsScale.SixteenBitOfFloat(sample, toUnit);
            int inDouble = FitsScale.SixteenBit(sample * 65535.0);
            if (inSingle != inDouble)
            {
                sixteenBitDiffers++;
                notOneUp += inSingle == inDouble + 1 ? 0 : 1;
                levelDiffers += (inSingle >> 8) == (inDouble >> 8) ? 0 : 1;
            }
        }

        Assert.Equal((16384, 127, 0), (sixteenBitDiffers, levelDiffers, notOneUp));
    }

    [Fact]
    public void R015_AFloatSample_KeepsEverySixteenBitValue_OnBothScales()
    {
        // The float a program writes when it divides a 16-bit value by 65535, and the same
        // value written as it is into a file that counts to 65535: both are that value again.
        float unit = FitsScale.ToUnitSingle(FitsScale.FullScale(-32));
        float rescaled = FitsScale.ToUnitSingle(FitsScale.FullScale(-32, rescaled: true));
        for (int value = 0; value <= 65535; value++)
        {
            Assert.Equal(value, FitsScale.SixteenBitOfFloat(value / 65535f, unit));
            Assert.Equal(value, FitsScale.SixteenBitOfFloat(value, rescaled));
        }
    }

    [Fact]
    public void R015_ToUnitSingle_IsOne_OrSirilsFactorForAFileThatCountsTo65535()
    {
        Assert.Equal(1f, FitsScale.ToUnitSingle(1));

        // Siril's INV_USHRT_MAX_SINGLE, as its source writes it.
        Assert.Equal(.000015259022f, FitsScale.ToUnitSingle(65535));
    }

    [Theory]
    [InlineData(8, 255.0)]
    [InlineData(16, 65535.0)]
    [InlineData(32, 4294967295.0)]
    [InlineData(-32, 1.0)]
    [InlineData(-64, 1.0)]
    public void R015_FullScale_IsTheRangeOfTheSampleType(int bitPix, double expected)
    {
        Assert.Equal(expected, FitsScale.FullScale(bitPix));
    }

    [Theory]
    [InlineData(-32, 65535.0)]
    [InlineData(-64, 65535.0)]
    [InlineData(16, 65535.0)]
    [InlineData(8, 255.0)]
    public void R016_FullScale_OnlyAFloatFileCanBeRescaled(int bitPix, double expected)
    {
        Assert.Equal(expected, FitsScale.FullScale(bitPix, rescaled: true));
    }

    [Theory]
    [InlineData(-32, 0, 1000, false)]
    [InlineData(-32, 1, 1000, false)]
    [InlineData(-32, 10, 1000, false)]
    [InlineData(-32, 11, 1000, true)]
    [InlineData(-64, 1000, 1000, true)]
    [InlineData(16, 1000, 1000, false)]
    [InlineData(8, 1000, 1000, false)]
    public void R016_IsRescaled_WhenMoreThanOneSampleInAHundredIsAboveTheThreshold(
        int bitPix, long above, long samples, bool rescaled)
    {
        Assert.Equal(rescaled, FitsScale.IsRescaled(bitPix, above, samples));
    }

    [Fact]
    public void R016_TheFiguresOfTheRequirement()
    {
        Assert.Equal(2.0, FitsScale.RescaleThreshold);
        Assert.Equal(0.01, FitsScale.RescaleFraction);
        Assert.Equal(160, FitsScale.ToByte(41200, FitsScale.FullScale(-32, rescaled: true)));

        // result_3813s_wcsplane_bge.fit: 1 sample above 1 (1.0005, not above 2) of 69 260 832.
        Assert.False(FitsScale.IsRescaled(-32, 0, 69260832));
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.49, 0)]
    [InlineData(0.5, 1)]
    [InlineData(255.4, 255)]
    [InlineData(32767.5, 32768)]
    [InlineData(65534.4, 65534)]
    [InlineData(65534.6, 65535)]
    [InlineData(65535.0, 65535)]
    [InlineData(70000.0, 65535)]
    [InlineData(-1.0, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 65535)]
    public void R111_SixteenBit_RoundsToTheNearestSample_AndClampsTheScale(double value, int expected)
    {
        Assert.Equal(expected, FitsScale.SixteenBit(value));
    }

}
