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
/// Phase 6: mode 3, the only mode that closes the loop.
///
/// The sign convention is the most valuable thing in this file. radPct is
/// -(d+e)/a*100, so:
///   positive -> background FALLS toward the edges -> UNDER-correction
///   negative -> background RISES toward the edges -> OVER-correction
/// Getting that backwards would send someone chasing the opposite fault, and nothing
/// else in the suite would notice.
/// </summary>
public class LightAnalyzerTests
{
    const int N = 512;

    /// <summary>
    /// A light with residual vignetting, a sky gradient and a nebulous blob gives the same radial
    /// and plane figures read the right way up, upside down and mirrored left to right (R-131):
    /// the background tiles are laid about the frame's centre and the fit is about it too. Two
    /// sizes; the analyzer prepares the frame first, so the tiles are laid on 256 x 256 (tiles
    /// of 6: 256 = 42 x 6 + 4, an even leftover) and on 263 x 259 (263 = 43 x 6 + 5, 259 = 43 x 6
    /// + 1, odd leftovers both ways, on an odd raster).
    /// </summary>
    [Theory]
    [InlineData(512, 512)]
    [InlineData(526, 518)]
    public void R131_TheLightFiguresAreTheSame_WhenTheRasterIsMirrored(int W, int H)
    {
        var radial = FitsTestData.Radial(W, H, k: 0.1);
        Func<int, int, double> scene = (x, y) =>
        {
            double dx = x - 0.62 * W, dy = y - 0.4 * H;
            double blob = 3000 * Math.Exp(-(dx * dx + dy * dy) / (2 * 0.06 * W * 0.06 * W));
            return radial(x, y) * (1 + 0.05 * x / W) + blob + 0.5 * (((x * 73856093) ^ (y * 19349663)) % 1000);
        };
        var a = LightAnalyzer.Analyze(TestImages.From(W, H, scene));
        var r = LightAnalyzer.Analyze(TestImages.From(W, H, (x, y) => scene(x, H - 1 - y)));
        var c = LightAnalyzer.Analyze(TestImages.From(W, H, (x, y) => scene(W - 1 - x, y)));

        Assert.Equal(a.RadPct, r.RadPct, 9);
        Assert.Equal(a.PlanePct, r.PlanePct, 9);
        Assert.Equal(a.RadPct, c.RadPct, 9);
        Assert.Equal(a.PlanePct, c.PlanePct, 9);
    }

    [Fact]
    public void BackgroundFallingTowardTheEdges_ReadsAsUnderCorrection()
    {
        // an uncorrected light: vignetting still present, so the background droops at the edges
        var img = TestImages.From(N, N, FitsTestData.Radial(N, N));

        var r = LightAnalyzer.Analyze(img);

        Assert.True(r.RadPct > 0, "radPct was " + r.RadPct);
        Assert.Equal("PROBLEMA", r.Verdict);
    }

    [Fact]
    public void BackgroundRisingTowardTheEdges_ReadsAsOverCorrection()
    {
        // divided by a flat that still contained an offset: the edges are lifted
        var img = TestImages.From(N, N, TestImages.InvertedRadial(N, N));

        var r = LightAnalyzer.Analyze(img);

        Assert.True(r.RadPct < 0, "radPct was " + r.RadPct);
        Assert.Equal("PROBLEMA", r.Verdict);
    }

    [Fact]
    public void AFlatBackground_HasNoRadialComponent()
    {
        var img = TestImages.From(N, N, (x, y) => 1000);

        var r = LightAnalyzer.Analyze(img);

        Assert.True(Math.Abs(r.RadPct) < Th.LightRadOk, "radPct was " + r.RadPct);
        Assert.Equal("OK", r.Verdict);
    }

    /// <summary>
    /// The claim that makes this test usable under a polluted sky: a linear sky gradient
    /// and residual vignetting have different shapes, so fitting a plane and the radial
    /// terms simultaneously separates them. A pure plane must therefore land almost
    /// entirely in planePct, leaving radPct near zero.
    ///
    /// This is the single thing most likely to regress silently - substituting
    /// percentiles on circular annuli for the plane fit would break it and still look
    /// plausible.
    /// </summary>
    [Fact]
    public void APureSkyGradient_LandsInThePlaneAndNotInTheRadialTerm()
    {
        var img = TestImages.From(N, N, (x, y) => 1000 * (1.0 + 0.30 * (x / (double)N - 0.5)));

        var r = LightAnalyzer.Analyze(img);

        Assert.True(r.PlanePct > 5, "plane was " + r.PlanePct);
        Assert.True(Math.Abs(r.RadPct) < Th.LightRadOk,
            "a pure plane leaked " + r.RadPct + "% into the radial term");
        Assert.Equal("OK", r.Verdict);
    }

    [Fact]
    public void ASkyGradientOnTopOfVignetting_StillRecoversTheVignetting()
    {
        var plain = TestImages.From(N, N, FitsTestData.Radial(N, N));
        var withSky = TestImages.From(N, N,
            (x, y) => FitsTestData.Radial(N, N)(x, y) * (1.0 + 0.30 * (x / (double)N - 0.5)));

        double clean = LightAnalyzer.Analyze(plain).RadPct;
        var r = LightAnalyzer.Analyze(withSky);

        Assert.True(r.PlanePct > 5, "plane was " + r.PlanePct);
        // the radial term should survive the added gradient largely intact
        Assert.InRange(r.RadPct, clean * 0.7, clean * 1.3);
    }

    [Fact]
    public void SigmaClippingRejectsNebulosity()
    {
        // a bright patch over one quadrant, as nebulosity would be
        var img = TestImages.From(N, N, (x, y) =>
            1000 + (x > N * 0.6 && y > N * 0.6 ? 4000 : 0));

        var r = LightAnalyzer.Analyze(img);

        Assert.True(r.KeptTiles < r.TotalTiles, "nothing was clipped");
        Assert.True(r.KeptTiles > r.TotalTiles / 2, "clipped too much: " + r.KeptTiles + "/" + r.TotalTiles);
    }

    [Fact]
    public void ClippingIsOneSided_SoDarkTilesAreNeverRejected()
    {
        // contaminants only ever ADD signal, so the clip is res < thr and a dark patch
        // must stay in the fit
        var img = TestImages.From(N, N, (x, y) =>
            1000 - (x > N * 0.6 && y > N * 0.6 ? 400 : 0));

        var r = LightAnalyzer.Analyze(img);

        Assert.Equal(r.TotalTiles, r.KeptTiles);
    }

    [Fact]
    public void TheRadialMapIsTileShapedAndCentredOnOne()
    {
        var img = TestImages.From(N, N, FitsTestData.Radial(N, N));

        var r = LightAnalyzer.Analyze(img);

        Assert.Equal(r.TileCols, r.RadialMap.W);
        Assert.Equal(r.TileRows, r.RadialMap.H);
        Assert.Equal(r.TotalTiles, r.RadialMap.D.Length);
        // 1 + radial/a, so the centre of the map sits at 1
        Assert.Equal(1.0, r.RadialMap.D.Max(), 1);
    }

    /// <summary>
    /// The fit has 5 parameters, so it needs at least 5 usable tiles. A frame this small
    /// yields fewer than that after Prepare and tiling, leaving the normal equations
    /// singular and Solve returning null on the very first iteration.
    ///
    /// Before phase 8 that path dereferenced the null and threw NullReferenceException.
    /// It now says what actually went wrong.
    /// </summary>
    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void ATooSmallFrame_FailsWithAnExplanationRatherThanANullReference(int n)
    {
        var img = TestImages.From(n, n, FitsTestData.Radial(n, n));

        var ex = Assert.Throws<Exception>(() => LightAnalyzer.Analyze(img));

        Assert.IsNotType<NullReferenceException>(ex);
        Assert.Contains("Fitul fondului a esuat", ex.Message);
    }

    [Fact]
    public void TheVerdictUsesTheAbsoluteRadialComponent()
    {
        // over-correction is just as bad as under-correction
        var under = LightAnalyzer.Analyze(TestImages.From(N, N, FitsTestData.Radial(N, N)));
        var over = LightAnalyzer.Analyze(TestImages.From(N, N, TestImages.InvertedRadial(N, N)));

        Assert.Equal(Stats.Verdict(Math.Abs(under.RadPct), Th.LightRadOk, Th.LightRadBad), under.Verdict);
        Assert.Equal(Stats.Verdict(Math.Abs(over.RadPct), Th.LightRadOk, Th.LightRadBad), over.Verdict);
    }
}
