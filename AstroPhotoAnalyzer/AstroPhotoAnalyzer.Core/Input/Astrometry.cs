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

// Astrometry.cs - geometria sferei: vectori unitate, distanta unghiulara, precesia
// B1950 -> J2000 a catalogului.
//
// Atat si nimic mai mult: unealta nu rezolva campuri, nu cauta stele, nu corecteaza
// nimic. Primeste un WCS gata facut de Siril (Wcs.cs) si un catalog cu coordonate din
// 1950 (DarkNebulaCatalogue.cs), si trebuie doar sa spuna daca centrul unei dale cade in
// discul unui nor. La 42"/pixel si dale de 32 px, o eroare de un minut de arc nu se vede.

using System;

namespace AstroPhotoAnalyzer
{
    internal static class Astrometry
    {
        internal const double Deg = Math.PI / 180;

        /// <summary>Vectorul unitate al directiei (RA, Dec), in grade.</summary>
        internal static (double X, double Y, double Z) UnitVector(double raDeg, double decDeg)
        {
            double ra = raDeg * Deg, dec = decDeg * Deg, c = Math.Cos(dec);
            return (c * Math.Cos(ra), c * Math.Sin(ra), Math.Sin(dec));
        }

        /// <summary>(RA, Dec) in grade, RA in [0, 360), din orice vector nenul.</summary>
        internal static (double Ra, double Dec) FromVector(double x, double y, double z)
        {
            double ra = Math.Atan2(y, x) / Deg;
            if (ra < 0) ra += 360;
            double dec = Math.Atan2(z, Math.Sqrt(x * x + y * y)) / Deg;
            return (ra, dec);
        }

        /// <summary>Distanta unghiulara intre doua directii, in grade.</summary>
        internal static double SeparationDeg(double ra1, double dec1, double ra2, double dec2)
        {
            var a = UnitVector(ra1, dec1);
            var b = UnitVector(ra2, dec2);
            double dot = Math.Clamp(a.X * b.X + a.Y * b.Y + a.Z * b.Z, -1, 1);
            return Math.Acos(dot) / Deg;
        }

        /// <summary>Raza, in grade, a discului cu aria data in grade patrate.</summary>
        internal static double RadiusOfArea(double areaDeg2) => Math.Sqrt(Math.Max(0, areaDeg2) / Math.PI);

        // Matricea de rotatie FK4 (B1950) -> FK5 (J2000), fara termenii E si fara miscare
        // proprie (Aoki et al. 1983, forma uzuala din SLALIB/astropy). Fata de coloanele ICRS
        // calculate de VizieR pentru acelasi catalog, abaterea e sub 0.02' pe toate cele 1787
        // de randuri (masurat pe 2026-10-02, ADR 0025).
        static readonly double[,] B1950ToJ2000Matrix =
        {
            { 0.9999256782, -0.0111820611, -0.0048579477 },
            { 0.0111820610, 0.9999374784, -0.0000271765 },
            { 0.0048579479, -0.0000271474, 0.9999881997 },
        };

        // Rotatia ecuatorial J2000 -> galactic (polul nord galactic la RA 192.859, Dec 27.128,
        // originea longitudinii la RA 266.405, Dec -28.936): matricea standard, aceeasi in
        // astropy si SLALIB. Unealta foloseste doar latitudinea b: banda Caii Lactee e |b| mic.
        static readonly double[,] EquatorialToGalacticMatrix =
        {
            { -0.0548755604, -0.8734370902, -0.4838350155 },
            { 0.4941094279, -0.4448296300, 0.7469822445 },
            { -0.8676661490, -0.1980763734, 0.4559837762 },
        };

        /// <summary>Latitudinea galactica, in grade, a directiei (RA, Dec) J2000.</summary>
        internal static double GalacticLatitude(double raDeg, double decDeg)
        {
            var v = UnitVector(raDeg, decDeg);
            double[,] m = EquatorialToGalacticMatrix;
            double z = m[2, 0] * v.X + m[2, 1] * v.Y + m[2, 2] * v.Z;
            return Math.Asin(Math.Clamp(z, -1, 1)) / Deg;
        }

        /// <summary>Precesia unei directii din B1950 in J2000, in grade.</summary>
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
