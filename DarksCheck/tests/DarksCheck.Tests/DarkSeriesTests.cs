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

/// <summary>Checks 2 to 8 on synthetic darks with known answers (R-020 to R-041).</summary>
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
        Assert.Contains("negrul luat din valoarea obisnuita a cadrului 1", d.ScaleSource, StringComparison.Ordinal);
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
    public void R023_TheSeriesIsFittedToAPlateauAndTheFramesOffItAreNamed()
    {
        // ten hot pixels of 300 ADU whose amplitude approaches 1.2 as 1.2 - 0.4 exp(-i / 3): the median
        // frame (the master) sits near 1.135, so the plateau reads about 1.06 of the master and the
        // first two frames are more than two degrees under it (their ratio under 1.06 x 0.79 = 0.84)
        using SessionFixture s = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 1.2 - 0.4 * Math.Exp(-i / 3.0) });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.Equal(10, w.Pixels);
        Assert.InRange(w.FirstRatio, 0.68, 0.73);
        Assert.InRange(w.LastRatio, 1.03, 1.07);
        Assert.InRange(w.DeltaCelsius, 3.0, 4.0);
        Assert.Equal(22, w.Minutes, 0.01);
        Assert.InRange(w.Plateau, 1.02, 1.10);
        Assert.InRange(w.TauMinutes, 4, 9);
        Assert.InRange(w.PlateauCelsius, -0.8, -0.2);
        Assert.InRange(Math.Abs(w.SettleCelsius), 0, 0.3);
        Assert.Equal([1, 2], w.OffPlateauFrames);
        Assert.Equal(Math.Sqrt(12.0 / 10), w.NoiseFactorWithoutOff, 1e-9);
        Assert.Equal(w.Plateau * Math.Pow(2, -Th.OffPlateauCelsius / Th.DoublingCelsius), w.OffPlateauLow, 1e-9);
        Assert.Equal(w.Plateau * Math.Pow(2, Th.OffPlateauCelsius / Th.DoublingCelsius), w.OffPlateauHigh, 1e-9);
        Assert.InRange(w.MasterWithoutOffCelsius, -0.7, -0.2);
        Assert.Equal(Verdict.Ok, r.Verdicts.Drift.Verdict);

        ReportSection check3 = DarkReport.Sections(r).Single(x => x.Title.StartsWith("3.", StringComparison.Ordinal));
        Assert.Equal(["pixeli fierbinti", "platou", "de aruncat", "verdict"], check3.Rows.Select(x => x.Label));
        Assert.StartsWith("seria se incalzeste spre o temperatura stabila (platou), la 1.0", check3.Rows[1].Value, StringComparison.Ordinal);
        Assert.Contains("; masterul e cu 0.", check3.Rows[1].Value, StringComparison.Ordinal);
        Assert.Contains("; ultimul cadru mai are +0.", check3.Rows[1].Value, StringComparison.Ordinal);
        Assert.StartsWith("2 cadre (1, 2) sunt la mai mult de 2.0 grade de platou (pixelii fierbinti sub 0.8", check3.Rows[2].Value, StringComparison.Ordinal);
        Assert.Contains("; fara ele masterul ar fi cu 0.", check3.Rows[2].Value, StringComparison.Ordinal);
        Assert.EndsWith(", dar cu 10 % mai mult zgomot", check3.Rows[2].Value, StringComparison.Ordinal);
        Assert.StartsWith("OK: seria s-a stabilizat (ultimul cadru mai are +0.", check3.Rows[3].Value, StringComparison.Ordinal);
        Assert.Contains("si toate cadrele sunt destul de aproape de master ca Siril sa le potriveasca cu -opt (intre 0.7 si 1.4); incalzirea de 3.", check3.Rows[3].Value, StringComparison.Ordinal);
        Assert.EndsWith("cadrele de aruncat sunt in randul de mai sus", check3.Rows[3].Value, StringComparison.Ordinal);

        // the table: the degrees from the plateau per frame, the note on the frames off it
        IReadOnlyList<DarkTableRow> rows = DarkReport.FrameRows(r);
        Assert.StartsWith("-3.", rows[0].Plateau, StringComparison.Ordinal);
        Assert.StartsWith("de aruncat: pixelii fierbinti sunt la 0.7", rows[0].Note, StringComparison.Ordinal);
        Assert.Contains(" sub 0.8", rows[0].Note, StringComparison.Ordinal);
        Assert.Contains(", adica la peste 2.0 grade sub platou (1.0", rows[0].Note, StringComparison.Ordinal);
        Assert.NotNull(rows[1].Note);
        Assert.Null(rows[2].Note);
        Assert.Null(rows[11].Note);
        Assert.StartsWith("-0.", rows[11].Plateau, StringComparison.Ordinal);
        Assert.Contains("platou = cu cate grade e cadrul fata de temperatura stabila a seriei; de aruncat = la peste 2.0 grade de ea (verificarea 3)", DarkReport.FrameTableNote(r), StringComparison.Ordinal);
        Assert.Contains("platou", DarkReport.FrameTableHeader(r), StringComparison.Ordinal);
        string line = DarkReport.FrameLine(rows[0]);
        Assert.EndsWith(rows[0].Note, line, StringComparison.Ordinal);
        Assert.DoesNotContain("de aruncat", DarkReport.FrameLine(rows[11]), StringComparison.Ordinal);
    }

    [Fact]
    public void R025_AFrameWithoutTheMastersHotPixelsIsMarkedToDiscard()
    {
        // the plateau series of R023 with its seventh frame's hot pixels under the background: a ratio under 0, so no
        // degrees, but the analysis puts it off the plateau, and the table says why and tints it
        using SessionFixture s = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i == 6 ? -0.5 : 1.2 - 0.4 * Math.Exp(-i / 3.0) });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.True(w.HasFit);
        Assert.True(r.Darks.Frames[6].AmpRatioMedian <= 0, $"ratio {r.Darks.Frames[6].AmpRatioMedian}");
        Assert.Contains(7, w.OffPlateauFrames);
        DarkTableRow row = DarkReport.FrameRows(r)[6];
        Assert.Equal("-", row.Plateau);
        Assert.Equal("de aruncat: pixelii fierbinti ai masterului nu se vad in acest cadru", row.Note);
    }

    [Fact]
    public void R023_ASeriesStillClimbingAtTheEndIsNotSettled()
    {
        // a straight climb of 0.05 per frame: the fit extrapolates a plateau far above the last frame
        using SessionFixture s = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 0.8 + 0.05 * i });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.True(w.SettleCelsius > Th.SettledCelsius, $"settle {w.SettleCelsius}");
        Assert.Equal(Verdict.Warning, r.Verdicts.Drift.Verdict);
        string verdict = DarkReport.Sections(r).Single(x => x.Title.StartsWith("3.", StringComparison.Ordinal)).Rows[^1].Value;
        Assert.StartsWith("ATENTIE: seria inca se incalzeste la ultimul cadru (mai are +", verdict, StringComparison.Ordinal);
        Assert.Contains("grade pana la platou, peste 1.0; platoul e ghicit din curba): masterul amesteca temperaturi diferite; continua seria", verdict, StringComparison.Ordinal);
        Assert.True(w.TauAtGridTop);
        Assert.Contains("seria e prea scurta ca sa se vada cat de repede se incalzeste (doua treimi din drum ar lua cel putin ", DarkReport.Sections(r).Single(x => x.Title.StartsWith("3.", StringComparison.Ordinal)).Rows[1].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void R023_AFrameOutsideTheOptMarginOfTheMasterFailsTheSpan()
    {
        // the first frame at half the master's hot pixels, the other six flat: settled, but outside 0.7 .. 1.4
        using SessionFixture s = new SessionFixture().WithDarkSeries(7, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = i == 0 ? 0.5 : 1.0 });
        DarkSeriesResult r = Run(s);
        Assert.Equal(Verdict.Warning, r.Verdicts.Drift.Verdict);
        Assert.Equal([1], r.Darks.Warming.OffPlateauFrames);
        string verdict = DarkReport.Sections(r).Single(x => x.Title.StartsWith("3.", StringComparison.Ordinal)).Rows[^1].Value;
        Assert.StartsWith("ATENTIE: 1 cadru (1) e prea departe de master ca Siril sa il potriveasca cu -opt (in afara 0.7 .. 1.4; cel mai departe 0.5", verdict, StringComparison.Ordinal);
        Assert.EndsWith("): seria a pornit rece; arunca-l", verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void R023_AFlatSeriesHasNoFrameOffThePlateau()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(7, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.InRange(w.Plateau, 0.97, 1.03);
        Assert.Empty(w.OffPlateauFrames);
        Assert.Equal(Verdict.Ok, r.Verdicts.Drift.Verdict);
        ReportSection check3 = DarkReport.Sections(r).Single(x => x.Title.StartsWith("3.", StringComparison.Ordinal));
        Assert.StartsWith("seria nu s-a incalzit: nivelul a stat la ", check3.Rows[1].Value, StringComparison.Ordinal);
        Assert.EndsWith(" din master, iar primul cadru e la mai putin de 1.0 grade de el", check3.Rows[1].Value, StringComparison.Ordinal);
        Assert.StartsWith("niciun cadru nu e la mai mult de 2.0 grade de platou (pixelii fierbinti intre ", check3.Rows[2].Value, StringComparison.Ordinal);
        Assert.All(DarkReport.FrameRows(r), row => Assert.Null(row.Note));
    }

    [Fact]
    public void R023_UnderSixFramesThereIsNoPlateauAndTheVerdictRestsOnTheSpan()
    {
        // five frames climbing 0.8 → 1.2 of the master: the first-to-last degrees, no fit
        using SessionFixture s = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 0.8 + 0.4 * i / 4 });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.InRange(w.DeltaCelsius, 3.0, 4.0);
        Assert.Equal(8, w.Minutes, 0.01);
        Assert.True(double.IsNaN(w.Plateau));
        Assert.Empty(w.OffPlateauFrames);
        Assert.Equal(Verdict.Ok, r.Verdicts.Drift.Verdict);
        ReportSection check3 = DarkReport.Sections(r).Single(x => x.Title.StartsWith("3.", StringComparison.Ordinal));
        Assert.Equal("NEMASURAT: sunt mai putin de 6 cadre cu pixeli fierbinti", check3.Rows[1].Value);
        Assert.Equal("NEMASURAT: sunt mai putin de 6 cadre cu pixeli fierbinti", check3.Rows[2].Value);
        Assert.Equal("OK: toate cadrele sunt destul de aproape de master ca Siril sa le potriveasca cu -opt (intre 0.7 si 1.4); sunt mai putin de 6 cadre, deci fara platou", check3.Rows[3].Value);
        Assert.All(DarkReport.FrameRows(r), row => Assert.Equal("-", row.Plateau));

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
        ReportRow litRow = DarkReport.Sections(r)[0].Rows.Single(x => x.Label == "lumina pe cadre");
        Assert.Contains($"{r.Darks.Noise.MasterNoise:F2} ADU", litRow.BoldParts);
        Assert.All(litRow.BoldParts, b => Assert.Contains(b, litRow.Value, StringComparison.Ordinal));
        Assert.Contains("   fierb.  platou  lumina  latura   >20   [pixelii verzi G1;", DarkReport.FrameTableHeader(r), StringComparison.Ordinal);
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

    [Fact]
    public void R029_LightInEveryFrameIsReadPerSideAndTheBiasClearsAReadoutBand()
    {
        // a band of +2 ADU over the top 64 rows (the top third) of every dark, the raised mirror's shadow of an eyepiece leak
        // (noise 1 ADU instead of the fixture's 3: a region of these 256 x 192 frames has 256 pixels, so at noise 3 the
        // intercept error reaches 0.7 ADU and the band of 2 sits at the 3-sigma condition; the D3300's regions have 2.4 Mpx)
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
        Assert.True(top.ErrorAdu > 0 && top.ErrorAdu < 0.4, $"error {top.ErrorAdu}");
        // the row and the verdict mark the light and the master's noise bold (ADR 0016); check 8's master row marks its noise
        IReadOnlyList<ReportSection> sections = DarkReport.Sections(r);
        ReportRow constantRow = sections[0].Rows.Single(x => x.Label == "lumina constanta");
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
        ReportRow verdictRow = sections[0].Rows.Single(x => x.Label == "verdict");
        Assert.Contains(masterNoise, verdictRow.BoldParts);
        Assert.Contains("; adica +", verdictRow.Value, StringComparison.Ordinal);
        ReportRow enough = sections.Single(s => s.Title.StartsWith("8.", StringComparison.Ordinal)).Rows.Single(x => x.Label == "master");
        Assert.Equal([masterNoise], enough.BoldParts);
        Assert.Contains(masterNoise, enough.Value, StringComparison.Ordinal);
        Assert.Empty(sections[0].Rows.Single(x => x.Label == "master dark").BoldParts);
        Assert.Contains("   fierb.  platou  lumina  latura   >20   [pixelii verzi G1;", DarkReport.FrameTableHeader(r), StringComparison.Ordinal);
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
            ReportRow verdict = DarkReport.Sections(r)[0].Rows.Single(x => x.Label == "verdict");
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
        Assert.Contains("; curentul de intuneric (ce aduna un pixel obisnuit din caldura senzorului) e ", Value("zgomot pe un dark"), StringComparison.Ordinal);
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
            Assert.Contains(english ? "the dark current (what a typical pixel gathers from the sensor's heat) is " : "curentul de intuneric (ce aduna un pixel obisnuit din caldura senzorului) e ", all, StringComparison.Ordinal);
            List<string> left = texts.Where(t => method.IsMatch(t)).Select(t => $"'{method.Match(t).Value}' in: {t}").ToList();
            Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R082_TheSummarysSessionHeaderAndChecks2To8AreInPlainWords(bool english)
    {
        // the variants the fixtures reach: lights and biases, a bare series, the eyepiece band of R029 with biases, a
        // series climbing to a plateau, one still climbing at its end, one whose first frame is cold, a flat one, a
        // single frame, a noisy frame among quiet ones, a row banding fixed in every frame, ten quiet frames
        static double Band(int x, int y) => y < 64 ? 2 : 0;
        static SyntheticLight Light(int i) => new() { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.0, Stars = SyntheticLight.ThreeStars };
        using SessionFixture full = new SessionFixture().WithDarkSeries(6, Plain, biases: 3).WithLights(4, Light);
        using SessionFixture bare = new SessionFixture().WithDarkSeries(4, Plain);
        using SessionFixture leak = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = Band, Noise = 1 }, biases: 4);
        using SessionFixture edge = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = TenHot, HotFactor = WarmFactor(i), Extra = (x, y) => x < 32 ? 3 : 0 });
        using SessionFixture plateau = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 1.2 - 0.4 * Math.Exp(-i / 3.0) });
        using SessionFixture climbing = new SessionFixture().WithDarkSeries(8, i => new SyntheticDark { Seed = 100 + i, Hot = StrongHot, HotFactor = 0.8 + 0.05 * i });
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
            foreach (SessionFixture s in new[] { full, bare, leak, edge, plateau, climbing, cold, flat, one, noisy, rows, quiet })
            {
                DarkSeriesResult r = Run(s);
                texts.AddRange(InventoryReport.HeaderRows(r.Inventory).SelectMany(row => new[] { row.Label, row.Value }));
                texts.Add(DarkReport.HeaderLine(r));
                texts.AddRange(DarkReport.HeaderRows(r).SelectMany(row => new[] { row.Label, row.Value }));
                // checks 2 to 8, the parts approved so far (ADR 0030)
                foreach (ReportSection section in DarkReport.Sections(r).Where(x => x.Title[0] is >= '2' and <= '8'))
                {
                    texts.Add(section.Title);
                    texts.AddRange(section.Rows.SelectMany(row => new[] { row.Label, row.Value }));
                }
            }

            // the scenes reached their variants, so the words below were looked at
            string all = string.Join("\n", texts);
            Assert.Contains(english ? "of constant light against the centre, which does not come from the bias" : "lumina constanta fata de centru, care nu vine din bias", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the series warms towards a steady temperature (the plateau)" : "seria se incalzeste spre o temperatura stabila (platou)", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the series is still warming at the last frame" : "seria inca se incalzeste la ultimul cadru", all, StringComparison.Ordinal);
            Assert.Contains(english ? "too far from the master for Siril to match it with -opt" : "prea departe de master ca Siril sa il potriveasca cu -opt", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the series did not warm" : "seria nu s-a incalzit", all, StringComparison.Ordinal);
            Assert.Contains(english ? "light that got in or a level difference of the readout" : "lumina intrata sau o diferenta de nivel din citire", all, StringComparison.Ordinal);
            Assert.Contains(english ? "a single frame has nothing to be compared with" : "un singur cadru nu are cu ce fi comparat", all, StringComparison.Ordinal);
            Assert.Contains(english ? "pairs of biases taken one after the other" : "perechi de bias-uri luate unul dupa altul", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the session has no biases" : "sesiunea nu are bias-uri", all, StringComparison.Ordinal);
            Assert.Contains(english ? "WARNING: 1 frame has noise more than 1.5 times the usual" : "ATENTIE: 1 cadru are zgomotul de peste 1.5 ori mai mare decat de obicei", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the part that grows as the sensor warms" : "partea care creste cand senzorul se incalzeste", all, StringComparison.Ordinal);
            Assert.Contains(english ? "WARNING: fixed banding of " : "ATENTIE: benzi fixe de ", all, StringComparison.Ordinal);
            Assert.Contains(english ? "as it is the only one" : "fiind unul singur", all, StringComparison.Ordinal);
            Assert.Contains(english ? "are enough; the master's noise" : "ajung; zgomotul masterului", all, StringComparison.Ordinal);
            Assert.Contains(english ? "so that one odd frame does not spoil the master" : "ca un cadru ciudat sa nu strice masterul", all, StringComparison.Ordinal);
            Assert.Contains(english ? "; it would drop under it from " : "; ar cobori sub el de la ", all, StringComparison.Ordinal);
            List<string> left = texts.Where(t => method.IsMatch(t)).Select(t => $"'{method.Match(t).Value}' in: {t}").ToList();
            Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R083_TheFramesTablesWordsAreInPlainWords(bool english)
    {
        // the variants the fixtures reach: a series that climbs to its plateau with frames to discard below it, one that
        // comes down to it with frames above it, one with a frame whose hot pixels do not show, the eyepiece band of R029 with biases, an edge without biases, a short
        // series without a plateau, a capped one with skipped darks and lights; and the messages when no dark, bias,
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

        System.Text.RegularExpressions.Regex method = english ? PlainWords.EnglishFramesWords() : PlainWords.RomanianFramesWords();
        using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
        {
            List<string> texts = [DarkReport.PlateauColumnTip];
            List<string> lightSkips = [];
            foreach (DarkSeriesResult r in new[] { Run(climbing), Run(cooling), Run(blind), Run(leak), Run(edge), Run(bare), DarkSeriesRun.Run(capped.Root, new DarkOptions(Threads: 2, MaxFrames: 2)) })
            {
                texts.Add(DarkReport.FrameTableHeader(r));
                texts.Add(DarkReport.WarmingChartNote(r));
                texts.AddRange(DarkReport.FrameRows(r).Select(row => row.Note ?? row.Skipped ?? row.Error ?? ""));
                lightSkips.AddRange(DarkReport.LightRows(r).Select(row => row.Skipped ?? ""));
            }

            texts.AddRange(lightSkips);

            texts.Add(Assert.ThrowsAny<Exception>(() => DarkSeriesRun.Run(broken.Dir, new DarkOptions(Threads: 2))).Message);
            texts.Add(Run(badBiases).BiasError ?? "");
            texts.Add(Run(badLights).LightsError ?? "");
            texts.Add(DarkSeriesRun.Run(climbing.Root, new DarkOptions(Threads: 2, Against: badPartner.Root)).PartnerError ?? "");

            // the scenes reached their variants, so the words below were looked at
            string all = string.Join("\n", texts);
            Assert.Contains(english ? "to discard: the hot pixels are at " : "de aruncat: pixelii fierbinti sunt la ", all, StringComparison.Ordinal);
            Assert.Contains(english ? ", that is more than 2.0 degrees above the plateau" : ", adica la peste 2.0 grade peste platou", all, StringComparison.Ordinal);
            Assert.Contains(english ? "The steady temperature of the series (the plateau) is at " : "Temperatura stabila a seriei (platoul) e la ", all, StringComparison.Ordinal);
            Assert.Contains(english ? "to discard: the hot pixels of the master do not show in this frame" : "de aruncat: pixelii fierbinti ai masterului nu se vad in acest cadru", all, StringComparison.Ordinal);
            Assert.Contains(english ? ", once the bias is taken off (glow included)" : ", dupa scaderea bias-ului (cu glow)", all, StringComparison.Ordinal);
            Assert.Contains(english ? " (no biases, glow included)" : " (fara bias-uri, cu glow)", all, StringComparison.Ordinal);
            Assert.Contains(english ? "against the centre (UNMEASURED)" : "fata de centru (NEMASURAT)", all, StringComparison.Ordinal);
            Assert.Contains(english ? "the plateau cannot be found" : "nu se poate gasi platoul", all, StringComparison.Ordinal);
            Assert.Contains(english ? "frames only one in 3 is read)" : "cadre se citeste doar unul din 3)", all, StringComparison.Ordinal);
            Assert.Contains(english ? "no dark could be read" : "niciun dark nu s-a putut citi", all, StringComparison.Ordinal);
            Assert.Contains(english ? "no bias could be read" : "niciun bias nu s-a putut citi", all, StringComparison.Ordinal);
            Assert.Contains(english ? "no light could be read" : "niciun light nu s-a putut citi", all, StringComparison.Ordinal);
            Assert.Contains(english ? "none of the partner's darks could be read" : "niciun dark al partenerului nu s-a putut citi", all, StringComparison.Ordinal);
            Assert.Contains(lightSkips, t => t.StartsWith(english ? "skipped (over 2 frames only one in 3 is read)" : "sarit (peste 2 cadre se citeste doar unul din 3)", StringComparison.Ordinal));
            List<string> left = texts.Where(t => method.IsMatch(t)).Select(t => $"'{method.Match(t).Value}' in: {t}").ToList();
            Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
        }
    }

    // R-082's words of the method in the summary, every section; the median only where check 8 names how a master is
    // made; LightsAndIndexTests reads check 9 and INDEX with the same words
    [System.Text.RegularExpressions.GeneratedRegex(@"\b(sigma|sqrt|abater\w*|offset\w*|brut\w*|extrapol\w*|decod\w*|BITPIX|CFA|p1|p10|p90|p99|q25|q75|amplitudin\w*|normaliz\w*|cuantiz\w*|cuadratur\w*|gradient\w*|deriv\w*|rezidual\w*|temporal\w*|glisant\w*|candidat\w*|rms|rang\w*|persistent\w*|izolat\w*|raport\w*|supracorect\w*|scaleaz\w*)\b|(?<!\bcu )\bmedian\w*|x eroarea|zgomot\w* alb|varf la varf|\bn=|±", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    internal static partial System.Text.RegularExpressions.Regex RomanianSummaryWords();

    [System.Text.RegularExpressions.GeneratedRegex(@"\b(sigma|sqrt|deviation\w*|offset\w*|raw ADU|extrapolat\w*|decod\w*|BITPIX|CFA|p1|p10|p90|p99|q25|q75|amplitude\w*|normali[sz]\w*|quanti[sz]\w*|quadrature|gradient\w*|drift\w*|residual\w*|temporal\w*|candidate\w*|rms|rank\w*|persistent\w*|isolated|ratio\w*|overcorrect\w*|scales)\b|(?<!\bwith the )\bmedian\w*|x the error|white noise|peak to peak|\bn=|±", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    internal static partial System.Text.RegularExpressions.Regex EnglishSummaryWords();

    // R-081's words of the method, whole words in any case; the median only where it names how a stack is made
    [System.Text.RegularExpressions.GeneratedRegex(@"\b(sigma|sqrt|cuadratur\w*|radacin\w*|cuantiz\w*|normaliz\w*|abater\w*|offset\w*|castig\w*|brut\w*|petic\w*|supraestim\w*)\b|(?<![\w-])e-(?!\w)|(?<!\bcu )\bmedian\w*", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex RomanianMethodWords();

    [System.Text.RegularExpressions.GeneratedRegex(@"\b(sigma|sqrt|quadrature|square root|quanti[sz]\w*|normali[sz]\w*|deviation\w*|offset\w*|gain\w*|overestimat\w*|raw|patch\w*)\b|(?<![\w-])e-(?!\w)|(?<!\bwith the )\bmedian\w*", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
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

        // three noisy frames: too few for a median, and the master would stay noisier than one quantisation step
        using SessionFixture few = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i, Noise = 6 });
        EnoughVerdict e = Run(few).Verdicts.Enough;
        Assert.Equal(Verdict.Warning, e.Verdict);
        Assert.True(e.TooFew);
        Assert.InRange(e.FramesNeeded, 50, 65);

        // a single frame is no series: unmeasured, never OK; the residual against its own median is zero by construction, so check 4 is unmeasured too
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

        // ten quiet frames whose master would still sit over the step: warning with the frames that would do
        using SessionFixture noisy = new SessionFixture().WithDarkSeries(10, i => new SyntheticDark { Seed = 100 + i, Noise = 4 });
        EnoughVerdict en = Run(noisy).Verdicts.Enough;
        Assert.Equal(Verdict.Warning, en.Verdict);
        Assert.False(en.TooFew);
        Assert.InRange(en.FramesNeeded, 22, 30);
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

    [Fact]
    public void R027_AFrameWithOtherSettingsIsLeftOutWithItsError()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(4, Plain);
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0005.fit"), new SyntheticDark { Seed = 9 }.Build(SessionFixture.FirstStart.AddSeconds(480), 60, 400));
        // ISO 800 at 60 s has the first frame's ISO x exposure product: each setting is compared on its own
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_0006.fit"), new SyntheticDark { Seed = 10 }.Build(SessionFixture.FirstStart.AddSeconds(600), 60, 800));
        DarkSeriesResult r = Run(s);
        Assert.Equal(4, r.Darks.Used);
        Assert.Contains("setari diferite de primul cadru: ISO 400, 60 s", r.Darks.Frames[4].Error, StringComparison.Ordinal);
        Assert.Contains("setari diferite de primul cadru: ISO 800, 60 s", r.Darks.Frames[5].Error, StringComparison.Ordinal);
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

    [Fact]
    public void R027_AboveTheCapOnlyEveryKthFrameIsDecoded()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(5, Plain);
        FrameStack stack = FrameStack.Load(SessionFolder.Open(s.Root).Darks, 2, maxFrames: 2);
        Assert.Equal(2, stack.Decoded.Count);
        Assert.Equal([1, 4], stack.Decoded.Select(f => f.Info.Index));
        Assert.Contains("sarit (peste 2 cadre se citeste doar unul din 3)", stack.Frames[1].Error, StringComparison.Ordinal);
        Assert.Equal(5, stack.Frames.Count);
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
