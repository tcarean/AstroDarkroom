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

public class PrintStretchSimulatorTests
{
    static readonly Parameters P = new();

    static SimulationResult Simulate(Raster r, Parameters? p = null)
    {
        p ??= P;
        return PrintStretchSimulator.Simulate(r, TonalAnalyzer.Analyze(r, p), p);
    }

    [Fact]
    public void ADarkBackgroundGetsTheLowestSafeTarget()
    {
        // 0.08: L* ~6, sub negrul baryta-ului. Tintele implicite sunt 0.15, 0.19, 0.20;
        // deja 0.15 (L* ~16) e print-safe, deci e cea recomandata.
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.08f));

        Assert.Equal(0.08, s.BackgroundMeasured, 6);
        Assert.False(s.AlreadyPrintSafe);
        Assert.Equal(4, s.Rows.Count);
        Assert.True(s.Rows[0].AsIs);
        Assert.Equal(new[] { 0.15, 0.19, 0.20 }, s.Rows.Skip(1).Select(x => x.Target));
        Assert.Equal(0.15, s.RecommendedTarget);
        Assert.Equal(Math.Log(0.15) / Math.Log(0.08), s.Rows[1].Gamma, 6);
        Assert.All(s.Rows.Skip(1), x => Assert.True(x.PrintSafe));
    }

    [Fact]
    public void ThePrimaryRowIsTheParametersTargetAndLandsWhereTheMathSays()
    {
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.08f));

        Assert.Equal(0.19, s.Primary.Target);
        // 0.19 sRGB -> Y 0.0301 -> L* 20.07
        Assert.Equal(20.1, s.Primary.LstarMedian, 1);
        Assert.Equal(0, s.Primary.PctBelowPaperBlack);
    }

    [Fact]
    public void AnAlreadySafeImageRecommendsNothing()
    {
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.3f));

        Assert.True(s.AlreadyPrintSafe);
        Assert.Null(s.RecommendedTarget);
    }

    [Fact]
    public void ACustomTargetListIsHonoured()
    {
        var p = new Parameters { TargetBackground = 0.25, SimulationTargets = new[] { 0.12 } };
        var s = Simulate(SyntheticRasters.Flat(64, 64, 0.08f), p);

        Assert.Equal(new[] { 0.12, 0.25 }, s.Rows.Skip(1).Select(x => x.Target));
        Assert.Equal(0.25, s.Primary.Target);
        // 0.12 -> Y 0.0137 -> L* 11.9: peste negrul hartiei, deci print-safe si recomandat.
        Assert.Equal(0.12, s.RecommendedTarget);
    }

    [Fact]
    public void ABlackImageDoesNotThrow()
    {
        var s = Simulate(SyntheticRasters.Flat(16, 16, 0f));

        Assert.Single(s.Rows);
        Assert.Null(s.RecommendedTarget);
    }

    [Fact]
    public void TheSimulationLiftsTheSkyIntoTheTargetWithoutBurningTheCore()
    {
        // Trei sferturi cer la 0.08, un sfert nucleu la 0.6 - cerul domina, deci mediana
        // E cerul, cum e pe orice astrofoto. Ridicarea la 0.19 duce cerul exact in tinta
        // si nucleul putin mai sus (0.6^0.66 ~ 0.71), fara sa-l arda.
        var r = SyntheticRasters.From(64, 64, 3, (x, y, p) => x < 48 ? 0.08f : 0.6f);
        var s = Simulate(r);

        Assert.Equal(0.08, s.BackgroundMeasured, 6);
        Assert.Equal(75, s.Rows[0].PctBelowPaperBlack, 6);
        Assert.Equal(0, s.Primary.PctBelowPaperBlack, 6);
        Assert.Equal(25, s.Primary.PctAboveHighlight, 6);
    }

    /// <summary>
    /// Limita cunoscuta a "fundal = mediana": pe o imagine in care cerul NU domina,
    /// mediana cade in structura si simularea ridica de la un fundal gresit. Nu se
    /// intampla pe astrofoto; testul doar pastreaza faptul vizibil.
    /// </summary>
    [Fact]
    public void WhenTheSkyDoesNotDominateTheMedianIsNotTheSky()
    {
        var r = SyntheticRasters.From(64, 64, 3, (x, y, p) => x < 32 ? 0.08f : 0.6f);
        var s = Simulate(r);

        Assert.Equal(0.34, s.BackgroundMeasured, 2);
    }
}
