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

/// <summary>Check 2 (the lights against the dark), the index, the label and the matching, on synthetic frames with known answers (R-050 to R-065).</summary>
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

    /// <summary>
    /// CR-006: lights at ISO 800 against darks at ISO 400, at the same temperature, read a ratio of 1.00 and
    /// "the dark can be subtracted as it is", while in the files each hot pixel is twice as high in the light.
    /// </summary>
    [Fact]
    public void R052_LightsAtOtherSettingsThanTheDarksAreNeverSubtractedAsTheyAre()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i, 2.0), iso: 800);
        DarkSeriesResult r = Run(s);
        LightsAnalysis l = r.Lights!;
        // the temperature is the same, and the ratio still says so
        Assert.InRange(l.LightsOverDark.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.WithOpt, l.LightsOverDark.Verdict);
        string report = Report(r);
        Assert.DoesNotContain("se poate scadea asa cum e", report, StringComparison.Ordinal);
        Assert.Contains("    verdict            : ATENTIE: nu scadea dark-ul asa cum e: dark-urile sunt facute la alte setari decat lights-urile (120.0 s, ISO 400 fata de 120.0 s, ISO 800), asa ca in fisiere pixelii fierbinti din lights sunt de 2.0", report, StringComparison.Ordinal);
        Assert.Contains(" ori cat in dark. Cel mai bine fa dark-uri la setarile lights-urilor. Cu aceste dark-uri, scade cu -opt (-opt: Siril inmulteste dark-ul cat sa se potriveasca cu lights-urile; are nevoie si de bias-uri) sau foloseste dark-ul doar pentru corectia cosmetica (-cc=dark); socotiti pentru aceleasi setari, pixelii fierbinti sunt de ", report, StringComparison.Ordinal);
        Assert.Contains(" ori cat in dark: dark-ul e la aceeasi temperatura", report, StringComparison.Ordinal);
        Assert.Contains("; pe 20 de pixeli; socotit pentru ISO-ul si expunerea dark-urilor, in fisiere de 2.0", report, StringComparison.Ordinal);
        // the verdict row is the one the window draws large and red (R-124)
        Assert.True(DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Lights).Rows.Single(row => row.Label == "verdict").Alert);
        using (Ui.Use(UiLanguage.English))
        {
            string english = Report(r);
            Assert.Contains("    verdict            : WARNING: do not subtract the dark as it is: the darks are made at other settings than the lights (120.0 s, ISO 400 against 120.0 s, ISO 800), so in the files the hot pixels of the lights are 2.0", english, StringComparison.Ordinal);
            Assert.Contains(" times what they are in the dark. Best make darks at the lights' settings. With these darks, subtract with -opt (-opt: Siril multiplies the dark so that it matches the lights; it needs the biases too) or use the dark for the cosmetic correction only (-cc=dark); worked out for the same settings, the hot pixels are ", english, StringComparison.Ordinal);
            Assert.Contains(" times what they are in the dark: the dark is at the same temperature", english, StringComparison.Ordinal);
            Assert.Contains("; on 20 pixels; worked out for the darks' ISO and exposure, in the files 2.0", english, StringComparison.Ordinal);
        }

        // each light's point on the window's chart is judged the same way: never as it is
        Assert.Equal(MatchVerdict.WithOpt, l.Judge(1.0));
        Assert.Equal(MatchVerdict.CosmeticOnly, l.Judge(0.5));

        // a dark much warmer than such lights keeps its verdict, and its ratio is said to be worked out
        using SessionFixture warm = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i, 1.1), iso: 800);
        DarkSeriesResult rw = Run(warm);
        Assert.Equal(MatchVerdict.CosmeticOnly, rw.Lights!.LightsOverDark.Verdict);
        Assert.Contains(" ori cat in dark, socotiti pentru aceleasi setari: dark-ul e mai cald cu ", Report(rw), StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Contains(" times what they are in the dark, worked out for the same settings: the dark is warmer by ", Report(rw), StringComparison.Ordinal);
        }

        // "the lights are at other settings" needs more than half of them: with two of four the lights are judged as before
        using SessionFixture half = new SessionFixture().WithDarkSeries(6, Dark).WithLights(2, i => Light(i));
        for (int i = 2; i < 4; i++)
        {
            File.WriteAllBytes(Path.Combine(half.LightsDir, $"light_{i + 1:0000}.fit"), Light(i, 2.0).Build(SessionFixture.FirstStart.AddSeconds(-3000 + i * 150), 120, 800));
        }

        LightsAnalysis lh = Run(half).Lights!;
        Assert.Equal(4, lh.Used);
        Assert.Equal(2, lh.Frames.Count(f => f.OtherSettings));
        Assert.Null(lh.Other);
        Assert.Equal(MatchVerdict.Direct, lh.Judge(1.0));
        // and with three of four they are
        File.WriteAllBytes(Path.Combine(half.LightsDir, "light_0002.fit"), Light(1, 2.0).Build(SessionFixture.FirstStart.AddSeconds(-3500), 120, 800));
        LightsAnalysis lm = Run(half).Lights!;
        Assert.Equal(3, lm.Frames.Count(f => f.OtherSettings));
        Assert.NotNull(lm.Other);
        Assert.Equal(MatchVerdict.WithOpt, lm.Judge(1.0));

        // the same lights at the darks' ISO, with the hot pixels as high as the dark's, are subtracted as they are
        using SessionFixture same = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i));
        Assert.Contains("    verdict            : OK: dark-ul se poate scadea asa cum e", Report(Run(same)), StringComparison.Ordinal);
    }

    /// <summary>
    /// CR-006: at other settings a plain subtraction of the dark leaves half of each hot pixel of the light,
    /// while the row said nothing was left: the trace was read on the ratio brought to one setting.
    /// </summary>
    [Fact]
    public void R067_AtOtherSettingsWhatASubtractionLeavesIsWorkedOutOnTheFiles()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i, 2.0), iso: 800);
        DarkSeriesResult r = Run(s);
        // 240 ADU in the light, 120 taken by the dark: a rest of 120 ADU over a light's noise of about 6.6
        Assert.InRange(r.Lights!.Traces.Median, 14, 23);
        string report = Report(r);
        Assert.Contains("    ce ramane          : scaderea ia prea putin: la un pixel fierbinte ramane in medie un rest de 1", report, StringComparison.Ordinal);
        Assert.Contains(" ori cat zgomotul unui light; la 20 din 20 restul trece de zgomot, la 20 de 3 ori zgomotul", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// CR-002: darks that do not say their ISO were read at factor 1 against lights at their own, so
    /// lights at ISO 1600 and the darks' temperature read a quarter of the dark.
    /// </summary>
    [Fact]
    public void R052_DarksThatDoNotSayTheirSettingsAreComparedWithTheLightsAsMadeAtTheSame()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark, iso: null).WithLights(3, i => Light(i), iso: 1600);
        DarkSeriesResult r = Run(s);
        LightsAnalysis l = r.Lights!;
        Assert.False(r.Darks.SaysSettings);
        Assert.InRange(l.LightsOverDark.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.Direct, l.LightsOverDark.Verdict);
        // the lights say their settings: they are not the ones assumed, the darks are
        Assert.Equal(0, l.AssumedSettings);
        Assert.Equal(3, l.DarksSilent);
        Assert.InRange(r.LightsIndex, 0.95 * r.Index.Index, 1.05 * r.Index.Index);
        // check 9 reads the lights' noise at the same factor: about 6.5 ADU, not a quarter of it
        Assert.InRange(r.Verdicts.Enough.Stack!.LightNoise, 5.5, 7.8);

        string report = Report(r);
        Assert.Contains("    ATENTIE            : dark-urile nu spun ISO-ul sau expunerea; ce nu spun s-a luat ca la lights-uri", report, StringComparison.Ordinal);
        Assert.DoesNotContain("s-au luat cele ale dark-urilor", report, StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Contains("    WARNING            : the darks do not give their ISO or exposure; what they do not give was taken as the lights'", Report(r), StringComparison.Ordinal);
        }

        // darks and lights that both say theirs: no such line; and when neither gives the ISO no set took it from the
        // other: the row of what neither gives stands in place of the lights' own line (ADR 0060)
        using SessionFixture both = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i));
        DarkSeriesResult rb = Run(both);
        Assert.True(rb.Darks.SaysSettings);
        Assert.Equal(0, rb.Lights!.DarksSilent);
        Assert.DoesNotContain("ce nu spun s-a luat ca la", Report(rb), StringComparison.Ordinal);
        using SessionFixture neither = new SessionFixture().WithDarkSeries(6, Dark, iso: null).WithLights(3, i => Light(i), iso: null);
        DarkSeriesResult rn = Run(neither);
        Assert.Equal(0, rn.Lights!.AssumedSettings);
        Assert.Equal(0, rn.Lights.DarksSilent);
        Assert.Equal(UnsaidSettings.Iso, rn.Lights.UnsaidByBoth);
        Assert.InRange(rn.Lights.LightsOverDark.Ratio, 0.95, 1.05);
        Assert.DoesNotContain("ce nu spun s-a luat ca la", Report(rn), StringComparison.Ordinal);
        Assert.DoesNotContain("s-au luat cele ale dark-urilor", Report(rn), StringComparison.Ordinal);
        Assert.Contains("    ATENTIE            : nici dark-urile, nici lights-urile nu spun ISO-ul; s-a luat ca fiind acelasi", Report(rn), StringComparison.Ordinal);
    }

    /// <summary>CR-002 (c): the INDEX row said "worked out for ISO 400 and 120 s" of a set that does not say its ISO.</summary>
    [Fact]
    public void R060_TheIndexRowSaysWhenTheFilesDoNotGiveTheSettings()
    {
        using SessionFixture silent = new SessionFixture().WithDarkSeries(6, Dark, iso: null);
        using SessionFixture known = new SessionFixture().WithDarkSeries(6, Dark);
        DarkSeriesResult rs = Run(silent), rk = Run(known);
        Assert.Contains("; in ADU din fisier, pentru ca fisierele nu spun ISO-ul sau expunerea)", Report(rs), StringComparison.Ordinal);
        Assert.DoesNotContain("socotita pentru ISO 400", Report(rs), StringComparison.Ordinal);
        Assert.Contains("; socotita pentru ISO 400 si 120 s)", Report(rk), StringComparison.Ordinal);
        Assert.DoesNotContain("in ADU din fisier, pentru ca", Report(rk), StringComparison.Ordinal);
        // and the figure's unit follows: ADU for the set that does not say its settings, ADU-eq for the one that does
        Assert.Matches(@"dark-uri           : idx\d+ \(\d+\.\d ADU: valoarea medie a pixelilor fierbinti", Report(rs));
        Assert.DoesNotContain(" ADU-eq: valoarea medie", Report(rs), StringComparison.Ordinal);
        Assert.Matches(@"dark-uri           : idx\d+ \(\d+\.\d ADU-eq: valoarea medie a pixelilor fierbinti", Report(rk));
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Matches(@"darks              : idx\d+ \(\d+\.\d ADU: the average value of the hot pixels", Report(rs));
            Assert.Matches(@"darks              : idx\d+ \(\d+\.\d ADU-eq: the average value of the hot pixels", Report(rk));
            Assert.Contains("; in ADU of the file, as the files do not give the ISO or the exposure)", Report(rs), StringComparison.Ordinal);
            Assert.DoesNotContain("worked out for ISO 400", Report(rs), StringComparison.Ordinal);
            Assert.Contains("; worked out for ISO 400 and 120 s)", Report(rk), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// CR-002 (b): a partner that does not say its ISO was read at factor 1 against darks at their own,
    /// so the same darks at ISO 1600 read 0.25 of it, and 4 times it the other way round.
    /// </summary>
    [Fact]
    public void R063_APartnerOrASessionThatDoesNotSayItsSettingsIsComparedAsMadeAtTheSame()
    {
        static SyntheticDark PartnerDark(int i) => new() { Seed = 700 + i, Hot = SyntheticLight.Lattice };
        using SessionFixture at1600 = new SessionFixture().WithDarkSeries(6, Dark, iso: 1600);
        using SessionFixture silent = new SessionFixture().WithDarkSeries(6, Dark, iso: null);
        using SessionFixture partnerSilent = new SessionFixture().WithDarkSeries(5, PartnerDark, iso: null, inSubfolder: false);
        using SessionFixture partnerAt1600 = new SessionFixture().WithDarkSeries(5, PartnerDark, iso: 1600, inSubfolder: false);

        // the partner is the silent one
        DarkSeriesResult a = Run(at1600, partnerSilent.Root);
        PartnerAnalysis pa = Assert.IsType<PartnerAnalysis>(a.Partner);
        Assert.Equal(SilentSide.Partner, pa.Silent);
        Assert.Equal(20, pa.OwnOverPartner.Pixels);
        Assert.InRange(pa.OwnOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.Direct, pa.OwnOverPartner.Verdict);
        // the overlap levels are read at one factor too: the same pixels on both sides
        Assert.Equal(pa.OwnHot50, pa.PartnerHot50);
        // each index stays at its own factor
        Assert.Equal(1, pa.Norm);
        Assert.InRange(pa.Index.Index, 114, 126);
        string ra = Report(a);
        Assert.Contains(", 120.0 s); eticheta ", ra, StringComparison.Ordinal);
        // the partner's own figure is in the file's ADU then, and the row says ADU, not ADU-eq
        Assert.Matches(@"partener           : .*: idx\d+ \(\d+\.\d ADU; cadre citite 5 din 5, 120\.0 s\); eticheta ", ra);
        Assert.Contains("    ATENTIE            : dark-urile partenerului nu spun ISO-ul sau expunerea; ce nu spun s-a luat ca la dark-urile sesiunii", ra, StringComparison.Ordinal);
        Assert.Contains("    dark-uri / partener: pixelii fierbinti sunt in dark-urile sesiunii de 1.0", ra, StringComparison.Ordinal);

        // the count above is 0 on both sides (120 ADU against a level of 200), so the level itself is pinned here:
        // darks 2.5 times as hot pass the session's 200 ADU in the file (50 ADU-eq at ISO 1600), and the silent
        // partner's 120 ADU, read at the same factor, stay under it; at its own factor of 1 they passed 50
        using SessionFixture hotter = new SessionFixture().WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice, HotFactor = 2.5 }, iso: 1600);
        PartnerAnalysis ph = Assert.IsType<PartnerAnalysis>(Run(hotter, partnerSilent.Root).Partner);
        Assert.Equal(SilentSide.Partner, ph.Silent);
        Assert.InRange(ph.OwnOverPartner.Ratio, 2.4, 2.6);
        Assert.Equal(20, ph.OwnHot50);
        Assert.Equal(0, ph.OwnHot50InPartner50);
        Assert.Equal(0, ph.PartnerHot50);

        // the session is the silent one
        DarkSeriesResult b = Run(silent, partnerAt1600.Root);
        PartnerAnalysis pb = Assert.IsType<PartnerAnalysis>(b.Partner);
        Assert.Equal(SilentSide.Own, pb.Silent);
        Assert.Equal(20, pb.OwnOverPartner.Pixels);
        Assert.InRange(pb.OwnOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(20, pb.OwnHot50);
        Assert.Equal(pb.OwnHot50, pb.PartnerHot50);
        Assert.Equal(4, pb.Norm);
        string rb = Report(b);
        Assert.Contains("    ATENTIE            : dark-urile sesiunii nu spun ISO-ul sau expunerea; ce nu spun s-a luat ca la partener", rb, StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Contains("    WARNING            : the partner's darks do not give their ISO or exposure; what they do not give was taken as the session's darks'", Report(a), StringComparison.Ordinal);
            Assert.Contains("    WARNING            : the session's darks do not give their ISO or exposure; what they do not give was taken as the partner's", Report(b), StringComparison.Ordinal);
        }

        // both give both settings: each at its own factor as before, and no line
        DarkSeriesResult c = Run(at1600, partnerAt1600.Root);
        Assert.Equal(SilentSide.None, c.Partner!.Silent);
        Assert.InRange(c.Partner.OwnOverPartner.Ratio, 0.95, 1.05);
        Assert.DoesNotContain("ce nu spun s-a luat ca la", Report(c), StringComparison.Ordinal);
        Assert.DoesNotContain("s-a luat ca fiind", Report(c), StringComparison.Ordinal);
        Assert.Matches(@"partener           : .*: idx\d+ \(\d+\.\d ADU-eq; cadre citite 5 din 5, ISO 1600, 120\.0 s\); eticheta ", Report(c));
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Matches(@"partner            : .*: idx\d+ \(\d+\.\d ADU; frames read 5 of 5, 120\.0 s\); label ", Report(a));
            Assert.Matches(@"partner            : .*: idx\d+ \(\d+\.\d ADU-eq; frames read 5 of 5, ISO 1600, 120\.0 s\); label ", Report(c));
        }
        DarkSeriesResult d = Run(silent, partnerSilent.Root);
        Assert.Equal(SilentSide.None, d.Partner!.Silent);
        Assert.InRange(d.Partner.OwnOverPartner.Ratio, 0.95, 1.05);
        // neither gives the ISO: no set took anything from the other, and the row says what was taken as the same (ADR 0060)
        Assert.Equal(UnsaidSettings.Iso, d.Partner.UnsaidByBoth);
        Assert.DoesNotContain("ce nu spun s-a luat ca la", Report(d), StringComparison.Ordinal);
        Assert.Contains("    ATENTIE            : nici dark-urile sesiunii, nici ale partenerului nu spun ISO-ul; s-a luat ca fiind acelasi", Report(d), StringComparison.Ordinal);
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
        // no index for such lights (R-064): the old bound, at most 0.05 of the darks' index, let any negative through (CR-004)
        Assert.True(double.IsNaN(r.LightsIndex), $"lights' index {r.LightsIndex}");
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
        foreach (ReportRow row in DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Lights).Rows.Where(x => x.Label is "ce ramane" or "verdict"))
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
    public void R053_WithoutLightsCheck2IsUnmeasuredAndTheIndexHasNoLightsLine()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark);
        DarkSeriesResult r = Run(s);
        Assert.Null(r.Lights);
        Assert.Null(r.LightsError);
        Assert.True(double.IsNaN(r.LightsIndex));
        string report = Report(r);
        Assert.Contains("  2. CAT DE BINE SE POTRIVESTE DARK-UL CU LIGHTS-URILE\n    verdict            : NEMASURAT: sesiunea nu are lights", report.ReplaceLineEndings("\n"), StringComparison.Ordinal);
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

        // no light decodes: check 2 says why, the run exits 1
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
    public void R056_Check2NamesBothIndexesAndCarriesTheOverlapAsTables()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 1.3));
        DarkSeriesResult r = Run(s);
        LightsAnalysis l = r.Lights!;
        ReportSection check2 = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Lights);
        ReportSection index = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Index);
        Assert.Equal(["pixeli fierbinti", "in masterul dark", "masterul in lights", "lipsesc din master", "lights fata de dark", "ce ramane", "index dark-uri", "index lights", "verdict"], check2.Rows.Select(row => row.Label));

        // the two indexes, each on its own row: the darks' tag, and the lights' equivalent tag with the INDEX row's parenthesis
        Assert.Equal(r.Index.Tag, check2.Rows[6].Value);
        string like = Math.Round(r.LightsIndex, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
        Assert.StartsWith("idx" + like + " (de 1.30 ori cat dark-ul, adica cu 2.3 grade mai calde decat el)", check2.Rows[7].Value, StringComparison.Ordinal);
        Assert.Equal("ca un dark cu " + check2.Rows[7].Value, index.Rows.Single(row => row.Label == "lights").Value);
        Assert.Null(check2.Rows[6].Table);
        Assert.Null(check2.Rows[7].Lines);

        // the overlap rows: the console's line as before, the same figures as a table for the window
        foreach ((ReportRow row, Func<OverlapAtLevel, int> of) in new[] { (check2.Rows[1], (Func<OverlapAtLevel, int>)(o => l.Persistent)), (check2.Rows[2], o => o.DarkCount) })
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

        Assert.StartsWith("cati dintre acesti pixeli sunt si in masterul dark, peste fiecare prag in ADU", check2.Rows[1].Table!.Note, StringComparison.Ordinal);
        Assert.Equal("cati dintre pixelii masterului de peste fiecare prag se vad si in lights", check2.Rows[2].Table!.Note);
        Assert.Contains("    index dark-uri     : " + r.Index.Tag + Environment.NewLine + "    index lights       : idx" + like + " (", Report(r), StringComparison.Ordinal);

        // the darks are the partner's (R-054): the row says so
        using SessionFixture alone = new SessionFixture().WithLights(4, i => Light(i, 1.3)).WithBiases(3);
        using SessionFixture partner = new SessionFixture().WithDarkSeries(5, Dark, inSubfolder: false);
        DarkSeriesResult taken = Run(alone, partner.Root);
        ReportSection check2Alone = DarkReport.Sections(taken).Single(x => x.Check == ReportCheck.Lights);
        Assert.Equal(taken.Index.Tag, check2Alone.Rows.Single(row => row.Label == "index partener").Value);
        Assert.Contains(check2Alone.Rows, row => row.Label == "index lights");

        // without lights: the one unmeasured verdict row, as before
        using SessionFixture darks = new SessionFixture().WithDarkSeries(6, Dark);
        Assert.Equal(["verdict"], DarkReport.Sections(Run(darks)).Single(x => x.Check == ReportCheck.Lights).Rows.Select(row => row.Label));
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
    public void R082_Check2AndTheIndexAreInPlainWords(bool english)
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
                foreach (ReportSection section in DarkReport.Sections(r).Where(x => x.Check == ReportCheck.Lights || x.Check == ReportCheck.Index))
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

    /// <summary>
    /// CR-006: a partner at ISO 800 at the session's temperature read "the partner can be subtracted as it is",
    /// while in the files its hot pixels are twice the session's.
    /// </summary>
    [Fact]
    public void R063_APartnerAtOtherSettingsThanTheSessionIsNeverSubtractedAsItIs()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark);
        using SessionFixture p = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 2 }, iso: 800, inSubfolder: false);
        DarkSeriesResult r = Run(s, p.Root);
        PartnerAnalysis partner = Assert.IsType<PartnerAnalysis>(r.Partner);
        Assert.InRange(partner.OwnOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.WithOpt, partner.OwnOverPartner.Verdict);
        string report = Report(r);
        Assert.DoesNotContain("se poate scadea asa cum e", report, StringComparison.Ordinal);
        Assert.Contains("    verdict partener   : ATENTIE: nu scadea partenerul asa cum e: e facut la alte setari decat dark-urile sesiunii (120.0 s, ISO 800 fata de 120.0 s, ISO 400), asa ca in fisiere pixelii fierbinti din dark-urile sesiunii sunt de 0.5", report, StringComparison.Ordinal);
        Assert.Contains(" ori cat la partener. Cel mai bine foloseste dark-uri facute la setarile lights-urilor. Cu acest partener, scade cu -opt (-opt: Siril inmulteste partenerul cat sa se potriveasca cu lights-urile; are nevoie si de bias-uri) sau foloseste-l doar pentru corectia cosmetica (-cc=dark); socotiti pentru aceleasi setari, pixelii fierbinti sunt de ", report, StringComparison.Ordinal);
        Assert.Contains(" ori cat la partener: partenerul e la aceeasi temperatura", report, StringComparison.Ordinal);

        // with lights at the session's settings the chained row carries the verdict, against the lights' settings
        using SessionFixture withLights = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i));
        DarkSeriesResult rl = Run(withLights, p.Root);
        Assert.Equal(MatchVerdict.WithOpt, rl.Partner!.LightsOverPartner.Verdict);
        Assert.Contains("    verdict partener   : ATENTIE: nu scadea partenerul asa cum e: e facut la alte setari decat lights-urile (120.0 s, ISO 800 fata de 120.0 s, ISO 400), asa ca in fisiere pixelii fierbinti din lights sunt de 0.5", Report(rl), StringComparison.Ordinal);
        // the session's own darks are at the lights' settings and are subtracted as they are
        Assert.Contains("    verdict            : OK: dark-ul se poate scadea asa cum e", Report(rl), StringComparison.Ordinal);
        Assert.True(DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Index).Rows.Single(row => row.Label == "verdict partener").Alert);
        Assert.True(DarkReport.Sections(rl).Single(x => x.Check == ReportCheck.Index).Rows.Single(row => row.Label == "verdict partener").Alert);
        using (Ui.Use(UiLanguage.English))
        {
            string english = Report(r);
            Assert.Contains("    partner verdict    : WARNING: do not subtract the partner as it is: it is made at other settings than the session's darks (120.0 s, ISO 800 against 120.0 s, ISO 400), so in the files the hot pixels of the session's darks are 0.5", english, StringComparison.Ordinal);
            Assert.Contains(" times what they are at the partner. Best use darks made at the lights' settings. With this partner, subtract with -opt (-opt: Siril multiplies the partner so that it matches the lights; it needs the biases too) or use it for the cosmetic correction only (-cc=dark); worked out for the same settings, the hot pixels are ", english, StringComparison.Ordinal);
            Assert.Contains("    partner verdict    : WARNING: do not subtract the partner as it is: it is made at other settings than the lights (120.0 s, ISO 800 against 120.0 s, ISO 400), so in the files the hot pixels of the lights are 0.5", Report(rl), StringComparison.Ordinal);
        }

        // lights at the partner's settings and temperature, darks at others: the partner fits the lights as it is,
        // and it is the session's own dark that does not
        using SessionFixture atPartner = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i, 2.0), iso: 800);
        DarkSeriesResult ra = Run(atPartner, p.Root);
        Assert.NotNull(ra.Lights!.Other);
        Assert.NotNull(ra.Partner!.Other);
        Assert.Null(ra.Partner.LightsOther);
        Assert.Equal(MatchVerdict.Direct, ra.Partner.LightsOverPartner.Verdict);
        Assert.Contains("    verdict partener   : OK: partenerul se poate scadea asa cum e", Report(ra), StringComparison.Ordinal);
        Assert.Contains("    verdict            : ATENTIE: nu scadea dark-ul asa cum e", Report(ra), StringComparison.Ordinal);

        // lights, darks and partner at three ISOs and one temperature: in the files the lights are 2 times the
        // darks, the darks a quarter of the partner, so the lights half of the partner
        using SessionFixture p1600 = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 4 }, iso: 1600, inSubfolder: false);
        DarkSeriesResult r3 = Run(atPartner, p1600.Root);
        Assert.InRange(r3.Partner!.Other!.RatioInFiles, 0.24, 0.26);
        Assert.InRange(r3.Partner.LightsOther!.RatioInFiles, 0.48, 0.52);
        Assert.Equal(MatchVerdict.WithOpt, r3.Partner.LightsOverPartner.Verdict);
        Assert.Contains("e facut la alte setari decat lights-urile (120.0 s, ISO 1600 fata de 120.0 s, ISO 800), asa ca in fisiere pixelii fierbinti din lights sunt de 0.5", Report(r3), StringComparison.Ordinal);
    }

    /// <summary>
    /// The review of the CR-006 fix: lights that do not say their settings are taken at the darks' (ADR 0053),
    /// so against a partner at other settings than the darks they are at other settings too; the chained
    /// verdict read "the partner can be subtracted as it is", the lights not being counted as other.
    /// </summary>
    [Fact]
    public void R064_LightsTakenAtTheDarksSettingsFollowTheDarksAgainstAPartnerAtOtherSettings()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i), iso: null);
        using SessionFixture p = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 2 }, iso: 800, inSubfolder: false);
        DarkSeriesResult r = Run(s, p.Root);
        Assert.Equal(3, r.Lights!.AssumedSettings);
        PartnerAnalysis partner = Assert.IsType<PartnerAnalysis>(r.Partner);
        Assert.InRange(partner.LightsOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.WithOpt, partner.LightsOverPartner.Verdict);
        string report = Report(r);
        Assert.DoesNotContain("partenerul se poate scadea asa cum e", report, StringComparison.Ordinal);
        // the lights' settings are the darks', which they were taken at
        Assert.Contains("    verdict partener   : ATENTIE: nu scadea partenerul asa cum e: e facut la alte setari decat lights-urile (120.0 s, ISO 800 fata de 120.0 s, ISO 400), asa ca in fisiere pixelii fierbinti din lights sunt de 0.5", report, StringComparison.Ordinal);
        Assert.True(DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Index).Rows.Single(row => row.Label == "verdict partener").Alert);
    }

    /// <summary>
    /// The second review: the same with the silent set swapped. A partner that does not say its settings is
    /// taken at the darks' (ADR 0053), so lights at other settings than the darks are at other settings than
    /// it too, and the chained verdict read "the partner can be subtracted as it is".
    /// </summary>
    [Fact]
    public void R064_APartnerThatDoesNotSayItsSettingsStandsWhereTheDarksStandForTheLights()
    {
        // darks ISO 400; lights ISO 800 with twice the amplitude; a partner as hot as the darks, without an ISO
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i, 2.0), iso: 800);
        using SessionFixture silentPartner = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }, iso: null, inSubfolder: false);
        DarkSeriesResult r = Run(s, silentPartner.Root);
        PartnerAnalysis partner = Assert.IsType<PartnerAnalysis>(r.Partner);
        Assert.Equal(SilentSide.Partner, partner.Silent);
        Assert.Null(partner.Other);
        Assert.InRange(partner.LightsOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.WithOpt, partner.LightsOverPartner.Verdict);
        // in the files the lights are twice the darks, and the darks as high as the partner
        Assert.InRange(partner.LightsOther!.RatioInFiles, 1.9, 2.1);
        string report = Report(r);
        Assert.DoesNotContain("partenerul se poate scadea asa cum e", report, StringComparison.Ordinal);
        // the partner's settings are the darks', which it was taken at
        Assert.Contains("    verdict partener   : ATENTIE: nu scadea partenerul asa cum e: e facut la alte setari decat lights-urile (120.0 s, ISO 400 fata de 120.0 s, ISO 800), asa ca in fisiere pixelii fierbinti din lights sunt de 2.0", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// CR-077: with darks that give no ISO between lights at ISO 800 and a partner at ISO 400, the darks' ISO was
    /// taken as the lights' in one pair and as the partner's in the other, so it dropped out of the chain and the
    /// two ISOs the files do give were never compared: lights as hot in the files as the partner read 1.00 and
    /// "the partner can be subtracted as it is". The lights are compared with the partner on the settings those
    /// two give; a missing one is taken through the darks.
    /// </summary>
    [Fact]
    public void R064_TheLightsAreComparedWithThePartnerOnTheSettingsThoseTwoGive()
    {
        static SyntheticDark Partner(int i) => new() { Seed = 700 + i, Hot = SyntheticLight.Lattice };
        using SessionFixture p400 = new SessionFixture().WithDarkSeries(5, Partner, inSubfolder: false);

        // darks without an ISO; lights at ISO 800 and a partner at ISO 400, all as hot in the files: at twice the
        // ISO the lights' hot pixels are half the partner's
        using SessionFixture silentDarks = new SessionFixture().WithDarkSeries(6, Dark, iso: null).WithLights(3, i => Light(i), iso: 800);
        DarkSeriesResult rd = Run(silentDarks, p400.Root);
        Assert.Equal(SilentSide.Own, rd.Partner!.Silent);
        Assert.Equal(3, rd.Lights!.DarksSilent);
        Assert.Null(rd.Lights.Other);
        Assert.Null(rd.Partner.Other);
        Assert.InRange(rd.Partner.LightsOverPartner.Ratio, 0.47, 0.53);
        Assert.Equal(MatchVerdict.CosmeticOnly, rd.Partner.LightsOverPartner.Verdict);
        Assert.InRange(rd.Partner.LightsOther!.RatioInFiles, 0.95, 1.05);
        string cold = Report(rd);
        Assert.DoesNotContain("partenerul se poate scadea asa cum e", cold, StringComparison.Ordinal);
        // half the partner's: 6 degrees colder by the doubling rule
        Assert.InRange(rd.Partner.LightsOverPartner.DeltaCelsius, -6.6, -5.4);
        Assert.Contains("    lights / partener  : pixelii fierbinti sunt in lights de 0.", cold, StringComparison.Ordinal);
        Assert.Contains("    verdict partener   : ATENTIE: foloseste partenerul doar pentru corectia cosmetica", cold, StringComparison.Ordinal);
        Assert.Contains(" ori cat la partener, socotiti pentru aceleasi setari: partenerul e mai cald cu ", cold, StringComparison.Ordinal);
        Assert.True(DarkReport.Sections(rd).Single(x => x.Check == ReportCheck.Index).Rows.Single(row => row.Label == "verdict partener").Alert);

        // the same with lights twice as high: the same temperature as the partner, at other settings
        using SessionFixture sameTemperature = new SessionFixture().WithDarkSeries(6, Dark, iso: null).WithLights(3, i => Light(i, 2.0), iso: 800);
        DarkSeriesResult rt = Run(sameTemperature, p400.Root);
        Assert.InRange(rt.Partner!.LightsOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.WithOpt, rt.Partner.LightsOverPartner.Verdict);
        Assert.InRange(rt.Partner.LightsOther!.RatioInFiles, 1.9, 2.1);
        Assert.Contains("    verdict partener   : ATENTIE: nu scadea partenerul asa cum e: e facut la alte setari decat lights-urile (120.0 s, ISO 400 fata de 120.0 s, ISO 800), asa ca in fisiere pixelii fierbinti din lights sunt de ", Report(rt), StringComparison.Ordinal);

        // the usual case of darks without an ISO: lights and a partner at one ISO. Nothing differs, by the same
        // reckoning: 1.00, as it is
        using SessionFixture sameIso = new SessionFixture().WithDarkSeries(6, Dark, iso: null).WithLights(3, i => Light(i));
        DarkSeriesResult rs = Run(sameIso, p400.Root);
        Assert.Equal(SilentSide.Own, rs.Partner!.Silent);
        Assert.Null(rs.Partner.LightsOther);
        Assert.InRange(rs.Partner.LightsOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.Direct, rs.Partner.LightsOverPartner.Verdict);
        Assert.Contains("    verdict partener   : OK: partenerul se poate scadea asa cum e", Report(rs), StringComparison.Ordinal);

        // mixed lights, each brought by its own settings: the first gives no ISO and reads 1.00 of the partner,
        // the other two are at ISO 800 and read 0.50; the night's figure is the median, and most lights are at
        // other settings than the partner
        using SessionFixture mixed = new SessionFixture().WithDarkSeries(6, Dark, iso: null).WithLights(3, i => Light(i), iso: 800);
        File.WriteAllBytes(Path.Combine(mixed.LightsDir, "light_0001.fit"), Light(0).Build(SessionFixture.FirstStart.AddSeconds(-4050), 120, null));
        DarkSeriesResult rm = Run(mixed, p400.Root);
        Assert.Equal(2, rm.Lights!.DarksSilent);
        Assert.InRange(rm.Partner!.LightsOverPartner.Ratio, 0.47, 0.53);
        Assert.NotNull(rm.Partner.LightsOther);
        Assert.Equal(800, rm.Partner.LightsOther!.Set.Iso);
        // and the other way: two lights without an ISO, one at ISO 800. The night's figure is the two's, 1.00
        using SessionFixture mostlySilent = new SessionFixture().WithDarkSeries(6, Dark, iso: null).WithLights(3, i => Light(i), iso: null);
        File.WriteAllBytes(Path.Combine(mostlySilent.LightsDir, "light_0003.fit"), Light(2).Build(SessionFixture.FirstStart.AddSeconds(-3750), 120, 800));
        DarkSeriesResult rq = Run(mostlySilent, p400.Root);
        Assert.Equal(1, rq.Lights!.DarksSilent);
        Assert.InRange(rq.Partner!.LightsOverPartner.Ratio, 0.95, 1.05);
        Assert.Null(rq.Partner.LightsOther);
        Assert.Equal(MatchVerdict.Direct, rq.Partner.LightsOverPartner.Verdict);

        // lights that give no ISO either: nothing to compare the partner's ISO with, the three as made at the same
        using SessionFixture noIso = new SessionFixture().WithDarkSeries(6, Dark, iso: null).WithLights(3, i => Light(i), iso: null);
        DarkSeriesResult rn = Run(noIso, p400.Root);
        Assert.Null(rn.Partner!.LightsOther);
        Assert.InRange(rn.Partner.LightsOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.Direct, rn.Partner.LightsOverPartner.Verdict);
    }

    /// <summary>
    /// The two ways the lights are read against a partner meet where nothing is missing in effect: the same
    /// pixels, one light with its ISO in the file (the product of the two ratios, as ever) and without it (each
    /// light's ratio in the files, brought by its factor over the partner's), give the same figure.
    /// </summary>
    [Fact]
    public void R064_TheChainedRatioIsTheSameWhetherALightGivesTheDarksIsoOrTakesIt()
    {
        // darks at ISO 400, a partner at ISO 1600 four times as high; one light at ISO 800 twice as high and two
        // at the darks' ISO, as high as the darks: one temperature. The two decide the median, so a light that
        // took the darks' ISO and was then read without it (a quarter of the figure) would show
        using SessionFixture p1600 = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 4 }, iso: 1600, inSubfolder: false);
        using SessionFixture gives = new SessionFixture().WithDarkSeries(6, Dark).WithLights(1, i => Light(i, 2.0), iso: 800);
        using SessionFixture takes = new SessionFixture().WithDarkSeries(6, Dark).WithLights(1, i => Light(i, 2.0), iso: 800);
        for (int i = 1; i < 3; i++)
        {
            DateTime start = SessionFixture.FirstStart.AddSeconds(-3000 + i * 150);
            File.WriteAllBytes(Path.Combine(gives.LightsDir, $"light_{i + 1:0000}.fit"), Light(i).Build(start, 120, 400));
            File.WriteAllBytes(Path.Combine(takes.LightsDir, $"light_{i + 1:0000}.fit"), Light(i).Build(start, 120, null));
        }

        DarkSeriesResult rg = Run(gives, p1600.Root), rt = Run(takes, p1600.Root);
        Assert.Equal(0, rg.Lights!.AssumedSettings);
        Assert.Equal(2, rt.Lights!.AssumedSettings);
        Assert.InRange(rg.Partner!.LightsOverPartner.Ratio, 0.9, 1.1);
        Assert.InRange(rt.Partner!.LightsOverPartner.Ratio, 0.9, 1.1);
        Assert.Equal(rg.Partner.LightsOverPartner.Ratio, rt.Partner.LightsOverPartner.Ratio, 5);
        Assert.Equal(rg.Partner.LightsOverPartner.Verdict, rt.Partner.LightsOverPartner.Verdict);
        // every light is at another ISO than the partner, given or taken
        Assert.NotNull(rg.Partner.LightsOther);
        Assert.NotNull(rt.Partner.LightsOther);
    }

    /// <summary>
    /// CR-076: a set whose frames give the exposure and no ISO had no factor, so it was compared with another
    /// as made at the same settings and its exposure was thrown away: darks of 300 s against lights of 120 s
    /// were judged on the files' ratio as it stood. A setting both sets give is used; one a set does not give
    /// is taken from the other; one neither gives is taken as the same, and a row says so (ADR 0060).
    /// </summary>
    [Fact]
    public void R052_ASettingBothSetsGiveIsUsedAndOneASetDoesNotGiveIsTakenFromTheOther()
    {
        // no ISO anywhere; darks of 300 s, lights of 120 s with hot pixels 0.4 times as high: the same temperature
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark, exposure: 300, iso: null).WithLights(3, i => Light(i, 0.4), exposure: 120, iso: null);
        DarkSeriesResult r = Run(s);
        Assert.Equal(Verdict.Warning, r.Inventory.Match.DarksVsLights);
        LightsAnalysis l = r.Lights!;
        Assert.All(l.Frames, f => Assert.Equal(2.5, f.ToDarks, 9));
        Assert.All(l.Frames, f => Assert.True(f.OtherSettings));
        Assert.InRange(l.LightsOverDark.Ratio, 0.93, 1.07);
        Assert.Equal(MatchVerdict.WithOpt, l.LightsOverDark.Verdict);
        Assert.InRange(l.Other!.RatioInFiles, 0.37, 0.43);
        Assert.Equal(UnsaidSettings.Iso, l.UnsaidByBoth);
        Assert.Equal(0, l.AssumedSettings);
        Assert.Equal(0, l.DarksSilent);
        string report = Report(r);
        Assert.Contains("    ATENTIE            : nici dark-urile, nici lights-urile nu spun ISO-ul; s-a luat ca fiind acelasi", report, StringComparison.Ordinal);
        Assert.DoesNotContain("s-au luat cele ale dark-urilor", report, StringComparison.Ordinal);
        Assert.Contains("    verdict            : ATENTIE: nu scadea dark-ul asa cum e: dark-urile sunt facute la alte setari decat lights-urile (300.0 s fata de 120.0 s), asa ca in fisiere pixelii fierbinti din lights sunt de 0.4", report, StringComparison.Ordinal);
        // the dark takes 120 ADU from a pixel that has 48 in the light
        Assert.Contains("    ce ramane          : scaderea ia prea mult: la un pixel fierbinte ramane in medie o gaura de 1", report, StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Contains("    WARNING            : neither the darks nor the lights give the ISO; it was taken as the same", Report(r), StringComparison.Ordinal);
        }

        // the same frames with hot pixels as high as the dark's: per second the lights' are 2.5 times the dark's
        using SessionFixture hot = new SessionFixture().WithDarkSeries(6, Dark, exposure: 300, iso: null).WithLights(3, i => Light(i), exposure: 120, iso: null);
        DarkSeriesResult rh = Run(hot);
        Assert.InRange(rh.Lights!.LightsOverDark.Ratio, 2.35, 2.65);
        Assert.InRange(rh.Lights.Other!.RatioInFiles, 0.95, 1.05);
        Assert.Equal(MatchVerdict.Unmatched, rh.Lights.LightsOverDark.Verdict);
        Assert.DoesNotContain("se poate scadea asa cum e", Report(rh), StringComparison.Ordinal);
        Assert.Contains("socotit pentru ISO-ul si expunerea dark-urilor, in fisiere de 1.0", Report(rh), StringComparison.Ordinal);

        // lights that give the exposure and no ISO against darks that give both: the ISO is the darks'
        using SessionFixture noIsoLights = new SessionFixture().WithDarkSeries(6, Dark, exposure: 300).WithLights(3, i => Light(i, 0.4), exposure: 120, iso: null);
        DarkSeriesResult rl = Run(noIsoLights);
        Assert.Equal(3, rl.Lights!.AssumedSettings);
        Assert.Equal(UnsaidSettings.None, rl.Lights.UnsaidByBoth);
        Assert.All(rl.Lights.Frames, f => Assert.Equal(2.5, f.ToDarks, 9));
        Assert.InRange(rl.Lights.LightsOverDark.Ratio, 0.93, 1.07);
        Assert.NotNull(rl.Lights.Other);
        // the row's figure is those lights' own: the darks' factor, 2.5, brought to the lights' exposure, 120 / 300
        Assert.Equal(2.5, rl.Darks.Norm, 9);
        Assert.Contains("    ATENTIE            : 3 lights nu spun ISO-ul sau expunerea; s-au luat cele ale dark-urilor (1 ADU-eq = 1.00 ADU)", Report(rl), StringComparison.Ordinal);
        Assert.Contains("(300.0 s, ISO 400 fata de 120.0 s)", Report(rl), StringComparison.Ordinal);
        // and where it is neither 1.00 nor the darks' own: darks at ISO 800 and 300 s have a factor of 5, the
        // lights of 120 s read at 5 x 120 / 300
        using SessionFixture iso800 = new SessionFixture().WithDarkSeries(6, Dark, exposure: 300, iso: 800).WithLights(3, i => Light(i, 0.4), exposure: 120, iso: null);
        DarkSeriesResult r8 = Run(iso800);
        Assert.Equal(5, r8.Darks.Norm, 9);
        Assert.Equal(2, r8.Lights!.AssumedNorm, 6);
        Assert.Contains("    ATENTIE            : 3 lights nu spun ISO-ul sau expunerea; s-au luat cele ale dark-urilor (1 ADU-eq = 2.00 ADU)", Report(r8), StringComparison.Ordinal);

        // darks that give the exposure and no ISO against lights that give both: the ISO is the lights'
        using SessionFixture noIsoDarks = new SessionFixture().WithDarkSeries(6, Dark, exposure: 300, iso: null).WithLights(3, i => Light(i, 0.4), iso: 800);
        DarkSeriesResult rd = Run(noIsoDarks);
        Assert.Equal(3, rd.Lights!.DarksSilent);
        Assert.Equal(0, rd.Lights.AssumedSettings);
        Assert.Equal(UnsaidSettings.None, rd.Lights.UnsaidByBoth);
        Assert.All(rd.Lights.Frames, f => Assert.Equal(2.5, f.ToDarks, 9));
        Assert.InRange(rd.Lights.LightsOverDark.Ratio, 0.93, 1.07);
        Assert.Contains("    ATENTIE            : dark-urile nu spun ISO-ul sau expunerea; ce nu spun s-a luat ca la lights-uri", Report(rd), StringComparison.Ordinal);
        Assert.Contains("(300.0 s fata de 120.0 s, ISO 800)", Report(rd), StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Contains("    WARNING            : the darks do not give their ISO or exposure; what they do not give was taken as the lights'", Report(rd), StringComparison.Ordinal);
        }

        // mixed lights: two give the ISO, one does not. The darks took the ISO from the two, so it is not a
        // setting neither set gives
        using SessionFixture mixed = new SessionFixture().WithDarkSeries(6, Dark, exposure: 300, iso: null).WithLights(2, i => Light(i, 0.4), iso: 800);
        File.WriteAllBytes(Path.Combine(mixed.LightsDir, "light_0003.fit"), Light(2, 0.4).Build(SessionFixture.FirstStart.AddSeconds(-3000), 120, null));
        LightsAnalysis lx = Run(mixed).Lights!;
        Assert.Equal(3, lx.Used);
        Assert.Equal(2, lx.DarksSilent);
        Assert.Equal(0, lx.AssumedSettings);
        Assert.Equal(UnsaidSettings.None, lx.UnsaidByBoth);

        // frames without an exposure (an EXPTIME of 0 is none): lights at ISO 800 twice as high against darks at
        // ISO 400 and 120 s take the darks' exposure and are brought by the ISO alone
        using SessionFixture isoOnly = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i, 2.0), exposure: 0, iso: 800);
        DarkSeriesResult ri = Run(isoOnly);
        Assert.All(ri.Lights!.Frames, f => Assert.Null(f.Info.ExposureSeconds));
        Assert.Equal(3, ri.Lights.AssumedSettings);
        Assert.All(ri.Lights.Frames, f => Assert.Equal(0.5, f.ToDarks, 9));
        Assert.InRange(ri.Lights.LightsOverDark.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.WithOpt, ri.Lights.LightsOverDark.Verdict);
        Assert.Contains("    ATENTIE            : 3 lights nu spun ISO-ul sau expunerea; s-au luat cele ale dark-urilor (1 ADU-eq = 2.00 ADU)", Report(ri), StringComparison.Ordinal);
        Assert.Contains("(120.0 s, ISO 400 fata de ISO 800)", Report(ri), StringComparison.Ordinal);

        // neither the darks nor the lights give the exposure: taken as the same, and the row names it
        using SessionFixture noExposure = new SessionFixture().WithDarkSeries(6, Dark, exposure: 0).WithLights(3, i => Light(i), exposure: 0);
        DarkSeriesResult re = Run(noExposure);
        Assert.Equal(UnsaidSettings.Exposure, re.Lights!.UnsaidByBoth);
        Assert.Equal(0, re.Lights.AssumedSettings);
        Assert.Null(re.Lights.Other);
        Assert.InRange(re.Lights.LightsOverDark.Ratio, 0.95, 1.05);
        Assert.Contains("    ATENTIE            : nici dark-urile, nici lights-urile nu spun expunerea; s-a luat ca fiind aceeasi", Report(re), StringComparison.Ordinal);

        // the three wordings of what neither set gives
        Assert.Equal("nici dark-urile, nici lights-urile nu spun expunerea; s-a luat ca fiind aceeasi", DarkReport.UnsaidText(UnsaidSettings.Exposure, partner: false));
        Assert.Equal("nici dark-urile, nici lights-urile nu spun nici ISO-ul, nici expunerea; s-au luat ca fiind aceleasi", DarkReport.UnsaidText(UnsaidSettings.Iso | UnsaidSettings.Exposure, partner: false));
        Assert.Equal("nici dark-urile sesiunii, nici ale partenerului nu spun ISO-ul; s-a luat ca fiind acelasi", DarkReport.UnsaidText(UnsaidSettings.Iso, partner: true));
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("neither the darks nor the lights give the exposure; it was taken as the same", DarkReport.UnsaidText(UnsaidSettings.Exposure, partner: false));
            Assert.Equal("neither the darks nor the lights give the ISO or the exposure; they were taken as the same", DarkReport.UnsaidText(UnsaidSettings.Iso | UnsaidSettings.Exposure, partner: false));
            Assert.Equal("neither the session's darks nor the partner's give the ISO; it was taken as the same", DarkReport.UnsaidText(UnsaidSettings.Iso, partner: true));
        }
    }

    /// <summary>
    /// CR-076 for a partner: the session's darks of 120 s against a partner of 300 s, no ISO on either side, read
    /// "the partner can be subtracted as it is" with no word of the exposures.
    /// </summary>
    [Fact]
    public void R063_APartnerIsComparedOnTheSettingBothGiveAndTheReportSaysWhatWasTaken()
    {
        // the partner's hot pixels are 2.5 times the session's, in 2.5 times the exposure: the same temperature
        static SyntheticDark Long(int i) => new() { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 2.5 };
        using SessionFixture own = new SessionFixture().WithDarkSeries(6, Dark, iso: null);
        using SessionFixture p = new SessionFixture().WithDarkSeries(5, Long, exposure: 300, iso: null, inSubfolder: false);
        DarkSeriesResult r = Run(own, p.Root);
        PartnerAnalysis partner = Assert.IsType<PartnerAnalysis>(r.Partner);
        Assert.Equal(SilentSide.None, partner.Silent);
        Assert.Equal(UnsaidSettings.Iso, partner.UnsaidByBoth);
        Assert.InRange(partner.OwnOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.WithOpt, partner.OwnOverPartner.Verdict);
        Assert.InRange(partner.Other!.RatioInFiles, 0.38, 0.42);
        string report = Report(r);
        Assert.Contains("; cadre citite 5 din 5, 300.0 s); eticheta ", report, StringComparison.Ordinal);
        Assert.Contains("    ATENTIE            : nici dark-urile sesiunii, nici ale partenerului nu spun ISO-ul; s-a luat ca fiind acelasi", report, StringComparison.Ordinal);
        Assert.Contains("    verdict partener   : ATENTIE: nu scadea partenerul asa cum e: e facut la alte setari decat dark-urile sesiunii (300.0 s fata de 120.0 s), asa ca in fisiere pixelii fierbinti din dark-urile sesiunii sunt de 0.4", report, StringComparison.Ordinal);
        Assert.DoesNotContain("partenerul se poate scadea asa cum e", report, StringComparison.Ordinal);

        // the session gives both settings, the partner the exposure alone: its ISO is the session's
        using SessionFixture says = new SessionFixture().WithDarkSeries(6, Dark);
        DarkSeriesResult rs = Run(says, p.Root);
        Assert.Equal(SilentSide.Partner, rs.Partner!.Silent);
        Assert.Equal(UnsaidSettings.None, rs.Partner.UnsaidByBoth);
        Assert.InRange(rs.Partner.OwnOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.WithOpt, rs.Partner.OwnOverPartner.Verdict);
        Assert.Contains("    ATENTIE            : dark-urile partenerului nu spun ISO-ul sau expunerea; ce nu spun s-a luat ca la dark-urile sesiunii", Report(rs), StringComparison.Ordinal);
        Assert.Contains("(300.0 s fata de 120.0 s, ISO 400)", Report(rs), StringComparison.Ordinal);

        // with lights of 120 s, no ISO, as hot as the session's darks: they keep their own exposure, which the
        // darks give too, and it is not the partner's
        using SessionFixture withLights = new SessionFixture().WithDarkSeries(6, Dark, iso: null).WithLights(3, i => Light(i), iso: null);
        DarkSeriesResult rl = Run(withLights, p.Root);
        Assert.Null(rl.Lights!.Other);
        Assert.Equal(MatchVerdict.WithOpt, rl.Partner!.LightsOverPartner.Verdict);
        Assert.InRange(rl.Partner.LightsOther!.RatioInFiles, 0.38, 0.42);
        Assert.Contains("    verdict partener   : ATENTIE: nu scadea partenerul asa cum e: e facut la alte setari decat lights-urile (300.0 s fata de 120.0 s), asa ca in fisiere pixelii fierbinti din lights sunt de 0.4", Report(rl), StringComparison.Ordinal);

        // each set lacks what the other gives: the session's darks give the ISO alone, the partner the exposure
        // alone. Nothing both give, so nothing differs, and both rows say what was taken
        using SessionFixture isoAlone = new SessionFixture().WithDarkSeries(6, Dark, exposure: 0);
        using SessionFixture exposureAlone = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }, iso: null, inSubfolder: false);
        DarkSeriesResult rb = Run(isoAlone, exposureAlone.Root);
        Assert.Equal(SilentSide.Both, rb.Partner!.Silent);
        Assert.Equal(UnsaidSettings.None, rb.Partner.UnsaidByBoth);
        Assert.Null(rb.Partner.Other);
        Assert.InRange(rb.Partner.OwnOverPartner.Ratio, 0.95, 1.05);
        Assert.Contains("    ATENTIE            : dark-urile partenerului nu spun ISO-ul sau expunerea; ce nu spun s-a luat ca la dark-urile sesiunii", Report(rb), StringComparison.Ordinal);
        Assert.Contains("    ATENTIE            : dark-urile sesiunii nu spun ISO-ul sau expunerea; ce nu spun s-a luat ca la partener", Report(rb), StringComparison.Ordinal);
        Assert.Contains("; cadre citite 5 din 5, 120.0 s); eticheta ", Report(rb), StringComparison.Ordinal);

        // CR-077 for the exposure: darks without one, between lights of 120 s and a partner of 300 s with hot
        // pixels 2.5 times as high, all at ISO 400 and one temperature. The darks' exposure drops out of the two
        // pairs; the lights and the partner are compared on theirs
        using SessionFixture noExposureDarks = new SessionFixture().WithDarkSeries(6, Dark, exposure: 0).WithLights(3, i => Light(i));
        using SessionFixture long400 = new SessionFixture().WithDarkSeries(5, Long, exposure: 300, inSubfolder: false);
        DarkSeriesResult rx = Run(noExposureDarks, long400.Root);
        Assert.Equal(3, rx.Lights!.DarksSilent);
        Assert.Equal(SilentSide.Own, rx.Partner!.Silent);
        Assert.InRange(rx.Partner.OwnOverPartner.Ratio, 0.38, 0.42);
        Assert.InRange(rx.Partner.LightsOverPartner.Ratio, 0.95, 1.05);
        Assert.Equal(MatchVerdict.WithOpt, rx.Partner.LightsOverPartner.Verdict);
        Assert.InRange(rx.Partner.LightsOther!.RatioInFiles, 0.38, 0.42);
        Assert.Contains("e facut la alte setari decat lights-urile (300.0 s, ISO 400 fata de 120.0 s, ISO 400), asa ca in fisiere pixelii fierbinti din lights sunt de 0.4", Report(rx), StringComparison.Ordinal);

        // setting by setting: darks at ISO 400 and 300 s, lights of 120 s without an ISO, a partner at ISO 400 and
        // 120 s, all at one temperature (the lights' and the partner's hot pixels 0.4 times the darks'). The lights
        // took their ISO from the darks and kept their exposure, so they stand at the partner's settings, though
        // they are at other settings than the darks, and the darks than the partner
        static SyntheticDark Short(int i) => new() { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 0.4 };
        using SessionFixture longDarks = new SessionFixture().WithDarkSeries(6, Dark, exposure: 300).WithLights(3, i => Light(i, 0.4), exposure: 120, iso: null);
        using SessionFixture shortPartner = new SessionFixture().WithDarkSeries(5, Short, inSubfolder: false);
        DarkSeriesResult rc = Run(longDarks, shortPartner.Root);
        Assert.NotNull(rc.Lights!.Other);
        Assert.NotNull(rc.Partner!.Other);
        Assert.Null(rc.Partner.LightsOther);
        Assert.InRange(rc.Partner.LightsOverPartner.Ratio, 0.9, 1.1);
        Assert.Equal(MatchVerdict.Direct, rc.Partner.LightsOverPartner.Verdict);
        Assert.Contains("    verdict partener   : OK: partenerul se poate scadea asa cum e", Report(rc), StringComparison.Ordinal);
    }

    /// <summary>
    /// The helpers of ADR 0060 on frames built by hand, every combination at once: the factor of one frame over
    /// another from the settings both give, what a frame lacks, what neither gives, and a frame's settings with
    /// the missing ones taken from another.
    /// </summary>
    [Fact]
    public void R052_TheFactorBetweenTwoFramesIsReadFromTheSettingsBothGive()
    {
        static FrameInfo F(double? exposure, double? iso) => new(1, "x.fit", null, null, null, exposure, iso, null, null, null, null, null, null, null, null, null);
        Normalisation linear = Normalisation.Default, slower = new(IsoFactorPerStop: 1.8);
        FrameInfo a = F(120, 800), b = F(300, 400);

        // both give both: the one's factor over the other's, whatever the gain per ISO stop
        Assert.Equal(0.8, linear.Relative(a, b), 12);
        Assert.Equal(1.25, linear.Relative(b, a), 12);
        Assert.Equal(linear.NormOf(a) / linear.NormOf(b), linear.Relative(a, b), 12);
        Assert.Equal(1.8 * 0.4, slower.Relative(a, b), 12);
        Assert.Equal(slower.NormOf(a) / slower.NormOf(b), slower.Relative(a, b), 12);
        // two stops between ISO 1600 and ISO 400
        Assert.Equal(1.8 * 1.8, slower.Relative(F(300, 1600), b), 12);
        // a setting either does not give counts as the same
        Assert.Equal(0.4, linear.Relative(F(120, null), b), 12);
        Assert.Equal(2.0, linear.Relative(F(null, 800), b), 12);
        Assert.Equal(0.4, linear.Relative(a, F(300, null)), 12);
        Assert.Equal(1.0, linear.Relative(F(null, null), b));
        Assert.Equal(1.0, linear.Relative(F(120, null), F(null, 400)));
        Assert.Equal(1.0, linear.Relative(a, a));
        Assert.Equal(1.0, linear.Relative(a, null));
        // a setting of 0 is not given, on either side
        Assert.Equal(0.4, linear.Relative(F(120, 0), b), 12);
        Assert.Equal(2.0, linear.Relative(F(0, 800), b), 12);
        Assert.Equal(0.4, linear.Relative(a, F(300, 0)), 12);
        Assert.Equal(2.0, linear.Relative(a, F(0, 400)), 12);
        Assert.False(Normalisation.GivesExposure(F(0, 400)));
        Assert.False(Normalisation.GivesIso(F(120, 0)));
        Assert.True(Normalisation.Lacks(F(0, 400), b));

        Assert.True(Normalisation.Lacks(F(120, null), b));
        Assert.False(Normalisation.Lacks(b, F(120, null)));
        Assert.True(Normalisation.Lacks(F(null, 800), b));
        Assert.False(Normalisation.Lacks(F(120, null), F(300, null)));
        // each lacks what the other gives
        Assert.True(Normalisation.Lacks(F(120, null), F(null, 800)));
        Assert.True(Normalisation.Lacks(F(null, 800), F(120, null)));

        Assert.Equal(UnsaidSettings.None, Normalisation.UnsaidBy(a, b));
        Assert.Equal(UnsaidSettings.None, Normalisation.UnsaidBy(F(120, null), F(null, 800)));
        Assert.Equal(UnsaidSettings.Iso, Normalisation.UnsaidBy(F(120, null), F(300, null)));
        Assert.Equal(UnsaidSettings.Exposure, Normalisation.UnsaidBy(F(null, 400), F(null, 800)));
        Assert.Equal(UnsaidSettings.Iso | UnsaidSettings.Exposure, Normalisation.UnsaidBy(F(null, null), F(null, null)));

        Assert.Equal((120.0, 400.0), (Normalisation.TakenFrom(F(120, null), b).ExposureSeconds, Normalisation.TakenFrom(F(120, null), b).Iso));
        Assert.Equal((300.0, 800.0), (Normalisation.TakenFrom(F(null, 800), b).ExposureSeconds, Normalisation.TakenFrom(F(null, 800), b).Iso));
        Assert.Equal((120.0, 800.0), (Normalisation.TakenFrom(a, b).ExposureSeconds, Normalisation.TakenFrom(a, b).Iso));
        Assert.Equal((120.0, (double?)null), (Normalisation.TakenFrom(F(null, null), F(120, null)).ExposureSeconds, Normalisation.TakenFrom(F(null, null), F(120, null)).Iso));
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
        ReportRow shared = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Index).Rows.Single(row => row.Label == "pixeli comuni");
        IReadOnlyList<string> lines = Assert.IsAssignableFrom<IReadOnlyList<string>>(shared.Lines);
        Assert.Equal(2, lines.Count);
        Assert.Equal(string.Join("; ", lines), shared.Value);
        Assert.Equal($"{partner.OwnHot50InPartner50} din {partner.OwnHot50} ai sesiunii peste 50 ADU-eq sunt peste 50 si la partener (100 %)", lines[0]);
        Assert.Equal($"{partner.PartnerHot50InOwn20} din {partner.PartnerHot50} ai partenerului peste 50 sunt peste 20 in sesiune (100 %)", lines[1]);
        Assert.Null(shared.Table);
    }

    /// <summary>
    /// CR-020: the writer refused the partner's folder only through the partner it had matched, so after a
    /// partner that failed (the window's export; the console checks before the run) the files could be
    /// written into the partner's folder.
    /// </summary>
    [Fact]
    public void R026_AfterAPartnerThatFailedItsFolderIsStillRefused()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, Dark);

        // a partner whose darks have another size: the run ends with the partner's error and no partner
        using SessionFixture small = new SessionFixture().WithDarks(3, inSubfolder: false);
        DarkSeriesResult r = Run(s, small.Root);
        Assert.Null(r.Partner);
        Assert.NotNull(r.PartnerError);
        Assert.Equal(small.Root, r.PartnerFolder);
        foreach (string dir in new[] { small.Root, Path.Combine(small.Root, "rapoarte") })
        {
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => DarkOutputs.WriteAll(r, dir));
            Assert.StartsWith("--out nu poate fi folderul partenerului sau unul din el (", e.Message, StringComparison.Ordinal);
        }

        Assert.False(Directory.Exists(Path.Combine(small.Root, "rapoarte")));
        Assert.DoesNotContain(Directory.GetFiles(small.Root), f => Path.GetFileName(f).StartsWith("darkscheck", StringComparison.Ordinal));

        // a library kept as name\darks that fails: the folder around it too
        using SessionFixture kept = new SessionFixture().WithDarks(3);
        DarkSeriesResult k = Run(s, kept.DarksDir);
        Assert.NotNull(k.PartnerError);
        Assert.Throws<InvalidOperationException>(() => DarkOutputs.WriteAll(k, Path.Combine(kept.Root, "rapoarte")));
        Assert.False(Directory.Exists(Path.Combine(kept.Root, "rapoarte")));

        // a folder outside is written, the partner's error in its report; a run without a partner names none
        using TempWorkspace outDir = new();
        Assert.Contains(Path.Combine(outDir.Dir, DarkOutputs.ReportName), DarkOutputs.WriteAll(r, outDir.Dir));
        Assert.Contains("    partener           : ATENTIE: ", File.ReadAllText(Path.Combine(outDir.Dir, DarkOutputs.ReportName)), StringComparison.Ordinal);
        Assert.Null(Run(s).PartnerFolder);
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

    /// <summary>
    /// CR-004: lights with in-camera noise reduction read a ratio around zero, and the darks' index
    /// times it was printed as `idx-0 (-0.00 times the dark)`.
    /// </summary>
    [Fact]
    public void R064_LightsThatDoNotShowTheDarksHotPixelsHaveNoIndex()
    {
        using SessionFixture none = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i, 0));
        DarkSeriesResult r = Run(none);
        MatchFigures m = r.Lights!.LightsOverDark;
        Assert.True(m.Absent);
        Assert.True(double.IsNaN(r.LightsIndex), $"lights' index {r.LightsIndex}");
        Assert.True(double.IsNaN(IndexAnalyzer.EquivalentIndex(r.Index, m)));
        IReadOnlyList<ReportSection> sections = DarkReport.Sections(r);
        IReadOnlyList<ReportRow> check2 = sections.Single(x => x.Check == ReportCheck.Lights).Rows, index = sections.Single(x => x.Check == ReportCheck.Index).Rows;
        Assert.DoesNotContain(check2, row => row.Label == "index lights");
        Assert.DoesNotContain(index, row => row.Label == "lights");
        // the darks keep theirs, and the verdict still says what was seen
        Assert.Contains(check2, row => row.Label == "index dark-uri" && row.Value == r.Index.Tag);
        Assert.Contains(check2, row => row.Label == "verdict" && row.Value.StartsWith("ATENTIE: nu se potrivesc: in lights nu se vad pixelii fierbinti ai dark-ului", StringComparison.Ordinal));
        Assert.DoesNotMatch(@"idx-?0(?![0-9])", Report(r));

        // the rule itself: the ratios sit around zero, by the median or by the first quartile, on an unmatched verdict only
        Assert.True(new MatchFigures(5, -0.004, -0.02, 0.01, double.NaN, MatchVerdict.Unmatched).Absent);
        Assert.True(new MatchFigures(5, 0.01, -0.01, 0.03, -39.9, MatchVerdict.Unmatched).Absent);
        Assert.False(new MatchFigures(5, 1.6, 1.5, 1.7, 4.1, MatchVerdict.Unmatched).Absent);
        Assert.False(new MatchFigures(5, 0.5, -0.1, 0.6, -6, MatchVerdict.CosmeticOnly).Absent);
        Assert.False(MatchFigures.None.Absent);
        Assert.True(double.IsNaN(IndexAnalyzer.EquivalentIndex(r.Index, new MatchFigures(5, 0.01, -0.01, 0.03, -39.9, MatchVerdict.Unmatched))));
        Assert.Equal(1.6 * r.Index.Index, IndexAnalyzer.EquivalentIndex(r.Index, new MatchFigures(5, 1.6, 1.5, 1.7, 4.1, MatchVerdict.Unmatched)), 9);

        // lights that show them keep both rows
        using SessionFixture same = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i));
        DarkSeriesResult rs = Run(same);
        Assert.False(rs.Lights!.LightsOverDark.Absent);
        Assert.Contains(DarkReport.Sections(rs).Single(x => x.Check == ReportCheck.Lights).Rows, row => row.Label == "index lights");
        Assert.Contains(DarkReport.Sections(rs).Single(x => x.Check == ReportCheck.Index).Rows, row => row.Label == "lights");
    }

    /// <summary>CR-004: a lights-only session was labelled `...-idx-0`, a name the next run does not strip.</summary>
    [Fact]
    public void R066_ALightsOnlySessionWhoseLightsShowNoHotPixelIsLabelledIdxDash()
    {
        using SessionFixture alone = new SessionFixture().WithLights(4, i => Light(i, 0));
        using SessionFixture library = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }, inSubfolder: false);
        DarkSeriesResult r = Run(alone, library.Root);
        Assert.Equal(library.Root, r.DarksFromPartner);
        Assert.True(r.Lights!.LightsOverDark.Absent);
        Assert.True(double.IsNaN(r.LightsIndex), $"lights' index {r.LightsIndex}");
        Assert.Equal("idx-", r.LabelTag);
        Assert.Equal(Path.GetFileName(alone.Root) + "-idx-", r.Label);
        string report = Report(r);
        Assert.Contains("    eticheta           : " + r.Label + Environment.NewLine, report, StringComparison.Ordinal);
        Assert.DoesNotContain("index lights", report, StringComparison.Ordinal);
        // the partner keeps its own index and label on its row
        Assert.Contains("    dark-uri (partener): " + r.Index.Tag + " (", report, StringComparison.Ordinal);
        Assert.Contains("; eticheta " + Path.GetFileName(library.Root) + "-" + r.Index.Tag, report, StringComparison.Ordinal);
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

    /// <summary>
    /// CR-017: a session given as the lights' folder fell back to its own FITS files when its lights\ was empty,
    /// so the stacked result.fit at its root was measured as the one light.
    /// </summary>
    [Fact]
    public void R055_ASessionWithoutLightsIsRefusedNotReadFromItsRoot()
    {
        using SessionFixture library = new SessionFixture().WithDarkSeries(3, Dark, inSubfolder: false);

        // a session whose lights\ is empty (the raws archived), with a stacked result at its root
        using SessionFixture emptied = new SessionFixture().WithDarkSeries(2, Dark);
        Directory.CreateDirectory(emptied.LightsDir);
        File.WriteAllBytes(Path.Combine(emptied.Root, "result.fit"), Light(0).Build(SessionFixture.FirstStart.AddSeconds(-3600)));
        InvalidOperationException none = Assert.Throws<InvalidOperationException>(() => Run(library, lightsFrom: emptied.Root));
        Assert.StartsWith($"lights-urile alese: niciun light in {emptied.LightsDir} (raw: ", none.Message, StringComparison.Ordinal);

        // a session with no lights\ at all: its darks\ says it is a session, so its root is not read either
        using SessionFixture darksOnly = new SessionFixture().WithDarkSeries(2, Dark);
        File.WriteAllBytes(Path.Combine(darksOnly.Root, "result.fit"), Light(0).Build(SessionFixture.FirstStart.AddSeconds(-3600)));
        InvalidOperationException noFolder = Assert.Throws<InvalidOperationException>(() => Run(library, lightsFrom: darksOnly.Root));
        Assert.StartsWith($"lights-urile alese: niciun light in {darksOnly.LightsDir} (raw: ", noFolder.Message, StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            InvalidOperationException en = Assert.Throws<InvalidOperationException>(() => Run(library, lightsFrom: darksOnly.Root));
            Assert.StartsWith($"the chosen lights: no light in {darksOnly.LightsDir} (raw: ", en.Message, StringComparison.Ordinal);
        }

        // the same for biases\ and flats\ alone
        foreach (string sub in new[] { "biases", "flats" })
        {
            using TempWorkspace ws = new();
            Directory.CreateDirectory(Path.Combine(ws.Dir, sub));
            File.WriteAllBytes(Path.Combine(ws.Dir, "result.fit"), Light(0).Build(SessionFixture.FirstStart.AddSeconds(-3600)));
            Assert.Throws<InvalidOperationException>(() => Run(library, lightsFrom: ws.Dir));
        }

        // through the console: refused before anything is decoded
        (int code, string stdout, string stderr) = Cli.Run(library.Root, "--threads", "2", "--lights", emptied.Root);
        Assert.Equal(1, code);
        Assert.Contains($"EROARE: lights-urile alese: niciun light in {emptied.LightsDir} (raw: ", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("Se citesc", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("=== DARK-URI", stdout, StringComparison.Ordinal);

        // a folder with no session subfolder still gives its own frames
        using TempWorkspace bare = new();
        for (int i = 0; i < 2; i++)
        {
            File.WriteAllBytes(Path.Combine(bare.Dir, $"light_{i + 1:0000}.fit"), Light(i).Build(SessionFixture.FirstStart.AddSeconds(-3600 + (i * 150))));
        }

        DarkSeriesResult r = Run(library, lightsFrom: bare.Dir);
        Assert.Equal(2, r.Inventory.Session.Lights.Count);
        Assert.Equal(bare.Dir, r.Inventory.Session.LightsDir);
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

    [Fact]
    public void R024_WithLightsTheFramesNeededKeepTheStackUnderTheChosenLimit()
    {
        // twenty darks of noise 4 and five lights of noise 6: about 16 darks keep the stack within 10 %, about 31 within 5 %
        using SessionFixture s = new SessionFixture().WithDarkSeries(20, i => new SyntheticDark { Seed = 100 + i, Noise = 4, Hot = SyntheticLight.Lattice }).WithLights(5, i => Light(i));
        DarkSeriesResult r = Run(s);
        EnoughVerdict e = r.Verdicts.Enough;
        StackLimit st = Assert.IsType<StackLimit>(e.Stack);
        Assert.Equal(Th.StackNoiseLimit, st.Limit);
        Assert.Equal(5, st.Lights);

        // the noise of a light: the median over the lights of the noise against the neighbours, on the reference plane
        List<double> local = [.. r.Lights!.Frames.Select(f => f.LocalSigma[r.ReferencePlane]).Order()];
        Assert.Equal(local[2], st.LightNoise, 4);
        double share = st.LightNoise / Math.Sqrt(5);
        double perFrame = e.MasterNoise * Math.Sqrt(20);
        Assert.Equal(Math.Sqrt(1 + Math.Pow(e.MasterNoise / share, 2)) - 1, st.Penalty, 9);
        int Needed(double limit) => Math.Max(Th.MinDarks, (int)Math.Ceiling(Math.Pow(perFrame / (share * Math.Sqrt(((1 + limit) * (1 + limit)) - 1)), 2)));
        Assert.Equal([0.05, 0.10], st.Needed.Select(n => n.Limit));
        Assert.Equal([Needed(0.05), Needed(0.10)], st.Needed.Select(n => n.Frames));
        Assert.InRange(Needed(0.10), 12, 20);
        Assert.InRange(Needed(0.05), 24, 40);
        Assert.Equal(Needed(0.10), e.FramesNeeded);
        Assert.Equal(Verdict.Ok, e.Verdict);

        Assert.Equal([5, 10, 15, 20, 25, 30], e.Points.Select(p => p.N));
        foreach (FramesPoint p in e.Points)
        {
            Assert.Equal(Math.Sqrt(1 + Math.Pow(perFrame / Math.Sqrt(p.N) / share, 2)) - 1, p.StackPenalty, 9);
            Assert.Equal(p.N < Th.MinDarks ? FramesRating.TooFew : p.N >= e.FramesNeeded ? FramesRating.Enough : FramesRating.Few, p.Rating);
        }

        // the stricter limit: the same figures, more frames needed, and the twenty are few
        DarkSeriesResult strict = DarkSeriesRun.WithStackLimit(r, 0.05);
        EnoughVerdict e5 = strict.Verdicts.Enough;
        Assert.Equal(0.05, e5.Stack!.Limit);
        Assert.Equal(st.Penalty, e5.Stack.Penalty, 12);
        Assert.Equal(Needed(0.05), e5.FramesNeeded);
        Assert.Equal(Verdict.Warning, e5.Verdict);
        Assert.False(e5.TooFew);
        Assert.Equal(FramesRating.Few, e5.Points.Single(p => p.Now).Rating);
        Assert.Equal(Math.Max(30, e5.FramesNeeded), e5.Points[^1].N);
        Assert.Equal(FramesRating.Enough, e5.Points[^1].Rating);
        Assert.Same(r.Darks, strict.Darks);
        Assert.Same(r.Lights, strict.Lights);
        Assert.Equal(r.Verdicts.Drift, strict.Verdicts.Drift);

        // and back to the default: the first verdict again
        Assert.Equal(Verdict.Ok, DarkSeriesRun.WithStackLimit(strict, Th.StackNoiseLimit).Verdicts.Enough.Verdict);
    }

    [Fact]
    public void R024_WithLightsTheRowsNameTheStackTheLimitAndTheFramesToLeaveOut()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(20, i => new SyntheticDark { Seed = 100 + i, Noise = 4, Hot = SyntheticLight.Lattice }).WithLights(5, i => Light(i));
        DarkSeriesResult r = Run(s);
        EnoughVerdict e = r.Verdicts.Enough;
        StackLimit st = e.Stack!;
        int at10 = st.Needed[1].Frames, at5 = st.Needed[0].Frames;
        string noisier = RunReport.F(st.Penalty * 100, 1) + " %";
        IReadOnlyList<ReportRow> rows = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Enough).Rows;

        // the table gains the stack's column, its note names the lights and their noise, and the row offers the limit
        ReportRow frames = rows.Single(row => row.Label == "cu n cadre");
        string against = $"fata de stack-ul facut cu un master dark ideal, adica din foarte multe cadre, fara zgomot propriu; e stack-ul celor 5 lights ({RunReport.F(st.LightNoise, 1)} ADU de zgomot fiecare), socotit fara dithering; cu dithering diferenta e mai mica";
        ReportTable t = frames.Table!;
        Assert.Equal(["cadre", "zgomotul masterului", "fata de masterul de acum", "stack mai zgomotos cu", "cum e"], t.Header);
        Assert.Equal(["20 (acum)", RunReport.F(e.MasterNoise, 2) + " ADU", "", noisier, "ajung"], t.Rows[3]);
        Assert.Equal(["5", RunReport.F(e.Points[0].MasterNoise, 2) + " ADU", "+100 %", RunReport.F(e.Points[0].StackPenalty * 100, 1) + " %", "prea putine"], t.Rows[0]);
        // under the table the note is led by the column's name; the console, which prints no column, has it without
        Assert.Equal("stack mai zgomotos cu: " + against, t.Note);
        Assert.StartsWith($"5 cadre: {RunReport.F(e.Points[0].MasterNoise, 2)} ADU (+100 % fata de masterul de acum; stack cu {RunReport.F(e.Points[0].StackPenalty * 100, 1)} % mai zgomotos; prea putine); 10 cadre: ", frames.Value, StringComparison.Ordinal);
        Assert.Contains($"; 20 de cadre (acum): {RunReport.F(e.MasterNoise, 2)} ADU (stack cu {noisier} mai zgomotos; ajung); 25 de cadre: ", frames.Value, StringComparison.Ordinal);
        Assert.EndsWith("; ajung)  [" + against + "]", frames.Value, StringComparison.Ordinal);
        ReportChoice choice = Assert.IsType<ReportChoice>(frames.Choice);
        Assert.Equal("fata de un master dark ideal, stack-ul poate fi mai zgomotos cu cel mult:", choice.Label);
        Assert.Equal(["5 %", "10 %"], choice.Options);
        Assert.Equal(1, choice.Selected);

        Assert.Equal($"{RunReport.Frames(at10)} pentru un stack cu cel mult 10 % mai zgomotos decat cu un master dark ideal; {at5} pentru cel mult 5 %", rows.Single(row => row.Label == "ajung de la").Value);
        Assert.True(at10 < 20, $"needed at 10 %: {at10}");
        Assert.Equal(
            $"OK: 20 de cadre ajung: stack-ul celor 5 lights iese cu {noisier} mai zgomotos decat cu un master dark ideal, sub limita de 10 %; ar ajunge {at10}, deci poti scoate pana la {20 - at10}",
            rows[^1].Value);

        // at 5 % the same twenty are few: the verdict, the limit first in its row, the ratings and the selected option follow
        IReadOnlyList<ReportRow> strict = DarkReport.Sections(DarkSeriesRun.WithStackLimit(r, 0.05)).Single(x => x.Check == ReportCheck.Enough).Rows;
        Assert.Equal($"ATENTIE: 20 de cadre sunt putine: stack-ul celor 5 lights iese cu {noisier} mai zgomotos decat cu un master dark ideal, peste limita de 5 %; trebuie {at5}", strict[^1].Value);
        Assert.Equal($"{RunReport.Frames(at5)} pentru un stack cu cel mult 5 % mai zgomotos decat cu un master dark ideal; {at10} pentru cel mult 10 %", strict.Single(row => row.Label == "ajung de la").Value);
        ReportRow strictFrames = strict.Single(row => row.Label == "cu n cadre");
        Assert.Equal("putine", strictFrames.Table!.Rows[3][4]);
        Assert.Equal(0, strictFrames.Choice!.Selected);

        // a figure that one decimal would print as the limit keeps its decimals, in the verdict and in the table
        DarkSeriesResult near = r with
        {
            Verdicts = r.Verdicts with
            {
                Enough = e with { Stack = st with { Penalty = 0.0996 }, Points = [.. e.Points.Select(p => p.Now ? p with { StackPenalty = 0.0996 } : p)] },
            },
        };
        IReadOnlyList<ReportRow> nearRows = DarkReport.Sections(near).Single(x => x.Check == ReportCheck.Enough).Rows;
        Assert.Contains(" iese cu 9.96 % mai zgomotos decat cu un master dark ideal, sub limita de 10 %", nearRows[^1].Value, StringComparison.Ordinal);
        Assert.Equal("9.96 %", nearRows.Single(row => row.Label == "cu n cadre").Table!.Rows[3][3]);

        // the same rows in English
        using (Ui.Use(UiLanguage.English))
        {
            IReadOnlyList<ReportRow> en = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Enough).Rows;
            Assert.Equal($"OK: 20 frames are enough: the stack of the 5 lights comes out {noisier} noisier than with an ideal dark master, under the limit of 10 %; {at10} would do, so up to {20 - at10} can be left out", en[^1].Value);
            Assert.Equal($"{at10} frames for a stack at most 10 % noisier than with an ideal dark master; {at5} for at most 5 %", en.Single(row => row.Label == "enough from").Value);
            ReportRow enFrames = en.Single(row => row.Label == "with n frames");
            Assert.Equal(["frames", "master's noise", "against the master of now", "the stack is noisier by", "how it is"], enFrames.Table!.Header);
            Assert.Equal("against an ideal dark master, the stack may be noisier by at most:", enFrames.Choice!.Label);
            Assert.StartsWith("the stack is noisier by: against the stack made with an ideal dark master, that is, one made from very many frames, with no noise of its own; it is the stack of the 5 lights (", enFrames.Table.Note, StringComparison.Ordinal);
            Assert.EndsWith($"; enough)  [against the stack made with an ideal dark master, that is, one made from very many frames, with no noise of its own; it is the stack of the 5 lights ({RunReport.F(st.LightNoise, 1)} ADU of noise each), counted without dithering; with dithering the difference is smaller]", enFrames.Value, StringComparison.Ordinal);
            Assert.Equal(
                $"WARNING: 20 frames are few: the stack of the 5 lights comes out {noisier} noisier than with an ideal dark master, over the limit of 5 %; {at5} are needed",
                DarkReport.Sections(DarkSeriesRun.WithStackLimit(r, 0.05)).Single(x => x.Check == ReportCheck.Enough).Rows[^1].Value);
        }
    }

    [Fact]
    public void R024_TheNoiseOfALightIsBroughtToTheDarksSettings()
    {
        // the same lights declared at 60 s: against darks of 120 s a light's ADU count double, and so does its noise
        static SyntheticDark D(int i) => new() { Seed = 100 + i, Noise = 4, Hot = SyntheticLight.Lattice };
        using SessionFixture same = new SessionFixture().WithDarkSeries(20, D).WithLights(5, i => Light(i));
        using SessionFixture half = new SessionFixture().WithDarkSeries(20, D).WithLights(5, i => Light(i), exposure: 60);
        StackLimit atSame = Run(same).Verdicts.Enough.Stack!;
        DarkSeriesResult r = Run(half);
        StackLimit atHalf = r.Verdicts.Enough.Stack!;

        double normDarks = r.Darks.Norm;
        List<double> eq = [.. r.Lights!.Frames.Select(f => f.LocalSigma[r.ReferencePlane] / r.Darks.Normalisation.NormOf(f.Info)).Order()];
        Assert.NotEqual(normDarks, r.Darks.Normalisation.NormOf(r.Lights.Frames[0].Info));
        Assert.Equal(eq[2] * normDarks, atHalf.LightNoise, 4);
        Assert.Equal(2 * atSame.LightNoise, atHalf.LightNoise, 3);
        // a noisier light hides the master better: fewer darks are needed
        Assert.True(atHalf.Penalty < atSame.Penalty, $"{atHalf.Penalty} against {atSame.Penalty}");
    }

    [Fact]
    public void R024_WhenTheFrameCapLeavesDarksOutTheVerdictDoesNotSayHowManyToLeaveOut()
    {
        // forty darks under a cap of twenty: twenty are read, spread over the forty, so they are not the folder's frames
        using SessionFixture s = new SessionFixture().WithDarkSeries(40, i => new SyntheticDark { Seed = 100 + i, Noise = 4, Hot = SyntheticLight.Lattice }).WithLights(5, i => Light(i));
        DarkSeriesResult r = Run(s, maxFrames: 20);
        EnoughVerdict e = r.Verdicts.Enough;
        Assert.Equal(20, e.Frames);
        Assert.Equal(Verdict.Ok, e.Verdict);
        Assert.True(e.FramesNeeded < 20, $"needed {e.FramesNeeded}");
        string verdict = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Enough).Rows[^1].Value;
        Assert.EndsWith($", sub limita de 10 %; ar ajunge {e.FramesNeeded}", verdict, StringComparison.Ordinal);
        Assert.DoesNotContain("poti scoate", verdict, StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.EndsWith($", under the limit of 10 %; {e.FramesNeeded} would do", DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Enough).Rows[^1].Value, StringComparison.Ordinal);
        }
    }
}
