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
    private static DarkSeriesResult Run(SessionFixture s, string? against = null, int maxFrames = Th.MaxFramesInMemory) => DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2, MaxFrames: maxFrames, Against: against));

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
        Assert.Contains($"pixeli fierbinti   : {Ui.RoCount(l.Persistent)}pixeli persistenti in lights (izolati, peste 6 sigma locale, in cel putin 4 din 5 cadre)", report, StringComparison.Ordinal);
        Assert.Contains($"in masterul dark   : peste 10: {l.Persistent - 1} din {l.Persistent} (95 %); peste 20: {l.Persistent - 1} din {l.Persistent} (95 %); peste 50: ", report, StringComparison.Ordinal);
        Assert.Contains($"master in lights   : peste 10: {l.Persistent - 1} din 20 (", report, StringComparison.Ordinal);
        Assert.Contains("nepotriviti        : 1 pixel persistent in lights sub 20 ADU in master", report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1.0, "Direct", "OK: scadere directa (raport 1.0")]
    [InlineData(1.3, "WithOpt", "ATENTIE: scade cu -opt (raport 1.3")]
    [InlineData(0.5, "CosmeticOnly", "ATENTIE: doar corectie cosmetica (-cc=dark): raport 0.5")]
    [InlineData(1.6, "Unmatched", "ATENTIE: nepotrivit (raport 1.6")]
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

        Assert.Contains($"amplitudine        : lights / dark {l.LightsOverDark.Ratio.ToString("0.00", CultureInfo.InvariantCulture)} pe noapte (mediana pe 5 cadre; primul {l.RatioFirst.ToString("0.00", CultureInfo.InvariantCulture)}, ultimul {l.RatioLast.ToString("0.00", CultureInfo.InvariantCulture)}; 20 de pixeli)", Report(Run(s)), StringComparison.Ordinal);

        // lights at ISO 800 with twice the amplitude are at the same temperature as darks at ISO 400
        using SessionFixture iso = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i, 2.0), iso: 800);
        DarkSeriesResult ri = Run(iso);
        Assert.InRange(ri.Lights!.LightsOverDark.Ratio, 0.95, 1.05);
        Assert.Equal(0, ri.Lights.AssumedSettings);
        Assert.DoesNotContain("presupuse ale dark-urilor", Report(ri), StringComparison.Ordinal);

        // lights without an ISO card are taken at the darks' settings, and the report says so
        using SessionFixture noIso = new SessionFixture().WithDarkSeries(6, Dark).WithLights(3, i => Light(i), iso: null);
        DarkSeriesResult rn = Run(noIso);
        Assert.Equal(3, rn.Lights!.AssumedSettings);
        Assert.All(rn.Lights.Frames, f => Assert.True(f.AssumedSettings));
        Assert.InRange(rn.Lights.LightsOverDark.Ratio, 0.95, 1.05);
        Assert.Contains("ATENTIE            : 3 lights nu spun ISO-ul sau expunerea; presupuse ale dark-urilor (normalizare x1.00)", Report(rn), StringComparison.Ordinal);
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
        Assert.Contains("verdict            : ATENTIE: nepotrivit: lights-urile nu arata pixelii fierbinti ai dark-ului (raport ", report, StringComparison.Ordinal);
        Assert.Contains("; reducere de zgomot in aparat, alt aparat sau alta orientare?)", report, StringComparison.Ordinal);
        Assert.DoesNotContain("NEMASURAT: niciun pixel fierbinte", report, StringComparison.Ordinal);
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
        Assert.Contains("  9. LIGHTS FATA DE DARK\n    verdict            : NEMASURAT: sesiunea nu are lights", report.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("echivalent idx", report, StringComparison.Ordinal);
        Assert.DoesNotContain("TABEL PE LIGHTS", report, StringComparison.Ordinal);
    }

    [Fact]
    public void R053_ALightThatCannotBeDecodedIsAnErrorASkippedOneIsNot()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i));
        File.WriteAllBytes(Path.Combine(s.LightsDir, "light_0003.fit"), new byte[100]);
        File.WriteAllBytes(Path.Combine(s.LightsDir, "light_0004.fit"), new SyntheticLight { Width = 128, Height = 96, Hot = [], Stars = [] }.Build(SessionFixture.FirstStart));
        (int code, string stdout, _) = Cli.Run(s.Root, "--threads", "2");
        Assert.Equal(1, code);
        Assert.Contains("ATENTIE            : 2 lights nu s-au putut decoda (primul: light_0003.fit:", stdout, StringComparison.Ordinal);
        Assert.Contains("(mediana pe 3 cadre; ", stdout, StringComparison.Ordinal);
        Assert.Contains("light_0004.fit        eroare: dimensiune diferita: 128 x 96 fata de 256 x 192", stdout, StringComparison.Ordinal);
        Assert.Matches(@"pixeli fierbinti   : (18 |19 |20 de )pixeli persistenti in lights \(izolati, peste 6 sigma locale, in cel putin 3 din 3 cadre\)", stdout);

        // the cap: one light in two is decoded, the others are skipped, no error
        using SessionFixture capped = new SessionFixture().WithDarkSeries(6, Dark).WithLights(5, i => Light(i));
        DarkSeriesResult r = Run(capped, maxFrames: 3);
        Assert.False(r.HasErrors);
        Assert.Equal(3, r.Lights!.Used);
        Assert.Equal(2, r.Lights.Frames.Count(f => f.Skipped));
        Assert.Contains("lights sarite      : 2 din 5 (plafonul de cadre; folosite 3)", Report(r), StringComparison.Ordinal);

        // no light decodes: check 9 says why, the run exits 1
        using SessionFixture none = new SessionFixture().WithDarkSeries(6, Dark).WithLights(2, i => Light(i));
        foreach (string f in Directory.GetFiles(none.LightsDir))
        {
            File.WriteAllBytes(f, new byte[100]);
        }

        (int code2, string stdout2, _) = Cli.Run(none.Root, "--threads", "2");
        Assert.Equal(1, code2);
        Assert.Contains("verdict            : NEMASURAT: lights-urile nu s-au putut folosi: niciun light nu s-a putut decoda: light_0001.fit:", stdout2, StringComparison.Ordinal);
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
        Assert.StartsWith("index,file,time,exposure_s,iso,error,skipped,R_sky_median,R_sky_sigma,R_local_sigma,", csv[0], StringComparison.Ordinal);
        Assert.EndsWith(",candidates,isolated_hot,amp_ratio_median,amp_ratio_q25,amp_ratio_q75", csv[0], StringComparison.Ordinal);
        int columns = csv[0].Split(',').Length;
        Assert.All(csv.Skip(1), row => Assert.Equal(columns, row.Split(',').Length));
        Assert.StartsWith("1,\"light_0001.fit\",2026-08-12 23:38:53,120,400,\"\",\"\",500,", csv[1], StringComparison.Ordinal);
        Assert.Matches(",2[0-9],(0\\.9|1\\.0)[0-9]*,0\\.[0-9]+,1\\.[0-9]+$", csv[1]);
        Assert.StartsWith("2,\"light_0002.fit\",,,,\"", csv[2], StringComparison.Ordinal);
        Assert.EndsWith("\",\"\"" + new string(',', columns - 7), csv[2], StringComparison.Ordinal);
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
        Assert.Contains(" ADU-eq: mediana pixelilor de pe rangurile 5-20 pe Mpx, al 2-lea .. al 2-lea din 0.05 Mpx, normalizat la ISO 400 si 120 s)", Report(r), StringComparison.Ordinal);
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
        Assert.Equal("OK: scadere directa (raport 1.00, in 0.9 .. 1.1; dark-ul e la aceeasi temperatura)", DarkReport.MatchText(m, false, "lights-urile"));
        Assert.Equal("ATENTIE: nepotrivit (raport 2.00: partenerul e mai rece cu 6.0 grade; prea rece, pierde pixelii fierbinti slabi)", DarkReport.MatchText(twice, true, "dark-urile sesiunii"));
        MatchFigures none = IndexAnalyzer.Match([-0.01f, 0.02f, 0f]);
        Assert.Equal(MatchVerdict.Unmatched, none.Verdict);
        Assert.True(double.IsNaN(none.DeltaCelsius));
        Assert.Equal("ATENTIE: nepotrivit: dark-urile sesiunii nu arata pixelii fierbinti ai partenerului (raport 0.00; reducere de zgomot in aparat, alt aparat sau alta orientare?)", DarkReport.MatchText(none, true, "dark-urile sesiunii"));
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
        Assert.Contains("; 5 cadre din 5, ISO 400, 120.0 s); eticheta " + partner.Label, report, StringComparison.Ordinal);
        Assert.Equal(0, partner.Failed);
        Assert.Null(partner.FirstError);
        Assert.Contains("    dark-uri / partener: 0.5", report, StringComparison.Ordinal);
        Assert.Contains("20 de pixeli comuni) dupa normalizare, adica dark-urile sesiunii sunt cu -6.", report, StringComparison.Ordinal);
        Assert.Contains("    pixeli comuni      : 20 din 20 ai sesiunii peste 50 ADU-eq sunt peste 50 si la partener (100 %); 20 din 20 ai partenerului peste 50 sunt peste 20 in sesiune (100 %)", report, StringComparison.Ordinal);
        // the lights at the darks' temperature: the chained ratio is the darks' and so is the verdict
        Assert.Contains("    lights / partener  : 0.5", report, StringComparison.Ordinal);
        Assert.Contains("    verdict partener   : ATENTIE: doar corectie cosmetica (-cc=dark): raport 0.5", report, StringComparison.Ordinal);
        Assert.Contains("partenerul e mai cald cu 6.", report, StringComparison.Ordinal);
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
        Assert.Contains("; 3 cadre din 4, ISO 400, 120.0 s); eticheta ", stdout1, StringComparison.Ordinal);
        Assert.Contains("    ATENTIE            : 1 dark al partenerului nu s-a putut folosi (primul: DSC_0002.fit: ", stdout1, StringComparison.Ordinal);
        Assert.Contains("    dark-uri / partener: 1.0", stdout1, StringComparison.Ordinal);

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

        Assert.StartsWith("niciun dark al partenerului nu s-a putut decoda: DSC_0001.fit:", Run(s, broken.Root).PartnerError, StringComparison.Ordinal);

        // frames of another size
        using SessionFixture small = new SessionFixture().WithDarks(3, inSubfolder: false);
        DarkSeriesResult r = Run(s, small.Root);
        Assert.Null(r.Partner);
        Assert.Equal("partenerul are 32 x 24 px, dark-urile sesiunii 256 x 192", r.PartnerError);
        Assert.True(r.HasErrors);

        // without lights the verdict is the darks' against the partner
        using SessionFixture warm = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.2 }, inSubfolder: false);
        string report = Report(Run(s, warm.Root));
        Assert.Contains("    verdict partener   : ATENTIE: scade cu -opt (raport 0.8", report, StringComparison.Ordinal);
        Assert.Contains(" (dark-urile sesiunii fata de partener)", report, StringComparison.Ordinal);
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
        Assert.Contains($"    lights             : echivalent idx{expected} (dark x 1.3", report, StringComparison.Ordinal);
        Assert.Contains("adica +2.", report, StringComparison.Ordinal);
        // the lights are at the partner's temperature: chained ratio 1.3 x (1 / 1.3) = 1, direct subtraction
        Assert.Contains("    lights / partener  : 1.0", report, StringComparison.Ordinal);
        Assert.Contains("    verdict partener   : OK: scadere directa (raport 1.0", report, StringComparison.Ordinal);
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
        Assert.Equal(Path.GetFileName(library.Root) + "-" + b.Index.Tag, b.Label);
        Assert.Equal(a.Lights!.Persistent, b.Lights!.Persistent);
        Assert.Equal(a.Lights.LightsOverDark.Ratio, b.Lights.LightsOverDark.Ratio, 1e-9);
        Assert.Equal(a.Lights.LightsOverDark.Verdict, b.Lights.LightsOverDark.Verdict);
        Assert.Equal(a.LightsIndex, b.LightsIndex, 1e-9);
        Assert.NotNull(b.Biases);
        Assert.Equal(Verdict.Ok, b.Inventory.Match.DarksVsLights);

        string report = Report(b);
        Assert.Contains($"=== DARK-URI (ale partenerului {library.Root}): 5 cadre decodate din 5", report, StringComparison.Ordinal);
        Assert.Contains($"    darks              : (lipsa; se folosesc dark-urile partenerului {library.Root})", report, StringComparison.Ordinal);
        Assert.Contains("    expunere partener  : 120.0 s la ISO 400 (toate cadrele)", report, StringComparison.Ordinal);
        Assert.Contains("    interval darks     : (lipsa)", report, StringComparison.Ordinal);
        Assert.Contains("    potrivire          : OK: dark-urile partenerului au expunerea (120.0 s) si ISO-ul (400) lights-urilor", report, StringComparison.Ordinal);
        Assert.Contains("    dark-uri (partener): idx", report, StringComparison.Ordinal);
        Assert.Contains("    eticheta           : " + b.Label, report, StringComparison.Ordinal);
        Assert.Contains("    lights             : echivalent idx", report, StringComparison.Ordinal);
        Assert.Contains("    verdict            : ATENTIE: scade cu -opt (raport 1.3", report, StringComparison.Ordinal);
        Assert.DoesNotContain("    partener           :", report, StringComparison.Ordinal);
        Assert.DoesNotContain("dark-uri / partener", report, StringComparison.Ordinal);
        Assert.DoesNotContain("lights / partener", report, StringComparison.Ordinal);

        // a partner at another ISO is flagged by check 1, as a session's own darks would be
        using SessionFixture iso800 = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 700 + i, Hot = SyntheticLight.Lattice }, iso: 800, inSubfolder: false);
        DarkSeriesResult c = Run(alone, iso800.Root);
        Assert.Equal(Verdict.Warning, c.Inventory.Match.DarksVsLights);
        Assert.Contains("    potrivire          : ATENTIE: dark-uri ale partenerului de 120.0 s la ISO 800, lights de 120.0 s la ISO 400", Report(c), StringComparison.Ordinal);

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
}
