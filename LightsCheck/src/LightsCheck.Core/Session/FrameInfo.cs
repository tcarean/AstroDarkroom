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

using static LightsCheck.Core.Ui;

namespace LightsCheck.Core;

/// <summary>
/// What a frame's file says about the shot (R-003), read without decoding the pixels. A
/// field the file does not carry is null; a file that cannot be read keeps its name and
/// index and carries the error instead.
/// </summary>
/// <param name="Index">
/// The frame's 1-based position in the sorted list of the session's lights.
/// </param>
/// <param name="Path">The full path of the frame's file.</param>
/// <param name="Camera">
/// The camera's name (INSTRUME on a FITS, libraw's on a raw); null when unknown.
/// </param>
/// <param name="FocalMm">
/// The focal length in millimetres; null when the file does not say it.
/// </param>
/// <param name="FNumber">The f-number of the shot (a raw only); null when unknown.</param>
/// <param name="ExposureSeconds">The exposure time in seconds; null when unknown.</param>
/// <param name="Iso">The ISO speed; null when unknown.</param>
/// <param name="Timestamp">
/// The time of capture: DATE-OBS (UTC, as Siril writes it) on a FITS, the camera's wall clock on a
/// raw; null when unknown.
/// </param>
/// <param name="PixelSizeUm">
/// The pixel size in microns (XPIXSZ on a FITS; a raw does not say it); null when unknown.
/// </param>
/// <param name="NikonLensId">The Nikon lens ID from a raw's MakerNote; null when absent.</param>
/// <param name="LensMinFocalMm">
/// The lens's shortest focal length in millimetres, from the MakerNote; null when absent.
/// </param>
/// <param name="LensMaxFocalMm">
/// The lens's longest focal length in millimetres, from the MakerNote; null when absent.
/// </param>
/// <param name="LensMaxAperture">
/// The lens's widest f-number at its shortest focal length, from the MakerNote; null when absent.
/// </param>
/// <param name="Error">
/// Why the file could not be read (Romanian, shown to the user); null for a readable file.
/// </param>
internal sealed record FrameInfo(
    int Index,
    string Path,
    string? Camera,
    double? FocalMm,
    double? FNumber,
    double? ExposureSeconds,
    double? Iso,
    DateTime? Timestamp,
    double? PixelSizeUm,
    long? NikonLensId,
    double? LensMinFocalMm,
    double? LensMaxFocalMm,
    double? LensMaxAperture,
    string? Error)
{
    internal string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>
    /// The shot fields of a loaded frame, whether or not its pixels were decoded.
    /// </summary>
    /// <param name="index">The frame's 1-based position in the session.</param>
    /// <param name="path">The frame's file.</param>
    /// <param name="f">The loaded frame; must not be null.</param>
    /// <returns>The frame's metadata, with no error.</returns>
    internal static FrameInfo From(int index, string path, LoadedFrame f)
    {
        ArgumentNullException.ThrowIfNull(f);
        return new FrameInfo(index, path, f.Camera, f.FocalMm, f.FNumber, f.ExposureSeconds, f.Iso,
            f.Timestamp, f.PixelSizeUm, f.NikonLensId, f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture, null);
    }

    /// <summary>
    /// Reads the metadata of one file; never throws, the error becomes part of the record.
    /// </summary>
    /// <param name="index">The frame's 1-based position in the session.</param>
    /// <param name="path">The frame's file, a camera raw or a FITS.</param>
    /// <returns>
    /// The frame's metadata, or a record with every shot field null and <see cref="Error"/> set
    /// when the file cannot be read, or has a FITS extension and an empty header (a file that does
    /// not begin with SIMPLE, as a rule).
    /// </returns>
    internal static FrameInfo Read(int index, string path)
    {
        try
        {
            LoadedFrame f = ImageLoader.LoadInfo(path);

            // The FITS reader does not check SIMPLE: a FITS-named file whose header gives no card
            // (as a rule, a file that is not a FITS) is a frame nothing can be said about.
            if (FitsReader.IsFitsPath(path) && f.Header.Count == 0)
            {
                return new FrameInfo(index, path, null, null, null, null, null, null, null, null, null, null, null, T("not a FITS file (it does not begin with SIMPLE)", "nu e un fisier FITS (nu incepe cu SIMPLE)"));
            }

            return From(index, path, f);
        }
        catch (Exception e)
        {
            // Anything the file makes the readers throw, libraw's own exception type included,
            // is this frame's error, never the run's: the other frames are still listed.
            return new FrameInfo(index, path, null, null, null, null, null, null, null, null, null, null, null, e.Message);
        }
    }
}
