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

namespace AstroPhotoAnalyzer.Wpf.Tests;

public class ParameterFieldsTests
{
    [Fact]
    public void R024_TheDefaultsBuildTheDefaultParameters()
    {
        Parameters d = new();
        Parameters? p = new ParameterFields().TryBuild(out string? error);

        Assert.Null(error);
        Assert.Equal(d.Medium, p!.Medium);
        Assert.Null(p.DmaxOverride);   // the field shows the medium's Dmax; untouched, it is not an override
        Assert.Equal(d.Dmax, p.Dmax);
        Assert.Null(p.ViewingDistanceM);
        Assert.Equal(d.CandidateWidthsCm, p.CandidateWidthsCm);
        Assert.Null(p.TargetAspect);
        Assert.Equal(d.TargetBackground, p.TargetBackground);
        Assert.Equal(d.PassepartoutCm, p.PassepartoutCm);
        Assert.Equal(d.FrameCm, p.FrameCm);
    }

    /// <summary>
    /// The Dmax field always shows the Dmax in use: the chosen medium's, from the core, until
    /// the user types another; choosing a medium writes over whatever the field held.
    /// </summary>
    [Fact]
    public void R020_ChoosingAMediumFillsTheDmaxFieldWithItsDmax()
    {
        var f = new ParameterFields();
        Assert.Equal("2.4", f.Dmax);

        f.Medium = "canvas";
        Assert.Equal("1.5", f.Dmax);

        f.Dmax = "1.7";
        f.Medium = "acrilic";
        Assert.Equal("2.2", f.Dmax);
        Assert.Null(f.TryBuild(out _)!.DmaxOverride);   // untouched: the core's own value, not a copy of its text

        f.Medium = "Canvas";   // from code, in another case: still the medium, still its Dmax
        Assert.Equal("1.5", f.Dmax);
        f.Medium = "acrilic";

        f.Dmax = "2.35";
        Parameters? typed = f.TryBuild(out _);
        Assert.Equal(2.35, typed!.Dmax);
        Assert.True(typed.DmaxIsExplicit);

        f.Dmax = "";   // emptied by hand: the medium's
        Parameters? empty = f.TryBuild(out string? error);
        Assert.Null(error);
        Assert.Null(empty!.DmaxOverride);
        Assert.Equal(2.2, empty.Dmax);
    }

    [Fact]
    public void R024_EveryFieldIsHonoured()
    {
        // 8 cu 4: passepartout-ul trebuie sa fie cel putin dublul ramei (Parameters.Validate).
        var f = new ParameterFields { Medium = "canvas", Dmax = "1.7", Distance = "2", Sizes = "60, 70;80", Aspect = "50x35", TargetBg = "0.2", Passepartout = "8", Frame = "4" };
        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(error);
        Assert.Equal(PrintMedium.Canvas, p!.Medium);
        Assert.Equal(1.7, p.Dmax);
        Assert.Equal(2, p.ViewingDistanceM);
        Assert.Equal(new double[] { 60, 70, 80 }, p.CandidateWidthsCm);
        Assert.Equal(50.0 / 35.0, p.TargetAspect!.Value, 9);
        Assert.Equal(0.2, p.TargetBackground);
        Assert.Equal(8, p.PassepartoutCm);
        Assert.Equal(4, p.FrameCm);
    }

    [Theory]
    [InlineData("Dmax", "1,5", "punctul")]
    [InlineData("Dmax", "9", "intervalului")]
    [InlineData("Distance", "abc", "numar")]
    [InlineData("Sizes", "", "cel putin o latime")]
    [InlineData("Sizes", "60,x", "latime")]
    [InlineData("Aspect", "50", "latime:inaltime")]
    [InlineData("TargetBg", "1.5", "intervalului")]
    public void R024_ABadFieldGivesAMessageNotAnException(string field, string value, string fragment)
    {
        var f = new ParameterFields();
        typeof(ParameterFields).GetProperty(field)!.SetValue(f, value);

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(p);
        Assert.NotNull(error);
        Assert.Contains(fragment, error);
    }

    [Fact]
    public void R025_APassepartoutNarrowerThanTwiceTheFrameIsRefusedWithTheCoresMessage()
    {
        var f = new ParameterFields { Passepartout = "5", Frame = "3" };

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(p);
        Assert.Equal(new Parameters { PassepartoutCm = 5, FrameCm = 3 }.Validate(), error);
    }

    [Fact]
    public void R025_ZeroMeansNoPassepartoutAndIsAllowed()
    {
        var f = new ParameterFields { Passepartout = "0", Frame = "3" };

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(error);
        Assert.Equal(0, p!.PassepartoutCm);
    }

    [Fact]
    public void R026_TheFramingFieldsCarryHelpText()
    {
        var f = new ParameterFields();

        Assert.Contains("dublul", f.PassepartoutHelp);
        Assert.Contains("3-4 cm", f.FrameHelp);
        Assert.Contains("Gol = raportul nativ", f.AspectHelp);
        Assert.Contains("formate fixe", f.AspectHelp);
    }

    [Fact]
    public void R026_TheDistanceFieldExplainsTheEmptyCaseAndTheFactor()
    {
        var f = new ParameterFields();

        Assert.Contains("Gol = 1.5 x diagonala", f.DistanceHelp);
        Assert.Contains("nu se recomanda nicio marime", f.DistanceHelp);
        Assert.Contains("1-2 x diagonala", f.DistanceHelp);
    }

    /// <summary>
    /// Tinta se explica prin ce face simularea cu ea, si cifrele din text sunt cele
    /// din nucleu, nu tastate: implicitul, pragul print-safe, tintele suplimentare.
    /// </summary>
    [Fact]
    public void R026_TheTargetBackgroundFieldExplainsTheSimulationAndTheDefault()
    {
        var f = new ParameterFields();

        Assert.Contains("gamma = ln(tinta) / ln(fundal)", f.TargetBgHelp);
        Assert.Contains("mediana canalului verde", f.TargetBgHelp);
        Assert.Contains("nu stretch-ul lui Siril", f.TargetBgHelp);
        Assert.Contains("grila ei 0.10..0.20", f.TargetBgHelp);
        Assert.Contains("\"(dat)\"", f.TargetBgHelp);
        Assert.Contains("0.15-0.25", f.TargetBgHelp);
        Assert.Contains("nivelul minim", f.TargetBgHelp);
        Assert.Equal("", f.TargetBg);   // gol: doar grila
    }

    /// <summary>R-027: the WCS field lends a FITS's header; empty means none, and the help says what it is for.</summary>
    [Fact]
    public void R027_TheWcsFieldIsOptionalAndExplained()
    {
        var f = new ParameterFields();
        Assert.Equal("", f.WcsPath);
        Assert.Null(f.TryBuild(out _)!.WcsPath);

        f.WcsPath = "  E:\\stack.fit ";
        Assert.Equal("E:\\stack.fit", f.TryBuild(out _)!.WcsPath);

        Assert.Contains("plate-solved", f.WcsHelp);
        Assert.Contains("aceeasi latime si inaltime", f.WcsHelp);
        Assert.Contains("catalogul Lynds", f.WcsHelp);
        Assert.Contains("cu rosu", f.WcsHelp);
    }

    /// <summary>
    /// Un singur bloc de 900 de caractere nu se citeste intr-un tooltip: fiecare text de
    /// ajutor e in paragrafe despartite de o linie goala, fara spatii la capete.
    /// </summary>
    [Fact]
    public void R026_TheHelpTextsAreParagraphs()
    {
        var f = new ParameterFields();
        foreach (string help in new[] { f.DistanceHelp, f.AspectHelp, f.TargetBgHelp, f.PassepartoutHelp, f.FrameHelp, f.WcsHelp })
        {
            string[] paragraphs = help.Split("\n\n");
            Assert.True(paragraphs.Length >= 2, "un singur paragraf: " + help);
            Assert.All(paragraphs, p => Assert.Equal(p.Trim(), p));
            Assert.All(paragraphs, p => Assert.DoesNotContain("\n", p));
        }
    }

    [Fact]
    public void R020_TheMediaListIsTheCoreOne()
    {
        Assert.Equal(new[] { "baryta", "canvas", "acrilic" }, new ParameterFields().Media);
    }
}
