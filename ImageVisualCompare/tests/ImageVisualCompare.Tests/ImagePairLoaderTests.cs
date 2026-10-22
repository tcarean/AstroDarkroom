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

public class ImagePairLoaderTests : IDisposable
{
    private readonly TempImages _images = new();

    public void Dispose() => _images.Dispose();

    private static ImagePairLoader Real() => new(new WicImageLoader());

    [Fact]
    public async Task R007_LoadsAMatchingPair()
    {
        string before = _images.WritePng("before.png", PixelFormats.Rgb48, 64, 48);
        string after = _images.WritePng("after.png", PixelFormats.Bgr24, 64, 48);

        ImagePairLoadResult result = await Real().LoadAsync(before, after);

        Assert.True(result.IsSuccess);
        Assert.Equal(64, result.Pair!.PixelWidth);
        Assert.Equal(48, result.Pair.PixelHeight);
        Assert.Equal("16-bit RGB", result.Pair.Before.Info.FormatDescription);
        Assert.Equal("8-bit RGB", result.Pair.After.Info.FormatDescription);
    }

    [Fact]
    public async Task R003_WithNoOneToAskAMismatchedPairIsStillRefused()
    {
        string before = _images.WritePng("before.png", PixelFormats.Bgr24, 64, 48);
        string after = _images.WritePng("after.png", PixelFormats.Bgr24, 65, 48);

        ImagePairLoadResult result = await Real().LoadAsync(before, after);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("64 × 48", result.Error);
        Assert.Contains("65 × 48", result.Error);
    }

    [Fact]
    public async Task R003_AMismatchedPairIsRejectedBeforeAnyPixelsAreDecoded()
    {
        var loader = new RecordingLoader(
            new ImageInfo("a.png", 100, 100, 8, ColorChannels.Rgb),
            new ImageInfo("b.png", 200, 100, 8, ColorChannels.Rgb));

        ImagePairLoadResult result = await new ImagePairLoader(loader).LoadAsync("a.png", "b.png");

        Assert.False(result.IsSuccess);
        Assert.Equal(2, loader.ProbeCount);
        Assert.Equal(0, loader.LoadCount);
    }

    [Fact]
    public async Task R003_DecliningThePaddingCostsNothingAndIsNotAnError()
    {
        var loader = new RecordingLoader(
            new ImageInfo("a.png", 100, 100, 8, ColorChannels.Rgb),
            new ImageInfo("b.png", 200, 100, 8, ColorChannels.Rgb));

        ImagePairLoadResult result = await new ImagePairLoader(loader)
            .LoadAsync("a.png", "b.png", confirmPadding: _ => Task.FromResult(false));

        Assert.True(result.WasDeclined);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(0, loader.LoadCount);
    }

    [Fact]
    public async Task R004_AcceptingThePaddingGivesACanvasBigEnoughForBothImages()
    {
        var loader = new RecordingLoader(
            new ImageInfo("a.png", 100, 200, 8, ColorChannels.Rgb),
            new ImageInfo("b.png", 300, 50, 8, ColorChannels.Rgb));

        ImagePairLoadResult result = await new ImagePairLoader(loader)
            .LoadAsync("a.png", "b.png", confirmPadding: _ => Task.FromResult(true));

        Assert.True(result.IsSuccess);
        Assert.True(result.Pair!.Pad.RequiresPadding);
        Assert.Equal(300, result.Pair.PixelWidth);
        Assert.Equal(200, result.Pair.PixelHeight);

        // The images themselves are untouched: the padding is something the view draws, not
        // something anyone copied pixels for.
        Assert.Equal(100, result.Pair.Before.Info.PixelWidth);
        Assert.Equal(300, result.Pair.After.Info.PixelWidth);
    }

    [Fact]
    public async Task R003_TheQuestionCarriesBothFilesAndTheResultingCanvas()
    {
        var loader = new RecordingLoader(
            new ImageInfo(@"C:\astro\a.png", 100, 200, 8, ColorChannels.Rgb),
            new ImageInfo(@"C:\astro\b.png", 300, 50, 8, ColorChannels.Rgb));
        PadRequest? asked = null;

        await new ImagePairLoader(loader).LoadAsync("a.png", "b.png", confirmPadding: request =>
        {
            asked = request;
            return Task.FromResult(true);
        });

        Assert.NotNull(asked);
        Assert.Contains("a.png", asked!.Value.Message);
        Assert.Contains("b.png", asked.Value.Message);
        Assert.Contains("300 \u00d7 200", asked.Value.Message);
    }

    [Fact]
    public async Task R003_AMatchingPairIsNeverQuestioned()
    {
        var info = new ImageInfo("x.png", 10, 10, 8, ColorChannels.Rgb);
        var loader = new RecordingLoader(info, info);
        bool asked = false;

        ImagePairLoadResult result = await new ImagePairLoader(loader).LoadAsync(
            "a.png", "b.png", confirmPadding: _ =>
            {
                asked = true;
                return Task.FromResult(true);
            });

        Assert.True(result.IsSuccess);
        Assert.False(asked);
        Assert.False(result.Pair!.Pad.RequiresPadding);
    }

    [Fact]
    public async Task R005_AMissingFileBecomesAMessageNamingIt()
    {
        string before = _images.WritePng("before.png", PixelFormats.Bgr24, 32, 32);
        string missing = _images.PathFor("not-here.png");

        ImagePairLoadResult result = await Real().LoadAsync(before, missing);

        Assert.False(result.IsSuccess);
        Assert.Contains("not-here.png", result.Error);
    }

    [Fact]
    public async Task R005_ACorruptFileBecomesAMessageRatherThanACrash()
    {
        string before = _images.WritePng("before.png", PixelFormats.Bgr24, 32, 32);
        string corrupt = _images.WriteCorrupt("broken.png");

        ImagePairLoadResult result = await Real().LoadAsync(before, corrupt);

        Assert.False(result.IsSuccess);
        Assert.Contains("broken.png", result.Error);
    }

    [Fact]
    public async Task R005_AnImageTooLargeToDecode_IsAMessageNamingIt()
    {
        // It probes as 40000 x 30000, and four bytes a pixel overflow WIC's 32-bit buffer size
        // when the decode sets the pixels aside.
        string huge = _images.WritePngWithHeaderSize("huge.png", 40000, 30000);

        ImagePairLoadResult result = await Real().LoadAsync(huge, huge);

        Assert.Equal(
            "Could not read huge.png." + Environment.NewLine + "An image of 40000 × 30000 is too large to show.",
            result.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task R005_ADamagedWebP_IsAMessage_AtTheProbeOrAtTheDecode(bool lossless)
    {
        string broken = _images.WriteDamagedWebP("broken.webp", lossless);

        ImagePairLoadResult result = await Real().LoadAsync(broken, broken);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("Could not read broken.webp." + Environment.NewLine, result.Error);
    }

    [Fact]
    public async Task R006_ReportsProgressAsEachImageStarts()
    {
        string before = _images.WritePng("before.png", PixelFormats.Bgr24, 32, 32);
        string after = _images.WritePng("after.png", PixelFormats.Bgr24, 32, 32);
        var progress = new RecordingProgress();

        await Real().LoadAsync(before, after, progress);

        Assert.Contains(progress.Reported, p => p.FileName == "before.png");
        Assert.Contains(progress.Reported, p => p.FileName == "after.png");
        Assert.Contains(progress.Reported, p => p.Fraction == 1 && p.FileName is null);
    }

    [Fact]
    public async Task R006_ImagesAreDecodedOneAfterTheOtherSoThePeakStaysLow()
    {
        var info = new ImageInfo("x.png", 10, 10, 8, ColorChannels.Rgb);
        var loader = new RecordingLoader(info, info);

        await new ImagePairLoader(loader).LoadAsync("a.png", "b.png");

        Assert.Equal(1, loader.MaxConcurrentLoads);
    }

    /// <summary>A loader that answers from canned metadata and counts what was asked of it.</summary>
    private sealed class RecordingLoader(ImageInfo first, ImageInfo second) : IImageLoader
    {
        private int _concurrentLoads;

        public int ProbeCount { get; private set; }

        public int LoadCount { get; private set; }

        public int MaxConcurrentLoads { get; private set; }

        public ImageInfo Probe(string path)
        {
            ProbeCount++;
            return ProbeCount == 1 ? first : second;
        }

        public async Task<LoadedImage> LoadAsync(string path, CancellationToken cancellationToken = default)
        {
            int running = Interlocked.Increment(ref _concurrentLoads);
            MaxConcurrentLoads = Math.Max(MaxConcurrentLoads, running);
            LoadCount++;

            await Task.Delay(10, cancellationToken);

            Interlocked.Decrement(ref _concurrentLoads);
            ImageInfo info = LoadCount == 1 ? first : second;
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
            bitmap.Freeze();
            return new LoadedImage(info, bitmap);
        }
    }

    /// <summary>
    /// Records each report on the thread that makes it, before the load goes on. A
    /// <see cref="Progress{T}"/> posts its callback through the synchronisation context instead,
    /// so a test that asserts once the load has returned races the posted callbacks.
    /// </summary>
    private sealed class RecordingProgress : IProgress<ImageLoadProgress>
    {
        public List<ImageLoadProgress> Reported { get; } = [];

        public void Report(ImageLoadProgress value) => Reported.Add(value);
    }
}
