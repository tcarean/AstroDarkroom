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
using ImageVisualCompare.Wpf.Logging;
using NLog;

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
    /// <summary>The load's lines in the diagnostic log (R-121).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Load);

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

        // The load's start and done lines, with the time of each stage between them; a load that
        // throws past the catches below leaves its stages too (R-121).
        using RunClock clock = RunClock.Start(Log, "load", "A=" + beforePath + " B=" + afterPath + " mode=" + mode);

        try
        {
            // Both headers first. A pair that turns out to need a decision is then put to the
            // user having allocated nothing, instead of after spending several seconds and
            // ~170 MB on pixels that may be about to be thrown away.
            ImageInfo beforeInfo = await Task.Run(() => loader.Probe(beforePath), cancellationToken)
                .ConfigureAwait(false);
            clock.Lap("probe A");
            LogProbe("A", beforeInfo);

            currentPath = afterPath;
            ImageInfo afterInfo = await Task.Run(() => loader.Probe(afterPath), cancellationToken)
                .ConfigureAwait(false);
            clock.Lap("probe B");
            LogProbe("B", afterInfo);

            PadPlan pad = ImagePairValidator.Inspect(beforeInfo, afterInfo);

            if (await SettlePaddingAsync(beforeInfo, afterInfo, pad, confirmPadding, clock).ConfigureAwait(false) is { } unsettled)
            {
                return unsettled;
            }

            // Sequentially, not in parallel: one decoder at a time, and the progress report names
            // the file being read. The reason first given here, a transient decode buffer that two
            // decodes at once would double, did not hold when measured on 2026-09-08: WIC streams
            // each conversion with no spike above the finished bitmap (docs/design/imaging.md).
            // Decoding both at once has not been measured.
            mode = ModeFor(beforeInfo, afterInfo, mode);

            currentPath = beforePath;
            progress?.Report(new ImageLoadProgress(0, beforeInfo.FileName));
            LoadedImage before = await loader.LoadAsync(beforePath, mode, cancellationToken).ConfigureAwait(false);
            clock.Lap("decode A");
            Log.Info("decoded A: " + LogText.Decoded(before.Info, before.Mode));

            currentPath = afterPath;
            progress?.Report(new ImageLoadProgress(0.5, afterInfo.FileName));
            LoadedImage after = await loader.LoadAsync(afterPath, mode, cancellationToken).ConfigureAwait(false);
            clock.Lap("decode B");
            Log.Info("decoded B: " + LogText.Decoded(after.Info, after.Mode));

            progress?.Report(new ImageLoadProgress(1, null));
            return Done(new ImagePair(before, after, pad), clock);
        }
        catch (OperationCanceledException)
        {
            // Asked for by the caller, so a line without a stack (R-121).
            clock.Done("cancelled during " + currentPath);
            throw;
        }
        catch (Exception ex) when (IsExpectedLoadFailure(ex))
        {
            return Failed(ex, currentPath, clock);
        }
    }

    /// <summary>
    /// Changes one image of a pair on screen and keeps the other as it is (R-009): the new file's
    /// header first, then, if its size differs from the kept image's, the question whether to
    /// pad, then the one decode; the kept image is not read again, unless the pair's mode is no
    /// longer the one it was rendered in.
    /// </summary>
    /// <param name="current">The pair on screen, both images decoded.</param>
    /// <param name="side">The image the file replaces; the other is kept.</param>
    /// <param name="path">The new file.</param>
    /// <param name="progress">
    /// Told 0 and the new file's name as its decode starts, 0.5 and the kept file's name when
    /// that one has to be decoded again, and 1 with no name at the end; null for no reports.
    /// </param>
    /// <param name="confirmPadding">
    /// As for <see cref="LoadAsync"/>: asked only when the new file's size differs from the kept
    /// image's, with the kept image on its own side of the question.
    /// </param>
    /// <param name="mode">
    /// How the samples become pixels: asked of the decode when the kept image and the new file
    /// are both shown as stored, and <see cref="DisplayMode.Linear"/> otherwise. The kept image
    /// is decoded again in the pair's mode when it was rendered in another, so that both images
    /// of a pair are always in one mode.
    /// </param>
    /// <param name="cancellationToken">As for <see cref="LoadAsync"/>.</param>
    /// <returns>
    /// The new pair, in which the kept image is the very <see cref="LoadedImage"/> of
    /// <paramref name="current"/> unless it had to be decoded again; or a failure naming the
    /// file that could not be read, or the mismatch when there was no one to ask; or
    /// <see cref="ImagePairLoadResult.Declined"/>.
    /// </returns>
    public async Task<ImagePairLoadResult> ReplaceAsync(
        ImagePair current,
        ImageSide side,
        string path,
        IProgress<ImageLoadProgress>? progress = null,
        Func<PadRequest, Task<bool>>? confirmPadding = null,
        DisplayMode mode = DisplayMode.Linear,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);

        LoadedImage kept = side == ImageSide.A ? current.After : current.Before;
        string keptSide = side == ImageSide.A ? "B" : "A";
        string currentPath = path;

        using RunClock clock = RunClock.Start(
            Log,
            "replace",
            "A=" + (side == ImageSide.A ? path : kept.Info.Path) + " B=" + (side == ImageSide.B ? path : kept.Info.Path)
                + " changes=" + side + " mode=" + mode);

        try
        {
            // Only the new file's header: the kept image's was read when it was opened, and its
            // size is what the new file is measured against.
            ImageInfo info = await Task.Run(() => loader.Probe(path), cancellationToken).ConfigureAwait(false);
            clock.Lap("probe " + side);
            LogProbe(side.ToString(), info);

            ImageInfo beforeInfo = side == ImageSide.A ? info : kept.Info;
            ImageInfo afterInfo = side == ImageSide.B ? info : kept.Info;
            PadPlan pad = ImagePairValidator.Inspect(beforeInfo, afterInfo);

            if (await SettlePaddingAsync(beforeInfo, afterInfo, pad, confirmPadding, clock).ConfigureAwait(false) is { } unsettled)
            {
                return unsettled;
            }

            mode = ModeFor(beforeInfo, afterInfo, mode);

            progress?.Report(new ImageLoadProgress(0, info.FileName));
            LoadedImage changed = await loader.LoadAsync(path, mode, cancellationToken).ConfigureAwait(false);
            clock.Lap("decode " + side);
            Log.Info("decoded " + side + ": " + LogText.Decoded(changed.Info, changed.Mode));

            // A pair is in one mode (R-110): a kept image equalised beside a new file that can
            // only be linear, or the other way round, would compare nothing.
            if (kept.Mode != mode)
            {
                currentPath = kept.Info.Path;
                Log.Info("the kept " + keptSide + " was rendered in " + kept.Mode + ", so it is decoded again in " + mode);
                progress?.Report(new ImageLoadProgress(0.5, kept.Info.FileName));
                kept = await loader.LoadAsync(kept.Info.Path, mode, cancellationToken).ConfigureAwait(false);
                clock.Lap("decode " + keptSide);
                Log.Info("decoded " + keptSide + ": " + LogText.Decoded(kept.Info, kept.Mode));
            }

            progress?.Report(new ImageLoadProgress(1, null));
            return Done(side == ImageSide.A ? new ImagePair(changed, kept, pad) : new ImagePair(kept, changed, pad), clock);
        }
        catch (OperationCanceledException)
        {
            clock.Done("cancelled during " + currentPath);
            throw;
        }
        catch (Exception ex) when (IsExpectedLoadFailure(ex))
        {
            return Failed(ex, currentPath, clock);
        }
    }

    /// <summary>
    /// Settles a pair whose sizes differ before anything is decoded: refuses it when there is no
    /// one to ask, or asks and takes the answer. A matching pair needs nothing.
    /// </summary>
    /// <param name="beforeInfo">Image A, as probed.</param>
    /// <param name="afterInfo">Image B, as probed.</param>
    /// <param name="pad">The canvas the two would be shown on.</param>
    /// <param name="confirmPadding">The question, or null when there is no one to ask.</param>
    /// <param name="clock">The load's clock, told the question's time and the outcome of a refusal.</param>
    /// <returns>
    /// The result to return without decoding, the mismatch as a failure or
    /// <see cref="ImagePairLoadResult.Declined"/>; null when the load goes on.
    /// </returns>
    private static async Task<ImagePairLoadResult?> SettlePaddingAsync(
        ImageInfo beforeInfo, ImageInfo afterInfo, PadPlan pad, Func<PadRequest, Task<bool>>? confirmPadding, RunClock clock)
    {
        if (!pad.RequiresPadding)
        {
            return null;
        }

        string sizes = "the sizes differ: A " + LogText.Size(beforeInfo.PixelWidth, beforeInfo.PixelHeight)
            + ", B " + LogText.Size(afterInfo.PixelWidth, afterInfo.PixelHeight)
            + ", canvas " + LogText.Size(pad.Width, pad.Height);
        if (confirmPadding is null)
        {
            Log.Info(sizes + "; no one to ask, so the pair is refused");
            clock.Done("refused: the sizes differ");
            return ImagePairLoadResult.Failure(ImagePairValidator.DescribeMismatch(beforeInfo, afterInfo, pad));
        }

        Log.Info(sizes + "; asking whether to pad");
        bool padded = await confirmPadding(new PadRequest(beforeInfo, afterInfo, pad)).ConfigureAwait(false);
        clock.Lap("question");
        if (!padded)
        {
            clock.Done("declined: the padding was answered no");
            return ImagePairLoadResult.Declined;
        }

        Log.Info("the padding was answered yes");
        return null;
    }

    /// <summary>
    /// The mode a pair is rendered in: the one asked for when both files are shown as stored,
    /// and linear otherwise (R-110). For any other pair the loaders would render linear anyway;
    /// asking them so keeps the pair's own mode true to what is on screen.
    /// </summary>
    /// <param name="beforeInfo">Image A, as probed.</param>
    /// <param name="afterInfo">Image B, as probed.</param>
    /// <param name="mode">The mode asked for.</param>
    /// <returns>The mode to ask of both decodes.</returns>
    private static DisplayMode ModeFor(ImageInfo beforeInfo, ImageInfo afterInfo, DisplayMode mode)
    {
        if (beforeInfo.ShownAsStored && afterInfo.ShownAsStored)
        {
            return mode;
        }

        if (mode != DisplayMode.Linear)
        {
            Log.Debug("mode " + mode + " asked, Linear used: not both files are shown as stored");
        }

        return DisplayMode.Linear;
    }

    /// <summary>Writes the done line of a load that ended in a pair, and wraps the pair.</summary>
    /// <param name="pair">The pair, both images decoded.</param>
    /// <param name="clock">The load's clock.</param>
    /// <returns>The success result.</returns>
    private static ImagePairLoadResult Done(ImagePair pair, RunClock clock)
    {
        clock.Done("mode=" + pair.Mode + " padded=" + LogText.Yes(pair.Pad.RequiresPadding)
            + " canvas=" + LogText.Size(pair.PixelWidth, pair.PixelHeight) + " buffers=" + LogText.MiB(pair.DisplayBytes));
        return ImagePairLoadResult.Success(pair);
    }

    /// <summary>
    /// Turns a failure of a file into the message the user reads (R-005), logged beside the
    /// exception with its type, its stack and its inner exception (a codec's own, ADR 0031),
    /// which the message leaves out.
    /// </summary>
    /// <param name="ex">What the probe, the question or the decode threw.</param>
    /// <param name="path">The file being read when it was thrown.</param>
    /// <param name="clock">The load's clock, told the outcome.</param>
    /// <returns>The failure result with its message.</returns>
    private static ImagePairLoadResult Failed(Exception ex, string path, RunClock clock)
    {
        string message = $"{UiText.Current.CouldNotRead(Path.GetFileName(path))}{Environment.NewLine}{ex.Message}";
        Log.Error(ex, "could not read " + path + "; the user is told: " + LogText.OneLine(message));
        clock.Done("failed on " + path);
        return ImagePairLoadResult.Failure(message);
    }

    /// <summary>
    /// Writes what a probe found a file to be: its path and size, then the log's own words for
    /// what <see cref="ImageInfo"/> holds.
    /// </summary>
    /// <param name="which">The image: <c>A</c> or <c>B</c>.</param>
    /// <param name="info">What the probe found.</param>
    private static void LogProbe(string which, ImageInfo info) =>
        Log.Info("probed " + which + ": " + info.Path + " (" + LogText.FileMiB(info.Path) + ") " + LogText.Image(info));

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
