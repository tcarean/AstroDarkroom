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

        // The paper checks, and only they, are the warnings the view paints red.
        CurveSuggestion core = CurveRun.Execute(result, CurvePurpose.Print, 800, 500).Suggestions[0];
        Assert.NotEmpty(core.Evidence.PaperChecks);
        Assert.Equal(tab.Current.Evidence, tab.Current.EvidenceRows.Select(r => r.Text).ToArray());
        Assert.Equal(core.Evidence.PaperChecks.Select(c => $"! {c.Text} (fiindca: {c.Because})").ToArray(),
            tab.Current.EvidenceRows.Where(r => r.IsWarning).Select(r => r.Text).ToArray());

        tab.IsScreen = true;

        Assert.True(tab.HasGallery);
        Assert.Equal(names, tab.Items.Select(i => i.Name).ToArray());
        Assert.Equal("Blanda", tab.Current.Name);
        Assert.DoesNotContain("Din profil ICC...", string.Join("\n", tab.Current.Evidence));
        Assert.DoesNotContain(tab.Current.EvidenceRows, r => r.IsWarning);

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
        // Vertical stripes with five increasing levels, so that the frame has its five anchors.
        string input = ws.WriteFile("calea-lactee.tif", TiffTestData.Build16Rgb(512, 512,
            (x, y, c) => (int)(65535 * (x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.7 : x < 32 ? 0.05 : x < 416 ? 0.15 : x < 480 ? 0.3 : 0.5)), IccTestData.V2("sRGB test")));
        AnalysisResult result = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.Compute(400, 300);
        Assert.True(tab.HasGallery, tab.Message);
        Assert.Equal(ws.Dir, tab.InputFolder);
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

        byte[] before = File.ReadAllBytes(input);
        tab.Save(input);   // over the input: refused, said, nothing written
        Assert.Contains("imaginea de intrare", tab.SaveError);
        Assert.Null(tab.SaveMessage);
        Assert.Equal(before, File.ReadAllBytes(input));

        // The saved file is a float FITS that reads back as the curve applied to the image.
        Raster back = ImageLoader.Load(target);
        Assert.Equal("FITS", back.Source.Format);
        Assert.Equal(512, back.W);
        Assert.Equal(3, back.Planes.Length);
    }

    [Fact]
    public async Task R146_CalculeazaRunsInTheBackgroundAndALateResultNeverReachesAnotherImage()
    {
        AnalysisResult first = Result(), second = Result();
        var tab = new CurvesTabViewModel();
        tab.Populate(first);

        // The button's own path: busy while it runs, then the gallery for the purpose chosen
        // at the end, not at the start.
        Task run = tab.ComputeAsync();
        Assert.True(tab.IsBusy);
        Assert.False(tab.Calculate.CanExecute(null));
        tab.IsScreen = true;
        await run;
        Assert.False(tab.IsBusy);
        Assert.True(tab.HasGallery, tab.Message);
        Assert.Null(tab.Message);
        tab.Next.Execute(null);
        Assert.DoesNotContain(tab.Current.EvidenceRows, r => r.IsWarning);
        tab.IsPrint = true;
        Assert.Contains(tab.Current.EvidenceRows, r => r.IsWarning);

        // Another image opened while a computation runs: the old result is dropped, the tab
        // is free for the new image at once.
        Task late = tab.ComputeAsync();
        tab.Populate(second);
        Assert.False(tab.IsBusy);
        Assert.True(tab.Calculate.CanExecute(null));
        await late;
        Assert.False(tab.HasGallery);
        Assert.Empty(tab.Items);
        Assert.Null(tab.Message);
        Assert.False(tab.IsBusy);
    }

    [Fact]
    public void R146_AReasonForNoCurvesIsShownAsAProblemAndTheGalleryStaysEmpty()
    {
        // Stripes with the peak patch at 0.95, over the target: the core gives a reason, the tab
        // marks it as a problem (drawn red), and there is nothing to page through.
        Raster ceiling = SyntheticRasters.From(512, 512, 3, (x, y, p) => x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.95f : x < 32 ? 0.05f : x < 416 ? 0.15f : x < 480 ? 0.3f : 0.5f);
        var tab = new CurvesTabViewModel();
        tab.Populate(AnalysisRun.Execute(ceiling, "memorie", Scenes.OnBuiltForPaper()));
        Assert.False(tab.IsProblem);

        tab.Compute(400, 300);

        Assert.True(tab.IsProblem);
        Assert.False(tab.HasGallery);
        Assert.Empty(tab.Items);
        Assert.Contains($"tinta {Thresholds.CurvePeakTarget:0.00}", tab.Message);
        Assert.Contains("clasele tabului Tonuri", tab.AnchorSource);

        // A gallery clears the mark; so does opening another image.
        tab.Populate(Result());
        Assert.False(tab.IsProblem);
        Assert.Null(tab.Message);
        tab.Compute(400, 300);
        Assert.True(tab.HasGallery, tab.Message);
        Assert.False(tab.IsProblem);
    }

    [Fact]
    public void R146_ChangingThePurposeKeepsTheSameImages()
    {
        var tab = new CurvesTabViewModel();
        tab.Populate(Result());
        tab.Compute(800, 500);
        var images = tab.Items.Select(i => i.Image).ToArray();

        tab.IsScreen = true;
        Assert.Equal(images.Length, tab.Items.Count);
        for (int i = 0; i < images.Length; i++) Assert.Same(images[i], tab.Items[i].Image);
        tab.IsPrint = true;
        for (int i = 0; i < images.Length; i++) Assert.Same(images[i], tab.Items[i].Image);
    }

    [Fact]
    public void R147_TheCurveWindowSaysWhatItsColumnsMeanForTheImageAtHand()
    {
        CurveWindowViewModel Open(AnalysisResult result)
        {
            var tab = new CurvesTabViewModel();
            tab.Populate(result);
            tab.Compute(400, 300);
            Assert.True(tab.HasGallery, tab.Message);
            tab.Next.Execute(null);
            CurveWindowViewModel? opened = null;
            tab.CurveRequested += w => opened = w;
            tab.OpenCurve.Execute(null);
            return opened!;
        }

        CurveWindowViewModel classes = Open(Result());
        Assert.Contains("aceeasi curba pe R, G si B", classes.Caption);
        Assert.Contains("L* e al dalelor clasei", classes.PointsNote);
        Assert.Contains("clasele benzii", classes.PointsNote);
        Assert.Contains(classes.Rows, r => r.Label == "banda");

        // A mono frame without a WCS: no R, G and B to speak of, and the sky-relative classes behind the points.
        Raster mono = SyntheticRasters.From(512, 512, 1, (x, y, p) => x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.7f : x < 32 ? 0.05f : x < 416 ? 0.15f : x < 480 ? 0.3f : 0.5f);
        CurveWindowViewModel frame = Open(AnalysisRun.Execute(mono, "memorie", Scenes.OnBuiltForPaper()));
        Assert.DoesNotContain("R, G si B", frame.Caption);
        Assert.DoesNotContain("R, G si B", frame.PointsNote);
        Assert.Contains("clase relative la cerul modelat", frame.PointsNote);
        Assert.Contains(frame.Rows, r => r.Label == "nebuloasa stralucitoare");
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
        Assert.Equal(CurveAdvisor.ShadowChartLimit(strong.Curve), w.ShadowLimit, 9);
        Assert.InRange(w.ShadowLimit, strong.Curve.Points[3].X, strong.Curve.Points[4].X);   // past the band, short of the clouds
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
            view.UpdateLayout();
            Assert.True(view.ActualWidth > 0);

            // The image keeps its size and its place from the original to every suggestion, and
            // when a save message appears: the gallery area never changes.
            var gallery = (FrameworkElement)view.FindName("GalleryArea");
            Rect Place() { view.UpdateLayout(); return gallery.TransformToAncestor(view).TransformBounds(new Rect(gallery.RenderSize)); }
            Rect original = Place();
            Assert.True(original.Height > 100);
            Assert.Empty(tab.Current.Evidence);
            while (tab.Next.CanExecute(null))
            {
                tab.Next.Execute(null);
                Assert.NotEmpty(tab.Current.Evidence);
                Assert.Equal(original, Place());
                Assert.Single(tab.Items, i => i.IsCurrent);
            }
            tab.Save(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nu-exista-" + Guid.NewGuid().ToString("N"), "x.fit"));
            Assert.NotNull(tab.SaveError);
            Assert.Equal(original, Place());
            while (tab.Previous.CanExecute(null)) tab.Previous.Execute(null);
            Assert.Equal(original, Place());
            tab.Next.Execute(null);

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
