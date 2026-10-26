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
/// The linear part of a plate solve's WCS: the scale per axis (degrees per pixel, the sign
/// included) and the PC matrix, world axis 1 being RA and 2 DEC (R-025, R-038). A CD matrix
/// is the same with both scales 1. DecDeg is the declination of the reference pixel
/// (CRVAL2, degrees), null when the header did not carry it (R-050).
/// </summary>
/// <param name="Cdelt1">
/// The RA axis's scale, in degrees per pixel, the sign included; 1 with a CD matrix.
/// </param>
/// <param name="Cdelt2">
/// The DEC axis's scale, in degrees per pixel, the sign included; 1 with a CD matrix.
/// </param>
/// <param name="Pc11">The matrix's RA row, x term.</param>
/// <param name="Pc12">The matrix's RA row, y term.</param>
/// <param name="Pc21">The matrix's DEC row, x term.</param>
/// <param name="Pc22">The matrix's DEC row, y term.</param>
/// <param name="DecDeg">
/// The declination of the reference pixel, in degrees; null when the header did not carry it.
/// </param>
/// <param name="FramesFlipped">
/// Whether the lights' rows run the other way from the solved FITS's: true for camera raws,
/// read top-down while Siril stores its FITS bottom-up (ADR 0013); false for FITS lights,
/// stored as the solve is and read in the same file order (ADR 0023).
/// </param>
internal sealed record Wcs(double Cdelt1, double Cdelt2, double Pc11, double Pc12, double Pc21, double Pc22, double? DecDeg = null, bool FramesFlipped = true)
{
    /// <summary>
    /// The image axis RA runs along: the one that dominates the RA row of the matrix.
    /// </summary>
    internal Axis RaAxis => Math.Abs(Pc12) > Math.Abs(Pc11) ? Axis.Y : Axis.X;

    /// <summary>
    /// The handedness of the pixel frame against the sky: +1 or -1, the sign of the determinant
    /// of the map from a star's pixel shift (X, Y) to its motion on the sky (east, north)
    /// through <see cref="PointingOffsetDeg"/>, the lights' flip included (ADR 0013, ADR 0023).
    /// A mirrored frame reads a rotation on the sky with the opposite sign, so the sign of a
    /// rotation the sky geometry predicts is multiplied by this before it is compared with the
    /// registration's (R-050).
    /// </summary>
    internal int Handedness
    {
        get
        {
            (double ra1, double dec1) = PointingOffsetDeg(-1, 0);
            (double ra2, double dec2) = PointingOffsetDeg(0, -1);
            return Math.Sign(ra1 * dec2 - dec1 * ra2);
        }
    }

    /// <summary>
    /// The change of RA (scaled by cos DEC) and of DEC, in degrees, for a displacement of the
    /// pointing given in the lights' own pixels: <see cref="SkyOffsetDeg"/> with the y term
    /// reversed when the lights' rows run the other way from the solve's
    /// (<see cref="FramesFlipped"/>, the raws' case).
    /// </summary>
    /// <param name="dx">The pointing's displacement along the lights' x, in pixels.</param>
    /// <param name="dy">The pointing's displacement along the lights' y, in pixels, row 0 first.</param>
    /// <returns>
    /// The change of RA times cos DEC and the change of DEC, in degrees, signed as the WCS's world
    /// axes.
    /// </returns>
    internal (double RaDeg, double DecDeg) PointingOffsetDeg(double dx, double dy)
        => SkyOffsetDeg(dx, FramesFlipped ? -dy : dy);

    /// <summary>
    /// Degrees per pixel along RA: the scale times the length of the matrix row, so a CD matrix
    /// (unit scales) gives the same figure.
    /// </summary>
    internal double RaScaleDeg => Math.Abs(Cdelt1) * Math.Sqrt(Pc11 * Pc11 + Pc12 * Pc12);

    /// <summary>Degrees per pixel along DEC, from the DEC scale and row.</summary>
    internal double DecScaleDeg => Math.Abs(Cdelt2) * Math.Sqrt(Pc21 * Pc21 + Pc22 * Pc22);

    /// <summary>
    /// The change of RA (scaled by cos DEC, as the matrix gives it) and of DEC, in degrees,
    /// for a displacement in the solved frame's own pixels.
    /// </summary>
    /// <param name="dx">The displacement along the solved frame's x, in pixels.</param>
    /// <param name="dy">
    /// The displacement along the solved frame's y (Siril's rows, bottom-up), in pixels.
    /// </param>
    /// <returns>
    /// The change of RA times cos DEC and the change of DEC, in degrees, signed as the WCS's world
    /// axes.
    /// </returns>
    internal (double RaDeg, double DecDeg) SkyOffsetDeg(double dx, double dy)
        => (Cdelt1 * (Pc11 * dx + Pc12 * dy), Cdelt2 * (Pc21 * dx + Pc22 * dy));
}
