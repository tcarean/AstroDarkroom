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

        LightReport.Write(result, "f.fit", w);

        Assert.Contains("componenta radiala      :  -0.00 %   [OK]", w.ToString());
    }
}
