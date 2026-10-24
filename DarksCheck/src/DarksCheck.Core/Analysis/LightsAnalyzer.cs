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

/// <summary>One light (R-050): the sky and its sigma per plane, the detector's sigma (of the local excess) per plane, the hot-pixel candidates and the isolated ones, its amplitude at the dark's hot pixels relative to the master, or why it has no figures.</summary>
internal sealed record LightFigures(FrameInfo Info, string? Error, bool Skipped, bool AssumedSettings, IReadOnlyList<double> SkyMedian, IReadOnlyList<double> SkySigma, IReadOnlyList<double> LocalSigma, int Candidates, int IsolatedHot, double AmpRatioMedian, double AmpRatioQ25, double AmpRatioQ75);

/// <summary>At one level of the dark master: how many pixels the master has above it, and how many of those are persistent in the lights (R-051); the persistent count itself is <see cref="LightsAnalysis.Persistent"/>.</summary>
internal sealed record OverlapAtLevel(double LevelAdu, int DarkCount, int Both);

/// <summary>
/// The lights against the dark (check 9): every light's figures, the pixels persistent over
/// the lights, their overlap with the dark master at the hot levels, the ones the dark misses,
/// the amplitude ratio of the lights over the master per light and over the night, and the
/// verdict (R-050 to R-052).
/// </summary>
internal sealed record LightsAnalysis(
    int Used,
    int AssumedSettings,
    IReadOnlyList<LightFigures> Frames,
    int PersistentThreshold,
    int Persistent,
    IReadOnlyList<int> HotCountHistogram,
    IReadOnlyList<OverlapAtLevel> Overlap,
    int Missed,
    double MissedDarkExcessMedian,
    int AmpPixels,
    MatchFigures LightsOverDark,
    double RatioFirst,
    double RatioLast);

/// <summary>
/// Check 9 as the prototype did it (`prototype/analyze_darks.py`, the lights loop): each
/// light is decoded on its own, never kept; a pixel more than <see cref="Th.LightHotSigmas"/>
/// above the median of its eight same-colour neighbours, under saturation, with no immediate
/// neighbour above <see cref="Th.LightIsolationSigmas"/>, is an isolated hot pixel; a pixel
/// flagged in <see cref="Th.PersistentFraction"/> of the lights is persistent. The amplitude
/// of the dark master's hot pixels in each light is the local excess at those positions.
/// </summary>
internal static class LightsAnalyzer
{
    internal static LightsAnalysis Analyze(IReadOnlyList<string> paths, DarkSeriesAnalysis darks, int threads, int maxFrames, IProgress<(int Done, int Total)>? progress = null, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(darks);
        int w = darks.Width, h = darks.Height, n = paths.Count;
        int step = n > maxFrames ? (int)Math.Ceiling(n / (double)maxFrames) : 1;
        float[] excess = new float[w * h];
        for (int i = 0; i < excess.Length; i++)
        {
            excess[i] = (float)(darks.MedianMaster[i] - darks.Black);
        }

        double hotT = DarkSeriesAnalyzer.HotLevel(Th.HotAdu, darks.Norm);
        double ampMax = Th.AmplitudeMaxFraction * darks.White;
        List<int> ampIdx = [];
        for (int i = 0; i < excess.Length; i++)
        {
            if (excess[i] > hotT && darks.MedianMaster[i] < ampMax)
            {
                ampIdx.Add(i);
            }
        }

        int[] counts = new int[w * h];
        LightFigures[] frames = new LightFigures[n];
        object gate = new();
        int done = 0, used = 0;
        ParallelOptions po = new() { MaxDegreeOfParallelism = Math.Max(1, threads), CancellationToken = cancel };
        Parallel.For(0, n, po, i =>
        {
            string path = paths[i];
            if (i % step != 0)
            {
                frames[i] = new LightFigures(FrameInfo.Read(i + 1, path), $"sarit (peste {maxFrames} cadre, se decodeaza unul din {step})", true, false, [], [], [], 0, 0, double.NaN, double.NaN, double.NaN);
                progress?.Report((Interlocked.Increment(ref done), n));
                return;
            }

            try
            {
                (StackFrame frame, double black, double white) = FrameStack.DecodeOne(i + 1, path, w, h);
                if (frame.Pixels is null)
                {
                    frames[i] = new LightFigures(frame.Info, frame.Error, false, false, [], [], [], 0, 0, double.NaN, double.NaN, double.NaN);
                }
                else
                {
                    double lightBlack = double.IsNaN(black) ? darks.Black : black;
                    // a light that does not say its ISO or exposure is taken at the darks' settings, the
                    // usual case and the one check 1 verifies, and the light is marked
                    bool assumed = frame.Info.Iso is not double || frame.Info.ExposureSeconds is not double;
                    double normL = assumed ? darks.Norm : darks.Normalisation.NormOf(frame.Info);
                    (LightFigures f, List<int> isolated) = Measure(frame, w, h, lightBlack, white, normL, assumed, excess, darks.Norm, ampIdx);
                    frames[i] = f;
                    lock (gate)
                    {
                        used++;
                        foreach (int k in isolated)
                        {
                            counts[k]++;
                        }
                    }
                }

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                frames[i] = new LightFigures(FrameInfo.Read(i + 1, path), e.Message, false, false, [], [], [], 0, 0, double.NaN, double.NaN, double.NaN);
            }

            progress?.Report((Interlocked.Increment(ref done), n));
        });

        if (used == 0)
        {
            throw new InvalidOperationException("niciun light nu s-a putut decoda" + FirstErrors(frames));
        }

        // persistence and overlap with the dark master
        int threshold = (int)Math.Ceiling(Th.PersistentFraction * used);
        int[] hist = new int[used + 1];
        int persistent = 0;
        foreach (int c in counts)
        {
            hist[Math.Min(c, used)]++;
            if (c >= threshold)
            {
                persistent++;
            }
        }

        List<OverlapAtLevel> overlap = [];
        foreach (double level in Th.OverlapLevelsAdu)
        {
            double raw = DarkSeriesAnalyzer.HotLevel(level, darks.Norm);
            int dark = 0, both = 0;
            for (int i = 0; i < excess.Length; i++)
            {
                if (excess[i] > raw)
                {
                    dark++;
                    if (counts[i] >= threshold)
                    {
                        both++;
                    }
                }
            }

            overlap.Add(new OverlapAtLevel(level, dark, both));
        }

        List<float> missedExcess = [];
        for (int i = 0; i < excess.Length; i++)
        {
            if (counts[i] >= threshold && excess[i] <= hotT)
            {
                missedExcess.Add((float)(excess[i] / darks.Norm));
            }
        }

        LightFigures[] ok = frames.Where(f => f.Error is null && !double.IsNaN(f.AmpRatioMedian)).ToArray();
        MatchFigures match = IndexAnalyzer.Match(ok.Select(f => (float)f.AmpRatioMedian).ToArray(), darks.Normalisation);
        return new LightsAnalysis(used, frames.Count(f => f.AssumedSettings), frames, threshold, persistent, hist, overlap, missedExcess.Count,
            missedExcess.Count == 0 ? double.NaN : Planes.Median([.. missedExcess]), ampIdx.Count, match,
            ok.Length == 0 ? double.NaN : ok[0].AmpRatioMedian, ok.Length == 0 ? double.NaN : ok[^1].AmpRatioMedian);
    }

    private static string FirstErrors(LightFigures[] frames)
    {
        string[] errors = frames.Where(f => f.Error is not null && !f.Skipped).Take(3).Select(f => f.Info.FileName + ": " + f.Error).ToArray();
        return errors.Length == 0 ? "" : ": " + string.Join("; ", errors);
    }

    /// <summary>One light: the local excess per plane, its sigma, the candidates and the isolated hot pixels, the sky, the amplitude at the dark's hot pixels.</summary>
    private static (LightFigures Figures, List<int> Isolated) Measure(StackFrame frame, int w, int h, double black, double white, double normL, bool assumed, float[] darkExcess, double normD, List<int> ampIdx)
    {
        ushort[] px = frame.Pixels!;
        float[] e = new float[w * h];
        float[] z = new float[w * h];
        double[] skyMedian = new double[4];
        double[] skySigma = new double[4];
        double[] localSigma = new double[4];
        for (int p = 0; p < 4; p++)
        {
            int pw = Planes.PlaneWidth(w, p), ph = Planes.PlaneHeight(h, p);
            float[] plane = Planes.Extract(px, w, h, p);
            (double sSigma, double sMedian) = Planes.ClippedSigma(Subsample(plane, pw, ph, 4));
            skyMedian[p] = sMedian - black;
            skySigma[p] = sSigma;
            float[] local = LocalExcess(plane, pw, ph);
            (double sigma, _) = Planes.ClippedSigma(Subsample(local, pw, ph, 4));
            localSigma[p] = sigma;
            float inv = sigma > 0 ? (float)(1 / sigma) : 0;
            int y0 = p >> 1, x0 = p & 1;
            for (int j = 0; j < ph; j++)
            {
                int row = (y0 + 2 * j) * w + x0;
                int o = j * pw;
                for (int i = 0; i < pw; i++)
                {
                    e[row + 2 * i] = local[o + i];
                    z[row + 2 * i] = local[o + i] * inv;
                }
            }
        }

        // candidates and isolation: the neighbours of a pixel are the eight around it at full
        // resolution, whatever their colour, with 0 outside the frame (as the prototype)
        float hot = (float)Th.LightHotSigmas, iso = (float)Th.LightIsolationSigmas;
        float sat = (float)(Th.LightSaturationFraction * white);
        int candidates = 0;
        List<int> isolated = [];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (z[i] <= hot || px[i] >= sat)
                {
                    continue;
                }

                candidates++;
                float max = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = y + dy;
                    if (yy < 0 || yy >= h)
                    {
                        continue;
                    }

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx;
                        if ((dx == 0 && dy == 0) || xx < 0 || xx >= w)
                        {
                            continue;
                        }

                        float v = z[yy * w + xx];
                        if (v > max)
                        {
                            max = v;
                        }
                    }
                }

                if (max < iso)
                {
                    isolated.Add(i);
                }
            }
        }

        float[] ratio = new float[ampIdx.Count];
        for (int k = 0; k < ratio.Length; k++)
        {
            int i = ampIdx[k];
            ratio[k] = (float)((e[i] / normL) / (darkExcess[i] / normD));
        }

        bool any = ratio.Length > 0;
        LightFigures f = new(frame.Info, null, false, assumed, skyMedian, skySigma, localSigma, candidates, isolated.Count,
            any ? Planes.Median(ratio) : double.NaN, any ? Planes.Percentile(ratio, 25) : double.NaN, any ? Planes.Percentile(ratio, 75) : double.NaN);
        return (f, isolated);
    }

    /// <summary>Every k-th row and column of a plane, as the prototype's `e[::4, ::4]`.</summary>
    private static float[] Subsample(float[] plane, int pw, int ph, int k)
    {
        int sw = (pw + k - 1) / k, sh = (ph + k - 1) / k;
        float[] s = new float[sw * sh];
        for (int j = 0; j < sh; j++)
        {
            for (int i = 0; i < sw; i++)
            {
                s[j * sw + i] = plane[(j * k) * pw + i * k];
            }
        }

        return s;
    }

    /// <summary>Each pixel minus the median of its eight same-colour neighbours (the mean of the two middle values), the border repeated (the prototype's `mode='edge'`).</summary>
    internal static float[] LocalExcess(float[] plane, int pw, int ph)
    {
        ArgumentNullException.ThrowIfNull(plane);
        float[] e = new float[pw * ph];
        Span<float> nb = stackalloc float[8];
        for (int y = 0; y < ph; y++)
        {
            int ym = Math.Max(y - 1, 0), yp = Math.Min(y + 1, ph - 1);
            for (int x = 0; x < pw; x++)
            {
                int xm = Math.Max(x - 1, 0), xp = Math.Min(x + 1, pw - 1);
                nb[0] = plane[ym * pw + xm];
                nb[1] = plane[ym * pw + x];
                nb[2] = plane[ym * pw + xp];
                nb[3] = plane[y * pw + xm];
                nb[4] = plane[y * pw + xp];
                nb[5] = plane[yp * pw + xm];
                nb[6] = plane[yp * pw + x];
                nb[7] = plane[yp * pw + xp];
                InsertionSort(nb);
                e[y * pw + x] = plane[y * pw + x] - 0.5f * (nb[3] + nb[4]);
            }
        }

        return e;
    }

    private static void InsertionSort(Span<float> a)
    {
        for (int i = 1; i < a.Length; i++)
        {
            float v = a[i];
            int j = i - 1;
            while (j >= 0 && a[j] > v)
            {
                a[j + 1] = a[j];
                j--;
            }

            a[j + 1] = v;
        }
    }
}
