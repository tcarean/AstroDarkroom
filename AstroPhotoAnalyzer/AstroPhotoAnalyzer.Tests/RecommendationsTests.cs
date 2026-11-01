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

public class RecommendationsTests
{
    static AnalysisResult Run(Raster r, Parameters? p = null)
        => AnalysisRun.Execute(r, "memorie", Scenes.OnBuiltForPaper(p));

    static IEnumerable<Recommendation> Of(AnalysisResult a, Topic t) => a.Recommendations.Where(x => x.Topic == t);

    [Fact]
    public void R090_ACleanImageGetsAnOverallOk()
    {
        // A 512 px scene has ~170 px of real detail: it is sharp for print only at
        // 10 cm viewed from 3 m. The test is about the other rules, not about the size.
        var a = Run(Scenes.CleanSky(), new Parameters { ViewingDistanceM = 3.0, CandidateWidthsCm = new double[] { 10 } });

        Assert.Equal(Verdict.Ok, a.Overall);
        Assert.All(a.Recommendations, r => Assert.Equal(Verdict.Ok, r.Verdict));
        Assert.Contains(Of(a, Topic.Background), r => r.Text.Contains("print-safe"));
        Assert.Contains(Of(a, Topic.Denoise), r => r.Text.Contains("exact cat trebuie"));
    }

    [Fact]
    public void R092_TheDarkSkyRaisesEveryFlagTheRealImageDid()
    {
        var a = Run(Scenes.DarkSky(), new Parameters { TargetAspect = 50.0 / 35.0, ViewingDistanceM = 1.5 });

        Assert.Equal(Verdict.Problem, a.Overall);
        Assert.Contains(Of(a, Topic.Background), r => r.Verdict == Verdict.Problem && r.Text.Contains("Refa stretch-ul"));
        // The scene's two levels of green are a chroma of 1.7, no cast since ADR 0057, as on
        // the real image (1.0); the cast itself is tested on R093_AFiveLevelGreenSkyIsACast.
        Assert.Contains(Of(a, Topic.Colour), r => r.Verdict == Verdict.Ok && r.Text.Contains("Fundalul e neutru") && r.Because.Contains("(croma): 1."));
        Assert.Contains(Of(a, Topic.Colour), r => r.Verdict == Verdict.Attention && r.Text.StartsWith("Colt dreapta-jos"));
        Assert.Contains(Of(a, Topic.Size), r => r.Verdict == Verdict.Attention && r.Text.Contains("decupaj"));
        Assert.Contains(Of(a, Topic.Denoise), r => r.Verdict == Verdict.Attention && r.Text.Contains("prea puternic"));
        // The 16-bit master and the q100 floor are facts of the Export tab since ADR 0061, not
        // warnings: neither line is in the report, though the scene raises both measurements.
        Assert.True(a.Quantization.Keep16BitMaster);
        Assert.DoesNotContain(Of(a, Topic.Warnings), r => r.Text.Contains("originalul pe 16 biti"));
        Assert.DoesNotContain(Of(a, Topic.Format), r => r.Text.StartsWith("Nu cobori"));
    }

    [Fact]
    public void R090_EveryRecommendationCarriesItsEvidenceAndTopicsAreInDecisionOrder()
    {
        var a = Run(Scenes.DarkSky());

        Assert.All(a.Recommendations, r => Assert.False(string.IsNullOrWhiteSpace(r.Because)));
        Assert.Equal(a.Recommendations.Select(r => r.Topic).OrderBy(t => t), a.Recommendations.Select(r => r.Topic));
        Assert.Throws<ArgumentException>(() => new Recommendation(Topic.Format, Verdict.Ok, "x", ""));
    }

    [Fact]
    public void R096_AnUpscaledFileAndAMissingProfileAreWarnings()
    {
        Raster r = SyntheticRasters.Upscaled8(Scenes.CleanSky());
        r.Source = new SourceInfo { Format = "memorie", BitDepth = 16, IsFloat = false, PlanesInFile = 3 };

        var a = Run(r);

        Assert.Contains(Of(a, Topic.Warnings), x => x.Text.Contains("8 biti de informatie, salvati ca 16 biti"));
        Assert.Contains(Of(a, Topic.Warnings), x => x.Text.Contains("Fisierul nu are profil de culoare (ICC)"));
        Assert.Equal(Verdict.Attention, Of(a, Topic.Warnings).Max(x => x.Verdict));
    }

    [Fact]
    public void R096_ANonSrgbProfileIsNamedInTheWarning()
    {
        Raster r = Scenes.CleanSky();
        r.Source = r.Source! with { IccDescription = "ProPhoto RGB" };

        var a = Run(r);

        Assert.Contains(Of(a, Topic.Warnings), x => x.Text.Contains("ProPhoto RGB"));
    }

    [Fact]
    public void R093_AFiveLevelGreenSkyIsACastNamedByItsDirection()
    {
        // A flat sky at 0.14 with five levels of green: a* -3.5, b* +2.5, chroma 4.3, so an
        // Attention named green by the larger of the two level figures (ADR 0057).
        var a = Run(SyntheticRasters.Flat(256, 256, 0.14f, 0.14f + 5f / 255, 0.14f));

        Recommendation cast = Assert.Single(Of(a, Topic.Colour), r => r.Verdict == Verdict.Attention);
        Assert.StartsWith("Fundalul are o tenta verde", cast.Text);
        Assert.Contains("cat de departe e de un gri (croma): 4.3, prag 3.0, depasit (a* -3.5, b* +2.5)", cast.Because);
        Assert.Contains("verdele iese peste rosu si albastru cu +5.0 niveluri", cast.Because);
    }

    [Fact]
    public void R093_ACornerDarkerThanTheSkyIsNamedADarkeningNotAGlow()
    {
        // The top-left corner of the 2026-08-12 core, in miniature: brown, under the sky on
        // every channel, the blue missing most. Its patches are dust, so the region is measured
        // on every pixel; the departure is coloured, and the words must say "darker", not
        // "horizon glow".
        const float sky = 0.14f;
        Raster r = SyntheticRasters.From(320, 320, 3, (x, y, p) =>
            y < 48 && x < 64 ? (p == 0 ? sky - 8f / 255 : p == 1 ? sky - 7f / 255 : sky - 12f / 255) : sky);
        var a = Run(r);

        Recommendation corner = Assert.Single(Of(a, Topic.Colour), x => x.Verdict == Verdict.Attention);
        Assert.StartsWith("Colt stanga-sus: mai intunecat si mai brun decat cerul (albastrul lipseste cel mai mult). Poate fi praf la marginea cadrului", corner.Text);
        Assert.DoesNotContain("orizont", corner.Text);
        Assert.Contains("R -8.0, G -7.0, B -12.0", corner.Because);
    }

    [Fact]
    public void R093_TheBottomEdgeUnderTheSkyWithTheBlueMissingIsStillTheHorizon()
    {
        // The bottom edge of the 2026-08-12 core: under the sky on every channel (-0.6 / -2.2 /
        // -4.5), the blue missing most. On the bottom row that is the horizon's signature, so
        // it keeps its name; the same colours in a top corner would be a darkening.
        const float sky = 0.14f;
        Raster r = SyntheticRasters.From(320, 320, 3, (x, y, p) =>
            y >= 272 && x >= 64 && x < 256 ? (p == 0 ? sky - 1f / 255 : p == 1 ? sky - 2f / 255 : sky - 5f / 255) : sky);
        var a = Run(r);

        Recommendation edge = Assert.Single(Of(a, Topic.Colour), x => x.Verdict == Verdict.Attention);
        Assert.StartsWith("Latura de jos: mai cald decat restul cerului (lumina de la orizont sau poluare luminoasa). Corecteaz-o pe datele liniare", edge.Text);
    }

    [Fact]
    public void R093_AnEdgeWithTheCornersColourAndLessOfItIsTheCornersExtension()
    {
        // The horizon of the 2026-08-12 core: the bottom-right corner warm (R +10, B -8), the
        // bottom edge warm but less (R +4, B -3). One line, the corner's, naming the edge as its
        // reach, with the edge's figures in the because (ADR 0058).
        const float sky = 0.14f;
        Raster r = SyntheticRasters.From(320, 320, 3, (x, y, p) =>
            y >= 272 && x >= 256 ? (p == 0 ? sky + 10f / 255 : p == 2 ? sky - 8f / 255 : sky)
            : y >= 272 && x >= 64 ? (p == 0 ? sky + 4f / 255 : p == 2 ? sky - 3f / 255 : sky) : sky);
        var a = Run(r);

        Recommendation corner = Assert.Single(Of(a, Topic.Colour), x => x.Verdict == Verdict.Attention);
        Assert.StartsWith("Colt dreapta-jos: mai cald decat restul cerului (lumina de la orizont sau poluare luminoasa), intins si pe latura de jos. Corecteaz-o", corner.Text);
        Assert.Contains("; latura de jos: R +4.0, G 0.0, B -3.0", corner.Because);
    }

    [Fact]
    public void R093_AnEdgeThatDepartsMoreThanItsCornerKeepsItsOwnLine()
    {
        // The same colours the other way round: the edge departs more than the corner, so it
        // is not the corner's reach, and both have their line.
        const float sky = 0.14f;
        Raster r = SyntheticRasters.From(320, 320, 3, (x, y, p) =>
            y >= 272 && x >= 256 ? (p == 0 ? sky + 4f / 255 : p == 2 ? sky - 3f / 255 : sky)
            : y >= 272 && x >= 64 ? (p == 0 ? sky + 10f / 255 : p == 2 ? sky - 8f / 255 : sky) : sky);
        var a = Run(r);

        var lines = Of(a, Topic.Colour).Where(x => x.Verdict == Verdict.Attention).Select(x => x.Text).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, t => t.StartsWith("Colt dreapta-jos: mai cald decat restul cerului (lumina de la orizont sau poluare luminoasa). "));
        Assert.Contains(lines, t => t.StartsWith("Latura de jos: mai cald decat restul cerului (lumina de la orizont sau poluare luminoasa). "));
    }

    [Fact]
    public void R091_TheBandingLineSaysWhereThePlateauWasMeasured()
    {
        // The dark scene's plateau is measured on every clean tile among the 5000 positions (419
        // of them): the because gives the share of the frame with a visible step and the
        // threshold, then the typical plateau with the tiles, their share of the frame and the
        // noise left on them, so that the reader can tell it apart from the denoise section's
        // median, taken on other tiles.
        var a = Run(Scenes.DarkSky(), new Parameters { TargetAspect = 50.0 / 35.0, ViewingDistanceM = 1.5 });

        Recommendation banding = Assert.Single(Of(a, Topic.Format), r => r.Text.StartsWith("Pe 8 biti pot aparea trepte vizibile in cer (banding)"));
        // The share and the threshold carry Fmt.Strong's marks, bold in the window's Raport tab
        // and stripped from the text report (R-112).
        Assert.Matches(@"^la \d+ dpi, zonele plate, fara nicio trecere de ton, mai late de 1\.0 mm acopera \*\*\d+\.\d\d %\*\* din cadru \(prag \*\*0\.10 %\*\*\); in cele \d+ (de )?zone mici de cer neted \(\d+\.\d % din cadru\), o zona plata are de obicei \d+\.\d px = \d+\.\d\d mm, iar zgomotul ramas acolo e de \d+\.\d\d niveluri$", banding.Because);
        Assert.DoesNotContain("**", a.ReportBody);
    }

    [Fact]
    public void R095_TheDenoiseBecauseAlwaysTellsTheShareOfTilesUnderTheBandingSigma()
    {
        // On a clean sky no tile is under 0.5 LSB, and the because says so all the same.
        var a = Run(Scenes.CleanSky());

        Recommendation denoise = Assert.Single(Of(a, Topic.Denoise));
        Assert.Equal(Verdict.Ok, denoise.Verdict);
        Assert.Contains("); 0.0 % din zone sunt sub 0.5 niveluri, unde zgomotul nu mai ascunde treptele; ", denoise.Because);
    }

    [Fact]
    public void R093_MonoHasNoColourToJudge()
    {
        var a = Run(Scenes.MonoSky());

        var colour = Assert.Single(Of(a, Topic.Colour));
        Assert.Contains("alb-negru", colour.Text);
        Assert.Equal(Verdict.Ok, colour.Verdict);
    }

    [Fact]
    public void R015_ALinearImageIsRefusedWithAMessage()
    {
        var e = Assert.Throws<RefusedImageException>(() => Run(SyntheticRasters.Flat(64, 64, 0.005f)));

        Assert.Contains("LINIARA", e.Message);
        Assert.Contains("0.0050", e.Message);
    }

    [Fact]
    public void R100_TheReportBodyCarriesTheVerdictAndEverySection()
    {
        var a = Run(Scenes.DarkSky());

        Assert.Contains("VERDICT: PROBLEMA", a.ReportBody);
        foreach (Topic t in Enum.GetValues(typeof(Topic)))
            Assert.Contains($"{(int)t}. {VerdictText.Title(t)}", a.ReportBody);
        Assert.Contains("Anexa F. Parametri", a.ReportBody);
        Assert.DoesNotContain(VersionInfo.Banner, a.ReportBody);
    }

    /// <summary>
    /// R-112 (ADR 0073): every text the Raport tab shows - the section titles and every
    /// recommendation with its "because" - is in plain words, in both languages, on scenes that
    /// raise most of the branches: the dark sky with and without a given distance (a problem, a
    /// coloured corner, banding, a crop), the clean sky, the mono sky without enough stars, a
    /// sky under L* 15, a green cast, a darkened corner, the band, and a dust no target lifts.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R112_TheReportTextsAreInPlainWords(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        const float sky = 0.14f;
        var results = new[]
        {
            Run(Scenes.DarkSky(), new Parameters { TargetAspect = 50.0 / 35.0, ViewingDistanceM = 1.5 }),
            Run(Scenes.DarkSky()),
            Run(Scenes.CleanSky()),
            Run(Scenes.MonoSky()),
            Run(SyntheticRasters.Flat(64, 64, 0.08f)),
            Run(SyntheticRasters.Flat(256, 256, sky, sky + 5f / 255, sky)),
            Run(SyntheticRasters.From(320, 320, 3, (x, y, p) =>
                y < 48 && x < 64 ? (p == 0 ? sky - 8f / 255 : p == 1 ? sky - 7f / 255 : sky - 12f / 255) : sky)),
            Run(WcsFixtures.BandScene()),
            Run(SyntheticRasters.From(512, 512, 3, (x, y, p) => (x - 255.5) * (x - 255.5) + (y - 255.5) * (y - 255.5) <= 70 * 70 ? 0.001f : 0.25f)),
        };

        var texts = results.SelectMany(a => a.Recommendations.SelectMany(r => new[] { r.Text, r.Because }))
            .Concat(Enum.GetValues(typeof(Topic)).Cast<Topic>().Select(VerdictText.Title))
            .ToList();
        var methodWords = new System.Text.RegularExpressions.Regex(
            @"\b(median\w*|percentil\w*|LSB|Q1|Q3|esantion\w*|samples?|re-stretch\w*|grila|grid|deghizat|disguised|octet_inalt|high_byte|palier\w*|plateau\w*|cuantiz\w*|quantiz\w*|gradient\w*|rezidual|residual|aplatiz\w*|flattening|vizibilitate|visibility|modelat|modelled|tiles?|dale (netede|curate))\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.All(texts, t => Assert.False(methodWords.IsMatch(t), t));
        Assert.Equal(Tr.Pick("The background colour and the frame's edges", "Culoarea fundalului si marginile cadrului"), VerdictText.Title(Topic.Colour));
        Assert.Contains(results[0].Recommendations, r => r.Text.Contains(Tr.Pick("Redo the stretch from the linear data up to a background of ", "Refa stretch-ul din datele liniare pana la fundalul ")));
        Assert.Contains(results[1].Recommendations, r => r.Because.StartsWith(Tr.Pick("the stars' width (FWHM) is ", "latimea stelelor (FWHM) e ")));
    }

    /// <summary>
    /// The Romanian counts take "de" before their noun from 20 on, and again at every hundred,
    /// but not for the numbers ending in 01 to 19: "64 de stele", "16 stele", "101 stele".
    /// </summary>
    [Theory]
    [InlineData("0", "0 ")]
    [InlineData("16", "16 ")]
    [InlineData("19", "19 ")]
    [InlineData("20", "20 de ")]
    [InlineData("64", "64 de ")]
    [InlineData("100", "100 de ")]
    [InlineData("101", "101 ")]
    [InlineData("119", "119 ")]
    [InlineData("167", "167 de ")]
    [InlineData("1000", "1000 de ")]
    public void RomanianCountsTakeDeWhereTheLanguageDoes(string shown, string expected)
        => Assert.Equal(expected, Fmt.RoCount(shown));

    [Fact]
    public void R002_TheExitCodeFollowsTheVerdict()
    {
        Assert.Equal(0, AnalyzerCli.ExitFor(Verdict.Ok));
        Assert.Equal(2, AnalyzerCli.ExitFor(Verdict.Attention));
        Assert.Equal(3, AnalyzerCli.ExitFor(Verdict.Problem));
    }
}
