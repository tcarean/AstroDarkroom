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
}
