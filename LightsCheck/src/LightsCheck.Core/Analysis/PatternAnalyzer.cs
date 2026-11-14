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
/// <param name="Size">The side of the square, in pixels, even.</param>
/// <param name="Data">
/// The pixels above black, in ADU, row by row, <paramref name="Size"/> squared of them.
/// </param>
internal sealed record PatternCrop(int Size, float[] Data)
{
    /// <summary>
    /// Th.PatternCrop a side, or the frame's shorter side rounded down to even when that is
    /// smaller.
    /// </summary>
    /// <param name="raw">The light's mosaic above black, in ADU; not modified. Not null.</param>
    /// <returns>
    /// A copy of the central square, its top-left corner on an even row and column.
    /// </returns>
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
/// <param name="Status">Whether the pattern was measured, and if not, why.</param>
/// <param name="Frames">
/// The crops the median was read on, or would have been; 0 without a crop or under two registered
/// frames.
/// </param>
/// <param name="CropSize">The side of the crops, in pixels; 0 when not measured.</param>
/// <param name="Pixels">
/// The pixels of the median the hot count is over: the positions with a known frame sigma; 0 when
/// not measured.
/// </param>
/// <param name="Hot">
/// The pixels whose high-pass residual exceeds <see cref="Th.PatternHotSigma"/> times their
/// position's frame sigma.
/// </param>
/// <param name="Names">The four positions' names; empty when not measured.</param>
/// <param name="PatternSigma">
/// Per position, the pattern's sigma with the median's own noise taken out, in ADU; NaN where an
/// estimate is missing. Empty when not measured.
/// </param>
/// <param name="FrameSigma">
/// Per position, the median over the frames of <see cref="ChannelLevel.HighPassSigma"/>, in ADU;
/// NaN where no frame gave one. Empty when not measured.
/// </param>
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

    /// <summary>
    /// The distance, in pixels, the frames' groups were judged at before the median was read:
    /// <see cref="Th.PatternClusterWidths"/> times the stars' width, <see cref="Th.CoverageTight"/>
    /// at least; 0 when the analysis stopped before that rule.
    /// </summary>
    internal double ClusterRadiusPx { get; init; }

    /// <summary>
    /// The frames of the largest group within <see cref="ClusterRadiusPx"/>; 0 when the analysis
    /// stopped before that rule.
    /// </summary>
    internal int LargestCluster { get; init; }

    /// <summary>
    /// Whether <see cref="ClusterRadiusPx"/> came from the stars' width, as against the floor of
    /// <see cref="Th.CoverageTight"/>, which the report words differently.
    /// </summary>
    internal bool ClusterByStarWidth { get; init; }

    /// <summary>
    /// The pattern's sigma as a fraction of one frame's noise, per position; NaN where either is
    /// unknown.
    /// </summary>
    /// <param name="p">The CFA position, 0 to 3.</param>
    /// <returns>
    /// The fraction, 0 or more; NaN for a position out of the lists or without a positive frame
    /// sigma.
    /// </returns>
    internal double FractionOfFrame(int p) => p < PatternSigma.Count && p < FrameSigma.Count && FrameSigma[p] > 0 ? PatternSigma[p] / FrameSigma[p] : double.NaN;

    /// <summary>A pattern that was not measured.</summary>
    /// <param name="status">Why not.</param>
    /// <param name="frames">The crops that would have been used.</param>
    /// <returns>The status and the count, every figure 0 and every list empty.</returns>
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
    /// read only when at least Th.PatternMinFrames frames give it and the largest group of
    /// frames within Th.PatternClusterWidths times the stars' width (Th.CoverageTight at least)
    /// holds under Th.PatternClusterFraction of the registered frames, so no pixel sees a star
    /// in half the frames or more.
    /// </summary>
    /// <param name="crops">
    /// Per frame, its central crop; null where the frame was not measured. Not null.
    /// </param>
    /// <param name="channels">
    /// Per frame, its channel levels; null where the frame was not measured. Not null.
    /// </param>
    /// <param name="dither">
    /// The series' dithering, in the same frame order, for which frames have a position and how
    /// many. Not null.
    /// </param>
    /// <param name="x">
    /// Per frame, its registered X position, in pixels; null where the frame has none. Not null.
    /// </param>
    /// <param name="y">Per frame, its registered Y position, in pixels; null where X is. Not null.</param>
    /// <param name="fwhm">
    /// Per frame, the median width (FWHM) of its stars, in pixels; null where the frame has none.
    /// Not null.
    /// </param>
    /// <returns>
    /// The measured pattern, or an empty one whose status says why it was not read.
    /// </returns>
    internal static SensorPattern Analyze(IReadOnlyList<PatternCrop?> crops, IReadOnlyList<IReadOnlyList<ChannelLevel>?> channels, DitherAnalysis dither,
        IReadOnlyList<double?> x, IReadOnlyList<double?> y, IReadOnlyList<double?> fwhm)
    {
        ArgumentNullException.ThrowIfNull(crops);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(dither);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(fwhm);
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

        // The frames must have moved by more than a star covers. A bright star's core is wider
        // than its FWHM, and a pixel between two groups of frames closer than that sees the star
        // from both: the groups are judged at a distance that follows the stars' width.
        List<double> widths = used.Where(i => i < fwhm.Count && fwhm[i] is not null).Select(i => fwhm[i]!.Value).ToList();
        double byWidth = widths.Count > 0 ? Th.PatternClusterWidths * Raster.Percentile(widths, 50) : 0;
        double radius = Math.Max(Th.CoverageTight, byWidth);
        int largest = DitherAnalyzer.LargestCluster(x, y, radius);
        if (largest >= Th.PatternClusterFraction * dither.Registered)
        {
            return SensorPattern.Empty(PatternStatus.FramesDidNotMove, used.Count) with { ClusterRadiusPx = radius, LargestCluster = largest, ClusterByStarWidth = byWidth > Th.CoverageTight };
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

        double noiseShare = NoiseShare(used.Count, odd.Count, even.Count);

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
            // Without the noise estimate the sigma would still hold the median's own noise: no
            // figure then.
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

        return new SensorPattern(PatternStatus.Measured, used.Count, size, pixels, hot, names, patternSigma, frameSigma)
        {
            ClusterRadiusPx = radius,
            LargestCluster = largest,
            ClusterByStarWidth = byWidth > Th.CoverageTight,
        };
    }

    /// <summary>
    /// The share of the variance of (the odd frames' median minus the even frames' median) that is
    /// the variance of the median of all the frames: the three medians are of different counts,
    /// and a median's variance follows its count and its parity
    /// (<see cref="MedianVariance"/>).
    /// </summary>
    /// <param name="n">The frames the pattern's median is taken over, 2 or more.</param>
    /// <param name="odd">The frames of the first half-median, 1 or more.</param>
    /// <param name="even">The frames of the second half-median, 1 or more.</param>
    /// <returns>The share, about a quarter: 0.241 for 10 frames in halves of 5, 0.282 for 8 in halves of 4.</returns>
    internal static double NoiseShare(int n, int odd, int even) => MedianVariance(n) / (MedianVariance(odd) + MedianVariance(even));

    /// <summary>
    /// The variance of the median of <paramref name="n"/> independent Gaussian values of variance
    /// 1, the median taken as <see cref="Raster.Median"/> takes it: the middle value of an odd
    /// count, the mean of the two middle values of an even one. Worked out by numeric integration
    /// over the middle values' distribution, not read from a table.
    /// </summary>
    /// <param name="n">How many values, 1 or more.</param>
    /// <returns>The variance: 1 for one value, 0.5 for two, near pi / (2 n) for many.</returns>
    internal static double MedianVariance(int n)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);
        // The grid: 8 sigmas each way, fine enough for the middle values of many frames, which
        // crowd within about 1.25 / sqrt(n) of zero.
        double h = Math.Min(1.0 / 128, 0.05 / Math.Sqrt(n));
        int count = (int)(16 / h) + 1;
        double[] x = new double[count], density = new double[count], below = new double[count], above = new double[count];
        for (int i = 0; i < count; i++)
        {
            x[i] = -8 + i * h;
            density[i] = Math.Exp(-0.5 * x[i] * x[i]) / Math.Sqrt(2 * Math.PI);
        }

        // The chance of a value below and above each point, each summed from its own tail so
        // that neither loses its small end.
        for (int i = 1; i < count; i++)
        {
            below[i] = below[i - 1] + 0.5 * h * (density[i - 1] + density[i]);
        }

        for (int i = count - 2; i >= 0; i--)
        {
            above[i] = above[i + 1] + 0.5 * h * (density[i + 1] + density[i]);
        }

        int m = n / 2;
        double weights = 0, squares = 0;
        if (n % 2 == 1)
        {
            // The middle value is at x when m values fall below it and m above. The chances
            // are doubled, so the weight is 1 at the middle whatever the count: left as they
            // are, 0.25 to the power m is too small for a double from about 1070 values on.
            for (int i = 1; i < count - 1; i++)
            {
                double w = Math.Exp(m * (Math.Log(2 * below[i]) + Math.Log(2 * above[i]))) * density[i];
                weights += w;
                squares += w * x[i] * x[i];
            }

            return squares / weights;
        }

        // An even count: the mean of the lower middle value a and the upper one b. By symmetry
        // its variance is (E[a a] + E[a b]) / 2, and E[a b] = E[a (a + gap)], the gap being the
        // mean distance from a to the smallest of the m values above it.
        double[] gap = new double[count];
        for (int i = count - 3; i >= 1; i--)
        {
            double ratio = Math.Pow(above[i + 1] / above[i], m);
            gap[i] = 0.5 * h * (1 + ratio) + ratio * gap[i + 1];
        }

        for (int i = 1; i < count - 1; i++)
        {
            double w = Math.Exp((m - 1) * Math.Log(2 * below[i]) + m * Math.Log(2 * above[i])) * density[i];
            weights += w;
            squares += w * (x[i] * x[i] + 0.5 * x[i] * gap[i]);
        }

        return squares / weights;
    }

    /// <summary>
    /// The per-pixel median of the crops at the given indices, as a raster of the crop's size.
    /// </summary>
    /// <param name="crops">Per frame, its crop.</param>
    /// <param name="indices">
    /// The frames whose crops are used, each with a crop of <paramref name="size"/>; at least one.
    /// </param>
    /// <param name="size">The crops' side, in pixels.</param>
    /// <returns>The median raster, in ADU above black.</returns>
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
