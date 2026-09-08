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

public readonly record struct ValidationResult(bool IsValid, string? Error)
{
    public static ValidationResult Success { get; } = new(true, null);

    public static ValidationResult Failure(string error) => new(false, error);
}

public static class ImagePairValidator
{
    /// <summary>
    /// A side-by-side comparison only means anything when both images cover the same pixels,
    /// so a mismatched pair is rejected outright — with both resolutions spelled out, because
    /// "wrong size" without the numbers leaves the user to go and check for themselves.
    /// </summary>
    public static ValidationResult Validate(ImageInfo before, ImageInfo after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (before.PixelWidth == after.PixelWidth && before.PixelHeight == after.PixelHeight)
        {
            return ValidationResult.Success;
        }

        return ValidationResult.Failure(
            string.Join(
                Environment.NewLine,
                "The two images must have identical dimensions.",
                $"A: {before.FileName} — {before.Dimensions}",
                $"B: {after.FileName} — {after.Dimensions}"));
    }
}
