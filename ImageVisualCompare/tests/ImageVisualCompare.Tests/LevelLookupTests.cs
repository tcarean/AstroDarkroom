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

/// <summary>
/// The equalisation of R-111: Siril 1.4.4's "Histogram" display, the inclusive CDF times 255
/// with its rounding, zeros skipped but counted.
/// </summary>
public class LevelLookupTests
{
    /// <summary>A histogram of the given sixteen-bit values.</summary>
    private static SampleHistogram Histogram(params ushort[] values)
    {
        var histogram = new SampleHistogram();
        histogram.Count(values);
        return histogram;
    }

    [Fact]
    public void R111_TheLookupIsTheInclusiveCdfTimes255_RoundedAsSirilRounds()
    {
        // 1/4, 3/4 and 4/4 of the pixels at or below 1, 2 and 3: 63.75, 191.25 and 255.
        LevelLookup lookup = LevelLookup.Equalising(Histogram(1, 2, 2, 3));

        Assert.Equal(64, lookup.Level(1));
        Assert.Equal(191, lookup.Level(2));
        Assert.Equal(255, lookup.Level(3));
        Assert.Equal(255, lookup.Level(65535));
    }

    [Fact]
    public void R111_ZerosAreLeftOutOfTheCountsButCountedAmongThePixels()
    {
        // Ten pixels, six of them 0: 1/10, 3/10 and 4/10 give 25.5, 76.5 and 102.
        LevelLookup lookup = LevelLookup.Equalising(Histogram(1, 2, 2, 3, 0, 0, 0, 0, 0, 0));

        Assert.Equal(0, lookup.Level(0));
        Assert.Equal(26, lookup.Level(1));
        Assert.Equal(77, lookup.Level(2));
        Assert.Equal(102, lookup.Level(3));
        Assert.Equal(102, lookup.Level(65535));
    }

    [Fact]
    public void R111_ValueZeroIsLevelZero_WhateverTheHistogram()
    {
        Assert.Equal(0, LevelLookup.Equalising(Histogram(0, 0, 0)).Level(0));
        Assert.Equal(0, LevelLookup.Equalising(Histogram(5, 5, 5)).Level(0));
    }

    [Theory]
    [InlineData(63.5, 64)]
    [InlineData(63.49, 63)]
    [InlineData(254.5, 255)]
    [InlineData(300, 255)]
    [InlineData(0, 0)]
    public void R111_RoundToByte_IsSirilsRoundToByte(double value, byte expected)
    {
        Assert.Equal(expected, LevelLookup.RoundToByte(value));
    }

    [Fact]
    public void R111_AUniformHistogram_ComesOutAsALinearRamp()
    {
        var histogram = new SampleHistogram();
        for (int value = 1; value < LevelLookup.Size; value++)
        {
            histogram.Count((ushort)value);
        }

        LevelLookup lookup = LevelLookup.Equalising(histogram);

        for (int value = 1; value < LevelLookup.Size; value++)
        {
            Assert.Equal(LevelLookup.RoundToByte(value / 65535.0 * 255), lookup.Level((ushort)value));
        }
    }

    [Fact]
    public void R111_EveryLevelHoldsTheSameShareOfThePixels()
    {
        // 2550 samples spread over 255 distinct values, ten each: value k lands on level k, so
        // every level from 1 to 255 holds ten, and level 0 (value 0 only) none.
        var histogram = new SampleHistogram();
        for (int value = 1; value <= 255; value++)
        {
            for (int i = 0; i < 10; i++)
            {
                histogram.Count((ushort)(value * 200));
            }
        }

        LevelLookup lookup = LevelLookup.Equalising(histogram);

        var perLevel = new int[256];
        for (int value = 1; value <= 255; value++)
        {
            perLevel[lookup.Level((ushort)(value * 200))] += 10;
        }

        Assert.Equal(0, perLevel[0]);
        Assert.All(perLevel[1..], count => Assert.Equal(10, count));
    }

    [Fact]
    public void R111_AnEmptyHistogram_IsAllZero()
    {
        LevelLookup lookup = LevelLookup.Equalising(new SampleHistogram());

        Assert.All(lookup.Table, level => Assert.Equal(0, level));
    }

    [Fact]
    public void R015_TheLinearLookup_IsTheHighByte()
    {
        for (int value = 0; value < LevelLookup.Size; value++)
        {
            Assert.Equal(value >> 8, LevelLookup.Linear.Level((ushort)value));
        }
    }

    [Fact]
    public void R111_ClearForgetsTheCounts()
    {
        SampleHistogram histogram = Histogram(7, 7, 0);
        Assert.Equal(3, histogram.Pixels);
        Assert.Equal(2, histogram.Counts[7]);

        histogram.Clear();

        Assert.Equal(0, histogram.Pixels);
        Assert.Equal(0, histogram.Counts[7]);
    }
}
