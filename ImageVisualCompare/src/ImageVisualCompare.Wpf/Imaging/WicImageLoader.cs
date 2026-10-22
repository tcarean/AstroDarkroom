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

using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
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
/// image all arrive at the same place. The one exception is floating-point samples, as in the
/// 32-bit TIFF exports of Siril and GraXpert: WIC's conversion to levels takes them for linear
/// light and brightens them, so they are shown as stored, as a FITS of the same samples is
/// (ADR 0035).
///
/// WIC fails some files with exception types of its own, which <see cref="ImagePairLoader"/>
/// does not take for a failure of the file: <see cref="OverflowException"/> when it sets aside
/// the pixels of an image too large for it, and <see cref="COMException"/> when a codec rejects
/// a damaged file, as Windows' WebP, HEIC and AVIF codecs do, at the probe or at the decode.
/// This loader turns them into a <see cref="NotSupportedException"/> and a
/// <see cref="FileFormatException"/>, which become a message (ADR 0031).
/// </summary>
public sealed class WicImageLoader : IImageLoader
{
    /// <inheritdoc/>
    public ImageInfo Probe(string path)
    {
        using FileStream stream = OpenRead(path);

        try
        {
            // DelayCreation reads the header and stops there, so this costs nothing even on a
            // 108 MB file.
            BitmapFrame frame = CreateDecoder(stream, BitmapCreateOptions.DelayCreation).Frames[0];
            return Describe(path, frame);
        }
        catch (COMException ex)
        {
            throw Rejected(ex);
        }
    }

    /// <inheritdoc/>
    public Task<LoadedImage> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(path, cancellationToken), cancellationToken);

    /// <summary>
    /// Decodes the full image on the calling thread: the file's own format first, reported as
    /// it is, then converted to Bgra32 and frozen, float samples as stored (see the class
    /// summary). The file is closed again before this returns. A file that cannot be read or
    /// decoded throws an exception of a type <see cref="ImagePairLoader"/> turns into a message,
    /// WIC's own failures included.
    /// </summary>
    /// <param name="path">The file; what it holds, not its name, says what it is.</param>
    /// <param name="cancellationToken">
    /// Looked at before each band of rows of float samples; a stopped load throws
    /// <see cref="OperationCanceledException"/>. Any other format WIC converts in one call that
    /// does not look at it.
    /// </param>
    /// <returns>The frozen Bgra32 pixels, and what the file was.</returns>
    public LoadedImage Load(string path, CancellationToken cancellationToken = default)
    {
        using FileStream stream = OpenRead(path);

        try
        {
            // PreservePixelFormat stops WIC from quietly converting on the way in, so the format
            // reported to the user is the one the file really has.
            BitmapFrame frame = CreateDecoder(stream, BitmapCreateOptions.PreservePixelFormat).Frames[0];
            ImageInfo info = Describe(path, frame);

            if (info.IsFloat)
            {
                return LoadFloat(frame, info, cancellationToken);
            }

            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

            // The conversion above is lazy. Realise makes the pixels now, while the file stream
            // is still open, and Freeze makes the result usable from the UI thread even though
            // the decode happened on a worker.
            CachedBitmap bitmap = Realise(converted, info);
            bitmap.Freeze();

            return new LoadedImage(info, bitmap);
        }
        catch (COMException ex)
        {
            throw Rejected(ex);
        }
    }

    /// <summary>
    /// Decodes and converts the pixels into memory, with <see cref="BitmapCacheOption.OnLoad"/>.
    /// An image too large for WIC fails here with an <see cref="OverflowException"/> (a PNG
    /// whose header says 40000 x 30000 does, measured on 2026-10-22), which is said as a
    /// message instead.
    /// </summary>
    /// <param name="converted">The frame, converted to Bgra32 lazily.</param>
    /// <param name="info">What the file is; its size goes into the message.</param>
    /// <returns>The pixels in memory, not yet frozen.</returns>
    /// <exception cref="NotSupportedException">The image is too large for WIC to hold.</exception>
    private static CachedBitmap Realise(BitmapSource converted, ImageInfo info)
    {
        try
        {
            return new CachedBitmap(converted, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch (OverflowException ex)
        {
            throw TooLarge(info, ex);
        }
    }

    /// <summary>
    /// Decodes floating-point samples as a FITS of the same samples is shown: as stored, through
    /// <see cref="FloatSamples"/>, read as 0 to 1, and once more as 0 to 65535 when
    /// <see cref="FitsScale.IsRescaled"/> finds the image counts so (ADR 0035).
    /// </summary>
    /// <param name="frame">The decoded frame, in its own float format.</param>
    /// <param name="info">
    /// What the file is, float, gray, RGB or RGBA; the range is added here.
    /// </param>
    /// <param name="cancellationToken">
    /// Looked at before each band of rows; a stopped load throws
    /// <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>
    /// The frozen Bgra32 pixels, and what the file was, with the range its values were read in.
    /// </returns>
    private static LoadedImage LoadFloat(BitmapFrame frame, ImageInfo info, CancellationToken cancellationToken)
    {
        bool gray = info.Channels == ColorChannels.Gray;

        // Between float formats WIC copies the values; only its conversion to levels takes them
        // for light. Colour comes four floats a pixel, the fourth not read.
        PixelFormat target = gray ? PixelFormats.Gray32Float
            : info.Channels == ColorChannels.Rgba ? PixelFormats.Rgba128Float
            : PixelFormats.Rgb128Float;
        var floats = new FormatConvertedBitmap(frame, target, null, 0);

        WriteableBitmap bitmap = CreateBitmap(info);
        long above = Decode(floats, gray, bitmap, FitsScale.FullScale(-32), cancellationToken);

        // As for a float FITS: whether the samples count in 0 to 1 or in 0 to 65535 is known only
        // once all of them have been seen, so the rare image in 0 to 65535 is decoded twice.
        string? valueRange = null;
        long samples = (long)info.PixelWidth * info.PixelHeight * (gray ? 1 : 3);
        if (FitsScale.IsRescaled(-32, above, samples))
        {
            Decode(floats, gray, bitmap, FitsScale.FullScale(-32, rescaled: true), cancellationToken);
            valueRange = FitsScale.SixteenBitRange;
        }

        bitmap.Freeze();
        return new LoadedImage(info with { ValueRange = valueRange }, bitmap);
    }

    /// <summary>
    /// Creates the bitmap rows of float samples are written into; an image too large for one is
    /// said as the too-large message, as <see cref="Realise"/> says it.
    /// </summary>
    /// <param name="info">The image whose size the bitmap takes.</param>
    /// <returns>An empty Bgra32 bitmap of the image's size, at 96 dpi.</returns>
    /// <exception cref="NotSupportedException">The image is too large for a bitmap.</exception>
    private static WriteableBitmap CreateBitmap(ImageInfo info)
    {
        try
        {
            return new WriteableBitmap(info.PixelWidth, info.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentException)
        {
            throw TooLarge(info, ex);
        }
    }

    /// <summary>
    /// Writes every row of float samples into the bitmap, a band of rows at a time, as
    /// <see cref="FloatSamples.ToBgra32"/> converts them.
    /// </summary>
    /// <param name="floats">
    /// The frame converted to a float format: one float a pixel when <paramref name="gray"/>,
    /// otherwise four.
    /// </param>
    /// <param name="gray">True for one float a pixel.</param>
    /// <param name="bitmap">Receives the rows: the image's size, Bgra32, not yet frozen.</param>
    /// <param name="fullScale">
    /// The value shown as white (<see cref="FitsScale.FullScale"/>).
    /// </param>
    /// <param name="cancellationToken">
    /// Looked at before each band; a stopped decode throws
    /// <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>How many samples count towards the rescale.</returns>
    private static long Decode(
        BitmapSource floats, bool gray, WriteableBitmap bitmap, double fullScale, CancellationToken cancellationToken)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int floatsPerRow = width * (gray ? 1 : 4);

        // A band at a time keeps the floats held to a few megabytes; the whole of a 6016 x 4016
        // RGB image as floats would be 386 MB.
        int bandRows = Math.Max(1, BandFloats / floatsPerRow);
        float[] band = new float[bandRows * floatsPerRow];
        byte[] row = new byte[width * 4];
        long above = 0;

        for (int top = 0; top < height; top += bandRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int rows = Math.Min(bandRows, height - top);
            floats.CopyPixels(new Int32Rect(0, top, width, rows), band, floatsPerRow * sizeof(float), 0);
            for (int y = 0; y < rows; y++)
            {
                above += FloatSamples.ToBgra32(band.AsSpan(y * floatsPerRow, floatsPerRow), gray, row, fullScale);
                bitmap.WritePixels(new Int32Rect(0, top + y, width, 1), row, width * 4, 0);
            }
        }

        return above;
    }

    /// <summary>The floats a band of rows holds at most, 8 MB of them.</summary>
    private const int BandFloats = 2 * 1024 * 1024;

    /// <summary>Makes the refusal for an image too large for WIC or WPF to hold.</summary>
    /// <param name="info">What the file is; its size is named in the message.</param>
    /// <param name="ex">The exception WIC or WPF threw.</param>
    /// <returns>The exception to throw; its message is what the user reads.</returns>
    private static NotSupportedException TooLarge(ImageInfo info, Exception ex) =>
        new($"An image of {info.Dimensions} is too large to show.", ex);

    /// <summary>
    /// Makes the refusal for a file a codec rejected with an error of its own, in a type the pair
    /// loader turns into a message.
    /// </summary>
    /// <param name="ex">
    /// The codec's exception; its message says what is wrong with the file.
    /// </param>
    /// <returns>
    /// The exception to throw, with the codec's message and the codec's exception inside.
    /// </returns>
    private static FileFormatException Rejected(COMException ex) => new(ex.Message, ex);

    /// <summary>
    /// Opens a file for one read from start to end, sharing it with other readers.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The open stream, with a 64 KB buffer.</returns>
    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1 << 16, FileOptions.SequentialScan);

    /// <summary>Asks WIC for a decoder of whatever format the stream holds.</summary>
    /// <param name="stream">The open file, at its start.</param>
    /// <param name="options">
    /// <see cref="BitmapCreateOptions.DelayCreation"/> to read the header only,
    /// <see cref="BitmapCreateOptions.PreservePixelFormat"/> to decode the pixels as stored.
    /// </param>
    /// <returns>The decoder, caching nothing; its first frame is the image.</returns>
    private static BitmapDecoder CreateDecoder(Stream stream, BitmapCreateOptions options) =>
        BitmapDecoder.Create(stream, options, BitmapCacheOption.None);

    /// <summary>Says what a frame is, for the status bar.</summary>
    /// <param name="path">The file the frame came from.</param>
    /// <param name="frame">
    /// The first frame, probed or decoded, in the file's own pixel format.
    /// </param>
    /// <returns>
    /// The frame's size, and its depth and channels: for float samples as
    /// <see cref="FloatSamplesOf"/> reads them, with <see cref="ImageInfo.IsFloat"/>, and for any
    /// other format as <see cref="Classify"/> does.
    /// </returns>
    private static ImageInfo Describe(string path, BitmapFrame frame)
    {
        if (FloatSamplesOf(frame) is { } floats)
        {
            return new ImageInfo(
                path, frame.PixelWidth, frame.PixelHeight, floats.BitsPerChannel, floats.Channels, IsFloat: true);
        }

        (int bitsPerChannel, ColorChannels channels) = Classify(frame.Format);
        return new ImageInfo(path, frame.PixelWidth, frame.PixelHeight, bitsPerChannel, channels);
    }

    /// <summary>
    /// Tells whether a frame's samples are floating point: by the pixel format when WPF names a
    /// float one, and otherwise, for a TIFF, by its own tags, SampleFormat 3 (IEEE float) with
    /// SamplesPerPixel and BitsPerSample, as in the 96 bpp RGB that Siril and GraXpert write and
    /// WPF has no name for.
    /// </summary>
    /// <param name="frame">
    /// The first frame, probed or decoded, in the file's own pixel format.
    /// </param>
    /// <returns>
    /// The bits of one sample and the channels, gray, RGB or RGBA; null for integer samples,
    /// and for float samples in any other layout, which WIC then converts like any format.
    /// </returns>
    private static (int BitsPerChannel, ColorChannels Channels)? FloatSamplesOf(BitmapFrame frame)
    {
        PixelFormat format = frame.Format;
        if (format == PixelFormats.Gray32Float) return (32, ColorChannels.Gray);
        if (format == PixelFormats.Rgb128Float) return (32, ColorChannels.Rgb);
        if (format == PixelFormats.Rgba128Float || format == PixelFormats.Prgba128Float) return (32, ColorChannels.Rgba);

        // Only a TIFF has these tags to ask; another format's metadata has no TIFF directory.
        if (frame.Decoder is not TiffBitmapDecoder
            || frame.Metadata is not BitmapMetadata tags
            || FirstNumber(tags.GetQuery("/ifd/{ushort=339}")) != 3)
        {
            return null;
        }

        ColorChannels? channels = FirstNumber(tags.GetQuery("/ifd/{ushort=277}")) switch
        {
            1 => ColorChannels.Gray,
            3 => ColorChannels.Rgb,
            4 => ColorChannels.Rgba,
            _ => null,
        };
        return channels is { } known ? (FirstNumber(tags.GetQuery("/ifd/{ushort=258}")) ?? 32, known) : null;
    }

    /// <summary>Reads the first number of a TIFF tag, as WIC's metadata query returns it.</summary>
    /// <param name="value">
    /// What the query returned: one number, an array of them, or null for a tag the file lacks.
    /// </param>
    /// <returns>The first number; null when there is none.</returns>
    private static int? FirstNumber(object? value) => value switch
    {
        ushort one => one,
        ushort[] { Length: > 0 } several => several[0],
        uint one => (int)one,
        uint[] { Length: > 0 } several => (int)several[0],
        _ => null,
    };

    /// <summary>
    /// Translates a WIC pixel format of integer samples into something worth showing a person;
    /// float samples are <see cref="FloatSamplesOf"/>'s. Unrecognised formats fall through to
    /// the raw bits-per-pixel rather than being rejected — the conversion to Bgra32 handles
    /// them regardless.
    /// </summary>
    /// <param name="format">The frame's pixel format, as the file stores it.</param>
    /// <returns>
    /// The bits of one channel and the channel layout; for a format not listed, the bits of a
    /// whole pixel and <see cref="ColorChannels.Unknown"/>.
    /// </returns>
    private static (int BitsPerChannel, ColorChannels Channels) Classify(PixelFormat format)
    {
        if (format == PixelFormats.BlackWhite) return (1, ColorChannels.Gray);
        if (format == PixelFormats.Gray2) return (2, ColorChannels.Gray);
        if (format == PixelFormats.Gray4) return (4, ColorChannels.Gray);
        if (format == PixelFormats.Gray8) return (8, ColorChannels.Gray);
        if (format == PixelFormats.Gray16) return (16, ColorChannels.Gray);

        if (format == PixelFormats.Indexed1) return (1, ColorChannels.Indexed);
        if (format == PixelFormats.Indexed2) return (2, ColorChannels.Indexed);
        if (format == PixelFormats.Indexed4) return (4, ColorChannels.Indexed);
        if (format == PixelFormats.Indexed8) return (8, ColorChannels.Indexed);

        if (format == PixelFormats.Bgr24 || format == PixelFormats.Rgb24) return (8, ColorChannels.Rgb);
        if (format == PixelFormats.Bgr32) return (8, ColorChannels.Rgb);
        if (format == PixelFormats.Bgr555 || format == PixelFormats.Bgr565) return (5, ColorChannels.Rgb);
        if (format == PixelFormats.Rgb48) return (16, ColorChannels.Rgb);
        if (format == PixelFormats.Bgr101010) return (10, ColorChannels.Rgb);

        if (format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32) return (8, ColorChannels.Rgba);
        if (format == PixelFormats.Rgba64 || format == PixelFormats.Prgba64) return (16, ColorChannels.Rgba);

        return (format.BitsPerPixel, ColorChannels.Unknown);
    }
}
