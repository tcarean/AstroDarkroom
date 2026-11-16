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

/// <summary>
/// What a region's excess over the centre is made of (R-029): the part that grows with the warming
/// is glow; a constant part is a readout offset when the biases hold it and a leak when they do
/// not, and undecidable (<see cref="Constant"/>) without biases.
/// </summary>
internal enum LeakCause
{
    /// <summary>No split was measured.</summary>
    Unmeasured,

    /// <summary>The part that grows with the warming.</summary>
    Glow,

    /// <summary>A constant part, with no biases to tell readout from leak.</summary>
    Constant,

    /// <summary>A constant part the biases do not hold.</summary>
    Leak,

    /// <summary>A constant part the biases hold.</summary>
    Readout,
}

/// <summary>
/// R-028: every frame's excess light over the Theil-Sen fit of the frame mean on the warming (the
/// hot pixels' amplitude ratio), in ADU equivalent and aligned with the series' frames (NaN for a
/// frame without figures); the fit's scale (1.4826 x the median absolute residual, ADU equivalent);
/// the frames (1-based, the file's index) lit beyond <see cref="Th.LeakFrameSigmas"/> scales and
/// <see cref="Th.LeakFrameAdu"/>; the fit in raw ADU per unit of ratio and how many frames it
/// stands on.
/// </summary>
/// <param name="ExcessAdu">Each frame's residual to the fit, in ADU equivalent, one per frame
/// of the series; NaN for a frame not fitted.</param>
/// <param name="ScaleAdu">The fit's scale, in ADU equivalent.</param>
/// <param name="LitFrames">The indices (<see cref="FrameInfo.Index"/>) of the lit
/// frames.</param>
/// <param name="LitExcessAdu">Their excess, in ADU equivalent, in the same order.</param>
/// <param name="Slope">The fit's slope, in raw ADU per unit of ratio.</param>
/// <param name="Intercept">The fit's intercept, in raw ADU.</param>
/// <param name="FramesFitted">How many frames the fit read.</param>
/// <param name="RatioSpan">The span of the ratios of the fitted frames not lit; 0 when every
/// one is lit.</param>
internal sealed record FrameLight(IReadOnlyList<double> ExcessAdu, double ScaleAdu, IReadOnlyList<int> LitFrames, IReadOnlyList<double> LitExcessAdu, double Slope, double Intercept, int FramesFitted, double RatioSpan)
{
    /// <summary>
    /// The frames the split and the sides stand on: the fitted ones that are not lit.
    /// </summary>
    internal int FramesKept => FramesFitted - LitFrames.Count;
}

/// <summary>A side of the frame: an edge with its two corners (R-029, ADR 0014).</summary>
internal enum MapSide
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>
/// R-029: one side's constant light: the side's offset to the centre (the mean of its edge and two
/// corners) fitted on the ratio over the frames not lit, its constant part (the intercept) with the
/// error of that intercept, and, with biases, what remains of it after the bias (null
/// without biases), in ADU equivalent.
/// </summary>
/// <param name="Side">The side.</param>
/// <param name="ConstantAdu">The intercept, in ADU equivalent.</param>
/// <param name="ErrorAdu">The intercept's error at one sigma, in ADU equivalent, from the scatter of
/// the frames around the line and from how well the line's own pairwise slopes hold its slope
/// (ADR 0064); NaN when every ratio is the same.</param>
/// <param name="RemainderAdu">The intercept minus the bias master's offset of the side, in ADU
/// equivalent; null without biases.</param>
internal sealed record SideLight(MapSide Side, double ConstantAdu, double ErrorAdu, double? RemainderAdu)
{
    /// <summary>What is judged: the remainder with biases, the constant without.</summary>
    internal double LightAdu => RemainderAdu ?? ConstantAdu;

    /// <summary>The three regions of a side.</summary>
    /// <param name="side">The side.</param>
    /// <returns>Its two corners and its edge.</returns>
    internal static IReadOnlyList<MapRegion> RegionsOf(MapSide side) => side switch
    {
        MapSide.Top => [MapRegion.TopLeft, MapRegion.Top, MapRegion.TopRight],
        MapSide.Bottom => [MapRegion.BottomLeft, MapRegion.Bottom, MapRegion.BottomRight],
        MapSide.Left => [MapRegion.TopLeft, MapRegion.Left, MapRegion.BottomLeft],
        _ => [MapRegion.TopRight, MapRegion.Right, MapRegion.BottomRight],
    };
}

/// <summary>
/// R-029: one region's offset to the centre split into the part that grows with the warming (the
/// slope on the ratio, which is the value at ratio 1, the master's temperature), the constant part
/// (the intercept), the biases' share of it when there are biases (null without) and what remains
/// of the constant after it, all in ADU equivalent, with the cause named by the largest part on the
/// side of the total (the brighter side when the region is brighter than the centre).
/// </summary>
/// <param name="Where">The region.</param>
/// <param name="GlowAdu">The slope on the ratio, in ADU equivalent.</param>
/// <param name="ConstantAdu">The intercept, in ADU equivalent.</param>
/// <param name="ReadoutAdu">The bias master's offset of the region, in ADU equivalent; null
/// without biases.</param>
/// <param name="RemainderAdu">The constant minus the readout (the constant without
/// biases).</param>
/// <param name="Cause">The largest part on the side of the total.</param>
internal sealed record EdgeSplit(MapRegion Where, double GlowAdu, double ConstantAdu, double? ReadoutAdu, double RemainderAdu, LeakCause Cause);

/// <summary>
/// Tells a light leak from the sensor's glow by the warming of the series (R-028, R-029,
/// ADR 0012). Both fits read a per-frame figure against the hot pixels' amplitude ratio of
/// R-023: dark current, glow included, follows it; light from outside does not. The fit is
/// Theil-Sen, so a few lit frames do not tilt it; a block of lit frames, over a quarter of the
/// series, is found by a second fit that starts from a sturdier line (ADR 0063).
/// </summary>
internal static class LeakAnalyzer
{
    /// <summary>The median absolute deviation of a Gaussian scaled to its sigma.</summary>
    private const double MadToSigma = 1.4826;

    /// <summary>The fewest frames a fit stands on once the clipping has taken frames out.</summary>
    private const int MinKept = 3;

    /// <summary>
    /// The frame mean above black against the ratio (R-028); null under
    /// <see cref="Th.LeakFitMinFrames"/> frames with a ratio. The fit is run twice, from
    /// Theil-Sen's line and from <see cref="BlockStart"/>'s; the second result stands only when it
    /// names a block of lit frames the first one misses.
    /// </summary>
    /// <param name="frames">The series' frames; not null.</param>
    /// <param name="norm">The normalisation factor from raw ADU to ADU equivalent.</param>
    /// <returns>The frames' excess light, or null.</returns>
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
        bool[] all = new bool[x.Length];
        Array.Fill(all, true);
        (double slope, double intercept, double scale, int lit) fit = Passes(x, y, floor, TheilSen(x, y, all));

        // The median of the pairwise slopes gives way once about a third of the frames, at one end
        // of the warming, carry light: the line tilts through them, the scale grows and no frame is
        // over the limit. So the passes are run again from a line that holds up to nearly half of
        // the frames being lit, and that result stands only when it shows what the first cannot: a
        // block of lit frames, more of them than the first names, with a tighter fit of the rest.
        // On any other series every figure is the first fit's (ADR 0063).
        (double slope, double intercept, double scale, int lit) block = Passes(x, y, floor, BlockStart(x, y));
        if (block.lit > fit.lit && block.lit > Th.LeakBlockMinFraction * x.Length && block.lit < Th.LeakBlockMaxFraction * x.Length && block.scale < fit.scale)
        {
            fit = block;
        }

        (double slope, double intercept, double scale, _) = fit;
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

    /// <summary>
    /// The passes of R-028's fit from a given first line: each leaves out the frames further from
    /// the line than the limit (<see cref="Th.LeakFrameSigmas"/> scales, at least
    /// <paramref name="floor"/>) and refits Theil-Sen on the rest, up to
    /// <see cref="Th.LeakFitPasses"/> times, stopping when nothing changes or too few frames would
    /// be left. The figures returned are always Theil-Sen's over the frames kept, whatever line
    /// the passes started from.
    /// </summary>
    /// <param name="x">The frames' ratios; not null.</param>
    /// <param name="y">The frames' means above black, raw ADU, as many; not null.</param>
    /// <param name="floor">The least limit, in raw ADU.</param>
    /// <param name="start">The line the first pass measures the frames against.</param>
    /// <returns>The line over the frames kept, its scale in raw ADU, and how many frames lie
    /// above it by more than the limit.</returns>
    private static (double Slope, double Intercept, double Scale, int Lit) Passes(double[] x, double[] y, double floor, (double Slope, double Intercept) start)
    {
        bool[] kept = new bool[x.Length];
        Array.Fill(kept, true);
        (double slope, double intercept) = start;
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

        // From Theil-Sen's own line this recomputes the same values; from another first line that
        // no pass replaced it brings the figures back to Theil-Sen's.
        (slope, intercept) = TheilSen(x, y, kept);
        scale = Scale(x, y, kept, slope, intercept);
        double finalLimit = Math.Max(Th.LeakFrameSigmas * scale, floor);
        int lit = 0;
        for (int k = 0; k < x.Length; k++)
        {
            lit += y[k] - intercept - slope * x[k] > finalLimit ? 1 : 0;
        }

        return (slope, intercept, scale, lit);
    }

    /// <summary>
    /// The first line of R-028's second fit (ADR 0063): among the lines through two of the points,
    /// each with the median of the residuals as its intercept, the one whose median absolute
    /// residual is smallest (it needs only half of the points to lie close, so up to nearly half
    /// may carry light), then Theil-Sen over the points within
    /// <see cref="Th.LeakBlockInlierSigmas"/> scales of it, which reads the line from all the
    /// points that fit and not from two.
    /// </summary>
    /// <param name="x">The abscissae; not null.</param>
    /// <param name="y">The ordinates, as many; not null.</param>
    /// <returns>The slope and the intercept; slope 0 and the median of <paramref name="y"/> when
    /// no two abscissae differ.</returns>
    internal static (double Slope, double Intercept) BlockStart(double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        int n = x.Length;
        double best = double.PositiveInfinity, bestSlope = 0, bestIntercept = Planes.Median(y);
        double[] r = new double[n];
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (x[j] == x[i])
                {
                    continue;
                }

                double slope = (y[j] - y[i]) / (x[j] - x[i]);
                for (int k = 0; k < n; k++)
                {
                    r[k] = y[k] - slope * x[k];
                }

                double intercept = Planes.Median(r);
                for (int k = 0; k < n; k++)
                {
                    r[k] = Math.Abs(r[k] - intercept);
                }

                double middle = Planes.Median(r);
                if (middle < best)
                {
                    (best, bestSlope, bestIntercept) = (middle, slope, intercept);
                }
            }
        }

        if (double.IsPositiveInfinity(best))
        {
            return (bestSlope, bestIntercept);
        }

        double limit = Th.LeakBlockInlierSigmas * MadToSigma * best;
        bool[] near = new bool[n];
        int count = 0;
        for (int k = 0; k < n; k++)
        {
            near[k] = Math.Abs(y[k] - bestIntercept - bestSlope * x[k]) <= limit;
            count += near[k] ? 1 : 0;
        }

        return count >= MinKept ? TheilSen(x, y, near) : (bestSlope, bestIntercept);
    }

    /// <summary>
    /// The eight regions' offsets to the centre against the ratio, over the frames R-028 did not
    /// name (R-029); empty when R-028 is unmeasured, when those frames' ratios span less than
    /// <see cref="Th.LeakFitMinSpan"/> (the line to ratio 0 would be noise) or when too few frames
    /// remain.
    /// </summary>
    /// <param name="frames">The series' frames; not null.</param>
    /// <param name="light">R-028's result; null when unmeasured.</param>
    /// <param name="bias">The bias master's regions on the reference plane; null without
    /// biases.</param>
    /// <param name="norm">The normalisation factor.</param>
    /// <returns>One split per region, in <see cref="MapRegion"/> order, or none.</returns>
    internal static IReadOnlyList<EdgeSplit> EdgeSplitsOf(IReadOnlyList<DarkFrameFigures> frames, FrameLight? light, Regions? bias, double norm)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (KeptForSplit(frames, light) is not (int[] idx, double[] x))
        {
            return [];
        }

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
            // the cause is the largest part on the side the region departs to: the side of dark
            // minus bias when that departs beyond the threshold (a brighter region is explained by
            // what adds light, so a readout dip in the bias cannot name it), else the side of the
            // dark's own departure (nothing to explain on dark minus bias; the dark's edge is
            // described)
            double judged = glow + remainder;
            double side = (Math.Abs(judged) > Th.LeakAdu ? judged : glow + constant) >= 0 ? 1 : -1;
            LeakCause cause = readout is double ro
                ? Largest(side, (LeakCause.Glow, glow), (LeakCause.Readout, ro), (LeakCause.Leak, remainder))
                : Largest(side, (LeakCause.Glow, glow), (LeakCause.Constant, constant));
            splits.Add(new EdgeSplit(where, glow, constant, readout, remainder, cause));
        }

        return splits;
    }

    /// <summary>
    /// The four sides (R-029, ADR 0014): each side's per-frame offset to the centre, the mean of
    /// its edge and two corners, fitted on the ratio over the frames R-028 did not name, like the
    /// regions; the constant part comes with the error of the intercept,
    /// sqrt(scale^2 / n + (mean(x) x <see cref="SlopeError"/>)^2): the line's level at the mean
    /// ratio is known to the scale over sqrt(n), and carrying it to ratio 0 adds the slope's error
    /// times that distance (ADR 0064). With biases the bias master's offset of the side is taken
    /// out of the constant. Empty when the split is unmeasured.
    /// </summary>
    /// <param name="frames">The series' frames; not null.</param>
    /// <param name="light">R-028's result; null when unmeasured.</param>
    /// <param name="bias">The bias master's regions on the reference plane; null without
    /// biases.</param>
    /// <param name="norm">The normalisation factor.</param>
    /// <returns>One per side, in <see cref="MapSide"/> order, or none: also under
    /// <see cref="Th.LeakSidesMinFrames"/> frames.</returns>
    internal static IReadOnlyList<SideLight> SidesOf(IReadOnlyList<DarkFrameFigures> frames, FrameLight? light, Regions? bias, double norm)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (KeptForSplit(frames, light) is not (int[] idx, double[] x) || idx.Length < Th.LeakSidesMinFrames)
        {
            return [];
        }

        bool[] all = new bool[x.Length];
        Array.Fill(all, true);
        double meanX = x.Average();
        List<SideLight> sides = [];
        foreach (MapSide side in Enum.GetValues<MapSide>())
        {
            double[] y = idx.Select(i => SideOffset(frames[i].Edges!, side)).ToArray();
            (double slope, double intercept) = TheilSen(x, y, all);
            double scale = Scale(x, y, all, slope, intercept);

            // The slope's error is read from the pairwise slopes the line is the median of, not
            // from least squares' formula: one far frame gives that formula a long lever and a
            // small error, while a median of pairwise slopes gains nothing from one frame.
            double slopeError = SlopeError(x, y, all);
            double error = Math.Sqrt(scale * scale / x.Length + meanX * meanX * slopeError * slopeError);
            double? remainder = bias is null ? null : (intercept - SideOffset(bias, side)) / norm;
            sides.Add(new SideLight(side, intercept / norm, error / norm, remainder));
        }

        return sides;
    }

    /// <summary>
    /// The frames the split and the sides stand on: those with figures and a ratio, not lit, when
    /// R-028 is measured and their ratios span <see cref="Th.LeakFitMinSpan"/>; null otherwise.
    /// </summary>
    /// <param name="frames">The series' frames.</param>
    /// <param name="light">R-028's result; null when unmeasured.</param>
    /// <returns>The frames' positions in the list and their ratios, or null (also under three
    /// frames).</returns>
    private static (int[] Idx, double[] X)? KeptForSplit(IReadOnlyList<DarkFrameFigures> frames, FrameLight? light)
    {
        if (light is null || light.RatioSpan < Th.LeakFitMinSpan)
        {
            return null;
        }

        HashSet<int> litFrames = [.. light.LitFrames];
        int[] idx = Fitted(frames).Where(i => frames[i].Edges is not null && !litFrames.Contains(frames[i].Info.Index)).ToArray();
        return idx.Length < MinKept ? null : (idx, idx.Select(i => frames[i].AmpRatioMedian).ToArray());
    }

    /// <summary>
    /// A side's offset to the centre in one frame: the mean of its three regions, raw ADU.
    /// </summary>
    /// <param name="edges">The frame's edges and corners; not null.</param>
    /// <param name="side">The side.</param>
    /// <returns>The offset, in raw ADU.</returns>
    internal static double SideOffset(EdgeFigures edges, MapSide side)
    {
        ArgumentNullException.ThrowIfNull(edges);
        return SideLight.RegionsOf(side).Average(edges.Of);
    }

    /// <summary>
    /// A side's offset to the centre on a binned map (the bias master's), raw ADU.
    /// </summary>
    /// <param name="regions">The map's regions; not null.</param>
    /// <param name="side">The side.</param>
    /// <returns>The mean of the three regions' offsets to the centre, in raw ADU.</returns>
    internal static double SideOffset(Regions regions, MapSide side)
    {
        ArgumentNullException.ThrowIfNull(regions);
        return SideLight.RegionsOf(side).Average(regions.OffsetOf);
    }

    /// <summary>
    /// The Theil-Sen line over the kept points: the slope is the median of the pairwise slopes (0
    /// under two distinct x), the intercept the median of y - slope x.
    /// </summary>
    /// <param name="x">The abscissae; not null.</param>
    /// <param name="y">The ordinates, as many; not null.</param>
    /// <param name="kept">Which points count, as many; not null.</param>
    /// <returns>The slope and the intercept; the intercept NaN when no point is kept.</returns>
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

    /// <summary>
    /// The error of a Theil-Sen slope at one sigma, from its own pairwise slopes (Sen's interval,
    /// ADR 0064): with N pairwise slopes sorted and C = sqrt(n (n - 1) (2n + 5) / 18), the standard
    /// deviation of Kendall's statistic for n points, half the distance from slope
    /// floor((N - C) / 2) to slope ceil((N + C) / 2), counted from 0 and held inside the list.
    /// </summary>
    /// <param name="x">The abscissae; not null.</param>
    /// <param name="y">The ordinates, as many; not null.</param>
    /// <param name="kept">Which points count, as many; not null.</param>
    /// <returns>The slope's error, in the unit of <paramref name="y"/> per unit of
    /// <paramref name="x"/>, 0 or more; NaN when no two kept points have distinct abscissae.</returns>
    internal static double SlopeError(double[] x, double[] y, bool[] kept)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(kept);
        List<double> slopes = [];
        int n = 0;
        for (int i = 0; i < x.Length; i++)
        {
            if (!kept[i])
            {
                continue;
            }

            n++;
            for (int j = i + 1; j < x.Length; j++)
            {
                if (kept[j] && x[j] != x[i])
                {
                    slopes.Add((y[j] - y[i]) / (x[j] - x[i]));
                }
            }
        }

        if (slopes.Count == 0)
        {
            return double.NaN;
        }

        slopes.Sort();
        double c = Math.Sqrt(n * (n - 1.0) * (2.0 * n + 5) / 18);
        int low = Math.Max(0, (int)Math.Floor((slopes.Count - c) / 2));
        int high = Math.Min(slopes.Count - 1, (int)Math.Ceiling((slopes.Count + c) / 2));
        return (slopes[high] - slopes[low]) / 2;
    }

    /// <summary>
    /// The frames that carry figures and a ratio: decoded, not skipped, with hot pixels to read the
    /// warming from.
    /// </summary>
    /// <param name="frames">The series' frames.</param>
    /// <returns>Their positions in the list.</returns>
    private static int[] Fitted(IReadOnlyList<DarkFrameFigures> frames) =>
        Enumerable.Range(0, frames.Count).Where(i => frames[i].Error is null && !frames[i].Skipped && !double.IsNaN(frames[i].AmpRatioMedian) && !double.IsNaN(frames[i].MeanAboveBlack)).ToArray();

    /// <summary>
    /// The fit's scale: 1.4826 x the median absolute residual of the kept points.
    /// </summary>
    /// <param name="x">The abscissae.</param>
    /// <param name="y">The ordinates.</param>
    /// <param name="kept">Which points count.</param>
    /// <param name="slope">The line's slope.</param>
    /// <param name="intercept">The line's intercept.</param>
    /// <returns>The scale, in the unit of <paramref name="y"/>.</returns>
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

    /// <summary>
    /// The part that contributes most on the given side (+1 brighter, -1 darker); the first one on
    /// a tie.
    /// </summary>
    /// <param name="side">+1 for brighter, -1 for darker.</param>
    /// <param name="parts">The parts and their values; at least one.</param>
    /// <returns>The cause of the part with the largest side x value.</returns>
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
