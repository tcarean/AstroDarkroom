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
/// <param name="Path">The file, as the loader was given it.</param>
/// <param name="PixelWidth">The image's width in the file, in pixels.</param>
/// <param name="PixelHeight">The image's height in the file, in pixels.</param>
/// <param name="BitsPerChannel">
/// Bits of one channel as stored, from 1 to 64; for a WIC format this program does not
/// recognise, the bits of a whole pixel.
/// </param>
/// <param name="Channels">
/// The channel layout as stored; <see cref="ColorChannels.Unknown"/> for a format this program
/// does not recognise.
/// </param>
/// <param name="IsFloat">
/// True when the samples are floating point, in a FITS of BITPIX -32 or -64 or in a float TIFF
/// (R-090); <see cref="FormatDescription"/> then says "float".
/// </param>
/// <param name="ValueRange">
/// The range a float file's values were read in, when it is not 0 to 1
/// (<see cref="FitsScale.SixteenBitRange"/>). Known only once the samples have been read, so
/// null after a probe, and null for every other file.
/// </param>
public sealed record ImageInfo(
    string Path,
    int PixelWidth,
    int PixelHeight,
    int BitsPerChannel,
    ColorChannels Channels,
    bool IsFloat = false,
    string? ValueRange = null)
{
    public string FileName => System.IO.Path.GetFileName(Path);

    public string Dimensions => $"{PixelWidth} × {PixelHeight}";

    /// <summary>
    /// The source format in words, in the interface's language (<see cref="UiText.Format"/>), for
    /// example "16-bit RGB", or "32-bit float RGB (0..65535)" for a FITS whose floats were not in
    /// 0 to 1.
    /// </summary>
    public string FormatDescription
    {
        get
        {
            string format = UiText.Current.Format(BitsPerChannel, IsFloat, Channels);
            return ValueRange is null ? format : $"{format} ({ValueRange})";
        }
    }

    /// <summary>Bytes the image occupies once converted to Bgra32.</summary>
    public long DisplayBytes => (long)PixelWidth * PixelHeight * 4;
}
