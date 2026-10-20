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

namespace DarksCheck.Core;

/// <summary>What a region's excess over the centre is made of (R-029): the part that grows with the warming is glow; a constant part is a readout offset when the biases hold it and a leak when they do not, and undecidable (<see cref="Constant"/>) without biases.</summary>
internal enum LeakCause
{
    Unmeasured,
    Glow,
    Constant,
    Leak,
    Readout,
}

/// <summary>R-028: every frame's excess light over the Theil-Sen fit of the frame mean on the warming (the hot pixels' amplitude ratio), in ADU equivalent and aligned with the series' frames (NaN for a frame without figures); the fit's scale (1.4826 x the median absolute residual, ADU equivalent); the frames (1-based, the file's index) lit beyond <see cref="Th.LeakFrameSigmas"/> scales and <see cref="Th.LeakFrameAdu"/>; the fit in raw ADU per unit of ratio and how many frames it stands on.</summary>
internal sealed record FrameLight(IReadOnlyList<double> ExcessAdu, double ScaleAdu, IReadOnlyList<int> LitFrames, IReadOnlyList<double> LitExcessAdu, double Slope, double Intercept, int FramesFitted, double RatioSpan);

/// <summary>R-029: one region's offset to the centre split into the part that grows with the warming (the slope on the ratio, which is the value at ratio 1, the master's temperature), the constant part (the intercept), the biases' share of it when there are biases (null without) and what remains of the constant after it, all in ADU equivalent, with the cause named by the largest part on the side of the total (the brighter side when the region is brighter than the centre).</summary>
internal sealed record EdgeSplit(MapRegion Where, double GlowAdu, double ConstantAdu, double? ReadoutAdu, double RemainderAdu, LeakCause Cause);

/// <summary>
/// Tells a light leak from the sensor's glow by the warming of the series (R-028, R-029,
/// ADR 0012). Both fits read a per-frame figure against the hot pixels' amplitude ratio of
/// R-023: dark current, glow included, follows it; light from outside does not. The fit is
/// Theil-Sen, so a few lit frames do not tilt it.
/// </summary>
internal static class LeakAnalyzer
{
    /// <summary>The median absolute deviation of a Gaussian scaled to its sigma.</summary>
    private const double MadToSigma = 1.4826;

    /// <summary>The fewest frames a fit stands on once the clipping has taken frames out.</summary>
    private const int MinKept = 3;

    /// <summary>The frame mean above black against the ratio (R-028); null under <see cref="Th.LeakFitMinFrames"/> frames with a ratio.</summary>
    internal static FrameLight? FrameLightOf(IReadOnlyList<DarkFrameFigures> frames, double norm)
    {
        ArgumentNullException.ThrowIfNull(frames);
        int[] idx = Fitted(frames);
        if (idx.Length < Th.LeakFitMinFrames)
        {
            return null;
        }

        double[] x = idx.Select(i => frames[i].AmpRatioMedian).ToArray();
        double[] y = idx.Select(i => frames[i].MeanAboveBlack).ToArray();
        double floor = Th.LeakFrameAdu * norm;
        bool[] kept = new bool[x.Length];
        Array.Fill(kept, true);
        (double slope, double intercept) = TheilSen(x, y, kept);
        double scale = Scale(x, y, kept, slope, intercept);
        for (int pass = 0; pass < Th.LeakFitPasses; pass++)
        {
            double limit = Math.Max(Th.LeakFrameSigmas * scale, floor);
            bool[] next = new bool[x.Length];
            int count = 0;
            for (int k = 0; k < x.Length; k++)
            {
                next[k] = Math.Abs(y[k] - intercept - slope * x[k]) <= limit;
                count += next[k] ? 1 : 0;
            }

            if (count < MinKept || next.SequenceEqual(kept))
            {
                break;
            }

            kept = next;
            (slope, intercept) = TheilSen(x, y, kept);
            scale = Scale(x, y, kept, slope, intercept);
        }

        double finalLimit = Math.Max(Th.LeakFrameSigmas * scale, floor);
        double[] excess = new double[frames.Count];
        Array.Fill(excess, double.NaN);
        List<int> lit = [];
        List<double> litExcess = [];
        double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
        for (int k = 0; k < x.Length; k++)
        {
            double res = y[k] - intercept - slope * x[k];
            excess[idx[k]] = res / norm;
            if (res > finalLimit)
            {
                lit.Add(frames[idx[k]].Info.Index);
                litExcess.Add(res / norm);
            }
            else
            {
                lo = Math.Min(lo, x[k]);
                hi = Math.Max(hi, x[k]);
            }
        }

        return new FrameLight(excess, scale / norm, lit, litExcess, slope, intercept, x.Length, hi >= lo ? hi - lo : 0);
    }

    /// <summary>The eight regions' offsets to the centre against the ratio, over the frames R-028 did not name (R-029); empty when R-028 is unmeasured, when those frames' ratios span less than <see cref="Th.LeakFitMinSpan"/> (the line to ratio 0 would be noise) or when too few frames remain.</summary>
    internal static IReadOnlyList<EdgeSplit> EdgeSplitsOf(IReadOnlyList<DarkFrameFigures> frames, FrameLight? light, Regions? bias, double norm)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (light is null || light.RatioSpan < Th.LeakFitMinSpan)
        {
            return [];
        }

        HashSet<int> litFrames = [.. light.LitFrames];
        int[] idx = Fitted(frames).Where(i => frames[i].Edges is not null && !litFrames.Contains(frames[i].Info.Index)).ToArray();
        if (idx.Length < MinKept)
        {
            return [];
        }

        double[] x = idx.Select(i => frames[i].AmpRatioMedian).ToArray();
        bool[] all = new bool[x.Length];
        Array.Fill(all, true);
        List<EdgeSplit> splits = [];
        foreach (MapRegion where in Enum.GetValues<MapRegion>())
        {
            double[] y = idx.Select(i => frames[i].Edges!.Of(where)).ToArray();
            (double slope, double intercept) = TheilSen(x, y, all);
            double glow = slope / norm, constant = intercept / norm;
            double? readout = bias is null ? null : bias.OffsetOf(where) / norm;
            double remainder = constant - (readout ?? 0);
            // the cause is the largest part on the side the region departs to: the side of dark minus
            // bias when that departs beyond the threshold (a brighter region is explained by what adds
            // light, so a readout dip in the bias cannot name it), else the side of the dark's own
            // departure (nothing to explain on dark minus bias; the dark's edge is described)
            double judged = glow + remainder;
            double side = (Math.Abs(judged) > Th.LeakAdu ? judged : glow + constant) >= 0 ? 1 : -1;
            LeakCause cause = readout is double ro
                ? Largest(side, (LeakCause.Glow, glow), (LeakCause.Readout, ro), (LeakCause.Leak, remainder))
                : Largest(side, (LeakCause.Glow, glow), (LeakCause.Constant, constant));
            splits.Add(new EdgeSplit(where, glow, constant, readout, remainder, cause));
        }

        return splits;
    }

    /// <summary>The Theil-Sen line over the kept points: the slope is the median of the pairwise slopes (0 under two distinct x), the intercept the median of y - slope x.</summary>
    internal static (double Slope, double Intercept) TheilSen(double[] x, double[] y, bool[] kept)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(kept);
        List<double> slopes = [];
        for (int i = 0; i < x.Length; i++)
        {
            if (!kept[i])
            {
                continue;
            }

            for (int j = i + 1; j < x.Length; j++)
            {
                if (kept[j] && x[j] != x[i])
                {
                    slopes.Add((y[j] - y[i]) / (x[j] - x[i]));
                }
            }
        }

        double slope = slopes.Count == 0 ? 0 : Planes.Median(slopes);
        List<double> offsets = [];
        for (int i = 0; i < x.Length; i++)
        {
            if (kept[i])
            {
                offsets.Add(y[i] - slope * x[i]);
            }
        }

        return (slope, Planes.Median(offsets));
    }

    /// <summary>The frames that carry figures and a ratio: decoded, not skipped, with hot pixels to read the warming from.</summary>
    private static int[] Fitted(IReadOnlyList<DarkFrameFigures> frames) =>
        Enumerable.Range(0, frames.Count).Where(i => frames[i].Error is null && !frames[i].Skipped && !double.IsNaN(frames[i].AmpRatioMedian) && !double.IsNaN(frames[i].MeanAboveBlack)).ToArray();

    private static double Scale(double[] x, double[] y, bool[] kept, double slope, double intercept)
    {
        List<double> abs = [];
        for (int i = 0; i < x.Length; i++)
        {
            if (kept[i])
            {
                abs.Add(Math.Abs(y[i] - intercept - slope * x[i]));
            }
        }

        return MadToSigma * Planes.Median(abs);
    }

    /// <summary>The part that contributes most on the given side (+1 brighter, -1 darker); the first one on a tie.</summary>
    private static LeakCause Largest(double side, params (LeakCause Cause, double Value)[] parts)
    {
        (LeakCause cause, double best) = (parts[0].Cause, side * parts[0].Value);
        for (int i = 1; i < parts.Length; i++)
        {
            if (side * parts[i].Value > best)
            {
                (cause, best) = (parts[i].Cause, side * parts[i].Value);
            }
        }

        return cause;
    }
}
