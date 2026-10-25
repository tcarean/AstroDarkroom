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
/// Formatarea componentei radiale. Semnul ei E diagnosticul - pozitiv inseamna
/// sub-corectie, negativ supra-corectie - deci se scrie mereu explicit.
///
/// Valorile asteptate sunt cele pe care le produce Python cu "%+6.2f", verificate rulând
/// formatul acolo: flatcheck.py e oracolul, si cele doua trebuie sa scrie la fel.
/// </summary>
public class LightReportTests
{
    [Theory]
    [InlineData(0.0, "+0.00")]
    [InlineData(46.86, "+46.86")]
    [InlineData(-44.4, "-44.40")]
    [InlineData(-0.5, "-0.50")]
    [InlineData(123.456, "+123.46")]
    [InlineData(0.004, "+0.00")]      // pozitiv mic: rotunjeste la zero, semnul ramane +
    [InlineData(-0.004, "-0.00")]     // negativ mic: rotunjeste la zero, semnul ramane -
    public void SignIsAlwaysExplicit(double value, string expected)
    {
        Assert.Equal(expected, LightReport.Signed(value));
    }

    /// <summary>
    /// Regresie. Formatul cu sectiuni "+0.00;-0.00" dadea "-+0.00" pentru zero negativ:
    /// .NET alege sectiunea pozitiva (fiindca -0.0 nu e sub zero) dar tot pune minusul.
    /// Se intampla fix pe cazul bun - un fond fara componenta radiala - iar Python scria
    /// "-0.00" acolo, deci era si o divergenta intre implementari pe care crosscheck.py
    /// n-o prindea, ambele parsandu-se ca 0.
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

    /// <summary>Un negativ prea mic ca sa se vada la doua zecimale isi pastreaza semnul.</summary>
    [Fact]
    public void ATinyNegativeKeepsItsSign()
    {
        Assert.Equal("-0.00", LightReport.Signed(-1e-15));
    }

    /// <summary>Iesirea completa, ca sa se vada ca alinierea nu s-a stricat.</summary>
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
    /// Cand campul nu s-a putut afla, raportul spune asta explicit in loc sa taca.
    /// Tacerea ar lasa impresia ca s-a verificat si e in regula.
    /// </summary>
    [Fact]
    public void AnUnknownFieldOfViewIsStatedInTheReport()
    {
        var w = new StringWriter();

        LightReport.WriteSuitability(Unknown, w);

        Assert.Contains("camp acoperit           : necunoscut", w.ToString());
        Assert.Contains("nu pot verifica daca testul se aplica", w.ToString());
    }

    /// <summary>Pe camp la limita se ruleaza, dar cifra vine cu un avertisment.</summary>
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
    /// Refuzul nu e un raport prescurtat: nu contine nicio cifra de componenta radiala,
    /// tocmai fiindca acolo nu se poate masura una.
    /// </summary>
    [Fact]
    public void TheRefusalExplainsTheDegeneracyAndPointsAtTheRatioTest()
    {
        var s = new SuitabilityResult { Level = FovLevel.TooWide, FovDeg = 70.4, Source = "WCS" };
        var w = new StringWriter();

        LightReport.WriteTooWide(s, "light.fit", w);
        string text = w.ToString();

        Assert.Contains("TESTUL NU SE APLICA", text);
        Assert.Contains("degenerare geometrica", text);
        Assert.Contains("--ratio", text);
        Assert.DoesNotContain("componenta radiala", text);
    }

    /// <summary>
    /// The window's field line, warning and refusal live here with the console's: they quote
    /// the field limits from Th, and the refusal names the Ratio tab where the console names
    /// --ratio.
    /// </summary>
    [Fact]
    public void TheWindowsFieldTextsQuoteThAndTheRefusalNamesTheRatioTab()
    {
        var marginal = new SuitabilityResult { Level = FovLevel.Marginal, FovDeg = 30.0, Source = "WCS" };
        var ok = new SuitabilityResult { Level = FovLevel.Ok, FovDeg = 12.0, Source = "WCS" };

        Assert.Equal("30.0 grade  (WCS)", LightReport.FieldText(marginal));
        Assert.Contains($"peste {Th.LightFovOk:F0} grade", LightReport.FieldWarning(marginal));
        Assert.Equal("", LightReport.FieldWarning(ok));
        Assert.Contains("necunoscut inseamna neverificat", LightReport.FieldWarning(Unknown));

        string refusal = LightReport.TooWideText(new SuitabilityResult { Level = FovLevel.TooWide, FovDeg = 70.4, Source = "WCS" });
        Assert.Contains($"Peste {Th.LightFovMax:F0} de grade", refusal);
        Assert.Contains("tabul Ratio", refusal);
        Assert.DoesNotContain("--ratio", refusal);
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
