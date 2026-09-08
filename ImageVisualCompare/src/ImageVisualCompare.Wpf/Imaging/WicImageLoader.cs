using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf.Imaging;

/// <summary>
/// Decodes through the Windows Imaging Component — the same codec WPF uses internally,
/// which is why the result reaches the <c>Image</c> control without an intermediate copy.
///
/// Nothing downstream branches on bit depth or channel layout: WIC is asked for the file
/// exactly as it was written, the format is recorded for the status bar, and then everything
/// is converted to Bgra32. A 16-bit PNG from Siril, an 8-bit screenshot and a palettised
/// image all arrive at the same place.
/// </summary>
public sealed class WicImageLoader : IImageLoader
{
    public ImageInfo Probe(string path)
    {
        using FileStream stream = OpenRead(path);

        // DelayCreation reads the header and stops there, so this costs nothing even on a
        // 108 MB file.
        BitmapFrame frame = CreateDecoder(stream, BitmapCreateOptions.DelayCreation).Frames[0];
        return Describe(path, frame);
    }

    public Task<LoadedImage> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(path), cancellationToken);

    public LoadedImage Load(string path)
    {
        using FileStream stream = OpenRead(path);

        // PreservePixelFormat stops WIC from quietly converting on the way in, so the format
        // reported to the user is the one the file really has.
        BitmapFrame frame = CreateDecoder(stream, BitmapCreateOptions.PreservePixelFormat).Frames[0];
        ImageInfo info = Describe(path, frame);

        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

        // The conversion above is lazy. CachedBitmap with OnLoad realises the pixels now,
        // while the file stream is still open, and Freeze makes the result usable from the UI
        // thread even though the decode happened on a worker.
        var bitmap = new CachedBitmap(converted, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        bitmap.Freeze();

        return new LoadedImage(info, bitmap);
    }

    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1 << 16, FileOptions.SequentialScan);

    private static BitmapDecoder CreateDecoder(Stream stream, BitmapCreateOptions options) =>
        BitmapDecoder.Create(stream, options, BitmapCacheOption.None);

    private static ImageInfo Describe(string path, BitmapSource frame)
    {
        (int bitsPerChannel, ColorChannels channels) = Classify(frame.Format);
        return new ImageInfo(path, frame.PixelWidth, frame.PixelHeight, bitsPerChannel, channels);
    }

    /// <summary>
    /// Translates a WIC pixel format into something worth showing a person. Unrecognised
    /// formats fall through to the raw bits-per-pixel rather than being rejected — the
    /// conversion to Bgra32 handles them regardless.
    /// </summary>
    private static (int BitsPerChannel, ColorChannels Channels) Classify(PixelFormat format)
    {
        if (format == PixelFormats.BlackWhite) return (1, ColorChannels.Gray);
        if (format == PixelFormats.Gray2) return (2, ColorChannels.Gray);
        if (format == PixelFormats.Gray4) return (4, ColorChannels.Gray);
        if (format == PixelFormats.Gray8) return (8, ColorChannels.Gray);
        if (format == PixelFormats.Gray16) return (16, ColorChannels.Gray);
        if (format == PixelFormats.Gray32Float) return (32, ColorChannels.Gray);

        if (format == PixelFormats.Indexed1) return (1, ColorChannels.Indexed);
        if (format == PixelFormats.Indexed2) return (2, ColorChannels.Indexed);
        if (format == PixelFormats.Indexed4) return (4, ColorChannels.Indexed);
        if (format == PixelFormats.Indexed8) return (8, ColorChannels.Indexed);

        if (format == PixelFormats.Bgr24 || format == PixelFormats.Rgb24) return (8, ColorChannels.Rgb);
        if (format == PixelFormats.Bgr32) return (8, ColorChannels.Rgb);
        if (format == PixelFormats.Bgr555 || format == PixelFormats.Bgr565) return (5, ColorChannels.Rgb);
        if (format == PixelFormats.Rgb48) return (16, ColorChannels.Rgb);
        if (format == PixelFormats.Bgr101010) return (10, ColorChannels.Rgb);
        if (format == PixelFormats.Rgb128Float) return (32, ColorChannels.Rgb);

        if (format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32) return (8, ColorChannels.Rgba);
        if (format == PixelFormats.Rgba64 || format == PixelFormats.Prgba64) return (16, ColorChannels.Rgba);
        if (format == PixelFormats.Rgba128Float || format == PixelFormats.Prgba128Float) return (32, ColorChannels.Rgba);

        return (format.BitsPerPixel, ColorChannels.Unknown);
    }
}
