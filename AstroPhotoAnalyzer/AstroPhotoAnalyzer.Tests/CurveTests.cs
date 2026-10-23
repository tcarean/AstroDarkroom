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
        Assert.Contains("clase relative la cerul modelat (fara WCS)", set.SourceSentence);
        Assert.Contains("nebuloasa la percentila 80 %", set.SourceSentence);
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
        Assert.Contains("clasa \"nebuloasa stralucitoare\" nu are nicio dala", empty.Problem);
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
        Assert.Contains("ancora \"varfuri\" e deja la 0.950", set.Problem);
        Assert.Contains($"tinta {Thresholds.CurvePeakTarget:0.00}", set.Problem);
        Assert.Contains("la plafon", set.Problem);
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
        Assert.False(header.BottomUp);
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
        Assert.Contains("nu incap", e.Message);
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
        Assert.Contains("1.40 LSB x 1.20", check.Because);
        Assert.Contains("1.68 LSB", check.Because);
        Assert.Contains($"{Thresholds.NoisePrintHiLsb:0.0} LSB", check.Because);
        Assert.DoesNotContain(CurveTexts.PaperChecks(a.Simulation, few, With(few)), r => r.Topic == Topic.Denoise);       // not enough flat sky: no figure, no check
    }

    [Fact]
    public void R144_TheGamutTableIsInterpolatedAndCountedPerClassOfThePatch()
    {
        // A hand-built table on a 2-cube: only the corner of pure red is displaced, by 12 in a*.
        var table = new GamutTable { N = 2, Da = new float[8], Db = new float[8] };
        table.Da[(1 * 2 + 0) * 2 + 0] = 12;
        Assert.True(GamutAnalyzer.Outside(table, 1, 0, 0));
        Assert.False(GamutAnalyzer.Outside(table, 0, 0, 0));
        Assert.False(GamutAnalyzer.Outside(table, 0, 1, 0));
        Assert.False(GamutAnalyzer.Outside(table, 0.2f, 0, 0));    // 12 x 0.2 = 2.4, under the threshold of 3
        Assert.True(GamutAnalyzer.Outside(table, 0.3f, 0, 0));     // 3.6
        Assert.False(GamutAnalyzer.Outside(table, 1, 0.8f, 0));    // 12 x 0.2 = 2.4

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
            Assert.StartsWith("gamutul hartiei: necunoscut - ", s.Evidence.GamutNote);
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
}
