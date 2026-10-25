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
/// <param name="FirstIndex">The first frame of the stretch, 1-based position in the series.</param>
/// <param name="LastIndex">The last frame of the stretch, 1-based position in the series.</param>
/// <param name="Frames">
/// How many frames the stretch holds, <paramref name="LastIndex"/> - <paramref name="FirstIndex"/>
/// + 1.
/// </param>
internal sealed record FrameStretch(int FirstIndex, int LastIndex, int Frames);

/// <summary>
/// One frame's place in a run at <see cref="Th.CoverageLoose"/>: the k-th of n frames from
/// frame f (R-034).
/// </summary>
/// <param name="Ordinal">k, the frame's place in the run, from 1.</param>
/// <param name="Frames">n, how many frames the run holds.</param>
/// <param name="FirstIndex">f, the run's first frame, 1-based position in the series.</param>
internal sealed record SpotRun(int Ordinal, int Frames, int FirstIndex);

/// <summary>
/// A count per size class of the dither moves, in pixels, the classes bounded by
/// <see cref="Th.DitherSizeBounds"/>.
/// </summary>
/// <param name="Lower">The class's lower bound, in pixels, included.</param>
/// <param name="Upper">
/// The class's upper bound, in pixels, excluded; null for the last, open class.
/// </param>
/// <param name="Count">How many moves fall in the class.</param>
internal sealed record SizeClass(double Lower, double? Upper, int Count);

/// <summary>
/// The dithering of a session, from the registered positions (R-019 to R-021): how often it
/// was done, how far, along which axes, and how well the positions cover the sensor. Every
/// index is 1-based like FrameInfo.Index; a frame without a position (registration failed)
/// has null in the per-frame arrays and breaks a step.
/// </summary>
/// <param name="Step">
/// Per frame, the length of the step from the previous frame, in pixels; null for the first frame
/// and where either frame has no position.
/// </param>
/// <param name="StepX">
/// Per frame, the X component of that step, in pixels; null as <paramref name="Step"/>.
/// </param>
/// <param name="StepY">
/// Per frame, the Y component of that step, in pixels; null as <paramref name="Step"/>.
/// </param>
/// <param name="IsMove">
/// Per frame, whether its step reaches <see cref="Th.DitherStep"/>: a dither move (ADR 0007).
/// </param>
/// <param name="Moves">How many frames are moves.</param>
/// <param name="Pauses">
/// How many frames, the first excepted, follow a pause longer than <see cref="Th.DitherPause"/>.
/// </param>
/// <param name="PausesWithoutMove">The pauses whose frame has a step that is not a move.</param>
/// <param name="MovesWithoutPause">
/// The moves whose pause is known and not longer than <see cref="Th.DitherPause"/>.
/// </param>
/// <param name="FramesBetweenMoves">
/// How many times each gap between one move and the next occurs: the key is the gap in frames (1 =
/// every frame), the value the count.
/// </param>
/// <param name="LongestStretchFrames">
/// The length, in frames, of the longest stretch without a move, from a move (or the first frame)
/// up to the frame before the next move (or the last frame).
/// </param>
/// <param name="LongestStretchFirst">
/// That stretch's first frame, 1-based; 0 for an empty list.
/// </param>
/// <param name="LongestStretchLast">
/// That stretch's last frame, 1-based; 0 for an empty list.
/// </param>
/// <param name="MedianMove">The median length of the moves, in pixels; null without a move.</param>
/// <param name="Sizes">
/// The moves counted per size class, from <see cref="Th.DitherStep"/> through
/// <see cref="Th.DitherSizeBounds"/> to an open last class.
/// </param>
/// <param name="MovesOnXOnly">
/// The moves with an X component of at least <see cref="Th.AxisStep"/> and a Y component under it.
/// </param>
/// <param name="MovesOnYOnly">
/// The moves with a Y component of at least <see cref="Th.AxisStep"/> and an X component under it.
/// </param>
/// <param name="MovesOnBoth">
/// The moves with both components of at least <see cref="Th.AxisStep"/>.
/// </param>
/// <param name="MovesXPlus">
/// The moves with an X component of at least <see cref="Th.AxisStep"/>, positive.
/// </param>
/// <param name="MovesXMinus">
/// The moves with an X component of at least <see cref="Th.AxisStep"/>, negative.
/// </param>
/// <param name="MovesYPlus">
/// The moves with a Y component of at least <see cref="Th.AxisStep"/>, positive.
/// </param>
/// <param name="MovesYMinus">
/// The moves with a Y component of at least <see cref="Th.AxisStep"/>, negative.
/// </param>
/// <param name="NearestOther">
/// Per frame, the distance to the nearest other registered frame, in pixels; null for a frame
/// without a position or the only registered one.
/// </param>
/// <param name="WithinTight">
/// The registered frames whose nearest other frame is closer than <see cref="Th.CoverageTight"/>.
/// </param>
/// <param name="WithinMid">The same, closer than <see cref="Th.CoverageMid"/>.</param>
/// <param name="WithinLoose">The same, closer than <see cref="Th.CoverageLoose"/>.</param>
/// <param name="IsolatedLoose">
/// The registered frames whose nearest other frame is at least <see cref="Th.CoverageLoose"/> away.
/// </param>
/// <param name="DistinctMid">
/// The distinct positions: the clusters of the greedy clustering at <see cref="Th.CoverageMid"/>.
/// </param>
/// <param name="DistinctLoose">The same at <see cref="Th.CoverageLoose"/>.</param>
/// <param name="LargestClusterTight">
/// The frames of the largest greedy cluster at <see cref="Th.CoverageTight"/>; 0 without a
/// registered frame.
/// </param>
/// <param name="SharingPairs">
/// The pairs of registered frames closer than <see cref="Th.PatternSharePx"/>.
/// </param>
/// <param name="PatternSurviving">
/// The fraction of one frame's pattern the mean of the stack keeps, (n + 2 pairs) / n^2 over the n
/// registered frames (R-048); null without a registered frame.
/// </param>
/// <param name="WalkingStretch">
/// The longest stretch of two or more consecutive registered frames within
/// <see cref="Th.CoverageLoose"/> of its first (R-043); null when there is none.
/// </param>
/// <param name="RunsMid">
/// The runs of <see cref="Th.RunMinFrames"/> or more consecutive frames within
/// <see cref="Th.CoverageMid"/> of the run's first, in order.
/// </param>
/// <param name="RunsLoose">The same within <see cref="Th.CoverageLoose"/>.</param>
/// <param name="SpotRun">
/// Per frame, its place in a run of <paramref name="RunsLoose"/>; null outside a run.
/// </param>
/// <param name="SpanX">
/// The extent of the registered positions along X, in pixels; 0 without a registered frame.
/// </param>
/// <param name="SpanY">The same along Y.</param>
/// <param name="Registered">How many frames have a position.</param>
/// <param name="HotPixelsOk">
/// Whether the longest run at <see cref="Th.CoverageTight"/> holds no more than
/// <see cref="Th.HotPixelRunMax"/> frames.
/// </param>
/// <param name="WalkingNoiseOk">
/// Whether the dithering keeps walking noise away: enough distinct positions
/// (<see cref="Th.WalkingNoiseDistinctFraction"/>) and isolated frames
/// (<see cref="Th.WalkingNoiseIsolatedFraction"/>) at <see cref="Th.CoverageLoose"/>, and moves
/// along both axes (<see cref="Th.TwoDAxisFraction"/>).
/// </param>
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
    int LargestClusterTight,
    int SharingPairs,
    double? PatternSurviving,
    FrameStretch? WalkingStretch,
    IReadOnlyList<FrameStretch> RunsMid,
    IReadOnlyList<FrameStretch> RunsLoose,
    IReadOnlyList<SpotRun?> SpotRun,
    double SpanX,
    double SpanY,
    int Registered,
    bool HotPixelsOk,
    bool WalkingNoiseOk);

/// <summary>
/// Reads the dithering of a session from the frames' registered positions and pauses (R-019 to
/// R-021).
/// </summary>
internal static class DitherAnalyzer
{
    /// <summary>
    /// <paramref name="x"/> and <paramref name="y"/> are the registered positions per frame
    /// (null where unknown); <paramref name="pause"/> the seconds before each frame (null
    /// where unknown).
    /// </summary>
    /// <param name="x">
    /// Per frame, the registered X position, in pixels (the registration's shift); null where
    /// unknown. Not null.
    /// </param>
    /// <param name="y">
    /// Per frame, the registered Y position, in pixels; null where unknown. As long as
    /// <paramref name="x"/>. Not null.
    /// </param>
    /// <param name="pause">
    /// Per frame, the seconds of pause before it; null where unknown. As long as
    /// <paramref name="x"/>. Not null.
    /// </param>
    /// <returns>
    /// The steps, moves, pauses, sizes, axes, coverage, runs and the two verdicts.
    /// </returns>
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

        // The frames that share the dash of walking noise (R-043): the longest stretch of two
        // or more consecutive registered frames within CoverageLoose of its first. A pair
        // counts: two frames at one spot with a large drift between them draw a dash as long
        // as that drift, which the runs of RunMinFrames above would miss.
        FrameStretch? walkingStretch = Runs(x, y, Th.CoverageLoose, 2).MaxBy(st => st.Frames);
        SpotRun?[] spot = new SpotRun?[n];
        foreach (FrameStretch run in runsLoose)
        {
            for (int k = 0; k < run.Frames; k++)
            {
                spot[run.FirstIndex - 1 + k] = new SpotRun(k + 1, run.Frames, run.FirstIndex);
            }
        }

        int longestTight = Runs(x, y, Th.CoverageTight).Select(r => r.Frames).DefaultIfEmpty(1).Max();
        int distinctMid = Clusters(x, y, reg, Th.CoverageMid).Count, distinctLoose = Clusters(x, y, reg, Th.CoverageLoose).Count;
        int largestTight = Clusters(x, y, reg, Th.CoverageTight).DefaultIfEmpty(0).Max();

        // How much of one frame's pattern the mean of the stack keeps (R-048): every frame adds
        // its own copy (1/n of the mean), and every pair closer than PatternSharePx adds the
        // same pixels twice more, so (n + 2 pairs) / n^2; 1/n with no pair, 1 with every frame
        // at one spot.
        int sharing = 0;
        for (int a = 0; a < reg.Count; a++)
        {
            for (int b = a + 1; b < reg.Count; b++)
            {
                if (Dist(x, y, reg[a], reg[b]) < Th.PatternSharePx)
                {
                    sharing++;
                }
            }
        }

        double? surviving = reg.Count > 0 ? (reg.Count + 2.0 * sharing) / ((double)reg.Count * reg.Count) : null;

        bool hotOk = longestTight <= Th.HotPixelRunMax;
        bool twoD = moves > 0 && (xOnly + both) >= Th.TwoDAxisFraction * moves && (yOnly + both) >= Th.TwoDAxisFraction * moves;
        bool walkingOk = reg.Count > 0 && distinctLoose >= Th.WalkingNoiseDistinctFraction * reg.Count
            && isolated >= Th.WalkingNoiseIsolatedFraction * reg.Count && twoD;

        return new DitherAnalysis(step, sx, sy, move, moves, pauses, pausesWithoutMove, movesWithoutPause, between,
            longest, longestFirst, longestLast,
            moveSizes.Count > 0 ? Raster.Percentile(moveSizes, 50) : null, sizes,
            xOnly, yOnly, both, xPlus, xMinus, yPlus, yMinus,
            nearest, tight, mid, loose, isolated, distinctMid, distinctLoose, largestTight, sharing, surviving, walkingStretch, runsMid, runsLoose, spot, spanX, spanY, reg.Count, hotOk, walkingOk);
    }

    /// <summary>The distance between two frames' positions; both must have one.</summary>
    /// <param name="x">Per frame, the X position, in pixels.</param>
    /// <param name="y">Per frame, the Y position, in pixels.</param>
    /// <param name="i">The first frame, 0-based position in the lists.</param>
    /// <param name="j">The second frame, 0-based position in the lists.</param>
    /// <returns>The Euclidean distance, in pixels.</returns>
    private static double Dist(IReadOnlyList<double?> x, IReadOnlyList<double?> y, int i, int j)
    {
        double dx = x[i]!.Value - x[j]!.Value, dy = y[i]!.Value - y[j]!.Value;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Stretches of <paramref name="minFrames"/> (Th.RunMinFrames by default) or more
    /// consecutive registered frames within <paramref name="radius"/> of the stretch's first
    /// frame.
    /// </summary>
    /// <param name="x">Per frame, the X position, in pixels; null where unknown.</param>
    /// <param name="y">Per frame, the Y position, in pixels; null where unknown.</param>
    /// <param name="radius">
    /// The largest distance from the stretch's first frame, in pixels, included.
    /// </param>
    /// <param name="minFrames">The fewest frames a stretch must hold to be kept.</param>
    /// <returns>The stretches in series order; a frame without a position ends a stretch.</returns>
    private static List<FrameStretch> Runs(IReadOnlyList<double?> x, IReadOnlyList<double?> y, double radius, int minFrames = Th.RunMinFrames)
    {
        List<FrameStretch> runs = [];
        int n = x.Count, s = 0;
        for (int i = 1; i <= n; i++)
        {
            bool breaks = i == n || x[i] is null || y[i] is null || x[s] is null || y[s] is null || Dist(x, y, i, s) > radius;
            if (breaks)
            {
                if (i - s >= minFrames && x[s] is not null)
                {
                    runs.Add(new FrameStretch(s + 1, i, i - s));
                }

                s = i;
            }
        }

        return runs;
    }

    /// <summary>
    /// Greedy clustering: a frame joins the first cluster whose seed is within
    /// <paramref name="radius"/>, else seeds a new one. The sizes, one per cluster, in the
    /// order the seeds appeared; the count is the distinct positions.
    /// </summary>
    /// <param name="x">Per frame, the X position, in pixels.</param>
    /// <param name="y">Per frame, the Y position, in pixels.</param>
    /// <param name="reg">The 0-based positions of the registered frames, in series order.</param>
    /// <param name="radius">
    /// The largest distance from a cluster's seed, in pixels, included.
    /// </param>
    /// <returns>
    /// The frames per cluster, in the order the seeds appeared; empty when <paramref name="reg"/>
    /// is.
    /// </returns>
    private static List<int> Clusters(IReadOnlyList<double?> x, IReadOnlyList<double?> y, List<int> reg, double radius)
    {
        List<int> seeds = [];
        List<int> sizes = [];
        foreach (int i in reg)
        {
            int k = seeds.FindIndex(s => Dist(x, y, i, s) <= radius);
            if (k < 0)
            {
                seeds.Add(i);
                sizes.Add(1);
            }
            else
            {
                sizes[k]++;
            }
        }

        return sizes;
    }
}
