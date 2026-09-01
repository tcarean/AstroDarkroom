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
/// Phase 5: the geometry and metrics, tested against scenes whose answer is known
/// analytically rather than taken from the code's own output.
///
/// The recurring shape of these tests: on a *perfectly radial* field every structure
/// metric must read ~0, because that is the tool's entire premise - a correct flat is
/// radial by construction, so anything the metrics report on one is measurement error.
/// On a field with an injected linear gradient the same metrics must fire.
/// </summary>
public class MetricsTests
{
    static Img Make(int w, int h, Func<int, int, double> f) => TestImages.From(w, h, f);


    /// <summary>A field that is exactly quadratic in log space, so FitCenter's paraboloid
    /// fit is exact and the vertex it recovers is the one we put in.</summary>
    static Img LogParaboloid(int w, int h, double px, double py, double sigma = 100)
        => Make(w, h, (x, y) =>
        {
            double dx = x - px, dy = y - py;
            return 30000 * Math.Exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));
        });

    // ---------------------------------------------------------------- FitCenter

    [Fact]
    public void FitCenter_OnACentredRadialField_FindsTheGeometricCentre()
    {
        var img = Make(128, 128, FitsTestData.Radial(128, 128));

        var (cx, cy) = Metrics.FitCenter(img);

        Assert.Equal(64, cx, 1);
        Assert.Equal(64, cy, 1);
    }

    [Fact]
    public void FitCenter_RecoversAnOffCentrePeak()
    {
        var img = LogParaboloid(128, 128, px: 80, py: 40);

        var (cx, cy) = Metrics.FitCenter(img);

        Assert.Equal(80, cx, 1);
        Assert.Equal(40, cy, 1);
    }

    /// <summary>
    /// The one place the C# deliberately diverges from flatcheck.py, found by the
    /// cross-check. On a constant frame the fitted quadratic coefficients are pure
    /// floating-point noise (~1e-15) and the vertex determinant is ~1e-29. The
    /// |det| &lt; 1e-15 guard falls back to the sensor centre; Python has no guard, so
    /// np.linalg.solve happily returns garbage and reports a ~6% centroid offset and a
    /// SUSPECT verdict on a perfectly uniform frame.
    ///
    /// Do not "fix" this to match the oracle - the guard is what makes the degenerate
    /// case behave. See CLAUDE.md, "Parity with Python".
    /// </summary>
    [Fact]
    public void FitCenter_OnAConstantImage_FallsBackToTheGeometricCentre()
    {
        // no curvature -> the vertex system is degenerate
        var img = Make(64, 32, (x, y) => 1000);

        var (cx, cy) = Metrics.FitCenter(img);

        Assert.Equal(32, cx, 6);
        Assert.Equal(16, cy, 6);
    }

    [Fact]
    public void FitCenter_WhenTheVertexLandsOutsideTheFrame_FallsBackToTheGeometricCentre()
    {
        // peak far off to the right: the fit is fine, the answer is just not in frame
        var img = LogParaboloid(128, 128, px: 500, py: 64);

        var (cx, cy) = Metrics.FitCenter(img);

        Assert.Equal(64, cx, 6);
        Assert.Equal(64, cy, 6);
    }

    // ---------------------------------------------------------------- RadialModel

    [Fact]
    public void RadialModel_OnAPerfectlyRadialField_LeavesAFlatResidual()
    {
        var img = Make(256, 256, FitsTestData.Radial(256, 256));

        var (_, _, model) = Metrics.RadialModel(img, 128, 128);
        var resid = Img.Divide(img, model);
        var vals = resid.D.ToList();

        double ptp = Stats.Percentile(vals, 99) - Stats.Percentile(vals, 1);
        Assert.True(ptp < 0.005, "residual ptp was " + ptp);
    }

    [Fact]
    public void RadialModel_ProducesAMonotonicallyFallingProfile()
    {
        var img = Make(256, 256, FitsTestData.Radial(256, 256));

        var (centers, prof, _) = Metrics.RadialModel(img, 128, 128);

        for (int i = 1; i < centers.Length; i++)
            Assert.True(centers[i] > centers[i - 1], "radii must increase");
        for (int i = 1; i < prof.Length; i++)
            Assert.True(prof[i] <= prof[i - 1] + 1e-9, "vignetting must not increase outward");
    }

    [Fact]
    public void RadialModel_ReturnsAModelTheSameShapeAsTheInput()
    {
        var img = Make(64, 32, FitsTestData.Radial(64, 32));

        var (_, _, model) = Metrics.RadialModel(img, 32, 16);

        Assert.Equal(img.W, model.W);
        Assert.Equal(img.H, model.H);
    }

    /// <summary>
    /// Radial bins holding fewer than 8 pixels are dropped, so a sparsely sampled radius
    /// never contributes a median built from one or two pixels. Exact counts, because a
    /// loose "fewer than nbins" assertion cannot tell the 8-pixel rule from no rule at
    /// all - the counts below are the ones a numpy reimplementation of the binning gives.
    /// </summary>
    [Theory]
    [InlineData(8, 3)]      // 15 bins are non-empty, but only 3 reach 8 pixels
    [InlineData(16, 20)]    // 39 non-empty
    [InlineData(32, 57)]    // 67 non-empty
    [InlineData(64, 73)]    // 79 non-empty
    public void RadialModel_DropsBinsWithFewerThanEightPixels(int n, int expectedKept)
    {
        var img = Make(n, n, FitsTestData.Radial(n, n));

        var (centers, prof, _) = Metrics.RadialModel(img, n / 2.0, n / 2.0);

        Assert.Equal(expectedKept, centers.Length);
        Assert.Equal(centers.Length, prof.Length);
    }

    [Fact]
    public void RadialModel_KeepsEveryBinOnAWellSampledFrame()
    {
        var img = Make(256, 256, FitsTestData.Radial(256, 256));

        var (centers, _, _) = Metrics.RadialModel(img, 128, 128);

        Assert.Equal(80, centers.Length);
    }

    // ---------------------------------------------------------------- CornerStats

    [Fact]
    public void CornerStats_OnARadialField_SeesAllFourCornersAsEqual()
    {
        // 1024 is the realistic scale: the analysis raster is ~1500x1000 in practice
        var img = Make(1024, 1024, FitsTestData.Radial(1024, 1024));

        var (vals, _) = Metrics.CornerStats(img);

        double mean = vals.Values.Average();
        double spread = (vals.Values.Max() - vals.Values.Min()) / mean * 100;
        Assert.True(spread < 0.35, "corner spread was " + spread + "%");
    }

    /// <summary>
    /// The corner spread never reaches exactly zero, even on a perfectly radial scene,
    /// and it is worth knowing why before reading a real number as signal.
    ///
    /// The analysis centre is (w/2, h/2), but pixel indices run 0..w-1, whose true middle
    /// is (w-1)/2. That half-pixel offset makes the top-left corner sit fractionally
    /// farther from the centre than the bottom-right, so a radial field reads slightly
    /// darker there. The artifact scales as ~1/n: measured 2.88% at 100 px, 1.44% at 200,
    /// 0.56% at 512, 0.28% at 1024 (values cross-checked against a numpy reimplementation).
    ///
    /// At the sizes the tool actually runs it is ~0.2%, far under the 1.5% OK threshold,
    /// so it is harmless - but it is a floor on the metric, not noise, and on a small
    /// enough frame it would swamp the threshold on its own.
    /// </summary>
    [Fact]
    public void CornerStats_HasAHalfPixelCentringArtifact_ThatHalvesAsTheFrameDoubles()
    {
        double Spread(int n)
        {
            var (vals, _) = Metrics.CornerStats(Make(n, n, FitsTestData.Radial(n, n)));
            return (vals.Values.Max() - vals.Values.Min()) / vals.Values.Average() * 100;
        }

        double at256 = Spread(256), at512 = Spread(512), at1024 = Spread(1024);

        Assert.Equal(1.134, at256, 2);
        Assert.Equal(0.564, at512, 2);
        Assert.Equal(0.282, at1024, 2);
        Assert.InRange(at256 / at512, 1.9, 2.1);
        Assert.InRange(at512 / at1024, 1.9, 2.1);
    }

    [Fact]
    public void CornerStats_OnARadialField_SeesTheCentreAsBrighterThanTheCorners()
    {
        var img = Make(200, 200, FitsTestData.Radial(200, 200));

        var (vals, ctr) = Metrics.CornerStats(img);

        Assert.True(ctr > vals.Values.Max());
    }

    [Fact]
    public void CornerStats_OnAHorizontalGradient_SplitsLeftFromRight()
    {
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(200, 200), 200, 0.20);
        var img = Make(200, 200, scene);

        var (vals, _) = Metrics.CornerStats(img);

        // the ramp is horizontal, so right beats left on both rows
        Assert.True(vals["dreapta-sus"] > vals["stanga-sus"]);
        Assert.True(vals["dreapta-jos"] > vals["stanga-jos"]);

        // top and bottom are *not* exactly equal - the half-pixel centring artifact
        // above separates them too - but that split is an order of magnitude smaller
        // than the injected horizontal one, which is what the metric is meant to see.
        double horizontal = vals["dreapta-sus"] - vals["stanga-sus"];
        double vertical = Math.Abs(vals["stanga-sus"] - vals["stanga-jos"]);
        Assert.True(horizontal > 10 * vertical,
            "horizontal split " + horizontal + " should dominate vertical " + vertical);
    }

    [Fact]
    public void CornerStats_OnAConstantImage_ReportsThatConstantEverywhere()
    {
        var img = Make(100, 100, (x, y) => 777);

        var (vals, ctr) = Metrics.CornerStats(img);

        Assert.All(vals.Values, v => Assert.Equal(777, v, 9));
        Assert.Equal(777, ctr, 9);
    }

    [Fact]
    public void CornerStats_NamesTheFourCornersInRomanian()
    {
        var img = Make(100, 100, (x, y) => 1);

        var (vals, _) = Metrics.CornerStats(img);

        Assert.Equal(
            new[] { "dreapta-jos", "dreapta-sus", "stanga-jos", "stanga-sus" },
            vals.Keys.OrderBy(k => k).ToArray());
    }

    // ---------------------------------------------------------------- AzimuthalAsymmetry

    [Fact]
    public void AzimuthalAsymmetry_OnARadialField_IsEssentiallyZero()
    {
        var img = Make(256, 256, FitsTestData.Radial(256, 256));

        var rings = Metrics.AzimuthalAsymmetry(img, 128, 128);

        Assert.NotEmpty(rings);
        Assert.True(rings.Max(r => r.ptp) < 0.5, "max ptp was " + rings.Max(r => r.ptp));
    }

    [Fact]
    public void AzimuthalAsymmetry_FiresOnAGradient()
    {
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(256, 256), 256, 0.20);
        var img = Make(256, 256, scene);

        var rings = Metrics.AzimuthalAsymmetry(img, 128, 128);

        Assert.True(rings.Max(r => r.ptp) > 5, "max ptp was " + rings.Max(r => r.ptp));
    }

    /// <summary>
    /// Rings span 0.15*r_inscribed to r_inscribed, and the reported radius is the ring
    /// midpoint normalised by r_inscribed. Restricted to the inscribed circle because
    /// only there is every azimuth sampled.
    /// </summary>
    [Fact]
    public void AzimuthalAsymmetry_ReportsTenRingsSpanningTheInscribedRadius()
    {
        var img = Make(256, 256, FitsTestData.Radial(256, 256));

        var rings = Metrics.AzimuthalAsymmetry(img, 128, 128);

        Assert.Equal(10, rings.Count);
        Assert.Equal(0.1925, rings.First().r, 4);   // midpoint of [0.15, 0.235]
        Assert.Equal(0.9575, rings.Last().r, 4);    // midpoint of [0.915, 1.0]
    }

    [Fact]
    public void AzimuthalAsymmetry_OnATooSmallImage_ReturnsNothing()
    {
        // rings this thin cannot reach the 8-pixel-per-sector minimum
        var img = Make(8, 8, FitsTestData.Radial(8, 8));

        Assert.Empty(Metrics.AzimuthalAsymmetry(img, 4, 4));
    }

    // ---------------------------------------------------------------- LargeScalePtp

    [Fact]
    public void LargeScalePtp_OnAConstantResidual_IsZero()
    {
        var img = Make(400, 400, (x, y) => 1.0);

        Assert.Equal(0, Metrics.LargeScalePtp(img, 200, 200, 200), 9);
    }

    /// <summary>
    /// The reason the metric averages into tiles at all: noise falls as sqrt(pixels per
    /// tile) while real structure survives. Without the tile step this would measure
    /// noise rather than error.
    /// </summary>
    [Fact]
    public void LargeScalePtp_AveragesPixelNoiseAwayInsteadOfMeasuringIt()
    {
        var rng = new Random(12345);
        var img = Make(400, 400, (x, y) => 1.0 + (rng.NextDouble() - 0.5) * 0.2);

        var perPixel = img.D.ToList();
        double pixelPtp = Stats.Percentile(perPixel, 95) - Stats.Percentile(perPixel, 5);
        double tiled = Metrics.LargeScalePtp(img, 200, 200, 200);

        Assert.True(pixelPtp > 0.15, "per-pixel spread was " + pixelPtp);
        Assert.True(tiled < pixelPtp / 5, "tiled ptp was " + tiled + " vs per-pixel " + pixelPtp);
    }

    [Fact]
    public void LargeScalePtp_WithTooFewTilesInside_ReturnsNaN()
    {
        var img = Make(400, 400, (x, y) => 1.0);

        // a radius this small admits almost no tile centres
        Assert.True(double.IsNaN(Metrics.LargeScalePtp(img, 200, 200, rIns: 1)));
    }

    [Fact]
    public void LargeScalePtp_SeesLargeScaleStructureThatSurvivesTiling()
    {
        // a horizontal ramp of amplitude 0.10 across the frame
        var img = Make(400, 400, (x, y) => 1.0 + 0.10 * (x / 400.0 - 0.5));

        double ptp = Metrics.LargeScalePtp(img, 200, 200, 200);

        Assert.True(ptp > 0.05, "ramp should survive tiling, got " + ptp);
    }

    // ---------------------------------------------------------------- TileBackground

    [Fact]
    public void TileBackground_PlacesTileCentresAtTheirGeometricMiddle()
    {
        var img = Make(200, 200, (x, y) => 100);

        var (bg, bx, by, th, tw, k) = Metrics.TileBackground(img);

        Assert.Equal(5, k);              // max(4, 200/40)
        Assert.Equal(40, tw);
        Assert.Equal(40, th);
        Assert.Equal(bg.Length, tw * th);
        Assert.Equal(2.5, bx[0], 9);     // (0 + 0.5) * 5
        Assert.Equal(2.5, by[0], 9);
        Assert.Equal(7.5, bx[1], 9);     // (1 + 0.5) * 5
    }

    [Fact]
    public void TileBackground_OnAConstantFrame_ReturnsThatConstant()
    {
        var img = Make(200, 200, (x, y) => 42);

        var (bg, _, _, _, _, _) = Metrics.TileBackground(img);

        Assert.All(bg, v => Assert.Equal(42, v, 9));
    }

    /// <summary>
    /// A low percentile inside each tile is what makes the background estimate robust:
    /// stars and nebulosity only ever add signal, so they sit above p15 and are ignored.
    /// </summary>
    [Fact]
    public void TileBackground_IgnoresBrightStarsSittingOnTheBackground()
    {
        // background 100, with one pixel in every 25 blown up to 10000
        var img = Make(200, 200, (x, y) => (x % 5 == 0 && y % 5 == 0) ? 10000 : 100);

        var (bg, _, _, _, _, _) = Metrics.TileBackground(img);

        Assert.All(bg, v => Assert.Equal(100, v, 9));
    }

    [Fact]
    public void TileBackground_EnforcesAMinimumTileSize()
    {
        // 40/40 would give k = 1; the floor of 4 keeps tiles big enough to hold a percentile
        var img = Make(40, 40, (x, y) => 100);

        var (_, _, _, _, _, k) = Metrics.TileBackground(img);

        Assert.Equal(4, k);
    }
}
