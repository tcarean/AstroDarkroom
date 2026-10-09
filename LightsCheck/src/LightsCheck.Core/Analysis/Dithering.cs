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

namespace LightsCheck.Core;

/// <summary>A stretch of consecutive frames that stayed within a radius of the first one.</summary>
internal sealed record FrameStretch(int FirstIndex, int LastIndex, int Frames);

/// <summary>A count per size class of the dither moves, in pixels, the classes bounded by Th.DitherSizeBounds.</summary>
internal sealed record SizeClass(double Lower, double? Upper, int Count);

/// <summary>
/// The dithering of a session, from the registered positions (R-019 to R-021): how often it
/// was done, how far, along which axes, and how well the positions cover the sensor. Every
/// index is 1-based like FrameInfo.Index; a frame without a position (registration failed)
/// has null in the per-frame arrays and breaks a step.
/// </summary>
internal sealed record DitherAnalysis(
    IReadOnlyList<double?> Step,
    IReadOnlyList<double?> StepX,
    IReadOnlyList<double?> StepY,
    IReadOnlyList<bool> IsMove,
    int Moves,
    int Pauses,
    int PausesWithoutMove,
    int MovesWithoutPause,
    IReadOnlyDictionary<int, int> FramesBetweenMoves,
    int LongestStretchFrames,
    int LongestStretchFirst,
    int LongestStretchLast,
    double? MedianMove,
    IReadOnlyList<SizeClass> Sizes,
    int MovesOnXOnly,
    int MovesOnYOnly,
    int MovesOnBoth,
    int MovesXPlus,
    int MovesXMinus,
    int MovesYPlus,
    int MovesYMinus,
    IReadOnlyList<double?> NearestOther,
    int WithinTight,
    int WithinMid,
    int WithinLoose,
    int IsolatedLoose,
    int DistinctMid,
    int DistinctLoose,
    IReadOnlyList<FrameStretch> RunsMid,
    IReadOnlyList<FrameStretch> RunsLoose,
    double SpanX,
    double SpanY,
    int Registered,
    bool HotPixelsOk,
    bool WalkingNoiseOk);

internal static class DitherAnalyzer
{
    /// <summary>
    /// <paramref name="x"/> and <paramref name="y"/> are the registered positions per frame
    /// (null where unknown); <paramref name="pause"/> the seconds before each frame (null
    /// where unknown).
    /// </summary>
    internal static DitherAnalysis Analyze(IReadOnlyList<double?> x, IReadOnlyList<double?> y, IReadOnlyList<double?> pause)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(pause);
        int n = x.Count;
        double?[] step = new double?[n], sx = new double?[n], sy = new double?[n];
        bool[] move = new bool[n];
        for (int i = 1; i < n; i++)
        {
            if (x[i] is double xi && y[i] is double yi && x[i - 1] is double xp && y[i - 1] is double yp)
            {
                sx[i] = xi - xp;
                sy[i] = yi - yp;
                step[i] = Math.Sqrt((xi - xp) * (xi - xp) + (yi - yp) * (yi - yp));
                move[i] = step[i] >= Th.DitherStep;
            }
        }

        int moves = move.Count(m => m);
        int pauses = 0, pausesWithoutMove = 0, movesWithoutPause = 0;
        for (int i = 1; i < n; i++)
        {
            bool paused = pause[i] is double p && p > Th.DitherPause;
            if (paused)
            {
                pauses++;
            }

            if (paused && step[i] is not null && !move[i])
            {
                pausesWithoutMove++;
            }

            if (move[i] && !paused && pause[i] is not null)
            {
                movesWithoutPause++;
            }
        }

        // How often: frames between one move and the next (1 = every frame).
        Dictionary<int, int> between = [];
        int last = -1, longest = 0, longestFirst = 0, longestLast = 0;
        for (int i = 0; i < n; i++)
        {
            if (move[i])
            {
                if (last >= 0)
                {
                    int gap = i - last;
                    between[gap] = between.TryGetValue(gap, out int c) ? c + 1 : 1;
                }

                last = i;
            }
        }

        // The longest stretch of frames without a move: from a move (or the start) to the next.
        int start = 0;
        for (int i = 1; i <= n; i++)
        {
            if (i == n || move[i])
            {
                int len = i - start;
                if (len > longest)
                {
                    longest = len;
                    longestFirst = start + 1;
                    longestLast = i;
                }

                start = i;
            }
        }

        // How far: size classes and axes.
        List<double> moveSizes = [];
        List<SizeClass> sizes = [];
        int xOnly = 0, yOnly = 0, both = 0, xPlus = 0, xMinus = 0, yPlus = 0, yMinus = 0;
        for (int i = 0; i < n; i++)
        {
            if (!move[i])
            {
                continue;
            }

            moveSizes.Add(step[i]!.Value);
            bool onX = Math.Abs(sx[i]!.Value) >= Th.AxisStep, onY = Math.Abs(sy[i]!.Value) >= Th.AxisStep;
            if (onX && onY)
            {
                both++;
            }
            else if (onX)
            {
                xOnly++;
            }
            else if (onY)
            {
                yOnly++;
            }

            if (onX)
            {
                if (sx[i] > 0)
                {
                    xPlus++;
                }
                else
                {
                    xMinus++;
                }
            }
            if (onY)
            {
                if (sy[i] > 0)
                {
                    yPlus++;
                }
                else
                {
                    yMinus++;
                }
            }
        }

        double lower = Th.DitherStep;
        foreach (double upper in Th.DitherSizeBounds)
        {
            sizes.Add(new SizeClass(lower, upper, moveSizes.Count(s => s >= lower && s < upper)));
            lower = upper;
        }

        sizes.Add(new SizeClass(lower, null, moveSizes.Count(s => s >= lower)));

        // Coverage: every registered frame against every other.
        List<int> reg = Enumerable.Range(0, n).Where(i => x[i] is not null && y[i] is not null).ToList();
        double?[] nearest = new double?[n];
        int tight = 0, mid = 0, loose = 0, isolated = 0;
        foreach (int i in reg)
        {
            double best = double.MaxValue;
            foreach (int j in reg)
            {
                if (j != i)
                {
                    best = Math.Min(best, Dist(x, y, i, j));
                }
            }

            if (best < double.MaxValue)
            {
                nearest[i] = best;
                if (best < Th.CoverageTight)
                {
                    tight++;
                }

                if (best < Th.CoverageMid)
                {
                    mid++;
                }

                if (best < Th.CoverageLoose)
                {
                    loose++;
                }

                if (best >= Th.CoverageLoose)
                {
                    isolated++;
                }
            }
        }

        double spanX = reg.Count > 0 ? reg.Max(i => x[i]!.Value) - reg.Min(i => x[i]!.Value) : 0;
        double spanY = reg.Count > 0 ? reg.Max(i => y[i]!.Value) - reg.Min(i => y[i]!.Value) : 0;
        List<FrameStretch> runsMid = Runs(x, y, Th.CoverageMid), runsLoose = Runs(x, y, Th.CoverageLoose);
        int longestTight = Runs(x, y, Th.CoverageTight).Select(r => r.Frames).DefaultIfEmpty(1).Max();
        int distinctMid = Distinct(x, y, reg, Th.CoverageMid), distinctLoose = Distinct(x, y, reg, Th.CoverageLoose);

        bool hotOk = longestTight <= Th.HotPixelRunMax;
        bool twoD = moves > 0 && (xOnly + both) >= Th.TwoDAxisFraction * moves && (yOnly + both) >= Th.TwoDAxisFraction * moves;
        bool walkingOk = reg.Count > 0 && distinctLoose >= Th.WalkingNoiseDistinctFraction * reg.Count
            && isolated >= Th.WalkingNoiseIsolatedFraction * reg.Count && twoD;

        return new DitherAnalysis(step, sx, sy, move, moves, pauses, pausesWithoutMove, movesWithoutPause, between,
            longest, longestFirst, longestLast,
            moveSizes.Count > 0 ? Raster.Percentile(moveSizes, 50) : null, sizes,
            xOnly, yOnly, both, xPlus, xMinus, yPlus, yMinus,
            nearest, tight, mid, loose, isolated, distinctMid, distinctLoose, runsMid, runsLoose, spanX, spanY, reg.Count, hotOk, walkingOk);
    }

    private static double Dist(IReadOnlyList<double?> x, IReadOnlyList<double?> y, int i, int j)
    {
        double dx = x[i]!.Value - x[j]!.Value, dy = y[i]!.Value - y[j]!.Value;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Stretches of Th.RunMinFrames or more consecutive registered frames within <paramref name="radius"/> of the stretch's first frame.</summary>
    private static List<FrameStretch> Runs(IReadOnlyList<double?> x, IReadOnlyList<double?> y, double radius)
    {
        List<FrameStretch> runs = [];
        int n = x.Count, s = 0;
        for (int i = 1; i <= n; i++)
        {
            bool breaks = i == n || x[i] is null || y[i] is null || x[s] is null || y[s] is null || Dist(x, y, i, s) > radius;
            if (breaks)
            {
                if (i - s >= Th.RunMinFrames && x[s] is not null)
                {
                    runs.Add(new FrameStretch(s + 1, i, i - s));
                }

                s = i;
            }
        }

        return runs;
    }

    /// <summary>Greedy clustering: a frame joins the first cluster whose seed is within <paramref name="radius"/>, else seeds a new one.</summary>
    private static int Distinct(IReadOnlyList<double?> x, IReadOnlyList<double?> y, List<int> reg, double radius)
    {
        List<int> seeds = [];
        foreach (int i in reg)
        {
            if (!seeds.Any(s => Dist(x, y, i, s) <= radius))
            {
                seeds.Add(i);
            }
        }

        return seeds.Count;
    }
}
