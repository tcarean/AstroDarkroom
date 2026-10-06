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
using System.Windows;

namespace AstroPhotoAnalyzer.Wpf.Tests;

/// <summary>
/// R-113, R-130, R-131: the Tonuri tab's definitions, warning and buttons, and the two
/// visualisation windows' view-models, with and without a WCS. The windows compute nothing:
/// every figure asserted here is read back from the core's result.
/// </summary>
[Collection("fereastra")]
public class OverlayWindowTests
{
    readonly StaFixture _sta;

    public OverlayWindowTests(StaFixture sta) => _sta = sta;

    static AnalysisResult Result(Raster r) => AnalysisRun.Execute(r, "memorie", Scenes.OnBuiltForPaper());

    static Raster Ldn42Scene() => WcsFixtures.Ldn42Scene();

    [Fact]
    public void R113_TheTabStatesTheDefinitionWarnsWithoutAWcsAndOffersTheMap()
    {
        var noWcs = new TonesTabViewModel();
        noWcs.Populate(Result(Scenes.DarkSky()));

        Assert.StartsWith("praful = ", noWcs.Definition);
        Assert.Contains(" (fara WCS); cerul = mediana dalelor de cer (clase relative la cerul modelat); negru pur = dale de 32 px sub negrul hartiei", noWcs.Definition);
        Assert.Equal(TonesTabViewModel.NoWcsSentence, noWcs.NoWcsWarning);
        Assert.Empty(noWcs.CloudRows);
        Assert.StartsWith("tinta exacta a prafului pe negru:", noWcs.ExactLine);
        Assert.Equal($"Imaginea la tinta randului, cu ce e sub negrul hartiei si sub L* {Thresholds.LowSeparationLstar:0} vopsit", noWcs.PreviewTooltip);
        Assert.Contains($"grila {new Parameters().SimulationTargets.Min():0.00}..{new Parameters().SimulationTargets.Max():0.00}", string.Join(" ", noWcs.Guide.Select(g => g.Text)));
        Assert.True(noWcs.OpenMap.CanExecute(null));

        var withWcs = new TonesTabViewModel();
        withWcs.Populate(Result(WcsFixtures.BandScene()));

        Assert.StartsWith("praful = percentila 25 % a dalelor de praf (", withWcs.Definition);
        Assert.Null(withWcs.NoWcsWarning);
        Assert.Contains(withWcs.CloudRows, s => s.StartsWith("LDN "));
        Assert.Equal(ReportWriter.CloudHeader, withWcs.CloudRows[0]);

        var inside = new TonesTabViewModel();
        inside.Populate(Result(Ldn42Scene()));
        Assert.Contains("(cadrul e in intregime in banda)", inside.Definition);
        Assert.Contains(inside.CloudRows, s => s.StartsWith("LDN 42 (B78)"));

        MapViewModel? requested = null;
        withWcs.MapRequested += m => requested = m;
        withWcs.OpenMap.Execute(null);
        Assert.NotNull(requested);
        Assert.True(requested!.HasWcs);

        PreviewWindowViewModel? previewed = null;
        withWcs.PreviewRequested += p => previewed = p;
        withWcs.SimulationRows[1].Preview!.Execute(null);
        Assert.NotNull(previewed);

        withWcs.Clear();
        Assert.Empty(withWcs.SimulationRows);
    }

    [Fact]
    public void R130_TheMapCarriesTheFiveLayersTheCloudsAndTheirCaptions()
    {
        AnalysisResult result = Result(WcsFixtures.BandScene());
        MapViewModel map = MapViewModel.From(result);

        Assert.True(map.HasWcs);
        Assert.True(map.ShowClouds);
        Assert.Equal(new[] { "praf", "cer", "banda", "nori luminosi", "varfuri" }, map.Layers.Select(l => l.Name));
        Assert.All(map.Layers, l => Assert.True(l.IsVisible));
        Assert.All(map.Layers, l => Assert.Equal(map.Width, l.Layer.PixelWidth));
        Assert.All(map.Layers, l => Assert.Contains("% din dale", l.Legend));
        Assert.Contains("sub nivelul local al benzii", map.Layers[0].Legend);
        Assert.Equal(512, map.Width);
        Assert.Equal(512, map.Height);
        Assert.Equal(map.Width, map.Image.PixelWidth);
        Assert.Null(map.NoWcsWarning);
        Assert.StartsWith("praful = percentila 25 % a dalelor de praf", map.Definition);
        Assert.NotEmpty(map.Clouds);
        Assert.Equal(result.Simulation.Clouds.Select(c => c.Name), map.Clouds.Select(c => c.Name));

        // The LDN 42 scene (wholly in the band): the sky path's five layers, the cloud named and circled.
        MapViewModel ldnMap = MapViewModel.From(Result(Ldn42Scene()));
        Assert.Equal(new[] { "praf", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri" }, ldnMap.Layers.Select(l => l.Name));
        CloudLayer ldn42 = Assert.Single(ldnMap.Clouds, c => c.Name == "LDN 42 (B78)");
        Assert.True(ldn42.Labelled);
        Assert.True(ldn42.IsVisible);
        Assert.InRange(ldn42.CentreX, 252, 259);
        Assert.Equal(WcsFixtures.Ldn42RadiusPx, ldn42.Radius, 0);
        Assert.Contains("L* 3.6", ldn42.Caption);
        Assert.Contains("tinta 0.", ldn42.Caption);
        Assert.Equal(ldn42.CentreX - ldn42.Radius, ldn42.Left);
        Assert.Equal(2 * ldn42.Radius, ldn42.Diameter);

        // "Niciunul" / "Toti" flip every cloud's box; the layer box is separate.
        map.DeselectAllClouds.Execute(null);
        Assert.All(map.Clouds, c => Assert.False(c.IsVisible));
        Assert.True(map.ShowClouds);
        map.SelectAllClouds.Execute(null);
        Assert.All(map.Clouds, c => Assert.True(c.IsVisible));
    }

    [Fact]
    public void R130_WithoutAWcsTheMapHasNoCloudsAndTheLabelsAreOff()
    {
        MapViewModel map = MapViewModel.From(Result(Scenes.DarkSky()));

        Assert.False(map.HasWcs);
        Assert.False(map.ShowClouds);
        Assert.Empty(map.Clouds);
        Assert.Equal(TonesTabViewModel.NoWcsSentence, map.NoWcsWarning);
        Assert.Contains("fara WCS", map.Definition);
        // Five layers on the sky path too, named for it, each with the image's own threshold in its legend.
        Assert.Equal(new[] { "praf", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri" }, map.Layers.Select(l => l.Name).ToArray());
        Assert.Contains("sigma ale cerului", map.Layers[2].Legend);
        Assert.Contains("pragul Otsu", map.Layers[3].Legend);
        Assert.Contains("L*-ul cerului = mediana lor", map.Layers[1].Legend);
        Assert.All(map.Layers, l => Assert.NotNull(l.Layer));

        MapViewModel band = MapViewModel.From(Result(WcsFixtures.BandScene()));
        Assert.Equal(new[] { "praf", "cer", "banda", "nori luminosi", "varfuri" }, band.Layers.Select(l => l.Name).ToArray());
        Assert.Contains("sub nivelul local al benzii", band.Layers[0].Legend);
    }

    [Fact]
    public void R131_ThePreviewLiftsToTheRowAndPaintsTheTwoClasses()
    {
        AnalysisResult result = Result(SyntheticRasters.Flat(64, 64, 0.08f));
        SimulationRow asIs = result.Simulation.AsIs;
        SimulationRow lifted = result.Simulation.Rows.Last();   // 0.20

        PreviewWindowViewModel a = PreviewWindowViewModel.From(result, asIs);
        Assert.StartsWith("Preview asa cum e (0.08)", a.Title);
        Assert.Contains("gamma 1.000", a.Caption);
        Assert.Contains("praful la L* 6.", a.Caption);
        Assert.Contains("sub negrul hartiei (L* 9.0): 100.0 % din pixeli", a.BlackLegend);
        Assert.True(a.ShowBlack && a.ShowLow);
        Assert.Equal(64, a.Width);

        PreviewWindowViewModel b = PreviewWindowViewModel.From(result, lifted);
        Assert.StartsWith("Preview fundal 0.20", b.Title);
        Assert.Contains("print-safe", b.Caption);
        Assert.Contains("sub negrul hartiei (L* 9.0): 0.0 % din pixeli", b.BlackLegend);
        Assert.Contains("0.0 % din pixeli", b.LowLegend);
    }

    [Fact]
    public void R130_TheMapWindowRendersWithAWcsAndNoBindingErrors()
        => _sta.Invoke(() =>
        {
            MapViewModel map = MapViewModel.From(Result(WcsFixtures.BandScene()));
            var window = new MapWindow { DataContext = map };
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(1400, 860));
            content.Arrange(new Rect(0, 0, 1400, 860));
            content.UpdateLayout();
            Assert.True(content.ActualWidth > 0);
            map.ShowClouds = false;
            map.Clouds[0].IsVisible = false;
            map.Layers[0].IsVisible = false;
            content.UpdateLayout();
        });
}
