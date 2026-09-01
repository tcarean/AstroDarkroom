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
/// Phase 6: mode 2. The ratio validates the flat *setup*, so a clean setup must produce
/// a featureless A/B - which is why the thresholds here are tighter than mode 1's.
/// </summary>
public class RatioAnalyzerTests
{
    const int N = 512;

    static Img Radial(double center = 30000, double k = 0.45)
        => TestImages.From(N, N, FitsTestData.Radial(N, N, center, k));

    [Fact]
    public void AFrameAgainstItself_IsPerfectlyClean()
    {
        var r = RatioAnalyzer.Analyze(Radial(), Radial());

        Assert.Equal(0, r.PtpPct, 9);
        Assert.Equal(0, r.CornerSpreadPct, 9);
        Assert.Equal("OK", r.Verdict);
    }

    /// <summary>
    /// Vignetting is fixed on the sensor, so it divides out even when the two masters
    /// were shot at different brightness. Only *external* structure survives.
    /// </summary>
    [Fact]
    public void AnOverallBrightnessDifference_DividesOutCompletely()
    {
        var r = RatioAnalyzer.Analyze(Radial(center: 30000), Radial(center: 20000));

        Assert.Equal(0, r.PtpPct, 9);
        Assert.Equal("OK", r.Verdict);
    }

    [Fact]
    public void TheRatioIsNormalisedToAMedianOfOne()
    {
        var r = RatioAnalyzer.Analyze(Radial(center: 30000), Radial(center: 20000));

        Assert.Equal(1.0, Stats.Percentile(r.Ratio.D.ToList(), 50), 9);
    }

    [Fact]
    public void AGradientInOneOfThePair_ShowsUpAsAProblem()
    {
        var grad = FitsTestData.WithLinearGradient(FitsTestData.Radial(N, N), N, 0.12);

        var r = RatioAnalyzer.Analyze(Radial(), TestImages.From(N, N, grad));

        Assert.Equal("PROBLEMA", r.Verdict);
        Assert.True(r.PtpPct > Th.RatioBad, "ptp was " + r.PtpPct);
        Assert.True(r.CornerSpreadPct > Th.RatioBad, "corner spread was " + r.CornerSpreadPct);
    }

    [Fact]
    public void MismatchedDimensions_AreRejected()
    {
        var small = TestImages.From(256, 256, FitsTestData.Radial(256, 256));

        var ex = Assert.Throws<Exception>(() => RatioAnalyzer.Analyze(Radial(), small));

        Assert.Contains("dimensiuni diferite", ex.Message);
    }

    /// <summary>
    /// Documented blind spot: contamination that is itself radial about the optical axis
    /// survives the 180 rotation and is reported clean. The counter-test is a distance
    /// test, not this one - so this must keep reading OK, or the docs are lying.
    /// </summary>
    [Fact]
    public void RadiallySymmetricContamination_SlipsThroughAsExpected()
    {
        // B carries an extra radial falloff centred on the same axis
        var contaminated = TestImages.From(N, N, (x, y) =>
        {
            double baseVal = FitsTestData.Radial(N, N)(x, y);
            double cx = N / 2.0, cy = N / 2.0;
            double rn = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / Math.Sqrt(cx * cx + cy * cy);
            return baseVal * (1.0 - 0.10 * rn * rn);
        });

        var r = RatioAnalyzer.Analyze(Radial(), contaminated);

        Assert.Equal("OK", r.CornerVerdict);
    }

    [Fact]
    public void TheOverallVerdictIsTheWorstOfTheTwoTests()
    {
        var grad = FitsTestData.WithLinearGradient(FitsTestData.Radial(N, N), N, 0.12);
        var r = RatioAnalyzer.Analyze(Radial(), TestImages.From(N, N, grad));

        Assert.Equal(
            Math.Max(Stats.Severity(r.PtpVerdict), Stats.Severity(r.CornerVerdict)),
            Stats.Severity(r.Verdict));
    }
}
