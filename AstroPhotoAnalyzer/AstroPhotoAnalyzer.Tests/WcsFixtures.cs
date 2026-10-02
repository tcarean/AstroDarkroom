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
}
