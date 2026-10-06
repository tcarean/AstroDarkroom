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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// Synthetic WCS headers and the one scene the dust tests share with the window tests
/// (linked into AstroPhotoAnalyzer.Wpf.Tests like Scenes.cs): a raster centred on LDN 42
/// with the cloud's disc dark on a flat sky.
/// </summary>
internal static class WcsFixtures
{
    /// <summary>
    /// A TAN WCS with north up and east left, <paramref name="scaleDeg"/> per pixel, centred on
    /// (ra, dec) in the middle of a w x h raster that is NOT flipped (a synthetic raster in memory).
    /// </summary>
    internal static Wcs SyntheticWcs(int w, int h, double ra, double dec, double scaleDeg)
    {
        var hdr = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CTYPE1"] = "RA---TAN",
            ["CTYPE2"] = "DEC--TAN",
            ["CRPIX1"] = (w / 2.0 + 0.5).ToString("R"),
            ["CRPIX2"] = (h / 2.0 + 0.5).ToString("R"),
            ["CRVAL1"] = ra.ToString("R"),
            ["CRVAL2"] = dec.ToString("R"),
            ["CD1_1"] = (-scaleDeg).ToString("R"),
            ["CD1_2"] = "0",
            ["CD2_1"] = "0",
            ["CD2_2"] = scaleDeg.ToString("R"),
        };
        return Wcs.TryFrom(hdr, w, h, rowsFlipped: false)!;
    }

    /// <summary>
    /// 9"/px: a 512 px frame is 1.3 degrees, so LDN 42's disc (radius 0.68 deg = 272 px) fills
    /// the middle and only a corner of its neighbour LDN 19 enters. At 42"/px the 6 degree field
    /// would hold a dozen Pipe clouds on the flat synthetic sky, and the union's P10 would be
    /// the sky, not the one dark disc.
    /// </summary>
    internal const double NarrowScale = 0.0025;
    internal const double Ldn42RadiusPx = 271.6;

    /// <summary>The dark disc of the synthetic scene: inside the catalogue disc, so that the patches outside the cloud stay fully bright.</summary>
    internal const double DarkDiscPx = 240;

    /// <summary>J2000 centre of LDN 42 (B78), from the catalogue.</summary>
    internal const double Ldn42Ra = 263.27, Ldn42Dec = -25.70;

    internal static Wcs OnLdn42() => SyntheticWcs(512, 512, Ldn42Ra, Ldn42Dec, NarrowScale);

    /// <summary>A flat sky at <paramref name="sky"/> with a dark disc of radius <paramref name="rPx"/> at the centre, at <paramref name="dust"/>.</summary>
    internal static Raster SkyWithDisc(float sky, float dust, double rPx)
        => SyntheticRasters.From(512, 512, 3, (x, y, p) => (x - 255.5) * (x - 255.5) + (y - 255.5) * (y - 255.5) <= rPx * rPx ? dust : sky);

    /// <summary>The LDN 42 scene: the dark disc of the cloud on a 0.25 sky, with the narrow WCS on its source.</summary>
    internal static Raster Ldn42Scene()
    {
        Raster r = SkyWithDisc(0.25f, 0.05f, DarkDiscPx);
        r.Source = new SourceInfo { Path = "scena.fit", Format = "FITS", Wcs = OnLdn42() };
        return r;
    }

    /// <summary>
    /// A wide field across the galactic plane: 512 px at 0.06 deg/px (31 degrees), centred on
    /// the plane in Scutum (l = 30, b = 0: RA 281.4, Dec -1.95), so that |b| &lt; 10 degrees is a
    /// strip of about two thirds of the frame and the rest is plain sky (ADR 0026's classes
    /// need both); a 32 px patch is 3.7 square degrees, so the largest Rift cloud (LDN 621, 45
    /// square degrees, 5 degrees north of the centre) still gets the 9 patches of a row.
    /// </summary>
    internal const double WideScale = 0.06;
    internal static Wcs OnThePlane() => SyntheticWcs(512, 512, 281.4, -1.95, WideScale);

    /// <summary>The dust blob's centre (the frame's centre, on the plane) and the bright blob's, 100 px to the right, still in the band.</summary>
    internal const int DustX = 255, DustY = 255, BrightX = 355, BrightY = 255;

    /// <summary>
    /// The band scene: plain sky at 0.25 outside |b| &lt; 10 degrees, the band at 0.35 inside it, a
    /// dark blob (0.05, radius 80 px: with its edge patches about 10 % of the frame, over the
    /// `DustClassMinFraction` a dust class needs to decide the verdict) on the plane, a bright
    /// blob (0.6, radius 40 px) in the band, and one 32 px patch at 1.0 inside the bright blob,
    /// the peak. The WCS is on the source.
    /// </summary>
    internal static Raster BandScene(bool withDust = true)
    {
        Wcs wcs = OnThePlane();
        var inBand = new bool[512 * 512];
        for (int y = 0; y < 512; y++)
            for (int x = 0; x < 512; x++)
            {
                var (ra, dec) = wcs.PixelToSky(x, y);
                inBand[y * 512 + x] = Math.Abs(Astrometry.GalacticLatitude(ra, dec)) < Thresholds.BandGalacticLatitudeDeg;
            }
        Raster r = SyntheticRasters.From(512, 512, 3, (x, y, p) =>
        {
            double dd = (x - DustX) * (x - DustX) + (y - DustY) * (y - DustY);
            double db = (x - BrightX) * (x - BrightX) + (y - BrightY) * (y - BrightY);
            if (x >= 384 && x < 416 && y >= 256 && y < 288) return 1.0f;
            if (withDust && dd <= 80 * 80) return 0.05f;
            if (db <= 40 * 40) return 0.6f;
            // Without the dust blob the band sits at the sky's level too: a band that is only
            // brighter than the sky has mixed patches on its edge that read as dust by a hair.
            return inBand[y * 512 + x] && withDust ? 0.35f : 0.25f;
        });
        r.Source = new SourceInfo { Path = "banda.fit", Format = "FITS", Wcs = wcs };
        return r;
    }

    /// <summary>
    /// A deep-sky frame without a WCS (ADR 0033's classes): a flat sky at 0.15 with Gaussian
    /// pixel noise of <paramref name="noise"/>, a wide halo (0.25 over the sky, sigma 70 px) with
    /// a bright core (0.4 more, sigma 25 px) at the centre, and, with <paramref name="withLane"/>,
    /// a dark lane at 0.03 over the patch columns 1 and 2 (x 32..96, 12.5 % of the patches): a
    /// dust class large enough to decide the verdict.
    /// </summary>
    internal static Raster NebulaScene(bool withLane = false, double noise = 4.0 / 255, int seed = 31)
    {
        Raster r = SyntheticRasters.From(512, 512, 3, (x, y, p) =>
        {
            if (withLane && x >= 32 && x < 96) return 0.03f;
            double d2 = (x - 255.5) * (x - 255.5) + (y - 255.5) * (y - 255.5);
            return (float)(0.15 + 0.25 * Math.Exp(-d2 / (2 * 70.0 * 70.0)) + 0.4 * Math.Exp(-d2 / (2 * 25.0 * 25.0)));
        });
        return SyntheticRasters.AddNoise(r, noise, seed);
    }

    /// <summary>The patch of the band scene fully inside the dust blob, and the one fully inside the bright blob.</summary>
    internal static int DustPatchIndex(DustMap map) => (DustY / 32) * map.Cols + DustX / 32;
    internal static int BrightPatchIndex(DustMap map) => (BrightY / 32) * map.Cols + (BrightX + 16) / 32;
}
