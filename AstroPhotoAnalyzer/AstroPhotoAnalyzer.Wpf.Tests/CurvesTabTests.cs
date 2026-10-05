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
/// R-146, R-147: the Curbe tab's gallery and its curve window. The tab computes nothing: every
/// name, point and figure asserted here is read back from the core's <c>CurveRun</c>.
/// </summary>
[Collection("fereastra")]
public class CurvesTabTests
{
    readonly StaFixture _sta;

    public CurvesTabTests(StaFixture sta) => _sta = sta;

    /// <summary>The band scene at 0.7 of its values: its peak patch is under white, so the five anchors increase.</summary>
    static AnalysisResult Result()
    {
        Raster r = WcsFixtures.BandScene();
        foreach (float[] plane in r.Planes)
            for (int i = 0; i < plane.Length; i++) plane[i] *= 0.7f;
        return AnalysisRun.Execute(r, "memorie", Scenes.OnBuiltForPaper());
    }

    [Fact]
    public void R146_TheGalleryStartsEmptyThenHoldsTheOriginalAndTheSuggestions()
    {
        AnalysisResult result = Result();
        var tab = new CurvesTabViewModel();
        tab.Populate(result);

        Assert.True(tab.IsPrint);
        Assert.False(tab.IsScreen);
        Assert.False(tab.HasGallery);
        Assert.Empty(tab.Items);
        Assert.True(tab.Calculate.CanExecute(null));

        tab.Compute(800, 500);   // what Calculeaza runs, with the size of the gallery area

        CurveSet core = CurveRun.Execute(result, CurvePurpose.Print, 800, 500);
        Assert.True(tab.HasGallery);
        Assert.Equal(new[] { "Originalul" }.Concat(core.Suggestions.Select(s => s.Name)).ToArray(), tab.Items.Select(i => i.Name).ToArray());
        Assert.Equal("Originalul", tab.Current.Name);
        Assert.Equal($"1 / {tab.Items.Count}", tab.Position);
        Assert.InRange(tab.Current.Image.PixelWidth, 1, 800);
        Assert.InRange(tab.Current.Image.PixelHeight, 1, 500);
        Assert.Contains("clasele", tab.AnchorSource);

        // The original has no curve to show or to save.
        Assert.False(tab.OpenCurve.CanExecute(null));
        Assert.False(tab.SaveAs.CanExecute(null));
        Assert.False(tab.Previous.CanExecute(null));

        tab.Next.Execute(null);
        Assert.Equal("Blanda", tab.Current.Name);
        Assert.Equal($"2 / {tab.Items.Count}", tab.Position);
        Assert.True(tab.OpenCurve.CanExecute(null));
        Assert.True(tab.SaveAs.CanExecute(null));
        Assert.NotEmpty(tab.Current.Evidence);

        for (int i = 0; i < 10; i++) if (tab.Next.CanExecute(null)) tab.Next.Execute(null);
        Assert.Equal(tab.Items[^1].Name, tab.Current.Name);
        Assert.False(tab.Next.CanExecute(null));
        tab.Previous.Execute(null);
        Assert.Equal(tab.Items[^2].Name, tab.Current.Name);
    }

    [Fact]
    public void R146_ThePurposeChangesTheEvidenceOnlyAndAnotherImageClearsTheGallery()
    {
        AnalysisResult result = Result();
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.Compute(800, 500);
        tab.Next.Execute(null);
        string[] names = tab.Items.Select(i => i.Name).ToArray();
        string printEvidence = string.Join("\n", tab.Current.Evidence);
        Assert.Contains("Din profil ICC...", printEvidence);

        tab.IsScreen = true;

        Assert.True(tab.HasGallery);
        Assert.Equal(names, tab.Items.Select(i => i.Name).ToArray());
        Assert.Equal("Blanda", tab.Current.Name);
        Assert.DoesNotContain("Din profil ICC...", string.Join("\n", tab.Current.Evidence));

        tab.Clear();
        Assert.False(tab.HasGallery);
        Assert.Empty(tab.Items);
        Assert.True(tab.IsPrint);
    }

    [Fact]
    public void R146_SaveAsProposesANameAndWritesTheCoresFile()
    {
        using var ws = new TempWorkspace();
        // A TIFF input: the proposed name and the saved file are a FITS all the same (R-145).
        string input = ws.WriteFile("calea-lactee.tif", TiffTestData.Build16Rgb(64, 64,
            (x, y, c) => (ushort)(3000 + 40000 * ((x * 7 + y * 3 + c) % 64) / 64), IccTestData.V2("sRGB test")));
        AnalysisResult result = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.Compute(400, 300);
        if (!tab.HasGallery || tab.Items.Count < 2) return;   // a scene whose anchors do not increase has nothing to save
        tab.Next.Execute(null);

        string? proposed = null;
        tab.SaveRequested += name => proposed = name;
        tab.SaveAs.Execute(null);
        Assert.Equal("calea-lactee_Blanda.fit", proposed);

        string target = ws.PathTo(proposed!);
        tab.Save(target);
        Assert.True(File.Exists(target));
        Assert.Null(tab.SaveError);
        Assert.Contains(target, tab.SaveMessage);

        tab.Save(input);   // over the input: refused, said, nothing written
        Assert.Contains("imaginea de intrare", tab.SaveError);
    }

    [Fact]
    public void R147_TheCurveWindowListsThePointsWithTheirLabelsAndSamplesBothCharts()
    {
        AnalysisResult result = Result();
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.Compute(800, 500);
        while (tab.Current.Name != "Puternica") tab.Next.Execute(null);

        CurveWindowViewModel? opened = null;
        tab.CurveRequested += w => opened = w;
        tab.OpenCurve.Execute(null);
        Assert.NotNull(opened);
        CurveWindowViewModel w = opened!;

        CurveSuggestion strong = CurveRun.Execute(result, CurvePurpose.Print, 800, 500).Suggestions[2];
        Assert.Contains("Puternica", w.Title);
        Assert.Equal(7, w.Rows.Count);
        Assert.Equal(new[] { "", "praf", "cer", "banda", "nori luminosi", "varfuri", "" }, w.Rows.Select(r => r.Label).ToArray());
        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(strong.Curve.Points[i].X.ToString("0.000"), w.Rows[i].Input);
            Assert.Equal(strong.Curve.Points[i].Y.ToString("0.000"), w.Rows[i].Output);
        }
        Assert.Equal(w.Rows[2].Input, w.Rows[2].Output);   // the sky stays where it is

        // The whole curve on 0..1, and the shadows alone up to a little over the band's input.
        Assert.Equal(0, w.Chart.First().X, 9);
        Assert.Equal(1, w.Chart.Last().X, 9);
        Assert.Equal(strong.Curve.Map(w.Chart[w.Chart.Count / 2].X), w.Chart[w.Chart.Count / 2].Y, 6);
        Assert.Equal(Thresholds.CurveShadowChartFactor * strong.Curve.Points[3].X, w.ShadowLimit, 9);
        Assert.Equal(w.ShadowLimit, w.ShadowChart.Last().X, 9);
        Assert.Equal(5, w.Markers.Count);
        Assert.Equal("banda", w.Markers[2].Label);
    }

    [Fact]
    public void R146_TheTabAndTheCurveWindowRenderWithNoBindingErrors()
        => _sta.Invoke(() =>
        {
            var tab = new CurvesTabViewModel();
            tab.Populate(Result());
            var view = new CurvesView { DataContext = tab };
            view.Measure(new Size(1400, 860));
            view.Arrange(new Rect(0, 0, 1400, 860));
            view.UpdateLayout();

            // The two radio buttons follow the purpose, both ways.
            var radios = new List<System.Windows.Controls.RadioButton>();
            void Collect(DependencyObject d)
            {
                foreach (object child in LogicalTreeHelper.GetChildren(d))
                {
                    if (child is System.Windows.Controls.RadioButton r) radios.Add(r);
                    if (child is DependencyObject c) Collect(c);
                }
            }
            Collect(view);
            Assert.Equal(new[] { "Print", "Ecran" }, radios.Select(r => (string)r.Content).ToArray());
            Assert.True(radios[0].IsChecked);
            Assert.False(radios[1].IsChecked);
            radios[1].IsChecked = true;
            Assert.True(tab.IsScreen);
            Assert.False(radios[0].IsChecked);
            tab.IsPrint = true;
            Assert.True(radios[0].IsChecked);

            tab.Compute(800, 500);
            tab.Next.Execute(null);
            view.UpdateLayout();
            Assert.True(view.ActualWidth > 0);

            CurveWindowViewModel? opened = null;
            tab.CurveRequested += w => opened = w;
            tab.OpenCurve.Execute(null);
            var window = new CurveWindow { DataContext = opened };
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(1200, 800));
            content.Arrange(new Rect(0, 0, 1200, 800));
            content.UpdateLayout();
            Assert.True(content.ActualWidth > 0);
        });
}
