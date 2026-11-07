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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>The classes, flags and trends on hand-made measurements (R-022, R-023).</summary>
public class SeriesAnalyzerTests
{
    private static FrameMeasurement Measurement(double fwhm, double elongation, int stars, double background, double halo = 0.02, int trails = 0, double coherence = 0.95)
    {
        ZoneShape z = new(fwhm, elongation, 10);
        List<ChannelLevel> ch = [new("R", 'R', 30, 0, 0, 5), new("G1", 'G', background, 0, 0, 10), new("G2", 'G', background, 0, 0, 10), new("B", 'B', 40, 0, 0, 6)];
        return new FrameMeasurement(4016, 6016, new AduScale(4095, 150, "t"), ch, background, background - 5, background + 5, background, [background, background, background, background], 12,
            stars, stars / 10, 0, [], fwhm, fwhm + 0.5, elongation, elongation + 0.2, elongation > 1.3 ? 0.9 : 0.1, elongation > 1.3 ? 88 : null, elongation > 1.3 ? coherence : null,
            z, z, z, halo, Enumerable.Repeat(new Trail(100, 300, 0, 0), trails).ToList(), []);
    }

    private static AnalyzedFrame Frame(int i, FrameMeasurement? m, string? error = null, double exposure = 120)
        => new(new FrameInfo(i, $"DSC_{i:0000}.NEF", "cam", 135, 2.8, exposure, 1600, SessionFixture.FirstStart.AddSeconds(i * 141.0), null, null, null, null, null, null, null, null, null, null, error), m,
            m is null ? null : new RegistrationResult(true, 300, 200, 0, 0, 0, 1, 0.3), error);

    /// <summary>n good frames 141 s apart (120 s exposures), the k-th registered at (dx * k, dy * k), with the dithering read from those positions.</summary>
    private static (AnalyzedFrame[] Frames, DitherAnalysis Dither) Drifting(int n, double dx, double dy)
    {
        AnalyzedFrame[] frames = Enumerable.Range(1, n).Select(i => Frame(i, Measurement(3.0, 1.1, 10000, 120))).ToArray();
        double?[] x = frames.Select((f, k) => (double?)(dx * k)).ToArray(), y = frames.Select((f, k) => (double?)(dy * k)).ToArray();
        Cadence c = CadenceAnalyzer.Analyze(frames.Select(f => f.Info).ToList());
        return (frames, DitherAnalyzer.Analyze(x, y, c.PauseSeconds));
    }

    /// <summary>12 frames drifting 1 px along Y per frame: 0.43 px/min, 0.85 px per 120 s exposure, a stretch of 11 frames within 10 px of the first, a dash of 8.5 px, INSUFICIENT (R-043).</summary>
    [Fact]
    public void R043_TheDriftOverOneExposureDrawsTheDashOfTheLongestRunAndTheVerdictReadsIt()
    {
        (AnalyzedFrame[] frames, DitherAnalysis d) = Drifting(12, 0, 1.0);
        Cadence c = CadenceAnalyzer.Analyze(frames.Select(f => f.Info).ToList());

        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, VeilWcs);

        Assert.Equal(1.0 / 2.35, s.DriftYPerMin!.Value, 0.01);
        Assert.Equal(0.851, s.DriftPerExposurePx!.Value, 0.01);
        Assert.Equal(Axis.Y, s.DriftAxis);
        Assert.Equal(120, s.MedianExposureSeconds);
        Assert.Equal(new FrameStretch(1, 11, 11), d.WalkingStretch);   // frames 1-11 within 10 px of frame 1
        Assert.Equal(8.51, s.WalkingStreakPx!.Value, 0.05);
        Assert.Equal(11, s.WalkingSharedFrames);   // the 11 frames of the one stretch, whose dash shows (R-084)
        Assert.Equal(11.0 / 12, s.WalkingSharedFraction, 0.001);
        Assert.Equal(WalkingNoise.Insufficient, s.WalkingNoise);
    }

    /// <summary>The same 12 frames drifting 0.1 px per frame: the dash is 0.9 px, under 3, so no frame counts as streaked, and the piled pattern alone gives Static (R-043, R-084).</summary>
    [Fact]
    public void R043_ADashUnderThreePixelsIsNoWalkingNoiseEvenWithoutDithering()
    {
        (AnalyzedFrame[] frames, DitherAnalysis d) = Drifting(12, 0.1, 0);
        Cadence c = CadenceAnalyzer.Analyze(frames.Select(f => f.Info).ToList());

        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);

        Assert.Equal(Axis.X, s.DriftAxis);
        Assert.Equal(new FrameStretch(1, 12, 12), d.WalkingStretch);
        Assert.InRange(s.WalkingStreakPx!.Value, 0.9, 1.0);
        Assert.Equal(0, s.WalkingSharedFrames);   // the dash of 0.9 px does not show (R-084)
        Assert.Equal(1.0, d.PatternSurviving!.Value, 0.001);   // all 12 frames within 1.1 px: the pattern piles, hence Static
        Assert.Equal(WalkingNoise.Static, s.WalkingNoise);
    }

    /// <summary>Two frames at one spot with a 4.5 px drift between them draw a dash of the drift per exposure: a pair counts, not only a run of three (R-043, the review of 2026-10-10).</summary>
    [Fact]
    public void R043_APairOfFramesAtOneSpotDrawsADashOfTheDriftBetweenThem()
    {
        AnalyzedFrame[] frames = Enumerable.Range(1, 6).Select(i => Frame(i, Measurement(3.0, 1.1, 10000, 120))).ToArray();
        Cadence c = CadenceAnalyzer.Analyze(frames.Select(f => f.Info).ToList());
        // Pairs 4.5 px apart along Y (under the 5 px move threshold), then a 30 px dither: no three frames within 10 px.
        double?[] x = [0, 0, 30, 30, 60, 60], y = [0, 4.5, 0, 4.5, 0, 4.5];
        DitherAnalysis d = DitherAnalyzer.Analyze(x, y, c.PauseSeconds);
        Assert.Empty(d.RunsLoose);
        Assert.Equal(new FrameStretch(1, 2, 2), d.WalkingStretch);

        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);
        Assert.Equal(4.5 / 2.35 * 2, s.DriftPerExposurePx!.Value, 0.01);   // the three 4.5 px steps over 2.35 min, times a 120 s exposure
        Assert.Equal(s.DriftPerExposurePx.Value, s.WalkingStreakPx!.Value, 0.001);
        Assert.Equal(6, s.WalkingSharedFrames);   // every pair's dash of 3.8 px shows: all six frames (R-084)
        Assert.Equal(WalkingNoise.Insufficient, s.WalkingNoise);
    }

    /// <summary>A short run in a well dithered session: 4 frames of 20 (20 %) share a streak of 5.1 px, under the 25 % from which it shows in the stack, and no pair sits on the same pixels (R-084, ADR 0039).</summary>
    [Fact]
    public void R084_AShortRunInAWellDitheredSessionIsOk()
    {
        (AnalyzedFrame[] frames, DitherAnalysis d, Cadence c) = RunThenDither(4);
        Assert.Equal([new FrameStretch(1, 4, 4)], d.WalkingStretches);

        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);

        Assert.Equal(2.0 / 2.35 * 2, s.DriftPerExposurePx!.Value, 0.01);   // the run's three 2 px steps over 2.35 min each, times a 120 s exposure
        Assert.Equal(5.1, s.WalkingStreakPx!.Value, 0.05);
        Assert.Equal(4, s.WalkingSharedFrames);
        Assert.Equal(0.2, s.WalkingSharedFraction, 0.001);
        Assert.Equal(1.0 / 20, d.PatternSurviving!.Value, 0.001);   // no pair under 1.5 px: the ideal
        Assert.Equal(WalkingNoise.Ok, s.WalkingNoise);
    }

    /// <summary>The same session with a run of 6 (30 %): the streak sits in too many frames for the rejection to drop it (R-084).</summary>
    [Fact]
    public void R084_ARunOfAQuarterOfTheFramesIsInsufficient()
    {
        (AnalyzedFrame[] frames, DitherAnalysis d, Cadence c) = RunThenDither(6);

        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);

        Assert.Equal(6, s.WalkingSharedFrames);
        Assert.Equal(0.3, s.WalkingSharedFraction, 0.001);
        Assert.Equal(WalkingNoise.Insufficient, s.WalkingNoise);
    }

    /// <summary>Ten pairs of frames on the same pixels, 30 px apart: no dash reaches 3 px, but 10 % of the pattern stays against 5 % ideal (over 1.5 times): the pattern piles in place, NO walking noise (R-084).</summary>
    [Fact]
    public void R084_PairsOnTheSamePixelsPileThePatternAndReadStatic()
    {
        AnalyzedFrame[] frames = Enumerable.Range(1, 20).Select(i => Frame(i, Measurement(3.0, 1.1, 10000, 120))).ToArray();
        Cadence c = CadenceAnalyzer.Analyze(frames.Select(f => f.Info).ToList());
        double?[] x = new double?[20], y = new double?[20];
        for (int i = 0; i < 20; i++)
        {
            x[i] = 30 * (i / 2);
            y[i] = 0.5 * (i % 2);
        }

        DitherAnalysis d = DitherAnalyzer.Analyze(x, y, c.PauseSeconds);
        Assert.Equal(10, d.WalkingStretches.Count);
        Assert.Equal(10, d.SharingPairs);
        Assert.Equal(0.1, d.PatternSurviving!.Value, 0.001);

        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);

        Assert.InRange(s.WalkingStreakPx!.Value, 0.4, 0.5);   // the ten 0.5 px steps read as a drift of 0.43 px per exposure
        Assert.Equal(0, s.WalkingSharedFrames);
        Assert.Equal(WalkingNoise.Static, s.WalkingNoise);
    }

    /// <summary>Four frames 141 s apart at 120 s but the last two at 0.2 s: a plain one, a trailed one, a trailed short one and a marginally trailed short one (R-085).</summary>
    internal static (AnalyzedFrame[] Frames, DitherAnalysis Dither, Cadence Cadence) ShortAndTrailed()
    {
        AnalyzedFrame[] frames =
        [
            Frame(1, Measurement(3.0, 1.1, 10000, 120)),
            Frame(2, Measurement(3.0, 1.7, 10000, 120)),
            Frame(3, Measurement(3.0, 1.7, 100, 1), exposure: 0.2),
            Frame(4, Measurement(3.0, 1.4, 100, 1), exposure: 0.2),
        ];
        Cadence c = CadenceAnalyzer.Analyze(frames.Select(f => f.Info).ToList());
        double?[] zeros = new double?[4];
        return (frames, DitherAnalyzer.Analyze(zeros, zeros, c.PauseSeconds), c);
    }

    /// <summary>20 frames 141 s apart: the first <paramref name="run"/> at one spot drifting 2 px along Y per frame, the rest 20 px apart along X.</summary>
    internal static (AnalyzedFrame[] Frames, DitherAnalysis Dither, Cadence Cadence) RunThenDither(int run)
    {
        AnalyzedFrame[] frames = Enumerable.Range(1, 20).Select(i => Frame(i, Measurement(3.0, 1.1, 10000, 120))).ToArray();
        Cadence c = CadenceAnalyzer.Analyze(frames.Select(f => f.Info).ToList());
        double?[] x = new double?[20], y = new double?[20];
        for (int i = 0; i < 20; i++)
        {
            x[i] = i < run ? 0 : 20 * (i - run + 1);
            y[i] = i < run ? 2 * i : 0;
        }

        return (frames, DitherAnalyzer.Analyze(x, y, c.PauseSeconds), c);
    }

    /// <summary>A session with no two frames in a row at one spot is OK whatever the drift; one without a registered pair is unknown (R-043, R-084).</summary>
    [Fact]
    public void R043_NoStretchIsOkAndTwoRegisteredFramesAreNeeded()
    {
        AnalyzedFrame[] frames = Enumerable.Range(1, 8).Select(i => Frame(i, Measurement(3.0, 1.1, 10000, 120))).ToArray();
        Cadence c = CadenceAnalyzer.Analyze(frames.Select(f => f.Info).ToList());
        // Every frame 15 px from every other, on both axes.
        double?[] x = [0, 15, 0, 15, 30, 45, 30, 45], y = [0, 15, 30, 45, 0, 15, 30, 45];
        DitherAnalysis d = DitherAnalyzer.Analyze(x, y, c.PauseSeconds);
        Assert.Empty(d.WalkingStretches);
        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);
        Assert.Equal(WalkingNoise.Ok, s.WalkingNoise);
        Assert.Equal(0, s.WalkingSharedFrames);
        Assert.Null(s.DriftPerExposurePx);      // every step was a move: no drift pair
        Assert.Null(d.WalkingStretch);          // no two consecutive frames within 10 px
        Assert.Null(s.WalkingStreakPx);

        DitherAnalysis none = DitherAnalyzer.Analyze(new double?[8], new double?[8], c.PauseSeconds);
        Assert.Equal(WalkingNoise.Unknown, SeriesAnalyzer.Analyze(frames, c, none, null, null).WalkingNoise);
    }

    /// <summary>The Veil plate solve's shape: RA along Y, east towards -Y of the solved frame, north towards +X (R-025, R-038).</summary>
    private static readonly Wcs VeilWcs = new(-0.00172, 0.00172, 0.0037, -1, 1, 0.0037);

    private static SeriesAnalysis Analyze(params AnalyzedFrame[] frames) => Analyze(null, frames);

    private static SeriesAnalysis Analyze(Wcs? wcs, params AnalyzedFrame[] frames)
    {
        List<FrameInfo> infos = frames.Select(f => f.Info).ToList();
        Cadence c = CadenceAnalyzer.Analyze(infos);
        double?[] zeros = new double?[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            zeros[i] = frames[i].Measurement is null ? null : 0;
        }

        return SeriesAnalyzer.Analyze(frames, c, DitherAnalyzer.Analyze(zeros, zeros, c.PauseSeconds), null, wcs);
    }

    /// <summary>The Veil measurement of R-038: stars shifted (+128.2, -142.2) px, the plate solves said the centre went 0.2431 deg west and 0.2208 deg south.</summary>
    [Fact]
    public void R038_AMoveMapsToTheSkyThroughTheWcsWithSirilsRowsFlipped()
    {
        AnalyzedFrame[] frames = [Frame(1, Measurement(3.0, 1.1, 10000, 120)), Frame(2, Measurement(3.0, 1.1, 10000, 120))];
        Cadence c = CadenceAnalyzer.Analyze(frames.Select(f => f.Info).ToList());
        DitherAnalysis d = DitherAnalyzer.Analyze([0, 128.2], [0, -142.2], c.PauseSeconds);
        Assert.True(d.IsMove[1]);

        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, VeilWcs);
        Assert.Equal(Axis.Y, s.RaAxis);
        Assert.Null(s.MountMoves[0]);
        MountMove m = Assert.IsType<MountMove>(s.MountMoves[1]);
        Assert.True(m.Signed);
        Assert.Equal(-142.2, m.RaPx, 1.0);     // west
        Assert.Equal(-128.2, m.DecPx, 1.0);    // south
        Assert.Equal("RA 142 px ← V, DEC 129 px ↑ S", SeriesReport.MoveCell(m, ArrowKeys.Default));   // 128.7 px through the matrix's 0.0037 shear

        // The same step with the axis from the trails only: the components without their sense.
        SeriesAnalysis u = SeriesAnalyzer.Analyze(frames, c, d, null, null);
        Assert.Null(u.MountMoves[1]);   // no axis at all: nothing
        Assert.Equal("-", SeriesReport.MoveCell(null, ArrowKeys.Default));
        Assert.Equal("RA 142 px, DEC 128 px", SeriesReport.MoveCell(new MountMove(142.2, 128.2, false), ArrowKeys.Default));
        Assert.Equal("RA 15 px ← V", SeriesReport.MoveCell(new MountMove(-15.3, 0.5, true), ArrowKeys.Default));   // DEC under Th.AxisStep left out
        Assert.Equal("RA 4 px → E, DEC 20 px ↓ N", SeriesReport.MoveCell(new MountMove(4.0, 20.0, true), ArrowKeys.Default));
    }

    /// <summary>FITS lights are stored as the solve is, so the same step reads the sense its y feeds the other way round, and the handedness with it (R-038, ADR 0023).</summary>
    [Fact]
    public void R038_FitsLightsAreNotFlippedAgainstTheSolve()
    {
        AnalyzedFrame[] frames = [Frame(1, Measurement(3.0, 1.1, 10000, 120)), Frame(2, Measurement(3.0, 1.1, 10000, 120))];
        Cadence c = CadenceAnalyzer.Analyze(frames.Select(f => f.Info).ToList());
        DitherAnalysis d = DitherAnalyzer.Analyze([0, 128.2], [0, -142.2], c.PauseSeconds);
        Wcs fits = VeilWcs with { FramesFlipped = false };

        MountMove m = Assert.IsType<MountMove>(SeriesAnalyzer.Analyze(frames, c, d, null, fits).MountMoves[1]);
        Assert.Equal(142.2, m.RaPx, 1.0);      // RA runs along Y on this solve: east, where the raws read west
        Assert.Equal(-128.2, m.DecPx, 1.0);    // DEC runs along X: south, as for the raws
        Assert.Equal(-VeilWcs.Handedness, fits.Handedness);
        Assert.True(VeilWcs.FramesFlipped);    // the default is the raws' case, the measured one
    }

    /// <summary>The arrows are symbols; right is east and down is north by default, the arrows of a Star Adventurer GTi with SynScan Pro; the setting swaps them (R-040, ADR 0041).</summary>
    [Fact]
    public void R040_TheArrowsAreSymbolsFromTheKeysRightIsEastAndDownIsNorthByDefault()
    {
        Assert.Equal(new ArrowKeys(Arrow.Right, Arrow.Down), ArrowKeys.Default);
        Assert.Equal(Arrow.Left, ArrowKeys.Default.West);
        Assert.Equal(Arrow.Up, ArrowKeys.Default.South);
        Assert.Equal("←", ArrowKeys.Symbol(Arrow.Left));
        Assert.Equal("→", ArrowKeys.Symbol(Arrow.Right));
        Assert.Equal("↑", ArrowKeys.Symbol(Arrow.Up));
        Assert.Equal("↓", ArrowKeys.Symbol(Arrow.Down));

        MountMove both = new(-15.3, 12.0, true);
        ArrowKeys swapped = new(Arrow.Left, Arrow.Up);
        Assert.Equal(Arrow.Right, swapped.West);
        Assert.Equal(Arrow.Down, swapped.South);
        Assert.Equal("RA 15 px → V, DEC 12 px ↑ N", SeriesReport.MoveCell(both, swapped));   // north is the up arrow under these keys
        Assert.Equal("→, ↑", SeriesReport.MoveArrows(both, swapped));
        Assert.Equal("RA 15 px, DEC 12 px", SeriesReport.MoveCell(new MountMove(15.3, 12.0, false), swapped));   // unsigned: no arrow, whatever the keys
    }

    [Fact]
    public void R038_TheWcsSignsAndTheRaAxisComeFromTheMatrix()
    {
        Assert.Equal(Axis.Y, VeilWcs.RaAxis);
        Assert.Equal(Axis.X, new Wcs(-0.00172, 0.00172, 1, 0.003, 0.003, 1).RaAxis);
        (double ra, double dec) = VeilWcs.SkyOffsetDeg(0, -100);   // +100 px up the solved frame
        Assert.Equal(-0.172, ra, 0.001);
        Assert.Equal(0, dec, 0.001);
        Assert.Equal(0.00172, VeilWcs.RaScaleDeg, 0.00001);
        Assert.Equal(0.00172, VeilWcs.DecScaleDeg, 0.00001);

        // The same solve written as a CD matrix (unit scales, the matrix holds them): the same pixels and senses.
        Wcs cd = new(1, 1, -0.00172 * 0.0037, 0.00172, 0.00172, 0.00172 * 0.0037);
        Assert.Equal(Axis.Y, cd.RaAxis);
        Assert.Equal(0.00172, cd.RaScaleDeg, 0.00001);
        Assert.Equal(0.00172, cd.DecScaleDeg, 0.00001);
        (double cdRa, double cdDec) = cd.SkyOffsetDeg(0, -100);
        Assert.Equal(ra, cdRa, 0.0001);
        Assert.Equal(dec, cdDec, 0.0001);
        Assert.Equal(-100, cdRa / cd.RaScaleDeg, 0.1);

        // A move on both axes words both, in a cell and as arrows, through one rule.
        MountMove both = new(-15.3, 12.0, true);
        Assert.Equal("RA 15 px ← V, DEC 12 px ↓ N", SeriesReport.MoveCell(both, ArrowKeys.Default));
        Assert.Equal("←, ↓", SeriesReport.MoveArrows(both, ArrowKeys.Default));
        Assert.Equal("", SeriesReport.MoveArrows(new MountMove(15.3, 12.0, false), ArrowKeys.Default));
    }

    [Fact]
    public void R036_TheAxisWordsFollowTheRaAxisAndStayPlainWithoutOne()
    {
        SeriesAnalysis s = Analyze(Frame(1, Measurement(3.0, 1.1, 10000, 120)));
        Assert.Equal(Axis.Unknown, s.RaAxis);
        Assert.Equal(("X", "Y"), SeriesReport.AxisLabels(s));
        Assert.Equal(("dX", "dY"), SeriesReport.PositionHeaders(s));
        Assert.Equal(("X = RA", "Y = DEC"), SeriesReport.AxisLabels(s with { RaAxis = Axis.X }));
        Assert.Equal(("dX (RA)", "dY (DEC)"), SeriesReport.PositionHeaders(s with { RaAxis = Axis.X }));
        Assert.Equal(("dX (DEC)", "dY (RA)"), SeriesReport.PositionHeaders(s with { RaAxis = Axis.Y }));
    }

    [Fact]
    public void R033_TheTrailAxisIsTheImageAxisWithinTheTolerance()
    {
        Assert.Equal(TrailAxis.Ra, SeriesAnalyzer.TrailAxisOf(-88, Axis.Y));
        Assert.Equal(TrailAxis.Ra, SeriesAnalyzer.TrailAxisOf(70, Axis.Y));       // 20 degrees from vertical, within 25
        Assert.Equal(TrailAxis.Dec, SeriesAnalyzer.TrailAxisOf(10, Axis.Y));
        Assert.Equal(TrailAxis.Oblique, SeriesAnalyzer.TrailAxisOf(45, Axis.Y));
        Assert.Equal(TrailAxis.Ra, SeriesAnalyzer.TrailAxisOf(10, Axis.X));
        Assert.Equal(TrailAxis.Dec, SeriesAnalyzer.TrailAxisOf(-80, Axis.X));
        Assert.Equal(TrailAxis.Unknown, SeriesAnalyzer.TrailAxisOf(88, Axis.Unknown));
    }

    /// <summary>With the RA axis known, a trailed and a marginal frame name the axis; a frame whose stars disagree, or a session without an axis, gives the angle alone.</summary>
    [Fact]
    public void R033_TheFlagsCarryTheTrailAxisWhenTheStarsAgreeAndTheAxisIsKnown()
    {
        SeriesAnalysis s = Analyze(VeilWcs,
            Frame(1, Measurement(3.0, 1.1, 10000, 120)),
            Frame(2, Measurement(3.0, 1.7, 9000, 121)),                       // bad, coherent: `pe RA`
            Frame(3, Measurement(3.1, 1.4, 10000, 122)),                      // marginal, coherent: `pe RA`
            Frame(4, Measurement(3.0, 1.7, 9000, 121, coherence: 0.5)));      // bad, the stars disagree: angle alone
        Assert.Equal(TrailAxis.Ra, s.Flags[1][0].On);
        Assert.Equal("alungire 1.70 (de la 1.60 cadrul e slab), dara pe RA (88 de grade)", SeriesReport.Reason(s.Flags[1][0]));
        Assert.Equal(TrailAxis.Ra, s.Flags[2][0].On);
        Assert.Equal("alungire 1.40 (de la 1.35 cadrul e la limita), dara pe RA (88 de grade)", SeriesReport.Reason(s.Flags[2][0]));
        Assert.Equal(TrailAxis.Unknown, s.Flags[3][0].On);
        Assert.Equal("alungire 1.70 (de la 1.60 cadrul e slab), dara la 88 de grade", SeriesReport.Reason(s.Flags[3][0]));

        // One bad frame cannot set the axis from the trails (Th.TrailAxisMinFrames is 2): no axis, the angle alone.
        SeriesAnalysis u = Analyze(
            Frame(1, Measurement(3.0, 1.1, 10000, 120)),
            Frame(2, Measurement(3.0, 1.7, 9000, 121)),
            Frame(3, Measurement(3.1, 1.4, 10000, 122)));
        Assert.Equal(Axis.Unknown, u.RaAxis);
        Assert.Equal(TrailAxis.Unknown, u.TrailOn);
        Assert.Equal("alungire 1.70 (de la 1.60 cadrul e slab), dara la 88 de grade", SeriesReport.Reason(u.Flags[1][0]));
        Assert.Equal("alungire 1.40 (de la 1.35 cadrul e la limita), dara la 88 de grade", SeriesReport.Reason(u.Flags[2][0]));
    }

    [Fact]
    public void R022_EveryClassAndFlagHasItsRule()
    {
        AnalyzedFrame[] frames =
        [
            Frame(1, Measurement(3.0, 1.1, 10000, 120)),
            Frame(2, Measurement(3.0, 1.7, 9000, 121)),            // bad: trailed
            Frame(3, Measurement(3.1, 1.4, 10000, 122)),           // marginal: trail at the limit
            Frame(4, Measurement(4.5, 1.1, 10000, 123)),           // marginal: soft (> 1.3 x 3.0)
            Frame(5, Measurement(3.0, 1.1, 5000, 124)),            // marginal: clouds (< 60 % of 10000)
            Frame(6, Measurement(3.0, 1.1, 10000, 160)),           // good, with a background jump flag
            Frame(7, Measurement(3.0, 1.1, 10000, 125, trails: 1)),
            Frame(8, null, "stricat"),                              // unknown
            Frame(9, Measurement(3.0, 1.1, 10000, 126)),
        ];
        SeriesAnalysis s = Analyze(frames);

        Assert.Equal([FrameClass.Good, FrameClass.Bad, FrameClass.Marginal, FrameClass.Marginal, FrameClass.Marginal, FrameClass.Good, FrameClass.Good, FrameClass.Unknown, FrameClass.Good], s.Classes);
        Assert.Equal(FlagKind.Trailed, Assert.Single(s.Flags[1]).Kind);
        Assert.Equal(FlagKind.MarginalTrail, Assert.Single(s.Flags[2]).Kind);
        Assert.Equal(FlagKind.Soft, Assert.Single(s.Flags[3]).Kind);
        Assert.Equal(FlagKind.Clouds, Assert.Single(s.Flags[4]).Kind);
        Assert.Equal(FlagKind.BackgroundJump, Assert.Single(s.Flags[5]).Kind);
        Assert.InRange(s.Flags[5][0].Value, 25, 35);
        Assert.Equal(FlagKind.Trails, Assert.Single(s.Flags[6]).Kind);
        Assert.Equal(FlagKind.Unreadable, Assert.Single(s.Flags[7]).Kind);
        Assert.Equal("stricat", s.Flags[7][0].Detail);
        Assert.Equal(4, s.GoodCount);
        Assert.Equal(3, s.MarginalCount);
        Assert.Equal(1, s.BadCount);
        Assert.Equal(1, s.UnknownCount);
        Assert.Equal(3.0, s.SessionFwhm!.Value, 0.01);
        Assert.Equal(10000, s.SessionStars!.Value);
    }

    /// <summary>
    /// The session's usual exposure is the one most frames carry; a frame under half of it is bad
    /// with the short-exposure flag and gets neither the clouds nor the jump flag, and its stars
    /// stay out of the session's usual count; a frame with another exposure keeps its class with
    /// a note, and a background jump the exposure predicts carries the exposure detail (R-085).
    /// </summary>
    [Fact]
    public void R085_AFrameUnderHalfTheUsualExposureIsBadAndAnotherExposureIsNoted()
    {
        List<AnalyzedFrame> frames = [];
        for (int i = 1; i <= 10; i++)
        {
            frames.Add(Frame(i, Measurement(3.0, 1.1, 10000, i == 5 ? 160 : 120)));   // frame 5: a +33 % jump that no exposure explains
        }

        frames.Add(Frame(11, Measurement(3.0, 1.1, 100, 1), exposure: 0.2));           // bad: 0.2 s; its 100 stars and its -99 % background are the exposure's doing, and count for no neighbour
        frames.Add(Frame(12, Measurement(3.0, 1.1, 10000, 137), exposure: 137.5));     // good, noted: 137.5 s, and the +14 % background it predicts
        frames.Add(Frame(13, Measurement(3.0, 1.1, 10000, 120), exposure: 100));       // good, noted: 100 s, between half and the usual
        frames.Add(Frame(14, Measurement(3.0, 1.1, 10000, 120), exposure: 121));       // good, within 2 % of the usual: no note
        SeriesAnalysis s = Analyze(frames.ToArray());

        Assert.Equal(120, s.UsualExposureSeconds);
        Assert.Equal(FrameClass.Bad, s.Classes[10]);
        FrameFlag shortFlag = Assert.Single(s.Flags[10]);
        Assert.Equal(FlagKind.ShortExposure, shortFlag.Kind);
        Assert.Equal(0.2, shortFlag.Value);
        Assert.Equal(60, shortFlag.Threshold);
        Assert.Equal(10000, s.SessionStars!.Value);
        Assert.Equal(FrameClass.Good, s.Classes[11]);
        Assert.Equal([FlagKind.OtherExposure, FlagKind.BackgroundJump], s.Flags[11].Select(f => f.Kind));
        Assert.Equal(137.5, s.Flags[11][0].Value);
        Assert.Equal(120, s.Flags[11][0].Threshold);
        Assert.Equal(FrameFlag.ExposureDetail, s.Flags[11][1].Detail);
        Assert.Equal(FrameClass.Good, s.Classes[12]);
        Assert.Equal(FlagKind.OtherExposure, Assert.Single(s.Flags[12]).Kind);
        Assert.Empty(s.Flags[13]);
        Assert.Equal(FlagKind.BackgroundJump, Assert.Single(s.Flags[4]).Kind);
        Assert.Null(s.Flags[4][0].Detail);
        Assert.Equal(13, s.GoodCount);
        Assert.Equal(1, s.BadCount);
    }

    /// <summary>The usual exposure on a tie is the longer one, exactly half of it is not under it, and no frame with an exposure means no usual one and no flag (R-085).</summary>
    [Fact]
    public void R085_TheUsualExposureTakesTheLongerOnATieAndIsNullWithoutOne()
    {
        AnalyzedFrame[] frames = [Frame(1, Measurement(3.0, 1.1, 10000, 120), exposure: 60), Frame(2, Measurement(3.0, 1.1, 10000, 120), exposure: 120)];
        SeriesAnalysis s = Analyze(frames);
        Assert.Equal(120, s.UsualExposureSeconds);
        Assert.Equal(FlagKind.OtherExposure, Assert.Single(s.Flags[0]).Kind);   // 60 s is exactly half, not under it: noted, in the stack
        Assert.Equal(FrameClass.Good, s.Classes[0]);
        Assert.Empty(s.Flags[1]);

        SeriesAnalysis none = Analyze(Frame(1, Measurement(3.0, 1.1, 10000, 120), exposure: 0), Frame(2, Measurement(3.0, 1.1, 10000, 120), exposure: 0));
        Assert.Null(none.UsualExposureSeconds);
        Assert.All(none.Flags, f => Assert.Empty(f));
    }

    [Fact]
    public void R023_DewIsSuspectedWhenFwhmAndHaloRiseTogether()
    {
        List<AnalyzedFrame> frames = [];
        for (int i = 1; i <= 20; i++)
        {
            double late = i > 10 ? 1 : 0;
            frames.Add(Frame(i, Measurement(3.0 + 0.8 * late, 1.1, 10000, 120, halo: 0.02 + 0.05 * late)));
        }

        SeriesAnalysis s = Analyze(frames.ToArray());
        Assert.True(s.DewSuspected);
        Assert.False(s.FocusHeld);
        Assert.Equal(3.0, s.Fwhm.First!.Value, 0.01);
        Assert.Equal(3.8, s.Fwhm.Last!.Value, 0.01);
    }

    [Fact]
    public void R023_AStableNightKeepsFocusAndNoDew()
    {
        AnalyzedFrame[] frames = Enumerable.Range(1, 12).Select(i => Frame(i, Measurement(3.0, 1.1, 10000, 120 + i))).ToArray();
        SeriesAnalysis s = Analyze(frames);
        Assert.True(s.FocusHeld);
        Assert.False(s.DewSuspected);
        Assert.Null(s.RotationTotalDeg is double rt && rt != 0 ? rt : null);
        Assert.Equal(Axis.Unknown, s.RaAxis);
    }

    /// <summary>A 120 s frame starting at <paramref name="start"/>, registered with the rotation given, its stars good or trailed as the measurement says.</summary>
    private static AnalyzedFrame Timed(int i, DateTime start, double rotationDeg = 0, FrameMeasurement? m = null)
        => new(new FrameInfo(i, $"DSC_{i:0000}.NEF", "cam", 135, 2.8, 120, 1600, start, null, null, null, null, null, null, null, null, null, null, null),
            m ?? Measurement(3.0, 1.1, 10000, 120), new RegistrationResult(true, 300, 200, 0, 0, rotationDeg, 1, 0.3), null);

    /// <summary>The frames, cadence and dithering of a session whose frames start at the given seconds and sit at the given positions.</summary>
    private static (AnalyzedFrame[] Frames, Cadence Cadence, DitherAnalysis Dither) Session(double[] startSeconds, double[] x, double[] y, Func<int, AnalyzedFrame>? frame = null)
    {
        AnalyzedFrame[] frames = startSeconds.Select((s, k) => frame is null ? Timed(k + 1, SessionFixture.FirstStart.AddSeconds(s)) : frame(k)).ToArray();
        Cadence c = CadenceAnalyzer.Analyze(frames.Select(f => f.Info).ToList());
        return (frames, c, DitherAnalyzer.Analyze(x.Select(v => (double?)v).ToArray(), y.Select(v => (double?)v).ToArray(), c.PauseSeconds));
    }

    /// <summary>
    /// Four pairs shot back to back (121 s apart) with a 30 s pause and a 4 px DEC press between
    /// the pairs, the third pair jumping 8 px: the drift is the three quiet pairs' -2 px per 121 s
    /// along Y; the three presses, under the 5 px move rule, and the jump, a move with no pause,
    /// are not drift pairs (R-049, ADR 0016).
    /// </summary>
    [Fact]
    public void R049_TheDriftComesFromThePairsWithoutAPauseOrAMoveAndAPressUnderFivePixelsIsNotADriftPair()
    {
        double[] t = [0, 121, 271, 392, 542, 663, 813, 934];
        double[] x = [0, 0, 4, 4, 8, 8, 12, 12], y = [0, -2, -2, -4, -4, -12, -12, -14];
        (AnalyzedFrame[] frames, Cadence c, DitherAnalysis d) = Session(t, x, y);
        Assert.Equal(1, d.Moves);
        Assert.Equal(1, d.MovesWithoutPause);
        Assert.Equal(3, d.PausesWithoutMove);

        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);
        Assert.Equal(3, s.DriftPairs);
        Assert.False(s.DriftApproximate);
        Assert.Equal(-2 / (121 / 60.0), s.DriftYPerMin!.Value, 0.001);
        Assert.Equal(0, s.DriftXPerMin!.Value, 0.001);
        Assert.Equal(2 * 120 / 121.0, s.DriftPerExposurePx!.Value, 0.01);   // a magnitude
    }

    /// <summary>A session dithered every frame with presses under 5 px has no drift pair and falls back to the paused pairs, marked approximate; so does one with fewer than Th.DriftMinPairs clean pairs (R-049).</summary>
    [Fact]
    public void R049_WithoutThreeCleanPairsTheDriftFallsBackToThePausedPairsAndSaysSo()
    {
        (AnalyzedFrame[] frames, Cadence c, DitherAnalysis d) = Session([0, 150, 300, 450, 600], [0, 3, 6, 9, 12], [0, -1, -2, -3, -4]);
        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);
        Assert.Equal(4, s.DriftPairs);
        Assert.True(s.DriftApproximate);
        Assert.Equal(12 / 10.0, s.DriftXPerMin!.Value, 0.001);   // 12 px over four 150 s intervals
        Assert.Equal(WalkingNoise.Insufficient, s.WalkingNoise);   // the dash still reads the approximate drift

        // Two clean pairs and four paused ones: under the three clean pairs needed, all six count.
        (frames, c, d) = Session([0, 121, 271, 392, 542, 692, 842], [0, 0, 3, 3, 6, 9, 12], [0, -2, -2, -4, -4, -5, -6]);
        s = SeriesAnalyzer.Analyze(frames, c, d, null, null);
        Assert.Equal(6, s.DriftPairs);
        Assert.True(s.DriftApproximate);
        Assert.Equal(2, Th.DriftMinPairs - 1);

        // Two clean pairs and nothing else to add: the drift is theirs, exact, but too thin for the alignment.
        (frames, c, d) = Session([0, 121, 242, 392, 513], [0, 0, 0, 20, 20], [0, -2, -4, -24, -44]);
        s = SeriesAnalyzer.Analyze(frames, c, d, null, SolveAt30, 6.0);
        Assert.Equal(2, s.DriftPairs);
        Assert.False(s.DriftApproximate);
        Assert.Null(s.StarDriftPerMin);
        Assert.Null(s.PolarErrorMinArcmin);
    }

    /// <summary>The Veil solve's shape with the field at DEC +30 degrees (R-050).</summary>
    private static readonly Wcs SolveAt30 = new(-0.00172, 0.00172, 0.0037, -1, 1, 0.0037, 30.0);

    /// <summary>
    /// <paramref name="count"/> 120 s frames back to back (31 by default, an hour's span), the
    /// stars drifting <paramref name="decPxPerMin"/> along X (DEC) and <paramref name="raPxPerMin"/>
    /// along -Y (east), the field turning <paramref name="rotationArcminPerHour"/>; the last two
    /// frames trailed when <paramref name="trailed"/>; with <paramref name="ditherPx"/> the
    /// pointing slews that far east before frame 16 and the frame turns by the slew's
    /// hour-circle convergence (tan DEC per radian of arc, negative in a frame of handedness +1
    /// like the Veil solve's, as measured there), the registration's own view of a dither.
    /// </summary>
    private static (AnalyzedFrame[] Frames, Cadence Cadence, DitherAnalysis Dither) Polar(double decPxPerMin, double raPxPerMin, double rotationArcminPerHour, bool trailed = false, int count = 31, double ditherPx = 0)
    {
        double[] t = Enumerable.Range(0, count).Select(k => k * 121.0).ToArray();
        double[] x = t.Select(s => decPxPerMin * s / 60).ToArray(), y = t.Select((s, k) => -raPxPerMin * s / 60 + (k >= 15 ? ditherPx : 0)).ToArray();
        double ditherDeg = -Math.Tan(30 * Math.PI / 180) * ditherPx * 6.0 / 206265 * 180 / Math.PI;
        return Session(t, x, y, k => Timed(k + 1, SessionFixture.FirstStart.AddSeconds(t[k]), rotationArcminPerHour / 60 * t[k] / 3600 + (k >= 15 ? ditherDeg : 0), trailed && k >= count - 2 ? Measurement(3.0, 1.7, 10000, 120) : null));
    }

    /// <summary>
    /// The requirement's example: 0.2 px/min on DEC and 0.3 on RA at 6 "/px, 2 arcmin/h of
    /// rotation, DEC +30: the error is between 4.6' (the DEC drift over the sidereal rate)
    /// and 9.9' (the rotation over the rate times cos DEC added in quadrature), the drift the
    /// alignment causes in a 120 s exposure between 0.40 and 0.56 px, an elongation of 1.008
    /// on a 3 px star, at most 1.15 "/min of the RA drift from the alignment, OK (R-050).
    /// </summary>
    [Fact]
    public void R050_ThePolarErrorIsBracketedFromTheDecDriftAndTheRotationAndJudgedByItsCost()
    {
        (AnalyzedFrame[] frames, Cadence c, DitherAnalysis d) = Polar(0.2, 0.3, 2.0);
        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, SolveAt30, 6.0);

        Assert.False(s.DriftApproximate);
        Assert.Equal(30, s.DriftPairs);
        Assert.Equal(30.0, s.DeclinationDeg);
        StarDrift drift = Assert.IsType<StarDrift>(s.StarDriftPerMin);
        Assert.True(drift.Signed);
        Assert.Equal(0.30, drift.RaPxPerMin, 0.01);     // the stars slide east
        Assert.Equal(0.20, drift.DecPxPerMin, 0.01);    // and north
        Assert.Equal(2.0 / 60, s.PolarRotationPerHourDeg!.Value, 0.0001);   // no dither to take out
        Assert.Equal(4.59, s.PolarErrorMinArcmin!.Value, 0.05);
        Assert.Equal(9.92, s.PolarErrorMaxArcmin!.Value, 0.05);
        Assert.Equal(0.40, s.PolarDriftMinPx!.Value, 0.01);
        Assert.Equal(0.56, s.PolarDriftMaxPx!.Value, 0.01);
        Assert.Equal(1.008, s.PolarElongation!.Value, 0.002);
        Assert.Equal(1.155, s.PolarRaDriftMaxArcsecPerMin!.Value, 0.01);
        Assert.Equal(PolarAlignment.Ok, s.PolarAlignment);

        // 1 px/min on DEC: at least 22.9', at least 2 px per exposure on a 3 px star, ATENTIE.
        (frames, c, d) = Polar(1.0, 0.3, 2.0);
        s = SeriesAnalyzer.Analyze(frames, c, d, null, SolveAt30, 6.0);
        Assert.Equal(22.9, s.PolarErrorMinArcmin!.Value, 0.2);
        Assert.Equal(2.0, s.PolarDriftMinPx!.Value, 0.02);
        Assert.Equal(PolarAlignment.Attention, s.PolarAlignment);

        // 0.4 px/min on DEC with 10 arcmin/h of rotation: 0.8 px sure, up to 2.1 px: the bracket straddles the third of the FWHM.
        (frames, c, d) = Polar(0.4, 0.3, 10.0);
        s = SeriesAnalyzer.Analyze(frames, c, d, null, SolveAt30, 6.0);
        Assert.Equal(0.80, s.PolarDriftMinPx!.Value, 0.02);
        Assert.Equal(2.08, s.PolarDriftMaxPx!.Value, 0.05);
        Assert.Equal(PolarAlignment.Marginal, s.PolarAlignment);
        Assert.Equal(1.0 / 3, Th.PolarDriftFwhmFraction, 0.001);
    }

    /// <summary>
    /// A 20 px slew east before frame 16 turns the frame by its hour-circle convergence (the
    /// registration sees 0.0192 degrees at DEC 30 and 6 "/px): the raw rate reads 0.8 arcmin/h
    /// more, the rate behind the upper bound takes the slew out and the bound stays 9.9'; a
    /// run under an hour gives no upper bound at all (R-050, the review of 2026-10-24).
    /// </summary>
    [Fact]
    public void R050_TheDithersNetSlewIsTakenOutOfTheRotationAndAShortRunGivesNoUpperBound()
    {
        (AnalyzedFrame[] frames, Cadence c, DitherAnalysis d) = Polar(0.2, 0.3, 2.0, ditherPx: 20);
        Assert.Equal(1, d.Moves);
        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, SolveAt30, 6.0);
        Assert.Equal(29, s.DriftPairs);
        Assert.Equal(1, SolveAt30.Handedness);
        Assert.Equal(-1, new Wcs(-0.00172, 0.00172, 0.0037, 1, 1, -0.0037).Handedness);   // the same solve mirrored on Y
        Assert.Equal(20.0 - 0.3 * 121 / 60, s.MountMoves[15]!.RaPx, 0.05);   // the pointing's step: the slew minus the drift over the interval
        Assert.InRange(Math.Abs(s.RotationPerHourDeg!.Value * 60 - 2.0), 0.5, 1.5);   // the raw rate carries the slew
        Assert.Equal(2.0 / 60, s.PolarRotationPerHourDeg!.Value, 0.0005);
        Assert.Equal(9.92, s.PolarErrorMaxArcmin!.Value, 0.1);
        Assert.Equal(PolarAlignment.Ok, s.PolarAlignment);

        (frames, c, d) = Polar(0.2, 0.3, 2.0, count: 6);
        s = SeriesAnalyzer.Analyze(frames, c, d, null, SolveAt30, 6.0);
        Assert.Equal(4.59, s.PolarErrorMinArcmin!.Value, 0.05);
        Assert.Null(s.PolarRotationPerHourDeg);
        Assert.Null(s.PolarErrorMaxArcmin);
        Assert.Equal(PolarAlignment.Unknown, s.PolarAlignment);
        Assert.Equal(1.0, Th.PolarRotationMinHours);
    }

    /// <summary>Without a solve the axis from the trails gives the lower bound alone and no sense; without an axis, or with an approximate drift, nothing (R-050).</summary>
    [Fact]
    public void R050_WithoutASolveOnlyTheLowerBoundAndWithoutAnAxisOrACleanDriftNothing()
    {
        (AnalyzedFrame[] frames, Cadence c, DitherAnalysis d) = Polar(0.2, 0.3, 2.0, trailed: true);
        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null, 6.0);
        Assert.Equal(Axis.Y, s.RaAxis);
        Assert.Null(s.DeclinationDeg);
        StarDrift drift = Assert.IsType<StarDrift>(s.StarDriftPerMin);
        Assert.False(drift.Signed);
        Assert.Equal(0.30, drift.RaPxPerMin, 0.01);
        Assert.Equal(0.20, drift.DecPxPerMin, 0.01);
        Assert.Equal(4.57, s.PolarErrorMinArcmin!.Value, 0.05);
        Assert.Null(s.PolarErrorMaxArcmin);
        Assert.Equal(0.40, s.PolarDriftMinPx!.Value, 0.01);
        Assert.Null(s.PolarDriftMaxPx);
        Assert.Null(s.PolarRaDriftMaxArcsecPerMin);
        Assert.Equal(PolarAlignment.Unknown, s.PolarAlignment);   // the lower bound is harmless, the upper unknown

        (frames, c, d) = Polar(1.0, 0.3, 2.0, trailed: true);
        Assert.Equal(PolarAlignment.Attention, SeriesAnalyzer.Analyze(frames, c, d, null, null, 6.0).PolarAlignment);   // the lower bound alone condemns it

        (frames, c, d) = Polar(0.2, 0.3, 2.0);
        s = SeriesAnalyzer.Analyze(frames, c, d, null, null, 6.0);
        Assert.Equal(Axis.Unknown, s.RaAxis);
        Assert.Null(s.StarDriftPerMin);
        Assert.Null(s.PolarErrorMinArcmin);
        Assert.Equal(PolarAlignment.Unknown, s.PolarAlignment);

        // Without a scale the arcminutes cannot be had.
        (frames, c, d) = Polar(0.2, 0.3, 2.0);
        Assert.Null(SeriesAnalyzer.Analyze(frames, c, d, null, SolveAt30, null).PolarErrorMinArcmin);

        // Every frame after a pause: the drift is approximate and the alignment is not read from it.
        (frames, c, d) = Session([0, 150, 300, 450, 600], [0, 1, 2, 3, 4], [0, -1, -2, -3, -4]);
        s = SeriesAnalyzer.Analyze(frames, c, d, null, SolveAt30, 6.0);
        Assert.True(s.DriftApproximate);
        Assert.Null(s.PolarErrorMinArcmin);
        Assert.Null(s.StarDriftPerMin);
        Assert.Equal(PolarAlignment.Unknown, s.PolarAlignment);
    }
}
