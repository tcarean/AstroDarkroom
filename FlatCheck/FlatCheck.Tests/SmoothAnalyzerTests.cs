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

namespace FlatCheck.Tests;

/// <summary>
/// The smoothing analyzer against synthetics with a KNOWN truth - the same scenes the
/// numpy prototype was validated on (docs/smooth-validation.md), scaled from the D3300's
/// 3008 x 2008 position raster to 1024 x 683 so the suite stays fast. The tile grid is
/// then 41 x 28 of 25 px, the same geometry, and the kernel is passed as the scale that
/// gives sigma = 2 tiles, as on the real masters.
///
/// Truth = vignetting to ~55 % at the corner x (6 % gradient + 3 % + 2 % saddle), with
/// mid-scale blobs where a test says so; contamination = 0.5 % noise, dust, and the
/// fabric cells (a random field with a 300-sensor-px correlation length, here 51 px).
/// </summary>
public class SmoothAnalyzerTests
{
    internal const int W = 1024, H = 683;
    internal const int Tile = 25;                       // Metrics.TileSize on a 1024-wide raster
    internal const double Sigma2 = 2 * Tile;            // scale (sigma in px) that gives sigma = 2 tiles on mono

    internal static Func<int, int, double> Truth(bool blobs = false, int seed = 1)
    {
        double cx = W / 2.0, cy = H / 2.0, hd = Math.Sqrt(cx * cx + cy * cy);
        var rng = new Random(seed);
        var blob = new List<(double x, double y, double a)>();
        if (blobs)
            for (int i = 0; i < 12; i++)
                blob.Add((rng.NextDouble() * W, rng.NextDouble() * H, (rng.NextDouble() - 0.5) * 0.06));
        return (x, y) =>
        {
            double xn = (x - cx) / hd, yn = (y - cy) / hd, r2 = xn * xn + yn * yn;
            double v = 1.0 / Math.Pow(1 + 0.9 * r2, 2) * (1 + 0.06 * xn + 0.03 * yn + 0.02 * xn * yn);
            double b = 0;
            foreach (var (bx, by, a) in blob)
                b += a * Math.Exp(-((x - bx) * (x - bx) + (y - by) * (y - by)) / (2 * 204.0 * 204.0));
            return v * (1 + b);
        };
    }

    internal static Func<int, int, double> Cells(int count = 400, double sigma = 12.6, double amp = 0.03, int seed = 2)
    {
        var rng = new Random(seed);
        var cells = new List<(double x, double y, double a)>();
        for (int i = 0; i < count; i++)
            cells.Add((rng.NextDouble() * W, rng.NextDouble() * H, (rng.NextDouble() * 2 - 1) * amp));
        return (x, y) =>
        {
            double s = 0;
            foreach (var (bx, by, a) in cells)
            {
                double d2 = (x - bx) * (x - bx) + (y - by) * (y - by);
                if (d2 < 25 * sigma * sigma) s += a * Math.Exp(-d2 / (2 * sigma * sigma));
            }
            return 1 + s;
        };
    }

    static readonly (double x, double y)[] DustAt = { (170, 136), (851, 510), (987, 34) };

    internal static Func<int, int, double> Dust(double depth = 0.15, double sigma = 10)
        => (x, y) =>
        {
            double s = 0;
            foreach (var (bx, by) in DustAt)
                s -= depth * Math.Exp(-((x - bx) * (x - bx) + (y - by) * (y - by)) / (2 * sigma * sigma));
            return 1 + s;
        };

    internal static Func<int, int, double> Noise(double sigma = 0.005, int seed = 3)
    {
        var rng = new Random(seed);
        return (x, y) =>
        {
            // Box-Muller
            double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();
            return 1 + sigma * Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        };
    }

    internal static Img Observed(Func<int, int, double> truth, params Func<int, int, double>[] factors)
        => TestImages.From(W, H, (x, y) =>
        {
            double v = truth(x, y);
            foreach (var f in factors) v *= f(x, y);
            return v;
        });

    internal static Img Error(Img smoothed, Func<int, int, double> truth)
        => TestImages.From(W, H, (x, y) => smoothed.At(y, x) / truth(x, y) - 1);

    internal static double MaxAbs(Img e, int inset = 0)
    {
        double m = 0;
        for (int y = inset; y < e.H - inset; y++)
            for (int x = inset; x < e.W - inset; x++)
                m = Math.Max(m, Math.Abs(e.At(y, x)));
        return m;
    }

    internal static double Rms(Img e)
    {
        double s = 0;
        foreach (double v in e.D) s += v * v;
        return Math.Sqrt(s / e.D.Length);
    }

    // ---------------------------------------------------------------- the floor

    /// <summary>
    /// The method's floor: vignetting, gradient and saddle plus noise, nothing to
    /// remove. The prototype read 0.19 % over the whole frame at full size; here the
    /// tile medians carry a little more noise (25 px tiles instead of 75), hence the
    /// margin. Corners included - that is where a wide lens is steepest.
    /// </summary>
    [Fact]
    public void AVignettedGradientIsRecoveredToUnderATenthOfAPercentRms_CornersIncluded()
    {
        var truth = Truth();
        var r = SmoothAnalyzer.Analyze(Observed(truth, Noise()), Sigma2);

        Img e = Error(r.Smoothed, truth);

        Assert.InRange(MaxAbs(e), 0, 0.004);
        Assert.InRange(Rms(e), 0, 0.001);
        Assert.False(r.IsCfa);
        Assert.Single(r.Positions);
        Assert.Equal(0, r.Positions[0].Rejected);
    }

    /// <summary>
    /// The partial last tile sits where it is. 1023 columns leave a 23-px last tile;
    /// fitted one full tile from its neighbour it put a 0.8 % band along the edge on the
    /// prototype. No band now: the outer 30 px are as good as the interior.
    /// </summary>
    [Fact]
    public void ThePartialLastTileSitsWhereItIs_NoErrorBandAlongTheEdge()
    {
        var truth = Truth();
        Img obs = TestImages.From(W - 1, H, (x, y) => truth(x, y) * Noise()(x, y));

        var r = SmoothAnalyzer.Analyze(obs, Sigma2);
        Img e = TestImages.From(W - 1, H, (x, y) => r.Smoothed.At(y, x) / truth(x, y) - 1);

        double band = 0;
        for (int y = 0; y < e.H; y++)
            for (int x = e.W - 30; x < e.W; x++)
                band = Math.Max(band, Math.Abs(e.At(y, x)));
        Assert.InRange(band, 0, 0.004);
        Assert.Equal(W - 1, r.Smoothed.W);
    }

    // ---------------------------------------------------------------- what goes, what stays

    [Fact]
    public void CellsAndDustAreRemoved_TheGradientAndBlobsAreKept()
    {
        var truth = Truth(blobs: true);
        var r = SmoothAnalyzer.Analyze(Observed(truth, Cells(), Dust(), Noise()), Sigma2);

        Img e = Error(r.Smoothed, truth);

        // the truth, blobs included, is recovered to well under the cells' 3 %
        Assert.InRange(Rms(e), 0, 0.006);
        // at the dust spots the file no longer has the -15 % dip
        foreach (var (x, y) in DustAt)
            Assert.InRange(Math.Abs(e.At((int)y, (int)x)), 0, 0.015);
        // and the removed component says so: the deepest spot is dust-sized and negative
        Assert.True(r.RemovedSpotPct < -1.0, "removed spot " + r.RemovedSpotPct);
        Assert.True(r.Positions[0].Rejected > 0);
    }

    /// <summary>
    /// The clip floor: a clean master with 0.5 % blobs must have NO tile rejected. Relative
    /// to the MAD alone, 8 % of the Samyang's tiles were thrown out and the blobs with them.
    /// </summary>
    [Fact]
    public void TheClipFloorKeepsSmoothStructureOnACleanField()
    {
        var truth = Truth(blobs: true);
        Img obs = TestImages.From(W, H, (x, y) => truth(x, y) * (1 + 0.005 * Math.Sin(x / 60.0) * Math.Cos(y / 45.0)));

        var r = SmoothAnalyzer.Analyze(obs, Sigma2);

        Assert.Equal(0, r.Positions[0].Rejected);
        Assert.InRange(r.RemovedMaxTilePct, 0, 1.0);
        Assert.False(r.RemovedFlagged);
    }

    [Fact]
    public void ADeepRemovalFlagsTheWarning()
    {
        var truth = Truth();
        // one +8 % blotch 40 px wide: compact, so the smoothing takes it, and above the 2 % warning
        Img obs = TestImages.From(W, H, (x, y) =>
            truth(x, y) * (1 + 0.08 * Math.Exp(-((x - 300) * (x - 300) + (y - 200) * (y - 200)) / (2 * 40.0 * 40.0))));

        var r = SmoothAnalyzer.Analyze(obs, Sigma2);

        Assert.True(r.RemovedFlagged);
        Assert.True(r.RemovedMaxTilePct >= Th.SmoothKeptWarn);
    }

    // ---------------------------------------------------------------- CFA

    /// <summary>
    /// Four positions with four levels, smoothed separately: each keeps its level, the
    /// 2x2 pattern is exactly the input's, and no chequerboard is written.
    /// </summary>
    [Fact]
    public void TheOutputHasNoChequerboard_AndEachPositionKeepsItsLevel()
    {
        var truth = Truth();
        Img obs = TestImages.From(W, H, (x, y) =>
            truth(x, y) * FitsTestData.WithBayerMosaic(FitsTestData.Constant(1000))(x, y) * Noise()(x, y));

        var r = SmoothAnalyzer.Analyze(obs, 2 * 2 * 12);   // position tiles are 12 px, pitch 2 -> sigma 2

        Assert.True(r.IsCfa);
        Assert.Equal(4, r.Positions.Length);
        for (int p = 0; p < 4; p++)
        {
            double inMed = Stats.Percentile(Mosaic.Extract(obs, p).D, 50);
            double outMed = Stats.Percentile(Mosaic.Extract(r.Smoothed, p).D, 50);
            Assert.InRange(outMed / inMed, 0.999, 1.001);
        }
        // a 2x2 block of the output has the input's gains, not an average of them
        Assert.InRange(r.Smoothed.At(100, 100) / r.Smoothed.At(100, 101), 0.62 * 0.995, 0.62 * 1.005);
        Assert.Equal(H, r.Smoothed.H);
        Assert.Equal(W, r.Smoothed.W);
    }

    [Fact]
    public void TheScaleIsInSensorPixels_WhateverTheTile()
    {
        Assert.Equal(2.0, SmoothAnalyzer.SigmaTiles(300, 150), 9);   // CFA on a 6016-wide sensor
        Assert.Equal(2.0, SmoothAnalyzer.SigmaTiles(300, 150), 9);   // mono on a 6016-wide sensor: same tile in px
        Assert.Equal(1.0, SmoothAnalyzer.SigmaTiles(150, 150), 9);
    }

    // ---------------------------------------------------------------- invariants

    [Fact]
    public void NothingIsRenormalised_AndRemovedIsCentredOnOne()
    {
        var truth = Truth();
        Img obs = Observed(truth, Noise());

        var r = SmoothAnalyzer.Analyze(obs, Sigma2);

        Assert.InRange(Stats.Percentile(r.Smoothed.D, 50) / Stats.Percentile(obs.D, 50), 0.998, 1.002);
        Assert.InRange(Stats.Percentile(r.Removed.D, 50), 0.999, 1.001);
        Assert.Equal(r.Removed.W, r.Width);
        Assert.Equal(r.Removed.H, r.Height);
        Assert.Equal(r.Before.Width, r.After.Width);
    }

    [Fact]
    public void AConstantFrameDoesNotThrow_AndComesBackConstant()
    {
        var r = SmoothAnalyzer.Analyze(TestImages.From(200, 120, (x, y) => 7), 10);

        Assert.All(r.Smoothed.D, v => Assert.Equal(7, v, 6));
        Assert.Equal(0, r.Positions[0].Rejected);
    }

    /// <summary>
    /// Small, but not tiny: below ~8 rows the structure battery itself throws
    /// (CornerStats, known issue 3), and that is not this mode's to fix.
    /// </summary>
    [Fact]
    public void ASmallOddFrameDoesNotThrow_AndKeepsItsShape()
    {
        var r = SmoothAnalyzer.Analyze(TestImages.From(65, 47, (x, y) => 100 + x), 4);

        Assert.Equal(47, r.Smoothed.H);
        Assert.Equal(65, r.Smoothed.W);
        Assert.All(r.Smoothed.D, v => Assert.False(double.IsNaN(v)));
        Assert.InRange(r.Smoothed.At(20, 30), 129.5, 130.5);
    }
}
