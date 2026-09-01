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

namespace FocalLenghChecker;

/// <summary>Converts between DX and full-frame-equivalent focal lengths.</summary>
public static class FocalLengthConverter
{
    /// <summary>
    /// The full-frame equivalent of a focal length as recorded by the camera.
    /// When the recorded values are already full-frame it is returned unchanged;
    /// otherwise it is a DX value and gets multiplied by the crop factor.
    /// </summary>
    public static double ToFullFrame(double recordedMm, bool sourceIsFullFrame, double cropFactor)
    {
        EnsureValidCropFactor(cropFactor);
        return sourceIsFullFrame ? recordedMm : recordedMm * cropFactor;
    }

    /// <summary>The DX equivalent of a focal length as recorded by the camera.</summary>
    public static double ToDx(double recordedMm, bool sourceIsFullFrame, double cropFactor)
    {
        EnsureValidCropFactor(cropFactor);
        return sourceIsFullFrame ? recordedMm / cropFactor : recordedMm;
    }

    private static void EnsureValidCropFactor(double cropFactor)
    {
        if (!double.IsFinite(cropFactor) || cropFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(cropFactor), cropFactor, "The crop factor must be positive.");
    }
}
