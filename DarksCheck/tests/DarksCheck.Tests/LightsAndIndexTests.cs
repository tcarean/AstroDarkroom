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

using System.Globalization;
using DarksCheck.Core;

namespace DarksCheck.Tests;

/// <summary>Check 9 (the lights against the dark), the index, the label and the matching, on synthetic frames with known answers (R-050 to R-065).</summary>
public class LightsAndIndexTests
{
    private static DarkSeriesResult Run(SessionFixture s, string? against = null, int maxFrames = Th.MaxFramesInMemory, string? lightsFrom = null) => DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2, MaxFrames: maxFrames, Against: against, LightsFrom: lightsFrom));

    /// <summary>Darks with the twenty lattice hot pixels of 120 ADU.</summary>
    private static SyntheticDark Dark(int i) => new() { Seed = 100 + i, Hot = SyntheticLight.Lattice };

    /// <summary>Lights with the same hot pixels at <paramref name="factor"/> of the darks' amplitude and three stars.</summary>
    private static SyntheticLight Light(int i, double factor = 1) => new() { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = factor, Stars = SyntheticLight.ThreeStars };

    private static string Report(DarkSeriesResult r)
    {
        using StringWriter sw = new();
        DarkReport.Write(r, sw);
        return sw.ToString();
    }

    [Fact]
    public void R050_EveryLightReadsItsSkyAndItsIsolatedHotPixelsAndStarsAreNotCounted()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i));
        DarkSeriesResult r = Run(s);
        Assert.Null(r.LightsError);
        LightsAnalysis l = Assert.IsType<LightsAnalysis>(r.Lights);
        Assert.Equal(5, l.Used);
        Assert.Equal(5, l.Frames.Count);
        foreach (LightFigures f in l.Frames)
        {
            Assert.Null(f.Error);
            for (int p = 0; p < 4; p++)
            {
                Assert.InRange(f.SkyMedian[p], 497, 503);
                Assert.InRange(f.SkySigma[p], 5.0, 7.0);
                Assert.InRange(f.LocalSigma[p], 5.5, 7.8);
            }

            // the twenty lattice pixels (120 ADU, 18 sigma) are isolated, except when one of a pixel's eight
            // neighbours crosses 3 sigma by noise (about 1 % of the hot pixels per light, as in the prototype);
            // the stars' pixels are candidates with lit neighbours
            Assert.InRange(f.IsolatedHot, 18, 20);
            Assert.True(f.Candidates > f.IsolatedHot, $"candidates {f.Candidates}");
        }

        Assert.Equal(4, l.PersistentThreshold);
        Assert.InRange(l.Persistent, 18, 20);
        Assert.Equal(l.Persistent, l.HotCountHistogram[5] + l.HotCountHistogram[4]);
    }

    [Fact]
    public void R051_PersistenceOverlapWithTheMasterAndThePixelsTheDarkMisses()
    {
        // a rogue pixel in two lights of five is not persistent; one in every light but not in the dark is persistent and missed
        static SyntheticLight L(int i) => new()
        {
            Seed = 300 + i,
            Hot = SyntheticLight.Lattice,
            Stars = SyntheticLight.ThreeStars,
            Rogue = i < 2 ? [(150, 100, 200), (170, 120, 100)] : [(170, 120, 100)],
        };
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, L);
        LightsAnalysis l = Run(s).Lights!;
        // a lattice pixel can lose the isolation test twice in five lights by noise (about 0.1 % each)
        Assert.InRange(l.Persistent, 20, 21);
        Assert.Equal(l.Persistent, l.HotCountHistogram[5] + l.HotCountHistogram[4]);
        Assert.Equal(1, l.HotCountHistogram[2]);
        Assert.Equal(4, l.Overlap.Count);
        Assert.Equal([10.0, 20.0, 50.0, 100.0], l.Overlap.Select(o => o.LevelAdu));
        foreach (OverlapAtLevel o in l.Overlap)
        {
            Assert.Equal(20, o.DarkCount);
            Assert.Equal(l.Persistent - 1, o.Both);
        }

        Assert.Equal(1, l.Missed);
        Assert.InRange(l.MissedDarkExcessMedian, -4, 4);
        string report = Report(Run(s));
        Assert.Contains($"pixeli fierbinti   : {Ui.RoCount(l.Persistent)}pixeli fierbinti se vad in lights in acelasi loc, in cel putin 4 din 5 cadre (pixeli singuri, nu parte dintr-o stea, mai luminosi decat imprejurimea cu peste 6 ori zgomotul ei)", report, StringComparison.Ordinal);
        Assert.Contains($"in masterul dark   : peste 10: {l.Persistent - 1} din {l.Persistent} (95 %); peste 20: {l.Persistent - 1} din {l.Persistent} (95 %); peste 50: ", report, StringComparison.Ordinal);
        Assert.Contains($"masterul in lights : peste 10: {l.Persistent - 1} din 20 (", report, StringComparison.Ordinal);
        Assert.Contains("lipsesc din master : 1 pixel fierbinte din lights e sub 20 ADU in master", report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1.0, "Direct", "OK: dark-ul se poate scadea asa cum e; in lights pixelii fierbinti sunt de 1.0")]
    [InlineData(1.3, "WithOpt", "ATENTIE: scade dark-ul cu -opt (-opt: Siril il inmulteste cat sa se potriveasca cu lights-urile); in lights pixelii fierbinti sunt de 1.3")]
    [InlineData(0.5, "CosmeticOnly", "ATENTIE: foloseste dark-ul doar pentru corectia cosmetica (-cc=dark: Siril gaseste in el pixelii fierbinti si ii repara in lights); in lights pixelii fierbinti sunt de 0.5")]
    [InlineData(1.6, "Unmatched", "ATENTIE: nu se potrivesc; in lights pixelii fierbinti sunt de 1.6")]
    public void R052_TheAmplitudeRatioOfTheLightsOverTheMasterGivesTheVerdict(double factor, string verdict, string text)
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, factor));
        DarkSeriesResult r = Run(s);
        LightsAnalysis l = r.Lights!;
        Assert.Equal(20, l.AmpPixels);
        Assert.InRange(l.LightsOverDark.Ratio, factor - 0.05, factor + 0.05);
        Assert.Equal(5, l.LightsOverDark.Pixels);
        Assert.Equal(verdict, l.LightsOverDark.Verdict.ToString());
        Assert.InRange(l.LightsOverDark.DeltaCelsius, 6 * Math.Log2(factor) - 0.5, 6 * Math.Log2(factor) + 0.5);
        Assert.Contains("    verdict            : " + text, Report(r), StringComparison.Ordinal);
    }

    [Fact]
    public void R052_TheRatioIsReadPerLightAndNormalisedByTheLightsOwnSettings()
    {
        // lights warming from 0.8 to 1.2 of the master
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 0.8 + 0.1 * i));
        LightsAnalysis l = Run(s).Lights!;
        Assert.InRange(l.RatioFirst, 0.75, 0.85);
        Assert.InRange(l.RatioLast, 1.15, 1.25);
        Assert.InRange(l.LightsOverDark.Ratio, 0.95, 1.05);
        for (int i = 0; i < 5; i++)
        {
            Assert.InRange(l.Frames[i].AmpRatioMedian, 0.8 + 0.1 * i - 0.05, 0.8 + 0.1 * i + 0.05);
            Assert.True(l.Frames[i].AmpRatioQ25 < l.Frames[i].AmpRatioMedian && l.Frames[i].AmpRatioMedian < l.Frames[i].AmpRatioQ75);
        }

        Assert.Contains($"lights fata de dark: in lights, pixelii fierbinti sunt in medie de {l.LightsOverDark.Ratio.ToString("0.00", CultureInfo.InvariantCulture)} ori cat in dark (pe cele 5 cadre; in primul {l.RatioFirst.ToString("0.00", CultureInfo.InvariantCulture)}, in ultimul {l.RatioLast.ToString("0.00", CultureInfo.InvariantCulture)}; pe 20 de pixeli)", Report(Run(s)), StringComparison.Ordinal);

        // lights at ISO 800 with twice the amplitude are at the same temperature as darks at ISO 400
        using SessionFixture iso = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i, 2.0), iso: 800);
        DarkSeriesResult ri = Run(iso);
        Assert.InRange(ri.Lights!.LightsOverDark.Ratio, 0.95, 1.05);
        Assert.Equal(0, ri.Lights.AssumedSettings);
        Assert.DoesNotContain("s-au luat cele ale dark-urilor", Report(ri), StringComparison.Ordinal);

        // lights without an ISO card are taken at the darks' settings, and the report says so
        using SessionFixture noIso = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i), iso: null);
        DarkSeriesResult rn = Run(noIso);
        Assert.Equal(3, rn.Lights!.AssumedSettings);
        Assert.All(rn.Lights.Frames, f => Assert.True(f.AssumedSettings));
        Assert.InRange(rn.Lights.LightsOverDark.Ratio, 0.95, 1.05);
        Assert.Contains("ATENTIE            : 3 lights nu spun ISO-ul sau expunerea; s-au luat cele ale dark-urilor (1 ADU-eq = 1.00 ADU)", Report(rn), StringComparison.Ordinal);
    }

    [Fact]
    public void R052_LightsWithoutTheMastersHotPixelsAreUnmatchedNotUnmeasured()
    {
        // in-camera noise reduction, another body or a mirrored orientation: the ratios sit around zero
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 0));
        DarkSeriesResult r = Run(s);
        MatchFigures m = r.Lights!.LightsOverDark;
        Assert.Equal(MatchVerdict.Unmatched, m.Verdict);
        Assert.InRange(m.Ratio, -0.05, 0.05);
        Assert.True(m.Q25 <= 0);
        Assert.True(double.IsNaN(r.LightsIndex) || r.LightsIndex <= 0.05 * r.Index.Index);
        string report = Report(r);
        Assert.Contains("verdict            : ATENTIE: nu se potrivesc: in lights nu se vad pixelii fierbinti ai dark-ului (de ", report, StringComparison.Ordinal);
        Assert.Contains(" ori cat in dark; poate reducerea de zgomot din aparat, alt aparat sau cadre rotite?)", report, StringComparison.Ordinal);
        Assert.DoesNotContain("NEMASURAT: masterul nu are niciun pixel fierbinte", report, StringComparison.Ordinal);
    }

    /// <summary>A hundred hot pixels of 30 ADU on a grid: just over the hot threshold of 20, weak against a light's noise of 6.</summary>
    private static readonly IReadOnlyList<(int X, int Y, double Adu)> WeakHot = [.. Enumerable.Range(0, 100).Select(k => (20 + 22 * (k % 10), 20 + 16 * (k / 10), 30.0))];

    [Theory]
    [InlineData(0.85)]
    [InlineData(1.15)]
    public void R067_ARatioOutsideTheDirectBandIsOkWhenWhatItLeavesIsUnderTheNoiseOfALight(double factor)
    {
        // a hundred hot pixels of 30 ADU and lights with a noise of 6 (a pixel differs from its neighbours by about
        // 6.5): at 0.85 or 1.15 of the dark a plain subtraction leaves 4.5 ADU, about 0.7 of the noise, so the dark
        // can be subtracted as it is although the ratio is outside 0.9 .. 1.1 (ADR 0044)
        using SessionFixture s = new SessionFixture()
            .WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = WeakHot })
            .WithLights(5, i => new SyntheticLight { Seed = 300 + i, Hot = WeakHot, HotFactor = factor });
        DarkSeriesResult r = Run(s);
        LightsAnalysis l = r.Lights!;
        Assert.Equal(100, l.AmpPixels);
        MatchFigures m = l.LightsOverDark;
        Assert.InRange(m.Ratio, factor - 0.05, factor + 0.05);
        Assert.Equal(MatchVerdict.WithOpt, IndexAnalyzer.Judge(m.Ratio));
        TraceFigures t = l.Traces;
        Assert.Equal(100, t.Pixels);
        // the level over the noise at a typical hot pixel, then the trace at the night's ratio
        Assert.InRange(t.PerUnit, 30 / 7.8, 30 / 5.5);
        Assert.Equal(Math.Abs(m.Ratio - 1) * t.PerUnit, t.Median, 1e-9);
        Assert.InRange(t.Median, 0.35, 0.99);
        Assert.Equal(0, t.AboveNoise);
        Assert.Equal(0, t.AboveStrong);
        Assert.Equal(1 - Th.TraceNoiseLimit / t.PerUnit, t.DirectLow, 1e-9);
        Assert.Equal(1 + Th.TraceNoiseLimit / t.PerUnit, t.DirectHigh, 1e-9);
        Assert.InRange(t.DirectLow, Th.MatchOptLow, Th.MatchDirectLow);
        Assert.InRange(t.DirectHigh, Th.MatchDirectHigh, Th.MatchOptHigh);
        Assert.True(t.Relaxed);
        Assert.Equal(MatchVerdict.Direct, m.Verdict);
        Assert.Equal(MatchVerdict.Direct, l.Judge(m.Ratio));
        Assert.Equal(MatchVerdict.WithOpt, l.Judge(factor < 1 ? t.DirectLow - 0.01 : t.DirectHigh + 0.01));

        string report = Report(r);
        string median = t.Median.ToString("0.00", CultureInfo.InvariantCulture);
        string ratio = m.Ratio.ToString("0.00", CultureInfo.InvariantCulture);
        string band = $"intre {t.DirectLow.ToString("0.00", CultureInfo.InvariantCulture)} si {t.DirectHigh.ToString("0.00", CultureInfo.InvariantCulture)}";
        string tail = "(zgomotul light-ului pe culoarea pixelului; cu dithering urmele se imprastie, fara el raman in acelasi loc in stack)";
        Assert.Contains(
            factor < 1
                ? $"    ce ramane          : scaderea ia prea mult: la un pixel fierbinte ramane in medie o gaura de {median} din zgomotul unui light; la 0 din 100 gaura trece de zgomot, la 0 de 3 ori zgomotul {tail}"
                : $"    ce ramane          : scaderea ia prea putin: la un pixel fierbinte ramane in medie un rest de {median} din zgomotul unui light; la 0 din 100 restul trece de zgomot, la 0 de 3 ori zgomotul {tail}",
            report,
            StringComparison.Ordinal);
        Assert.Contains($"    verdict            : OK: dark-ul se poate scadea asa cum e; in lights pixelii fierbinti sunt de {ratio} ori cat in dark, in afara 0.9 .. 1.1, dar ce ramane dupa scadere e in medie sub zgomotul unui light ({median} din el; pe aceste lights, asa e {band}): dark-ul e mai {(factor < 1 ? "cald" : "rece")} cu ", report, StringComparison.Ordinal);
        // the new words name no part of the method (ADR 0028)
        foreach (ReportRow row in DarkReport.Sections(r).Single(x => x.Title.StartsWith("9.", StringComparison.Ordinal)).Rows.Where(x => x.Label is "ce ramane" or "verdict"))
        {
            Assert.DoesNotMatch(PlainWords.RomanianTabWords(), row.Value);
        }

        // the row sits between the ratio's row and the indexes
        Assert.True(report.IndexOf("lights fata de dark:", StringComparison.Ordinal) < report.IndexOf("ce ramane          :", StringComparison.Ordinal));
        Assert.True(report.IndexOf("ce ramane          :", StringComparison.Ordinal) < report.IndexOf("index dark-uri     :", StringComparison.Ordinal));

        using (Ui.Use(UiLanguage.English))
        {
            string en = Report(r);
            Assert.Contains(
                factor < 1
                    ? $"    what is left       : the subtraction takes too much: a hot pixel keeps on average a hole of {median} of the noise of a light; at 0 of 100 the hole is over the noise, at 0 over 3 times the noise (the light's noise on the pixel's colour; with dithering the traces spread, without it they stay in the same place in the stack)"
                    : $"    what is left       : the subtraction takes too little: a hot pixel keeps on average a rest of {median} of the noise of a light; at 0 of 100 the rest is over the noise, at 0 over 3 times the noise (the light's noise on the pixel's colour; with dithering the traces spread, without it they stay in the same place in the stack)",
                en,
                StringComparison.Ordinal);
            Assert.Contains($"    verdict            : OK: the dark can be subtracted as it is; in the lights the hot pixels are {ratio} times what they are in the dark, outside 0.9 .. 1.1, but what the subtraction leaves is on average under the noise of a light ({median} of it; on these lights that holds between {t.DirectLow.ToString("0.00", CultureInfo.InvariantCulture)} and {t.DirectHigh.ToString("0.00", CultureInfo.InvariantCulture)}): the dark is {(factor < 1 ? "warmer" : "colder")} by ", en, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void R067_StrongHotPixelsAtTheSameRatioKeepTheOptVerdict()
    {
        // the same ratio over twenty hot pixels of 120 ADU: the subtraction leaves 18 ADU, about 2.8 times the noise
        // of a light, so the ratio's verdict stands and says what is left
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 0.85 });
        DarkSeriesResult r = Run(s);
        LightsAnalysis l = r.Lights!;
        TraceFigures t = l.Traces;
        Assert.Equal(20, t.Pixels);
        Assert.InRange(t.Median, 1.8, 3.6);
        Assert.Equal(20, t.AboveNoise);
        Assert.InRange(t.AboveStrong, 0, 20);
        Assert.Equal(Th.MatchDirectLow, t.DirectLow);
        Assert.Equal(Th.MatchDirectHigh, t.DirectHigh);
        Assert.False(t.Relaxed);
        Assert.Equal(MatchVerdict.WithOpt, l.LightsOverDark.Verdict);
        string report = Report(r);
        string median = t.Median.ToString("0.00", CultureInfo.InvariantCulture);
        // from 1.00 up the trace is said in times the noise, as the verdict under it says it
        Assert.Contains($"    ce ramane          : scaderea ia prea mult: la un pixel fierbinte ramane in medie o gaura de {median} ori cat zgomotul unui light; la 20 din 20 gaura trece de zgomot, la {t.AboveStrong} de 3 ori zgomotul (", report, StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Contains($"    what is left       : the subtraction takes too much: a hot pixel keeps on average a hole of {median} times the noise of a light; at 20 of 20 the hole is over the noise, at {t.AboveStrong} over 3 times the noise (", Report(r), StringComparison.Ordinal);
        }

        // too little, and over the noise: a rest of so many times the noise
        using SessionFixture warm = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 1.3));
        DarkSeriesResult rw = Run(warm);
        string rest = rw.Lights!.Traces.Median.ToString("0.00", CultureInfo.InvariantCulture);
        Assert.InRange(rw.Lights.Traces.Median, 3.5, 7.5);
        Assert.Contains($"    ce ramane          : scaderea ia prea putin: la un pixel fierbinte ramane in medie un rest de {rest} ori cat zgomotul unui light; la 20 din 20 restul trece de zgomot, la ", Report(rw), StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Contains($"    what is left       : the subtraction takes too little: a hot pixel keeps on average a rest of {rest} times the noise of a light; at 20 of 20 the rest is over the noise, at ", Report(rw), StringComparison.Ordinal);
        }
        Assert.Contains($" ori cat in dark, intre 0.7 si 1.4, iar ce ramane dupa scadere e in medie de {median} ori zgomotul unui light: dark-ul e mai cald cu ", report, StringComparison.Ordinal);
        Assert.Contains("    verdict            : ATENTIE: scade dark-ul cu -opt (-opt: Siril il inmulteste cat sa se potriveasca cu lights-urile); in lights pixelii fierbinti sunt de 0.8", report, StringComparison.Ordinal);
    }

    [Fact]
    public void R067_TheTwoCountsSplitThePixelsAtOnceAndAtThreeTimesTheNoise()
    {
        // ten hot pixels of 60 ADU and ten of 240, lights at 0.85 with a noise of 6: the subtraction leaves 9 ADU at
        // the first ten (about 1.4 times the noise) and 36 at the others (about 5.5 times), so all twenty are above
        // the noise and exactly the ten strong ones above three times it
        (int X, int Y, double Adu)[] twoLevels = [.. SyntheticLight.Lattice.Select((h, k) => (h.X, h.Y, k < 10 ? 60.0 : 240.0))];
        using SessionFixture s = new SessionFixture()
            .WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = twoLevels })
            .WithLights(5, i => new SyntheticLight { Seed = 300 + i, Hot = twoLevels, HotFactor = 0.85 });
        DarkSeriesResult r = Run(s);
        TraceFigures t = r.Lights!.Traces;
        Assert.Equal(20, t.Pixels);
        Assert.Equal(20, t.AboveNoise);
        Assert.Equal(10, t.AboveStrong);
        Assert.Contains("; la 20 din 20 gaura trece de zgomot, la 10 de 3 ori zgomotul (", Report(r), StringComparison.Ordinal);
    }

    [Fact]
    public void R067_TheTracesOnlyRelaxTheOptBandAndAMatchedDarkLeavesNothing()
    {
        // weak hot pixels at 0.6 of the dark: under 0.7 the dark is for the cosmetic correction only, whatever is left
        using SessionFixture cosmetic = new SessionFixture()
            .WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = WeakHot })
            .WithLights(5, i => new SyntheticLight { Seed = 300 + i, Hot = WeakHot, HotFactor = 0.6 });
        LightsAnalysis lc = Run(cosmetic).Lights!;
        Assert.InRange(lc.Traces.Median, 1.0, 2.6);
        Assert.False(lc.Traces.Relaxed);
        Assert.Equal(MatchVerdict.CosmeticOnly, lc.LightsOverDark.Verdict);
        Assert.True(lc.LightsOverDark.Ratio < lc.Traces.DirectLow, $"{lc.LightsOverDark.Ratio} against {lc.Traces.DirectLow}");
        Assert.InRange(lc.Traces.DirectLow, Th.MatchOptLow, Th.MatchDirectLow);

        // hot pixels so weak against the lights' noise (22 ADU against 10) that the trace would stay under the noise
        // well past the -opt band: the band stops at 0.7 .. 1.4, and lights at 0.6 are still for the cosmetic
        // correction only
        (int X, int Y, double Adu)[] faint = [.. WeakHot.Select(h => (h.X, h.Y, 22.0))];
        using SessionFixture clamped = new SessionFixture()
            .WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = faint })
            .WithLights(5, i => new SyntheticLight { Seed = 300 + i, Hot = faint, HotFactor = 0.6, Noise = 10 });
        LightsAnalysis lk = Run(clamped).Lights!;
        Assert.True(Th.TraceNoiseLimit / lk.Traces.PerUnit > Th.MatchOptHigh - 1, $"per unit {lk.Traces.PerUnit}");
        Assert.Equal(Th.MatchOptLow, lk.Traces.DirectLow);
        Assert.Equal(Th.MatchOptHigh, lk.Traces.DirectHigh);
        Assert.Equal(MatchVerdict.CosmeticOnly, lk.LightsOverDark.Verdict);
        Assert.Equal(MatchVerdict.CosmeticOnly, lk.Judge(0.65));
        Assert.Equal(MatchVerdict.Unmatched, lk.Judge(1.45));
        Assert.Equal(MatchVerdict.Direct, lk.Judge(0.7));

        // lights at the dark's own level: inside 0.9 .. 1.1, the old words, and a trace near nothing
        using SessionFixture matched = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i));
        DarkSeriesResult rm = Run(matched);
        Assert.Equal(MatchVerdict.Direct, rm.Lights!.LightsOverDark.Verdict);
        Assert.False(rm.Lights.Traces.Relaxed);
        Assert.InRange(rm.Lights.Traces.Median, 0, 0.9);
        Assert.Contains(", intre 0.9 si 1.1: dark-ul e ", Report(rm), StringComparison.Ordinal);

        // lights that show none of the dark's hot pixels have no row: there is no subtraction to speak of
        using SessionFixture none = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 0));
        Assert.DoesNotContain("ce ramane", Report(Run(none)), StringComparison.Ordinal);
    }

    [Fact]
    public void R053_WithoutLightsCheck9IsUnmeasuredAndTheIndexHasNoLightsLine()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark);
        DarkSeriesResult r = Run(s);
        Assert.Null(r.Lights);
        Assert.Null(r.LightsError);
        Assert.True(double.IsNaN(r.LightsIndex));
        string report = Report(r);
        Assert.Contains("  9. CAT DE BINE SE POTRIVESTE DARK-UL CU LIGHTS-URILE\n    verdict            : NEMASURAT: sesiunea nu are lights", report.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("ca un dark cu idx", report, StringComparison.Ordinal);
        Assert.DoesNotContain("TABEL PE LIGHTS", report, StringComparison.Ordinal);
    }

    [Fact]
    public void R053_ALightThatCannotBeDecodedIsAnErrorAndEveryLightIsReadWhateverTheCap()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i));
        File.WriteAllBytes(Path.Combine(s.LightsDir, "light_0003.fit"), new byte[100]);
        File.WriteAllBytes(Path.Combine(s.LightsDir, "light_0004.fit"), new SyntheticLight { Width = 128, Height = 96, Hot = [], Stars = [] }.Build(SessionFixture.FirstStart));
        (int code, string stdout, _) = Cli.Run(s.Root, "--threads", "2");
        Assert.Equal(1, code);
        Assert.Contains("ATENTIE            : 2 lights nu s-au putut citi (primul: light_0003.fit:", stdout, StringComparison.Ordinal);
        Assert.Contains("); sunt in tabelul de lights", stdout, StringComparison.Ordinal);
        Assert.Contains("(pe cele 3 cadre; ", stdout, StringComparison.Ordinal);
        Assert.Contains("light_0004.fit        eroare: dimensiune diferita: 128 x 96 fata de 256 x 192", stdout, StringComparison.Ordinal);
        Assert.Matches(@"pixeli fierbinti   : (18 |19 |20 de )pixeli fierbinti se vad in lights in acelasi loc, in cel putin 3 din 3 cadre \(pixeli singuri, nu parte dintr-o stea, mai luminosi decat imprejurimea cu peste 6 ori zgomotul ei\)", stdout);

        // the cap of R-027 is the darks' and the biases': every light is read (R-050, ADR 0041)
        using SessionFixture capped = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i));
        DarkSeriesResult r = Run(capped, maxFrames: 3);
        Assert.False(r.HasErrors);
        Assert.Equal(3, r.Darks.Used);
        Assert.Equal(5, r.Lights!.Used);
        Assert.All(r.Lights.Frames, f => Assert.Null(f.Error));
        Assert.DoesNotContain("sarit", Report(r).Split("TABEL PE LIGHTS")[1], StringComparison.Ordinal);
        Assert.DoesNotContain("lights sarite", Report(r), StringComparison.Ordinal);

        // no light decodes: check 9 says why, the run exits 1
        using SessionFixture none = new SessionFixture().WithDarkSeries(6, Dark).WithLights(2, i => Light(i));
        foreach (string f in Directory.GetFiles(none.LightsDir))
        {
            File.WriteAllBytes(f, new byte[100]);
        }

        (int code2, string stdout2, _) = Cli.Run(none.Root, "--threads", "2");
        Assert.Equal(1, code2);
        Assert.Contains("verdict            : NEMASURAT: lights-urile nu s-au putut folosi: niciun light nu s-a putut citi: light_0001.fit:", stdout2, StringComparison.Ordinal);
    }

    [Fact]
    public void R053_TheLightsCsvHasOneRowPerLightWithTheSkyTheCountsAndTheRatio()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(4, i => Light(i));
        File.WriteAllBytes(Path.Combine(s.LightsDir, "light_0002.fit"), new byte[100]);
        using TempWorkspace outDir = new();
        (int code, string stdout, _) = Cli.Run(s.Root, "--threads", "2", "--out", outDir.Dir);
        Assert.Equal(1, code);
        string path = Path.Combine(outDir.Dir, "darkscheck-lights.csv");
        Assert.Contains("-> scris: " + path, stdout, StringComparison.Ordinal);
        string[] csv = File.ReadAllLines(path);
        Assert.Equal(5, csv.Length);
        Assert.StartsWith("index,file,time,exposure_s,iso,error,R_sky_median,R_sky_sigma,R_local_sigma,", csv[0], StringComparison.Ordinal);
        Assert.EndsWith(",candidates,isolated_hot,amp_ratio_median,amp_ratio_q25,amp_ratio_q75", csv[0], StringComparison.Ordinal);
        int columns = csv[0].Split(',').Length;
        Assert.All(csv.Skip(1), row => Assert.Equal(columns, row.Split(',').Length));
        Assert.StartsWith("1,\"light_0001.fit\",2026-08-12 23:38:53,120,400,\"\",500,", csv[1], StringComparison.Ordinal);
        Assert.Matches(",2[0-9],(0\\.9|1\\.0)[0-9]*,0\\.[0-9]+,1\\.[0-9]+$", csv[1]);
        Assert.StartsWith("2,\"light_0002.fit\",,,,\"", csv[2], StringComparison.Ordinal);
        Assert.EndsWith("\"" + new string(',', columns - 6), csv[2], StringComparison.Ordinal);
    }

    [Fact]
    public void R056_Check9NamesBothIndexesAndCarriesTheOverlapAsTables()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 1.3));
        DarkSeriesResult r = Run(s);
        LightsAnalysis l = r.Lights!;
        ReportSection nine = DarkReport.Sections(r).Single(x => x.Title.StartsWith("9.", StringComparison.Ordinal));
        ReportSection index = DarkReport.Sections(r).Single(x => x.Title == "INDEX");
        Assert.Equal(["pixeli fierbinti", "in masterul dark", "masterul in lights", "lipsesc din master", "lights fata de dark", "ce ramane", "index dark-uri", "index lights", "verdict"], nine.Rows.Select(row => row.Label));

        // the two indexes, each on its own row: the darks' tag, and the lights' equivalent tag with the INDEX row's parenthesis
        Assert.Equal(r.Index.Tag, nine.Rows[6].Value);
        string like = Math.Round(r.LightsIndex, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
        Assert.StartsWith("idx" + like + " (de 1.30 ori cat dark-ul, adica cu 2.3 grade mai calde decat el)", nine.Rows[7].Value, StringComparison.Ordinal);
        Assert.Equal("ca un dark cu " + nine.Rows[7].Value, index.Rows.Single(row => row.Label == "lights").Value);
        Assert.Null(nine.Rows[6].Table);
        Assert.Null(nine.Rows[7].Lines);

        // the overlap rows: the console's line as before, the same figures as a table for the window
        foreach ((ReportRow row, Func<OverlapAtLevel, int> of) in new[] { (nine.Rows[1], (Func<OverlapAtLevel, int>)(o => l.Persistent)), (nine.Rows[2], o => o.DarkCount) })
        {
            ReportTable t = Assert.IsType<ReportTable>(row.Table);
            Assert.Equal(["prag", "pixeli", "procent"], t.Header);
            Assert.Equal(l.Overlap.Count, t.Rows.Count);
            for (int i = 0; i < l.Overlap.Count; i++)
            {
                OverlapAtLevel o = l.Overlap[i];
                Assert.Equal([$"peste {RunReport.F(o.LevelAdu, 0)}", $"{o.Both} din {of(o)}", $"{RunReport.F(100.0 * o.Both / of(o), 0)} %"], t.Rows[i]);
            }

            Assert.StartsWith($"peste 10: {l.Overlap[0].Both} din {of(l.Overlap[0])} ({RunReport.F(100.0 * l.Overlap[0].Both / of(l.Overlap[0]), 0)} %); peste 20: ", row.Value, StringComparison.Ordinal);
            Assert.Null(row.Lines);
        }

        Assert.StartsWith("cati dintre acesti pixeli sunt si in masterul dark, peste fiecare prag in ADU", nine.Rows[1].Table!.Note, StringComparison.Ordinal);
        Assert.Equal("cati dintre pixelii masterului de peste fiecare prag se vad si in lights", nine.Rows[2].Table!.Note);
        Assert.Contains("    index dark-uri     : " + r.Index.Tag + Environment.NewLine + "    index lights       : idx" + like + " (", Report(r), StringComparison.Ordinal);

        // the darks are the partner's (R-054): the row says so
        using SessionFixture alone = new SessionFixture().WithLights(4, i => Light(i, 1.3)).WithBiases(3);
        using SessionFixture partner = new SessionFixture().WithDarkSeries(5, Dark, inSubfolder: false);
        DarkSeriesResult taken = Run(alone, partner.Root);
        ReportSection nineAlone = DarkReport.Sections(taken).Single(x => x.Title.StartsWith("9.", StringComparison.Ordinal));
        Assert.Equal(taken.Index.Tag, nineAlone.Rows.Single(row => row.Label == "index partener").Value);
        Assert.Contains(nineAlone.Rows, row => row.Label == "index lights");

        // without lights: the one unmeasured verdict row, as before
        using SessionFixture darks = new SessionFixture().WithDarkSeries(6, Dark);
        Assert.Equal(["verdict"], DarkReport.Sections(Run(darks)).Single(x => x.Title.StartsWith("9.", StringComparison.Ordinal)).Rows.Select(row => row.Label));
    }

    [Fact]
    public void R060_TheIndexIsTheMedianOfTheRankBandOfTheUnsaturatedExcess()
    {
        // one megapixel: ranks 5 to 20, the band's median is the 13th strongest pixel
        const int w = 1000, h = 1000;
        float[] master = new float[w * h];
        Array.Fill(master, 100f);
        for (int k = 0; k < 40; k++)
        {
            master[1000 * (k + 1)] = 100f + 1000f - 10f * k;
        }

        master[7] = 4095f; // saturated, the strongest, excluded
        IndexFigures ix = IndexAnalyzer.RankIndex(master, 100, 4095, 1, w, h);
        Assert.Equal(5, ix.RankLow);
        Assert.Equal(20, ix.RankHigh);
        Assert.Equal(15, ix.BandPixels);
        Assert.Equal(w * h - 1, ix.Ranked);
        Assert.Equal(880, ix.Index, 1e-6);
        Assert.Equal("idx880", ix.Tag);

        // normalised: ISO 1600 reads a quarter
        Assert.Equal(220, IndexAnalyzer.RankIndex(master, 100, 4095, 4, w, h).Index, 1e-6);

        // nothing unsaturated: no index
        float[] white = new float[16];
        Array.Fill(white, 4095f);
        IndexFigures none = IndexAnalyzer.RankIndex(white, 100, 4095, 1, 4, 4);
        Assert.True(double.IsNaN(none.Index));
        Assert.Equal("idx-", none.Tag);
        Assert.Equal(0, none.Ranked);

        // on a 0.05 Mpx synthetic frame the band is the single strongest pixel: a lattice pixel of 120 ADU
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark);
        DarkSeriesResult r = Run(s);
        Assert.Equal(1, r.Index.RankLow);
        Assert.Equal(2, r.Index.RankHigh);
        Assert.InRange(r.Index.Index, 114, 126);
        Assert.Contains("    dark-uri           : idx1", Report(r), StringComparison.Ordinal);
        Assert.Contains(" ADU-eq: valoarea medie a pixelilor fierbinti de pe locurile 2 .. 2, numarand de la cel mai fierbinte, adica 5-20 pe fiecare Mpx din 0.05 Mpx; socotita pentru ISO 400 si 120 s)", Report(r), StringComparison.Ordinal);
    }

    [Fact]
    public void R061_TheLabelIsTheFolderNamePlusTheTagAndADarksSubfolderTakesItsParent()
    {
        using SessionFixture session = new SessionFixture().WithDarkSeries(6, Dark);
        DarkSeriesResult r = Run(session);
        Assert.Equal(Path.GetFileName(session.Root) + "-" + r.Index.Tag, r.Label);
        Assert.Contains("    eticheta           : " + r.Label, Report(r), StringComparison.Ordinal);

        using SessionFixture library = new SessionFixture().WithDarkSeries(6, Dark, inSubfolder: false);
        DarkSeriesResult rl = Run(library);
        Assert.Equal(Path.GetFileName(library.Root) + "-" + rl.Index.Tag, rl.Label);

        IndexFigures ix = new(66.5, 121, 483, 362, 24_000_000);
        Assert.Equal(Path.GetFileName(session.Root) + "-idx67", IndexAnalyzer.Label(SessionFolder.Open(session.Root), ix));
        Assert.Equal(Path.GetFileName(library.Root) + "-idx67", IndexAnalyzer.Label(SessionFolder.Open(library.Root), ix));
        Assert.Equal(Path.GetFileName(session.Root) + "-idx67", IndexAnalyzer.Label(SessionFolder.Open(session.DarksDir), ix));
    }

    [Theory]
    [InlineData("2026-10-10-ISO1600-120s-15.8celsius-idx67", true, "2026-10-10-ISO1600-120s-15.8celsius-idx67")]
    [InlineData("2026-10-10-ISO1600-120s-15.8celsius-idx60", true, "2026-10-10-ISO1600-120s-15.8celsius-idx67")]
    [InlineData("night-idx60", false, "night-idx67")]
    [InlineData("night-idx67-b", false, "night-idx67-b-idx67")]
    [InlineData("night-idx", false, "night-idx-idx67")]
    [InlineData("night-IDX60", false, "night-idx67")]
    [InlineData("night-idx-", false, "night-idx67")]
    [InlineData("night-idx-60", false, "night-idx-60-idx67")]
    [InlineData("-idx67", false, "-idx67-idx67")]
    [InlineData("-idx-", false, "-idx--idx67")]
    public void R061_AFolderAlreadyNamedWithALabelLosesItsOldSuffixBeforeTheNewOne(string folder, bool inSubfolder, string expected)
    {
        using SessionFixture fx = new SessionFixture().WithDarkSeries(1, Dark);
        string root = Path.Combine(fx.Root, folder);
        string dir = inSubfolder ? Path.Combine(root, "darks") : root;
        Directory.CreateDirectory(dir);
        File.Copy(Directory.GetFiles(fx.DarksDir)[0], Path.Combine(dir, "DSC_0001.fit"));

        IndexFigures ix = new(66.5, 121, 483, 362, 24_000_000);
        Assert.Equal(expected, IndexAnalyzer.Label(SessionFolder.Open(root), ix));
        if (inSubfolder)
        {
            Assert.Equal(expected, IndexAnalyzer.Label(SessionFolder.Open(dir), ix));
        }
    }

    [Fact]
    public void R061_AFolderRenamedOnTheUnmeasuredTagKeepsThatLabelWhileTheIndexStaysUnmeasured()
    {
        using SessionFixture fx = new SessionFixture().WithDarkSeries(1, Dark);
        string root = Path.Combine(fx.Root, "night-idx-");
        Directory.CreateDirectory(root);
        File.Copy(Directory.GetFiles(fx.DarksDir)[0], Path.Combine(root, "DSC_0001.fit"));

        IndexFigures unmeasured = new(double.NaN, 0, 0, 0, 0);
        Assert.Equal("night-idx-", IndexAnalyzer.Label(SessionFolder.Open(root), unmeasured));
    }

    [Theory]
    [InlineData(0.9, "Direct")]
    [InlineData(1.1, "Direct")]
    [InlineData(0.89, "WithOpt")]
    [InlineData(0.7, "WithOpt")]
    [InlineData(1.4, "WithOpt")]
    [InlineData(1.41, "Unmatched")]
    [InlineData(0.69, "CosmeticOnly")]
    [InlineData(0.4, "CosmeticOnly")]
    [InlineData(0.39, "Unmatched")]
    [InlineData(0.0, "Unmatched")]
    [InlineData(-0.2, "Unmatched")]
    [InlineData(double.NaN, "Unmeasured")]
    public void R062_TheMatchVerdictFollowsTheFourBounds(double ratio, string expected) => Assert.Equal(expected, IndexAnalyzer.Judge(ratio).ToString());

    [Fact]
    public void R062_TheMatchReadsTheMedianTheQuartilesAndTheDegrees()
    {
        MatchFigures m = IndexAnalyzer.Match([0.5f, 1f, 2f, 1f, 1f]);
        Assert.Equal(5, m.Pixels);
        Assert.Equal(1.0, m.Ratio, 1e-6);
        Assert.Equal(1.0, m.Q25, 1e-6);
        Assert.Equal(1.0, m.Q75, 1e-6);
        Assert.Equal(0.0, m.DeltaCelsius, 1e-6);
        Assert.Equal(MatchVerdict.Direct, m.Verdict);
        MatchFigures twice = IndexAnalyzer.Match([2f, 2f, 2f]);
        Assert.Equal(6.0, twice.DeltaCelsius, 1e-6);
        Assert.Equal(MatchVerdict.Unmatched, twice.Verdict);
        Assert.Same(MatchFigures.None, IndexAnalyzer.Match([]));
        Assert.Equal("OK: dark-ul se poate scadea asa cum e; in lights pixelii fierbinti sunt de 1.00 ori cat in dark, intre 0.9 si 1.1: dark-ul e la aceeasi temperatura", DarkReport.MatchText(m, false, "in lights"));
        Assert.Equal("ATENTIE: nu se potrivesc; in dark-urile sesiunii pixelii fierbinti sunt de 2.00 ori cat la partener: partenerul e mai rece cu 6.0 grade; e prea rece si ii pierde pe cei slabi", DarkReport.MatchText(twice, true, "in dark-urile sesiunii"));
        MatchFigures none = IndexAnalyzer.Match([-0.01f, 0.02f, 0f]);
        Assert.Equal(MatchVerdict.Unmatched, none.Verdict);
        Assert.True(double.IsNaN(none.DeltaCelsius));
        Assert.Equal("ATENTIE: nu se potrivesc: in dark-urile sesiunii nu se vad pixelii fierbinti ai partenerului (de 0.00 ori cat la partener; poate reducerea de zgomot din aparat, alt aparat sau cadre rotite?)", DarkReport.MatchText(none, true, "in dark-urile sesiunii"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R082_Check9AndTheIndexAreInPlainWords(bool english)
    {
        // the variants the fixtures reach: lights at the dark's temperature, warmer (-opt), colder (cosmetic
        // correction only), far warmer (they do not match) and without the dark's hot pixels, lights without an
        // ISO, lights against a partner, darks against a partner without lights, lights
        // that take the partner's darks as their own, a session without lights
        static SyntheticDark PartnerDark(int i, double factor) => new() { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = factor };
        using SessionFixture same = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i));
        using SessionFixture warmer = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 1.3));
        using SessionFixture colder = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 0.5));
        using SessionFixture far = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 1.6));
        using SessionFixture none = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 0));
        using SessionFixture noIso = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i), iso: null);
        using SessionFixture darks = new SessionFixture().WithDarkSeries(6, Dark);
        using SessionFixture alone = new SessionFixture().WithLights(4, i => Light(i, 1.3)).WithBiases(3);
        using SessionFixture hotter = new SessionFixture().WithDarkSeries(5, i => PartnerDark(i, 2), inSubfolder: false);
        using SessionFixture warm = new SessionFixture().WithDarkSeries(5, i => PartnerDark(i, 1.2), inSubfolder: false);
        System.Text.RegularExpressions.Regex method = english ? DarkSeriesTests.EnglishSummaryWords() : DarkSeriesTests.RomanianSummaryWords();
        using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
        {
            List<string> texts = [];
            foreach (DarkSeriesResult r in new[] { Run(same), Run(warmer), Run(colder), Run(far), Run(none), Run(noIso), Run(same, hotter.Root), Run(darks, warm.Root), Run(alone, hotter.Root), Run(darks) })
            {
                foreach (ReportSection section in DarkReport.Sections(r).Where(x => x.Title.StartsWith("9.", StringComparison.Ordinal) || x.Title == "INDEX"))
                {
                    texts.Add(section.Title);
                    texts.AddRange(section.Rows.SelectMany(row => new[] { row.Label, row.Value }));
                }
            }

            // the scenes reached their variants, so the words below were looked at
            string all = string.Join("\n", texts);
            Assert.Contains(english ? "OK: the dark can be subtracted as it is" : "OK: dark-ul se poate scadea asa cum e", all, StringComparison.Ordinal);
            Assert.Contains(english ? "WARNING: subtract the dark with -opt" : "ATENTIE: scade dark-ul cu -opt", all, StringComparison.Ordinal);
            Assert.Contains(english ? "WARNING: use the dark for the cosmetic correction only" : "ATENTIE: foloseste dark-ul doar pentru corectia cosmetica", all, StringComparison.Ordinal);
            Assert.Contains(english ? "it is too cold and misses the faint ones" : "e prea rece si ii pierde pe cei slabi", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the dark's hot pixels do not show" : "nu se vad pixelii fierbinti ai dark-ului", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the darks' were taken" : "s-au luat cele ale dark-urilor", all, StringComparison.Ordinal);
            Assert.Contains(english ? "index lights" : "index lights", all, StringComparison.Ordinal);
            Assert.Contains(english ? "times what they are at the partner, at the same settings" : "ori cat la partener, la aceleasi setari", all, StringComparison.Ordinal);
            Assert.Contains(english ? "(worked out through the session's darks)" : "(socotit prin dark-urile sesiunii)", all, StringComparison.Ordinal);
            Assert.Contains(english ? "WARNING: subtract the partner with -opt" : "ATENTIE: scade partenerul cu -opt", all, StringComparison.Ordinal);
            Assert.Contains(english ? "darks (partner)" : "dark-uri (partener)", all, StringComparison.Ordinal);
            Assert.Contains(english ? "UNMEASURED: the session has no lights" : "NEMASURAT: sesiunea nu are lights", all, StringComparison.Ordinal);
            List<string> left = texts.Where(t => method.IsMatch(t)).Select(t => $"'{method.Match(t).Value}' in: {t}").ToList();
            Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
        }
    }

    /// <summary>
    /// The console's lights table (R-086): each column name ends where its figures end, the file and
    /// the time start where theirs start, in both languages.
    /// </summary>
    /// <param name="english">True for the English pass, false for the Romanian one.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R086_TheLightsTableNamesStandAboveTheirFigures(bool english)
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i));
        DarkSeriesResult r = Run(s);
        using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
        {
            string[] lines = Report(r).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            int at = Array.IndexOf(lines, english ? "  LIGHTS TABLE" : "  TABEL PE LIGHTS");
            Assert.True(at > 0, "no lights table");
            string head = lines[at + 1][..lines[at + 1].IndexOf('[', StringComparison.Ordinal)];
            System.Text.RegularExpressions.Match[] names = System.Text.RegularExpressions.Regex.Matches(head, @"\S+").ToArray();
            System.Text.RegularExpressions.Match[] figures = System.Text.RegularExpressions.Regex.Matches(lines[at + 2], @"\S+").ToArray();
            Assert.Equal(10, names.Length);
            Assert.Equal(10, figures.Length);
            for (int c = 0; c < 10; c++)
            {
                // the file and the time are left-aligned, the rest right-aligned
                bool left = c is 1 or 2;
                Assert.True(
                    left ? names[c].Index == figures[c].Index : names[c].Index + names[c].Length == figures[c].Index + figures[c].Length,
                    $"'{names[c].Value}' does not stand above '{figures[c].Value}'");
            }
        }
    }

    /// <summary>
    /// The progress lines in plain words (R-088, ADR 0037): the phases of a run with biases, lights and
    /// a partner, and of a session that takes the partner's darks, name no part of the method, in
    /// either language.
    /// </summary>
    /// <param name="english">True for the English pass, false for the Romanian one.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R088_TheProgressLinesAreInPlainWords(bool english)
    {
        using SessionFixture full = new SessionFixture().WithDarkSeries(6, Dark, biases: 3).WithLights(3, i => Light(i));
        using SessionFixture alone = new SessionFixture().WithLights(3, i => Light(i, 1.3)).WithBiases(3);
        using SessionFixture partner = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.2 }, inSubfolder: false);
        PhaseLog phases = new();
        using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
        {
            foreach (SessionFixture s in new[] { full, alone })
            {
                DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2, MaxFrames: Th.MaxFramesInMemory, Against: partner.Root), phases);
            }
        }

        string[] expected = english
            ? ["Measuring the darks", "Measuring the lights", "Reading the biases", "Reading the darks", "Reading the partner", "Reading the partner's darks"]
            : ["Se citeste partenerul", "Se citesc bias-urile", "Se citesc dark-urile", "Se citesc dark-urile partenerului", "Se masoara dark-urile", "Se masoara lights-urile"];
        Assert.Equal(expected.Order(StringComparer.Ordinal), phases.Seen.Order(StringComparer.Ordinal));
        System.Text.RegularExpressions.Regex method = english ? PlainWords.EnglishTabWords() : PlainWords.RomanianTabWords();
        Assert.DoesNotContain(phases.Seen, method.IsMatch);
    }

    /// <summary>
    /// The console's lights table in plain words (R-086, ADR 0034): its column names and its note
    /// name no part of the method, in either language.
    /// </summary>
    /// <param name="english">True for the English pass, false for the Romanian one.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R086_TheLightsTableIsInPlainWords(bool english)
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i));
        DarkSeriesResult r = Run(s);
        using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
        {
            string header = DarkReport.LightTableHeader(r);
            string names = header[..header.IndexOf('[', StringComparison.Ordinal)];
            Assert.Equal(english ? "no file time sky noise bright single hot low high" : "nr fisier ora cer zgomot luminosi singuri fierb. jos sus", string.Join(" ", names.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
            Assert.Contains(english
                ? "[the green pixels G1; sky = how bright the sky is, ADU; noise = how much a pixel differs from its neighbours, ADU; bright = brighter than their neighbours by more than 6 times the noise;"
                : "[pixelii verzi G1; cer = cat de luminos e cerul, ADU; zgomot = cat difera un pixel de vecinii lui, ADU; luminosi = mai luminosi decat vecinii cu peste 6 ori zgomotul;", header, StringComparison.Ordinal);
            Assert.EndsWith(english ? "low, high = half of them are between these two values]" : "jos, sus = jumatate dintre ei sunt intre aceste doua valori]", header, StringComparison.Ordinal);
            System.Text.RegularExpressions.Regex method = english ? PlainWords.EnglishTabWords() : PlainWords.RomanianTabWords();
            Assert.False(method.IsMatch(header), "the method's word left: " + method.Match(header).Value);
        }
    }

    /// <summary>
    /// The console's progress line (R-025): each rewrite and the final wipe cover the whole line
    /// before them, so no tail of a longer phase is left in front of the report.
    /// </summary>
    [Fact]
    public void R025_TheProgressLineCoversWhatItRewrites()
    {
        using SessionFixture alone = new SessionFixture().WithLights(3, i => Light(i, 1.3)).WithBiases(3);
        using SessionFixture partner = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.2 }, inSubfolder: false);
        (_, _, string stderr) = Cli.Run(alone.Root, "--against", partner.Root, "--threads", "2");

        // Cli.Run turns every line ending into \n, the carriage returns that rewrite the line included
        string[] segments = stderr.Split('\n').Where(s => s.Length > 0).ToArray();
        Assert.Contains(segments, s => s.Contains("Se citesc dark-urile partenerului: ", StringComparison.Ordinal));
        for (int k = 1; k < segments.Length; k++)
        {
            Assert.True(segments[k].Length >= segments[k - 1].Length, $"'{segments[k]}' leaves the end of '{segments[k - 1]}'");
        }

        Assert.True(string.IsNullOrWhiteSpace(segments[^1]), $"the progress line is not wiped at the end: '{segments[^1]}'");
    }

    /// <summary>The phase names a run reports, from whichever thread reports them.</summary>
    private sealed class PhaseLog : IProgress<RunProgress>
    {
        private readonly object _gate = new();
        private readonly HashSet<string> _seen = [];

        /// <summary>The phases seen so far, each once.</summary>
        internal IReadOnlyCollection<string> Seen
        {
            get
            {
                lock (_gate)
                {
                    return [.. _seen];
                }
            }
        }

        /// <inheritdoc/>
        public void Report(RunProgress value)
        {
            lock (_gate)
            {
                _seen.Add(value.Phase);
            }
        }
    }

    [Fact]
    public void R063_AgainstAPartnerFolderReadsItsIndexTheRatioOnSharedPixelsAndTheOverlap()
    {
        // the partner's hot pixels are twice the session's: the session is 6 degrees colder
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i));
        using SessionFixture p = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 2 }, inSubfolder: false);
        DarkSeriesResult r = Run(s, p.Root);
        Assert.Null(r.PartnerError);
        PartnerAnalysis partner = Assert.IsType<PartnerAnalysis>(r.Partner);
        Assert.Equal(p.Root, partner.Folder);
        Assert.Equal(5, partner.Frames);
        Assert.Equal(5, partner.Used);
        Assert.Equal(400, partner.Iso);
        Assert.Equal(120, partner.ExposureSeconds);
        Assert.InRange(partner.Index.Index, 228, 252);
        Assert.Equal(Path.GetFileName(p.Root) + "-" + partner.Index.Tag, partner.Label);
        MatchFigures m = partner.OwnOverPartner;
        Assert.Equal(20, m.Pixels);
        Assert.InRange(m.Ratio, 0.46, 0.54);
        Assert.InRange(m.DeltaCelsius, -7, -5);
        Assert.Equal(MatchVerdict.CosmeticOnly, m.Verdict);
        Assert.Equal(20, partner.OwnHot50);
        Assert.Equal(20, partner.OwnHot50InPartner50);
        Assert.Equal(20, partner.PartnerHot50);
        Assert.Equal(20, partner.PartnerHot50InOwn20);

        string report = Report(r);
        Assert.Contains($"    partener           : {p.Root}: {partner.Index.Tag} (", report, StringComparison.Ordinal);
        Assert.Contains("; cadre citite 5 din 5, ISO 400, 120.0 s); eticheta " + partner.Label, report, StringComparison.Ordinal);
        Assert.Equal(0, partner.Failed);
        Assert.Null(partner.FirstError);
        Assert.Contains("    dark-uri / partener: pixelii fierbinti sunt in dark-urile sesiunii de 0.5", report, StringComparison.Ordinal);
        Assert.Contains("; 20 de pixeli fierbinti in amandoua), adica dark-urile sesiunii sunt cu 6.", report, StringComparison.Ordinal);
        Assert.Contains(" grade mai reci decat partenerul", report, StringComparison.Ordinal);
        Assert.Contains("    pixeli comuni      : 20 din 20 ai sesiunii peste 50 ADU-eq sunt peste 50 si la partener (100 %); 20 din 20 ai partenerului peste 50 sunt peste 20 in sesiune (100 %)", report, StringComparison.Ordinal);
        // the lights at the darks' temperature: the chained ratio is the darks' and so is the verdict
        Assert.Contains("    lights / partener  : pixelii fierbinti sunt in lights de 0.5", report, StringComparison.Ordinal);
        Assert.Contains("    verdict partener   : ATENTIE: foloseste partenerul doar pentru corectia cosmetica (-cc=dark: Siril gaseste in el pixelii fierbinti si ii repara in lights); in lights pixelii fierbinti sunt de 0.5", report, StringComparison.Ordinal);
        Assert.Contains("partenerul e mai cald cu 6.", report, StringComparison.Ordinal);
    }

    [Fact]
    public void R063_TheSharedPixelsRowCarriesItsTwoStatementsAsLines()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark);
        using SessionFixture p = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 2 }, inSubfolder: false);
        DarkSeriesResult r = Run(s, p.Root);
        PartnerAnalysis partner = r.Partner!;
        ReportRow shared = DarkReport.Sections(r).Single(x => x.Title == "INDEX").Rows.Single(row => row.Label == "pixeli comuni");
        IReadOnlyList<string> lines = Assert.IsAssignableFrom<IReadOnlyList<string>>(shared.Lines);
        Assert.Equal(2, lines.Count);
        Assert.Equal(string.Join("; ", lines), shared.Value);
        Assert.Equal($"{partner.OwnHot50InPartner50} din {partner.OwnHot50} ai sesiunii peste 50 ADU-eq sunt peste 50 si la partener (100 %)", lines[0]);
        Assert.Equal($"{partner.PartnerHot50InOwn20} din {partner.PartnerHot50} ai partenerului peste 50 sunt peste 20 in sesiune (100 %)", lines[1]);
        Assert.Null(shared.Table);
    }

    [Fact]
    public void R063_APartnerThatCannotBeUsedIsAnAttentionLineAndExit1()
    {
        // the console refuses a partner folder that does not open before decoding anything
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark);
        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--threads", "2", "--against", Path.Combine(s.Root, "nu-exista"));
        Assert.Equal(1, code);
        Assert.StartsWith("EROARE: ", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("=== DARK-URI", stdout, StringComparison.Ordinal);

        // the core carries the same as an error in the result
        DarkSeriesResult missing = Run(s, Path.Combine(s.Root, "nu-exista"));
        Assert.Null(missing.Partner);
        Assert.NotNull(missing.PartnerError);
        Assert.True(missing.HasErrors);
        Assert.Contains("    partener           : ATENTIE: ", Report(missing), StringComparison.Ordinal);
        Assert.DoesNotContain("dark-uri / partener", Report(missing), StringComparison.Ordinal);

        // a partner frame that fails is counted, named, and makes the run exit 1
        using SessionFixture oneBad = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }, inSubfolder: false);
        File.WriteAllBytes(Path.Combine(oneBad.Root, "DSC_0002.fit"), new byte[100]);
        (int code1, string stdout1, _) = Cli.Run(s.Root, "--threads", "2", "--against", oneBad.Root);
        Assert.Equal(1, code1);
        Assert.Contains("; cadre citite 3 din 4, ISO 400, 120.0 s); eticheta ", stdout1, StringComparison.Ordinal);
        Assert.Contains("    ATENTIE            : 1 dark al partenerului nu s-a putut folosi (primul: DSC_0002.fit: ", stdout1, StringComparison.Ordinal);
        Assert.Contains("    dark-uri / partener: pixelii fierbinti sunt in dark-urile sesiunii de 1.0", stdout1, StringComparison.Ordinal);

        // --out inside the partner folder is refused before the run, like inside the session
        using SessionFixture p = new SessionFixture().WithDarkSeries(3, Dark, inSubfolder: false);
        (int code2, string stdout2, string stderr2) = Cli.Run(s.Root, "--threads", "2", "--against", p.Root, "--out", Path.Combine(p.Root, "rapoarte"));
        Assert.Equal(1, code2);
        Assert.Contains("EROARE: --out nu poate fi folderul partenerului sau unul din el", stderr2, StringComparison.Ordinal);
        Assert.DoesNotContain("=== DARK-URI", stdout2, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(p.Root, "rapoarte")));

        // no frame of the partner decodes: the files are named, not a size of 0 x 0
        using SessionFixture broken = new SessionFixture().WithDarkSeries(2, Dark, inSubfolder: false);
        foreach (string f in Directory.GetFiles(broken.Root))
        {
            File.WriteAllBytes(f, new byte[100]);
        }

        Assert.StartsWith("niciun dark al partenerului nu s-a putut citi: DSC_0001.fit:", Run(s, broken.Root).PartnerError, StringComparison.Ordinal);

        // frames of another size
        using SessionFixture small = new SessionFixture().WithDarks(3, inSubfolder: false);
        DarkSeriesResult r = Run(s, small.Root);
        Assert.Null(r.Partner);
        Assert.Equal("partenerul are 32 x 24 px, dark-urile sesiunii 256 x 192", r.PartnerError);
        Assert.True(r.HasErrors);

        // without lights the verdict is the darks' against the partner
        using SessionFixture warm = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.2 }, inSubfolder: false);
        string report = Report(Run(s, warm.Root));
        Assert.Contains("    verdict partener   : ATENTIE: scade partenerul cu -opt (-opt: Siril il inmulteste cat sa se potriveasca cu lights-urile); ", report, StringComparison.Ordinal);
        Assert.Contains("; in dark-urile sesiunii pixelii fierbinti sunt de 0.8", report, StringComparison.Ordinal);
        Assert.DoesNotContain("lights / partener", report, StringComparison.Ordinal);
    }

    [Fact]
    public void R064_TheLightsGetAnEquivalentIndexAndTheChainedRatioAgainstThePartner()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 1.3));
        using SessionFixture p = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.3 }, inSubfolder: false);
        DarkSeriesResult r = Run(s, p.Root);
        string report = Report(r);
        Assert.Equal(r.Index.Index * r.Lights!.LightsOverDark.Ratio, r.LightsIndex, 1e-9);
        int expected = (int)Math.Round(r.LightsIndex, MidpointRounding.AwayFromZero);
        Assert.InRange(expected, 148, 164);
        Assert.InRange(r.Partner!.LightsOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.Direct, r.Partner.LightsOverPartner.Verdict);
        Assert.Contains($"    lights             : ca un dark cu idx{expected} (de 1.3", report, StringComparison.Ordinal);
        Assert.Contains(" ori cat dark-ul, adica cu 2.", report, StringComparison.Ordinal);
        Assert.Contains(" grade mai calde decat el)", report, StringComparison.Ordinal);
        // the lights are at the partner's temperature: chained ratio 1.3 x (1 / 1.3) = 1, direct subtraction
        Assert.Contains("    lights / partener  : pixelii fierbinti sunt in lights de 1.0", report, StringComparison.Ordinal);
        Assert.Contains("    verdict partener   : OK: partenerul se poate scadea asa cum e; in lights pixelii fierbinti sunt de 1.0", report, StringComparison.Ordinal);
    }

    [Fact]
    public void R054_ASessionWithoutDarksTakesThePartnersDarksAsItsOwn()
    {
        // the same lights once in a session holding the darks, once alone against the same darks as a partner
        using SessionFixture with = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }, biases: 3).WithLights(4, i => Light(i, 1.3));
        using SessionFixture alone = new SessionFixture().WithLights(4, i => Light(i, 1.3)).WithBiases(3);
        using SessionFixture library = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }, inSubfolder: false);
        DarkSeriesResult a = Run(with);
        DarkSeriesResult b = Run(alone, library.Root);

        Assert.Null(a.DarksFromPartner);
        Assert.Equal(library.Root, b.DarksFromPartner);
        Assert.Null(b.Partner);
        Assert.Null(b.PartnerError);
        Assert.False(b.HasErrors);
        Assert.Equal(a.Darks.Used, b.Darks.Used);
        Assert.Equal(a.Index.Index, b.Index.Index, 1e-9);
        // the session's label is its own, on the lights' index (R-066, ADR 0039); the partner keeps its label on its row
        Assert.Equal(Path.GetFileName(alone.Root) + "-" + IndexFigures.TagFor(b.LightsIndex), b.Label);
        Assert.Equal(Path.GetFileName(library.Root) + "-" + b.Index.Tag, b.DarksFromPartnerLabel);
        Assert.Equal(a.Lights!.Persistent, b.Lights!.Persistent);
        Assert.Equal(a.Lights.LightsOverDark.Ratio, b.Lights.LightsOverDark.Ratio, 1e-9);
        Assert.Equal(a.Lights.LightsOverDark.Verdict, b.Lights.LightsOverDark.Verdict);
        Assert.Equal(a.LightsIndex, b.LightsIndex, 1e-9);
        Assert.NotNull(b.Biases);
        Assert.Equal(Verdict.Ok, b.Inventory.Match.DarksVsLights);

        string report = Report(b);
        Assert.Contains($"=== DARK-URI (ale partenerului {library.Root}): cadre citite 5 din 5", report, StringComparison.Ordinal);
        Assert.Contains($"    darks              : (lipsa; se folosesc dark-urile partenerului {library.Root})", report, StringComparison.Ordinal);
        Assert.Contains("    expunere partener  : 120.0 s la ISO 400 (toate cadrele)", report, StringComparison.Ordinal);
        Assert.Contains("    interval darks     : (lipsa)", report, StringComparison.Ordinal);
        Assert.Contains("    potrivire          : OK: dark-urile partenerului au aceeasi expunere (120.0 s) si acelasi ISO (400) ca lights-urile", report, StringComparison.Ordinal);
        Assert.Contains("    dark-uri (partener): idx", report, StringComparison.Ordinal);
        Assert.Contains("    eticheta           : " + b.Label, report, StringComparison.Ordinal);
        Assert.Contains("    lights             : ca un dark cu idx", report, StringComparison.Ordinal);
        Assert.Contains("    verdict            : ATENTIE: scade dark-ul cu -opt (-opt: Siril il inmulteste cat sa se potriveasca cu lights-urile); in lights pixelii fierbinti sunt de 1.3", report, StringComparison.Ordinal);
        Assert.DoesNotContain("    partener           :", report, StringComparison.Ordinal);
        Assert.DoesNotContain("dark-uri / partener", report, StringComparison.Ordinal);
        Assert.DoesNotContain("lights / partener", report, StringComparison.Ordinal);

        // a partner at another ISO is flagged by check 1, as a session's own darks would be
        using SessionFixture iso800 = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }, iso: 800, inSubfolder: false);
        DarkSeriesResult c = Run(alone, iso800.Root);
        Assert.Equal(Verdict.Warning, c.Inventory.Match.DarksVsLights);
        Assert.Contains("    potrivire          : ATENTIE: dark-urile partenerului (120.0 s, ISO 800) nu au setarile lights-urilor (120.0 s, ISO 400)", Report(c), StringComparison.Ordinal);

        // without a partner the run refuses, naming what to give
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => Run(alone));
        Assert.Equal(RunReport.LightsOnlyNeedsPartner(alone.Root), e.Message);
        Assert.Contains("--against DIR in consola", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void R065_AgainstTakesAFolderAndDoesNotGoWithInventory()
    {
        (int code, _, string stderr) = Cli.Run(@"E:\x", "--against");
        Assert.Equal(1, code);
        Assert.Contains("EROARE: --against cere o valoare", stderr, StringComparison.Ordinal);
        (int code1, _, string stderr1) = Cli.Run(@"E:\x", "--against", " ");
        Assert.Equal(1, code1);
        Assert.Contains("EROARE: --against cere un folder de dark-uri", stderr1, StringComparison.Ordinal);
        (int code2, _, string stderr2) = Cli.Run(@"E:\x", "--inventory", "--against", @"E:\y");
        Assert.Equal(1, code2);
        Assert.Contains("EROARE: --inventory nu merge cu --out sau --against", stderr2, StringComparison.Ordinal);
        (_, string help, _) = Cli.Run("--help");
        Assert.Contains("[--against DIR]", help, StringComparison.Ordinal);
        Assert.Contains("--against DIR alt folder de dark-uri", help, StringComparison.Ordinal);
    }

    [Fact]
    public void R055_LightsFromAnotherFolderAreMeasuredAgainstTheOpenDarks()
    {
        // the same lights once in the session's lights\, once in another session given to the library as --lights
        using SessionFixture with = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }, biases: 3).WithLights(4, i => Light(i, 1.3));
        using SessionFixture library = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }, inSubfolder: false);
        using SessionFixture other = new SessionFixture().WithLights(4, i => Light(i, 1.3));
        DarkSeriesResult a = Run(with);
        DarkSeriesResult b = Run(library, lightsFrom: other.Root);
        DarkSeriesResult c = Run(library, lightsFrom: other.LightsDir);

        foreach (DarkSeriesResult r in new[] { b, c })
        {
            Assert.False(r.HasErrors);
            Assert.Equal(other.LightsDir, r.Inventory.Session.LightsDir);
            Assert.Equal(4, r.Inventory.Session.Lights.Count);
            Assert.Equal(a.Index.Index, r.Index.Index, 1e-9);
            Assert.Equal(Path.GetFileName(library.Root) + "-" + r.Index.Tag, r.Label);
            Assert.Equal(a.Lights!.Persistent, r.Lights!.Persistent);
            Assert.Equal(a.Lights.LightsOverDark.Ratio, r.Lights.LightsOverDark.Ratio, 1e-9);
            Assert.Equal(a.Lights.LightsOverDark.Verdict, r.Lights.LightsOverDark.Verdict);
            Assert.Equal(a.LightsIndex, r.LightsIndex, 1e-9);
            Assert.Equal(Verdict.Ok, r.Inventory.Match.DarksVsLights);

            string report = Report(r);
            Assert.Contains("    darks              : . (5 cadre .fit)", report, StringComparison.Ordinal);
            Assert.Contains($"    lights             : {other.LightsDir} (4 cadre .fit; alese de tine)", report, StringComparison.Ordinal);
            Assert.Contains("    verdict            : ATENTIE: scade dark-ul cu -opt", report, StringComparison.Ordinal);
            Assert.Contains("    lights             : ca un dark cu idx", report, StringComparison.Ordinal);
            Assert.Contains("  TABEL PE LIGHTS", report, StringComparison.Ordinal);
        }

        // the folder's own lights are left out: colder lights given to the session that holds warmer ones
        using SessionFixture cold = new SessionFixture().WithLights(4, i => Light(i, 1.0));
        DarkSeriesResult d = Run(with, lightsFrom: cold.Root);
        Assert.Equal(cold.LightsDir, d.Inventory.Session.LightsDir);
        Assert.Equal(MatchVerdict.Direct, d.Lights!.LightsOverDark.Verdict);
        Assert.InRange(d.Lights.LightsOverDark.Ratio, 0.95, 1.05);

        // a folder that does not exist or holds no frame is refused with its own words
        DirectoryNotFoundException missing = Assert.Throws<DirectoryNotFoundException>(() => Run(library, lightsFrom: Path.Combine(other.Root, "nu-exista")));
        Assert.StartsWith("lights-urile alese: nu gasesc folderul ", missing.Message, StringComparison.Ordinal);
        using TempWorkspace empty = new();
        InvalidOperationException none = Assert.Throws<InvalidOperationException>(() => Run(library, lightsFrom: empty.Dir));
        Assert.StartsWith($"lights-urile alese: niciun light in {empty.Dir} (raw: ", none.Message, StringComparison.Ordinal);

        // a session with lights and no darks still needs its partner, whichever lights it is given
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => Run(other, lightsFrom: cold.Root));
        Assert.Equal(RunReport.LightsOnlyNeedsPartner(other.Root), e.Message);
    }

    [Fact]
    public void R055_LightsTakesAFolderAndIsRefusedBeforeDecoding()
    {
        using SessionFixture library = new SessionFixture().WithDarkSeries(3, Dark, inSubfolder: false);
        using SessionFixture other = new SessionFixture().WithLights(2, i => Light(i, 1.3));

        (int code, _, string stderr) = Cli.Run(library.Root, "--lights");
        Assert.Equal(1, code);
        Assert.Contains("EROARE: --lights cere o valoare", stderr, StringComparison.Ordinal);
        (int code1, _, string stderr1) = Cli.Run(library.Root, "--lights", " ");
        Assert.Equal(1, code1);
        Assert.Contains("EROARE: --lights cere un folder de lights", stderr1, StringComparison.Ordinal);

        // refused at once, before the darks are decoded: no progress line on stderr
        (int code2, string stdout2, string stderr2) = Cli.Run(library.Root, "--lights", Path.Combine(other.Root, "nu-exista"));
        Assert.Equal(1, code2);
        Assert.Contains("EROARE: lights-urile alese: nu gasesc folderul ", stderr2, StringComparison.Ordinal);
        Assert.DoesNotContain("Se citesc", stderr2, StringComparison.Ordinal);
        Assert.DoesNotContain("=== DARK-URI", stdout2, StringComparison.Ordinal);

        // the inventory lists the given lights; --out inside their folder is refused like inside the session
        (int code3, string stdout3, _) = Cli.Run(library.Root, "--inventory", "--lights", other.Root);
        Assert.Equal(0, code3);
        Assert.Contains($"    lights             : {other.LightsDir} (2 cadre .fit; alese de tine)", stdout3, StringComparison.Ordinal);
        Assert.Contains("  LIGHTS", stdout3, StringComparison.Ordinal);
        Assert.Contains("light_0002.fit", stdout3, StringComparison.Ordinal);
        (int code4, _, string stderr4) = Cli.Run(library.Root, "--lights", other.Root, "--out", Path.Combine(other.LightsDir, "rapoarte"));
        Assert.Equal(1, code4);
        Assert.Contains("EROARE: --out nu poate fi folderul lights-urilor alese sau unul din el", stderr4, StringComparison.Ordinal);

        // --out elsewhere writes the lights CSV about the chosen lights, and the darks CSV about the library
        using TempWorkspace outDir = new();
        (int code5, string stdout5, _) = Cli.Run(library.Root, "--threads", "2", "--lights", other.Root, "--out", outDir.Dir);
        Assert.Equal(0, code5);
        Assert.Contains("    -> scris: " + Path.Combine(outDir.Dir, "darkscheck-lights.csv"), stdout5, StringComparison.Ordinal);
        string lightsCsv = File.ReadAllText(Path.Combine(outDir.Dir, "darkscheck-lights.csv"));
        Assert.Contains("light_0001.fit", lightsCsv, StringComparison.Ordinal);
        Assert.Contains("light_0002.fit", lightsCsv, StringComparison.Ordinal);
        Assert.Equal(2, lightsCsv.Split('\n').Count(l => l.Contains(".fit", StringComparison.Ordinal)));
        Assert.Contains("    lights             : " + other.LightsDir + " (2 cadre .fit; alese de tine)", File.ReadAllText(Path.Combine(outDir.Dir, "darkscheck-report.txt")), StringComparison.Ordinal);

        (_, string help, _) = Cli.Run("--help");
        Assert.Contains("[--lights DIR]", help, StringComparison.Ordinal);
        Assert.Contains("--lights DIR  lights-urile din alt folder", help, StringComparison.Ordinal);
    }

    [Fact]
    public void R066_ASessionWithoutDarksIsLabelledWithItsLightsIndexAndThePartnerKeepsItsOwn()
    {
        using SessionFixture alone = new SessionFixture().WithLights(4, i => Light(i, 1.3)).WithBiases(3);
        using SessionFixture library = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }, inSubfolder: false);
        DarkSeriesResult b = Run(alone, library.Root);

        Assert.False(double.IsNaN(b.LightsIndex));
        string like = Math.Round(b.LightsIndex, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
        Assert.Equal(Path.GetFileName(alone.Root) + "-idx" + like, b.Label);
        Assert.NotEqual(b.Index.Tag, "idx" + like);

        string report = Report(b);
        Assert.Contains("    eticheta           : " + b.Label, report, StringComparison.Ordinal);
        Assert.Contains($"    dark-uri (partener): {b.Index.Tag} (", report, StringComparison.Ordinal);
        Assert.Contains($"); eticheta {Path.GetFileName(library.Root)}-{b.Index.Tag}", report, StringComparison.Ordinal);
        Assert.Contains($"    lights             : ca un dark cu idx{like} (", report, StringComparison.Ordinal);
        Assert.DoesNotContain($"    eticheta           : {Path.GetFileName(library.Root)}", report, StringComparison.Ordinal);
        Assert.Equal("idx" + like, b.LabelTag);

        // the same session given other lights (R-055), at the partner's level: its own lights are left out,
        // and the label follows the chosen lights' index
        using SessionFixture same = new SessionFixture().WithLights(4, i => Light(i, 1.0));
        DarkSeriesResult c = Run(alone, library.Root, lightsFrom: same.Root);
        Assert.Equal(library.Root, c.DarksFromPartner);
        Assert.Equal(same.LightsDir, c.Inventory.Session.LightsDir);
        Assert.Equal(MatchVerdict.Direct, c.Lights!.LightsOverDark.Verdict);
        Assert.Equal(Path.GetFileName(alone.Root) + "-" + IndexFigures.TagFor(c.LightsIndex), c.Label);
        Assert.Equal(IndexFigures.TagFor(c.LightsIndex), c.LabelTag);
        Assert.Equal(Path.GetFileName(library.Root) + "-" + c.Index.Tag, c.DarksFromPartnerLabel);

        // a session with darks keeps the darks' label, and so does a library given lights (R-061)
        using SessionFixture with = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }).WithLights(4, i => Light(i, 1.3));
        DarkSeriesResult a = Run(with);
        Assert.Equal(Path.GetFileName(with.Root) + "-" + a.Index.Tag, a.Label);
        Assert.DoesNotContain("; eticheta ", Report(a), StringComparison.Ordinal);
    }
}
