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
        Task.Run(() => Load(path), cancellationToken);

    public LoadedImage Load(string path)
    {
        using FileStream stream = OpenRead(path);
        FitsImageLayout layout = FitsHeader.ReadLayout(stream);

        var bitmap = new WriteableBitmap(layout.Width, layout.Height, 96, 96, PixelFormats.Bgra32, null);
        double largest = Decode(layout, stream, bitmap, FitsScale.FullScale(layout.BitPix));

        // Whether a float file counts in 0 to 1 or in 0 to 65535 is known only once every
        // sample has been seen. The rare file that turns out to be the second pays for a
        // second pass; holding the floats to avoid it would cost every file 277 MB.
        string? valueRange = null;
        if (FitsScale.IsRescaled(layout.BitPix, largest))
        {
            Decode(layout, stream, bitmap, FitsScale.FullScale(layout.BitPix, largest));
            valueRange = FitsScale.SixteenBitRange;
        }

        bitmap.Freeze();
        return new LoadedImage(layout.Describe(path, valueRange), bitmap);
    }

    /// <summary>Writes every row into the bitmap and returns the largest finite sample.</summary>
    private static double Decode(FitsImageLayout layout, Stream stream, WriteableBitmap bitmap, double fullScale)
    {
        var decoder = new FitsRowDecoder(layout, stream, fullScale);
        int stride = layout.Width * 4;
        byte[] row = new byte[stride];

        // In the order of the file, whichever way up the image is, so the disk is read forwards.
        for (int fileRow = 0; fileRow < layout.Height; fileRow++)
        {
            decoder.ReadFileRow(fileRow, row);
            bitmap.WritePixels(new Int32Rect(0, layout.DisplayRow(fileRow), layout.Width, 1), row, stride, 0);
        }

        return decoder.LargestSample;
    }

    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1 << 16, FileOptions.RandomAccess);
}
