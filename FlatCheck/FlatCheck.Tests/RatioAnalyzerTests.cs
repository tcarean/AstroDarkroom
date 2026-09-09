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
/// Mode 2, rewritten as attribution. The mode no longer judges the *setup*; it splits the
/// master's non-radial structure into the part fixed to the camera - which is what the
/// combined master (A+B)/2 carries, and where the verdict comes from - and the part fixed
/// in the world, which the rotation cancels and which is reported with an error bar.
///
/// The synthetic pairs below encode the geometry explicitly: a world-fixed gradient is
/// one that INVERTS between A and B, a camera-fixed one is identical in both.
/// </summary>
public class RatioAnalyzerTests
{
    const int N = 512;

    static Img Radial(double center = 30000, double k = 0.45)
        => TestImages.From(N, N, FitsTestData.Radial(N, N, center, k));

    /// <summary>Horizontal ramp of total amplitude g, as seen at sensor position x.</summary>
    static Img RadialWithRamp(double g, double center = 30000)
        => TestImages.From(N, N, FitsTestData.WithLinearGradient(FitsTestData.Radial(N, N, center), N, g));

    [Fact]
    public void AFrameAgainstItself_ReportsNoWorldComponent()
    {
        var r = RatioAnalyzer.Analyze(Radial(), Radial());

        Assert.Equal(0, r.WorldPtpPct, 9);
        Assert.Equal(0, r.WorldErrPct, 9);
        Assert.Equal(0, r.WorldCornerPct, 9);
    }

    /// <summary>
    /// The verdict is the structure of the combined master, not a judgement about the
    /// setup. Against itself the combined master IS the frame, so the two must agree.
    /// </summary>
    [Fact]
    public void TheVerdictIsTheStructureVerdictOfTheCombinedMaster()
    {
        var r = RatioAnalyzer.Analyze(Radial(), Radial());

        Assert.Equal(StructureAnalyzer.Analyze(Radial()).Verdict, r.Verdict);
        Assert.Same(r.Sensor, r.Sensor);
        Assert.Equal(r.Sensor.Verdict, r.Verdict);
    }

    /// <summary>
    /// Each stack is normalised to a median of 1 before averaging - Siril's
    /// "multiplicative normalized input". Without it a fading sky would weight the
    /// brighter stack more and the gradient would not fully cancel.
    /// </summary>
    [Fact]
    public void AnOverallBrightnessDifference_DividesOutCompletely()
    {
        var r = RatioAnalyzer.Analyze(Radial(center: 30000), Radial(center: 20000));

        Assert.Equal(0, r.WorldPtpPct, 9);
        Assert.Equal(1.0, Stats.Percentile(r.World.D.ToList(), 50), 9);
    }

    /// <summary>
    /// The defining case: a gradient that INVERTS between the two orientations is fixed
    /// in the world. It must land in the world component, and the combined master must
    /// come out as clean as the ungradiented frame.
    /// </summary>
    [Fact]
    public void AGradientThatInvertsBetweenTheTwo_IsAttributedToTheWorld()
    {
        var r = RatioAnalyzer.Analyze(RadialWithRamp(+0.12), RadialWithRamp(-0.12));

        // The ramp spans 12% peak-to-peak in A and the opposite in B, so A/B carries
        // ~24% and the per-orientation figure is back to ~12% - trimmed to ~10.8% by the
        // p95/p5 the amplitude is measured with.
        Assert.InRange(r.WorldPtpPct, 9.0, 12.0);
        // A pure linear ramp is exactly antisymmetric: nothing left over.
        Assert.True(r.WorldErrPct < 0.05, "error bar was " + r.WorldErrPct);
        // ...and the combined master is back to the clean radial field.
        Assert.Equal(StructureAnalyzer.Analyze(Radial()).Verdict, r.Sensor.Verdict);
    }

    /// <summary>
    /// The mirror case, and the one the old mode was blind to: a gradient present in the
    /// SAME sensor orientation in both stacks does not invert, so it is fixed to the
    /// camera. It must NOT appear in the world component - it must survive into the
    /// combined master and be judged there.
    /// </summary>
    [Fact]
    public void AGradientPresentInBothOrientations_IsAttributedToTheCamera()
    {
        var r = RatioAnalyzer.Analyze(RadialWithRamp(+0.12), RadialWithRamp(+0.12));

        Assert.Equal(0, r.WorldPtpPct, 9);
        Assert.True(r.Sensor.TiltPct > 5.0, "tilt was " + r.Sensor.TiltPct);
        Assert.Equal(StructureAnalyzer.Analyze(RadialWithRamp(+0.12)).Verdict, r.Verdict);
    }

    /// <summary>
    /// The error bar is not calibrated, it is derived: a world-fixed signal goes entirely
    /// into the odd part and zero into the even part, while noise splits with equal
    /// variance. So on a pair with no antisymmetric relation at all - two independent
    /// noise realisations - the two must come out comparable.
    /// </summary>
    [Fact]
    public void WithoutARotation_TheErrorBarMatchesTheSignal()
    {
        var a = TestImages.From(N, N, Noisy(FitsTestData.Radial(N, N), seed: 1));
        var b = TestImages.From(N, N, Noisy(FitsTestData.Radial(N, N), seed: 2));

        var r = RatioAnalyzer.Analyze(a, b);

        double ratio = r.WorldPtpPct / r.WorldErrPct;
        Assert.InRange(ratio, 0.5, 2.0);
    }

    static Func<int, int, double> Noisy(Func<int, int, double> b, int seed)
    {
        var rng = new Random(seed);
        var noise = new double[N * N];
        for (int i = 0; i < noise.Length; i++) noise[i] = 1.0 + (rng.NextDouble() - 0.5) * 0.04;
        return (x, y) => b(x, y) * noise[y * N + x];
    }

    [Fact]
    public void MismatchedDimensions_AreRejected()
    {
        var small = TestImages.From(256, 256, FitsTestData.Radial(256, 256));

        var ex = Assert.Throws<Exception>(() => RatioAnalyzer.Analyze(Radial(), small));

        Assert.Contains("dimensiuni diferite", ex.Message);
    }

    /// <summary>
    /// Documented blind spot, and the reason the mode reports attribution rather than a
    /// clean bill of health: contamination that is radial about the optical axis survives
    /// the rotation, so it is indistinguishable from the lens. It must read as
    /// camera-fixed here - the counter-test is a distance test, not this one.
    /// </summary>
    [Fact]
    public void RadiallySymmetricContamination_IsCountedAsCameraFixed()
    {
        var contaminated = TestImages.From(N, N, (x, y) =>
        {
            double baseVal = FitsTestData.Radial(N, N)(x, y);
            double cx = N / 2.0, cy = N / 2.0;
            double rn = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / Math.Sqrt(cx * cx + cy * cy);
            return baseVal * (1.0 - 0.10 * rn * rn);
        });

        var r = RatioAnalyzer.Analyze(contaminated, contaminated);

        Assert.Equal(0, r.WorldPtpPct, 9);
    }
}
