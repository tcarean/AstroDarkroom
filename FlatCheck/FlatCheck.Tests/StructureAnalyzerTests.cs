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
/// Phase 6: mode 1, asserted on numbers rather than on console text.
/// </summary>
public class StructureAnalyzerTests
{
    const int N = 1024;   // -> 512x512 after Prepare, the realistic analysis scale

    [Fact]
    public void OnAPerfectlyRadialFlat_EveryTestPasses()
    {
        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, FitsTestData.Radial(N, N)));

        Assert.Equal("PERFECT", r.CornerVerdict);
        Assert.Equal("PERFECT", r.AzimuthalVerdict);
        Assert.Equal("PERFECT", r.ResidualVerdict);
        Assert.Equal("PERFECT", r.TiltVerdict);
        Assert.Equal("PERFECT", r.Verdict);
    }

    [Fact]
    public void OnAPerfectlyRadialFlat_TheIlluminationPeakSitsOnTheSensorCentre()
    {
        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, FitsTestData.Radial(N, N)));

        Assert.True(r.CentroidValid);
        Assert.True(r.CentroidOffsetPct < 1, "centroid offset was " + r.CentroidOffsetPct);
        Assert.Equal(255.5, r.SensorCx, 6);
        Assert.Equal(255.5, r.SensorCy, 6);
    }

    /// <summary>
    /// The regression that motivated 1.2.0, at mode level. A lens with almost no
    /// vignetting plus a real gradient used to come back with the centroid offset either
    /// wildly inflated or - once the vertex left the frame - reported as 0.00% OK. The
    /// tilt reads the gradient itself, so the mode fires on the defect, not on how much
    /// vignetting happens to be there to divide by.
    /// </summary>
    [Fact]
    public void OnAFlatLensWithAGradient_TheTiltCarriesTheVerdict()
    {
        var scene = FitsTestData.WithLinearGradient(
            FitsTestData.Radial(N, N, 30000, 0.02), N, 0.30);

        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, scene));

        Assert.False(r.CentroidValid);                 // the old metric has nothing to say
        Assert.Equal(0.0, r.CentroidOffsetPct, 6);     // and would have read as "centred"
        Assert.InRange(r.TiltPct, 29.0, 31.5);         // the injected 30%
        Assert.Equal("REFA", r.TiltVerdict);
        Assert.Equal("REFA", r.Verdict);
    }

    /// <summary>
    /// A horizontal ramp of amplitude g puts the two corner boxes at roughly x/w = 0.025
    /// and 0.975, so the injected spread is about 0.95*g -> ~11.4% for g = 0.12. Anything
    /// in that neighbourhood means the metric is reading the gradient, not something else.
    /// </summary>
    [Fact]
    public void OnALinearGradient_TheCornerSpreadMatchesTheInjectedAmplitude()
    {
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(N, N), N, 0.12);

        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, scene));

        Assert.InRange(r.CornerSpreadPct, 11.0, 12.5);
        Assert.Equal("ACCEPTABIL", r.CornerVerdict);   // the 10-20% band
        Assert.Equal("ACCEPTABIL", r.Verdict);
    }

    [Fact]
    public void OnALinearGradient_TheAzimuthalAndResidualTestsFireToo()
    {
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(N, N), N, 0.12);

        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, scene));

        // both are well past the "perfect" band, which is what "they fire too" means
        Assert.True(r.AzimuthalMaxPct > Th.ResidPerfect, "azimuthal was " + r.AzimuthalMaxPct);
        Assert.True(r.ResidualPtpPct > Th.ResidPerfect, "residual was " + r.ResidualPtpPct);
        Assert.NotEqual("PERFECT", r.AzimuthalVerdict);
        Assert.NotEqual("PERFECT", r.ResidualVerdict);
    }

    /// <summary>The overall verdict is the worst of the five, not an average.</summary>
    [Fact]
    public void R011_TheOverallVerdictIsTheWorstOfTheFiveTests()
    {
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(N, N), N, 0.12);
        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, scene));

        var all = new[] { r.CornerVerdict, r.AzimuthalVerdict, r.ResidualVerdict, r.TiltVerdict, r.MaxTileVerdict };
        Assert.Equal(all.Max(Stats.Severity), Stats.Severity(r.Verdict));
    }

    // ------------------------------------------------- the largest spot and the local spots

    /// <summary>
    /// A compact +10 % blotch in a corner, the size of a fingertip on a 24 MP frame.
    /// </summary>
    static Func<int, int, double> CornerBlotch(Func<int, int, double> b, int w, int h, double amp) =>
        (x, y) =>
        {
            double dx = x - 0.1 * w, dy = y - 0.1 * h, s = 0.05 * w;
            return b(x, y) * (1 + amp * Math.Exp(-(dx * dx + dy * dy) / (2 * s * s)));
        };

    /// <summary>
    /// A dust shadow: a disc of radius <paramref name="radius"/> px, <paramref name="depth"/>
    /// darker.
    /// </summary>
    static Func<int, int, double> DustShadow(Func<int, int, double> b, double px, double py, double radius, double depth) =>
        (x, y) =>
        {
            double dx = x - px, dy = y - py;
            return b(x, y) * (dx * dx + dy * dy < radius * radius ? 1 - depth : 1);
        };

    /// <summary>
    /// THE false negative that motivated the fifth test. The residual trims 5 % of the
    /// tiles at each end, so a compact blotch vanished from it and the mode said PERFECT
    /// with a +10 % blotch plainly visible in the map. Measured before the change: 0.04 %
    /// residual, PERFECT. The worst tile reads it, and it carries the verdict.
    /// </summary>
    [Fact]
    public void ACompactBlotchInACorner_IsNoLongerReportedAsPerfect()
    {
        const int W = 1536, H = 1024;   // 3:2 like a real sensor -> 768x512
        var scene = CornerBlotch(FitsTestData.Radial(W, H), W, H, 0.10);

        var r = StructureAnalyzer.Analyze(TestImages.From(W, H, scene));

        Assert.True(r.ResidualPtpPct < Th.ResidPerfect, "the trimmed residual still misses it: " + r.ResidualPtpPct);
        Assert.InRange(r.MaxTilePct, 7.0, 10.5);       // most of the injected 10 %
        Assert.Equal("BUN", r.MaxTileVerdict);
        Assert.NotEqual("PERFECT", r.Verdict);
    }

    /// <summary>
    /// The dust case, stated as the user stated it: a compact shadow is correct for the
    /// session whose lights carry the same shadow and wrong for a library flat. So it is
    /// flagged, with a position, and it is not turned into a verdict of its own - the
    /// verdict comes from the worst tile like any other deviation.
    /// </summary>
    [Fact]
    public void ADustShadow_RaisesTheLocalSpotWarningAndSaysWhere()
    {
        const int W = 1536, H = 1024;
        // 5 % deep, radius 30 px on the sensor -> ~15 px on the 768x512 raster, at (0.3w, 0.3h)
        var scene = DustShadow(FitsTestData.Radial(W, H), 0.3 * W, 0.3 * H, 30, 0.05);

        var r = StructureAnalyzer.Analyze(TestImages.From(W, H, scene));

        Assert.True(r.LocalSpotFlagged);
        Assert.InRange(r.LocalSpotPct, -5.5, -3.0);   // negative: a shadow
        Assert.InRange(r.LocalSpotX, 0.3 * 768 - 12, 0.3 * 768 + 12);
        Assert.InRange(r.LocalSpotY, 0.3 * 512 - 12, 0.3 * 512 + 12);
        Assert.NotNull(StructureReport.LocalSpotWarning(r));
    }

    /// <summary>
    /// An asymmetric scene (vignetting, a gradient along x and a weaker one along y, a dust
    /// shadow off centre, deterministic noise) read the right way up, upside down and mirrored
    /// left to right gives the same six figures (R-131): the geometry is built about the
    /// frame's centre, so the orientation a file is read in cannot move a figure. Two sizes:
    /// 1536 x 1024 -> 768 x 512, even leftovers for the 40-tile grid (tiles of 19) and an odd one
    /// for the 80-tile grid of the local spot (tiles of 9: 768 = 85 x 9 + 3); 1580 x 1060 -> 790
    /// x 530, odd leftovers both ways for the 40-tile grid (790 = 41 x 19 + 11, 530 = 27 x 19 +
    /// 17).
    /// </summary>
    [Theory]
    [InlineData(1536, 1024)]
    [InlineData(1580, 1060)]
    public void R131_TheFiguresAreTheSame_WhenTheRasterIsMirrored(int W, int H)
    {
        var shadow = DustShadow(FitsTestData.Radial(W, H), 0.3 * W, 0.3 * H, 30, 0.05);
        Func<int, int, double> scene = (x, y) => shadow(x, y) * (1 + 0.06 * x / W) * (1 + 0.02 * y / H)
            * (1 + 0.004 * (((x * 73856093) ^ (y * 19349663)) % 1000) / 1000.0);
        Img img = TestImages.From(W, H, scene);
        Img rows = TestImages.From(W, H, (x, y) => scene(x, H - 1 - y));
        Img cols = TestImages.From(W, H, (x, y) => scene(W - 1 - x, y));

        var a = StructureAnalyzer.Analyze(img);
        var r = StructureAnalyzer.Analyze(rows);
        var c = StructureAnalyzer.Analyze(cols);

        foreach (var m in new[] { r, c })
        {
            var pairs = new (string name, double a, double b)[]
            {
                ("corners", a.CornerSpreadPct, m.CornerSpreadPct), ("azimuthal", a.AzimuthalMaxPct, m.AzimuthalMaxPct),
                ("residual", a.ResidualPtpPct, m.ResidualPtpPct), ("tilt", a.TiltPct, m.TiltPct),
                ("maxTile", a.MaxTilePct, m.MaxTilePct), ("localSpot", a.LocalSpotPct, m.LocalSpotPct),
                ("stops", a.VignetteStops, m.VignetteStops),
            };
            string report = string.Join("; ", pairs.Select(p => $"{p.name} {p.a:0.000000000} vs {p.b:0.000000000}"));
            Assert.True(pairs.All(p => Math.Abs(p.a - p.b) < 1e-9), report);
        }
        // the spot's position mirrors with the frame: tile centres rounded to a pixel, so within one
        Assert.InRange(r.LocalSpotY + a.LocalSpotY, a.Height - 2, a.Height);
        Assert.Equal(a.LocalSpotX, r.LocalSpotX);
        Assert.InRange(c.LocalSpotX + a.LocalSpotX, a.Width - 2, a.Width);
        Assert.Equal(a.LocalSpotY, c.LocalSpotY);
    }

    /// <summary>
    /// The same shadow drawn in a FITS, in the file's upper rows, is reported in the lower
    /// half of the frame, and a file whose last rows are the brightest has its brightest
    /// corners at the top: the raster is the frame as Siril shows it, the file's first row at
    /// the bottom (R-130, ADR 0094). This is the one test that ties the reader's row order to
    /// what the user reads: a coordinate and a corner name.
    /// </summary>
    [Fact]
    public void R130_AShadowInTheFilesFirstRows_IsReportedInTheLowerHalf_AndTheLastRowsAreTheTopCorners()
    {
        const int W = 1536, H = 1024;
        var shadow = DustShadow(FitsTestData.Radial(W, H), 0.3 * W, 0.3 * H, 30, 0.05);
        Func<int, int, double> brighterDown = (x, y) => shadow(x, y) * (1 + 0.1 * y / H);
        var r = StructureAnalyzer.Analyze(FitsReader.Load(FitsTestData.BuildFits(W, H, brighterDown, bottomUp: false)));

        Assert.True(r.LocalSpotFlagged);
        Assert.InRange(r.LocalSpotX, 0.3 * 768 - 12, 0.3 * 768 + 12);
        Assert.InRange(r.LocalSpotY, 0.7 * 512 - 12, 0.7 * 512 + 12);
        Assert.True(r.Corners["stanga-sus"] > r.Corners["stanga-jos"], "the file's last rows are not at the top");
        Assert.True(r.Corners["dreapta-sus"] > r.Corners["dreapta-jos"]);
    }

    /// <summary>
    /// And it still finds the shadow under a gradient: the gradient goes into the other
    /// four tests, the shadow into this one. Measured on 24 MP: 4.71 % with a 12 % ramp
    /// and 3 % noise on top, against 4.99 % on its own.
    /// </summary>
    [Fact]
    public void ADustShadowUnderAGradient_IsStillFlagged()
    {
        const int W = 1536, H = 1024;
        var scene = FitsTestData.WithLinearGradient(
            DustShadow(FitsTestData.Radial(W, H), 0.3 * W, 0.3 * H, 30, 0.05), W, 0.12);

        var r = StructureAnalyzer.Analyze(TestImages.From(W, H, scene));

        Assert.True(r.LocalSpotFlagged);
        Assert.InRange(r.LocalSpotPct, -5.5, -3.0);
        Assert.Equal("ACCEPTABIL", r.Verdict);          // the gradient, not the dust
    }

    [Fact]
    public void ACleanFieldRaisesNoLocalSpotWarning()
    {
        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, FitsTestData.Radial(N, N)));

        Assert.False(r.LocalSpotFlagged);
        Assert.True(Math.Abs(r.LocalSpotPct) < 0.5, "local spot floor was " + r.LocalSpotPct);
        Assert.Equal("PERFECT", r.MaxTileVerdict);
        Assert.Null(StructureReport.LocalSpotWarning(r));
    }

    /// <summary>
    /// A gradient is not a spot: the warning must not fire on the commonest defect.
    /// </summary>
    [Fact]
    public void AGradientRaisesNoLocalSpotWarning()
    {
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(N, N), N, 0.25);

        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, scene));

        Assert.False(r.LocalSpotFlagged);
        Assert.Equal("REFA", r.Verdict);
    }

    /// <summary>
    /// The residual now covers the whole frame, so on a 3:2 frame it reads the same
    /// gradient whichever way it runs, and close to the tilt. Restricted to the
    /// inscribed circle it read 6.5 % along the long axis against 9.7 % along the
    /// short one, for a tilt of 12 %.
    /// </summary>
    [Fact]
    public void OnAThreeByTwoFrame_TheResidualReadsAGradientTheSameAlongEitherAxis()
    {
        const int W = 1536, H = 1024;
        var radial = FitsTestData.Radial(W, H);
        var alongX = FitsTestData.WithLinearGradient(radial, W, 0.12);
        Func<int, int, double> alongY = (x, y) => radial(x, y) * (1.0 + 0.12 * (y / (double)H - 0.5));

        var rx = StructureAnalyzer.Analyze(TestImages.From(W, H, alongX));
        var ry = StructureAnalyzer.Analyze(TestImages.From(W, H, alongY));

        Assert.InRange(rx.ResidualPtpPct / ry.ResidualPtpPct, 0.9, 1.1);
        Assert.True(rx.ResidualPtpPct > 0.8 * rx.TiltPct,
            $"residual {rx.ResidualPtpPct:F2} should read most of the tilt {rx.TiltPct:F2}");
    }

    [Fact]
    public void ReportsTheAnalysisRasterNotTheInputRaster()
    {
        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, FitsTestData.Radial(N, N)));

        Assert.Equal(512, r.Width);    // 1024 -> 512 via the 2x2 block mean
        Assert.Equal(512, r.Height);
        Assert.Equal(512, r.Residual.W);
        Assert.Equal(512, r.Residual.H);
    }

    [Fact]
    public void ReportsMedianAndMaxFromTheFullResolutionInput()
    {
        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, FitsTestData.Radial(N, N, center: 30000)));

        Assert.Equal(30000, r.RawMax, 0);          // the centre pixels, half a pixel from the geometric centre
        Assert.True(r.RawMedian < 30000);          // dragged down by the vignetted corners
    }

    [Fact]
    public void VignettingIsReportedInStopsAndIsInformativeOnly()
    {
        // corner sits at (1 - 0.45) of centre -> log2(1/0.55) = 0.86 stops at the extreme,
        // less at the 5%-inset corner boxes the metric actually samples
        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, FitsTestData.Radial(N, N, k: 0.45)));

        Assert.InRange(r.VignetteStops, 0.6, 0.9);
        Assert.True(r.CentreMedian > r.CornerMean);
    }

    [Fact]
    public void TheResidualIsCentredOnOne_SoTheHeatMapScaleIsMeaningful()
    {
        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, FitsTestData.Radial(N, N)));

        Assert.Equal(1.0, Stats.Percentile(r.Residual.D.ToList(), 50), 3);
    }

    /// <summary>
    /// The map must not invent rings. On a frame carrying nothing but smooth vignetting
    /// and a linear gradient, the residual used to show concentric rings outside the
    /// inscribed circle plus a hard circular edge on it - see <see cref="Img.Symmetrise"/>
    /// for the mechanism. Someone reading that map would go looking for a round
    /// obstruction that was never there, which is exactly what happened.
    ///
    /// Read as ring MEANS: the mean over each ring collapses the gradient (the two halves
    /// cancel), so what is left is the radial behaviour of the model, and it has to be
    /// smooth from one ring to the next. Rings are the 80 profile bins of the radial model,
    /// centre to corner, and only bins holding at least 5000 pixels are used (ADR 0018): the
    /// innermost bins and the last slivers before the corner tip are a few arcs whose mean is
    /// sampling noise. Same definition as the whole-library table of
    /// `radial-model-even-term.md` §7.3, which it reproduces.
    ///
    /// Measured on this scene: 3.08 % worst step from the raw profile, 0.08 % from the
    /// symmetrised one (2026-09-27, with the 80-bin definition; 2.47 % and 0.11 % with the
    /// earlier 2 %-of-radius steps). Both are asserted, so the test states what it
    /// protects against.
    /// </summary>
    [Fact]
    public void OnAGradient_TheResidualHasNoRingsOutToTheCorner()
    {
        var img = Img.Prepare(TestImages.From(2400, 1600,
            FitsTestData.WithLinearGradient(FitsTestData.Radial(2400, 1600), 2400, 0.12)));
        double cx = img.W / 2.0, cy = img.H / 2.0;

        var (_, _, rawModel) = Metrics.RadialModel(img, cx, cy);
        var fixedResidual = StructureAnalyzer.Analyze(TestImages.From(2400, 1600,
            FitsTestData.WithLinearGradient(FitsTestData.Radial(2400, 1600), 2400, 0.12))).Residual;

        double worstRaw = WorstRingStep(Img.Divide(img, rawModel));
        double worstFixed = WorstRingStep(fixedResidual);

        Assert.True(worstRaw > 1.0,
            $"profilul brut ar trebui sa lase inele, dar cel mai mare salt a fost {worstRaw:F3} %");
        Assert.True(worstFixed < 0.5,
            $"harta nu ar trebui sa aiba inele, dar cel mai mare salt a fost {worstFixed:F3} %");
    }

    /// <summary>
    /// The third face of the same artefact, end to end (2026-09-23): a four-fold term - the
    /// only non-radial family a master rotated through four orientations keeps - must leave
    /// a map without rings, and the residual must read the injected structure rather than
    /// the model's error. With the 2-theta-only ring fit this scene read 6.97 % residual
    /// and 2.6 % ring steps for 3.05 % of injected structure; now 3.04 % and 0.14 %.
    /// </summary>
    [Fact]
    public void OnAFourfoldTerm_TheResidualHasNoRingsAndReadsTheInjectedStructure()
    {
        var r = StructureAnalyzer.Analyze(TestImages.From(2400, 1600,
            FitsTestData.WithFourfold(FitsTestData.Radial(2400, 1600), 2400, 1600, 0.03)));

        double worst = WorstRingStep(r.Residual);

        Assert.True(worst < 0.5,
            $"harta nu ar trebui sa aiba inele, dar cel mai mare salt a fost {worst:F3} %");
        Assert.InRange(r.ResidualPtpPct, 2.5, 3.6);   // the injected 3 %, not the model's error
    }

    /// <summary>
    /// The fourth face, end to end (2026-11-06): a 2-theta + 4-theta mix with the Rosette
    /// master's amplitudes must leave a map without a circle on the inscribed radius, and the
    /// residual must read the injected structure. Measured on this scene: ring step 0.463 %
    /// and residual 3.60 % with the plain median on the whole rings, 0.232 % and 3.31 % with
    /// the correction everywhere (the worst tile 2.90 % both ways). The step passed the bound
    /// before the fix too, so this is the guard; the regression test proper is the
    /// model-against-truth one in MetricsTests, whose error this scene drove from 0.51 to 0.04 %.
    /// </summary>
    [Fact]
    public void OnASaddleWithAFourfoldTerm_TheResidualHasNoRingsAndReadsTheInjectedStructure()
    {
        var r = StructureAnalyzer.Analyze(TestImages.From(2400, 1600,
            FitsTestData.WithEvenHarmonics(FitsTestData.Radial(2400, 1600), 2400, 1600, 0.046, -0.019)));

        double worst = WorstRingStep(r.Residual);

        Assert.True(worst < 0.5,
            $"harta nu ar trebui sa aiba inele, dar cel mai mare salt a fost {worst:F3} %");
        Assert.InRange(r.ResidualPtpPct, 3.0, 5.0);   // the injected mix, not the model's error
    }

    /// <summary>The largest jump between the means of two consecutive rings, in
    /// percent.</summary>
    static double WorstRingStep(Img resid)
    {
        double cx = resid.W / 2.0, cy = resid.H / 2.0;
        double rMax = Math.Sqrt(cx * cx + cy * cy);
        const int bins = 80, minPixels = 5000;
        var sum = new double[bins];
        var count = new int[bins];
        for (int y = 0; y < resid.H; y++)
            for (int x = 0; x < resid.W; x++)
            {
                double d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                int b = Math.Min(bins - 1, (int)(d / rMax * bins));
                sum[b] += resid.At(y, x);
                count[b]++;
            }

        double worst = 0, prev = double.NaN;
        int rings = 0;
        for (int b = 0; b < bins; b++)
        {
            if (count[b] < minPixels) continue;
            double mean = sum[b] / count[b];
            if (!double.IsNaN(prev)) worst = Math.Max(worst, Math.Abs(mean - prev) * 100);
            prev = mean;
            rings++;
        }

        Assert.True(rings > 40, $"prea putine inele masurate: {rings}");
        return worst;
    }
}
