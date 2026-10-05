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

using System.IO;
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

public class FitsRowDecoderTests
{
    /// <summary>One row of the file, decoded on the full scale of its type.</summary>
    private static (byte[] Bgra, FitsRowDecoder Decoder) Row(byte[] file, int fileRow)
    {
        var stream = new MemoryStream(file);
        FitsImageLayout layout = FitsHeader.ReadLayout(stream);
        var decoder = new FitsRowDecoder(layout, stream, FitsScale.FullScale(layout.BitPix));
        byte[] bgra = new byte[layout.Width * 4];
        decoder.ReadFileRow(fileRow, bgra);
        return (bgra, decoder);
    }

    [Fact]
    public void R014_ThreePlanes_AreRedGreenBlue_WrittenAsBgraWithFullAlpha()
    {
        // One pixel: red 1, green 0.5, blue 0.
        byte[] file = FitsFixture.Image(-32, 1, 1, 3, [1, 0.5, 0]);

        Assert.Equal([0, 128, 255, 255], Row(file, 0).Bgra);
    }

    [Fact]
    public void R014_OnePlane_IsShownGray()
    {
        byte[] file = FitsFixture.Image(-64, 2, 1, 1, [0.5, 1]);

        Assert.Equal([128, 128, 128, 255, 255, 255, 255, 255], Row(file, 0).Bgra);
    }

    [Fact]
    public void R014_BZeroMakesSixteenBitSamplesUnsigned()
    {
        // Stored -32768, 0 and 32767 with BZERO 32768 are 0, 32768 and 65535.
        byte[] file = FitsFixture.Image(16, 3, 1, 1, [-32768, 0, 32767], FitsFixture.Card("BZERO", 32768));

        byte[] bgra = Row(file, 0).Bgra;

        Assert.Equal(0, bgra[0]);
        Assert.Equal(128, bgra[4]);
        Assert.Equal(255, bgra[8]);
    }

    [Fact]
    public void R014_BScaleMultipliesBeforeBZeroIsAdded()
    {
        // 100 x 2 + 55 = 255, white for an 8-bit file.
        byte[] file = FitsFixture.Image(8, 1, 1, 1, [100], FitsFixture.Card("BSCALE", 2), FitsFixture.Card("BZERO", 55));

        Assert.Equal(255, Row(file, 0).Bgra[0]);
    }

    [Theory]
    [InlineData(8, 200.0, 200)]
    [InlineData(16, 32767.0, 127)]
    [InlineData(32, 2147483647.0, 127)]
    public void R015_Integers_AreShownOverTheirFullRange(int bitPix, double stored, byte expected)
    {
        byte[] file = FitsFixture.Image(bitPix, 1, 1, 1, [stored]);

        Assert.Equal(expected, Row(file, 0).Bgra[0]);
    }

    [Fact]
    public void R014_EachRowIsReadFromItsOwnPlaceInEveryPlane()
    {
        // 2 x 2, three planes; the second row of the file holds red 1, 0; green 0, 1; blue 0, 0.
        byte[] file = FitsFixture.Image(-32, 2, 2, 3, [0, 0, 1, 0, /* green */ 0, 0, 0, 1, /* blue */ 0, 0, 0, 0]);

        Assert.Equal([0, 0, 255, 255, 0, 255, 0, 255], Row(file, 1).Bgra);
        Assert.Equal([0, 0, 0, 255, 0, 0, 0, 255], Row(file, 0).Bgra);
    }

    [Fact]
    public void R016_LargestSample_IsTheLargestFiniteValue()
    {
        byte[] file = FitsFixture.Image(
            -32, 5, 1, 1, [0.25, double.NaN, double.PositiveInfinity, 0.75, double.NegativeInfinity]);

        (byte[] bgra, FitsRowDecoder decoder) = Row(file, 0);

        Assert.Equal(0.75, decoder.LargestSample);
        Assert.Equal(0, bgra[4]);
        Assert.Equal(255, bgra[8]);
        Assert.Equal(0, bgra[16]);
    }

    [Fact]
    public void R016_LargestSample_CountsBZeroAndBScale_AndEveryRowReadSoFar()
    {
        byte[] file = FitsFixture.Image(16, 1, 2, 1, [100, -5], FitsFixture.Card("BZERO", 32768));
        var stream = new MemoryStream(file);
        FitsImageLayout layout = FitsHeader.ReadLayout(stream);
        var decoder = new FitsRowDecoder(layout, stream, 65535);
        byte[] bgra = new byte[4];

        decoder.ReadFileRow(1, bgra);
        Assert.Equal(32763, decoder.LargestSample);

        decoder.ReadFileRow(0, bgra);
        Assert.Equal(32868, decoder.LargestSample);
    }

    [Fact]
    public void R019_ARowPastTheEndOfTheData_IsAMessageNotACrash()
    {
        var layout = new FitsImageLayout(4, 4, 1, -32, 0, 1, BottomUp: true, DataOffset: 2880);
        var decoder = new FitsRowDecoder(layout, new MemoryStream(new byte[2880 + 16]), 1);

        var ex = Assert.Throws<EndOfStreamException>(() => decoder.ReadFileRow(1, new byte[16]));

        Assert.Equal("FITS: the file ends before the image data does.", ex.Message);
    }
}
