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
/// Textul intreg al consolei, pe scene sintetice deterministe, comparat cu Golden/*.txt.
/// Cand iesirea se schimba INTENTIONAT, se reaproba cu APA_UPDATE_GOLDEN=1 si se citeste
/// diff-ul rezultat - acela e review-ul schimbarii de raport.
///
/// Se scrubeaza doua lucruri care se schimba fara ca raportul sa se schimbe: banner-ul
/// de versiune (un bump nu trebuie sa pice 6 teste) si folderul temporar al rularii.
/// </summary>
public class GoldenOutputTests
{
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    static string GoldenDir => Path.Combine(RepoDir(), "AstroPhotoAnalyzer.Tests", "Golden");

    static bool Update => Environment.GetEnvironmentVariable("APA_UPDATE_GOLDEN") == "1";

    static string RunCli(TempWorkspace ws, params string[] args)
    {
        var o = new StringWriter(); var e = new StringWriter();
        int code = AnalyzerCli.Run(args, o, e);
        string text = o + "--- stderr ---\n" + e + "--- exit " + code + "\n";
        text = text.Replace("\r\n", "\n");
        text = text.Replace(ws.Dir, "<TEMP>");
        text = Regex.Replace(text, "^" + Regex.Escape(VersionInfo.Banner) + "$", "AstroPhotoAnalyzer <VERSIUNE>", RegexOptions.Multiline);
        return text;
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
        Assert.True(File.Exists(path), "lipseste " + path + " - genereaza-l cu APA_UPDATE_GOLDEN=1");
        string expected = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void R004_Help()
    {
        using var ws = new TempWorkspace();
        Check("help", RunCli(ws, "--help"));
    }

    [Fact]
    public void R100_DarkSkyTiff16WithEveryFlag()
    {
        using var ws = new TempWorkspace();
        Raster r = Scenes.DarkSky();
        string tif = ws.WriteFile("cer-intunecat.tif", TiffTestData.Build16Rgb(r.W, r.H,
            (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V2("sRGB test")));

        Check("dark-sky", RunCli(ws, tif, "--aspect", "50:35", "--distance", "1.5"));
    }

    [Fact]
    public void R073_CleanSkyOnCanvasAtTheNaturalDistance()
    {
        using var ws = new TempWorkspace();
        Raster r = Scenes.CleanSky();
        string tif = ws.WriteFile("cer-curat.tif", TiffTestData.Build16Rgb(r.W, r.H,
            (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V4("sRGB IEC61966-2.1")));

        Check("clean-sky-canvas", RunCli(ws, tif, "--medium", "canvas", "--sizes", "40,60,80"));
    }

    [Fact]
    public void R096_MonoPng8WithoutProfile()
    {
        using var ws = new TempWorkspace();
        string png = ImageFiles.SavePng8Gray(Scenes.MonoSky(), ws.PathTo("mono.png"));

        Check("mono-png8", RunCli(ws, png));
    }

    [Fact]
    public void R015_LinearFitsIsRefused()
    {
        using var ws = new TempWorkspace();
        string fits = ws.WriteFile("liniar.fit", FitsTestData.Build(64, 64, 3, (x, y, p) => 0.004, bitpix: -32));

        Check("linear-fits", RunCli(ws, fits));
    }

    [Fact]
    public void R003_MissingFileAndBadOption()
    {
        using var ws = new TempWorkspace();
        Check("missing-file", RunCli(ws, ws.PathTo("nu-exista.tif")));
        Check("bad-option", RunCli(ws, "x.tif", "--dmax", "nouă"));
    }
}
