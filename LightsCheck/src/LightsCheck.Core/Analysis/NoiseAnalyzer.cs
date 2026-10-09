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
/// The read noise of one CFA position, in ADU: from the difference of two biases (clean) and
/// from the high-pass estimator on one bias (to correct the lights' high-pass sigma by the
/// same estimator's bias).
/// </summary>
internal sealed record ReadNoise(string Name, double PairSigma, double HighPassSigma);

/// <summary>
/// On one light, one CFA position: the sky median, the total noise, the shot noise after the
/// read noise is removed, the gain that implies, and the swamping ratio (shot variance over
/// read variance) with the noise penalty read noise adds, in percent (R-024).
/// </summary>
internal sealed record ChannelNoise(string Name, double Sky, double TotalSigma, double ShotSigma, double? GainEPerAdu, double SwampingRatio, double PenaltyPct);

/// <summary>Noise figures: the prototype's `noise2.py`.</summary>
internal static class NoiseAnalyzer
{
    /// <summary>Half-side of the central window of the CFA position (300 x 300 of the position, 600 x 600 of the frame).</summary>
    internal const int WindowHalf = 300;

    /// <summary>
    /// The sigma-clipped standard deviation (4 sigma, 5 iterations) of the central window of
    /// the CFA position (<paramref name="py"/>, <paramref name="px"/>) minus its 5 x 5 median:
    /// the noise with the sky and the stars taken out. The window shrinks on a small frame.
    /// </summary>
    internal static double HighPassSigma(Raster raw, int py, int px)
    {
        ArgumentNullException.ThrowIfNull(raw);
        int ch = (raw.H - py + 1) / 2, cw = (raw.W - px + 1) / 2;
        int half = Math.Min(WindowHalf, Math.Min(ch, cw) / 2);
        int y0 = ch / 2 - half, x0 = cw / 2 - half, n = 2 * half;
        if (n < 7)
        {
            return double.NaN;
        }

        float[] c = new float[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                c[y * n + x] = raw.D[(2 * (y0 + y) + py) * raw.W + 2 * (x0 + x) + px];
            }
        }

        float[] buf = new float[25];
        List<double> hp = new((n - 4) * (n - 4));
        for (int y = 2; y < n - 2; y++)
        {
            for (int x = 2; x < n - 2; x++)
            {
                int k = 0;
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        buf[k++] = c[(y + dy) * n + x + dx];
                    }
                }

                hp.Add(c[y * n + x] - Raster.Median(buf, 25));
            }
        }

        return ClippedStd(hp, 4.0, 5);
    }

    /// <summary>astropy's sigma_clipped_stats: clip at <paramref name="sigmas"/> around the median, <paramref name="iterations"/> times, then the standard deviation.</summary>
    internal static double ClippedStd(List<double> values, double sigmas, int iterations)
    {
        ArgumentNullException.ThrowIfNull(values);
        List<double> v = values;
        for (int it = 0; it < iterations && v.Count > 2; it++)
        {
            double med = Raster.Percentile(v, 50);
            double sd = Std(v);
            List<double> kept = v.Where(x => Math.Abs(x - med) <= sigmas * sd).ToList();
            if (kept.Count == v.Count)
            {
                break;
            }

            v = kept;
        }

        return Std(v);
    }

    private static double Std(List<double> v)
    {
        if (v.Count < 2)
        {
            return double.NaN;
        }

        double mean = v.Average();
        double s = 0;
        foreach (double x in v)
        {
            s += (x - mean) * (x - mean);
        }

        return Math.Sqrt(s / v.Count);
    }

    /// <summary>
    /// The read noise per CFA position from two biases: the standard deviation of their
    /// difference over sqrt 2 in the central window (the fixed pattern cancels), clipped at
    /// 4 sigma; and the high-pass estimator on the first bias, for the correction of the lights.
    /// </summary>
    internal static IReadOnlyList<ReadNoise> ReadNoiseFromPair(Raster a, Raster b, CfaPattern? cfa)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.H != b.H || a.W != b.W)
        {
            throw new InvalidOperationException("cele doua bias-uri au dimensiuni diferite");
        }

        List<ReadNoise> result = [];
        for (int p = 0; p < 4; p++)
        {
            int py = p / 2, px = p % 2;
            int ch = (a.H - py + 1) / 2, cw = (a.W - px + 1) / 2;
            int half = Math.Min(WindowHalf, Math.Min(ch, cw) / 2);
            int y0 = ch / 2 - half, x0 = cw / 2 - half, n = 2 * half;
            List<double> diff = new(n * n);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = (2 * (y0 + y) + py) * a.W + 2 * (x0 + x) + px;
                    diff.Add(a.D[i] - b.D[i]);
                }
            }

            result.Add(new ReadNoise(Mosaic.Label(cfa, p), ClippedStd(diff, 4.0, 5) / Math.Sqrt(2), HighPassSigma(a, py, px)));
        }

        return result;
    }

    /// <summary>
    /// The swamping figures of one light's channels against the read noise: the light's
    /// high-pass sigma is corrected by the ratio of the pair sigma to the high-pass sigma on
    /// the bias (the same estimator's bias on both), the read variance subtracted, and the
    /// gain read as sky over shot variance (null when the shot variance is not positive).
    /// </summary>
    internal static IReadOnlyList<ChannelNoise> Swamping(IReadOnlyList<ChannelLevel> channels, IReadOnlyList<ReadNoise> readNoise)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(readNoise);
        List<ChannelNoise> result = [];
        for (int p = 0; p < channels.Count && p < readNoise.Count; p++)
        {
            ChannelLevel c = channels[p];
            ReadNoise rn = readNoise[p];
            // The estimator's correction only when the bias gave a usable high-pass sigma.
            double correction = rn.HighPassSigma > 0 && !double.IsNaN(rn.HighPassSigma) ? rn.PairSigma / rn.HighPassSigma : 1.0;
            double total = c.HighPassSigma * correction;
            double shotVar = total * total - rn.PairSigma * rn.PairSigma;
            double ratio = shotVar / (rn.PairSigma * rn.PairSigma);
            double penalty = shotVar > 0 ? 100 * (Math.Sqrt(1 + 1 / ratio) - 1) : double.NaN;
            result.Add(new ChannelNoise(c.Name, c.Median, total, shotVar > 0 ? Math.Sqrt(shotVar) : 0, shotVar > 0 ? c.Median / shotVar : null, ratio, penalty));
        }

        return result;
    }
}
