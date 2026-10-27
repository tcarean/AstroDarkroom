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

namespace LightsCheck.Core;

/// <summary>Where the pixel size of a series run came from (R-066).</summary>
internal enum PixelPitchSource
{
    /// <summary>A file's XPIXSZ card.</summary>
    File,

    /// <summary>Typed by the user: --pixel, or the window's field.</summary>
    Entered,

    /// <summary>Derived from a raw's 35 mm-equivalent focal length and its mosaic's diagonal.</summary>
    Raw,

    /// <summary>The Nikon D3300's pitch, when nothing else is known.</summary>
    Default,
}

/// <summary>
/// The pixel size a series is measured with (R-066, ADR 0026): what the files say, what the user
/// typed, what the raw implies, or the D3300's, in that order.
/// </summary>
internal static class PixelPitch
{
    /// <summary>The diagonal of a 36 x 24 mm frame, the reference of the 35 mm equivalence.</summary>
    internal const double FullFrameDiagonalMm = 43.2666;

    /// <summary>
    /// The pixel size a raw implies: the camera's 35 mm-equivalent focal length over the shot's
    /// focal length is the crop factor, a ratio of sensor diagonals, so the pitch is the
    /// full-frame diagonal over that crop, spread over the mosaic's diagonal in pixels. The
    /// diagonal, not the width: the two agree on a 3:2 sensor and differ by 4 % on a 4:3 one.
    /// On a D3300 (6016 x 4016) the Veil session's 135 mm with 202 gives 3.998 um, the Smida
    /// session's 20 mm with 30 gives 3.988 um, against the 3.92 of its spec sheet: the camera
    /// writes an integer, rounded from a nominal crop.
    /// </summary>
    /// <param name="focalMm">The shot's focal length, in mm; null or not positive gives null.</param>
    /// <param name="focal35Mm">The 35 mm-equivalent focal length the camera wrote, in mm; null or not positive gives null.</param>
    /// <param name="widthPx">The visible mosaic's width, in pixels; null or not positive gives null.</param>
    /// <param name="heightPx">The visible mosaic's height, in pixels; null or not positive gives null.</param>
    /// <returns>The pixel size in microns, or null when any input is missing.</returns>
    internal static double? FromCrop(double? focalMm, double? focal35Mm, int? widthPx, int? heightPx)
    {
        if (focalMm is not > 0 || focal35Mm is not > 0 || widthPx is not > 0 || heightPx is not > 0)
        {
            return null;
        }

        double diagonalPx = Math.Sqrt((double)widthPx.Value * widthPx.Value + (double)heightPx.Value * heightPx.Value);
        return 1000.0 * FullFrameDiagonalMm * focalMm.Value / (focal35Mm.Value * diagonalPx);
    }

    /// <summary>
    /// The pixel size of a series and where it came from: the first frame that carries XPIXSZ,
    /// else the typed value, else the first raw whose 35 mm-equivalent focal length and size
    /// give one (<see cref="FromCrop"/>), else <see cref="SeriesOptions.D3300PixelUm"/>.
    /// </summary>
    /// <param name="frames">The lights, in the session's order; unreadable ones carry nulls and are skipped by their nulls.</param>
    /// <param name="typedUm">The pixel size the user typed, in microns; null when none was typed.</param>
    /// <returns>The pixel size in microns and its source.</returns>
    internal static (double Um, PixelPitchSource Source) Choose(IReadOnlyList<FrameInfo> frames, double? typedUm)
    {
        ArgumentNullException.ThrowIfNull(frames);
        double? filePixel = frames.Select(f => f.PixelSizeUm).FirstOrDefault(p => p is not null);
        if (filePixel is double file)
        {
            return (file, PixelPitchSource.File);
        }

        if (typedUm is double typed)
        {
            return (typed, PixelPitchSource.Entered);
        }

        double? derived = frames.Select(f => FromCrop(f.FocalMm, f.FocalLength35Mm, f.WidthPx, f.HeightPx)).FirstOrDefault(p => p is not null);
        return derived is double raw ? (raw, PixelPitchSource.Raw) : (SeriesOptions.D3300PixelUm, PixelPitchSource.Default);
    }
}
