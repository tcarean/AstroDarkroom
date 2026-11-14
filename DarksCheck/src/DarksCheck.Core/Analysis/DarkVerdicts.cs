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

/// <summary>Where on the map the largest departure from the centre sits.</summary>
internal enum MapRegion
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>
/// Check 3: the largest edge or corner offset from the centre, in ADU equivalent, on the
/// dark-minus-bias map when there is one, else on the dark master (R-021); the frames' excess light
/// over the warming (R-028, null under <see cref="Th.LeakFitMinFrames"/> frames); the eight
/// regions' glow / constant splits and the verdict region's own (R-029, empty and null when
/// unmeasured).
/// </summary>
/// <param name="Verdict">Ok when the offset is within <see cref="Th.LeakAdu"/>, no frame is
/// lit and no side holds constant light; Unmeasured when the offset is NaN.</param>
/// <param name="OffsetAdu">The largest offset, signed, in ADU equivalent; NaN when the map has
/// no region.</param>
/// <param name="Where">The region of that offset.</param>
/// <param name="FromDarkMinusBias">True when it was read on the dark-minus-bias map.</param>
/// <param name="Frames">The frames' excess light; null under
/// <see cref="Th.LeakFitMinFrames"/> frames.</param>
/// <param name="Splits">The eight regions' splits; empty when unmeasured.</param>
/// <param name="Sides">The four sides' constant light; empty when unmeasured.</param>
internal sealed record LeakVerdict(Verdict Verdict, double OffsetAdu, MapRegion Where, bool FromDarkMinusBias, FrameLight? Frames, IReadOnlyList<EdgeSplit> Splits, IReadOnlyList<SideLight> Sides)
{
    /// <summary>The verdict region's own split; null when R-029 is unmeasured.</summary>
    internal EdgeSplit? Split => Splits.FirstOrDefault(s => s.Where == Where);

    /// <summary>
    /// What the verdict region's excess is made of; <see cref="LeakCause.Unmeasured"/> without a
    /// split.
    /// </summary>
    internal LeakCause Cause => Split?.Cause ?? LeakCause.Unmeasured;

    /// <summary>
    /// The side with the most constant light (the remainder with biases, the constant without);
    /// null when unmeasured.
    /// </summary>
    internal SideLight? WorstSide => Sides.Count == 0 ? null : Sides.MaxBy(s => s.LightAdu);

    /// <summary>
    /// The side whose light after the bias passes <see cref="Th.LeakConstantAdu"/> and
    /// <see cref="Th.LeakConstantSigmas"/> times its own error, the strongest when several; null
    /// without biases or under them (ADR 0014).
    /// </summary>
    internal SideLight? ConstantLight => Sides.Where(IsConstantLight).OrderByDescending(s => s.LightAdu).FirstOrDefault();

    /// <summary>
    /// Whether a side's light after the bias is a leak by ADR 0014's two conditions.
    /// </summary>
    /// <param name="s">The side.</param>
    /// <returns>True when its remainder after the bias (null without biases: false) passes
    /// both.</returns>
    internal static bool IsConstantLight(SideLight s) => s.RemainderAdu is double x && x > Th.LeakConstantAdu && x > Th.LeakConstantSigmas * s.ErrorAdu;

    /// <summary>
    /// Whether the sides were measured with biases, so the constant light could be judged.
    /// </summary>
    internal bool SidesAfterBias => Sides.Count > 0 && Sides[0].RemainderAdu is not null;
}

/// <summary>
/// Check 4: the warming of the series against its end (R-023, ADR 0043): whether the master is
/// near the end of the series (within <see cref="Th.MasterNearEndCelsius"/>; true without an
/// end), the frames whose ratio to the master is outside <see cref="Th.MatchOptLow"/> ..
/// <see cref="Th.MatchOptHigh"/> and, of those, the ratio farthest from 1 (NaN when none).
/// </summary>
/// <param name="Verdict">Redo when <paramref name="MostlyOff"/>; else Ok when the master is
/// near the end and no frame is outside; Unmeasured without a first ratio (under two frames
/// with one, or no hot pixel).</param>
/// <param name="Warming">The series' warming.</param>
/// <param name="MasterNearEnd">True when the master is within
/// <see cref="Th.MasterNearEndCelsius"/> of the end of the series, or without an end.</param>
/// <param name="MostlyOff">True when the frames off the end are more than
/// <see cref="Th.RedoOffEndFraction"/> of <paramref name="FramesWithRatio"/> (ADR 0038); false
/// without an end.</param>
/// <param name="FramesWithRatio">How many frames have an amplitude ratio (decoded, hot pixels
/// measured).</param>
/// <param name="OutsideFrames">The indices (<see cref="FrameInfo.Index"/>) of the frames
/// outside the band.</param>
/// <param name="FarthestRatio">The ratio of those farthest from 1 on the log scale; NaN when
/// none.</param>
internal sealed record DriftVerdict(Verdict Verdict, Warming Warming, bool MasterNearEnd, bool MostlyOff, int FramesWithRatio, IReadOnlyList<int> OutsideFrames, double FarthestRatio);

/// <summary>
/// Check 5: the per-frame residual noise on the reference plane, the frames that stand out, the
/// read noise and the temporal noise over it (dark current and quantisation), the banding against
/// white noise (R-022).
/// </summary>
/// <param name="Verdict">Ok when no frame stands out; Unmeasured under two decoded
/// frames.</param>
/// <param name="MedianSigma">The median residual sigma, in raw ADU; NaN under two
/// frames.</param>
/// <param name="OutlierFrames">The indices of the frames above
/// <see cref="Th.NoiseOutlierFactor"/> times the median; empty under two frames.</param>
/// <param name="ReadNoise">The biases' read noise on the reference plane, in raw ADU; null
/// without biases.</param>
/// <param name="NoiseOverRead">sqrt(temporal^2 - read^2), at least 0, in raw ADU; null without
/// a read noise or a temporal sigma.</param>
/// <param name="RowBandingAdu">The median of the frames' row-mean deviation, in raw ADU; NaN
/// under two frames.</param>
/// <param name="RowBandingRatio">The median of the frames' row deviation over white noise's;
/// NaN likewise.</param>
/// <param name="ColBandingRatio">The same for the columns.</param>
internal sealed record NoiseVerdict(Verdict Verdict, double MedianSigma, IReadOnlyList<int> OutlierFrames, double? ReadNoise, double? NoiseOverRead, double RowBandingAdu, double RowBandingRatio, double ColBandingRatio)
{
    /// <summary>
    /// The residual sigma over which a frame is an outlier: <see cref="Th.NoiseOutlierFactor"/> x
    /// the median.
    /// </summary>
    internal double OutlierLimitAdu => Th.NoiseOutlierFactor * MedianSigma;
}

/// <summary>
/// Check 7: the structured banding of the master and the glow at its edges: R-029's glow part of
/// the verdict region, or the whole offset when the split is unmeasured (R-040).
/// </summary>
/// <param name="Verdict">Ok when the banding is within <see cref="Th.BandingAdu"/> and the
/// offset within <see cref="Th.LeakAdu"/>; Unmeasured when either is NaN.</param>
/// <param name="BandingP2pAdu">The largest row or column peak to peak over the four positions,
/// in ADU equivalent.</param>
/// <param name="GlowAdu">The glow of the verdict region, or check 3's offset, in ADU
/// equivalent.</param>
internal sealed record PatternVerdict(Verdict Verdict, double BandingP2pAdu, double GlowAdu);

/// <summary>Check 8: dead and saturated pixels on the master (R-032).</summary>
/// <param name="Verdict">Ok when there are none of either.</param>
/// <param name="Dead">The master's pixels more than <see cref="Th.DeadBelowAdu"/> under black,
/// over the four positions.</param>
/// <param name="Saturated">The master's pixels at the white minus 1 or above.</param>
internal sealed record DeadSatVerdict(Verdict Verdict, int Dead, int Saturated);

/// <summary>How a number of darks rates in check 9's table (R-024).</summary>
internal enum FramesRating
{
    /// <summary>Under <see cref="Th.MinDarks"/>: one odd frame can spoil the median.</summary>
    TooFew,

    /// <summary>At least <see cref="Th.MinDarks"/>, under the frames needed.</summary>
    Few,

    /// <summary>The frames needed, or more.</summary>
    Enough,

    /// <summary>
    /// At least <see cref="Th.MinDarks"/>, without lights: whether so many are enough shows only
    /// against the lights' stack, so the row is not rated (ADR 0056).
    /// </summary>
    Unjudged,
}

/// <summary>One row of check 9's table: the master a number of darks would give (R-024).</summary>
/// <param name="N">The number of darks, at least 1.</param>
/// <param name="MasterNoise">The median master's predicted per-pixel noise with that many, in raw
/// ADU of the darks.</param>
/// <param name="AgainstNow">That noise against the noise with the frames stacked, as a fraction:
/// 0.87 is 87 % more, -0.06 is 6 % less, 0 on the row of the frames stacked.</param>
/// <param name="StackPenalty">How much noisier the lights' stack comes out than with an ideal
/// master, one with no noise of its own, as a fraction (0.104 is 10.4 %), without dithering; NaN
/// without measured lights.</param>
/// <param name="Rating">Too few, few or enough, by the rule of the verdict.</param>
/// <param name="Now">True on the row of the frames stacked.</param>
internal sealed record FramesPoint(int N, double MasterNoise, double AgainstNow, double StackPenalty, FramesRating Rating, bool Now);

/// <summary>The darks one limit of the stack's noise asks for.</summary>
/// <param name="Limit">The limit, as a fraction (0.10 is 10 %).</param>
/// <param name="Frames">The fewest darks whose master keeps the stack within it, at least
/// <see cref="Th.MinDarks"/>.</param>
internal sealed record LimitFrames(double Limit, int Frames);

/// <summary>
/// The lights' stack the darks are judged against in check 9 (R-024, ADR 0051): the master is
/// subtracted from every light alike, so without dithering its noise stays whole in the stack
/// while the lights' own falls with their number.
/// </summary>
/// <param name="Limit">The limit in force, as a fraction: the stack at most this much noisier.</param>
/// <param name="Lights">How many lights gave the noise, at least 1.</param>
/// <param name="LightNoise">The noise of one light: the median over the lights of the noise
/// against the neighbours on the reference plane, brought to the darks' settings, in raw ADU of
/// the darks; above 0.</param>
/// <param name="Penalty">How much noisier the master of the frames stacked makes the stack, as a
/// fraction.</param>
/// <param name="Needed">The darks each of <see cref="Th.StackNoiseLimits"/> asks for, in that
/// order.</param>
internal sealed record StackLimit(double Limit, int Lights, double LightNoise, double Penalty, IReadOnlyList<LimitFrames> Needed)
{
    /// <summary>The lights' own share of the stack's per-pixel noise: one light's over the square root of their number.</summary>
    internal double ShareAdu => LightNoise / Math.Sqrt(Lights);
}

/// <summary>
/// Check 9: whether the frames are enough (R-024, ADR 0051): at least <see cref="Th.MinDarks"/>,
/// and, with measured lights, a master that keeps their stack within the limit of
/// <see cref="Stack"/>; without lights, the median master's noise under the quantisation step.
/// </summary>
/// <param name="Verdict">Ok when the frames are at least <paramref name="FramesNeeded"/>;
/// Unmeasured under two frames or without a noise.</param>
/// <param name="Frames">The frames stacked.</param>
/// <param name="MasterNoise">The median master's predicted noise, in raw ADU.</param>
/// <param name="QuantStep">The quantisation step, in ADU (0 unknown, read as 1).</param>
/// <param name="FramesNeeded">The frames the rule in force asks for, at least
/// <see cref="Th.MinDarks"/>.</param>
/// <param name="TooFew">True under <see cref="Th.MinDarks"/> frames.</param>
internal sealed record EnoughVerdict(Verdict Verdict, int Frames, double MasterNoise, int QuantStep, int FramesNeeded, bool TooFew)
{
    /// <summary>
    /// The table's rows, by rising number of darks: every <see cref="Th.FramesTableStep"/> frames
    /// to <see cref="Th.FramesTablePast"/> rows past the frames stacked, their own row, and the
    /// frames needed when they lie beyond; empty when the verdict is unmeasured.
    /// </summary>
    public IReadOnlyList<FramesPoint> Points { get; init; } = [];

    /// <summary>The lights' stack the verdict is judged against; null without measured lights.</summary>
    public StackLimit? Stack { get; init; }
}

/// <summary>The six verdicts of checks 3, 4, 5, 7, 8 and 9 on a dark series.</summary>
/// <param name="Leak">Check 3.</param>
/// <param name="Drift">Check 4.</param>
/// <param name="Noise">Check 5.</param>
/// <param name="Pattern">Check 7.</param>
/// <param name="DeadSat">Check 8.</param>
/// <param name="Enough">Check 9.</param>
internal sealed record DarkVerdicts(LeakVerdict Leak, DriftVerdict Drift, NoiseVerdict Noise, PatternVerdict Pattern, DeadSatVerdict DeadSat, EnoughVerdict Enough);

/// <summary>
/// Turns the figures of a dark series (and its biases, when there are any) into the six verdicts;
/// every threshold is in <see cref="Th"/>.
/// </summary>
internal static class DarkVerdictAnalyzer
{
    /// <summary>Judges a dark series.</summary>
    /// <param name="d">The darks' figures; not null.</param>
    /// <param name="b">The biases' figures; null without biases.</param>
    /// <param name="dc">The dark current (dark minus bias); null without biases.</param>
    /// <param name="refPlane">The reference CFA position, 0..3.</param>
    /// <returns>The six verdicts.</returns>
    internal static DarkVerdicts Judge(DarkSeriesAnalysis d, BiasAnalysis? b, DarkCurrent? dc, int refPlane)
    {
        ArgumentNullException.ThrowIfNull(d);
        double norm = d.Norm;

        // 2. leak: the map with the bias taken out when there is one
        Regions r = dc is not null ? dc.Maps[refPlane].Regions : d.MeanMaps[refPlane].Regions;
        (double offset, MapRegion where) = Largest(r);
        offset /= norm;
        // the frames' light over the warming and the glow / constant split of every region (R-028,
        // R-029)
        FrameLight? light = LeakAnalyzer.FrameLightOf(d.Frames, norm);
        IReadOnlyList<EdgeSplit> splits = LeakAnalyzer.EdgeSplitsOf(d.Frames, light, b?.MeanMaps[refPlane].Regions, norm);
        IReadOnlyList<SideLight> sides = LeakAnalyzer.SidesOf(d.Frames, light, b?.MeanMaps[refPlane].Regions, norm);
        bool lit = light is { LitFrames.Count: > 0 };
        bool constantLight = sides.Any(LeakVerdict.IsConstantLight);
        LeakVerdict leak = new(double.IsNaN(offset) ? Verdict.Unmeasured : Math.Abs(offset) <= Th.LeakAdu && !lit && !constantLight ? Verdict.Ok : Verdict.Warning, offset, where, dc is not null, light, splits, sides);

        // 3. drift
        Warming wm = d.Warming;
        DarkFrameFigures[] withRatio = d.Frames.Where(f => f.Error is null && !double.IsNaN(f.AmpRatioMedian)).ToArray();
        DarkFrameFigures[] outside = withRatio.Where(f => f.AmpRatioMedian < Th.MatchOptLow || f.AmpRatioMedian > Th.MatchOptHigh).ToArray();
        double farthest = outside.Length == 0 ? double.NaN : outside.Select(f => f.AmpRatioMedian).MaxBy(x => x > 0 ? Math.Abs(Math.Log(x)) : double.PositiveInfinity);
        // the master against where the series ended, not against where a curve says it would settle (ADR 0043)
        bool nearEnd = !wm.HasEnd || Math.Abs(wm.MasterCelsius) <= Th.MasterNearEndCelsius;
        // more than half the frames off the end of the series: the median master is made of them (ADR 0038)
        bool mostlyOff = wm.HasEnd && wm.OffEndFrames.Count > Th.RedoOffEndFraction * withRatio.Length;
        DriftVerdict drift = new(
            // unmeasured only without ratios to read (R-023); a last frame whose hot pixels do not show has no
            // first-to-last degrees, and is judged as a frame outside the -opt band
            double.IsNaN(wm.FirstRatio) ? Verdict.Unmeasured : mostlyOff ? Verdict.Redo : nearEnd && outside.Length == 0 ? Verdict.Ok : Verdict.Warning,
            wm, nearEnd, mostlyOff, withRatio.Length, outside.Select(f => f.Info.Index).ToArray(), farthest);

        // 4. noise
        DarkFrameFigures[] ok = d.Frames.Where(f => f.Error is null).ToArray();
        float[] residual = ok.Select(f => (float)f.Residuals[refPlane].Sigma).ToArray();
        double medianSigma = residual.Length == 0 ? double.NaN : Planes.Median(residual);
        int[] outliers = ok.Where(f => f.Residuals[refPlane].Sigma > Th.NoiseOutlierFactor * medianSigma).Select(f => f.Info.Index).ToArray();
        double? read = b is null || double.IsNaN(b.ReadNoise[refPlane]) ? null : b.ReadNoise[refPlane];
        double? overRead = read is double rn && !double.IsNaN(d.Noise.TemporalSigma) ? Math.Sqrt(Math.Max(d.Noise.TemporalSigma * d.Noise.TemporalSigma - rn * rn, 0)) : null;
        // One frame has no residual against its own median (it is zero by construction):
        // check 5 is unmeasured, as checks 4 and 9 are.
        bool series = residual.Length >= 2;
        double rowAdu = series ? Med(ok.Select(f => f.Residuals[refPlane].RowStd)) : double.NaN;
        double rowRatio = series ? Med(ok.Select(f => f.Residuals[refPlane].RowExpected > 0 ? f.Residuals[refPlane].RowStd / f.Residuals[refPlane].RowExpected : double.NaN)) : double.NaN;
        double colRatio = series ? Med(ok.Select(f => f.Residuals[refPlane].ColExpected > 0 ? f.Residuals[refPlane].ColStd / f.Residuals[refPlane].ColExpected : double.NaN)) : double.NaN;
        NoiseVerdict noise = new(!series ? Verdict.Unmeasured : outliers.Length == 0 ? Verdict.Ok : Verdict.Warning, series ? medianSigma : double.NaN, series ? outliers : [], read, overRead, rowAdu, rowRatio, colRatio);

        // 6. pattern: the worst structured banding over the planes, and the glow: the part of the
        // leak map's offset that grows with the warming, or the whole offset when unmeasured
        double p2p = d.Banding.Max(x => Math.Max(x.RowP2p, x.ColP2p)) / norm;
        Verdict patternVerdict = double.IsNaN(p2p) || double.IsNaN(offset) ? Verdict.Unmeasured
            : p2p <= Th.BandingAdu && Math.Abs(offset) <= Th.LeakAdu ? Verdict.Ok : Verdict.Warning;
        PatternVerdict pattern = new(patternVerdict, p2p, leak.Split?.GlowAdu ?? offset);

        // 7. dead and saturated
        int dead = d.Master.Sum(m => m.Low), sat = d.Master.Sum(m => m.Saturated);
        DeadSatVerdict deadSat = new(dead == 0 && sat == 0 ? Verdict.Ok : Verdict.Warning, dead, sat);

        // 9. enough frames, judged without lights here: the run judges it again once it has them
        EnoughVerdict enough = EnoughOf(d, null, refPlane, Th.StackNoiseLimit);

        return new DarkVerdicts(leak, drift, noise, pattern, deadSat, enough);
    }

    /// <summary>
    /// Check 9 (R-024, ADR 0051): whether the darks are enough, the frames needed and the table of
    /// what fewer or more darks would give. With measured lights the frames needed keep the lights'
    /// stack within <paramref name="limit"/> of what an ideal master, one with no noise of its
    /// own, would leave; without them only the minimum is judged, since how many are enough
    /// depends on the lights (ADR 0056). Either way at least <see cref="Th.MinDarks"/>.
    /// </summary>
    /// <param name="d">The darks' figures; not null.</param>
    /// <param name="lights">The lights measured against the dark; null without lights.</param>
    /// <param name="refPlane">The reference CFA position, 0..3.</param>
    /// <param name="limit">How much noisier the stack may be, as a fraction above 0 (0.10 is
    /// 10 %); read only with lights.</param>
    /// <returns>The verdict with its table and, with lights, the stack's figures.</returns>
    internal static EnoughVerdict EnoughOf(DarkSeriesAnalysis d, LightsAnalysis? lights, int refPlane, double limit)
    {
        ArgumentNullException.ThrowIfNull(d);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        double masterNoise = d.Noise.MasterNoise;
        bool tooFew = d.Used < Th.MinDarks;
        if (d.Used < 2 || double.IsNaN(masterNoise))
        {
            return new EnoughVerdict(Verdict.Unmeasured, d.Used, masterNoise, d.QuantStep, Th.MinDarks, tooFew);
        }

        // the noise one frame brings to a median master: the master's noise is this over sqrt(n)
        double perFrame = masterNoise * Math.Sqrt(d.Used);
        (double lightNoiseEq, int lightCount) = lights is null ? (double.NaN, 0) : LightsAnalyzer.NoiseOnPlane(lights.Frames, d, refPlane);
        StackLimit? stack = null;
        int needed;
        if (lightCount > 0 && lightNoiseEq > 0 && perFrame > 0)
        {
            // both noises at the darks' settings, so the master's raw ADU compare with the light's
            double lightNoise = lightNoiseEq * d.Norm;
            double share = lightNoise / Math.Sqrt(lightCount);
            List<LimitFrames> byLimit = [.. Th.StackNoiseLimits.Select(l => new LimitFrames(l, FramesWithin(perFrame, share, l)))];
            stack = new StackLimit(limit, lightCount, lightNoise, StackPenalty(masterNoise, share), byLimit);
            needed = FramesWithin(perFrame, share, limit);
        }
        else
        {
            // without lights there is nothing to hold the master's noise against: the minimum alone (ADR 0056)
            needed = Th.MinDarks;
        }

        // the rows: every step under the frames stacked, their own row, the steps past them, then
        // the frames needed when they lie beyond the last row
        List<int> counts = [];
        for (int n = Th.FramesTableStep; n < d.Used; n += Th.FramesTableStep)
        {
            counts.Add(n);
        }

        counts.Add(d.Used);
        int next = ((d.Used / Th.FramesTableStep) + 1) * Th.FramesTableStep;
        for (int k = 0; k < Th.FramesTablePast; k++)
        {
            counts.Add(next + (k * Th.FramesTableStep));
        }

        if (needed > counts[^1])
        {
            counts.Add(needed);
        }

        List<FramesPoint> points = [];
        foreach (int n in counts)
        {
            double noiseAtN = perFrame / Math.Sqrt(n);
            FramesRating rating = n < Th.MinDarks ? FramesRating.TooFew : stack is null ? FramesRating.Unjudged : n >= needed ? FramesRating.Enough : FramesRating.Few;
            points.Add(new FramesPoint(n, noiseAtN, n == d.Used ? 0 : Math.Sqrt((double)d.Used / n) - 1, stack is null ? double.NaN : StackPenalty(noiseAtN, stack.ShareAdu), rating, n == d.Used));
        }

        Verdict verdict = tooFew || d.Used < needed ? Verdict.Warning : Verdict.Ok;
        return new EnoughVerdict(verdict, d.Used, masterNoise, d.QuantStep, needed, tooFew) { Points = points, Stack = stack };
    }

    /// <summary>
    /// How much noisier the lights' stack comes out with a master than with an ideal one, with no
    /// noise of its own: the master's noise does not average out over the lights, so it adds in
    /// quadrature to their share.
    /// </summary>
    /// <param name="masterNoise">The master's per-pixel noise, in raw ADU of the darks.</param>
    /// <param name="share">The lights' share of the stack's noise, in the same ADU; above 0.</param>
    /// <returns>The stack's noise with the master over the stack's noise without it, minus 1.</returns>
    private static double StackPenalty(double masterNoise, double share) => Math.Sqrt(1 + Math.Pow(masterNoise / share, 2)) - 1;

    /// <summary>The fewest darks whose master keeps the stack within a limit.</summary>
    /// <param name="perFrame">The noise one frame brings to the median master (the master's noise
    /// x the square root of its frames), in raw ADU of the darks; above 0.</param>
    /// <param name="share">The lights' share of the stack's noise, in the same ADU; above 0.</param>
    /// <param name="limit">The limit, as a fraction above 0.</param>
    /// <returns>The smallest n with <see cref="StackPenalty"/> of perFrame / sqrt(n) at or under
    /// the limit, at least <see cref="Th.MinDarks"/>.</returns>
    private static int FramesWithin(double perFrame, double share, double limit)
    {
        // the master's noise may reach the share times sqrt((1 + limit)^2 - 1)
        double allowed = share * Math.Sqrt(((1 + limit) * (1 + limit)) - 1);
        double frames = Math.Ceiling(Math.Pow(perFrame / allowed, 2));
        return frames >= int.MaxValue ? int.MaxValue : Math.Max(Th.MinDarks, (int)frames);
    }

    /// <summary>The median of the finite values.</summary>
    /// <param name="values">The values; NaN and infinities are dropped.</param>
    /// <returns>The median, or NaN when none is finite.</returns>
    private static double Med(IEnumerable<double> values)
    {
        float[] v = values.Where(x => !double.IsNaN(x) && !double.IsInfinity(x)).Select(x => (float)x).ToArray();
        return v.Length == 0 ? double.NaN : Planes.Median(v);
    }

    /// <summary>
    /// The largest departure from the centre over the four edges and the four corners, signed, with
    /// its region.
    /// </summary>
    /// <param name="r">The map's regions.</param>
    /// <returns>The offset in the map's unit and its region; the first in the order top,
    /// bottom, left, right, then the corners, on a tie.</returns>
    private static (double Offset, MapRegion Where) Largest(Regions r)
    {
        MapRegion[] order = [MapRegion.Top, MapRegion.Bottom, MapRegion.Left, MapRegion.Right, MapRegion.TopLeft, MapRegion.TopRight, MapRegion.BottomLeft, MapRegion.BottomRight];
        return order.Select(m => (r.OffsetOf(m), m)).MaxBy(x => double.IsNaN(x.Item1) ? double.NegativeInfinity : Math.Abs(x.Item1));
    }
}
