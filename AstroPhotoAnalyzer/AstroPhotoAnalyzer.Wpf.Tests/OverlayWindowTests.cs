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
/// visualisation windows: their view-models, with and without a WCS, and the windows built over
/// them, the preview's check boxes driving its layers. The windows compute nothing: every figure
/// asserted here is read back from the core's result.
/// </summary>
[Collection("fereastra")]
public class OverlayWindowTests
{
    readonly StaFixture _sta;

    public OverlayWindowTests(StaFixture sta) => _sta = sta;

    static AnalysisResult Result(Raster r) => AnalysisRun.Execute(r, "memorie", Scenes.OnBuiltForPaper());

    static Raster Ldn42Scene() => WcsFixtures.Ldn42Scene();

    [Fact]
    public void R113_TheHistogramMarksPaperBlackAndTheVisibilityThresholdInEitherOrder()
    {
        // The scenes' paper (Dmax 2.0): paper black L* 9.0, left of the threshold; canvas (Dmax 1.5):
        // L* 20.7, right of it. The chart's texts must be the same strings either way.
        var baryta = new TonesTabViewModel();
        baryta.Populate(Result(Scenes.DarkSky()));
        var canvas = new TonesTabViewModel();
        canvas.Populate(AnalysisRun.Execute(Scenes.DarkSky(), "memorie", new Parameters { Medium = PrintMedium.Canvas }));

        Assert.Equal(Thresholds.LowSeparationLstar, baryta.LowSeparation);
        Assert.Equal(Thresholds.LowSeparationLstar, canvas.LowSeparation);
        Assert.True(baryta.PaperBlack < baryta.LowSeparation, $"baryta {baryta.PaperBlack}");
        Assert.True(canvas.PaperBlack > canvas.LowSeparation, $"canvas {canvas.PaperBlack}");
        Assert.Equal(baryta.HistogramHeading, canvas.HistogramHeading);
        Assert.Equal(baryta.HistogramNote, canvas.HistogramNote);
        Assert.StartsWith("HISTOGRAMA L* (0..100); linia rosie = negrul hartiei, linia galbena intrerupta = L* 15, sub care ochiul abia mai deosebeste tonurile", baryta.HistogramHeading);
        Assert.StartsWith("La stanga liniei rosii totul iese pe hartie cu acelasi negru. La stanga liniei galbene intrerupte (sub L* 15) hartia inca deosebeste tonurile, dar ochiul", baryta.HistogramNote);
        // Under paper black the paper separates nothing, and that region is also left of the threshold.
        Assert.DoesNotContain("hartia inca separa", baryta.HistogramNote);
        // Each sentence says what lies left of one line; none says which line is left of the other.
        Assert.DoesNotContain("intre linia", baryta.HistogramNote);
        Assert.DoesNotContain("intre cele", baryta.HistogramNote);
        Assert.DoesNotContain("dreapta", baryta.HistogramNote);

        canvas.Clear();
        Assert.True(double.IsNaN(canvas.PaperBlack));
        Assert.True(double.IsNaN(canvas.LowSeparation));
        Assert.Empty(canvas.Histogram);
    }

    [Fact]
    public void R113_TheTabStatesTheDefinitionWarnsWithoutAWcsAndOffersTheMap()
    {
        var noWcs = new TonesTabViewModel();
        noWcs.Populate(Result(Scenes.DarkSky()));

        Assert.StartsWith("praful = ", noWcs.Definition);
        Assert.Contains(" (fara WCS); cerul = valoarea din mijloc a dalelor de cer (dalele impartite prin comparatie cu cerul); negru pur = dale de 32 px mai intunecate decat negrul hartiei", noWcs.Definition);
        Assert.Equal(TonesTabViewModel.NoWcsSentence, noWcs.NoWcsWarning);
        Assert.StartsWith("fundalul la care praful ajunge exact pe negrul hartiei:", noWcs.ExactLine);
        Assert.Equal($"Arata imaginea cu fundalul mutat la valoarea din rand, intr-o fereastra separata; cele doua casute de acolo coloreaza ce ar iesi negru pe hartie si ce e sub L* {Thresholds.LowSeparationLstar:0}", noWcs.PreviewTooltip);
        Assert.Contains($"grila {new Parameters().SimulationTargets.Min():0.00}..{new Parameters().SimulationTargets.Max():0.00}", string.Join(" ", noWcs.Guide.Select(g => g.Text)));
        Assert.True(noWcs.OpenMap.CanExecute(null));

        var withWcs = new TonesTabViewModel();
        withWcs.Populate(Result(WcsFixtures.BandScene()));

        Assert.StartsWith("praful = cele mai intunecate zone intinse din norii de praf (pragul sub care stau 25 % din dalele de praf; ", withWcs.Definition);
        Assert.Null(withWcs.NoWcsWarning);
        // The clouds' rows are the map window's list (R-130), not a table on the tab.
        Assert.Null(typeof(TonesTabViewModel).GetProperty("CloudRows"));

        var inside = new TonesTabViewModel();
        inside.Populate(Result(Ldn42Scene()));
        Assert.Contains("(cadrul e in intregime in banda)", inside.Definition);

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

    /// <summary>
    /// The share in each legend is the core's figure (DustMap.FractionOf, the one the report's
    /// class fractions use), not a count made in the window (R-130; the rule "Computed only in
    /// the core"). It held before the window called the core, the two counts being the same, so
    /// it guards the figure, not the call.
    /// </summary>
    [Fact]
    public void R130_TheLegendsShareIsTheCoresFraction()
    {
        PatchClass[] order = { PatchClass.Dust, PatchClass.Sky, PatchClass.Band, PatchClass.BrightCloud, PatchClass.Peak };
        foreach (AnalysisResult result in new[] { Result(WcsFixtures.BandScene()), Result(Scenes.DarkSky()) })
        {
            MapViewModel map = MapViewModel.From(result);
            DustMap core = result.Simulation.Map;
            Assert.Equal(order.Length, map.Layers.Count);
            for (int k = 0; k < order.Length; k++)
                Assert.StartsWith($"{map.Layers[k].Name}: {Fmt.Pct(100 * core.FractionOf(order[k]))} din dale", map.Layers[k].Legend);
        }
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
        Assert.Contains("mai intunecate decat banda din jur", map.Layers[0].Legend);
        Assert.Equal(512, map.Width);
        Assert.Equal(512, map.Height);
        Assert.Equal(map.Width, map.Image.PixelWidth);
        Assert.Null(map.NoWcsWarning);
        Assert.StartsWith("praful = cele mai intunecate zone intinse din norii de praf", map.Definition);
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
        Assert.StartsWith("LDN 42 (B78)", ldn42.Caption);
        Assert.Contains("L* 3.6", ldn42.Caption);
        Assert.Contains("tinta 0.", ldn42.Caption);
        Assert.Contains(" dale)", ldn42.Caption);
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
        Assert.Contains("ori zgomotul lui, care e de", map.Layers[2].Legend);
        Assert.Contains("pragul la care nebuloasa se desparte", map.Layers[3].Legend);
        Assert.Contains("valoarea din mijloc a acestor dale", map.Layers[1].Legend);
        Assert.All(map.Layers, l => Assert.NotNull(l.Layer));

        MapViewModel band = MapViewModel.From(Result(WcsFixtures.BandScene()));
        Assert.Equal(new[] { "praf", "cer", "banda", "nori luminosi", "varfuri" }, band.Layers.Select(l => l.Name).ToArray());
        Assert.Contains("mai intunecate decat banda din jur", band.Layers[0].Legend);
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
        Assert.False(a.ShowBlack || a.ShowLow);   // both layers start off (ADR 0064)
        Assert.Equal(64, a.Width);

        PreviewWindowViewModel b = PreviewWindowViewModel.From(result, lifted);
        Assert.StartsWith("Preview fundal 0.20", b.Title);
        Assert.Contains("print-safe", b.Caption);
        Assert.Contains("sub negrul hartiei (L* 9.0): 0.0 % din pixeli", b.BlackLegend);
        Assert.Contains("0.0 % din pixeli", b.LowLegend);
    }

    /// <summary>
    /// The window as it opens: both boxes unchecked and both layers hidden over the lifted image
    /// (ADR 0064); a ticked box shows its own layer and leaves the other hidden.
    /// </summary>
    [Fact]
    public void R131_ThePreviewWindowOpensWithBothLayersOff()
        => _sta.Invoke(() =>
        {
            AnalysisResult result = Result(SyntheticRasters.Flat(64, 64, 0.08f));
            PreviewWindowViewModel preview = PreviewWindowViewModel.From(result, result.Simulation.AsIs);
            var window = new PreviewWindow { DataContext = preview };
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(1280, 760));
            content.Arrange(new Rect(0, 0, 1280, 760));
            content.UpdateLayout();

            var boxes = new List<System.Windows.Controls.CheckBox>();
            var images = new List<System.Windows.Controls.Image>();
            void Collect(DependencyObject d)
            {
                foreach (object child in LogicalTreeHelper.GetChildren(d))
                {
                    if (child is System.Windows.Controls.CheckBox b) boxes.Add(b);
                    if (child is System.Windows.Controls.Image i) images.Add(i);
                    if (child is DependencyObject c) Collect(c);
                }
            }
            Collect(content);

            // The boxes: paper black, then L* 15, each labelled with its legend. The images: the
            // lifted one, then the L* 15 and the paper-black layers over it, in the XAML's order.
            Assert.Equal(new[] { preview.BlackLegend, preview.LowLegend }, boxes.Select(box => ((System.Windows.Controls.TextBlock)box.Content).Text));
            Assert.All(boxes, box => Assert.False(box.IsChecked));
            Assert.Equal(new[] { Visibility.Visible, Visibility.Collapsed, Visibility.Collapsed }, images.Select(i => i.Visibility));
            Assert.Same(preview.LowLayer, images[1].Source);
            Assert.Same(preview.BlackLayer, images[2].Source);

            boxes[0].IsChecked = true;
            Assert.True(preview.ShowBlack);
            Assert.False(preview.ShowLow);
            Assert.Equal(new[] { Visibility.Visible, Visibility.Collapsed, Visibility.Visible }, images.Select(i => i.Visibility));

            boxes[0].IsChecked = false;
            boxes[1].IsChecked = true;
            Assert.False(preview.ShowBlack);
            Assert.True(preview.ShowLow);
            Assert.Equal(new[] { Visibility.Visible, Visibility.Visible, Visibility.Collapsed }, images.Select(i => i.Visibility));
        });

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

    /// <summary>
    /// R-130 and R-131 (ADR 0078): the texts of the map window and of the preview window are in
    /// plain words, in both languages: every layer's legend on the sky path and on the band path,
    /// the preview's caption and its two boxes for the row as it is and a lifted one, and the
    /// texts the two windows write themselves, read from their XAML.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R130_TheMapAndPreviewTextsAreInPlainWords(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        var texts = new List<string>();
        foreach (Raster r in new[] { Scenes.DarkSky(), WcsFixtures.BandScene() })
        {
            AnalysisResult result = Result(r);
            texts.AddRange(MapViewModel.From(result).Layers.Select(l => l.Legend));
            foreach (SimulationRow row in new[] { result.Simulation.AsIs, result.Simulation.Rows.Last() })
            {
                PreviewWindowViewModel p = PreviewWindowViewModel.From(result, row);
                texts.AddRange(new[] { p.Caption, p.BlackLegend, p.LowLegend });
            }
        }
        var pair = new System.Text.RegularExpressions.Regex(@"\{local:T En='([^']*)', Ro='([^']*)'\}");
        foreach (string view in new[] { "MapWindow.xaml", "PreviewWindow.xaml" })
            texts.AddRange(pair.Matches(Xaml(view)).Select(m => m.Groups[english ? 1 : 2].Value));

        var methodWords = new System.Text.RegularExpressions.Regex(
            @"\b(mediana|median|percentil\w*|Otsu|modelat|modelled|sigma|catalogati|etichet\w*|labels?|nivelul ei local|local level|coada|tail)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.All(texts, t => Assert.False(methodWords.IsMatch(t), t));
        Assert.All(texts, t => Assert.DoesNotContain("|b|", t));
        Assert.Contains(Tr.Pick("CATALOGUED DUST CLOUDS", "NORI DE PRAF DIN CATALOG"), texts);
        Assert.Contains(texts, t => t.StartsWith(Tr.Pick("For each cloud: its L*, the target", "Pentru fiecare nor: L*-ul lui, tinta")));
    }

    /// <summary>A view's XAML, read from the source tree beside this file's project.</summary>
    static string Xaml(string view, [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)!)!, "AstroPhotoAnalyzer.Wpf", "Views", view));
}
