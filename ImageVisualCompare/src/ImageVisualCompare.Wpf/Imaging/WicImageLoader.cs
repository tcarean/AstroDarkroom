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
        Task.Run(() => Load(path), cancellationToken);

    /// <summary>
    /// Decodes the full image on the calling thread: the file's own format first, reported as
    /// it is, then converted to Bgra32 and frozen. The file is closed again before this returns.
    /// A file that cannot be read or decoded throws an exception of a type
    /// <see cref="ImagePairLoader"/> turns into a message, WIC's own failures included (see the
    /// class summary).
    /// </summary>
    /// <param name="path">The file; what it holds, not its name, says what it is.</param>
    /// <returns>The frozen Bgra32 pixels, and what the file was.</returns>
    public LoadedImage Load(string path)
    {
        using FileStream stream = OpenRead(path);

        try
        {
            // PreservePixelFormat stops WIC from quietly converting on the way in, so the format
            // reported to the user is the one the file really has.
            BitmapFrame frame = CreateDecoder(stream, BitmapCreateOptions.PreservePixelFormat).Frames[0];
            ImageInfo info = Describe(path, frame);

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
            throw new NotSupportedException($"An image of {info.Dimensions} is too large to show.", ex);
        }
    }

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
    /// The frame's size, and its depth and channels as <see cref="Classify"/> reads them.
    /// </returns>
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
