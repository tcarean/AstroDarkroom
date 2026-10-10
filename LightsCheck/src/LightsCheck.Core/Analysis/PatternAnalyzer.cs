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

/// <summary>
/// The central square of one light's raw mosaic, above black, kept for the sensor's pattern
/// (R-046): <see cref="Size"/> pixels a side, cut at an even row and column so its CFA phase
/// is the frame's.
/// </summary>
internal sealed record PatternCrop(int Size, float[] Data)
{
    /// <summary>Th.PatternCrop a side, or the frame's shorter side rounded down to even when that is smaller.</summary>
    internal static PatternCrop Cut(Raster raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        int size = Math.Min(Th.PatternCrop, Math.Min(raw.H, raw.W)) & ~1;
        int y0 = ((raw.H - size) / 2) & ~1, x0 = ((raw.W - size) / 2) & ~1;
        float[] d = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            Array.Copy(raw.D, (y0 + y) * raw.W + x0, d, y * size, size);
        }

        return new PatternCrop(size, d);
    }
}

/// <summary>Whether the pattern could be read, and why not.</summary>
internal enum PatternStatus
{
    Measured,
    NoCrops,
    TooFewFrames,
    Unregistered,
    FramesDidNotMove,
}

/// <summary>
/// The sensor's fixed pattern as the lights carry it (R-046): per CFA position, the sigma of
/// the per-pixel median of the crops after the high-pass, with the median's own noise (measured
/// from the odd and the even frames' medians) taken out, against the frames' median high-pass
/// sigma (FrameSigma); and how many pixels per
/// megapixel stand above Th.PatternHotSigma sigmas of that noise. The counts are over the
/// pixels measured; Frames and CropSize say on what.
/// </summary>
internal sealed record SensorPattern(
    PatternStatus Status,
    int Frames,
    int CropSize,
    int Pixels,
    int Hot,
    IReadOnlyList<string> Names,
    IReadOnlyList<double> PatternSigma,
    IReadOnlyList<double> FrameSigma)
{
    internal double HotPerMpx => Pixels > 0 ? 1e6 * Hot / Pixels : 0;

    /// <summary>The pattern's sigma as a fraction of one frame's noise, per position; NaN where either is unknown.</summary>
    internal double FractionOfFrame(int p) => p < PatternSigma.Count && p < FrameSigma.Count && FrameSigma[p] > 0 ? PatternSigma[p] / FrameSigma[p] : double.NaN;

    internal static SensorPattern Empty(PatternStatus status, int frames) => new(status, frames, 0, 0, 0, [], [], []);
}

/// <summary>
/// Reads the sensor's pattern out of the lights themselves: the frames are not registered,
/// so the sky and the stars move with the dither while the warm pixels stay put, and the
/// per-pixel median across the frames keeps what stayed. The pattern of one frame is lost in
/// that frame's noise, but the median of n frames carries only a fraction of it; that
/// remainder is measured on the same crops, as the difference of the medians of the odd and
/// the even frames (the pattern cancels, the noise stays), and taken out of the pattern's
/// sigma in quadrature.
/// </summary>
internal static class PatternAnalyzer
{
    /// <summary>
    /// <paramref name="crops"/> and <paramref name="channels"/> are per frame (null where the
    /// frame was not measured); only the crops of registered frames of the first crop's size
    /// count, since the rule below reads the registered positions. The median is
    /// read only when at least Th.PatternMinFrames frames give it and the largest cluster of
    /// frames at Th.CoverageTight holds under Th.PatternClusterFraction of the registered
    /// frames, so no pixel sees a star in half the frames or more.
    /// </summary>
    internal static SensorPattern Analyze(IReadOnlyList<PatternCrop?> crops, IReadOnlyList<IReadOnlyList<ChannelLevel>?> channels, DitherAnalysis dither)
    {
        ArgumentNullException.ThrowIfNull(crops);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(dither);
        PatternCrop? first = crops.FirstOrDefault(c => c is not null);
        if (first is null)
        {
            return SensorPattern.Empty(PatternStatus.NoCrops, 0);
        }

        if (dither.Registered < 2)
        {
            return SensorPattern.Empty(PatternStatus.Unregistered, 0);
        }

        int size = first.Size;
        List<int> used = Enumerable.Range(0, crops.Count).Where(i => crops[i] is { } c && c.Size == size && i < dither.NearestOther.Count && dither.NearestOther[i] is not null).ToList();
        if (used.Count < Th.PatternMinFrames)
        {
            return SensorPattern.Empty(PatternStatus.TooFewFrames, used.Count);
        }

        if (dither.LargestClusterTight >= Th.PatternClusterFraction * dither.Registered)
        {
            return SensorPattern.Empty(PatternStatus.FramesDidNotMove, used.Count);
        }

        Raster median = Median(crops, used, size);
        // The medians of the odd and the even frames: their difference holds the noise of a
        // half-median twice and none of the pattern.
        List<int> odd = used.Where((_, k) => k % 2 == 0).ToList(), even = used.Where((_, k) => k % 2 == 1).ToList();
        Raster diff = Median(crops, odd, size);
        Raster evenMedian = Median(crops, even, size);
        for (int i = 0; i < diff.D.Length; i++)
        {
            diff.D[i] -= evenMedian.D[i];
        }

        // Var(odd - even) = c / na + c / nb and Var(median of n) = c / n, for the same c.
        double noiseShare = 1.0 / used.Count / (1.0 / odd.Count + 1.0 / even.Count);

        List<IReadOnlyList<ChannelLevel>> with = used.Select(i => channels[i]).Where(c => c is not null).Select(c => c!).ToList();
        List<string> names = [];
        List<double> patternSigma = [], frameSigma = [];
        int pixels = 0, hot = 0;
        for (int p = 0; p < 4; p++)
        {
            List<double> sigmas = with.Where(c => c.Count > p).Select(c => c[p].HighPassSigma).Where(v => !double.IsNaN(v)).ToList();
            double fs = sigmas.Count > 0 ? Raster.Percentile(sigmas, 50) : double.NaN;
            List<double> hp = NoiseAnalyzer.HighPass(median, p / 2, p % 2);
            double measured = hp.Count == 0 ? double.NaN : NoiseAnalyzer.ClippedStd(hp, 4.0, 5);
            List<double> hpDiff = NoiseAnalyzer.HighPass(diff, p / 2, p % 2);
            double diffSigma = hpDiff.Count == 0 ? double.NaN : NoiseAnalyzer.ClippedStd(hpDiff, 4.0, 5);
            // Without the noise estimate the sigma would still hold the median's own noise: no figure then.
            double noise2 = diffSigma * diffSigma * noiseShare;
            names.Add(with.Count > 0 && with[0].Count > p ? with[0][p].Name : $"CFA[{p / 2},{p % 2}]");
            patternSigma.Add(double.IsNaN(measured) || double.IsNaN(noise2) ? double.NaN : Math.Sqrt(Math.Max(0, measured * measured - noise2)));
            frameSigma.Add(fs);
            if (double.IsNaN(fs) || fs <= 0)
            {
                continue;
            }

            pixels += hp.Count;
            hot += hp.Count(v => v > Th.PatternHotSigma * fs);
        }

        return new SensorPattern(PatternStatus.Measured, used.Count, size, pixels, hot, names, patternSigma, frameSigma);
    }

    /// <summary>The per-pixel median of the crops at the given indices, as a raster of the crop's size.</summary>
    private static Raster Median(IReadOnlyList<PatternCrop?> crops, List<int> indices, int size)
    {
        Raster m = new(size, size);
        float[] buf = new float[indices.Count];
        for (int i = 0; i < size * size; i++)
        {
            for (int k = 0; k < indices.Count; k++)
            {
                buf[k] = crops[indices[k]]!.Data[i];
            }

            m.D[i] = Raster.Median(buf, indices.Count);
        }

        return m;
    }
}
