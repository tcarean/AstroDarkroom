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
/// is the same with both scales 1.
/// </summary>
internal sealed record Wcs(double Cdelt1, double Cdelt2, double Pc11, double Pc12, double Pc21, double Pc22)
{
    /// <summary>The image axis RA runs along: the one that dominates the RA row of the matrix.</summary>
    internal Axis RaAxis => Math.Abs(Pc12) > Math.Abs(Pc11) ? Axis.Y : Axis.X;

    /// <summary>Degrees per pixel, from the RA scale.</summary>
    internal double ScaleDeg => Math.Abs(Cdelt1);

    /// <summary>
    /// The change of RA (scaled by cos DEC, as the matrix gives it) and of DEC, in degrees,
    /// for a displacement in the solved frame's own pixels.
    /// </summary>
    internal (double RaDeg, double DecDeg) SkyOffsetDeg(double dx, double dy)
        => (Cdelt1 * (Pc11 * dx + Pc12 * dy), Cdelt2 * (Pc21 * dx + Pc22 * dy));
}
