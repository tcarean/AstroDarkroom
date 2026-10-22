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

using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf.Imaging;

namespace ImageVisualCompare.Tests;

public class WicImageLoaderTests : IDisposable
{
    private readonly TempImages _images = new();
    private readonly WicImageLoader _loader = new();

    public void Dispose() => _images.Dispose();

    [Theory]
    [InlineData(1, "32-bit float gray")]
    [InlineData(3, "32-bit float RGB")]
    [InlineData(4, "32-bit float RGBA")]
    public async Task R090_AFloatTiff_IsReportedAsFloat(int channels, string expected)
    {
        string path = _images.WriteFloatTiff(
            $"float{channels}.tif", 8, 4, channels, Enumerable.Repeat(0.5f, 8 * 4 * channels).ToArray());

        ImageInfo probed = _loader.Probe(path);
        LoadedImage loaded = await _loader.LoadAsync(path);

        Assert.Equal(expected, probed.FormatDescription);
        Assert.Equal(expected, loaded.Info.FormatDescription);
        Assert.True(loaded.Info.IsFloat);
    }

    [Fact]
    public async Task R091_AFloatTiff_IsShownAsStored_NotAsWicConvertsIt()
    {
        // Two RGB pixels. 0.5 is 32768 of 65535 and shows as 128, as the same value in a FITS
        // does; WIC's own conversion takes float samples for linear light and gave 188.
        float[] samples = [0.5f, 0.25f, 1.0f, float.NaN, -1f, 2f];
        string path = _images.WriteFloatTiff("float.tif", 2, 1, 3, samples);

        LoadedImage loaded = await _loader.LoadAsync(path);
        byte[] bgra = new byte[8];
        loaded.Bitmap.CopyPixels(bgra, 8, 0);

        Assert.Equal(new byte[] { 255, 64, 128, 255, 255, 0, 0, 255 }, bgra);
        Assert.Equal("32-bit float RGB", loaded.Info.FormatDescription);
    }

    [Fact]
    public async Task R091_AFloatTiffInZeroTo65535_IsReadSo()
    {
        // Every sample is above 2, so the file counts to 65535 (R-016), and 41200, which is
        // 0.6287 of 65535, shows as 160.
        string path = _images.WriteFloatTiff("float65535.tif", 4, 2, 3, Enumerable.Repeat(41200f, 4 * 2 * 3).ToArray());

        LoadedImage loaded = await _loader.LoadAsync(path);
        byte[] bgra = new byte[4];
        loaded.Bitmap.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), bgra, 4, 0);

        Assert.Equal(new byte[] { 160, 160, 160, 255 }, bgra);
        Assert.Equal("32-bit float RGB (0..65535)", loaded.Info.FormatDescription);
    }

    [Fact]
    public void R010_Loads16BitRgbPng_TheFormatSirilProduces()
    {
        string path = _images.WritePng("rgb48.png", PixelFormats.Rgb48, 64, 48);

        LoadedImage loaded = _loader.Load(path);

        Assert.Equal(16, loaded.Info.BitsPerChannel);
        Assert.Equal(ColorChannels.Rgb, loaded.Info.Channels);
        Assert.Equal("16-bit RGB", loaded.Info.FormatDescription);
        Assert.Equal(64, loaded.Info.PixelWidth);
        Assert.Equal(48, loaded.Info.PixelHeight);
    }

    [Theory]
    [InlineData("rgb24.png", 8, ColorChannels.Rgb)]
    [InlineData("gray8.png", 8, ColorChannels.Gray)]
    [InlineData("gray16.png", 16, ColorChannels.Gray)]
    [InlineData("bgra32.png", 8, ColorChannels.Rgba)]
    [InlineData("indexed8.png", 8, ColorChannels.Indexed)]
    public void R011_ReportsWhateverFormatTheFileTurnsOutToHave(string name, int bits, ColorChannels channels)
    {
        PixelFormat format = name switch
        {
            "rgb24.png" => PixelFormats.Bgr24,
            "gray8.png" => PixelFormats.Gray8,
            "gray16.png" => PixelFormats.Gray16,
            "bgra32.png" => PixelFormats.Bgra32,
            _ => PixelFormats.Indexed8,
        };
        string path = _images.WritePng(name, format, 32, 32);

        ImageInfo info = _loader.Load(path).Info;

        Assert.Equal(bits, info.BitsPerChannel);
        Assert.Equal(channels, info.Channels);
    }

    [Fact]
    public void R010_EveryFormatIsNormalisedToFrozenBgra32()
    {
        string sixteenBit = _images.WritePng("a.png", PixelFormats.Rgb48, 32, 32);
        string eightBit = _images.WritePng("b.png", PixelFormats.Bgr24, 32, 32);

        foreach (string path in new[] { sixteenBit, eightBit })
        {
            BitmapSource bitmap = _loader.Load(path).Bitmap;

            Assert.Equal(PixelFormats.Bgra32, bitmap.Format);
            Assert.True(bitmap.IsFrozen, "the UI thread has to be able to use a bitmap decoded on a worker");
        }
    }

    [Fact]
    public void R010_ReadsTiffToo()
    {
        string path = _images.WriteTiff("stack.tiff", PixelFormats.Rgb48, 40, 30);

        LoadedImage loaded = _loader.Load(path);

        Assert.Equal("16-bit RGB", loaded.Info.FormatDescription);
        Assert.Equal(40, loaded.Info.PixelWidth);
    }

    [Fact]
    public void R011_Probe_ReportsTheSameThingAsAFullLoad()
    {
        string path = _images.WritePng("probe.png", PixelFormats.Rgb48, 128, 64);

        ImageInfo probed = _loader.Probe(path);
        ImageInfo loaded = _loader.Load(path).Info;

        Assert.Equal(loaded, probed);
    }

    [Fact]
    public void R010_LoadingLeavesTheFileUnlocked()
    {
        string path = _images.WritePng("unlocked.png", PixelFormats.Bgr24, 32, 32);

        _loader.Load(path);

        // The stream is closed before Load returns, so nothing stops the user replacing the
        // file and opening it again.
        System.IO.File.Delete(path);
    }

    [Fact]
    public async Task R010_LoadAsync_ProducesTheSameResultOffTheCallingThread()
    {
        string path = _images.WritePng("async.png", PixelFormats.Rgb48, 32, 32);

        LoadedImage loaded = await _loader.LoadAsync(path);

        Assert.True(loaded.Bitmap.IsFrozen);
        Assert.Equal(32, loaded.Info.PixelWidth);
    }
}
