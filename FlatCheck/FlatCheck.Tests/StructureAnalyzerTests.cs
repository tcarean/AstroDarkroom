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

        Assert.Equal("OK", r.CornerVerdict);
        Assert.Equal("OK", r.AzimuthalVerdict);
        Assert.Equal("OK", r.ResidualVerdict);
        Assert.Equal("OK", r.CentroidVerdict);
        Assert.Equal("OK", r.Verdict);
    }

    [Fact]
    public void OnAPerfectlyRadialFlat_TheIlluminationPeakSitsOnTheSensorCentre()
    {
        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, FitsTestData.Radial(N, N)));

        Assert.True(r.CentroidOffsetPct < 1, "centroid offset was " + r.CentroidOffsetPct);
        Assert.Equal(256, r.SensorCx, 6);
        Assert.Equal(256, r.SensorCy, 6);
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
        Assert.Equal("PROBLEMA", r.CornerVerdict);
        Assert.Equal("PROBLEMA", r.Verdict);
    }

    [Fact]
    public void OnALinearGradient_TheAzimuthalAndResidualTestsFireToo()
    {
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(N, N), N, 0.12);

        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, scene));

        Assert.True(r.AzimuthalMaxPct > Th.AzimBad, "azimuthal was " + r.AzimuthalMaxPct);
        Assert.True(r.ResidualPtpPct > Th.AzimBad, "residual was " + r.ResidualPtpPct);
    }

    /// <summary>The overall verdict is the worst of the four, not an average.</summary>
    [Fact]
    public void TheOverallVerdictIsTheWorstOfTheFourTests()
    {
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(N, N), N, 0.12);
        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, scene));

        var all = new[] { r.CornerVerdict, r.AzimuthalVerdict, r.ResidualVerdict, r.CentroidVerdict };
        Assert.Equal(all.Max(Stats.Rank), Stats.Rank(r.Verdict));
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

        Assert.Equal(30000, r.RawMax, 6);          // the exact centre pixel
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
}
