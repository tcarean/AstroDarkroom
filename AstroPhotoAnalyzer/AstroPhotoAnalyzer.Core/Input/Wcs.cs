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

// Wcs.cs - WCS-ul din antetul unui FITS plate-solved: de la pixel la (RA, Dec) si inapoi.
//
// Doar proiectia gnomonica (TAN), singura pe care o scrie Siril, cu partea liniara in
// oricare din cele trei forme din antete: matricea CD, matricea PC cu CDELT (Siril 1.4),
// sau CDELT cu CROTA2 (antete vechi). Termenii SIP, cand exista, se IGNORA: pe imaginea
// de referinta muta centrul unei dale cu cel mult 6 px la marginea cadrului, sub o dala
// de 32 px (ADR 0025). Un antet fara WCS nu e o eroare: intoarce null, si analiza merge
// pe regula fara WCS.
//
// CONVENTIA DE PIXELI. FITS-ul numara de la 1, cu randul 1 JOS; rasterul numara de la 0,
// cu randul 0 SUS, si cititorul rastoarna randurile pe ROWORDER = BOTTOM-UP. Un WCS se
// aplica mereu in conventia FITS, deci intre cele doua sta un rand de conversie care
// depinde de rasturnare: y_fits = H - y_raster cand randurile au fost rasturnate, y + 1
// cand nu. Toate coordonatele de aici sunt in pixeli ai rasterului, centrul pixelului
// (0, 0) fiind (0, 0); conversia e inauntru, o singura data.

using System;
using System.Collections.Generic;

namespace AstroPhotoAnalyzer
{
    internal sealed record Wcs
    {
        internal double Crpix1 { get; init; }
        internal double Crpix2 { get; init; }
        internal double Crval1 { get; init; }
        internal double Crval2 { get; init; }

        /// <summary>Matricea de la (pixel - CRPIX) la coordonate intermediare, in grade pe pixel.</summary>
        internal double Cd11 { get; init; }
        internal double Cd12 { get; init; }
        internal double Cd21 { get; init; }
        internal double Cd22 { get; init; }

        internal int W { get; init; }
        internal int H { get; init; }

        /// <summary>Randurile rasterului sunt rasturnate fata de FITS (ROWORDER = BOTTOM-UP).</summary>
        internal bool RowsFlipped { get; init; }

        /// <summary>Antetul avea termeni SIP (ignorati); pentru raport.</summary>
        internal bool HasSip { get; init; }

        /// <summary>Scara, in grade pe pixel: radacina determinantului matricei.</summary>
        internal double ScaleDegPerPx => Math.Sqrt(Math.Abs(Cd11 * Cd22 - Cd12 * Cd21));

        /// <summary>
        /// WCS-ul din antet, sau null cand antetul n-are unul citibil: fara CTYPE, alta
        /// proiectie decat TAN, fara CRPIX/CRVAL, sau fara nicio forma a partii liniare.
        /// </summary>
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
                    // PC are implicit identitatea pe diagonala si 0 in afara ei.
                    cd11 = d1 * FitsReader.HDbl(h, "PC1_1", 1); cd12 = d1 * FitsReader.HDbl(h, "PC1_2", 0);
                    cd21 = d2 * FitsReader.HDbl(h, "PC2_1", 0); cd22 = d2 * FitsReader.HDbl(h, "PC2_2", 1);
                }
                else
                {
                    // CROTA2: rotatia axelor, conventia AIPS (Paper II, sectiunea 6.1).
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

        /// <summary>Acelasi WCS, pe un raster cu alta rasturnare (un PNG care imprumuta WCS-ul unui FITS).</summary>
        internal Wcs WithRowsFlipped(bool flipped) => this with { RowsFlipped = flipped };

        double FitsY(double yRaster) => RowsFlipped ? H - yRaster : yRaster + 1;
        double RasterY(double yFits) => RowsFlipped ? H - yFits : yFits - 1;

        /// <summary>(RA, Dec) in grade pentru pozitia (x, y) in pixeli ai rasterului, randul 0 sus.</summary>
        internal (double Ra, double Dec) PixelToSky(double x, double y)
        {
            double px = x + 1 - Crpix1, py = FitsY(y) - Crpix2;
            double ix = Cd11 * px + Cd12 * py, iy = Cd21 * px + Cd22 * py;

            // Proiectia zenitala: unghiul nativ (phi, theta) din planul tangent (Paper II, 5.1.3).
            double r = Math.Sqrt(ix * ix + iy * iy);
            double phi = r == 0 ? 0 : Math.Atan2(ix, -iy);
            double theta = r == 0 ? Math.PI / 2 : Math.Atan2(180 / Math.PI, r);

            // De la nativ la ceresc, cu polul nativ in CRVAL si LONPOLE = 180 (Paper II, 2.3).
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
        /// Pozitia (x, y) in pixeli ai rasterului pentru (RA, Dec); NaN pentru o directie
        /// din emisfera opusa planului tangent, care n-are proiectie.
        /// </summary>
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
