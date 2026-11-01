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
using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// What a frame's file says about the shot (R-003), read without decoding the pixels. A
/// field the file does not carry is null; a file that cannot be read keeps its name and
/// index and carries the error instead.
/// </summary>
/// <param name="Index">The frame's position in its folder's sorted list, from 1.</param>
/// <param name="Path">The full path of the file.</param>
/// <param name="Camera">The camera model, or null.</param>
/// <param name="FocalMm">The focal length in mm, or null.</param>
/// <param name="FNumber">The aperture as an f-number, or null.</param>
/// <param name="ExposureSeconds">The exposure time in seconds, or null.</param>
/// <param name="Iso">The ISO setting, or null.</param>
/// <param name="Timestamp">
/// The time of the shot as the file states it (a raw's converted to this computer's local time),
/// or null.
/// </param>
/// <param name="PixelSizeUm">The pixel pitch in micrometres, or null.</param>
/// <param name="LensId">The lens ID as the raw carries it, whichever the mount (Nikon's own
/// number on a Nikon body, the maker's lens type elsewhere; ADR 0023), or null.</param>
/// <param name="LensMount">The mount as libraw names it (`Nikon F`, `Canon EF`): the lens's,
/// or the camera's when the lens's is unknown; null when both are.</param>
/// <param name="LensName">The lens's name as the camera wrote it (EXIF LensModel), or null.</param>
/// <param name="LensMinFocalMm">The lens's shortest focal length in mm, from libraw, or
/// null.</param>
/// <param name="LensMaxFocalMm">The lens's longest focal length in mm, from libraw, or
/// null.</param>
/// <param name="LensMaxAperture">The lens's widest aperture (f-number) at its shortest focal
/// length, or null.</param>
/// <param name="Error">The message of the read that failed (in the run's language, for the user),
/// or null when the file was read.</param>
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
    long? LensId,
    string? LensMount,
    string? LensName,
    double? LensMinFocalMm,
    double? LensMaxFocalMm,
    double? LensMaxAperture,
    string? Error)
{
    /// <summary>The diagnostic log: a frame whose metadata could not be read, with its exception (R-101).</summary>
    private static readonly Logger Log = LogManager.GetLogger(nameof(FrameInfo));

    internal string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>
    /// The shot fields of a loaded frame, whether or not its pixels were decoded.
    /// </summary>
    /// <param name="index">The frame's position in its folder's sorted list, from 1.</param>
    /// <param name="path">The full path of the file.</param>
    /// <param name="f">The loaded frame whose shot fields are copied; not null.</param>
    /// <returns>The record, with no error.</returns>
    internal static FrameInfo From(int index, string path, LoadedFrame f)
    {
        ArgumentNullException.ThrowIfNull(f);
        return new FrameInfo(index, path, f.Camera, f.FocalMm, f.FNumber, f.ExposureSeconds, f.Iso,
            f.Timestamp, f.PixelSizeUm, f.LensId, f.LensMount, f.LensName, f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture, null);
    }

    /// <summary>
    /// Reads the metadata of one file; never throws, the error becomes part of the record, and a
    /// failure is written to the diagnostic log with its exception, which the row cannot hold
    /// (R-101), followed, the first time in the process that LibRaw cannot be loaded, by the
    /// Visual C++ runtime's line (R-110).
    /// </summary>
    /// <param name="index">The frame's position in its folder's sorted list, from 1.</param>
    /// <param name="path">The full path of the file.</param>
    /// <returns>The record; every shot field null and <see cref="Error"/> set when the file could
    /// not be read.</returns>
    internal static FrameInfo Read(int index, string path)
    {
        FrameInfo info = Read(index, path, out Exception? failure);
        if (failure is not null)
        {
            Log.Warn(failure, "frame " + LogText.Int(index) + " " + path + " could not be read; its row carries the error");
            VcRuntime.LogOnce(failure);
        }
        else if (info.Error is not null)
        {
            Log.Warn("frame " + LogText.Int(index) + " " + path + " is no FITS file (it does not begin with SIMPLE); its row says so");
        }

        return info;
    }

    /// <summary>
    /// Reads the metadata of one file as <see cref="Read(int, string)"/> does, without writing to
    /// the diagnostic log: for a caller that writes the failure itself (a decoding loop, whose
    /// decode of the same file failed first).
    /// </summary>
    /// <param name="index">The frame's position in its folder's sorted list, from 1.</param>
    /// <param name="path">The full path of the file.</param>
    /// <param name="failure">The exception the read met, or null when the file was read (a file
    /// that is no FITS is read, and its record says so).</param>
    /// <returns>The record <see cref="Read(int, string)"/> returns.</returns>
    internal static FrameInfo Read(int index, string path, out Exception? failure)
    {
        failure = null;
        try
        {
            LoadedFrame f = ImageLoader.LoadInfo(path);

            // The FITS reader answers a file that does not start with SIMPLE with an empty
            // header, not an error; for a frame that is one nothing can be said about (ADR 0003).
            if (FitsReader.IsFitsPath(path) && f.Header.Count == 0)
            {
                return new FrameInfo(index, path, null, null, null, null, null, null, null, null, null, null, null, null, null, T("not a FITS file (it does not begin with SIMPLE)", "nu e un fisier FITS (nu incepe cu SIMPLE)"));
            }

            return From(index, path, f);
        }
        catch (Exception e)
        {
            // Anything the file makes the readers throw, libraw's own exception type included,
            // is this frame's error, never the run's: the other frames are still listed.
            failure = e;
            return new FrameInfo(index, path, null, null, null, null, null, null, null, null, null, null, null, null, null, e.Message);
        }
    }
}
