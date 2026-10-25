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

using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf.Imaging;

/// <summary>
/// What the two loaders that write rows themselves share: the bitmap the rows go into, and the
/// refusal for an image too large for one, said as a message rather than left to be an
/// exception nothing expects (ADR 0031).
/// </summary>
internal static class Bitmaps
{
    /// <summary>
    /// Creates an empty Bgra32 bitmap of an image's size, at 96 dpi. A file can be whole and
    /// still hold more pixels than WPF makes a bitmap of; that is said as a message, as every
    /// other file that cannot be shown is.
    /// </summary>
    /// <param name="width">The image's width in pixels, above 0.</param>
    /// <param name="height">The image's height in pixels, above 0.</param>
    /// <param name="fits">
    /// True when the FITS reader asks, so that the message starts with "FITS: " and the user
    /// knows which reader refused.
    /// </param>
    /// <returns>The bitmap, not yet frozen.</returns>
    /// <exception cref="NotSupportedException">The image is too large for a bitmap.</exception>
    public static WriteableBitmap Create(int width, int height, bool fits)
    {
        try
        {
            return new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentException)
        {
            throw TooLarge(width, height, fits, ex);
        }
    }

    /// <summary>Makes the refusal for an image too large for WIC or WPF to hold.</summary>
    /// <param name="width">The image's width in pixels, as its header gives it.</param>
    /// <param name="height">The image's height in pixels, as its header gives it.</param>
    /// <param name="fits">True for the FITS reader's wording, which starts with "FITS: ".</param>
    /// <param name="ex">The exception WIC or WPF threw.</param>
    /// <returns>
    /// The exception to throw; its message, naming the size in the interface's language, is
    /// what the user reads.
    /// </returns>
    public static NotSupportedException TooLarge(int width, int height, bool fits, Exception ex) =>
        new(fits ? UiText.Current.FitsImageTooLarge(width, height) : UiText.Current.ImageTooLarge(width, height), ex);
}
