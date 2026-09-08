namespace ImageVisualCompare.Core;

/// <summary>How the decoder saw the source file.</summary>
public enum ColorChannels
{
    Unknown,
    Gray,
    GrayAlpha,
    Rgb,
    Rgba,
    Indexed,
}

/// <summary>
/// What was actually loaded from disk. No logic branches on the bit depth or channel
/// layout — everything is normalised to Bgra32 for display — so this exists purely to
/// report the truth to the user in the status bar instead of leaving them to guess.
/// </summary>
public sealed record ImageInfo(
    string Path,
    int PixelWidth,
    int PixelHeight,
    int BitsPerChannel,
    ColorChannels Channels)
{
    public string FileName => System.IO.Path.GetFileName(Path);

    public string Dimensions => $"{PixelWidth} × {PixelHeight}";

    /// <summary>The source format in words, for example "16-bit RGB".</summary>
    public string FormatDescription => Channels switch
    {
        ColorChannels.Gray => $"{BitsPerChannel}-bit gray",
        ColorChannels.GrayAlpha => $"{BitsPerChannel}-bit gray + alpha",
        ColorChannels.Rgb => $"{BitsPerChannel}-bit RGB",
        ColorChannels.Rgba => $"{BitsPerChannel}-bit RGBA",
        ColorChannels.Indexed => $"{BitsPerChannel}-bit indexed",
        _ => $"{BitsPerChannel}-bit",
    };

    /// <summary>Bytes the image occupies once converted to Bgra32, for the memory budget.</summary>
    public long DisplayBytes => (long)PixelWidth * PixelHeight * 4;
}
