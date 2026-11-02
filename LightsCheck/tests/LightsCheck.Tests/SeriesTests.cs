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
            [FrameClass.Good, FrameClass.Good, FrameClass.Marginal, FrameClass.Marginal, FrameClass.Marginal, FrameClass.Good, FrameClass.Bad, FrameClass.Good, FrameClass.Unknown, FrameClass.Good],
            s.Classes);
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
        Assert.Equal(5, s.GoodCount);
        Assert.Equal(3, s.MarginalCount);
        Assert.Equal(1, s.BadCount);
        Assert.Equal(1, s.UnknownCount);
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
        Assert.Equal("din fisier (XPIXSZ)", r.PixelSizeSource);
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
        Assert.InRange(flag.Value, 1.6, 2.2);
        Assert.Equal(Th.ElongationBad, flag.Threshold);
        Assert.Empty(s.Flags[0]);
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
                "dare de walking noise de cel mult 0.5 px: driftul e de 0.50 px intr-o expunere; cadrele 1-2, 2 cadre la rand in acelasi loc, la 10 px; 3 miscari la 8 cadre, 4 locuri diferite (la 10 px), 0 cadre singure, 0 miscari pe ambele axe",
                SeriesReport.DitherScalarRows(results[0])[^1].Value);
        }
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
        Assert.Equal($"alungire {RunReport.F(flag.Value, 2)} (prag 1.60), dara pe RA (-90 grade)", SeriesReport.Reasons(s.Flags[2]));
        Assert.Equal(TrailAxis.Ra, s.TrailOn);
        Assert.Contains("= pe RA", SeriesReport.TrailRows(r).Single(x => x.Label == "directia darelor").Value, StringComparison.Ordinal);

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
        Assert.StartsWith("FWHM median 3.25 px", focus.Value, StringComparison.Ordinal);
        using StringWriter sw = new();
        SeriesReport.Write(r, sw);
        Assert.Contains("    verdict            : BUN - FWHM median 3.25 px = 19.4\" pe cadrele bune, 3.25 / 3.25 px (-0 %) la inceput / sfarsit: a tinut toata sesiunea", sw.ToString(), StringComparison.Ordinal);

        IReadOnlyList<ReportParagraph> paragraphs = SeriesReport.RejectedExplanation;
        Assert.True(paragraphs.Count >= 3, "fewer than three paragraphs");
        ReportParagraph bold = Assert.Single(paragraphs, p => p.Emphasis);
        Assert.StartsWith("Dara pe RA", bold.Text, StringComparison.Ordinal);
        Assert.Contains("pe DEC", bold.Text, StringComparison.Ordinal);

        IReadOnlyList<ReportTable> t = SeriesReport.DitherTables(r);
        Assert.Equal(["cat de des", "cat de mult", "directie", "apropierea cadrelor", "grupuri in acelasi loc"], t.Select(x => x.Title));
        Assert.Equal(["o miscare la fiecare", "de cate ori"], t[0].Columns);
        Assert.Equal(["2 cadre | 2 ori"], Rows(t[0]));
        Assert.Equal(["cat de mare", "cate miscari"], t[1].Columns);
        Assert.Equal(["de obicei | 15.32 px = 91.7\"", "5-10 px | 0", "10-20 px | 2", "20+ px | 1"], Rows(t[1]));
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
        Assert.Matches("^DEC 1[12] px ↓ S$", rows[2].Move);   // frame 3: stars +11.5 px in X -> the mount went south, the down arrow by default
        Assert.Equal("-", rows[3].Move);
        Assert.Equal("RA 15 px ← V", rows[4].Move);      // frame 5: stars -15.3 px in Y -> the mount went west, the left arrow by default
        Assert.StartsWith("DEC 2", rows[6].Move, StringComparison.Ordinal);
        Assert.EndsWith(" px ↓ S", rows[6].Move, StringComparison.Ordinal);
        Assert.Null(rows[0].StepTip);
        Assert.Equal("cadrul 5: pas 15.3 px; RA 15 px ← V", rows[4].StepTip);
        Assert.Equal("cadrul 2: pas 0.5 px; -", rows[1].StepTip);

        using StringWriter sw = new();
        SeriesReport.Write(r, sw);
        Assert.Matches("      dY  dithering {33}  acelasi loc", sw.ToString());
        // A two-axis cell (41 characters at most, three-digit moves included) fits the column.
        Assert.True("RA 142 px ← V, DEC 129 px ↓ S".Length <= 42);

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

    /// <summary>The console takes the arrow keys as words, names them in the help, and the report follows them (R-040).</summary>
    [Fact]
    public void R040_TheConsoleTakesTheArrowKeysAndTheReportFollowsThem()
    {
        Assert.Equal(ArrowKeys.Default, Options.Parse(["x"]).Arrows);
        Options o = Options.Parse(["x", "--est", "stanga", "--nord", "jos"]);
        Assert.Null(o.Error);
        Assert.Equal(new ArrowKeys(Arrow.Left, Arrow.Down), o.Arrows);
        Assert.Equal(new ArrowKeys(Arrow.Right, Arrow.Up), Options.Parse(["x", "--est", "dreapta", "--nord", "sus"]).Arrows);
        Assert.Contains("--est", Options.Parse(["x", "--est", "sus"]).Error, StringComparison.Ordinal);
        Assert.Contains("--nord", Options.Parse(["x", "--nord", "x"]).Error, StringComparison.Ordinal);
        Assert.Contains("--est", Options.Parse(["x", "--est"]).Error, StringComparison.Ordinal);

        (int code, string help, _) = Cli.Run("--help");
        Assert.Equal(0, code);
        Assert.Contains("--est stanga|dreapta", help, StringComparison.Ordinal);
        Assert.Contains("--nord sus|jos", help, StringComparison.Ordinal);

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
        Assert.Matches("DEC 1[12] px ↓ S", stdout);
        (_, string swapped, _) = Cli.Run(s.Root, "--threads", "2", "--est", "stanga", "--nord", "jos");
        Assert.Contains("RA 15 px → V", swapped, StringComparison.Ordinal);
        Assert.Matches("DEC 1[12] px ↑ S", swapped);
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
        Assert.Contains("(4 perechi cu pauza, aproximativ: pasii mici de dithering intra in cifra)", rows.Single(x => x.Label == "drift intre cadre consecutive fara dithering").Value, StringComparison.Ordinal);
        Assert.StartsWith("nemasurabila (sub 3 perechi de cadre consecutive fara pauza si fara miscare", rows.Single(x => x.Label == "aliniere polara").Value, StringComparison.Ordinal);
        ReportRow summary = SeriesReport.SummaryRows(r).Single(x => x.Label == "aliniere polara");
        Assert.Null(summary.Verdict);
        Assert.StartsWith("nemasurabila", summary.Value, StringComparison.Ordinal);
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
        Assert.Contains("sesiune sub 1 h: rotatia nu da inca limita de sus", SeriesReport.DriftRows(rs).Single(x => x.Label == "aliniere polara").Value, StringComparison.Ordinal);
        Assert.Contains("(sesiune sub 1 h: nu se stie inca cat e din aliniere)", SeriesReport.DriftRows(rs).Single(x => x.Label == "drift pe RA").Value, StringComparison.Ordinal);

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
        Assert.Contains("(4 perechi fara pauza)", rows.Single(x => x.Label == "drift intre cadre consecutive fara dithering").Value, StringComparison.Ordinal);
        string polar = rows.Single(x => x.Label == "aliniere polara").Value;
        Assert.StartsWith("eroare ", polar, StringComparison.Ordinal);
        Assert.Contains("'/h fara dithering la DEC +30.0 grade", polar, StringComparison.Ordinal);
        Assert.Contains("intr-o expunere de 600 s", polar, StringComparison.Ordinal);
        Assert.Contains("alungire adaugata cel mult 1.0", polar, StringComparison.Ordinal);
        string ra = rows.Single(x => x.Label == "drift pe RA").Value;
        Assert.Contains("din aliniere cel mult", ra, StringComparison.Ordinal);
        ReportRow summary = SeriesReport.SummaryRows(r).Single(x => x.Label == "aliniere polara");
        Assert.Equal("OK", summary.Verdict);
        Assert.Contains("intr-o expunere", summary.Value, StringComparison.Ordinal);
    }
}
