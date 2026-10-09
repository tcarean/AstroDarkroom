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

    private static AnalyzedFrame Frame(int i, FrameMeasurement? m, string? error = null)
        => new(new FrameInfo(i, $"DSC_{i:0000}.NEF", "cam", 135, 2.8, 120, 1600, SessionFixture.FirstStart.AddSeconds(i * 141.0), null, null, null, null, null, error), m,
            m is null ? null : new RegistrationResult(true, 300, 200, 0, 0, 0, 1, 0.3), error);

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
        Assert.Equal("RA 142 px V (dreapta), DEC 129 px S (jos)", SeriesReport.MoveCell(m));   // 128.7 px through the matrix's 0.0037 shear

        // The same step with the axis from the trails only: the components without their sense.
        SeriesAnalysis u = SeriesAnalyzer.Analyze(frames, c, d, null, null);
        Assert.Null(u.MountMoves[1]);   // no axis at all: nothing
        Assert.Equal("-", SeriesReport.MoveCell(null));
        Assert.Equal("RA 142 px, DEC 128 px", SeriesReport.MoveCell(new MountMove(142.2, 128.2, false)));
        Assert.Equal("RA 15 px V (dreapta)", SeriesReport.MoveCell(new MountMove(-15.3, 0.5, true)));   // DEC under Th.AxisStep left out
        Assert.Equal("RA 4 px E (stanga), DEC 20 px N (sus)", SeriesReport.MoveCell(new MountMove(4.0, 20.0, true)));
    }

    [Fact]
    public void R038_TheWcsSignsAndTheRaAxisComeFromTheMatrix()
    {
        Assert.Equal(Axis.Y, VeilWcs.RaAxis);
        Assert.Equal(Axis.X, new Wcs(-0.00172, 0.00172, 1, 0.003, 0.003, 1).RaAxis);
        (double ra, double dec) = VeilWcs.SkyOffsetDeg(0, -100);   // +100 px up the solved frame
        Assert.Equal(-0.172, ra, 0.001);
        Assert.Equal(0, dec, 0.001);
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
            Frame(2, Measurement(3.0, 1.7, 9000, 121)),                       // bad, coherent: pe RA
            Frame(3, Measurement(3.1, 1.4, 10000, 122)),                      // marginal, coherent: pe RA
            Frame(4, Measurement(3.0, 1.7, 9000, 121, coherence: 0.5)));      // bad, the stars disagree: angle alone
        Assert.Equal(TrailAxis.Ra, s.Flags[1][0].On);
        Assert.Equal("alungire 1.70 (prag 1.60), dara pe RA (88 grade)", SeriesReport.Reason(s.Flags[1][0]));
        Assert.Equal(TrailAxis.Ra, s.Flags[2][0].On);
        Assert.Equal("alungire 1.40 la limita (prag 1.35), dara pe RA (88 grade)", SeriesReport.Reason(s.Flags[2][0]));
        Assert.Equal(TrailAxis.Unknown, s.Flags[3][0].On);
        Assert.Equal("alungire 1.70 (prag 1.60), dara la 88 grade", SeriesReport.Reason(s.Flags[3][0]));

        // One bad frame cannot set the axis from the trails (Th.TrailAxisMinFrames is 2): no axis, the angle alone.
        SeriesAnalysis u = Analyze(
            Frame(1, Measurement(3.0, 1.1, 10000, 120)),
            Frame(2, Measurement(3.0, 1.7, 9000, 121)),
            Frame(3, Measurement(3.1, 1.4, 10000, 122)));
        Assert.Equal(Axis.Unknown, u.RaAxis);
        Assert.Equal(TrailAxis.Unknown, u.TrailOn);
        Assert.Equal("alungire 1.70 (prag 1.60), dara la 88 grade", SeriesReport.Reason(u.Flags[1][0]));
        Assert.Equal("alungire 1.40 la limita (prag 1.35), dara la 88 grade", SeriesReport.Reason(u.Flags[2][0]));
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
}
