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

using System.Windows.Media.Imaging;
using LightsCheck.Core;
using LightsCheck.Tests;

namespace LightsCheck.Wpf.Tests;

/// <summary>
/// Opening a folder end to end (R-029), what the tabs hold (R-030), the star cut-outs (R-031)
/// and the export (R-032). The frames are the synthetic eight-frame pattern of the core's
/// tests, with frames 3 and 6 trailed.
/// </summary>
public class MainViewModelTests
{
    [Fact]
    public async Task R029_OpeningAFolderRunsTheWholeSeriesByItself()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);

        Assert.True(vm.HasResult);
        Assert.False(vm.IsRunning);
        Assert.True(vm.IsIdle);
        Assert.False(vm.HasProblem);
        Assert.Equal(f.Root, vm.Folder);
        Assert.Equal(8, vm.Frames.Count);
        Assert.StartsWith("lightscheck ", vm.Report, StringComparison.Ordinal);
        Assert.Null(vm.Status);
    }

    [Fact]
    public async Task R029_AFolderThatCannotBeAnalysedBecomesTheProblemCard()
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
    public async Task R029_StoppingTheRunLeavesNoResultAndTheWindowIdle()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();

        Task run = vm.OpenAsync(f.Root);
        vm.CancelPending();
        await run;

        Assert.False(vm.HasResult);
        Assert.False(vm.IsRunning);
        Assert.False(vm.HasProblem);
    }

    /// <summary>A run superseded by a later OpenAsync must not write its "stopped" line over the new run's state.</summary>
    [Fact]
    public async Task R029_ARunSupersededByAnotherFolderLeavesNoStatusLine()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace ws = new();
        MainViewModel vm = new();

        Task first = vm.OpenAsync(f.Root);
        Task second = vm.OpenAsync(Path.Combine(ws.Dir, "missing"));
        await Task.WhenAll(first, second);

        Assert.Null(vm.Status);
        Assert.True(vm.HasProblem);
        Assert.False(vm.HasResult);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task R029_StoppingAlwaysSaysSo()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();

        Task run = vm.OpenAsync(f.Root);
        vm.CancelPending();
        await run;

        Assert.Equal("Analiza a fost oprita.", vm.Status);
    }

    [Fact]
    public async Task R029_OpeningAnotherFolderDropsTheFirstResult()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace ws = new();
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);
        Assert.True(vm.HasResult);

        await vm.OpenAsync(Path.Combine(ws.Dir, "missing"));

        Assert.False(vm.HasResult);
        Assert.Empty(vm.Frames);
        Assert.Empty(vm.Report);
        Assert.True(vm.HasProblem);
    }

    /// <summary>
    /// A progress report queued by a run that was replaced must not write over the new run's
    /// progress (R-029): the callback checks that its run is still the current one, as the finally
    /// block does. The posts are held back by a queued context, so a report of the first run is
    /// delivered only after the second has started, as it can be on the window's dispatcher when
    /// the second folder is opened while the first run's frames are still finishing.
    /// </summary>
    [Fact]
    public void R029_AQueuedReportOfAReplacedRunDoesNotWriteOverTheNewRunsProgress()
    {
        using SessionFixture eight = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using SessionFixture three = new SessionFixture().WithStarSeries(SessionFixture.Pattern.Take(3).ToList());
        MainViewModel vm = new();
        using QueuedContext ctx = new();

        Task first = vm.OpenAsync(eight.Root);
        ctx.WaitForAPost();   // at least one report of the eight-frame run is queued, undelivered
        Task second = vm.OpenAsync(three.Root);
        List<string> afterSecond = [];
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.BusyMessage))
            {
                afterSecond.Add(vm.BusyMessage);
            }
        };
        ctx.RunUntil(second);
        ctx.RunUntil(first);

        Assert.DoesNotContain(afterSecond, m => m.Contains("/ 8", StringComparison.Ordinal));
        Assert.Contains(afterSecond, m => m.Contains("/ 3", StringComparison.Ordinal));
        Assert.True(vm.HasResult);
        Assert.Equal(3, vm.Frames.Count);
        Assert.False(vm.IsRunning);
    }

    /// <summary>
    /// The inputs the files do not carry: invariant parsing; blank or wrong means none typed
    /// (null, the run then reads the files or the raws, R-066); blank is not invalid, wrong is.
    /// </summary>
    [Theory]
    [InlineData("4.5", 4.5, true)]
    [InlineData("3.92", 3.92, true)]
    [InlineData("", null, true)]
    [InlineData("  ", null, true)]
    [InlineData("abc", null, false)]
    [InlineData("4,5", null, false)]
    [InlineData("-1", null, false)]
    [InlineData("NaN", null, false)]
    [InlineData("Infinity", null, false)]
    public void R029_ThePixelInputIsParsedInvariantAndBlankMeansNoneTyped(string typed, double? expected, bool valid)
    {
        MainViewModel vm = new() { PixelText = typed };

        Assert.Equal(expected, vm.Options.PixelSizeUm);
        Assert.Equal(valid, vm.PixelIsValid);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("3", 3)]
    [InlineData("0", 0)]
    [InlineData("x", 0)]
    public void R029_TheThreadsInputIsAPositiveCountOrTheDefault(string typed, int expected)
    {
        MainViewModel vm = new() { ThreadsText = typed };

        Assert.Equal(expected, vm.Options.Threads);
    }

    [Fact]
    public void R029_ThePixelInputStartsBlank_SoTheFilesOrTheRawsDecide()
    {
        MainViewModel vm = new();

        Assert.Equal("", vm.PixelText);
        Assert.True(vm.PixelIsValid);
        Assert.Null(vm.Options.PixelSizeUm);
        Assert.Equal("", vm.ThreadsText);
    }

    [Fact]
    public async Task R030_TheTabsHoldTheReportRowsAndTheFramesTable()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2);
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);

        Assert.NotEmpty(vm.SessionRows);
        Assert.Contains(vm.SessionRows, r => r.Label == "scara");
        Assert.NotEmpty(vm.SummaryRows);
        Assert.NotEmpty(vm.FocusRows);
        Assert.NotEmpty(vm.NoiseRows);
        Assert.NotEmpty(vm.DewRows);
        Assert.NotEmpty(vm.TrailRows);
        Assert.NotEmpty(vm.DriftRows);
        Assert.NotEmpty(vm.DitherRows);
        Assert.Contains(vm.SummaryRows, r => r.HasVerdict);

        Assert.Equal(Enumerable.Range(1, 8), vm.Frames.Select(r => r.Index));
        Assert.Equal(SeriesReport.FrameRows(vm.Result!).Select(r => r.Fwhm), vm.Frames.Select(r => r.Fwhm));
        Assert.Equal("SLAB", vm.Frames[2].Class);
        Assert.Equal("SLAB", vm.Frames[5].Class);
        Assert.Equal("bun", vm.Frames[0].Class);
        Assert.Contains("alungire", vm.Frames[2].Reasons, StringComparison.Ordinal);
        Assert.Equal("", vm.Frames[0].Reasons);

        Assert.Equal(8, vm.MapPoints.Count);
        Assert.Equal(8, vm.StepPoints.Count);
        Assert.Equal(8, vm.NearestPoints.Count);
        Assert.Equal(8, vm.FwhmPoints.Count);
        Assert.Equal(8, vm.ElongationPoints.Count);
        Assert.Equal(8, vm.StarPoints.Count);
        Assert.Equal(8, vm.BackgroundPoints.Count);
        Assert.Equal(2, vm.StepGuides.Count);
        Assert.Equal(2, vm.ElongationGuides.Count);
        Assert.Equal(Th.ElongationMarginal, vm.ElongationGuides[0].Value);
        Assert.Equal(Th.ElongationBad, vm.ElongationGuides[1].Value);
        Assert.Equal(Th.DitherStep, vm.StepGuides[0].Value);
        Assert.Equal(SeriesReport.ClassWord(FrameClass.Marginal), vm.ElongationGuides[0].Label);
        Assert.Equal(SeriesReport.ClassWord(FrameClass.Bad), vm.ElongationGuides[1].Label);
        Assert.Equal(SeriesReport.ScaleLabel, vm.SessionRows[^1].Label);
    }

    /// <summary>The frames to drop are the bad and the unreadable ones, the marginal stay with the user.</summary>
    [Fact]
    public async Task R030_TheRejectedTabListsTheBadFramesWithTheirReasons()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);

        Assert.Equal([3, 6], vm.Rejected.Select(r => r.Index));
        Assert.All(vm.Rejected, r => Assert.Contains("prag", r.Reasons, StringComparison.Ordinal));
        Assert.Equal(SeriesReport.RejectedHeading(vm.Result!), vm.RejectedHeading);
        Assert.StartsWith("2 cadre slabe", vm.RejectedHeading, StringComparison.Ordinal);
    }

    /// <summary>The report tab is the console's text, byte for byte: banner, then the series report.</summary>
    [Fact]
    public async Task R030_TheReportTabIsTheConsoleText()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);

        using StringWriter expected = new();
        RunReport.WriteBanner(expected);
        SeriesReport.Write(vm.Result!, expected);
        Assert.Equal(expected.ToString(), vm.Report);
    }

    /// <summary>The map's axis labels follow the RA axis the core found; unknown here, on synthetic frames.</summary>
    [Fact]
    public async Task R030_TheMapLabelsNameTheAxesOnlyWhenTheRaAxisIsKnown()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);

        Assert.Equal(vm.Result!.Series.RaAxis switch { Axis.Y => "Y = RA", Axis.X => "Y = DEC", _ => "Y" }, vm.MapYLabel);
    }

    [Fact]
    public void R030_TheClassColoursFollowTheVerdictPalette()
    {
        Assert.Equal(VerdictPalette.Good, MainViewModel.ClassColour(FrameClass.Good));
        Assert.Equal(VerdictPalette.Warn, MainViewModel.ClassColour(FrameClass.Marginal));
        Assert.Equal(VerdictPalette.Bad, MainViewModel.ClassColour(FrameClass.Bad));
        Assert.Equal(VerdictPalette.Neutral, MainViewModel.ClassColour(FrameClass.Unknown));
    }

    /// <summary>Selecting a frame decodes it again and cuts its brightest stars out, 25 x 25 px each.</summary>
    [Fact]
    public async Task R031_SelectingAFrameLoadsItsStarCutOuts()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);

        vm.SelectedFrame = vm.Frames[0];
        await vm.CropsPending;

        Assert.NotEmpty(vm.Crops);
        Assert.Null(vm.CropsNote);
        Assert.All(vm.Crops, c =>
        {
            Assert.Equal(2 * StarCrops.Radius + 1, c.Image.PixelWidth);
            Assert.Equal(2 * StarCrops.Radius + 1, c.Image.PixelHeight);
            Assert.True(c.Image.IsFrozen);
            Assert.False(string.IsNullOrWhiteSpace(c.Caption));
        });
        Assert.Contains(vm.Crops, c => c.Caption.Contains("centru", StringComparison.Ordinal));

        vm.SelectedFrame = null;
        await vm.CropsPending;
        Assert.Empty(vm.Crops);
    }

    [Fact]
    public async Task R031_ALaterSelectionWinsOverAnEarlierDecode()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);

        vm.SelectedFrame = vm.Frames[0];
        Task first = vm.CropsPending;
        vm.SelectedFrame = vm.Frames[1];
        await first;
        await vm.CropsPending;

        Assert.Same(vm.Frames[1], vm.SelectedFrame);
        Assert.NotEmpty(vm.Crops);
    }

    /// <summary>
    /// A decode that fails after another frame was selected must not write its error over the
    /// newer frame's note (R-031): the error path of LoadCropsAsync checks the selection as the
    /// success path does. The failing decode's continuation is held back by a queued context, so
    /// it runs only after the newer selection, as a slow decode's would on the window's dispatcher.
    /// </summary>
    [Fact]
    public async Task R031_AStaleDecodeThatFailsDoesNotOverwriteTheNewerFramesNote()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);
        FrameTableRow row = SeriesReport.FrameRows(vm.Result!)[0];
        // The same measured frame twice: once with a path no reader can open, once without a
        // path, which gets its note at once with no decode.
        FrameRowVm failing = new(row, Path.Combine(f.LightsDir, "lipseste.fit"));
        FrameRowVm unmeasured = new(row, null);

        using QueuedContext ctx = new();
        // The decode fails in microseconds. If it is already over when the await looks, the
        // catch runs inline, nothing is queued, and the attempt proves nothing: select again
        // until the continuation is the queued one (the pending task is then still open).
        Task stale = Task.CompletedTask;
        for (int attempt = 0; attempt < 20 && stale.IsCompleted; attempt++)
        {
            vm.SelectedFrame = null;
            vm.SelectedFrame = failing;
            stale = vm.CropsPending;
        }

        Assert.False(stale.IsCompleted, "the failing decode finished inline twenty times; nothing was queued");
        vm.SelectedFrame = unmeasured;
        Assert.Equal(WindowTexts.NoCrops, vm.CropsNote);
        ctx.RunUntil(stale);

        Assert.Equal(WindowTexts.NoCrops, vm.CropsNote);
        Assert.Same(unmeasured, vm.SelectedFrame);
    }

    /// <summary>The export writes the console's three files into the chosen folder and nothing into lights/.</summary>
    [Fact]
    public async Task R032_ExportWritesTheThreeFilesIntoTheChosenFolderOnly()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace ws = new();
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);
        string[] lightsBefore = Directory.GetFiles(f.LightsDir);
        string outDir = Path.Combine(ws.Dir, "out");

        vm.Export(outDir);

        Assert.StartsWith("Scris:", vm.Status, StringComparison.Ordinal);
        Assert.Equal(vm.Report, File.ReadAllText(Path.Combine(outDir, "lightscheck-report.txt")));
        Assert.True(File.Exists(Path.Combine(outDir, "lightscheck-frames.csv")));
        Assert.Equal(vm.Rejected.Select(r => r.File), File.ReadAllLines(Path.Combine(outDir, "lightscheck-rejected.txt")));
        Assert.Equal(lightsBefore, Directory.GetFiles(f.LightsDir));
    }

    [Fact]
    public void R032_ExportWithoutAResultDoesNothing()
    {
        using TempWorkspace ws = new();
        MainViewModel vm = new();

        vm.Export(Path.Combine(ws.Dir, "out"));

        Assert.Null(vm.Status);
        Assert.False(Directory.Exists(Path.Combine(ws.Dir, "out")));
    }

    [Fact]
    public async Task R032_AnExportThatCannotWriteBecomesAStatusLineNotACrash()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace ws = new();
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);
        string blocker = Path.Combine(ws.Dir, "file");
        File.WriteAllText(blocker, "not a folder");

        vm.Export(Path.Combine(blocker, "out"));

        Assert.StartsWith("EROARE", vm.Status, StringComparison.Ordinal);
    }

    /// <summary>Whatever the decoder throws for the selected frame becomes its note, never an unobserved exception.</summary>
    [Fact]
    public async Task R031_AFrameThatCannotBeDecodedAgainShowsTheErrorAsItsNote()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();
        await vm.OpenAsync(f.Root);
        File.WriteAllText(vm.Frames[0].Path!, "no longer a FITS");

        vm.SelectedFrame = vm.Frames[0];
        await vm.CropsPending;

        Assert.Empty(vm.Crops);
        Assert.StartsWith("EROARE", vm.CropsNote, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReportRowsHaveNoNoteAndNoKeyRow()
    {
        RowVm row = new("focus", "tinut", "BUN");

        Assert.True(row.HasVerdict);
        Assert.False(row.HasNote);
        Assert.False(row.IsKey);
        Assert.Null(row.Note);
        Assert.False(new RowVm("x", "y", null).HasVerdict);
    }

    /// <summary>The explanation under RESPINSE is the core's text; the frames carry the run cell of R-034 and the map rings the frames of a run.</summary>
    [Fact]
    public async Task R035_TheRejectedTabCarriesTheCoreExplanationAndTheFramesTheirRunCell()
    {
        (double Dx, double Dy, double Elongation, double PauseSeconds)[] pattern = [(0, 0, 1.0, 0), (1, 0.5, 1.0, 1), (0.5, 1.5, 1.0, 1), (30, 0, 2.0, 30), (30.5, 0.2, 1.0, 1)];
        using SessionFixture f = new SessionFixture().WithStarSeries(pattern);
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);

        Assert.Equal(SeriesReport.RejectedExplanation.Select(p => p.Text), vm.RejectedExplanation.Select(p => p.Text));
        string explanation = string.Join(" ", vm.RejectedExplanation.Select(p => p.Text));
        Assert.Contains("RA", explanation, StringComparison.Ordinal);
        Assert.Contains("DEC", explanation, StringComparison.Ordinal);
        Assert.Contains("la limita", explanation, StringComparison.Ordinal);
        Assert.Contains("CADRE MISCATE", explanation, StringComparison.Ordinal);

        Assert.Equal(SeriesReport.FrameRows(vm.Result!).Select(r => r.Spot), vm.Frames.Select(r => r.Spot));
        Assert.Equal("3/3 de la 1", vm.Frames[2].Spot);
        Assert.Equal("-", vm.Frames[3].Spot);
        Assert.Equal([true, true, true, false, false], vm.MapPoints.Select(p => p.Ring));
    }

    /// <summary>The headers, the FOCUS chip, the explanation paragraphs and the dithering tables are the core's, arranged (R-037).</summary>
    [Fact]
    public async Task R037_TheWindowTakesTheHeadersTheFocusChipTheParagraphsAndTheTablesFromTheCore()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2);
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);

        Assert.Equal("dX (DEC)", vm.DxHeader);
        Assert.Equal("dY (RA)", vm.DyHeader);
        Assert.Equal("X = DEC", vm.MapXLabel);
        Assert.Equal("Y = RA", vm.MapYLabel);

        RowVm focus = vm.FocusRows[^1];
        Assert.Equal("verdict", focus.Label);
        Assert.True(focus.HasVerdict);
        Assert.Equal("BUN", focus.Verdict);

        Assert.Equal(SeriesReport.RejectedExplanation.Select(p => p.Emphasis), vm.RejectedExplanation.Select(p => p.Emphasis));
        Assert.Single(vm.RejectedExplanation, p => p.Emphasis);

        Assert.Equal(SeriesReport.DitherTables(vm.Result!).Select(t => t.Title), vm.DitherTables.Select(t => t.Title));
        Assert.Equal(5, vm.DitherTables.Count);
        Assert.Equal(["axa", "numai pe ea", "semn +", "semn -"], vm.DitherTables[2].Columns);
        Assert.Equal(["X = DEC", "Y = RA", "ambele"], vm.DitherTables[2].Rows.Select(r => r[0]));
        Assert.Equal(SeriesReport.DitherScalarRows(vm.Result!).Select(r => r.Label), vm.DitherRows.Select(r => r.Label));
        Assert.Equal("verdict", vm.DitherRows[^1].Label);
    }

    /// <summary>The Dithering card carries the core's explanation, one paragraph bold, and the new rows of R-043, R-044 and R-046 reach the window untouched (R-047).</summary>
    [Fact]
    public async Task R047_TheDitherExplanationAndTheNewRowsReachTheWindow()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern, darks: 2);
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);

        Assert.Equal(SeriesReport.DitherExplanation.Select(p => p.Text), vm.DitherExplanation.Select(p => p.Text));
        Assert.Single(vm.DitherExplanation, p => p.Emphasis);
        Assert.Contains(vm.DitherRows, r => r.Label == "scara");
        Assert.Contains(vm.DitherRows, r => r.Label == "pattern fix" && r.Value.Contains("dark-uri", StringComparison.Ordinal));
        Assert.Contains(vm.DitherRows, r => r.Label == "pattern in stiva");
        Assert.Contains(vm.DriftRows, r => r.Label == "drift intr-o expunere");
        Assert.Contains(vm.SessionRows, r => r.Label == "darks" && r.Value.Contains("2 cadre", StringComparison.Ordinal));
        Assert.Equal(0, VerdictPalette.Severity("FARA walking noise, OK pentru pixeli calzi"));
        Assert.Equal(2, VerdictPalette.Severity("ATENTIE la pixeli calzi, FARA walking noise"));
    }

    /// <summary>The Cadre column and the bar tooltips carry the move cell and the tip the core words (R-039).</summary>
    [Fact]
    public async Task R039_TheFramesCarryTheMoveAndTheStepBarsTheirTip()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        MainViewModel vm = new();

        await vm.OpenAsync(f.Root);

        IReadOnlyList<FrameTableRow> rows = SeriesReport.FrameRows(vm.Result!);
        Assert.Equal(rows.Select(r => r.Move), vm.Frames.Select(r => r.Move));
        Assert.Equal("RA 15 px", vm.Frames[4].Move);
        Assert.Equal(rows.Select(r => r.StepTip), vm.StepPoints.Select(p => p.Tip));
        Assert.Null(vm.StepPoints[0].Tip);
        Assert.Equal("cadrul 5: pas 15.3 px; RA 15 px", vm.StepPoints[4].Tip);
    }

    /// <summary>The two arrow selectors start at the defaults, are remembered in the settings file, survive a broken file, and the cells follow them (R-041).</summary>
    [Fact]
    public async Task R041_TheArrowSelectorsAreRememberedAndTheCellsFollowThem()
    {
        using TempWorkspace ws = new();
        string file = Path.Combine(ws.Dir, "settings.json");
        string? previous = UserSettings.Path;
        UserSettings.Path = file;
        try
        {
            MainViewModel fresh = new();
            Assert.Equal("→", fresh.EastArrow);
            Assert.Equal("↑", fresh.NorthArrow);
            Assert.Equal(["→", "←"], fresh.EastChoices);
            Assert.Equal(["↑", "↓"], fresh.NorthChoices);
            Assert.Equal(ArrowKeys.Default, fresh.Options.Arrows);
            Assert.False(File.Exists(file));

            fresh.EastArrow = "←";
            fresh.NorthArrow = "↓";
            Assert.True(File.Exists(file));
            MainViewModel again = new();
            Assert.Equal("←", again.EastArrow);
            Assert.Equal("↓", again.NorthArrow);
            Assert.Equal(new ArrowKeys(Arrow.Left, Arrow.Down), again.Options.Arrows);

            using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
            // The fixture's lights are FITS: the Veil solve with its y column mirrored (ADR 0023).
            string[] wcs =
            [
                FitsTestData.Card("CTYPE1", "'RA---TAN'"), FitsTestData.Card("CTYPE2", "'DEC--TAN'"),
                FitsTestData.Card("CDELT1", "-0.00172"), FitsTestData.Card("CDELT2", "0.00172"),
                FitsTestData.Card("PC1_1", "0.0037"), FitsTestData.Card("PC1_2", "0.99999"),
                FitsTestData.Card("PC2_1", "0.99999"), FitsTestData.Card("PC2_2", "-0.0037"),
            ];
            File.WriteAllBytes(Path.Combine(f.Root, "result.fit"), FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: wcs));
            await again.OpenAsync(f.Root);
            Assert.Equal("RA 15 px → V", again.Frames[4].Move);
            Assert.Equal("cadrul 5: pas 15.3 px; RA 15 px → V", again.StepPoints[4].Tip);

            // The exported files follow the selection too: frame 5's arrow in the CSV.
            again.Export(ws.Dir);
            string[] csv = File.ReadAllLines(Path.Combine(ws.Dir, "lightscheck-frames.csv"));
            int col = Array.IndexOf(csv[0].Split(','), "move_arrows");
            Assert.Equal("→", csv[5].Split(',')[col].Trim('"'));   // the cell is quoted, like every text cell of the CSV
            Assert.Equal("{\"east\":\"←\",\"north\":\"↓\"}", File.ReadAllText(file));   // the symbols themselves, as the README says

            File.WriteAllText(file, "{ not json");
            MainViewModel broken = new();
            Assert.Equal("→", broken.EastArrow);
            Assert.Equal("↑", broken.NorthArrow);

            // A path that cannot be written (a directory in its place): the selection still changes, the next start reads the defaults.
            UserSettings.Path = ws.Dir;
            MainViewModel blocked = new() { EastArrow = "←" };
            Assert.Equal("←", blocked.EastArrow);
            Assert.Equal("→", new MainViewModel().EastArrow);
        }
        finally
        {
            UserSettings.Path = previous;
        }
    }

    [Fact]
    public void TheCropImageIsAGreyBitmap()
    {
        BitmapSource b = RasterBitmaps.Grey(new byte[] { 0, 128, 255, 7 }, 2, 2);

        Assert.Equal(2, b.PixelWidth);
        Assert.True(b.IsFrozen);
    }
}
