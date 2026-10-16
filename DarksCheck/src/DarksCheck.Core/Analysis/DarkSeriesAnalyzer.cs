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

/// <summary>The figures of one CFA position of one frame (R-020): levels in ADU above black, the counts at <see cref="Th.HotLevelsAdu"/>.</summary>
internal sealed record PlaneFigures(string Name, double Median, double Sigma, double Mean, double P999, IReadOnlyList<int> Above, int Saturated, int Low, int Min, int Max);

/// <summary>One CFA position of one frame against the median master (R-022): the residual's noise and the banding of its row and column means against white noise.</summary>
internal sealed record ResidualFigures(string Name, double Sigma, double Median, double Mean, double RowStd, double RowExpected, double ColStd, double ColExpected)
{
    /// <summary>The row banding against white noise: the deviation of the row means over what white noise would give (1 = no banding); NaN without an expectation.</summary>
    internal double RowBandingRatio => RowExpected > 0 ? RowStd / RowExpected : double.NaN;
}

/// <summary>The edges of a frame's binned reference plane relative to its centre, in ADU (R-021).</summary>
internal sealed record EdgeFigures(double Left, double Right, double Top, double Bottom, double TopLeft);

/// <summary>One dark of the series: its metadata, its figures per plane and against the master, its hot-pixel amplitude relative to the master (R-023), or the error that kept it out.</summary>
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

/// <summary>The median master's figures on one CFA position (R-030): level, noise, the counts at the hot levels, saturated and dead pixels.</summary>
internal sealed record MasterFigures(string Name, double Median, double Sigma, IReadOnlyList<int> Above, int Saturated, int Low);

/// <summary>
/// The stability of the master's hot pixels over the frames (R-031): how many frames keep
/// each above half the threshold, how many are always there, how many flicker, the
/// amplitude percentiles, the temporal noise of hot and quiet pixels and the
/// random-telegraph candidates of the reference plane (a temporal sigma above
/// <see cref="Th.RtsSigmaFactor"/> times the typical frame noise).
/// </summary>
internal sealed record HotStability(double ThresholdAdu, int Hot, IReadOnlyList<int> FramesHotHistogram, int Always, int Flicker, double P10, double P50, double P90, double P99, double HotTemporalStd, double QuietTemporalStd, int RtsCandidates);

internal sealed record NoisePoint(int N, double Measured, double Expected);

/// <summary>The noise of the mean of n frames against sigma / sqrt(n) (R-024); the temporal sigma (the n = 1 point: frame against frame, the fixed pattern out), the median master's noise from half the frames, the predicted noise of the whole median master (<see cref="Th.MedianNoiseFactor"/> x temporal / sqrt(N)), and the side of the crop read.</summary>
internal sealed record NoiseVsN(IReadOnlyList<NoisePoint> Curve, double SigmaFrame, double TemporalSigma, double MedianHalfVsMeanHalf, double MasterNoise, int CropPx);

/// <summary>The mean of nine areas of a binned map, in ADU above black: the centre (40 .. 60 %), the four corners and the four edges (each a tenth of the map).</summary>
internal sealed record Regions(double Centre, double TopLeft, double TopRight, double BottomLeft, double BottomRight, double Top, double Bottom, double Left, double Right)
{
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
internal sealed record BinnedMap(string Name, IReadOnlyList<float> Values, int W, int H, Regions Regions);

/// <summary>The structured banding of the master on one CFA position (R-040): the row and column profiles minus their running median, as a standard deviation and peak to peak.</summary>
internal sealed record BandingFigures(string Name, double RowStd, double ColStd, double RowP2p, double ColP2p);

/// <summary>The warming of the series read from its hot pixels (R-023): the amplitude ratio to the master at the first and the last frame, and the temperature offset between them.</summary>
internal sealed record Warming(double FirstRatio, double LastRatio, double DeltaCelsius, double Minutes, int Pixels);

/// <summary>Everything measured on a series of darks; the words are the report's.</summary>
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
    internal static DarkSeriesAnalysis Analyze(FrameStack stack, int threads, IProgress<(int Done, int Total)>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(stack);
        IReadOnlyList<StackFrame> decoded = stack.Decoded;
        if (decoded.Count == 0)
        {
            throw new InvalidOperationException("niciun dark nu s-a putut decoda" + FrameStack.FirstErrors(stack));
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

    /// <summary>The smallest spacing between the values present from black - 10 to black + 30, and how many distinct values the plane holds.</summary>
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
    internal static float[] MedianMaster(FrameStack stack, int threads)
    {
        ArgumentNullException.ThrowIfNull(stack);
        IReadOnlyList<StackFrame> decoded = stack.Decoded;
        if (decoded.Count == 0)
        {
            throw new InvalidOperationException("niciun dark al partenerului nu s-a putut decoda" + FrameStack.FirstErrors(stack));
        }

        float[] median = new float[stack.Width * stack.Height];
        Masters(decoded, stack.Width, stack.Height, median, new float[median.Length], new float[median.Length], new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads) });
        return median;
    }

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
                edges = new EdgeFigures(r.Left - r.Centre, r.Right - r.Centre, r.Top - r.Centre, r.Bottom - r.Centre, r.TopLeft - r.Centre);
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

    /// <summary>A level in ADU equivalent as raw ADU of this stack, never under <see cref="Th.MinHotAdu"/>.</summary>
    internal static double HotLevel(double levelEq, double norm) => Math.Max(levelEq * norm, Th.MinHotAdu);

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

    /// <summary>The profiles are read with the hot pixels clamped to the hot threshold: one 600 ADU pixel would otherwise lift its row by 600 / width, a band that is not there.</summary>
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

    private static Warming WarmingOf(DarkFrameFigures[] frames, int pixels, Normalisation nz)
    {
        DarkFrameFigures[] ok = frames.Where(f => f.Error is null && !double.IsNaN(f.AmpRatioMedian)).ToArray();
        if (ok.Length < 2 || pixels == 0)
        {
            return new Warming(double.NaN, double.NaN, double.NaN, 0, pixels);
        }

        double first = ok[0].AmpRatioMedian, last = ok[^1].AmpRatioMedian;
        double dT = first > 0 && last > 0 ? nz.Degrees(last / first) : double.NaN;
        double minutes = ok[0].Info.Timestamp is DateTime t0 && ok[^1].Info.Timestamp is DateTime t1 ? (t1 - t0).TotalMinutes : double.NaN;
        return new Warming(first, last, dT, minutes, pixels);
    }
}
