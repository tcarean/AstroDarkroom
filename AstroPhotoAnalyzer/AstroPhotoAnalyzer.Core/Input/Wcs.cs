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

// Wcs.cs - the WCS in the header of a plate-solved FITS: from pixel to (RA, Dec) and back.
//
// Only the gnomonic projection (TAN), the only one Siril writes, with the linear part in any of
// the three forms found in headers: the CD matrix, the PC matrix with CDELT (Siril 1.4), or
// CDELT with CROTA2 (old headers). The SIP terms, when present, are IGNORED: on the reference
// image they move the centre of a patch by at most 6 px at the edge of the frame, under a 32 px
// patch (ADR 0025). A header without a WCS is not an error: it returns null, and the analysis
// takes its path without a WCS.
//
// THE PIXEL CONVENTION. FITS counts from 1, with row 1 at the BOTTOM; the raster counts from 0,
// with row 0 at the TOP, and the reader always flips the rows, as Siril shows every FITS
// whatever its ROWORDER says (ADR 0049). A WCS always applies in the FITS convention, so
// between the two sits a line of conversion that depends on the flip: y_fits = H - y_raster on
// a flipped raster (every one the reader makes), y + 1 on one that is not (tests only). Every
// coordinate here is in raster pixels, the centre of pixel (0, 0) being (0, 0); the conversion
// is inside, once.

using System;
using System.Collections.Generic;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The TAN projection of a plate-solved header, between raster pixels and (RA, Dec) in
    /// degrees, SIP ignored.
    /// </summary>
    internal sealed record Wcs
    {
        internal double Crpix1 { get; init; }
        internal double Crpix2 { get; init; }
        internal double Crval1 { get; init; }
        internal double Crval2 { get; init; }

        /// <summary>
        /// The matrix from (pixel - CRPIX) to intermediate coordinates, in degrees per pixel.
        /// </summary>
        internal double Cd11 { get; init; }
        internal double Cd12 { get; init; }
        internal double Cd21 { get; init; }
        internal double Cd22 { get; init; }

        internal int W { get; init; }
        internal int H { get; init; }

        /// <summary>
        /// The raster's rows are flipped relative to the FITS: true for every raster the reader
        /// makes (ADR 0049), false only in tests.
        /// </summary>
        internal bool RowsFlipped { get; init; }

        /// <summary>The header had SIP terms (ignored); for the report.</summary>
        internal bool HasSip { get; init; }

        /// <summary>
        /// The scale, in degrees per pixel: the root of the matrix's determinant.
        /// </summary>
        internal double ScaleDegPerPx => Math.Sqrt(Math.Abs(Cd11 * Cd22 - Cd12 * Cd21));

        /// <summary>
        /// The WCS from the header, or null when the header has no readable one: no CTYPE, a
        /// projection other than TAN, no CRPIX/CRVAL, or no form of the linear part.
        /// </summary>
        /// <param name="h">The FITS header, keyword -> value.</param>
        /// <param name="w">The raster's width, in pixels.</param>
        /// <param name="h2">The raster's height, in pixels.</param>
        /// <param name="rowsFlipped">
        /// Whether the raster's rows were flipped from the file's: true for every raster the
        /// reader makes (ADR 0049).
        /// </param>
        /// <returns>
        /// The WCS; null when there is none to read, or its linear part is singular.
        /// </returns>
        internal static Wcs TryFrom(IReadOnlyDictionary<string, string> h, int w, int h2, bool rowsFlipped)
        {
            string ctype1 = FitsReader.HStr(h, "CTYPE1", ""), ctype2 = FitsReader.HStr(h, "CTYPE2", "");
            if (!ctype1.StartsWith("RA---TAN", StringComparison.Ordinal) || !ctype2.StartsWith("DEC--TAN", StringComparison.Ordinal))
                return null;
            if (!h.ContainsKey("CRPIX1") || !h.ContainsKey("CRPIX2") || !h.ContainsKey("CRVAL1") || !h.ContainsKey("CRVAL2"))
                return null;

            double cd11, cd12, cd21, cd22;
            if (h.ContainsKey("CD1_1") || h.ContainsKey("CD2_2"))
            {
                cd11 = FitsReader.HDbl(h, "CD1_1", 0); cd12 = FitsReader.HDbl(h, "CD1_2", 0);
                cd21 = FitsReader.HDbl(h, "CD2_1", 0); cd22 = FitsReader.HDbl(h, "CD2_2", 0);
            }
            else if (h.ContainsKey("CDELT1") && h.ContainsKey("CDELT2"))
            {
                double d1 = FitsReader.HDbl(h, "CDELT1", 0), d2 = FitsReader.HDbl(h, "CDELT2", 0);
                if (h.ContainsKey("PC1_1") || h.ContainsKey("PC2_2"))
                {
                    // PC defaults to the identity on the diagonal and to 0 off it.
                    cd11 = d1 * FitsReader.HDbl(h, "PC1_1", 1); cd12 = d1 * FitsReader.HDbl(h, "PC1_2", 0);
                    cd21 = d2 * FitsReader.HDbl(h, "PC2_1", 0); cd22 = d2 * FitsReader.HDbl(h, "PC2_2", 1);
                }
                else
                {
                    // CROTA2: the rotation of the axes, the AIPS convention (Paper II,
                    // section 6.1).
                    double rot = FitsReader.HDbl(h, "CROTA2", 0) * Astrometry.Deg;
                    double c = Math.Cos(rot), s = Math.Sin(rot);
                    cd11 = d1 * c; cd12 = -d2 * s;
                    cd21 = d1 * s; cd22 = d2 * c;
                }
            }
            else return null;

            if (cd11 * cd22 - cd12 * cd21 == 0) return null;

            return new Wcs
            {
                Crpix1 = FitsReader.HDbl(h, "CRPIX1", 0),
                Crpix2 = FitsReader.HDbl(h, "CRPIX2", 0),
                Crval1 = FitsReader.HDbl(h, "CRVAL1", 0),
                Crval2 = FitsReader.HDbl(h, "CRVAL2", 0),
                Cd11 = cd11,
                Cd12 = cd12,
                Cd21 = cd21,
                Cd22 = cd22,
                W = w,
                H = h2,
                RowsFlipped = rowsFlipped,
                HasSip = ctype1.EndsWith("-SIP", StringComparison.Ordinal) || h.ContainsKey("A_ORDER"),
            };
        }

        /// <summary>
        /// The same WCS with another flip. Only tests call it: a WCS borrowed through --wcs keeps
        /// the lent FITS's flip (AnalysisRun.BorrowWcs), since the borrowing PNG is that FITS's
        /// flipped array; re-targeting it to the PNG's own order would mirror every row.
        /// </summary>
        /// <param name="flipped">Whether that raster's rows are flipped from the FITS's.</param>
        /// <returns>A copy with <see cref="RowsFlipped"/> set.</returns>
        internal Wcs WithRowsFlipped(bool flipped) => this with { RowsFlipped = flipped };

        /// <summary>
        /// A row coordinate converted from the raster's convention to the FITS's.
        /// </summary>
        /// <param name="yRaster">The row, in raster pixels, 0 at the top.</param>
        /// <returns>The row in FITS pixels, from 1.</returns>
        double FitsY(double yRaster) => RowsFlipped ? H - yRaster : yRaster + 1;

        /// <summary>
        /// A row coordinate converted from the FITS's convention to the raster's.
        /// </summary>
        /// <param name="yFits">The row in FITS pixels, from 1.</param>
        /// <returns>The row, in raster pixels, 0 at the top.</returns>
        double RasterY(double yFits) => RowsFlipped ? H - yFits : yFits - 1;

        /// <summary>
        /// (RA, Dec) in degrees for the position (x, y) in raster pixels, row 0 at the top.
        /// </summary>
        /// <param name="x">The column, in raster pixels; the centre of pixel 0 is 0.</param>
        /// <param name="y">The row, in raster pixels; the centre of the top row is 0.</param>
        /// <returns>
        /// RA in [0, 360] and Dec, in degrees, in the header's frame (J2000 from Siril).
        /// </returns>
        internal (double Ra, double Dec) PixelToSky(double x, double y)
        {
            double px = x + 1 - Crpix1, py = FitsY(y) - Crpix2;
            double ix = Cd11 * px + Cd12 * py, iy = Cd21 * px + Cd22 * py;

            // The zenithal projection: the native angle (phi, theta) from the tangent plane
            // (Paper II, 5.1.3).
            double r = Math.Sqrt(ix * ix + iy * iy);
            double phi = r == 0 ? 0 : Math.Atan2(ix, -iy);
            double theta = r == 0 ? Math.PI / 2 : Math.Atan2(180 / Math.PI, r);

            // From native to celestial, with the native pole at CRVAL and LONPOLE = 180
            // (Paper II, 2.3).
            double ap = Crval1 * Astrometry.Deg, dp = Crval2 * Astrometry.Deg, phip = Math.PI;
            double st = Math.Sin(theta), ct = Math.Cos(theta), sdp = Math.Sin(dp), cdp = Math.Cos(dp);
            double dphi = phi - phip;
            double dec = Math.Asin(Math.Clamp(st * sdp + ct * cdp * Math.Cos(dphi), -1, 1));
            double ra = ap + Math.Atan2(-ct * Math.Sin(dphi), st * cdp - ct * sdp * Math.Cos(dphi));
            ra /= Astrometry.Deg;
            ra %= 360; if (ra < 0) ra += 360;
            return (ra, dec / Astrometry.Deg);
        }

        /// <summary>
        /// The position (x, y) in raster pixels for (RA, Dec); NaN for a direction in the
        /// hemisphere opposite the tangent plane, which has no projection.
        /// </summary>
        /// <param name="raDeg">The right ascension, in degrees, in the header's frame.</param>
        /// <param name="decDeg">The declination, in degrees.</param>
        /// <returns>
        /// The column and the row, in raster pixels, possibly outside the raster; NaN and NaN
        /// behind the tangent plane.
        /// </returns>
        internal (double X, double Y) SkyToPixel(double raDeg, double decDeg)
        {
            double a = raDeg * Astrometry.Deg, d = decDeg * Astrometry.Deg;
            double ap = Crval1 * Astrometry.Deg, dp = Crval2 * Astrometry.Deg, phip = Math.PI;
            double sd = Math.Sin(d), cd = Math.Cos(d), sdp = Math.Sin(dp), cdp = Math.Cos(dp);
            double da = a - ap;
            double theta = Math.Asin(Math.Clamp(sd * sdp + cd * cdp * Math.Cos(da), -1, 1));
            double phi = phip + Math.Atan2(-cd * Math.Sin(da), sd * cdp - cd * sdp * Math.Cos(da));
            if (theta <= 0) return (double.NaN, double.NaN);

            double r = 180 / Math.PI / Math.Tan(theta);
            double ix = r * Math.Sin(phi), iy = -r * Math.Cos(phi);

            double det = Cd11 * Cd22 - Cd12 * Cd21;
            double px = (Cd22 * ix - Cd12 * iy) / det, py = (-Cd21 * ix + Cd11 * iy) / det;
            return (px + Crpix1 - 1, RasterY(py + Crpix2));
        }
    }
}
