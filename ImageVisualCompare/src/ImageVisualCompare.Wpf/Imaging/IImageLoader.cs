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

using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf.Imaging;

/// <summary>An image that has been decoded and normalised, ready to hand to the UI.</summary>
/// <param name="Info">What the file actually turned out to be, for the status bar.</param>
/// <param name="Bitmap">Frozen Bgra32 pixels, safe to use from any thread.</param>
public sealed record LoadedImage(ImageInfo Info, BitmapSource Bitmap);

/// <summary>
/// The seam between the application and whatever decodes image files.
///
/// It is typed on <see cref="BitmapSource"/> because that is what the WPF <c>Image</c>
/// control consumes: any replacement decoder — ImageSharp, for instance — would still have
/// to produce one, so hiding that behind a generic parameter would buy nothing real. This
/// interface lives in the UI assembly for the same reason, while everything genuinely
/// framework-independent stays in ImageVisualCompare.Core.
/// </summary>
public interface IImageLoader
{
    /// <summary>
    /// Reads only the header. Used to check that a pair matches before committing to the
    /// cost of decoding either one.
    /// </summary>
    ImageInfo Probe(string path);

    /// <summary>Decodes the full image on a background thread.</summary>
    Task<LoadedImage> LoadAsync(string path, CancellationToken cancellationToken = default);
}
