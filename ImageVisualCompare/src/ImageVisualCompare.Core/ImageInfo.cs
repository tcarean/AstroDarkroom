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

/// <summary>How the decoder saw the source file.</summary>
public enum ColorChannels
{
    Unknown,
    Gray,
    GrayAlpha,
    Rgb,
    Rgba,
    Indexed,
}

/// <summary>
/// What was actually loaded from disk. No logic branches on the bit depth or channel
/// layout — everything is normalised to Bgra32 for display — so this exists purely to
/// report the truth to the user in the status bar instead of leaving them to guess.
/// </summary>
public sealed record ImageInfo(
    string Path,
    int PixelWidth,
    int PixelHeight,
    int BitsPerChannel,
    ColorChannels Channels)
{
    public string FileName => System.IO.Path.GetFileName(Path);

    public string Dimensions => $"{PixelWidth} × {PixelHeight}";

    /// <summary>The source format in words, for example "16-bit RGB".</summary>
    public string FormatDescription => Channels switch
    {
        ColorChannels.Gray => $"{BitsPerChannel}-bit gray",
        ColorChannels.GrayAlpha => $"{BitsPerChannel}-bit gray + alpha",
        ColorChannels.Rgb => $"{BitsPerChannel}-bit RGB",
        ColorChannels.Rgba => $"{BitsPerChannel}-bit RGBA",
        ColorChannels.Indexed => $"{BitsPerChannel}-bit indexed",
        _ => $"{BitsPerChannel}-bit",
    };

    /// <summary>Bytes the image occupies once converted to Bgra32, for the memory budget.</summary>
    public long DisplayBytes => (long)PixelWidth * PixelHeight * 4;
}
