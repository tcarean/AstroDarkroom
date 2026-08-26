namespace FlatCheck.Tests;

/// <summary>
/// Phase 4: the numeric primitives. All pure functions with hand-computable answers.
///
/// <see cref="Stats.Percentile"/> is meant to match <c>numpy.percentile</c>'s default
/// (linear interpolation between the two neighbouring order statistics), because
/// flatcheck.py is the numeric oracle - the expected values below are the ones numpy
/// produces for the same input.
/// </summary>
public class StatsTests
{
    // ---------------------------------------------------------------- Percentile

    [Fact]
    public void Percentile_AtZeroAndHundred_ReturnsTheExtremes()
    {
        var data = new List<double> { 5, 1, 4, 2, 3 };

        Assert.Equal(1, Stats.Percentile(data, 0), 9);
        Assert.Equal(5, Stats.Percentile(data, 100), 9);
    }

    [Fact]
    public void Percentile_SortsBeforeMeasuring()
    {
        Assert.Equal(2, Stats.Percentile(new List<double> { 3, 1, 2 }, 50), 9);
    }

    [Fact]
    public void Percentile_OnAnOddCount_HitsTheMiddleElementExactly()
    {
        // idx = 0.5 * (3-1) = 1 -> s[1], no interpolation
        Assert.Equal(2, Stats.Percentile(new List<double> { 1, 2, 3 }, 50), 9);
    }

    [Fact]
    public void Percentile_OnAnEvenCount_InterpolatesBetweenTheMiddleTwo()
    {
        // idx = 0.5 * (4-1) = 1.5 -> halfway between 2 and 3
        Assert.Equal(2.5, Stats.Percentile(new List<double> { 1, 2, 3, 4 }, 50), 9);
    }

    [Fact]
    public void Percentile_InterpolatesLinearly_LikeNumpy()
    {
        // idx = 0.25 * 3 = 0.75 -> 1*0.25 + 2*0.75
        Assert.Equal(1.75, Stats.Percentile(new List<double> { 1, 2, 3, 4 }, 25), 9);
    }

    [Fact]
    public void Percentile_OnASingleElement_ReturnsIt()
    {
        Assert.Equal(42, Stats.Percentile(new List<double> { 42 }, 50), 9);
        Assert.Equal(42, Stats.Percentile(new List<double> { 42 }, 0), 9);
        Assert.Equal(42, Stats.Percentile(new List<double> { 42 }, 100), 9);
    }

    /// <summary>
    /// Deliberate divergence from numpy, which raises on an empty input. Documented in
    /// CLAUDE.md: a degenerate input yields a silent 0 rather than an error.
    /// </summary>
    [Fact]
    public void Percentile_OnAnEmptyList_Returns0_DivergingFromNumpy()
    {
        Assert.Equal(0, Stats.Percentile(new List<double>(), 50), 9);
    }

    [Fact]
    public void Percentile_DoesNotMutateTheCallersList()
    {
        var data = new List<double> { 3, 1, 2 };

        Stats.Percentile(data, 50);

        Assert.Equal(new List<double> { 3, 1, 2 }, data);
    }

    // ---------------------------------------------------------------- RobustPtp

    /// <summary>
    /// Dead code - nothing calls it, and its p2/p98 default disagrees with the p5/p95
    /// that LargeScalePtp inlines. Pinned so that deleting it in Phase 8 is a conscious
    /// act; this test should be deleted along with it.
    /// </summary>
    [Fact]
    public void RobustPtp_DefaultsToP2ToP98_WhichIsNotWhatLargeScalePtpUses()
    {
        var data = Enumerable.Range(0, 101).Select(i => (double)i);

        Assert.Equal(96, Stats.RobustPtp(data), 9);          // p98 - p2 = 98 - 2
        Assert.Equal(90, Stats.RobustPtp(data, 5, 95), 9);   // what the real metric uses
    }

    // ---------------------------------------------------------------- Interp

    [Fact]
    public void Interp_ClampsBelowTheFirstNode()
    {
        Assert.Equal(10, Stats.Interp(-5, new double[] { 0, 1, 2 }, new double[] { 10, 20, 30 }), 9);
    }

    [Fact]
    public void Interp_ClampsAboveTheLastNode()
    {
        Assert.Equal(30, Stats.Interp(99, new double[] { 0, 1, 2 }, new double[] { 10, 20, 30 }), 9);
    }

    [Fact]
    public void Interp_ReturnsTheNodeValueAtAnExactNode()
    {
        Assert.Equal(20, Stats.Interp(1, new double[] { 0, 1, 2 }, new double[] { 10, 20, 30 }), 9);
    }

    [Fact]
    public void Interp_InterpolatesBetweenNodes()
    {
        Assert.Equal(15, Stats.Interp(0.5, new double[] { 0, 1, 2 }, new double[] { 10, 20, 30 }), 9);
        Assert.Equal(27.5, Stats.Interp(1.75, new double[] { 0, 1, 2 }, new double[] { 10, 20, 30 }), 9);
    }

    [Fact]
    public void Interp_HandlesUnevenNodeSpacing()
    {
        // nodes at 0 and 10: halfway in x is halfway in y
        Assert.Equal(15, Stats.Interp(5, new double[] { 0, 10 }, new double[] { 10, 20 }), 9);
    }

    [Fact]
    public void Interp_OnEmptyNodes_Returns0()
    {
        Assert.Equal(0, Stats.Interp(1, Array.Empty<double>(), Array.Empty<double>()), 9);
    }

    // ---------------------------------------------------------------- Solve

    [Fact]
    public void Solve_WithTheIdentityMatrix_ReturnsTheRightHandSide()
    {
        var a = new double[,] { { 1, 0 }, { 0, 1 } };

        var x = Stats.Solve(a, new double[] { 3, 4 }, 2);

        Assert.NotNull(x);
        Assert.Equal(3, x![0], 9);
        Assert.Equal(4, x[1], 9);
    }

    [Fact]
    public void Solve_SolvesAKnownThreeByThreeSystem()
    {
        // 2x + y - z = 8 ; -3x - y + 2z = -11 ; -2x + y + 2z = -3  ->  (2, 3, -1)
        var a = new double[,] { { 2, 1, -1 }, { -3, -1, 2 }, { -2, 1, 2 } };

        var x = Stats.Solve(a, new double[] { 8, -11, -3 }, 3);

        Assert.NotNull(x);
        Assert.Equal(2, x![0], 9);
        Assert.Equal(3, x[1], 9);
        Assert.Equal(-1, x[2], 9);
    }

    [Fact]
    public void Solve_PivotsWhenTheLeadingCoefficientIsZero()
    {
        // a zero in the first pivot position forces a row swap
        var a = new double[,] { { 0, 1 }, { 1, 0 } };

        var x = Stats.Solve(a, new double[] { 1, 2 }, 2);

        Assert.NotNull(x);
        Assert.Equal(2, x![0], 9);
        Assert.Equal(1, x[1], 9);
    }

    [Fact]
    public void Solve_OnASingularMatrix_ReturnsNull()
    {
        // second row is twice the first
        var a = new double[,] { { 1, 2 }, { 2, 4 } };

        Assert.Null(Stats.Solve(a, new double[] { 1, 2 }, 2));
    }

    // ---------------------------------------------------------------- Verdict / Rank

    [Fact]
    public void Verdict_BelowTheOkThreshold_IsOk()
    {
        Assert.Equal("OK", Stats.Verdict(0.5, 1.5, 4.0));
    }

    [Fact]
    public void Verdict_ExactlyAtTheOkThreshold_IsAlreadySuspect()
    {
        // the comparison is strict "<", so the boundary falls into the worse band
        Assert.Equal("SUSPECT", Stats.Verdict(1.5, 1.5, 4.0));
    }

    [Fact]
    public void Verdict_ExactlyAtTheBadThreshold_IsAlreadyAProblem()
    {
        Assert.Equal("PROBLEMA", Stats.Verdict(4.0, 1.5, 4.0));
    }

    /// <summary>
    /// NaN compares false against both bounds, so a degenerate metric (too few tiles, no
    /// valid rings) degrades to PROBLEMA rather than passing silently. This matches
    /// flatcheck.py and is how bad inputs surface at all - do not "fix" it.
    /// </summary>
    [Fact]
    public void Verdict_OnNaN_DegradesToProblema()
    {
        Assert.Equal("PROBLEMA", Stats.Verdict(double.NaN, 1.5, 4.0));
    }

    [Theory]
    [InlineData("OK", 0)]
    [InlineData("SUSPECT", 1)]
    [InlineData("PROBLEMA", 2)]
    public void Rank_OrdersTheVerdicts(string verdict, int expected)
    {
        Assert.Equal(expected, Stats.Rank(verdict));
    }

    [Fact]
    public void Rank_TreatsAnUnknownStringAsTheWorstCase()
    {
        Assert.Equal(2, Stats.Rank("something else"));
    }

    [Fact]
    public void Rank_LetsTheWorstVerdictWinAnOrdering()
    {
        var verdicts = new[] { "OK", "PROBLEMA", "SUSPECT" };

        string worst = verdicts.OrderByDescending(Stats.Rank).First();

        Assert.Equal("PROBLEMA", worst);
    }
}
