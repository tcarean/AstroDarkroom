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
    static AnalysisResult Result(Raster r, Parameters? p = null) => AnalysisRun.Execute(r, "memorie", p ?? new Parameters());

    [Fact]
    public void TheReportTabHasOneSectionPerTopicInDecisionOrder()
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
    public void ClearEmptiesEveryTab()
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
        Assert.Equal(5, tones.SimulationRows.Count);   // antet + "asa cum e" + tintele 0.15, 0.19, 0.20
        Assert.Equal(4, export.JpegRows.Count);
        Assert.Contains(export.Rows, r => r.Label.StartsWith("Palier"));
        Assert.Contains(res.Rows, r => r.Label == "Stele masurate");
        Assert.Equal(2 + 6 + 2, res.SizeRows.Count);   // 2 antete + 6 marimi + nota de 2 randuri: fara distanta nu e rand recomandat
        Assert.DoesNotContain(res.SizeRows, s => s.Contains(" *"));
        Assert.Contains(res.SizeRows, s => s.StartsWith("fara rand recomandat"));
        Assert.Equal(1 + 5, res.DpiRows.Count);        // antet + scara de referinta; fara distanta data, fara randul ei
        Assert.Contains(res.DpiRows, s => s.Contains("180") && s.Contains("0.49 m"));
        Assert.Equal(8, bg.RegionRows.Count);
        Assert.Contains(bg.RegionRows, r => r.EndsWith("!"));
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
