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
/// R-146, R-147: the Editare Curbe tab's gallery and its curve window. The tab computes nothing: every
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

        tab.Compute(800, 500);   // the computation, with the size of the gallery area

        CurveSet core = CurveRun.Execute(result, CurvePurpose.Print, 800, 500);
        Assert.True(tab.HasGallery);
        Assert.Equal(new[] { "Originalul" }.Concat(core.Suggestions.Select(s => s.Name)).ToArray(), tab.Items.Select(i => i.Name).ToArray());
        Assert.Equal("Originalul", tab.Current.Name);
        Assert.Equal($"1 / {tab.Items.Count}", tab.Position);
        // The preview covers the fitted size (the area's here, the aspect being kept), or is the
        // whole image when that is smaller (ADR 0084).
        Assert.True(tab.Current.Image.PixelWidth >= Math.Min(800, result.Raster.W), tab.Current.Image.PixelWidth.ToString());
        Assert.True(tab.Current.Image.PixelHeight >= Math.Min(500, result.Raster.H), tab.Current.Image.PixelHeight.ToString());
        Assert.Contains("felurile de dale", tab.AnchorSource);

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
        tab.DustOnPaperBlack = false;   // the plain gallery: the box of R-185 off
        tab.Compute(800, 500);
        tab.Next.Execute(null);
        string[] names = tab.Items.Select(i => i.Name).ToArray();
        string printEvidence = string.Join("\n", tab.Current.Evidence);
        Assert.Contains("Din profil ICC...", printEvidence);

        // The paper checks, and only they, are the warnings the view paints red.
        CurveSuggestion core = CurveRun.Execute(result, CurvePurpose.Print, 800, 500).Suggestions[0];
        Assert.NotEmpty(core.Evidence.PaperChecks);
        Assert.Equal(tab.Current.Evidence, tab.Current.EvidenceRows.Select(r => r.Text).ToArray());
        Assert.Equal(core.Evidence.PaperChecks.Select(c => $"! {c.Text} (fiindca: {Fmt.Plain(c.Because)})").ToArray(),
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
    public async Task R146_TheComputationRunsInTheBackgroundAndALateResultNeverReachesAnotherImage()
    {
        AnalysisResult first = Result(), second = Result();
        var tab = new CurvesTabViewModel();
        tab.Populate(first);
        tab.DustOnPaperBlack = false;   // the plain gallery: the box of R-185 off, so one computation serves both purposes

        // The background path: busy while it runs, then the gallery for the purpose chosen at
        // the end, not at the start.
        Task run = tab.ComputeAsync();
        Assert.True(tab.IsBusy);
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
        await late;
        Assert.False(tab.HasGallery);
        Assert.Empty(tab.Items);
        Assert.Null(tab.Message);
        Assert.False(tab.IsBusy);
    }

    [Fact]
    public async Task R146_ALargerAreaRecomputesTheGalleryByItselfAndTheImageCoversIt()
    {
        AnalysisResult result = Result();
        int w = result.Raster.W, h = result.Raster.H;
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.SetGallerySize(w, h);      // before any computation: nothing to redo
        Assert.False(tab.IsBusy);

        // A small area of the image's own aspect: the preview is at least the area, never under it (ADR 0084).
        tab.Compute(w / 5, h / 5);
        Assert.True(tab.HasGallery, tab.Message);
        Assert.True(tab.Current!.Width >= w / 5 && tab.Current.Height >= h / 5, $"{tab.Current.Width}x{tab.Current.Height} under {w / 5}x{h / 5}");
        int before = tab.Current.Width;

        // The same area again, or a smaller one: no computation starts.
        tab.SetGallerySize(w / 5, h / 5);
        tab.SetGallerySize(w / 8, h / 8);
        Assert.False(tab.IsBusy);
        Assert.Equal(before, tab.Current.Width);

        // A larger area, which allows a finer preview: computed again by itself, the gallery
        // staying until the new one is there, then at least the area's size.
        tab.SetGallerySize(w, h);
        Assert.True(tab.IsBusy);
        Assert.True(tab.HasGallery);
        await tab.Computation;
        Assert.False(tab.IsBusy);
        Assert.Equal(w, tab.Current!.Width);
        Assert.Equal(h, tab.Current.Height);
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
        Assert.Contains($"la sau peste {Thresholds.CurvePeakTarget:0.00}, unde ajung varfurile", tab.Message);
        Assert.Contains("felurile de dale ale tabului Tonuri", tab.AnchorSource);

        // A larger area does not run the computation again: there is no gallery to refine and
        // the reason does not depend on the size (ADR 0084). The problem is computed at a size
        // whose step is 5 on the 512 px image, and the area then allows a step of 1: without the
        // rule a computation would start here (IsBusy is set before its first await).
        tab.Compute(100, 100);
        Assert.True(tab.IsProblem);
        tab.SetGallerySize(512, 512);
        Assert.False(tab.IsBusy);
        Assert.True(tab.IsProblem);

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
        tab.DustOnPaperBlack = false;   // the plain gallery: the box of R-185 off
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
        Assert.Contains("aceeasi pe R, G si B", classes.Caption);
        Assert.Contains("L* e luminozitatea dalelor de fiecare fel", classes.PointsNote);
        Assert.Contains("felurile de dale ale benzii Caii Lactee", classes.PointsNote);
        Assert.Contains(classes.Rows, r => r.Label == "banda");

        // A mono frame without a WCS: no R, G and B to speak of, and the sky-relative classes behind the points.
        Raster mono = SyntheticRasters.From(512, 512, 1, (x, y, p) => x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.7f : x < 32 ? 0.05f : x < 416 ? 0.15f : x < 480 ? 0.3f : 0.5f);
        CurveWindowViewModel frame = Open(AnalysisRun.Execute(mono, "memorie", Scenes.OnBuiltForPaper()));
        Assert.DoesNotContain("R, G si B", frame.Caption);
        Assert.DoesNotContain("R, G si B", frame.PointsNote);
        Assert.Contains("dalele impartite prin comparatie cu cerul", frame.PointsNote);
        Assert.Contains(frame.Rows, r => r.Label == "nebuloasa stralucitoare");
    }

    [Fact]
    public void R147_TheCurveWindowListsThePointsWithTheirLabelsAndSamplesBothCharts()
    {
        AnalysisResult result = Result();
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.DustOnPaperBlack = false;   // the plain curve's table: the box of R-185 off
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

    [Fact]
    public void R147_TheChartsLabelsThatWouldOverlapStartLowerSoThatEachCanBeRead()
    {
        // The whole-curve chart of a curve with the dust move: four labels within a few pixels of
        // each other (under the dust, dust, dark sky, sky), then three well apart. Each is a
        // text 15 px wide on screen (rotated), running down from its start for its length.
        var labels = new (double X, double Length)[] { (67, 76), (75, 24), (92, 46), (102, 20), (148, 28), (226, 70), (289, 34) };
        IReadOnlyList<double> starts = CurveCanvas.LabelStarts(labels, 15, 400);

        Assert.Equal(labels.Length, starts.Count);
        Assert.Equal(0, starts[0]);                      // the first one at the top
        Assert.True(starts[1] >= 76);                    // 8 px from it: under it
        Assert.Equal(0, starts[2]);                      // 25 px from the first and 17 from the second: free at the top
        Assert.True(starts[3] >= 46);                    // 10 px from "dark sky": under it
        Assert.Equal(new double[] { 0, 0, 0 }, starts.Skip(4).ToArray());   // far apart: all at the top

        // No two labels closer than a text's width share any of their run.
        for (int i = 0; i < labels.Length; i++)
            for (int j = 0; j < i; j++)
                if (Math.Abs(labels[i].X - labels[j].X) < 15 + 2)
                    Assert.True(starts[i] >= starts[j] + labels[j].Length || starts[j] >= starts[i] + labels[i].Length, $"{i} over {j}");

        // A label that would run out of the chart goes back to the top rather than be cut away.
        IReadOnlyList<double> tight = CurveCanvas.LabelStarts(new (double X, double Length)[] { (10, 80), (14, 80) }, 15, 100);
        Assert.Equal(new double[] { 0, 0 }, tight.ToArray());
    }

    [Fact]
    public void R146_TheSentenceOfNoCurveAndTheBoxsReasonWrapInsideTheTab()
        => _sta.Invoke(() =>
        {
            Rect Place(CurvesView view, string name)
            {
                var block = (FrameworkElement)view.FindName(name);
                return block.TransformToAncestor(view).TransformBounds(new Rect(block.RenderSize));
            }
            CurvesView Laid(CurvesTabViewModel tab, double width)
            {
                var view = new CurvesView { DataContext = tab };
                view.Measure(new Size(width, 600));
                view.Arrange(new Rect(0, 0, width, 600));
                view.UpdateLayout();
                return view;
            }

            // An image that gets no curve: the red sentence, long, at the tab's width in the
            // window's default size. It ends inside the tab, on as many lines as it needs.
            Raster ceiling = SyntheticRasters.From(512, 512, 3, (x, y, p) => x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.95f : x < 32 ? 0.05f : x < 416 ? 0.15f : x < 480 ? 0.3f : 0.5f);
            var none = new CurvesTabViewModel();
            none.Populate(AnalysisRun.Execute(ceiling, "memorie", Scenes.OnBuiltForPaper()));
            none.Compute(400, 300);
            Assert.True(none.IsProblem, none.Message);
            CurvesView noneView = Laid(none, 1084);
            Rect sentence = Place(noneView, "TabMessage");
            Assert.True(sentence.Width > 100);
            Assert.True(sentence.Right <= 1084, $"the sentence ends at {sentence.Right:0}, past the tab's 1084");
            Assert.True(sentence.Height > 20, "one line only: the sentence did not wrap");

            // The box's reason, long, in a narrow window: inside the tab too.
            var refused = new CurvesTabViewModel();
            refused.Populate(DustResult(dmax: 1.2));
            refused.Compute(400, 300);
            Assert.Contains("cerul insusi e prea aproape", refused.DustBoxReason);
            CurvesView refusedView = Laid(refused, 900);
            Rect reason = Place(refusedView, "DustBoxReason");
            Assert.True(reason.Width > 100);
            Assert.True(reason.Right <= 900, $"the reason ends at {reason.Right:0}, past the tab's 900");
        });

    [Fact]
    public async Task R146_ASaveConfirmedAfterAnotherGalleryLandedWritesTheCurveTheDialogWasOpenedFor()
    {
        using var ws = new TempWorkspace();
        AnalysisResult result = DustResult();
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.Compute(400, 300);
        tab.Next.Execute(null);
        Assert.Equal("Blanda", tab.Current.Name);
        CurveSuggestion moved = CurveRun.Execute(result, CurvePurpose.Print, 400, 300, dustOnPaperBlack: true).Suggestions[0];
        float dust = result.Raster.At(0, 10, 10), movedDust = moved.Curve.MapSample(dust);
        Assert.True(movedDust < dust - 0.02f);

        // A recompute for a larger area lands while the save dialog is open: the position is
        // kept, and the curve asked for is written (it used to go back to the original, which
        // has no curve).
        string? proposed = null;
        tab.SaveRequested += name => proposed = name;
        tab.SaveAs.Execute(null);
        tab.SetGallerySize(result.Raster.W * 4, result.Raster.H * 4);
        await tab.Computation;
        Assert.Equal("Blanda", tab.Current.Name);
        string first = ws.PathTo("dupa-recalculare.fit");
        tab.Save(first);
        Assert.Null(tab.SaveError);
        Assert.Equal(movedDust, ImageLoader.Load(first).At(0, 10, 10), 4);

        // The box unticked while the dialog is open: the plain gallery lands under it, and the
        // file still holds the curve that was on screen when Save as... was pressed.
        tab.SaveAs.Execute(null);
        tab.DustOnPaperBlack = false;
        await tab.Computation;
        Assert.DoesNotContain(tab.Current.Evidence, l => l.StartsWith("praful pe negrul hartiei"));
        string second = ws.PathTo("dupa-debifare.fit");
        tab.Save(second);
        Assert.Null(tab.SaveError);
        Assert.Equal(movedDust, ImageLoader.Load(second).At(0, 10, 10), 4);

        // A save with no dialog before it (the tests' way) writes the curve shown.
        string third = ws.PathTo("fara-dialog.fit");
        tab.Save(third);
        Assert.Equal(dust, ImageLoader.Load(third).At(0, 10, 10), 4);
    }

    // ---- R-185: the box "Put dust on paper black" ----

    /// <summary>The stripe scene with a dust zone at L* 7.7, on a paper whose black is L* 3.6 unless another Dmax is given.</summary>
    static AnalysisResult DustResult(double dmax = 2.4)
        => AnalysisRun.Execute(
            SyntheticRasters.From(512, 512, 3, (x, y, p) => x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.7f : x < 32 ? 0.09f : x < 416 ? 0.15f : x < 480 ? 0.3f : 0.5f),
            "memorie", new Parameters { DmaxOverride = dmax });

    [Fact]
    public void R185_TheBoxIsTickedWhereTheDustCanBeMovedAndTheGalleryHoldsTheMovedCurves()
    {
        AnalysisResult result = DustResult();
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.Compute(400, 300);

        Assert.True(tab.HasGallery, tab.Message);
        Assert.True(tab.DustBoxEnabled);
        Assert.True(tab.DustOnPaperBlack);
        Assert.Null(tab.DustBoxReason);

        CurveSet core = CurveRun.Execute(result, CurvePurpose.Print, 400, 300, dustOnPaperBlack: true);
        Assert.True(core.DustOnPaperBlack);
        Assert.Equal(core.Footnote, tab.Footnote);
        Assert.Contains("Praful e pus pe negrul hartiei", tab.Footnote);
        tab.Next.Execute(null);
        Assert.Equal(CurveTexts.EvidenceLines(core.Suggestions[0]), tab.Current.Evidence);
        Assert.Contains(tab.Current.Evidence, l => l.StartsWith("praful pe negrul hartiei: de la L* 7.7 la "));
        Assert.Contains(tab.Current.Evidence, l => l.StartsWith("zgomotul din praf creste cu "));

        // The curve window lists the move's rows by their names, without an L* of their own.
        CurveWindowViewModel? opened = null;
        tab.CurveRequested += w => opened = w;
        tab.OpenCurve.Execute(null);
        CurveWindowViewModel window = opened!;
        Assert.Equal(core.Suggestions[0].Curve.Points.Select(p => p.Label ?? "").ToArray(), window.Rows.Select(r => r.Label).ToArray());
        CurvePointRow dust = Assert.Single(window.Rows, r => r.Label == "praf");
        Assert.Equal("7.7", dust.LstarBefore);
        Assert.Equal(Fmt.F1(core.Suggestions[0].Evidence.LstarAfter[0]), dust.LstarAfter);
        Assert.Equal(Fmt.F1(core.Suggestions[0].Evidence.LstarAfter[1]), Assert.Single(window.Rows, r => r.Label == "cer").LstarAfter);
        foreach (CurvePointRow extra in window.Rows.Where(r => r.Label is "cer inchis" or "sub praf"))
        {
            Assert.Equal("", extra.LstarBefore);
            Assert.Equal("", extra.LstarAfter);
        }
        Assert.Equal(5, window.Rows.Count(r => r.LstarAfter != ""));

        // This frame's sky is one level, so the curve has no row for the sky's dark end, and the note says nothing of one.
        Assert.DoesNotContain(window.Rows, r => r.Label == "cer inchis");
        Assert.DoesNotContain("cer inchis", window.PointsNote);
        Assert.Equal(window.Rows.Any(r => r.Label == "sub praf"), window.PointsNote.Contains("sub praf"));
    }

    [Fact]
    public async Task R185_UntickedOrOnTheScreenTheGalleryIsThePlainOneAndEachIsComputedOnce()
    {
        AnalysisResult result = DustResult();
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.Compute(400, 300);
        tab.Next.Execute(null);
        var movedImages = tab.Items.Select(i => i.Image).ToArray();
        string[] movedEvidence = tab.Current.Evidence.ToArray();
        CurveSet plain = CurveRun.Execute(result, CurvePurpose.Print, 400, 300);

        // Unticked: the plain gallery, computed now, the position kept.
        tab.DustOnPaperBlack = false;
        Assert.True(tab.IsBusy);
        await tab.Computation;
        Assert.False(tab.IsBusy);
        Assert.False(tab.DustOnPaperBlack);
        Assert.True(tab.DustBoxEnabled);
        Assert.Null(tab.DustBoxReason);
        Assert.Equal("Blanda", tab.Current.Name);
        Assert.Equal(CurveTexts.EvidenceLines(plain.Suggestions[0]), tab.Current.Evidence);
        Assert.Contains("Curbele nu muta praful si cerul", tab.Footnote);
        var plainImages = tab.Items.Select(i => i.Image).ToArray();
        Assert.NotSame(movedImages[1], plainImages[1]);

        // Ticked again, unticked again: nothing is computed, the same images come back.
        tab.DustOnPaperBlack = true;
        Assert.False(tab.IsBusy);
        Assert.True(tab.DustOnPaperBlack);
        Assert.Equal(movedEvidence, tab.Current.Evidence.ToArray());
        for (int i = 0; i < movedImages.Length; i++) Assert.Same(movedImages[i], tab.Items[i].Image);
        tab.DustOnPaperBlack = false;
        Assert.False(tab.IsBusy);
        for (int i = 0; i < plainImages.Length; i++) Assert.Same(plainImages[i], tab.Items[i].Image);

        // The screen: the box is off and says why, whatever the choice, and the gallery is the plain one.
        tab.DustOnPaperBlack = true;
        tab.IsScreen = true;
        Assert.False(tab.IsBusy);
        Assert.False(tab.DustOnPaperBlack);
        Assert.False(tab.DustBoxEnabled);
        Assert.Equal("doar pentru print: ecranul nu are negrul hartiei", tab.DustBoxReason);
        for (int i = 0; i < plainImages.Length; i++) Assert.Same(plainImages[i], tab.Items[i].Image);
        Assert.DoesNotContain(tab.Current.Evidence, l => l.StartsWith("praful pe negrul hartiei"));

        // Back to print: the choice is still on, and the moved gallery is still there.
        tab.IsPrint = true;
        Assert.False(tab.IsBusy);
        Assert.True(tab.DustOnPaperBlack);
        for (int i = 0; i < movedImages.Length; i++) Assert.Same(movedImages[i], tab.Items[i].Image);
    }

    [Fact]
    public async Task R185_TheFirstChangeToTheScreenComputesThePlainGalleryAndNothingBreaksMeanwhile()
    {
        var tab = new CurvesTabViewModel();
        tab.Populate(DustResult());
        tab.Compute(400, 300);
        tab.Next.Execute(null);

        // Only the gallery with the move exists: the screen's is computed now, and until it is
        // there every property can still be read.
        tab.IsScreen = true;
        Assert.True(tab.IsBusy);
        Assert.NotNull(tab.Footnote);
        Assert.False(tab.DustOnPaperBlack);
        Assert.False(tab.DustBoxEnabled);
        Assert.Equal("doar pentru print: ecranul nu are negrul hartiei", tab.DustBoxReason);

        await tab.Computation;
        Assert.False(tab.IsBusy);
        Assert.Equal("Blanda", tab.Current.Name);
        Assert.DoesNotContain(tab.Current.Evidence, l => l.StartsWith("praful pe negrul hartiei"));
        Assert.DoesNotContain(tab.Current.EvidenceRows, r => r.IsWarning);
        Assert.Contains("Curbele nu muta praful si cerul", tab.Footnote);
    }

    [Fact]
    public async Task R185_AnAreaOfAnotherSizeThatAllowsNoFinerPreviewRecomputesNothing()
    {
        // The gallery area changes its height when the evidence appears and between the two
        // galleries, whose evidence is not as long: a kept gallery stays good while the area
        // allows no finer preview than it was made with.
        AnalysisResult result = DustResult();
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.Compute(400, 300);
        Assert.Equal(CurveRun.PreviewStride(result.Raster, 400, 300), CurveRun.PreviewStride(result.Raster, 400, 260));
        tab.SetGallerySize(400, 260);
        Assert.False(tab.IsBusy);

        tab.DustOnPaperBlack = false;   // the plain gallery, made at the area's size now
        await tab.Computation;
        tab.SetGallerySize(400, 280);
        Assert.False(tab.IsBusy);
        var plain = tab.Items.Select(i => i.Image).ToArray();

        tab.DustOnPaperBlack = true;    // made at 400 x 300, the same step: nothing to compute
        Assert.False(tab.IsBusy);
        Assert.True(tab.DustOnPaperBlack);
        tab.IsScreen = true;
        Assert.False(tab.IsBusy);
        for (int i = 0; i < plain.Length; i++) Assert.Same(plain[i], tab.Items[i].Image);
        tab.IsPrint = true;
        Assert.False(tab.IsBusy);
    }

    [Fact]
    public async Task R185_AKeptGalleryMadeWithACoarserStepIsShownAndComputedAgainWhenItIsNextAskedFor()
    {
        AnalysisResult result = DustResult();
        int w = result.Raster.W, h = result.Raster.H;
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.Compute(w / 5, h / 5);              // the gallery with the move, small
        tab.DustOnPaperBlack = false;
        await tab.Computation;                  // the plain one, as small
        var smallMoved = tab.Items.Select(i => i.Image).ToArray();
        Assert.True(smallMoved[0].PixelWidth < w);

        // A larger area: the gallery shown, the plain one, is computed again by itself.
        tab.SetGallerySize(w, h);
        Assert.True(tab.IsBusy);
        await tab.Computation;
        Assert.Equal(w, tab.Current!.Width);

        // The other was kept at its coarser step: asked for, it is shown at once and computed again.
        tab.DustOnPaperBlack = true;
        Assert.True(tab.IsBusy);
        Assert.True(tab.DustOnPaperBlack);
        Assert.True(tab.Current!.Width < w);
        Assert.Contains("Praful e pus pe negrul hartiei", tab.Footnote);
        await tab.Computation;
        Assert.False(tab.IsBusy);
        Assert.Equal(w, tab.Current!.Width);

        // The area back to the small size: both kept galleries are finer than it needs, nothing is computed.
        tab.SetGallerySize(w / 5, h / 5);
        tab.DustOnPaperBlack = false;
        Assert.False(tab.IsBusy);
        tab.DustOnPaperBlack = true;
        Assert.False(tab.IsBusy);
    }

    [Fact]
    public async Task R185_AKeptGalleryIsRefinedWhenTheAreaGrowsAgainAfterTheOtherOneWasRefined()
    {
        AnalysisResult result = DustResult();
        int w = result.Raster.W, h = result.Raster.H;
        var tab = new CurvesTabViewModel();
        tab.Populate(result);
        tab.Compute(w / 5, h / 5);              // small: the gallery with the move
        tab.DustOnPaperBlack = false;
        await tab.Computation;                  // small: the plain one
        tab.SetGallerySize(w, h);               // large: the plain one again, finer
        await tab.Computation;
        Assert.Equal(w, tab.Current!.Width);
        tab.SetGallerySize(w / 5, h / 5);       // small again: nothing to do
        Assert.False(tab.IsBusy);

        // The box ticked in the small window: the kept gallery with the move is as fine as the
        // area needs. Then the window grows: it is the one shown, and it is computed again.
        tab.DustOnPaperBlack = true;
        Assert.False(tab.IsBusy);
        Assert.True(tab.Current!.Width < w);
        tab.SetGallerySize(w, h);
        Assert.True(tab.IsBusy);
        await tab.Computation;
        Assert.False(tab.IsBusy);
        Assert.True(tab.DustOnPaperBlack);
        Assert.Equal(w, tab.Current!.Width);
    }

    [Fact]
    public async Task R185_ThePurposeChangedDuringTheFirstComputationEndsOnThePlainGallery()
    {
        // The window's own start: the box ticked, the first computation asks for the move, and
        // the screen is chosen before it lands. What lands is not what is wanted: the plain
        // gallery is computed after it, and the tab ends on it.
        var tab = new CurvesTabViewModel();
        tab.Populate(DustResult());
        Task first = tab.ComputeAsync();
        Assert.True(tab.IsBusy);
        tab.IsScreen = true;
        await first;
        while (tab.IsBusy) await tab.Computation;

        Assert.True(tab.HasGallery, tab.Message);
        Assert.Null(tab.Message);
        Assert.False(tab.DustOnPaperBlack);
        Assert.Equal("doar pentru print: ecranul nu are negrul hartiei", tab.DustBoxReason);
        tab.Next.Execute(null);
        Assert.DoesNotContain(tab.Current.Evidence, l => l.StartsWith("praful pe negrul hartiei"));
        Assert.DoesNotContain(tab.Current.EvidenceRows, r => r.IsWarning);
        Assert.Contains("Curbele nu muta praful si cerul", tab.Footnote);

        // Back to print: the gallery with the move was kept from the first computation.
        tab.IsPrint = true;
        Assert.False(tab.IsBusy);
        Assert.True(tab.DustOnPaperBlack);
        Assert.Contains(tab.Current.Evidence, l => l.StartsWith("praful pe negrul hartiei"));
    }

    [Fact]
    public void R147_TheRowsADustMoveAddsHaveNoLightnessAndTheNoteExplainsEachOnlyWhereItIs()
    {
        CurveWindowViewModel Open(double dmax)
        {
            Raster spread = SyntheticRasters.From(512, 512, 3, (x, y, p) =>
                x >= 448 && x < 480 && y >= 224 && y < 256 ? 0.7f : x < 32 ? 0.09f : x < 416 ? 0.14f + 0.02f * y / 511 : x < 480 ? 0.3f : 0.5f);
            var tab = new CurvesTabViewModel();
            tab.Populate(AnalysisRun.Execute(spread, "memorie", new Parameters { DmaxOverride = dmax }));
            tab.Compute(400, 300);
            Assert.True(tab.DustOnPaperBlack, tab.DustBoxReason);
            tab.Next.Execute(null);
            CurveWindowViewModel? opened = null;
            tab.CurveRequested += w => opened = w;
            tab.OpenCurve.Execute(null);
            return opened!;
        }

        // The sky's dark end held, no helper: one extra row, and the note names it alone.
        CurveWindowViewModel held = Open(2.4);
        Assert.Equal(new[] { "", "praf", "cer inchis", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri", "" }, held.Rows.Select(r => r.Label).ToArray());
        Assert.Equal("", held.Rows[2].LstarBefore);
        Assert.Equal("", held.Rows[2].LstarAfter);
        Assert.Equal(new[] { 1, 3, 4, 5, 6 }, Enumerable.Range(0, held.Rows.Count).Where(i => held.Rows[i].LstarAfter != "").ToArray());
        Assert.Contains("\"cer inchis\" e partea inchisa a cerului", held.PointsNote);
        Assert.DoesNotContain("sub praf", held.PointsNote);
        Assert.Equal(7 - 1, held.Markers.Count);

        // A blacker paper: the helper under the dust too, and both rows explained.
        CurveWindowViewModel both = Open(2.6);
        Assert.Equal(new[] { "", "sub praf", "praf", "cer inchis", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri", "" }, both.Rows.Select(r => r.Label).ToArray());
        Assert.Equal(new[] { 2, 4, 5, 6, 7 }, Enumerable.Range(0, both.Rows.Count).Where(i => both.Rows[i].LstarAfter != "").ToArray());
        Assert.Contains("\"cer inchis\" e partea inchisa a cerului", both.PointsNote);
        Assert.Contains("\"sub praf\" tine curba lina spre negru", both.PointsNote);
        // The shadow chart still ends past the band's point, not past one of the added ones.
        Assert.InRange(both.ShadowLimit, double.Parse(both.Rows[5].Input, System.Globalization.CultureInfo.InvariantCulture), double.Parse(both.Rows[6].Input, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void R185_WhereTheMoveCannotBeHadTheBoxIsUntickedAndDisabledWithTheReasonAndTheChoiceIsKept()
    {
        // A paper whose black is above the sky: the move is refused, the box is off, the plain curves are shown.
        AnalysisResult weak = DustResult(dmax: 1.2);
        var tab = new CurvesTabViewModel();
        tab.Populate(weak);
        Assert.False(tab.DustBoxEnabled);        // nothing computed yet
        Assert.False(tab.DustOnPaperBlack);
        tab.Compute(400, 300);

        Assert.True(tab.HasGallery, tab.Message);
        Assert.False(tab.DustBoxEnabled);
        Assert.False(tab.DustOnPaperBlack);
        Assert.Contains("nu pe imaginea asta: cerul insusi e prea aproape de negrul hartiei", tab.DustBoxReason);
        CurveSet plain = CurveRun.Execute(weak, CurvePurpose.Print, 400, 300);
        tab.Next.Execute(null);
        Assert.Equal(CurveTexts.EvidenceLines(plain.Suggestions[0]), tab.Current.Evidence);

        // Another image, where the move can be had: the choice was never turned off, so the box is ticked again.
        tab.Populate(DustResult());
        tab.Compute(400, 300);
        Assert.True(tab.DustBoxEnabled);
        Assert.True(tab.DustOnPaperBlack);
        Assert.Null(tab.DustBoxReason);

        // A choice turned off stays off on the next image.
        tab.DustOnPaperBlack = false;
        tab.Populate(DustResult());
        tab.Compute(400, 300);
        Assert.True(tab.DustBoxEnabled);
        Assert.False(tab.DustOnPaperBlack);
        tab.Next.Execute(null);
        Assert.DoesNotContain(tab.Current.Evidence, l => l.StartsWith("praful pe negrul hartiei"));
    }

    [Fact]
    public void R185_TheBoxAndItsReasonRenderAndFollowTheViewModel()
        => _sta.Invoke(() =>
        {
            var tab = new CurvesTabViewModel();
            tab.Populate(DustResult());
            var view = new CurvesView { DataContext = tab };
            view.Measure(new Size(1400, 860));
            view.Arrange(new Rect(0, 0, 1400, 860));
            tab.Compute(800, 500);
            view.UpdateLayout();

            var box = (System.Windows.Controls.CheckBox)view.FindName("DustBox");
            Assert.Equal("Pune praful pe negrul hartiei", (string)box.Content);
            Assert.True(box.IsEnabled);
            Assert.True(box.IsChecked);

            Assert.Equal(1, box.Opacity);
            tab.IsScreen = true;
            view.UpdateLayout();
            Assert.False(box.IsEnabled);
            Assert.False(box.IsChecked);
            // Disabled, it is dimmed as the theme's disabled buttons are: a box that only loses
            // the colour of its little square reads as one that can be ticked.
            Assert.Equal(0.4, box.Opacity, 3);
            var reason = (System.Windows.Controls.TextBlock)view.FindName("DustBoxReason");
            Assert.Equal("doar pentru print: ecranul nu are negrul hartiei", reason.Text);

            tab.IsPrint = true;
            view.UpdateLayout();
            Assert.True(box.IsEnabled);
            Assert.True(box.IsChecked);
            var note = (System.Windows.Controls.TextBlock)view.FindName("Footnote");
            Assert.Contains("Praful e pus pe negrul hartiei", note.Text);
        });
}
