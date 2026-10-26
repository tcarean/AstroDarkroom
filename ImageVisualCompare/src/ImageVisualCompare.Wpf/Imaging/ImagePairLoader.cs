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
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf.Imaging;

/// <summary>The two images being compared, on the canvas they share.</summary>
/// <param name="Before">Image A, shown left of the divider.</param>
/// <param name="After">Image B, shown right of the divider.</param>
/// <param name="Pad">
/// The common canvas. Equal to both images when they agree; the larger extent on each axis
/// when they do not, in which case the smaller one is shown padded with black. The padding
/// is layout only — no bitmap here is any larger than the file it came from.
/// </param>
public sealed record ImagePair(LoadedImage Before, LoadedImage After, PadPlan Pad)
{
    public int PixelWidth => Pad.Width;

    public int PixelHeight => Pad.Height;

    /// <summary>
    /// The display mode both images were rendered in: the one asked for when the pair allows
    /// it, <see cref="DisplayMode.Linear"/> otherwise (R-110).
    /// </summary>
    public DisplayMode Mode => Before.Mode;

    /// <summary>
    /// True when both files are shown as stored, so that <see cref="DisplayMode.Histogram"/> and
    /// <see cref="DisplayMode.FalseColours"/> can be asked for (R-110).
    /// </summary>
    public bool AllowsDisplayModes => Before.Info.ShownAsStored && After.Info.ShownAsStored;

    /// <summary>Combined footprint of both Bgra32 buffers, in bytes.</summary>
    public long DisplayBytes => Before.Info.DisplayBytes + After.Info.DisplayBytes;
}

/// <summary>How far the load of a pair has got, for the progress overlay.</summary>
/// <param name="Fraction">Overall progress in the range 0 to 1.</param>
/// <param name="FileName">The file currently being decoded, or null when finished.</param>
public readonly record struct ImageLoadProgress(double Fraction, string? FileName);

/// <summary>
/// The outcome of a load. Declining the padding is deliberately not a failure: the user
/// answered a question, so there is nothing to report back to them and the caller can go
/// straight back to picking files.
/// </summary>
/// <param name="Pair">The loaded pair; null unless the load succeeded.</param>
/// <param name="Error">
/// What went wrong, worded for the user; null on success and when the user declined.
/// </param>
/// <param name="WasDeclined">
/// True when the user declined the padding; there is then neither a pair nor an error.
/// </param>
public sealed record ImagePairLoadResult(ImagePair? Pair, string? Error, bool WasDeclined = false)
{
    public bool IsSuccess => Pair is not null;

    /// <summary>Wraps a loaded pair.</summary>
    /// <param name="pair">The pair, both images decoded.</param>
    /// <returns>A result whose <see cref="IsSuccess"/> is true.</returns>
    public static ImagePairLoadResult Success(ImagePair pair) => new(pair, null);

    /// <summary>Wraps a load that failed, with what the user is told.</summary>
    /// <param name="error">What went wrong, worded for the user.</param>
    /// <returns>A result with no pair and this error.</returns>
    public static ImagePairLoadResult Failure(string error) => new(null, error);

    public static ImagePairLoadResult Declined { get; } = new(null, null, WasDeclined: true);
}

/// <summary>
/// Loads the two images of a pair onto the canvas they share; <see cref="LoadAsync"/> says in
/// what order, when it asks, and what comes back.
/// </summary>
/// <param name="loader">The decoder each file goes through.</param>
public sealed class ImagePairLoader(IImageLoader loader)
{
    /// <summary>
    /// Loads a pair: both headers first, then, if their sizes differ, the question whether to
    /// pad, and only then the two decodes, one after the other.
    /// </summary>
    /// <param name="beforePath">Image A's file, shown left of the divider.</param>
    /// <param name="afterPath">Image B's file, shown right of the divider.</param>
    /// <param name="progress">
    /// Told 0 and A's file name as A's decode starts, 0.5 and B's as B's starts, and 1 with no
    /// name at the end; null for no reports.
    /// </param>
    /// <param name="confirmPadding">
    /// Asked, and only asked, when the two images differ in size: answering yes pads them to
    /// a common canvas, no abandons the load. It is invoked on a thread pool thread, so a UI
    /// implementation has to marshal. Leaving it null means a mismatched pair is refused
    /// outright, which is the right default for anything without a user attached.
    /// </param>
    /// <param name="mode">
    /// How the samples become pixels (ADR 0040): asked of both loads when both files are shown
    /// as stored, and <see cref="DisplayMode.Linear"/> otherwise, whatever was asked; the pair
    /// says which (<see cref="ImagePair.Mode"/>, R-110).
    /// </param>
    /// <param name="cancellationToken">
    /// Looked at as each probe and each decode starts, and inside a decode that can stop; a
    /// stopped load throws <see cref="OperationCanceledException"/> rather than returning a
    /// result. It is not looked at between the second probe and the padding question, so a load
    /// cancelled there still asks, and can come back declined or as the mismatch.
    /// </param>
    /// <returns>
    /// The pair; or a failure whose message names the file that could not be read, or the
    /// mismatch when there was no one to ask; or <see cref="ImagePairLoadResult.Declined"/>.
    /// An exception <see cref="IsExpectedLoadFailure"/> does not accept reaches the caller
    /// instead: the loaders turn their decoders' failures into accepted types, so such an
    /// exception is a defect.
    /// </returns>
    public async Task<ImagePairLoadResult> LoadAsync(
        string beforePath,
        string afterPath,
        IProgress<ImageLoadProgress>? progress = null,
        Func<PadRequest, Task<bool>>? confirmPadding = null,
        DisplayMode mode = DisplayMode.Linear,
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

            // Sequentially, not in parallel: one decoder at a time, and the progress report names
            // the file being read. The reason first given here, a transient decode buffer that two
            // decodes at once would double, did not hold when measured on 2026-09-08: WIC streams
            // each conversion with no spike above the finished bitmap (docs/design/imaging.md).
            // Decoding both at once has not been measured.
            // A mode other than linear needs both files shown as stored (R-110); for any other
            // pair the loaders would render linear anyway, and asking them so keeps the pair's
            // own mode true to what is on screen.
            if (!beforeInfo.ShownAsStored || !afterInfo.ShownAsStored)
            {
                mode = DisplayMode.Linear;
            }

            currentPath = beforePath;
            progress?.Report(new ImageLoadProgress(0, beforeInfo.FileName));
            LoadedImage before = await loader.LoadAsync(beforePath, mode, cancellationToken).ConfigureAwait(false);

            currentPath = afterPath;
            progress?.Report(new ImageLoadProgress(0.5, afterInfo.FileName));
            LoadedImage after = await loader.LoadAsync(afterPath, mode, cancellationToken).ConfigureAwait(false);

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
                $"{UiText.Current.CouldNotRead(Path.GetFileName(currentPath))}{Environment.NewLine}{ex.Message}");
        }
    }

    /// <summary>
    /// Tells a failure of the file from a defect of the program. A file the user picked can be
    /// missing, locked, truncated or in a format no codec handles, and it can simply be too
    /// large for the machine. None of those are defects in this program, so the exception
    /// types listed here become a message rather than a crash dialog. A decoder that reports
    /// such a failure with a type of its own has it turned into one of these by its loader, as
    /// <see cref="WicImageLoader"/> does (ADR 0031); any other type is a defect and is not caught.
    /// </summary>
    /// <param name="ex">What a probe, the padding question or a decode threw.</param>
    /// <returns>
    /// True for an I/O or access error, an unsupported or damaged format, a bad argument, or
    /// running out of memory.
    /// </returns>
    private static bool IsExpectedLoadFailure(Exception ex) => ex
        is IOException
        or UnauthorizedAccessException
        or NotSupportedException
        or FileFormatException
        or ArgumentException
        or OutOfMemoryException;
}
