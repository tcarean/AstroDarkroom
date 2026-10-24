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
    // The enum is internal, so the public tests receive it by name, not as a type.
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

    /// <summary>The framers' rule: the passepartout at least twice the frame; 0 = none, and not checked.</summary>
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
            Assert.Contains("6.0 cm", error);   // the minimum required for a frame of 3
        }
    }

    [Fact]
    public void R025_TheDefaultsValidate()
    {
        Assert.Null(new Parameters().Validate());
    }

    /// <summary>
    /// The target background is kept to three decimals, the ones the report prints, and refused
    /// by the one message of the core when, once rounded, it is not strictly inside (0, 1), or
    /// is NaN or infinite (ADR 0040).
    /// </summary>
    [Theory]
    [InlineData(0.1234567, 0.123, true)]
    [InlineData(0.19, 0.19, true)]
    [InlineData(0.0014, 0.001, true)]
    [InlineData(0.9994, 0.999, true)]
    [InlineData(0.0004, 0.0, false)]
    [InlineData(0.9996, 1.0, false)]
    [InlineData(0.0, 0.0, false)]
    [InlineData(1.0, 1.0, false)]
    [InlineData(-0.2, -0.2, false)]
    [InlineData(double.NaN, double.NaN, false)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity, false)]
    public void R023_TheTargetBackgroundIsRoundedToThreeDecimalsAndRefusedOutsideTheOpenInterval(double given, double stored, bool valid)
    {
        var p = new Parameters { TargetBackground = given };
        Assert.Equal(stored, p.TargetBackground!.Value);
        string error = p.Validate();
        // Validate judges the stored value and quotes it; the applications call TargetBackgroundError
        // on the typed one and quote that. Both refuse the same values.
        Assert.Equal(Parameters.TargetBackgroundError(stored), error);
        Assert.Equal(error == null, Parameters.TargetBackgroundError(given) == null);
        if (valid) Assert.Null(error);
        else
        {
            Assert.NotNull(error);
            Assert.StartsWith("fundalul-tinta trebuie sa fie un numar strict intre 0 si 1 (se rotunjeste la 3 zecimale); a primit ", error);
            Assert.DoesNotContain("--target-bg", error);
            if (double.IsNaN(given)) Assert.EndsWith("a primit NaN, care nu e un numar", error);
            if (double.IsPositiveInfinity(given)) Assert.EndsWith("a primit infinit", error);
            Assert.All(error, ch => Assert.True(ch < 128, "non-ASCII in the message: " + error));
        }
    }

    /// <summary>
    /// NaN and infinity read as numbers in both applications but no analysis can use them: the
    /// core's Validate refuses each number parameter with one message, which the console and
    /// the window show in the same words (R-024, ADR 0045). The aspect must also be positive:
    /// "1:Infinity" parses to 0.
    /// </summary>
    [Theory]
    [InlineData("Dmax", double.NaN, "Dmax trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("Dmax", double.PositiveInfinity, "Dmax trebuie sa fie un numar; a primit infinit")]
    [InlineData("Distance", double.NaN, "distanta de privire trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("Distance", double.NegativeInfinity, "distanta de privire trebuie sa fie un numar; a primit minus infinit")]
    [InlineData("Width", double.NaN, "latimea de print trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("Width", double.PositiveInfinity, "latimea de print trebuie sa fie un numar; a primit infinit")]
    [InlineData("Aspect", double.NaN, "raportul de aspect trebuie sa fie un numar pozitiv; a primit NaN, care nu e un numar")]
    [InlineData("Aspect", double.PositiveInfinity, "raportul de aspect trebuie sa fie un numar pozitiv; a primit infinit")]
    [InlineData("Aspect", 0.0, "raportul de aspect trebuie sa fie un numar pozitiv; a primit 0")]
    [InlineData("Passepartout", double.NaN, "passepartout-ul trebuie sa fie un numar; a primit NaN, care nu e un numar")]
    [InlineData("Frame", double.PositiveInfinity, "rama trebuie sa fie un numar; a primit infinit")]
    public void R024_NaNAndInfinityAreRefusedByValidateWithOneMessagePerParameter(string which, double value, string message)
    {
        string error = With(which, value).Validate();

        Assert.Equal(message, error);
        Assert.All(error, ch => Assert.True(ch < 128, "non-ASCII in the message: " + error));
        Assert.Null(With(which, 20).Validate());   // the same parameter, finite: nothing to say
    }

    /// <summary>
    /// The order of Validate's checks, which R-025 states: the target background's first, then
    /// the finiteness checks in the parameters' order, then the rule between the passepartout
    /// and the frame. The first rule broken is the message, so the order is what the user reads.
    /// </summary>
    [Fact]
    public void R025_ValidateChecksTheTargetBackgroundThenFinitenessThenThePassepartoutRule()
    {
        // The target background before Dmax.
        Assert.StartsWith("fundalul-tinta", new Parameters { TargetBackground = 1, DmaxOverride = double.NaN }.Validate());
        // A width before the aspect, and Dmax before the distance.
        Assert.StartsWith("latimea de print", new Parameters { CandidateWidthsCm = new[] { double.NaN }, TargetAspect = 0 }.Validate());
        Assert.StartsWith("Dmax", new Parameters { DmaxOverride = double.NaN, ViewingDistanceM = double.PositiveInfinity }.Validate());
        // The finiteness block before the passepartout rule: 4 cm under twice a 3 cm frame.
        Assert.StartsWith("Dmax", new Parameters { DmaxOverride = double.NaN, PassepartoutCm = 4, FrameCm = 3 }.Validate());
        Assert.StartsWith("passepartout-ul (4.0 cm)", new Parameters { PassepartoutCm = 4, FrameCm = 3 }.Validate());
    }

    static Parameters With(string which, double value) => which switch
    {
        "Dmax" => new Parameters { DmaxOverride = value },
        "Distance" => new Parameters { ViewingDistanceM = value },
        "Width" => new Parameters { CandidateWidthsCm = new[] { 60, value } },
        "Aspect" => new Parameters { TargetAspect = value },
        "Passepartout" => new Parameters { PassepartoutCm = value },
        "Frame" => new Parameters { FrameCm = value, PassepartoutCm = 0 },   // 0 = no passepartout, so the rule between the two stays out
        _ => throw new ArgumentException(which),
    };
}
