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
/// The three display modes over sixteen-bit rows (R-111, R-112): per channel, the mean layer,
/// the gray image, and the linear mode unchanged.
/// </summary>
public class DisplayRendererTests
{
    /// <summary>Counts every row, prepares, and renders every row, as a loader does.</summary>
    private static byte[] Render(DisplayMode mode, int width, int planes, params ushort[][] rows)
    {
        var renderer = new DisplayRenderer(mode, width, planes);
        if (renderer.NeedsCount)
        {
            foreach (ushort[] row in rows)
            {
                renderer.Count(row);
            }
        }

        renderer.Prepare();

        byte[] bgra = new byte[rows.Length * width * 4];
        for (int y = 0; y < rows.Length; y++)
        {
            renderer.Render(rows[y], bgra.AsSpan(y * width * 4, width * 4));
        }

        return bgra;
    }

    [Fact]
    public void R015_Linear_IsTheHighByteOfEachPlane_BlueFirst_Alpha255()
    {
        // Red 65535, green 32768, blue 0 for the first pixel; all 256 for the second.
        byte[] bgra = Render(DisplayMode.Linear, 2, 3, [65535, 256, 32768, 256, 0, 256]);

        Assert.Equal([0, 128, 255, 255, 1, 1, 1, 255], bgra);
    }

    [Fact]
    public void R111_EachChannelIsEqualisedOnItsOwnHistogram()
    {
        // Red holds 1, 2, 2, 3 (64, 191, 191, 255); green holds 3, 3, 3, 3 (all 255); blue holds
        // 0, 0, 0, 9 (0, 0, 0, 64): the same value lands on different levels per channel.
        byte[] bgra = Render(DisplayMode.Histogram, 4, 3, [1, 2, 2, 3, 3, 3, 3, 3, 0, 0, 0, 9]);

        Assert.Equal(
            [0, 255, 64, 255, 0, 255, 191, 255, 0, 255, 191, 255, 64, 255, 255, 255],
            bgra);
    }

    [Fact]
    public void R111_RowsAreCountedTogether_AndAGrayPlaneGoesToAllThreeChannels()
    {
        byte[] bgra = Render(DisplayMode.Histogram, 2, 1, [1, 2], [2, 3]);

        Assert.Equal([64, 64, 64, 255, 191, 191, 191, 255, 191, 191, 191, 255, 255, 255, 255, 255], bgra);
    }

    [Fact]
    public void R112_AGrayImage_IsColouredByItsEqualisedLevel()
    {
        // 1, 2, 2, 3 equalise to 64, 191, 191, 255.
        byte[] bgra = Render(DisplayMode.FalseColours, 4, 1, [1, 2, 2, 3]);

        AssertColour(64, bgra.AsSpan(0, 4));
        AssertColour(191, bgra.AsSpan(4, 4));
        AssertColour(191, bgra.AsSpan(8, 4));
        AssertColour(255, bgra.AsSpan(12, 4));
    }

    [Fact]
    public void R112_AnRgbImage_IsColouredByTheMeanOfItsPlanes_RoundedToNearest()
    {
        // Means: (1+1+1+1)/3 = 1; (2+2+3+1)/3 = 2 (2.33 down); (2+3+3+1)/3 = 3 (2.67 up);
        // (3+3+3+1)/3 = 3. The layer 1, 2, 3, 3 equalises to 64, 128, 255, 255.
        byte[] bgra = Render(DisplayMode.FalseColours, 4, 3, [1, 2, 2, 3, 1, 2, 3, 3, 1, 3, 3, 3]);

        AssertColour(64, bgra.AsSpan(0, 4));
        AssertColour(128, bgra.AsSpan(4, 4));
        AssertColour(255, bgra.AsSpan(8, 4));
        AssertColour(255, bgra.AsSpan(12, 4));
    }

    [Fact]
    public void R112_ValueZero_IsTheColourOfLevelZero_NotBlack()
    {
        byte[] bgra = Render(DisplayMode.FalseColours, 2, 1, [0, 5]);

        AssertColour(0, bgra.AsSpan(0, 4));
        Assert.Equal([255, 0, 255, 255], bgra[..4]);
    }

    [Fact]
    public void R016_ClearCountsForgetsTheFirstPass()
    {
        var renderer = new DisplayRenderer(DisplayMode.Histogram, 2, 1);
        renderer.Count([9, 9]);
        renderer.ClearCounts();
        renderer.Count([1, 3]);
        renderer.Prepare();

        byte[] bgra = new byte[8];
        renderer.Render([1, 3], bgra);

        // Half the pixels at or below 1, all at or below 3: 128 and 255, nothing from the 9s.
        Assert.Equal(128, bgra[0]);
        Assert.Equal(255, bgra[4]);
    }

    [Fact]
    public void R110_OnlyTheLinearModeNeedsNoCount()
    {
        Assert.False(new DisplayRenderer(DisplayMode.Linear, 1, 1).NeedsCount);
        Assert.True(new DisplayRenderer(DisplayMode.Histogram, 1, 1).NeedsCount);
        Assert.True(new DisplayRenderer(DisplayMode.FalseColours, 1, 3).NeedsCount);
    }

    [Fact]
    public void R110_TwoPlanesAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DisplayRenderer(DisplayMode.Linear, 1, 2));
    }

    /// <summary>Asserts that a pixel is the rainbow colour of a level, with alpha 255.</summary>
    private static void AssertColour(byte level, ReadOnlySpan<byte> bgra)
    {
        (byte r, byte g, byte b) = RainbowTable.Colour(level);
        Assert.Equal([b, g, r, 255], bgra.ToArray());
    }
}
