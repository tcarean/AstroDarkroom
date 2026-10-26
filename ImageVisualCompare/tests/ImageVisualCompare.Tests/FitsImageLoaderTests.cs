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
        string path = _images.WriteBytes("async.fit", FitsFixture.Image(16, 3, 2, 1, [0, 5000, 10000, 15000, 20000, 32767]));

        LoadedImage loaded = await _loader.LoadAsync(path);
        LoadedImage direct = _loader.Load(path);

        Assert.True(loaded.Bitmap.IsFrozen);
        Assert.Equal(direct.Info, loaded.Info);
        Assert.Equal(Pixels(direct.Bitmap), Pixels(loaded.Bitmap));
    }

    [Fact]
    public void R014_LoadsDoubles()
    {
        string path = _images.WriteBytes("double.fit", FitsFixture.Image(-64, 2, 1, 3, [1, 0, 0.5, 0.5, 0, 1]));

        LoadedImage loaded = _loader.Load(path);

        Assert.Equal([0, 128, 255, 255, 255, 128, 0, 255], Pixels(loaded.Bitmap));
        Assert.Equal("64-bit float RGB", loaded.Info.FormatDescription);
    }

    [Fact]
    public void R014_Load_StopsWhenCancelled()
    {
        string path = _images.WriteBytes("cancel.fit", FitsFixture.Image(8, 4, 4, 1, new double[16]));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(() => _loader.Load(path, cancelled.Token));
    }

    [Fact]
    public async Task R014_Routing_LooksAtTheFileOffTheCallingThread()
    {
        // The task is handed back before the file is opened: a file that is not there faults
        // the task, it does not throw at the caller, who may be the UI thread.
        Task<LoadedImage> loading = new RoutingImageLoader().LoadAsync(_images.PathFor("not-here.fit"));

        await Assert.ThrowsAsync<System.IO.FileNotFoundException>(() => loading);
    }

    [Fact]
    public void R016_OneHotPixelAboveTwo_DoesNotTurnAZeroToOneImageBlack()
    {
        // 20 x 10 at 0.5, one sample at 2.5: 1 in 200, under the 1 in 100 that changes the scale.
        // The first sample of the file is the bottom-left pixel of the image.
        double[] samples = Enumerable.Repeat(0.5, 200).ToArray();
        samples[0] = 2.5;
        string path = _images.WriteBytes("hot.fit", FitsFixture.Image(-32, 20, 10, 1, samples));

        LoadedImage loaded = _loader.Load(path);
        byte[] pixels = Pixels(loaded.Bitmap);

        Assert.Equal(255, pixels[9 * 20 * 4]);
        Assert.Equal(128, pixels[0]);
        Assert.Equal(128, pixels[^4]);
        Assert.Equal("32-bit float gray", loaded.Info.FormatDescription);
    }

    [Fact]
    public void R016_TheSecondPass_RewritesEveryRowOfEveryPlane_TheRightWayUp()
    {
        // 2 x 2, three planes, bottom-up, counting to 65535: red 65535 in the first row of the
        // file only, green 32768 everywhere, blue 256 in the second row of the file only.
        double[] samples = [65535, 65535, 0, 0, /* green */ 32768, 32768, 32768, 32768, /* blue */ 0, 0, 256, 256];
        string path = _images.WriteBytes("adu-rgb.fit", FitsFixture.Image(-32, 2, 2, 3, samples));

        LoadedImage loaded = _loader.Load(path);

        byte[] top = [1, 128, 0, 255];
        byte[] bottom = [0, 128, 255, 255];
        Assert.Equal([.. top, .. top, .. bottom, .. bottom], Pixels(loaded.Bitmap));
        Assert.Equal("32-bit float RGB (0..65535)", loaded.Info.FormatDescription);
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
            // The PNG's rows run top-down; a FITS's run bottom-up, so the same pixel sits in
            // the mirrored row of its plane.
            int fitsPixel = ((Height - 1 - (pixel / Width)) * Width) + (pixel % Width);
            for (int channel = 0; channel < 3; channel++)
            {
                ushort value = (ushort)random.Next(65536);
                interleaved[(pixel * 3) + channel] = value;

                // Stored signed, as FITS stores unsigned 16-bit samples, under BZERO 32768.
                planes[(channel * Width * Height) + fitsPixel] = value - 32768;
            }
        }

        string png = _images.WritePng("same.png", interleaved, Width, Height);
        string fits = _images.WriteBytes("same.fit", FitsFixture.Image(
            16, Width, Height, 3, planes, FitsFixture.Card("BZERO", 32768)));

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
    [InlineData(null)]
    [InlineData("BOTTOM-UP")]
    [InlineData("TOP-DOWN")]
    public void R017_TheFirstRowOfTheFile_IsTheBottomRowWhateverRowOrderSays(string? rowOrder)
    {
        // 2 x 2, the first sample of the file white and the other three black: white at the
        // bottom left, as Siril shows the file whatever its ROWORDER says (ADR 0039).
        string[] cards = rowOrder is null ? [] : [FitsFixture.Text("ROWORDER", rowOrder)];
        string path = _images.WriteBytes("rows.fit", FitsFixture.Image(-32, 2, 2, 1, [1, 0, 0, 0], cards));

        byte[] pixels = Pixels(_loader.Load(path).Bitmap);

        Assert.Equal(0, pixels[0]);
        Assert.Equal(0, pixels[4]);
        Assert.Equal(255, pixels[8]);
        Assert.Equal(0, pixels[12]);
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
