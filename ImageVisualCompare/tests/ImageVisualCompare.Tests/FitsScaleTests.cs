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
