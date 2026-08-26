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

        Assert.True(Math.Abs(r.RadPct) < LightAnalyzer.RadOk, "radPct was " + r.RadPct);
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
        Assert.True(Math.Abs(r.RadPct) < LightAnalyzer.RadOk,
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

    [Fact]
    public void TheVerdictUsesTheAbsoluteRadialComponent()
    {
        // over-correction is just as bad as under-correction
        var under = LightAnalyzer.Analyze(TestImages.From(N, N, FitsTestData.Radial(N, N)));
        var over = LightAnalyzer.Analyze(TestImages.From(N, N, TestImages.InvertedRadial(N, N)));

        Assert.Equal(Stats.Verdict(Math.Abs(under.RadPct), LightAnalyzer.RadOk, LightAnalyzer.RadBad), under.Verdict);
        Assert.Equal(Stats.Verdict(Math.Abs(over.RadPct), LightAnalyzer.RadOk, LightAnalyzer.RadBad), over.Verdict);
    }
}
