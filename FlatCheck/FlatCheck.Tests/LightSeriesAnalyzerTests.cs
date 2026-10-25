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
/// Combining a series of --light results into a single verdict.
///
/// The idea this file defends: the mean over the series estimates the flat's error ONLY
/// if the sky's radial term averages to zero, and that is not guaranteed. So it is
/// checked, through three conditions, and if any fails the answer is NEDETERMINAT
/// instead of a number that looks confident. The tests below pin each condition
/// separately and - just as important - pin that a clean series really gets a verdict,
/// so that the guard does not become a permanent refusal.
///
/// The data in <see cref="RealSessionSweep"/> are the real figures of the session
/// 2026-08-12-Smida, which motivated the whole mode.
/// </summary>
public class LightSeriesAnalyzerTests
{
    /// <summary>A --light result with only the figures that matter to the combination.</summary>
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

    /// <summary>n frames placed symmetrically around the mean, with a constant sky
    /// gradient.</summary>
    static List<(string, LightResult)> Steady(int n, double mean, double jitter = 0.0)
        => Enumerable.Range(0, n)
            .Select(i => Frame($"f{i:00}.fit", mean + (i % 2 == 0 ? jitter : -jitter), 10.0))
            .ToList();

    // ------------------------------------------------------------ statistics

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

    /// <summary>The standard deviation is the sample one (ddof = 1), like
    /// numpy.std(ddof=1).</summary>
    [Fact]
    public void TheStandardDeviationUsesTheSampleDivisor()
    {
        // 0,2,4,6,8: mean 4, sum of squared deviations 40, /(5-1) = 10 -> sqrt(10)
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
    /// On a constant series the question "do they move together?" has no answer; it does
    /// not have the answer "no". That is why it is NaN, and why the correlation test is
    /// skipped altogether in that case (see
    /// <see cref="AConstantSkyGradientSkipsTheCorrelationTest"/>).
    /// </summary>
    [Fact]
    public void PearsonIsNaNWhenEitherSeriesIsConstant()
    {
        Assert.True(double.IsNaN(LightSeriesAnalyzer.Pearson(
            new double[] { 1, 1, 1 }, new double[] { 1, 2, 3 })));
    }

    // ------------------------------------------------------ conclusive verdict

    [Fact]
    public void ATightSeriesGetsARealVerdict()
    {
        var r = LightSeriesAnalyzer.Combine(Steady(10, mean: 0.5, jitter: 0.2));

        Assert.True(r.Conclusive);
        Assert.Equal("OK", r.Verdict);
        Assert.Empty(r.Reasons);
    }

    /// <summary>
    /// A series tight around a large error still gets a verdict - a bad one. The guard
    /// separates "I do not know" from "it is bad", it does not confuse them.
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

    // ------------------------------------------------------ inconclusive verdict

    [Fact]
    public void R032_TooFewFramesIsNotEnoughToAverage()
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
    /// Large scatter with a constant sky gradient: the correlation does not apply, but the
    /// error of the mean alone is enough to stop the verdict.
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
    /// The case that gives the mode all its value: the mean is practically zero and would
    /// pass the OK threshold, but the figure follows the sky gradient. The flat is the same
    /// on all frames by construction, so what moves is the sky - and the verdict refuses to
    /// pronounce.
    /// </summary>
    [Fact]
    public void AMeanNearZeroIsStillRefusedWhenItTracksTheSky()
    {
        var r = LightSeriesAnalyzer.Combine(RealSessionSweep());

        Assert.Equal(0.23, r.MeanRad, 2);              // would have passed as OK
        Assert.Equal(3.16, r.Sem, 2);
        Assert.Equal(-0.94, r.SkyCorr, 2);
        Assert.Equal(LightSeriesAnalyzer.Undetermined, r.Verdict);
        Assert.Contains("sem", r.Reasons);
        Assert.Contains("corelatie", r.Reasons);
    }

    /// <summary>
    /// The correlation is always computed, but it is JUDGED only when the gradient really
    /// varies. On a clean session where it stays constant, the correlation is noise and
    /// would reject good data.
    /// </summary>
    [Fact]
    public void AConstantSkyGradientSkipsTheCorrelationTest()
    {
        // perfect correlation over a minute gradient interval: ignored
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

    // ------------------------------------------------------------ degenerate cases

    /// <summary>
    /// A single frame has no scatter to compute, so sem comes out NaN. NaN must fall on the
    /// bad side - the comparisons are written as !(x &lt; threshold) precisely for that. If
    /// they were written as (x &gt;= threshold), NaN would pass SILENTLY and a single frame
    /// would get a verdict it cannot support.
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
    /// A mean map makes no sense over frames of different sizes; better to stop here than
    /// to produce a mixed map from which nothing can be read.
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

        // each map is constant at 1 + rad/100, so the mean is 1 + 0.04
        Assert.All(r.RadialMap.D, v => Assert.Equal(1.04, v, 9));
    }

    /// <summary>
    /// The real figures of the session 2026-08-12-Smida: 32 frames, 81 minutes, the Milky
    /// Way at 20 mm. The radial component sweeps smoothly from +3.13 through -19.20 to
    /// +35.73 while the sky gradient falls from 88 to 49.
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
