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
/// The float samples of a TIFF, as WIC hands them over, turned into levels the way a float FITS
/// is shown (R-091, ADR 0035).
/// </summary>
public class FloatSamplesTests
{
    [Fact]
    public void R091_RgbSamples_AreShownAsAFitsShowsThem()
    {
        // Four floats a pixel, red, green, blue and one that is not read.
        float[] row = [0.5f, 0.25f, 1.0f, 0.9f, float.NaN, -1f, 2f, 0f];
        byte[] bgra = new byte[8];

        FloatSamples.ToBgra32(row, gray: false, bgra, fullScale: 1);

        Assert.Equal(
            new byte[]
            {
                FitsScale.ToByte(1.0, 1), FitsScale.ToByte(0.25, 1), FitsScale.ToByte(0.5, 1), 255,
                255, 0, 0, 255,
            },
            bgra);
        Assert.Equal(128, bgra[2]);
    }

    [Fact]
    public void R091_GraySamples_AreShownOnAllThreeChannels()
    {
        float[] row = [0.5f, 0f];
        byte[] bgra = new byte[8];

        FloatSamples.ToBgra32(row, gray: true, bgra, fullScale: 1);

        Assert.Equal(new byte[] { 128, 128, 128, 255, 0, 0, 0, 255 }, bgra);
    }

    [Fact]
    public void R091_SamplesAboveTwo_AreCounted_FiniteOnes_AndNotTheFourthFloat()
    {
        // Three red-green-blue samples above 2 in the first pixel, and its fourth float, which is
        // not a channel; in the second an infinity and NaN, which say nothing of the scale.
        float[] row = [3f, 3f, 3f, 100f, float.PositiveInfinity, float.NaN, 2.5f, 0f];

        long above = FloatSamples.ToBgra32(row, gray: false, new byte[8], fullScale: 1);

        Assert.Equal(4, above);
    }

    [Fact]
    public void R091_AFullScaleOf65535_ReadsTheSampleAsSixteenBits()
    {
        float[] row = [41200f];
        byte[] bgra = new byte[4];

        FloatSamples.ToBgra32(row, gray: true, bgra, fullScale: 65535);

        Assert.Equal(160, bgra[0]);
    }
}
