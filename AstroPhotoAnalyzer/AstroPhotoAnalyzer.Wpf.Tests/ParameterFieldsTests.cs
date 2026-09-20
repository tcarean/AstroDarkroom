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
    public void TheDefaultsBuildTheDefaultParameters()
    {
        Parameters d = new();
        Parameters? p = new ParameterFields().TryBuild(out string? error);

        Assert.Null(error);
        Assert.Equal(d.Medium, p!.Medium);
        Assert.Null(p.DmaxOverride);
        Assert.Null(p.ViewingDistanceM);
        Assert.Equal(d.CandidateWidthsCm, p.CandidateWidthsCm);
        Assert.Null(p.TargetAspect);
        Assert.Equal(d.TargetBackground, p.TargetBackground);
        Assert.Equal(d.PassepartoutCm, p.PassepartoutCm);
        Assert.Equal(d.FrameCm, p.FrameCm);
    }

    [Fact]
    public void EveryFieldIsHonoured()
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
    public void ABadFieldGivesAMessageNotAnException(string field, string value, string fragment)
    {
        var f = new ParameterFields();
        typeof(ParameterFields).GetProperty(field)!.SetValue(f, value);

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(p);
        Assert.NotNull(error);
        Assert.Contains(fragment, error);
    }

    [Fact]
    public void APassepartoutNarrowerThanTwiceTheFrameIsRefusedWithTheCoresMessage()
    {
        var f = new ParameterFields { Passepartout = "5", Frame = "3" };

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(p);
        Assert.Equal(new Parameters { PassepartoutCm = 5, FrameCm = 3 }.Validate(), error);
    }

    [Fact]
    public void ZeroMeansNoPassepartoutAndIsAllowed()
    {
        var f = new ParameterFields { Passepartout = "0", Frame = "3" };

        Parameters? p = f.TryBuild(out string? error);

        Assert.Null(error);
        Assert.Equal(0, p!.PassepartoutCm);
    }

    [Fact]
    public void TheFramingFieldsCarryHelpText()
    {
        var f = new ParameterFields();

        Assert.Contains("dublul", f.PassepartoutHelp);
        Assert.Contains("3-4 cm", f.FrameHelp);
        Assert.Contains("Gol = raportul nativ", f.AspectHelp);
        Assert.Contains("formate fixe", f.AspectHelp);
    }

    [Fact]
    public void TheDistanceFieldExplainsTheEmptyCaseAndTheFactor()
    {
        var f = new ParameterFields();

        Assert.Contains("Gol = 1.5 x diagonala", f.DistanceHelp);
        Assert.Contains("nu se recomanda nicio marime", f.DistanceHelp);
        Assert.Contains("1-2 x diagonala", f.DistanceHelp);
    }

    /// <summary>
    /// Un singur bloc de 900 de caractere nu se citeste intr-un tooltip: fiecare text de
    /// ajutor e in paragrafe despartite de o linie goala, fara spatii la capete.
    /// </summary>
    [Fact]
    public void TheHelpTextsAreParagraphs()
    {
        var f = new ParameterFields();
        foreach (string help in new[] { f.DistanceHelp, f.AspectHelp, f.PassepartoutHelp, f.FrameHelp })
        {
            string[] paragraphs = help.Split("\n\n");
            Assert.True(paragraphs.Length >= 2, "un singur paragraf: " + help);
            Assert.All(paragraphs, p => Assert.Equal(p.Trim(), p));
            Assert.All(paragraphs, p => Assert.DoesNotContain("\n", p));
        }
    }

    [Fact]
    public void TheMediaListIsTheCoreOne()
    {
        Assert.Equal(new[] { "baryta", "canvas", "acrilic" }, new ParameterFields().Media);
    }
}
