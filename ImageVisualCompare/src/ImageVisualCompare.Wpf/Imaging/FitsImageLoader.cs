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
using System.Windows.Media;
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
    public ImageInfo Probe(string path)
    {
        using FileStream stream = OpenRead(path);
        return FitsHeader.ReadLayout(stream).Describe(path);
    }

    public Task<LoadedImage> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(path, cancellationToken), cancellationToken);

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
    /// The bitmap the rows are written into. A file can be whole and still hold more pixels
    /// than WPF makes a bitmap of; that is said as a message, as every other FITS that cannot
    /// be shown is, and not left to be an <see cref="OverflowException"/> nothing expects.
    /// </summary>
    private static WriteableBitmap CreateBitmap(FitsImageLayout layout)
    {
        try
        {
            return new WriteableBitmap(layout.Width, layout.Height, 96, 96, PixelFormats.Bgra32, null);
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentException)
        {
            throw new NotSupportedException(
                $"FITS: an image of {layout.Width} × {layout.Height} is too large to show.", ex);
        }
    }

    /// <summary>
    /// Writes every row into the bitmap and returns how many float samples were above
    /// <see cref="FitsScale.RescaleThreshold"/>.
    /// </summary>
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
    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.RandomAccess);
}
