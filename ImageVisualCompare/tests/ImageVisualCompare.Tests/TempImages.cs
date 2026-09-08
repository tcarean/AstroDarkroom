using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageVisualCompare.Tests;

/// <summary>
/// Writes throwaway image files in every format the loader claims to handle, so the imaging
/// tests exercise the real WIC codecs rather than a stand-in. Generating them beats checking
/// binary fixtures into the repository, and it keeps the formats under test visible in code.
/// </summary>
public sealed class TempImages : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ivc-tests-" + Guid.NewGuid().ToString("N"));

    public TempImages() => Directory.CreateDirectory(_directory);

    public string WritePng(string name, PixelFormat format, int width, int height) =>
        Write(name, format, width, height, new PngBitmapEncoder());

    public string WriteTiff(string name, PixelFormat format, int width, int height) =>
        Write(name, format, width, height, new TiffBitmapEncoder());

    /// <summary>A file with the right extension and nothing a codec can make sense of.</summary>
    public string WriteCorrupt(string name)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02]);
        return path;
    }

    public string PathFor(string name) => Path.Combine(_directory, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }

    private string Write(string name, PixelFormat format, int width, int height, BitmapEncoder encoder)
    {
        int stride = (width * format.BitsPerPixel + 7) / 8;
        byte[] pixels = new byte[stride * height];
        new Random(name.GetHashCode(StringComparison.Ordinal)).NextBytes(pixels);

        BitmapPalette? palette = format == PixelFormats.Indexed8 ? BitmapPalettes.Halftone256 : null;
        BitmapSource source = BitmapSource.Create(width, height, 96, 96, format, palette, pixels, stride);

        string path = Path.Combine(_directory, name);
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }
}
