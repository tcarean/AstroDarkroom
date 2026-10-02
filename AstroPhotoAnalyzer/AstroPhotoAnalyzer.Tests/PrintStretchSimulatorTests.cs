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
    static readonly Parameters P = new();

    static SimulationResult Simulate(Raster r, Parameters? p = null)
    {
        p ??= P;
        return PrintStretchSimulator.Simulate(r, TonalAnalyzer.Analyze(r, p), p);
    }

    static double Lstar(double v) => ColorMath.LstarOfSrgb(v, v, v);

    [Fact]
    public void R043_ADarkBackgroundGetsTheLowestSafeRowAbove()
    {
        // Flat 0.08: L* ~6, under paper black (9.0). The grid row that first puts it at or
        // above 9.0 is 0.10 (L* 9.03, by a hair); the rows are the as-is row and 0.10..0.20.
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.08f));

        Assert.Equal(0.08, s.BackgroundMeasured, 6);
        Assert.False(s.AlreadyPrintSafe);
        Assert.True(s.DustBelowPaperBlack);
        Assert.True(s.SkyBelowPaperBlack);
        Assert.Equal(12, s.Rows.Count);
        Assert.True(s.Rows[0].AsIs);
        Assert.Equal(P.SimulationTargets, s.Rows.Skip(1).Select(x => x.Target));
        Assert.Equal(0.10, s.RecommendedTarget);
        Assert.True(s.Rows[1].Recommended);
        Assert.Equal(Math.Log(0.10) / Math.Log(0.08), s.Rows[1].Gamma, 6);
        Assert.All(s.Rows.Skip(1), x => Assert.True(x.PrintSafe));
        Assert.Equal(DustMethod.NoWcs, s.Method);
        Assert.Empty(s.Clouds);
        Assert.Equal(Lstar(0.08), s.AsIs.LstarDust, 1);
        Assert.Equal(Lstar(0.08), s.AsIs.LstarSky, 1);
        Assert.Equal(100, s.AsIs.PctPatchesBelowPaperBlack);
    }

    [Fact]
    public void R043_ABrightImageIsToldHowFarDownItCanGo()
    {
        // Flat 0.30 (L* ~31): print-safe as is; the lowest grid row that keeps the dust on
        // paper black is 0.10 (L* 9.03), so that is recommended, below the measured level.
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.3f));

        Assert.True(s.AlreadyPrintSafe);
        Assert.NotNull(s.Recommended);
        Assert.False(s.RecommendedIsAsIs);
        Assert.Equal(0.10, s.RecommendedTarget);
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

        Assert.Equal(13, s.Rows.Count);
        SimulationRow given = Assert.Single(s.Rows, x => x.Explicit);
        Assert.Equal(0.135, given.Target);
        Assert.Same(given, s.Primary);
        Assert.Equal(Lstar(0.135), given.LstarSky, 1);

        // On the grid: no extra row, but the row is marked explicit.
        var onGrid = Simulate(SyntheticRasters.Flat(64, 64, 0.08f), new Parameters { TargetBackground = 0.15 });
        Assert.Equal(12, onGrid.Rows.Count);
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
        Assert.Equal(0.12, s.RecommendedTarget);
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
    public void R044_WithAWcsTheRowsNameTheCloudsAndTheDustIsTheirs()
    {
        // 512 x 512 at 9"/px on LDN 42: the cloud's disc (272 px) is the dark disc in the middle.
        double rr = WcsFixtures.DarkDiscPx;
        Raster r = SyntheticRasters.From(512, 512, 3, (x, y, p) => (x - 255.5) * (x - 255.5) + (y - 255.5) * (y - 255.5) <= rr * rr ? 0.05f : 0.25f);
        r.Source = new SourceInfo { Wcs = WcsFixtures.SyntheticWcs(512, 512, 263.27, -25.70, WcsFixtures.NarrowScale) };
        var s = Simulate(r);

        Assert.Equal(DustMethod.Catalogue, s.Method);
        Assert.Contains(s.Clouds, c => c.Name == "LDN 42 (B78)");
        CloudRow ldn42 = s.Clouds.Single(c => c.Name == "LDN 42 (B78)");
        Assert.True(ldn42.Patches >= Thresholds.DustCloudMinPatches);
        Assert.Equal(Lstar(0.05), ldn42.Lstar, 1);
        Assert.NotNull(ldn42.ExactTarget);
        Assert.Equal(Lstar(0.05), s.AsIs.LstarDust, 1);
        Assert.Equal(Lstar(0.25), s.AsIs.LstarSky, 1);
        Assert.True(s.DustBelowPaperBlack);
        Assert.False(s.SkyBelowPaperBlack);
        Assert.Equal(s.Clouds.OrderBy(c => c.Lstar).Select(c => c.Name), s.Clouds.Select(c => c.Name));
        Assert.Contains("norilor catalogati", s.DustDescription);
        Assert.Equal("mediana dalelor din afara norilor", s.SkyDescription);
        Assert.Null(s.MethodNote);

        // The ladder's exact target for the cloud agrees with the simulation's: the cloud is the dust here.
        Assert.Equal(s.ExactTarget!.Value, ldn42.ExactTarget!.Value, 3);
    }

    [Fact]
    public void R044_WithoutAWcsTheDescriptionSaysSo()
    {
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.08f));

        Assert.StartsWith("cele mai intunecate 1 % din dalele de 32 px", s.DustDescription);
        Assert.Equal("mediana dalelor", s.SkyDescription);
        Assert.Equal("fara WCS", s.MethodNote);
    }
}
