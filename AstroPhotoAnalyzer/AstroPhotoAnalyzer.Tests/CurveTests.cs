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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// R-140 to R-145: the suggested curves (ADR 0032). The spline against values computed with
/// scipy's natural cubic spline on the owner's earlier curve; the anchors, the strengths and
/// the evidence on the synthetic band scene; the saved FITS against the file it came from.
/// </summary>
public class CurveTests
{
    static readonly (double X, double Y)[] Earlier =
        { (0, 0), (0.08, 0.08), (0.17, 0.165), (0.32, 0.42), (0.55, 0.76), (0.72, 0.92), (1, 1) };

    static ToneCurve Curve(params (double X, double Y)[] pts) => new(pts.Select(p => new CurvePoint(p.X, p.Y, null)).ToArray());

    /// <summary>
    /// The band scene at 0.7 of its values, so that its peak patch is under white: dust 0.035,
    /// sky 0.175, band 0.245, bright blob 0.42, peak 0.7. The WCS stays on the source.
    /// </summary>
    static Raster BandScene()
    {
        Raster r = WcsFixtures.BandScene();
        foreach (float[] plane in r.Planes)
            for (int i = 0; i < plane.Length; i++) plane[i] *= 0.7f;
        return r;
    }

    /// <summary>
    /// A frame without a WCS, in vertical stripes aligned to the 32 px patches: dust 0.05 (one
    /// column, 6 % of the patches), sky 0.15 (twelve columns), nebula 0.3 (two), bright nebula
    /// 0.5 (one), and one patch at <paramref name="peak"/> inside the nebula, the peak. The
    /// sky-relative classes find exactly these (ADR 0033), so the anchors are the five levels.
    /// </summary>
    internal static Raster StripeScene(float peak = 0.7f)
        => SyntheticRasters.From(512, 512, 3, (x, y, p) =>
            x >= 448 && x < 480 && y >= 224 && y < 256 ? peak : x < 32 ? 0.05f : x < 416 ? 0.15f : x < 480 ? 0.3f : 0.5f);

    static AnalysisResult Band() => AnalysisRun.Execute(BandScene(), "memorie", Scenes.OnBuiltForPaper());

    static CurveSet Suggest(AnalysisResult a, CurvePurpose purpose = CurvePurpose.Print) => CurveRun.Execute(a, purpose, 256, 256);

    [Fact]
    public void R140_TheCurveIsTheNaturalCubicSplineThroughItsPoints()
    {
        ToneCurve c = Curve(Earlier);

        // scipy.interpolate.CubicSpline(x, y, bc_type="natural") on the same points.
        Assert.Equal(0.0415792, c.Map(0.04), 6);
        Assert.Equal(0.1188232, c.Map(0.125), 6);
        Assert.Equal(0.2903529, c.Map(0.25), 6);
        Assert.Equal(0.42, c.Map(0.32), 9);
        Assert.Equal(0.6299974, c.Map(0.45), 6);
        Assert.Equal(0.9874927, c.Map(0.9), 6);
        Assert.Equal(1.8092, c.Slope(0.25), 3);
        Assert.Equal(0, c.Map(0), 12);
        Assert.Equal(1, c.Map(1), 12);
        Assert.True(c.IsMonotone);
    }

    [Fact]
    public void R140_ACurveIsClampedAppliedToEveryPlaneAlikeAndKnowsWhenItIsNotMonotone()
    {
        Assert.False(Curve((0, 0), (0.2, 0.6), (0.4, 0.3), (1, 1)).IsMonotone);

        // The spline through these points stays within 0..1 (measured 2026-10-23; the test's
        // earlier comment said it passed 1) but wavers on its way to (1, 1), so it is not
        // monotone and not suggestible. A spline that does pass 1 is the Maxima of
        // R140_ACurveWhoseSplineLeavesTheRangeIsNotSuggestible.
        ToneCurve over = Curve((0, 0), (0.5, 0.98), (0.6, 0.99), (1, 1));
        for (double v = 0; v <= 1; v += 0.01) Assert.InRange(over.Map(v), 0, 1);
        Assert.True(over.IsWithinRange);
        Assert.False(over.IsMonotone);
        Assert.False(over.IsSuggestible);

        ToneCurve c = Curve(Earlier);
        Raster r = SyntheticRasters.Flat(4, 4, 0.25f, 0.32f, 0.45f);
        Raster o = c.Apply(r);
        Assert.Equal(0.2903529, o.At(0, 1, 1), 4);
        Assert.Equal(0.42, o.At(1, 1, 1), 4);
        Assert.Equal(0.6299974, o.At(2, 1, 1), 4);
        Assert.Equal(0.25f, r.At(0, 1, 1));   // the input raster is not touched

        Assert.Throws<ArgumentException>(() => Curve((0, 0), (0.5, 0.5)));               // does not end at (1, 1)
        Assert.Throws<ArgumentException>(() => Curve((0, 0), (0.5, 0.4), (0.5, 0.6), (1, 1)));   // x not strictly increasing
    }

    /// <summary>
    /// A curve is judged on its unclamped spline (ADR 0039): the uncapped Maxima of
    /// de_printat.png (V-009, measured 2026-10-23: the spline reaches 1.010 before (1, 1) and
    /// comes back) is monotone and within 0..1 once clamped, and is not suggestible; the owner's
    /// earlier curve is.
    /// </summary>
    [Fact]
    public void R140_ACurveWhoseSplineLeavesTheRangeIsNotSuggestible()
    {
        ToneCurve maxima = Curve((0, 0), (0.0574, 0.0574), (0.0989, 0.0989), (0.2160, 0.3504), (0.3945, 0.6841), (0.5932, 0.92), (1, 1));
        for (double v = 0; v <= 1; v += 0.001) Assert.InRange(maxima.Map(v), 0, 1);
        Assert.False(maxima.IsWithinRange);
        Assert.False(maxima.IsMonotone);
        Assert.False(maxima.IsSuggestible);

        ToneCurve earlier = Curve(Earlier);
        Assert.True(earlier.IsWithinRange);
        Assert.True(earlier.IsMonotone);
        Assert.True(earlier.IsSuggestible);

        // The judgement without the table agrees with the built curve, and a flat run of points
        // on the diagonal, whose spline is straight up to rounding, is monotone.
        Assert.False(ToneCurve.WouldBeSuggestible(maxima.Points));
        Assert.True(ToneCurve.WouldBeSuggestible(earlier.Points));
        ToneCurve flat = Curve((0, 0), (0.1, 0.1), (0.2, 0.2), (0.3, 0.3), (0.4, 0.4), (1, 1));
        Assert.True(flat.IsMonotone);
        Assert.True(flat.IsSuggestible);
    }

    /// <summary>
    /// Maxima's factor is capped at the largest one whose spline stays within 0..1 (ADR 0039):
    /// on de_printat.png's anchors the recipe's factor 1.263 overshoots, the cap gives 1.220, the
    /// peaks land under the target and still gain more than CurveMaximaMinGain over Puternica;
    /// every suggestion is suggestible, and the uncapped curve would not have been.
    /// </summary>
    [Fact]
    public void R142_MaximaIsCappedSoThatItsSplineStaysWithinTheRange()
    {
        var labels = new[] { "praf", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri" };
        var a = new CurveAnchors { Dust = 0.0574, Sky = 0.0989, Band = 0.2160, Clouds = 0.3945, Peaks = 0.5932 };
        IReadOnlyList<(string Name, ToneCurve Curve)> curves = CurveAdvisor.Curves(a, labels);

        Assert.Equal(new[] { "Blanda", "Medie", "Puternica", "Maxima" }, curves.Select(c => c.Name).ToArray());
        Assert.All(curves, c => Assert.True(c.Curve.IsSuggestible, c.Name));
        double puternica = curves[2].Curve.Points[5].Y, maxima = curves[3].Curve.Points[5].Y;
        Assert.InRange(maxima, 0.89, 0.895);                       // 0.892 measured; 0.92 uncapped
        Assert.True(maxima < Thresholds.CurvePeakTarget);
        Assert.True(maxima - puternica > Thresholds.CurveMaximaMinGain);
        // The capped factor against the recipe's: the band point's rise over the sky, scaled.
        double reach = Thresholds.CurveSlopeSkyBand * (a.Band - a.Sky) + Thresholds.CurveSlopeBandCloud * (a.Clouds - a.Band) + Thresholds.CurveSlopeCloudPeak * (a.Peaks - a.Clouds);
        double factor = (curves[3].Curve.Points[3].Y - a.Sky) / (Thresholds.CurveSlopeSkyBand * (a.Band - a.Sky));
        Assert.InRange(factor, 1.21, 1.23);                        // 1.220 measured
        Assert.True(factor < (Thresholds.CurvePeakTarget - a.Sky) / reach);
        // Puternica's factor is 1 here: room / reach > 1 and its spline stays within the range.
        Assert.Equal(a.Sky + Thresholds.CurveSlopeSkyBand * (a.Band - a.Sky), curves[2].Curve.Points[3].Y, 9);
    }

    [Fact]
    public void R141_WithTheClassesEveryAnchorComesFromThemAndTheyIncrease()
    {
        AnalysisResult a = Band();
        CurveSet set = Suggest(a);

        Assert.Null(set.Problem);
        Assert.Equal(DustMethod.Band, set.Method);
        Assert.Equal(new[] { "praf", "cer", "banda", "nori luminosi", "varfuri" }, set.Labels);
        Assert.All(set.Suggestions, s => Assert.Equal("banda", s.Curve.Points[3].Label));
        CurveAnchors k = set.Anchors;
        Assert.Equal(ColorMath.GrayLevelOfLstar(a.Simulation.AsIs.LstarDust) / 255, k.Dust, 9);
        Assert.Equal(ColorMath.GrayLevelOfLstar(a.Simulation.AsIs.LstarSky) / 255, k.Sky, 9);
        Assert.True(k.Dust < k.Sky && k.Sky < k.Band && k.Band < k.Clouds && k.Clouds < k.Peaks);

        // The band's anchor is the upper part of its class, not its median.
        DustMap map = a.Simulation.Map;
        float[] L = a.Simulation.AsIsDust.PatchLstars;
        float[] band = Enumerable.Range(0, L.Length).Where(i => map.Classes[i] == PatchClass.Band).Select(i => L[i]).ToArray();
        Assert.Equal(ColorMath.GrayLevelOfLstar(Stats.PercentileSorted(Stats.Sorted(band), Thresholds.CurveBandClassPercentile)) / 255, k.Band, 9);
    }

    [Fact]
    public void R141_WithoutTheBandTheAnchorsAreTheSkyRelativeClassesAndTheirNamesAreThePoints()
    {
        CurveSet set = Suggest(AnalysisRun.Execute(StripeScene(), "memorie", Scenes.OnBuiltForPaper()));
        Assert.Equal(DustMethod.Sky, set.Method);
        Assert.Equal(new[] { "praf", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri" }, set.Labels);
        Assert.Null(set.Problem);
        Assert.Equal(0.05, set.Anchors.Dust, 3);
        Assert.Equal(0.15, set.Anchors.Sky, 3);
        Assert.Equal(0.3, set.Anchors.Band, 3);
        Assert.Equal(0.5, set.Anchors.Clouds, 3);
        Assert.Equal(0.7, set.Anchors.Peaks, 3);
        Assert.All(set.Suggestions, s => Assert.Equal("nebuloasa", s.Curve.Points[3].Label));
        Assert.Contains("dalele impartite prin comparatie cu cerul (fara WCS)", set.SourceSentence);
        Assert.Contains("nebuloasa la pragul sub care stau 80 % din dalele ei", set.SourceSentence);
        Assert.All(set.Suggestions, s => Assert.Equal(set.Labels, s.Evidence.Labels));

        // A flat frame has one level: the anchors cannot increase, and nothing is suggested.
        CurveSet flat = Suggest(AnalysisRun.Execute(SyntheticRasters.Flat(64, 64, 0.2f), "memorie", Scenes.OnBuiltForPaper()));
        Assert.NotNull(flat.Problem);
        Assert.Empty(flat.Suggestions);

        // The dust at black (a stack's black border is the darkest sky): no refusal, the dust
        // point is the curve's own origin, labelled, and the five labelled points are still there.
        Raster border = SyntheticRasters.From(512, 512, 3, (x, y, p) => y < 32 ? 0f : x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.7f : x < 416 ? 0.15f : x < 480 ? 0.3f : 0.5f);
        CurveSet black = Suggest(AnalysisRun.Execute(border, "memorie", Scenes.OnBuiltForPaper()));
        Assert.Null(black.Problem);
        Assert.Equal(0, black.Anchors.Dust, 6);
        Assert.NotEmpty(black.Suggestions);
        foreach (CurveSuggestion s in black.Suggestions)
        {
            Assert.Equal(6, s.Curve.Points.Count);
            Assert.Equal(new[] { "praf", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri", null }, s.Curve.Points.Select(q => q.Label).ToArray());
            Assert.Equal(0, s.Curve.Points[0].X);
            Assert.Equal(0, s.Curve.Points[0].Y);
            Assert.Equal(5, s.Evidence.LstarBefore.Count);
        }

        // Without a bright-nebula column there is no fourth class: a problem naming it, no suggestion.
        Raster noBright = SyntheticRasters.From(512, 512, 3, (x, y, p) => x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.7f : x < 32 ? 0.05f : x < 416 ? 0.15f : 0.3f);
        CurveSet empty = Suggest(AnalysisRun.Execute(noBright, "memorie", Scenes.OnBuiltForPaper()));
        Assert.Contains("nu exista nicio dala de felul \"nebuloasa stralucitoare\"", empty.Problem);
        Assert.Empty(empty.Suggestions);
    }

    [Fact]
    public void R142_PeaksAlreadyAtOrOverTheTargetGetNoCurveAndTheReasonNamesTheTarget()
    {
        // The same stripes with the peak patch at 0.95: above CurvePeakTarget, the recipe would
        // leave every point on the diagonal, so nothing is suggested and the message says why.
        CurveSet set = Suggest(AnalysisRun.Execute(StripeScene(peak: 0.95f), "memorie", Scenes.OnBuiltForPaper()));
        Assert.Equal(0.95, set.Anchors.Peaks, 3);
        Assert.NotNull(set.Problem);
        Assert.Contains("punctul \"varfuri\" e deja la 0.950", set.Problem);
        Assert.Contains($"la sau peste {Thresholds.CurvePeakTarget:0.00}, unde ajung varfurile", set.Problem);
        Assert.Contains("imaginea e deja cat de luminoasa poate fi", set.Problem);
        Assert.Empty(set.Suggestions);

        // Exactly at the target counts too; just under it does not.
        var labels = new[] { "praf", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri" };
        Assert.NotNull(CurveAdvisor.Problem(new CurveAnchors { Dust = 0.05, Sky = 0.15, Band = 0.3, Clouds = 0.5, Peaks = Thresholds.CurvePeakTarget }, labels));
        Assert.Null(CurveAdvisor.Problem(new CurveAnchors { Dust = 0.05, Sky = 0.15, Band = 0.3, Clouds = 0.5, Peaks = Thresholds.CurvePeakTarget - 0.01 }, labels));
    }

    [Fact]
    public void R142_TheSuggestionsNeverMoveTheDustOrTheSkyAndGrowWithTheirStrength()
    {
        foreach (CurvePurpose purpose in new[] { CurvePurpose.Print, CurvePurpose.Screen })
        {
            CurveSet set = Suggest(Band(), purpose);
            Assert.Equal(new[] { "Blanda", "Medie", "Puternica" }, set.Suggestions.Take(3).Select(s => s.Name).ToArray());

            foreach (CurveSuggestion s in set.Suggestions)
            {
                IReadOnlyList<CurvePoint> p = s.Curve.Points;
                Assert.Equal(7, p.Count);
                Assert.Equal(new[] { null, "praf", "cer", "banda", "nori luminosi", "varfuri", null }, p.Select(x => x.Label).ToArray());
                Assert.Equal(set.Anchors.Dust, p[1].X, 12);
                Assert.Equal(p[1].X, p[1].Y, 12);
                Assert.Equal(p[2].X, p[2].Y, 12);
                Assert.True(p[3].Y > p[3].X && p[4].Y > p[4].X && p[5].Y > p[5].X);
                Assert.True(p[5].Y <= Thresholds.CurvePeakTarget + 1e-9);
                Assert.True(s.Curve.IsMonotone);
            }

            // Blanda and Medie are half and three quarters of the way to Puternica.
            IReadOnlyList<CurvePoint> full = set.Suggestions[2].Curve.Points;
            for (int i = 3; i <= 5; i++)
            {
                Assert.Equal(full[i].X + 0.5 * (full[i].Y - full[i].X), set.Suggestions[0].Curve.Points[i].Y, 9);
                Assert.Equal(full[i].X + 0.75 * (full[i].Y - full[i].X), set.Suggestions[1].Curve.Points[i].Y, 9);
            }

            // The segments of the full curve keep the three slopes' proportions.
            double sb = (full[3].Y - full[2].Y) / (full[3].X - full[2].X), bc = (full[4].Y - full[3].Y) / (full[4].X - full[3].X);
            Assert.Equal(Thresholds.CurveSlopeSkyBand / Thresholds.CurveSlopeBandCloud, sb / bc, 6);
            Assert.True(sb <= Thresholds.CurveSlopeSkyBand + 1e-9);

            // Maxima exists exactly when its peaks gain more than the minimum over Puternica's; they
            // reach the target unless the cap of ADR 0039 holds them under it.
            if (set.Suggestions.Count == 4)
            {
                Assert.Equal("Maxima", set.Suggestions[3].Name);
                double peaks = set.Suggestions[3].Curve.Points[5].Y;
                Assert.True(peaks <= Thresholds.CurvePeakTarget + 1e-9);
                Assert.True(peaks - full[5].Y > Thresholds.CurveMaximaMinGain);
                Assert.True(set.Suggestions[3].Curve.IsSuggestible);
            }
            else Assert.Equal(3, set.Suggestions.Count);
        }
    }

    [Fact]
    public void R142_ThePurposeChangesNoCurve()
    {
        AnalysisResult a = Band();
        CurveSet print = Suggest(a, CurvePurpose.Print), screen = Suggest(a, CurvePurpose.Screen);
        Assert.Equal(print.Suggestions.Count, screen.Suggestions.Count);
        for (int i = 0; i < print.Suggestions.Count; i++)
            Assert.Equal(print.Suggestions[i].Curve.Points, screen.Suggestions[i].Curve.Points);
    }

    [Fact]
    public void R143_EverySuggestionCarriesItsMeasuredEvidence()
    {
        AnalysisResult a = Band();
        CurveSet set = Suggest(a);
        CurveSuggestion strong = set.Suggestions[2];
        CurveEvidence e = strong.Evidence;

        Assert.Equal(5, e.LstarBefore.Count);
        Assert.Equal(a.Simulation.AsIs.LstarDust, e.LstarBefore[0], 6);
        Assert.Equal(a.Simulation.AsIs.LstarSky, e.LstarBefore[1], 6);
        // The dust and the sky stay within a fraction of an L*; the band, the clouds and the peaks rise.
        Assert.InRange(e.LstarAfter[0] - e.LstarBefore[0], -0.5, 0.5);
        Assert.InRange(e.LstarAfter[1] - e.LstarBefore[1], -0.5, 0.5);
        for (int i = 2; i < 5; i++) Assert.True(e.LstarAfter[i] > e.LstarBefore[i]);

        Assert.Equal(strong.Curve.Slope(set.Anchors.Sky), e.NoiseGain, 9);
        Assert.True(e.NoiseGain >= 1);
        Assert.True(set.Suggestions[0].Evidence.NoiseGain <= e.NoiseGain);
        Assert.InRange(e.PctClippedAfter, e.PctClippedBefore, 100);

        // The preview is the curved image, reduced to fit the size asked for.
        Assert.InRange(strong.Preview.W, 1, 256);
        Assert.InRange(strong.Preview.H, 1, 256);
        Assert.Equal(set.OriginalPreview.W, strong.Preview.W);
    }

    [Fact]
    public void R143_ThePaperChecksAppearForPrintOnlyAndCarryTheirNumbers()
    {
        // A sky under L* 15 or a dust under paper black: for print, an Attention that sends to the stretch.
        AnalysisResult dark = Band();
        CurveSet print = Suggest(dark, CurvePurpose.Print), screen = Suggest(dark, CurvePurpose.Screen);

        foreach (CurveSuggestion s in screen.Suggestions)
        {
            Assert.Empty(s.Evidence.PaperChecks);
            Assert.Null(s.Evidence.Gamut);
            Assert.Null(s.Evidence.GamutNote);
        }
        foreach (CurveSuggestion s in print.Suggestions)
        {
            foreach (Recommendation r in s.Evidence.PaperChecks)
            {
                Assert.False(string.IsNullOrWhiteSpace(r.Because));
                Assert.True(r.Verdict >= Verdict.Attention);
            }
            bool skyLow = Math.Round(dark.Simulation.AsIs.LstarSky, 1) < Thresholds.LowSeparationLstar;
            Assert.Equal(skyLow || dark.Simulation.DustBelowPaperBlack, s.Evidence.PaperChecks.Any(r => r.Text.Contains("stretch")));
        }
    }

    [Fact]
    public void R144_WithoutAProfileTheGamutFigureIsAbsentAndTheEvidenceSaysHowToGetOne()
    {
        CurveSet print = Suggest(Band(), CurvePurpose.Print);
        foreach (CurveSuggestion s in print.Suggestions)
        {
            Assert.Null(s.Evidence.Gamut);
            Assert.Contains("Din profil ICC...", s.Evidence.GamutNote);
        }
    }

    static string[] Cards(byte[] file)
    {
        var cards = new List<string>();
        for (int i = 0; i + 80 <= file.Length; i += 80)
        {
            string c = System.Text.Encoding.ASCII.GetString(file, i, 80);
            cards.Add(c);
            if (c.StartsWith("END     ")) break;
        }
        return cards.ToArray();
    }

    [Fact]
    public void R146_ThePreviewStrideCoversTheFittedSizeAndStaysUnderTheCap()
    {
        // 5837 x 3867, the reference image's size, as a one-plane raster (ADR 0084).
        var r = new Raster(3867, 5837, 1);
        // The presumed area: the height limits the fit (4.30 against 3.65), so the stride is 4
        // and the preview, 1460 x 967, covers the fitted 1359 x 900.
        Assert.Equal(4, CurveRun.PreviewStride(r, 1600, 900));
        // A wide, short area: the height still limits; covering both dimensions would take 4.
        Assert.Equal(5, CurveRun.PreviewStride(r, 1340, 650));
        // A 4K area: the whole image would cover it, but the cap of CurvePreviewMaxPixels holds
        // the stride at 2 (5.6 MP), and the core says so, the one case the view enlarges.
        Assert.Equal(2, CurveRun.PreviewStride(r, 3840, 2160));
        Assert.True(CurveRun.PreviewCapped(r, 3840, 2160));
        Assert.False(CurveRun.PreviewCapped(r, 1600, 900));
        Assert.False(CurveRun.PreviewCapped(new Raster(400, 600, 1), 1600, 900));
        // An image smaller than the area: 1; one larger than a tiny area: the step that covers it.
        Assert.Equal(1, CurveRun.PreviewStride(new Raster(400, 600, 1), 1600, 900));
        Assert.Equal(8, CurveRun.PreviewStride(new Raster(400, 600, 1), 100, 50));
    }

    [Fact]
    public void R145_AFitsInputKeepsEveryCardAndItsBitpixAndGetsTheCurve()
    {
        using var ws = new TempWorkspace();
        string[] extra =
        {
            FitsTestData.StringCard("ROWORDER", "BOTTOM-UP"), FitsTestData.StringCard("INSTRUME", "Nikon D3300", "Instrument name"),
            FitsTestData.StringCard("CTYPE1", "RA---TAN"), FitsTestData.StringCard("CTYPE2", "DEC--TAN"),
            FitsTestData.Card("CRPIX1", "32.5"), FitsTestData.Card("CRPIX2", "32.5"), FitsTestData.Card("CRVAL1", "281.4"), FitsTestData.Card("CRVAL2", "-1.95"),
            FitsTestData.Card("CD1_1", "-0.06"), FitsTestData.Card("CD1_2", "0"), FitsTestData.Card("CD2_1", "0"), FitsTestData.Card("CD2_2", "0.06"),
            FitsTestData.Card("EXPTIME", "119.2"),
        };
        byte[] source = FitsTestData.Build(64, 64, 3, (x, y, p) => 0.05 + 0.9 * (x + 64 * y) / 4096.0 * (p + 1) / 3, bitpix: -32, extraCards: extra);
        string input = ws.WriteFile("in.fit", source);
        string output = ws.PathTo("out.fit");

        AnalysisResult a = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        ToneCurve c = Curve(Earlier);
        CurveExport.Save(a, new CurveSuggestion { Name = "Puternica", Curve = c }, output);

        Assert.Equal(source, File.ReadAllBytes(input));   // the input is byte for byte what it was

        string[] before = Cards(source), after = Cards(File.ReadAllBytes(output));
        // Every card of the source, in order, then HISTORY, then END.
        Assert.Equal(before[..^1], after[..(before.Length - 1)]);
        string[] added = after[(before.Length - 1)..^1];
        Assert.NotEmpty(added);
        Assert.All(added, card => Assert.StartsWith("HISTORY ", card));
        string history = string.Concat(added.Select(card => card[8..].TrimEnd() + " "));
        Assert.Contains("AstroPhotoAnalyzer", history);
        Assert.Contains("Puternica", history);
        Assert.Contains("0.320 0.420", history);

        Raster back = ImageLoader.Load(output);
        Assert.Equal(-32, FitsReader.HInt(FitsReader.ReadImageHeader(output).Header, "BITPIX", 0));
        Assert.NotNull(back.Source.Wcs);
        Raster expected = c.Apply(a.Raster);
        for (int p = 0; p < 3; p++)
            for (int i = 0; i < expected.Planes[p].Length; i += 7)
                Assert.Equal(expected.Planes[p][i], back.Planes[p][i], 4);
    }

    [Fact]
    public void R145_AnIntegerFitsKeepsItsBitpixAndAPngBecomesAFloatFits()
    {
        using var ws = new TempWorkspace();
        ToneCurve c = Curve(Earlier);

        string fit16 = ws.WriteFile("in16.fit", FitsTestData.Build(48, 48, 3, (x, y, p) => 3000 + 900 * ((x + y + p) % 60)));
        AnalysisResult a16 = AnalysisRun.Execute(fit16, Scenes.OnBuiltForPaper());
        string out16 = ws.PathTo("out16.fit");
        CurveExport.Save(a16, new CurveSuggestion { Name = "Medie", Curve = c }, out16);
        Assert.Equal(16, FitsReader.HInt(FitsReader.ReadImageHeader(out16).Header, "BITPIX", 0));
        Raster back16 = ImageLoader.Load(out16), want16 = c.Apply(a16.Raster);
        for (int i = 0; i < want16.Planes[1].Length; i += 5)
            Assert.InRange(Math.Abs(back16.Planes[1][i] - want16.Planes[1][i]), 0, 1.0 / 65535 + 1e-7);

        string png = ImageFiles.SavePng16Rgb(SyntheticRasters.Ramp(48, 48, 0.06f, 0.8f), ws.PathTo("in.png"));
        byte[] pngBytes = File.ReadAllBytes(png);
        AnalysisResult ap = AnalysisRun.Execute(png, Scenes.OnBuiltForPaper());
        string outPng = ws.PathTo("din-png.fit");
        CurveExport.Save(ap, new CurveSuggestion { Name = "Blanda", Curve = c }, outPng);
        Assert.Equal(pngBytes, File.ReadAllBytes(png));
        var header = FitsReader.ReadImageHeader(outPng);
        Assert.Equal(-32, FitsReader.HInt(header.Header, "BITPIX", 0));
        Assert.Equal("BOTTOM-UP", FitsReader.HStr(header.Header, "ROWORDER", ""));
        Raster backPng = ImageLoader.Load(outPng), wantPng = c.Apply(ap.Raster);
        Assert.Equal(wantPng.W, backPng.W);
        Assert.Equal(3, backPng.Planes.Length);
        for (int i = 0; i < wantPng.Planes[0].Length; i += 5)
            Assert.Equal(wantPng.Planes[0][i], backPng.Planes[0][i], 5);
    }

    [Fact]
    public void R145_TheInputIsProtectedWhateverSpellingTheTargetReachesItBy()
    {
        if (!OperatingSystem.IsWindows()) return;   // the device spellings and the sharing rules are Windows'
        using var ws = new TempWorkspace();
        byte[] source = FitsTestData.Build(48, 48, 3, (x, y, p) => 3000 + 900 * ((x + y + p) % 60));
        string input = ws.WriteFile("in.fit", source);
        AnalysisResult a = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        var s = new CurveSuggestion { Name = "Medie", Curve = Curve(Earlier) };

        // The same file through the Win32 file and device namespaces: a string compare cannot
        // see it, so the input is held open against writing for the length of the save.
        foreach (string prefix in new[] { @"\\?\", @"\\.\" })
        {
            Exception e = Assert.ThrowsAny<Exception>(() => CurveExport.Save(a, s, prefix + input));
            Assert.Contains("imaginea de intrare", e.Message);
            Assert.Equal(source, File.ReadAllBytes(input));
        }
        Assert.Equal(new[] { input }, Directory.GetFiles(ws.Dir));   // no temporary file left
    }

    [Fact]
    public void R145_TheInputIsKnownByItsFullPathAndASaveWithoutItIsRefused()
    {
        using var ws = new TempWorkspace();
        byte[] source = FitsTestData.Build(48, 48, 3, (x, y, p) => 3000 + 900 * ((x + y + p) % 60));
        string input = ws.WriteFile("in.fit", source);
        var s = new CurveSuggestion { Name = "Medie", Curve = Curve(Earlier) };

        // Opened by a relative path: the result keeps the full path of that moment, so a later
        // change of the working directory cannot make the tool lose the input.
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, input);
        AnalysisResult a = AnalysisRun.Execute(relative, Scenes.OnBuiltForPaper());
        Assert.Equal(Path.GetFullPath(input), a.FullPath);
        Exception same = Assert.ThrowsAny<Exception>(() => CurveExport.Save(a, s, input));
        Assert.Contains("imaginea de intrare", same.Message);
        Assert.Equal(source, File.ReadAllBytes(input));

        // The input gone between the analysis and the save: refused, never a FITS without its header.
        File.Delete(input);
        string target = ws.PathTo("out.fit");
        Exception gone = Assert.ThrowsAny<Exception>(() => CurveExport.Save(a, s, target));
        Assert.Contains("nu mai e", gone.Message);
        Assert.False(File.Exists(target));
        Assert.Empty(Directory.GetFiles(ws.Dir));
    }

    [Fact]
    public void R145_ValuesThatDoNotFitTheSourcesEncodingRefuseTheSave()
    {
        using var ws = new TempWorkspace();
        // A signed 16-bit FITS without BZERO holds 0..32767, half the scale: the curve lifts the
        // bright samples past it. Clipping them silently would be a wrong file.
        string input = ws.WriteFile("in.fit", FitsTestData.Build(48, 48, 3, (x, y, p) => 3000 + 500 * ((x + y + p) % 56), bzero: 0));
        AnalysisResult a = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        string target = ws.PathTo("out.fit");

        Exception e = Assert.ThrowsAny<Exception>(() => CurveExport.Save(a, new CurveSuggestion { Name = "Medie", Curve = Curve(Earlier) }, target));
        Assert.Contains("nu mai incap", e.Message);
        Assert.False(File.Exists(target));
        Assert.Equal(new[] { input }, Directory.GetFiles(ws.Dir));
    }

    [Fact]
    public void R145_TheHdusAroundTheImagePassUnchangedAndABottomUpIntegerImageKeepsItsRows()
    {
        using var ws = new TempWorkspace();
        byte[] image = FitsTestData.Build(40, 24, 1, (x, y, _) => 2000 + 1500 * ((x + 3 * y) % 36),
            extraCards: new[] { FitsTestData.StringCard("ROWORDER", "BOTTOM-UP") });
        byte[] trailing = FitsTestData.Build2D(8, 8, (x, y) => 100 * (x + y));
        byte[] source = FitsTestData.WithEmptyPrimary(image).Concat(trailing).ToArray();
        string input = ws.WriteFile("in.fit", source);
        AnalysisResult a = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        ToneCurve c = Curve(Earlier);
        string target = ws.PathTo("out.fit");

        CurveExport.Save(a, new CurveSuggestion { Name = "Medie", Curve = c }, target);

        byte[] saved = File.ReadAllBytes(target);
        Assert.Equal(source[..2880], saved[..2880]);                                   // the empty primary
        Assert.Equal(trailing, saved[^trailing.Length..]);                            // the HDU after the image
        Assert.Equal(0, saved.Length % 2880);
        Raster back = ImageLoader.Load(target), want = c.Apply(a.Raster);
        Assert.Single(back.Planes);
        for (int i = 0; i < want.Planes[0].Length; i++)
            Assert.InRange(Math.Abs(back.Planes[0][i] - want.Planes[0][i]), 0, 1.0 / 65535 + 1e-7);
    }

    [Fact]
    public void R145_AFreshFitsIsWrittenBottomUpSoThatSirilShowsItTheRightWayUp()
    {
        // Siril puts the file's first row at the bottom whatever ROWORDER says (ADR 0049): a
        // FITS made from a PNG carries the PNG's bottom row first and its top row last.
        using var ws = new TempWorkspace();
        var ramp = new Raster(48, 48, 3);
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
                for (int p = 0; p < 3; p++) ramp.Set(p, y, x, 0.06f + 0.74f * y / 47);
        string png = ImageFiles.SavePng16Rgb(ramp, ws.PathTo("ramp.png"));
        AnalysisResult a = AnalysisRun.Execute(png, Scenes.OnBuiltForPaper());
        ToneCurve c = Curve(Earlier);
        string target = ws.PathTo("ramp.fit");

        CurveExport.Save(a, new CurveSuggestion { Name = "Blanda", Curve = c }, target);

        byte[] saved = File.ReadAllBytes(target);
        int endCard = 0;
        while (System.Text.Encoding.ASCII.GetString(saved, endCard * 80, 8).TrimEnd() != "END") endCard++;
        int data = (endCard * 80 / 2880 + 1) * 2880;
        float first = BitConverter.ToSingle(saved.Skip(data).Take(4).Reverse().ToArray(), 0);
        float lastRow = BitConverter.ToSingle(saved.Skip(data + 47 * 48 * 4).Take(4).Reverse().ToArray(), 0);
        Raster want = c.Apply(a.Raster);
        Assert.Equal(want.At(0, 47, 0), first, 5);      // the file starts with the image's bottom row
        Assert.Equal(want.At(0, 0, 0), lastRow, 5);     // and ends its first plane with the top row
        Assert.Equal("BOTTOM-UP", FitsReader.HStr(FitsReader.ReadImageHeader(target).Header, "ROWORDER", ""));
    }

    [Fact]
    public void R142_TheScreenEvidenceIsThePrintEvidenceWithoutThePaperChecks()
    {
        AnalysisResult a = Band();
        CurveSet print = Suggest(a, CurvePurpose.Print), derived = CurveRun.ForScreen(print), screen = Suggest(a, CurvePurpose.Screen);

        Assert.Equal(CurvePurpose.Screen, derived.Purpose);
        Assert.Equal(screen.Suggestions.Count, derived.Suggestions.Count);
        for (int i = 0; i < screen.Suggestions.Count; i++)
        {
            CurveEvidence d = derived.Suggestions[i].Evidence, e = screen.Suggestions[i].Evidence;
            Assert.Same(print.Suggestions[i].Curve, derived.Suggestions[i].Curve);
            Assert.Equal(e.LstarAfter, d.LstarAfter);
            Assert.Equal(e.NoiseGain, d.NoiseGain);
            Assert.Equal(e.PctClippedAfter, d.PctClippedAfter);
            Assert.Empty(d.PaperChecks);
            Assert.Null(d.Gamut);
            Assert.Null(d.GamutNote);
        }
    }

    [Fact]
    public void R143_TheNoiseCheckFiresWhenTheGainTakesTheNoisePastThePrintTarget()
    {
        AnalysisResult a = Band();
        var quiet = new NoiseResult { SmoothTiles = Thresholds.NoiseMinTiles, SigmaMedianLsb = 1.0 };
        var loud = new NoiseResult { SmoothTiles = Thresholds.NoiseMinTiles, SigmaMedianLsb = 1.4 };
        var few = new NoiseResult { SmoothTiles = 0, SigmaMedianLsb = 5 };
        CurveEvidence With(NoiseResult n) => new()
        {
            NoiseGain = 1.2,
            NoiseAfterLsb = n.Insufficient ? double.NaN : n.SigmaMedianLsb * 1.2,
        };

        Assert.DoesNotContain(CurveTexts.PaperChecks(a.Simulation, quiet, With(quiet)), r => r.Topic == Topic.Denoise);   // 1.2 LSB, under 1.5
        Recommendation check = Assert.Single(CurveTexts.PaperChecks(a.Simulation, loud, With(loud)), r => r.Topic == Topic.Denoise);   // 1.68 LSB
        Assert.Equal(Verdict.Attention, check.Verdict);
        Assert.Contains("1.40 niveluri (sigma) x 1.20", check.Because);
        Assert.Contains("= 1.68 niveluri", check.Because);
        Assert.Contains($"{Thresholds.NoisePrintHiLsb:0.0} niveluri", check.Because);
        Assert.DoesNotContain(CurveTexts.PaperChecks(a.Simulation, few, With(few)), r => r.Topic == Topic.Denoise);       // not enough flat sky: no figure, no check
    }

    [Fact]
    public void R144_TheGamutTableIsInterpolatedAndCountedPerClassOfThePatch()
    {
        // A hand-built table on a 2-cube: only the corner of pure red is displaced, by 16 in a*.
        var table = new GamutTable { N = 2, Dl = new float[8], Da = new float[8], Db = new float[8] };
        table.Da[(1 * 2 + 0) * 2 + 0] = 16;
        Assert.True(GamutAnalyzer.Outside(table, 1, 0, 0));
        Assert.False(GamutAnalyzer.Outside(table, 0, 0, 0));
        Assert.False(GamutAnalyzer.Outside(table, 0, 1, 0));
        Assert.False(GamutAnalyzer.Outside(table, 0.2f, 0, 0));    // 16 x 0.2 = 3.2, under the threshold of 4
        Assert.True(GamutAnalyzer.Outside(table, 0.3f, 0, 0));     // 4.8
        Assert.False(GamutAnalyzer.Outside(table, 1, 0.8f, 0));    // 16 x 0.2 = 3.2

        // 16 x 16 samples, patches of 8: the left half red (outside), the right half grey; the
        // top-left patch is dust, the bottom-left sky, the right two band.
        Raster sub = SyntheticRasters.From(16, 16, 3, (x, y, p) => x < 8 ? (p == 0 ? 1f : 0f) : 0.3f);
        var map = new DustMap
        {
            Cols = 2,
            Rows = 2,
            Side = 8,
            Stride = 4,
            Classes = new[] { PatchClass.Dust, PatchClass.Band, PatchClass.Sky, PatchClass.Band },
        };
        GamutFigure g = GamutAnalyzer.Measure(table, sub, map);
        Assert.Equal(50, g.PctAll, 9);
        Assert.Equal(new[] { 100.0, 100.0, 0.0, double.NaN, double.NaN }, g.PctByClass);

        // A mono image has no chroma to lose.
        Assert.Equal(0, GamutAnalyzer.Measure(table, SyntheticRasters.Flat(16, 16, 0.5f, planes: 1), map).PctAll);
    }

    /// <summary>
    /// Near the threshold the exact round trip decides, once per 8-bit colour, through the
    /// table's cache; away from it the interpolation does; a table without a trip behind it
    /// (hand-built, as above) is interpolation alone; and Measure, in parallel, counts what the
    /// pixel decisions say (ADR 0042).
    /// </summary>
    [Fact]
    public void R144_NearTheThresholdTheExactRoundTripDecidesOncePerEightBitColour()
    {
        // A 2-cube whose red corner is displaced by 5 in a*: red 0.65 interpolates to 3.25 and
        // red 0.95 to 4.75, both within the margin of 1.0 around 4; red 0.2 interpolates to 1.0,
        // outside it. The "exact" trip says the opposite of the interpolation for the two.
        var table = new GamutTable { N = 2, Dl = new float[8], Da = new float[8], Db = new float[8] };
        table.Da[(1 * 2 + 0) * 2 + 0] = 5;
        int trips = 0;
        table.Exact = (r, g, b) => { Interlocked.Increment(ref trips); return (0, r > 0.9 ? 2.0 : 6.0, 0); };

        Assert.True(GamutAnalyzer.Outside(table, 0.65f, 0, 0));     // exact: 6 > 4
        Assert.False(GamutAnalyzer.Outside(table, 0.95f, 0, 0));    // exact: 2 < 4
        Assert.False(GamutAnalyzer.Outside(table, 0.2f, 0, 0));     // interpolation alone: 1.0
        Assert.Equal(2, trips);
        Assert.True(GamutAnalyzer.Outside(table, 0.65f, 0, 0));     // the same 8-bit colour: cached
        Assert.Equal(2, trips);
        Assert.True(GamutAnalyzer.Outside(table, 0.65f + 1f / 255, 0, 0));   // one level away: one more trip
        Assert.Equal(3, trips);

        var plain = new GamutTable { N = 2, Dl = table.Dl, Da = table.Da, Db = table.Db };
        Assert.False(GamutAnalyzer.Outside(plain, 0.65f, 0, 0));
        Assert.True(GamutAnalyzer.Outside(plain, 0.95f, 0, 0));

        // 64 x 64, the left half red 0.65 (outside by the exact trip), the right half red 0.95
        // (inside): the left patches are dust, the right ones sky.
        Raster sub = SyntheticRasters.From(64, 64, 3, (x, y, p) => p == 0 ? (x < 32 ? 0.65f : 0.95f) : 0f);
        var map = new DustMap { Cols = 2, Rows = 2, Side = 32, Stride = 4, Classes = new[] { PatchClass.Dust, PatchClass.Sky, PatchClass.Dust, PatchClass.Sky } };
        GamutFigure g = GamutAnalyzer.Measure(table, sub, map);
        Assert.Equal(50, g.PctAll, 9);
        Assert.Equal(100, g.PctByClass[0], 9);
        Assert.Equal(0, g.PctByClass[1], 9);
        Assert.Equal(3, trips);   // both colours were in the cache already
    }

    /// <summary>
    /// The figure counts the whole colour difference, lightness included (ADR 0081): a corner
    /// whose L* alone moves by 6 is outside, and L* and a* add as a distance, not one at a time.
    /// </summary>
    [Fact]
    public void R144_ALightnessShiftAloneCountsAsOutside()
    {
        var table = new GamutTable { N = 2, Dl = new float[8], Da = new float[8], Db = new float[8] };
        table.Dl[(1 * 2 + 0) * 2 + 0] = 6;                              // red: the tone alone, 6
        table.Dl[(0 * 2 + 1) * 2 + 0] = 3; table.Da[(0 * 2 + 1) * 2 + 0] = 3;   // green: 3 and 3, a distance of 4.24
        table.Dl[(0 * 2 + 0) * 2 + 1] = 2; table.Db[(0 * 2 + 0) * 2 + 1] = 3;   // blue: 2 and 3, a distance of 3.61
        Assert.True(GamutAnalyzer.Outside(table, 1, 0, 0));
        Assert.True(GamutAnalyzer.Outside(table, 0, 1, 0));
        Assert.False(GamutAnalyzer.Outside(table, 0, 0, 1));
        Assert.False(GamutAnalyzer.Outside(table, 0.6f, 0, 0));          // 3.6
    }

    [Fact]
    public void R144_AProfileThatCannotBeUsedGivesItsReasonInsteadOfAFigure()
    {
        using var ws = new TempWorkspace();
        string notAProfile = ws.WriteFile("gol.icc", new byte[200]);
        Assert.NotNull(GamutAnalyzer.Build(notAProfile, 3).Problem);
        Assert.NotNull(GamutAnalyzer.Build(ws.PathTo("lipsa.icc"), 3).Problem);

        // Through the run: the Dmax came from a profile that then cannot give a round trip.
        AnalysisResult a = AnalysisRun.Execute(BandScene(), "memorie",
            Scenes.OnBuiltForPaper() with { DmaxProfile = "Gol", DmaxProfilePath = notAProfile });
        foreach (CurveSuggestion s in Suggest(a, CurvePurpose.Print).Suggestions)
        {
            Assert.Null(s.Evidence.Gamut);
            Assert.StartsWith("culorile pe care le poate reda hartia (gamutul): necunoscute - ", s.Evidence.GamutNote);
            Assert.DoesNotContain("Din profil ICC...", s.Evidence.GamutNote);
        }
    }

    [Fact]
    public void R145_SavingOverTheInputIsRefusedAndAFailedWriteLeavesNoFile()
    {
        using var ws = new TempWorkspace();
        byte[] source = FitsTestData.Build(48, 48, 3, (x, y, p) => 3000 + 900 * ((x + y + p) % 60));
        string input = ws.WriteFile("in.fit", source);
        AnalysisResult a = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        var s = new CurveSuggestion { Name = "Medie", Curve = Curve(Earlier) };

        Exception same = Assert.ThrowsAny<Exception>(() => CurveExport.Save(a, s, Path.Combine(ws.Dir, ".", "IN.FIT")));
        Assert.Contains("imaginea de intrare", same.Message);
        Assert.Equal(source, File.ReadAllBytes(input));

        string missing = Path.Combine(ws.Dir, "nu-exista", "out.fit");
        Assert.ThrowsAny<Exception>(() => CurveExport.Save(a, s, missing));
        Assert.False(File.Exists(missing));
        Assert.Empty(Directory.GetFiles(ws.Dir).Where(f => !f.EndsWith("in.fit")));
    }

    /// <summary>
    /// A paper check whose because carries Fmt.Strong's marks shows them stripped on the curves
    /// tab's evidence rows, as the text report does (R-112): only the Raport tab renders them.
    /// </summary>
    [Fact]
    public void R112_TheCurvesEvidenceStripsTheMarksOfAPaperCheck()
    {
        CurveSuggestion s = Suggest(Band()).Suggestions[0];
        var marked = new Recommendation(Topic.Background, Verdict.Attention, "verificare", "pe " + Fmt.Strong("0.20 %") + " din cadru");
        s = s with { Evidence = s.Evidence with { PaperChecks = new[] { marked } } };

        (string Text, bool Warning) row = Assert.Single(CurveTexts.EvidenceRows(s), r => r.Warning);
        Assert.Equal("! verificare (fiindca: pe 0.20 % din cadru)", row.Text);
    }

    /// <summary>
    /// R-146 (ADR 0076): every text of the Editare Curbe tab and of the curve window is in plain
    /// words, in both languages: where the curve's points come from, the evidence of every
    /// suggestion for print (its paper checks and the gamut note included), the reasons no curve
    /// is suggested, the window's caption and note, the save's messages and the saved FITS's
    /// HISTORY lines, each of which fits its card's 72 characters.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R146_TheCurvesTextsAreInPlainWords(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        var texts = new List<string>();
        foreach (Raster r in new[] { StripeScene(), StripeScene(peak: 0.95f), BandScene(), Scenes.DarkSky(), SyntheticRasters.Flat(64, 64, 0.2f) })
        {
            CurveSet set = Suggest(AnalysisRun.Execute(r, "memorie", Scenes.OnBuiltForPaper()));
            texts.Add(set.SourceSentence);
            texts.Add(set.Problem);
            foreach (CurveSuggestion s in set.Suggestions)
            {
                texts.AddRange(CurveTexts.EvidenceLines(s));
                texts.AddRange(CurveTexts.History(s.Name, s.Curve));
            }
        }
        foreach (DustMethod method in new[] { DustMethod.Band, DustMethod.Sky })
            foreach (bool colour in new[] { true, false })
            {
                texts.Add(CurveTexts.CurveCaption(colour));
                texts.Add(CurveTexts.PointsNote(method, colour));
            }
        texts.AddRange(new[]
        {
            CurveTexts.NoMonotoneCurve, CurveTexts.GamutNeedsProfile, CurveTexts.GamutUnreadable("x"), CurveTexts.InputGone("x.fit"),
            CurveTexts.ValuesDoNotFit(123, 16, 0), CurveTexts.AnchorsNotIncreasing("cer", 0.13, "nebuloasa", 0.12),
            CurveTexts.ClassEmpty("varfuri"), CurveTexts.PeaksOverTarget("varfuri", 0.95),
        });

        var methodWords = new System.Text.RegularExpressions.Regex(
            @"\b(ancor\w*|anchors?|percentil\w*|median\w*|LSB|panta|slope|clas[ae]|clasele|class|classes|modelat|modelled|vizibilitate|visibility|plafon|ceiling|crescatoare|increasing|codarea|encoding|gamutul hartiei|paper gamut|control points|puncte de control)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.All(texts.Where(t => t != null), t => Assert.False(methodWords.IsMatch(t), t));
        Assert.All(CurveTexts.History("Puternica", Curve(Earlier)), line => Assert.True(line.Length <= 72, line));
    }

    // ---- R-184: the dust move (ADR 0101) ----

    static readonly string[] BandLabels = { "praf", "cer", "banda", "nori luminosi", "varfuri" };

    /// <summary>The anchors of the reference FITS (V-008), and its sky's dark end.</summary>
    static CurveAnchors ReferenceAnchors => new() { Dust = 0.0897, Sky = 0.1690, Band = 0.3035, Clouds = 0.5374, Peaks = 0.7199 };
    const double ReferenceHold = 0.1396;

    /// <summary>A neutral, even dust: its L* after a curve is the L* of the curve's output at its anchor.</summary>
    static Func<ToneCurve, double> GreyDust(CurveAnchors a) => c => ColorMath.LstarOfGrayLevel(c.Map(a.Dust) * 255);

    /// <summary>
    /// The stripe scene with its dust at 0.09 (L* 7.7), one column, 6 % of the patches, so that
    /// the dust zone gives the dust's figure; the sky is one level, 0.15 (L* 15.0).
    /// </summary>
    internal static Raster DustStripeScene()
        => SyntheticRasters.From(512, 512, 3, (x, y, p) =>
            x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.7f : x < 32 ? 0.09f : x < 416 ? 0.15f : x < 480 ? 0.3f : 0.5f);

    static AnalysisResult DustStripes(double dmax) => AnalysisRun.Execute(DustStripeScene(), "memorie", new Parameters { DmaxOverride = dmax });

    /// <summary>
    /// The same stripes with a sky that has its own spread: it brightens down the frame, from
    /// 0.14 to 0.16 (L* 14.0 to 16.0), so that its dark end lies under its middle and the dust
    /// move has a point of its own to hold.
    /// </summary>
    internal static Raster DustStripeSceneWithASpreadSky()
        => SyntheticRasters.From(512, 512, 3, (x, y, p) =>
            x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.7f : x < 32 ? 0.09f : x < 416 ? 0.14f + 0.02f * y / 511 : x < 480 ? 0.3f : 0.5f);

    static CurveSet Moved(AnalysisResult a, CurvePurpose purpose = CurvePurpose.Print) => CurveRun.Execute(a, purpose, 256, 256, dustOnPaperBlack: true);

    [Fact]
    public void R184_TheMovePutsItsPointsUnderTheSkyAndLeavesTheRestAsItWas()
    {
        CurveAnchors a = ReferenceAnchors;
        IReadOnlyList<(string Name, ToneCurve Curve)> plain = CurveAdvisor.Curves(a, BandLabels);

        foreach (bool helper in new[] { false, true })
        {
            var move = new DustMove { DustOutput = 0.0505, Hold = ReferenceHold, Helper = helper };
            IReadOnlyList<(string Name, ToneCurve Curve)> moved = CurveAdvisor.Curves(a, BandLabels, move);
            Assert.Equal(plain.Select(c => c.Name).ToArray(), moved.Select(c => c.Name).ToArray());

            for (int k = 0; k < moved.Count; k++)
            {
                IReadOnlyList<CurvePoint> p = moved[k].Curve.Points;
                string?[] labels = helper
                    ? new[] { null, "sub praf", "praf", "cer inchis", "cer", "banda", "nori luminosi", "varfuri", null }
                    : new[] { null, "praf", "cer inchis", "cer", "banda", "nori luminosi", "varfuri", null };
                Assert.Equal(labels, p.Select(q => q.Label).ToArray());
                int d = helper ? 2 : 1;
                if (helper)
                {
                    Assert.Equal(new CurvePoint(a.Dust * Thresholds.CurveDustHelperFraction, 0.0505 * Thresholds.CurveDustHelperFraction, "sub praf", true), p[1]);
                }
                // The dust's point is the move's at every strength; the held point and the sky map to themselves.
                Assert.Equal(new CurvePoint(a.Dust, 0.0505, "praf"), p[d]);
                Assert.Equal(new CurvePoint(ReferenceHold, ReferenceHold, "cer inchis", true), p[d + 1]);
                Assert.Equal(new CurvePoint(a.Sky, a.Sky, "cer"), p[d + 2]);
                Assert.True(moved[k].Curve.IsSuggestible);

                // Above the sky nothing changed, and the shadow chart still ends past the band.
                IReadOnlyList<CurvePoint> q0 = plain[k].Curve.Points;
                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(q0[3 + i].X, p[d + 3 + i].X, 12);
                    Assert.Equal(q0[3 + i].Y, p[d + 3 + i].Y, 9);
                }
                Assert.Equal(CurveAdvisor.ShadowChartLimit(plain[k].Curve), CurveAdvisor.ShadowChartLimit(moved[k].Curve), 12);
            }
        }
    }

    [Fact]
    public void R184_TheAimPutsTheDustOnPaperBlackLoweredOrLifted()
    {
        CurveAnchors a = ReferenceAnchors;
        double dust = ColorMath.LstarOfGrayLevel(a.Dust * 255);
        Assert.Equal(7.67, dust, 1);

        // Paper black under the dust (Dmax 2.4) and over it (Dmax 2.0): lowered, then lifted.
        foreach (double black in new[] { ColorMath.PaperBlackLstar(2.4), ColorMath.PaperBlackLstar(2.0) })
        {
            DustMove m = CurveAdvisor.AimDust(a, BandLabels, ReferenceHold, dust, black, GreyDust(a));
            Assert.NotNull(m);
            Assert.Equal(ReferenceHold, m.Hold, 12);
            Assert.False(m.Helper);
            Assert.False(m.StoppedShort);
            Assert.Equal(black < dust, m.DustOutput < a.Dust);

            IReadOnlyList<(string Name, ToneCurve Curve)> curves = CurveAdvisor.Curves(a, BandLabels, m);
            Assert.True(curves.Count >= 3);
            foreach (var (_, c) in curves)
            {
                Assert.True(c.IsSuggestible);
                // At paper black or a hair above it, never under.
                Assert.InRange(GreyDust(a)(c), black, black + 0.02);
                Assert.Equal(ReferenceHold, c.Map(ReferenceHold), 6);
                Assert.Equal(a.Sky, c.Map(a.Sky), 6);
            }
        }

        // A sky of one level: its dark end is its own point, and no point is added.
        DustMove flat = CurveAdvisor.AimDust(a, BandLabels, a.Sky, dust, ColorMath.PaperBlackLstar(2.4), GreyDust(a));
        Assert.NotNull(flat);
        Assert.True(double.IsNaN(flat.Hold));
        Assert.DoesNotContain(CurveAdvisor.Curves(a, BandLabels, flat)[0].Curve.Points, p => p.Extra && p.Label == "cer inchis");
    }

    [Fact]
    public void R184_AHelperPointIsAddedOnlyWhenTheSplineNeedsItAndTheMoveStopsWhereNoSplineIsLeft()
    {
        CurveAnchors a = ReferenceAnchors;
        double dust = ColorMath.LstarOfGrayLevel(a.Dust * 255);
        ToneCurve full = CurveAdvisor.Curves(a, BandLabels)[2].Curve;

        // A black of L* 2.6: the plain spline through the dust's point at that grey is not monotone near black.
        double black = 2.6, grey = ColorMath.GrayLevelOfLstar(black) / 255;
        var byHand = new List<CurvePoint> { new(0, 0, null), new(a.Dust, grey, "praf"), new(ReferenceHold, ReferenceHold, "cer inchis", true) };
        byHand.AddRange(full.Points.Skip(2));
        Assert.False(ToneCurve.WouldBeSuggestible(byHand));

        DustMove m = CurveAdvisor.AimDust(a, BandLabels, ReferenceHold, dust, black, GreyDust(a));
        Assert.NotNull(m);
        Assert.True(m.Helper);
        Assert.False(m.StoppedShort);
        foreach (var (_, c) in CurveAdvisor.Curves(a, BandLabels, m))
        {
            Assert.True(c.IsSuggestible);
            Assert.Equal(new CurvePoint(a.Dust * Thresholds.CurveDustHelperFraction, m.DustOutput * Thresholds.CurveDustHelperFraction, "sub praf", true), c.Points[1]);
            Assert.InRange(GreyDust(a)(c), black, black + 0.02);
        }

        // A black no spline reaches: the move stops short, as far as a suggestible spline goes.
        DustMove far = CurveAdvisor.AimDust(a, BandLabels, ReferenceHold, dust, 0.5, GreyDust(a));
        Assert.NotNull(far);
        Assert.True(far.StoppedShort);
        Assert.True(far.DustOutput < a.Dust);
        foreach (var (_, c) in CurveAdvisor.Curves(a, BandLabels, far))
        {
            Assert.True(c.IsSuggestible);
            Assert.InRange(GreyDust(a)(c), 0.5 + 0.1, dust - 1);
        }
    }

    [Fact]
    public void R184_TheMoveIsRefusedWithItsReason()
    {
        CurveAnchors a = ReferenceAnchors;
        double dust = ColorMath.LstarOfGrayLevel(a.Dust * 255), black = ColorMath.PaperBlackLstar(2.4);

        Assert.Null(CurveAdvisor.DustMoveProblem(CurvePurpose.Print, null, a, ReferenceHold, dust, black));
        Assert.Contains("doar pentru print", CurveAdvisor.DustMoveProblem(CurvePurpose.Screen, null, a, ReferenceHold, dust, black));

        // The dust's figure is the darkest sky: the sentence the run hands over, as it is, and before any other reason but the screen's.
        const string noZone = "nu pe imaginea asta: are prea putin praf";
        Assert.Equal(noZone, CurveAdvisor.DustMoveProblem(CurvePurpose.Print, noZone, a with { Dust = 0 }, a.Dust, 0, 14.5));
        Assert.Contains("doar pentru print", CurveAdvisor.DustMoveProblem(CurvePurpose.Screen, noZone, a, ReferenceHold, dust, black));

        // The dust at black, or not under the sky's dark end.
        Assert.Contains("praful e deja la negru complet", CurveAdvisor.DustMoveProblem(CurvePurpose.Print, null, a with { Dust = 0 }, ReferenceHold, 0, black));
        Assert.Contains("praful nu e mai inchis decat partea inchisa a cerului", CurveAdvisor.DustMoveProblem(CurvePurpose.Print, null, a, a.Dust, dust, black));

        // Paper black at or over the sky's dark end (L* 14.0): no room, with the two levels.
        string noRoom = CurveAdvisor.DustMoveProblem(CurvePurpose.Print, null, a, ReferenceHold, dust, 14.5);
        Assert.Contains("cerul insusi e prea aproape de negrul hartiei (partea lui inchisa la L* 14.0, negrul hartiei la L* 14.5)", noRoom);
        Assert.Contains("ridica fundalul cand refaci stretch-ul, cum spune tabul Tonuri", noRoom);

        // Within the tolerance of paper black: already there.
        Assert.Contains("praful e deja pe negrul hartiei (L* 3.6)", CurveAdvisor.DustMoveProblem(CurvePurpose.Print, null, a, ReferenceHold, black + 0.8 * Thresholds.CurveDustAimTolerance, black));
        Assert.Contains("praful e deja pe negrul hartiei (L* 3.6)", CurveAdvisor.DustMoveProblem(CurvePurpose.Print, null, a, ReferenceHold, black - 0.8 * Thresholds.CurveDustAimTolerance, black));
        Assert.Null(CurveAdvisor.DustMoveProblem(CurvePurpose.Print, null, a, ReferenceHold, black + 2 * Thresholds.CurveDustAimTolerance, black));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R184_AnImageWithoutADustZoneIsToldWhyInASentenceOfItsOwn(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        // A hundred patches, of which so many are dust; the darkest sky gives the dust's figure, or the dust zone does.
        SimulationResult With(int dustPatches, DustSource source) => new()
        {
            Map = new DustMap { Cols = 10, Rows = 10, Side = 8, Stride = 4, Classes = new PatchClass[100], Clouds = Array.Empty<DustCloud>(), NoiseThreshold = 1, DustSource = source },
            AsIsDust = new DustStats { DustPatchCount = dustPatches },
        };

        // The dust zone decides: nothing to say.
        Assert.Null(CurveTexts.DustMoveNoDustZone(With(10, DustSource.DustClass)));

        // No dust at all; too little of it, with its share and the share needed; and enough of it,
        // but with a part of the sky darker than the dust.
        Assert.Equal(
            Tr.Pick("not on this image: no dust was found in it, so there is nothing to set apart from the sky",
                    "nu pe imaginea asta: nu s-a gasit praf in ea, deci nu e nimic de desprins de cer"),
            CurveTexts.DustMoveNoDustZone(With(0, DustSource.DarkestSky)));
        Assert.Equal(
            Tr.Pick("not on this image: it has too little dust (3.0 % of the image, and at least 5 % is needed), so the darkest part of the sky is taken in its place",
                    "nu pe imaginea asta: are prea putin praf (3.0 % din imagine, si e nevoie de cel putin 5 %), asa ca in locul lui e luata partea cea mai inchisa a cerului"),
            CurveTexts.DustMoveNoDustZone(With(3, DustSource.DarkestSky)));
        Assert.Equal(
            Tr.Pick("not on this image: the darkest part of the sky is darker than the dust, so it is taken in the dust's place",
                    "nu pe imaginea asta: partea cea mai inchisa a cerului e mai inchisa decat praful, asa ca ea e luata in locul lui"),
            CurveTexts.DustMoveNoDustZone(With(10, DustSource.DarkestSky)));

        // The report's own reasons did not change with the sentences.
        Assert.Equal(Tr.Pick("no dust found", "nu s-a gasit praf"), With(0, DustSource.DarkestSky).DarkestSkyReason);
        Assert.StartsWith(Tr.Pick("too little dust: 3.0 % of the frame", "prea putin praf: 3.0 % din cadru"), With(3, DustSource.DarkestSky).DarkestSkyReason);
        Assert.StartsWith(Tr.Pick("darker than the 25 % threshold", "mai intunecat decat pragul de 25 %"), With(10, DustSource.DarkestSky).DarkestSkyReason);
        Assert.Null(With(10, DustSource.DustClass).DarkestSkyReason);
    }

    [Fact]
    public void R184_TheRunMovesTheDustForPrintWhenAskedAndOnlyThen()
    {
        AnalysisResult a = DustStripes(2.4);
        Assert.Equal(DustSource.DustClass, a.Simulation.Map.DustSource);
        double black = a.Simulation.PaperBlackLstar, dust = a.Simulation.AsIs.LstarDust;
        Assert.Equal(3.60, black, 2);
        Assert.Equal(7.7, dust, 1);

        // Not asked: today's curves, and the run still says the move can be had.
        CurveSet plain = Suggest(a);
        Assert.False(plain.DustOnPaperBlack);
        Assert.Null(plain.DustMoveRefusal);
        Assert.All(plain.Suggestions, s => Assert.Null(s.Evidence.Dust));
        Assert.All(plain.Suggestions, s => Assert.DoesNotContain(s.Curve.Points, p => p.Extra));
        Assert.Contains("Curbele nu muta praful si cerul", plain.Footnote);

        // Asked, for print: the dust lowered onto paper black, the sky where it was, the rest as before.
        CurveSet moved = Moved(a);
        Assert.Null(moved.Problem);
        Assert.True(moved.DustOnPaperBlack);
        Assert.Null(moved.DustMoveRefusal);
        Assert.Equal(plain.Suggestions.Select(s => s.Name).ToArray(), moved.Suggestions.Select(s => s.Name).ToArray());
        Assert.Contains("Praful e pus pe negrul hartiei", moved.Footnote);
        Assert.Contains("Curbele nu muta cerul", moved.Footnote);
        for (int i = 0; i < moved.Suggestions.Count; i++)
        {
            CurveSuggestion s = moved.Suggestions[i];
            CurvePoint point = Assert.Single(s.Curve.Points, p => p.Label == "praf");
            Assert.Equal(moved.Anchors.Dust, point.X, 12);
            Assert.True(point.Y < point.X);
            Assert.Equal(moved.Suggestions[0].Curve.Points.Single(p => p.Label == "praf"), point);   // the same at every strength
            Assert.True(s.Curve.IsSuggestible);

            DustMoveFigures f = s.Evidence.Dust;
            Assert.NotNull(f);
            Assert.Equal(dust, f.LstarBefore, 6);
            Assert.Equal(s.Evidence.LstarAfter[0], f.LstarAfter, 9);
            Assert.Equal(black, f.PaperBlackLstar, 9);
            Assert.Equal(2.4, f.Dmax, 9);
            Assert.False(f.StoppedShort);
            Assert.InRange(f.LstarAfter, black, black + 0.05);
            Assert.InRange(s.Evidence.LstarAfter[1] - s.Evidence.LstarBefore[1], -0.2, 0.2);

            // On paper the dust was above black before, so the difference grows by what the dust fell.
            double before = s.Evidence.LstarBefore[1] - dust, after = s.Evidence.LstarAfter[1] - Math.Max(f.LstarAfter, black);
            Assert.Equal(100 * (after / before - 1), f.PaperContrastChangePct, 6);
            Assert.True(f.PaperContrastChangePct > 20);
            Assert.True(f.DustNoiseChangePct > 0);
            // The sky's noise of a moved curve is the larger of the slope at the sky and the mean
            // slope over the sky's wide range, never the slope alone when the range says more.
            LstarRange wide = a.Simulation.AsIsDust.SkyWide;
            Assert.Equal(CurveAdvisor.MovedSkyNoiseGain(s.Curve, moved.Anchors.Sky, wide), s.Evidence.NoiseGain, 12);
            Assert.True(s.Evidence.NoiseGain >= s.Curve.Slope(moved.Anchors.Sky));
            Assert.Equal(a.Simulation.AsIs.PctPatchesBelowPaperBlack, f.PctUnderBlackBefore, 9);

            string evidence = string.Join("\n", CurveTexts.EvidenceLines(s));
            Assert.Contains($"praful pe negrul hartiei: de la L* 7.7 la {f.LstarAfter:0.0} (negrul hartiei e L* 3.6 la Dmax 2.4)", evidence);
            Assert.Contains($"pe hartie, diferenta dintre praf si cer creste cu {f.PaperContrastChangePct:0} %", evidence);
            Assert.Contains($"zgomotul din praf creste cu {f.DustNoiseChangePct:0} %", evidence);
            Assert.Contains("negru plin pe hartie (sub negrul hartiei): ", evidence);
            Assert.DoesNotContain("niveluri", string.Join("\n", CurveTexts.EvidenceLines(s).Where(l => !l.StartsWith("!"))));
        }

        // The screen has no paper black: asked or not, the plain curves, and the reason.
        CurveSet screen = Moved(a, CurvePurpose.Screen);
        Assert.False(screen.DustOnPaperBlack);
        Assert.Contains("doar pentru print", screen.DustMoveRefusal);
        for (int i = 0; i < plain.Suggestions.Count; i++)
            Assert.Equal(plain.Suggestions[i].Curve.Points, screen.Suggestions[i].Curve.Points);
        Assert.Contains("doar pentru print", CurveRun.ForScreen(plain).DustMoveRefusal);

        // Paper black above the dust (Dmax 2.0): lifted, and the history of a saved curve says what was done.
        CurveSet lifted = Moved(DustStripes(2.0));
        Assert.True(lifted.DustOnPaperBlack);
        CurveSuggestion up = lifted.Suggestions[0];
        Assert.True(up.Curve.Points.Single(p => p.Label == "praf").Y > lifted.Anchors.Dust);
        Assert.InRange(up.Evidence.Dust.LstarAfter, 8.99, 9.05);
        Assert.True(up.Evidence.Dust.PctUnderBlackBefore > 5);    // the dust column, 6 % of the patches, was under paper black
        Assert.Equal(0, up.Evidence.Dust.PctUnderBlackAfter, 9);  // and is on it now
        Assert.Contains(CurveTexts.History(up.Name, up.Curve, up.Evidence.Dust), l => l.Contains("praful pus pe negrul hartiei (L* 9.0, Dmax 2.0)"));
        Assert.All(CurveTexts.History(up.Name, up.Curve, up.Evidence.Dust), l => Assert.True(l.Length <= 72, l));
        Assert.DoesNotContain(CurveTexts.History(up.Name, up.Curve), l => l.Contains("negrul hartiei"));

        // A paper whose black is above the sky itself: refused, the plain curves, the reason for the box.
        AnalysisResult weak = DustStripes(1.2);
        CurveSet refused = Moved(weak);
        Assert.False(refused.DustOnPaperBlack);
        Assert.Contains("cerul insusi e prea aproape de negrul hartiei", refused.DustMoveRefusal);
        Assert.All(refused.Suggestions, s => Assert.DoesNotContain(s.Curve.Points, p => p.Extra));
        Assert.Equal(refused.DustMoveRefusal, Suggest(weak).DustMoveRefusal);
    }

    [Fact]
    public void R184_TheSkysNoiseOfAMovedCurveIsNotReadAtTheBendOfTheSkysPoint()
    {
        // The reference FITS at Dmax 2.4 (V-017): the dust's point at 0.0322 with the helper, the sky's wide range 14.01 to 19.99.
        CurveAnchors a = ReferenceAnchors;
        var wide = new LstarRange(14.01, 19.99);
        double lo = ColorMath.GrayLevelOfLstar(wide.Low) / 255, hi = ColorMath.GrayLevelOfLstar(wide.High) / 255;
        ToneCurve lowered = CurveAdvisor.Curves(a, BandLabels, new DustMove { DustOutput = 0.0322, Hold = ReferenceHold, Helper = true })[2].Curve;
        ToneCurve plain = CurveAdvisor.Curves(a, BandLabels)[2].Curve;

        // The lowered dust bends the curve at the sky's anchor: the slope there says the sky is
        // flattened by a fifth, while across the sky's range the curve rises about one for one.
        double mean = (lowered.Map(hi) - lowered.Map(lo)) / (hi - lo);
        Assert.InRange(lowered.Slope(a.Sky), 0.7, 0.9);
        Assert.InRange(mean, 0.95, 1.1);
        Assert.Equal(mean, CurveAdvisor.MovedSkyNoiseGain(lowered, a.Sky, wide), 12);

        // Where the slope at the anchor is the larger, it stays the figure; and a range without width has only it.
        Assert.True(plain.Slope(a.Sky) > (plain.Map(hi) - plain.Map(lo)) / (hi - lo));
        Assert.Equal(plain.Slope(a.Sky), CurveAdvisor.MovedSkyNoiseGain(plain, a.Sky, wide), 12);
        Assert.Equal(lowered.Slope(a.Sky), CurveAdvisor.MovedSkyNoiseGain(lowered, a.Sky, new LstarRange(17.58, 17.58)), 12);
    }

    [Fact]
    public void R143_AMovedCurveIsCheckedOnTheSkyAloneUnlessItsDustIsStillUnderPaperBlack()
    {
        // The stripes on a paper whose black (L* 9.0) is above the dust (7.7): as it is, the dust is under paper black.
        AnalysisResult a = DustStripes(2.0);
        SimulationResult sim = a.Simulation;
        double black = sim.PaperBlackLstar;
        Assert.True(sim.DustBelowPaperBlack);
        bool skyLow = sim.SkyBelowVisibility;
        CurveEvidence With(DustMoveFigures? dust) => new() { NoiseGain = 1, NoiseAfterLsb = double.NaN, Dust = dust };
        var reached = new DustMoveFigures { LstarBefore = 7.7, LstarAfter = black, PaperBlackLstar = black, Dmax = 2.0 };
        DustMoveFigures stopped = reached with { LstarAfter = black - 2, StoppedShort = true };

        // Without the move: the check that sends to the stretch, for the dust.
        Recommendation plain = Assert.Single(CurveTexts.PaperChecks(sim, a.Noise, With(null)), r => r.Topic == Topic.Background);
        Assert.EndsWith("Curba nu muta cerul si praful.", plain.Text);

        // The dust put on paper black: nothing left to say about it; the sky alone decides.
        IReadOnlyList<Recommendation> moved = CurveTexts.PaperChecks(sim, a.Noise, With(reached));
        Assert.Equal(skyLow, moved.Any(r => r.Topic == Topic.Background));
        Assert.All(moved, r => Assert.DoesNotContain("praful la L*", r.Because));

        // A move that stopped with the dust still under paper black: the check stays, with where the dust got.
        Recommendation left = Assert.Single(CurveTexts.PaperChecks(sim, a.Noise, With(stopped)), r => r.Topic == Topic.Background);
        Assert.EndsWith("Curba nu muta cerul.", left.Text);
        Assert.Contains($"praful la L* {black - 2:0.0} dupa curba", left.Because);
        // This sky is not too dark (L* 15.0): the because names the dust alone, not a sky that is fine.
        Assert.False(skyLow);
        Assert.StartsWith("praful la L*", left.Because);
        Assert.DoesNotContain("cerul la L*", left.Because);
        Assert.Contains($"negrul hartiei e L* {black:0.0} la Dmax 2.0", left.Because);

        // What decides is where the dust measures, to the tolerance, not the flag.
        DustMoveFigures hair = reached with { LstarAfter = black - 0.8 * Thresholds.CurveDustAimTolerance, StoppedShort = true };
        DustMoveFigures under = reached with { LstarAfter = black - 2 * Thresholds.CurveDustAimTolerance };
        Assert.Equal(skyLow, CurveTexts.PaperChecks(sim, a.Noise, With(hair)).Any(r => r.Topic == Topic.Background));
        Assert.Contains("dupa curba", Assert.Single(CurveTexts.PaperChecks(sim, a.Noise, With(under)), r => r.Topic == Topic.Background).Because);

        // And neither the saved file nor the sentence under the evidence says the dust is there.
        CurveSuggestion s = Moved(a).Suggestions[0];
        Assert.Contains(CurveTexts.History(s.Name, s.Curve, reached), l => l.StartsWith("praful pus pe negrul hartiei (L* 9.0, Dmax 2.0)"));
        Assert.Contains(CurveTexts.History(s.Name, s.Curve, stopped), l => l.StartsWith("praful mutat spre negrul hartiei (L* 9.0, Dmax 2.0)"));
        Assert.Contains("Praful e pus pe negrul hartiei", CurveTexts.Footnote(true));
        Assert.Contains("Praful e mutat spre negrul hartiei, cat poate curba", CurveTexts.Footnote(true, stoppedShort: true));
        CurveSuggestion shortOne = s with { Evidence = s.Evidence with { Dust = stopped } };
        Assert.Contains(CurveTexts.EvidenceLines(shortOne), l => l.EndsWith("; mai departe de atat curba n-ar mai urca lin"));

        // The lifted dust reads as the lowered one does.
        Assert.False(s.Evidence.Dust.StoppedShort);
        Assert.Contains("praful pe negrul hartiei: de la L* 7.7 la 9.0 (negrul hartiei e L* 9.0 la Dmax 2.0)", CurveTexts.EvidenceLines(s));
        Assert.Contains(CurveTexts.EvidenceLines(s), l => l.StartsWith("zgomotul din praf scade cu "));
    }

    [Fact]
    public void R143_TheNoiseCheckIsSilentWhenTheImageWasOverTheTargetBeforeTheCurve()
    {
        // The reference FITS: 2.04 levels as it is, over the print target's 1.5 before any curve.
        AnalysisResult a = Band();
        var over = new NoiseResult { SmoothTiles = Thresholds.NoiseMinTiles, SigmaMedianLsb = 2.04 };
        CurveEvidence With(NoiseResult n, double gain) => new() { NoiseGain = gain, NoiseAfterLsb = n.SigmaMedianLsb * gain };

        // A curve that lowers the sky's noise, and one that raises it: neither is why it is over.
        Assert.DoesNotContain(CurveTexts.PaperChecks(a.Simulation, over, With(over, 0.95)), r => r.Topic == Topic.Denoise);
        Assert.DoesNotContain(CurveTexts.PaperChecks(a.Simulation, over, With(over, 1.26)), r => r.Topic == Topic.Denoise);

        // At the target's upper bound before, over it after: the curve is the cause, and a gentler one helps.
        var edge = new NoiseResult { SmoothTiles = Thresholds.NoiseMinTiles, SigmaMedianLsb = Thresholds.NoisePrintHiLsb };
        Recommendation check = Assert.Single(CurveTexts.PaperChecks(a.Simulation, edge, With(edge, 1.1)), r => r.Topic == Topic.Denoise);
        Assert.Contains("alege o varianta mai blanda", check.Text);
        Assert.DoesNotContain(CurveTexts.PaperChecks(a.Simulation, edge, With(edge, 1.0)), r => r.Topic == Topic.Denoise);
    }

    [Fact]
    public void R184_ADustThatEndsWithinTheToleranceOfPaperBlackHasArrived()
    {
        CurveAnchors a = ReferenceAnchors;
        double dust = ColorMath.LstarOfGrayLevel(a.Dust * 255);

        // Paper blacks ever lower, until the aimed move is no longer whole: the first one cut is
        // cut by a hair, and a dust that close to paper black is on it.
        DustMove? cut = null;
        double black = 0, after = 0;
        for (double b = 2.6; b > 0.5 && cut == null; b -= 0.01)
        {
            DustMove m = CurveAdvisor.AimDust(a, BandLabels, ReferenceHold, dust, b, GreyDust(a));
            double landed = ColorMath.LstarOfGrayLevel(m.DustOutput * 255);
            if (landed > b + 0.005) { cut = m; black = b; after = landed; }
        }
        Assert.NotNull(cut);
        Assert.InRange(after - black, 0.005, Thresholds.CurveDustAimTolerance);
        Assert.False(cut!.StoppedShort);

        // Further than the tolerance from paper black, it stopped short.
        DustMove far = CurveAdvisor.AimDust(a, BandLabels, ReferenceHold, dust, black - 1, GreyDust(a));
        Assert.True(ColorMath.LstarOfGrayLevel(far.DustOutput * 255) - (black - 1) > Thresholds.CurveDustAimTolerance);
        Assert.True(far.StoppedShort);
    }

    [Fact]
    public void R184_OnASkyWithItsOwnSpreadTheRunHoldsItsDarkEndAndAddsTheHelperWhenItIsNeeded()
    {
        // Paper black at L* 3.6: the dust lowered, the sky's dark end a point of its own.
        AnalysisResult a = AnalysisRun.Execute(DustStripeSceneWithASpreadSky(), "memorie", new Parameters { DmaxOverride = 2.4 });
        Assert.Equal(DustSource.DustClass, a.Simulation.Map.DustSource);
        LstarRange wide = a.Simulation.AsIsDust.SkyWide;
        Assert.True(wide.High - wide.Low > 1, "the sky has no spread");
        CurveSet moved = Moved(a);
        Assert.True(moved.DustOnPaperBlack, moved.DustMoveRefusal);
        double hold = ColorMath.GrayLevelOfLstar(wide.Low) / 255;
        Assert.InRange(hold, moved.Anchors.Dust + 0.01, moved.Anchors.Sky - 0.001);
        foreach (CurveSuggestion s in moved.Suggestions)
        {
            CurvePoint held = Assert.Single(s.Curve.Points, q => q.Extra && q.Label == "cer inchis");
            Assert.Equal(hold, held.X, 9);
            Assert.Equal(held.X, held.Y);
            CurvePoint dust = s.Curve.Points.Single(q => q.Label == "praf");
            // The dust's noise is the stretch from the dust's point to the held one, not to the sky's.
            Assert.Equal(100 * ((hold - dust.Y) / (hold - dust.X) - 1), s.Evidence.Dust.DustNoiseChangePct, 6);
            // And the sky's noise takes the mean slope over the sky's range when that is the larger.
            double lo = ColorMath.GrayLevelOfLstar(wide.Low) / 255, hi = ColorMath.GrayLevelOfLstar(wide.High) / 255;
            double mean = (s.Curve.Map(hi) - s.Curve.Map(lo)) / (hi - lo);
            Assert.Equal(Math.Max(s.Curve.Slope(moved.Anchors.Sky), mean), s.Evidence.NoiseGain, 9);
            Assert.InRange(s.Evidence.Dust.LstarAfter, a.Simulation.PaperBlackLstar, a.Simulation.PaperBlackLstar + 0.05);
            Assert.InRange(s.Evidence.LstarAfter[1] - s.Evidence.LstarBefore[1], -0.3, 0.3);
        }

        // A blacker paper (L* 2.3): the spline needs the point under the dust, and gets it.
        AnalysisResult deep = AnalysisRun.Execute(DustStripeSceneWithASpreadSky(), "memorie", new Parameters { DmaxOverride = 2.6 });
        CurveSet helped = Moved(deep);
        Assert.True(helped.DustOnPaperBlack, helped.DustMoveRefusal);
        foreach (CurveSuggestion s in helped.Suggestions)
        {
            Assert.True(s.Curve.IsSuggestible);
            CurvePoint dust = s.Curve.Points.Single(q => q.Label == "praf");
            CurvePoint helper = Assert.Single(s.Curve.Points, q => q.Extra && q.Label == "sub praf");
            Assert.Equal(dust.X * Thresholds.CurveDustHelperFraction, helper.X, 12);
            Assert.Equal(dust.Y * Thresholds.CurveDustHelperFraction, helper.Y, 12);
            Assert.Single(s.Curve.Points, q => q.Extra && q.Label == "cer inchis");
            Assert.Equal(5, s.Evidence.LstarAfter.Count);
        }
    }

    [Fact]
    public void R143_TheNoiseInTheSkyIsWrittenAsAPercentage()
    {
        CurveSuggestion s = Suggest(Band()).Suggestions[2];
        CurveSuggestion With(double gain) => s with { Evidence = s.Evidence with { NoiseGain = gain } };
        Assert.Contains("zgomotul din cer creste cu 26 %", CurveTexts.EvidenceLines(With(1.26)));
        Assert.Contains("zgomotul din cer scade cu 14 %", CurveTexts.EvidenceLines(With(0.86)));
        Assert.Contains("zgomotul din cer creste cu 0 %", CurveTexts.EvidenceLines(With(1.0)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R185_TheDustMovesTextsAreInPlainWords(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        CurveAnchors a = ReferenceAnchors;
        var texts = new List<string>();
        CurveSet moved = Moved(DustStripes(2.4));
        texts.Add(moved.Footnote);
        texts.Add(Suggest(DustStripes(2.4)).Footnote);
        foreach (CurveSuggestion s in moved.Suggestions)
        {
            texts.AddRange(CurveTexts.EvidenceLines(s));
            texts.AddRange(CurveTexts.History(s.Name, s.Curve, s.Evidence.Dust));
            texts.AddRange(s.Curve.Points.Select(p => p.Label));
        }
        texts.Add(CurveAdvisor.DustMoveProblem(CurvePurpose.Screen, null, a, ReferenceHold, 7.7, 3.6));
        foreach (int dustPatches in new[] { 0, 3, 10 })
            texts.Add(CurveTexts.DustMoveNoDustZone(new SimulationResult
            {
                Map = new DustMap { Cols = 10, Rows = 10, Side = 8, Stride = 4, Classes = new PatchClass[100], Clouds = Array.Empty<DustCloud>(), NoiseThreshold = 1, DustSource = DustSource.DarkestSky },
                AsIsDust = new DustStats { DustPatchCount = dustPatches },
            }));
        texts.Add(CurveAdvisor.DustMoveProblem(CurvePurpose.Print, null, a with { Dust = 0 }, ReferenceHold, 0, 3.6));
        texts.Add(CurveAdvisor.DustMoveProblem(CurvePurpose.Print, null, a, a.Dust, 7.7, 3.6));
        texts.Add(CurveAdvisor.DustMoveProblem(CurvePurpose.Print, null, a, ReferenceHold, 7.7, 14.5));
        texts.Add(CurveAdvisor.DustMoveProblem(CurvePurpose.Print, null, a, ReferenceHold, 3.62, 3.6));
        texts.Add(CurveTexts.DustMoveNotSmooth);
        var stopped = new DustMoveFigures { LstarBefore = 3.0, LstarAfter = 7.0, PaperBlackLstar = 9.0, Dmax = 2.0, StoppedShort = true };
        CurveSuggestion shortOne = moved.Suggestions[0] with { Evidence = moved.Suggestions[0].Evidence with { Dust = stopped } };
        IReadOnlyList<string> shortHistory = CurveTexts.History(shortOne.Name, shortOne.Curve, stopped);
        Assert.All(shortHistory, l => Assert.True(l.Length <= 72, l));
        var shortTexts = new List<string> { CurveTexts.Footnote(true, stoppedShort: true) };
        shortTexts.AddRange(CurveTexts.EvidenceLines(shortOne));
        shortTexts.AddRange(shortHistory);
        shortTexts.AddRange(CurveTexts.PaperChecks(DustStripes(2.0).Simulation, null, shortOne.Evidence).Select(r => r.Text + " " + r.Because));
        Assert.Contains(shortTexts, t => t.Contains(english ? "after the curve" : "dupa curba"));
        Assert.Contains(shortTexts, t => t.Contains(english ? "moved towards" : "mutat spre"));
        foreach (DustMethod method in new[] { DustMethod.Band, DustMethod.Sky })
            texts.Add(CurveTexts.PointsNote(method, true, heldRow: true, helperRow: true));
        texts.AddRange(shortTexts);

        // The curve window's note explains a row only on a curve that has it.
        string heldWord = english ? "held in place" : "tinuta pe loc", helperWord = english ? "smooth towards black" : "lina spre negru";
        Assert.DoesNotContain(heldWord, CurveTexts.PointsNote(DustMethod.Band, true));
        Assert.DoesNotContain(helperWord, CurveTexts.PointsNote(DustMethod.Band, true));
        Assert.Contains(heldWord, CurveTexts.PointsNote(DustMethod.Band, true, heldRow: true));
        Assert.DoesNotContain(helperWord, CurveTexts.PointsNote(DustMethod.Band, true, heldRow: true));
        Assert.Contains(helperWord, CurveTexts.PointsNote(DustMethod.Band, true, helperRow: true));
        Assert.DoesNotContain(heldWord, CurveTexts.PointsNote(DustMethod.Band, true, helperRow: true));
        // Every refusal and both footnotes are there to be read: none came back empty.
        Assert.Equal(2, texts.Take(2).Count(t => !string.IsNullOrWhiteSpace(t)));
        Assert.All(texts.Skip(texts.Count - shortTexts.Count - 11), t => Assert.False(string.IsNullOrWhiteSpace(t)));

        var methodWords = new System.Text.RegularExpressions.Regex(
            @"\b(ancor\w*|anchors?|percentil\w*|median\w*|LSB|panta|slope|clas[ae]|clasele|class|classes|modelat|modelled|vizibilitate|visibility|plafon|ceiling|crescatoare|increasing|codarea|encoding|gamutul hartiei|paper gamut|control points|puncte de control)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.All(texts.Where(t => t != null), t => Assert.False(methodWords.IsMatch(t), t));
    }
}
