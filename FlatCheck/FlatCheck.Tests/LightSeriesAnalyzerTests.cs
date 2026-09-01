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
/// Combinarea unei serii de rezultate --light intr-un singur verdict.
///
/// Ideea pe care o apara fisierul asta: media pe serie estimeaza eroarea de flat DOAR
/// daca termenul radial al cerului se mediaza la zero, si asta nu e garantat. Deci se
/// verifica, prin trei conditii, iar daca vreuna pica raspunsul e NEDETERMINAT in loc de
/// un numar care arata increzator. Testele de mai jos fixeaza fiecare conditie separat si
/// - la fel de important - fixeaza faptul ca o serie curata chiar primeste un verdict, ca
/// garda sa nu devina un refuz permanent.
///
/// Datele din <see cref="RealSessionSweep"/> sunt cifrele reale ale sesiunii
/// 2026-08-12-Smida, care a motivat tot modul.
/// </summary>
public class LightSeriesAnalyzerTests
{
    /// <summary>Un rezultat --light cu doar cifrele care conteaza la combinare.</summary>
    static (string, LightResult) Frame(string name, double rad, double plane) =>
        (name, new LightResult
        {
            RadPct = rad,
            PlanePct = plane,
            KeptTiles = 100,
            TotalTiles = 110,
            RadialMap = TestImages.From(4, 4, (x, y) => 1.0 + rad / 100.0),
            Verdict = Stats.Verdict(Math.Abs(rad), Th.LightRadOk, Th.LightRadBad),
        });

    /// <summary>n cadre asezate simetric in jurul mediei, cu gradientul de cer constant.</summary>
    static List<(string, LightResult)> Steady(int n, double mean, double jitter = 0.0)
        => Enumerable.Range(0, n)
            .Select(i => Frame($"f{i:00}.fit", mean + (i % 2 == 0 ? jitter : -jitter), 10.0))
            .ToList();

    // ------------------------------------------------------------ statistica

    [Fact]
    public void TheMeanMinAndMaxComeFromTheRadialComponents()
    {
        var r = LightSeriesAnalyzer.Combine(new List<(string, LightResult)>
        {
            Frame("a", -2.0, 10), Frame("b", 0.0, 10), Frame("c", 4.0, 10),
            Frame("d", 1.0, 10), Frame("e", 2.0, 10),
        });

        Assert.Equal(1.0, r.MeanRad, 6);
        Assert.Equal(-2.0, r.MinRad, 6);
        Assert.Equal(4.0, r.MaxRad, 6);
        Assert.Equal(5, r.Count);
    }

    /// <summary>Deviatia standard e de esantion (ddof = 1), ca numpy.std(ddof=1).</summary>
    [Fact]
    public void TheStandardDeviationUsesTheSampleDivisor()
    {
        // 0,2,4,6,8: media 4, suma patratelor abaterilor 40, /(5-1) = 10 -> sqrt(10)
        var r = LightSeriesAnalyzer.Combine(new List<(string, LightResult)>
        {
            Frame("a", 0, 10), Frame("b", 2, 10), Frame("c", 4, 10),
            Frame("d", 6, 10), Frame("e", 8, 10),
        });

        Assert.Equal(Math.Sqrt(10.0), r.StdDev, 9);
        Assert.Equal(Math.Sqrt(10.0) / Math.Sqrt(5), r.Sem, 9);
    }

    [Fact]
    public void PearsonIsOneOnAPerfectlyLinearRelation()
    {
        double[] a = { 1, 2, 3, 4 };
        double[] b = { 10, 20, 30, 40 };

        Assert.Equal(1.0, LightSeriesAnalyzer.Pearson(a, b), 9);
        Assert.Equal(-1.0, LightSeriesAnalyzer.Pearson(a, b.Select(x => -x).ToArray()), 9);
    }

    /// <summary>
    /// Pe o serie constanta intrebarea "se misca impreuna?" nu are raspuns, nu are
    /// raspunsul "nu". De asta e NaN, si de asta testul de corelatie e sarit cu totul in
    /// cazul ala (vezi <see cref="AConstantSkyGradientSkipsTheCorrelationTest"/>).
    /// </summary>
    [Fact]
    public void PearsonIsNaNWhenEitherSeriesIsConstant()
    {
        Assert.True(double.IsNaN(LightSeriesAnalyzer.Pearson(
            new double[] { 1, 1, 1 }, new double[] { 1, 2, 3 })));
    }

    // ------------------------------------------------------ verdict concludent

    [Fact]
    public void ATightSeriesGetsARealVerdict()
    {
        var r = LightSeriesAnalyzer.Combine(Steady(10, mean: 0.5, jitter: 0.2));

        Assert.True(r.Conclusive);
        Assert.Equal("OK", r.Verdict);
        Assert.Empty(r.Reasons);
    }

    /// <summary>
    /// O serie stransa in jurul unei erori mari primeste tot un verdict - unul prost.
    /// Garda separa "nu stiu" de "e rau", nu le confunda.
    /// </summary>
    [Fact]
    public void ATightSeriesAroundALargeErrorReportsTheProblem()
    {
        var r = LightSeriesAnalyzer.Combine(Steady(10, mean: 8.0, jitter: 0.2));

        Assert.True(r.Conclusive);
        Assert.Equal("PROBLEMA", r.Verdict);
    }

    [Fact]
    public void ATightSeriesJustOverTheOkThresholdIsSuspect()
    {
        var r = LightSeriesAnalyzer.Combine(Steady(10, mean: 3.0, jitter: 0.2));

        Assert.Equal("SUSPECT", r.Verdict);
    }

    // ------------------------------------------------------ verdict nedeterminat

    [Fact]
    public void TooFewFramesIsNotEnoughToAverage()
    {
        var r = LightSeriesAnalyzer.Combine(Steady(Th.LightSeriesMinFrames - 1, mean: 0.5));

        Assert.Equal(LightSeriesAnalyzer.Undetermined, r.Verdict);
        Assert.Contains(r.Reasons, x => x.StartsWith("prea putine cadre"));
    }

    [Fact]
    public void ExactlyTheMinimumFrameCountIsAccepted()
    {
        var r = LightSeriesAnalyzer.Combine(Steady(Th.LightSeriesMinFrames, mean: 0.5));

        Assert.DoesNotContain(r.Reasons, x => x.StartsWith("prea putine cadre"));
    }

    /// <summary>
    /// Imprastiere mare cu gradientul de cer constant: corelatia nu se aplica, dar
    /// eroarea mediei singura ajunge ca sa opreasca verdictul.
    /// </summary>
    [Fact]
    public void AWideSpreadAloneMakesTheMeanUnreadable()
    {
        var r = LightSeriesAnalyzer.Combine(Steady(10, mean: 0.0, jitter: 30.0));

        Assert.Equal(LightSeriesAnalyzer.Undetermined, r.Verdict);
        Assert.Contains("sem", r.Reasons);
        Assert.False(r.CorrApplies);
    }

    /// <summary>
    /// Cazul care da toata valoarea modului: media e practic zero si ar trece pragul de
    /// OK, dar cifra urmareste gradientul de cer. Flat-ul e acelasi pe toate cadrele prin
    /// constructie, deci ce se misca e cerul - si verdictul refuza sa se pronunte.
    /// </summary>
    [Fact]
    public void AMeanNearZeroIsStillRefusedWhenItTracksTheSky()
    {
        var r = LightSeriesAnalyzer.Combine(RealSessionSweep());

        Assert.Equal(0.23, r.MeanRad, 2);              // ar fi trecut drept OK
        Assert.Equal(3.16, r.Sem, 2);
        Assert.Equal(-0.94, r.SkyCorr, 2);
        Assert.Equal(LightSeriesAnalyzer.Undetermined, r.Verdict);
        Assert.Contains("sem", r.Reasons);
        Assert.Contains("corelatie", r.Reasons);
    }

    /// <summary>
    /// Corelatia se calculeaza mereu, dar se JUDECA doar cand gradientul chiar variaza.
    /// Pe o sesiune curata unde sta constant, corelatia e zgomot si ar respinge date bune.
    /// </summary>
    [Fact]
    public void AConstantSkyGradientSkipsTheCorrelationTest()
    {
        // corelatie perfecta pe un interval de gradient minuscul: se ignora
        var frames = Enumerable.Range(0, 10)
            .Select(i => Frame($"f{i}.fit", 0.5 + i * 0.01, 10.0 + i * 0.01)).ToList();

        var r = LightSeriesAnalyzer.Combine(frames);

        Assert.False(r.CorrApplies);
        Assert.DoesNotContain("corelatie", r.Reasons);
        Assert.Equal("OK", r.Verdict);
    }

    [Fact]
    public void TheCorrelationTestAppliesOnceTheGradientMovesEnough()
    {
        var frames = Enumerable.Range(0, 10)
            .Select(i => Frame($"f{i}.fit", 0.5 + i * 0.01, 10.0 + i * 5.0)).ToList();

        var r = LightSeriesAnalyzer.Combine(frames);

        Assert.True(r.CorrApplies);
        Assert.Contains("corelatie", r.Reasons);
        Assert.Equal(LightSeriesAnalyzer.Undetermined, r.Verdict);
    }

    // ------------------------------------------------------------ degenerari

    /// <summary>
    /// Un singur cadru nu are imprastiere de calculat, deci sem iese NaN. NaN trebuie sa
    /// cada pe partea proasta - comparatiile sunt scrise ca !(x &lt; prag) tocmai pentru
    /// asta. Daca ar fi scrise ca (x &gt;= prag), NaN ar trece TACUT si un singur cadru ar
    /// primi un verdict pe care nu-l poate sustine.
    /// </summary>
    [Fact]
    public void ASingleFrameCannotProduceAVerdict()
    {
        var r = LightSeriesAnalyzer.Combine(new List<(string, LightResult)> { Frame("a", 0.1, 10) });

        Assert.True(double.IsNaN(r.Sem));
        Assert.Equal(LightSeriesAnalyzer.Undetermined, r.Verdict);
        Assert.Contains("sem", r.Reasons);
    }

    [Fact]
    public void AnEmptySeriesThrowsInsteadOfReturningNothing()
    {
        Assert.Throws<Exception>(() =>
            LightSeriesAnalyzer.Combine(new List<(string, LightResult)>()));
    }

    /// <summary>
    /// Harta medie n-are sens pe cadre de dimensiuni diferite; e mai bine sa se opreasca
    /// aici decat sa produca o harta amestecata din care nu se poate citi nimic.
    /// </summary>
    [Fact]
    public void FramesOfDifferentSizesAreRefusedWithAnExplanation()
    {
        var frames = Steady(6, mean: 0.5);
        frames[3] = ("odd.fit", new LightResult
        {
            RadPct = 0.5,
            PlanePct = 10,
            RadialMap = TestImages.From(8, 8, (x, y) => 1.0),
            Verdict = "OK",
        });

        var ex = Assert.Throws<Exception>(() => LightSeriesAnalyzer.Combine(frames));
        Assert.Contains("aceeasi dimensiune", ex.Message);
    }

    [Fact]
    public void TheMeanMapIsTheAverageOfThePerFrameMaps()
    {
        var frames = new List<(string, LightResult)>
        {
            Frame("a", 0.0, 10), Frame("b", 2.0, 10), Frame("c", 4.0, 10),
            Frame("d", 6.0, 10), Frame("e", 8.0, 10),
        };

        var r = LightSeriesAnalyzer.Combine(frames);

        // fiecare harta e constanta la 1 + rad/100, deci media e 1 + 0.04
        Assert.All(r.RadialMap.D, v => Assert.Equal(1.04, v, 9));
    }

    /// <summary>
    /// Cifrele reale ale sesiunii 2026-08-12-Smida: 32 de cadre, 81 de minute, Calea
    /// Lactee la 20 mm. Componenta radiala baleiaza neted de la +3.13 prin -19.20 pana la
    /// +35.73 in timp ce gradientul de cer scade de la 88 la 49.
    /// </summary>
    static List<(string, LightResult)> RealSessionSweep()
    {
        double[] rad =
        {
            3.13, 2.07, 0.71, -1.73, -3.00, -4.65, -10.71, -14.96, -13.66, -14.86,
            -15.22, -15.92, -16.56, -17.70, -17.57, -17.87, -19.03, -19.20, -17.28,
            -4.02, -0.88, 3.21, 3.07, 3.61, 12.70, 20.69, 22.17, 24.18, 31.91, 35.05,
            35.73, 33.85,
        };
        double[] plane =
        {
            77.01, 79.53, 79.72, 80.93, 81.72, 80.90, 83.50, 81.97, 80.56, 80.04,
            79.38, 79.72, 88.46, 86.92, 82.59, 80.00, 80.70, 79.90, 81.05, 79.76,
            77.54, 75.21, 74.76, 73.24, 70.44, 65.69, 61.93, 61.66, 57.14, 53.99,
            51.92, 49.44,
        };
        return rad.Select((v, i) => Frame($"pp_light_{i + 1:00000}.fit", v, plane[i])).ToList();
    }
}
