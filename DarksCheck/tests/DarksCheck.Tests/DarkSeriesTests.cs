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
