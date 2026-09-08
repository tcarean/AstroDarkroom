using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf.Imaging;

/// <summary>An image that has been decoded and normalised, ready to hand to the UI.</summary>
/// <param name="Info">What the file actually turned out to be, for the status bar.</param>
/// <param name="Bitmap">Frozen Bgra32 pixels, safe to use from any thread.</param>
public sealed record LoadedImage(ImageInfo Info, BitmapSource Bitmap);

/// <summary>
/// The seam between the application and whatever decodes image files.
///
/// It is typed on <see cref="BitmapSource"/> because that is what the WPF <c>Image</c>
/// control consumes: any replacement decoder — ImageSharp, for instance — would still have
/// to produce one, so hiding that behind a generic parameter would buy nothing real. This
/// interface lives in the UI assembly for the same reason, while everything genuinely
/// framework-independent stays in ImageVisualCompare.Core.
/// </summary>
public interface IImageLoader
{
    /// <summary>
    /// Reads only the header. Used to check that a pair matches before committing to the
    /// cost of decoding either one.
    /// </summary>
    ImageInfo Probe(string path);

    /// <summary>Decodes the full image on a background thread.</summary>
    Task<LoadedImage> LoadAsync(string path, CancellationToken cancellationToken = default);
}
