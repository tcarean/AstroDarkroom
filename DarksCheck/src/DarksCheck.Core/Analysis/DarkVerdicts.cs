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
/// Check 2: the largest edge or corner offset from the centre, in ADU equivalent, on the
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
/// Check 3: the warming of the series and its plateau (R-023): whether the series has settled (the
/// fitted curve within <see cref="Th.SettledCelsius"/> of the plateau at the last frame; true
/// without a fit), the frames whose ratio to the master is outside <see cref="Th.MatchOptLow"/> ..
/// <see cref="Th.MatchOptHigh"/> and, of those, the ratio farthest from 1 (NaN when none).
/// </summary>
/// <param name="Verdict">Redo when <paramref name="MostlyOff"/>; else Ok when settled and no
/// frame is outside; Unmeasured when the warming's first-to-last degrees are NaN.</param>
/// <param name="Warming">The series' warming.</param>
/// <param name="Settled">True when the fitted last frame is within
/// <see cref="Th.SettledCelsius"/> of the plateau, or without a fit.</param>
/// <param name="MostlyOff">True when the frames off the plateau are more than
/// <see cref="Th.RedoOffPlateauFraction"/> of <paramref name="FramesWithRatio"/> (ADR 0038); false
/// without a fit, or with a plateau extrapolated from the top of the grid.</param>
/// <param name="FramesWithRatio">How many frames have an amplitude ratio (decoded, hot pixels
/// measured).</param>
/// <param name="OutsideFrames">The indices (<see cref="FrameInfo.Index"/>) of the frames
/// outside the band.</param>
/// <param name="FarthestRatio">The ratio of those farthest from 1 on the log scale; NaN when
/// none.</param>
internal sealed record DriftVerdict(Verdict Verdict, Warming Warming, bool Settled, bool MostlyOff, int FramesWithRatio, IReadOnlyList<int> OutsideFrames, double FarthestRatio);

/// <summary>
/// Check 4: the per-frame residual noise on the reference plane, the frames that stand out, the
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
/// Check 6: the structured banding of the master and the glow at its edges: R-029's glow part of
/// the verdict region, or the whole offset when the split is unmeasured (R-040).
/// </summary>
/// <param name="Verdict">Ok when the banding is within <see cref="Th.BandingAdu"/> and the
/// offset within <see cref="Th.LeakAdu"/>; Unmeasured when either is NaN.</param>
/// <param name="BandingP2pAdu">The largest row or column peak to peak over the four positions,
/// in ADU equivalent.</param>
/// <param name="GlowAdu">The glow of the verdict region, or check 2's offset, in ADU
/// equivalent.</param>
internal sealed record PatternVerdict(Verdict Verdict, double BandingP2pAdu, double GlowAdu);

/// <summary>Check 7: dead and saturated pixels on the master (R-032).</summary>
/// <param name="Verdict">Ok when there are none of either.</param>
/// <param name="Dead">The master's pixels more than <see cref="Th.DeadBelowAdu"/> under black,
/// over the four positions.</param>
/// <param name="Saturated">The master's pixels at the white minus 1 or above.</param>
internal sealed record DeadSatVerdict(Verdict Verdict, int Dead, int Saturated);

/// <summary>
/// Check 8: whether the frames are enough: at least <see cref="Th.MinDarks"/>, and the median
/// master's noise under the quantisation step; the frames that would do both (R-024).
/// </summary>
/// <param name="Verdict">Ok when there are enough frames and the noise is under
/// <see cref="Th.MasterNoiseVsStep"/> times the step; Unmeasured under two frames or without a
/// noise.</param>
/// <param name="Frames">The frames stacked.</param>
/// <param name="MasterNoise">The median master's predicted noise, in raw ADU.</param>
/// <param name="QuantStep">The quantisation step, in ADU (0 unknown, read as 1).</param>
/// <param name="FramesNeeded">The frames that would meet both conditions, at least
/// <see cref="Th.MinDarks"/>.</param>
/// <param name="TooFew">True under <see cref="Th.MinDarks"/> frames.</param>
internal sealed record EnoughVerdict(Verdict Verdict, int Frames, double MasterNoise, int QuantStep, int FramesNeeded, bool TooFew);

/// <summary>The six verdicts of checks 2, 3, 4, 6, 7 and 8 on a dark series.</summary>
/// <param name="Leak">Check 2.</param>
/// <param name="Drift">Check 3.</param>
/// <param name="Noise">Check 4.</param>
/// <param name="Pattern">Check 6.</param>
/// <param name="DeadSat">Check 7.</param>
/// <param name="Enough">Check 8.</param>
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
        bool settled = !wm.HasFit || double.IsNaN(wm.SettleCelsius) || Math.Abs(wm.SettleCelsius) <= Th.SettledCelsius;
        // more than half the frames off the plateau: the median master is made of them (ADR 0038); a
        // plateau guessed from a curve still climbing at the top of the grid condemns nothing
        bool mostlyOff = wm.HasFit && !wm.TauAtGridTop && wm.OffPlateauFrames.Count > Th.RedoOffPlateauFraction * withRatio.Length;
        DriftVerdict drift = new(
            double.IsNaN(wm.DeltaCelsius) ? Verdict.Unmeasured : mostlyOff ? Verdict.Redo : settled && outside.Length == 0 ? Verdict.Ok : Verdict.Warning,
            wm, settled, mostlyOff, withRatio.Length, outside.Select(f => f.Info.Index).ToArray(), farthest);

        // 4. noise
        DarkFrameFigures[] ok = d.Frames.Where(f => f.Error is null).ToArray();
        float[] residual = ok.Select(f => (float)f.Residuals[refPlane].Sigma).ToArray();
        double medianSigma = residual.Length == 0 ? double.NaN : Planes.Median(residual);
        int[] outliers = ok.Where(f => f.Residuals[refPlane].Sigma > Th.NoiseOutlierFactor * medianSigma).Select(f => f.Info.Index).ToArray();
        double? read = b is null || double.IsNaN(b.ReadNoise[refPlane]) ? null : b.ReadNoise[refPlane];
        double? overRead = read is double rn && !double.IsNaN(d.Noise.TemporalSigma) ? Math.Sqrt(Math.Max(d.Noise.TemporalSigma * d.Noise.TemporalSigma - rn * rn, 0)) : null;
        // One frame has no residual against its own median (it is zero by construction):
        // check 4 is unmeasured, as checks 3 and 8 are.
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

        // 8. enough frames: at least MinDarks for an honest median, and the median master's
        // noise (median factor x temporal sigma / sqrt(N)) under the quantisation step
        double step = d.QuantStep > 0 ? d.QuantStep : 1;
        double masterNoise = d.Noise.MasterNoise;
        double perFrame = masterNoise * Math.Sqrt(d.Used);
        int needed = double.IsNaN(masterNoise) ? Th.MinDarks : Math.Max(Th.MinDarks, (int)Math.Ceiling(Math.Pow(perFrame / (Th.MasterNoiseVsStep * step), 2)));
        bool tooFew = d.Used < Th.MinDarks;
        Verdict enoughVerdict = d.Used < 2 || double.IsNaN(masterNoise) ? Verdict.Unmeasured
            : tooFew || masterNoise > Th.MasterNoiseVsStep * step ? Verdict.Warning : Verdict.Ok;
        EnoughVerdict enough = new(enoughVerdict, d.Used, masterNoise, d.QuantStep, needed, tooFew);

        return new DarkVerdicts(leak, drift, noise, pattern, deadSat, enough);
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
