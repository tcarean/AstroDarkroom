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

/// <summary>
/// R-043: the grid 0.10..0.20 with the as-is row among it, the dust and the sky per row, the
/// lowest print-safe row recommended in either direction, the exact target from the ladder,
/// the Dmax figures, and the cloud rows with a WCS.
/// </summary>
public class PrintStretchSimulatorTests
{
    static readonly Parameters P = Scenes.OnBuiltForPaper();

    static SimulationResult Simulate(Raster r, Parameters? p = null)
    {
        p = Scenes.OnBuiltForPaper(p);
        return PrintStretchSimulator.Simulate(r, TonalAnalyzer.Analyze(r, p), p);
    }

    static double Lstar(double v) => ColorMath.LstarOfSrgb(v, v, v);

    [Fact]
    public void R043_ADarkBackgroundGetsTheLowestSafeRowAbove()
    {
        // Flat 0.08: L* ~6, under paper black (9.0). The grid row that first puts it at or
        // above 9.0 is 0.10 (L* 9.03, by a hair); the first that also has the sky at the
        // visibility threshold (L* 15) is 0.15, the recommended one. The rows are 0.05..0.07,
        // the as-is row, and 0.09..0.20 (the as-is 0.08 takes the grid's 0.08 place).
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.08f));

        Assert.Equal(0.08, s.BackgroundMeasured, 6);
        Assert.False(s.AlreadyPrintSafe);
        Assert.True(s.DustBelowPaperBlack);
        Assert.True(s.SkyBelowPaperBlack);
        Assert.Equal(17, s.Rows.Count);
        Assert.True(s.Rows[3].AsIs);   // the measured 0.08 sorts just before the grid's 0.08
        Assert.Equal(P.SimulationTargets, s.Rows.Where(x => !x.AsIs).Select(x => x.Target));
        Assert.Equal(0.15, s.RecommendedTarget);
        Assert.Same(s.Rows[11], Assert.Single(s.Rows, x => x.Recommended));
        Assert.Equal(Math.Log(0.05) / Math.Log(0.08), s.Rows[0].Gamma, 6);
        Assert.All(s.Rows.Where(x => x.Target >= 0.10), x => Assert.True(x.DustOnPaperBlack));
        Assert.All(s.Rows.Where(x => x.Target < 0.10), x => Assert.False(x.DustOnPaperBlack));
        Assert.Equal(DustMethod.Sky, s.Method);
        Assert.Equal("fara WCS", s.MethodNote);
        Assert.Empty(s.Clouds);
        Assert.Equal(Lstar(0.08), s.AsIs.LstarDust, 1);
        Assert.Equal(Lstar(0.08), s.AsIs.LstarSky, 1);
        Assert.Equal(100, s.AsIs.PctPatchesBelowPaperBlack);
    }

    [Fact]
    public void R043_ABrightImageIsToldHowFarDownItCanGo()
    {
        // Flat 0.30 (L* ~31): print-safe as is; the lowest grid row that keeps the dust on
        // paper black is 0.10 (L* 9.03), the lowest that also keeps the sky at L* 15 is 0.15,
        // so that is recommended, below the measured level.
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.3f));

        Assert.True(s.AlreadyPrintSafe);
        Assert.NotNull(s.Recommended);
        Assert.False(s.RecommendedIsAsIs);
        Assert.Equal(0.15, s.RecommendedTarget);
        Assert.True(s.Rows[0].Target < s.AsIs.Target);
        Assert.Equal(s.Rows.Last(), s.AsIs);   // 0.30 sorts after 0.20
        // The exact target of a flat image is the level whose L* is paper black, ~0.0999.
        Assert.Equal(0.0999, s.ExactTarget!.Value, 3);
    }

    [Fact]
    public void R043_TheExactTargetIsInterpolatedOnTheLadderAndMovesWithDmax()
    {
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.08f));

        // A flat image lifted to t has L* = L*(t); the exact target is the t whose L* is 9.0:
        // sRGB of Y = 0.01, i.e. 0.0999.
        double exact = ColorMath.LinearToSrgb(ColorMath.YOfLstar(ColorMath.PaperBlackLstar(2.0)));
        Assert.NotNull(s.ExactTarget);
        Assert.Equal(exact, s.ExactTarget!.Value, 3);
        Assert.Equal(2.0, s.Dmax);

        // Dmax 1.9: paper black L* 11.0, a higher target; Dmax 2.1: L* 7.2, a lower one.
        Assert.True(s.ExactTargetDmaxLower > s.ExactTarget);
        Assert.True(s.ExactTargetDmaxHigher < s.ExactTarget);
        Assert.Equal(ColorMath.LinearToSrgb(ColorMath.YOfLstar(ColorMath.PaperBlackLstar(1.9))), s.ExactTargetDmaxLower!.Value, 3);

        // The Dmax from which the as-is image passes: the one whose paper black is the dust's L*.
        Assert.Equal(-Math.Log10(ColorMath.YOfLstar(s.AsIs.LstarDust)), s.DmaxToPassAsIs, 6);
        Assert.InRange(s.DmaxToPassAsIs, 2.1, 2.2);   // Y(0.08) = 0.0072, -log10 = 2.14
    }

    /// <summary>
    /// A target at either end of the ladder is a bound, not a value: `<= 0.05` when even the
    /// lowest step already puts the dust (or a cloud) on paper black, `> 0.60` when the highest
    /// does not. Written the same way in the cloud table, the exact-target line and the map.
    /// </summary>
    [Fact]
    public void R043_ATargetAtTheLadderEndsIsWrittenAsABound()
    {
        Assert.Equal("<= 0.05", ReportWriter.TargetText(Thresholds.ExactTargetLo));
        Assert.Equal("> 0.60", ReportWriter.TargetText(null));
        Assert.Equal("0.187", ReportWriter.TargetText(0.187));
        Assert.Equal("0.10", ReportWriter.TargetText(0.10));

        string cloud = ReportWriter.CloudLine(new CloudRow { Name = "LDN 42 (B78)", Patches = 12, Lstar = 38.5, ExactTarget = Thresholds.ExactTargetLo });
        Assert.EndsWith("   <= 0.05", cloud);

        var s = new SimulationResult { Dmax = 2.0, ExactTarget = Thresholds.ExactTargetLo, ExactTargetDmaxLower = 0.06, ExactTargetDmaxHigher = Thresholds.ExactTargetLo, DmaxToPassAsIs = 1.3 };
        Assert.StartsWith("tinta exacta a prafului pe negru: <= 0.05 la Dmax 2.0 (0.06 la Dmax 1.9, <= 0.05 la Dmax 2.1); ", ReportWriter.ExactLine(s));
        var none = s with { ExactTarget = null, ExactTargetDmaxLower = null, ExactTargetDmaxHigher = null };
        Assert.StartsWith("tinta exacta a prafului pe negru: > 0.60 la Dmax 2.0 (> 0.60 la Dmax 1.9, > 0.60 la Dmax 2.1); ", ReportWriter.ExactLine(none));
    }

    /// <summary>
    /// The recommended row needs both: the dust on paper black and the sky at the visibility
    /// threshold. On the default paper a 0.08 sky passes the first at every row and the second
    /// only from 0.15, a grey of L* 15 being the level 0.148.
    /// </summary>
    [Fact]
    public void R043_TheRecommendedRowAlsoHasTheSkyAtTheVisibilityThreshold()
    {
        var p = new Parameters();
        Raster r = SyntheticRasters.Flat(64, 64, 0.08f);
        SimulationResult s = PrintStretchSimulator.Simulate(r, TonalAnalyzer.Analyze(r, p), p);

        Assert.True(s.AlreadyPrintSafe);
        Assert.True(s.SkyBelowVisibility);
        // On the default baryta (paper black L* 3.6) the dust is on black from 0.06 (L* 4.4) up,
        // but under 0.15 the sky is under the visibility threshold: no row before it is print-safe.
        Assert.All(s.Rows.Where(x => x.Target < 0.145), x => Assert.True(!x.SkyVisible && !x.PrintSafe));
        Assert.All(s.Rows.Where(x => x.Target >= 0.06 && x.Target < 0.145), x => Assert.True(x.DustOnPaperBlack));
        Assert.Equal(0.15, s.Recommended.Target, 9);
        // The mark of a row says which of the two it passes: a row with the dust on paper black
        // and the sky under the threshold is not called print-safe.
        Assert.EndsWith("   cer sub L* 15", ReportWriter.SimulationLine(s.Rows.Single(x => Math.Abs(x.Target - 0.12) < 1e-9)));
        Assert.EndsWith("   print-safe", ReportWriter.SimulationLine(s.Recommended));
        // The sky is judged as the table prints it: 14.96 shows as 15.0 and passes, 14.94 does not.
        Assert.True(new SimulationRow { LstarSky = 14.96 }.SkyVisible);
        Assert.False(new SimulationRow { LstarSky = 14.94 }.SkyVisible);
        Assert.InRange(s.VisibilityTarget!.Value, 0.147, 0.149);
        Assert.EndsWith("cerul ajunge la pragul de vizibilitate L* 15 la 0.148", ReportWriter.ExactLine(s));
        Assert.Equal("* = recomandat: cea mai mica tinta print-safe - praful pe negrul hartiei si cerul la L* 15 sau peste", ReportWriter.SimulationFooter(s));
    }

    [Fact]
    public void R043_CrossingInterpolatesBetweenTwoStepsAndHandlesTheEnds()
    {
        double[] t = { 0.10, 0.11, 0.12 };
        Assert.Equal(0.105, PrintStretchSimulator.Crossing(t, new double[] { 8, 10, 12 }, 9)!.Value, 9);
        Assert.Equal(0.10, PrintStretchSimulator.Crossing(t, new double[] { 9, 10, 12 }, 9));
        Assert.Null(PrintStretchSimulator.Crossing(t, new double[] { 1, 2, 3 }, 9));
        Assert.Equal(0.12, PrintStretchSimulator.Crossing(t, new double[] { 1, 2, 9 }, 9)!.Value, 9);
    }

    [Fact]
    public void R023_AnExplicitTargetJoinsTheGridAndIsThePrimaryRow()
    {
        var p = new Parameters { TargetBackground = 0.135 };
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.08f), p);

        Assert.Equal(18, s.Rows.Count);   // 16 grid rows, the as-is row, the explicit one
        SimulationRow given = Assert.Single(s.Rows, x => x.Explicit);
        Assert.Equal(0.135, given.Target);
        Assert.Same(given, s.Primary);
        Assert.Equal(Lstar(0.135), given.LstarSky, 1);

        // On the grid: no extra row, but the row is marked explicit.
        var onGrid = Simulate(SyntheticRasters.Flat(64, 64, 0.08f), new Parameters { TargetBackground = 0.15 });
        Assert.Equal(17, onGrid.Rows.Count);
        Assert.True(onGrid.Rows.Single(x => Math.Abs(x.Target - 0.15) < 1e-9).Explicit);
        Assert.Equal(0.15, onGrid.Primary.Target);

        // Without one, the primary row is the recommended one.
        var none = Simulate(SyntheticRasters.Flat(64, 64, 0.08f));
        Assert.Same(none.Recommended, none.Primary);
    }

    [Fact]
    public void R043_ACustomGridIsHonoured()
    {
        var p = new Parameters { SimulationTargets = new[] { 0.12 } };
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.08f), p);

        Assert.Equal(new[] { 0.08, 0.12 }, s.Rows.Select(x => Math.Round(x.Target, 2)));
        // 0.12 puts the dust on paper black but leaves the sky at L* 11.6: no row to recommend, and the footer says what would do.
        Assert.True(s.Rows[1].DustOnPaperBlack);
        Assert.False(s.Rows[1].PrintSafe);
        Assert.Null(s.RecommendedTarget);
        Assert.Equal("nicio tinta din grila nu aduce cerul la L* 15 (ar trebui 0.148)", ReportWriter.SimulationFooter(s));
    }

    [Fact]
    public void R043_ABlackImageDoesNotThrow()
    {
        var s = Simulate(SyntheticRasters.Flat(16, 16, 0f));

        Assert.Single(s.Rows);
        Assert.Null(s.RecommendedTarget);
        Assert.Null(s.ExactTarget);
        Assert.True(double.IsNaN(s.DmaxToPassAsIs));
        Assert.Same(s.AsIs, s.Primary);
    }

    [Fact]
    public void R043_TheSimulationLiftsTheSkyWithoutBurningTheCore()
    {
        // Three quarters sky at 0.08, a quarter core at 0.6: the sky dominates, so the median
        // IS the sky. Lifted to 0.19 the sky lands on the target and the core a little higher
        // (0.6^0.66 ~ 0.71), unburnt.
        var r = SyntheticRasters.From(64, 64, 3, (x, y, p) => x < 48 ? 0.08f : 0.6f);
        var s = Simulate(r, new Parameters { TargetBackground = 0.19 });

        Assert.Equal(0.08, s.BackgroundMeasured, 6);
        // 64 px = 2 x 2 patches of 32 px: the left patches are sky, the right ones half sky,
        // half core, whose MEAN linear luminance is above paper black: 50 % of the patches.
        Assert.Equal(50, s.AsIs.PctPatchesBelowPaperBlack, 6);
        Assert.Equal(0, s.Primary.PctPatchesBelowPaperBlack, 6);
        Assert.Equal(25, s.Primary.PctAboveHighlight, 6);
    }

    /// <summary>
    /// Known limit of "background = median": on an image where the sky does NOT dominate,
    /// the median falls in the structure and the simulation lifts from a wrong level. It
    /// does not happen on an astrophoto; the test only keeps the fact visible.
    /// </summary>
    [Fact]
    public void WhenTheSkyDoesNotDominateTheMedianIsNotTheSky()
    {
        var r = SyntheticRasters.From(64, 64, 3, (x, y, p) => x < 32 ? 0.08f : 0.6f);
        var s = Simulate(r);

        Assert.Equal(0.34, s.BackgroundMeasured, 2);
    }

    [Fact]
    public void R044_WithABandTheDustAndTheSkyAreTheClassesAndTheCloudsHaveRows()
    {
        // The wide band scene across the plane in Scutum: the classes apply, and the Rift's
        // largest cloud, in the frame, gets its row from the catalogue.
        Raster r = WcsFixtures.BandScene();
        var s = Simulate(r);

        Assert.Equal(DustMethod.Band, s.Method);
        Assert.InRange(s.AsIs.LstarDust, 3, 8);           // the blob at 0.05 (L* 3.6) dominates the dust class
        Assert.Equal(Lstar(0.25), s.AsIs.LstarSky, 0);    // the plain sky outside the band
        Assert.True(s.DustBelowPaperBlack);
        Assert.False(s.SkyBelowPaperBlack);
        Assert.NotEmpty(s.Clouds);
        Assert.Contains(s.Clouds, c => c.Name.StartsWith("LDN "));
        Assert.Equal(s.Clouds.OrderBy(c => c.Lstar).Select(c => c.Name), s.Clouds.Select(c => c.Name));
        Assert.StartsWith("percentila 25 % a dalelor de praf (cu 5 L* sub nivelul local al benzii sau sub cerul modelat; ", s.DustDescription);
        Assert.StartsWith("mediana dalelor de cer, in afara benzii", s.SkyDescription);
        Assert.Null(s.MethodNote);
        Assert.StartsWith("praf ", s.ClassFractions);
        Assert.Contains("varfuri ", s.ClassFractions);
        Assert.NotNull(s.ExactTarget);
    }

    [Fact]
    public void R044_ABandWithoutDustTakesTheDarkestSkyAndStillListsItsClasses()
    {
        var s = Simulate(WcsFixtures.BandScene(withDust: false));

        Assert.Equal(DustMethod.Band, s.Method);
        Assert.Null(s.MethodNote);
        Assert.Equal("cel mai intunecat 1 % al dalelor de cer (nicio dala de praf)", s.DustDescription);
        Assert.StartsWith("mediana dalelor de cer, in afara benzii", s.SkyDescription);
        Assert.StartsWith("praf 0.0 %", s.ClassFractions);
        Assert.Contains(", banda ", s.ClassFractions);
    }

    [Fact]
    public void R044_AFrameNarrowerThanTheBandNamesItsSpanAsTheReason()
    {
        var s = Simulate(WcsFixtures.BandScene(WcsFixtures.NearTheVeil(), withDust: true));

        Assert.Equal(DustMethod.Sky, s.Method);
        Assert.InRange(s.Map.LatitudeSpanDeg, 8, Thresholds.BandMinLatitudeSpanDeg - 1);
        Assert.Equal($"camp prea ingust pentru banda: {s.Map.LatitudeSpanDeg:F1} grade de latitudine galactica, sub {Thresholds.BandMinLatitudeSpanDeg:F0}", s.MethodNote);
        Assert.Equal("mediana dalelor de cer (clase relative la cerul modelat)", s.SkyDescription);
        Assert.Contains(", nebuloasa ", s.ClassFractions);
    }

    [Fact]
    public void R044_AFrameWhollyInsideTheBandTakesTheSkyPathAndStillNamesItsClouds()
    {
        var s = Simulate(WcsFixtures.Ldn42Scene());

        Assert.Equal(DustMethod.Sky, s.Method);
        Assert.Equal("cadrul e in intregime in banda", s.MethodNote);
        // The disc covers nine tenths of the frame: the modelled sky is the cloud itself, no
        // patch is under it, and the dust is the darkest sky, the cloud's level.
        Assert.Equal("cel mai intunecat 1 % al dalelor de cer (nicio dala de praf)", s.DustDescription);
        Assert.Equal("mediana dalelor de cer (clase relative la cerul modelat)", s.SkyDescription);
        Assert.Contains(", nebuloasa ", s.ClassFractions);
        Assert.Contains("sigma ale cerului", s.ClassFractions);
        Assert.Equal(Lstar(0.05), s.AsIs.LstarDust, 1);
        CloudRow ldn42 = s.Clouds.Single(c => c.Name == "LDN 42 (B78)");
        Assert.Equal(Lstar(0.05), ldn42.Lstar, 1);
        Assert.NotNull(ldn42.ExactTarget);
        Assert.Equal(s.ExactTarget!.Value, ldn42.ExactTarget!.Value, 3);   // the darkest sky is the cloud
    }

    [Fact]
    public void R044_WithoutAWcsTheDescriptionSaysSo()
    {
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.08f));

        Assert.Equal("cel mai intunecat 1 % al dalelor de cer (nicio dala de praf)", s.DustDescription);
        Assert.Equal("mediana dalelor de cer (clase relative la cerul modelat)", s.SkyDescription);
        Assert.Equal("fara WCS", s.MethodNote);
        Assert.StartsWith("praf 0.0 %, cer 100.0 %, nebuloasa 0.0 % (peste cerul modelat cu ", s.ClassFractions);
    }
}
