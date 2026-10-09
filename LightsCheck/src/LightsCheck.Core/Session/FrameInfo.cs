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

/// <summary>
/// What a frame's file says about the shot (R-003), read without decoding the pixels. A
/// field the file does not carry is null; a file that cannot be read keeps its name and
/// index and carries the error instead.
/// </summary>
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

    /// <summary>The shot fields of a loaded frame, whether or not its pixels were decoded.</summary>
    internal static FrameInfo From(int index, string path, LoadedFrame f)
    {
        ArgumentNullException.ThrowIfNull(f);
        return new FrameInfo(index, path, f.Camera, f.FocalMm, f.FNumber, f.ExposureSeconds, f.Iso,
            f.Timestamp, f.PixelSizeUm, f.NikonLensId, f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture, null);
    }

    /// <summary>Reads the metadata of one file; never throws, the error becomes part of the record.</summary>
    internal static FrameInfo Read(int index, string path)
    {
        try
        {
            LoadedFrame f = ImageLoader.LoadInfo(path);

            // The FITS reader answers a file that does not start with SIMPLE with an empty
            // header, not an error; for a light that is a frame nothing can be said about.
            if (FitsReader.IsFitsPath(path) && f.Header.Count == 0)
            {
                return new FrameInfo(index, path, null, null, null, null, null, null, null, null, null, null, null, "nu e un fisier FITS (nu incepe cu SIMPLE)");
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
