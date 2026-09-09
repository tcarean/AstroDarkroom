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
public readonly record struct PadPlan(int Width, int Height, bool RequiresPadding)
{
    public string Dimensions => $"{Width} × {Height}";
}

/// <summary>
/// What the user is being asked to agree to, carried whole so the caller putting the question
/// on screen does not have to assemble the wording itself.
/// </summary>
public readonly record struct PadRequest(ImageInfo Before, ImageInfo After, PadPlan Plan)
{
    public string Message => ImagePairValidator.DescribeMismatch(Before, After, Plan);
}

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
    /// The question put to the user, with both resolutions spelled out — "wrong size" without
    /// the numbers leaves them to go and check for themselves.
    /// </summary>
    public static string DescribeMismatch(ImageInfo before, ImageInfo after, PadPlan plan)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return string.Join(
            Environment.NewLine,
            "The two images have different dimensions.",
            $"A: {before.FileName} — {before.Dimensions}",
            $"B: {after.FileName} — {after.Dimensions}",
            string.Empty,
            // Broken into short lines on purpose. The dialog wraps anything longer, and a
            // resolution split across two lines is exactly the part being read.
            $"Both can be padded with black to {plan.Dimensions},",
            "anchored top-left, and compared on that canvas.",
            "The image files themselves are not modified.",
            string.Empty,
            "Continue?");
    }
}
