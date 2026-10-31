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

using NLog;

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
/// <param name="FocalLength35Mm">
/// The 35 mm-equivalent focal length the camera wrote, in mm (a raw only; R-066); null when
/// unknown.
/// </param>
/// <param name="WidthPx">The visible mosaic's width in pixels (a raw only); null when unknown.</param>
/// <param name="HeightPx">The visible mosaic's height in pixels (a raw only); null when unknown.</param>
/// <param name="LensId">
/// The lens ID as the raw carries it, whichever the mount (Nikon's own number on a Nikon body,
/// the maker's lens type elsewhere; R-065); null when absent.
/// </param>
/// <param name="LensMount">
/// The camera's mount as libraw names it (`Nikon F`, `Canon EF`); null when unknown.
/// </param>
/// <param name="LensName">
/// The lens's name as the camera wrote it (EXIF LensModel); null when the file has none.
/// </param>
/// <param name="LensMinFocalMm">
/// The lens's shortest focal length in millimetres, from libraw; null when absent.
/// </param>
/// <param name="LensMaxFocalMm">
/// The lens's longest focal length in millimetres, from libraw; null when absent.
/// </param>
/// <param name="LensMaxAperture">
/// The lens's widest f-number at its shortest focal length, from libraw; null when absent.
/// </param>
/// <param name="Error">
/// Why the file could not be read, as the user is shown it; null for a readable file.
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
    double? FocalLength35Mm,
    int? WidthPx,
    int? HeightPx,
    long? LensId,
    string? LensMount,
    string? LensName,
    double? LensMinFocalMm,
    double? LensMaxFocalMm,
    double? LensMaxAperture,
    string? Error)
{
    /// <summary>The diagnostic log: a frame whose metadata could not be read, with its exception (R-071).</summary>
    private static readonly Logger Log = LogManager.GetLogger(nameof(FrameInfo));

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
            f.Timestamp, f.PixelSizeUm, f.FocalLength35Mm, f.Width > 0 ? f.Width : null, f.Height > 0 ? f.Height : null,
            f.LensId, f.LensMount, f.LensName, f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture, null);
    }

    /// <summary>
    /// Reads the metadata of one file; never throws, the error becomes part of the record, and a
    /// failure is written to the diagnostic log with its exception, which the row cannot hold
    /// (R-071).
    /// </summary>
    /// <param name="index">The frame's 1-based position in the session.</param>
    /// <param name="path">The frame's file, a camera raw or a FITS.</param>
    /// <returns>
    /// The frame's metadata, or a record with every shot field null and <see cref="Error"/> set
    /// when the file cannot be read; a `.fit` that is not a FITS is refused by the reader itself
    /// (`FITS: the file does not begin with SIMPLE.`, R-008), so no guess is made here.
    /// </returns>
    internal static FrameInfo Read(int index, string path)
    {
        FrameInfo info = Read(index, path, out Exception? failure);
        if (failure is not null)
        {
            Log.Warn(failure, "frame " + LogText.Int(index) + " " + path + " could not be read; its row carries the error");
        }

        return info;
    }

    /// <summary>
    /// Reads the metadata of one file as <see cref="Read(int, string)"/> does, without writing to
    /// the diagnostic log: for a caller that writes the failure itself (the series, whose
    /// measurement of the same file failed first).
    /// </summary>
    /// <param name="index">The frame's 1-based position in the session.</param>
    /// <param name="path">The frame's file, a camera raw or a FITS.</param>
    /// <param name="failure">The exception the read met, or null when the file was read.</param>
    /// <returns>
    /// The frame's metadata, or for a file that cannot be read the record
    /// <see cref="Read(int, string)"/> returns.
    /// </returns>
    internal static FrameInfo Read(int index, string path, out Exception? failure)
    {
        try
        {
            FrameInfo info = From(index, path, ImageLoader.LoadInfo(path));
            failure = null;
            return info;
        }
        catch (Exception e)
        {
            // Anything the file makes the readers throw, libraw's own exception type included,
            // is this frame's error, never the run's: the other frames are still listed.
            failure = e;
            return new FrameInfo(index, path, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, e.Message);
        }
    }
}
