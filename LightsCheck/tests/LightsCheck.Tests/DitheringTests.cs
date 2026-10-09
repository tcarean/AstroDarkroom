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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>The dithering tables on hand-made position lists (R-019 to R-021).</summary>
public class DitheringTests
{
    private static DitherAnalysis Analyze(double?[] x, double?[] y, double?[]? pause = null)
        => DitherAnalyzer.Analyze(x, y, pause ?? new double?[x.Length]);

    /// <summary>Pairs at the same spot, a move every two frames: the "every 2 frames" row counts them.</summary>
    [Fact]
    public void R019_FramesBetweenMovesCountsHowOftenTheDitherWasDone()
    {
        // positions: 0,0 | 1,0 | 12,0 | 13,0 | 12,15 | 13,15 | 40,15 | 41,15 | 42,16
        double?[] x = [0, 1, 12, 13, 12, 13, 40, 41, 42];
        double?[] y = [0, 0, 0, 0, 15, 15, 15, 15, 16];
        double?[] pause = [null, 1, 30, 1, 30, 1, 30, 1, 25];
        DitherAnalysis d = Analyze(x, y, pause);

        Assert.Equal([false, false, true, false, true, false, true, false, false], d.IsMove);
        Assert.Equal(3, d.Moves);
        Assert.Equal(4, d.Pauses);
        Assert.Equal(1, d.PausesWithoutMove);       // frame 9: a pause of 25 s, a step of 1.4 px
        Assert.Equal(0, d.MovesWithoutPause);
        Assert.Equal(2, d.FramesBetweenMoves[2]);   // moves at frames 3, 5, 7: two gaps of 2
        Assert.Single(d.FramesBetweenMoves);
        Assert.Equal(3, d.LongestStretchFrames);        // frames 7, 8, 9: the move at 7, then two without
        Assert.Equal(7, d.LongestStretchFirst);
        Assert.Equal(9, d.LongestStretchLast);
        Assert.Equal(Math.Sqrt(1 + 15.0 * 15.0), d.MedianMove!.Value, 0.01);   // the moves are 11, 15.03 and 27 px
    }

    [Fact]
    public void R020_SizesAndAxesClassifyEveryMove()
    {
        double?[] x = [0, 4, 4, 30, 30, 30, 45];
        double?[] y = [0, 0, -8, -8, 7, 7, -5];
        DitherAnalysis d = Analyze(x, y);

        // steps: (4,0)=4 (under 5 px: drift, not a move) ; (0,-8)=8 ; (26,0)=26 ; (0,15)=15 ; (15,-12)=19.2
        Assert.Equal(4, d.Moves);
        Assert.Equal([1, 2, 1], d.Sizes.Select(s => s.Count).ToArray());
        Assert.Equal(5.0, d.Sizes[0].Lower);
        Assert.Null(d.Sizes[2].Upper);
        Assert.Equal(1, d.MovesOnXOnly);
        Assert.Equal(2, d.MovesOnYOnly);
        Assert.Equal(1, d.MovesOnBoth);
        Assert.Equal(2, d.MovesXPlus);
        Assert.Equal(0, d.MovesXMinus);
        Assert.Equal(1, d.MovesYPlus);
        Assert.Equal(2, d.MovesYMinus);
        Assert.Equal(45, d.SpanX);
        Assert.Equal(15, d.SpanY);
    }

    [Fact]
    public void R021_CoverageCountsRepeatsRunsAndDistinctSpots()
    {
        // 6 frames: 4 piled within 2 px, then two far apart
        double?[] x = [0, 1, 0, 2, 100, 300];
        double?[] y = [0, 1, 2, 1, 100, 300];
        DitherAnalysis d = Analyze(x, y);

        Assert.Equal(4, d.WithinTight);
        Assert.Equal(2, d.IsolatedLoose);
        Assert.Equal(3, d.DistinctMid);
        Assert.Equal(3, d.DistinctLoose);
        FrameStretch run = Assert.Single(d.RunsMid);
        Assert.Equal(1, run.FirstIndex);
        Assert.Equal(4, run.LastIndex);
        Assert.Equal(4, run.Frames);
        Assert.Equal(6, d.Registered);
        Assert.False(d.HotPixelsOk);   // four consecutive frames within 3 px
        Assert.Equal(Math.Sqrt(2), d.NearestOther[0]!.Value, 1e-9);
        Assert.Equal(Math.Sqrt(98.0 * 98.0 + 99.0 * 99.0), d.NearestOther[4]!.Value, 1e-9);   // frame 5 against frame 4 at (2, 1)
    }

    /// <summary>Big moves along one axis only: distinct and isolated, yet not two-dimensional.</summary>
    [Fact]
    public void R021_AOneDimensionalPatternFailsTheWalkingNoiseVerdict()
    {
        double?[] x = Enumerable.Range(0, 12).Select(i => (double?)(i * 15)).ToArray();
        double?[] y = Enumerable.Range(0, 12).Select(i => (double?)(i % 2)).ToArray();
        DitherAnalysis d = Analyze(x, y);
        Assert.Equal(11, d.Moves);
        Assert.Equal(11, d.MovesOnXOnly);
        Assert.Equal(12, d.DistinctLoose);
        Assert.Equal(12, d.IsolatedLoose);
        Assert.True(d.HotPixelsOk);
        Assert.False(d.WalkingNoiseOk);
    }

    [Fact]
    public void R021_ARandomTwoDimensionalPatternPassesBothVerdicts()
    {
        Random rng = new(5);
        int n = 40;
        double?[] x = new double?[n], y = new double?[n];
        double px = 0, py = 0;
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
            {
                px += (rng.NextDouble() < 0.5 ? -1 : 1) * (12 + rng.NextDouble() * 20);
                py += (rng.NextDouble() < 0.5 ? -1 : 1) * (12 + rng.NextDouble() * 20);
            }

            x[i] = px;
            y[i] = py;
        }

        DitherAnalysis d = Analyze(x, y);
        Assert.Equal(n - 1, d.Moves);
        Assert.Equal(n - 2, d.FramesBetweenMoves[1]);   // n - 1 moves, n - 2 gaps between them
        Assert.True(d.HotPixelsOk);
        Assert.True(d.WalkingNoiseOk, $"distinct {d.DistinctLoose}, isolated {d.IsolatedLoose}, both {d.MovesOnBoth}");
    }

    [Fact]
    public void R021_AnUnregisteredFrameBreaksTheStepsAndIsLeftOut()
    {
        double?[] x = [0, null, 20, 20];
        double?[] y = [0, null, 0, 1];
        DitherAnalysis d = Analyze(x, y);
        Assert.Null(d.Step[1]);
        Assert.Null(d.Step[2]);
        Assert.Equal(1.0, d.Step[3]!.Value, 1e-9);
        Assert.Equal(3, d.Registered);
        Assert.Null(d.NearestOther[1]);
    }
}
