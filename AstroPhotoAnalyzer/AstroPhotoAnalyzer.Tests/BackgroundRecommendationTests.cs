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

/// <summary>R-092: the background verdict on the dust and the sky, its three branches, and what its Because names.</summary>
public class BackgroundRecommendationTests
{
    static Recommendation Background(Raster r, Parameters? p = null)
        => AnalysisRun.Execute(r, "memorie", Scenes.OnBuiltForPaper(p)).Recommendations.Single(x => x.Topic == Topic.Background);

    /// <summary>
    /// On the default paper (baryta, Dmax 2.4, black at L* 3.6) a flat 0.08 sky clears paper
    /// black, and the paper would print it; the eye in room light would not see it. The sky
    /// under the visibility threshold is an Attention of its own, and the fix is the row that
    /// brings the sky to L* 15 (ADR 0029).
    /// </summary>
    [Fact]
    public void R092_ASkyUnderTheVisibilityThresholdIsAnAttentionEvenAbovePaperBlack()
    {
        Recommendation rec = AnalysisRun.Execute(SyntheticRasters.Flat(64, 64, 0.08f), "memorie", new Parameters())
            .Recommendations.Single(x => x.Topic == Topic.Background);

        Assert.Equal(Verdict.Attention, rec.Verdict);
        Assert.StartsWith("Cerul e sub L* 15: pe hartie nu e negru", rec.Text);
        Assert.Contains("Refa stretch-ul din datele liniare pana la fundalul 0.15 (in simulare: praful la L* 15.3, cerul la L* 15.3; cerul ajunge la L* 15 de la 0.14", rec.Text);
        Assert.Contains("negrul hartiei e L* 3.6 la Dmax 2.4", rec.Because);
        Assert.Contains("100.0 % din pixeli sunt sub L* 15, unde ochiul abia mai deosebeste tonurile", rec.Because);

        // A sky already above the threshold: Ok, and the way down stops at the threshold, not at paper black.
        Recommendation bright = AnalysisRun.Execute(SyntheticRasters.Flat(64, 64, 0.3f), "memorie", new Parameters())
            .Recommendations.Single(x => x.Topic == Topic.Background);
        Assert.Equal(Verdict.Ok, bright.Verdict);
        Assert.Contains("il poti cobori pana la 0.15 (atunci praful ar fi la L* 15.3, cerul la L* 15.3)", bright.Text);
    }

    [Fact]
    public void R092_ASkyUnderPaperBlackIsAProblemWithTheReStretch()
    {
        // Flat 0.08: dust = sky = L* 6, under paper black.
        Recommendation rec = Background(SyntheticRasters.Flat(64, 64, 0.08f));

        Assert.Equal(Verdict.Problem, rec.Verdict);
        Assert.StartsWith("Cerul e mai intunecat decat negrul hartiei", rec.Text);
        // 0.10 already puts the dust on paper black (exact 0.10); the row recommended is the first that also has the sky at L* 15.
        Assert.Contains("Refa stretch-ul din datele liniare pana la fundalul 0.15 (in simulare: praful la L* 15.3, cerul la L* 15.3; praful ajunge exact pe negru la 0.1", rec.Text);
        Assert.Contains("praful la L* 6.", rec.Because);
        Assert.Contains(" (cel mai intunecat 1 % din dalele de cer (nu s-a gasit praf); fara WCS); negrul hartiei e L* 9.0 la Dmax 2.0", rec.Because);
        Assert.Contains("negrul hartiei e L* 9.0 la Dmax 2.0", rec.Because);
        Assert.Contains("cerul la L* 6.", rec.Because);
        Assert.Contains(" (valoarea din mijloc a dalelor de cer (dalele impartite prin comparatie cu cerul)); nivelul fundalului 0.08", rec.Because);
        Assert.Contains("nivelul fundalului 0.08", rec.Because);
        // The Dmax figure is the dust's; the sky (L* 6) is under the visibility threshold, so no paper passes as it is.
        Assert.Contains("asa cum e imaginea, praful ramane deasupra negrului de la Dmax 2.", rec.Because);
        Assert.Contains("; dar nicio hartie nu salveaza printul asa cum e: cerul e sub L* 15, si singurul remediu e sa ridici fundalul cand refaci stretch-ul", rec.Because);
        Assert.DoesNotContain("trece asa cum e", rec.Because);
    }

    [Fact]
    public void R092_DustUnderPaperBlackWithTheSkyAboveIsAnAttention()
    {
        // Sky at 0.25 with a dark disc at 0.05 (the darkest 1 % of the patches): the dust is
        // under black, the sky is not.
        Raster r = SyntheticRasters.From(512, 512, 3, (x, y, p) => (x - 255.5) * (x - 255.5) + (y - 255.5) * (y - 255.5) <= 70 * 70 ? 0.05f : 0.25f);
        Recommendation rec = Background(r);

        Assert.Equal(Verdict.Attention, rec.Verdict);
        Assert.StartsWith("Praful e mai intunecat decat negrul hartiei: norii de praf ar iesi negru pur, fara detalii.", rec.Text);
        // The sky is at 0.25, above the whole grid: every row darkens, none puts the dust on
        // black; the lift it would take is on the ladder, above 0.20.
        Assert.Contains("Nicio tinta din simulare (pana la 0.20) nu ridica praful pana la negrul hartiei; ar fi nevoie de ~0.", rec.Text);
    }

    [Fact]
    public void R092_WithABandTheBecauseNamesTheClassesAndTheClouds()
    {
        Recommendation rec = Background(WcsFixtures.BandScene());

        Assert.Equal(Verdict.Attention, rec.Verdict);
        Assert.StartsWith("praful la L* ", rec.Because);
        Assert.Contains(" (cele mai intunecate zone intinse din norii de praf (pragul sub care stau 25 % din dalele de praf; praful e ", rec.Because);
        Assert.Contains("; norii cei mai intunecati: LDN ", rec.Because);
        Assert.Contains("cerul la L* 2", rec.Because);
        Assert.Contains(" (cerul din afara benzii Caii Lactee, la peste 10 grade de ea (valoarea din mijloc a dalelor de cer)); nivelul fundalului", rec.Because);
        Assert.DoesNotContain("fara WCS", rec.Because);

        // Wholly inside the band: the sky path, named; the cloud fills the frame and is the
        // modelled sky itself, so the dust is the darkest sky.
        Recommendation inside = Background(WcsFixtures.Ldn42Scene());
        Assert.StartsWith("praful la L* ", inside.Because);
        Assert.Contains(" (cel mai intunecat 1 % din dalele de cer (nu s-a gasit praf); cadrul e in intregime in banda); norii cei mai intunecati: LDN 42 (B78) L* ", inside.Because);
        Assert.Contains("; norii cei mai intunecati: LDN 42 (B78) L* ", inside.Because);
    }

    [Fact]
    public void R092_APrintSafeImageIsToldHowFarDownItCanGo()
    {
        Recommendation rec = Background(SyntheticRasters.Flat(64, 64, 0.3f));

        Assert.Equal(Verdict.Ok, rec.Verdict);
        // The way down stops where the sky meets the visibility threshold (L* 15, the level 0.148), not at paper black.
        Assert.StartsWith("Fundalul e print-safe asa cum e; il poti cobori pana la 0.15 (atunci praful ar fi la L* 15.3, cerul la L* 15.3).", rec.Text);
    }

    [Fact]
    public void R092_WhenNoGridRowReachesPaperBlackTheExactTargetIsNamed()
    {
        // Flat 0.02 is refused as linear; 0.03 is accepted (median 0.03 > 0.02) and sits at
        // L* 2.3: the ladder reaches paper black at ~0.10, within the grid. Use a dark disc on
        // a 0.25 sky instead: the disc at 0.001 needs more than 0.20 to reach L* 9.
        Raster r = SyntheticRasters.From(512, 512, 3, (x, y, p) => (x - 255.5) * (x - 255.5) + (y - 255.5) * (y - 255.5) <= 70 * 70 ? 0.001f : 0.25f);
        Recommendation rec = Background(r);

        Assert.Equal(Verdict.Attention, rec.Verdict);
        Assert.Contains("Nicio tinta din simulare (pana la 0.20) nu ridica praful pana la negrul hartiei", rec.Text);
        // No WCS, so no named clouds: the sentence points at Appendix B without promising a list.
        Assert.Contains("accepta ca norii cei mai intunecati sa iasa negri (vezi Anexa B) sau alege alta hartie", rec.Text);
    }

    [Fact]
    public void R095_TheDenoiseFigureAfterTheReStretchSaysWhetherTheNoiseGrowsOrShrinks()
    {
        var dark = AnalysisRun.Execute(Scenes.DarkSky(), "memorie", Scenes.OnBuiltForPaper());
        Recommendation up = dark.Recommendations.Single(x => x.Topic == Topic.Denoise);
        Assert.Contains("dupa ce refaci stretch-ul la 0.1", up.Because);
        Assert.Contains("zgomotul creste de ~", up.Because);

        var bright = AnalysisRun.Execute(Scenes.CleanSky(), "memorie", Scenes.OnBuiltForPaper());
        Recommendation down = bright.Recommendations.Single(x => x.Topic == Topic.Denoise);
        Assert.Contains("dupa ce refaci stretch-ul la 0.1", down.Because);
        Assert.Contains("zgomotul scade de ~", down.Because);
    }
}
