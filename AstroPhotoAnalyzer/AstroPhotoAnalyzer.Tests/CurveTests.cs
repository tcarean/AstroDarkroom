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
/// R-140 to R-145: the suggested curves (ADR 0031). The spline against values computed with
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
    /// A frame without a WCS, in vertical stripes: dust 0.05 (6 % of the frame), sky 0.15, a band
    /// at 0.3 from 78 % to 96 % of the pixels, clouds at 0.5 up to 99.6 %, peaks at 0.7 above.
    /// </summary>
    static Raster StripeScene()
        => SyntheticRasters.From(512, 512, 3, (x, y, p) => x < 32 ? 0.05f : x < 400 ? 0.15f : x < 490 ? 0.3f : x < 508 ? 0.5f : 0.7f);

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

        // The spline through these points passes 1 between them: the curve is clamped there.
        ToneCurve over = Curve((0, 0), (0.5, 0.98), (0.6, 0.99), (1, 1));
        for (double v = 0; v <= 1; v += 0.01) Assert.InRange(over.Map(v), 0, 1);

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

    [Fact]
    public void R141_WithTheClassesEveryAnchorComesFromThemAndTheyIncrease()
    {
        AnalysisResult a = Band();
        CurveSet set = Suggest(a);

        Assert.Null(set.Problem);
        Assert.Equal(CurveAnchorSource.Classes, set.Source);
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
    public void R141_WithoutTheClassesTheUpperAnchorsAreTheFramesPercentilesAndABadOrderIsAProblem()
    {
        CurveSet set = Suggest(AnalysisRun.Execute(StripeScene(), "memorie", Scenes.OnBuiltForPaper()));
        Assert.Equal(CurveAnchorSource.FrameHistogram, set.Source);
        Assert.Null(set.Problem);
        Assert.Equal(0.15, set.Anchors.Sky, 3);
        Assert.Equal(0.3, set.Anchors.Band, 3);
        Assert.Equal(0.5, set.Anchors.Clouds, 3);
        Assert.Equal(0.7, set.Anchors.Peaks, 3);

        // A flat frame has one level: the anchors cannot increase, and nothing is suggested.
        CurveSet flat = Suggest(AnalysisRun.Execute(SyntheticRasters.Flat(64, 64, 0.2f), "memorie", Scenes.OnBuiltForPaper()));
        Assert.NotNull(flat.Problem);
        Assert.Empty(flat.Suggestions);
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

            // Maxima exists exactly when Puternica leaves the peaks under the target, and reaches it.
            bool under = Thresholds.CurvePeakTarget - full[5].Y > Thresholds.CurveMaximaMinGain;
            Assert.Equal(under ? 4 : 3, set.Suggestions.Count);
            if (under)
            {
                Assert.Equal("Maxima", set.Suggestions[3].Name);
                Assert.Equal(Thresholds.CurvePeakTarget, set.Suggestions[3].Curve.Points[5].Y, 9);
            }
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
        GamutFigure g = GamutAnalyzer.Measure(table, sub, map, byClass: true);
        Assert.Equal(50, g.PctAll, 9);
        Assert.Equal(new[] { 100.0, 100.0, 0.0, double.NaN, double.NaN }, g.PctByClass);
        Assert.Null(GamutAnalyzer.Measure(table, sub, map, byClass: false).PctByClass);

        // A mono image has no chroma to lose.
        Assert.Equal(0, GamutAnalyzer.Measure(table, SyntheticRasters.Flat(16, 16, 0.5f, planes: 1), map, byClass: true).PctAll);
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
