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

public class CliTests
{
    static (int Code, string Out, string Err) Run(params string[] args)
    {
        var o = new StringWriter(); var e = new StringWriter();
        int code = AnalyzerCli.Run(args, o, e);
        return (code, o.ToString(), e.ToString());
    }

    static string DarkTiff(TempWorkspace ws)
    {
        Raster r = Scenes.DarkSky();
        return ws.WriteFile("dark.tif", TiffTestData.Build16Rgb(r.W, r.H,
            (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V2("sRGB test")));
    }

    [Fact]
    public void R002_AProblemImageExitsWithThreeAndPrintsTheReport()
    {
        using var ws = new TempWorkspace();
        var (code, output, err) = Run(DarkTiff(ws), "--aspect", "50:35", "--distance", "1.5", "--dmax", "2.0");

        Assert.Equal(3, code);
        Assert.Equal("", err);
        Assert.StartsWith(VersionInfo.Banner, output);
        Assert.Contains("VERDICT: PROBLEMA", output);
        Assert.Contains("Re-stretch din liniar", output);
    }

    [Fact]
    public void R002_ACleanMonoPngExitsWithAttentionForTheMissingProfile()
    {
        using var ws = new TempWorkspace();
        string png = ImageFiles.SavePng8Gray(Scenes.MonoSky(), ws.PathTo("mono.png"));

        var (code, output, _) = Run(png);

        Assert.Equal(2, code);
        Assert.Contains("Fara profil ICC", output);
        Assert.Contains("Imagine mono", output);
    }

    [Fact]
    public void R005_TheSavedReportIsTheBannerPlusExactlyWhatWasPrinted()
    {
        using var ws = new TempWorkspace();
        string outFile = ws.PathTo("raport.txt");

        var (_, output, _) = Run(DarkTiff(ws), "-o", outFile);
        string saved = File.ReadAllText(outFile);

        Assert.StartsWith(VersionInfo.Banner + Environment.NewLine + VersionInfo.Copyright, saved);
        Assert.Contains("    -> raport salvat: " + outFile, output);
        string printedBody = output.Substring(0, output.IndexOf("    -> raport salvat", StringComparison.Ordinal));
        Assert.Equal(printedBody.TrimEnd(), saved.TrimEnd());
    }

    /// <summary>
    /// The report file is written whole or not at all (R-005): a target another program holds
    /// open keeps its old content, no temporary file stays beside it, and the console says why
    /// with exit 1. The same path serves the window's save (R-111).
    /// </summary>
    [Fact]
    public void R005_AReportThatCannotBeWrittenLeavesTheOldFileWholeAndNoTemporary()
    {
        using var ws = new TempWorkspace();
        string tif = DarkTiff(ws);
        string outFile = ws.PathTo("raport.txt");
        File.WriteAllText(outFile, "vechi");
        int code;
        string err;
        using (new FileStream(outFile, FileMode.Open, FileAccess.Read, FileShare.None))
            (code, _, err) = Run(tif, "-o", outFile);

        Assert.Equal(1, code);
        Assert.StartsWith("EROARE: nu s-a putut scrie raportul: ", err);
        Assert.Equal("vechi", File.ReadAllText(outFile));
        Assert.Equal(new[] { Path.GetFileName(tif), "raport.txt" }, Directory.GetFiles(ws.Dir).Select(Path.GetFileName).OrderBy(n => n));

        // The direct call, with the same guarantee, and the written file is WriteFull's bytes.
        // The held target is found by AtomicFile's probe, before anything is written: the
        // sharing violation, an IOException naming the target, not the final move's "access
        // denied" on a temporary name.
        AnalysisResult result = AnalysisRun.Execute(tif, new Parameters());
        using (new FileStream(outFile, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Contains("raport.txt", Assert.Throws<IOException>(() => ReportWriter.SaveFull(result, outFile)).Message);
        Assert.Equal("vechi", File.ReadAllText(outFile));
        ReportWriter.SaveFull(result, outFile);
        var expected = new StringWriter();
        ReportWriter.WriteFull(result, expected);
        Assert.Equal(expected.ToString(), File.ReadAllText(outFile));
        Assert.Equal(2, Directory.GetFiles(ws.Dir).Length);
    }

    [Fact]
    public void R015_ALinearFitsIsRefusedWithExitOne()
    {
        using var ws = new TempWorkspace();
        string fits = ws.WriteFile("lin.fit", FitsTestData.Build(128, 128, 3, (x, y, p) => 0.004 + 0.0001 * (x % 7), bitpix: -32));

        var (code, _, err) = Run(fits);

        Assert.Equal(1, code);
        Assert.Contains("LINIARA", err);
    }

    [Fact]
    public void R025_APassepartoutNarrowerThanTwiceTheFrameIsRefusedBeforeAnyWork()
    {
        using var ws = new TempWorkspace();
        string tif = ws.WriteFile("x.tif", new byte[16]);   // reading is never reached

        var (code, output, err) = Run(tif, "--passepartout", "4", "--frame", "3");

        Assert.Equal(1, code);
        Assert.StartsWith("EROARE: passepartout-ul", err);
        Assert.Contains("dublul latimii ramei", err);
        Assert.DoesNotContain("nu e o imagine", err);   // stopped before reading: the file is empty
        Assert.StartsWith(VersionInfo.Banner, output);
    }

    [Fact]
    public void R002_AnUnreadableFileIsAnErrorNotACrash()
    {
        using var ws = new TempWorkspace();
        string bad = ws.WriteFile("bad.tif", new byte[32]);

        var (code, output, err) = Run(bad);

        Assert.Equal(1, code);
        Assert.StartsWith("EROARE:", err);
        Assert.StartsWith(VersionInfo.Banner, output);
    }

    [Fact]
    public void R024_OptionsMapOntoParametersWithoutACopyOfTheDefaults()
    {
        Parameters d = new();
        Parameters p = AnalyzerCli.ParametersFrom(Options.Parse(new[] { "x.tif" }));
        Assert.Equal(d.Medium, p.Medium);
        Assert.Equal(d.CandidateWidthsCm, p.CandidateWidthsCm);
        Assert.Equal(d.TargetBackground, p.TargetBackground);
        Assert.Null(p.ViewingDistanceM);

        Parameters q = AnalyzerCli.ParametersFrom(Options.Parse(new[] { "x.tif", "--medium", "canvas", "--dmax", "1.7", "--sizes", "60,70", "--distance", "2", "--frame", "4" }));
        Assert.Equal(PrintMedium.Canvas, q.Medium);
        Assert.Equal(1.7, q.Dmax);
        Assert.Equal(new double[] { 60, 70 }, q.CandidateWidthsCm);
        Assert.Equal(2, q.ViewingDistanceM);
        Assert.Equal(4, q.FrameCm);
        Assert.Equal(d.PassepartoutCm, q.PassepartoutCm);
    }

    /// <summary>
    /// The number flags read "NaN" and "Infinity" (R-024): the core refuses them before the
    /// image is read, with its message behind "EROARE: " and exit 1, like the rule between
    /// parameters (R-025). --dmax Infinity is not here: the console's own range check refuses it.
    /// The aspect's sides are syntax: a side that reads as NaN or infinity gets the console's
    /// own aspect message, which quotes the text typed, not the core's on the ratio it would give.
    /// </summary>
    [Theory]
    [InlineData("--dmax", "NaN", "Dmax trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("--distance", "NaN", "distanta de privire trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("--distance", "Infinity", "distanta de privire trebuie sa fie un numar; a primit infinit")]
    [InlineData("--sizes", "NaN", "latimea de print trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("--sizes", "60,Infinity", "latimea de print trebuie sa fie un numar; a primit infinit")]
    [InlineData("--aspect", "NaN:1", "--aspect asteapta latime:inaltime (ex. 50:35), a primit 'NaN:1'")]
    [InlineData("--aspect", "Infinity:1", "--aspect asteapta latime:inaltime (ex. 50:35), a primit 'Infinity:1'")]
    [InlineData("--aspect", "1:Infinity", "--aspect asteapta latime:inaltime (ex. 50:35), a primit '1:Infinity'")]
    [InlineData("--passepartout", "NaN", "passepartout-ul trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("--frame", "Infinity", "rama trebuie sa fie un numar; a primit infinit")]
    public void R024_TheConsoleRefusesNaNAndInfinityBeforeReading(string flag, string value, string message)
    {
        using var ws = new TempWorkspace();
        string tif = ws.WriteFile("x.tif", new byte[16]);   // never read: refused before

        var (code, output, err) = Run(tif, flag, value);

        Assert.Equal(1, code);
        Assert.Equal("EROARE: " + message + Environment.NewLine, err);
        Assert.StartsWith(VersionInfo.Banner, output);
    }
}
