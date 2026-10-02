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
/// R-018: the TAN WCS of a plate-solved FITS, in its three linear forms, against figures
/// astropy computed on the owner's plate-solved FITS of 2026-10-02 (5837 x 3867, Siril 1.4.4,
/// CDELT + PC, SIP ignored: wcs_pix2world, not all_pix2world).
/// </summary>
public class WcsTests
{
    /// <summary>The header of starless_rbf0.5_stretch0.17.fit, the WCS cards only.</summary>
    internal static Dictionary<string, string> PlateSolvedHeader() => new(StringComparer.Ordinal)
    {
        ["CTYPE1"] = "RA---TAN-SIP",
        ["CTYPE2"] = "DEC--TAN-SIP",
        ["CRPIX1"] = "2919.5",
        ["CRPIX2"] = "1934.5",
        ["CRVAL1"] = "271.59218667",
        ["CRVAL2"] = "-10.8308300198",
        ["CDELT1"] = "-0.0115791302748302",
        ["CDELT2"] = "0.0115872424930148",
        ["PC1_1"] = "0.9904131000865",
        ["PC1_2"] = "-0.1381372186525",
        ["PC2_1"] = "0.138363973675057",
        ["PC2_2"] = "0.99038144711462",
        ["LONPOLE"] = "180.0",
        ["EQUINOX"] = "2000.0",
        ["A_ORDER"] = "3",
        ["B_ORDER"] = "3",
    };

    internal const int PlateW = 5837, PlateH = 3867;

    static Wcs Plate(bool flipped = true) => Wcs.TryFrom(PlateSolvedHeader(), PlateW, PlateH, flipped)!;

    /// <summary>FITS pixel (1-based, bottom-up) to raster coordinates (0-based, row 0 at the top) for a flipped raster.</summary>
    static (double X, double Y) RasterOf(double fx, double fy) => (fx - 1, PlateH - fy);

    [Theory]
    [InlineData(2919.5, 1934.5, 271.592187, -10.830830)]   // CRPIX -> CRVAL
    [InlineData(1, 1, 302.259944, -31.958928)]
    [InlineData(5837, 1, 236.992747, -23.482853)]
    [InlineData(1, 3867, 303.134422, 5.248235)]
    [InlineData(5837, 3867, 245.246924, 12.850191)]
    [InlineData(1000, 2000, 293.201025, -12.259745)]
    public void R018_PixelToSkyMatchesAstropyOnThePlateSolvedHeader(double fx, double fy, double ra, double dec)
    {
        Wcs w = Plate();
        var (x, y) = RasterOf(fx, fy);
        var sky = w.PixelToSky(x, y);

        Assert.Equal(ra, sky.Ra, 4);
        Assert.Equal(dec, sky.Dec, 4);

        var back = w.SkyToPixel(sky.Ra, sky.Dec);
        Assert.Equal(x, back.X, 3);
        Assert.Equal(y, back.Y, 3);
    }

    [Fact]
    public void R018_TheRowFlipFollowsTheRaster()
    {
        // Same FITS pixel, two rasters: flipped (Siril's BOTTOM-UP) and not. FITS pixel (1, 1)
        // is raster row H-1 on the flipped raster and row 0 on the other.
        var flipped = Plate(true).PixelToSky(0, PlateH - 1);
        var straight = Plate(false).PixelToSky(0, 0);

        Assert.Equal(302.259944, flipped.Ra, 4);
        Assert.Equal(flipped.Ra, straight.Ra, 9);
        Assert.Equal(flipped.Dec, straight.Dec, 9);
        Assert.Equal(straight.Ra, Plate(true).WithRowsFlipped(false).PixelToSky(0, 0).Ra, 9);
    }

    [Fact]
    public void R018_AntaresLandsInsideTheFrame()
    {
        // astropy wcs_world2pix: Antares (247.3519, -26.4320) at FITS pixel (4735.0, 81.0).
        var p = Plate().SkyToPixel(247.3519, -26.4320);
        var expected = RasterOf(4735.023, 81.037);

        Assert.Equal(expected.X, p.X, 1);
        Assert.Equal(expected.Y, p.Y, 1);
        Assert.InRange(p.X, 0, PlateW);
        Assert.InRange(p.Y, 0, PlateH);
    }

    [Fact]
    public void R018_TheScaleAndTheSipFlagAreRead()
    {
        Wcs w = Plate();

        Assert.Equal(0.01158, w.ScaleDegPerPx, 4);   // 41.7"/px
        Assert.True(w.HasSip);
        Assert.Equal(PlateW, w.W);
        Assert.True(w.RowsFlipped);
    }

    [Fact]
    public void R018_TheThreeLinearFormsGiveTheSameMatrix()
    {
        var pc = PlateSolvedHeader();
        Wcs a = Wcs.TryFrom(pc, PlateW, PlateH, true)!;

        var cd = new Dictionary<string, string>(pc, StringComparer.Ordinal);
        foreach (string k in new[] { "CDELT1", "CDELT2", "PC1_1", "PC1_2", "PC2_1", "PC2_2" }) cd.Remove(k);
        cd["CD1_1"] = a.Cd11.ToString("R"); cd["CD1_2"] = a.Cd12.ToString("R");
        cd["CD2_1"] = a.Cd21.ToString("R"); cd["CD2_2"] = a.Cd22.ToString("R");
        Wcs b = Wcs.TryFrom(cd, PlateW, PlateH, true)!;

        // CROTA2: the AIPS convention, CD = [[d1 cos, -d2 sin], [d1 sin, d2 cos]]. The PC of the
        // plate is a rotation with CDELT1 negative, so its angle is -atan2(PC2_1, PC2_2); CDELT1
        // and CDELT2 differ by 0.07 %, so the two forms agree to a tenth of a degree at 2000 px
        // from the centre, not exactly.
        var crota = new Dictionary<string, string>(pc, StringComparer.Ordinal);
        foreach (string k in new[] { "PC1_1", "PC1_2", "PC2_1", "PC2_2" }) crota.Remove(k);
        double rot = -Math.Atan2(0.138363973675057, 0.99038144711462) / Astrometry.Deg;
        crota["CROTA2"] = rot.ToString("R");
        Wcs c = Wcs.TryFrom(crota, PlateW, PlateH, true)!;

        var s1 = a.PixelToSky(100, 200);
        var s2 = b.PixelToSky(100, 200);
        Assert.Equal(s1.Ra, s2.Ra, 6);
        Assert.Equal(s1.Dec, s2.Dec, 6);
        var s3 = c.PixelToSky(100, 200);
        Assert.Equal(s1.Ra, s3.Ra, 1);
        Assert.Equal(s1.Dec, s3.Dec, 1);
        Assert.Equal(a.Cd11, b.Cd11, 12);
        Assert.Equal(Math.Sign(a.Cd21), Math.Sign(c.Cd21));
        Assert.Equal(Math.Sign(a.Cd12), Math.Sign(c.Cd12));
    }

    [Fact]
    public void R018_AHeaderWithoutAWcsGivesNullNotAnError()
    {
        Assert.Null(Wcs.TryFrom(new Dictionary<string, string>(), 10, 10, true));

        var noCtype = PlateSolvedHeader(); noCtype.Remove("CTYPE1");
        Assert.Null(Wcs.TryFrom(noCtype, 10, 10, true));

        var otherProjection = PlateSolvedHeader(); otherProjection["CTYPE1"] = "RA---SIN"; otherProjection["CTYPE2"] = "DEC--SIN";
        Assert.Null(Wcs.TryFrom(otherProjection, 10, 10, true));

        var noLinear = PlateSolvedHeader();
        foreach (string k in new[] { "CDELT1", "CDELT2", "PC1_1", "PC1_2", "PC2_1", "PC2_2" }) noLinear.Remove(k);
        Assert.Null(Wcs.TryFrom(noLinear, 10, 10, true));

        var noCrval = PlateSolvedHeader(); noCrval.Remove("CRVAL2");
        Assert.Null(Wcs.TryFrom(noCrval, 10, 10, true));
    }

    [Fact]
    public void R018_TheFitsReaderCarriesTheWcsOnTheSource()
    {
        var cards = PlateSolvedHeader().Select(kv => FitsTestData.Card(kv.Key, kv.Value)).ToList();
        cards.Add(FitsTestData.StringCard("ROWORDER", "BOTTOM-UP"));
        cards[0] = FitsTestData.StringCard("CTYPE1", "RA---TAN-SIP");
        cards[1] = FitsTestData.StringCard("CTYPE2", "DEC--TAN-SIP");
        byte[] fits = FitsTestData.Build(16, 8, 3, (x, y, p) => 0.2, bitpix: -32, extraCards: cards);

        var (raster, _, _, _, _) = FitsReader.LoadFrom(new MemoryStream(fits));
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("wcs.fit", fits);
        Raster loaded = ImageLoader.Load(path);

        Assert.NotNull(loaded.Source.Wcs);
        Assert.True(loaded.Source.Wcs.RowsFlipped);
        Assert.Equal(16, loaded.Source.Wcs.W);
        Assert.Equal(16, raster.W);

        string plain = ws.WriteFile("plain.fit", FitsTestData.Build(16, 8, 3, (x, y, p) => 0.2, bitpix: -32));
        Assert.Null(ImageLoader.Load(plain).Source.Wcs);
    }

    [Fact]
    public void R019_TheCatalogueIsEmbeddedPrecessedAndNamed()
    {
        var all = DarkNebulaCatalogue.All;

        Assert.Equal(1787, all.Count);
        DarkNebula ldn42 = all.Single(n => n.Ldn == 42);
        // VizieR's ICRS columns for LDN 42: 17 33 05.7 -25 42 04.
        Assert.InRange(Astrometry.SeparationDeg(ldn42.Ra, ldn42.Dec, 263.2738, -25.7011) * 60, 0, 1);
        Assert.Equal("LDN 42 (B78)", ldn42.Name);
        Assert.Equal(4, ldn42.Opacity);

        DarkNebula ldn621 = all.Single(n => n.Ldn == 621);
        Assert.Equal(45.0, ldn621.AreaDeg2);
        Assert.Equal(3.78, ldn621.RadiusDeg, 2);
        Assert.Equal("LDN 621", ldn621.Name);

        Assert.Equal("LDN 303 (B304, B301, B297)", all.Single(n => n.Ldn == 303).Name);
        // A southern cloud below the equator by under a degree keeps its sign ("-00 30").
        Assert.All(all, n => Assert.InRange(n.Dec, -90, 90));
        Assert.All(all, n => Assert.InRange(n.Ra, 0, 360));
    }

    [Fact]
    public void R019_ThePrecessionMatchesAKnownPair()
    {
        // The FK4 to FK5 rotation on the B1950 centre of LDN 1769 (17 39.0, -30 00): VizieR
        // gives 265.5508, -30.0236 in ICRS.
        var (ra, dec) = Astrometry.B1950ToJ2000(17.65 * 15, -30);

        Assert.Equal(265.5509, ra, 3);
        Assert.Equal(-30.0235, dec, 3);
        Assert.Equal(0, Astrometry.SeparationDeg(10, 20, 10, 20), 9);
        Assert.Equal(90, Astrometry.SeparationDeg(0, 0, 90, 0), 9);
    }

    [Fact]
    public void R019_ACatalogueLineIsParsedAsWritten()
    {
        var one = DarkNebulaCatalogue.Load(new StringReader("# comment\n7\t17 25.5\t-26 15\t0.036\t6\t\n11\t17 05.1\t-22 50\t0.012\t6\t57\n"));

        Assert.Equal(2, one.Count);
        Assert.Equal(7, one[0].Ldn);
        Assert.Equal(6, one[0].Opacity);
        Assert.Empty(one[0].Barnard);
        Assert.Equal(new[] { "57" }, one[1].Barnard);
        Assert.Equal(0.012, one[1].AreaDeg2);
    }
}
