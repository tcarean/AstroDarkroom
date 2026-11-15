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
/// R-046 (ADR 0100): the two ranges of the dust class and of the patches that give the sky's
/// figure, on every row of the simulation, and the table's text with a range in brackets.
/// </summary>
public class DustRangeTests
{
    static SimulationResult Simulate(Raster r)
    {
        Parameters p = Scenes.OnBuiltForPaper();
        return PrintStretchSimulator.Simulate(r, TonalAnalyzer.Analyze(r, p), p);
    }

    /// <summary>A 10 x 10 map with the given class per patch and no cloud.</summary>
    static DustMap Map(Func<int, PatchClass> cls, DustSource source)
    {
        var map = new DustMap { Cols = 10, Rows = 10, Side = 8, Stride = 4, Classes = new PatchClass[100], Clouds = Array.Empty<DustCloud>(), NoiseThreshold = 1, DustSource = source };
        for (int i = 0; i < 100; i++) map.Classes[i] = cls(i);
        return map;
    }

    /// <summary>
    /// A sky at 0.25 with the ripple and the noise of a real one, no WCS, and a dark blob of the
    /// given radius at the centre: a dust class of a few patches.
    /// </summary>
    static Raster SkyWithASmallDarkBlob(int radius)
        => SyntheticRasters.AddNoise(SyntheticRasters.From(512, 512, 3, (x, y, p) =>
            (x - 255) * (x - 255) + (y - 255) * (y - 255) <= radius * radius
                ? 0.05f
                : 0.25f + (float)(0.01 * Math.Sin(x / 37.0) * Math.Cos(y / 53.0))), 0.3 / 255, 11);

    [Fact]
    public void R046_TheRangesAreThePercentilesOfTheTwoPatchSets()
    {
        // 41 dust patches at L* 0..40, 41 sky patches at 50..90, 18 band patches at 95: with 41
        // values a step apart, the 10, 25, 75 and 90 % percentiles fall on whole values.
        DustMap map = Map(i => i < 41 ? PatchClass.Dust : i < 82 ? PatchClass.Sky : PatchClass.Band, DustSource.DustClass);
        var L = new float[100];
        for (int i = 0; i < 100; i++) L[i] = i < 41 ? i : i < 82 ? 50 + (i - 41) : 95;
        DustStats d = DustAnalyzer.Measure(L, map, 0);

        Assert.Equal(new LstarRange(10, 30), d.DustHalf);
        Assert.Equal(new LstarRange(4, 36), d.DustWide);
        Assert.Equal(new LstarRange(60, 80), d.SkyHalf);
        Assert.Equal(new LstarRange(54, 86), d.SkyWide);
        // The dust's figure is the low end of its middle half when the class decides, and the
        // sky's figure the middle of both of its ranges.
        Assert.Equal(10, d.LstarDust, 9);
        Assert.Equal(70, d.LstarSky, 9);

        // The cuts are the thresholds': a range from p to 100 - p.
        Assert.Equal(0.25, Thresholds.RangeHalfCut);
        Assert.Equal(0.10, Thresholds.RangeWideCut);

        // The dust's ranges are the class's whatever gave the dust's figure: with the darkest
        // sky as the source the figure, 50.4, lies outside them (ADR 0100).
        DustStats fromSky = DustAnalyzer.Measure(L, Map(i => map.Classes[i], DustSource.DarkestSky), 0);
        Assert.Equal(new LstarRange(10, 30), fromSky.DustHalf);
        Assert.Equal(50.4, fromSky.LstarDust, 5);

        // With no patch classed sky, the sky's figure and its ranges are taken on all the patches.
        var all = new float[100];
        for (int i = 0; i < 100; i++) all[i] = i;
        DustStats noSky = DustAnalyzer.Measure(all, Map(i => i < 41 ? PatchClass.Dust : PatchClass.Band, DustSource.DustClass), 0);
        Assert.Equal(24.75, noSky.SkyHalf.Low, 5);
        Assert.Equal(74.25, noSky.SkyHalf.High, 5);
        Assert.Equal(9.9, noSky.SkyWide.Low, 5);
        Assert.Equal(89.1, noSky.SkyWide.High, 5);
    }

    [Fact]
    public void R046_ADustClassWithoutPatchesHasNoRangeAndLeavesTheBracketBlank()
    {
        var L = new float[100];
        for (int i = 0; i < 100; i++) L[i] = 20 + i * 0.1f;
        DustStats d = DustAnalyzer.Measure(L, Map(_ => PatchClass.Sky, DustSource.DarkestSky), 0);
        Assert.Null(d.DustHalf);
        Assert.Null(d.DustWide);

        var row = new SimulationRow { Target = 0.1, Gamma = 1, LstarDust = 20.1, LstarSky = 25, SkyHalf = new LstarRange(22.5, 27.4), SkyWide = new LstarRange(21, 28.9), PaperBlackLstar = 3.6 };
        Assert.Null(row.DustRange(RangeKind.MiddleHalf));
        Assert.Contains("   " + "   20.1 " + new string(' ', 13) + "   " + "  25.0 (22.5 - 27.4)" + "   ", ReportWriter.SimulationLine(row, RangeKind.MiddleHalf));
    }

    [Fact]
    public void R046_EveryRowCarriesItsRangesAtItsOwnLift()
    {
        SimulationResult s = Simulate(WcsFixtures.BandScene());
        Assert.Equal(DustSource.DustClass, s.Map.DustSource);
        Assert.True(s.Rows.Count > 3);
        foreach (SimulationRow row in s.Rows)
        {
            Assert.NotNull(row.DustHalf);
            Assert.NotNull(row.DustWide);
            LstarRange dustHalf = row.DustHalf.Value, dustWide = row.DustWide.Value;
            // The class decides, so the figure is the class's 25 % percentile: the low end.
            Assert.Equal(row.LstarDust, dustHalf.Low, 9);
            Assert.InRange(row.LstarSky, row.SkyHalf.Low, row.SkyHalf.High);
            Assert.True(dustWide.Low <= dustHalf.Low && dustHalf.Low <= dustHalf.High && dustHalf.High <= dustWide.High);
            Assert.True(row.SkyWide.Low <= row.SkyHalf.Low && row.SkyHalf.Low < row.SkyHalf.High && row.SkyHalf.High <= row.SkyWide.High);
            Assert.Equal(row.DustHalf, row.DustRange(RangeKind.MiddleHalf));
            Assert.Equal(row.DustWide, row.DustRange(RangeKind.Wide));
            Assert.Equal(row.SkyHalf, row.SkyRange(RangeKind.MiddleHalf));
            Assert.Equal(row.SkyWide, row.SkyRange(RangeKind.Wide));
            Assert.Null(row.DustRange(RangeKind.None));
            Assert.Null(row.SkyRange(RangeKind.None));
            // The two ranges are two measurements, not one carried twice.
            Assert.NotEqual(row.DustHalf, row.DustWide);
            Assert.NotEqual(row.SkyHalf, row.SkyWide);
        }
        // Each row is measured at its own lift: a higher target lifts both ends of the sky's range.
        for (int i = 1; i < s.Rows.Count; i++)
        {
            Assert.True(s.Rows[i].SkyHalf.Low > s.Rows[i - 1].SkyHalf.Low);
            Assert.True(s.Rows[i].SkyWide.High > s.Rows[i - 1].SkyWide.High);
        }
        // The as-is row is the measurement of the image as it is.
        Assert.Equal(s.AsIsDust.DustHalf, s.AsIs.DustHalf);
        Assert.Equal(s.AsIsDust.DustWide, s.AsIs.DustWide);
        Assert.Equal(s.AsIsDust.SkyHalf, s.AsIs.SkyHalf);
        Assert.Equal(s.AsIsDust.SkyWide, s.AsIs.SkyWide);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R046_WithoutARangeTheTableIsTodaysTextByteForByte(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        SimulationResult s = Simulate(WcsFixtures.BandScene());
        Assert.Equal(
            Tr.Pick("target background    gamma   L* dust   L* sky  pure black    L* < 15      L* > 50",
                    "fundal-tinta         gamma   L* praf   L* cer   negru pur   sub L*15   peste L*50"),
            ReportWriter.SimulationHeaderFor(RangeKind.None));
        Assert.Equal(ReportWriter.SimulationHeader, ReportWriter.SimulationHeaderFor(RangeKind.None));
        Assert.All(s.Rows, row => Assert.Equal(ReportWriter.SimulationLine(row), ReportWriter.SimulationLine(row, RangeKind.None)));
        Assert.Null(ReportWriter.RangeNote(s, RangeKind.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R046_ARowWritesEachRangeInBracketsAfterItsFigure(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        var row = new SimulationRow
        {
            Target = 0.172,
            AsIs = true,
            Gamma = 1,
            LstarDust = 7.67,
            LstarSky = 17.58,
            PctPatchesBelowPaperBlack = 3.7,
            PctBelowLowSeparation = 40,
            PctAboveHighlight = 1,
            PaperBlackLstar = 3.6,
            DustHalf = new LstarRange(7.67, 12.94),
            DustWide = new LstarRange(5.81, 14.86),
            SkyHalf = new LstarRange(15.44, 19.04),
            SkyWide = new LstarRange(14.01, 19.99),
        };
        string plain = ReportWriter.SimulationLine(row);
        string half = ReportWriter.SimulationLine(row, RangeKind.MiddleHalf);
        string wide = ReportWriter.SimulationLine(row, RangeKind.Wide);

        // The figure where it was, the bracket after it, padded to 13 so that the next column
        // starts at the same place on every row.
        Assert.Contains("   " + "    7.7 (7.7 - 12.9) " + "   " + "  17.6 (15.4 - 19.0)" + "   ", half);
        Assert.Contains("   " + "    7.7 (5.8 - 14.9) " + "   " + "  17.6 (14.0 - 20.0)" + "   ", wide);
        // Nothing else moves: the row is the plain one with the two brackets put in.
        int dustEnd = plain.IndexOf("7.7", StringComparison.Ordinal) + 3, skyEnd = plain.IndexOf("17.6", StringComparison.Ordinal) + 4;
        Assert.Equal(plain[..dustEnd] + " (7.7 - 12.9) " + plain[dustEnd..skyEnd] + " (15.4 - 19.0)" + plain[skyEnd..], half);
        Assert.Equal(plain.Length + 28, wide.Length);

        // The header names the two columns, each title over its figure.
        string header = ReportWriter.SimulationHeaderFor(RangeKind.MiddleHalf);
        string dust = Tr.Pick("L* dust", "L* praf"), sky = Tr.Pick("L* sky", "L* cer");
        Assert.Contains(dust + " (interval)", header);
        Assert.Contains(sky + " (interval)", header);
        Assert.Equal(header, ReportWriter.SimulationHeaderFor(RangeKind.Wide));
        Assert.Equal(ReportWriter.SimulationHeader.Length + 28, header.Length);
        Assert.Equal(half.IndexOf("7.7 (", StringComparison.Ordinal) + 3, header.IndexOf(dust, StringComparison.Ordinal) + dust.Length);
        Assert.Equal(half.IndexOf("17.6 (", StringComparison.Ordinal) + 4, header.IndexOf(sky, StringComparison.Ordinal) + sky.Length);
        Assert.EndsWith(ReportWriter.SimulationHeader[(ReportWriter.SimulationHeader.IndexOf(sky, StringComparison.Ordinal) + sky.Length)..], header);
    }

    /// <summary>
    /// On the rows of a real simulation, with their marks, the star and the as-is label: a range
    /// adds its two brackets and leaves the rest of the row where it was. That the ranges decide
    /// nothing is not shown here but by the goldens and the validation figures, which they leave
    /// as they were.
    /// </summary>
    [Fact]
    public void R046_ARangeLeavesTheRestOfEveryRowWhereItWas()
    {
        SimulationResult s = Simulate(WcsFixtures.BandScene());
        foreach (SimulationRow row in s.Rows)
        {
            string plain = ReportWriter.SimulationLine(row);
            foreach (RangeKind kind in new[] { RangeKind.MiddleHalf, RangeKind.Wide })
            {
                string line = ReportWriter.SimulationLine(row, kind);
                Assert.NotEqual(plain, line);
                // The target with its star, and everything from "pure black" to the mark.
                Assert.StartsWith(plain[..29], line);
                Assert.EndsWith(plain[45..], line);
            }
        }
    }

    /// <summary>
    /// The third reason of the dust's definition: a dust class over the 5 % floor whose dark part
    /// is lighter than the darkest sky. The note carries it as the definition words it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R046_TheNoteGivesTheReasonWhenTheDarkestSkyIsDarkerThanTheDustClass(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        var s = new SimulationResult
        {
            Map = Map(i => i < 10 ? PatchClass.Dust : PatchClass.Sky, DustSource.DarkestSky),
            AsIsDust = new DustStats { DustPatchCount = 10 },
        };
        string reason = s.DarkestSkyReason;
        Assert.Equal(
            Tr.Pick("darker than the 25 % threshold of the dust patches, 10.0 % of the frame", "mai intunecat decat pragul de 25 % al dalelor de praf, 10.0 % din cadru"),
            reason);
        Assert.Contains("(" + reason + ")", s.DustDescription);
        Assert.Contains("(" + reason + ")", ReportWriter.RangeNote(s, RangeKind.Wide));
        Assert.Contains(Tr.Pick("can fall outside the brackets", "poate cadea in afara parantezei"), ReportWriter.RangeNote(s, RangeKind.Wide));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R046_TheNoteSaysWhatTheBracketsHoldAndWhereTheDustFigureComesFrom(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);

        // The dust class decides: the note is the two sentences, with the cut of the range shown.
        SimulationResult band = Simulate(WcsFixtures.BandScene());
        Assert.Null(band.DarkestSkyReason);
        Assert.Equal(
            Tr.Pick("In brackets: the L* values the zone's patches lie between, without the darkest 25 % and the lightest 25 % of them. The verdict reads the figure before the brackets, not the range.",
                    "In paranteze: intre ce valori de L* stau dalele zonei, fara cele mai intunecate 25 % si fara cele mai deschise 25 % din ele. Verdictul citeste cifra dinaintea parantezei, nu intervalul."),
            ReportWriter.RangeNote(band, RangeKind.MiddleHalf));
        Assert.Equal(
            Tr.Pick("In brackets: the L* values the zone's patches lie between, without the darkest 10 % and the lightest 10 % of them. The verdict reads the figure before the brackets, not the range.",
                    "In paranteze: intre ce valori de L* stau dalele zonei, fara cele mai intunecate 10 % si fara cele mai deschise 10 % din ele. Verdictul citeste cifra dinaintea parantezei, nu intervalul."),
            ReportWriter.RangeNote(band, RangeKind.Wide));

        // A dust class under the 5 % floor: the darkest sky gives the figure, the bracket is still
        // the class's, and the note says so with the report's own reason.
        SimulationResult little = Simulate(SkyWithASmallDarkBlob(30));
        Assert.Equal(DustSource.DarkestSky, little.Map.DustSource);
        Assert.InRange(little.DustPatchCount, 1, (int)(Thresholds.DustClassMinFraction * little.PatchCount));
        Assert.NotNull(little.AsIs.DustHalf);
        string reason = little.DarkestSkyReason;
        Assert.StartsWith(Tr.Pick("too little dust: ", "prea putin praf: "), reason);
        Assert.Contains("(" + reason + ")", little.DustDescription);
        Assert.EndsWith(
            Tr.Pick($" On this image the dust's figure comes from the darkest sky, not from the dust zone ({reason}), so it can fall outside the brackets.",
                    $" Pe imaginea asta cifra prafului vine din cerul cel mai intunecat, nu din zona de praf ({reason}), asa ca poate cadea in afara parantezei."),
            ReportWriter.RangeNote(little, RangeKind.MiddleHalf));
        Assert.StartsWith(ReportWriter.RangeNote(band, RangeKind.Wide), ReportWriter.RangeNote(little, RangeKind.Wide));

        // No dust patch at all: no bracket, and the note says the dust has no interval.
        SimulationResult none = Simulate(SyntheticRasters.Flat(64, 64, 0.08f));
        Assert.Equal(0, none.DustPatchCount);
        Assert.Null(none.AsIs.DustHalf);
        Assert.Equal(Tr.Pick("no dust found", "nu s-a gasit praf"), none.DarkestSkyReason);
        Assert.EndsWith(
            Tr.Pick(" On this image no dust was found, so the dust has no interval; its figure comes from the darkest sky.",
                    " Pe imaginea asta nu s-a gasit praf, asa ca praful nu are interval; cifra lui vine din cerul cel mai intunecat."),
            ReportWriter.RangeNote(none, RangeKind.MiddleHalf));
    }
}
