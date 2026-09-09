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
        Assert.Equal(256, r.SensorCx, 6);
        Assert.Equal(256, r.SensorCy, 6);
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

    /// <summary>The overall verdict is the worst of the four, not an average.</summary>
    [Fact]
    public void TheOverallVerdictIsTheWorstOfTheFourTests()
    {
        var scene = FitsTestData.WithLinearGradient(FitsTestData.Radial(N, N), N, 0.12);
        var r = StructureAnalyzer.Analyze(TestImages.From(N, N, scene));

        var all = new[] { r.CornerVerdict, r.AzimuthalVerdict, r.ResidualVerdict, r.TiltVerdict };
        Assert.Equal(all.Max(Stats.Severity), Stats.Severity(r.Verdict));
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

    /// <summary>
    /// The map must not invent rings. On a frame carrying nothing but smooth vignetting
    /// and a linear gradient, the residual used to show concentric rings outside the
    /// inscribed circle plus a hard circular edge on it - see <see cref="Img.Symmetrise"/>
    /// for the mechanism. Someone reading that map would go looking for a round
    /// obstruction that was never there, which is exactly what happened.
    ///
    /// Read as ring MEANS: the mean over each ring collapses the gradient (the two halves
    /// cancel), so what is left is the radial behaviour of the model, and it has to be
    /// smooth from one ring to the next. Rings are sampled from 0.6x the inscribed radius
    /// out to the corner, which spans the whole region where the artefact lived, and only
    /// rings holding at least 2000 pixels are used - the last sliver before the corner tip
    /// is four arcs of some 90 pixels each, whose mean is sampling noise and jumps 0.5 %
    /// on its own.
    ///
    /// Measured on this scene: 2.47 % worst step from the raw profile, 0.11 % from the
    /// symmetrised one, over 57 rings. Both are asserted, so the test states what it
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

    /// <summary>Cel mai mare salt intre mediile a doua inele consecutive, in procente.</summary>
    static double WorstRingStep(Img resid)
    {
        double cx = resid.W / 2.0, cy = resid.H / 2.0;
        double rIns = Math.Min(cx, cy), rMax = Math.Sqrt(cx * cx + cy * cy);
        double step = 0.02 * rIns;

        double worst = 0, prev = double.NaN;
        int rings = 0;
        for (double rad = 0.6 * rIns; rad < rMax; rad += step)
        {
            double sum = 0; int n = 0;
            for (int y = 0; y < resid.H; y++)
                for (int x = 0; x < resid.W; x++)
                {
                    double d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d >= rad && d < rad + step) { sum += resid.At(y, x); n++; }
                }
            if (n < 2000) continue;

            double mean = sum / n;
            if (!double.IsNaN(prev)) worst = Math.Max(worst, Math.Abs(mean - prev) * 100);
            prev = mean;
            rings++;
        }

        Assert.True(rings > 40, $"prea putine inele masurate: {rings}");
        return worst;
    }
}
