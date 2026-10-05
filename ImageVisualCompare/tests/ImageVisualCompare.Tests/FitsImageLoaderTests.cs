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

public class FitsImageLoaderTests : IDisposable
{
    private readonly TempImages _images = new();
    private readonly FitsImageLoader _loader = new();

    public void Dispose() => _images.Dispose();

    private static byte[] Pixels(BitmapSource bitmap)
    {
        byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return pixels;
    }

    [Fact]
    public void R014_LoadsAFloatFits_AsAFrozenBgra32BitmapOfTheSameSize()
    {
        string path = _images.WriteBytes("stack.fit", FitsFixture.Image(-32, 6, 4, 3, new double[6 * 4 * 3]));

        LoadedImage loaded = _loader.Load(path);

        Assert.Equal(PixelFormats.Bgra32, loaded.Bitmap.Format);
        Assert.True(loaded.Bitmap.IsFrozen, "the UI thread has to be able to use a bitmap decoded on a worker");
        Assert.Equal(6, loaded.Bitmap.PixelWidth);
        Assert.Equal(4, loaded.Bitmap.PixelHeight);
        Assert.Equal(6, loaded.Info.PixelWidth);
        Assert.Equal(4, loaded.Info.PixelHeight);
        Assert.Equal("32-bit float RGB", loaded.Info.FormatDescription);
    }

    [Fact]
    public void R014_LoadingLeavesTheFileUnlocked()
    {
        string path = _images.WriteBytes("unlocked.fit", FitsFixture.Image(8, 2, 2, 1, [1, 2, 3, 4]));

        _loader.Load(path);

        System.IO.File.Delete(path);
    }

    [Fact]
    public async Task R014_LoadAsync_ProducesTheSameResultOffTheCallingThread()
    {
        string path = _images.WriteBytes("async.fit", FitsFixture.Image(16, 3, 2, 1, new double[6]));

        LoadedImage loaded = await _loader.LoadAsync(path);

        Assert.True(loaded.Bitmap.IsFrozen);
        Assert.Equal(3, loaded.Info.PixelWidth);
    }

    [Fact]
    public void R015_TheSameSamplesAsSixteenBitFitsAndAsSixteenBitPng_GiveTheSameBytes()
    {
        const int Width = 64;
        const int Height = 48;
        var random = new Random(20261005);
        ushort[] interleaved = new ushort[Width * Height * 3];
        double[] planes = new double[interleaved.Length];
        for (int pixel = 0; pixel < Width * Height; pixel++)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                ushort value = (ushort)random.Next(65536);
                interleaved[(pixel * 3) + channel] = value;

                // Stored signed, as FITS stores unsigned 16-bit samples, under BZERO 32768.
                planes[(channel * Width * Height) + pixel] = value - 32768;
            }
        }

        string png = _images.WritePng("same.png", interleaved, Width, Height);
        string fits = _images.WriteBytes("same.fit", FitsFixture.Image(
            16, Width, Height, 3, planes, FitsFixture.Card("BZERO", 32768), FitsFixture.Text("ROWORDER", "TOP-DOWN")));

        byte[] fromPng = Pixels(new WicImageLoader().Load(png).Bitmap);
        byte[] fromFits = Pixels(_loader.Load(fits).Bitmap);

        Assert.Equal(fromPng, fromFits);
    }

    [Fact]
    public void R016_AFloatFitsCountingToSixtyFiveThousand_IsReadOnThatScaleAndSaysSo()
    {
        string path = _images.WriteBytes("adu.fit", FitsFixture.Image(-32, 3, 1, 1, [41200, 65535, 0]));

        LoadedImage loaded = _loader.Load(path);
        byte[] pixels = Pixels(loaded.Bitmap);

        Assert.Equal(160, pixels[0]);
        Assert.Equal(255, pixels[4]);
        Assert.Equal(0, pixels[8]);
        Assert.Equal("32-bit float gray (0..65535)", loaded.Info.FormatDescription);
    }

    [Fact]
    public void R016_AFloatFitsThatOnlyOvershootsOne_IsReadAsZeroToOne()
    {
        string path = _images.WriteBytes("siril.fit", FitsFixture.Image(-32, 3, 1, 1, [1.0005, 0.5, double.NaN]));

        LoadedImage loaded = _loader.Load(path);
        byte[] pixels = Pixels(loaded.Bitmap);

        Assert.Equal(255, pixels[0]);
        Assert.Equal(128, pixels[4]);
        Assert.Equal(0, pixels[8]);
        Assert.Equal("32-bit float gray", loaded.Info.FormatDescription);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("BOTTOM-UP", 1)]
    [InlineData("TOP-DOWN", 0)]
    public void R017_TheFirstRowOfTheFile_IsTheBottomRowUnlessTheFileSaysTopDown(string? rowOrder, int whiteRow)
    {
        // 2 x 2, the first sample of the file white and the other three black.
        string[] cards = rowOrder is null ? [] : [FitsFixture.Text("ROWORDER", rowOrder)];
        string path = _images.WriteBytes("rows.fit", FitsFixture.Image(-32, 2, 2, 1, [1, 0, 0, 0], cards));

        byte[] pixels = Pixels(_loader.Load(path).Bitmap);

        for (int row = 0; row < 2; row++)
        {
            Assert.Equal(row == whiteRow ? 255 : 0, pixels[row * 8]);
            Assert.Equal(0, pixels[(row * 8) + 4]);
        }
    }

    [Fact]
    public void R018_Probe_ReportsTheSizeTheDepthAndThePlanesOfTheLoad()
    {
        string path = _images.WriteBytes("probe.fit", FitsFixture.Image(-32, 8, 4, 3, new double[8 * 4 * 3]));

        ImageInfo probed = _loader.Probe(path);
        ImageInfo loaded = _loader.Load(path).Info;

        Assert.Equal(loaded, probed);
        Assert.Equal(32, probed.BitsPerChannel);
        Assert.Equal(ColorChannels.Rgb, probed.Channels);
        Assert.True(probed.IsFloat);
    }

    [Fact]
    public void R018_OnlyTheLoadedImageKnowsTheScale()
    {
        string path = _images.WriteBytes("adu.fit", FitsFixture.Image(-32, 2, 1, 1, [41200, 100]));

        ImageInfo probed = _loader.Probe(path);
        ImageInfo loaded = _loader.Load(path).Info;

        Assert.Null(probed.ValueRange);
        Assert.Equal("32-bit float gray", probed.FormatDescription);
        Assert.Equal(probed with { ValueRange = "0..65535" }, loaded);
    }

    [Fact]
    public void R014_Routing_AFitsUnderAnyNameGoesToTheFitsReader_AndAPngNamedFitToWic()
    {
        var routing = new RoutingImageLoader();
        string fitsNamedPng = _images.WriteBytes("stack.png", FitsFixture.Image(-32, 4, 2, 1, new double[8]));
        string pngNamedFit = _images.WritePng("export.fit", PixelFormats.Rgb48, 4, 2);

        Assert.Equal("32-bit float gray", routing.Probe(fitsNamedPng).FormatDescription);
        Assert.Equal("16-bit RGB", routing.Probe(pngNamedFit).FormatDescription);
    }

    [Fact]
    public async Task R014_AFitsAndAPng_LoadAsOnePair()
    {
        string fits = _images.WriteBytes("before.fit", FitsFixture.Image(-32, 64, 48, 3, new double[64 * 48 * 3]));
        string png = _images.WritePng("after.png", PixelFormats.Rgb48, 64, 48);

        ImagePairLoadResult result = await new ImagePairLoader(new RoutingImageLoader()).LoadAsync(fits, png);

        Assert.True(result.IsSuccess);
        Assert.False(result.Pair!.Pad.RequiresPadding);
        Assert.Equal("32-bit float RGB", result.Pair.Before.Info.FormatDescription);
        Assert.Equal("16-bit RGB", result.Pair.After.Info.FormatDescription);
        Assert.Equal(PixelFormats.Bgra32, result.Pair.Before.Bitmap.Format);
    }

    [Fact]
    public async Task R019_AFitsCutShort_IsAMessageNamingTheFile()
    {
        string png = _images.WritePng("before.png", PixelFormats.Bgr24, 100, 100);
        string cut = _images.WriteBytes("cut.fit", FitsFixture.Image(-32, 100, 100, 3, new double[30000])[..10000]);

        ImagePairLoadResult result = await new ImagePairLoader(new RoutingImageLoader()).LoadAsync(png, cut);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Could not read cut.fit." + Environment.NewLine + "FITS: the file ends before the image data does.",
            result.Error);
    }

    [Fact]
    public async Task R019_AFitsWithNoImage_IsAMessageNamingTheFile()
    {
        string png = _images.WritePng("after.png", PixelFormats.Bgr24, 8, 8);
        string table = _images.WriteBytes("catalogue.fits", FitsFixture.Header(
            [FitsFixture.Card("SIMPLE", "T"), FitsFixture.Card("BITPIX", 8), FitsFixture.Card("NAXIS", 0)]));

        ImagePairLoadResult result = await new ImagePairLoader(new RoutingImageLoader()).LoadAsync(table, png);

        Assert.Equal(
            "Could not read catalogue.fits." + Environment.NewLine + "FITS: no image found in the file.",
            result.Error);
    }
}
