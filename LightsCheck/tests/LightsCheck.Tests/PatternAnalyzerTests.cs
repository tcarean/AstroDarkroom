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

/// <summary>The sensor's pattern read from the lights' median (R-046), on synthetic crops with known warm and hot pixels.</summary>
public class PatternAnalyzerTests
{
    private const int Size = 64;
    private const double FrameSigma = 10;

    /// <summary>A crop of pure Gaussian noise of <see cref="FrameSigma"/> around 1000 ADU, plus the given pixels; the stars are left out on purpose.</summary>
    private static PatternCrop Crop(int seed, IReadOnlyList<(int X, int Y, double Adu)> pattern, double dx = 0)
    {
        Random rng = new(seed);
        float[] d = new float[Size * Size];
        for (int i = 0; i < d.Length; i += 2)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            double m = Math.Sqrt(-2.0 * Math.Log(u1));
            d[i] = (float)(1000 + m * Math.Cos(2 * Math.PI * u2) * FrameSigma);
            if (i + 1 < d.Length)
            {
                d[i + 1] = (float)(1000 + m * Math.Sin(2 * Math.PI * u2) * FrameSigma);
            }
        }

        foreach ((int x, int y, double adu) in pattern)
        {
            d[y * Size + x] += (float)adu;
        }

        // A "star" that moves with the dither: a 3 x 3 block of +500 at a position shifted per frame.
        int sx = 10 + (int)dx, sy = 20;
        for (int y = sy; y < sy + 3; y++)
        {
            for (int x = sx; x < sx + 3; x++)
            {
                d[y * Size + x] += 500;
            }
        }

        return new PatternCrop(Size, d);
    }

    private static IReadOnlyList<ChannelLevel> Channels() =>
        [new("R", 'R', 1000, 0, 0, FrameSigma), new("G1", 'G', 1000, 0, 0, FrameSigma), new("G2", 'G', 1000, 0, 0, FrameSigma), new("B", 'B', 1000, 0, 0, FrameSigma)];

    /// <summary>n frames whose positions step by <paramref name="step"/> px each, so the clusters at 3 px are what the step makes them.</summary>
    private static DitherAnalysis Dither(int n, double step)
    {
        double?[] x = Enumerable.Range(0, n).Select(i => (double?)(i * step)).ToArray();
        double?[] y = new double?[n];
        Array.Fill(y, 0.0);
        return DitherAnalyzer.Analyze(x, y, new double?[n]);
    }

    private static readonly IReadOnlyList<(int X, int Y, double Adu)> Pattern =
    [
        .. Enumerable.Range(0, 30).Select(i => (5 + (i * 7) % 50, 5 + (i * 11) % 50, 5 * FrameSigma)),   // warm: 5 sigma
        .. Enumerable.Range(0, 6).Select(i => (6 + (i * 13) % 50, 40 + (i * 3) % 20, 40 * FrameSigma)),   // hot: 40 sigma
    ];

    [Fact]
    public void R046_ThePatternSigmaAndTheHotPixelsOfTheMedianAreReadAndTheMovingStarIsNot()
    {
        int n = 12;
        PatternCrop?[] crops = Enumerable.Range(0, n).Select(i => (PatternCrop?)Crop(i, Pattern, dx: 4 * i)).ToArray();
        IReadOnlyList<ChannelLevel>?[] ch = Enumerable.Repeat(Channels(), n).ToArray();

        SensorPattern p = PatternAnalyzer.Analyze(crops, ch, Dither(n, 4));

        Assert.Equal(PatternStatus.Measured, p.Status);
        Assert.Equal(n, p.Frames);
        Assert.Equal(Size, p.CropSize);
        // The window of a CFA position of a 64 px crop is 32 x 32, minus the 2 px border: 28 x 28, four positions.
        Assert.Equal(4 * 28 * 28, p.Pixels);
        Assert.InRange(p.Hot, 5, 6);
        Assert.Equal(["R", "G1", "G2", "B"], p.Names);
        Assert.All(p.FrameSigma, v => Assert.Equal(FrameSigma, v));
        // The crops hold noise and 36 pattern pixels only: the median's own noise (1.25 * 10 / sqrt(12) = 3.6)
        // is taken out, and what is left is the pattern's few pixels spread over 28 x 28, under 2 sigma.
        Assert.All(p.PatternSigma, v => Assert.InRange(v, 0.0, 2.0));
        Assert.All(Enumerable.Range(0, 4), i => Assert.InRange(p.FractionOfFrame(i), 0.0, 0.2));
    }

    [Fact]
    public void R046_FewerThanSevenFramesOrNoRegistrationGiveNoFigure()
    {
        PatternCrop?[] six = Enumerable.Range(0, 6).Select(i => (PatternCrop?)Crop(i, Pattern)).ToArray();
        Assert.Equal(PatternStatus.TooFewFrames, PatternAnalyzer.Analyze(six, Enumerable.Repeat(Channels(), 6).ToArray(), Dither(6, 4)).Status);

        PatternCrop?[] eight = Enumerable.Range(0, 8).Select(i => (PatternCrop?)Crop(i, Pattern)).ToArray();
        DitherAnalysis none = DitherAnalyzer.Analyze(new double?[8], new double?[8], new double?[8]);
        Assert.Equal(PatternStatus.Unregistered, PatternAnalyzer.Analyze(eight, Enumerable.Repeat(Channels(), 8).ToArray(), none).Status);

        Assert.Equal(PatternStatus.NoCrops, PatternAnalyzer.Analyze(new PatternCrop?[3], new IReadOnlyList<ChannelLevel>?[3], Dither(3, 4)).Status);
    }

    /// <summary>Frames that stayed put put the same star on the same pixels in every frame: the median would read it as pattern, so nothing is read.</summary>
    [Fact]
    public void R046_FramesThatDidNotMoveGiveNoFigure()
    {
        PatternCrop?[] crops = Enumerable.Range(0, 10).Select(i => (PatternCrop?)Crop(i, Pattern)).ToArray();
        SensorPattern p = PatternAnalyzer.Analyze(crops, Enumerable.Repeat(Channels(), 10).ToArray(), Dither(10, 0.2));
        Assert.Equal(PatternStatus.FramesDidNotMove, p.Status);
        Assert.Equal(10, p.Frames);
    }

    /// <summary>The crop is cut at an even row and column, so a pixel's CFA position in the crop is its position in the frame.</summary>
    [Fact]
    public void R046_TheCropKeepsTheCfaPhaseAndShrinksToASmallFrame()
    {
        Raster r = new(30, 45);
        for (int y = 0; y < r.H; y++)
        {
            for (int x = 0; x < r.W; x++)
            {
                r.D[y * r.W + x] = 100 * (y % 2) + (x % 2);   // the CFA position written into every pixel
            }
        }

        PatternCrop c = PatternCrop.Cut(r);
        Assert.Equal(30, c.Size);
        Assert.Equal(0, c.Data[0]);          // even row, even column
        Assert.Equal(101, c.Data[c.Size + 1]);
    }

    [Fact]
    public void R046_TheCropIsTheCentralSquareOfALargeFrame()
    {
        Raster r = new(1000, 1500);
        r.D[500 * r.W + 750] = 7;
        PatternCrop c = PatternCrop.Cut(r);
        Assert.Equal(Th.PatternCrop, c.Size);
        int y0 = ((1000 - Th.PatternCrop) / 2) & ~1, x0 = ((1500 - Th.PatternCrop) / 2) & ~1;
        Assert.Equal(7, c.Data[(500 - y0) * c.Size + 750 - x0]);
    }
}
