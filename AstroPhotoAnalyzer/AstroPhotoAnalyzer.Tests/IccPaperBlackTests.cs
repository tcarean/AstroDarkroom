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

using System.Text;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// The Dmax read from an ICC printer profile (R-028, ADR 0030), on profiles built by hand with a
/// black the test chose. L* 20 is Y = (36 / 116)^3 = 0.02989, Dmax 1.52; L* 30 is Y = 0.06236,
/// Dmax 1.21; L* 8 is Y = 8 x 27 / 24389 = 0.00886, Dmax 2.05.
/// </summary>
public class IccPaperBlackTests
{
    [Fact]
    public void R028_TheColorimetricBlackOfAV2ProfileIsItsDmax()
    {
        IccBlack b = IccPaperBlack.Read(IccPrinterTestData.V2Lut16("Hartie de test", 20), "test.icc");

        Assert.True(b.Readable, b.Problem);
        Assert.Equal("Hartie de test", b.Description);
        Assert.Equal(20.0, b.BlackLstar, 2);
        Assert.Equal(1.52, b.Dmax);
        Assert.Null(b.Note);
    }

    /// <summary>
    /// The perceptual table of the fixtures puts black at L* 0. On the v2 one the package then
    /// answers L* 3.14 (Dmax 2.46), the ICC reference medium's black, as it does for real
    /// profiles; on the v4 one it answers Y 0, which is refused. A reader that took the header's
    /// intent, or the perceptual one, would give 2.46 or a problem here instead of 1.52 and 1.21.
    /// </summary>
    [Fact]
    public void R028_ThePerceptualTableIsNeverRead()
    {
        Assert.Equal(1.52, IccPaperBlack.Read(IccPrinterTestData.V2Lut16("slab", 20), "slab.icc").Dmax);
        Assert.Equal(1.21, IccPaperBlack.Read(IccPrinterTestData.V4LutAToB("slab v4", 30), "slab4.icc").Dmax);
    }

    [Fact]
    public void R028_AV4LutAToBProfileIsReadTheSameWay()
    {
        IccBlack b = IccPaperBlack.Read(IccPrinterTestData.V4LutAToB("Hartie v4", 30), "v4.icc");

        Assert.True(b.Readable, b.Problem);
        Assert.Equal("Hartie v4", b.Description);
        Assert.Equal(30.0, b.BlackLstar, 2);
        Assert.Equal(1.21, b.Dmax);
    }

    /// <summary>The Dmax is relative to paper white: a darker paper white (Y 0.9) does not move it.</summary>
    [Fact]
    public void R028_TheDmaxIsRelativeToPaperWhite()
    {
        Assert.Equal(1.52, IccPaperBlack.Read(IccPrinterTestData.V2Lut16("alb 0.9", 20, whiteY: 0.9), "w.icc").Dmax);
    }

    [Fact]
    public void R028_ACmykProfileIsReadAtFullInkAndSaysSo()
    {
        IccBlack b = IccPaperBlack.Read(IccPrinterTestData.V2Lut16("Tipar", 8, space: "CMYK"), "cmyk.icc");

        Assert.True(b.Readable, b.Problem);
        Assert.Equal(8.0, b.BlackLstar, 2);
        Assert.Equal(2.05, b.Dmax);
        Assert.Contains("profil CMYK", b.Note);
    }

    [Fact]
    public void R028_WhatIsNotAPrinterProfileSaysWhy()
    {
        IccBlack monitor = IccPaperBlack.Read(IccPrinterTestData.V2Lut16("Monitor", 20, deviceClass: "mntr"), "m.icc");
        Assert.False(monitor.Readable);
        Assert.Contains("nu e un profil de imprimanta (clasa 'mntr')", monitor.Problem);
        Assert.Equal("Monitor", monitor.Description);

        IccBlack text = IccPaperBlack.Read(Encoding.ASCII.GetBytes(new string('x', 400)), "note.icc");
        Assert.Equal("nu e un profil ICC", text.Problem);
        Assert.Equal("note.icc", text.Description);

        IccBlack grey = IccPaperBlack.Read(IccPrinterTestData.V2Lut16("Gri", 20, space: "GRAY"), "g.icc");
        Assert.Contains("se citesc doar profilele RGB si CMYK", grey.Problem);

        using var ws = new TempWorkspace();
        IccBlack missing = IccPaperBlack.Read(ws.PathTo("nu-exista.icc"));
        Assert.StartsWith("fisierul nu se poate citi", missing.Problem);

        // A printer profile cut in the middle of its table: a message, never an exception.
        byte[] whole = IccPrinterTestData.V2Lut16("Rupt", 20);
        IccBlack cut = IccPaperBlack.Read(whole.Take(whole.Length - 60).ToArray(), "rupt.icc");
        Assert.False(cut.Readable);

        // A class of four NUL bytes is shown as nothing, not as control characters on the console.
        byte[] zeroed = IccPrinterTestData.V2Lut16("Fara clasa", 20, deviceClass: "\0\0\0\0");
        Assert.Contains("(clasa '')", IccPaperBlack.Read(zeroed, "z.icc").Problem);
    }

    /// <summary>
    /// A table that gives a black the paper cannot have is a damaged profile, and says so: Y 0
    /// from a black at L* 0, and a Dmax above the range --dmax itself accepts (L* 0.05 is
    /// Y 0.0000554, Dmax 4.26). The console refuses the second one instead of analysing with it.
    /// </summary>
    [Fact]
    public void R028_ABlackThePaperCannotHaveIsADamagedProfile()
    {
        IccBlack zero = IccPaperBlack.Read(IccPrinterTestData.V2Lut16("Negru zero", 0), "zero.icc");
        Assert.Equal("tabela colorimetrica a profilului nu da un negru intre 0 si albul hartiei; profilul e probabil stricat", zero.Problem);

        IccBlack deep = IccPaperBlack.Read(IccPrinterTestData.V2Lut16("Prea negru", 0.05), "deep.icc");
        Assert.StartsWith("profilul da Dmax 4.2", deep.Problem);
        Assert.EndsWith("in afara intervalului (0, 4]; profilul e probabil stricat", deep.Problem);

        IccBlack white = IccPaperBlack.Read(IccPrinterTestData.V2Lut16("Fara negru", 99.9), "white.icc");
        Assert.False(white.Readable);

        using var ws = new TempWorkspace();
        Assert.Null(new Parameters().WithDmaxFromProfile(ws.WriteFile("deep.icc", IccPrinterTestData.V2Lut16("Prea negru", 0.05)), out string? error));
        Assert.Contains("profilul e probabil stricat", error);
    }

    /// <summary>The CMYK reserve travels with the Dmax: Anexa F prints it under the parameters, in the console and in the saved report.</summary>
    [Fact]
    public void R028_TheCmykNoteReachesTheReport()
    {
        using var ws = new TempWorkspace();
        string cmyk = ws.WriteFile("tipar.icc", IccPrinterTestData.V2Lut16("Tipar", 8, space: "CMYK"));
        string rgb = ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20));

        Parameters? p = new Parameters().WithDmaxFromProfile(cmyk, out _);
        var w = new StringWriter();
        ReportWriter.WriteBody(AnalysisRun.Execute(Scenes.CleanSky(), "memorie", p!), w);
        Assert.Contains("Dmax 2.05 (din profilul ICC \"Tipar\")", w.ToString());
        Assert.Contains("  nota profilului: profil CMYK: negrul e cu toate cernelurile la maximum", w.ToString());

        var plain = new StringWriter();
        ReportWriter.WriteBody(AnalysisRun.Execute(Scenes.CleanSky(), "memorie", new Parameters().WithDmaxFromProfile(rgb, out _)!), plain);
        Assert.DoesNotContain("nota profilului", plain.ToString());
    }

    [Fact]
    public void R028_TheInstalledListKeepsThePrinterProfilesByName()
    {
        using var ws = new TempWorkspace();
        ws.WriteFile("b.icc", IccPrinterTestData.V2Lut16("Zeta baryta", 20));
        ws.WriteFile("a.ICM", IccPrinterTestData.V4LutAToB("alfa canvas", 30));   // the extension in capitals, as Windows often has it
        ws.WriteFile("m.icc", IccPrinterTestData.V2Lut16("Monitor", 20, deviceClass: "mntr"));
        ws.WriteFile("gunoi.icc", Encoding.ASCII.GetBytes(new string('x', 400)));
        ws.WriteFile("alt.txt", IccPrinterTestData.V2Lut16("Nu se vede", 20));

        IReadOnlyList<IccBlack> found = IccPaperBlack.Installed(ws.Dir);

        Assert.Equal(new[] { "alfa canvas", "Zeta baryta" }, found.Select(f => f.Description));
        Assert.Equal(new[] { 1.21, 1.52 }, found.Select(f => f.Dmax));
        Assert.Empty(IccPaperBlack.Installed(ws.PathTo("nu-exista")));
    }

    [Fact]
    public void R028_TheParametersTakeTheProfilesDmaxAndRememberItsName()
    {
        using var ws = new TempWorkspace();
        string icc = ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20));

        Parameters? p = new Parameters { Medium = PrintMedium.Canvas }.WithDmaxFromProfile(icc, out string? error);

        Assert.Null(error);
        Assert.Equal(1.52, p!.Dmax);
        Assert.Equal(PrintMedium.Canvas, p.Medium);
        Assert.Equal(" (din profilul ICC \"Hartie de test\")", ReportWriter.DmaxOrigin(p));

        Assert.Null(new Parameters().WithDmaxFromProfile(ws.PathTo("nu-exista.icc"), out error));
        Assert.StartsWith("fisierul nu se poate citi", error);
    }

    [Fact]
    public void R028_TheConsoleReadsTheDmaxFromIccAndRefusesWhatItCannotRead()
    {
        using var ws = new TempWorkspace();
        Raster r = Scenes.CleanSky();
        string tif = ws.WriteFile("curat.tif", TiffTestData.Build16Rgb(r.W, r.H, (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V2("sRGB test")));
        string icc = ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20));
        string notIcc = ws.WriteFile("note.icc", Encoding.ASCII.GetBytes(new string('x', 400)));

        var (code, output, err) = Run(tif, "--icc", icc);
        Assert.Equal("", err);
        Assert.NotEqual(1, code);
        Assert.Contains("Mediu: baryta (Dmax 1.52, negrul hartiei L* 20.1)", output);
        Assert.Contains("mediu baryta, Dmax 1.52 (din profilul ICC \"Hartie de test\")", output);

        (code, output, err) = Run(tif, "--icc", notIcc);
        Assert.Equal(1, code);
        Assert.Contains("EROARE: --icc: nu e un profil ICC", err);
        Assert.DoesNotContain("VERDICT", output);

        (code, _, err) = Run(tif, "--icc", icc, "--dmax", "2.0");
        Assert.Equal(1, code);
        Assert.Contains("EROARE: --icc si --dmax dau amandoua Dmax-ul hartiei; alege unul", err);
    }

    static (int Code, string Out, string Err) Run(params string[] args)
    {
        var o = new StringWriter();
        var e = new StringWriter();
        int code = AnalyzerCli.Run(args, o, e);
        return (code, o.ToString(), e.ToString());
    }
}
