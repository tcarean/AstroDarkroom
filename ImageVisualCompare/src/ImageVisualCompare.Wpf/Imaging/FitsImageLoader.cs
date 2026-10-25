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
using System.Windows;
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf.Imaging;

/// <summary>
/// Reads FITS, which WIC has no codec for, with the project's own reader in Core, and arrives
/// where <see cref="WicImageLoader"/> does: a frozen Bgra32 bitmap and what the file was. The
/// values are shown as they are stored, with no stretch (ADR 0029).
/// </summary>
public sealed class FitsImageLoader : IImageLoader
{
    /// <summary>
    /// Reads only the FITS header, to check that a pair matches before either is decoded. A
    /// file this program cannot show throws as <see cref="FitsHeader.ReadLayout"/> does.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>
    /// The image's size, depth and planes, with no value range: whether a float file counts in
    /// 0 to 1 or in 0 to 65535 shows only in its samples.
    /// </returns>
    public ImageInfo Probe(string path)
    {
        using FileStream stream = OpenRead(path);
        return FitsHeader.ReadLayout(stream).Describe(path);
    }

    /// <summary>Decodes a FITS on a background thread, as <see cref="Load"/> does.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">
    /// Looked at before the decode starts and before each row; a stopped load throws
    /// <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>
    /// The bitmap, and what the file was, with the range its values were read in.
    /// </returns>
    public Task<LoadedImage> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(path, cancellationToken), cancellationToken);

    /// <summary>
    /// Decodes a FITS on the calling thread into a frozen Bgra32 bitmap, the values as stored.
    /// A file this program cannot show throws as <see cref="FitsHeader.ReadLayout"/> does, and
    /// so does an image too large for a bitmap.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">
    /// Looked at before each row; a stopped load throws <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>
    /// The bitmap, and what the file was, with the range its values were read in.
    /// </returns>
    public LoadedImage Load(string path, CancellationToken cancellationToken = default)
    {
        using FileStream stream = OpenRead(path);
        FitsImageLayout layout = FitsHeader.ReadLayout(stream);

        WriteableBitmap bitmap = CreateBitmap(layout);
        long above = Decode(layout, stream, bitmap, FitsScale.FullScale(layout.BitPix), cancellationToken);

        // Whether a float file counts in 0 to 1 or in 0 to 65535 is known only once every
        // sample has been seen. The rare file that turns out to be the second pays for a
        // second pass; holding the floats to avoid it would cost every file 277 MB.
        string? valueRange = null;
        if (FitsScale.IsRescaled(layout.BitPix, above, (long)layout.Width * layout.Height * layout.Planes))
        {
            Decode(layout, stream, bitmap, FitsScale.FullScale(layout.BitPix, rescaled: true), cancellationToken);
            valueRange = FitsScale.SixteenBitRange;
        }

        bitmap.Freeze();
        return new LoadedImage(layout.Describe(path, valueRange), bitmap);
    }

    /// <summary>
    /// Creates the bitmap the rows are written into, through <see cref="Bitmaps.Create"/>: an
    /// image too large for one is the FITS reader's too-large message.
    /// </summary>
    /// <param name="layout">The image whose size the bitmap takes.</param>
    /// <returns>An empty Bgra32 bitmap of the image's size, at 96 dpi.</returns>
    private static WriteableBitmap CreateBitmap(FitsImageLayout layout) =>
        Bitmaps.Create(layout.Width, layout.Height, fits: true);

    /// <summary>
    /// Writes every row into the bitmap, in the order of the file, each where it belongs in the
    /// image.
    /// </summary>
    /// <param name="layout">Where the image's samples are and how they are stored.</param>
    /// <param name="stream">The open file; each row is read from where it is.</param>
    /// <param name="bitmap">Receives the rows: the image's size, Bgra32, not yet frozen.</param>
    /// <param name="fullScale">
    /// The value shown as white (<see cref="FitsScale.FullScale"/>).
    /// </param>
    /// <param name="cancellationToken">
    /// Looked at before each row; a stopped decode throws <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>
    /// How many float samples were above <see cref="FitsScale.RescaleThreshold"/>; 0 for an
    /// integer file.
    /// </returns>
    private static long Decode(
        FitsImageLayout layout, Stream stream, WriteableBitmap bitmap, double fullScale, CancellationToken cancellationToken)
    {
        var decoder = new FitsRowDecoder(layout, stream, fullScale);
        int stride = layout.Width * 4;
        byte[] row = new byte[stride];

        // In the order of the file, whichever way up the image is, so the disk is read forwards.
        for (int fileRow = 0; fileRow < layout.Height; fileRow++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            decoder.ReadFileRow(fileRow, row);
            bitmap.WritePixels(new Int32Rect(0, layout.DisplayRow(fileRow), layout.Width, 1), row, stride, 0);
        }

        return decoder.SamplesAboveThreshold;
    }

    // No buffer of the stream's own: the decoder reads one whole row at each of three places
    // in turn, and a buffer would be filled past the row and thrown away at every seek.
    /// <summary>
    /// Opens a file for reads here and there in it, sharing it with other readers.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The open stream, with no buffer of its own.</returns>
    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.RandomAccess);
}
