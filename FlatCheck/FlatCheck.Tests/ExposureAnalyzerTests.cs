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

namespace FlatCheck.Tests;

/// <summary>
/// Phase 6: mode 4.
///
/// The bands are dialled in exactly: <see cref="TestImages.FlatCfa"/> holds the brightest
/// channel at a constant level, and passing --sat makes the reported percentage exactly
/// level/sat. So "55%" in a test really is 55%, and the boundaries can be probed on both
/// sides rather than approximately.
/// </summary>
public class ExposureAnalyzerTests
{
    const int N = 128;
    const double Sat = 1000;

    /// <summary>A raw-looking CFA frame whose centre reads exactly <paramref name="pct"/>%.</summary>
    static ExposureResult AtLevel(double pct, bool sky = false, double? black = null)
        => ExposureAnalyzer.Analyze(TestImages.FlatCfa(N, N, Sat * pct / 100.0), Sat, black, sky);

    // ---------------------------------------------------------------- the core rationale

    /// <summary>
    /// The whole reason the mode measures a central window: a flat has a wide distribution
    /// by definition - that IS the vignetting - so the global median sits below the centre.
    /// Follow the global median and you overexpose until the centre clips.
    /// </summary>
    [Fact]
    public void OnAVignettedFrame_TheCentreReadsHigherThanTheGlobalMedian()
    {
        var vignetted = FitsTestData.WithBayerMosaic(FitsTestData.Radial(256, 256, center: 34000));

        var r = ExposureAnalyzer.Analyze(TestImages.From(256, 256, vignetted), null, null, false);

        Assert.True(r.CentreMedianPct > r.GlobalMedianPct,
            "centre " + r.CentreMedianPct + " vs global " + r.GlobalMedianPct);
    }

    [Fact]
    public void PicksTheBrightestBayerPositionAsTheLimitingChannel()
    {
        var mosaic = FitsTestData.WithBayerMosaic(FitsTestData.Radial(256, 256, center: 34000));

        var r = ExposureAnalyzer.Analyze(TestImages.From(256, 256, mosaic), null, null, false);

        Assert.True(r.IsCfa);
        Assert.Equal(1, r.BrightestChannel);          // the first of the two "green" positions
        Assert.Equal(r.ChannelMedians.Max(), r.ChannelMedians[r.BrightestChannel], 9);
    }

    // ---------------------------------------------------------------- panel bands

    [Theory]
    [InlineData(20, nameof(ExposureVerdict.TooLow))]
    [InlineData(29, nameof(ExposureVerdict.TooLow))]
    [InlineData(30, nameof(ExposureVerdict.AcceptableLow))]   // boundary: not < 30
    [InlineData(45, nameof(ExposureVerdict.AcceptableLow))]
    [InlineData(50, nameof(ExposureVerdict.Ok))]              // boundary: not < 50
    [InlineData(57, nameof(ExposureVerdict.Ok))]
    [InlineData(65, nameof(ExposureVerdict.Ok))]              // boundary: <= 65
    [InlineData(66, nameof(ExposureVerdict.AcceptableHigh))]
    [InlineData(80, nameof(ExposureVerdict.AcceptableHigh))]  // boundary: not > 80
    [InlineData(81, nameof(ExposureVerdict.TooHigh))]
    public void PanelBands(double pct, string expected)
    {
        var r = AtLevel(pct);

        Assert.Equal(pct, r.CentreMedianPct, 6);
        Assert.Equal(expected, r.Verdict.ToString());
    }

    // ---------------------------------------------------------------- sky bands

    /// <summary>
    /// Sky flats are supposed to sit modestly - the blue-hour sky darkens continuously and
    /// is full of faint stars, so master SNR comes from the number of frames. Without
    /// --sky a sky flat at 22% would be wrongly reported as underexposed.
    /// </summary>
    [Theory]
    [InlineData(10, nameof(ExposureVerdict.TooLow))]
    [InlineData(14, nameof(ExposureVerdict.TooLow))]
    [InlineData(15, nameof(ExposureVerdict.Ok))]              // boundary: not < 15
    [InlineData(22, nameof(ExposureVerdict.Ok))]              // the case the flag exists for
    [InlineData(50, nameof(ExposureVerdict.Ok))]              // boundary: <= 50
    [InlineData(51, nameof(ExposureVerdict.AcceptableHigh))]
    [InlineData(60, nameof(ExposureVerdict.AcceptableHigh))]  // boundary: not > 60
    [InlineData(61, nameof(ExposureVerdict.TooHigh))]
    public void SkyBands(double pct, string expected)
    {
        var r = AtLevel(pct, sky: true);

        Assert.True(r.IsSky);
        Assert.Equal(expected, r.Verdict.ToString());
    }

    [Fact]
    public void TheSameFrameReadsDifferentlyWithAndWithoutSky()
    {
        Assert.Equal(ExposureVerdict.TooLow, AtLevel(22).Verdict);
        Assert.Equal(ExposureVerdict.Ok, AtLevel(22, sky: true).Verdict);
    }

    [Fact]
    public void SkyHasNoAcceptableLowBand()
    {
        // 15-50 is simply OK; there is no "a bit low but fine" for a sky flat
        foreach (double pct in new double[] { 15, 20, 30, 40, 50 })
            Assert.Equal(ExposureVerdict.Ok, AtLevel(pct, sky: true).Verdict);
    }

    // ---------------------------------------------------------------- clipping

    /// <summary>
    /// Saturates <paramref name="count"/> pixels inside the exact window the analyzer
    /// samples: the brightest Bayer position (even row, odd column), within the central
    /// 20% of that channel's sub-image. The window holds 169 pixels here, so even one
    /// saturated pixel clears the 0.05% clipping threshold.
    /// </summary>
    static void SaturateCentre(Img img, int count)
    {
        int half = N / 2;
        int lo = (int)(half * 0.4), hi = (int)(half * 0.6);
        int done = 0;
        for (int y = lo; y < hi && done < count; y++)
            for (int x = lo; x < hi && done < count; x++, done++)
                img.Set(y * 2, x * 2 + 1, Sat);
    }

    [Fact]
    public void ClippingInTheCentre_ForcesTooHigh_EvenInsideTheTargetBand()
    {
        // the centre reads a healthy 55%, but a few pixels there are already saturated
        var img = TestImages.FlatCfa(N, N, Sat * 0.55);
        SaturateCentre(img, 5);

        var r = ExposureAnalyzer.Analyze(img, Sat, null, false);

        Assert.Equal(55, r.CentreMedianPct, 6);          // the median has not moved
        Assert.True(r.CentreClipPct > Th.ExpoClip, "clip was " + r.CentreClipPct);
        Assert.Equal(ExposureVerdict.TooHigh, r.Verdict);
    }

    [Fact]
    public void ClippingAlsoForcesTooHighForSkyFlats()
    {
        // 30% would be squarely OK for a sky flat; the clipping overrides that
        var img = TestImages.FlatCfa(N, N, Sat * 0.30);
        SaturateCentre(img, 5);

        var r = ExposureAnalyzer.Analyze(img, Sat, null, true);

        Assert.Equal(ExposureVerdict.Ok, AtLevel(30, sky: true).Verdict);
        Assert.Equal(ExposureVerdict.TooHigh, r.Verdict);
    }

    [Fact]
    public void WithoutClipping_TheCentreLevelAloneDecides()
    {
        var img = TestImages.FlatCfa(N, N, Sat * 0.55);

        var r = ExposureAnalyzer.Analyze(img, Sat, null, false);

        Assert.Equal(0, r.CentreClipPct, 9);
        Assert.Equal(ExposureVerdict.Ok, r.Verdict);
    }

    // ---------------------------------------------------------------- sanity gate

    [Fact]
    public void ADebayeredFrame_IsRefusedBecauseTheCfaPositionsMatch()
    {
        var img = TestImages.From(N, N, FitsTestData.Radial(N, N, center: 30000));

        var r = ExposureAnalyzer.Analyze(img, null, null, false);

        Assert.True(r.LooksDebayered);
        Assert.False(r.LevelTooLow);
        Assert.False(r.IsRaw);
        Assert.Equal(ExposureVerdict.NotRaw, r.Verdict);
        Assert.True(r.CfaSpread < 1.02, "spread was " + r.CfaSpread);
    }

    [Fact]
    public void AnAlreadyCalibratedFrame_IsRefusedBecauseTheLevelIsNearZero()
    {
        // CFA mosaic intact (so it is not the debayer gate firing) but the level is gone
        var r = AtLevel(0.5);

        Assert.True(r.LevelTooLow);
        Assert.False(r.LooksDebayered);
        Assert.False(r.IsRaw);
        Assert.Equal(ExposureVerdict.NotRaw, r.Verdict);
    }

    [Fact]
    public void ARawCfaFrameAtASensibleLevel_PassesTheGate()
    {
        var r = AtLevel(55);

        Assert.True(r.IsRaw);
        Assert.False(r.LevelTooLow);
        Assert.False(r.LooksDebayered);
    }

    // ---------------------------------------------------------------- bands

    /// <summary>
    /// The bands exist as a value so the window can draw the ladder that actually votes.
    /// If these drift from Th, the chart would shade a band the verdict does not use.
    /// </summary>
    [Fact]
    public void ThePanelBandsAreTheThresholdsClassifyUses()
    {
        var b = ExposureBands.For(false);

        Assert.False(b.Sky);
        Assert.Equal(Th.ExpoTooLow, b.TooLow, 9);
        Assert.Equal(Th.ExpoLo, b.Lo, 9);
        Assert.Equal(Th.ExpoHi, b.Hi, 9);
        Assert.Equal(Th.ExpoTooHigh, b.TooHigh, 9);
        Assert.Equal(Th.ExpoTarget, b.Target, 9);
        Assert.True(b.HasLowShoulder);
    }

    /// <summary>
    /// On the sky ladder the target starts exactly at the too-low threshold, so there is
    /// no "acceptable but low" shoulder - which is the honest shape, not an oversight.
    /// </summary>
    [Fact]
    public void TheSkyBandsHaveNoLowShoulder()
    {
        var b = ExposureBands.For(true);

        Assert.True(b.Sky);
        Assert.Equal(Th.ExpoSkyLo, b.TooLow, 9);
        Assert.Equal(b.TooLow, b.Lo, 9);
        Assert.Equal(Th.ExpoSkyHi, b.Hi, 9);
        Assert.Equal(Th.ExpoSkyTooHigh, b.TooHigh, 9);
        Assert.Equal(Th.ExpoSkyTarget, b.Target, 9);
        Assert.False(b.HasLowShoulder);
    }

    [Fact]
    public void TheResultCarriesTheBandsItWasJudgedOn()
    {
        Assert.Equal(Th.ExpoLo, AtLevel(55).Bands.Lo, 9);
        Assert.Equal(Th.ExpoSkyLo, AtLevel(55, sky: true).Bands.Lo, 9);
    }

    // ---------------------------------------------------------------- histograms

    /// <summary>The bin ladder, probed at both ends and on a boundary.</summary>
    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.49, 0)]
    [InlineData(0.5, 1)]
    [InlineData(55.0, 110)]
    [InlineData(99.9, 199)]
    [InlineData(100.0, 199)]      // at white: the last bin, not out of range
    [InlineData(300.0, 199)]      // above white, e.g. a --sat below the real one
    [InlineData(-0.1, 0)]         // below black
    [InlineData(-500.0, 0)]
    public void BinOfClampsInsteadOfDroppingPixels(double pct, int expected)
        => Assert.Equal(expected, LevelHistogram.BinOf(pct));

    /// <summary>
    /// Nothing is dropped: each channel's histogram holds every pixel of that channel, and
    /// the centre one holds exactly the decision window - 13x13 = 169 on a 128px frame,
    /// the same window the clipping tests above rely on.
    /// </summary>
    [Fact]
    public void EveryChannelHistogramCountsEveryPixelOfThatChannel()
    {
        var r = AtLevel(55);

        Assert.Equal(4, r.Channels.Length);
        foreach (var c in r.Channels)
        {
            Assert.Equal(N * N / 4, c.Whole.Total);
            Assert.Equal(N * N / 4, c.Whole.Counts.Sum());
            Assert.Equal(169, c.Centre.Total);
            Assert.Equal(169, c.Centre.Counts.Sum());
        }
    }

    /// <summary>
    /// A channel held at one level lands in one bin. FlatCfa is constant per position and
    /// --sat makes the percentage exact, so this pins the axis: 55% really is bin 110.
    /// </summary>
    [Fact]
    public void AConstantChannelLandsInOneBin()
    {
        var r = AtLevel(55);
        var bright = r.Channels[r.BrightestChannel];

        Assert.Equal(1, bright.Whole.Counts.Count(n => n > 0));
        Assert.Equal(N * N / 4, bright.Whole.Counts[LevelHistogram.BinOf(55)]);
    }

    /// <summary>
    /// A saturated tail must stay visible rather than fall off the axis - it is the one
    /// thing that overrides the median. Here --sat sits below the real level, so every
    /// pixel of the brightest channel reads above white.
    /// </summary>
    [Fact]
    public void LevelsAtOrAboveWhiteLandInTheLastBin()
    {
        var r = ExposureAnalyzer.Analyze(TestImages.FlatCfa(N, N, 2000), Sat, null, false);
        var bright = r.Channels[r.BrightestChannel];

        Assert.Equal(N * N / 4, bright.Whole.Counts[LevelHistogram.Bins - 1]);
    }

    /// <summary>The other end: a black level above the data must not produce a negative index.</summary>
    [Fact]
    public void LevelsBelowBlackLandInTheFirstBin()
    {
        var r = ExposureAnalyzer.Analyze(TestImages.FlatCfa(N, N, 500), 1000, 600, false);

        Assert.Equal(N * N / 4, r.Channels[0].Whole.Counts[0]);
    }

    /// <summary>
    /// The claim the whole card rests on: the centre curve of the brightest channel is
    /// the very window the verdict is read from. If these two ever differ, the chart is
    /// explaining a different measurement than the one on screen.
    /// </summary>
    [Fact]
    public void TheCentreOfTheBrightestChannelIsTheDecisionWindow()
    {
        var r = ExposureAnalyzer.Analyze(
            TestImages.From(256, 256, FitsTestData.WithBayerMosaic(
                FitsTestData.Radial(256, 256, center: 34000))), null, null, false);

        Assert.Equal(r.CentreMedianPct, r.Channels[r.BrightestChannel].CentreMedianPct, 12);
        Assert.Equal(r.GlobalMedianPct, r.Channels[r.BrightestChannel].MedianPct, 12);
        Assert.Single(r.Channels.Where(c => c.IsBrightest));
    }

    /// <summary>
    /// The card's own argument, as a number: on a vignetted frame the whole-channel curve
    /// sits below the centre curve. That distance is vignetting, not underexposure.
    /// </summary>
    [Fact]
    public void TheWholeChannelCurveSitsBelowTheCentreCurve()
    {
        var r = ExposureAnalyzer.Analyze(
            TestImages.From(256, 256, FitsTestData.WithBayerMosaic(
                FitsTestData.Radial(256, 256, center: 34000))), null, null, false);

        foreach (var c in r.Channels)
            Assert.True(c.MedianPct < c.CentreMedianPct,
                $"{c.Label}: global {c.MedianPct} vs centru {c.CentreMedianPct}");
    }

    /// <summary>A mono/processed frame has no mosaic left, so it gets one unnamed channel.</summary>
    [Fact]
    public void OnAMonoFrameThereIsOneChannel()
    {
        var r = ExposureAnalyzer.Analyze(
            TestImages.From(N, N, FitsTestData.Radial(N, N, center: 30000)), null, null, false);

        Assert.False(r.IsCfa);
        var only = Assert.Single(r.Channels);
        Assert.Equal("mono", only.Label);
        Assert.Equal('\0', only.Colour);
        Assert.Equal(N * N, only.Whole.Total);
        Assert.Equal(r.CentreMedianPct, only.CentreMedianPct, 12);
        Assert.Equal(r.GlobalMedianPct, only.MedianPct, 12);
    }

    // ---------------------------------------------------------------- channel names

    /// <summary>
    /// With the order read off the file the positions get their colours. The two greens
    /// are numbered, because on a correct mosaic they sit at almost the same level and
    /// "almost" is what the histogram is there to show.
    /// </summary>
    [Fact]
    public void WithAKnownOrderTheChannelsAreNamedByColour()
    {
        var r = ExposureAnalyzer.Analyze(TestImages.FlatCfa(N, N, 550), Sat, null, false,
                                         null, CfaPattern.Parse("RGGB", "BAYERPAT"));

        Assert.Equal(new[] { "R", "G1", "G2", "B" }, r.Channels.Select(c => c.Label));
        Assert.Equal(new[] { 'R', 'G', 'G', 'B' }, r.Channels.Select(c => c.Colour));
        Assert.Equal("G1", r.Channels[r.BrightestChannel].Label);
    }

    /// <summary>
    /// Without it they stay positions. Naming them anyway would be a guess wearing the
    /// clothes of a measurement - the order differs between cameras.
    /// </summary>
    [Fact]
    public void WithoutAKnownOrderTheChannelsStayPositions()
    {
        var r = AtLevel(55);

        Assert.Null(r.Cfa);
        Assert.Equal(new[] { "CFA[0,0]", "CFA[0,1]", "CFA[1,0]", "CFA[1,1]" },
                     r.Channels.Select(c => c.Label));
        Assert.All(r.Channels, c => Assert.Equal('\0', c.Colour));
    }

    /// <summary>A different order relabels the same pixels; no figure moves.</summary>
    [Fact]
    public void TheOrderChangesOnlyTheNames()
    {
        var img = TestImages.FlatCfa(N, N, 550);

        var plain = ExposureAnalyzer.Analyze(img, Sat, null, false);
        var named = ExposureAnalyzer.Analyze(img, Sat, null, false, null,
                                             CfaPattern.Parse("BGGR", "BAYERPAT"));

        Assert.Equal(plain.CentreMedianPct, named.CentreMedianPct, 12);
        Assert.Equal(plain.Verdict, named.Verdict);
        Assert.Equal(new[] { "B", "G1", "G2", "R" }, named.Channels.Select(c => c.Label));
    }

    // ---------------------------------------------------------------- scale

    [Fact]
    public void SatOverride_RescalesThePercentage()
    {
        var img = TestImages.FlatCfa(N, N, 2000);

        var wide = ExposureAnalyzer.Analyze(img, 4000, null, false);
        var narrow = ExposureAnalyzer.Analyze(img, 2500, null, false);

        Assert.Equal(50, wide.CentreMedianPct, 6);
        Assert.Equal(80, narrow.CentreMedianPct, 6);
        Assert.Equal("--sat=4000", wide.ScaleSource);
    }

    [Fact]
    public void BlackOverride_ShiftsTheZeroPoint()
    {
        var img = TestImages.FlatCfa(N, N, 2000);

        // (2000 - 500) / (4000 - 500) = 42.857%
        var r = ExposureAnalyzer.Analyze(img, 4000, 500, false);

        Assert.Equal(42.857, r.CentreMedianPct, 3);
        Assert.Equal(500, r.Black, 6);
        Assert.Equal(3500, r.Range, 6);
    }

    [Fact]
    public void WithoutAnOverride_TheScaleSourceSaysItWasAutoDetected()
    {
        var r = ExposureAnalyzer.Analyze(TestImages.FlatCfa(N, N, 2000), null, null, false);

        Assert.Contains("auto-detectat", r.ScaleSource);
        Assert.Equal(0, r.Black, 6);
    }

    /// <summary>The white-level ladder, probed on both sides of each step.</summary>
    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.5, 1.0)]        // boundary: <= 1.5
    [InlineData(1.6, 4095.0)]
    [InlineData(4300.0, 4095.0)]  // boundary: <= 4096 * 1.05 = 4300.8
    [InlineData(4301.0, 16383.0)]
    [InlineData(17203.0, 16383.0)] // boundary: <= 16384 * 1.05 = 17203.2
    [InlineData(17204.0, 65535.0)]
    [InlineData(60000.0, 65535.0)]
    public void AutoWhiteLadder(double max, double expected)
    {
        Assert.Equal(expected, ExposureAnalyzer.AutoWhite(max), 6);
    }

    // ------------------------------------------------ scale read from the file (raws)

    /// <summary>
    /// A camera raw carries the real white and black levels, so the guess is skipped
    /// entirely. That is the whole point of reading NEFs directly: TestImages.FlatCfa
    /// peaks at 2000, which AutoWhite would round up to 4095 - the file says 3827, and
    /// 2000/3827 is a different number from 2000/4095.
    /// </summary>
    [Fact]
    public void AScaleReadFromTheFileBeatsTheGuess()
    {
        var img = TestImages.FlatCfa(N, N, 2000);
        var scale = new ExposureScale(3827, 150, "libraw (exact)");

        var r = ExposureAnalyzer.Analyze(img, null, null, false, scale);

        Assert.Equal(3827, r.White, 6);
        Assert.Equal(150, r.Black, 6);
        Assert.Equal("libraw (exact)", r.ScaleSource);
        Assert.Equal((2000 - 150) / (3827 - 150.0) * 100, r.CentreMedianPct, 6);
    }

    /// <summary>
    /// The precedence is the one flatcheck.py uses: --sat beats the file, and it also
    /// takes over the source line, because the reported scale is no longer the file's.
    /// The black level is overridden separately, so an unaccompanied --sat leaves the
    /// file's black in place.
    /// </summary>
    [Fact]
    public void SatOverridesTheFileScaleButLeavesItsBlackAlone()
    {
        var scale = new ExposureScale(3827, 150, "libraw (exact)");

        var r = ExposureAnalyzer.Analyze(TestImages.FlatCfa(N, N, 2000), 4095, null, false, scale);

        Assert.Equal(4095, r.White, 6);
        Assert.Equal(150, r.Black, 6);
        Assert.Equal("--sat=4095", r.ScaleSource);
    }

    [Fact]
    public void BlackOverridesTheFileScaleWithoutTouchingItsWhite()
    {
        var scale = new ExposureScale(3827, 150, "libraw (exact)");

        var r = ExposureAnalyzer.Analyze(TestImages.FlatCfa(N, N, 2000), null, 0, false, scale);

        Assert.Equal(3827, r.White, 6);
        Assert.Equal(0, r.Black, 6);
        Assert.Equal("libraw (exact)", r.ScaleSource);
    }

    /// <summary>The FITS path is untouched: no file scale means the ladder still runs.</summary>
    [Fact]
    public void WithoutAFileScale_TheGuessStillRuns()
    {
        var r = ExposureAnalyzer.Analyze(TestImages.FlatCfa(N, N, 2000), null, null, false, null);

        Assert.Equal(4095, r.White, 6);
        Assert.Contains("auto-detectat", r.ScaleSource);
    }

    /// <summary>
    /// The claim the whole raw feature rests on: reading a NEF changes only *where the
    /// scale comes from*, never how anything is computed. Same pixels, same white, same
    /// black - so every field of the result must match, whether the levels arrived from
    /// libraw or were typed on the command line as --sat 4095 --black 150.
    ///
    /// If these two ever diverge, one of the formats has grown its own arithmetic and the
    /// mode no longer means the same thing depending on what you feed it.
    /// </summary>
    [Fact]
    public void ReadingTheScaleFromARawComputesExactlyWhatSatAndBlackDo()
    {
        var img = FitsTestData.WithBayerMosaic(FitsTestData.Radial(256, 256, center: 3600));

        var fromRaw = ExposureAnalyzer.Analyze(TestImages.From(256, 256, img), null, null, false,
                                               new ExposureScale(4095, 150, "libraw (exact)"));
        var fromFlags = ExposureAnalyzer.Analyze(TestImages.From(256, 256, img), 4095, 150, false);

        Assert.Equal(fromFlags.White, fromRaw.White, 12);
        Assert.Equal(fromFlags.Black, fromRaw.Black, 12);
        Assert.Equal(fromFlags.Range, fromRaw.Range, 12);
        Assert.Equal(fromFlags.ChannelMedians, fromRaw.ChannelMedians);
        Assert.Equal(fromFlags.CfaSpread, fromRaw.CfaSpread, 12);
        Assert.Equal(fromFlags.IsCfa, fromRaw.IsCfa);
        Assert.Equal(fromFlags.BrightestChannel, fromRaw.BrightestChannel);
        Assert.Equal(fromFlags.CentreMedianPct, fromRaw.CentreMedianPct, 12);
        Assert.Equal(fromFlags.GlobalMedianPct, fromRaw.GlobalMedianPct, 12);
        Assert.Equal(fromFlags.CentreClipPct, fromRaw.CentreClipPct, 12);
        Assert.Equal(fromFlags.LevelTooLow, fromRaw.LevelTooLow);
        Assert.Equal(fromFlags.LooksDebayered, fromRaw.LooksDebayered);
        Assert.Equal(fromFlags.IsRaw, fromRaw.IsRaw);
        Assert.Equal(fromFlags.Verdict, fromRaw.Verdict);

        // ...including everything the histogram card shows, which is the same claim one
        // layer down: the chart must not depend on which container the pixels arrived in.
        for (int i = 0; i < fromFlags.Channels.Length; i++)
        {
            Assert.Equal(fromFlags.Channels[i].Whole.Counts, fromRaw.Channels[i].Whole.Counts);
            Assert.Equal(fromFlags.Channels[i].Centre.Counts, fromRaw.Channels[i].Centre.Counts);
            Assert.Equal(fromFlags.Channels[i].P99Pct, fromRaw.Channels[i].P99Pct, 12);
            Assert.Equal(fromFlags.Channels[i].ClipPct, fromRaw.Channels[i].ClipPct, 12);
            Assert.Equal(fromFlags.Channels[i].CentreMedianPct, fromRaw.Channels[i].CentreMedianPct, 12);
            Assert.Equal(fromFlags.Channels[i].CentreP1Pct, fromRaw.Channels[i].CentreP1Pct, 12);
            Assert.Equal(fromFlags.Channels[i].CentreP99Pct, fromRaw.Channels[i].CentreP99Pct, 12);
        }

        // The one field that is *allowed* to differ, and must: the report has to say where
        // the numbers came from, otherwise the user cannot tell an exact scale from a guess.
        Assert.Equal("libraw (exact)", fromRaw.ScaleSource);
        Assert.Equal("--sat=4095", fromFlags.ScaleSource);
    }

    /// <summary>
    /// The same equivalence through the sky bands, since those are a separate branch of
    /// Classify and could drift on their own.
    /// </summary>
    [Fact]
    public void TheEquivalenceHoldsInSkyModeToo()
    {
        var img = TestImages.FlatCfa(N, N, 1200);

        var fromRaw = ExposureAnalyzer.Analyze(img, null, null, true, new ExposureScale(4095, 150, "x"));
        var fromFlags = ExposureAnalyzer.Analyze(img, 4095, 150, true);

        Assert.Equal(fromFlags.CentreMedianPct, fromRaw.CentreMedianPct, 12);
        Assert.Equal(fromFlags.Verdict, fromRaw.Verdict);
    }
}
