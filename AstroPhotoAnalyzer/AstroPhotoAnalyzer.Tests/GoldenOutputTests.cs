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

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// The whole console text, on deterministic synthetic scenes, compared with Golden/*.txt.
/// When the output changes INTENTIONALLY, it is re-approved with APA_UPDATE_GOLDEN=1 and the
/// resulting diff is read - that is the review of the report's change.
///
/// Two things that change without the report changing are scrubbed: the version banner (a
/// bump must not fail every golden test) and the run's temporary folder.
///
/// Every case runs in both languages (ADR 0048): Golden/name.txt holds the Romanian text,
/// --lang ro, the same as before the English interface; Golden/name.en.txt the English one, the
/// default.
/// </summary>
public class GoldenOutputTests
{
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    static string GoldenDir => Path.Combine(RepoDir(), "AstroPhotoAnalyzer.Tests", "Golden");

    static bool Update => Environment.GetEnvironmentVariable("APA_UPDATE_GOLDEN") == "1";

    static string RunCli(TempWorkspace ws, string lang, params string[] args)
    {
        var o = new StringWriter(); var e = new StringWriter();
        int code = AnalyzerCli.Run([.. args, "--lang", lang], o, e);
        string text = o + "--- stderr ---\n" + e + "--- exit " + code + "\n";
        text = text.Replace("\r\n", "\n");
        text = text.Replace(ws.Dir, "<TEMP>");
        text = Regex.Replace(text, "^" + Regex.Escape(VersionInfo.Banner) + "$", "AstroPhotoAnalyzer <VERSIUNE>", RegexOptions.Multiline);
        return text;
    }

    static void CheckBoth(string name, TempWorkspace ws, params string[] args)
    {
        Check(name, RunCli(ws, "ro", args));
        Check(name + ".en", RunCli(ws, "en", args));
    }

    static void Check(string name, string actual)
    {
        Directory.CreateDirectory(GoldenDir);
        string path = Path.Combine(GoldenDir, name + ".txt");
        if (Update)
        {
            File.WriteAllText(path, actual);
            return;
        }
        Assert.True(File.Exists(path), "missing " + path + " - generate it with APA_UPDATE_GOLDEN=1");
        string expected = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void R004_Help()
    {
        using var ws = new TempWorkspace();
        CheckBoth("help", ws, "--help");
    }

    [Fact]
    public void R100_DarkSkyTiff16WithEveryFlag()
    {
        using var ws = new TempWorkspace();
        Raster r = Scenes.DarkSky();
        string tif = ws.WriteFile("cer-intunecat.tif", TiffTestData.Build16Rgb(r.W, r.H,
            (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V2("sRGB test")));

        CheckBoth("dark-sky", ws, tif, "--aspect", "50:35", "--distance", "1.5", "--dmax", "2.0");
    }

    [Fact]
    public void R073_CleanSkyOnCanvasAtTheNaturalDistance()
    {
        using var ws = new TempWorkspace();
        Raster r = Scenes.CleanSky();
        string tif = ws.WriteFile("cer-curat.tif", TiffTestData.Build16Rgb(r.W, r.H,
            (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V4("sRGB IEC61966-2.1")));

        CheckBoth("clean-sky-canvas", ws, tif, "--medium", "canvas", "--sizes", "40,60,80");
    }

    [Fact]
    public void R096_MonoPng8WithoutProfile()
    {
        using var ws = new TempWorkspace();
        string png = ImageFiles.SavePng8Gray(Scenes.MonoSky(), ws.PathTo("mono.png"));

        CheckBoth("mono-png8", ws, png);
    }

    /// <summary>
    /// The dark scene again, as a float FITS whose header carries a TAN WCS centred on LDN 42
    /// (42"/px, 512 px = 6 degrees): the report names the catalogued clouds and the method.
    /// </summary>
    [Fact]
    public void R044_DarkSkyFitsWithAWcsNamesTheClouds()
    {
        using var ws = new TempWorkspace();
        Raster r = Scenes.DarkSky();
        var cards = new List<string>
        {
            FitsTestData.StringCard("CTYPE1", "RA---TAN"), FitsTestData.StringCard("CTYPE2", "DEC--TAN"),
            FitsTestData.Card("CRPIX1", "256.5"), FitsTestData.Card("CRPIX2", "256.5"),
            FitsTestData.Card("CRVAL1", "263.27"), FitsTestData.Card("CRVAL2", "-25.70"),
            FitsTestData.Card("CDELT1", "-0.01158"), FitsTestData.Card("CDELT2", "-0.01158"),
            FitsTestData.StringCard("ROWORDER", "BOTTOM-UP"),
        };
        // Written as Siril writes a FITS, the scene's bottom row first and the header saying so,
        // so that the reader, which always flips (ADR 0049), gets the scene the way it was drawn.
        // The WCS is mirrored with the rows (CDELT2 negative about CRPIX2 at the centre), so the
        // catalogue meets the same scene pixels as when the rows were written top first.
        string fits = ws.WriteFile("cer-intunecat-wcs.fit", FitsTestData.Build(r.W, r.H, 3, (x, y, p) => r.At(p, r.H - 1 - y, x), bitpix: -32, extraCards: cards));

        CheckBoth("dark-sky-wcs", ws, fits, "--target-bg", "0.135", "--dmax", "2.0");
    }

    [Fact]
    public void R015_LinearFitsIsRefused()
    {
        using var ws = new TempWorkspace();
        string fits = ws.WriteFile("liniar.fit", FitsTestData.Build(64, 64, 3, (x, y, p) => 0.004, bitpix: -32));

        CheckBoth("linear-fits", ws, fits);
    }

    [Fact]
    public void R003_MissingFileAndBadOption()
    {
        using var ws = new TempWorkspace();
        CheckBoth("missing-file", ws, ws.PathTo("nu-exista.tif"));
        CheckBoth("bad-option", ws, "x.tif", "--dmax", "nouă");
    }
}
