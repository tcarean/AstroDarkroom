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

public class ParametersTests
{
    // Enum-ul e internal, deci testele publice il primesc pe nume, nu ca tip.
    [Theory]
    [InlineData("baryta", 2.4)]
    [InlineData("canvas", 1.5)]
    [InlineData("acrilic", 2.2)]
    public void R020_EachMediumHasItsDmax(string medium, double dmax)
    {
        Assert.Equal(dmax, new Parameters { Medium = Parameters.MediumFromName(medium) }.Dmax);
    }

    [Fact]
    public void R020_AnExplicitDmaxBeatsTheMedium()
    {
        Assert.Equal(1.8, new Parameters { Medium = PrintMedium.Baryta, DmaxOverride = 1.8 }.Dmax);
    }

    /// <summary>
    /// The window shows its Dmax field filled with the chosen medium's Dmax, so a Dmax that
    /// equals the medium's arrives as an override; it is not one, and the report must not say so.
    /// </summary>
    [Fact]
    public void R020_ADmaxEqualToTheMediumsIsNotExplicit()
    {
        Assert.False(new Parameters().DmaxIsExplicit);
        Assert.False(new Parameters { Medium = PrintMedium.Canvas, DmaxOverride = 1.5 }.DmaxIsExplicit);
        Assert.True(new Parameters { Medium = PrintMedium.Canvas, DmaxOverride = 2.4 }.DmaxIsExplicit);

        var given = new StringWriter();
        ReportWriter.WriteBody(AnalysisRun.Execute(Scenes.CleanSky(), "memorie", new Parameters { DmaxOverride = 1.8 }), given);
        Assert.Contains("Dmax 1.8 (explicit)", given.ToString());

        var same = new StringWriter();
        ReportWriter.WriteBody(AnalysisRun.Execute(Scenes.CleanSky(), "memorie", new Parameters { DmaxOverride = 2.4 }), same);
        Assert.DoesNotContain("(explicit)", same.ToString());
    }

    /// <summary>One list of names, the core's, in the enum's order; the console and the window both read it.</summary>
    [Fact]
    public void R020_TheMediumNamesAreTheCoresAndRoundTrip()
    {
        Assert.Equal(new[] { "baryta", "canvas", "acrilic" }, Parameters.MediumNames);
        Assert.Equal(Parameters.MediumNames, Options.Media);
        Assert.All(Parameters.MediumNames, n => Assert.Equal(n, Parameters.NameOf(Parameters.MediumFromName(n))));
    }

    /// <summary>
    /// A Dmax keeps its second decimal wherever the report prints it: with one decimal,
    /// `--dmax 2.35` read "Dmax 2.4" in the header and in Anexa F, the Dmax of another paper.
    /// </summary>
    [Fact]
    public void R101_ADmaxKeepsItsSecondDecimalInTheHeaderAndInTheParameterAppendix()
    {
        var w = new StringWriter();
        ReportWriter.WriteBody(AnalysisRun.Execute(Scenes.CleanSky(), "memorie", new Parameters { DmaxOverride = 2.35 }), w);

        Assert.Contains("Mediu: baryta (Dmax 2.35, negrul hartiei L* 4.0)", w.ToString());
        Assert.Contains("mediu baryta, Dmax 2.35 (explicit)", w.ToString());
    }

    [Fact]
    public void R021_TheDefaultsAreTheOnesFromTheConversation()
    {
        var p = new Parameters();

        Assert.Equal(PrintMedium.Baryta, p.Medium);
        Assert.Equal(new double[] { 50, 60, 70, 80, 90, 100 }, p.CandidateWidthsCm);
        Assert.Null(p.ViewingDistanceM);
        Assert.Equal(7, p.PassepartoutCm);
        Assert.Equal(3, p.FrameCm);
        Assert.Null(p.TargetAspect);
        Assert.Null(p.TargetBackground);
        Assert.Equal(16, p.SimulationTargets.Length);
        Assert.Equal(0.05, p.SimulationTargets[0]);
        Assert.Equal(0.20, p.SimulationTargets[15]);
        Assert.Null(p.WcsPath);
        Assert.Equal(0, p.Seed);
    }

    [Theory]
    [InlineData("baryta", "baryta")]
    [InlineData("Canvas", "canvas")]
    [InlineData("ACRILIC", "acrilic")]
    public void R020_MediumNamesRoundTripCaseInsensitively(string typed, string canonical)
    {
        Assert.Equal(canonical, Parameters.NameOf(Parameters.MediumFromName(typed)));
    }

    [Fact]
    public void R020_TheThreeMediaAreDistinct()
    {
        Assert.NotEqual(Parameters.MediumFromName("baryta"), Parameters.MediumFromName("canvas"));
        Assert.NotEqual(Parameters.MediumFromName("canvas"), Parameters.MediumFromName("acrilic"));
    }

    [Fact]
    public void R020_AnUnknownMediumNameIsAnArgumentError()
    {
        Assert.Throws<ArgumentException>(() => Parameters.MediumFromName("hartie"));
    }

    /// <summary>Regula inramatorilor: passepartout-ul cel putin dublul ramei; 0 = fara, si nu se verifica.</summary>
    [Theory]
    [InlineData(7, 3, true)]
    [InlineData(6, 3, true)]
    [InlineData(5, 3, false)]
    [InlineData(4, 2, true)]
    [InlineData(0, 3, true)]
    [InlineData(5, 0, true)]
    public void R025_ThePassepartoutMustBeAtLeastTwiceTheFrame(double passepartout, double frame, bool valid)
    {
        string error = new Parameters { PassepartoutCm = passepartout, FrameCm = frame }.Validate();

        if (valid) Assert.Null(error);
        else
        {
            Assert.Contains("dublul", error);
            Assert.Contains("6.0 cm", error);   // minimul cerut pentru rama de 3
        }
    }

    [Fact]
    public void R025_TheDefaultsValidate()
    {
        Assert.Null(new Parameters().Validate());
    }
}
