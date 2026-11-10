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

using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// The figures of one CFA position of one frame (R-020): levels in ADU above black, the counts at
/// <see cref="Th.HotLevelsAdu"/>.
/// </summary>
/// <param name="Name">The position's name (R, G1, G2, B or CFA[r,c]).</param>
/// <param name="Median">The median of the sampled values minus the black, in raw ADU.</param>
/// <param name="Sigma">The clipped sigma (<see cref="Planes.ClippedSigma"/>), in raw ADU.</param>
/// <param name="Mean">The mean minus the black, in raw ADU.</param>
/// <param name="P999">The 99.9th percentile of the values above the black, in raw ADU.</param>
/// <param name="Above">How many pixels lie above each of <see cref="Th.HotLevelsAdu"/>, the
/// levels read as raw ADU of this stack (<see cref="DarkSeriesAnalyzer.HotLevel"/>).</param>
/// <param name="Saturated">How many pixels are at the white minus 1 or above.</param>
/// <param name="Low">How many pixels lie more than <see cref="Th.DeadBelowAdu"/> under the
/// black.</param>
/// <param name="Min">The smallest value, in raw ADU, the black included.</param>
/// <param name="Max">The largest value, in raw ADU, the black included.</param>
internal sealed record PlaneFigures(string Name, double Median, double Sigma, double Mean, double P999, IReadOnlyList<int> Above, int Saturated, int Low, int Min, int Max);

/// <summary>
/// One CFA position of one frame against the median master (R-022): the residual's noise and the
/// banding of its row and column means against white noise.
/// </summary>
/// <param name="Name">The position's name.</param>
/// <param name="Sigma">The clipped sigma of the frame minus the master, in raw ADU.</param>
/// <param name="Median">The median of the sampled residual values, in raw ADU.</param>
/// <param name="Mean">The mean of the residual, in raw ADU.</param>
/// <param name="RowStd">The standard deviation of the residual's row means, in raw ADU.</param>
/// <param name="RowExpected">What white noise would give for it: sigma / sqrt(plane
/// width).</param>
/// <param name="ColStd">The standard deviation of the residual's column means, in raw
/// ADU.</param>
/// <param name="ColExpected">What white noise would give for it: sigma / sqrt(plane
/// height).</param>
internal sealed record ResidualFigures(string Name, double Sigma, double Median, double Mean, double RowStd, double RowExpected, double ColStd, double ColExpected)
{
    /// <summary>
    /// The row banding against white noise: the deviation of the row means over what white noise
    /// would give (1 = no banding); NaN without an expectation.
    /// </summary>
    internal double RowBandingRatio => RowExpected > 0 ? RowStd / RowExpected : double.NaN;
}

/// <summary>
/// The four edges and the four corners of a frame's binned reference plane relative to its centre,
/// in raw ADU (R-021, R-029).
/// </summary>
/// <param name="Left">The left edge minus the centre.</param>
/// <param name="Right">The right edge minus the centre.</param>
/// <param name="Top">The top edge minus the centre.</param>
/// <param name="Bottom">The bottom edge minus the centre.</param>
/// <param name="TopLeft">The top-left corner minus the centre.</param>
/// <param name="TopRight">The top-right corner minus the centre.</param>
/// <param name="BottomLeft">The bottom-left corner minus the centre.</param>
/// <param name="BottomRight">The bottom-right corner minus the centre.</param>
internal sealed record EdgeFigures(double Left, double Right, double Top, double Bottom, double TopLeft, double TopRight, double BottomLeft, double BottomRight)
{
    /// <summary>The offset of one region.</summary>
    /// <param name="where">The edge or corner; any other value reads the right edge.</param>
    /// <returns>The region minus the centre, in raw ADU.</returns>
    internal double Of(MapRegion where) => where switch
    {
        MapRegion.TopLeft => TopLeft,
        MapRegion.TopRight => TopRight,
        MapRegion.BottomLeft => BottomLeft,
        MapRegion.BottomRight => BottomRight,
        MapRegion.Top => Top,
        MapRegion.Bottom => Bottom,
        MapRegion.Left => Left,
        _ => Right,
    };
}

/// <summary>
/// One dark of the series: its metadata, its figures per plane and against the master, its
/// hot-pixel amplitude relative to the master (R-023), or the error that kept it out.
/// </summary>
/// <param name="Info">The frame's metadata.</param>
/// <param name="Error">Why the frame has no figures, in the run's language; null when it
/// decoded.</param>
/// <param name="Skipped">True when the frame cap left it out.</param>
/// <param name="Planes">The four positions' figures; empty without a mosaic.</param>
/// <param name="Residuals">The four positions against the median master; empty without a
/// mosaic.</param>
/// <param name="AmpRatioMedian">The median, over the amplitude pixels, of the frame's level
/// above black over the master's; NaN without a mosaic or without amplitude pixels.</param>
/// <param name="AmpRatioQ25">The 25th percentile of those ratios; NaN likewise.</param>
/// <param name="AmpRatioQ75">The 75th percentile of those ratios; NaN likewise.</param>
/// <param name="MeanAboveBlack">The mean of the whole mosaic minus the black, in raw ADU; NaN
/// without a mosaic.</param>
/// <param name="Edges">The reference plane's edges and corners; null without a mosaic.</param>
internal sealed record DarkFrameFigures(
    FrameInfo Info,
    string? Error,
    bool Skipped,
    IReadOnlyList<PlaneFigures> Planes,
    IReadOnlyList<ResidualFigures> Residuals,
    double AmpRatioMedian,
    double AmpRatioQ25,
    double AmpRatioQ75,
    double MeanAboveBlack,
    EdgeFigures? Edges);

/// <summary>
/// The median master's figures on one CFA position (R-030): level, noise, the counts at the hot
/// levels, saturated and dead pixels.
/// </summary>
/// <param name="Name">The position's name.</param>
/// <param name="Median">The median of the master minus the black, in raw ADU.</param>
/// <param name="Sigma">The clipped sigma of the master, in raw ADU.</param>
/// <param name="Above">How many pixels lie above each hot level (as
/// <see cref="PlaneFigures.Above"/>); empty for the biases' master.</param>
/// <param name="Saturated">How many pixels are at the white minus 1 or above; 0 for the
/// biases.</param>
/// <param name="Low">How many pixels lie more than <see cref="Th.DeadBelowAdu"/> under the
/// black; 0 for the biases.</param>
internal sealed record MasterFigures(string Name, double Median, double Sigma, IReadOnlyList<int> Above, int Saturated, int Low);

/// <summary>
/// The stability of the master's hot pixels over the frames (R-031): how many frames keep
/// each above half the threshold, how many are always there, how many flicker, the
/// amplitude percentiles, the temporal noise of hot and quiet pixels and the
/// random-telegraph candidates of the reference plane (a temporal sigma above
/// <see cref="Th.RtsSigmaFactor"/> times the typical frame noise).
/// </summary>
/// <param name="ThresholdAdu">The hot threshold, in raw ADU above black.</param>
/// <param name="Hot">How many pixels of the median master, on all four positions, lie above
/// the threshold.</param>
/// <param name="FramesHotHistogram">Entry k: how many hot pixels stay above
/// <see cref="Th.HotKeepFraction"/> of the threshold in exactly k frames; n + 1
/// entries.</param>
/// <param name="Always">How many hot pixels stay above it in every frame.</param>
/// <param name="Flicker">How many stay above it in fewer than n - 1 frames.</param>
/// <param name="P10">The 10th percentile of the hot pixels' level in the master, in raw ADU
/// above black; NaN without a hot pixel.</param>
/// <param name="P50">The median of it; NaN likewise.</param>
/// <param name="P90">The 90th percentile of it; NaN likewise.</param>
/// <param name="P99">The 99th percentile of it; NaN likewise.</param>
/// <param name="HotTemporalStd">The median, over the hot pixels, of their standard deviation
/// over the frames, in raw ADU; NaN without a hot pixel.</param>
/// <param name="QuietTemporalStd">The median temporal standard deviation of the reference plane
/// read at every 8th pixel in both directions, in raw ADU.</param>
/// <param name="RtsCandidates">How many pixels of the reference plane have a temporal standard
/// deviation above <see cref="Th.RtsSigmaFactor"/> times the typical frame noise.</param>
internal sealed record HotStability(double ThresholdAdu, int Hot, IReadOnlyList<int> FramesHotHistogram, int Always, int Flicker, double P10, double P50, double P90, double P99, double HotTemporalStd, double QuietTemporalStd, int RtsCandidates);

/// <summary>
/// One point of the noise curve (R-024): the mean of n frames against sigma / sqrt(n).
/// </summary>
/// <param name="N">How many frames each mean takes.</param>
/// <param name="Measured">The clipped sigma of the difference of two disjoint means of
/// <paramref name="N"/> frames over sqrt(2), at the quiet pixels of the crop, in raw
/// ADU.</param>
/// <param name="Expected">The typical frame sigma over sqrt(<paramref name="N"/>), in raw
/// ADU.</param>
internal sealed record NoisePoint(int N, double Measured, double Expected);

/// <summary>
/// The noise of the mean of n frames against sigma / sqrt(n) (R-024); the temporal sigma (the n = 1
/// point: frame against frame, the fixed pattern out), the median master's noise from half the
/// frames, the predicted noise of the whole median master (<see cref="Th.MedianNoiseFactor"/> x
/// temporal / sqrt(N)), and the side of the crop read.
/// </summary>
/// <param name="Curve">The points, for each of <see cref="Th.NoiseCurveCounts"/> while two
/// disjoint groups of that size fit in the series; empty under two frames.</param>
/// <param name="SigmaFrame">The median over the decoded frames of the reference plane's clipped
/// sigma, in raw ADU.</param>
/// <param name="TemporalSigma">The first point's measured noise; NaN when the curve is
/// empty.</param>
/// <param name="MedianHalfVsMeanHalf">The clipped sigma of the mean of the first half of the
/// frames minus the median of the second half, at the quiet pixels, in raw ADU; NaN under two
/// frames.</param>
/// <param name="MasterNoise">The median master's predicted per-pixel noise, in raw ADU; from
/// <paramref name="SigmaFrame"/> when there is no temporal figure.</param>
/// <param name="CropPx">The side of the centred crop read, in mosaic pixels.</param>
internal sealed record NoiseVsN(IReadOnlyList<NoisePoint> Curve, double SigmaFrame, double TemporalSigma, double MedianHalfVsMeanHalf, double MasterNoise, int CropPx);

/// <summary>
/// The mean of nine areas of a binned map, in ADU above black: the centre (40 .. 60 %), the four
/// corners and the four edges (each a tenth of the map).
/// </summary>
/// <param name="Centre">The centre's mean.</param>
/// <param name="TopLeft">The top-left corner's mean.</param>
/// <param name="TopRight">The top-right corner's mean.</param>
/// <param name="BottomLeft">The bottom-left corner's mean.</param>
/// <param name="BottomRight">The bottom-right corner's mean.</param>
/// <param name="Top">The top edge's mean, the whole width.</param>
/// <param name="Bottom">The bottom edge's mean, the whole width.</param>
/// <param name="Left">The left edge's mean, the whole height.</param>
/// <param name="Right">The right edge's mean, the whole height.</param>
internal sealed record Regions(double Centre, double TopLeft, double TopRight, double BottomLeft, double BottomRight, double Top, double Bottom, double Left, double Right)
{
    /// <summary>One edge or corner relative to the centre.</summary>
    /// <param name="where">The edge or corner.</param>
    /// <returns>Its mean minus the centre's, in the map's unit.</returns>
    internal double OffsetOf(MapRegion where) => Of(where) - Centre;

    /// <summary>The value of one edge or corner.</summary>
    /// <param name="where">The edge or corner; any other value reads the right edge.</param>
    /// <returns>Its mean, in the map's unit.</returns>
    internal double Of(MapRegion where) => where switch
    {
        MapRegion.TopLeft => TopLeft,
        MapRegion.TopRight => TopRight,
        MapRegion.BottomLeft => BottomLeft,
        MapRegion.BottomRight => BottomRight,
        MapRegion.Top => Top,
        MapRegion.Bottom => Bottom,
        MapRegion.Left => Left,
        _ => Right,
    };

    /// <summary>The nine areas of a binned map.</summary>
    /// <param name="map">The map, row-major, <paramref name="w"/> x <paramref name="h"/>.</param>
    /// <param name="w">The map's width, in blocks.</param>
    /// <param name="h">The map's height, in blocks.</param>
    /// <returns>The areas' means; all NaN for a map without a block.</returns>
    internal static Regions Of(float[] map, int w, int h)
    {
        if (w <= 0 || h <= 0)
        {
            // a plane under one block in either direction has no map
            return new Regions(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        }

        int h10 = Math.Max(1, h / 10), w10 = Math.Max(1, w / 10);
        return new Regions(
            Planes.MeanOf(map, w, (int)(0.4 * h), Math.Max((int)(0.6 * h), (int)(0.4 * h) + 1), (int)(0.4 * w), Math.Max((int)(0.6 * w), (int)(0.4 * w) + 1)),
            Planes.MeanOf(map, w, 0, h10, 0, w10), Planes.MeanOf(map, w, 0, h10, w - w10, w),
            Planes.MeanOf(map, w, h - h10, h, 0, w10), Planes.MeanOf(map, w, h - h10, h, w - w10, w),
            Planes.MeanOf(map, w, 0, h10, 0, w), Planes.MeanOf(map, w, h - h10, h, 0, w),
            Planes.MeanOf(map, w, 0, h, 0, w10), Planes.MeanOf(map, w, 0, h, w - w10, w));
    }
}

/// <summary>A binned map of one CFA position, in ADU above black, with its regions.</summary>
/// <param name="Name">The position's name.</param>
/// <param name="Values">The block means, row-major, <paramref name="W"/> x
/// <paramref name="H"/>, in raw ADU.</param>
/// <param name="W">The map's width, in blocks of <see cref="Th.BinPx"/> plane pixels.</param>
/// <param name="H">The map's height, in blocks.</param>
/// <param name="Regions">The map's nine areas.</param>
internal sealed record BinnedMap(string Name, IReadOnlyList<float> Values, int W, int H, Regions Regions);

/// <summary>
/// The structured banding of the master on one CFA position (R-040): the row and column profiles
/// minus their running median, as a standard deviation and peak to peak.
/// </summary>
/// <param name="Name">The position's name.</param>
/// <param name="RowStd">The standard deviation of the row profile's residual, in raw
/// ADU.</param>
/// <param name="ColStd">The standard deviation of the column profile's residual, in raw
/// ADU.</param>
/// <param name="RowP2p">The row residual's peak to peak, in raw ADU.</param>
/// <param name="ColP2p">The column residual's peak to peak, in raw ADU.</param>
internal sealed record BandingFigures(string Name, double RowStd, double ColStd, double RowP2p, double ColP2p);

/// <summary>
/// The warming of the series read from its hot pixels (R-023, ADR 0043): the amplitude ratio to the
/// master at the first and the last frame and the degrees between them; then, with at least
/// <see cref="Th.WarmingFitMinFrames"/> frames with a ratio, the end of the series (the median ratio
/// of the last <see cref="Th.SeriesEndFrames"/> of them), the first frame's and the master's degrees
/// from it, the frames more than <see cref="Th.OffEndCelsius"/> from it with the two bounds of that
/// band, the master's degrees from the end without them and the factor by which the master's noise
/// would grow without them (sqrt of frames over frames left). The time constant of the fit of
/// <see cref="DarkSeriesAnalyzer.PlateauFit"/> is kept for the REFA advice of a cooling series only.
/// </summary>
/// <param name="FirstRatio">The first measured frame's amplitude ratio to the master.</param>
/// <param name="LastRatio">The last measured frame's amplitude ratio to the master.</param>
/// <param name="DeltaCelsius">The degrees from the first to the last, by the doubling rule;
/// NaN when a ratio is not positive.</param>
/// <param name="Minutes">The minutes from the first to the last; NaN without both stamps, 0
/// under two measured frames.</param>
/// <param name="Pixels">How many amplitude pixels the ratios are read on.</param>
/// <param name="Measured">How many frames have a ratio.</param>
/// <param name="EndRatio">The end of the series, as a ratio to the master: the median ratio of
/// the last <see cref="Th.SeriesEndFrames"/> frames with one; NaN under
/// <see cref="Th.WarmingFitMinFrames"/> frames, or when that median is not positive.</param>
/// <param name="FirstCelsius">The first frame's degrees from the end, negative under it; NaN
/// without an end or when the first ratio is not positive.</param>
/// <param name="MasterCelsius">The master's degrees from the end, positive when the master is
/// above (warmer than) the end; NaN without an end.</param>
/// <param name="Cooling">True when the series cooled: the end is under the first frame, or,
/// without an end, the last frame's ratio is under the first's; the one direction every row of
/// check 3 names (ADR 0038).</param>
/// <param name="OffEndFrames">The indices (<see cref="FrameInfo.Index"/>) of the frames outside
/// the band, a ratio at or under 0 included; empty without an end.</param>
/// <param name="OffEndLow">The band's lower bound, as a ratio to the master.</param>
/// <param name="OffEndHigh">The band's upper bound, as a ratio to the master.</param>
/// <param name="MasterWithoutOffCelsius">The degrees from the end to the median ratio of the
/// frames left, negative under the end; NaN without an end.</param>
/// <param name="NoiseFactorWithoutOff">sqrt(frames measured / frames left); NaN without an
/// end.</param>
/// <param name="TauMinutes">The fitted time constant, in minutes, or in frames when
/// <paramref name="TimeFromStamps"/> is false; NaN when the fit cannot give one: no fit, a
/// plateau or fitted last frame that is not positive, a curve that does not move the way the
/// series did, or a time constant at the top of the grid.</param>
/// <param name="TimeFromStamps">True when the fit's time axis is the stamps' minutes.</param>
internal sealed record Warming(
    double FirstRatio, double LastRatio, double DeltaCelsius, double Minutes, int Pixels, int Measured,
    double EndRatio, double FirstCelsius, double MasterCelsius, bool Cooling,
    IReadOnlyList<int> OffEndFrames, double OffEndLow, double OffEndHigh, double MasterWithoutOffCelsius, double NoiseFactorWithoutOff,
    double TauMinutes, bool TimeFromStamps)
{
    internal static readonly Warming None = new(double.NaN, double.NaN, double.NaN, double.NaN, 0, 0, double.NaN, double.NaN, double.NaN, false, [], double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, false);

    /// <summary>True when the series has an end to be read against.</summary>
    internal bool HasEnd => !double.IsNaN(EndRatio);
}

/// <summary>Everything measured on a series of darks; the words are the report's.</summary>
/// <param name="Width">The mosaic's width, in pixels.</param>
/// <param name="Height">The mosaic's height, in pixels.</param>
/// <param name="Black">The black level, in raw ADU.</param>
/// <param name="White">The white level, in raw ADU.</param>
/// <param name="ScaleSource">Where the black and the white come from, in the run's
/// language.</param>
/// <param name="CfaLetters">The colour order's letters; null when the file does not say
/// it.</param>
/// <param name="Norm">The factor from raw ADU to ADU equivalent at ISO 400 and 120 s.</param>
/// <param name="Normalisation">The settings the factor was read with.</param>
/// <param name="QuantStep">The quantisation step near black, in ADU; 0 when fewer than two
/// values lie there.</param>
/// <param name="UniqueValues">How many distinct values the first frame's reference plane
/// holds.</param>
/// <param name="Frames">Every file of the series, in file order.</param>
/// <param name="Used">How many frames decoded and were stacked.</param>
/// <param name="Master">The median master's figures, one per CFA position.</param>
/// <param name="MedianMaster">The median master, row-major, in raw ADU, the black
/// included.</param>
/// <param name="MeanMaster">The mean master, row-major, in raw ADU, the black included.</param>
/// <param name="Hot">The stability of the master's hot pixels.</param>
/// <param name="Noise">The noise curve.</param>
/// <param name="MeanMaps">The binned mean master per CFA position, above black.</param>
/// <param name="Banding">The master's structured banding per CFA position.</param>
/// <param name="Warming">The warming of the series; its figures are NaN where they could not
/// be read.</param>
internal sealed record DarkSeriesAnalysis(
    int Width,
    int Height,
    double Black,
    double White,
    string ScaleSource,
    string? CfaLetters,
    double Norm,
    Normalisation Normalisation,
    int QuantStep,
    int UniqueValues,
    IReadOnlyList<DarkFrameFigures> Frames,
    int Used,
    IReadOnlyList<MasterFigures> Master,
    IReadOnlyList<float> MedianMaster,
    IReadOnlyList<float> MeanMaster,
    HotStability Hot,
    NoiseVsN Noise,
    IReadOnlyList<BinnedMap> MeanMaps,
    IReadOnlyList<BandingFigures> Banding,
    Warming Warming)
{
    internal IReadOnlyList<string> PlaneNames => Master.Select(m => m.Name).ToList();
}

/// <summary>
/// Checks 2 to 8 on a stack of darks, as the prototype did them (`prototype/analyze_darks.py`,
/// `supplementary.py`): the median and mean masters, every frame's figures and residual, the
/// hot pixels and their stability, the noise curve, the binned maps, the structured banding
/// and the warming of the series. Numbers only; the verdicts are <see cref="DarkVerdicts"/>.
/// </summary>
internal static class DarkSeriesAnalyzer
{
    /// <summary>Measures a stack of darks: checks 2 to 8's numbers.</summary>
    /// <param name="stack">The decoded darks; not null; at least one must have decoded.</param>
    /// <param name="threads">How many threads the loops take; below 1 counts as 1.</param>
    /// <param name="progress">Told (done, total) after each frame's figures; null for
    /// none.</param>
    /// <returns>Everything measured.</returns>
    /// <exception cref="InvalidOperationException">No dark decoded.</exception>
    internal static DarkSeriesAnalysis Analyze(FrameStack stack, int threads, IProgress<(int Done, int Total)>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(stack);
        IReadOnlyList<StackFrame> decoded = stack.Decoded;
        if (decoded.Count == 0)
        {
            throw new InvalidOperationException(T("no dark could be read", "niciun dark nu s-a putut citi") + FrameStack.FirstErrors(stack));
        }

        int w = stack.Width, h = stack.Height, n = decoded.Count;
        double black = stack.Black, norm = stack.Norm;
        int refPlane = stack.ReferencePlane;
        ParallelOptions po = new() { MaxDegreeOfParallelism = Math.Max(1, threads) };

        // quantisation: the values the first frame takes near black
        (int quantStep, int unique) = Quantisation(Planes.Extract(decoded[0].Pixels!, w, h, refPlane), black);

        // the masters: median and mean per pixel, and the temporal std
        float[] median = new float[w * h];
        float[] mean = new float[w * h];
        float[] tstd = new float[w * h];
        Masters(decoded, w, h, median, mean, tstd, po);
        float[] excess = new float[w * h];
        for (int i = 0; i < excess.Length; i++)
        {
            excess[i] = (float)(median[i] - black);
        }

        // per plane: the master's figures, the binned mean map, the structured banding
        double hotT = HotLevel(Th.HotAdu, norm);
        MasterFigures[] master = new MasterFigures[4];
        BinnedMap[] maps = new BinnedMap[4];
        BandingFigures[] banding = new BandingFigures[4];
        Parallel.For(0, 4, po, p =>
        {
            string name = stack.PlaneName(p);
            float[] ex = Planes.Extract(excess, w, h, p);
            int pw = Planes.PlaneWidth(w, p), ph = Planes.PlaneHeight(h, p);
            (double sigma, _) = Planes.ClippedSigma(ex);
            master[p] = new MasterFigures(name, Planes.Median(ex), sigma, CountAbove(ex, norm), CountAtLeast(Planes.Extract(median, w, h, p), stack.White - 1), CountBelow(ex, -Th.DeadBelowAdu));
            float[] meanPlane = Planes.Extract(mean, w, h, p);
            for (int i = 0; i < meanPlane.Length; i++)
            {
                meanPlane[i] -= (float)black;
            }

            float[] bm = Planes.BlockMean(meanPlane, pw, ph, Th.BinPx, out int bw, out int bh);
            maps[p] = new BinnedMap(name, bm, bw, bh, Regions.Of(bm, bw, bh));
            banding[p] = Banding(name, ex, pw, ph, hotT);
        });

        // every frame: its figures, its residual against the master, its amplitude ratio

        // the amplitude pixels for the warming curve: hot, not close to saturation
        double ampMax = Th.AmplitudeMaxFraction * stack.White;
        List<int> ampIdx = [];
        for (int i = 0; i < excess.Length; i++)
        {
            if (excess[i] > hotT && median[i] < ampMax)
            {
                ampIdx.Add(i);
            }
        }

        // the master's planes once, shared read-only by every frame's residual
        float[][] masterPlanes = Enumerable.Range(0, 4).Select(p => Planes.Extract(median, w, h, p)).ToArray();
        DarkFrameFigures[] frames = new DarkFrameFigures[stack.Frames.Count];
        int done = 0;
        Parallel.For(0, stack.Frames.Count, po, i =>
        {
            StackFrame f = stack.Frames[i];
            frames[i] = f.Pixels is null
                ? new DarkFrameFigures(f.Info, f.Error, f.Skipped, [], [], double.NaN, double.NaN, double.NaN, double.NaN, null)
                : FrameFigures(f, stack, masterPlanes, excess, ampIdx, refPlane);
            progress?.Report((Interlocked.Increment(ref done), stack.Frames.Count));
        });

        NoiseVsN noise = NoiseCurve(decoded, excess, w, h, refPlane, hotT, frames);

        // the hot pixels of the master and their stability over the frames; the RTS limit is
        // set by the typical frame noise of the reference plane, as the prototype did
        HotStability hot = Stability(decoded, excess, tstd, w, h, black, hotT, refPlane, noise.SigmaFrame);
        Warming warming = WarmingOf(frames, ampIdx.Count, stack.Normalisation);
        return new DarkSeriesAnalysis(w, h, black, stack.White, stack.ScaleSource, stack.Cfa?.Letters, norm, stack.Normalisation, quantStep, unique,
            frames, n, master, median, mean, hot, noise, maps, banding, warming);
    }

    /// <summary>
    /// The smallest spacing between the values present from black - 10 to black + 30, and how many
    /// distinct values the plane holds.
    /// </summary>
    /// <param name="plane">One plane's values, in raw ADU; not null.</param>
    /// <param name="black">The black level, in raw ADU.</param>
    /// <returns>The step, rounded to a whole ADU (0 when fewer than two values lie near
    /// black), and the count of distinct values.</returns>
    internal static (int Step, int Unique) Quantisation(float[] plane, double black)
    {
        ArgumentNullException.ThrowIfNull(plane);
        HashSet<float> all = [];
        SortedSet<float> near = [];
        foreach (float v in plane)
        {
            all.Add(v);
            if (v >= black - 10 && v <= black + 30)
            {
                near.Add(v);
            }
        }

        int step = 0;
        float? prev = null;
        foreach (float v in near)
        {
            if (prev is float p)
            {
                int d = (int)Math.Round(v - p);
                step = step == 0 ? d : Math.Min(step, d);
            }

            prev = v;
        }

        return (step, all.Count);
    }

    /// <summary>The median master alone, for a partner set of darks (R-063).</summary>
    /// <param name="stack">The partner's decoded darks; not null; at least one must have
    /// decoded.</param>
    /// <param name="threads">How many threads; below 1 counts as 1.</param>
    /// <returns>The median master, row-major, in raw ADU, the black included.</returns>
    /// <exception cref="InvalidOperationException">No dark decoded.</exception>
    internal static float[] MedianMaster(FrameStack stack, int threads)
    {
        ArgumentNullException.ThrowIfNull(stack);
        IReadOnlyList<StackFrame> decoded = stack.Decoded;
        if (decoded.Count == 0)
        {
            throw new InvalidOperationException(T("none of the partner's darks could be read", "niciun dark al partenerului nu s-a putut citi") + FrameStack.FirstErrors(stack));
        }

        float[] median = new float[stack.Width * stack.Height];
        Masters(decoded, stack.Width, stack.Height, median, new float[median.Length], new float[median.Length], new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads) });
        return median;
    }

    /// <summary>
    /// The per-pixel median, mean and population standard deviation over the frames.
    /// </summary>
    /// <param name="decoded">The decoded frames, at least one.</param>
    /// <param name="w">The mosaic's width.</param>
    /// <param name="h">The mosaic's height.</param>
    /// <param name="median">Filled with the median master, w x h.</param>
    /// <param name="mean">Filled with the mean master, w x h.</param>
    /// <param name="tstd">Filled with the temporal standard deviation, w x h.</param>
    /// <param name="po">The parallelism, over rows.</param>
    private static void Masters(IReadOnlyList<StackFrame> decoded, int w, int h, float[] median, float[] mean, float[] tstd, ParallelOptions po)
    {
        int n = decoded.Count;
        ushort[][] px = decoded.Select(f => f.Pixels!).ToArray();
        Parallel.For(0, h, po, y =>
        {
            ushort[] buf = new ushort[n];
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int i = row + x;
                double s = 0, s2 = 0;
                for (int k = 0; k < n; k++)
                {
                    ushort v = px[k][i];
                    buf[k] = v;
                    s += v;
                    s2 += (double)v * v;
                }

                Array.Sort(buf);
                median[i] = n % 2 == 1 ? buf[n / 2] : 0.5f * (buf[n / 2 - 1] + buf[n / 2]);
                double m = s / n;
                mean[i] = (float)m;
                tstd[i] = (float)Math.Sqrt(Math.Max(s2 / n - m * m, 0));
            }
        });
    }

    /// <summary>
    /// One decoded frame's figures: per plane, against the master, the reference plane's edges, and
    /// the amplitude ratio at the amplitude pixels.
    /// </summary>
    /// <param name="f">The frame; its mosaic not null.</param>
    /// <param name="stack">The stack, for the geometry and the scale.</param>
    /// <param name="masterPlanes">The median master's four planes.</param>
    /// <param name="excess">The median master minus the black, row-major.</param>
    /// <param name="ampIdx">The mosaic indices of the amplitude pixels.</param>
    /// <param name="refPlane">The reference CFA position.</param>
    /// <returns>The frame's figures.</returns>
    private static DarkFrameFigures FrameFigures(StackFrame f, FrameStack stack, float[][] masterPlanes, float[] excess, List<int> ampIdx, int refPlane)
    {
        int w = stack.Width, h = stack.Height;
        double black = stack.Black, norm = stack.Norm;
        ushort[] px = f.Pixels!;
        PlaneFigures[] planes = new PlaneFigures[4];
        ResidualFigures[] residuals = new ResidualFigures[4];
        EdgeFigures? edges = null;
        for (int p = 0; p < 4; p++)
        {
            string name = stack.PlaneName(p);
            int pw = Planes.PlaneWidth(w, p), ph = Planes.PlaneHeight(h, p);
            float[] plane = Planes.Extract(px, w, h, p);
            planes[p] = PlaneFiguresOf(name, plane, black, stack.White, norm);

            // the residual against the median master, on this plane
            float[] masterPlane = masterPlanes[p];
            float[] d = new float[plane.Length];
            for (int i = 0; i < d.Length; i++)
            {
                d[i] = plane[i] - masterPlane[i];
            }

            (double sig, double med) = Planes.ClippedSigma(d);
            double meanD = 0;
            foreach (float v in d)
            {
                meanD += v;
            }

            meanD /= d.Length;
            (double[] rows, double[] cols) = Planes.Profiles(d, pw, ph);
            residuals[p] = new ResidualFigures(name, sig, med, meanD, Planes.Std(rows), sig / Math.Sqrt(pw), Planes.Std(cols), sig / Math.Sqrt(ph));

            if (p == refPlane)
            {
                for (int i = 0; i < plane.Length; i++)
                {
                    plane[i] -= (float)black;
                }

                float[] bm = Planes.BlockMean(plane, pw, ph, Th.BinPx, out int bw, out int bh);
                Regions r = Regions.Of(bm, bw, bh);
                edges = new EdgeFigures(r.Left - r.Centre, r.Right - r.Centre, r.Top - r.Centre, r.Bottom - r.Centre, r.TopLeft - r.Centre, r.TopRight - r.Centre, r.BottomLeft - r.Centre, r.BottomRight - r.Centre);
            }
        }

        // the amplitude of the master's hot pixels in this frame
        float[] ratio = new float[ampIdx.Count];
        for (int k = 0; k < ratio.Length; k++)
        {
            int i = ampIdx[k];
            ratio[k] = (float)((px[i] - black) / excess[i]);
        }

        double meanAbove = 0;
        foreach (ushort v in px)
        {
            meanAbove += v;
        }

        meanAbove = meanAbove / px.Length - black;
        return ratio.Length == 0
            ? new DarkFrameFigures(f.Info, null, false, planes, residuals, double.NaN, double.NaN, double.NaN, meanAbove, edges)
            : new DarkFrameFigures(f.Info, null, false, planes, residuals, Planes.Median(ratio), Planes.Percentile(ratio, 25), Planes.Percentile(ratio, 75), meanAbove, edges);
    }

    /// <summary>The figures of one plane of one frame; shared with the biases.</summary>
    /// <param name="name">The position's name.</param>
    /// <param name="plane">The plane's values, in raw ADU; not null, not empty.</param>
    /// <param name="black">The black level, in raw ADU.</param>
    /// <param name="white">The white level, in raw ADU.</param>
    /// <param name="norm">The normalisation factor the hot levels are scaled by (1 for the
    /// biases).</param>
    /// <returns>The plane's figures.</returns>
    internal static PlaneFigures PlaneFiguresOf(string name, float[] plane, double black, double white, double norm)
    {
        ArgumentNullException.ThrowIfNull(plane);
        (double sigma, double med) = Planes.ClippedSigma(plane);
        double mean = 0;
        int min = int.MaxValue, max = int.MinValue;
        foreach (float v in plane)
        {
            mean += v;
            int iv = (int)v;
            if (iv < min)
            {
                min = iv;
            }

            if (iv > max)
            {
                max = iv;
            }
        }

        mean /= plane.Length;
        float[] ex = new float[plane.Length];
        for (int i = 0; i < ex.Length; i++)
        {
            ex[i] = (float)(plane[i] - black);
        }

        return new PlaneFigures(name, med - black, sigma, mean - black, Planes.Percentile(ex, 99.9), CountAbove(ex, norm),
            CountAtLeast(plane, white - 1), CountBelow(ex, -Th.DeadBelowAdu), min, max);
    }

    /// <summary>
    /// A level in ADU equivalent as raw ADU of this stack, never under <see cref="Th.MinHotAdu"/>.
    /// </summary>
    /// <param name="levelEq">The level, in ADU equivalent at ISO 400 and 120 s.</param>
    /// <param name="norm">The stack's normalisation factor.</param>
    /// <returns>The level in raw ADU.</returns>
    internal static double HotLevel(double levelEq, double norm) => Math.Max(levelEq * norm, Th.MinHotAdu);

    /// <summary>
    /// How many values lie above each of <see cref="Th.HotLevelsAdu"/>, read as raw ADU by
    /// <see cref="HotLevel"/>.
    /// </summary>
    /// <param name="excess">The values above black, in raw ADU.</param>
    /// <param name="norm">The normalisation factor.</param>
    /// <returns>One count per level, strictly above it.</returns>
    private static int[] CountAbove(float[] excess, double norm)
    {
        int[] counts = new int[Th.HotLevelsAdu.Count];
        double[] levels = Th.HotLevelsAdu.Select(l => HotLevel(l, norm)).ToArray();
        foreach (float v in excess)
        {
            for (int k = 0; k < levels.Length; k++)
            {
                if (v > levels[k])
                {
                    counts[k]++;
                }
                else
                {
                    break;
                }
            }
        }

        return counts;
    }

    /// <summary>How many values are at the limit or above.</summary>
    /// <param name="values">The values.</param>
    /// <param name="limit">The limit, included.</param>
    /// <returns>The count.</returns>
    private static int CountAtLeast(float[] values, double limit)
    {
        int c = 0;
        foreach (float v in values)
        {
            if (v >= limit)
            {
                c++;
            }
        }

        return c;
    }

    /// <summary>How many values lie strictly under the limit.</summary>
    /// <param name="values">The values.</param>
    /// <param name="limit">The limit, excluded.</param>
    /// <returns>The count.</returns>
    private static int CountBelow(float[] values, double limit)
    {
        int c = 0;
        foreach (float v in values)
        {
            if (v < limit)
            {
                c++;
            }
        }

        return c;
    }

    /// <summary>
    /// The profiles are read with the hot pixels clamped to the hot threshold: one 600 ADU pixel
    /// would otherwise lift its row by 600 / width, a band that is not there.
    /// </summary>
    /// <param name="name">The position's name.</param>
    /// <param name="excessPlane">The master's plane minus the black, in raw ADU.</param>
    /// <param name="pw">The plane's width.</param>
    /// <param name="ph">The plane's height.</param>
    /// <param name="hotT">The hot threshold, in raw ADU: values are clamped to plus or minus
    /// it.</param>
    /// <returns>The profiles minus their running median over <see cref="Th.BandingWindow"/>
    /// values, as a standard deviation and peak to peak.</returns>
    private static BandingFigures Banding(string name, float[] excessPlane, int pw, int ph, double hotT)
    {
        float[] clamped = new float[excessPlane.Length];
        float limit = (float)hotT;
        for (int i = 0; i < clamped.Length; i++)
        {
            clamped[i] = Math.Clamp(excessPlane[i], -limit, limit);
        }

        (double[] rows, double[] cols) = Planes.Profiles(clamped, pw, ph);
        double[] rf = Planes.MedianFilter(rows, Th.BandingWindow);
        double[] cf = Planes.MedianFilter(cols, Th.BandingWindow);
        double[] rr = new double[rows.Length];
        double[] cr = new double[cols.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            rr[i] = rows[i] - rf[i];
        }

        for (int i = 0; i < cols.Length; i++)
        {
            cr[i] = cols[i] - cf[i];
        }

        return new BandingFigures(name, Planes.Std(rr), Planes.Std(cr), rr.Max() - rr.Min(), cr.Max() - cr.Min());
    }

    /// <summary>
    /// The master's hot pixels over the frames, and the temporal noise of the reference plane.
    /// </summary>
    /// <param name="decoded">The decoded frames.</param>
    /// <param name="excess">The median master minus the black, row-major.</param>
    /// <param name="tstd">The temporal standard deviation, row-major.</param>
    /// <param name="w">The mosaic's width.</param>
    /// <param name="h">The mosaic's height.</param>
    /// <param name="black">The black level, in raw ADU.</param>
    /// <param name="hotT">The hot threshold, in raw ADU above black.</param>
    /// <param name="refPlane">The reference CFA position.</param>
    /// <param name="sigmaFrame">The typical frame noise, in raw ADU.</param>
    /// <returns>The stability figures.</returns>
    private static HotStability Stability(IReadOnlyList<StackFrame> decoded, float[] excess, float[] tstd, int w, int h, double black, double hotT, int refPlane, double sigmaFrame)
    {
        int n = decoded.Count;
        List<int> idx = [];
        for (int i = 0; i < excess.Length; i++)
        {
            if (excess[i] > hotT)
            {
                idx.Add(i);
            }
        }

        int[] hist = new int[n + 1];
        double keep = Th.HotKeepFraction * hotT;
        float[] hotExcess = new float[idx.Count];
        float[] hotTstd = new float[idx.Count];
        for (int k = 0; k < idx.Count; k++)
        {
            int i = idx[k];
            int framesHot = 0;
            foreach (StackFrame f in decoded)
            {
                if (f.Pixels![i] - black > keep)
                {
                    framesHot++;
                }
            }

            hist[framesHot]++;
            hotExcess[k] = excess[i];
            hotTstd[k] = tstd[i];
        }

        int always = hist[n];
        int flicker = 0;
        for (int k = 0; k < n - 1; k++)
        {
            flicker += hist[k];
        }

        // the quiet temporal noise: the reference plane read at every 8th pixel in both directions
        float[] tplane = Planes.Extract(tstd, w, h, refPlane);
        int pw = Planes.PlaneWidth(w, refPlane), ph = Planes.PlaneHeight(h, refPlane);
        List<float> quiet = [];
        for (int y = 0; y < ph; y += 8)
        {
            for (int x = 0; x < pw; x += 8)
            {
                quiet.Add(tplane[y * pw + x]);
            }
        }

        double quietStd = Planes.Median([.. quiet]);
        int rts = 0;
        double rtsLimit = Th.RtsSigmaFactor * sigmaFrame;
        foreach (float v in tplane)
        {
            if (v > rtsLimit)
            {
                rts++;
            }
        }

        bool any = idx.Count > 0;
        return new HotStability(hotT, idx.Count, hist, always, flicker,
            any ? Planes.Percentile(hotExcess, 10) : double.NaN, any ? Planes.Percentile(hotExcess, 50) : double.NaN,
            any ? Planes.Percentile(hotExcess, 90) : double.NaN, any ? Planes.Percentile(hotExcess, 99) : double.NaN,
            any ? Planes.Median(hotTstd) : double.NaN, quietStd, rts);
    }

    /// <summary>
    /// The noise curve on a centred crop of the reference plane, at most
    /// <see cref="Th.NoiseCropPx"/> mosaic pixels on a side, at the pixels under the hot threshold.
    /// </summary>
    /// <param name="decoded">The decoded frames.</param>
    /// <param name="excess">The median master minus the black, row-major.</param>
    /// <param name="w">The mosaic's width.</param>
    /// <param name="h">The mosaic's height.</param>
    /// <param name="refPlane">The reference CFA position.</param>
    /// <param name="hotT">The hot threshold, in raw ADU above black.</param>
    /// <param name="frames">Every frame's figures, for the typical frame sigma.</param>
    /// <returns>The curve and the master's noise.</returns>
    private static NoiseVsN NoiseCurve(IReadOnlyList<StackFrame> decoded, float[] excess, int w, int h, int refPlane, double hotT, DarkFrameFigures[] frames)
    {
        int n = decoded.Count;
        int size = Math.Min(Th.NoiseCropPx, Math.Min(w, h));
        int y0 = (h - size) / 2, x0 = (w - size) / 2;
        y0 -= y0 & 1;
        x0 -= x0 & 1;
        int pr = refPlane >> 1, pc = refPlane & 1;
        int side = size / 2;
        float[][] crops = new float[n][];
        for (int k = 0; k < n; k++)
        {
            float[] c = new float[side * side];
            ushort[] px = decoded[k].Pixels!;
            for (int j = 0; j < side; j++)
            {
                int row = (y0 + pr + 2 * j) * w + x0 + pc;
                for (int i = 0; i < side; i++)
                {
                    c[j * side + i] = px[row + 2 * i];
                }
            }

            crops[k] = c;
        }

        bool[] quietMask = new bool[side * side];
        for (int j = 0; j < side; j++)
        {
            int row = (y0 + pr + 2 * j) * w + x0 + pc;
            for (int i = 0; i < side; i++)
            {
                quietMask[j * side + i] = excess[row + 2 * i] < hotT;
            }
        }

        double sigmaFrame = Planes.Median([.. frames.Where(f => f.Error is null).Select(f => (float)f.Planes[refPlane].Sigma)]);
        List<NoisePoint> curve = [];
        foreach (int m in Th.NoiseCurveCounts)
        {
            if (2 * m > n)
            {
                break;
            }

            float[] diff = MeanDiff(crops, 0, m, m, 2 * m, quietMask);
            (double s, _) = Planes.ClippedSigma(diff);
            curve.Add(new NoisePoint(m, s / Math.Sqrt(2), sigmaFrame / Math.Sqrt(m)));
        }

        // the mean of the first half against the median of the second half
        double halfVs = double.NaN;
        if (n >= 2)
        {
            int half = n / 2;
            float[] first = new float[side * side];
            for (int k = 0; k < half; k++)
            {
                for (int i = 0; i < first.Length; i++)
                {
                    first[i] += crops[k][i];
                }
            }

            float[] buf = new float[n - half];
            List<float> diff = [];
            for (int i = 0; i < first.Length; i++)
            {
                if (!quietMask[i])
                {
                    continue;
                }

                for (int k = half; k < n; k++)
                {
                    buf[k - half] = crops[k][i];
                }

                diff.Add(first[i] / half - (float)Planes.Median(buf));
            }

            (halfVs, _) = Planes.ClippedSigma([.. diff]);
        }

        // The median master's noise: the temporal sigma (frame against frame, the pattern out)
        // over sqrt(N), times the median's penalty; the spatial sigma stands in when a single
        // frame leaves no temporal figure.
        double temporal = curve.Count > 0 ? curve[0].Measured : double.NaN;
        double basis = double.IsNaN(temporal) ? sigmaFrame : temporal;
        return new NoiseVsN(curve, sigmaFrame, temporal, halfVs, Th.MedianNoiseFactor * basis / Math.Sqrt(n), size);
    }

    /// <summary>mean(frames a0..a1) - mean(frames b0..b1), at the quiet pixels only.</summary>
    /// <param name="crops">The frames' crops, all of one length.</param>
    /// <param name="a0">The first frame of the first group.</param>
    /// <param name="a1">The frame after the first group's last.</param>
    /// <param name="b0">The first frame of the second group.</param>
    /// <param name="b1">The frame after the second group's last.</param>
    /// <param name="quiet">Which crop pixels are read.</param>
    /// <returns>One difference per quiet pixel, in raw ADU.</returns>
    private static float[] MeanDiff(float[][] crops, int a0, int a1, int b0, int b1, bool[] quiet)
    {
        List<float> d = [];
        int len = crops[0].Length;
        for (int i = 0; i < len; i++)
        {
            if (!quiet[i])
            {
                continue;
            }

            double a = 0, b = 0;
            for (int k = a0; k < a1; k++)
            {
                a += crops[k][i];
            }

            for (int k = b0; k < b1; k++)
            {
                b += crops[k][i];
            }

            d.Add((float)(a / (a1 - a0) - b / (b1 - b0)));
        }

        return [.. d];
    }

    /// <summary>
    /// The warming of the series from its frames' amplitude ratios: first against last, then the
    /// end of the series, the frames off it, and the time constant the REFA advice quotes.
    /// </summary>
    /// <param name="frames">Every frame's figures; those with a ratio are read, in order.</param>
    /// <param name="pixels">How many amplitude pixels the ratios are read on.</param>
    /// <param name="nz">The normalisation settings, for the doubling rule.</param>
    /// <returns>The warming; <see cref="Warming.None"/> with the pixel count under two
    /// frames.</returns>
    private static Warming WarmingOf(DarkFrameFigures[] frames, int pixels, Normalisation nz)
    {
        DarkFrameFigures[] ok = frames.Where(f => f.Error is null && !double.IsNaN(f.AmpRatioMedian)).ToArray();
        if (ok.Length < 2 || pixels == 0)
        {
            return Warming.None with { Minutes = 0, Pixels = pixels, Measured = ok.Length };
        }

        double first = ok[0].AmpRatioMedian, last = ok[^1].AmpRatioMedian;
        double dT = first > 0 && last > 0 ? nz.Degrees(last / first) : double.NaN;
        double minutes = ok[0].Info.Timestamp is DateTime t0 && ok[^1].Info.Timestamp is DateTime t1 ? (t1 - t0).TotalMinutes : double.NaN;
        Warming w = Warming.None with { FirstRatio = first, LastRatio = last, DeltaCelsius = dT, Minutes = minutes, Pixels = pixels, Measured = ok.Length, Cooling = dT < 0 };
        if (ok.Length < Th.WarmingFitMinFrames)
        {
            return w;
        }

        // the end of the series: the median of the last frames that have a ratio (under the frame
        // cap they are the last decoded ones); a median, so one odd frame does not move it
        double end = Planes.Median(ok[^Th.SeriesEndFrames..].Select(f => (float)f.AmpRatioMedian).ToArray());
        if (!(end > 0))
        {
            return w;
        }

        double low = end * Math.Pow(2, -Th.OffEndCelsius / nz.DoublingCelsius);
        double high = end * Math.Pow(2, Th.OffEndCelsius / nz.DoublingCelsius);
        List<int> off = [];
        List<float> left = [];
        foreach (DarkFrameFigures f in ok)
        {
            if (f.AmpRatioMedian < low || f.AmpRatioMedian > high)
            {
                off.Add(f.Info.Index);
            }
            else
            {
                left.Add((float)f.AmpRatioMedian);
            }
        }

        bool cooling = end < first;
        (double tau, bool stamps) = TimeConstant(ok, cooling);
        return w with
        {
            EndRatio = end,
            FirstCelsius = first > 0 ? nz.Degrees(first / end) : double.NaN,
            MasterCelsius = nz.Degrees(1 / end),
            // the end against the first frame, the two figures the row prints, so it cannot name a
            // direction its own figures contradict
            Cooling = cooling,
            OffEndFrames = off,
            OffEndLow = low,
            OffEndHigh = high,
            // the end is one of the last five ratios, so its own frame is always among those left
            MasterWithoutOffCelsius = nz.Degrees(Planes.Median([.. left]) / end),
            NoiseFactorWithoutOff = Math.Sqrt((double)ok.Length / left.Count),
            TauMinutes = tau,
            TimeFromStamps = stamps,
        };
    }

    /// <summary>
    /// The time constant of the series for the REFA advice (R-023): the tau of
    /// <see cref="PlateauFit"/> when the fit can be trusted for it, that is when its plateau and
    /// its fitted last frame are positive, its curve moves the way the series did and its tau is
    /// not the top of the grid, where the speed is a guess. No verdict and no other figure reads
    /// the fit (ADR 0043).
    /// </summary>
    /// <param name="ok">The frames with a ratio, in order, at least
    /// <see cref="Th.WarmingFitMinFrames"/>.</param>
    /// <param name="cooling">Whether the series cooled.</param>
    /// <returns>The time constant, NaN when the fit gives none, and whether its unit is the
    /// stamps' minutes (else frames).</returns>
    private static (double Tau, bool FromStamps) TimeConstant(DarkFrameFigures[] ok, bool cooling)
    {
        (double[] t, bool stamps) = TimeAxis(ok);
        double[] y = ok.Select(f => f.AmpRatioMedian).ToArray();
        (double plateau, double drop, double tau, _) = PlateauFit(t, y);
        if (double.IsNaN(plateau) || plateau <= 0 || plateau - drop * Math.Exp(-(t[^1] - t[0]) / tau) <= 0)
        {
            return (double.NaN, stamps);
        }

        // the curve starts at plateau - drop: it falls when the drop is negative
        bool falls = drop < 0;
        bool atGridTop = tau >= 3 * (t[^1] - t[0]) - 1e-9;
        return (falls == cooling && !atGridTop ? tau : double.NaN, stamps);
    }

    /// <summary>
    /// The time axis of the fit: minutes from the first frame when every frame has a timestamp, the
    /// stamps never go back in series order and the span is positive; else the frame's position in
    /// the series (a stamp out of order would throw the exponentials off).
    /// </summary>
    /// <param name="ok">The frames with a ratio, at least one.</param>
    /// <returns>The time of each frame, and whether it is the stamps' minutes.</returns>
    private static (double[] T, bool FromStamps) TimeAxis(DarkFrameFigures[] ok)
    {
        if (ok.All(f => f.Info.Timestamp is not null) && ok[0].Info.Timestamp is DateTime t0 && ok[^1].Info.Timestamp is DateTime t1 && t1 > t0)
        {
            double[] minutes = ok.Select(f => (f.Info.Timestamp!.Value - t0).TotalMinutes).ToArray();
            if (Enumerable.Range(1, minutes.Length - 1).All(i => minutes[i] >= minutes[i - 1]))
            {
                return (minutes, true);
            }
        }

        return (Enumerable.Range(0, ok.Length).Select(i => (double)i).ToArray(), false);
    }

    /// <summary>
    /// The least-squares fit of y = plateau - drop x exp(-t / tau): tau on a grid from a fiftieth
    /// of the span to three times it (step a two-hundredth), the two linear coefficients solved
    /// for each, the best by the residual sum of squares. Returns NaN when nothing fits (fewer
    /// than three points, a span that is not positive, or every exponential degenerate).
    /// </summary>
    /// <param name="t">The times, increasing; not null.</param>
    /// <param name="y">The values, one per time; not null.</param>
    /// <returns>The plateau, the drop, the time constant (in the unit of
    /// <paramref name="t"/>) and the rms residual, or four NaNs.</returns>
    internal static (double Plateau, double Drop, double Tau, double Rms) PlateauFit(double[] t, double[] y)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(y);
        int n = t.Length;
        double span = t[^1] - t[0];
        if (n < 3 || span <= 0)
        {
            return (double.NaN, double.NaN, double.NaN, double.NaN);
        }

        double bestRss = double.PositiveInfinity, bestPlateau = double.NaN, bestDrop = double.NaN, bestTau = double.NaN;
        double step = span / 200;
        for (int k = 0; k <= 596; k++)
        {
            double tau = span / 50 + k * step;
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                double x = Math.Exp(-(t[i] - t[0]) / tau);
                sx += x;
                sy += y[i];
                sxx += x * x;
                sxy += x * y[i];
            }

            double den = n * sxx - sx * sx;
            if (den <= 1e-12)
            {
                continue;
            }

            double c = (n * sxy - sx * sy) / den;
            double a = (sy - c * sx) / n;
            double rss = 0;
            for (int i = 0; i < n; i++)
            {
                double e = y[i] - a - c * Math.Exp(-(t[i] - t[0]) / tau);
                rss += e * e;
            }

            if (rss < bestRss)
            {
                (bestRss, bestPlateau, bestDrop, bestTau) = (rss, a, -c, tau);
            }
        }

        return double.IsNaN(bestPlateau) ? (double.NaN, double.NaN, double.NaN, double.NaN) : (bestPlateau, bestDrop, bestTau, Math.Sqrt(bestRss / n));
    }
}
