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

namespace ImageVisualCompare.Core;

/// <summary>
/// The common canvas the two images are shown on: the larger extent of the pair on each
/// axis. When the two agree it is simply their shared size and nothing else happens.
///
/// There are no per-image offsets here on purpose. Both images are anchored top-left, which
/// is what keeps a content coordinate equal to a pixel coordinate in *both* images — the
/// pixel readout and the minimap then need no arithmetic of their own. Centring the smaller
/// image would change that, and this is where the offsets would have to go.
/// </summary>
/// <param name="Width">The canvas width, in image pixels: that of the wider image.</param>
/// <param name="Height">The canvas height, in image pixels: that of the taller image.</param>
/// <param name="RequiresPadding">
/// True when the two images differ in size, so one or both are shown padded and the user is
/// asked first.
/// </param>
public readonly record struct PadPlan(int Width, int Height, bool RequiresPadding)
{
    public string Dimensions => $"{Width} × {Height}";
}

/// <summary>
/// What the user is being asked to agree to, carried whole so the caller putting the question
/// on screen does not have to assemble the wording itself.
/// </summary>
/// <param name="Before">Image A, shown left of the divider, as its header was probed.</param>
/// <param name="After">Image B, shown right of the divider, as its header was probed.</param>
/// <param name="Plan">The canvas the two would be shown on.</param>
public readonly record struct PadRequest(ImageInfo Before, ImageInfo After, PadPlan Plan)
{
    public string Message => ImagePairValidator.DescribeMismatch(Before, After, Plan);
}

/// <summary>
/// Works out the canvas a pair is compared on, and words the question put to the user when
/// that canvas means padding.
/// </summary>
public static class ImagePairValidator
{
    /// <summary>
    /// Works out the canvas the pair will be compared on.
    ///
    /// Differing dimensions are not a refusal any more: the pair is padded with black to the
    /// larger extent on each axis, which keeps every pixel of both images visible and lets
    /// the two originals stay untouched. It is still not something to do behind the user's
    /// back — half the point of a before/after slider is that the two halves line up — so
    /// <see cref="PadPlan.RequiresPadding"/> is what the caller asks about before committing
    /// to a decode.
    /// </summary>
    /// <param name="before">Image A, as its header was probed; only its size is read.</param>
    /// <param name="after">Image B, as its header was probed; only its size is read.</param>
    /// <returns>
    /// The larger extent of the two on each axis, and whether their sizes differ.
    /// </returns>
    public static PadPlan Inspect(ImageInfo before, ImageInfo after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        int width = Math.Max(before.PixelWidth, after.PixelWidth);
        int height = Math.Max(before.PixelHeight, after.PixelHeight);
        bool padded = before.PixelWidth != after.PixelWidth || before.PixelHeight != after.PixelHeight;

        return new PadPlan(width, height, padded);
    }

    /// <summary>
    /// Words the question put to the user before a mismatched pair is padded, with both
    /// resolutions spelled out — "wrong size" without the numbers leaves them to go and check
    /// for themselves.
    /// </summary>
    /// <param name="before">Image A: its file name and dimensions are quoted.</param>
    /// <param name="after">Image B: its file name and dimensions are quoted.</param>
    /// <param name="plan">
    /// The canvas the pair would be padded to: its dimensions are quoted.
    /// </param>
    /// <returns>
    /// The question in the interface's language (<see cref="UiText.PadQuestion"/>), in short
    /// lines, ending with "Continue?".
    /// </returns>
    public static string DescribeMismatch(ImageInfo before, ImageInfo after, PadPlan plan)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return UiText.Current.PadQuestion(
            before.FileName, before.Dimensions, after.FileName, after.Dimensions, plan.Dimensions);
    }
}
