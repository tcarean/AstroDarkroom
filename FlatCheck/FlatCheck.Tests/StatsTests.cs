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
    /// docs/design/python-parity.md, "Intentional divergences of the frozen file" (ADR 0038):
    /// a degenerate input yields a silent 0 rather than an error.
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

    /// <summary>
    /// Severity spans both ladders, so a structure verdict (PERFECT/BUN/ACCEPTABIL/REFA)
    /// and a centroid verdict (OK/SUSPECT/PROBLEMA) can be compared to pick the worst.
    /// </summary>
    [Theory]
    [InlineData("OK", 0)]
    [InlineData("PERFECT", 0)]
    [InlineData("BUN", 1)]
    [InlineData("SUSPECT", 2)]
    [InlineData("ACCEPTABIL", 2)]
    [InlineData("PROBLEMA", 3)]
    [InlineData("REFA", 3)]
    public void Severity_SpansBothLadders(string verdict, int expected)
    {
        Assert.Equal(expected, Stats.Severity(verdict));
    }

    [Fact]
    public void Severity_TreatsAnUnknownStringAsTheWorstCase()
    {
        Assert.Equal(3, Stats.Severity("something else"));
    }

    [Fact]
    public void Severity_LetsTheWorstVerdictWinAnOrdering()
    {
        var verdicts = new[] { "PERFECT", "REFA", "BUN" };

        string worst = verdicts.OrderByDescending(Stats.Severity).First();

        Assert.Equal("REFA", worst);
    }

    // ---------------------------------------------------------------- ResidualVerdict

    /// <summary>
    /// The scale shared by the three structure deviation tests:
    ///   &lt; 2 %    perfect
    ///   2 - 10 %  usable, background extraction removes what is left
    ///   10 - 20 % usable, but confirm with --light before a hard stretch
    ///   &gt; 20 %  reshoot
    /// Boundaries are strict "&lt;", so each one falls into the worse band.
    /// </summary>
    [Theory]
    [InlineData(0.0, "PERFECT")]
    [InlineData(1.99, "PERFECT")]
    [InlineData(2.0, "BUN")]          // boundary
    [InlineData(9.99, "BUN")]
    [InlineData(10.0, "ACCEPTABIL")]  // boundary
    [InlineData(19.99, "ACCEPTABIL")]
    [InlineData(20.0, "REFA")]        // boundary
    [InlineData(500.0, "REFA")]
    public void ResidualVerdict_Bands(double value, string expected)
    {
        Assert.Equal(expected, Stats.ResidualVerdict(value));
    }

    /// <summary>Same NaN rule as Verdict: every comparison is false, so it degrades.</summary>
    [Fact]
    public void ResidualVerdict_OnNaN_DegradesToReshoot()
    {
        Assert.Equal("REFA", Stats.ResidualVerdict(double.NaN));
    }

    [Fact]
    public void ResidualBands_AreListedWorstLast()
    {
        Assert.Equal(new[] { "PERFECT", "BUN", "ACCEPTABIL", "REFA" }, Stats.ResidualBands);
        for (int i = 0; i < Stats.ResidualBands.Length; i++)
            Assert.Equal(i, Stats.Severity(Stats.ResidualBands[i]));
    }
}
