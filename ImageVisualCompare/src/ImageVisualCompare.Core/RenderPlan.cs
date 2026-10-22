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

/// <summary>How the bitmaps are resampled on screen at the current zoom.</summary>
public enum ImageScalingMode
{
    /// <summary>Interpolated. Correct everywhere the user is looking at the picture.</summary>
    Smooth,

    /// <summary>Nearest neighbour. Correct once the user is looking at individual pixels.</summary>
    Pixelated,
}

/// <summary>
/// What the view puts on its shared transform and on both images for one state of the
/// viewport.
/// </summary>
/// <param name="TranslateX">Translation to apply before scaling, in content units.</param>
/// <param name="TranslateY">Translation to apply before scaling, in content units.</param>
/// <param name="RenderScale">Scale in device-independent units, ready to hand to the UI.</param>
/// <param name="ScalingMode">How the bitmap should be resampled at this zoom.</param>
public readonly record struct RenderPlan(
    double TranslateX,
    double TranslateY,
    double RenderScale,
    ImageScalingMode ScalingMode);

/// <summary>
/// Turns the viewport's zoom, counted in physical pixels, into a scale in device-independent
/// pixels; its pan, in image pixels, passes through as the translation.
/// </summary>
public static class RenderPlanner
{
    /// <summary>
    /// Past this zoom the interpolation is smoothing away exactly what the user zoomed in to
    /// see, so the pixels are shown as the squares they are.
    /// </summary>
    public const double PixelatedThreshold = 4.0;

    /// <summary>
    /// Turns viewport state into the numbers the view puts on its transform.
    ///
    /// The division by <paramref name="deviceScale"/> is the whole reason this is worth
    /// isolating: the viewport counts in physical screen pixels so that 100% means one image
    /// pixel per screen pixel, while the UI lays out in device-independent pixels. Getting
    /// that conversion backwards is invisible on an unscaled display and wrong everywhere else.
    /// </summary>
    /// <param name="viewport">
    /// The zoom, in physical screen pixels per image pixel, and the pan, offsets in image
    /// pixels.
    /// </param>
    /// <param name="deviceScale">
    /// Physical pixels per device-independent pixel: 1 at 100% display scaling, 1.5 at 150%;
    /// greater than 0.
    /// </param>
    /// <returns>
    /// The negated offsets as the translation, in image pixels; the viewport's scale divided by
    /// <paramref name="deviceScale"/>; and <see cref="ImageScalingMode.Pixelated"/> from
    /// <see cref="PixelatedThreshold"/> up.
    /// </returns>
    public static RenderPlan For(Viewport viewport, double deviceScale)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deviceScale);

        return new RenderPlan(
            -viewport.OffsetX,
            -viewport.OffsetY,
            viewport.Scale / deviceScale,
            viewport.Scale >= PixelatedThreshold ? ImageScalingMode.Pixelated : ImageScalingMode.Smooth);
    }
}
