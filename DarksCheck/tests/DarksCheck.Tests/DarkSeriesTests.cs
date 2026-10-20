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
public class DarkSeriesTests
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
        Assert.Contains("negru 200 estimat din mediana cadrului 1", d.ScaleSource, StringComparison.Ordinal);
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
        Assert.Contains(" ADU de centru (pe dark - bias); peste 1.0: scurgere de lumina sau glow", sw.ToString(), StringComparison.Ordinal);

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
    public void R023_TheWarmingOfTheSeriesIsReadFromTheHotPixels()
    {
        // ten hot pixels of 100 ADU whose amplitude climbs from 0.8 to 1.2 of the master's (the median frame)
        (int X, int Y, double Adu)[] hot = Enumerable.Range(0, 10).Select(k => (60 + 7 * k, 100 + 5 * k, 100.0)).ToArray();
        using SessionFixture s = new SessionFixture().WithDarkSeries(7, i => new SyntheticDark { Seed = 100 + i, Hot = hot, HotFactor = 0.8 + 0.4 * i / 6 });
        DarkSeriesResult r = Run(s);
        Warming w = r.Darks.Warming;
        Assert.Equal(10, w.Pixels);
        Assert.InRange(w.FirstRatio, 0.75, 0.85);
        Assert.InRange(w.LastRatio, 1.15, 1.25);
        Assert.InRange(w.DeltaCelsius, 3.0, 4.0);
        Assert.Equal(12, w.Minutes, 0.01);
        Assert.Equal(Verdict.Warning, r.Verdicts.Drift.Verdict);

        using SessionFixture flat = new SessionFixture().WithDarkSeries(7, i => new SyntheticDark { Seed = 100 + i, Hot = hot });
        Assert.Equal(Verdict.Ok, Run(flat).Verdicts.Drift.Verdict);
    }

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
        Assert.Contains("    lumina pe cadre    : 2 cadre: 7 +0.", text, StringComparison.Ordinal);
        Assert.Contains("  amplit.  lumina  latura  >20   [G1;", DarkReport.FrameTableHeader(r), StringComparison.Ordinal);
        Assert.Contains("lumina = ADU", DarkReport.FrameTableNote(r), StringComparison.Ordinal);
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
        Assert.Contains("; niciun cadru cu lumina peste incalzirea seriei", swc.ToString(), StringComparison.Ordinal);
        Assert.Contains("    lumina pe cadre    : niciun cadru peste 5 x abaterea tipica (", swc.ToString(), StringComparison.Ordinal);

        // under six frames the fit has nothing to stand on
        using SessionFixture few = new SessionFixture().WithDarkSeries(4, Plain);
        DarkSeriesResult rf = Run(few);
        Assert.Null(rf.Verdicts.Leak.Frames);
        Assert.Equal("-", DarkReport.FrameRows(rf)[0].Light);
        using StringWriter swf = new();
        DarkReport.Write(rf, swf);
        Assert.Contains("    lumina pe cadre    : NEMASURAT: sub 6 cadre cu pixeli fierbinti", swf.ToString(), StringComparison.Ordinal);
        Assert.Contains("; lumina pe cadre NEMASURATA", swf.ToString(), StringComparison.Ordinal);
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
        Assert.Contains("): glow al senzorului, dark-urile raman bune", swg.ToString(), StringComparison.Ordinal);
        Assert.Contains("    glow / constant    : la ", swg.ToString(), StringComparison.Ordinal);
        Assert.Contains(", partea care creste cu incalzirea (constant ", swg.ToString(), StringComparison.Ordinal);
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
        Assert.Contains("): scurgere de lumina, refa dark-urile", swl.ToString(), StringComparison.Ordinal);
        Assert.Contains(", din care in bias ", swl.ToString(), StringComparison.Ordinal);

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
        Assert.Contains("): scurgere de lumina sau offset de citire, fara bias-uri nu se pot deosebi", swk.ToString(), StringComparison.Ordinal);
        Assert.Contains(" ADU (fara bias: lumina sau offset de citire)", swk.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("(brut:", swk.ToString(), StringComparison.Ordinal);

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
        Assert.Contains("lumina constanta in toate cadrele: +", text, StringComparison.Ordinal);
        Assert.Contains(") pe latura de sus fata de centru, nu e in bias: scurgere de lumina (ocular, montura)", text, StringComparison.Ordinal);
        Assert.Contains("    lumina constanta   : dupa bias, fata de centru: sus +", text, StringComparison.Ordinal);
        Assert.Contains(" ADU; peste 0.15 si peste 3 x eroarea la sus", text, StringComparison.Ordinal);
        Assert.True(top.ErrorAdu > 0 && top.ErrorAdu < 0.4, $"error {top.ErrorAdu}");
        Assert.Contains("  amplit.  lumina  latura  >20   [G1;", DarkReport.FrameTableHeader(r), StringComparison.Ordinal);
        Assert.Contains("latura = sus - centru dupa bias (glow inclus), ADU", DarkReport.FrameTableNote(r), StringComparison.Ordinal);
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
        Assert.Contains("; niciuna peste 0.15 si peste 3 x eroarea; o lumina uniforma pe tot cadrul, in toate cadrele, nu are reper", swr.ToString(), StringComparison.Ordinal);
        Assert.Contains("; nicio latura cu lumina constanta peste 0.15 si 3 x eroarea", swr.ToString(), StringComparison.Ordinal);
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
        Assert.Contains("    lumina constanta   : fara bias nu se desparte de offsetul de citire: constant sus +", swn.ToString(), StringComparison.Ordinal);
        Assert.Contains("latura = sus - centru (fara bias, glow inclus), ADU", DarkReport.FrameTableNote(rn), StringComparison.Ordinal);

        // under six frames: unmeasured, like the split
        using SessionFixture few = new SessionFixture().WithDarkSeries(4, Plain, biases: 3);
        DarkSeriesResult rf = Run(few);
        Assert.Empty(rf.Verdicts.Leak.Sides);
        Assert.Null(rf.Verdicts.Leak.WorstSide);
        Assert.Equal("-", DarkReport.FrameRows(rf)[0].Side);
        Assert.Contains("latura = latura cu cea mai multa lumina constanta - centru (NEMASURAT)", DarkReport.FrameTableNote(rf), StringComparison.Ordinal);
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
        Assert.Contains("    lumina constanta   : NEMASURAT: prea putine cadre ramase dupa cele luminate (4, sub 5)", sw.ToString(), StringComparison.Ordinal);
        Assert.Contains("    glow / constant    : la ", sw.ToString(), StringComparison.Ordinal);
        Assert.Equal("-", DarkReport.FrameRows(r2)[0].Side);
    }

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
        Assert.Contains("    glow / constant    : NEMASURAT: seria s-a incalzit prea putin ca sa desparta glow-ul de constant (raportul de amplitudine se intinde pe 0.0", sw.ToString(), StringComparison.Ordinal);
        Assert.Contains("; peste 1.0: scurgere de lumina sau glow", sw.ToString(), StringComparison.Ordinal);
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
        Assert.Contains("=== DARK-URI: 1 cadru decodat din 1", sw.ToString(), StringComparison.Ordinal);
        Assert.Contains("sigma rezidual     : NEMASURAT: un singur cadru", sw.ToString(), StringComparison.Ordinal);
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
        Assert.Contains("bias-uri sarite    : 3 din 5 (plafonul de cadre; folosite 2)", sw.ToString(), StringComparison.Ordinal);
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
        Assert.Contains("ATENTIE            : 1 bias nu s-a putut decoda (primul: bias_0004.fit: ", sw.ToString(), StringComparison.Ordinal);
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
        Assert.Contains("valori in ADU, alb presupus 65535", adu.ScaleSource, StringComparison.Ordinal);
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
        Assert.Contains("sarit (peste 2 cadre, se decodeaza unul din 3)", stack.Frames[1].Error, StringComparison.Ordinal);
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
