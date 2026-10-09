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
/// The lift table (ADR 0035): one table of lin(pow(level, gamma)) per lift, interpolated on the
/// linear value, against the path it replaced, a lifted raster (RasterOps.Gamma) linearised
/// sample by sample. The bounds are the ones measured on 2026-10-09; on the two reference images
/// no patch moved by more than one float ulp of its L* (docs/validation.md).
/// </summary>
public class LiftTableTests
{
    static readonly double[] Gammas = { 0.3, 0.7788, 1, 1.5, 3 };

    /// <summary>The old path, sample by sample: the lifted raster's float, then the exact linearisation.</summary>
    static double OldPath(double[] pow, float s)
    {
        double v = Math.Clamp(s, 0f, 1f) * 65535.0;
        int k = (int)v;
        double f = v - k;
        float lifted = (float)(f == 0 ? pow[k] : pow[k] + f * (pow[k + 1] - pow[k]));
        return ColorMath.SrgbToLinear(lifted);
    }

    [Fact]
    public void AtEverySixteenBitLevelTheTableIsTheLinearisedPowerCurve()
    {
        foreach (double gamma in Gammas)
        {
            var t = new LiftTable(gamma);
            double worst = 0;
            for (int k = 0; k <= 65535; k++)
            {
                double exact = ColorMath.SrgbToLinear(Math.Pow(k / 65535.0, gamma));
                worst = Math.Max(worst, Math.Abs(t.Linear((float)(k / 65535.0)) - exact));
            }
            // A level's float does not always map back onto its integer (k / 65535 rounded to
            // float, times 65535, lands within 0.004 of k), so the table interpolates by that
            // much: 2.0e-7 in luminance at most, measured at gamma 3 (1.0e-7 at 1.5).
            Assert.True(worst < 5e-7, $"gamma {gamma}: {worst:E2}");
        }
    }

    [Fact]
    public void BetweenLevelsTheTableAgreesWithTheLiftedRasterPath()
    {
        var rng = new Random(7);
        float[] samples = Enumerable.Range(0, 50000).Select(_ => (float)(0.002 + 0.998 * rng.NextDouble())).ToArray();
        foreach (double gamma in Gammas)
        {
            var t = new LiftTable(gamma);
            double[] pow = RasterOps.PowerLut(gamma);
            double worst = 0;
            foreach (float s in samples) worst = Math.Max(worst, Math.Abs(t.Linear(s) - OldPath(pow, s)));
            // The second-order error of interpolating the sRGB curve over one 16-bit step of the
            // lifted value: largest in the first steps at a small gamma, where the step is widest.
            Assert.True(worst < 2e-7, $"gamma {gamma}: {worst:E2}");
        }
    }

    [Fact]
    public void ANaNIsBlackAndOneIsWhite()
    {
        var t = new LiftTable(0.7);

        Assert.Equal(0, t.Linear(float.NaN));
        Assert.Equal(0, t.Linear(0f));
        Assert.Equal(1, t.Linear(1f), 12);
        Assert.Equal(1, t.Linear(1.5f), 12);   // clamped, not past the table's end
        Assert.Equal(0.7, t.Gamma);
    }

    [Fact]
    public void TheExactLineariserIsSrgbToLinear()
    {
        var e = new ExactLinearizer();

        Assert.Equal(ColorMath.SrgbToLinear(0.3f), e.Linear(0.3f));
        Assert.Equal(ColorMath.SrgbToLinear(0.01f), e.Linear(0.01f));
    }

    static Raster RandomRaster(int seed, bool quantised)
    {
        var rng = new Random(seed);
        return SyntheticRasters.From(64, 64, 3, (x, y, p) =>
        {
            double v = 0.02 + 0.88 * rng.NextDouble();
            return quantised ? (float)(Math.Round(v * 65535) / 65535.0) : (float)v;
        });
    }

    /// <summary>The largest difference between two L* planes; fails the test when they differ in length.</summary>
    static double Worst(float[] old, float[] now)
    {
        Assert.Equal(old.Length, now.Length);
        double worst = 0;
        for (int i = 0; i < old.Length; i++) worst = Math.Max(worst, Math.Abs(old[i] - now[i]));
        return worst;
    }

    /// <summary>
    /// On a float raster and on one quantised to 16-bit levels alike, every patch is within one
    /// float ulp of its L* (3.8e-6 to 7.6e-6 at L* 32 to 64) of the lifted raster's: the figure
    /// measured on the two reference images, 7.63e-6.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PatchLstarsThroughTheTableMatchThePatchesOfTheLiftedRaster(bool quantised)
    {
        Raster sub = RandomRaster(3, quantised);
        foreach (double gamma in new[] { 0.7788, 1.5 })
        {
            float[] old = DustAnalyzer.PatchLstars(RasterOps.Gamma(sub, gamma));
            float[] now = DustAnalyzer.PatchLstars(sub, new LiftTable(gamma));

            Assert.Equal(64, old.Length);
            double worst = Worst(old, now);
            Assert.True(worst < 1e-5, $"gamma {gamma}, quantised {quantised}: {worst:E2}");
        }
    }

    [Fact]
    public void PatchLstarsWithTheExactLineariserIsTheAsIsPath()
    {
        Raster sub = RandomRaster(5, quantised: false);

        Assert.Equal(DustAnalyzer.PatchLstars(sub), DustAnalyzer.PatchLstars(sub, new ExactLinearizer()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LstarPlaneThroughTheTableMatchesThePlaneOfTheLiftedRaster(bool quantised)
    {
        Raster sub = RandomRaster(11, quantised);
        foreach (double gamma in new[] { 0.7788, 1.5 })
        {
            float[] old = RasterOps.LstarPlane(RasterOps.Gamma(sub, gamma), 1);
            float[] now = RasterOps.LstarPlane(sub, new LiftTable(gamma));

            Assert.Equal(64 * 64, now.Length);
            double worst = Worst(old, now);
            Assert.True(worst < 1e-5, $"gamma {gamma}, quantised {quantised}: {worst:E2}");
        }
    }

    [Fact]
    public void LstarPlaneOnMonoGoesThroughTheSameTable()
    {
        Raster mono = SyntheticRasters.Flat(4, 4, 0.1f, planes: 1);
        float[] l = RasterOps.LstarPlane(mono, new LiftTable(RasterOps.GammaFor(0.1, 0.19)));

        Assert.All(l, v => Assert.Equal(ColorMath.LstarOfY(ColorMath.SrgbToLinear(0.19)), v, 3));
    }

    [Fact]
    public void LstarSharesAreTheTwoSharesOfLstarStats()
    {
        float[] L = RasterOps.LstarPlane(RandomRaster(13, quantised: false), new ExactLinearizer());
        var (_, _, low, high, _) = TonalAnalyzer.LstarStats(L, 2.4);
        var shares = TonalAnalyzer.LstarShares(L);

        Assert.Equal(low, shares.PctBelowLow);
        Assert.Equal(high, shares.PctAboveHigh);
        Assert.True(low > 0 && high > 0);   // the random raster straddles both thresholds
    }

    [Fact]
    public void LstarStatsOnAPlaneEqualsLstarStatsOnTheRaster()
    {
        Raster r = RandomRaster(17, quantised: false);
        var onRaster = TonalAnalyzer.LstarStats(r, 2.4, stride: 1);
        var onPlane = TonalAnalyzer.LstarStats(RasterOps.LstarPlane(r, 1), 2.4);

        Assert.Equal(onRaster.Median, onPlane.Median);
        Assert.Equal(onRaster.PctBelowPaperBlack, onPlane.PctBelowPaperBlack);
        Assert.Equal(onRaster.PctBelowLow, onPlane.PctBelowLow);
        Assert.Equal(onRaster.PctAboveHigh, onPlane.PctAboveHigh);
        Assert.Equal(onRaster.HistPct, onPlane.HistPct);
    }
}
