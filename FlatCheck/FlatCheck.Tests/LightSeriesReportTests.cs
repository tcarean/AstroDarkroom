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
/// The text of the series report.
///
/// Two things matter here and are not covered by the analyzer's tests. The first: when
/// the verdict is NEDETERMINAT, the report must say WHICH condition failed - "I do not
/// know" without a reason is worse than a wrong number, because it leaves the user
/// nothing to do. The second: the report must not let slip a quality verdict when it is
/// not allowed to give one, nor refuse when the series is clean.
/// </summary>
public class LightSeriesReportTests
{
    static (string, LightResult) Frame(string name, double rad, double plane) =>
        (name, new LightResult
        {
            RadPct = rad,
            PlanePct = plane,
            KeptTiles = 984,
            TotalTiles = 1080,
            RadialMap = TestImages.From(4, 4, (x, y) => 1.0),
            Verdict = Stats.Verdict(Math.Abs(rad), Th.LightRadOk, Th.LightRadBad),
        });

    static string Summary(List<(string, LightResult)> frames)
    {
        var w = new StringWriter();
        LightSeriesReport.WriteSummary(LightSeriesAnalyzer.Combine(frames), w);
        return w.ToString();
    }

    static List<(string, LightResult)> Steady(int n, double mean, double jitter = 0.1)
        => Enumerable.Range(0, n)
            .Select(i => Frame($"f{i:00}.fit", mean + (i % 2 == 0 ? jitter : -jitter), 10.0))
            .ToList();

    // ------------------------------------------------------------ header and rows

    [Fact]
    public void TheHeaderNamesTheFolderThePatternAndTheCount()
    {
        var w = new StringWriter();

        LightSeriesReport.WriteHeader(
            "process", "pp_light_*.fit", 32,
            new SuitabilityResult { Level = FovLevel.Ok, FovDeg = 12.0, Source = "WCS" }, w);
        string text = w.ToString();

        Assert.Contains("=== TEST END-TO-END (SERIE): process ===", text);
        Assert.Contains("cadre gasite            : 32  (tipar pp_light_*.fit)", text);
        Assert.Contains("camp diagonal           :  12.0 grade  (WCS)", text);
        // the input requirement differs from a single frame's: also UNREGISTERED
        Assert.Contains("NEREGISTRATE", text);
    }

    [Fact]
    public void EachRowCarriesTheRadialTheSkyGradientAndTheTileCount()
    {
        var w = new StringWriter();

        LightSeriesReport.WriteRow("pp_light_00001.fit", Frame("x", 3.13, 77.01).Item2, w);

        Assert.Contains("pp_light_00001.fit", w.ToString());
        Assert.Contains("+3.13 %", w.ToString());
        Assert.Contains("77.01 %", w.ToString());
        Assert.Contains("984/1080", w.ToString());
    }

    /// <summary>A long name is shortened from the left, so that the frame number stays
    /// visible.</summary>
    [Fact]
    public void ALongFileNameIsTrimmedFromTheLeft()
    {
        var w = new StringWriter();

        LightSeriesReport.WriteRow(
            "un_nume_absurd_de_lung_pentru_o_singura_coloana_00042.fit",
            Frame("x", 1.0, 10).Item2, w);

        Assert.Contains("_00042.fit", w.ToString());
        Assert.Contains("...", w.ToString());
    }

    // ------------------------------------------------------------ conclusive

    [Fact]
    public void AConclusiveSeriesReportsTheVerdictAndTheDiagnosis()
    {
        string text = Summary(Steady(10, mean: 0.4));

        Assert.Contains("VERDICT: OK", text);
        Assert.Contains("Flat-ul face treaba", text);
        Assert.Contains("Media pe 10 cadre e estimarea erorii de flat", text);
        Assert.DoesNotContain("NEDETERMINAT", text);
    }

    /// <summary>The sign of the mean is the diagnosis, just as for a single frame.</summary>
    [Fact]
    public void TheSignOfTheMeanIsReadAsTheDiagnosis()
    {
        Assert.Contains("SUB-corectie", Summary(Steady(10, mean: 3.5)));
        Assert.Contains("SUPRA-corectie", Summary(Steady(10, mean: -3.5)));
    }

    // ------------------------------------------------------------ inconclusive

    /// <summary>
    /// The real figures of the session that motivated the mode: the mean is +0.23, that is
    /// it would have passed as OK, but both conditions fail. The report must say both.
    /// </summary>
    [Fact]
    public void BothFailedConditionsAreSpelledOut()
    {
        double[] rad = { 3.13, -8.0, -19.20, -4.0, 12.70, 24.18, 35.73, -14.86 };
        double[] plane = { 77.0, 82.0, 80.0, 79.0, 70.0, 61.0, 51.0, 80.0 };
        string text = Summary(rad.Select((v, i) => Frame($"f{i}.fit", v, plane[i])).ToList());

        Assert.Contains("VERDICT: NEDETERMINAT", text);
        Assert.Contains("Media NU se poate citi ca eroare de flat", text);
        Assert.Contains("eroarea mediei", text);
        Assert.Contains("urmareste gradientul de cer", text);
        // and points to the test that really answers on such data
        Assert.Contains("--ratio", text);
    }

    /// <summary>
    /// The underlying explanation: the flat is constant by construction, so what moves is
    /// the sky. Without this sentence, "nedeterminat" stays an unfounded claim.
    /// </summary>
    [Fact]
    public void TheRefusalExplainsWhyMovementMeansSky()
    {
        string text = Summary(Steady(10, mean: 0.0, jitter: 30.0));

        Assert.Contains("Flat-ul e acelasi pe toate cadrele prin constructie", text);
        Assert.Contains("tot ce se misca in timpul seriei e cer", text);
    }

    [Fact]
    public void TooFewFramesIsReportedAsItsOwnReason()
    {
        string text = Summary(Steady(Th.LightSeriesMinFrames - 1, mean: 0.4));

        Assert.Contains("VERDICT: NEDETERMINAT", text);
        Assert.Contains("prea putine cadre", text);
    }

    // ------------------------------------------------------------ the correlation

    /// <summary>
    /// When the sky gradient does not vary, the line says the test was NOT applied, instead
    /// of printing a meaningless correlation. Otherwise it would look like a measurement.
    /// </summary>
    [Fact]
    public void AConstantSkyGradientSaysTheTestDidNotApply()
    {
        string text = Summary(Steady(10, mean: 0.4));

        Assert.Contains("corelatie cu grad. de cer : nu se aplica", text);
        Assert.Contains("gradientul variaza doar", text);
    }

    [Fact]
    public void TheFailingThresholdIsPrintedNextToTheNumberThatFailedIt()
    {
        string text = Summary(Steady(10, mean: 0.0, jitter: 30.0));

        Assert.Contains($"<- peste pragul de {Th.LightSeriesSemMax:F2}", text);
    }

    /// <summary>
    /// The window's reasons are the console's, joined into paragraphs and capitalised, and close
    /// on the Ratio tab where the console names --ratio; its correlation figure is the
    /// console's "nu se aplica" when the sky gradient does not vary.
    /// </summary>
    [Fact]
    public void TheWindowsReasonsAndCorrelationAreTheConsolesWords()
    {
        LightSeriesResult r = LightSeriesAnalyzer.Combine(Steady(10, mean: 0.0, jitter: 30.0));
        string reasons = LightSeriesReport.ReasonsText(r);

        Assert.StartsWith($"Eroarea mediei ({r.Sem:F2}) depaseste chiar pragul de OK ({Th.LightRadOk:F2}), ", reasons);
        Assert.Contains("Flat-ul e acelasi pe toate cadrele prin constructie - s-a impartit din fiecare.", reasons);
        Assert.Contains("tabul Ratio", reasons);
        Assert.DoesNotContain("--ratio", reasons);
        Assert.StartsWith("nu se aplica (gradientul variaza doar", LightSeriesReport.CorrelationValue(r));
    }
}
