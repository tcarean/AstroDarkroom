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

    [Fact]
    public void Loads16BitRgbPng_TheFormatSirilProduces()
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
    public void ReportsWhateverFormatTheFileTurnsOutToHave(string name, int bits, ColorChannels channels)
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
    public void EveryFormatIsNormalisedToFrozenBgra32()
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
    public void ReadsTiffToo()
    {
        string path = _images.WriteTiff("stack.tiff", PixelFormats.Rgb48, 40, 30);

        LoadedImage loaded = _loader.Load(path);

        Assert.Equal("16-bit RGB", loaded.Info.FormatDescription);
        Assert.Equal(40, loaded.Info.PixelWidth);
    }

    [Fact]
    public void Probe_ReportsTheSameThingAsAFullLoad()
    {
        string path = _images.WritePng("probe.png", PixelFormats.Rgb48, 128, 64);

        ImageInfo probed = _loader.Probe(path);
        ImageInfo loaded = _loader.Load(path).Info;

        Assert.Equal(loaded, probed);
    }

    [Fact]
    public void LoadingLeavesTheFileUnlocked()
    {
        string path = _images.WritePng("unlocked.png", PixelFormats.Bgr24, 32, 32);

        _loader.Load(path);

        // The stream is closed before Load returns, so nothing stops the user replacing the
        // file and opening it again.
        System.IO.File.Delete(path);
    }

    [Fact]
    public async Task LoadAsync_ProducesTheSameResultOffTheCallingThread()
    {
        string path = _images.WritePng("async.png", PixelFormats.Rgb48, 32, 32);

        LoadedImage loaded = await _loader.LoadAsync(path);

        Assert.True(loaded.Bitmap.IsFrozen);
        Assert.Equal(32, loaded.Info.PixelWidth);
    }
}
