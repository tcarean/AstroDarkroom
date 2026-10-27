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

using AstroPhotoAnalyzer.Tests;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf.Tests;

public class ViewModelTests
{
    static AnalysisResult Result(Raster r, Parameters? p = null) => AnalysisRun.Execute(r, "memorie", Scenes.OnBuiltForPaper(p));

    [Fact]
    public void R112_TheReportTabHasOneSectionPerTopicInDecisionOrder()
    {
        var vm = new ReportTabViewModel();
        vm.Populate(Result(Scenes.DarkSky(), new Parameters { TargetAspect = 50.0 / 35.0 }));

        Assert.True(vm.HasResult);
        Assert.Equal("PROBLEMA", vm.VerdictLabel);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, vm.Sections.Select(s => s.Number));
        Assert.Equal("PROBLEMA", vm.Sections[1].VerdictLabel);
        Assert.All(vm.Sections.SelectMany(s => s.Recommendations), r => Assert.False(string.IsNullOrEmpty(r.Because)));
        Assert.StartsWith(VersionInfo.Banner, vm.Text);
    }

    [Fact]
    public void R111_ClearEmptiesEveryTab()
    {
        var vm = new MainViewModel();
        var result = Result(Scenes.CleanSky());
        vm.Report.Populate(result);
        vm.Preview.Populate(result);

        vm.Report.Clear();
        vm.Preview.Clear();

        Assert.False(vm.Report.HasResult);
        Assert.Empty(vm.Report.Sections);
        Assert.Empty(vm.Preview.Tiles);
        Assert.Empty(vm.Preview.Guide);
    }

    [Fact]
    public void R117_PreviewTilesSayWhatToCheckAndTheGuideHasTheStepBack()
    {
        var vm = new PreviewTabViewModel();
        vm.Populate(Result(Scenes.DarkSky()));

        Assert.All(vm.Tiles, t => Assert.False(string.IsNullOrWhiteSpace(t.Check), t.Title));
        Assert.Equal(4, vm.Guide.Count);
        // The scene is 512 px: at 100 % it would be 12.8 cm, less than the 50 cm print - the
        // "printul e mai mare" branch.
        Assert.Contains("e mai mare decat vederea la 100 %", vm.Guide[1].Text);
    }

    [Fact]
    public void TonesExportResolutionAndBackgroundCarryTheirRows()
    {
        var result = Result(Scenes.DarkSky());
        var tones = new TonesTabViewModel(); tones.Populate(result);
        var export = new ExportTabViewModel(); export.Populate(result);
        var res = new ResolutionTabViewModel(); res.Populate(result);
        var bg = new BackgroundTabViewModel(); bg.Populate(result);

        Assert.Equal(101, tones.Histogram.Count);
        Assert.InRange(tones.PaperBlack, 8.9, 9.1);
        Assert.Equal(Thresholds.LowSeparationLstar, tones.LowSeparation);
        Assert.Contains($"linia rosie = negrul hartiei, linia galbena intrerupta = pragul de vizibilitate L* {Thresholds.LowSeparationLstar:0}", tones.HistogramHeading);
        Assert.Contains($"La stanga liniei galbene intrerupte (sub L* {Thresholds.LowSeparationLstar:0})", tones.HistogramNote);
        Assert.Equal(1 + 17 + 1, tones.SimulationRows.Count);   // header + "asa cum e" + grid 0.05..0.20 + note
        Assert.Null(tones.SimulationRows[0].Preview);
        Assert.All(tones.SimulationRows.Skip(1).Take(17), row => Assert.NotNull(row.Preview));
        Assert.Contains(tones.SimulationRows, row => row.Target == "asa cum e");
        Assert.All(tones.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Description), row.Label));
        // Rezolutie: a description on every row, except the corners (the centre covers them).
        Assert.Contains(res.Rows, row => row.Label == "FWHM median in centru" && row.Description != null);
        Assert.All(res.Rows, row => Assert.Equal(row.Label.Contains("coltul "), string.IsNullOrWhiteSpace(row.Description)));
        // header + 3 blacks + the scene's paper black (explicit Dmax 2.0, no medium's), 32/64,
        // L* 15/50, the recommended background
        Assert.Equal(1 + 9, tones.LadderRows.Count);
        Assert.Contains(tones.LadderRows, s => s.Contains("fundalul recomandat 0.1"));
        Assert.Single(tones.LadderRows, s => s.EndsWith("<- mediul ales") && s.Contains("Dmax explicit 2.0"));
        Assert.Equal(6, tones.Guide.Count);
        string guide = string.Join(" ", tones.Guide.Select(g => g.Lead + " " + g.Text));
        Assert.Contains($"percentila {Thresholds.DustClassFraction * 100:0} % a dalelor de praf", guide);
        Assert.Contains($"cel mai intunecat {Thresholds.SkyDarkestFraction * 100:0} % al dalelor de cer", guide);
        Assert.Contains($"peste {Thresholds.SkyNoiseK:0} sigma ale cerului", guide);
        Assert.Contains($"cel putin {Thresholds.DustClassMinFraction * 100:0} % din dale", guide);
        Assert.Contains("nivelul MINIM", guide);
        Assert.Contains("nivelul 13 e L* 4 (negrul baryta)", guide);
        Assert.Contains("canvas 1.5 -> L* 20.7", guide);
        Assert.Equal(4, export.JpegRows.Count);
        Assert.Equal(5, export.JpegGuide.Count);
        Assert.Contains(export.Rows, r => r.Label.StartsWith("Palier"));
        Assert.All(export.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Description), row.Label));
        Assert.Contains(res.Rows, r => r.Label == "Stele masurate");
        Assert.Equal(2 + 6 + 2, res.SizeRows.Count);   // 2 headers + 6 sizes + a 2-row note: no distance, no recommended row
        Assert.DoesNotContain(res.SizeRows, s => s.Contains(" *"));
        Assert.Contains(res.SizeRows, s => s.StartsWith("fara rand recomandat"));
        Assert.Equal(1 + 5, res.DpiRows.Count);        // header + the reference ladder; no given distance, no row of its own
        Assert.Contains(res.DpiRows, s => s.Contains("180") && s.Contains("0.49 m"));
        Assert.Equal(8, bg.RegionRows.Count);
        Assert.Contains(bg.RegionRows, r => r.EndsWith("! colorat") || r.EndsWith("! neutru"));
        Assert.All(bg.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Description), row.Label));
        Assert.Equal(5, bg.Guide.Count);
    }

    /// <summary>
    /// R-146: the curves follow the analysis without a press, so the gallery is there when the
    /// tab is opened; the run itself does not wait for them, and a run that fails starts none.
    /// </summary>
    [Fact]
    public async Task R146_TheCurvesAreComputedWithTheAnalysisWithoutAPress()
    {
        using var ws = new TempWorkspace();
        // Vertical stripes with five increasing levels, so that the frame has its five anchors
        // and the core suggests curves (the fixture of CurvesTabTests' save test).
        string tif = ws.WriteFile("calea-lactee.tif", TiffTestData.Build16Rgb(512, 512,
            (x, y, c) => (int)(65535 * (x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.7 : x < 32 ? 0.05 : x < 416 ? 0.15 : x < 480 ? 0.3 : 0.5)), IccTestData.V2("sRGB test")));
        var vm = new MainViewModel();
        Assert.True(vm.CurvesComputation.IsCompleted);

        await vm.OpenAsync(tif);

        Assert.True(vm.HasResult, vm.Error);
        Assert.False(vm.IsBusy);
        await vm.CurvesComputation;
        Assert.True(vm.Curves.HasGallery, vm.Curves.Message);
        Assert.False(vm.Curves.IsBusy);
        Assert.Equal("Originalul", vm.Curves.Current.Name);
        Assert.True(vm.Curves.Items.Count > 1);

        // The size is the default of a screen never reported: the previews fit a Full HD gallery.
        Assert.InRange(vm.Curves.Current.Image.PixelWidth, 1, Thresholds.CurvePreviewDefaultWidth);
        Assert.InRange(vm.Curves.Current.Image.PixelHeight, 1, Thresholds.CurvePreviewDefaultHeight);

        // A run a bad field stops keeps the tabs, the gallery with them, and starts nothing.
        Task done = vm.CurvesComputation;
        vm.Parameters.Distance = "1,5";
        await vm.RunAsync();
        Assert.True(vm.HasError);
        Assert.True(vm.Curves.HasGallery);
        Assert.Same(done, vm.CurvesComputation);

        // A run that fails in the core clears the tabs and starts no computation.
        vm.Parameters.Distance = "";
        await vm.OpenAsync(ws.PathTo("nu-exista.tif"));
        Assert.True(vm.HasError);
        Assert.False(vm.Curves.HasGallery);
        Assert.Same(done, vm.CurvesComputation);
    }

    [Fact]
    public async Task RunningOnAFileAndSavingGivesTheConsolesReport()
    {
        using var ws = new TempWorkspace();
        Raster r = Scenes.CleanSky();
        string tif = ws.WriteFile("curat.tif", TiffTestData.Build16Rgb(r.W, r.H, (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V4("sRGB IEC61966-2.1")));
        var vm = new MainViewModel();

        await vm.OpenAsync(tif);

        Assert.True(vm.HasResult, vm.Error);
        Assert.False(vm.IsBusy);
        Assert.True(vm.CanSave);
        Assert.Equal(Path.Combine(ws.Dir, "Raport.txt"), vm.DefaultReportPath);

        string saved = ws.PathTo("r.txt");
        vm.SaveReport(saved);
        var expected = new StringWriter();
        ReportWriter.WriteFull(AnalysisRun.Execute(tif, new Parameters()), expected);
        Assert.Equal(expected.ToString(), File.ReadAllText(saved));
    }

    /// <summary>
    /// A report that cannot be written is told under the buttons, not thrown (R-111): the
    /// result and the tabs stay; a save that works says where it went; the next run clears both.
    /// </summary>
    [Fact]
    public async Task R111_ASaveThatCannotWriteIsToldNotThrown()
    {
        using var ws = new TempWorkspace();
        Raster r = Scenes.CleanSky();
        string tif = ws.WriteFile("curat.tif", TiffTestData.Build16Rgb(r.W, r.H, (x, y, c) => RasterOps.Level16(r.At(c, y, x)), IccTestData.V4("sRGB IEC61966-2.1")));
        var vm = new MainViewModel();
        await vm.OpenAsync(tif);
        Assert.True(vm.HasResult, vm.Error);
        Assert.Null(vm.SaveMessage);
        Assert.Null(vm.SaveError);

        // Held open by another program: a sharing violation; the old file stays whole, and no
        // temporary file stays beside it (R-005).
        string held = ws.PathTo("Raport.txt");
        File.WriteAllText(held, "vechi");
        using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.None))
            vm.SaveReport(held);
        Assert.NotNull(vm.SaveError);
        Assert.Null(vm.SaveMessage);
        Assert.True(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.Equal("vechi", File.ReadAllText(held));
        Assert.Equal(2, Directory.GetFiles(ws.Dir).Length);

        // A folder that does not exist.
        vm.SaveReport(ws.PathTo(Path.Combine("nu-exista", "r.txt")));
        Assert.NotNull(vm.SaveError);
        Assert.Null(vm.SaveMessage);

        // A save that works says where, and the file is the console's.
        string saved = ws.PathTo("r.txt");
        vm.SaveReport(saved);
        Assert.Null(vm.SaveError);
        Assert.Equal("raport salvat: " + saved, vm.SaveMessage);
        var expected = new StringWriter();
        ReportWriter.WriteFull(AnalysisRun.Execute(tif, new Parameters()), expected);
        Assert.Equal(expected.ToString(), File.ReadAllText(saved));

        // The next run clears both, even one a bad field stops: the text was the old report's.
        vm.Parameters.Distance = "1,5";
        await vm.RunAsync();
        Assert.True(vm.HasError);
        Assert.Null(vm.SaveMessage);
        Assert.Null(vm.SaveError);
        vm.Parameters.Distance = "";
        vm.SaveReport(saved);
        Assert.NotNull(vm.SaveMessage);
        await vm.RunAsync();
        Assert.False(vm.HasError);
        Assert.Null(vm.SaveMessage);
        Assert.Null(vm.SaveError);
    }

    [Fact]
    public async Task ABadParameterStopsTheRunWithAMessage()
    {
        var vm = new MainViewModel();
        vm.Parameters.Distance = "1,5";

        await vm.OpenAsync("nu-conteaza.tif");

        Assert.True(vm.HasError);
        Assert.Contains("punctul", vm.Error);
        Assert.False(vm.HasResult);
    }

    [Fact]
    public void VerdictColoursFollowTheLabelsAndUnknownIsNeutral()
    {
        Assert.Equal(VerdictPalette.Good, VerdictPalette.For("OK"));
        Assert.Equal(VerdictPalette.Warn, VerdictPalette.For("ATENTIE"));
        Assert.Equal(VerdictPalette.Bad, VerdictPalette.For("PROBLEMA"));
        Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For("altceva"));
        Assert.Equal(VerdictPalette.Neutral, VerdictPalette.For(null));
        Assert.IsType<SolidColorBrush>(VerdictPalette.BrushFor("OK"));
    }

    [Fact]
    public void RasterBitmapsGiveThePixelsAskedFor()
    {
        var r = SyntheticRasters.From(10, 10, 3, (x, y, p) => p == 0 ? x / 10f : p == 1 ? y / 10f : 0.5f);

        var bmp = RasterBitmaps.Region(r, 2, 3, 4, 2, stride: 1, zoom: 2);
        Assert.Equal(8, bmp.PixelWidth);
        Assert.Equal(4, bmp.PixelHeight);

        var whole = RasterBitmaps.Whole(r, 5);
        Assert.Equal(5, whole.PixelWidth);
    }
}
