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

namespace FlatCheck.Tests;

/// <summary>
/// The formatting of the radial component. Its sign IS the diagnosis - positive means
/// under-correction, negative over-correction - so it is always written explicitly.
///
/// The expected values are the ones Python produces with "%+6.2f", checked by running
/// the format there: flatcheck.py was the oracle until it froze at 2.2.0 (ADR 0047), and
/// the two wrote the same.
/// </summary>
public class LightReportTests
{
    [Theory]
    [InlineData(0.0, "+0.00")]
    [InlineData(46.86, "+46.86")]
    [InlineData(-44.4, "-44.40")]
    [InlineData(-0.5, "-0.50")]
    [InlineData(123.456, "+123.46")]
    [InlineData(0.004, "+0.00")]      // small positive: rounds to zero, the sign stays +
    [InlineData(-0.004, "-0.00")]     // small negative: rounds to zero, the sign stays -
    public void SignIsAlwaysExplicit(double value, string expected)
    {
        Assert.Equal(expected, LightReport.Signed(value));
    }

    /// <summary>
    /// Regression. The format with sections "+0.00;-0.00" gave "-+0.00" for negative zero:
    /// .NET picks the positive section (because -0.0 is not below zero) but still puts the
    /// minus. It happens exactly on the good case - a background without a radial
    /// component - and Python wrote "-0.00" there, so there was also a divergence between
    /// the implementations that crosscheck.py did not catch, both parsing as 0.
    /// </summary>
    [Fact]
    public void NegativeZeroRendersAsMinusZero_NotMinusPlusZero()
    {
        string s = LightReport.Signed(-0.0);

        Assert.Equal("-0.00", s);
        Assert.DoesNotContain("+", s);
    }

    [Fact]
    public void PositiveZeroRendersAsPlusZero()
    {
        Assert.Equal("+0.00", LightReport.Signed(0.0));
    }

    /// <summary>A negative too small to show at two decimals keeps its sign.</summary>
    [Fact]
    public void ATinyNegativeKeepsItsSign()
    {
        Assert.Equal("-0.00", LightReport.Signed(-1e-15));
    }

    /// <summary>The complete output, to show that the alignment did not break.</summary>
    [Fact]
    public void TheRadialLineIsAlignedToSixColumns()
    {
        var result = new LightResult
        {
            RadPct = -0.0,
            PlanePct = 1.0,
            KeptTiles = 10,
            TotalTiles = 10,
            Verdict = "OK",
        };
        var w = new StringWriter();

        LightReport.Write(result, Unknown, "f.fit", w);

        Assert.Contains("componenta radiala      :  -0.00 %   [OK]", w.ToString());
    }

    /// <summary>
    /// When the field could not be found, the report says so explicitly instead of staying
    /// silent. Silence would give the impression that it was checked and is fine.
    /// </summary>
    [Fact]
    public void AnUnknownFieldOfViewIsStatedInTheReport()
    {
        var w = new StringWriter();

        LightReport.WriteSuitability(Unknown, w);

        Assert.Contains("camp acoperit           : necunoscut", w.ToString());
        Assert.Contains("nu pot verifica daca testul se aplica", w.ToString());
    }

    /// <summary>On a field at the limit it runs, but the figure comes with a
    /// warning.</summary>
    [Fact]
    public void AMarginalFieldOfViewWarnsButStillReports()
    {
        var s = new SuitabilityResult { Level = FovLevel.Marginal, FovDeg = 30.0, Source = "WCS" };
        var w = new StringWriter();

        LightReport.WriteSuitability(s, w);

        Assert.Contains("camp diagonal           :  30.0 grade  (WCS)", w.ToString());
        Assert.Contains("ATENTIE: camp la limita", w.ToString());
    }

    /// <summary>
    /// The refusal is not a shortened report: it holds no radial component figure,
    /// precisely because none can be measured there.
    /// </summary>
    [Fact]
    public void TheRefusalExplainsTheDegeneracyAndPointsAtTheStructureTests()
    {
        var s = new SuitabilityResult { Level = FovLevel.TooWide, FovDeg = 70.4, Source = "WCS" };
        var w = new StringWriter();

        LightReport.WriteTooWide(s, "light.fit", w);
        string text = w.ToString();

        Assert.Contains("TESTUL NU SE APLICA", text);
        Assert.Contains("degenerare geometrica", text);
        Assert.Contains("testele de structura", text);
        Assert.DoesNotContain("componenta radiala", text);
    }

    /// <summary>
    /// The window's field line, warning and refusal live here with the console's: they quote
    /// the field limits from Th, and the refusal names the Structure tab where the console names
    /// the structure tests.
    /// </summary>
    [Fact]
    public void TheWindowsFieldTextsQuoteThAndTheRefusalNamesTheStructureTab()
    {
        var marginal = new SuitabilityResult { Level = FovLevel.Marginal, FovDeg = 30.0, Source = "WCS" };
        var ok = new SuitabilityResult { Level = FovLevel.Ok, FovDeg = 12.0, Source = "WCS" };

        Assert.Equal("30.0 grade  (WCS)", LightReport.FieldText(marginal));
        Assert.Contains($"peste {Th.LightFovOk:F0} grade", LightReport.FieldWarning(marginal));
        Assert.Equal("", LightReport.FieldWarning(ok));
        Assert.Contains("necunoscut inseamna neverificat", LightReport.FieldWarning(Unknown));

        string refusal = LightReport.TooWideText(new SuitabilityResult { Level = FovLevel.TooWide, FovDeg = 70.4, Source = "WCS" });
        Assert.Contains($"Peste {Th.LightFovMax:F0} de grade", refusal);
        Assert.Contains("tabul Structura", refusal);
    }

    /// <summary>
    /// The window's field line is the console's value, unpadded, in each of the three cases the
    /// console distinguishes: the field known, only the focal length known, nothing known.
    /// </summary>
    [Fact]
    public void TheWindowsFieldTextIsTheConsolesValueUnpadded()
    {
        var known = new SuitabilityResult { Level = FovLevel.Ok, FovDeg = 8.0, Source = "WCS" };
        var w = new StringWriter();
        LightReport.WriteSuitability(known, w);
        Assert.Contains("camp diagonal           :   8.0 grade  (WCS)", w.ToString());
        Assert.Equal("8.0 grade  (WCS)", LightReport.FieldText(known));

        w = new StringWriter();
        LightReport.WriteSuitability(FocalOnly, w);
        Assert.Contains("camp acoperit           : nedeterminat (FOCALLEN=20mm, fara marimea pixelului)", w.ToString());
        Assert.Equal("nedeterminat (FOCALLEN=20mm, fara marimea pixelului)", LightReport.FieldText(FocalOnly));

        Assert.Equal("necunoscut (headerul nu da nici focala, nici scara)", LightReport.FieldText(Unknown));
        Assert.Equal("", LightReport.FieldText(null));
    }

    /// <summary>
    /// A refusal judged from the focal length alone says so in both applications - the decision
    /// assumed the largest plausible sensor - and only the console adds the --fov way out, which
    /// the window has no control for. With the field known, neither says it.
    /// </summary>
    [Fact]
    public void ARefusalOnTheFocalLengthAloneSaysSoInBothApplications()
    {
        var w = new StringWriter();
        LightReport.WriteTooWide(FocalOnly, "light.fit", w);
        Assert.Contains("Decizia s-a luat doar pe focala, presupunand cel mai mare senzor", w.ToString());
        Assert.Contains("--fov <grade>", w.ToString());

        string window = LightReport.TooWideText(FocalOnly);
        Assert.Contains("Decizia s-a luat doar pe focala, presupunand cel mai mare senzor plauzibil.", window);
        Assert.DoesNotContain("--fov", window);

        var known = new SuitabilityResult { Level = FovLevel.TooWide, FovDeg = 70.4, Source = "WCS" };
        Assert.DoesNotContain("doar pe focala", LightReport.TooWideText(known));
        w = new StringWriter();
        LightReport.WriteTooWide(known, "light.fit", w);
        Assert.DoesNotContain("doar pe focala", w.ToString());
    }

    static SuitabilityResult Unknown => new() { Level = FovLevel.Unknown };

    /// <summary>What LightSuitability answers on a DSLR light through Siril: FOCALLEN, no
    /// pixel size, judged on the full-frame assumption.</summary>
    static SuitabilityResult FocalOnly => new()
    {
        Level = FovLevel.TooWide,
        FocalMm = 20,
        Source = "FOCALLEN=20mm, fara marimea pixelului",
    };
}
