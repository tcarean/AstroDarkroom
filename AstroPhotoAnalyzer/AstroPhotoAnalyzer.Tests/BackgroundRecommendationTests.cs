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

    [Fact]
    public void R092_ASkyUnderPaperBlackIsAProblemWithTheReStretch()
    {
        // Flat 0.08: dust = sky = L* 6, under paper black.
        Recommendation rec = Background(SyntheticRasters.Flat(64, 64, 0.08f));

        Assert.Equal(Verdict.Problem, rec.Verdict);
        Assert.StartsWith("Cerul e sub negrul hartiei", rec.Text);
        Assert.Contains("Re-stretch din liniar la fundal 0.10 (simulat: praful la L* 9.0, cerul la L* 9.0; exact pe negru la 0.1", rec.Text);
        Assert.Contains("praful (cele mai intunecate 1 % din dalele de 32 px; fara WCS) la L* 6.", rec.Because);
        Assert.Contains("negrul hartiei L* 9.0 la Dmax 2.0", rec.Because);
        Assert.Contains("cerul (mediana dalelor) la L* 6.", rec.Because);
        Assert.Contains("nivelul fundalului 0.08", rec.Because);
        Assert.Contains("trece asa cum e de la Dmax 2.", rec.Because);
    }

    [Fact]
    public void R092_DustUnderPaperBlackWithTheSkyAboveIsAnAttention()
    {
        // Sky at 0.25 with a dark disc at 0.05 (the darkest 1 % of the patches): the dust is
        // under black, the sky is not.
        Raster r = SyntheticRasters.From(512, 512, 3, (x, y, p) => (x - 255.5) * (x - 255.5) + (y - 255.5) * (y - 255.5) <= 70 * 70 ? 0.05f : 0.25f);
        Recommendation rec = Background(r);

        Assert.Equal(Verdict.Attention, rec.Verdict);
        Assert.StartsWith("Praful e sub negrul hartiei: norii de praf s-ar tipari negru pur.", rec.Text);
        // The sky is at 0.25, above the whole grid: every row darkens, none puts the dust on
        // black; the lift it would take is on the ladder, above 0.20.
        Assert.Contains("Nicio tinta pana la 0.20 nu aduce praful pe negrul hartiei; ar trebui ~0.", rec.Text);
    }

    [Fact]
    public void R092_WithABandTheBecauseNamesTheClassesAndTheClouds()
    {
        Recommendation rec = Background(WcsFixtures.BandScene());

        Assert.Equal(Verdict.Attention, rec.Verdict);
        Assert.Contains("praful (percentila 25 % a dalelor de praf (cu 5 L* sub nivelul local al benzii sau sub cerul modelat; ", rec.Because);
        Assert.Contains("cei mai intunecati: LDN ", rec.Because);
        Assert.Contains("cerul (mediana dalelor de cer, in afara benzii (|b| peste 10 grade)) la L* 2", rec.Because);
        Assert.DoesNotContain("fara WCS", rec.Because);

        // Wholly inside the band: the fallback rule, named.
        Recommendation inside = Background(WcsFixtures.Ldn42Scene());
        Assert.Contains("praful (cele mai intunecate 1 % din dalele de 32 px; cadrul e in intregime in banda)", inside.Because);
        Assert.Contains("cei mai intunecati: LDN 42 (B78) L* ", inside.Because);
    }

    [Fact]
    public void R092_APrintSafeImageIsToldHowFarDownItCanGo()
    {
        Recommendation rec = Background(SyntheticRasters.Flat(64, 64, 0.3f));

        Assert.Equal(Verdict.Ok, rec.Verdict);
        Assert.StartsWith("Fundalul e print-safe asa cum e; poti cobori pana la 0.10 (praful la L* 9.0, cerul la L* 9.0); grila nu coboara mai jos.", rec.Text);
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
        Assert.Contains("Nicio tinta pana la 0.20 nu aduce praful pe negrul hartiei", rec.Text);
        Assert.Contains("accepta norii cei mai intunecati negri (Anexa B) sau alege alt mediu", rec.Text);
    }

    [Fact]
    public void R095_TheDenoiseFigureAfterTheReStretchSaysWhetherTheNoiseGrowsOrShrinks()
    {
        var dark = AnalysisRun.Execute(Scenes.DarkSky(), "memorie", Scenes.OnBuiltForPaper());
        Recommendation up = dark.Recommendations.Single(x => x.Topic == Topic.Denoise);
        Assert.Contains("dupa re-stretch la 0.1", up.Because);
        Assert.Contains("zgomotul creste de ~", up.Because);

        var bright = AnalysisRun.Execute(Scenes.CleanSky(), "memorie", Scenes.OnBuiltForPaper());
        Recommendation down = bright.Recommendations.Single(x => x.Topic == Topic.Denoise);
        Assert.Contains("dupa re-stretch la 0.1", down.Because);
        Assert.Contains("zgomotul scade de ~", down.Because);
    }
}
