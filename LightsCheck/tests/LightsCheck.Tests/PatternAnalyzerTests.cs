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

    /// <summary>
    /// The pattern of crops whose frames step by <paramref name="step"/> px each along X, so the
    /// groups of frames are what the step makes them; every frame's stars are
    /// <paramref name="width"/> px wide, or have no width.
    /// </summary>
    private static SensorPattern Analyze(PatternCrop?[] crops, IReadOnlyList<ChannelLevel>?[] channels, double step, double? width = 3.0)
    {
        int n = crops.Length;
        double?[] x = Enumerable.Range(0, n).Select(i => (double?)(i * step)).ToArray();
        double?[] y = new double?[n];
        Array.Fill(y, 0.0);
        return PatternAnalyzer.Analyze(crops, channels, DitherAnalyzer.Analyze(x, y, new double?[n]), x, y, Enumerable.Repeat(width, n).ToArray());
    }

    /// <summary>
    /// A crop of noise with six Gaussian stars of <paramref name="fwhm"/> px, 30 to 280 sigma
    /// high, the same stars in every frame, moved by <paramref name="dx"/> px along X.
    /// </summary>
    private static PatternCrop StarCrop(int seed, double fwhm, double dx)
    {
        float[] d = Crop(seed, []).Data;
        // Crop draws its own 3 x 3 block at (10..12, 20..22): taken back out, the stars below stand alone.
        for (int y = 20; y < 23; y++)
        {
            for (int x = 10; x < 13; x++)
            {
                d[y * Size + x] -= 500;
            }
        }

        double sigma = fwhm / 2.3548;
        (double X, double Y, double Peak)[] stars = [(12, 12, 300), (30, 15, 2800), (45, 28, 900), (14, 40, 1800), (33, 47, 600), (48, 52, 2400)];
        foreach ((double sx, double sy, double peak) in stars)
        {
            double cx = sx + dx;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    double r2 = (x - cx) * (x - cx) + (y - sy) * (y - sy);
                    if (r2 < 25 * sigma * sigma)
                    {
                        d[y * Size + x] += (float)(peak * Math.Exp(-r2 / (2 * sigma * sigma)));
                    }
                }
            }
        }

        return new PatternCrop(Size, d);
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

        SensorPattern p = Analyze(crops, ch, 4);

        Assert.Equal(PatternStatus.Measured, p.Status);
        Assert.Equal(n, p.Frames);
        Assert.Equal(Size, p.CropSize);
        // The window of a CFA position of a 64 px crop is 32 x 32, minus the 2 px border: 28 x 28, four positions.
        Assert.Equal(4 * 28 * 28, p.Pixels);
        Assert.InRange(p.Hot, 5, 6);
        Assert.Equal(["R", "G1", "G2", "B"], p.Names);
        Assert.All(p.FrameSigma, v => Assert.Equal(FrameSigma, v));
        // The crops hold noise and 36 pattern pixels only: the median's own noise, measured from the odd and
        // the even frames' medians, is taken out, and what is left is the pattern's few pixels spread over 28 x 28, under 2 sigma.
        Assert.All(p.PatternSigma, v => Assert.InRange(v, 0.0, 2.0));
        Assert.All(Enumerable.Range(0, 4), i => Assert.InRange(p.FractionOfFrame(i), 0.0, 0.2));
    }

    [Fact]
    public void R046_FewerThanSevenFramesOrNoRegistrationGiveNoFigure()
    {
        PatternCrop?[] six = Enumerable.Range(0, 6).Select(i => (PatternCrop?)Crop(i, Pattern)).ToArray();
        Assert.Equal(PatternStatus.TooFewFrames, Analyze(six, Enumerable.Repeat(Channels(), 6).ToArray(), 4).Status);

        PatternCrop?[] eight = Enumerable.Range(0, 8).Select(i => (PatternCrop?)Crop(i, Pattern)).ToArray();
        DitherAnalysis none = DitherAnalyzer.Analyze(new double?[8], new double?[8], new double?[8]);
        Assert.Equal(PatternStatus.Unregistered, PatternAnalyzer.Analyze(eight, Enumerable.Repeat(Channels(), 8).ToArray(), none, new double?[8], new double?[8], new double?[8]).Status);

        Assert.Equal(PatternStatus.NoCrops, Analyze(new PatternCrop?[3], new IReadOnlyList<ChannelLevel>?[3], 4).Status);
    }

    /// <summary>Frames that stayed put put the same star on the same pixels in every frame: the median would read it as pattern, so nothing is read.</summary>
    [Fact]
    public void R046_FramesThatDidNotMoveGiveNoFigure()
    {
        PatternCrop?[] crops = Enumerable.Range(0, 10).Select(i => (PatternCrop?)Crop(i, Pattern)).ToArray();
        SensorPattern p = Analyze(crops, Enumerable.Repeat(Channels(), 10).ToArray(), 0.2);
        Assert.Equal(PatternStatus.FramesDidNotMove, p.Status);
        Assert.Equal(10, p.Frames);
    }

    /// <summary>
    /// Forty frames that were not dithered and drifted 0.17 px a frame, 6.6 px in all, with stars
    /// 4.2 px wide: no three-pixel group holds half the frames, yet every star sits on its own
    /// pixels in most of them, and the median would count it as hot pixels. Judged at three star
    /// widths the frames did not move, and nothing is read (R-046, CR-005 of the review of
    /// 2026-11-13).
    /// </summary>
    [Fact]
    public void R046_StarsThatDriftSlowlyWithoutDitheringGiveNoFigure()
    {
        const int n = 40;
        PatternCrop?[] crops = Enumerable.Range(0, n).Select(i => (PatternCrop?)StarCrop(i, 4.2, 0.17 * i)).ToArray();

        SensorPattern p = Analyze(crops, Enumerable.Repeat(Channels(), n).ToArray(), 0.17, width: 4.2);

        Assert.Equal(PatternStatus.FramesDidNotMove, p.Status);
        Assert.Equal(0, p.Hot);
        Assert.Equal(12.6, p.ClusterRadiusPx, 6);
        Assert.Equal(n, p.LargestCluster);
    }

    /// <summary>
    /// The same stars on frames 4 px apart: three or four frames share a group at three star
    /// widths, the stars move off every pixel, and the figure is read with no star in it.
    /// </summary>
    [Fact]
    public void R046_StarsThatMoveWithTheDitherLeaveNoHotPixel()
    {
        const int n = 40;
        PatternCrop?[] crops = Enumerable.Range(0, n).Select(i => (PatternCrop?)StarCrop(i, 4.2, 4.0 * (i % 10))).ToArray();
        double?[] x = Enumerable.Range(0, n).Select(i => (double?)(4.0 * (i % 10))).ToArray();
        double?[] y = new double?[n];
        Array.Fill(y, 0.0);

        SensorPattern p = PatternAnalyzer.Analyze(crops, Enumerable.Repeat(Channels(), n).ToArray(), DitherAnalyzer.Analyze(x, y, new double?[n]), x, y, Enumerable.Repeat((double?)4.2, n).ToArray());

        Assert.Equal(PatternStatus.Measured, p.Status);
        Assert.Equal(16, p.LargestCluster);   // the positions 0, 4, 8 and 12 px, four frames at each
        Assert.Equal(0, p.Hot);
    }

    /// <summary>
    /// The distance the group is judged at is three times the stars' width, and 3 px when that
    /// is smaller or no frame has a width: twelve frames 4 px apart are spread enough for stars
    /// 3 px wide (three frames within 9 px), not for stars 8 px wide (seven within 24 px).
    /// </summary>
    [Theory]
    [InlineData(3.0, 9.0, 3, true)]
    [InlineData(8.0, 24.0, 7, false)]
    [InlineData(0.5, 3.0, 1, true)]
    [InlineData(null, 3.0, 1, true)]
    public void R046_TheGroupIsJudgedAtThreeTimesTheStarsWidth(double? width, double radius, int largest, bool measured)
    {
        const int n = 12;
        PatternCrop?[] crops = Enumerable.Range(0, n).Select(i => (PatternCrop?)Crop(i, Pattern, dx: 4 * i)).ToArray();

        SensorPattern p = Analyze(crops, Enumerable.Repeat(Channels(), n).ToArray(), 4, width);

        Assert.Equal(measured ? PatternStatus.Measured : PatternStatus.FramesDidNotMove, p.Status);
        Assert.Equal(radius, p.ClusterRadiusPx, 6);
        Assert.Equal(largest, p.LargestCluster);
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
