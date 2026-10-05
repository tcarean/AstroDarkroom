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

public class ImageInfoTests
{
    [Theory]
    [InlineData(16, ColorChannels.Rgb, "16-bit RGB")]
    [InlineData(8, ColorChannels.Rgba, "8-bit RGBA")]
    [InlineData(16, ColorChannels.Gray, "16-bit gray")]
    [InlineData(8, ColorChannels.GrayAlpha, "8-bit gray + alpha")]
    [InlineData(4, ColorChannels.Indexed, "4-bit indexed")]
    [InlineData(8, ColorChannels.Unknown, "8-bit")]
    public void R011_FormatDescription_ReportsWhatWasLoaded(int bits, ColorChannels channels, string expected)
    {
        var info = new ImageInfo(@"C:\x.png", 10, 10, bits, channels);

        Assert.Equal(expected, info.FormatDescription);
    }

    [Theory]
    [InlineData(32, ColorChannels.Rgb, null, "32-bit float RGB")]
    [InlineData(64, ColorChannels.Gray, null, "64-bit float gray")]
    [InlineData(32, ColorChannels.Gray, "0..65535", "32-bit float gray (0..65535)")]
    public void R018_FormatDescription_SaysFloatAndTheRangeTheValuesWereReadIn(
        int bits, ColorChannels channels, string? range, string expected)
    {
        var info = new ImageInfo(@"C:\x.fit", 10, 10, bits, channels, IsFloat: true, ValueRange: range);

        Assert.Equal(expected, info.FormatDescription);
    }

    [Fact]
    public void R013_FileName_StripsTheDirectory()
    {
        var info = new ImageInfo(@"E:\Photografie\Astro\final\stack.png", 10, 10, 16, ColorChannels.Rgb);

        Assert.Equal("stack.png", info.FileName);
    }

    [Fact]
    public void R013_DisplayBytes_CountsTheBgra32Footprint()
    {
        var info = new ImageInfo(@"C:\x.png", 5794, 3637, 16, ColorChannels.Rgb);

        // The 16-bit source is 126 MB on the way in, but only the converted copy is kept.
        Assert.Equal(5794L * 3637 * 4, info.DisplayBytes);
    }
}
