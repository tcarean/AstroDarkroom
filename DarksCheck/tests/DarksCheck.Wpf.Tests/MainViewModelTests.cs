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
using DarksCheck.Tests;

namespace DarksCheck.Wpf.Tests;

/// <summary>
/// Opening a folder end to end (R-070), what the tabs hold (R-071), the report text (R-072),
/// the partner (R-073), the export (R-074), the settings remembered (R-075) and reaching the
/// core (R-076), without a window.
/// </summary>
public class MainViewModelTests
{
    [Fact]
    public async Task R071_ARefaVerdictOfCheck3IsARedChip()
    {
        // twelve darks cooling steeply: seven are more than two degrees above where the series ended, check 3 reads REFA
        // (ADR 0038's limit against the end of the series, ADR 0043)
        (int X, int Y, double Adu)[] hot = Enumerable.Range(0, 10).Select(k => (60 + 7 * k, 100 + 5 * k, 300.0)).ToArray();
        using SessionFixture f = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = hot, HotFactor = 0.35 + 1.45 * Math.Exp(-i / 6.0) });
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);
        Assert.Equal(Verdict.Redo, vm.Result!.Verdicts.Drift.Verdict);

        RowVm verdict = vm.Sections.Single(s => s.Title.StartsWith("3.", StringComparison.Ordinal)).Rows[^1];
        Assert.Equal("verdict", verdict.Label);
        Assert.True(verdict.HasVerdict);
        Assert.Equal("REFA", verdict.Verdict);
        Assert.StartsWith("7 din 12 cadre sunt la peste 2.0 grade de sfarsitul seriei, deci masterul nu apartine niciunei temperaturi; ", verdict.Rest, StringComparison.Ordinal);
        Assert.Equal(VerdictPalette.Bad, VerdictPalette.For(verdict.Verdict));
        Assert.Equal(VerdictPalette.For("EROARE"), VerdictPalette.For(verdict.Verdict));
    }

    [Fact]
    public async Task R071_TheFramesOffTheEndOfTheSeriesAreMarkedInTheTableAndTheChart()
    {
        // twelve darks climbing to where the series ends: the first two are more than two degrees under it (R-023)
        (int X, int Y, double Adu)[] hot = Enumerable.Range(0, 10).Select(k => (60 + 7 * k, 100 + 5 * k, 300.0)).ToArray();
        using SessionFixture f = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = hot, HotFactor = 1.2 - 0.4 * Math.Exp(-i / 3.0) });
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);
        DarkSeriesResult r = vm.Result!;
        Warming w = r.Darks.Warming;
        Assert.Equal([1, 2], w.OffEndFrames);

        // the table: the core's cells, the flag and the note on the frames off the end of the series
        IReadOnlyList<DarkTableRow> rows = DarkReport.FrameRows(r);
        Assert.Equal(rows.Select(FrameRowVm.From), vm.Frames);
        Assert.True(vm.Frames[0].Off);
        Assert.Equal(rows[0].End, vm.Frames[0].End);
        Assert.Equal(rows[0].Note, vm.Frames[0].Note);
        Assert.StartsWith("de aruncat: pixelii fierbinti sunt la ", vm.Frames[0].Note, StringComparison.Ordinal);
        Assert.False(vm.Frames[11].Off);
        Assert.Null(vm.Frames[11].Note);

        // the chart: the frames off the end in the attention colour, guides at 1, the end of the series and its two bounds
        Assert.Equal(VerdictPalette.Warn, vm.WarmingPoints[0].Colour);
        Assert.Equal(VerdictPalette.Accent, vm.WarmingPoints[11].Colour);
        Assert.Equal([1.0, w.EndRatio, w.OffEndLow, w.OffEndHigh], vm.WarmingGuides.Select(g => g.Value));
        Assert.Equal("sfarsitul seriei " + RunReport.F(w.EndRatio, 2), vm.WarmingGuides[1].Label);
        Assert.Equal(VerdictPalette.Warn, vm.WarmingGuides[2].Colour);
        Assert.Equal(DarkReport.WarmingChartNote(r), vm.WarmingNote);
        Assert.Contains("Sfarsitul seriei (media ultimelor 5 cadre) e la ", vm.WarmingNote, StringComparison.Ordinal);
        Assert.DoesNotContain("plato", vm.WarmingNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R067_TheLightsChartFollowsTheBandTheTracesAllow()
    {
        // a hundred hot pixels of 30 ADU, lights at 0.85 of the dark with a noise of 6: what a plain subtraction leaves is
        // under the noise of a light, so the core widens the green band and the chart follows it (R-067, ADR 0044)
        (int X, int Y, double Adu)[] weak = Enumerable.Range(0, 100).Select(k => (20 + 22 * (k % 10), 20 + 16 * (k / 10), 30.0)).ToArray();
        using SessionFixture f = new SessionFixture()
            .WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = weak })
            .WithLights(5, i => new SyntheticLight { Seed = 300 + i, Hot = weak, HotFactor = 0.85 });
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);
        LightsAnalysis l = vm.Result!.Lights!;
        TraceFigures t = l.Traces;
        Assert.True(t.Relaxed);
        Assert.True(t.Widened);

        // each point takes the colour of the core's verdict for its own ratio, and the green lines the band's ends
        Assert.Equal(l.Frames.Select(x => VerdictPalette.For(l.Judge(x.AmpRatioMedian))), vm.LightPoints.Select(p => p.Colour));
        Assert.Contains(VerdictPalette.Good, vm.LightPoints.Select(p => p.Colour));
        Assert.Equal([t.DirectLow, t.DirectHigh, Th.MatchOptLow, Th.MatchOptHigh, Th.MatchCosmeticLow], vm.LightGuides.Select(g => g.Value));
        Assert.Equal(RunReport.F(t.DirectLow, 2) + " asa cum e", vm.LightGuides[0].Label);
        Assert.Equal(RunReport.F(t.DirectHigh, 2) + " asa cum e", vm.LightGuides[1].Label);
        Assert.Equal(WindowTexts.LightsChartLead(t), vm.LightsChartLead);
        Assert.Contains($"Verde intre {RunReport.F(t.DirectLow, 2)} si {RunReport.F(t.DirectHigh, 2)}: dark-ul se poate scadea asa cum e (0.9 .. 1.1, largit pana unde ce ramane dupa scadere ajunge cat zgomotul unui light). Galben de acolo pana la 0.7 si 1.4: ", vm.LightsChartLead, StringComparison.Ordinal);

        Assert.DoesNotMatch(PlainWords.RomanianTabWords(), vm.LightsChartLead);
        // the verdict's chip is green and the new row is on the Lights tab
        RowVm verdict = vm.LightRows[^1];
        Assert.Equal("OK", verdict.Verdict);
        Assert.Contains("ce ramane", vm.LightRows.Select(r => r.Label));

        // a plain result keeps the plain band and its words
        Assert.DoesNotContain("largit", WindowTexts.LightsChartLead(TraceFigures.None), StringComparison.Ordinal);
        Assert.Contains("Verde intre 0.9 si 1.1: dark-ul se poate scadea asa cum e. Galben intre 0.7 si 1.4: ", WindowTexts.LightsChartLead(TraceFigures.None), StringComparison.Ordinal);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Contains($"Green between {RunReport.F(t.DirectLow, 2)} and {RunReport.F(t.DirectHigh, 2)}: the dark can be subtracted as it is (0.9 .. 1.1, widened to where what the subtraction leaves reaches the noise of a light). Yellow from there to 0.7 and 1.4: ", WindowTexts.LightsChartLead(t), StringComparison.Ordinal);
            Assert.DoesNotMatch(PlainWords.EnglishTabWords(), WindowTexts.LightsChartLead(t));
        }
    }

    [Fact]
    public async Task R071_WithoutAnEndTheWarmingChartKeepsTheOneGuide()
    {
        // five darks: under the six the end of the series of R-023 needs
        using SessionFixture f = new SessionFixture().WithDarkSeries(5, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice });
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);
        Assert.Equal([1.0], vm.WarmingGuides.Select(g => g.Value));
        Assert.All(vm.Frames, row => Assert.False(row.Off));
        Assert.Equal(DarkReport.WarmingChartNote(vm.Result!), vm.WarmingNote);
    }

    [Fact]
    public async Task R070_OpeningAFolderRunsTheWholeAnalysisByItself()
    {
        using SessionFixture f = WindowFixture.Session();
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasResult);
        Assert.False(vm.IsRunning);
        Assert.True(vm.IsIdle);
        Assert.False(vm.HasProblem);
        Assert.Equal(f.Root, vm.Folder);
        Assert.Equal(6, vm.Frames.Count);
        Assert.Equal(4, vm.Lights.Count);
        Assert.StartsWith("darkscheck ", vm.Report, StringComparison.Ordinal);
        Assert.Null(vm.Status);
        // without a dispatcher the progress callbacks land on pool threads in any order, so only the range is pinned
        Assert.InRange(vm.Progress, 0, 1);
    }

    [Fact]
    public async Task R070_AFolderThatCannotBeAnalysedBecomesTheProblemCard()
    {
        using TempWorkspace ws = new();
        MainViewModel vm = new();

        await vm.OpenAsync(Path.Combine(ws.Dir, "missing"));

        Assert.True(vm.HasProblem);
        Assert.False(vm.HasResult);
        Assert.False(vm.IsRunning);
        Assert.True(vm.HasFolder);
        Assert.Contains("missing", vm.ProblemText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R070_StoppingTheRunLeavesNoResultAndSaysSo()
    {
        using SessionFixture f = WindowFixture.Session();
        MainViewModel vm = new();

        Task run = vm.OpenAsync(f.Root);
        vm.CancelPending();
        await run;

        Assert.False(vm.HasResult);
        Assert.False(vm.IsRunning);
        Assert.False(vm.HasProblem);
        Assert.Equal(WindowTexts.Cancelled, vm.Status);
    }

    [Fact]
    public async Task R070_OpeningAnotherFolderDropsTheFirstResult()
    {
        using SessionFixture a = WindowFixture.Session();
        using SessionFixture b = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 900 + i, Hot = SyntheticLight.Lattice }, inSubfolder: false);
        MainViewModel vm = new();

        await vm.OpenAsync(a.Root);
        Assert.Equal(4, vm.Lights.Count);
        await vm.OpenAsync(b.Root);

        Assert.Equal(b.Root, vm.Folder);
        Assert.Equal(4, vm.Frames.Count);
        Assert.Empty(vm.Lights);
        Assert.False(vm.HasLights);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("3", 3)]
    [InlineData("0", 0)]
    [InlineData("abc", 0)]
    public void R070_TheThreadsInputIsAPositiveCountOrTheDefault(string typed, int expected)
    {
        using TestSettings.Own _ = new();
        MainViewModel vm = new() { ThreadsText = typed };
        Assert.Equal(expected, vm.Options.Threads);
    }

    [Fact]
    public async Task R078_TheScaleTabIsEmptyBeforeAResultAndFilledAfter()
    {
        using TestSettings.Own _ = new();
        using SessionFixture f = WindowFixture.Session();
        MainViewModel vm = new();
        Assert.Empty(vm.ScaleRows);
        Assert.Empty(vm.ScaleParagraphs);
        await vm.OpenAsync(f.Root);
        Assert.True(vm.HasResult);
        Assert.Equal(11, vm.ScaleRows.Count);
        Assert.Equal(4, vm.ScaleParagraphs.Count);
        Assert.Equal("fisier", vm.ScaleRows[0].Label);
    }

    [Fact]
    public async Task R071_TheTabsHoldTheSectionsTheTablesTheMapsAndTheCharts()
    {
        using SessionFixture f = WindowFixture.Session();
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);
        DarkSeriesResult r = vm.Result!;

        // the session card: the inventory lines and the darks' header rows, as the console prints them
        Assert.Contains(vm.SessionRows, row => row.Label == "darks" && row.Value.Contains("6 cadre", StringComparison.Ordinal));
        Assert.Contains(vm.SessionRows, row => row.Label == "1 ADU-eq");
        Assert.StartsWith("=== DARK-URI: cadre citite 6 din 6", vm.HeaderLine, StringComparison.Ordinal);
        Assert.Equal(InventoryReport.HeaderRows(r.Inventory).Select(row => row.Label), vm.SessionRows.Take(InventoryReport.HeaderRows(r.Inventory).Count).Select(row => row.Label));

        // one card per check, the rows the core wrote, the verdict as a chip with the rest beside it; check 9 right
        // under the session card, then checks 2 to 8 and INDEX (ADR 0041), while the console keeps the checks' order
        Assert.Equal(["9. CAT DE BINE SE POTRIVESTE DARK-UL CU LIGHTS-URILE", "2. SCURGERI DE LUMINA SI DIFERENTE PE CADRU", "3. INCALZIREA CAMEREI IN TIMPUL SERIEI", "4. ZGOMOTUL FIECARUI CADRU", "5. PIXELI FIERBINTI", "6. MODEL FIX: CE E LA FEL IN TOATE CADRELE", "7. PIXELI MORTI SI SATURATI", "8. CATE DARK-URI AJUNG", "INDEX"], vm.Sections.Select(s => s.Title));
        Assert.Equal(["2. SCURGERI DE LUMINA SI DIFERENTE PE CADRU", "3. INCALZIREA CAMEREI IN TIMPUL SERIEI", "4. ZGOMOTUL FIECARUI CADRU", "5. PIXELI FIERBINTI", "6. MODEL FIX: CE E LA FEL IN TOATE CADRELE", "7. PIXELI MORTI SI SATURATI", "8. CATE DARK-URI AJUNG", "9. CAT DE BINE SE POTRIVESTE DARK-UL CU LIGHTS-URILE", "INDEX"], DarkReport.Sections(r).Select(s => s.Title));
        RowVm verdict = vm.Sections[1].Rows[^1];
        Assert.Equal("verdict", verdict.Label);
        Assert.True(verdict.HasVerdict);
        Assert.Equal("OK", verdict.Verdict);
        Assert.StartsWith("nicio margine", verdict.Rest, StringComparison.Ordinal);
        Assert.Equal("OK: " + verdict.Rest, verdict.Value);
        RowVm label = vm.Sections[^1].Rows.Single(row => row.Label == "eticheta");
        Assert.False(label.HasVerdict);
        Assert.Equal(label.Value, label.Rest);

        // the tables are the console's rows, cell for cell
        Assert.Equal(DarkReport.FrameRows(r).Select(FrameRowVm.From), vm.Frames);
        Assert.Equal(DarkReport.LightRows(r).Select(LightRowVm.From), vm.Lights);
        Assert.Equal(DarkReport.FrameRows(r)[1].Residual, vm.Frames[1].Residual);
        Assert.Equal(DarkReport.FrameRows(r)[1].Light, vm.Frames[1].Light);
        Assert.StartsWith("[pixelii verzi G1;", vm.FrameTableNote, StringComparison.Ordinal);
        Assert.StartsWith("[pixelii verzi G1;", vm.LightTableNote, StringComparison.Ordinal);

        // the three maps, with the report's row under each
        Assert.Equal(["MASTER DARK", "MASTER BIAS", "DARK MINUS BIAS"], vm.Maps.Select(m => m.Title));
        Assert.All(vm.Maps, m => Assert.True(m.HasImage));
        Assert.Equal(DarkOutputs.MapRaster(r.Darks.MeanMaps[r.ReferencePlane])!.Value.W, vm.Maps[0].Image!.PixelWidth);
        Assert.StartsWith("centru ", vm.Maps[2].Note, StringComparison.Ordinal);

        // the charts: one point per frame, the figures of the records, the guides from Th
        Assert.Equal(6, vm.WarmingPoints.Count);
        Assert.Equal(r.Darks.Frames[2].AmpRatioMedian, vm.WarmingPoints[2].Y);
        Assert.Equal(r.Darks.Frames[2].Residuals[r.ReferencePlane].Sigma, vm.ResidualPoints[2].Y);
        Assert.Equal(r.Verdicts.Noise.OutlierLimitAdu, Assert.Single(vm.ResidualGuides).Value);
        Assert.Equal(r.Darks.Frames[2].Residuals[r.ReferencePlane].RowBandingRatio, vm.BandingPoints[2].Y);
        Assert.Equal(r.Darks.Noise.Curve.Select(p => p.Measured), vm.NoisePoints.Select(p => p.Y));
        Assert.Equal(r.Darks.Noise.Curve.Select(p => p.Expected), vm.NoiseExpected.Select(p => p.Y));
        Assert.Equal(4, vm.LightPoints.Count);
        Assert.Equal(r.Lights!.Frames[1].AmpRatioMedian, vm.LightPoints[1].Y);
        Assert.Equal([Th.MatchDirectLow, Th.MatchDirectHigh, Th.MatchOptLow, Th.MatchOptHigh, Th.MatchCosmeticLow], vm.LightGuides.Select(g => g.Value));
        Assert.Equal(VerdictPalette.Warn, vm.LightPoints[1].Colour);

        // the hot-pixel, lights and index rows are the sections' rows; the Rezumat's check 5 card keeps only the
        // stability and the verdict, the Pixeli fierbinti tab all five (R-071, ADR 0041)
        Assert.Equal(["pixeli peste prag", "pe culori, peste 20", "cat de fierbinti", "stabilitate", "verdict"], vm.HotRows.Select(row => row.Label));
        Assert.Equal(["stabilitate", "verdict"], vm.Sections[4].Rows.Select(row => row.Label));
        Assert.Equal(vm.HotRows.TakeLast(2).Select(row => row.Value), vm.Sections[4].Rows.Select(row => row.Value));
        Assert.Equal(vm.LightRows.Select(row => row.Value), vm.Sections[0].Rows.Select(row => row.Value));
        Assert.Equal(vm.IndexRows.Select(row => row.Value), vm.Sections[^1].Rows.Select(row => row.Value));

        // a row's lines and table reach the view as the core gave them (R-031, R-024, R-051, R-063)
        RowVm stability = vm.HotRows.Single(row => row.Label == "stabilitate");
        Assert.Equal(4, stability.Lines.Count);
        Assert.Equal(string.Join("; ", stability.Lines), stability.Rest);
        Assert.Null(stability.Table);
        RowVm hotVerdict = vm.HotRows.Single(row => row.Label == "verdict");
        Assert.Equal([hotVerdict.Rest], hotVerdict.Lines);
        RowVm curve = vm.Sections.Single(s => s.Title.StartsWith("8.", StringComparison.Ordinal)).Rows.Single(row => row.Label == "zgomot dupa medie");
        Assert.Equal(["cadre", "masurat", "se astepta"], curve.Table!.Header);
        Assert.Equal(r.Darks.Noise.Curve.Count, curve.Table.Rows.Count);
        RowVm overlap = vm.LightRows.Single(row => row.Label == "in masterul dark");
        Assert.Equal(["prag", "pixeli", "procent"], overlap.Table!.Header);
        Assert.Equal(4, overlap.Table.Rows.Count);
        Assert.StartsWith("cati dintre acesti pixeli", overlap.Table.Note, StringComparison.Ordinal);
        Assert.Empty(overlap.Lines);
        Assert.Empty(curve.Lines);
        Assert.Contains(vm.LightRows, row => row.Label == "index dark-uri" && row.Value == r.Index.Tag);
        Assert.Contains(vm.LightRows, row => row.Label == "index lights");

        // R-078: the tab "Explicatii scara ADU" shows the core's scale rows and paragraphs, nothing reworded
        Assert.Equal(ScaleReport.Rows(r).Select(row => row.Label), vm.ScaleRows.Select(row => row.Label));
        Assert.Equal(ScaleReport.Rows(r).Select(row => row.Value), vm.ScaleRows.Select(row => row.Value));
        Assert.Equal(ScaleReport.Paragraphs(r), vm.ScaleParagraphs);
        Assert.Equal("EXPLICATII SCARA ADU", vm.ScaleTitle);

        // R-071 / ADR 0016: every row carries the core's bold parts, and the emphasis splits the text where they are
        foreach (SectionVm card in vm.Sections)
        {
            Assert.Equal(DarkReport.Sections(r).Single(s => s.Title == card.Title).Rows.Where(row => row.Summary).Select(row => row.BoldParts), card.Rows.Select(row => row.Bold));
        }

        Assert.Equal(ScaleReport.Rows(r).Select(row => row.BoldParts), vm.ScaleRows.Select(row => row.Bold));
        RowVm master = vm.Sections.Single(s => s.Title.StartsWith("8.", StringComparison.Ordinal)).Rows.Single(row => row.Label == "master");
        Assert.Single(master.Bold);
        Assert.Equal([("30 cadre -> ", false), (master.Bold[0], true), (" zgomot", false)], Emphasis.Segments("30 cadre -> " + master.Bold[0] + " zgomot", master.Bold).Select(p => (p.Text, p.Bold)).Take(3).ToList()[..3]);
        Assert.Equal([("a ", false), ("1.43 ADU", true), (" b ", false), ("1.43 ADU", true)], Emphasis.Segments("a 1.43 ADU b 1.43 ADU", ["1.43 ADU"]));
        Assert.Equal([("plain", false)], Emphasis.Segments("plain", []));
        // a mark is a figure of its own: not the tail of a longer number, not glued to a sign or a point
        Assert.Equal([("11.43 ADU si ", false), ("1.43 ADU", true)], Emphasis.Segments("11.43 ADU si 1.43 ADU", ["1.43 ADU"]));
        Assert.Equal([("+0.47 ADU-eq2 ", false), ("+0.47 ADU-eq", true)], Emphasis.Segments("+0.47 ADU-eq2 +0.47 ADU-eq", ["+0.47 ADU-eq"]));
        Assert.Empty(Emphasis.Segments("", ["x"]));
        Assert.Equal(r.Label, vm.Label);
        Assert.Equal(r.Index.Tag, vm.Tag);
        Assert.EndsWith("-" + vm.Tag, vm.Label, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R071_WithoutBiasesOrLightsTheMapsAndTheLightsTabSayWhy()
    {
        using SessionFixture f = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice });
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.Maps[0].HasImage);
        Assert.False(vm.Maps[1].HasImage);
        Assert.False(vm.Maps[2].HasImage);
        Assert.Empty(vm.Lights);
        Assert.Empty(vm.LightPoints);
        Assert.Equal("", vm.LightTableNote);
        RowVm lights = Assert.Single(vm.LightRows);
        Assert.Equal("NEMASURAT", lights.Verdict);
        Assert.Equal("sesiunea nu are lights", lights.Rest);
    }

    [Fact]
    public async Task R072_TheReportTabIsTheConsoleText()
    {
        using TestSettings.Own _ = new();
        using SessionFixture f = WindowFixture.Session();
        MainViewModel vm = new() { ThreadsText = "2" };

        await vm.OpenAsync(f.Root);

        using StringWriter stdout = new();
        using StringWriter stderr = new();
        int code = DarksCheck.Cli.Program.Run([f.Root, "--threads", "2"], stdout, stderr);
        Assert.Equal(0, code);
        Assert.Equal(stdout.ToString().ReplaceLineEndings("\n"), vm.Report.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task R073_ThePartnerReachesTheRunAndIsNeitherRememberedNorKeptForAnotherFolder()
    {
        using TestSettings.Own own = new();
        using SessionFixture f = WindowFixture.Session();
        using SessionFixture p = WindowFixture.Partner();
        MainViewModel vm = new();
        Assert.False(vm.HasPartner);
        Assert.Equal(WindowTexts.NoPartner, vm.PartnerText);

        vm.Partner = p.Root;
        Assert.True(vm.HasPartner);
        Assert.Equal(p.Root, vm.PartnerText);
        // not stored (R-075, ADR 0041): a new window starts without it, and the settings file never names it
        vm.ThreadsText = "2";
        Assert.False(new MainViewModel().HasPartner);
        Assert.DoesNotContain(Path.GetFileName(p.Root), File.ReadAllText(own.FilePath), StringComparison.Ordinal);

        await vm.OpenAsync(f.Root);
        Assert.NotNull(vm.Result!.Partner);
        Assert.Equal(p.Root, vm.Result.Partner!.Folder);
        Assert.Contains(vm.IndexRows, row => row.Label == "dark-uri / partener" && row.Value.StartsWith("pixelii fierbinti sunt in dark-urile sesiunii de 0.5", StringComparison.Ordinal));
        // lights at 1.3 of darks that are 0.5 of the partner: 0.65, cosmetic correction only
        Assert.Contains(vm.IndexRows, row => row.Label == "lights / partener" && row.Value.StartsWith("pixelii fierbinti sunt in lights de 0.6", StringComparison.Ordinal));
        Assert.Contains(vm.IndexRows, row => row.Label == "verdict partener" && row.Verdict == "ATENTIE" && row.Rest.StartsWith("foloseste partenerul doar pentru corectia cosmetica", StringComparison.Ordinal));

        // changing the partner after a run only says so; the next run takes it
        vm.Partner = null;
        Assert.Equal(WindowTexts.PartnerChanged, vm.Status);
        Assert.NotNull(vm.Result.Partner);
        await vm.RerunAsync();
        Assert.Null(vm.Result!.Partner);

        // a partner is kept for a rerun of the same folder and dropped when another folder is opened, like the lights folder (R-122)
        vm.Partner = p.Root;
        await vm.RerunAsync();
        Assert.True(vm.HasPartner);
        Assert.NotNull(vm.Result!.Partner);
        using SessionFixture other = WindowFixture.Session(lights: 2);
        await vm.OpenAsync(other.Root);
        Assert.False(vm.HasPartner);
        Assert.Equal(WindowTexts.NoPartner, vm.PartnerText);
        Assert.Null(vm.Result!.Partner);
    }

    [Fact]
    public async Task R074_ExportWritesTheFilesIntoTheChosenFolderOnly()
    {
        using SessionFixture f = WindowFixture.Session();
        using TempWorkspace outDir = new();
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);

        vm.Export(outDir.Dir);

        Assert.StartsWith("Scris:", vm.Status, StringComparison.Ordinal);
        string[] files = Directory.GetFiles(outDir.Dir).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;
        Assert.Equal(["darkscheck-biases.csv", "darkscheck-darks.csv", "darkscheck-lights.csv", "darkscheck-map-bias.png", "darkscheck-map-dark-minus-bias.png", "darkscheck-map-dark.png", "darkscheck-report.txt"], files);
        Assert.Equal(vm.Report.ReplaceLineEndings("\n"), File.ReadAllText(Path.Combine(outDir.Dir, "darkscheck-report.txt")).ReplaceLineEndings("\n"));
        Assert.DoesNotContain(Directory.GetFiles(f.DarksDir), p => Path.GetFileName(p).StartsWith("darkscheck", StringComparison.Ordinal));

        // inside the session: refused, as --out is, and nothing written there
        vm.Export(Path.Combine(f.Root, "rapoarte"));
        Assert.StartsWith("EROARE: --out nu poate fi folderul sesiunii", vm.Status, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(f.Root, "rapoarte")));
    }

    [Fact]
    public void R074_ExportWithoutAResultDoesNothing()
    {
        using TempWorkspace outDir = new();
        MainViewModel vm = new();

        vm.Export(outDir.Dir);

        Assert.Null(vm.Status);
        Assert.Empty(Directory.GetFiles(outDir.Dir));
    }

    [Fact]
    public void R075_TheSettingsAreRememberedBetweenRunsAndFallBackWhenInvalid()
    {
        using TestSettings.Own own = new();
        MainViewModel first = new();
        Assert.Equal("", first.ThreadsText);
        Assert.Equal("60", first.MaxFramesText);
        Assert.Equal("2", first.IsoFactorText);
        Assert.Equal("6", first.DoublingText);

        first.ThreadsText = "3";
        first.MaxFramesText = "20";
        first.IsoFactorText = "1.8";
        first.DoublingText = "7";
        Assert.True(File.Exists(own.FilePath));

        MainViewModel second = new();
        Assert.Equal("3", second.ThreadsText);
        Assert.Equal("20", second.MaxFramesText);
        Assert.Equal("1.8", second.IsoFactorText);
        Assert.Equal("7", second.DoublingText);
        DarkOptions o = second.Options;
        Assert.Equal((3, 20, 1.8, 7.0), (o.Threads, o.MaxFrames, o.IsoFactorPerStop, o.DoublingCelsius));

        // not a number, a comma, zero: the core's defaults, and the field says so
        second.MaxFramesText = "x";
        second.IsoFactorText = "1,5";
        second.DoublingText = "0";
        Assert.False(second.MaxFramesIsValid);
        Assert.False(second.IsoFactorIsValid);
        Assert.False(second.DoublingIsValid);
        o = second.Options;
        Assert.Equal((Th.MaxFramesInMemory, 2.0, Th.DoublingCelsius), (o.MaxFrames, o.IsoFactorPerStop, o.DoublingCelsius));

        // an unreadable file means the defaults, not a crash
        File.WriteAllText(own.FilePath, "{ not json");
        MainViewModel third = new();
        Assert.Equal("60", third.MaxFramesText);
    }

    [Fact]
    public async Task R076_TheDoublingAndTheIsoFactorReachTheDegreesAndTheNormalisation()
    {
        using TestSettings.Own _ = new();
        using SessionFixture f = WindowFixture.Session();
        MainViewModel vm = new() { DoublingText = "12" };
        await vm.OpenAsync(f.Root);
        double ratio = vm.Result!.Lights!.LightsOverDark.Ratio;
        Assert.InRange(ratio, 1.25, 1.35);
        Assert.Equal(12 * Math.Log2(ratio), vm.Result.Lights.LightsOverDark.DeltaCelsius, 1e-9);
        Assert.Equal(12.0, vm.Result.Darks.Normalisation.DoublingCelsius);
        Assert.Contains("semnalul termic se dubleaza la fiecare 12 grade", vm.Report, StringComparison.Ordinal);

        // a gain of 1 per stop: ISO changes nothing, so lights at ISO 800 read their raw ratio
        using SessionFixture iso = new SessionFixture().WithDarkSeries(6, i => new SyntheticDark { Seed = 100 + i, Hot = SyntheticLight.Lattice })
            .WithLights(3, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 2 }, iso: 800);
        MainViewModel linear = new() { IsoFactorText = "2" };
        await linear.OpenAsync(iso.Root);
        Assert.InRange(linear.Result!.Lights!.LightsOverDark.Ratio, 0.95, 1.05);
        MainViewModel flat = new() { IsoFactorText = "1" };
        await flat.OpenAsync(iso.Root);
        Assert.InRange(flat.Result!.Lights!.LightsOverDark.Ratio, 1.9, 2.1);
        Assert.Contains("o treapta de ISO inmulteste semnalul cu 1.00 (setarea ta)", flat.Report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R077_ASessionWithoutDarksIsAProblemUntilAPartnerIsChosenThenRunsWithIt()
    {
        using TestSettings.Own _ = new();
        using SessionFixture s = new SessionFixture().WithLights(3, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.3, Stars = SyntheticLight.ThreeStars });
        using SessionFixture p = WindowFixture.Partner();
        MainViewModel vm = new();

        await vm.OpenAsync(s.Root);
        Assert.True(vm.HasProblem);
        Assert.False(vm.HasResult);
        Assert.Contains("\"Alege partener...\" in fereastra", vm.ProblemText, StringComparison.Ordinal);

        vm.Partner = p.Root;
        Assert.Null(vm.Status);
        await vm.RerunAsync();

        Assert.True(vm.HasResult);
        Assert.False(vm.HasProblem);
        Assert.Equal(p.Root, vm.Result!.DarksFromPartner);
        Assert.Contains(vm.IndexRows, row => row.Label == "dark-uri (partener)");
        Assert.DoesNotContain(vm.IndexRows, row => row.Label == "partener");
        Assert.Contains(vm.SessionRows, row => row.Label == "darks" && row.Value.StartsWith("(lipsa; se folosesc dark-urile partenerului", StringComparison.Ordinal));
        Assert.StartsWith("=== DARK-URI (ale partenerului", vm.HeaderLine, StringComparison.Ordinal);
        Assert.Equal(3, vm.Lights.Count);
        Assert.Equal(3, vm.Frames.Count);
        Assert.Contains(vm.SessionRows, row => row.Label == "aparat" && row.Value == "NIKON D3300");

        // the export never writes into the partner whose darks the run took (R-074)
        vm.Export(Path.Combine(p.Root, "rapoarte"));
        Assert.StartsWith("EROARE: --out nu poate fi folderul partenerului", vm.Status, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(p.Root, "rapoarte")));

        // a partner that does not open says so on the card
        vm.Partner = Path.Combine(p.Root, "nu-exista");
        await vm.RerunAsync();
        Assert.True(vm.HasProblem);
        Assert.StartsWith("partenerul: nu gasesc folderul", vm.ProblemText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R122_TheLightsFolderReachesTheRunAndIsNeitherRememberedNorKeptForAnotherFolder()
    {
        using TestSettings.Own _ = new();
        using SessionFixture library = WindowFixture.Partner();
        using SessionFixture s = WindowFixture.Session();
        MainViewModel vm = new();
        Assert.False(vm.HasLightsFrom);
        Assert.Equal(WindowTexts.NoLightsFrom, vm.LightsFromText);

        vm.LightsFrom = s.LightsDir;
        Assert.True(vm.HasLightsFrom);
        Assert.Equal(s.LightsDir, vm.LightsFromText);
        Assert.Null(vm.Status);
        Assert.False(new MainViewModel().HasLightsFrom);

        await vm.OpenAsync(library.Root);
        Assert.True(vm.HasResult);
        Assert.True(vm.HasLights);
        Assert.Equal(4, vm.Lights.Count);
        Assert.Equal(s.LightsDir, vm.Result!.Inventory.Session.LightsDir);
        Assert.Contains(vm.SessionRows, row => row.Label == "lights" && row.Value == s.LightsDir + " (4 cadre .fit; alese de tine)");
        // lights at 1.3 of darks that are 0.5 of the library: 0.65, cosmetic correction only
        Assert.Contains(vm.LightRows, row => row.Label == "verdict" && row.Verdict == "ATENTIE" && row.Rest.StartsWith("foloseste dark-ul doar pentru corectia cosmetica", StringComparison.Ordinal));
        Assert.Equal(Path.GetFileName(library.Root) + "-" + vm.Result.Index.Tag, vm.Label);

        // the export never writes into the folder the lights were chosen from (R-055, the rule "Read-only input")
        vm.Export(Path.Combine(s.LightsDir, "rapoarte"));
        Assert.StartsWith("EROARE: --out nu poate fi folderul lights-urilor alese sau unul din el", vm.Status, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(s.LightsDir, "rapoarte")));

        // choosing or clearing after a run only says so; the next run of the same folder takes the change
        vm.LightsFrom = s.Root;
        Assert.Equal(WindowTexts.LightsFromChanged, vm.Status);
        Assert.Equal(s.LightsDir, vm.Result.Inventory.Session.LightsDir);
        await vm.RerunAsync();
        Assert.Null(vm.Status);
        Assert.Equal(s.LightsDir, vm.Result!.Inventory.Session.LightsDir);
        vm.LightsFrom = null;
        Assert.Equal(WindowTexts.LightsFromChanged, vm.Status);
        Assert.True(vm.HasLights);
        await vm.RerunAsync();
        Assert.False(vm.HasLights);

        // a choice is kept for a rerun of the same folder and dropped when another folder is opened
        vm.LightsFrom = s.LightsDir;
        await vm.RerunAsync();
        Assert.True(vm.HasLightsFrom);
        Assert.True(vm.HasLights);
        await vm.OpenAsync(s.Root);
        Assert.False(vm.HasLightsFrom);
        Assert.Equal(WindowTexts.NoLightsFrom, vm.LightsFromText);
        Assert.Contains(vm.SessionRows, row => row.Label == "lights" && row.Value == "lights\\ (4 cadre .fit)");

        // a lights folder that does not open says so on the card
        vm.LightsFrom = Path.Combine(s.Root, "nu-exista");
        await vm.RerunAsync();
        Assert.True(vm.HasProblem);
        Assert.StartsWith("lights-urile alese: nu gasesc folderul", vm.ProblemText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R066_ASessionWithoutDarksShowsItsOwnLabelAndSaysItIsTheLights()
    {
        using TestSettings.Own _ = new();
        using SessionFixture s = new SessionFixture().WithLights(3, i => new SyntheticLight { Seed = 300 + i, Hot = SyntheticLight.Lattice, HotFactor = 1.3, Stars = SyntheticLight.ThreeStars });
        using SessionFixture p = WindowFixture.Partner();
        MainViewModel vm = new();
        vm.Partner = p.Root;

        await vm.OpenAsync(s.Root);
        Assert.True(vm.HasResult);
        string like = Math.Round(vm.Result!.LightsIndex, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
        Assert.Equal(Path.GetFileName(s.Root) + "-idx" + like, vm.Label);
        Assert.Equal("idx" + like, vm.Tag);
        Assert.Equal(WindowTexts.IndexLeadLights, vm.IndexLead);
        Assert.Contains("dark-urile partenerului", vm.IndexLead, StringComparison.Ordinal);
        Assert.Contains(vm.IndexRows, row => row.Label == "dark-uri (partener)" && row.Value.EndsWith("; eticheta " + Path.GetFileName(p.Root) + "-" + vm.Result.Index.Tag, StringComparison.Ordinal));
        Assert.Contains(vm.IndexRows, row => row.Label == "eticheta" && row.Value == vm.Label);

        // a session with darks keeps the darks' label and the usual text
        using SessionFixture with = WindowFixture.Session();
        await vm.OpenAsync(with.Root);
        Assert.Equal(Path.GetFileName(with.Root) + "-" + vm.Result!.Index.Tag, vm.Label);
        Assert.Equal(WindowTexts.IndexLead, vm.IndexLead);
    }

    [Fact]
    public void TheNumbersAreParsedWithTheInvariantCulture()
    {
        using TestSettings.Own _ = new();
        MainViewModel vm = new() { IsoFactorText = "1.5", DoublingText = "5.5" };
        Assert.Equal(1.5, vm.Options.IsoFactorPerStop);
        Assert.Equal(5.5, vm.Options.DoublingCelsius);
        Assert.Equal("1.5", 1.5.ToString(CultureInfo.InvariantCulture));
    }
}
