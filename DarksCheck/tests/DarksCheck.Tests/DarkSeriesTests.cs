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

/// <summary>Checks 3 to 9 on synthetic darks with known answers (R-020 to R-041).</summary>
public partial class DarkSeriesTests
{
    private static DarkSeriesResult Run(SessionFixture s) => DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2));

    private static SyntheticDark Plain(int i) => new() { Seed = 100 + i, Hot = SyntheticDark.OnePerLevel };

    [Fact]
    public void R020_EveryPlaneReadsItsLevelNoiseAndCountsAndTheFitsBlackIsThePedestal()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Plain);
        DarkSeriesResult r = Run(s);
        DarkSeriesAnalysis d = r.Darks;
        Assert.Equal(6, d.Used);
        Assert.Equal(200, d.Black, 1.0);
        Assert.Contains("negrul luat din valoarea medie a cadrului 1", d.ScaleSource, StringComparison.Ordinal);
        Assert.Equal("RGGB", d.CfaLetters);
        Assert.Equal(["R", "G1", "G2", "B"], d.PlaneNames);
        Assert.Equal(1, d.QuantStep);
        foreach (DarkFrameFigures f in d.Frames)
        {
            foreach (PlaneFigures p in f.Planes)
            {
                Assert.InRange(p.Median, -1.5, 1.5);
                Assert.InRange(p.Sigma, 2.6, 3.4);
                Assert.Equal(0, p.Saturated);
                Assert.Equal(0, p.Low);
            }
        }

        // the five hot pixels, one per level: 15, 30, 60, 150, 600 ADU over the 10 / 20 / 50 / 100 / 500 levels
        int[] totals = Enumerable.Range(0, 5).Select(k => d.Master.Sum(m => m.Above[k])).ToArray();
        Assert.Equal([5, 4, 3, 2, 1], totals);
        Assert.Equal(1.0, d.Norm, 1e-9);
    }

    [Fact]
    public void R021_ABrighterEdgeIsALeakAndTheBiasTakesTheOffsetOut()
    {
        // the left 32 full-resolution columns 3 ADU up on the darks only: the map's left tenth is one block column
        static SyntheticDark Edge(int i) => new() { Seed = 100 + i, Extra = (x, y) => x < 32 ? 3 : 0 };
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, Edge, biases: 3);
        DarkSeriesResult r = Run(s);
        Assert.NotNull(r.DarkCurrent);
        Assert.True(r.Verdicts.Leak.FromDarkMinusBias);
        Assert.Equal(Verdict.Warning, r.Verdicts.Leak.Verdict);
        // the lit edge lights its two corners by the same 3 ADU: noise decides which of the three is named
        Assert.Contains(r.Verdicts.Leak.Where, new[] { MapRegion.Left, MapRegion.TopLeft, MapRegion.BottomLeft });
        Assert.InRange(r.Verdicts.Leak.OffsetAdu, 2.0, 4.0);
        Assert.InRange(r.DarkCurrent!.Median, -1.0, 1.0);
        using StringWriter sw = new();
        DarkReport.Write(r, sw);
        Assert.Contains(" ADU (pe dark minus bias), peste pragul de 1.0: scurgere de lumina sau glow", sw.ToString(), StringComparison.Ordinal);

        using SessionFixture flat = new SessionFixture().WithDarkSeries(4, Plain, biases: 3);
        DarkSeriesResult rf = Run(flat);
        Assert.Equal(Verdict.Ok, rf.Verdicts.Leak.Verdict);
        Assert.InRange(Math.Abs(rf.Verdicts.Leak.OffsetAdu), 0, 1.0);
        Assert.True(rf.Verdicts.Pattern.Verdict == Verdict.Ok, $"pattern: banding {rf.Verdicts.Pattern.BandingP2pAdu}, glow {rf.Verdicts.Pattern.GlowAdu}");
    }

    [Fact]
    public void R030_TheMasterCountsItsHotPixelsPerLevelAndReadsTheirAmplitudes()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Plain);
        DarkSeriesAnalysis d = Run(s).Darks;
        Assert.Equal([5, 4, 3, 2, 1], Enumerable.Range(0, 5).Select(k => d.Master.Sum(m => m.Above[k])));
        // the four pixels above 20 ADU: 30, 60, 150 and 600
        Assert.Equal(4, d.Hot.Hot);
        Assert.InRange(d.Hot.P50, 100, 110);
        Assert.InRange(d.Hot.P10, 35, 45);
        Assert.InRange(d.Hot.P99, 580, 605);
        Assert.Equal(20, d.Hot.ThresholdAdu, 1e-9);
    }

    [Fact]
    public void R021_WithoutBiasesTheLeakIsJudgedOnTheDarkMaster()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, Plain);
        DarkSeriesResult r = Run(s);
        Assert.Null(r.Biases);
        Assert.Null(r.DarkCurrent);
        Assert.False(r.Verdicts.Leak.FromDarkMinusBias);
        Assert.Equal(Verdict.Ok, r.Verdicts.Leak.Verdict);
    }

    [Fact]
    public void R022_RandomRowBandingShowsAgainstWhiteNoiseAndANoisyFrameIsAnOutlier()
    {
        // every frame gets its own row offsets of +-2 ADU: horizontal banding that the master averages out
        static SyntheticDark Banded(int i)
        {
            Random rows = new(900 + i);
            double[] offset = Enumerable.Range(0, 192).Select(_ => rows.NextDouble() * 4 - 2).ToArray();
            return new SyntheticDark { Seed = 100 + i, Extra = (x, y) => offset[y] };
        }

        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Banded);
        DarkSeriesResult r = Run(s);
        Assert.True(r.Verdicts.Noise.RowBandingRatio > 3, $"row banding ratio {r.Verdicts.Noise.RowBandingRatio}");
        Assert.InRange(r.Verdicts.Noise.ColBandingRatio, 0.5, 2.0);

        using SessionFixture plain = new SessionFixture().WithDarkSeries(6, Plain);
        DarkSeriesResult rp = Run(plain);
        Assert.InRange(rp.Verdicts.Noise.RowBandingRatio, 0.5, 2.0);
        Assert.Equal(Verdict.Ok, rp.Verdicts.Noise.Verdict);
        Assert.InRange(rp.Verdicts.Noise.MedianSigma, 2.6, 3.4);

        using SessionFixture noisy = new SessionFixture().WithDarkSeries(6, i => i == 3 ? new SyntheticDark { Seed = 100 + i, Noise = 9 } : Plain(i));
        DarkSeriesResult rn = Run(noisy);
        Assert.Equal(Verdict.Warning, rn.Verdicts.Noise.Verdict);
        Assert.Equal([4], rn.Verdicts.Noise.OutlierFrames);
    }

    [Fact]
    public void R023_TheSeriesIsReadAgainstItsEndAndTheFramesOffItAreNamed()
    {
        // ten hot pixels of 300 ADU whose amplitude approaches 1.2 as 1.2 - 0.4 exp(-i / 3): the median
        // frame (the master) sits near 1.135 and the last five frames near 1.18, so the end of the series reads
        // about 1.04 of the master and the first two frames are more than two degrees under it (under 1.04 x 0.79)
        using SessionFixture s = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 1.2 - 0.4 * Math.Exp(-i / 3.0) });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.Equal(10, w.Pixels);
        Assert.Equal(12, w.Measured);
        Assert.InRange(w.FirstRatio, 0.68, 0.73);
        Assert.InRange(w.LastRatio, 1.03, 1.07);
        Assert.InRange(w.DeltaCelsius, 3.0, 4.0);
        Assert.Equal(22, w.Minutes, 0.01);
        Assert.True(w.HasEnd);
        Assert.InRange(w.EndRatio, 1.02, 1.06);
        // the end is the median of the last five ratios, nothing fitted
        float[] lastFive = r.Darks.Frames.Skip(7).Select(f => (float)f.AmpRatioMedian).ToArray();
        Assert.Equal(Planes.Median(lastFive), w.EndRatio, 1e-9);
        Assert.InRange(w.FirstCelsius, -3.8, -3.0);
        Assert.InRange(w.MasterCelsius, -0.6, -0.15);
        Assert.Equal(Th.DoublingCelsius * Math.Log2(1 / w.EndRatio), w.MasterCelsius, 1e-9);
        Assert.Equal([1, 2], w.OffEndFrames);
        Assert.Equal(Math.Sqrt(12.0 / 10), w.NoiseFactorWithoutOff, 1e-9);
        Assert.Equal(w.EndRatio * Math.Pow(2, -Th.OffEndCelsius / Th.DoublingCelsius), w.OffEndLow, 1e-9);
        Assert.Equal(w.EndRatio * Math.Pow(2, Th.OffEndCelsius / Th.DoublingCelsius), w.OffEndHigh, 1e-9);
        Assert.InRange(w.MasterWithoutOffCelsius, -0.5, -0.05);
        Assert.False(w.Cooling, "a climbing series is not cooling");
        Assert.True(r.Verdicts.Drift.MasterNearEnd);
        Assert.Equal(Verdict.Ok, r.Verdicts.Drift.Verdict);

        ReportSection check4 = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming);
        Assert.Equal(["sfarsitul seriei", "de aruncat", "verdict"], check4.Rows.Select(x => x.Label));
        Assert.StartsWith("aparatul s-a incalzit in timpul seriei: pixelii fierbinti cresc de la 0.7", check4.Rows[0].Value, StringComparison.Ordinal);
        Assert.Contains(" din master in primul cadru la 1.0", check4.Rows[0].Value, StringComparison.Ordinal);
        Assert.Contains(" la sfarsitul seriei (media ultimelor 5 cadre), adica 3.", check4.Rows[0].Value, StringComparison.Ordinal);
        Assert.Matches(@" grade in 22 min; masterul e cu 0\.\d grade sub sfarsitul seriei$", check4.Rows[0].Value);
        Assert.DoesNotContain("+", check4.Rows[0].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("plato", string.Join(" ", check4.Rows.Select(x => x.Value)), StringComparison.Ordinal);
        Assert.StartsWith("2 cadre (1, 2) sunt la mai mult de 2.0 grade de sfarsitul seriei (pixelii fierbinti sub 0.8", check4.Rows[1].Value, StringComparison.Ordinal);
        Assert.Contains(" din master; sfarsitul seriei 1.0", check4.Rows[1].Value, StringComparison.Ordinal);
        Assert.Contains("; fara ele masterul ar fi cu 0.", check4.Rows[1].Value, StringComparison.Ordinal);
        Assert.EndsWith(" grade sub sfarsitul seriei, dar cu 10 % mai mult zgomot", check4.Rows[1].Value, StringComparison.Ordinal);
        Assert.Matches(@"^OK: masterul e aproape de sfarsitul seriei \(cu 0\.\d grade sub el, mai putin de 1\.0\) si niciun cadru nu e prea departe de master \(pixelii fierbinti ai fiecarui cadru sunt intre 0\.7 si 1\.4 din ai masterului\); gradele sunt socotite cu regula: semnalul termic se dubleaza la fiecare 6 grade; cadrele de aruncat sunt in randul de mai sus$", check4.Rows[2].Value);

        // the table: the degrees from the end of the series per frame, the note on the frames off it
        IReadOnlyList<DarkTableRow> rows = DarkReport.FrameRows(r);
        Assert.StartsWith("-3.", rows[0].End, StringComparison.Ordinal);
        Assert.StartsWith("de aruncat: pixelii fierbinti sunt la 0.7", rows[0].Note, StringComparison.Ordinal);
        Assert.Contains(" sub 0.8", rows[0].Note, StringComparison.Ordinal);
        Assert.Contains(", adica la peste 2.0 grade sub sfarsitul seriei (1.0", rows[0].Note, StringComparison.Ordinal);
        Assert.NotNull(rows[1].Note);
        Assert.Null(rows[2].Note);
        Assert.Null(rows[11].Note);
        Assert.Matches(@"^[+-]0\.\d$", rows[11].End);
        Assert.Contains("sfarsit = cu cate grade e cadrul fata de sfarsitul seriei; de aruncat = la peste 2.0 grade de el (verificarea 4)", DarkReport.FrameTableNote(r), StringComparison.Ordinal);
        Assert.Contains("sfarsit", DarkReport.FrameTableHeader(r), StringComparison.Ordinal);
        string line = DarkReport.FrameLine(rows[0]);
        Assert.EndsWith(rows[0].Note, line, StringComparison.Ordinal);
        Assert.DoesNotContain("de aruncat", DarkReport.FrameLine(rows[11]), StringComparison.Ordinal);

        // in English
        using (Ui.Use(UiLanguage.English))
        {
            ReportSection en = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming);
            Assert.Equal(["end of series", "to discard", "verdict"], en.Rows.Select(x => x.Label));
            Assert.StartsWith("the camera warmed during the series: the hot pixels rise from 0.7", en.Rows[0].Value, StringComparison.Ordinal);
            Assert.Contains(" at the end of the series (the average of the last 5 frames), that is 3.", en.Rows[0].Value, StringComparison.Ordinal);
            Assert.Matches(@" degrees in 22 min; the master is 0\.\d degrees below the end of the series$", en.Rows[0].Value);
            Assert.StartsWith("2 frames (1, 2) are more than 2.0 degrees from the end of the series (the hot pixels below 0.8", en.Rows[1].Value, StringComparison.Ordinal);
            Assert.Matches(@"^OK: the master is close to the end of the series \(0\.\d degrees below it, under 1\.0\) and no frame is too far from the master \(the hot pixels of every frame are between 0\.7 and 1\.4 of the master's\); the degrees are reckoned with the rule that the thermal signal doubles every 6 degrees; the frames to discard are in the row above$", en.Rows[2].Value);
            Assert.Contains(", that is more than 2.0 degrees below the end of the series (1.0", DarkReport.FrameRows(r)[0].Note, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void R025_AFrameWithoutTheMastersHotPixelsIsMarkedToDiscard()
    {
        // the climbing series of R023 with its seventh frame's hot pixels under the background: a ratio under 0, so no
        // degrees, but the analysis puts it off the end of the series, and the table says why and tints it
        using SessionFixture s = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i == 6 ? -0.5 : 1.2 - 0.4 * Math.Exp(-i / 3.0) });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.True(w.HasEnd);
        Assert.True(r.Darks.Frames[6].AmpRatioMedian <= 0, $"ratio {r.Darks.Frames[6].AmpRatioMedian}");
        Assert.Contains(7, w.OffEndFrames);
        DarkTableRow row = DarkReport.FrameRows(r)[6];
        Assert.Equal("-", row.End);
        Assert.Equal("de aruncat: pixelii fierbinti ai masterului nu se vad in acest cadru", row.Note);
    }

    [Fact]
    public void R023_WhenTheLastFramesShowNoHotPixelsTheSeriesHasNoEnd()
    {
        // eight frames, the last three with their hot pixels under the background: the median of the last five ratios is
        // not positive, so there is no end to read the series against
        using SessionFixture s = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i >= 5 ? -0.5 : 1.0 });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.Equal(8, w.Measured);
        Assert.False(w.HasEnd);
        Assert.Empty(w.OffEndFrames);
        ReportSection check4 = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming);
        Assert.Equal("NEMASURAT: la sfarsitul seriei pixelii fierbinti ai masterului nu se vad", check4.Rows[0].Value);
        Assert.Equal("NEMASURAT: la sfarsitul seriei pixelii fierbinti ai masterului nu se vad", check4.Rows[1].Value);
        Assert.All(DarkReport.FrameRows(r), row => Assert.Equal("-", row.End));
        // the verdict is still given: the three frames are outside what -opt corrects (R-023: NEMASURAT only under
        // two frames or without a hot pixel)
        Assert.Equal(Verdict.Warning, r.Verdicts.Drift.Verdict);
        Assert.Equal([6, 7, 8], r.Verdicts.Drift.OutsideFrames);
        Assert.StartsWith("ATENTIE: 3 cadre (6 .. 8) sunt prea departe de master ca Siril sa le potriveasca cu -opt (in afara 0.7 .. 1.4; ", check4.Rows[2].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void R023_ARefaWhoseSpeedTheSeriesDoesNotShowGivesTheAdviceWithoutMinutes()
    {
        // a straight fall of 0.12 per frame: the first seven of twelve frames are more than two degrees above the end
        // (and the last one sits at the band's lower edge), and a straight line has no time constant to quote, so the
        // advice names no minutes
        using SessionFixture s = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 2.2 - 0.12 * i });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.True(w.Cooling);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], w.OffEndFrames.Take(7));
        Assert.InRange(w.OffEndFrames.Count, 7, 8);
        Assert.True(double.IsNaN(w.TauMinutes), $"tau {w.TauMinutes}");
        Assert.Equal(Verdict.Redo, r.Verdicts.Drift.Verdict);
        string verdict = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming).Rows[2].Value;
        Assert.Matches(@"^REFA: (7|8) din 12 cadre sunt la peste 2\.0 grade de sfarsitul seriei, deci masterul nu apartine niciunei temperaturi; aparatul a pornit mai cald decat locul in care a stat: refa seria dupa ce aparatul a stat mai mult in locul unde se trag dark-urile(;|$)", verdict);
        Assert.DoesNotContain(" min ", verdict, StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Matches(@"^REDO: (7|8) of 12 frames are more than 2\.0 degrees from the end of the series, so the master belongs to no temperature; the camera started warmer than the place it was in: take the series again after the camera has sat longer where it shoots(;|$)", DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming).Rows[2].Value);
        }
    }

    [Fact]
    public void R023_ADistanceThatRoundsToTheLimitIsPrintedWithTwoDecimals()
    {
        // 0.987 degrees is under the limit of 1.0 and reads OK; printed as 1.0 it would stand beside "under 1.0"
        Assert.Equal("cu 0.99 grade sub sfarsitul seriei", DarkReport.FromEnd(-0.987));
        Assert.Equal("cu 1.04 grade peste sfarsitul seriei", DarkReport.FromEnd(1.04));
        Assert.Equal("cu 0.99 grade sub el", DarkReport.FromIt(-0.987));
        Assert.Equal("cu 0.6 grade sub sfarsitul seriei", DarkReport.FromEnd(-0.62));
        Assert.Equal("cu 1.4 grade peste el", DarkReport.FromIt(1.42));
        Assert.Equal("la nivelul sfarsitului seriei", DarkReport.FromEnd(0.04));
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("0.99 degrees below the end of the series", DarkReport.FromEnd(-0.987));
            Assert.Equal("0.6 degrees below it", DarkReport.FromIt(-0.62));
        }
    }

    [Fact]
    public void R023_ASteadyClimbIsReadAgainstItsEndNotAgainstWhereItWouldSettle()
    {
        // a straight climb of 0.05 per frame: the series never settles, but its master is 0.6 degrees under its end
        // and only the first frame is more than two degrees from it; the rule looks at the end reached (ADR 0043)
        using SessionFixture s = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 0.8 + 0.05 * i });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.InRange(w.EndRatio, 1.06, 1.10);
        Assert.InRange(w.MasterCelsius, -0.9, -0.4);
        Assert.Equal([1], w.OffEndFrames);
        Assert.True(double.IsNaN(w.TauMinutes), $"tau {w.TauMinutes}");
        Assert.Equal(Verdict.Ok, r.Verdicts.Drift.Verdict);
        string verdict = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming).Rows[^1].Value;
        Assert.StartsWith("OK: masterul e aproape de sfarsitul seriei (cu 0.", verdict, StringComparison.Ordinal);
        Assert.EndsWith("; cadrele de aruncat sunt in randul de mai sus", verdict, StringComparison.Ordinal);
        Assert.DoesNotContain("+", verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void R023_AFrameOutsideTheOptMarginOfTheMasterFailsTheSpan()
    {
        // the first frame at half the master's hot pixels, the other six flat: the master is at the end, but a frame is outside 0.7 .. 1.4
        using SessionFixture s = new SessionFixture().WithDarkSeries(7, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i == 0 ? 0.5 : 1.0 });
        DarkSeriesResult r = Run(s);
        Assert.Equal(Verdict.Warning, r.Verdicts.Drift.Verdict);
        Assert.True(r.Verdicts.Drift.MasterNearEnd);
        Assert.Equal([1], r.Darks.Warming.OffEndFrames);
        string verdict = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming).Rows[^1].Value;
        Assert.StartsWith("ATENTIE: 1 cadru (1) e prea departe de master ca Siril sa il potriveasca cu -opt (in afara 0.7 .. 1.4; cel mai departe 0.5", verdict, StringComparison.Ordinal);
        Assert.EndsWith("): seria a pornit rece; arunca-l", verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void R023_AMasterMoreThanADegreeFromTheEndIsAtentie()
    {
        // seven frames at 1.0, then five at 1.2: the median master sits with the first seven, 1.6 degrees under the end
        // of the series, and no frame is two degrees from it; the remedy is a longer series
        using SessionFixture s = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i < 7 ? 1.0 : 1.2 });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.InRange(w.EndRatio, 1.18, 1.22);
        Assert.InRange(w.MasterCelsius, -1.75, -1.4);
        Assert.Empty(w.OffEndFrames);
        Assert.False(r.Verdicts.Drift.MasterNearEnd);
        Assert.False(r.Verdicts.Drift.MostlyOff);
        Assert.Equal(Verdict.Warning, r.Verdicts.Drift.Verdict);
        ReportSection check4 = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming);
        Assert.Matches(@"^aparatul s-a incalzit in timpul seriei: pixelii fierbinti cresc de la (0\.9\d|1\.0\d) din master in primul cadru la 1\.[12]\d la sfarsitul seriei \(media ultimelor 5 cadre\), adica 1\.[4-7] grade in 22 min; masterul e cu 1\.[4-7] grade sub sfarsitul seriei$", check4.Rows[0].Value);
        Assert.Matches(@"^ATENTIE: masterul e cu 1\.[4-7] grade sub sfarsitul seriei, mai mult de 1\.0: cadrele de la inceput, mai reci, il trag in jos; fa seria mai lunga$", check4.Rows[2].Value);

        // the same the other way: the camera started warm, and the row says so
        using SessionFixture cooling = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i < 7 ? 1.2 : 1.0 });
        DarkSeriesResult rc = Run(cooling);
        Assert.True(rc.Darks.Warming.Cooling);
        Assert.InRange(rc.Darks.Warming.MasterCelsius, 1.4, 1.75);
        Assert.Equal(Verdict.Warning, rc.Verdicts.Drift.Verdict);
        string cold = DarkReport.Sections(rc).Single(x => x.Check == ReportCheck.Warming).Rows[2].Value;
        Assert.Matches(@"^ATENTIE: masterul e cu 1\.[4-7] grade peste sfarsitul seriei, mai mult de 1\.0: cadrele de la inceput, mai calde, il trag in sus \(aparatul a pornit mai cald decat locul in care a stat\); fa seria mai lunga$", cold);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Matches(@"^WARNING: the master is 1\.[4-7] degrees below the end of the series, more than 1\.0: the frames from the start, colder, pull it down; take a longer series$", DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming).Rows[2].Value);
            Assert.Matches(@"^WARNING: the master is 1\.[4-7] degrees above the end of the series, more than 1\.0: the frames from the start, warmer, pull it up \(the camera started warmer than the place it was in\); take a longer series$", DarkReport.Sections(rc).Single(x => x.Check == ReportCheck.Warming).Rows[2].Value);
        }
    }

    [Fact]
    public void R023_ASeriesMostlyOffItsEndIsRefa()
    {
        // a cooling series steeper than the owner's darks of 2026-11-07: the hot pixels fall as 0.35 + 1.45 exp(-i / 6),
        // so seven of the twelve frames are more than two degrees above where the series ended (ADR 0038's limit read
        // against the end, ADR 0043); the camera came out warm, and the advice is to let it sit for three time constants
        using SessionFixture s = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 0.35 + 1.45 * Math.Exp(-i / 6.0) });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.True(w.HasEnd);
        Assert.True(w.FirstRatio > w.EndRatio, $"{w.FirstRatio} -> {w.EndRatio}");
        Assert.True(w.Cooling);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], w.OffEndFrames);
        Assert.InRange(w.TauMinutes, 9, 16);
        Assert.True(r.Verdicts.Drift.MostlyOff);
        Assert.Equal(Verdict.Redo, r.Verdicts.Drift.Verdict);
        ReportSection check4 = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming);
        Assert.StartsWith("aparatul s-a racit in timpul seriei: pixelii fierbinti scad de la 1.9", check4.Rows[0].Value, StringComparison.Ordinal);
        Assert.Matches(@"; masterul e cu 2\.\d grade peste sfarsitul seriei$", check4.Rows[0].Value);
        string verdict = check4.Rows[2].Value;
        int tau = (int)Math.Round(w.TauMinutes);
        int rest = (int)Math.Ceiling(3 * w.TauMinutes / 5) * 5;
        Assert.StartsWith($"REFA: 7 din 12 cadre sunt la peste 2.0 grade de sfarsitul seriei, deci masterul nu apartine niciunei temperaturi; aparatul a pornit mai cald decat locul in care a stat: refa seria dupa ce aparatul a stat vreo {rest} min in locul unde se trag dark-urile (de trei ori cele {tau} min ale seriei); 5 cadre (1 .. 3, 11, 12) sunt prea departe de master", verdict, StringComparison.Ordinal);
        Assert.EndsWith("): seria a pornit calda; arunca-le", verdict, StringComparison.Ordinal);
        Assert.Equal("REFA", check4.Rows[2].Verdict);
        Assert.DoesNotContain("+", verdict, StringComparison.Ordinal);
        Assert.DoesNotContain("plato", verdict, StringComparison.Ordinal);

        // the chart's note names the falling curve and the end of the series
        Assert.Contains("cand curba urca, aparatul se incalzeste in timpul seriei; cand coboara, se raceste (verificarea 4).", DarkReport.WarmingChartNote(r), StringComparison.Ordinal);
        Assert.Contains(" Sfarsitul seriei (media ultimelor 5 cadre) e la 0.7", DarkReport.WarmingChartNote(r), StringComparison.Ordinal);

        // a warming series mostly off its end (the hot pixels double every eight frames): the same verdict, the advice
        // is a longer series, with no cause named; the frames outside the -opt band come last
        using SessionFixture warming = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = Math.Pow(2, i / 8.0) });
        DarkSeriesResult rw = Run(warming);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], rw.Darks.Warming.OffEndFrames);
        Assert.Equal(Verdict.Redo, rw.Verdicts.Drift.Verdict);
        string warm = DarkReport.Sections(rw).Single(x => x.Check == ReportCheck.Warming).Rows[2].Value;
        Assert.StartsWith("REFA: 7 din 12 cadre sunt la peste 2.0 grade de sfarsitul seriei, deci masterul nu apartine niciunei temperaturi; refa seria mai lunga: primele cadre sunt incalzirea; 4 cadre (1, 2, 11, 12) sunt prea departe de master", warm, StringComparison.Ordinal);

        // in English
        using (Ui.Use(UiLanguage.English))
        {
            string en = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming).Rows[2].Value;
            Assert.StartsWith($"REDO: 7 of 12 frames are more than 2.0 degrees from the end of the series, so the master belongs to no temperature; the camera started warmer than the place it was in: take the series again after the camera has sat where it shoots for about {rest} min (three times the {tau} min of this series); 5 frames (1 .. 3, 11, 12) are too far from the master", en, StringComparison.Ordinal);
            Assert.StartsWith("the camera cooled during the series: the hot pixels fall from 1.9", DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming).Rows[0].Value, StringComparison.Ordinal);
            Assert.Contains("; take a longer series: the first frames are the warm-up; 4 frames (1, 2, 11, 12) are too far from the master", DarkReport.Sections(rw).Single(x => x.Check == ReportCheck.Warming).Rows[2].Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void R023_SixFramesHaveAnEndAndHalfTheFramesOffItAreNotYetRefa()
    {
        // six frames are enough for an end (Th.WarmingFitMinFrames), the median of the last five
        using SessionFixture six = new SessionFixture().WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot });
        DarkSeriesResult r6 = Run(six);
        Assert.Equal(6, r6.Darks.Warming.Measured);
        Assert.True(r6.Darks.Warming.HasEnd);
        Assert.Equal(Planes.Median(r6.Darks.Frames.Skip(1).Select(f => (float)f.AmpRatioMedian).ToArray()), r6.Darks.Warming.EndRatio, 1e-9);

        // six frames at 1.6 and then six at 1.0: exactly half are more than two degrees above the end, and REFA needs
        // more than half (Th.RedoOffEndFraction); the master, between the two levels, is far from the end
        using SessionFixture half = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i < 6 ? 1.6 : 1.0 });
        DarkSeriesResult r = Run(half);
        Assert.Equal([1, 2, 3, 4, 5, 6], r.Darks.Warming.OffEndFrames);
        Assert.False(r.Verdicts.Drift.MostlyOff);
        Assert.False(r.Verdicts.Drift.MasterNearEnd);
        Assert.Equal(Verdict.Warning, r.Verdicts.Drift.Verdict);
        string verdict = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming).Rows[2].Value;
        Assert.Matches(@"^ATENTIE: masterul e cu 2\.\d grade peste sfarsitul seriei, mai mult de 1\.0: cadrele de la inceput, mai calde, il trag in sus \(aparatul a pornit mai cald decat locul in care a stat\); arunca cadrele din randul de mai sus$", verdict);
    }

    [Fact]
    public void R023_AFlatSeriesHasNoFrameOffItsEnd()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(7, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.InRange(w.EndRatio, 0.97, 1.03);
        Assert.Empty(w.OffEndFrames);
        Assert.Equal(Verdict.Ok, r.Verdicts.Drift.Verdict);
        ReportSection check4 = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming);
        Assert.StartsWith("aparatul a stat la aceeasi temperatura: pixelii fierbinti sunt la ", check4.Rows[0].Value, StringComparison.Ordinal);
        Assert.Contains(" din master in primul cadru si la ", check4.Rows[0].Value, StringComparison.Ordinal);
        Assert.Matches(@" la sfarsitul seriei \(media ultimelor 5 cadre\), mai putin de 1\.0 grade intre ele; masterul e (la nivelul sfarsitului seriei|cu 0\.\d grade (sub|peste) sfarsitul seriei)$", check4.Rows[0].Value);
        Assert.StartsWith("niciun cadru nu e la mai mult de 2.0 grade de sfarsitul seriei (pixelii fierbinti intre ", check4.Rows[1].Value, StringComparison.Ordinal);
        Assert.Matches(@"^OK: masterul e aproape de sfarsitul seriei \((la nivelul lui|cu 0\.\d grade (sub|peste) el), mai putin de 1\.0\) si niciun cadru nu e prea departe de master \(", check4.Rows[2].Value);
        Assert.DoesNotContain("-opt", check4.Rows[2].Value, StringComparison.Ordinal);
        Assert.EndsWith("; niciun cadru de aruncat", check4.Rows[2].Value, StringComparison.Ordinal);
        Assert.All(DarkReport.FrameRows(r), row => Assert.Null(row.Note));
    }

    [Fact]
    public void R023_UnderSixFramesThereIsNoEndAndTheVerdictRestsOnTheSpan()
    {
        // five frames climbing 0.8 → 1.2 of the master: the first-to-last degrees, no end of the series
        using SessionFixture s = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 0.8 + 0.4 * i / 4 });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.InRange(w.DeltaCelsius, 3.0, 4.0);
        Assert.Equal(8, w.Minutes, 0.01);
        Assert.False(w.HasEnd);
        Assert.True(double.IsNaN(w.EndRatio));
        Assert.Empty(w.OffEndFrames);
        Assert.Equal(Verdict.Ok, r.Verdicts.Drift.Verdict);
        ReportSection check4 = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Warming);
        Assert.Equal("NEMASURAT: sunt mai putin de 6 cadre cu pixeli fierbinti", check4.Rows[0].Value);
        Assert.Equal("NEMASURAT: sunt mai putin de 6 cadre cu pixeli fierbinti", check4.Rows[1].Value);
        Assert.Equal("OK: niciun cadru nu e prea departe de master (pixelii fierbinti ai fiecarui cadru sunt intre 0.7 si 1.4 din ai masterului); sunt mai putin de 6 cadre, deci sfarsitul seriei nu se masoara", check4.Rows[2].Value);
        // an OK asks nothing of the owner, so it does not name -opt (only the ATENTIE for a frame outside the band does)
        Assert.DoesNotContain("-opt", check4.Rows[2].Value, StringComparison.Ordinal);
        Assert.All(DarkReport.FrameRows(r), row => Assert.Equal("-", row.End));

        using SessionFixture cold = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i == 0 ? 0.5 : 1.0 });
        Assert.Equal(Verdict.Warning, Run(cold).Verdicts.Drift.Verdict);
    }

    /// <summary>Ten hot pixels of 300 ADU: strong enough for a frame's amplitude ratio to be read to half a percent.</summary>
    private static readonly (int X, int Y, double Adu)[] StrongHot = Enumerable.Range(0, 10).Select(k => (60 + 7 * k, 100 + 5 * k, 300.0)).ToArray();

    private static readonly (int X, int Y, double Adu)[] TenHot = Enumerable.Range(0, 10).Select(k => (60 + 7 * k, 100 + 5 * k, 100.0)).ToArray();

    /// <summary>Eight frames whose hot pixels climb from 0.8 to 1.2 of the master's: the warming of R-023, the x axis of R-028 and R-029.</summary>
    private static double WarmFactor(int i) => 0.8 + 0.4 * i / 7;

    [Fact]
    public void R028_FramesWithLightOverTheWarmingAreNamedAndTheTableShowsEachFramesExcess()
    {
        // the last two frames carry +0.6 ADU everywhere: a lamp that came on while the series warmed
        using SessionFixture s = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = i >= 6 ? (x, y) => 0.6 : null });
        DarkSeriesResult r = Run(s);
        FrameLight fl = r.Verdicts.Leak.Frames!;
        Assert.Equal([7, 8], fl.LitFrames);
        Assert.InRange(fl.ExcessAdu[6], 0.4, 0.8);
        Assert.InRange(fl.ExcessAdu[7], 0.4, 0.8);
        Assert.InRange(fl.ExcessAdu[0], -0.1, 0.1);
        Assert.InRange(fl.ScaleAdu, 0, 0.05);
        Assert.Equal(Verdict.Warning, r.Verdicts.Leak.Verdict);
        using StringWriter sw = new();
        DarkReport.Write(r, sw);
        string text = sw.ToString();
        Assert.Contains("ATENTIE: 2 cadre au lumina in plus fata de restul seriei (7: +0.", text, StringComparison.Ordinal);
        Assert.Contains(" ADU); scoate-le din darks\\ sau refa seria", text, StringComparison.Ordinal);
        Assert.Contains("    lumina pe cadre    : 2 cadre sunt mai luminoase decat ar explica incalzirea: 7: +0.", text, StringComparison.Ordinal);
        Assert.Contains("; cel mai luminat, +0.", text, StringComparison.Ordinal);
        ReportRow litRow = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Leaks).Rows.Single(x => x.Label == "lumina pe cadre");
        Assert.Contains($"{r.Darks.Noise.MasterNoise:F2} ADU", litRow.BoldParts);
        Assert.All(litRow.BoldParts, b => Assert.Contains(b, litRow.Value, StringComparison.Ordinal));
        Assert.Contains("   fierb. sfarsit  lumina  latura   >20   [pixelii verzi G1;", DarkReport.FrameTableHeader(r), StringComparison.Ordinal);
        Assert.Contains("lumina = lumina in plus fata de cat explica incalzirea, ADU", DarkReport.FrameTableNote(r), StringComparison.Ordinal);
        IReadOnlyList<DarkTableRow> rows = DarkReport.FrameRows(r);
        Assert.StartsWith("+0.", rows[6].Light, StringComparison.Ordinal);
        Assert.Matches("^[+-]0\\.0[0-9]$", rows[0].Light);

        // the same warming without the lamp: no frame is lit
        using SessionFixture clean = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i) });
        DarkSeriesResult rc = Run(clean);
        Assert.Empty(rc.Verdicts.Leak.Frames!.LitFrames);
        Assert.Equal(Verdict.Ok, rc.Verdicts.Leak.Verdict);
        using StringWriter swc = new();
        DarkReport.Write(rc, swc);
        Assert.Contains("; niciun cadru nu are lumina in plus", swc.ToString(), StringComparison.Ordinal);
        Assert.Contains("    lumina pe cadre    : niciun cadru nu e mai luminos decat ar explica incalzirea (prag: peste 0.1 ", swc.ToString(), StringComparison.Ordinal);

        // under six frames the fit has nothing to stand on
        using SessionFixture few = new SessionFixture().WithDarkSeries(4, Plain);
        DarkSeriesResult rf = Run(few);
        Assert.Null(rf.Verdicts.Leak.Frames);
        Assert.Equal("-", DarkReport.FrameRows(rf)[0].Light);
        using StringWriter swf = new();
        DarkReport.Write(rf, swf);
        Assert.Contains("    lumina pe cadre    : NEMASURAT: sunt mai putin de 6 cadre cu pixeli fierbinti", swf.ToString(), StringComparison.Ordinal);
        Assert.Contains("; lumina pe cadre nu s-a putut masura", swf.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void R071_TheVerdictsOfChecks3And4CarryOneStatementPerLine()
    {
        // the console's words cut where its text has "; " outside brackets, so the window draws one statement per
        // line; the value the console prints is the same (ADR 0047)
        Assert.Equal(["a (b; c)", "d [e; f]", "g"], ReportRow.StatementsOf("a (b; c); d [e; f]; g"));
        Assert.Equal(["one"], ReportRow.StatementsOf("one"));

        // two lit frames: what check 3 found, then what to do with them
        using SessionFixture lit = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = i >= 6 ? (x, y) => 0.6 : null });
        ReportRow leak = VerdictRow(Run(lit), ReportCheck.Leaks);
        Assert.Equal("ATENTIE", leak.Verdict);
        AssertStatements(leak);
        Assert.StartsWith("2 cadre au lumina in plus fata de restul seriei (7: +0.", leak.Lines![0], StringComparison.Ordinal);
        Assert.Equal("scoate-le din darks\\ sau refa seria", leak.Lines[1]);

        // a clean series: the OK on the map, then the frames
        using SessionFixture clean = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i) });
        ReportRow ok = VerdictRow(Run(clean), ReportCheck.Leaks);
        Assert.Equal("OK", ok.Verdict);
        AssertStatements(ok);
        Assert.StartsWith("nicio margine si niciun colt nu difera de centru", ok.Lines![0], StringComparison.Ordinal);
        Assert.Equal("niciun cadru nu are lumina in plus", ok.Lines[^1]);

        // check 4 on a series that cooled steeply: the count, the advice and the frames -opt cannot match, each on a line
        using SessionFixture cooling = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 0.35 + 1.45 * Math.Exp(-i / 6.0) });
        DarkSeriesResult r = Run(cooling);
        ReportRow redo = VerdictRow(r, ReportCheck.Warming);
        Assert.Equal("REFA", redo.Verdict);
        AssertStatements(redo);
        Assert.Equal("7 din 12 cadre sunt la peste 2.0 grade de sfarsitul seriei, deci masterul nu apartine niciunei temperaturi", redo.Lines![0]);
        Assert.StartsWith("aparatul a pornit mai cald decat locul in care a stat: refa seria", redo.Lines[1], StringComparison.Ordinal);
        Assert.Contains(redo.Lines, line => line.Contains("(in afara 0.7 .. 1.4; cel mai departe ", StringComparison.Ordinal));
        using (Ui.Use(UiLanguage.English))
        {
            ReportRow en = VerdictRow(r, ReportCheck.Warming);
            AssertStatements(en);
            Assert.Equal("7 of 12 frames are more than 2.0 degrees from the end of the series, so the master belongs to no temperature", en.Lines![0]);
        }

        // a verdict of one statement keeps one line: without hot pixels the warming is not measured
        using SessionFixture none = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i });
        ReportRow unmeasured = VerdictRow(Run(none), ReportCheck.Warming);
        Assert.Equal("NEMASURAT", unmeasured.Verdict);
        Assert.Null(unmeasured.Lines);
    }

    [Fact]
    public void R025_TheSectionsCarryTheirCheckInTheRezumatsOrder()
    {
        // the console prints the checks as the Rezumat shows them, numbered as they stand; each section names its check,
        // so nothing has to find one by its number (ADR 0046)
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, Plain);
        DarkSeriesResult r = Run(s);
        ReportCheck[] order = [ReportCheck.Lights, ReportCheck.Leaks, ReportCheck.Warming, ReportCheck.Noise, ReportCheck.HotPixels, ReportCheck.Pattern, ReportCheck.DeadSaturated, ReportCheck.Enough, ReportCheck.Index];
        Assert.Equal(order, DarkReport.Sections(r).Select(x => x.Check));
        Assert.Equal(["2.", "3.", "4.", "5.", "6.", "7.", "8.", "9.", "IN"], DarkReport.Sections(r).Select(x => x.Title[..2]));
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal(order, DarkReport.Sections(r).Select(x => x.Check));
            Assert.Equal(["2. HOW WELL THE DARK FITS THE LIGHTS", "3. LIGHT LEAKS AND DIFFERENCES ACROSS THE FRAME", "4. THE CAMERA'S WARMING DURING THE SERIES", "5. THE NOISE OF EACH FRAME", "6. HOT PIXELS", "7. FIXED PATTERN: WHAT IS THE SAME IN EVERY FRAME", "8. DEAD AND SATURATED PIXELS", "9. HOW MANY DARKS ARE ENOUGH", "INDEX"], DarkReport.Sections(r).Select(x => x.Title));
        }
    }

    /// <summary>The verdict row of one check of the report.</summary>
    private static ReportRow VerdictRow(DarkSeriesResult r, ReportCheck check) => DarkReport.Sections(r).Single(x => x.Check == check).Rows.Single(x => x.Label == "verdict");

    /// <summary>A row carries two or more statements, which joined by "; " give its text, none cut further.</summary>
    private static void AssertStatements(ReportRow row)
    {
        Assert.NotNull(row.Lines);
        Assert.True(row.Lines.Count >= 2, row.Value);
        Assert.Equal(row.Rest, string.Join("; ", row.Lines));
        Assert.All(row.Lines, line => Assert.Equal([line], ReportRow.StatementsOf(line)));
    }

    [Fact]
    public void R028_TheFitIsNotTiltedByLitFramesAtTheWarmEnd()
    {
        // twelve frames warming 0.8 -> 1.2, the last four carrying +0.8 ADU: a third of the frames, all at the warm end,
        // where an ordinary fit would read them as a steeper warming
        static double Factor12(int i) => 0.8 + 0.4 * i / 11;
        using SessionFixture s = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = Factor12(i), Extra = i >= 8 ? (x, y) => 0.8 : null });
        DarkSeriesResult r = Run(s);
        FrameLight fl = r.Verdicts.Leak.Frames!;
        Assert.Equal([9, 10, 11, 12], fl.LitFrames);
        // the clean frames' mean does not depend on the warming (ten hot pixels in 49k), so the slope stays near zero
        Assert.InRange(fl.Slope, -0.5, 0.5);
        Assert.InRange(fl.ScaleAdu, 0, 0.05);
        Assert.All(fl.LitExcessAdu, v => Assert.InRange(v, 0.6, 1.0));
        Assert.All(Enumerable.Range(0, 8), k => Assert.InRange(fl.ExcessAdu[k], -0.1, 0.1));
        Assert.InRange(fl.RatioSpan, 0.2, 0.35);
    }

    /// <summary>
    /// CR-014: the median of the pairwise slopes gives way once about a third of the frames, at one end of the
    /// warming, carry light: the line tilts through them, the scale grows, and no frame is named. A second fit,
    /// from a line that holds up to nearly half, finds such a block.
    /// </summary>
    [Fact]
    public void R028_ABlockOfLitFramesOverAQuarterOfTheSeriesIsNamed()
    {
        // eight frames warming 0.8 -> 1.2, the last three carrying +0.6 ADU
        using SessionFixture warm = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = i >= 5 ? (x, y) => 0.6 : null });
        DarkSeriesResult r = Run(warm);
        FrameLight fl = r.Verdicts.Leak.Frames!;
        Assert.Equal([6, 7, 8], fl.LitFrames);
        Assert.All(fl.LitExcessAdu, v => Assert.InRange(v, 0.4, 0.8));
        Assert.All(Enumerable.Range(0, 5), k => Assert.InRange(fl.ExcessAdu[k], -0.1, 0.1));
        Assert.InRange(fl.Slope, -0.5, 0.5);
        Assert.InRange(fl.ScaleAdu, 0, 0.05);
        Assert.Equal(Verdict.Warning, r.Verdicts.Leak.Verdict);
        using StringWriter sw = new();
        DarkReport.Write(r, sw);
        Assert.Contains("    lumina pe cadre    : 3 cadre sunt mai luminoase decat ar explica incalzirea: 6: +0.", sw.ToString(), StringComparison.Ordinal);

        // the same block at the cold end
        using SessionFixture cold = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = i < 3 ? (x, y) => 0.6 : null });
        Assert.Equal([1, 2, 3], Run(cold).Verdicts.Leak.Frames!.LitFrames);

        // two of six, a third of the shortest series the fit reads
        using SessionFixture six = new SessionFixture().WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = 0.8 + 0.4 * i / 5, Extra = i >= 4 ? (x, y) => 0.6 : null });
        Assert.Equal([5, 6], Run(six).Verdicts.Leak.Frames!.LitFrames);

        // eleven of thirty, as on a long series whose last third had the lamp on
        using SessionFixture thirty = new SessionFixture().WithDarkSeries(30, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = 0.8 + 0.4 * i / 29, Extra = i >= 19 ? (x, y) => 0.6 : null });
        Assert.Equal(Enumerable.Range(20, 11), Run(thirty).Verdicts.Leak.Frames!.LitFrames);
    }

    /// <summary>
    /// The two starts of R-028's fit on their own: Theil-Sen's line is tilted by a block of a third of the points,
    /// the block start's is not, and on points without a block the two agree.
    /// </summary>
    [Fact]
    public void R028_TheBlockStartHoldsWhereTheMedianOfThePairwiseSlopesTilts()
    {
        // twelve points on y = 1 + 2x with a small wobble, the last five lifted by 1: most pairs join a lifted point to a clean one
        double[] x = Enumerable.Range(0, 12).Select(i => 0.8 + 0.4 * i / 11).ToArray();
        double[] wobble = [0.004, -0.006, 0.002, -0.003, 0.005, -0.001, 0.003, -0.004, 0.001, -0.002, 0.006, -0.005];
        double[] clean = x.Select((v, i) => 1 + 2 * v + wobble[i]).ToArray();
        double[] lifted = clean.Select((v, i) => i >= 7 ? v + 1 : v).ToArray();
        bool[] all = Enumerable.Repeat(true, 12).ToArray();

        (double tiltedSlope, _) = LeakAnalyzer.TheilSen(x, lifted, all);
        Assert.True(tiltedSlope > 3, $"the median of the pairwise slopes is tilted by the block: {tiltedSlope}");
        (double slope, double intercept) = LeakAnalyzer.BlockStart(x, lifted);
        Assert.InRange(slope, 1.9, 2.1);
        Assert.InRange(intercept, 0.9, 1.1);

        // without a block the start is the line the points lie on
        (double cleanSlope, double cleanIntercept) = LeakAnalyzer.BlockStart(x, clean);
        Assert.InRange(cleanSlope, 1.9, 2.1);
        Assert.InRange(cleanIntercept, 0.9, 1.1);

        // every abscissa the same: no line through two points, so the level alone
        double[] flat = Enumerable.Repeat(1.0, 6).ToArray();
        (double flatSlope, double flatIntercept) = LeakAnalyzer.BlockStart(flat, [5, 5.1, 4.9, 5, 5.2, 4.8]);
        Assert.Equal(0, flatSlope);
        Assert.Equal(5, flatIntercept, 6);
    }

    [Fact]
    public void R029_AnEdgeIsSplitIntoGlowAndConstantAndTheBiasNamesTheReadoutOffset()
    {
        MapRegion[] leftish = [MapRegion.Left, MapRegion.TopLeft, MapRegion.BottomLeft];

        // glow: the left 32 columns carry 3 x the frame's hot factor, so the edge grows with the warming
        using SessionFixture glow = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = (x, y) => x < 32 ? 3 * WarmFactor(i) : 0 });
        DarkSeriesResult rg = Run(glow);
        Assert.Equal(Verdict.Warning, rg.Verdicts.Leak.Verdict);
        Assert.Contains(rg.Verdicts.Leak.Where, leftish);
        EdgeSplit sg = rg.Verdicts.Leak.Split!;
        Assert.Equal(rg.Verdicts.Leak.Where, sg.Where);
        Assert.InRange(sg.GlowAdu, 2.6, 3.4);
        Assert.InRange(sg.ConstantAdu, -0.5, 0.5);
        Assert.Null(sg.ReadoutAdu);
        Assert.Equal(LeakCause.Glow, sg.Cause);
        Assert.Equal(8, rg.Verdicts.Leak.Splits.Count);
        using StringWriter swg = new();
        DarkReport.Write(rg, swg);
        Assert.Contains("): e glow-ul senzorului, dark-urile raman bune", swg.ToString(), StringComparison.Ordinal);
        Assert.Contains("    glow / constant    : la ", swg.ToString(), StringComparison.Ordinal);
        Assert.Contains(", partea care creste cand senzorul se incalzeste (in afara ei, constant ", swg.ToString(), StringComparison.Ordinal);
        Assert.InRange(rg.Verdicts.Pattern.GlowAdu, 2.6, 3.4);

        // leak: the same edge constant on every dark and absent from flat biases
        using SessionFixture leak = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = (x, y) => x < 32 ? 3 : 0 }, biases: 4);
        DarkSeriesResult rl = Run(leak);
        Assert.Equal(Verdict.Warning, rl.Verdicts.Leak.Verdict);
        EdgeSplit sl = rl.Verdicts.Leak.Split!;
        Assert.InRange(sl.GlowAdu, -0.5, 0.5);
        Assert.InRange(sl.ConstantAdu, 2.5, 3.5);
        Assert.InRange(sl.ReadoutAdu!.Value, -0.5, 0.5);
        Assert.InRange(sl.RemainderAdu, 2.5, 3.5);
        Assert.Equal(LeakCause.Leak, sl.Cause);
        using StringWriter swl = new();
        DarkReport.Write(rl, swl);
        Assert.Contains("): e lumina intrata in aparat, refa dark-urile", swl.ToString(), StringComparison.Ordinal);
        Assert.Contains(" vine din bias, raman ", swl.ToString(), StringComparison.Ordinal);

        // readout: the same edge in the biases too; dark - bias is flat, so the verdict is OK and the left edge's split says readout
        using SessionFixture readout = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = (x, y) => x < 32 ? 3 : 0 }, biases: 4, bias: i => new SyntheticDark { Seed = 500 + i, Extra = (x, y) => x < 32 ? 3 : 0 });
        DarkSeriesResult rr = Run(readout);
        Assert.Equal(Verdict.Ok, rr.Verdicts.Leak.Verdict);
        EdgeSplit sr = rr.Verdicts.Leak.Splits.Single(x => x.Where == MapRegion.Left);
        Assert.InRange(sr.ReadoutAdu!.Value, 2.5, 3.5);
        Assert.InRange(sr.RemainderAdu, -0.5, 0.5);
        Assert.Equal(LeakCause.Readout, sr.Cause);

        // without biases a constant edge cannot be told from a readout offset
        using SessionFixture constant = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = (x, y) => x < 32 ? 3 : 0 });
        DarkSeriesResult rk = Run(constant);
        Assert.Equal(LeakCause.Constant, rk.Verdicts.Leak.Split!.Cause);
        using StringWriter swk = new();
        DarkReport.Write(rk, swk);
        Assert.Contains("): lumina intrata sau o diferenta de nivel din citire; fara bias-uri nu se pot deosebi", swk.ToString(), StringComparison.Ordinal);
        Assert.Contains(" ADU (fara bias-uri nu se stie daca e lumina sau o diferenta de nivel din citirea senzorului)", swk.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("(in fisier:", swk.ToString(), StringComparison.Ordinal);

        // the cause is read on the brighter side: a readout dip of -3 in the bias under a dark edge of -1 leaves
        // dark - bias at +2, light that the bias does not hold, so the cause is the leak, not the larger readout part
        using SessionFixture dip = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = (x, y) => x < 32 ? -1 : 0 }, biases: 4, bias: i => new SyntheticDark { Seed = 500 + i, Extra = (x, y) => x < 32 ? -3 : 0 });
        DarkSeriesResult rd = Run(dip);
        Assert.Equal(Verdict.Warning, rd.Verdicts.Leak.Verdict);
        Assert.Contains(rd.Verdicts.Leak.Where, leftish);
        EdgeSplit sd = rd.Verdicts.Leak.Split!;
        Assert.InRange(sd.ReadoutAdu!.Value, -3.5, -2.5);
        Assert.InRange(sd.RemainderAdu, 1.5, 2.5);
        Assert.Equal(LeakCause.Leak, sd.Cause);
    }

    /// <summary>
    /// CR-015: a side's constant light is the value of its line at ratio 0, and the line is the median of the
    /// pairwise slopes. One far frame gave the old error formula (least squares') a long lever and so a small
    /// error, though a median of pairwise slopes gains nothing from one frame: the error printed was several
    /// times too small, and a side without light could pass for light.
    /// </summary>
    [Fact]
    public void R029_AWarmingThatRestsOnOneColdFrameGivesASideAnErrorThatFitsItsLine()
    {
        // thirty darks, no light anywhere: the first at half the master's warmth, the others within 0.03 of each other
        using SessionFixture cold = new SessionFixture().WithDarkSeries(30, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = i == 0 ? 0.5 : 1.0 + 0.001 * i }, biases: 3);
        DarkSeriesResult rc = Run(cold);
        // the same darks warming evenly over the same range of the first frame's lever
        using SessionFixture even = new SessionFixture().WithDarkSeries(30, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = 0.5 + 0.53 * i / 29 }, biases: 3);
        DarkSeriesResult re = Run(even);
        Assert.Equal(4, rc.Verdicts.Leak.Sides.Count);
        Assert.Equal(4, re.Verdicts.Leak.Sides.Count);
        // one frame opens the gate of the span (0.1), as before
        Assert.InRange(rc.Verdicts.Leak.Frames!.RatioSpan, 0.4, 0.65);
        foreach (MapSide side in Enum.GetValues<MapSide>())
        {
            SideLight one = rc.Verdicts.Leak.Sides.Single(s => s.Side == side), spread = re.Verdicts.Leak.Sides.Single(s => s.Side == side);
            // with the warming spread over the frames the line is well held; resting on one frame it is not
            Assert.True(one.ErrorAdu > 3 * spread.ErrorAdu, $"{side}: {one.ErrorAdu} against {spread.ErrorAdu}");
            Assert.False(LeakVerdict.IsConstantLight(one), $"{side}: {one.LightAdu} +/- {one.ErrorAdu}");
        }

        Assert.Null(rc.Verdicts.Leak.ConstantLight);
    }

    /// <summary>The error of a Theil-Sen slope from its own pairwise slopes (Sen's interval at one sigma), on figures worked out by hand.</summary>
    [Fact]
    public void R029_TheSlopeErrorIsHalfTheRangeOfThePairwiseSlopesAtOneSigma()
    {
        // four points; the six pairwise slopes sorted: 0.7, 0.95, 0.95, 1.0333, 1.2, 1.2. C = sqrt(4 x 3 x 13 / 18) = 2.944,
        // so from slope floor((6 - 2.944) / 2) = 1 to slope ceil((6 + 2.944) / 2) = 5: (1.2 - 0.95) / 2
        double[] x = [0, 1, 2, 3], y = [0, 1.2, 1.9, 3.1];
        bool[] all = [true, true, true, true];
        Assert.Equal(0.125, LeakAnalyzer.SlopeError(x, y, all), 9);

        // points on a line: every pairwise slope is the line's
        Assert.Equal(0, LeakAnalyzer.SlopeError(x, [1, 3, 5, 7], all), 9);

        // only the kept points count: without the last one, three slopes 1.2, 0.95, 0.7 and C = sqrt(3 x 2 x 11 / 18) = 1.915,
        // from slope floor(0.54) = 0 to slope ceil(2.46) = 2 (the last): (1.2 - 0.7) / 2
        Assert.Equal(0.25, LeakAnalyzer.SlopeError(x, y, [true, true, true, false]), 9);

        // no two distinct abscissae: no slope, no error
        Assert.True(double.IsNaN(LeakAnalyzer.SlopeError([1, 1, 1], [1, 2, 3], [true, true, true])));
        Assert.True(double.IsNaN(LeakAnalyzer.SlopeError(x, y, [true, false, false, false])));
    }

    [Fact]
    public void R029_ALeftOrRightSideIsNamedWithItsPreposition()
    {
        // the band of the test below on the right 64 columns, with flat biases: every side text names it "latura din
        // dreapta", where the top band reads "latura de sus" (ADR 0031); the English keeps "the right side"
        static double Band(int x, int y) => x >= 192 ? 2 : 0;
        using SessionFixture leak = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = Band, Noise = 1 }, biases: 4);
        DarkSeriesResult r = Run(leak);
        Assert.Equal(MapSide.Right, r.Verdicts.Leak.WorstSide!.Side);
        Assert.NotNull(r.Verdicts.Leak.ConstantLight);
        foreach (bool english in new[] { false, true })
        {
            using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
            {
                using StringWriter sw = new();
                DarkReport.Write(r, sw);
                string text = sw.ToString();
                Assert.Contains(english ? "in every frame the right side has +" : "in toate cadrele latura din dreapta are +", text, StringComparison.Ordinal);
                Assert.Contains(english ? "; the right side goes over 0.15 and over 3 times" : "; latura din dreapta trece de 0.15 si de 3 ori", text, StringComparison.Ordinal);
                Assert.Contains(english ? "side = the right side against the centre, once the bias is taken off (glow included), ADU" : "latura = latura din dreapta fata de centru, dupa scaderea bias-ului (cu glow), ADU", DarkReport.FrameTableNote(r), StringComparison.Ordinal);
                Assert.Contains(ScaleReport.Rows(r), row => row.Value.Contains(english ? "constant light on the right side: " : "lumina constanta pe latura din dreapta: ", StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void R029_LightInEveryFrameIsReadPerSideAndTheBiasClearsAReadoutBand()
    {
        // a band of +2 ADU over the top 64 rows (the top third) of every dark, the raised mirror's shadow of an eyepiece leak
        // (noise 1 ADU instead of the fixture's 3: a region of these 256 x 192 frames has 256 pixels, so at noise 3 the
        // intercept error is far over the 0.67 at which a band of 2 stops passing the 3-sigma condition; the D3300's
        // regions have 2.4 Mpx)
        static double Band(int x, int y) => y < 64 ? 2 : 0;

        // flat biases: light the bias does not hold, in every frame
        using SessionFixture leak = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = Band, Noise = 1 }, biases: 4);
        DarkSeriesResult r = Run(leak);
        Assert.Equal(4, r.Verdicts.Leak.Sides.Count);
        SideLight top = r.Verdicts.Leak.Sides.Single(s => s.Side == MapSide.Top);
        Assert.InRange(top.RemainderAdu!.Value, 1.5, 2.5);
        Assert.InRange(top.ConstantAdu, 1.5, 2.5);
        Assert.Equal(MapSide.Top, r.Verdicts.Leak.WorstSide!.Side);
        Assert.NotNull(r.Verdicts.Leak.ConstantLight);
        Assert.Empty(r.Verdicts.Leak.Frames!.LitFrames);
        Assert.Equal(Verdict.Warning, r.Verdicts.Leak.Verdict);
        using StringWriter sw = new();
        DarkReport.Write(r, sw);
        string text = sw.ToString();
        Assert.Contains("in toate cadrele latura de sus are +", text, StringComparison.Ordinal);
        Assert.Contains(") lumina constanta fata de centru, care nu vine din bias: lumina intra in aparat (prin ocular sau prin montura obiectivului)", text, StringComparison.Ordinal);
        Assert.Contains("    lumina constanta   : fata de centru, dupa scaderea bias-ului: sus +", text, StringComparison.Ordinal);
        Assert.Contains(" ADU; latura de sus trece de 0.15 si de 3 ori cat poate gresi masuratoarea; +", text, StringComparison.Ordinal);
        Assert.Contains(" ADU e ", text, StringComparison.Ordinal);
        Assert.Contains(" din zgomotul pe pixel al masterului dark (", text, StringComparison.Ordinal);
        // the band of 2 passes three times its error while the error is under 0.67 (the bound was 0.4 for the error of
        // before ADR 0065, which was too small for a line read from eight frames; it reads 0.59 here)
        Assert.True(top.ErrorAdu > 0 && top.ErrorAdu < 0.67, $"error {top.ErrorAdu}");
        // the row and the verdict mark the light and the master's noise bold (ADR 0016); check 9's master row marks its noise
        IReadOnlyList<ReportSection> sections = DarkReport.Sections(r);
        ReportRow constantRow = sections.Single(x => x.Check == ReportCheck.Leaks).Rows.Single(x => x.Label == "lumina constanta");
        string masterNoise = $"{r.Darks.Noise.MasterNoise:F2} ADU";
        Assert.Contains(masterNoise, constantRow.BoldParts);
        Assert.Contains(constantRow.BoldParts, b => b.StartsWith('+') && b.EndsWith(" ADU", StringComparison.Ordinal));
        Assert.All(constantRow.BoldParts, b => Assert.Contains(b, constantRow.Value, StringComparison.Ordinal));
        // the ratio's value is the light in raw ADU over the master's noise (the normalisation is 1 here)
        System.Text.RegularExpressions.Match ratio = System.Text.RegularExpressions.Regex.Match(constantRow.Value, @"; ([+-][0-9.]+) ADU e ([0-9.]+) din zgomotul pe pixel al masterului dark \(([0-9.]+) ADU\)$");
        Assert.True(ratio.Success, constantRow.Value);
        Assert.Equal(top.LightAdu, double.Parse(ratio.Groups[1].Value, CultureInfo.InvariantCulture), 2);
        Assert.Equal(top.LightAdu / r.Darks.Noise.MasterNoise, double.Parse(ratio.Groups[2].Value, CultureInfo.InvariantCulture), 1);
        Assert.Equal(r.Darks.Noise.MasterNoise, double.Parse(ratio.Groups[3].Value, CultureInfo.InvariantCulture), 2);
        ReportRow verdictRow = sections.Single(x => x.Check == ReportCheck.Leaks).Rows.Single(x => x.Label == "verdict");
        Assert.Contains(masterNoise, verdictRow.BoldParts);
        Assert.Contains("; adica +", verdictRow.Value, StringComparison.Ordinal);
        ReportRow enough = sections.Single(s => s.Check == ReportCheck.Enough).Rows.Single(x => x.Label == "master");
        Assert.Equal([masterNoise], enough.BoldParts);
        Assert.Contains(masterNoise, enough.Value, StringComparison.Ordinal);
        Assert.Empty(sections.Single(x => x.Check == ReportCheck.Leaks).Rows.Single(x => x.Label == "master dark").BoldParts);
        Assert.Contains("   fierb. sfarsit  lumina  latura   >20   [pixelii verzi G1;", DarkReport.FrameTableHeader(r), StringComparison.Ordinal);
        Assert.Contains("latura = latura de sus fata de centru, dupa scaderea bias-ului (cu glow), ADU", DarkReport.FrameTableNote(r), StringComparison.Ordinal);
        IReadOnlyList<DarkTableRow> rows = DarkReport.FrameRows(r);
        // one frame, three regions of 256 synthetic pixels each: a noisy column, the band still in every frame
        Assert.All(rows, row => Assert.InRange(double.Parse(row.Side, CultureInfo.InvariantCulture), 1.0, 3.0));

        // the same band in the biases: a readout structure, nothing remains after the bias
        using SessionFixture readout = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = Band, Noise = 1 }, biases: 4, bias: i => new SyntheticDark { Seed = 500 + i, Extra = Band });
        DarkSeriesResult rr = Run(readout);
        SideLight topR = rr.Verdicts.Leak.Sides.Single(s => s.Side == MapSide.Top);
        Assert.InRange(topR.RemainderAdu!.Value, -0.5, 0.5);
        Assert.Null(rr.Verdicts.Leak.ConstantLight);
        Assert.Equal(Verdict.Ok, rr.Verdicts.Leak.Verdict);
        using StringWriter swr = new();
        DarkReport.Write(rr, swr);
        Assert.Contains("; nicio latura nu trece de 0.15 si de 3 ori cat poate gresi masuratoarea; o lumina egala pe tot cadrul nu se vede aici, pentru ca nu are cu ce fi comparata", swr.ToString(), StringComparison.Ordinal);
        Assert.Contains("; nicio latura nu are lumina constanta peste 0.15 ADU", swr.ToString(), StringComparison.Ordinal);
        Assert.All(DarkReport.FrameRows(rr), row => Assert.InRange(double.Parse(row.Side, CultureInfo.InvariantCulture), -1.0, 1.0));

        // without biases the sides are constants only: nothing to judge, the row says why
        using SessionFixture nobias = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = Band, Noise = 1 });
        DarkSeriesResult rn = Run(nobias);
        SideLight topN = rn.Verdicts.Leak.Sides.Single(s => s.Side == MapSide.Top);
        Assert.Null(topN.RemainderAdu);
        Assert.InRange(topN.ConstantAdu, 1.5, 2.5);
        Assert.Null(rn.Verdicts.Leak.ConstantLight);
        using StringWriter swn = new();
        DarkReport.Write(rn, swn);
        Assert.Contains("    lumina constanta   : fara bias-uri nu se poate desparti de diferenta de nivel din citire: constant sus +", swn.ToString(), StringComparison.Ordinal);
        Assert.Contains("latura = latura de sus fata de centru (fara bias-uri, cu glow), ADU", DarkReport.FrameTableNote(rn), StringComparison.Ordinal);

        // under six frames: unmeasured, like the split
        using SessionFixture few = new SessionFixture().WithDarkSeries(4, Plain, biases: 3);
        DarkSeriesResult rf = Run(few);
        Assert.Empty(rf.Verdicts.Leak.Sides);
        Assert.Null(rf.Verdicts.Leak.WorstSide);
        Assert.Equal("-", DarkReport.FrameRows(rf)[0].Side);
        Assert.Contains("latura = latura cu cea mai multa lumina constanta fata de centru (NEMASURAT)", DarkReport.FrameTableNote(rf), StringComparison.Ordinal);
    }

    [Fact]
    public void R021_InEnglishTheVerdictSetsTheConstantLightAgainstTheNoiseInOneClause()
    {
        // the eyepiece band of the test above, worded in English: "that is" opens a sentence with its own verb, so it
        // takes a comma ("that is, +x ADU is y times ..."), never "that is +x ADU is y times ..."
        static double Band(int x, int y) => y < 64 ? 2 : 0;
        using SessionFixture leak = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = Band, Noise = 1 }, biases: 4);
        using (Ui.Use(UiLanguage.English))
        {
            DarkSeriesResult r = Run(leak);
            Assert.NotNull(r.Verdicts.Leak.ConstantLight);
            ReportRow verdict = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Leaks).Rows.Single(x => x.Label == "verdict");
            Assert.Matches(@"; that is, [+-][0-9.]+ ADU is [0-9.]+ times the dark master's noise per pixel \([0-9.]+ ADU\)", verdict.Value);
            Assert.DoesNotContain("; that is +", verdict.Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void R029_TheSidesNeedFiveFramesLeftAfterTheLitOnes()
    {
        // six clean frames warming; the per-frame light is then told, by hand, that the last two are lit: four remain,
        // enough for the split (three) but not for the sides' error (five). A synthetic series cannot produce this
        // on its own: two lit frames of six would tilt the Theil-Sen line itself (8 of 15 pairs contaminated).
        static double Factor6(int i) => 0.8 + 0.4 * i / 5;
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = Factor6(i) }, biases: 4);
        DarkSeriesResult r = Run(s);
        FrameLight measured = r.Verdicts.Leak.Frames!;
        Assert.Empty(measured.LitFrames);
        FrameLight twoLit = measured with { LitFrames = [5, 6], LitExcessAdu = [0.8, 0.8] };
        Assert.Equal(4, twoLit.FramesKept);
        Regions bias = r.Biases!.MeanMaps[r.ReferencePlane].Regions;
        IReadOnlyList<EdgeSplit> splits = LeakAnalyzer.EdgeSplitsOf(r.Darks.Frames, twoLit, bias, r.Darks.Norm);
        Assert.Equal(8, splits.Count);
        Assert.Empty(LeakAnalyzer.SidesOf(r.Darks.Frames, twoLit, bias, r.Darks.Norm));

        // the report says why, and the column has nothing to show
        LeakVerdict leak = r.Verdicts.Leak with { Frames = twoLit, Splits = splits, Sides = [] };
        DarkSeriesResult r2 = r with { Verdicts = r.Verdicts with { Leak = leak } };
        Assert.Null(r2.Verdicts.Leak.WorstSide);
        using StringWriter sw = new();
        DarkReport.Write(r2, sw);
        Assert.Contains("    lumina constanta   : NEMASURAT: fara cadrele luminate raman prea putine (4, trebuie cel putin 5)", sw.ToString(), StringComparison.Ordinal);
        Assert.Contains("    glow / constant    : la ", sw.ToString(), StringComparison.Ordinal);
        Assert.Equal("-", DarkReport.FrameRows(r2)[0].Side);
    }

    [Fact]
    public void R080_TheScaleSectionExplainsTheAduOfTheOpenSet()
    {
        // the fixture's FITS name the camera NIKON D3300 at ISO 400 and 120 s: the reference scale, the gain from the table
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Plain, biases: 3).WithLights(4, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.0, Stars = SyntheticLight.ThreeStars });
        DarkSeriesResult r = Run(s);
        IReadOnlyList<ReportRow> rows = ScaleReport.Rows(r);
        Assert.Equal(["fisier", "1 ADU-eq", "pasul valorilor", "electroni", "zgomot de citire", "zgomot pe un dark", "zgomot in master", "praguri pe set", "ce s-a gasit", "cer", "saturatie"], rows.Select(x => x.Label));
        string Value(string label) => rows.Single(x => x.Label == label).Value;
        Assert.Equal("valori de la 0 la 65535 ADU (16 biti); negrul camerei e la 200 si albul la 65535, deci pentru lumina raman 0 .. 65335 ADU", Value("fisier"));
        Assert.Equal("1 ADU din fisier: setul e facut chiar la setarile de referinta, ISO 400, 120.0 s", Value("1 ADU-eq"));
        Assert.Equal("langa negru, valorile din fisier cresc din 1 in 1 ADU", Value("pasul valorilor"));
        Assert.Equal("la ISO 400, 1 ADU = 2.07 electroni (senzorul transforma lumina in electroni, iar camera ii numara in ADU); 1 ADU-eq = 1.00 ADU = 2.1 electroni; intr-un pixel incap 34446 electroni (full well), iar pixelul prinde 56 % din lumina care cade pe el (QE). Cifre pentru Nikon D3300 de pe photonstophotos.net, scoase din masuratorile DxOMark, citite pe 2026-10-20; la senzorii mici metoda da prea multi electroni pe ADU.", Value("electroni"));
        Assert.Matches(@"^[0-9.]+ ADU pe pixel = [0-9.]+ electroni: zgomotul pe care camera il adauga cand citeste senzorul, masurat pe bias-uri \(pixelii verzi G1\)$", Value("zgomot de citire"));
        Assert.StartsWith($"un dark are {r.Darks.Noise.SigmaFrame:F2} ADU de zgomot pe pixel; ", Value("zgomot pe un dark"), StringComparison.Ordinal);
        Assert.Contains("; curentul de intuneric (ce aduna in medie un pixel din caldura senzorului) e ", Value("zgomot pe un dark"), StringComparison.Ordinal);
        Assert.Equal("pixel fierbinte: peste 20 ADU-eq (20 ADU in fisier); scurgere de lumina pe harta: peste 1.0 ADU-eq (1.0 ADU); lumina constanta pe o latura: peste 0.15 ADU-eq (0.15 ADU); lumina pe un singur cadru: peste 0.1 ADU-eq (0.10 ADU)", Value("praguri pe set"));
        Assert.Equal("nimic peste praguri", Value("ce s-a gasit"));
        Assert.Matches(@"^pe un light, cerul e la [0-9.]+ ADU peste negru, cu [0-9.]+ ADU de zgomot pe pixel \(pixelii verzi G1, din 4 lights\)$", Value("cer"));
        Assert.Matches(@"^masterul dark, facut din 6 cadre, mai are [0-9.]+ ADU de zgomot pe pixel; stack-ul celor 4 lights are [0-9.]+ ADU pe pixel daca il faci cu media si [0-9.]+ daca il faci cu mediana \(cu zgomotul masterului dark inclus\)$", Value("zgomot in master"));
        ReportRow masterRow = rows.Single(x => x.Label == "zgomot in master");
        Assert.Equal(2, masterRow.BoldParts.Count);
        Assert.All(masterRow.BoldParts, b => Assert.Contains(b, masterRow.Value, StringComparison.Ordinal));
        StackNoise stack = StackNoise.Of(r.Lights, r.ReferencePlane, r.Darks.Noise.MasterNoise)!;
        Assert.Equal(4, stack.Lights);
        Assert.Equal(stack.SkySigmaAdu / 2, stack.LightsShareAdu, 6);
        Assert.Equal(r.Darks.Noise.MasterNoise, stack.MasterNoiseAdu, 6);
        // the master's noise is subtracted from every light alike, so it adds in quadrature and does not average out
        Assert.Equal(Math.Sqrt(stack.LightsShareAdu * stack.LightsShareAdu + stack.MasterNoiseAdu * stack.MasterNoiseAdu), stack.MeanStackAdu, 6);
        Assert.Equal(Math.Sqrt(Math.Pow(Th.MedianNoiseFactor * stack.LightsShareAdu, 2) + stack.MasterNoiseAdu * stack.MasterNoiseAdu), stack.MedianStackAdu, 6);
        Assert.Equal(0, StackNoise.Of(r.Lights, r.ReferencePlane, double.NaN)!.MasterNoiseAdu);
        Assert.Null(StackNoise.Of(null, 0, 1));
        Assert.Contains("cu zgomotul masterului dark inclus)", Value("zgomot in master"), StringComparison.Ordinal);
        Assert.Contains($" Al masterului dark ({stack.MasterNoiseAdu:F2} ADU) nu scade, pentru ca acelasi master se scade din toate lights-urile.", ScaleReport.Paragraphs(r)[2], StringComparison.Ordinal);
        Assert.Equal("la 65535 ADU pixelul e plin si nu mai poate creste; dupa sursa, intr-un pixel incap 34446 electroni (full well)", Value("saturatie"));
        IReadOnlyList<string> paragraphs = ScaleReport.Paragraphs(r);
        Assert.Equal(4, paragraphs.Count);
        Assert.StartsWith("ADU e unitatea in care fisierul scrie cat de luminos e fiecare pixel: aici numere intregi de la 0 la 65535 (16 biti), cu negrul camerei la 200.", paragraphs[0], StringComparison.Ordinal);
        Assert.Contains("In stack-ul celor 4 lights zgomotul pe pixel scade la ", paragraphs[2], StringComparison.Ordinal);
        Assert.Contains("intr-o zona de 50 x 50 de pixeli zgomotul se compenseaza, iar greseala se vede de 50 de ori mai bine decat pe un pixel", paragraphs[1], StringComparison.Ordinal);
        Assert.Contains($"Masterul dark, facut din 6 cadre, mai are {r.Darks.Noise.MasterNoise:F2} ADU de zgomot pe pixel", paragraphs[1], StringComparison.Ordinal);
        Assert.Contains("Lumina constanta pe o latura conteaza peste 0.15 ADU-eq, si doar daca e de peste 3 ori mai mare decat cat poate gresi masuratoarea.", paragraphs[3], StringComparison.Ordinal);

        // the console: the block at the end, the paragraphs wrapped under 96 columns
        using StringWriter sw = new();
        DarkReport.Write(r, sw);
        string text = sw.ToString().ReplaceLineEndings("\n");
        int at = text.IndexOf("\n  EXPLICATII SCARA ADU\n", StringComparison.Ordinal);
        Assert.True(at > text.IndexOf("TABEL PE LIGHTS", StringComparison.Ordinal), "the block comes after the tables");
        string block = text[at..];
        Assert.Contains("    electroni          : la ISO 400, 1 ADU = 2.07 electroni", block, StringComparison.Ordinal);
        Assert.All(block.Split('\n'), line => Assert.True(line.Length <= 96 || line.Contains(':', StringComparison.Ordinal), $"wrapped line too long: {line}"));
        Assert.Contains("\n    ADU e unitatea in care fisierul", block, StringComparison.Ordinal);

        // without biases and lights: the rows say what is missing
        using SessionFixture bare = new SessionFixture().WithDarkSeries(4, Plain);
        IReadOnlyList<ReportRow> bareRows = ScaleReport.Rows(Run(bare));
        Assert.Equal("NEMASURAT: sesiunea nu are bias-uri (sursa da 2.0 electroni pentru Nikon D3300)", bareRows.Single(x => x.Label == "zgomot de citire").Value);
        Assert.Equal("fara lights in sesiune", bareRows.Single(x => x.Label == "cer").Value);
        Assert.EndsWith("; stack-ul de lights: sesiunea nu are lights", bareRows.Single(x => x.Label == "zgomot in master").Value, StringComparison.Ordinal);
        Assert.EndsWith("; curentul de intuneric (ce aduna un pixel din caldura senzorului) se masoara doar cu bias-uri", bareRows.Single(x => x.Label == "zgomot pe un dark").Value, StringComparison.Ordinal);

        // frames whose noise could not be measured leave no master noise either (a single frame gets one, R-024);
        // no fixture reaches it, so the figure is taken away by hand
        DarkSeriesResult measured = Run(bare);
        DarkSeriesResult unmeasured = measured with { Darks = measured.Darks with { Noise = measured.Darks.Noise with { MasterNoise = double.NaN } } };
        Assert.Equal("NEMASURAT: zgomotul cadrelor nu s-a putut masura, deci nici al masterului", ScaleReport.Rows(unmeasured).Single(x => x.Label == "zgomot in master").Value);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("UNMEASURED: the frames' noise could not be measured, so neither could the master's", ScaleReport.Rows(unmeasured).Single(x => x.Label == "master noise").Value);
        }
        Assert.StartsWith("Fata de cer: sesiunea nu are lights", ScaleReport.Paragraphs(Run(bare))[2], StringComparison.Ordinal);

        // the camera table: the D3300 by any spelling, the log2 interpolation, nothing outside it, no other camera
        CameraFacts d3300 = CameraFacts.For("NIKON D3300")!;
        Assert.NotNull(d3300);
        Assert.Equal(0.526, d3300.GainAt(1600)!.Value, 3);
        Assert.InRange(d3300.GainAt(1131)!.Value, 0.72, 0.76);
        Assert.Null(d3300.GainAt(50));
        Assert.Null(d3300.GainAt(51200));
        Assert.Null(CameraFacts.For("Canon EOS 6D"));
        Assert.Null(CameraFacts.For(null));
        Assert.Equal(["a b c", "dd e"], ScaleReport.Wrap("a b c dd e", 5));
    }

    /// <summary>
    /// CR-003: the stack's noise added the lights' sky noise, in the lights' ADU, to the dark master's,
    /// in the darks' ADU; lights at twice the darks' ISO doubled the lights' share.
    /// </summary>
    [Fact]
    public void R080_LightsAtTwiceTheIsoGiveTheSameStackFigures()
    {
        // one scene at the darks' gain and at twice it: twice the sky, its noise and the hot pixels, declared at ISO 800
        static SyntheticLight L(int i, double gain) => new() { Seed = 300 + i, Sky = 500 * gain, Noise = 6 * gain, Hot = SyntheticLight.Lattice, HotFactor = gain };
        static SyntheticDark D(int i) => new() { Seed = 100 + i, Hot = SyntheticLight.Lattice };
        using SessionFixture same = new SessionFixture().WithDarkSeries(6, D).WithLights(4, i => L(i, 1));
        using SessionFixture twice = new SessionFixture().WithDarkSeries(6, D).WithLights(4, i => L(i, 2), iso: 800);
        DarkSeriesResult a = Run(same), b = Run(twice);
        StackNoise sa = StackNoise.Of(a.Lights, a.ReferencePlane, a.Darks.Noise.MasterNoise)!;
        StackNoise sb = StackNoise.Of(b.Lights, b.ReferencePlane, b.Darks.Noise.MasterNoise)!;

        // the darks are the same files, so the master's noise is the same figure, in the darks' ADU
        Assert.Equal(sa.MasterNoiseAdu, sb.MasterNoiseAdu, 9);
        Assert.False(sa.Scaled);
        Assert.True(sb.Scaled);
        Assert.All(a.Lights!.Frames, f => Assert.Equal(1, f.ToDarks, 9));
        Assert.All(b.Lights!.Frames, f => Assert.Equal(0.5, f.ToDarks, 9));
        // the lights' figures come to the darks' ADU: half of what the files at ISO 800 hold
        Assert.InRange(sb.SkyAdu, 0.99 * sa.SkyAdu, 1.01 * sa.SkyAdu);
        Assert.InRange(sb.SkySigmaAdu, 0.97 * sa.SkySigmaAdu, 1.03 * sa.SkySigmaAdu);
        Assert.InRange(sb.MeanStackAdu, 0.97 * sa.MeanStackAdu, 1.03 * sa.MeanStackAdu);
        Assert.InRange(sb.MedianStackAdu, 0.97 * sa.MedianStackAdu, 1.03 * sa.MedianStackAdu);
        // so a light of 0.79 ADU found in the darks is the same share of either stack's noise
        Assert.InRange((0.79 / sb.MeanStackAdu) / (0.79 / sa.MeanStackAdu), 0.97, 1.03);

        // the sky row says the figures were worked out for the darks' settings, and only then
        string Sky(DarkSeriesResult r) => ScaleReport.Rows(r).Single(x => x.Label == Ui.T("sky", "cer")).Value;
        const string Note = "; lights-urile sunt la alte setari decat dark-urile, asa ca cifrele sunt socotite pentru ISO-ul si expunerea dark-urilor";
        Assert.DoesNotContain("socotite pentru", Sky(a), StringComparison.Ordinal);
        Assert.EndsWith(" (pixelii verzi G1, din 4 lights)" + Note, Sky(b), StringComparison.Ordinal);
        Assert.StartsWith($"pe un light, cerul e la {sb.SkyAdu.ToString("0.0", CultureInfo.InvariantCulture)} ADU peste negru, cu {sb.SkySigmaAdu.ToString("0.0", CultureInfo.InvariantCulture)} ADU de zgomot pe pixel", Sky(b), StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.DoesNotContain("worked out for", Sky(a), StringComparison.Ordinal);
            Assert.EndsWith("; the lights are at other settings than the darks, so the figures are worked out for the darks' ISO and exposure", Sky(b), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Review of the CR-003 fix: a light a hair off the darks' exposure, as one of a real session's
    /// was (118.5 s among 119.2 s), made the sky row say the lights are at other settings, under a
    /// check 1 that calls them the same.
    /// </summary>
    [Fact]
    public void R080_LightsWithinTheExposureToleranceAreNotCalledOtherSettings()
    {
        static SyntheticLight L(int i) => new() { Seed = 300 + i, Hot = SyntheticLight.Lattice };
        static SyntheticDark D(int i) => new() { Seed = 100 + i, Hot = SyntheticLight.Lattice };
        static string Sky(DarkSeriesResult r) => ScaleReport.Rows(r).Single(x => x.Label == "cer").Value;

        // darks of 120 s, lights of 118.5 s: 1.25 % apart, inside the 2 % check 1 calls one exposure
        using SessionFixture near = new SessionFixture().WithDarkSeries(6, D).WithLights(4, L, exposure: 118.5);
        DarkSeriesResult rn = Run(near);
        Assert.Equal(Verdict.Ok, rn.Inventory.Match.DarksVsLights);
        // the figures are still brought to the darks' exposure, as check 9 brings them
        Assert.All(rn.Lights!.Frames, f => Assert.Equal(120 / 118.5, f.ToDarks, 9));
        Assert.False(StackNoise.Of(rn.Lights, rn.ReferencePlane, rn.Darks.Noise.MasterNoise)!.Scaled);
        Assert.DoesNotContain("socotite pentru", Sky(rn), StringComparison.Ordinal);
        Assert.EndsWith(" (pixelii verzi G1, din 4 lights)", Sky(rn), StringComparison.Ordinal);

        // the same the other way round, lights a little longer than the darks
        using SessionFixture nearLong = new SessionFixture().WithDarkSeries(6, D).WithLights(4, L, exposure: 122);
        DarkSeriesResult rl = Run(nearLong);
        Assert.Equal(Verdict.Ok, rl.Inventory.Match.DarksVsLights);
        Assert.False(StackNoise.Of(rl.Lights, rl.ReferencePlane, rl.Darks.Noise.MasterNoise)!.Scaled);

        // 110 s is another exposure for check 1, and for the row
        using SessionFixture far = new SessionFixture().WithDarkSeries(6, D).WithLights(4, L, exposure: 110);
        DarkSeriesResult rf = Run(far);
        Assert.Equal(Verdict.Warning, rf.Inventory.Match.DarksVsLights);
        Assert.True(StackNoise.Of(rf.Lights, rf.ReferencePlane, rf.Darks.Noise.MasterNoise)!.Scaled);
        Assert.EndsWith("; lights-urile sunt la alte setari decat dark-urile, asa ca cifrele sunt socotite pentru ISO-ul si expunerea dark-urilor", Sky(rf), StringComparison.Ordinal);

        // the second review: the note is decided by the settings themselves, as check 1 and the stack compare them,
        // not rebuilt from the factor. Exactly at the 2 % (100 s against 98 s) check 1 says the same exposure;
        // the factor 100 / 98 falls just beyond it in floating point
        using SessionFixture bound = new SessionFixture().WithDarkSeries(6, D, exposure: 100).WithLights(4, L, exposure: 98);
        DarkSeriesResult rb = Run(bound);
        Assert.Equal(Verdict.Ok, rb.Inventory.Match.DarksVsLights);
        Assert.False(StackNoise.Of(rb.Lights, rb.ReferencePlane, rb.Darks.Noise.MasterNoise)!.Scaled);
        // just outside it, 120 s against 117.5 s
        using SessionFixture outside = new SessionFixture().WithDarkSeries(6, D).WithLights(4, L, exposure: 117.5);
        DarkSeriesResult ro = Run(outside);
        Assert.Equal(Verdict.Warning, ro.Inventory.Match.DarksVsLights);
        Assert.True(StackNoise.Of(ro.Lights, ro.ReferencePlane, ro.Darks.Noise.MasterNoise)!.Scaled);

        // twice the ISO and half the exposure: the factor is exactly 1 and no figure moves, yet the lights are at
        // other settings, as check 1 says in the same report
        using SessionFixture cancel = new SessionFixture().WithDarkSeries(6, D).WithLights(4, L, exposure: 60, iso: 800);
        DarkSeriesResult rc = Run(cancel);
        Assert.Equal(Verdict.Warning, rc.Inventory.Match.DarksVsLights);
        Assert.All(rc.Lights!.Frames, f => Assert.Equal(1, f.ToDarks, 9));
        Assert.All(rc.Lights.Frames, f => Assert.True(f.OtherSettings));
        Assert.True(StackNoise.Of(rc.Lights, rc.ReferencePlane, rc.Darks.Noise.MasterNoise)!.Scaled);

        // one stray test frame among lights at the darks' settings: check 1 names the lights by their usual
        // setting, and the row's figures are those of the other four, so no note
        using SessionFixture stray = new SessionFixture().WithDarkSeries(6, D).WithLights(4, L);
        File.WriteAllBytes(Path.Combine(stray.LightsDir, "light_0005.fit"), L(4).Build(SessionFixture.FirstStart.AddSeconds(-3000), 30, 400));
        DarkSeriesResult rs = Run(stray);
        Assert.Equal(Verdict.Ok, rs.Inventory.Match.DarksVsLights);
        Assert.Equal(5, rs.Lights!.Used);
        Assert.Equal(1, rs.Lights.Frames.Count(f => f.OtherSettings));
        Assert.False(StackNoise.Of(rs.Lights, rs.ReferencePlane, rs.Darks.Noise.MasterNoise)!.Scaled);
        Assert.EndsWith(" (pixelii verzi G1, din 5 lights)", Sky(rs), StringComparison.Ordinal);
    }

    /// <summary>
    /// The third paragraph gives the lights' sky and noise as the `cer` row does, brought to the darks'
    /// settings; with lights at other settings it now opens by saying so, in the row's words.
    /// </summary>
    [Fact]
    public void R080_TheThirdParagraphSaysWhenTheLightsAreAtOtherSettings()
    {
        static SyntheticLight L(int i, double gain) => new() { Seed = 300 + i, Sky = 500 * gain, Noise = 6 * gain, Hot = SyntheticLight.Lattice, HotFactor = gain };
        static SyntheticDark D(int i) => new() { Seed = 100 + i, Hot = SyntheticLight.Lattice };
        using SessionFixture same = new SessionFixture().WithDarkSeries(6, D).WithLights(4, i => L(i, 1));
        using SessionFixture twice = new SessionFixture().WithDarkSeries(6, D).WithLights(4, i => L(i, 2), iso: 800);
        DarkSeriesResult a = Run(same), b = Run(twice);
        string pa = ScaleReport.Paragraphs(a)[2], pb = ScaleReport.Paragraphs(b)[2];

        Assert.StartsWith("Fata de cer: pe un light al acestei sesiuni cerul e la ", pa, StringComparison.Ordinal);
        Assert.DoesNotContain("alte setari", pa, StringComparison.Ordinal);
        const string Opening = "Fata de cer: lights-urile sunt la alte setari decat dark-urile, asa ca cifrele lor sunt socotite aici pentru ISO-ul si expunerea dark-urilor. Pe un light al acestei sesiuni cerul e la ";
        Assert.StartsWith(Opening, pb, StringComparison.Ordinal);
        // the rest of the paragraph is the one every session has, from the sky's figure on
        string Rest(string p) => p[p.IndexOf(" ADU peste negru, deci 1 ADU e ", StringComparison.Ordinal)..];
        Assert.Contains(" In stack-ul celor 4 lights zgomotul pe pixel scade la ", Rest(pb), StringComparison.Ordinal);
        Assert.EndsWith("Iar o greseala de 1 ADU din master ramane 1 ADU in stack.", pb, StringComparison.Ordinal);
        Assert.Equal(Rest(pa).Split(". ").Length, Rest(pb).Split(". ").Length);

        using (Ui.Use(UiLanguage.English))
        {
            Assert.StartsWith("Against the sky: on a light of this session the sky is at ", ScaleReport.Paragraphs(a)[2], StringComparison.Ordinal);
            Assert.StartsWith(
                "Against the sky: the lights are at other settings than the darks, so their figures are worked out here for the darks' ISO and exposure. On a light of this session the sky is at ",
                ScaleReport.Paragraphs(b)[2],
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void R080_TheFirstParagraphSaysWhenTheCameraIsNotInTheTable()
    {
        // a camera the table does not know (ADR 0023): the electroni row says so, and now the first paragraph too
        using SessionFixture canon = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticDark.OnePerLevel, Camera = "Canon EOS 6D" });
        DarkSeriesResult r = Run(canon);
        Assert.Equal("necunoscut pentru Canon EOS 6D: aplicatia nu are in tabel cati electroni inseamna 1 ADU la acest aparat", ScaleReport.Rows(r).Single(x => x.Label == "electroni").Value);
        Assert.EndsWith(" Aparatul Canon EOS 6D nu e in tabelul aplicatiei, asa ca nicio cifra de aici nu e data in electroni si lipsesc cifrele din sursa: cati electroni incap intr-un pixel (full well) si zgomotul de citire.", ScaleReport.Paragraphs(r)[0], StringComparison.Ordinal);
        Assert.Equal("NEMASURAT: sesiunea nu are bias-uri", ScaleReport.Rows(r).Single(x => x.Label == "zgomot de citire").Value);
        Assert.Equal("la 65535 ADU pixelul e plin si nu mai poate creste", ScaleReport.Rows(r).Single(x => x.Label == "saturatie").Value);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.EndsWith(" Canon EOS 6D is not in the application's table, so no figure here is given in electrons, and the source's figures are missing: how many electrons a pixel holds (full well) and the read noise.", ScaleReport.Paragraphs(r)[0], StringComparison.Ordinal);
        }

        // no readable dark names the camera
        using SessionFixture unnamed = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticDark.OnePerLevel, Camera = null });
        DarkSeriesResult r2 = Run(unnamed);
        Assert.EndsWith(" Fisierele nu spun ce aparat le-a facut, asa ca nicio cifra de aici nu e data in electroni si lipsesc cifrele din sursa: cati electroni incap intr-un pixel (full well) si zgomotul de citire.", ScaleReport.Paragraphs(r2)[0], StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Contains(" The files do not say which camera took them, so no figure", ScaleReport.Paragraphs(r2)[0], StringComparison.Ordinal);
        }

        // the D3300 gets no such sentence
        using SessionFixture nikon = new SessionFixture().WithDarkSeries(4, Plain);
        Assert.EndsWith("pentru ca zgomotul face ca unii pixeli sa cada pe o treapta si altii pe urmatoarea.", ScaleReport.Paragraphs(Run(nikon))[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R081_TheScaleSectionIsInPlainWords(bool english)
    {
        // every variant of the rows and the paragraphs the fixtures reach, in both languages: lights and biases, a
        // bare series, the eyepiece band of R029 found with lights beside it (the ratio to the stack), a camera the
        // table does not know, and frames that name no camera
        static double Band(int x, int y) => y < 64 ? 2 : 0;
        static SyntheticLight Light(int i) => new() { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.0, Stars = SyntheticLight.ThreeStars };
        using SessionFixture full = new SessionFixture().WithDarkSeries(6, Plain, biases: 3).WithLights(4, Light);
        using SessionFixture bare = new SessionFixture().WithDarkSeries(4, Plain);
        using SessionFixture leak = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = Band, Noise = 1 }, biases: 4).WithLights(4, Light);
        using SessionFixture canon = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticDark.OnePerLevel, Camera = "Canon EOS 6D" });
        using SessionFixture unnamed = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticDark.OnePerLevel, Camera = null });
        System.Text.RegularExpressions.Regex method = english ? EnglishMethodWords() : RomanianMethodWords();
        using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
        {
            List<string> texts = [];
            foreach (SessionFixture s in new[] { full, bare, leak, canon, unnamed })
            {
                DarkSeriesResult r = Run(s);
                texts.AddRange(ScaleReport.Rows(r).SelectMany(row => new[] { row.Label, row.Value }));
                texts.AddRange(ScaleReport.Paragraphs(r));
            }

            // the master-noise row of frames whose noise could not be measured, which no fixture reaches
            DarkSeriesResult measured = Run(bare);
            DarkSeriesResult unmeasured = measured with { Darks = measured.Darks with { Noise = measured.Darks.Noise with { MasterNoise = double.NaN } } };
            texts.AddRange(ScaleReport.Rows(unmeasured).SelectMany(row => new[] { row.Label, row.Value }));
            texts.AddRange(ScaleReport.Paragraphs(unmeasured));

            // the scenes reached their variants, so every wording below was looked at
            string all = string.Join("\n", texts);
            Assert.Contains(english ? " times the noise of the lights stack (" : " din zgomotul stack-ului de lights (", all, StringComparison.Ordinal);
            Assert.Contains(english ? "Canon EOS 6D is not in the application's table" : "Aparatul Canon EOS 6D nu e in tabelul aplicatiei", all, StringComparison.Ordinal);
            Assert.Contains(english ? "The files do not say which camera took them" : "Fisierele nu spun ce aparat le-a facut", all, StringComparison.Ordinal);
            Assert.Contains(english ? "UNMEASURED: the frames' noise could not be measured" : "NEMASURAT: zgomotul cadrelor nu s-a putut masura", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the dark current (what a pixel gathers on average from the sensor's heat) is " : "curentul de intuneric (ce aduna in medie un pixel din caldura senzorului) e ", all, StringComparison.Ordinal);
            List<string> left = texts.Where(t => method.IsMatch(t)).Select(t => $"'{method.Match(t).Value}' in: {t}").ToList();
            Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R082_TheSummarysSessionHeaderAndChecks3To9AreInPlainWords(bool english)
    {
        // the variants the fixtures reach: lights and biases, a bare series, the eyepiece band of R029 with biases, a
        // series climbing to where it ends, one still climbing at its end, one whose master sits far under its end,
        // one whose first frame is cold, a flat one, a
        // single frame, a noisy frame among quiet ones, a row banding fixed in every frame, ten quiet frames
        static double Band(int x, int y) => y < 64 ? 2 : 0;
        static SyntheticLight Light(int i) => new() { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.0, Stars = SyntheticLight.ThreeStars };
        using SessionFixture full = new SessionFixture().WithDarkSeries(6, Plain, biases: 3).WithLights(4, Light);
        using SessionFixture bare = new SessionFixture().WithDarkSeries(4, Plain);
        using SessionFixture leak = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = Band, Noise = 1 }, biases: 4);
        using SessionFixture edge = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = (x, y) => x < 32 ? 3 : 0 });
        using SessionFixture settling = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 1.2 - 0.4 * Math.Exp(-i / 3.0) });
        using SessionFixture climbing = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 0.8 + 0.05 * i });
        using SessionFixture step = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i < 7 ? 1.0 : 1.2 });
        using SessionFixture cold = new SessionFixture().WithDarkSeries(7, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i == 0 ? 0.5 : 1.0 });
        using SessionFixture flat = new SessionFixture().WithDarkSeries(7, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot });
        using SessionFixture one = new SessionFixture().WithDarkSeries(1, Plain);
        using SessionFixture noisy = new SessionFixture().WithDarkSeries(6, i => i == 3 ? new SyntheticDark { Seed = 100 + i, Noise = 9 } : Plain(i));
        using SessionFixture rows = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Extra = (x, y) => y % 8 == 0 ? 5 : 0 });
        using SessionFixture quiet = new SessionFixture().WithDarkSeries(10, i => new SyntheticDark { Seed = 100 + i, Noise = 2 });
        System.Text.RegularExpressions.Regex method = english ? EnglishSummaryWords() : RomanianSummaryWords();
        using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
        {
            List<string> texts = [];
            foreach (SessionFixture s in new[] { full, bare, leak, edge, settling, climbing, step, cold, flat, one, noisy, rows, quiet })
            {
                DarkSeriesResult r = Run(s);
                texts.AddRange(InventoryReport.HeaderRows(r.Inventory).SelectMany(row => new[] { row.Label, row.Value }));
                texts.Add(DarkReport.HeaderLine(r));
                texts.AddRange(DarkReport.HeaderRows(r).SelectMany(row => new[] { row.Label, row.Value }));
                // checks 3 to 9, the parts approved so far (ADR 0030)
                foreach (ReportSection section in DarkReport.Sections(r).Where(x => x.Check is not ReportCheck.Lights and not ReportCheck.Index))
                {
                    texts.Add(section.Title);
                    texts.AddRange(section.Rows.SelectMany(row => new[] { row.Label, row.Value }));
                }
            }

            // the scenes reached their variants, so the words below were looked at
            string all = string.Join("\n", texts);
            Assert.Contains(english ? "of constant light against the centre, which does not come from the bias" : "lumina constanta fata de centru, care nu vine din bias", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the camera warmed during the series: the hot pixels rise from " : "aparatul s-a incalzit in timpul seriei: pixelii fierbinti cresc de la ", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the frames from the start, colder, pull it down; take a longer series" : "cadrele de la inceput, mai reci, il trag in jos; fa seria mai lunga", all, StringComparison.Ordinal);
            Assert.Contains(english ? "too far from the master for Siril to match it with -opt" : "prea departe de master ca Siril sa il potriveasca cu -opt", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the camera stayed at the same temperature: the hot pixels are at " : "aparatul a stat la aceeasi temperatura: pixelii fierbinti sunt la ", all, StringComparison.Ordinal);
            Assert.Contains(english ? "light that got in or a level difference of the readout" : "lumina intrata sau o diferenta de nivel din citire", all, StringComparison.Ordinal);
            Assert.Contains(english ? "a single frame has nothing to be compared with" : "un singur cadru nu are cu ce fi comparat", all, StringComparison.Ordinal);
            Assert.Contains(english ? "pairs of biases taken one after the other" : "perechi de bias-uri luate unul dupa altul", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the session has no biases" : "sesiunea nu are bias-uri", all, StringComparison.Ordinal);
            Assert.Contains(english ? "WARNING: 1 frame has noise more than 1.5 times the average noise" : "ATENTIE: 1 cadru are zgomotul de peste 1.5 ori zgomotul mediu", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the part that grows as the sensor warms" : "partea care creste cand senzorul se incalzeste", all, StringComparison.Ordinal);
            Assert.Contains(english ? "WARNING: fixed banding of " : "ATENTIE: benzi fixe de ", all, StringComparison.Ordinal);
            Assert.Contains(english ? "as it is the only one" : "fiind unul singur", all, StringComparison.Ordinal);
            Assert.Contains(english ? "enough shows only against the lights: open the darks together with the lights" : "ajung se vede doar fata de lights-uri: deschide dark-urile impreuna cu lights-urile", all, StringComparison.Ordinal);
            Assert.Contains(english ? "so that one odd frame does not spoil the master" : "ca un cadru ciudat sa nu strice masterul", all, StringComparison.Ordinal);
            List<string> left = texts.Where(t => method.IsMatch(t)).Select(t => $"'{method.Match(t).Value}' in: {t}").ToList();
            Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R083_TheFramesTablesWordsAreInPlainWords(bool english)
    {
        // the variants the fixtures reach: a series that climbs to its end with frames to discard below it, one that
        // comes down to it with frames above it, one with a frame whose hot pixels do not show, the eyepiece band of R029 with biases, an edge without biases, a short
        // series without an end, a capped one with skipped darks and lights; and the messages when no dark, bias,
        // light or partner dark can be read
        static double Band(int x, int y) => y < 64 ? 2 : 0;
        using SessionFixture climbing = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 1.2 - 0.4 * Math.Exp(-i / 3.0) });
        using SessionFixture cooling = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 0.9 + 0.4 * Math.Exp(-i / 3.0) });
        using SessionFixture blind = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i == 6 ? -0.5 : 1.2 - 0.4 * Math.Exp(-i / 3.0) });
        using SessionFixture leak = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = Band, Noise = 1 }, biases: 4);
        using SessionFixture edge = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = (x, y) => x < 32 ? 3 : 0 });
        using SessionFixture bare = new SessionFixture().WithDarkSeries(4, Plain);
        static SyntheticLight Light(int i) => new() { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.0, Stars = SyntheticLight.ThreeStars };
        using SessionFixture capped = new SessionFixture().WithDarkSeries(5, Plain).WithLights(5, Light);
        using TempWorkspace broken = new();
        File.WriteAllBytes(broken.PathTo("DSC_0001.fit"), new byte[100]);
        using SessionFixture badBiases = new SessionFixture().WithDarkSeries(4, Plain, biases: 2);
        using SessionFixture badLights = new SessionFixture().WithDarkSeries(4, Plain).WithLights(2, Light);
        using SessionFixture badPartner = new SessionFixture().WithDarkSeries(2, Plain, inSubfolder: false);
        foreach (string f in Directory.GetFiles(badBiases.BiasesDir).Concat(Directory.GetFiles(badLights.LightsDir)).Concat(Directory.GetFiles(badPartner.Root)))
        {
            File.WriteAllBytes(f, new byte[100]);
        }

        System.Text.RegularExpressions.Regex method = english ? PlainWords.EnglishTabWords() : PlainWords.RomanianTabWords();
        using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
        {
            List<string> texts = [DarkReport.EndColumnTip];
            foreach (DarkSeriesResult r in new[] { Run(climbing), Run(cooling), Run(blind), Run(leak), Run(edge), Run(bare), DarkSeriesRun.Run(capped.Root, new DarkOptions(Threads: 2, MaxFrames: 2)) })
            {
                texts.Add(DarkReport.FrameTableHeader(r));
                texts.Add(DarkReport.WarmingChartNote(r));
                texts.AddRange(DarkReport.FrameRows(r).Select(row => row.Note ?? row.Skipped ?? row.Error ?? ""));
            }

            texts.Add(Assert.ThrowsAny<Exception>(() => DarkSeriesRun.Run(broken.Dir, new DarkOptions(Threads: 2))).Message);
            texts.Add(Run(badBiases).BiasError ?? "");
            texts.Add(Run(badLights).LightsError ?? "");
            texts.Add(DarkSeriesRun.Run(climbing.Root, new DarkOptions(Threads: 2, Against: badPartner.Root)).PartnerError ?? "");

            // the scenes reached their variants, so the words below were looked at
            string all = string.Join("\n", texts);
            Assert.Contains(english ? "to discard: the hot pixels are at " : "de aruncat: pixelii fierbinti sunt la ", all, StringComparison.Ordinal);
            Assert.Contains(english ? ", that is more than 2.0 degrees above the end of the series" : ", adica la peste 2.0 grade peste sfarsitul seriei", all, StringComparison.Ordinal);
            Assert.Contains(english ? "The end of the series (the average of the last 5 frames) is at " : "Sfarsitul seriei (media ultimelor 5 cadre) e la ", all, StringComparison.Ordinal);
            Assert.Contains(english ? "; when it falls, the camera cools (check 4)." : "; cand coboara, se raceste (verificarea 4).", all, StringComparison.Ordinal);
            Assert.Contains(english ? "to discard: the hot pixels of the master do not show in this frame" : "de aruncat: pixelii fierbinti ai masterului nu se vad in acest cadru", all, StringComparison.Ordinal);
            Assert.Contains(english ? ", once the bias is taken off (glow included)" : ", dupa scaderea bias-ului (cu glow)", all, StringComparison.Ordinal);
            Assert.Contains(english ? " (no biases, glow included)" : " (fara bias-uri, cu glow)", all, StringComparison.Ordinal);
            Assert.Contains(english ? "against the centre (UNMEASURED)" : "fata de centru (NEMASURAT)", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the end of the series is not measured" : "sfarsitul seriei nu se masoara", all, StringComparison.Ordinal);
            Assert.Contains(english ? "frames only 2 are read, evenly spread)" : "cadre se citesc doar 2, raspandite egal)", all, StringComparison.Ordinal);
            Assert.Contains(english ? "no dark could be read" : "niciun dark nu s-a putut citi", all, StringComparison.Ordinal);
            Assert.Contains(english ? "no bias could be read" : "niciun bias nu s-a putut citi", all, StringComparison.Ordinal);
            Assert.Contains(english ? "no light could be read" : "niciun light nu s-a putut citi", all, StringComparison.Ordinal);
            Assert.Contains(english ? "none of the partner's darks could be read" : "niciun dark al partenerului nu s-a putut citi", all, StringComparison.Ordinal);
            List<string> left = texts.Where(t => method.IsMatch(t)).Select(t => $"'{method.Match(t).Value}' in: {t}").ToList();
            Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
        }
    }

    // R-082's words of the method in the summary, every section; the median only where check 9 names how a master is
    // made; LightsAndIndexTests reads check 2 and INDEX with the same words; the lists also hold obisnuit, de obicei
    // and usual, whose place medie and average took (ADRs 0035 and 0040)
    [System.Text.RegularExpressions.GeneratedRegex(@"\b(sigma|sqrt|obisnuit\w*|abater\w*|offset\w*|brut\w*|extrapol\w*|decod\w*|BITPIX|CFA|p1|p10|p90|p99|q25|q75|amplitudin\w*|normaliz\w*|cuantiz\w*|cuadratur\w*|gradient\w*|deriv\w*|rezidual\w*|temporal\w*|glisant\w*|candidat\w*|rms|rang\w*|persistent\w*|izolat\w*|raport\w*|supracorect\w*|scaleaz\w*)\b|(?<!\bcu )\bmedian\w*|x eroarea|zgomot\w* alb|varf la varf|\bde obicei\b|\bn=|±", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    internal static partial System.Text.RegularExpressions.Regex RomanianSummaryWords();

    [System.Text.RegularExpressions.GeneratedRegex(@"\b(sigma|sqrt|deviation\w*|offset\w*|raw ADU|extrapolat\w*|decod\w*|BITPIX|CFA|p1|p10|p90|p99|q25|q75|amplitude\w*|normali[sz]\w*|quanti[sz]\w*|quadrature|gradient\w*|drift\w*|residual\w*|temporal\w*|candidate\w*|rms|rank\w*|persistent\w*|isolated|ratio\w*|overcorrect\w*|scales)\b|(?<!\bwith the )\bmedian\w*|x the error|white noise|peak to peak|\busual\w*|\bn=|±", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    internal static partial System.Text.RegularExpressions.Regex EnglishSummaryWords();

    // R-081's words of the method, whole words in any case; the median only where it names how a stack is made; the
    // lists also hold obisnuit, de obicei and usual, whose place medie and average took (ADRs 0035 and 0040)
    [System.Text.RegularExpressions.GeneratedRegex(@"\b(sigma|sqrt|obisnuit\w*|de obicei|cuadratur\w*|radacin\w*|cuantiz\w*|normaliz\w*|abater\w*|offset\w*|castig\w*|brut\w*|petic\w*|supraestim\w*)\b|(?<![\w-])e-(?!\w)|(?<!\bcu )\bmedian\w*", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex RomanianMethodWords();

    [System.Text.RegularExpressions.GeneratedRegex(@"\b(sigma|sqrt|quadrature|square root|quanti[sz]\w*|normali[sz]\w*|deviation\w*|offset\w*|gain\w*|overestimat\w*|raw|patch\w*|usual\w*)\b|(?<![\w-])e-(?!\w)|(?<!\bwith the )\bmedian\w*", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex EnglishMethodWords();

    [Fact]
    public void R029_ASeriesThatDidNotWarmLeavesTheSplitUnmeasured()
    {
        // the same constant edge, the hot pixels at the same amplitude in every frame: the ratios span only their noise
        using SessionFixture s = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, Extra = (x, y) => x < 32 ? 3 : 0 });
        DarkSeriesResult r = Run(s);
        FrameLight fl = r.Verdicts.Leak.Frames!;
        Assert.True(fl.RatioSpan < 0.1, $"span {fl.RatioSpan}");
        Assert.Empty(fl.LitFrames);
        Assert.Empty(r.Verdicts.Leak.Splits);
        Assert.Null(r.Verdicts.Leak.Split);
        Assert.Equal(LeakCause.Unmeasured, r.Verdicts.Leak.Cause);
        Assert.Equal(Verdict.Warning, r.Verdicts.Leak.Verdict);
        using StringWriter sw = new();
        DarkReport.Write(r, sw);
        Assert.Contains("    glow / constant    : NEMASURAT: seria s-a incalzit prea putin ca sa se poata desparti glow-ul de lumina constanta (pixelii fierbinti variaza doar cu 0.0", sw.ToString(), StringComparison.Ordinal);
        Assert.Contains(", peste pragul de 1.0: scurgere de lumina sau glow", sw.ToString(), StringComparison.Ordinal);
        Assert.InRange(r.Verdicts.Pattern.GlowAdu, 2.5, 3.5);
    }

    /// <summary>
    /// CR-005: darks whose level sits above the hot threshold in every pixel left no quiet pixel in the
    /// crop, the noise curve read 0 on nothing, and check 9 said OK on a master noise of 0.00 ADU.
    /// </summary>
    [Theory]
    [InlineData(230)] // every pixel 30 ADU above the biases' black: all "hot" against a threshold of 20
    [InlineData(219)] // the level just under the threshold: the upper part of the crop's pixels was cut off
    public void R024_ASetWarmAboveTheHotThresholdStillMeasuresItsNoise(double pedestal)
    {
        // the same noise, frame by frame, on a level of 200 and on a higher one; the biases set the black at 200 for both
        static SyntheticDark D(int i, double level) => new() { Seed = 100 + i, Noise = 3, Pedestal = level, Hot = SyntheticDark.OnePerLevel };
        using SessionFixture plain = new SessionFixture().WithDarkSeries(12, i => D(i, 200), biases: 4);
        using SessionFixture warm = new SessionFixture().WithDarkSeries(12, i => D(i, pedestal), biases: 4);
        DarkSeriesResult rp = Run(plain), rw = Run(warm);
        Assert.Equal(200, rp.Darks.Black, 0);
        Assert.Equal(rp.Darks.Black, rw.Darks.Black, 9);
        NoiseVsN np = rp.Darks.Noise, nw = rw.Darks.Noise;

        // a constant level changes no difference between two frames: every point of the curve is the plain series'
        Assert.InRange(np.TemporalSigma, 2.7, 3.3);
        Assert.Equal(np.TemporalSigma, nw.TemporalSigma, 6);
        Assert.Equal(np.Curve.Select(p => p.N), nw.Curve.Select(p => p.N));
        Assert.All(np.Curve.Zip(nw.Curve), pair => Assert.Equal(pair.First.Measured, pair.Second.Measured, 6));
        Assert.Equal(np.MedianHalfVsMeanHalf, nw.MedianHalfVsMeanHalf, 6);
        Assert.InRange(nw.MasterNoise, 0.9, 1.3);
        Assert.Equal(np.MasterNoise, nw.MasterNoise, 6);

        // and check 9 judges the two alike, on a noise that was measured
        EnoughVerdict ep = rp.Verdicts.Enough, ew = rw.Verdicts.Enough;
        Assert.Equal(ep.Verdict, ew.Verdict);
        Assert.Equal(ep.FramesNeeded, ew.FramesNeeded);
        using StringWriter sw = new();
        DarkReport.Write(rw, sw);
        Assert.DoesNotContain("0.00 ADU de zgomot pe pixel", sw.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("zgomotul masterului (0.00 ADU)", sw.ToString(), StringComparison.Ordinal);
    }

    /// <summary>CR-005: the sigma of no value at all was 0, a measured silence, where the comment promised NaN.</summary>
    [Fact]
    public void R024_TheClippedSigmaOfNoValueIsUnmeasuredNotZero()
    {
        (double sigma, double median) = Planes.ClippedSigma([]);
        Assert.True(double.IsNaN(sigma), $"sigma {sigma}");
        Assert.True(double.IsNaN(median), $"median {median}");
        (double one, double level) = Planes.ClippedSigma([7f, 7f, 7f]);
        Assert.Equal(0, one, 9);
        Assert.Equal(7, level, 9);
    }

    [Fact]
    public void R024_TheNoiseOfTheMeanFollowsSqrtNAndTenCleanFramesAreEnough()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(10, i => new SyntheticDark { Seed = 100 + i, Noise = 2 });
        DarkSeriesResult r = Run(s);
        NoiseVsN nv = r.Darks.Noise;
        Assert.Equal([1, 2, 4, 5], nv.Curve.Select(p => p.N));
        foreach (NoisePoint p in nv.Curve)
        {
            Assert.InRange(p.Measured / p.Expected, 0.8, 1.2);
        }

        Assert.InRange(nv.SigmaFrame, 1.8, 2.2);
        Assert.InRange(nv.TemporalSigma, 1.8, 2.2);
        // the median master of ten frames of noise 2: 1.25 x 2 / sqrt(10) = 0.79
        Assert.InRange(nv.MasterNoise, 0.70, 0.90);
        Assert.Equal(192, nv.CropPx);
        Assert.Equal(Verdict.Ok, r.Verdicts.Enough.Verdict);
        Assert.Equal(1, r.Verdicts.Enough.QuantStep);
        Assert.False(r.Verdicts.Enough.TooFew);

        // three noisy frames: too few for a median; without lights the frames needed are the minimum (ADR 0056)
        using SessionFixture few = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i, Noise = 6 });
        EnoughVerdict e = Run(few).Verdicts.Enough;
        Assert.Equal(Verdict.Warning, e.Verdict);
        Assert.True(e.TooFew);
        Assert.Equal(Th.MinDarks, e.FramesNeeded);

        // a single frame is no series: unmeasured, never OK; the residual against its own median is zero by construction, so check 5 is unmeasured too
        using SessionFixture one = new SessionFixture().WithDarkSeries(1, i => new SyntheticDark { Seed = 100 + i, Noise = 1, Hot = SyntheticDark.OnePerLevel });
        DarkSeriesResult single = Run(one);
        Assert.Equal(Verdict.Unmeasured, single.Verdicts.Enough.Verdict);
        Assert.Equal(Verdict.Unmeasured, single.Verdicts.Noise.Verdict);
        Assert.Equal(Verdict.Unmeasured, single.Verdicts.Drift.Verdict);
        using StringWriter sw = new();
        DarkReport.Write(single, sw);
        Assert.Contains("=== DARK-URI: cadre citite 1 din 1", sw.ToString(), StringComparison.Ordinal);
        Assert.Contains("fata de master     : NEMASURAT: un singur cadru nu are cu ce fi comparat", sw.ToString(), StringComparison.Ordinal);
        Assert.Contains("stabilitate        : NEMASURAT: stabilitatea cere cel putin 2 cadre", sw.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("0.00 .. 0.00", sw.ToString(), StringComparison.Ordinal);

        // ten frames whose master sits over the file's value step: without lights the step decides nothing (ADR 0056)
        using SessionFixture noisy = new SessionFixture().WithDarkSeries(10, i => new SyntheticDark { Seed = 100 + i, Noise = 4 });
        EnoughVerdict en = Run(noisy).Verdicts.Enough;
        Assert.Equal(Verdict.Ok, en.Verdict);
        Assert.False(en.TooFew);
        Assert.Equal(Th.MinDarks, en.FramesNeeded);
    }

    /// <summary>
    /// CR-018: the noise from one frame to the next, and with it the master's noise and the frames needed, rested
    /// on the first two decoded frames alone; it is the median over every pair of consecutive frames.
    /// </summary>
    [Fact]
    public void R024_OneNoisyFirstFrameDoesNotSetTheMastersNoise()
    {
        // twelve darks of noise 3, the first of noise 9: the first pair reads sqrt((81 + 9) / 2) = 6.7
        using SessionFixture s = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Noise = i == 0 ? 9 : 3 });
        NoiseVsN nv = Run(s).Darks.Noise;
        Assert.InRange(nv.TemporalSigma, 2.8, 3.2);
        // the median master of twelve frames of noise 3: 1.25 x 3 / sqrt(12) = 1.09 (2.42 from the first pair)
        Assert.InRange(nv.MasterNoise, 1.0, 1.17);
        // the measured curve is not touched: its first point is still the first frame against the second
        Assert.Equal(1, nv.Curve[0].N);
        Assert.InRange(nv.Curve[0].Measured, 6.2, 7.2);

        // the same with the noisy frame last, and in the middle, where it sits in two pairs of eleven
        foreach (int odd in new[] { 11, 5 })
        {
            using SessionFixture t = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Noise = i == odd ? 9 : 3 });
            Assert.InRange(Run(t).Darks.Noise.TemporalSigma, 2.8, 3.2);
        }

        // two frames have one pair: the figure is that pair's, as before
        using SessionFixture two = new SessionFixture().WithDarkSeries(2, i => new SyntheticDark { Seed = 100 + i, Noise = 3 });
        NoiseVsN pair = Run(two).Darks.Noise;
        Assert.Equal(pair.Curve[0].Measured, pair.TemporalSigma, 12);

        // one frame has none: no figure from one frame to the next
        using SessionFixture one = new SessionFixture().WithDarkSeries(1, i => new SyntheticDark { Seed = 100 + i, Noise = 3 });
        Assert.True(double.IsNaN(Run(one).Darks.Noise.TemporalSigma));
    }

    /// <summary>
    /// CR-007: without lights check 9 asked for a master noise under one value step, so on a camera whose values
    /// go 1 by 1 twelve darks of noise 9 read WARNING with 128 needed; the same darks with their lights are judged
    /// on the stack. Without lights only the minimum of ten is judged, and the words say where the answer is.
    /// </summary>
    [Fact]
    public void R024_WithoutLightsOnlyTheMinimumOfTenDarksIsJudged()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Noise = 9 });
        DarkSeriesResult r = Run(s);
        EnoughVerdict e = r.Verdicts.Enough;
        Assert.Equal(1, e.QuantStep);
        Assert.InRange(e.MasterNoise, 2.6, 3.9);
        Assert.Equal(Verdict.Ok, e.Verdict);
        Assert.Equal(Th.MinDarks, e.FramesNeeded);
        IReadOnlyList<ReportRow> rows = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Enough).Rows;
        Assert.Equal("cel putin 10 cadre, ca un cadru ciudat sa nu strice masterul; cate ajung se vede doar fata de lights-uri", rows[3].Value);
        Assert.Equal($"OK: 12 cadre, peste minimul de 10; zgomotul masterului ({RunReport.F(e.MasterNoise, 2)} ADU) se adauga la fiecare light calibrat, iar daca 12 ajung se vede doar fata de lights-uri: deschide dark-urile impreuna cu lights-urile (--lights)", rows[4].Value);
        // the table says too few under ten and nothing from ten on: without lights it cannot be told
        ReportTable t = Assert.IsType<ReportTable>(rows[2].Table);
        Assert.Equal(["5", "10", "12 (acum)", "15", "20"], t.Rows.Select(row => row[0]));
        Assert.Equal(["prea putine", "", "", "", ""], t.Rows.Select(row => row[^1]));
        Assert.Contains($"; 12 cadre (acum): {RunReport.F(e.MasterNoise, 2)} ADU; 15 cadre: ", rows[2].Value, StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            IReadOnlyList<ReportRow> en = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Enough).Rows;
            Assert.Equal("at least 10 frames, so that one odd frame does not spoil the master; how many are enough shows only against the lights", en[3].Value);
            Assert.Equal($"OK: 12 frames, over the minimum of 10; the master's noise ({RunReport.F(e.MasterNoise, 2)} ADU) adds to every calibrated light, and whether 12 are enough shows only against the lights: open the darks together with the lights (--lights)", en[4].Value);
        }

        // under ten the warning stays, with the minimum as the frames needed
        using SessionFixture few = new SessionFixture().WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Noise = 9 });
        EnoughVerdict ef = Run(few).Verdicts.Enough;
        Assert.Equal(Verdict.Warning, ef.Verdict);
        Assert.True(ef.TooFew);
        Assert.Equal(Th.MinDarks, ef.FramesNeeded);
    }

    /// <summary>The hot levels are in ADU equivalent but never under 5 raw ADU; the dead limit is raw; the biases keep raw levels.</summary>
    [Fact]
    public void R020_LevelsFloorAtFiveAduAndTheDeadLimitIsRawWhateverTheNormalisation()
    {
        // ISO 100 and 30 s: the factor is 1/16, so 20 ADU-eq would be 1.25 ADU; the floor holds it at 5.
        // Noise 1, so the 5 ADU floor sits 10 sigma over the master's noise and only the five hot pixels pass it.
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Noise = 1, Hot = SyntheticDark.OnePerLevel, Extra = (x, y) => (x, y) == (5, 5) ? -200 : 0 }, biases: 3, exposure: 30, iso: 100);
        DarkSeriesResult r = Run(s);
        DarkSeriesAnalysis d = r.Darks;
        Assert.Equal(1.0 / 16, d.Norm, 1e-9);
        Assert.Equal(5, d.Hot.ThresholdAdu, 1e-9);
        // raw levels max(l / 16, 5): 5, 5, 5, 6.25, 31.25 -> the pixels at 15, 30, 60, 150, 600 count 5, 5, 5, 5, 3
        Assert.Equal([5, 5, 5, 5, 3], Enumerable.Range(0, 5).Select(k => d.Master.Sum(m => m.Above[k])));
        Assert.Equal(1, r.Verdicts.DeadSat.Dead);
        // a bias at its own exposure is not normalised: no dead pixel, no hot pixel in a flat bias
        foreach (BiasFrameFigures b in r.Biases!.Frames)
        {
            Assert.All(b.Planes, p => Assert.Equal(0, p.Low));
            Assert.All(b.Planes, p => Assert.Equal(0, p.Above[0]));
        }
    }

    [Fact]
    public void R027_SkippedFramesAreNotErrorsAndTheRunExitsClean()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(5, Plain);
        DarkSeriesResult r = DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2, MaxFrames: 2));
        Assert.Equal(2, r.Darks.Used);
        Assert.False(r.HasErrors);
        Assert.True(r.Darks.Frames[1].Skipped);
        Assert.Null(r.Darks.Frames[0].Error);
    }

    /// <summary>
    /// CR-012: check 1 named the setting most frames have while the stack kept the first file's, so one test frame
    /// of 1 s listed before a series of 120 s left the whole series out under a match that read OK.
    /// </summary>
    [Fact]
    public void R020_AStrayFirstFrameDoesNotSetTheGroupsSettings()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, Plain);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0000.fit"), new SyntheticDark { Seed = 9 }.Build(SessionFixture.FirstStart.AddSeconds(-60), 1, 400));
        DarkSeriesResult r = Run(s);
        Assert.Equal(6, r.Darks.Used);
        Assert.Equal("alte setari decat majoritatea cadrelor: ISO 400, 1 s", r.Darks.Frames[0].Error);
        Assert.All(r.Darks.Frames.Skip(1), f => Assert.Null(f.Error));
        Assert.True(r.HasErrors);
        // check 1 and the analysis read the same setting
        Assert.Equal(120, r.Inventory.Match.Darks!.ExposureSeconds);
        Assert.Equal(120, r.Darks.FirstStacked!.ExposureSeconds);
        Assert.Equal(1.0, r.Darks.Norm, 1e-9);
        Assert.Equal([5, 4, 3, 2, 1], Enumerable.Range(0, 5).Select(k => r.Darks.Master.Sum(m => m.Above[k])).ToArray());
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("other settings than most frames: ISO 400, 1 s", Run(s).Darks.Frames[0].Error);
        }

        // a stray first frame of another size as well: it is named for its settings and sets no size
        using SessionFixture crop = new SessionFixture().WithDarkSeries(6, Plain);
        File.WriteAllBytes(Path.Combine(crop.DarksDir, "DSC_0000.fit"), new SyntheticDark { Seed = 9, Width = 128, Height = 96 }.Build(SessionFixture.FirstStart.AddSeconds(-60), 1, 800));
        DarkSeriesResult rc = Run(crop);
        Assert.Equal(6, rc.Darks.Used);
        Assert.Equal(256, rc.Darks.Width);
        Assert.Equal("alte setari decat majoritatea cadrelor: ISO 800, 1 s", rc.Darks.Frames[0].Error);

        // the same for the biases' stack: one bias at another ISO listed first does not take the others out
        using SessionFixture b = new SessionFixture().WithDarkSeries(4, Plain, biases: 4);
        File.WriteAllBytes(Path.Combine(b.BiasesDir, "bias_0000.fit"), new SyntheticDark { Seed = 499, Noise = 2 }.Build(SessionFixture.FirstStart.AddSeconds(-3700), 0.0002, 1600));
        DarkSeriesResult rb = Run(b);
        Assert.Null(rb.BiasError);
        Assert.Equal(4, rb.Biases!.Used);
        Assert.Equal("alte setari decat majoritatea cadrelor: ISO 1600, 0 s", rb.Biases.Frames[0].Error);

        // a tie goes to the setting met first, as in check 1: three of 60 s, then three of 120 s
        using SessionFixture tie = new SessionFixture().WithDarkSeries(3, Plain, exposure: 60);
        for (int i = 0; i < 3; i++)
        {
            File.WriteAllBytes(Path.Combine(tie.DarksDir, $"DSC_{i + 4:0000}.fit"), Plain(i + 3).Build(SessionFixture.FirstStart.AddSeconds(360 + i * 120), 120, 400));
        }

        DarkSeriesResult rt = Run(tie);
        Assert.Equal(60, rt.Inventory.Match.Darks!.ExposureSeconds);
        Assert.Equal([1, 2, 3], rt.Darks.Frames.Where(f => f.Error is null).Select(f => f.Info.Index));
    }

    /// <summary>CR-013: the partner's darks stand in for the session's and take check 4, so their names against their times are said too.</summary>
    [Fact]
    public void R126_ThePartnersDarksOfALightsOnlySessionGetTheLineToo()
    {
        // the partner's darks take checks 3 to 9 of a session without darks (R-054), check 4 with them
        using SessionFixture partner = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice }, inSubfolder: false);
        File.WriteAllBytes(Path.Combine(partner.Root, "DSC_9999.fit"), new SyntheticDark { Seed = 199, Hot = SyntheticLight.Lattice }.Build(SessionFixture.FirstStart.AddSeconds(-120), 120, 400));
        using SessionFixture s = new SessionFixture().WithLights(3, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice });
        DarkSeriesResult r = DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2, Against: partner.Root));
        ReportRow row = Assert.Single(InventoryReport.HeaderRows(r.Inventory), x => x.Label == "ATENTIE");
        Assert.StartsWith("numele fisierelor din dark-urile partenerului nu sunt in ordinea in care au fost facute cadrele: DSC_0004.fit e trecut inaintea lui DSC_9999.fit, dar a fost facut dupa el. Pauzele,", row.Value, StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.StartsWith("the partner's darks' file names are not in the order the frames were taken: DSC_0004.fit is listed before DSC_9999.fit and was taken after it. The pauses,", InventoryReport.HeaderRows(r.Inventory).Single(x => x.Label == "WARNING").Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void R027_AFrameWithOtherSettingsIsLeftOutWithItsError()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, Plain);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0005.fit"), new SyntheticDark { Seed = 9 }.Build(SessionFixture.FirstStart.AddSeconds(480), 60, 400));
        // ISO 800 at 60 s has the ISO x exposure product of the other four: each setting is compared on its own
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0006.fit"), new SyntheticDark { Seed = 10 }.Build(SessionFixture.FirstStart.AddSeconds(600), 60, 800));
        DarkSeriesResult r = Run(s);
        Assert.Equal(4, r.Darks.Used);
        Assert.Contains("alte setari decat majoritatea cadrelor: ISO 400, 60 s", r.Darks.Frames[4].Error, StringComparison.Ordinal);
        Assert.Contains("alte setari decat majoritatea cadrelor: ISO 800, 60 s", r.Darks.Frames[5].Error, StringComparison.Ordinal);
    }

    [Fact]
    public void R027_SkippedBiasesAreNotErrorsEither()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(3, Plain, biases: 5);
        DarkSeriesResult r = DarkSeriesRun.Run(s.Root, new DarkOptions(Threads: 2, MaxFrames: 2));
        Assert.False(r.HasErrors);
        Assert.Equal(3, r.Biases!.Frames.Count(f => f.Skipped));
        Assert.Equal(2, r.Biases.Used);
        using StringWriter sw = new();
        DarkReport.Write(r, sw);
        Assert.Contains("bias-uri sarite    : 3 din 5 (peste numarul maxim de cadre; s-au folosit 2)", sw.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ATENTIE            : ", sw.ToString().Split("=== DARK-URI")[1], StringComparison.Ordinal);
    }

    [Fact]
    public void R041_ABiasThatCannotBeDecodedIsCountedInTheHeaderAndMakesTheRunExit1()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(3, Plain, biases: 3);
        File.WriteAllBytes(Path.Combine(s.BiasesDir, "bias_0004.fit"), new byte[100]);
        DarkSeriesResult r = Run(s);
        Assert.True(r.HasErrors);
        Assert.Equal(3, r.Biases!.Used);
        using StringWriter sw = new();
        DarkReport.Write(r, sw);
        Assert.Contains("ATENTIE            : 1 bias nu s-a putut citi (primul: bias_0004.fit: ", sw.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void R020_AFloatFitsIsBroughtToSixteenBitsOrKeptInAdu()
    {
        using TempWorkspace ws = new();
        string[] cards = [.. SessionFixture.Cards(SessionFixture.FirstStart, 120, 400), .. FitsTestData.BayerCards("RGGB")];
        Random rng = new(3);
        File.WriteAllBytes(ws.PathTo("a.fit"), FitsTestData.BuildFits(64, 48, (x, y) => 0.3 + 0.001 * rng.NextDouble(), bitpix: -32, extraCards: cards));
        File.WriteAllBytes(ws.PathTo("b.fit"), FitsTestData.BuildFits(64, 48, (x, y) => 0.3 + 0.001 * rng.NextDouble(), bitpix: -32, extraCards: cards));
        FrameStack unit = FrameStack.Load(SessionFolder.Open(ws.Dir).Darks, 1, 60);
        Assert.Equal(65535, unit.White);
        Assert.InRange(unit.Black, 0.3 * 65535, 0.301 * 65535);
        Assert.Contains("adus la 0 .. 65535", unit.ScaleSource, StringComparison.Ordinal);

        using TempWorkspace ws2 = new();
        File.WriteAllBytes(ws2.PathTo("a.fit"), FitsTestData.BuildFits(64, 48, (x, y) => 200 + rng.NextDouble(), bitpix: -32, extraCards: cards));
        FrameStack adu = FrameStack.Load(SessionFolder.Open(ws2.Dir).Darks, 1, 60);
        Assert.Equal(65535, adu.White);
        Assert.InRange(adu.Black, 199.5, 201.5);
        Assert.Contains("valori in ADU, albul presupus 65535", adu.ScaleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void R021_FramesSmallerThanOneBlockLeaveTheMapsUnmeasured()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i, Width = 24, Height = 16 });
        DarkSeriesResult r = Run(s);
        Assert.Equal(Verdict.Unmeasured, r.Verdicts.Leak.Verdict);
        Assert.Equal(Verdict.Unmeasured, r.Verdicts.Pattern.Verdict);
        Assert.Equal(3, r.Darks.Used);
    }

    [Fact]
    public void R041_BiasesOfAnotherSizeAreReportedAndLeftOut()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(3, Plain, biases: 2, bias: i => new SyntheticDark { Seed = 500 + i, Width = 128, Height = 96 });
        DarkSeriesResult r = Run(s);
        Assert.Null(r.Biases);
        Assert.Null(r.DarkCurrent);
        Assert.Contains("bias-urile au 128 x 96 px", r.BiasError, StringComparison.Ordinal);
        Assert.True(r.HasErrors);
        Assert.False(r.Verdicts.Leak.FromDarkMinusBias);
    }

    /// <summary>
    /// CR-011: FITS darks took their black from the biases before the biases were refused for their size, so darks at
    /// pedestal 200 beside biases of another crop at pedestal 1000 were measured against black 1000: no hot pixel,
    /// every pixel "dead", under a header that said the black came from the biases.
    /// </summary>
    [Fact]
    public void R041_FitsDarksDoNotTakeTheirBlackFromBiasesOfAnotherSize()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, Plain, biases: 3, bias: i => new SyntheticDark { Seed = 500 + i, Noise = 2, Width = 128, Height = 96, Pedestal = 1000 });
        DarkSeriesResult r = Run(s);
        Assert.Null(r.Biases);
        Assert.Contains("bias-urile au 128 x 96 px", r.BiasError, StringComparison.Ordinal);
        DarkSeriesAnalysis d = r.Darks;
        Assert.Equal(200, d.Black, 1.0);
        Assert.Contains("negrul luat din valoarea medie a cadrului 1", d.ScaleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("negrul luat din bias-uri", d.ScaleSource, StringComparison.Ordinal);
        // the five hot pixels are counted and no pixel is under the black, as without biases
        Assert.Equal([5, 4, 3, 2, 1], Enumerable.Range(0, 5).Select(k => d.Master.Sum(m => m.Above[k])).ToArray());
        Assert.All(d.Frames, f => Assert.All(f.Planes, p => Assert.Equal(0, p.Low)));

        // biases of the darks' size still give the black: theirs, not the darks' own
        using SessionFixture same = new SessionFixture().WithDarkSeries(4, Plain, biases: 3);
        Assert.Contains("negrul luat din bias-uri", Run(same).Darks.ScaleSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// CR-010: nothing compared the biases' ISO with the darks', so biases at ISO 400 gave the read noise, the
    /// bias map and, for FITS darks, the black of darks at ISO 1600. They are left out, as biases of another size.
    /// </summary>
    [Fact]
    public void R041_BiasesAtAnotherIsoThanTheDarksAreReportedAndLeftOut()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, Plain, iso: 1600).WithBiases(3);
        DarkSeriesResult r = Run(s);
        Assert.Null(r.Biases);
        Assert.Null(r.DarkCurrent);
        Assert.Equal("bias-urile sunt la ISO 400, dark-urile la ISO 1600", r.BiasError);
        Assert.True(r.HasErrors);
        Assert.False(r.Verdicts.Leak.FromDarkMinusBias);
        Assert.Null(r.Verdicts.Noise.ReadNoise);
        using StringWriter sw = new();
        DarkReport.Write(r, sw);
        Assert.Contains("    ATENTIE            : bias-urile nu s-au putut folosi: bias-urile sunt la ISO 400, dark-urile la ISO 1600", sw.ToString(), StringComparison.Ordinal);
        // the darks' black is their own, not the one of the biases left out
        Assert.Contains("negrul luat din valoarea medie a cadrului 1", sw.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("negrul luat din bias-uri", sw.ToString(), StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("the biases are at ISO 400, the darks at ISO 1600", Run(s).BiasError);
        }

        // biases that do not give their ISO cannot be told apart and are used, as before
        using SessionFixture silent = new SessionFixture().WithDarkSeries(4, Plain, iso: 1600);
        Directory.CreateDirectory(silent.BiasesDir);
        for (int i = 0; i < 3; i++)
        {
            File.WriteAllBytes(Path.Combine(silent.BiasesDir, $"bias_{i + 1:0000}.fit"), new SyntheticDark { Seed = 500 + i, Noise = 2 }.Build(SessionFixture.FirstStart.AddSeconds(-3600 + i), 0.0002, null));
        }

        DarkSeriesResult rs = Run(silent);
        Assert.Null(rs.BiasError);
        Assert.Equal(3, rs.Biases!.Used);

        // and biases at the darks' ISO, as ever
        using SessionFixture same = new SessionFixture().WithDarkSeries(4, Plain, biases: 3, iso: 1600);
        Assert.Null(Run(same).BiasError);
    }

    /// <summary>
    /// CR-008: above the cap one file in k was read, k = ceil(count / cap), so 61 darks under the cap of 60 gave
    /// 31 and five under a cap of 2 gave files 1 and 4. Exactly the cap is read, from the first file to the last
    /// at even distances (ADR 0057).
    /// </summary>
    [Fact]
    public void R027_AboveTheCapExactlyTheCapIsDecodedEvenlySpreadFromTheFirstToTheLast()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(5, Plain);
        FrameStack stack = FrameStack.Load(SessionFolder.Open(s.Root).Darks, 2, maxFrames: 2);
        Assert.Equal(2, stack.Decoded.Count);
        Assert.Equal([1, 5], stack.Decoded.Select(f => f.Info.Index));
        Assert.Equal("sarit (peste 2 cadre se citesc doar 2, raspandite egal)", stack.Frames[1].Error);
        Assert.All(stack.Frames.Where(f => f.Pixels is null), f => Assert.True(f.Skipped));
        Assert.Equal(5, stack.Frames.Count);

        // a cap of 1 reads the first file, and says so in the singular
        FrameStack one = FrameStack.Load(SessionFolder.Open(s.Root).Darks, 2, maxFrames: 1);
        Assert.Equal([1], one.Decoded.Select(f => f.Info.Index));
        Assert.Equal("sarit (peste 1 cadru se citeste doar primul)", one.Frames[1].Error);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("skipped (over 2 frames only 2 are read, evenly spread)", FrameStack.Load(SessionFolder.Open(s.Root).Darks, 2, maxFrames: 2).Frames[1].Error);
            Assert.Equal("skipped (over 1 frame only the first is read)", FrameStack.Load(SessionFolder.Open(s.Root).Darks, 2, maxFrames: 1).Frames[1].Error);
        }

        // seven files under a cap of four: every second one, the last included
        using SessionFixture seven = new SessionFixture().WithDarkSeries(7, Plain);
        Assert.Equal([1, 3, 5, 7], FrameStack.Load(SessionFolder.Open(seven.Root).Darks, 2, maxFrames: 4).Decoded.Select(f => f.Info.Index));

        // one file over the default cap: sixty are read, not thirty-one, and the one left out is in the middle
        using SessionFixture many = new SessionFixture().WithDarkSeries(61, i => new SyntheticDark { Seed = 100 + i, Width = 24, Height = 16 });
        FrameStack sixty = FrameStack.Load(SessionFolder.Open(many.Root).Darks, 2, Th.MaxFramesInMemory);
        Assert.Equal(60, sixty.Decoded.Count);
        Assert.Equal(1, sixty.Decoded[0].Info.Index);
        Assert.Equal(61, sixty.Decoded[^1].Info.Index);
        Assert.Equal(31, Assert.Single(sixty.Frames, f => f.Skipped).Info.Index);
        Assert.Equal("sarit (peste 60 de cadre se citesc doar 60, raspandite egal)", sixty.Frames[30].Error);
    }

    /// <summary>A corrupt first file does not hand the geometry to whichever thread finishes first: the next file in order sets it.</summary>
    [Fact]
    public void R027_ACorruptFirstFileLeavesTheGeometryToTheNextInOrder()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, Plain);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0000.fit"), new byte[100]);
        FrameStack stack = FrameStack.Load(SessionFolder.Open(s.Root).Darks, 2, maxFrames: 60);
        Assert.Equal(256, stack.Width);
        Assert.Equal(4, stack.Decoded.Count);
        Assert.Contains("FITS", stack.Frames[0].Error, StringComparison.Ordinal);
    }

    [Fact]
    public void R027_AFrameOfAnotherSizeIsLeftOutWithItsError()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, i => i == 2 ? new SyntheticDark { Seed = 1, Width = 128, Height = 96 } : Plain(i));
        DarkSeriesResult r = Run(s);
        Assert.Equal(3, r.Darks.Used);
        Assert.Contains("dimensiune diferita: 128 x 96 fata de 256 x 192", r.Darks.Frames[2].Error, StringComparison.Ordinal);
        Assert.True(r.HasErrors);
    }

    /// <summary>
    /// CR-016: a frame was checked against the first frame's size and settings, never against its black and
    /// white, so files of another bit depth were measured on the first frame's scale.
    /// </summary>
    [Fact]
    public void R020_AFrameWithAnotherWhiteIsLeftOutWithItsError()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, Plain);
        // an 8-bit file of the series' size and settings: its white is 255, the others' 65535
        string[] cards = [.. SessionFixture.Cards(SessionFixture.FirstStart.AddSeconds(480), 120, 400), .. FitsTestData.BayerCards("RGGB")];
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0005.fit"), FitsTestData.BuildFits(256, 192, (x, y) => 100 + ((x + y) % 3), bitpix: 8, extraCards: cards));
        DarkSeriesResult r = Run(s);
        Assert.Equal(4, r.Darks.Used);
        Assert.Equal("alb diferit: 255 fata de 65535", r.Darks.Frames[4].Error);
        Assert.All(r.Darks.Frames.Take(4), f => Assert.Null(f.Error));
        Assert.True(r.HasErrors);
        Assert.Equal(65535, r.Darks.White);
        // the four darks read what they read alone
        Assert.Equal([5, 4, 3, 2, 1], Enumerable.Range(0, 5).Select(k => r.Darks.Master.Sum(m => m.Above[k])).ToArray());
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("different white: 255 instead of 65535", Run(s).Darks.Frames[4].Error);
        }
    }

    /// <summary>CR-016: the rule behind the row, with a raw's black, which no test file carries.</summary>
    [Fact]
    public void R020_ABlackOrWhiteWithinOnePercentOfTheWhiteIsTheSameScale()
    {
        // 12-bit files among 14-bit ones
        Assert.False(FrameStack.SameScale(150, 4095, 600, 16383));
        // the same white, a black 450 ADU off (over 1 % of 16383, which is 164)
        Assert.False(FrameStack.SameScale(150, 16383, 600, 16383));
        // a black the camera measures per frame moves by a few ADU: the same scale
        Assert.True(FrameStack.SameScale(2046, 15000, 2049, 15000));
        Assert.True(FrameStack.SameScale(600, 16383, 600, 16383));
        // exactly at the limit is still the same; just over it is not
        Assert.True(FrameStack.SameScale(100, 10000, 200, 10000));
        Assert.False(FrameStack.SameScale(100, 10000, 200.5, 10000));
        Assert.False(FrameStack.SameScale(0, 10100.5, 0, 10000));
        // a FITS carries no black: only the white is compared
        Assert.True(FrameStack.SameScale(double.NaN, 65535, double.NaN, 65535));
        Assert.False(FrameStack.SameScale(double.NaN, 255, double.NaN, 65535));
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Equal("different black and white: 150 and 4095 instead of 600 and 16383", FrameStack.ScaleError(150, 4095, 600, 16383));
        }

        Assert.Equal("negru si alb diferite: 150 si 4095 fata de 600 si 16383", FrameStack.ScaleError(150, 4095, 600, 16383));
    }

    /// <summary>
    /// CR-016: a float frame was read as "0 .. 1" only when its largest value was at most 1, so one value just
    /// over 1 made the whole frame read as ADU and round to zeros.
    /// </summary>
    [Fact]
    public void R020_AFloatFrameWithOneValueOverOneIsStillReadOnZeroToOne()
    {
        using TempWorkspace ws = new();
        List<string> paths = [];
        for (int i = 0; i < 3; i++)
        {
            int k = i;
            string[] cards = [.. SessionFixture.Cards(SessionFixture.FirstStart.AddSeconds(i * 120), 120, 400), .. FitsTestData.BayerCards("RGGB")];
            // 200 ADU of 65535 with a ripple; the second frame has one pixel just over 1
            double Pixel(int x, int y) => k == 1 && x == 7 && y == 5 ? 1.0001 : (200 + ((x + (2 * y) + k) % 5)) / 65535.0;
            string path = Path.Combine(ws.Dir, $"DSC_{i + 1:0000}.fit");
            File.WriteAllBytes(path, FitsTestData.BuildFits(64, 48, Pixel, bitpix: -32, extraCards: cards));
            paths.Add(path);
        }

        FrameStack stack = FrameStack.Load(paths, 2, maxFrames: 60);
        Assert.Equal(3, stack.Decoded.Count);
        Assert.Equal(65535, stack.White);
        foreach (StackFrame f in stack.Frames)
        {
            // every frame on 0 .. 65535: a frame taken as ADU would hold zeros and one 1
            Assert.InRange(Planes.Median([.. f.Pixels!.Select(v => (float)v)]), 200, 204);
        }

        // the value over 1 clips at the white
        Assert.Equal(65535, stack.Frames[1].Pixels![(5 * 64) + 7]);

        // the limit itself: of 3072 values 0.1 % is 3.07, so three over 1 are still a frame on 0 .. 1 and four
        // make it a frame in ADU, whose 200 / 65535 round to zero
        foreach ((int over, int median) in new[] { (3, 200), (4, 0) })
        {
            string limit = Path.Combine(ws.Dir, $"limit_{over}.fit");
            File.WriteAllBytes(limit, FitsTestData.BuildFits(64, 48, (x, y) => y == 0 && x < over ? 1.0001 : 200 / 65535.0, bitpix: -32, extraCards: FitsTestData.BayerCards("RGGB")));
            FrameStack one = FrameStack.Load([limit], 1, maxFrames: 60);
            Assert.Equal(median, Planes.Median([.. one.Frames[0].Pixels!.Select(v => (float)v)]));
        }

        // a float frame in ADU is still kept as it is: nearly every value is over 1
        string adu = Path.Combine(ws.Dir, "adu.fit");
        File.WriteAllBytes(adu, FitsTestData.BuildFits(64, 48, (x, y) => 200 + ((x + y) % 5), bitpix: -32, extraCards: FitsTestData.BayerCards("RGGB")));
        FrameStack kept = FrameStack.Load([adu], 1, maxFrames: 60);
        Assert.InRange(Planes.Median([.. kept.Frames[0].Pixels!.Select(v => (float)v)]), 200, 204);
    }

    [Fact]
    public void R031_AHotPixelPresentInHalfTheFramesFlickers()
    {
        // one pixel hot in every frame, one hot in three of six: the median keeps the second at 30 ADU, so it is hot and flickers
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = i < 3 ? [(10, 10, 60), (20, 20, 60)] : [(10, 10, 60)] });
        HotStability h = Run(s).Darks.Hot;
        Assert.Equal(2, h.Hot);
        Assert.Equal(1, h.Always);
        Assert.Equal(1, h.Flicker);
        Assert.Equal(1, h.FramesHotHistogram[6]);
        Assert.Equal(1, h.FramesHotHistogram[3]);
        Assert.InRange(h.QuietTemporalStd, 2.0, 4.0);
        Assert.True(h.HotTemporalStd > h.QuietTemporalStd, "the flickering pixel lifts the hot pixels' temporal sigma");
    }

    [Fact]
    public void R031_TheStabilityRowCarriesItsFourStatementsAsLines()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = i < 3 ? [(10, 10, 60), (20, 20, 60)] : [(10, 10, 60)] });
        DarkSeriesResult r = Run(s);
        ReportRow stability = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.HotPixels).Rows.Single(row => row.Label == "stabilitate");
        IReadOnlyList<string> lines = Assert.IsAssignableFrom<IReadOnlyList<string>>(stability.Lines);
        Assert.Equal(4, lines.Count);
        // the console prints the four joined by "; ", the window one under the other (R-071)
        Assert.Equal(string.Join("; ", lines), stability.Value);
        Assert.Equal("1 din 2 se vad (peste jumatate din prag) in toate cele 6 cadre", lines[0]);
        Assert.Equal("1 lipsesc din 2 sau mai multe", lines[1]);
        Assert.Matches(@"^de la un cadru la altul variaza cu [0-9.]+ ADU, ceilalti pixeli cu [0-9.]+$", lines[2]);
        Assert.Matches(@"^[0-9]+( de)? pixeli verzi G1 sar intre doua niveluri \(RTS: variaza de peste 6 ori cat zgomotul unui cadru, [0-9.]+\)$", lines[3]);
        Assert.Null(stability.Table);
        Assert.True(stability.Summary);

        // a single frame: one unmeasured line, nothing to split
        using SessionFixture one = new SessionFixture().WithDarkSeries(1, i => new SyntheticDark { Seed = 100 + i, Noise = 1, Hot = SyntheticDark.OnePerLevel });
        ReportRow single = DarkReport.Sections(Run(one)).Single(x => x.Check == ReportCheck.HotPixels).Rows.Single(row => row.Label == "stabilitate");
        Assert.Null(single.Lines);
        Assert.Equal("NEMASURAT: stabilitatea cere cel putin 2 cadre", single.Value);
    }

    [Fact]
    public void R024_TheRowsOfCheckNineSayTheMeasuredRuleTheTableAndTheFramesNeeded()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(10, i => new SyntheticDark { Seed = 100 + i, Noise = 2 });
        DarkSeriesResult r = Run(s);
        EnoughVerdict e = r.Verdicts.Enough;
        IReadOnlyList<ReportRow> rows = DarkReport.Sections(r).Single(x => x.Check == ReportCheck.Enough).Rows;
        Assert.Equal(["zgomot dupa medie", "master", "cu n cadre", "ajung de la", "verdict"], rows.Select(row => row.Label));

        // the measured rule: the last point alone, in a sentence, with the plane and the crop in brackets; no table
        NoisePoint last = r.Darks.Noise.Curve[^1];
        Assert.Equal(5, last.N);
        Assert.Equal(
            $"media a 5 cadre are {RunReport.F(last.Measured, 2)} ADU de zgomot; din zgomotul unui singur cadru ({RunReport.F(r.Darks.Noise.SigmaFrame, 2)} ADU) se asteptau {RunReport.F(last.Expected, 2)}  [pixelii verzi G1, centrul de 192 px, ADU; masurat de la un cadru la altul]",
            rows[0].Value);
        Assert.Null(rows[0].Table);
        Assert.Null(rows[0].Lines);

        // the table: one row per point, the frames stacked marked, no stack column and no choice without lights
        ReportRow frames = rows[2];
        ReportTable t = Assert.IsType<ReportTable>(frames.Table);
        Assert.Equal(["cadre", "zgomotul masterului", "fata de masterul de acum", "cum e"], t.Header);
        Assert.Equal(4, t.Rows.Count);
        Assert.Equal(["5", RunReport.F(e.Points[0].MasterNoise, 2) + " ADU", "+41 %", "prea putine"], t.Rows[0]);
        Assert.Equal(["10 (acum)", RunReport.F(e.MasterNoise, 2) + " ADU", "", ""], t.Rows[1]);
        Assert.Equal(["20", RunReport.F(e.Points[3].MasterNoise, 2) + " ADU", "-29 %", ""], t.Rows[3]);
        Assert.Equal("", t.Note);
        Assert.Null(frames.Choice);
        // the console's line holds the same figures, point by point
        Assert.Equal(
            $"5 cadre: {RunReport.F(e.Points[0].MasterNoise, 2)} ADU (+41 % fata de masterul de acum; prea putine); 10 cadre (acum): {RunReport.F(e.MasterNoise, 2)} ADU; "
            + $"15 cadre: {RunReport.F(e.Points[2].MasterNoise, 2)} ADU (-18 % fata de masterul de acum); 20 de cadre: {RunReport.F(e.Points[3].MasterNoise, 2)} ADU (-29 % fata de masterul de acum)",
            frames.Value);

        Assert.Equal("cel putin 10 cadre, ca un cadru ciudat sa nu strice masterul; cate ajung se vede doar fata de lights-uri", rows[3].Value);
        Assert.Equal($"OK: 10 cadre, exact minimul de 10; zgomotul masterului ({RunReport.F(e.MasterNoise, 2)} ADU) se adauga la fiecare light calibrat, iar daca 10 ajung se vede doar fata de lights-uri: deschide dark-urile impreuna cu lights-urile (--lights)", rows[4].Value);

        // twelve frames: over the minimum, and the verdict says so
        using SessionFixture twelve = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Noise = 2 });
        DarkSeriesResult r12 = Run(twelve);
        Assert.StartsWith("OK: 12 cadre, peste minimul de 10; ", DarkReport.Sections(r12).Single(x => x.Check == ReportCheck.Enough).Rows[^1].Value, StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            IReadOnlyList<ReportRow> en = DarkReport.Sections(r12).Single(x => x.Check == ReportCheck.Enough).Rows;
            Assert.Equal(["noise when averaged", "master", "with n frames", "enough from", "verdict"], en.Select(row => row.Label));
            Assert.StartsWith("the average of 5 frames has ", en[0].Value, StringComparison.Ordinal);
            Assert.Equal(["frames", "master's noise", "against the master of now", "how it is"], en[2].Table!.Header);
            Assert.Contains("; 12 frames (now): ", en[2].Value, StringComparison.Ordinal);
            Assert.Equal("at least 10 frames, so that one odd frame does not spoil the master; how many are enough shows only against the lights", en[3].Value);
            Assert.EndsWith(" adds to every calibrated light, and whether 12 are enough shows only against the lights: open the darks together with the lights (--lights)", en[4].Value, StringComparison.Ordinal);
        }

        // three frames: the one measured point is the frame against the next, and the row says so of 1 frame
        using SessionFixture three = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i, Noise = 2 });
        DarkSeriesResult r3 = Run(three);
        NoisePoint only = Assert.Single(r3.Darks.Noise.Curve);
        Assert.StartsWith(
            $"1 cadru are {RunReport.F(only.Measured, 2)} ADU de zgomot; din zgomotul unui singur cadru ({RunReport.F(r3.Darks.Noise.SigmaFrame, 2)} ADU) se asteptau {RunReport.F(only.Expected, 2)}  [",
            DarkReport.Sections(r3).Single(x => x.Check == ReportCheck.Enough).Rows[0].Value,
            StringComparison.Ordinal);

        // a single frame: unmeasured, no table
        using SessionFixture one = new SessionFixture().WithDarkSeries(1, i => new SyntheticDark { Seed = 100 + i, Noise = 1, Hot = SyntheticDark.OnePerLevel });
        IReadOnlyList<ReportRow> single = DarkReport.Sections(Run(one)).Single(x => x.Check == ReportCheck.Enough).Rows;
        Assert.StartsWith("NEMASURAT: sunt mai putin de 2 cadre  [", single[0].Value, StringComparison.Ordinal);
        Assert.Equal("NEMASURAT: sunt mai putin de 2 cadre", single[2].Value);
        Assert.Null(single[2].Table);
        Assert.Equal("NEMASURAT", single[3].Value);
    }

    [Fact]
    public void R024_TheFramesTableReadsTheMasterEveryFiveFramesAndRatesEachRow()
    {
        // ten frames of noise 2, no lights: a row every five frames to two rows past the ten; under ten too few,
        // from ten on unrated, since without lights it cannot be told (ADR 0056)
        using SessionFixture s = new SessionFixture().WithDarkSeries(10, i => new SyntheticDark { Seed = 100 + i, Noise = 2 });
        EnoughVerdict e = Run(s).Verdicts.Enough;
        Assert.Null(e.Stack);
        Assert.Equal(10, e.FramesNeeded);
        Assert.Equal([5, 10, 15, 20], e.Points.Select(p => p.N));
        Assert.Equal([false, true, false, false], e.Points.Select(p => p.Now));
        Assert.Equal([FramesRating.TooFew, FramesRating.Unjudged, FramesRating.Unjudged, FramesRating.Unjudged], e.Points.Select(p => p.Rating));
        foreach (FramesPoint p in e.Points)
        {
            // the master of n frames against the master of the ten: sqrt(10 / n) times its noise
            Assert.Equal(e.MasterNoise * Math.Sqrt(10.0 / p.N), p.MasterNoise, 9);
            Assert.Equal(Math.Sqrt(10.0 / p.N) - 1, p.AgainstNow, 9);
            Assert.True(double.IsNaN(p.StackPenalty));
        }

        // twelve frames: their own row stands between the tens and the fifteens
        using SessionFixture twelve = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Noise = 2 });
        EnoughVerdict e12 = Run(twelve).Verdicts.Enough;
        Assert.Equal([5, 10, 12, 15, 20], e12.Points.Select(p => p.N));
        Assert.Equal(12, Assert.Single(e12.Points, p => p.Now).N);
        Assert.Equal(0, e12.Points[2].AgainstNow, 9);

        // ten frames of noise 4: the master stays over the value step, which decides nothing without lights:
        // the frames needed are the minimum and no row is added for them (ADR 0056; with lights the rows rated
        // few and the row of the frames needed are R024_WithLights...'s in LightsAndIndexTests)
        using SessionFixture noisy = new SessionFixture().WithDarkSeries(10, i => new SyntheticDark { Seed = 100 + i, Noise = 4 });
        EnoughVerdict en = Run(noisy).Verdicts.Enough;
        Assert.Equal(Th.MinDarks, en.FramesNeeded);
        Assert.Equal([5, 10, 15, 20], en.Points.Select(p => p.N));
        Assert.Equal(FramesRating.TooFew, en.Points[0].Rating);
        Assert.All(en.Points.Skip(1), p => Assert.Equal(FramesRating.Unjudged, p.Rating));

        // three frames of noise 6: their own row first, then the five and the ten, too few under ten
        using SessionFixture few = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i, Noise = 6 });
        EnoughVerdict ef = Run(few).Verdicts.Enough;
        Assert.Equal([3, 5, 10], ef.Points.Select(p => p.N));
        Assert.Equal([FramesRating.TooFew, FramesRating.TooFew, FramesRating.Unjudged], ef.Points.Select(p => p.Rating));
        Assert.True(ef.Points[0].Now);

        // a single frame is no series: no table
        using SessionFixture one = new SessionFixture().WithDarkSeries(1, i => new SyntheticDark { Seed = 100 + i, Noise = 1, Hot = SyntheticDark.OnePerLevel });
        Assert.Empty(Run(one).Verdicts.Enough.Points);
    }

    [Fact]
    public void R024_AStackFigureThatRoundsToALimitIsPrintedWithMoreDecimals()
    {
        // one decimal would print a limit itself beside "under the limit" or "over the limit"
        Assert.Equal("9.96 %", DarkReport.StackPercent(0.0996));
        Assert.Equal("10.04 %", DarkReport.StackPercent(0.1004));
        Assert.Equal("4.96 %", DarkReport.StackPercent(0.0496));
        // the other limit is guarded too: the row "ajung de la" names both
        Assert.Equal("5.03 %", DarkReport.StackPercent(0.0503));
        // closer still: as many decimals as it takes, to four
        Assert.Equal("9.996 %", DarkReport.StackPercent(0.09996));
        Assert.Equal("10.0004 %", DarkReport.StackPercent(0.100004));
        // away from the limits, and on a limit itself, one decimal
        Assert.Equal("10.4 %", DarkReport.StackPercent(0.104));
        Assert.Equal("3.1 %", DarkReport.StackPercent(0.031));
        Assert.Equal("10.0 %", DarkReport.StackPercent(0.10));
        Assert.Equal("5.0 %", DarkReport.StackPercent(0.05));
    }

    [Fact]
    public void R030_TheHotPixelCountRowsLeaveTheSummaryToTheHotPixelsTab()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice });
        IReadOnlyList<ReportSection> sections = DarkReport.Sections(Run(s));
        ReportSection hot = sections.Single(x => x.Check == ReportCheck.HotPixels);
        Assert.Equal(["pixeli peste prag", "pe culori, peste 20", "cat de fierbinti", "stabilitate", "verdict"], hot.Rows.Select(row => row.Label));
        Assert.Equal([false, false, false, true, true], hot.Rows.Select(row => row.Summary));
        Assert.All(sections.Where(x => x != hot).SelectMany(x => x.Rows), row => Assert.True(row.Summary));
    }

    [Fact]
    public void R032_ADeadAndASaturatedPixelAreCountedAndFlagged()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Extra = (x, y) => (x, y) == (5, 5) ? -200 : (x, y) == (7, 9) ? 70000 : 0 });
        DarkSeriesResult r = Run(s);
        Assert.Equal(1, r.Verdicts.DeadSat.Dead);
        Assert.Equal(1, r.Verdicts.DeadSat.Saturated);
        Assert.Equal(Verdict.Warning, r.Verdicts.DeadSat.Verdict);

        using SessionFixture clean = new SessionFixture().WithDarkSeries(4, Plain);
        Assert.Equal(Verdict.Ok, Run(clean).Verdicts.DeadSat.Verdict);
    }

    [Fact]
    public void R040_FixedRowBandingOnEveryFrameIsAPattern()
    {
        // every eighth row 5 ADU up, the same in every frame: it survives the master and the running median sees it
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Extra = (x, y) => y % 8 == 0 ? 5 : 0 });
        DarkSeriesResult r = Run(s);
        Assert.True(r.Verdicts.Pattern.BandingP2pAdu > 2, $"banding {r.Verdicts.Pattern.BandingP2pAdu}");
        Assert.Equal(Verdict.Warning, r.Verdicts.Pattern.Verdict);
        BandingFigures worst = r.Darks.Banding.MaxBy(b => b.RowP2p)!;
        Assert.True(worst.RowP2p > worst.ColP2p, "the pattern is on the rows");
    }

    [Fact]
    public void R041_TheBiasesGiveTheReadNoiseTheClippingAndTheDarkCurrent()
    {
        // darks 3 ADU above the biases' pedestal: the dark current map reads 3
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Pedestal = 203 }, biases: 5, bias: i => new SyntheticDark { Seed = 500 + i, Noise = 2 });
        DarkSeriesResult r = Run(s);
        BiasAnalysis b = r.Biases!;
        Assert.Equal(5, b.Used);
        foreach (double rn in b.ReadNoise)
        {
            Assert.InRange(rn, 1.7, 2.3);
        }

        Assert.InRange(b.Clip.FractionBelowBlack, 0.4, 0.6);
        Assert.InRange(b.Clip.FractionBelowMinusTwoSigma, 0.01, 0.04);
        Assert.InRange(r.DarkCurrent!.Median, 2.5, 3.5);
        Assert.InRange(r.Verdicts.Noise.ReadNoise!.Value, 1.7, 2.3);
        Assert.InRange(r.Verdicts.Noise.NoiseOverRead!.Value, 1.8, 2.6);
    }
}
