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
        Assert.Contains(Of(a, Topic.Denoise), r => r.Text.Contains("exact unde trebuie"));
    }

    [Fact]
    public void R092_TheDarkSkyRaisesEveryFlagTheRealImageDid()
    {
        var a = Run(Scenes.DarkSky(), new Parameters { TargetAspect = 50.0 / 35.0, ViewingDistanceM = 1.5 });

        Assert.Equal(Verdict.Problem, a.Overall);
        Assert.Contains(Of(a, Topic.Background), r => r.Verdict == Verdict.Problem && r.Text.Contains("Re-stretch"));
        // The scene's two levels of green are a chroma of 1.7, no cast since ADR 0057, as on
        // the real image (1.0); the cast itself is tested on R093_AFiveLevelGreenSkyIsACast.
        Assert.Contains(Of(a, Topic.Colour), r => r.Verdict == Verdict.Ok && r.Text.Contains("Fundalul e neutru") && r.Because.Contains("croma 1."));
        Assert.Contains(Of(a, Topic.Colour), r => r.Verdict == Verdict.Attention && r.Text.StartsWith("Colt dreapta-jos"));
        Assert.Contains(Of(a, Topic.Size), r => r.Verdict == Verdict.Attention && r.Text.Contains("decupaj"));
        Assert.Contains(Of(a, Topic.Denoise), r => r.Verdict == Verdict.Attention && r.Text.Contains("apasat tare"));
        Assert.Contains(Of(a, Topic.Warnings), r => r.Text.Contains("master-ul de 16 biti"));
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

        Assert.Contains(Of(a, Topic.Warnings), x => x.Text.Contains("deghizat"));
        Assert.Contains(Of(a, Topic.Warnings), x => x.Text.Contains("Fara profil ICC"));
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
        Assert.StartsWith("Tenta verde pe fundal", cast.Text);
        Assert.Contains("croma 4.3 (a* -3.5, b* +2.5; prag 3.0, depasit)", cast.Because);
        Assert.Contains("G - (R+B)/2 = +5.0", cast.Because);
    }

    [Fact]
    public void R093_MonoHasNoColourToJudge()
    {
        var a = Run(Scenes.MonoSky());

        var colour = Assert.Single(Of(a, Topic.Colour));
        Assert.Contains("mono", colour.Text);
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

    [Fact]
    public void R002_TheExitCodeFollowsTheVerdict()
    {
        Assert.Equal(0, AnalyzerCli.ExitFor(Verdict.Ok));
        Assert.Equal(2, AnalyzerCli.ExitFor(Verdict.Attention));
        Assert.Equal(3, AnalyzerCli.ExitFor(Verdict.Problem));
    }
}
