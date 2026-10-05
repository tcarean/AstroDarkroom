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

/// <summary>
/// Sends a FITS file to the project's reader and everything else to WIC, by what the file
/// starts with, not by its name. It is the loader the application uses; the two behind it
/// know nothing of each other, and <see cref="ImagePairLoader"/> nothing of either.
/// </summary>
public sealed class RoutingImageLoader(IImageLoader fits, IImageLoader other) : IImageLoader
{
    public RoutingImageLoader()
        : this(new FitsImageLoader(), new WicImageLoader())
    {
    }

    public ImageInfo Probe(string path) => For(path).Probe(path);

    // Looking at the file is disk work too, and the caller may be the UI thread: after the
    // padding question the pair loader resumes there.
    public Task<LoadedImage> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => For(path).LoadAsync(path, cancellationToken), cancellationToken);

    private IImageLoader For(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return FitsHeader.IsFits(stream) ? fits : other;
    }
}
