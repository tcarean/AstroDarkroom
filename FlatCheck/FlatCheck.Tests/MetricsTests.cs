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

        var (cx, cy, _, _) = Metrics.FitCenter(img);

        Assert.Equal(64, cx, 1);
        Assert.Equal(64, cy, 1);
    }

    [Fact]
    public void FitCenter_RecoversAnOffCentrePeak()
    {
        var img = LogParaboloid(128, 128, px: 80, py: 40);

        var (cx, cy, _, _) = Metrics.FitCenter(img);

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

        var (cx, cy, _, _) = Metrics.FitCenter(img);

        Assert.Equal(32, cx, 6);
        Assert.Equal(16, cy, 6);
    }

    [Fact]
    public void FitCenter_WhenTheVertexLandsOutsideTheFrame_FallsBackToTheGeometricCentre()
    {
        // peak far off to the right: the fit is fine, the answer is just not in frame
        var img = LogParaboloid(128, 128, px: 500, py: 64);

        var (cx, cy, _, _) = Metrics.FitCenter(img);

        Assert.Equal(64, cx, 6);
        Assert.Equal(64, cy, 6);
    }

    // ------------------------------------------------------------ FitCenter: inclinare
    //
    // The tilt is the reason the vertex offset stopped being a test in 1.2.0. The vertex
    // is ~ -b/(2d), a gradient-over-curvature ratio, so it is not scale-invariant: the
    // same defect reads wildly differently depending on how much vignetting the lens has,
    // and past the point where the vertex leaves the frame it collapses to 0 = "clean".
    // The tilt is the numerator alone - a fraction, like every other metric in the tool.

    /// <summary>
    /// The whole point of the change: the same injected gradient must read the same
    /// whatever the vignetting depth. The old offset metric spanned 2.4% to 57% over
    /// roughly this range before falling back to 0.
    /// </summary>
    [Theory]
    [InlineData(0.05)]   // ~0.07 stops - a Samyang 135 at f/2.8 is about this flat
    [InlineData(0.15)]
    [InlineData(0.45)]   // the default fixture, ~0.86 stops
    [InlineData(0.60)]
    public void FitCenter_TiltIsInvariantToTheVignettingDepth(double k)
    {
        var scene = FitsTestData.WithLinearGradient(
            FitsTestData.Radial(256, 256, 30000, k), 256, 0.12);

        var (_, _, tilt, _) = Metrics.FitCenter(Make(256, 256, scene));

        Assert.InRange(tilt, 11.5, 12.5);
    }

    /// <summary>
    /// The tilt reads back the amplitude that was put in, within ~1%. It runs slightly
    /// high (by 0.7% at 24%, by 2.2% at 2%) because the ramp is injected multiplicatively
    /// on a field that is not exactly log-quadratic, so a sliver of it lands in the
    /// higher-order terms; the band is relative so one tolerance covers the whole range.
    /// </summary>
    [Theory]
    [InlineData(0.02, 2.0)]
    [InlineData(0.06, 6.0)]
    [InlineData(0.12, 12.0)]
    [InlineData(0.24, 24.0)]
    public void FitCenter_TiltRecoversTheInjectedGradientAmplitude(double g, double expected)
    {
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(256, 256), 256, g);

        var (_, _, tilt, _) = Metrics.FitCenter(Make(256, 256, scene));

        Assert.InRange(tilt, expected * 0.97, expected * 1.03);
    }

    /// <summary>
    /// The bug this change exists to fix. On a nearly vignetting-free field a strong
    /// gradient pushes the paraboloid vertex clean out of the frame, and the out-of-frame
    /// guard then falls back to the sensor centre - so the *worse* the defect got, the
    /// cleaner the old offset metric read. The tilt still measures it.
    /// </summary>
    [Fact]
    public void FitCenter_WhenAFlatFieldMakesTheVertexEscape_TheTiltStillReadsTheGradient()
    {
        var scene = FitsTestData.WithLinearGradient(
            FitsTestData.Radial(256, 256, 30000, 0.02), 256, 0.30);
        var img = Make(256, 256, scene);

        var (cx, cy, tilt, valid) = Metrics.FitCenter(img);

        // the vertex is unusable, and says so instead of reporting a centred peak
        Assert.False(valid);
        Assert.Equal(128, cx, 6);
        Assert.Equal(128, cy, 6);
        // ...but the defect is still measured, and lands in the worst band
        Assert.InRange(tilt, 29.0, 31.5);
        Assert.Equal("REFA", Stats.ResidualVerdict(tilt));
    }

    /// <summary>A genuinely decentred radial field still fires: the tilt is not only a
    /// gradient detector, it catches decentring too, weighted by the curvature - which is
    /// exactly what it costs photometrically.</summary>
    [Fact]
    public void FitCenter_OnADecentredRadialField_ReportsATilt()
    {
        var img = LogParaboloid(256, 256, px: 160, py: 128, sigma: 200);

        var (cx, _, tilt, valid) = Metrics.FitCenter(img);

        Assert.True(valid);
        Assert.Equal(160, cx, 1);
        Assert.True(tilt > Th.ResidPerfect, "tilt was " + tilt);
    }

    [Fact]
    public void FitCenter_OnAPerfectlyRadialField_ReportsNoTilt()
    {
        var img = Make(256, 256, FitsTestData.Radial(256, 256));

        var (_, _, tilt, _) = Metrics.FitCenter(img);

        Assert.True(tilt < Th.ResidPerfect, "tilt was " + tilt);
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

    // ------------------------------------------------- profilul radial nu sare

    /// <summary>
    /// Max relative deviation of a profile from its own 9-point Savitzky-Golay
    /// smoothing, in percent. A profile that steps from one bin to the next scores high;
    /// a smooth one scores its own discretisation floor.
    ///
    /// Coefficients are the exact rationals for window 9, degree 2, so there is no fit to
    /// disagree about.
    /// </summary>
    static double ProfileRoughnessPct(double[] prof)
    {
        double[] sg = { -21, 14, 39, 54, 59, 54, 39, 14, -21 };
        const double norm = 231.0;
        if (prof.Length < 9) return double.NaN;

        double worst = 0;
        for (int i = 4; i < prof.Length - 4; i++)
        {
            double smooth = 0;
            for (int j = 0; j < 9; j++) smooth += sg[j] * prof[i - 4 + j];
            smooth /= norm;
            worst = Math.Max(worst, Math.Abs(prof[i] - smooth) / smooth * 100);
        }
        return worst;
    }

    /// <summary>
    /// THE REGRESSION TEST for the ring artefact.
    ///
    /// Beyond the inscribed circle a constant-radius ring is only partial arcs. On a
    /// frame with a gradient those arcs hold a bimodal distribution - one end dark, the
    /// other bright - and the median lands in the sparse gap between the two modes, so it
    /// jumps from one bin to the next by an amount set by the gradient. The interpolated
    /// model inherited the jumps and `img / model` drew them as concentric rings that
    /// were not in the flat at all: reproduced on a synthetic frame that is nothing but
    /// smooth vignetting times an 11 % ramp.
    ///
    /// Symmetrising kills the odd part - the whole gradient - before the profile is
    /// taken. Both halves are asserted, so the test says what it protects against and
    /// fails if someone drops the symmetrisation as redundant.
    /// </summary>
    /// <summary>
    /// The scene is deliberately the size of a real analysis raster (2400x1600 prepares
    /// to 1200x800) rather than the usual 512 fixture. At 256x256 the profile's own
    /// discretisation floor is ~0.17 %, the same order as the artefact, so the fixture
    /// sizes used elsewhere would only show a factor of two. At analysis size the
    /// artefact is 77x the floor, which is what the real masters showed.
    /// </summary>
    [Fact]
    public void RadialModel_OnAGradient_TheProfileJumpsUnlessTheRasterIsSymmetrised()
    {
        var tilted = Img.Prepare(Make(2400, 1600,
            FitsTestData.WithLinearGradient(FitsTestData.Radial(2400, 1600), 2400, 0.12)));
        double cx = tilted.W / 2.0, cy = tilted.H / 2.0;

        var (_, rawProf, _) = Metrics.RadialModel(tilted, cx, cy);
        var (_, symProf, _) = Metrics.RadialModel(Img.Symmetrise(tilted), cx, cy);

        double raw = ProfileRoughnessPct(rawProf), sym = ProfileRoughnessPct(symProf);

        // masurat: 1.54 % brut, 0.02 % simetrizat
        Assert.True(raw > 1.0, $"profilul brut ar trebui sa sara, dar a dat {raw:F3} %");
        Assert.True(sym < 0.1, $"profilul simetrizat ar trebui neted, dar a dat {sym:F3} %");
    }

    /// <summary>
    /// Si celalalt capat: pe un camp radial simetrizarea nu are ce sa scoata, deci
    /// profilul nu se muta mai mult decat artefactul de o jumatate de pixel al centrarii
    /// (problema cunoscuta 5) - masurat 0.20 % pe 256x256, si scade ca ~1/n.
    /// </summary>
    [Fact]
    public void RadialModel_OnARadialField_SymmetrisingBarelyMovesTheProfile()
    {
        var radial = Img.Prepare(Make(512, 512, FitsTestData.Radial(512, 512)));
        double cx = radial.W / 2.0, cy = radial.H / 2.0;

        var (_, plain, _) = Metrics.RadialModel(radial, cx, cy);
        var (_, sym, _) = Metrics.RadialModel(Img.Symmetrise(radial), cx, cy);

        Assert.Equal(plain.Length, sym.Length);
        double scale = plain.Average();
        for (int i = 0; i < plain.Length; i++)
            Assert.True(Math.Abs(plain[i] - sym[i]) / scale < 0.005,
                $"binul {i} s-a mutat cu {Math.Abs(plain[i] - sym[i]) / scale * 100:F3} %");
    }
}
