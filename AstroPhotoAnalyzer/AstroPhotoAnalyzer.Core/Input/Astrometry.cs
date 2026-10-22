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

// Astrometry.cs - the geometry of the sphere: unit vectors, angular distance, the B1950 -> J2000
// precession of the catalogue, the galactic latitude.
//
// That and nothing more: the tool does not solve fields, does not look for stars, does not
// correct anything. It gets a WCS made by Siril (Wcs.cs) and a catalogue with coordinates from
// 1950 (DarkNebulaCatalogue.cs), and it only has to say whether the centre of a patch falls in a
// cloud's disc, and how far a patch lies from the galactic plane (the band, ADR 0026). At
// 42"/pixel and 32 px patches, an error of a minute of arc does not show.

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The geometry of the sphere that the catalogue and the band need: directions, distances,
    /// discs, the galactic latitude and the precession, all in degrees.
    /// </summary>
    internal static class Astrometry
    {
        internal const double Deg = Math.PI / 180;

        /// <summary>The unit vector of the direction (RA, Dec), in degrees.</summary>
        /// <param name="raDeg">The right ascension, in degrees.</param>
        /// <param name="decDeg">The declination, in degrees.</param>
        /// <returns>
        /// (X, Y, Z): X towards RA 0 on the equator, Y towards RA 90, Z towards the pole.
        /// </returns>
        internal static (double X, double Y, double Z) UnitVector(double raDeg, double decDeg)
        {
            double ra = raDeg * Deg, dec = decDeg * Deg, c = Math.Cos(dec);
            return (c * Math.Cos(ra), c * Math.Sin(ra), Math.Sin(dec));
        }

        /// <summary>(RA, Dec) in degrees, RA in [0, 360), from any non-zero vector.</summary>
        /// <param name="x">The component towards RA 0 on the equator.</param>
        /// <param name="y">The component towards RA 90 on the equator.</param>
        /// <param name="z">The component towards the north pole.</param>
        /// <returns>RA in [0, 360) and Dec in [-90, 90], in degrees.</returns>
        internal static (double Ra, double Dec) FromVector(double x, double y, double z)
        {
            double ra = Math.Atan2(y, x) / Deg;
            if (ra < 0) ra += 360;
            double dec = Math.Atan2(z, Math.Sqrt(x * x + y * y)) / Deg;
            return (ra, dec);
        }

        /// <summary>The angular distance between two directions, in degrees.</summary>
        /// <param name="ra1">The first direction's right ascension, in degrees.</param>
        /// <param name="dec1">The first direction's declination, in degrees.</param>
        /// <param name="ra2">The second direction's right ascension, in degrees.</param>
        /// <param name="dec2">The second direction's declination, in degrees.</param>
        /// <returns>The distance, 0..180 degrees.</returns>
        internal static double SeparationDeg(double ra1, double dec1, double ra2, double dec2)
        {
            var a = UnitVector(ra1, dec1);
            var b = UnitVector(ra2, dec2);
            double dot = Math.Clamp(a.X * b.X + a.Y * b.Y + a.Z * b.Z, -1, 1);
            return Math.Acos(dot) / Deg;
        }

        /// <summary>
        /// The radius, in degrees, of the disc with the given area in square degrees.
        /// </summary>
        /// <param name="areaDeg2">The area, in square degrees; a negative one counts as 0.</param>
        /// <returns>
        /// sqrt(area / pi), in degrees: the flat disc, close enough at a cloud's size.
        /// </returns>
        internal static double RadiusOfArea(double areaDeg2) => Math.Sqrt(Math.Max(0, areaDeg2) / Math.PI);

        // The rotation matrix FK4 (B1950) -> FK5 (J2000), without the E-terms and without proper
        // motion (Aoki et al. 1983, the usual form of SLALIB/astropy). Against the ICRS columns
        // VizieR computes for the same catalogue, the deviation is under 0.02' on all 1787 rows
        // (measured on 2026-10-02, ADR 0025).
        static readonly double[,] B1950ToJ2000Matrix =
        {
            { 0.9999256782, -0.0111820611, -0.0048579477 },
            { 0.0111820610, 0.9999374784, -0.0000271765 },
            { 0.0048579479, -0.0000271474, 0.9999881997 },
        };

        // The rotation equatorial J2000 -> galactic (the north galactic pole at RA 192.859, Dec
        // 27.128, the origin of longitude at RA 266.405, Dec -28.936): the standard matrix, the
        // same in astropy and SLALIB. The tool uses only the latitude b: the Milky Way's band is
        // small |b|.
        static readonly double[,] EquatorialToGalacticMatrix =
        {
            { -0.0548755604, -0.8734370902, -0.4838350155 },
            { 0.4941094279, -0.4448296300, 0.7469822445 },
            { -0.8676661490, -0.1980763734, 0.4559837762 },
        };

        /// <summary>The galactic latitude, in degrees, of the J2000 direction (RA, Dec).</summary>
        /// <param name="raDeg">The right ascension, J2000, in degrees.</param>
        /// <param name="decDeg">The declination, J2000, in degrees.</param>
        /// <returns>The latitude b, -90..90 degrees.</returns>
        internal static double GalacticLatitude(double raDeg, double decDeg)
        {
            var v = UnitVector(raDeg, decDeg);
            double[,] m = EquatorialToGalacticMatrix;
            double z = m[2, 0] * v.X + m[2, 1] * v.Y + m[2, 2] * v.Z;
            return Math.Asin(Math.Clamp(z, -1, 1)) / Deg;
        }

        /// <summary>The precession of a direction from B1950 to J2000, in degrees.</summary>
        /// <param name="raDeg">The right ascension, B1950, in degrees.</param>
        /// <param name="decDeg">The declination, B1950, in degrees.</param>
        /// <returns>RA in [0, 360) and Dec, J2000, in degrees.</returns>
        internal static (double Ra, double Dec) B1950ToJ2000(double raDeg, double decDeg)
        {
            var v = UnitVector(raDeg, decDeg);
            double[,] m = B1950ToJ2000Matrix;
            double x = m[0, 0] * v.X + m[0, 1] * v.Y + m[0, 2] * v.Z;
            double y = m[1, 0] * v.X + m[1, 1] * v.Y + m[1, 2] * v.Z;
            double z = m[2, 0] * v.X + m[2, 1] * v.Y + m[2, 2] * v.Z;
            return FromVector(x, y, z);
        }
    }
}
