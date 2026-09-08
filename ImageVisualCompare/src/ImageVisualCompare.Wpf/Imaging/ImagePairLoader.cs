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
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf.Imaging;

/// <summary>The two images being compared, guaranteed to share a pixel size.</summary>
public sealed record ImagePair(LoadedImage Before, LoadedImage After)
{
    public int PixelWidth => Before.Info.PixelWidth;

    public int PixelHeight => Before.Info.PixelHeight;

    /// <summary>Combined footprint of both Bgra32 buffers, for the memory readout.</summary>
    public long DisplayBytes => Before.Info.DisplayBytes + After.Info.DisplayBytes;
}

/// <param name="Fraction">Overall progress in the range 0 to 1.</param>
/// <param name="FileName">The file currently being decoded, or null when finished.</param>
public readonly record struct ImageLoadProgress(double Fraction, string? FileName);

public sealed record ImagePairLoadResult(ImagePair? Pair, string? Error)
{
    public bool IsSuccess => Pair is not null;

    public static ImagePairLoadResult Success(ImagePair pair) => new(pair, null);

    public static ImagePairLoadResult Failure(string error) => new(null, error);
}

/// <summary>
/// Loads a pair of images, refusing anything the comparison could not honestly display.
/// </summary>
public sealed class ImagePairLoader(IImageLoader loader)
{
    public async Task<ImagePairLoadResult> LoadAsync(
        string beforePath,
        string afterPath,
        IProgress<ImageLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string currentPath = beforePath;

        try
        {
            // Both headers first. A mismatched pair is then rejected having allocated nothing,
            // instead of after spending several seconds and ~170 MB on pixels about to be thrown
            // away.
            ImageInfo beforeInfo = await Task.Run(() => loader.Probe(beforePath), cancellationToken)
                .ConfigureAwait(false);

            currentPath = afterPath;
            ImageInfo afterInfo = await Task.Run(() => loader.Probe(afterPath), cancellationToken)
                .ConfigureAwait(false);

            ValidationResult validation = ImagePairValidator.Validate(beforeInfo, afterInfo);
            if (!validation.IsValid)
            {
                return ImagePairLoadResult.Failure(validation.Error!);
            }

            // Sequentially, not in parallel: the transient decode buffer is the largest single
            // allocation in the whole application, and overlapping the two would double the peak
            // for no gain the user would notice.
            currentPath = beforePath;
            progress?.Report(new ImageLoadProgress(0, beforeInfo.FileName));
            LoadedImage before = await loader.LoadAsync(beforePath, cancellationToken).ConfigureAwait(false);

            currentPath = afterPath;
            progress?.Report(new ImageLoadProgress(0.5, afterInfo.FileName));
            LoadedImage after = await loader.LoadAsync(afterPath, cancellationToken).ConfigureAwait(false);

            progress?.Report(new ImageLoadProgress(1, null));
            return ImagePairLoadResult.Success(new ImagePair(before, after));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsExpectedLoadFailure(ex))
        {
            return ImagePairLoadResult.Failure(
                $"Could not read {Path.GetFileName(currentPath)}.{Environment.NewLine}{ex.Message}");
        }
    }

    /// <summary>
    /// A file the user picked can be missing, locked, truncated or in a format no codec
    /// handles, and it can simply be too large for the machine. None of those are defects in
    /// this program, so all of them become a message rather than a crash dialog.
    /// </summary>
    private static bool IsExpectedLoadFailure(Exception ex) => ex
        is IOException
        or UnauthorizedAccessException
        or NotSupportedException
        or FileFormatException
        or ArgumentException
        or OutOfMemoryException;
}
