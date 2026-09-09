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

/// <summary>The two images being compared, on the canvas they share.</summary>
/// <param name="Pad">
/// The common canvas. Equal to both images when they agree; the larger extent on each axis
/// when they do not, in which case the smaller one is shown padded with black. The padding
/// is layout only — no bitmap here is any larger than the file it came from.
/// </param>
public sealed record ImagePair(LoadedImage Before, LoadedImage After, PadPlan Pad)
{
    public int PixelWidth => Pad.Width;

    public int PixelHeight => Pad.Height;

    /// <summary>Combined footprint of both Bgra32 buffers, for the memory readout.</summary>
    public long DisplayBytes => Before.Info.DisplayBytes + After.Info.DisplayBytes;
}

/// <param name="Fraction">Overall progress in the range 0 to 1.</param>
/// <param name="FileName">The file currently being decoded, or null when finished.</param>
public readonly record struct ImageLoadProgress(double Fraction, string? FileName);

/// <summary>
/// The outcome of a load. Declining the padding is deliberately not a failure: the user
/// answered a question, so there is nothing to report back to them and the caller can go
/// straight back to picking files.
/// </summary>
public sealed record ImagePairLoadResult(ImagePair? Pair, string? Error, bool WasDeclined = false)
{
    public bool IsSuccess => Pair is not null;

    public static ImagePairLoadResult Success(ImagePair pair) => new(pair, null);

    public static ImagePairLoadResult Failure(string error) => new(null, error);

    public static ImagePairLoadResult Declined { get; } = new(null, null, WasDeclined: true);
}

/// <summary>
/// Loads a pair of images, refusing anything the comparison could not honestly display.
/// </summary>
public sealed class ImagePairLoader(IImageLoader loader)
{
    /// <param name="confirmPadding">
    /// Asked, and only asked, when the two images differ in size: answering yes pads them to
    /// a common canvas, no abandons the load. It is invoked on a thread pool thread, so a UI
    /// implementation has to marshal. Leaving it null means a mismatched pair is refused
    /// outright, which is the right default for anything without a user attached.
    /// </param>
    public async Task<ImagePairLoadResult> LoadAsync(
        string beforePath,
        string afterPath,
        IProgress<ImageLoadProgress>? progress = null,
        Func<PadRequest, Task<bool>>? confirmPadding = null,
        CancellationToken cancellationToken = default)
    {
        string currentPath = beforePath;

        try
        {
            // Both headers first. A pair that turns out to need a decision is then put to the
            // user having allocated nothing, instead of after spending several seconds and
            // ~170 MB on pixels that may be about to be thrown away.
            ImageInfo beforeInfo = await Task.Run(() => loader.Probe(beforePath), cancellationToken)
                .ConfigureAwait(false);

            currentPath = afterPath;
            ImageInfo afterInfo = await Task.Run(() => loader.Probe(afterPath), cancellationToken)
                .ConfigureAwait(false);

            PadPlan pad = ImagePairValidator.Inspect(beforeInfo, afterInfo);

            if (pad.RequiresPadding)
            {
                if (confirmPadding is null)
                {
                    return ImagePairLoadResult.Failure(
                        ImagePairValidator.DescribeMismatch(beforeInfo, afterInfo, pad));
                }

                if (!await confirmPadding(new PadRequest(beforeInfo, afterInfo, pad)).ConfigureAwait(false))
                {
                    return ImagePairLoadResult.Declined;
                }
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
            return ImagePairLoadResult.Success(new ImagePair(before, after, pad));
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
