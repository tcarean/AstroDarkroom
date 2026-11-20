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
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using LightsCheck.Cli;
using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// The whole series on the synthetic eight-frame session (R-022 to R-028), and the options a
/// series run takes (the pixel-size check of R-004 and R-029).
/// </summary>
public class SeriesTests
{
    /// <summary>The one pixel-size check both applications call (R-004, R-029): finite and above 0.</summary>
    [Theory]
    [InlineData(3.92, true)]
    [InlineData(1e-9, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(double.NegativeInfinity, false)]
    public void R004_ThePixelSizeIsValidOnlyWhenFiniteAndPositive(double um, bool valid) => Assert.Equal(valid, SeriesOptions.PixelSizeIsValid(um));

    /// <summary>The table rows and the RESPINSE heading the window shows are the console's (R-027, ADR 0008).</summary>
    [Fact]
    public void R027_TheTableRowsAndTheRejectedHeadingAreWhatTheConsolePrints()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(), null, CancellationToken.None);

        IReadOnlyList<FrameTableRow> rows = SeriesReport.FrameRows(r);
        using StringWriter sw = new();
        SeriesReport.Write(r, sw);
        string text = sw.ToString();

        Assert.Equal(8, rows.Count);
        Assert.Equal("SLAB", rows[2].Class);
        Assert.Contains($"  RESPINSE ({SeriesReport.RejectedHeading(r)})", text, StringComparison.Ordinal);
        Assert.Contains($"  {rows[2].Elongation,5}  {rows[2].Fwhm,5}  ", text, StringComparison.Ordinal);
        Assert.Equal([3, 6], SeriesReport.RejectedRows(r).Select(x => x.Index));
    }

    /// <summary>
    /// Six frames whose last three are drawn upside down, as after a meridian flip: they have no
    /// position and keep their class, their reason says the flip, and DITHERING opens, in the
    /// console and in the window's rows, by saying what the figures cover (R-087). The stars
    /// are scattered: the fixture's grid turned upside down falls nearly on itself.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R087_FramesTakenAfterAMeridianFlipAreSaidAndNotMeasured(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        SeriesFrame[] frames =
        [
            new(0, 0, 1.0, 0), new(0.5, 0.2, 1.0, 1), new(12, 0, 1.0, 30),
            new(12.5, 0.3, 1.0, 1, Turned: true), new(12, -15, 1.0, 30, Turned: true), new(12.5, -15, 1.0, 1, Turned: true),
        ];
        using SessionFixture f = new SessionFixture().WithStarSeries(frames, scattered: true);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));

        Assert.Equal(3, r.Dither.Registered);
        Assert.All(Enumerable.Range(0, 3), i => Assert.Empty(r.Series.Flags[i]));
        string reason = english
            ? "taken after the meridian flip (the image is turned upside down), so its position is not measured"
            : "facut dupa meridian flip (imaginea e intoarsa cu susul in jos), asa ca pozitia lui nu se masoara";
        Assert.All(Enumerable.Range(3, 3), i =>
        {
            Assert.True(r.Frames[i].Registration!.Flipped);
            Assert.Equal(FlagKind.AfterFlip, Assert.Single(r.Series.Flags[i]).Kind);
            Assert.Equal(FrameClass.Good, r.Series.Classes[i]);
            Assert.Equal(reason, SeriesReport.Reason(r.Series.Flags[i][0]));
        });

        ReportRow first = SeriesReport.DitherRows(r)[0];
        Assert.Equal("meridian flip", first.Label);
        Assert.Equal(
            english
                ? "frames 4-6 (3 of 6) were taken after the meridian flip; the dithering, the drift, the field rotation and the fixed pattern are measured on the other frames only"
                : "cadrele 4-6 (3 din 6) sunt facute dupa meridian flip; ditheringul, driftul, rotatia de camp si pattern-ul fix sunt masurate doar pe celelalte cadre",
            first.Value);
        Assert.Equal(first, SeriesReport.DitherScalarRows(r)[0]);
        using StringWriter sw = new();
        SeriesReport.Write(r, sw);
        Assert.Contains(first.Value, sw.ToString(), StringComparison.Ordinal);
        Assert.Contains(english ? "frames aligned: 3 of 6" : "cadre aliniate: 3 din 6", sw.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Three frames whose stars are drawn 8 px wide read about 8 px with every star counted, the
    /// brightest included, and FOCUS keeps its four rows: the limits of the detection follow the
    /// stars' width, so nothing is left out of such a session (R-014, ADR 0059; the `wide stars`
    /// row of R-088 is dropped).
    /// </summary>
    [Fact]
    public void R014_ASessionOfWideStarsIsReadAtItsWidthWithEveryStarCounted()
    {
        using IDisposable _ = Ui.Use(UiLanguage.English);
        SeriesFrame[] frames = [new(0, 0, 1.0, 0, Fwhm: 8.0), new(0.5, 0.2, 1.0, 1, Fwhm: 8.0), new(12, 0, 1.0, 30, Fwhm: 8.0)];
        using SessionFixture f = new SessionFixture().WithStarSeries(frames);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));

        Assert.InRange(r.Series.SessionFwhm!.Value, 7.8, 8.4);
        Assert.All(r.Frames, fr => Assert.InRange(fr.Measurement!.Detected, 40, 41));
        IReadOnlyList<ReportRow> rows = SeriesReport.FocusRows(r);
        Assert.Equal(4, rows.Count);
        Assert.Equal("verdict", rows[^1].Label);
        Assert.DoesNotContain(rows, row => row.Label == "wide stars");
    }

    /// <summary>
    /// Stars 4.9 px wide are read at their width, where the moments of the first versions read
    /// them 4.7 px (R-014, ADR 0056).
    /// </summary>
    [Fact]
    public void R014_ASessionOfStarsFivePixelsWideIsReadAtItsWidth()
    {
        using IDisposable _ = Ui.Use(UiLanguage.English);
        SeriesFrame[] frames = [new(0, 0, 1.0, 0, Fwhm: 4.9), new(0.5, 0.2, 1.0, 1, Fwhm: 4.9), new(12, 0, 1.0, 30, Fwhm: 4.9)];
        using SessionFixture f = new SessionFixture().WithStarSeries(frames);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));

        Assert.InRange(r.Series.SessionFwhm!.Value, 4.85, 5.2);
        Assert.Equal(4, SeriesReport.FocusRows(r).Count);
    }

    /// <summary>
    /// A frame whose stars carry a second copy a third as bright beside a sharp core, the last of
    /// the classes pattern, right after a move: marginal with the tail flag, its reason in plain
    /// words with the move, a row of its own in TRAILED FRAMES and a paragraph in the explanation
    /// of the Rejected tab (R-090).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R090_AFrameWithATailBesideASharpCoreIsMarginalAndSaysTheStackRepairsIt(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.ClassesPattern, biases: 2);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));
        SeriesAnalysis s = r.Series;

        Assert.Equal(FrameClass.Marginal, s.Classes[13]);
        FrameFlag flag = Assert.Single(s.Flags[13]);
        Assert.Equal(FlagKind.Tail, flag.Kind);
        Assert.True(flag.AfterMove);
        Assert.True(flag.Value >= Th.ElongationBad, $"whole-star elongation {flag.Value}");
        Assert.InRange(flag.Threshold, 1.0, 1.3);
        Assert.InRange(r.Frames[13].Measurement!.FwhmMedian!.Value, 2.9, 3.6);

        string reason = SeriesReport.Reason(flag);
        Assert.StartsWith(english ? "the stars have a tail or a second copy: elongation " : "stelele au o dara sau o a doua copie: alungire ", reason, StringComparison.Ordinal);
        Assert.Contains(english ? "stacking with rejection removes it on most stars, so the frame stays in the stack" : "la stivuire, respingerea (rejection) o scoate la cele mai multe stele, deci cadrul ramane in stiva", reason, StringComparison.Ordinal);
        Assert.EndsWith(english ? "; the frame comes right after a move of the mount, which was probably still settling when the exposure began" : "; cadrul vine imediat dupa o miscare a monturii, care probabil inca se aseza cand a inceput expunerea", reason, StringComparison.Ordinal);
        PlainWords.AssertPlain(reason, "the reason of a tail");

        IReadOnlyList<ReportRow> rows = SeriesReport.TrailRows(r);
        Assert.Equal(5, rows.Count);
        ReportRow tail = rows[3];   // after the two marginal rows (R-094)
        Assert.Equal(english ? "tail or double" : "dara sau dublura", tail.Label);
        Assert.Equal(english ? "14 (the star's core is sharp; a stack with rejection repairs most of them)" : "14 (miezul stelei e bun; stiva cu respingere le repara pe cele mai multe)", tail.Value);
        Assert.DoesNotContain("14", rows[0].Value, StringComparison.Ordinal);
        // The frame reads past the marginal row's upper bound and has its own row: not listed twice.
        Assert.DoesNotContain("14", rows[1].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("14", rows[2].Value, StringComparison.Ordinal);
        PlainWords.AssertPlain(tail.Label + " " + tail.Value, "the tail row of TRAILED FRAMES");

        IReadOnlyList<ReportParagraph> why = SeriesReport.RejectedExplanation;
        Assert.Equal(6, why.Count);
        Assert.StartsWith(english ? "A frame whose stars have a tail or a second copy beside a sharp core is marginal whatever its elongation" : "Un cadru in care stelele au o dara sau o a doua copie langa un miez bun e la limita, oricat de mare i-ar fi alungirea", why[2].Text, StringComparison.Ordinal);
        Assert.EndsWith(english ? "These frames are on the 'tail or double' row of TRAILED FRAMES." : "Aceste cadre sunt pe randul 'dara sau dublura' din CADRE MISCATE.", why[2].Text, StringComparison.Ordinal);
        using StringWriter sw = new();
        SeriesReport.Write(r, sw);
        Assert.Contains(tail.Value, sw.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// TRAILED FRAMES lists the marginal frames by what makes them marginal (R-094, ADR 0068,
    /// CR-020): on the classes session frame 3, marginal by its elongation, is on the row whose
    /// label names the elongation bounds, and frames 4 (wide stars, elongation 1.01) and 5 (too
    /// few stars) on the row under it; the explanation of the Rejected tab points at both.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R094_TheMarginalFramesAreListedByWhatMakesThemMarginal(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.ClassesPattern, biases: 2);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));
        Assert.Equal(FlagKind.MarginalTrail, Assert.Single(r.Series.Flags[2]).Kind);
        Assert.Equal(FlagKind.Soft, Assert.Single(r.Series.Flags[3]).Kind);
        Assert.Equal(FlagKind.Clouds, Assert.Single(r.Series.Flags[4]).Kind);

        IReadOnlyList<ReportRow> rows = SeriesReport.TrailRows(r);

        Assert.Equal(english ? "marginal (elongation between 1.45 and 1.73)" : "la limita (alungire intre 1.45 si 1.73)", rows[1].Label);
        Assert.Equal("3", rows[1].Value);
        Assert.Equal(english ? "marginal for another reason" : "la limita din alt motiv", rows[2].Label);
        Assert.Equal(english ? "4, 5 (wide stars or too few stars)" : "4, 5 (stele late sau prea putine stele)", rows[2].Value);
        PlainWords.AssertPlain(rows[2].Label + " " + rows[2].Value, "the row of the frames marginal for another reason");

        string second = SeriesReport.RejectedExplanation[1].Text;
        Assert.Equal(
            english
                ? "Between 1.45 and 1.73 the frame is marginal and stays in the stack unless you decide otherwise. These frames are on the 'marginal' row of TRAILED FRAMES, on the Summary tab; a frame that is marginal for wide stars or too few stars is on the row under it, 'marginal for another reason'. On the Frames tab every marginal frame has a yellow class."
                : "Intre 1.45 si 1.73 cadrul e la limita si ramane in stiva, daca nu hotarasti altfel. Aceste cadre sunt pe randul 'la limita' din CADRE MISCATE, pe tabul Rezumat; un cadru la limita din cauza stelelor late sau a stelelor prea putine e pe randul de sub el, 'la limita din alt motiv'. Pe tabul Cadre, orice cadru la limita are clasa galbena.",
            second);
        PlainWords.AssertPlain(second, "the second paragraph of the Rejected explanation");
        using StringWriter sw = new();
        SeriesReport.Write(r, sw);
        Assert.Contains($"    {rows[2].Label,-19}: {rows[2].Value}", sw.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A session whose marginal frames are all marginal by their elongation, or that has none,
    /// has no row for another reason (R-094): the pattern session, and the four frames of the
    /// short-exposure fixture, whose marginally trailed frame is bad by its exposure.
    /// </summary>
    [Fact]
    public void R094_WithoutAFrameMarginalForAnotherReasonThereIsNoSuchRow()
    {
        SeriesResult pattern = RunPattern();
        Assert.DoesNotContain(SeriesReport.TrailRows(pattern), x => x.Label == "la limita din alt motiv");

        (AnalyzedFrame[] frames, DitherAnalysis d, Cadence c) = SeriesAnalyzerTests.ShortAndTrailed();
        frames[0] = frames[0] with { Measurement = frames[0].Measurement! with { ElongationMedian = 1.5, CoreElongationMedian = 1.5 } };
        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);
        Assert.Equal(FrameClass.Marginal, s.Classes[0]);
        IReadOnlyList<ReportRow> rows = SeriesReport.TrailRows(pattern with { Frames = frames, Cadence = c, Dither = d, Series = s });

        Assert.Equal(3, rows.Count);
        Assert.Equal("1", rows[1].Value);
    }

    /// <summary>The pattern session, none of whose frames has a tail, keeps the three rows (R-090).</summary>
    [Fact]
    public void R090_ASessionWithoutATailHasNoTailRow()
    {
        Assert.Equal(3, SeriesReport.TrailRows(RunPattern()).Count);
    }

    /// <summary>
    /// A series whose third light was taken before its second (a pause of -1000 s between the
    /// two): the report's session lines carry the warning, and the frames are measured in the
    /// order of their names as before (R-089).
    /// </summary>
    [Fact]
    public void R089_ASeriesWhoseNamesAreNotInTheOrderOfItsTimesSaysSoInTheSessionLines()
    {
        using IDisposable _ = Ui.Use(UiLanguage.English);
        SeriesFrame[] frames = [new(0, 0, 1.0, 0), new(0.5, 0.2, 1.0, 1), new(12, 0, 1.0, -1000)];
        using SessionFixture f = new SessionFixture().WithStarSeries(frames);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));

        Assert.Equal((1, 2), r.Cadence.Backwards);
        Assert.Equal(["DSC_0001.fit", "DSC_0002.fit", "DSC_0003.fit"], r.Frames.Select(x => x.Info.FileName));
        using StringWriter sw = new();
        SeriesReport.Write(r, sw);
        Assert.Contains(
            "    WARNING            : the file names are not in the order the frames were taken: DSC_0003.fit is listed after DSC_0002.fit and was taken before it. ",
            sw.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A session without a flip has no such row, and one frame or scattered frames are written as the other rows write frames (R-087).</summary>
    [Fact]
    public void R087_TheFlipRowIsThereOnlyAfterAFlipAndWritesRangesAndOneFrame()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern.Take(3).ToList());
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));
        Assert.DoesNotContain(SeriesReport.DitherRows(r), row => row.Label == "meridian flip");
        Assert.DoesNotContain(SeriesReport.DitherScalarRows(r), row => row.Label == "meridian flip");

        Assert.Equal("4-6", SeriesReport.Ranges([4, 5, 6]));
        Assert.Equal("4-6, 9, 11-12", SeriesReport.Ranges([4, 5, 6, 9, 11, 12]));
        Assert.Equal("87", SeriesReport.Ranges([87]));
        using IDisposable _ = Ui.Use(UiLanguage.English);
        Assert.StartsWith("frame 87 (1 of 87) was taken after the meridian flip; ", SeriesReport.FlipText([87], 87), StringComparison.Ordinal);
        using IDisposable ro = Ui.Use(UiLanguage.Romanian);
        Assert.StartsWith("cadrul 87 (1 din 87) e facut dupa meridian flip; ", SeriesReport.FlipText([87], 87), StringComparison.Ordinal);
    }

    /// <summary>
    /// Eight frames that drifted half a pixel each, 3.5 px in all: every one is within three star
    /// widths of the first, the fixed pattern is not read, and its row says the group and the
    /// distance it was judged at (R-046). The frames after the first sit 0.3 px down: the
    /// fixture's star grid repeats every 137 px, and with a shift of exactly 0 on an axis the
    /// vote, split between two bins, loses to the grid laid one column over.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R046_FramesWithinThreeStarWidthsSayTheGroupAndItsDistance(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        List<SeriesFrame> frames = Enumerable.Range(0, 8).Select(i => new SeriesFrame(0.5 * i, i == 0 ? 0 : 0.3, 1.0, 1)).ToList();
        using SessionFixture f = new SessionFixture().WithStarSeries(frames);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));

        Assert.Equal(PatternStatus.FramesDidNotMove, r.Pattern.Status);
        Assert.True(r.Pattern.ClusterByStarWidth);
        Assert.InRange(r.Pattern.ClusterRadiusPx, 3 * 2.8, 3 * 3.6);   // three times the width the 3.0 px stars read
        // All eight frames are good, so the width over the frames used is the session's own.
        Assert.Equal(3 * r.Series.SessionFwhm!.Value, r.Pattern.ClusterRadiusPx, 6);
        Assert.Equal(8, r.Pattern.LargestCluster);
        string px = r.Pattern.ClusterRadiusPx.ToString("F0", CultureInfo.InvariantCulture);
        string value = Assert.Single(SeriesReport.DitherRows(r), x => x.Label == (english ? "fixed pattern" : "pattern fix")).Value;
        Assert.StartsWith(
            english
                ? $"cannot be measured (the frames stayed in place too much and the stars cannot be told apart from the pattern; the largest group within {px} px, three times the stars' width, has 8 frames of 8); "
                : $"nu se poate masura (cadrele au stat prea mult pe loc si stelele nu se pot desparti de pattern; cel mai mare grup aflat la cel mult {px} px, de trei ori latimea stelelor, are 8 cadre din 8); ",
            value, StringComparison.Ordinal);
    }

    /// <summary>Every class and flag of R-022, end to end on files: the golden series-classes.txt pins the words, this pins the kinds.</summary>
    [Fact]
    public void R022_EveryClassAndFlagAppearsOnTheSyntheticClassesSession()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.ClassesPattern, biases: 2);
        File.WriteAllBytes(Path.Combine(f.LightsDir, "DSC_0009.fit"), new byte[100]);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));
        SeriesAnalysis s = r.Series;

        Assert.True(r.HasErrors);
        Assert.Equal(
            [FrameClass.Good, FrameClass.Good, FrameClass.Marginal, FrameClass.Marginal, FrameClass.Marginal, FrameClass.Good, FrameClass.Bad, FrameClass.Good, FrameClass.Unknown, FrameClass.Good, FrameClass.Bad, FrameClass.Good, FrameClass.Good, FrameClass.Marginal],
            s.Classes);
        Assert.Equal(FlagKind.Tail, Assert.Single(s.Flags[13]).Kind);   // a second copy beside a sharp core, right after a move (R-090)
        Assert.Equal(FlagKind.MarginalTrail, Assert.Single(s.Flags[2]).Kind);
        Assert.InRange(s.Flags[2][0].Value, Th.ElongationMarginal, Th.ElongationBad);
        Assert.Equal(FlagKind.Soft, Assert.Single(s.Flags[3]).Kind);
        Assert.Equal(Th.SoftFwhmFactor * s.SessionFwhm!.Value, s.Flags[3][0].Threshold, 0.01);
        Assert.Equal(FlagKind.Clouds, Assert.Single(s.Flags[4]).Kind);
        Assert.Equal(20, s.Flags[4][0].Value);
        Assert.Equal(FlagKind.BackgroundJump, Assert.Single(s.Flags[5]).Kind);
        Assert.InRange(s.Flags[5][0].Value, 25, 35);
        Assert.Equal(FlagKind.Trailed, Assert.Single(s.Flags[6]).Kind);
        Assert.Equal(FlagKind.Unreadable, Assert.Single(s.Flags[8]).Kind);
        Assert.Equal(FlagKind.ShortExposure, Assert.Single(s.Flags[10]).Kind);   // 0.2 s against the usual 120 s (R-085)
        Assert.Equal(120, s.UsualExposureSeconds);
        Assert.Equal([FlagKind.OtherExposure, FlagKind.BackgroundJump], s.Flags[11].Select(x => x.Kind));   // 137.5 s, and the +14 % jump it explains
        Assert.Equal(FrameFlag.ExposureDetail, s.Flags[11][1].Detail);
        Assert.Equal(7, s.GoodCount);
        Assert.Equal(4, s.MarginalCount);   // frames 3, 4, 5 and the tail of 14
        Assert.Equal(2, s.BadCount);
        Assert.Equal(1, s.UnknownCount);
    }

    /// <summary>
    /// No line of the series report carries a minus on a figure that rounds to zero (R-092): the
    /// classes session, whose change of the stars' width, halo, drift on Y and several table
    /// cells are a hair under zero, in both languages.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R092_NoLineOfTheSeriesReportCarriesAMinusOnAZero(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.ClassesPattern, biases: 2);
        File.WriteAllBytes(Path.Combine(f.LightsDir, "DSC_0009.fit"), new byte[100]);   // the session of the golden series-classes.txt
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));
        using StringWriter sw = new();
        SeriesReport.Write(r, sw);
        string text = sw.ToString();

        // A minus, a zero, any further zeros after a point, and then no other digit.
        Assert.DoesNotMatch(@"-0(?:\.0+)?(?![\d.])", text);
        Assert.Contains(english ? "3.22 px at the end (+0 %): the focus held" : "3.22 px la sfarsit (+0 %): focusul a tinut", text, StringComparison.Ordinal);
        Assert.Contains(": 0.000 / 0.000; ", text, StringComparison.Ordinal);
        Assert.Contains("Y +0.00 px/min = +1.8 / +0.0 \"/min", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// SUMMARY's row of the classes agrees its Romanian words with their counts, singular for
    /// one and with `de` from 20 (R-093, R-082); the English words have no plural.
    /// </summary>
    [Fact]
    public void R093_TheSummaryRowOfTheClassesCountsOneFrameInTheSingular()
    {
        SeriesResult pattern = RunPattern();
        SeriesResult one = pattern with { Series = pattern.Series with { BadCount = 1, MarginalCount = 3, GoodCount = 1, UnknownCount = 1 } };
        SeriesResult many = pattern with { Series = pattern.Series with { BadCount = 2, MarginalCount = 1, GoodCount = 22, UnknownCount = 2 } };
        static string Row(SeriesResult r) => SeriesReport.SummaryRows(r)[3].Value;

        Assert.StartsWith("1 slab, 3 la limita, 1 bun, 1 necitit (din 8); ", Row(one), StringComparison.Ordinal);
        Assert.StartsWith("2 slabe, 1 la limita, 22 de bune, 2 necitite (din 8); ", Row(many), StringComparison.Ordinal);
        using IDisposable _ = Ui.Use(UiLanguage.English);
        Assert.StartsWith("1 bad, 3 marginal, 1 good, 1 unread (of 8); ", Row(one), StringComparison.Ordinal);
        Assert.StartsWith("2 bad, 1 marginal, 22 good, 2 unread (of 8); ", Row(many), StringComparison.Ordinal);
    }

    /// <summary>
    /// The second row of FOCUS is labelled by how many good frames each end compares: singular
    /// for one, no count when fewer than two good frames leave nothing to compare (R-093).
    /// </summary>
    [Theory]
    [InlineData(10, true, "first / last 10 good frames")]
    [InlineData(10, false, "primele / ultimele 10 cadre bune")]
    [InlineData(20, false, "primele / ultimele 20 de cadre bune")]
    [InlineData(1, true, "first / last good frame")]
    [InlineData(1, false, "primul / ultimul cadru bun")]
    [InlineData(0, true, "first / last good frames")]
    [InlineData(0, false, "primele / ultimele cadre bune")]
    public void R093_TheFocusRowNamesOneGoodFrameInTheSingular(int count, bool english, string label)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        SeriesResult pattern = RunPattern();
        SeriesResult r = pattern with { Series = pattern.Series with { Fwhm = pattern.Series.Fwhm with { Count = count } } };

        Assert.Equal(label, SeriesReport.FocusRows(r)[1].Label);
    }

    /// <summary>The `fixed pattern` row counts one dark in the singular, in both languages (R-093).</summary>
    [Theory]
    [InlineData(1, true, "1 dark in darks\\, with which Siril takes the pattern out")]
    [InlineData(1, false, "1 dark in darks\\, cu care Siril scoate pattern-ul")]
    [InlineData(3, true, "3 darks in darks\\, with which Siril takes the pattern out")]
    [InlineData(3, false, "3 dark-uri in darks\\, cu care Siril scoate pattern-ul")]
    public void R093_TheFixedPatternRowCountsOneDarkInTheSingular(int darks, bool english, string ending)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern.Take(3).ToList(), darks: darks);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));
        Assert.Equal(darks, r.Session.Darks.Count);

        string value = Assert.Single(SeriesReport.DitherRows(r), x => x.Label == (english ? "fixed pattern" : "pattern fix")).Value;

        Assert.EndsWith("; " + ending, value, StringComparison.Ordinal);
    }

    /// <summary>
    /// `--frame N` past the end of the session says how many frames it has, one frame in the
    /// singular (R-093).
    /// </summary>
    [Theory]
    [InlineData(1, true, "frame 5 does not exist: the session has 1 frame in ")]
    [InlineData(1, false, "cadrul 5 nu exista: sesiunea are 1 cadru in ")]
    [InlineData(2, true, "frame 5 does not exist: the session has 2 frames in ")]
    [InlineData(2, false, "cadrul 5 nu exista: sesiunea are 2 cadre in ")]
    public void R093_AFrameIndexPastTheSessionSaysOneFrameInTheSingular(int lights, bool english, string start)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        using SessionFixture f = new SessionFixture().WithLights(lights);

        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => FrameRun.Run(f.Root, 5));

        Assert.StartsWith(start, e.Message, StringComparison.Ordinal);
    }

    private static SeriesResult RunPattern(int biases = 0)
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: biases);
        return SeriesRun.Run(s.Root, new SeriesOptions(Threads: 2));
    }

    [Fact]
    public void R026_EveryFrameIsMeasuredAndRegisteredToTheFirst()
    {
        SeriesResult r = RunPattern();
        Assert.Equal(8, r.Frames.Count);
        Assert.All(r.Frames, f => Assert.NotNull(f.Measurement));
        Assert.All(r.Frames, f => Assert.True(f.Registration!.Ok));
        Assert.Equal(1, r.ReferenceIndex);
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(SessionFixture.Pattern[i].Dx, r.Frames[i].Registration!.Dx, 0.3);
            Assert.Equal(SessionFixture.Pattern[i].Dy, r.Frames[i].Registration!.Dy, 0.3);
        }

        Assert.Equal(206.265 * 3.92 / 135, r.ArcsecPerPixel!.Value, 0.01);
        Assert.Equal("scris in fisier (XPIXSZ)", r.PixelSizeSource);
    }

    [Fact]
    public void R022_TrailedFramesAreBadAndCarryTheirReason()
    {
        SeriesResult r = RunPattern();
        SeriesAnalysis s = r.Series;
        Assert.Equal(FrameClass.Bad, s.Classes[2]);
        Assert.Equal(FrameClass.Bad, s.Classes[5]);
        Assert.Equal(2, s.BadCount);
        Assert.Equal(6, s.GoodCount);
        FrameFlag flag = Assert.Single(s.Flags[2]);
        Assert.Equal(FlagKind.Trailed, flag.Kind);
        Assert.InRange(flag.Value, Th.ElongationBad, 2.2);
        Assert.Equal(Th.ElongationBad, flag.Threshold);
        Assert.Empty(s.Flags[0]);
    }

    /// <summary>
    /// The verdict's text prints the share of frames whose streak would show with one decimal, in
    /// both languages, so a share just under the limit never reads as the limit (R-084): the
    /// 20-frame fixture with a run of 4 of the analyzer's tests, put into the pattern session's result.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R084_TheVerdictTextPrintsTheShareOfStreakedFramesWithOneDecimal(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        SeriesResult pattern = RunPattern();
        (AnalyzedFrame[] frames, DitherAnalysis d, Cadence c) = SeriesAnalyzerTests.RunThenDither(4);
        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);
        SeriesResult r = pattern with { Frames = frames, Cadence = c, Dither = d, Series = s };

        ReportRow verdict = SeriesReport.DitherScalarRows(r)[^1];

        Assert.StartsWith(english ? "OK for walking noise" : "OK pentru walking noise", verdict.Verdict, StringComparison.Ordinal);
        Assert.Contains(
            english ? "walking-noise streaks of up to 5.1 px along Y, in 4 frames of 20 (20.0 %; from 25 % they show in the stack): " : "dare de walking noise de pana la 5.1 px pe Y, in 4 cadre din 20 (20.0 %; de la 25 % se vad in stiva): ",
            verdict.Value, StringComparison.Ordinal);
        Assert.Contains(english ? "; in the stack 5.0 % of one frame's pattern stays (ideal 5.0 %, fine up to 7.5 %); " : "; in stiva ramane 5.0 % din pattern-ul unui cadru (ideal 5.0 %, bine pana la 7.5 %); ", verdict.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// The TRAILED FRAMES rows go by the trail, not the class (R-085): a trailed short frame is
    /// listed once under `bad`, a marginally trailed short frame, bad by its exposure, under
    /// neither row, while the summary row keeps counting the classes.
    /// </summary>
    [Fact]
    public void R085_ATrailedShortFrameIsListedOnceAndAMarginallyTrailedShortOneInNeitherRow()
    {
        SeriesResult pattern = RunPattern();
        (AnalyzedFrame[] frames, DitherAnalysis d, Cadence c) = SeriesAnalyzerTests.ShortAndTrailed();
        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);
        Assert.Equal([FrameClass.Good, FrameClass.Bad, FrameClass.Bad, FrameClass.Bad], s.Classes);
        Assert.Equal([FlagKind.ShortExposure, FlagKind.Trailed], s.Flags[2].Select(f => f.Kind));
        Assert.Equal([FlagKind.ShortExposure, FlagKind.MarginalTrail], s.Flags[3].Select(f => f.Kind));
        SeriesResult r = pattern with { Frames = frames, Cadence = c, Dither = d, Series = s };

        IReadOnlyList<ReportRow> rows = SeriesReport.TrailRows(r);

        Assert.Equal("2, 3", rows[0].Value);
        Assert.Equal("niciunul", rows[1].Value);
        Assert.StartsWith("3 slabe, 0 la limita, 1 bun (din 4)", Assert.Single(SeriesReport.SummaryRows(r), x => x.Label == "cadre miscate").Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// When too few frames give the trails' direction, the row says which frames count (R-085,
    /// ADR 0065): the fixture's `bad` row lists two frames trailed the same way, one of them too
    /// short to stack and so without a vote (ADR 0061), and the row under it must not read as if
    /// two bad frames trailed the same way were not enough.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R085_TheTrailDirectionRowSaysThatOnlyFramesLongEnoughToStackCount(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        SeriesResult pattern = RunPattern();
        (AnalyzedFrame[] frames, DitherAnalysis d, Cadence c) = SeriesAnalyzerTests.ShortAndTrailed();
        SeriesAnalysis s = SeriesAnalyzer.Analyze(frames, c, d, null, null);
        SeriesResult r = pattern with { Frames = frames, Cadence = c, Dither = d, Series = s };

        IReadOnlyList<ReportRow> rows = SeriesReport.TrailRows(r);

        Assert.Equal("2, 3", rows[0].Value);
        ReportRow direction = Assert.Single(rows, x => x.Label == (english ? "trail direction" : "directia darelor"));
        Assert.Equal(
            english
                ? "not known (too few bad frames, long enough to stack, with trails in the same direction); it is not known which axis is RA"
                : "nu se stie (prea putine cadre slabe, cu expunere destul de lunga, cu dare in aceeasi directie); nu se stie care axa e RA",
            direction.Value);
        PlainWords.AssertPlain(direction.Label + " " + direction.Value, "the trail direction row");
    }

    /// <summary>The `scara` row of DITHERING words the move thresholds in arcseconds at the fixture's 5.99 "/px (R-044).</summary>
    [Fact]
    public void R044_TheScaleRowSaysWhatAMoveIsOnTheSky()
    {
        SeriesResult r = RunPattern();
        ReportRow row = Assert.Single(SeriesReport.DitherRows(r), x => x.Label == "scara");
        Assert.Equal("1 px = 5.99\" pe cer; ca sa muti cadrul 5 px (cea mai mica miscare numarata) trebuie 30\" pe cer, iar pentru 10 px, 60\"", row.Value);
        Assert.Equal(row.Value, Assert.Single(SeriesReport.DitherScalarRows(r), x => x.Label == "scara").Value);
        Assert.Equal("verdict", SeriesReport.DitherScalarRows(r)[^1].Label);
    }

    /// <summary>The explanation under the Dithering card: five paragraphs, the one on what stops walking noise in bold, the thresholds of Th quoted, the 20 mm figure of paragraph 2 as ADR 0032 computes it (R-045).</summary>
    [Fact]
    public void R045_TheDitherExplanationQuotesTheThresholds()
    {
        IReadOnlyList<ReportParagraph> p = SeriesReport.DitherExplanation;
        Assert.Equal(5, p.Count);
        Assert.Single(p, x => x.Emphasis);
        Assert.StartsWith("Ce opreste walking noise-ul", p.Single(x => x.Emphasis).Text, StringComparison.Ordinal);
        Assert.Contains($"Sub {Th.WalkingStreakVisible:0} px dunga nu se vede", p[4].Text, StringComparison.Ordinal);
        Assert.Contains($"{Th.DitherStep:0} px ajung", p[2].Text, StringComparison.Ordinal);
        Assert.Contains("capacul de body", p[3].Text, StringComparison.Ordinal);
        Assert.Contains("0.15 px la 20 mm", p[1].Text, StringComparison.Ordinal);
    }

    /// <summary>The 8-frame pattern with the synthetic sensor: its bulk field of 8 ADU and its 20 hot pixels come back from the lights' median, and the darks line says what Siril will do (R-046, R-042).</summary>
    [Fact]
    public void R046_TheSensorPatternIsReadFromTheLightsAndTheDarksAreNoticed()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern, darks: 3, sensorPattern: true);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));
        SensorPattern p = r.Pattern;

        Assert.Equal(PatternStatus.Measured, p.Status);
        Assert.Equal(8, p.Frames);
        Assert.Equal(480, p.CropSize);            // the 640 x 480 frame's shorter side
        // The crop reads the central 480 x 480 minus a 2 px border per CFA position: the hot pixels inside it.
        int hotInside = SessionFixture.SensorPattern.Skip(SessionFixture.WarmPixels).Count(q => q.X >= 84 && q.X < 556 && q.Y >= 4 && q.Y < 476);
        Assert.InRange(p.Hot, hotInside - 1, hotInside + 1);
        Assert.InRange(p.HotPerMpx, 60, 110);
        // The bulk pattern is a fixed field of 8 ADU on every light (the spikes are clipped out of the sigma): 8 against the 40 of a frame.
        Assert.All(p.PatternSigma, v => Assert.InRange(v, 6.0, 10.5));
        Assert.All(Enumerable.Range(0, 4), i => Assert.InRange(p.FractionOfFrame(i), 0.14, 0.27));
        Assert.Equal(3, r.Session.Darks.Count);

        ReportRow row = Assert.Single(SeriesReport.DitherRows(r), x => x.Label == "pattern fix");
        Assert.StartsWith("tarie ", row.Value, StringComparison.Ordinal);
        Assert.Contains(" ADU pe R (", row.Value, StringComparison.Ordinal);
        Assert.Contains(" % din zgomotul unui cadru)", row.Value, StringComparison.Ordinal);
        Assert.Contains("pixeli calzi la un milion de pixeli, adica cei de peste 10 ori zgomotul unui cadru", row.Value, StringComparison.Ordinal);
        Assert.EndsWith("3 dark-uri in darks\\, cu care Siril scoate pattern-ul", row.Value, StringComparison.Ordinal);

        using SessionFixture g = new SessionFixture().WithStarSeries(SessionFixture.Pattern.Take(4).ToList());
        SeriesResult q = SeriesRun.Run(g.Root, new SeriesOptions(Threads: 2));
        Assert.Equal(PatternStatus.TooFewFrames, q.Pattern.Status);
        Assert.EndsWith("fara dark-uri in sesiune, pattern-ul ramane in cadre", Assert.Single(SeriesReport.DitherRows(q), x => x.Label == "pattern fix").Value, StringComparison.Ordinal);
    }

    /// <summary>The eight-frame pattern: four pairs at one spot, so 4 sharing pairs and (8 + 8) / 64 = 25 % of one frame's pattern in the mean, against 12.5 % ideal (R-048).</summary>
    [Fact]
    public void R048_ThePatternSurvivingInTheStackReadsThePairsAtOneSpot()
    {
        SeriesResult r = RunPattern();
        Assert.Equal(4, r.Dither.SharingPairs);
        Assert.Equal(0.25, r.Dither.PatternSurviving!.Value, 0.001);
        ReportRow row = Assert.Single(SeriesReport.DitherRows(r), x => x.Label == "pattern in stiva");
        Assert.Equal("in stiva ramane 25.0 % din pattern-ul unui cadru (ideal 12.5 %, cu 8 cadre; 4 perechi de cadre au stat la sub 1.5 px una de alta, iar pattern-ul lor se aduna)", row.Value);
        Assert.Equal(row.Value, Assert.Single(SeriesReport.DitherScalarRows(r), x => x.Label == "pattern in stiva").Value);
    }

    /// <summary>
    /// The Dithering texts (the console's rows, the window's rows and tables, the explanation)
    /// name none of the method's words in either language, over sessions that reach the
    /// pattern's and the verdict's branches; the eight-frame pattern's verdict reads as R-077
    /// quotes it (R-077).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R077_TheDitheringTextsAvoidTheMethodsVocabulary(bool english)
    {
        // Eight frames within 3 px of one spot: the pattern cannot be read and the hot pixels warn.
        (double Dx, double Dy, double Elongation, double PauseSeconds)[] still = [.. Enumerable.Range(0, 8).Select(i => (0.3 * i, 0.2 * i, 1.0, 1.0))];
        // Twelve frames drifting 1 px each along Y, no move: a long streak, INSUFFICIENT.
        (double Dx, double Dy, double Elongation, double PauseSeconds)[] drifting = [.. Enumerable.Range(0, 12).Select(i => (0.0, 1.0 * i, 1.0, 1.0))];
        // Five frames at one spot and one 40 px away: one frame alone, worded in the singular.
        (double Dx, double Dy, double Elongation, double PauseSeconds)[] oneAway = [(0, 0, 1.0, 0), (0.5, 0.3, 1.0, 1), (1.0, 0.5, 1.0, 1), (1.2, 0.8, 1.0, 1), (40, 0, 1.0, 30), (1.5, 1.0, 1.0, 30)];
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        using SessionFixture pattern = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using SessionFixture darks = new SessionFixture().WithStarSeries(SessionFixture.Pattern, darks: 3, sensorPattern: true);
        using SessionFixture few = new SessionFixture().WithStarSeries(SessionFixture.Pattern.Take(4).ToList());
        using SessionFixture stayed = new SessionFixture().WithStarSeries(still);
        using SessionFixture walked = new SessionFixture().WithStarSeries(drifting);
        using SessionFixture lone = new SessionFixture().WithStarSeries(oneAway);
        List<SeriesResult> results = [.. new[] { pattern, darks, few, stayed, walked, lone }.Select(f => SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2)))];

        foreach (SeriesResult r in results)
        {
            foreach (ReportRow row in SeriesReport.DitherRows(r).Concat(SeriesReport.DitherScalarRows(r)))
            {
                PlainWords.AssertPlain($"{row.Label}: {row.Verdict} {row.Value}", "a DITHERING row");
            }

            foreach (ReportTable t in SeriesReport.DitherTables(r))
            {
                PlainWords.AssertPlain(string.Join(" | ", t.Columns.Prepend(t.Title).Concat(t.Rows.SelectMany(cells => cells))), "a DITHERING table");
            }
        }

        foreach (ReportParagraph p in SeriesReport.DitherExplanation)
        {
            PlainWords.AssertPlain(p.Text, "the DITHERING explanation");
        }

        Assert.Equal(1, results[5].Dither.IsolatedLoose);
        Assert.Contains(english ? ", 1 frame alone, " : ", 1 cadru singur, ", SeriesReport.DitherScalarRows(results[5])[^1].Value, StringComparison.Ordinal);

        if (!english)
        {
            Assert.Equal(
                "dare de walking noise de cel mult 0.5 px (sub 3 px nu se vad): driftul e de 0.50 px intr-o expunere; cadrele 1-2, 2 cadre la rand in acelasi loc, la 10 px; pattern-ul se aduna pe loc: in stiva ramane 25.0 % din pattern-ul unui cadru (ideal 12.5 %, bine pana la 18.8 %); 3 miscari la 8 cadre, 4 locuri diferite (la 10 px), 0 cadre singure, 0 miscari pe ambele axe",
                SeriesReport.DitherScalarRows(results[0])[^1].Value);
        }
    }

    /// <summary>
    /// The Summary tab's texts (the session lines with the scale, then REZUMAT, FOCUS, EXPUNERE
    /// SI ZGOMOT, ROUA, CADRE MISCATE and DRIFT SI ROTATIE, as the console prints them) name none
    /// of the method's words in either language, over sessions that reach their branches; the
    /// eight-frame pattern's FOCUS verdict reads as R-078 quotes it (R-078).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R078_TheSummaryTextsAvoidTheMethodsVocabulary(bool english)
    {
        // Frames 1 px apart, each after a pause: every pair is a paused one, the drift approximate.
        (double Dx, double Dy, double Elongation, double PauseSeconds)[] paused = [(0, 0, 1.0, 0), (1, 0.5, 1.0, 30), (2, 1, 1.0, 30), (3, 1.5, 1.0, 30), (4, 2, 1.0, 30)];
        // Siril's solve at DEC +30, as R050's: the polar rows with a declination.
        string[] solve =
        [
            FitsTestData.Card("CTYPE1", "'RA---TAN'"), FitsTestData.Card("CTYPE2", "'DEC--TAN'"),
            FitsTestData.Card("CRVAL1", "314.43"), FitsTestData.Card("CRVAL2", "30.0"),
            FitsTestData.Card("CDELT1", "-0.00172"), FitsTestData.Card("CDELT2", "0.00172"),
            FitsTestData.Card("PC1_1", "0.0037"), FitsTestData.Card("PC1_2", "1"), FitsTestData.Card("PC2_1", "1"), FitsTestData.Card("PC2_2", "-0.0037"),
        ];
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        using SessionFixture pattern = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2);
        using SessionFixture noBiases = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using SessionFixture classes = new SessionFixture().WithStarSeries(SessionFixture.ClassesPattern, biases: 2, darks: 2);
        File.WriteAllBytes(Path.Combine(classes.LightsDir, "DSC_0009.fit"), new byte[100]);
        using SessionFixture drifting = new SessionFixture().WithStarSeries(paused);
        using SessionFixture shortSolved = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        File.WriteAllBytes(Path.Combine(shortSolved.Root, "result.fit"), FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: solve));
        using SessionFixture solved = new SessionFixture().WithStarSeries(SessionFixture.Pattern, exposure: 600);
        File.WriteAllBytes(Path.Combine(solved.Root, "result.fit"), FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: solve));

        string patternSummary = "";
        foreach (SessionFixture f in new[] { pattern, noBiases, classes, drifting, shortSolved, solved })
        {
            using StringWriter sw = new();
            SeriesReport.Write(SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2)), sw);
            string text = sw.ToString().Replace(f.Root, "", StringComparison.Ordinal);
            string summary = text[..text.IndexOf("  DITHERING", StringComparison.Ordinal)];
            foreach (string line in summary.Split('\n'))
            {
                PlainWords.AssertPlain(line, "the session lines or a Summary section");
            }

            patternSummary = f == pattern ? summary : patternSummary;
        }

        if (!english)
        {
            Assert.Contains("    verdict            : BUN - latimea stelelor (FWHM) e in medie 3.22 px = 19.3\" pe cadrele bune; 3.22 px la inceput si 3.22 px la sfarsit (+0 %): focusul a tinut toata sesiunea", patternSummary, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The reasons of every flag kind, the frame table's and the rejected list's reasons of the
    /// classes session, and the lines under a frame's star cut-outs name none of the method's
    /// words in either language; the Romanian reasons read as R-079 quotes them (R-079).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R079_TheReasonsAvoidTheMethodsVocabulary(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        FrameFlag[] flags =
        [
            new(FlagKind.Trailed, 1.76, Th.ElongationBad, "-90", TrailAxis.Ra),
            new(FlagKind.MarginalTrail, 1.50, Th.ElongationMarginal, "-85", TrailAxis.Dec),
            new(FlagKind.Trailed, 1.80, Th.ElongationBad, "88"),
            new(FlagKind.Tail, 1.83, 1.21, "88", TrailAxis.Ra, AfterMove: true),
            new(FlagKind.Tail, 1.95, 1.30, "-80"),
            new(FlagKind.Soft, 4.5, 4.06),
            new(FlagKind.Clouds, 400, 600),
            new(FlagKind.BackgroundJump, 25, Th.BackgroundJumpPct),
            new(FlagKind.Trails, 1, 0),
            new(FlagKind.Trails, 3, 0),
            new(FlagKind.Unregistered, 0, 0),
            new(FlagKind.Unreadable, 0, 0, "x"),
        ];
        foreach (FrameFlag flag in flags)
        {
            PlainWords.AssertPlain(SeriesReport.Reason(flag), $"the reason of {flag.Kind}");
        }

        using SessionFixture classes = new SessionFixture().WithStarSeries(SessionFixture.ClassesPattern, biases: 2);
        File.WriteAllBytes(Path.Combine(classes.LightsDir, "DSC_0009.fit"), new byte[100]);
        SeriesResult r = SeriesRun.Run(classes.Root, new SeriesOptions(Threads: 2));
        foreach (string reasons in SeriesReport.FrameRows(r).Select(row => row.Reasons).Concat(SeriesReport.RejectedRows(r).Select(row => row.Reasons)))
        {
            PlainWords.AssertPlain(reasons, "a frame's reasons");
        }

        AnalyzedFrame measured = r.Frames.First(f => f.Measurement is not null);
        foreach (StarCrop crop in StarCrops.Build(measured.Info.Path, measured.Measurement!))
        {
            PlainWords.AssertPlain(SeriesReport.CropCaption(crop), "the line under a cut-out");
        }

        if (!english)
        {
            Assert.Equal("alungire 1.76 (de la 1.73 cadrul e slab), dara pe RA (-90 de grade)", SeriesReport.Reason(flags[0]));
            Assert.Equal("1 dara de satelit sau avion (Siril o scoate la stivuire)", SeriesReport.Reason(flags[8]));
            Assert.Equal("3 dare de satelit sau avion (Siril le scoate la stivuire)", SeriesReport.Reason(flags[9]));
        }
    }

    /// <summary>
    /// The Rejected heading counts the bad and the unread frames, singular for one, on the
    /// pattern session (2 bad) and the classes session with an unreadable frame (1 bad, 1 unread),
    /// and neither it nor the explanation's paragraphs name any of the method's words, in either
    /// language (R-081).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R081_TheRejectedHeadingAndExplanationAreInPlainWords(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        SeriesResult pattern = RunPattern();
        using SessionFixture classes = new SessionFixture().WithStarSeries(SessionFixture.ClassesPattern, biases: 2);
        File.WriteAllBytes(Path.Combine(classes.LightsDir, "DSC_0009.fit"), new byte[100]);
        SeriesResult mixed = SeriesRun.Run(classes.Root, new SeriesOptions(Threads: 2));

        Assert.Equal(
            english ? "2 bad frames; the marginal ones stay in the stack, your choice" : "2 cadre slabe; cele la limita raman in stiva, la alegerea ta",
            SeriesReport.RejectedHeading(pattern));
        Assert.Equal(
            english ? "2 bad frames and 1 unread frame; the marginal ones stay in the stack, your choice" : "2 cadre slabe si 1 cadru necitit; cele la limita raman in stiva, la alegerea ta",
            SeriesReport.RejectedHeading(mixed));
        foreach (string text in SeriesReport.RejectedExplanation.Select(p => p.Text).Append(SeriesReport.RejectedHeading(mixed)))
        {
            PlainWords.AssertPlain(text, "the Rejected tab");
        }
    }

    /// <summary>
    /// A Romanian count of 20 and up takes "de" before the word it counts, unless its last two
    /// digits are 01 to 19; a figure with decimals or a text that is no number takes none; the
    /// Romanian report of a 24-frame session writes every count so, its English report as before
    /// (R-082).
    /// </summary>
    [Fact]
    public void R082_ARomanianCountOfTwentyAndUpTakesDe()
    {
        Assert.Equal("0 ", Ui.RoCount(0));
        Assert.Equal("1 ", Ui.RoCount(1));
        Assert.Equal("19 ", Ui.RoCount(19));
        Assert.Equal("20 de ", Ui.RoCount(20));
        Assert.Equal("99 de ", Ui.RoCount(99));
        Assert.Equal("100 de ", Ui.RoCount(100));
        Assert.Equal("101 ", Ui.RoCount(101));
        Assert.Equal("119 ", Ui.RoCount(119));
        Assert.Equal("120 de ", Ui.RoCount(120));
        Assert.Equal("-90 de ", Ui.RoCount("-90"));
        Assert.Equal("-5 ", Ui.RoCount("-5"));
        Assert.Equal("2.0 ", Ui.RoCount("2.0"));
        Assert.Equal("- ", Ui.RoCount("-"));

        (double Dx, double Dy, double Elongation, double PauseSeconds)[] frames = [.. SessionFixture.Pattern, .. SessionFixture.Pattern, .. SessionFixture.Pattern];
        using SessionFixture f = new SessionFixture().WithStarSeries(frames, biases: 2);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));
        string Report(UiLanguage language)
        {
            using IDisposable _ = Ui.Use(language);
            using StringWriter w = new();
            SeriesReport.Write(r, w);
            return w.ToString();
        }

        string ro = Report(UiLanguage.Romanian);
        Assert.Contains(@"lights\ (24 de cadre .fit)", ro, StringComparison.Ordinal);
        Assert.Contains("din 24 de cadre", ro, StringComparison.Ordinal);
        Assert.Contains("(-90 de grade)", ro, StringComparison.Ordinal);
        Regex counted = new(@"(?<![\w.:-])(-?\d+) (?!de )(cadre|cadru|perechi|miscari|locuri|ori|grade|dark-uri|pixeli|stele|slabe|bune|necitite|singure|detectate|luminoase|masurate|retinute)\b");
        foreach (Match m in counted.Matches(ro))
        {
            long n = Math.Abs(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)), last = n % 100;
            Assert.False(n != 0 && (last == 0 || last >= 20), $"a count of 20 and up without de: {m.Value}");
        }

        Assert.Contains(@"lights\ (24 frames .fit)", Report(UiLanguage.English), StringComparison.Ordinal);
    }

    [Fact]
    public void R019_TheDitherPatternIsReadBack()
    {
        DitherAnalysis d = RunPattern().Dither;
        Assert.Equal([false, false, true, false, true, false, true, false], d.IsMove);
        Assert.Equal(3, d.Moves);
        Assert.Equal(3, d.Pauses);
        Assert.Equal(0, d.PausesWithoutMove);
        Assert.Equal(2, d.FramesBetweenMoves[2]);
        Assert.Equal(2, d.MovesOnXOnly);
        Assert.Equal(1, d.MovesOnYOnly);
        Assert.Equal(4, d.DistinctLoose);
    }

    [Fact]
    public void R023_TrendsAndDriftOnAStableSession()
    {
        SeriesAnalysis s = RunPattern().Series;
        Assert.True(s.FocusHeld);
        Assert.False(s.DewSuspected);
        Assert.InRange(Math.Abs(s.DriftXPerMin!.Value), 0, 0.5);
        Assert.InRange(Math.Abs(s.RotationTotalDeg!.Value), 0, 0.05);
        Assert.Equal(Axis.Y, s.RaAxis);   // the trailed frames trail vertically
        Assert.Contains("darelor", s.AxisSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// The exposure verdict of SUMMARY is taken on the ratio as the row shows it, with one
    /// decimal, so the word never stands against its figure (R-024, ADR 0067, CR-017): a
    /// measured 9.96 reads `10.0 (good from 10)` and GOOD, 9.94 reads `9.9` and MARGINAL. The
    /// row gives what the read noise adds with one decimal. The pattern session's result with
    /// a noise sample of one frame whose blue channel carries the ratio.
    /// </summary>
    [Theory]
    [InlineData(9.96, "10.0", true, true)]
    [InlineData(9.96, "10.0", true, false)]
    [InlineData(9.94, "9.9", false, true)]
    [InlineData(9.94, "9.9", false, false)]
    [InlineData(10.04, "10.0", true, true)]
    public void R024_TheExposureVerdictIsTakenOnTheRatioAsItIsShown(double ratio, string shown, bool good, bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        SeriesResult pattern = RunPattern();
        static ChannelNoise Channel(string name, double r) => new(name, 2000, 40, 38, 1.4, r, 100 * (Math.Sqrt(1 + 1 / r) - 1));
        IReadOnlyList<IReadOnlyList<ChannelNoise>> sample = [[Channel("R", 14.2), Channel("G1", 31.0), Channel("G2", 30.5), Channel("B", ratio)]];
        SeriesResult r = pattern with { Series = pattern.Series with { Swamping = sample, SwampingFrames = [1] } };

        ReportRow row = Assert.Single(SeriesReport.SummaryRows(r), x => x.Label == (english ? "exposure" : "expunere"));

        Assert.Equal(good ? (english ? "GOOD" : "BUNA") : (english ? "MARGINAL" : "LA LIMITA"), row.Verdict);
        Assert.EndsWith(
            english
                ? $"; the weakest is channel B: shot/read {shown} (good from 10), the read noise adds 4.9 %"
                : $"; cel mai slab e canalul B: shot/citire {shown} (bine e de la 10), zgomotul de citire adauga 4.9 %",
            row.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// The `what it means` row says what the read noise adds from the threshold up with a
    /// figure worked out from the threshold: the first whole percent above it (R-024).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R024_TheWhatItMeansRowTakesItsPercentFromTheThreshold(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        double atThreshold = NoiseAnalyzer.PenaltyPct(Th.SwampingRatioOk);
        Assert.InRange(atThreshold, 4.85, 4.9);
        string percent = (Math.Floor(atThreshold) + 1).ToString(CultureInfo.InvariantCulture);

        string value = Assert.Single(SeriesReport.NoiseRows(RunPattern(biases: 2)), x => x.Label == (english ? "what it means" : "ce inseamna")).Value;

        Assert.Equal("5", percent);
        Assert.Contains(english ? $", which adds under {percent} %; under 10: " : $", care adauga sub {percent} %; sub 10: ", value, StringComparison.Ordinal);
    }

    [Fact]
    public void R024_ReadNoiseFromTheBiasesGivesTheSwampingRatios()
    {
        SeriesResult r = RunPattern(biases: 2);
        SeriesAnalysis s = r.Series;
        Assert.NotNull(s.ReadNoise);
        Assert.All(s.ReadNoise!, n => Assert.InRange(n.PairSigma, 10, 14));   // 12 ADU of noise drawn into the biases
        Assert.NotNull(s.Swamping);
        Assert.Equal(3, s.SwampingFrames.Count);
        Assert.All(s.Swamping!.SelectMany(x => x), c => Assert.True(c.SwampingRatio > 3, $"{c.Name} ratio {c.SwampingRatio}"));
    }

    /// <summary>A channel name with a comma in it, the CFA[row,col] of a mosaic of unknown order, is quoted: every CSV row keeps the header's cell count (R-028).</summary>
    [Fact]
    public void R028_AChannelNameWithACommaIsQuotedSoEveryRowHasTheHeadersCells()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern.Take(3).ToList(), bayer: false);
        SeriesResult r = SeriesRun.Run(s.Root, new SeriesOptions(Threads: 2));

        string[] csv = SeriesOutputs.Csv(r).Split('\n');
        string[] head = CsvCells(csv[0]);
        Assert.Equal("CFA[0,0]", CsvCells(csv[1])[Array.IndexOf(head, "ch0_name")]);
        Assert.Equal("CFA[1,1]", CsvCells(csv[1])[Array.IndexOf(head, "ch3_name")]);
        Assert.All(csv.Where(l => l.Length > 0), l => Assert.Equal(head.Length, CsvCells(l).Length));
    }

    /// <summary>
    /// The three files are refused in the folder of the lights, of the biases and of the darks and
    /// in a folder inside one of them, with a sentence that names the folder and says why, and
    /// nothing is created (R-028, ADR 0060, CR-014). The folder is the same one with a trailing
    /// separator or, on Windows, in another case.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R028_TheFilesAreRefusedInAnyFolderThatHoldsTheSessionsFrames(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2, darks: 2);
        SeriesResult r = SeriesRun.Run(s.Root, new SeriesOptions(Threads: 2));
        string biases = Path.Combine(s.Root, "biases"), darks = Path.Combine(s.Root, "darks"), inside = Path.Combine(s.LightsDir, "out", "today");
        string[] before = Directory.GetFileSystemEntries(s.Root, "*", SearchOption.AllDirectories);
        (string Dir, string En, string Ro)[] cases =
        [
            (s.LightsDir, "it is the folder with the session's lights", "e folderul cu cadrele lights ale sesiunii"),
            (biases, "it is the folder with the session's biases", "e folderul cu bias-urile sesiunii"),
            (darks, "it is the folder with the session's darks", "e folderul cu dark-urile sesiunii"),
            (inside, "it is inside the folder with the session's lights", "e in folderul cu cadrele lights ale sesiunii"),
        ];

        foreach ((string dir, string en, string ro) in cases)
        {
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => SeriesOutputs.WriteAll(r, dir));
            Assert.Equal(
                english
                    ? $"the files are not written into {dir}: {en}, and LightsCheck writes nothing among the frames. Choose another folder."
                    : $"fisierele nu se scriu in {dir}: {ro}, iar LightsCheck nu scrie nimic printre cadre. Alege alt folder.",
                e.Message);
            PlainWords.AssertPlain(e.Message, "the refusal of an output folder");
        }

        Assert.Throws<InvalidOperationException>(() => SeriesOutputs.WriteAll(r, s.LightsDir + Path.DirectorySeparatorChar));
        Assert.Throws<InvalidOperationException>(() => SeriesOutputs.WriteAll(r, Path.Combine(s.Root, "report", "..", "lights")));
        if (OperatingSystem.IsWindows())
        {
            Assert.Throws<InvalidOperationException>(() => SeriesOutputs.WriteAll(r, biases.ToUpperInvariant()));
        }

        Assert.Equal(before, Directory.GetFileSystemEntries(s.Root, "*", SearchOption.AllDirectories));
    }

    /// <summary>
    /// Beside the frames the files are written: in the session folder that holds <c>lights\</c>,
    /// in a new folder inside it, and in a folder whose name only begins like the lights' (R-028,
    /// ADR 0060).
    /// </summary>
    [Fact]
    public void R028_AFolderBesideTheFramesIsWritten()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2);
        SeriesResult r = SeriesRun.Run(s.Root, new SeriesOptions(Threads: 2));

        foreach (string dir in new[] { s.Root, Path.Combine(s.Root, "report"), s.LightsDir + "-report" })
        {
            Assert.Null(SeriesOutputs.Refusal(r.Session, dir));
            Assert.All(SeriesOutputs.WriteAll(r, dir), p => Assert.True(File.Exists(p)));
        }
    }

    /// <summary>
    /// When the lights sit in the session folder itself, that folder is refused, with one more
    /// sentence, and a new folder inside it is not; its <c>biases\</c> is refused as anywhere
    /// (R-028, ADR 0060, CR-014).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R028_WhenTheLightsSitInTheSessionFolderThatFolderIsRefusedAndOneInsideItIsNot(bool english)
    {
        using IDisposable _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        using SessionFixture s = new SessionFixture().WithLights(2, inSubfolder: false, biases: 2);
        SessionFolder session = SessionFolder.Open(s.Root);
        Assert.Equal(session.Root, session.LightsDir);

        string? refusal = SeriesOutputs.Refusal(session, s.Root);

        Assert.Equal(
            english
                ? $"the files are not written into {s.Root}: it is the folder with the session's lights, and LightsCheck writes nothing among the frames. Choose another folder. A new folder inside it is fine."
                : $"fisierele nu se scriu in {s.Root}: e folderul cu cadrele lights ale sesiunii, iar LightsCheck nu scrie nimic printre cadre. Alege alt folder. Un folder nou in el e bun.",
            refusal);
        PlainWords.AssertPlain(refusal!, "the refusal of the session folder");
        Assert.Null(SeriesOutputs.Refusal(session, Path.Combine(s.Root, "report")));
        Assert.Contains(english ? "it is inside the folder with the session's biases" : "e in folderul cu bias-urile sesiunii",
            SeriesOutputs.Refusal(session, Path.Combine(s.Root, "biases", "report")), StringComparison.Ordinal);

        // Through the console: refused before any frame is measured, nothing written.
        string[] before = Directory.GetFileSystemEntries(s.Root, "*", SearchOption.AllDirectories);
        (int code, string stdout, string stderr) = Cli.Run(s.Root, "--out", s.Root);
        Assert.Equal(1, code);
        Assert.Equal(RunReport.Error(refusal!), stderr.Trim());
        Assert.DoesNotContain(english ? "=== SESSION" : "=== SESIUNE", stdout, StringComparison.Ordinal);
        Assert.Equal(before, Directory.GetFileSystemEntries(s.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void R028_TheOutputsAreWrittenWhereAskedAndNeverIntoLights()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace outDir = new();
        SeriesResult r = SeriesRun.Run(s.Root, new SeriesOptions(Threads: 2));
        IReadOnlyList<string> written = SeriesOutputs.WriteAll(r, outDir.Dir);

        Assert.Equal(3, written.Count);
        Assert.All(written, p => Assert.True(File.Exists(p)));
        Assert.Equal("DSC_0003.fit\nDSC_0006.fit\n", File.ReadAllText(Path.Combine(outDir.Dir, SeriesOutputs.RejectedName)));
        string[] csv = File.ReadAllLines(Path.Combine(outDir.Dir, SeriesOutputs.CsvName));
        Assert.Equal(9, csv.Length);
        Assert.StartsWith("index,file,time,exposure_s", csv[0], StringComparison.Ordinal);
        // The header and a row without quoted commas (a good frame, no reasons) have the same number of columns.
        int columns = csv[0].Split(',').Length;
        Assert.Equal(64, columns);   // 57 before R-033 (trail_on), R-034 (three spot_run_*) and R-038 (three move_*)
        Assert.Equal(columns, csv[1].Split(',').Length);
        Assert.StartsWith("3,\"DSC_0003.fit\",", csv[3], StringComparison.Ordinal);
        Assert.Contains("SLAB", csv[3], StringComparison.Ordinal);
        Assert.Contains("=== SESIUNE:", File.ReadAllText(Path.Combine(outDir.Dir, SeriesOutputs.ReportName)), StringComparison.Ordinal);
        Assert.Equal(8, Directory.GetFiles(s.LightsDir).Length);
    }

    /// <summary>
    /// A FITS at the session root that Windows refuses to open is skipped as the plate solve and
    /// the next one is read; the series gives its report, where the refusal used to end the run
    /// after every frame was measured (R-025, R-026, ADR 0062, CR-015). Windows only: the test
    /// sets a rule on the file that denies reading it.
    /// </summary>
    [Fact]
    public void R025_AFitsAtTheRootThatCannotBeOpenedIsSkippedAsThePlateSolve()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        string[] wcs =
        [
            FitsTestData.Card("CTYPE1", "'RA---TAN'"), FitsTestData.Card("CTYPE2", "'DEC--TAN'"),
            FitsTestData.Card("PC1_1", "0.0037"), FitsTestData.Card("PC1_2", "-0.99999"),
        ];
        FileInfo denied = new(Path.Combine(s.Root, "a-denied.fit"));
        File.WriteAllBytes(denied.FullName, FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: wcs));
        File.WriteAllBytes(Path.Combine(s.Root, "result.fit"), FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: wcs));
        FileSecurity acl = denied.GetAccessControl();
        FileSystemAccessRule deny = new(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny);
        acl.AddAccessRule(deny);
        denied.SetAccessControl(acl);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => File.OpenRead(denied.FullName).Dispose());   // the rule holds on this machine

            using (LogCapture capture = LogCapture.Start())
            {
                Assert.Equal(Axis.Y, SeriesRun.WcsRaAxis(s.Root));
                LogCapture.Captured line = Assert.Single(capture.Events, e => e.Level == NLog.LogLevel.Warn);
                Assert.Equal(denied.FullName + " could not be read; skipped as a WCS source", line.Message);
                Assert.IsType<UnauthorizedAccessException>(line.Exception);
            }

            (int code, string stdout, string stderr) = Cli.Run(s.Root, "--threads", "2");

            Assert.Equal(0, code);
            Assert.DoesNotContain("EROARE", stderr, StringComparison.Ordinal);
            Assert.Contains("(aflat din plate solve (WCS))", stdout, StringComparison.Ordinal);
        }
        finally
        {
            acl.RemoveAccessRule(deny);
            denied.SetAccessControl(acl);
        }
    }

    /// <summary>
    /// A session folder that can no longer be listed when the plate solve is looked for (the
    /// drive unplugged, the folder renamed during the minutes of measuring) gives no plate
    /// solve and a line in the log, not an error that ends the run (R-025, ADR 0062; the review
    /// of 2026-11-16).
    /// </summary>
    [Fact]
    public void R025_ASessionFolderThatCannotBeListedGivesNoPlateSolve()
    {
        using TempWorkspace ws = new();
        string gone = ws.PathTo("gone");
        using LogCapture capture = LogCapture.Start();

        Assert.Null(SeriesRun.ReadWcs(gone));

        LogCapture.Captured line = Assert.Single(capture.Events, e => e.Level == NLog.LogLevel.Warn);
        Assert.Equal(gone + " could not be listed; no plate solve is read", line.Message);
        Assert.IsAssignableFrom<IOException>(line.Exception);
    }

    /// <summary>
    /// The `field rotation` row without a corner displacement, as when no frame carries an
    /// exposure, is the total and the rate alone; with one, it ends with the corner (R-023,
    /// CR-011).
    /// </summary>
    [Fact]
    public void R023_TheRotationRowHasNoCornerPartWithoutACornerDisplacement()
    {
        SeriesResult pattern = RunPattern();
        SeriesAnalysis turned = pattern.Series with { RotationTotalDeg = 0.1, RotationPerHourDeg = 1.2, CornerShiftPerExposurePx = null };

        string without = Assert.Single(SeriesReport.DriftRows(pattern with { Series = turned }), x => x.Label == "rotatie de camp").Value;
        string with = Assert.Single(SeriesReport.DriftRows(pattern with { Series = turned with { CornerShiftPerExposurePx = 2.686 } }), x => x.Label == "rotatie de camp").Value;

        Assert.Equal("imaginea s-a rotit cu 6.0' in total, 72.0' pe ora", without);
        Assert.Equal(without + "; din cauza asta, o stea din colt se misca 2.69 px intr-o expunere", with);
    }

    [Fact]
    public void R025_TheRaAxisComesFromAPlateSolvedFitsAtTheRootWhenThereIsOne()
    {
        using SessionFixture s = new SessionFixture().WithLights(1);
        Assert.Equal(Axis.Unknown, SeriesRun.WcsRaAxis(s.Root));
        string[] wcs =
        [
            FitsTestData.Card("CTYPE1", "'RA---TAN'"), FitsTestData.Card("CTYPE2", "'DEC--TAN'"),
            FitsTestData.Card("PC1_1", "0.0037"), FitsTestData.Card("PC1_2", "-0.99999"),
        ];
        File.WriteAllBytes(Path.Combine(s.Root, "result.fit"), FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: wcs));
        Assert.Equal(Axis.Y, SeriesRun.WcsRaAxis(s.Root));
    }

    /// <summary>The pattern's trails run along Y, the axis the series takes for RA: the reason, the row and the CSV say so.</summary>
    [Fact]
    public void R033_TrailedFramesNameTheAxisOfTheirTrail()
    {
        SeriesResult r = RunPattern();
        SeriesAnalysis s = r.Series;
        Assert.Equal(Axis.Y, s.RaAxis);
        FrameFlag flag = Assert.Single(s.Flags[2]);
        Assert.Equal(TrailAxis.Ra, flag.On);
        Assert.Equal($"alungire {RunReport.F(flag.Value, 2)} (de la 1.73 cadrul e slab), dara pe RA (-90 de grade)", SeriesReport.Reasons(s.Flags[2]));
        Assert.Equal(TrailAxis.Ra, s.TrailOn);
        Assert.Contains(", adica pe RA", SeriesReport.TrailRows(r).Single(x => x.Label == "directia darelor").Value, StringComparison.Ordinal);

        string[] csv = SeriesOutputs.Csv(r).Split('\n');
        int col = Array.IndexOf(CsvCells(csv[0]), "trail_on");
        Assert.True(col > 0, "no trail_on column");
        Assert.Equal("RA", CsvCells(csv[3])[col]);
        Assert.Equal("", CsvCells(csv[1])[col]);
    }

    /// <summary>Three frames within 10 px of the first make a run; the table cell names the frame's place in it, the class does not change.</summary>
    [Fact]
    public void R034_EveryRegisteredFrameNamesItsRunAtTenPixels()
    {
        (double Dx, double Dy, double Elongation, double PauseSeconds)[] pattern = [(0, 0, 1.0, 0), (1, 0.5, 1.0, 1), (0.5, 1.5, 1.0, 1), (30, 0, 1.0, 30), (30.5, 0.2, 1.0, 1)];
        using SessionFixture f = new SessionFixture().WithStarSeries(pattern);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));

        IReadOnlyList<FrameTableRow> rows = SeriesReport.FrameRows(r);
        Assert.Equal(["1/3 de la 1", "2/3 de la 1", "3/3 de la 1", "-", "-"], rows.Select(x => x.Spot));
        Assert.All(r.Series.Classes, c => Assert.Equal(FrameClass.Good, c));
        Assert.All(r.Series.Flags, fl => Assert.Empty(fl));

        using StringWriter sw = new();
        SeriesReport.Write(r, sw);
        string text = sw.ToString();
        Assert.Matches("      dY  dithering {33}  acelasi loc     motive", text);   // the dithering column of R-038 sits between dY and acelasi loc
        Assert.Contains("  3/3 de la 1    ", text, StringComparison.Ordinal);

        string[] csv = SeriesOutputs.Csv(r).Split('\n');
        string[] head = CsvCells(csv[0]);
        int index = Array.IndexOf(head, "spot_run_index"), frames = Array.IndexOf(head, "spot_run_frames"), first = Array.IndexOf(head, "spot_run_first");
        Assert.True(index > 0 && frames == index + 1 && first == index + 2, "spot_run columns missing or out of order");
        Assert.Equal(["2", "3", "1"], CsvCells(csv[2])[index..(index + 3)]);
        Assert.Equal(["", "", ""], CsvCells(csv[4])[index..(index + 3)]);
    }

    /// <summary>The words and tables the window arranges come from the report: axis labels, the FOCUS verdict row, the explanation paragraphs, the dithering tables (R-036).</summary>
    [Fact]
    public void R036_TheReportGivesTheAxisWordsTheFocusVerdictTheParagraphsAndTheDitherTables()
    {
        SeriesResult r = RunPattern();
        Assert.Equal(("X = DEC", "Y = RA"), SeriesReport.AxisLabels(r.Series));
        Assert.Equal(("dX (DEC)", "dY (RA)"), SeriesReport.PositionHeaders(r.Series));

        ReportRow focus = SeriesReport.FocusRows(r)[^1];
        Assert.Equal("verdict", focus.Label);
        Assert.Equal("BUN", focus.Verdict);
        Assert.StartsWith("latimea stelelor (FWHM) e in medie 3.22 px", focus.Value, StringComparison.Ordinal);
        using StringWriter sw = new();
        SeriesReport.Write(r, sw);
        Assert.Contains("    verdict            : BUN - latimea stelelor (FWHM) e in medie 3.22 px = 19.3\" pe cadrele bune; 3.22 px la inceput si 3.22 px la sfarsit (+0 %): focusul a tinut toata sesiunea", sw.ToString(), StringComparison.Ordinal);

        IReadOnlyList<ReportParagraph> paragraphs = SeriesReport.RejectedExplanation;
        Assert.True(paragraphs.Count >= 3, "fewer than three paragraphs");
        ReportParagraph bold = Assert.Single(paragraphs, p => p.Emphasis);
        Assert.StartsWith("O dara pe RA", bold.Text, StringComparison.Ordinal);
        Assert.Contains("pe DEC", bold.Text, StringComparison.Ordinal);

        IReadOnlyList<ReportTable> t = SeriesReport.DitherTables(r);
        Assert.Equal(["cat de des", "cat de mult", "directie", "apropierea cadrelor", "grupuri in acelasi loc"], t.Select(x => x.Title));
        Assert.Equal(["o miscare la fiecare", "de cate ori"], t[0].Columns);
        Assert.Equal(["2 cadre | 2 ori"], Rows(t[0]));
        Assert.Equal(["cat de mare", "cate miscari"], t[1].Columns);
        Assert.Equal(["in medie | 15.32 px = 91.7\"", "5-10 px | 0", "10-20 px | 2", "20+ px | 1"], Rows(t[1]));
        Assert.Equal(["axa", "doar pe ea", "spre +", "spre -"], t[2].Columns);
        Assert.Equal(["X = DEC | 2 | 2 | 0", "Y = RA | 1 | 0 | 1", "pe ambele | 0 |  | "], Rows(t[2]));
        Assert.Equal(["distanta", "cu alt cadru mai aproape", "locuri diferite", "grupuri de 3+ cadre la rand"], t[3].Columns);
        Assert.Equal(["3 px | 8 | - | -", "5 px | 8 | 4 | 0", "10 px | 8 | 4 | 0"], Rows(t[3]));
        Assert.Equal(["distanta", "cadrele", "cate", "cat timp"], t[4].Columns);
        Assert.Equal(["5 px | niciunul | - | -", "10 px | niciunul | - | -"], Rows(t[4]));

        // The scalar rows of R-036 plus the `scara` and `pattern fix` rows of R-044 and R-046, the verdict last.
        Assert.Equal(["pauze de peste 5 s", "cel mai lung sir fara miscare", "apropierea cadrelor", "zona pozitiilor", "axe", "scara", "pattern in stiva", "pattern fix", "verdict"], SeriesReport.DitherScalarRows(r).Select(x => x.Label));
        Assert.Equal("40 px pe X, 16 px pe Y", SeriesReport.DitherScalarRows(r)[3].Value);
        Assert.StartsWith("RA e axa Y a imaginii, DEC axa X", SeriesReport.DitherScalarRows(r)[4].Value, StringComparison.Ordinal);
    }

    /// <summary>A run of three frames fills the runs table at both radii with its frames, its length and its minutes; a session without moves keeps the console's words (R-036).</summary>
    [Fact]
    public void R036_TheRunsTableListsEachRunAndTheHowOftenTableKeepsTheNoMoveWords()
    {
        (double Dx, double Dy, double Elongation, double PauseSeconds)[] pattern = [(0, 0, 1.0, 0), (1, 0.5, 1.0, 1), (0.5, 1.5, 1.0, 1), (30, 0, 1.0, 30), (30.5, 0.2, 1.0, 1)];
        using SessionFixture f = new SessionFixture().WithStarSeries(pattern);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));

        IReadOnlyList<ReportTable> t = SeriesReport.DitherTables(r);
        string[] runs = Rows(t[4]);
        Assert.Equal(2, runs.Length);
        Assert.StartsWith("5 px | 1-3 | 3 | ", runs[0], StringComparison.Ordinal);
        Assert.EndsWith(" min", runs[0], StringComparison.Ordinal);
        Assert.StartsWith("10 px | 1-3 | 3 | ", runs[1], StringComparison.Ordinal);
        Assert.Equal(["1 cadru | 1 data"], Rows(t[0]));   // one move, from frame 3 to 4
        Assert.Equal("3 px | 3 | - | -", Rows(t[3])[0]);

        (double Dx, double Dy, double Elongation, double PauseSeconds)[] still = [(0, 0, 1.0, 0), (0.5, 0.2, 1.0, 1), (0.2, 0.6, 1.0, 1)];
        using SessionFixture g = new SessionFixture().WithStarSeries(still);
        IReadOnlyList<ReportTable> u = SeriesReport.DitherTables(SeriesRun.Run(g.Root, new SeriesOptions(Threads: 2)));
        Assert.Equal(["nicio miscare | -"], Rows(u[0]));
    }

    private static string[] Rows(ReportTable t) => t.Rows.Select(row => string.Join(" | ", row)).ToArray();

    /// <summary>With a plate-solved FITS at the root the moves carry their sense and the arrows; without it, the components alone (R-038).</summary>
    [Fact]
    public void R038_TheFrameTableAndTheCsvCarryTheMoveOnTheSky()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        // The fixture's lights are FITS, read as the solve is, so the solve's y column is the
        // Veil matrix mirrored: the senses below are those of the raw session (ADR 0023).
        string[] wcs =
        [
            FitsTestData.Card("CTYPE1", "'RA---TAN'"), FitsTestData.Card("CTYPE2", "'DEC--TAN'"),
            FitsTestData.Card("CDELT1", "-0.00172"), FitsTestData.Card("CDELT2", "0.00172"),
            FitsTestData.Card("PC1_1", "0.0037"), FitsTestData.Card("PC1_2", "0.99999"),
            FitsTestData.Card("PC2_1", "0.99999"), FitsTestData.Card("PC2_2", "-0.0037"),
        ];
        File.WriteAllBytes(Path.Combine(s.Root, "result.fit"), FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: wcs));
        SeriesResult r = SeriesRun.Run(s.Root, new SeriesOptions(Threads: 2));

        IReadOnlyList<FrameTableRow> rows = SeriesReport.FrameRows(r);
        Assert.Equal("-", rows[0].Move);
        Assert.Equal("-", rows[1].Move);
        Assert.Matches("^DEC 1[12] px ↑ S$", rows[2].Move);   // frame 3: stars +11.5 px in X -> the mount went south, the up arrow by default
        Assert.Equal("-", rows[3].Move);
        Assert.Equal("RA 15 px ← V", rows[4].Move);      // frame 5: stars -15.3 px in Y -> the mount went west, the left arrow by default
        Assert.StartsWith("DEC 2", rows[6].Move, StringComparison.Ordinal);
        Assert.EndsWith(" px ↑ S", rows[6].Move, StringComparison.Ordinal);
        Assert.Null(rows[0].StepTip);
        Assert.Equal("cadrul 5: pas 15.3 px; RA 15 px ← V", rows[4].StepTip);
        Assert.Equal("cadrul 2: pas 0.5 px; -", rows[1].StepTip);

        using StringWriter sw = new();
        SeriesReport.Write(r, sw);
        Assert.Matches("      dY  dithering {33}  acelasi loc", sw.ToString());
        // A two-axis cell (41 characters at most, three-digit moves included) fits the column.
        Assert.True("RA 142 px ← V, DEC 129 px ↑ S".Length <= 42);

        string[] csv = SeriesOutputs.Csv(r).Split('\n');
        string[] head = CsvCells(csv[0]);
        int ra = Array.IndexOf(head, "move_ra_px"), dec = Array.IndexOf(head, "move_dec_px"), arrows = Array.IndexOf(head, "move_arrows");
        Assert.True(ra > 0 && dec == ra + 1 && arrows == ra + 2, "move_* columns missing or out of order");
        Assert.Equal(-15.3, double.Parse(CsvCells(csv[5])[ra], System.Globalization.CultureInfo.InvariantCulture), 0.5);
        Assert.Equal("←", CsvCells(csv[5])[arrows]);
        Assert.Equal(-11.5, double.Parse(CsvCells(csv[3])[dec], System.Globalization.CultureInfo.InvariantCulture), 0.5);
        Assert.Equal("", CsvCells(csv[1])[ra]);
        Assert.All(csv.Where(l => l.Length > 0), l => Assert.Equal(head.Length, CsvCells(l).Length));   // a two-arrow cell is quoted, the row keeps its cell count

        // Without the FITS: the axis from the trails, the components without a sense.
        File.Delete(Path.Combine(s.Root, "result.fit"));
        IReadOnlyList<FrameTableRow> plain = SeriesReport.FrameRows(SeriesRun.Run(s.Root, new SeriesOptions(Threads: 2)));
        Assert.Matches("^DEC 1[12] px$", plain[2].Move);
        Assert.Equal("RA 15 px", plain[4].Move);
    }

    /// <summary>The console takes the arrow keys as words, names them and their defaults in the help, and the report follows them (R-040).</summary>
    [Fact]
    public void R040_TheConsoleTakesTheArrowKeysAndTheReportFollowsThem()
    {
        Assert.Equal(ArrowKeys.Default, Options.Parse(["x"]).Arrows);
        Options o = Options.Parse(["x", "--est", "stanga", "--nord", "sus"]);   // both keys off the default
        Assert.Null(o.Error);
        Assert.Equal(new ArrowKeys(Arrow.Left, Arrow.Up), o.Arrows);
        Assert.Equal(new ArrowKeys(Arrow.Right, Arrow.Down), Options.Parse(["x", "--est", "dreapta", "--nord", "jos"]).Arrows);
        Assert.Contains("--est", Options.Parse(["x", "--est", "sus"]).Error, StringComparison.Ordinal);
        Assert.Contains("--nord", Options.Parse(["x", "--nord", "x"]).Error, StringComparison.Ordinal);
        Assert.Contains("--est", Options.Parse(["x", "--est"]).Error, StringComparison.Ordinal);

        (int code, string help, _) = Cli.Run("--help");
        Assert.Equal(0, code);
        Assert.Contains("--est stanga|dreapta", help, StringComparison.Ordinal);
        Assert.Contains("--nord sus|jos", help, StringComparison.Ordinal);
        Assert.Contains("(implicit dreapta)", help, StringComparison.Ordinal);   // the defaults in words, from ArrowKeys.Default
        Assert.Contains("(implicit jos)", help, StringComparison.Ordinal);
        Assert.Contains("Implicit, sagetile sunt cele ale unui Star Adventurer GTi cu aplicatia SynScan Pro.", help, StringComparison.Ordinal);

        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        // The fixture's lights are FITS: the Veil solve with its y column mirrored (ADR 0023).
        string[] wcs =
        [
            FitsTestData.Card("CTYPE1", "'RA---TAN'"), FitsTestData.Card("CTYPE2", "'DEC--TAN'"),
            FitsTestData.Card("CDELT1", "-0.00172"), FitsTestData.Card("CDELT2", "0.00172"),
            FitsTestData.Card("PC1_1", "0.0037"), FitsTestData.Card("PC1_2", "0.99999"),
            FitsTestData.Card("PC2_1", "0.99999"), FitsTestData.Card("PC2_2", "-0.0037"),
        ];
        File.WriteAllBytes(Path.Combine(s.Root, "result.fit"), FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: wcs));
        (_, string stdout, _) = Cli.Run(s.Root, "--threads", "2");
        Assert.Contains("RA 15 px ← V", stdout, StringComparison.Ordinal);
        Assert.Matches("DEC 1[12] px ↑ S", stdout);
        (_, string swapped, _) = Cli.Run(s.Root, "--threads", "2", "--est", "stanga", "--nord", "sus");
        Assert.Contains("RA 15 px → V", swapped, StringComparison.Ordinal);
        Assert.Matches("DEC 1[12] px ↓ S", swapped);
    }

    /// <summary>One CSV line into its cells, the quoted ones unquoted (a reason holds commas).</summary>
    private static string[] CsvCells(string line)
    {
        List<string> cells = [];
        string cur = "";
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    cur += '"';
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    cur += c;
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                cells.Add(cur);
                cur = "";
            }
            else
            {
                cur += c;
            }
        }

        cells.Add(cur);
        return cells.ToArray();
    }

    /// <summary>Every frame after a 30 s pause with a press under 5 px: no drift pair, the paused pairs give an approximate drift and the rows say so; the alignment is not read (R-049, R-050).</summary>
    [Fact]
    public void R049_ASessionDitheredEveryFrameFallsBackToThePausedPairsWordedApproximate()
    {
        // Steps of about 1.1 px (a 3 px step on the synthetic star grid votes a grid spacing instead).
        (double Dx, double Dy, double Elongation, double PauseSeconds)[] pattern = [(0, 0, 1.0, 0), (1, 0.5, 1.0, 30), (2, 1, 1.0, 30), (3, 1.5, 1.0, 30), (4, 2, 1.0, 30)];
        using SessionFixture f = new SessionFixture().WithStarSeries(pattern);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));
        SeriesAnalysis s = r.Series;

        Assert.True(s.DriftApproximate);
        Assert.Equal(4, s.DriftPairs);
        IReadOnlyList<ReportRow> rows = SeriesReport.DriftRows(r);
        Assert.Contains("(din 4 perechi de cadre cu pauza; aproximativ, fiindca in cifra intra si miscarile mici de dithering)", rows.Single(x => x.Label == "drift intre cadre la rand, fara dithering").Value, StringComparison.Ordinal);
        Assert.StartsWith("nu se poate masura (sub 3 perechi de cadre la rand, fara pauza si fara miscare", rows.Single(x => x.Label == "aliniere polara").Value, StringComparison.Ordinal);
        ReportRow summary = SeriesReport.SummaryRows(r).Single(x => x.Label == "aliniere polara");
        Assert.Null(summary.Verdict);
        Assert.StartsWith("nu se poate masura", summary.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// The eight-frame pattern with Siril's solve at the root, the field at DEC +30, shot at
    /// 600 s so the span passes an hour: the drift pairs' 0.05 px/min along X (DEC through the
    /// solve) give an error of 1.1', the rotation of the synthetic frames adds nothing, the
    /// drift of the alignment stays under a third of the FWHM, and the summary row reads OK with
    /// a chip; at 120 s the span is 17 min and only the lower bound is read (R-050).
    /// </summary>
    [Fact]
    public void R050_TheRowsAndTheSummaryWordThePolarAlignmentFromTheSolve()
    {
        string[] wcs =
        [
            FitsTestData.Card("CTYPE1", "'RA---TAN'"), FitsTestData.Card("CTYPE2", "'DEC--TAN'"),
            FitsTestData.Card("CRVAL1", "314.43"), FitsTestData.Card("CRVAL2", "30.0"),
            FitsTestData.Card("CDELT1", "-0.00172"), FitsTestData.Card("CDELT2", "0.00172"),
            // The y column mirrored: FITS lights are read as the solve is (ADR 0023).
            FitsTestData.Card("PC1_1", "0.0037"), FitsTestData.Card("PC1_2", "1"), FitsTestData.Card("PC2_1", "1"), FitsTestData.Card("PC2_2", "-0.0037"),
        ];
        using SessionFixture shortRun = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        File.WriteAllBytes(Path.Combine(shortRun.Root, "result.fit"), FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: wcs));
        SeriesResult rs = SeriesRun.Run(shortRun.Root, new SeriesOptions(Threads: 2));
        Assert.Equal(30.0, rs.Series.DeclinationDeg);
        Assert.Equal(5.7, rs.Series.PolarErrorMinArcmin!.Value, 0.3);
        Assert.Null(rs.Series.PolarErrorMaxArcmin);
        Assert.Equal(PolarAlignment.Unknown, rs.Series.PolarAlignment);
        Assert.Contains("sesiunea are sub 1 h, prea putin ca sa se afle cat poate fi cel mult", SeriesReport.DriftRows(rs).Single(x => x.Label == "aliniere polara").Value, StringComparison.Ordinal);
        Assert.Contains("(sesiunea are sub 1 h: nu se stie inca cat vine din aliniere)", SeriesReport.DriftRows(rs).Single(x => x.Label == "drift pe RA").Value, StringComparison.Ordinal);

        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern, exposure: 600);
        File.WriteAllBytes(Path.Combine(f.Root, "result.fit"), FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: wcs));
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));
        SeriesAnalysis s = r.Series;

        Assert.Equal(30.0, s.DeclinationDeg);
        Assert.False(s.DriftApproximate);
        Assert.Equal(4, s.DriftPairs);
        Assert.Equal(1.14, s.PolarErrorMinArcmin!.Value, 0.1);
        Assert.NotNull(s.PolarRotationPerHourDeg);
        // The synthetic frames are shifted copies that do not turn with a slew, so taking the
        // pattern's 15 px RA move out adds its convergence (about 3') to the bound here; the
        // exact correction is pinned on hand-made frames in SeriesAnalyzerTests.
        Assert.InRange(s.PolarErrorMaxArcmin!.Value, s.PolarErrorMinArcmin.Value, s.PolarErrorMinArcmin.Value + 4.0);
        Assert.Equal(PolarAlignment.Ok, s.PolarAlignment);

        IReadOnlyList<ReportRow> rows = SeriesReport.DriftRows(r);
        Assert.Contains("(din 4 perechi de cadre fara pauza intre ele)", rows.Single(x => x.Label == "drift intre cadre la rand, fara dithering").Value, StringComparison.Ordinal);
        string polar = rows.Single(x => x.Label == "aliniere polara").Value;
        Assert.StartsWith("axa monturii e deviata de la pol cu ", polar, StringComparison.Ordinal);
        Assert.Contains("'/h fara dithering, la DEC +30.0 grade", polar, StringComparison.Ordinal);
        Assert.Contains("intr-o expunere de 600 s", polar, StringComparison.Ordinal);
        Assert.Contains("alungirea pe care o produce e cel mult 1.0", polar, StringComparison.Ordinal);
        string ra = rows.Single(x => x.Label == "drift pe RA").Value;
        Assert.Contains("din aliniere vine cel mult", ra, StringComparison.Ordinal);
        ReportRow summary = SeriesReport.SummaryRows(r).Single(x => x.Label == "aliniere polara");
        Assert.Equal("OK", summary.Verdict);
        Assert.Contains("intr-o expunere", summary.Value, StringComparison.Ordinal);
    }
}
