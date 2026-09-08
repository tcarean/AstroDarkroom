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
    public async Task LoadsAMatchingPair()
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
    public async Task RejectsAMismatchedPairWithBothResolutionsInTheMessage()
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
    public async Task AMismatchedPairIsRejectedBeforeAnyPixelsAreDecoded()
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
    public async Task AMissingFileBecomesAMessageNamingIt()
    {
        string before = _images.WritePng("before.png", PixelFormats.Bgr24, 32, 32);
        string missing = _images.PathFor("not-here.png");

        ImagePairLoadResult result = await Real().LoadAsync(before, missing);

        Assert.False(result.IsSuccess);
        Assert.Contains("not-here.png", result.Error);
    }

    [Fact]
    public async Task ACorruptFileBecomesAMessageRatherThanACrash()
    {
        string before = _images.WritePng("before.png", PixelFormats.Bgr24, 32, 32);
        string corrupt = _images.WriteCorrupt("broken.png");

        ImagePairLoadResult result = await Real().LoadAsync(before, corrupt);

        Assert.False(result.IsSuccess);
        Assert.Contains("broken.png", result.Error);
    }

    [Fact]
    public async Task ReportsProgressAsEachImageStarts()
    {
        string before = _images.WritePng("before.png", PixelFormats.Bgr24, 32, 32);
        string after = _images.WritePng("after.png", PixelFormats.Bgr24, 32, 32);
        List<ImageLoadProgress> reported = [];

        await Real().LoadAsync(before, after, new Progress<ImageLoadProgress>(reported.Add));

        // Progress<T> marshals through the synchronisation context, so give the posted
        // callbacks a chance to run before asserting on them.
        await Task.Delay(50);
        Assert.Contains(reported, p => p.FileName == "before.png");
        Assert.Contains(reported, p => p.FileName == "after.png");
        Assert.Contains(reported, p => p.Fraction == 1 && p.FileName is null);
    }

    [Fact]
    public async Task ImagesAreDecodedOneAfterTheOtherSoThePeakStaysLow()
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
}
