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

    [Theory]
    [InlineData(8, 0.0, 255.0)]
    [InlineData(16, 0.0, 65535.0)]
    [InlineData(32, 0.0, 4294967295.0)]
    [InlineData(-32, 0.0, 1.0)]
    [InlineData(-64, 0.0, 1.0)]
    public void R015_FullScale_IsTheRangeOfTheSampleType(int bitPix, double largest, double expected)
    {
        Assert.Equal(expected, FitsScale.FullScale(bitPix, largest));
    }

    [Theory]
    [InlineData(-32, 1.0005, 1.0, false)]
    [InlineData(-32, 2.0, 1.0, false)]
    [InlineData(-32, 2.001, 65535.0, true)]
    [InlineData(-64, 41200.0, 65535.0, true)]
    [InlineData(16, 60000.0, 65535.0, false)]
    [InlineData(8, 200.0, 255.0, false)]
    public void R016_FullScale_AFloatFileAboveTheThresholdCountsToSixtyFiveThousand(
        int bitPix, double largest, double expected, bool rescaled)
    {
        Assert.Equal(expected, FitsScale.FullScale(bitPix, largest));
        Assert.Equal(rescaled, FitsScale.IsRescaled(bitPix, largest));
    }

    [Fact]
    public void R016_TheExampleOfTheRequirement_FortyOneThousandTwoHundredShowsAs160()
    {
        Assert.Equal(2.0, FitsScale.RescaleThreshold);
        Assert.Equal(160, FitsScale.ToByte(41200, FitsScale.FullScale(-32, 41200)));
    }
}
