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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>The noise estimators on frames of known Gaussian noise (R-024).</summary>
public class NoiseAnalyzerTests
{
    private static Raster Noise(int seed, double sigma, double level, int w = 800, int h = 600)
    {
        SyntheticFrame f = new() { Width = w, Height = h, Seed = seed, NoiseSigma = sigma, BackgroundR = level, BackgroundG = level, BackgroundB = level, White = 65535 };
        return Raster.FromImg(f.Render(), 0);
    }

    [Fact]
    public void R024_HighPassSigmaReadsTheNoiseOfOneCfaPosition()
    {
        Raster r = Noise(1, 20, 1000);
        for (int p = 0; p < 4; p++)
        {
            // The 5 x 5 median adds a little variance of its own: a few percent above sigma.
            Assert.InRange(NoiseAnalyzer.HighPassSigma(r, p / 2, p % 2), 19, 22.5);
        }
    }

    [Fact]
    public void R024_ReadNoiseFromAPairIsTheNoiseOverRootTwo()
    {
        Raster a = Noise(1, 12, 100), b = Noise(2, 12, 100);
        IReadOnlyList<ReadNoise> rn = NoiseAnalyzer.ReadNoiseFromPair(a, b, CfaPattern.Parse("RGGB", "t"));
        Assert.Equal(["R", "G1", "G2", "B"], rn.Select(n => n.Name).ToArray());
        Assert.All(rn, n => Assert.InRange(n.PairSigma, 11, 13));
        Assert.All(rn, n => Assert.InRange(n.HighPassSigma, 11, 13.5));
    }

    [Fact]
    public void R024_SwampingSubtractsTheReadNoiseAndReadsTheGain()
    {
        // Sky 400 ADU with total noise 25 ADU against read noise 10: shot variance 525, ratio 5.25, gain 0.76.
        List<ChannelLevel> ch = [new("G1", 'G', 400, 0, 0, 25)];
        List<ReadNoise> rn = [new("G1", 10, 10)];
        ChannelNoise c = Assert.Single(NoiseAnalyzer.Swamping(ch, rn));
        Assert.Equal(525, c.ShotSigma * c.ShotSigma, 1e-6);
        Assert.Equal(5.25, c.SwampingRatio, 1e-9);
        Assert.Equal(400 / 525.0, c.GainEPerAdu!.Value, 1e-9);
        Assert.Equal(100 * (Math.Sqrt(1 + 1 / 5.25) - 1), c.PenaltyPct, 1e-9);
    }

    [Fact]
    public void R024_ClippedStdIgnoresOutliers()
    {
        List<double> v = Enumerable.Range(0, 1000).Select(i => (i % 2 == 0 ? 1.0 : -1.0)).ToList();
        v.Add(1000);
        Assert.Equal(1.0, NoiseAnalyzer.ClippedStd(v, 4, 5), 0.01);
    }
}
