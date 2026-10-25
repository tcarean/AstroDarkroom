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

/// <summary>R-027: a TIFF or PNG borrows the WCS of a FITS of the same size through --wcs.</summary>
public class WcsBorrowTests
{
    static byte[] WcsFits(int w, int h)
    {
        var cards = new List<string>
        {
            FitsTestData.StringCard("CTYPE1", "RA---TAN"), FitsTestData.StringCard("CTYPE2", "DEC--TAN"),
            FitsTestData.Card("CRPIX1", (w / 2.0).ToString("R")), FitsTestData.Card("CRPIX2", (h / 2.0).ToString("R")),
            FitsTestData.Card("CRVAL1", "263.27"), FitsTestData.Card("CRVAL2", "-25.70"),
            FitsTestData.Card("CDELT1", "-0.01158"), FitsTestData.Card("CDELT2", "0.01158"),
            FitsTestData.StringCard("ROWORDER", "BOTTOM-UP"),
        };
        return FitsTestData.Build(w, h, 3, (x, y, p) => 0.2, bitpix: -32, extraCards: cards);
    }

    [Fact]
    public void R027_TheFlagIsParsedAndMappedOntoParameters()
    {
        var o = Options.Parse(new[] { "poza.png", "--wcs", "stack.fit" });
        Assert.Null(o.Error);
        Assert.Equal("stack.fit", o.Wcs);
        Assert.Equal("stack.fit", AnalyzerCli.ParametersFrom(o).WcsPath);
        Assert.Null(AnalyzerCli.ParametersFrom(Options.Parse(new[] { "poza.png" })).WcsPath);

        Assert.Contains("--wcs", Options.Parse(new[] { "poza.png", "--wcs" }).Error);
    }

    [Fact]
    public void R027_TheWcsOfAFitsOfTheSameSizeIsLent()
    {
        using var ws = new TempWorkspace();
        string fits = ws.WriteFile("stack.fit", WcsFits(512, 512));
        Raster r = SyntheticRasters.Flat(512, 512, 0.2f);
        r.Source = new SourceInfo { Path = "poza.png", Format = "PNG" };

        Wcs wcs = AnalysisRun.BorrowWcs(fits, r);

        Assert.True(wcs.RowsFlipped);   // the FITS is BOTTOM-UP: the PNG is its flipped array
        Assert.Equal(512, wcs.W);
        var centre = wcs.PixelToSky(255.5, 255.5);
        Assert.Equal(263.27, centre.Ra, 1);
        Assert.Equal(-25.70, centre.Dec, 1);
    }

    [Fact]
    public void R027_ADifferentSizeOrNoWcsOrAMissingFileIsAnErrorNamingTheFlag()
    {
        using var ws = new TempWorkspace();
        Raster r = SyntheticRasters.Flat(512, 512, 0.2f);

        string small = ws.WriteFile("mic.fit", WcsFits(256, 256));
        var e1 = Assert.Throws<Exception>(() => AnalysisRun.BorrowWcs(small, r));
        Assert.StartsWith("--wcs:", e1.Message);
        Assert.Contains("256 x 256", e1.Message);

        string plain = ws.WriteFile("plain.fit", FitsTestData.Build(512, 512, 3, (x, y, p) => 0.2, bitpix: -32));
        var e2 = Assert.Throws<Exception>(() => AnalysisRun.BorrowWcs(plain, r));
        Assert.Contains("n-are WCS", e2.Message);

        var e3 = Assert.Throws<Exception>(() => AnalysisRun.BorrowWcs(ws.PathTo("nu.fit"), r));
        Assert.Contains("nu exista", e3.Message);
    }

    [Fact]
    public void R027_TheConsoleRefusesABadWcsWithExitOne()
    {
        using var ws = new TempWorkspace();
        string png = ImageFiles.SavePng8Gray(Scenes.MonoSky(), ws.PathTo("mono.png"));
        string small = ws.WriteFile("mic.fit", WcsFits(64, 64));

        var o = new StringWriter(); var e = new StringWriter();
        int code = AnalyzerCli.Run(new[] { png, "--wcs", small, "--lang", "ro" }, o, e);

        Assert.Equal(1, code);
        Assert.Contains("EROARE: --wcs:", e.ToString());
    }

    [Fact]
    public void R027_TheConsoleUsesTheLentWcsForTheClouds()
    {
        using var ws = new TempWorkspace();
        Raster scene = SyntheticRasters.From(512, 512, 3, (x, y, p) => (x - 255.5) * (x - 255.5) + (y - 255.5) * (y - 255.5) <= 70 * 70 ? 0.05f : 0.25f);
        string tif = ws.WriteFile("scena.tif", TiffTestData.Build16Rgb(scene.W, scene.H, (x, y, c) => RasterOps.Level16(scene.At(c, y, x)), IccTestData.V2("sRGB test")));
        string fits = ws.WriteFile("stack.fit", WcsFits(512, 512));

        var o = new StringWriter(); var e = new StringWriter();
        AnalyzerCli.Run(new[] { tif, "--wcs", fits, "--lang", "ro" }, o, e);

        Assert.Contains("LDN 42 (B78)", o.ToString());
        Assert.Contains("cadrul e in intregime in banda", o.ToString());   // a 1.3 degree field on the Pipe: no sky outside the band
        Assert.Contains("WCS " + fits, o.ToString());
    }
}
