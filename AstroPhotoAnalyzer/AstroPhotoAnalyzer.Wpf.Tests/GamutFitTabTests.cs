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

// GamutFitTabTests - the Culori pentru hartie tab (R-178, ADR 0083, ADR 0090): the gallery of
// the three variants beside the original, the red layers, the save, the sentence without a
// profile.

using AstroPhotoAnalyzer.Tests;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("fereastra")]
public class GamutFitTabTests
{
    readonly StaFixture _sta;

    public GamutFitTabTests(StaFixture sta) => _sta = sta;

    /// <summary>
    /// A colour frame with a saturated patch: on the hand-built profile, which prints every
    /// colour as the grey of its L*, every chroma over 4 is outside.
    /// </summary>
    static Raster Frame()
    {
        var r = new Raster(192, 256, 3);
        for (int y = 0; y < r.H; y++)
            for (int x = 0; x < r.W; x++)
            {
                int i = y * r.W + x;
                bool patch = x >= 64 && x < 192 && y >= 48 && y < 144;
                r.Planes[0][i] = patch ? 0.55f : 0.25f;
                r.Planes[1][i] = patch ? 0.25f : 0.25f;
                r.Planes[2][i] = patch ? 0.15f : 0.26f;
            }
        return r;
    }

    static AnalysisResult Result(TempWorkspace ws, bool withProfile, string? inputName = null)
    {
        Parameters p = Scenes.OnBuiltForPaper();
        if (withProfile)
        {
            string icc = ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20));
            p = p.WithDmaxFromProfile(icc, out string error) ?? throw new Exception(error);
        }
        if (inputName == null) return AnalysisRun.Execute(Frame(), "memorie", p);
        Raster f = Frame();
        string input = ws.WriteFile(inputName, TiffTestData.Build16Rgb(f.W, f.H, (x, y, c) => (int)Math.Round(65535 * f.Planes[c][y * f.W + x]), IccTestData.V2("sRGB test")));
        return AnalysisRun.Execute(input, p);
    }

    [Fact]
    public void R178_TheGalleryHoldsTheThreeVariantsBesideTheOriginalAndTheRedLayersFollowTheBox()
    {
        using var ws = new TempWorkspace();
        var tab = new GamutFitTabViewModel();
        Assert.False(tab.HasGallery);
        Assert.Null(tab.Before);
        Assert.True(tab.ShowOutside);   // on by default (R-178)
        tab.Populate(Result(ws, withProfile: true));
        Assert.False(tab.HasGallery);   // filled by the computation MainViewModel starts, not by Populate

        tab.Compute();
        Assert.True(tab.HasGallery, tab.Message);
        // The original is no position of the gallery: it is the other side of the comparison.
        Assert.Equal(new[] { "Exacta", "Lina", "Foarte lina" }, tab.Items.Select(i => i.Name).ToArray());
        Assert.Equal("1 / 3", tab.Position);
        Assert.Equal("Exacta", tab.Current!.Name);
        Assert.True(tab.SaveAs.CanExecute(null));
        Assert.False(tab.Previous.CanExecute(null));

        GamutFitGalleryItem before = tab.Before!;
        Assert.NotNull(before);
        Assert.Equal(tab.Current.Width, before.Width);
        Assert.Equal(tab.Current.Height, before.Height);
        Assert.Equal(tab.Current.Image.PixelWidth, before.Image.PixelWidth);
        Assert.NotSame(tab.Current.Image, before.Image);
        Assert.NotNull(before.Overlay);   // the box is on: the original's warning of R-161 over the patch
        Assert.Equal(before.Image.PixelWidth, before.Overlay!.PixelWidth);
        Assert.Empty(before.EvidenceRows);   // its share is in every variant's evidence
        Assert.Null(tab.Current.Overlay);    // nothing is left outside after the fit
        Assert.Contains(tab.Current.EvidenceRows, r => r.Text.Contains("inainte") && r.Text.Contains("%"));
        Assert.Contains(tab.Current.EvidenceRows, r => r.Text.Contains("dupa"));
        Assert.Contains(tab.Current.EvidenceRows, r => r.Text.Contains("tonuri inchise"));
        Assert.Contains(tab.Current.EvidenceRows, r => r.Text.StartsWith("de mana"));
        Assert.DoesNotContain(tab.Current.EvidenceRows, r => r.Text.Contains("median") || r.Text.Contains("percentil") || r.Text.Contains("croma"));

        // The box paints both sides, each its own layer: it reaches the original and every
        // variant (on this frame no variant has anything left to paint, so their state is read).
        Assert.All(tab.Items, i => Assert.True(i.ShowOutside));
        tab.ShowOutside = false;
        Assert.Null(before.Overlay);
        Assert.Null(tab.Current.Overlay);
        Assert.All(tab.Items, i => Assert.False(i.ShowOutside));
        tab.ShowOutside = true;
        Assert.NotNull(before.Overlay);
        Assert.All(tab.Items, i => Assert.True(i.ShowOutside));

        // The buttons change the variant only: the original stays the same image.
        tab.Next.Execute(null);
        Assert.Equal("Lina", tab.Current.Name);
        Assert.Equal("2 / 3", tab.Position);
        Assert.Same(before, tab.Before);
        Assert.True(tab.SaveAs.CanExecute(null));
        tab.Next.Execute(null);
        Assert.Equal("Foarte lina", tab.Current.Name);
        Assert.False(tab.Next.CanExecute(null));
        Assert.True(tab.SaveAs.CanExecute(null));
        tab.Previous.Execute(null);
        Assert.Equal("Lina", tab.Current.Name);
        Assert.Same(before, tab.Before);

        // The sentence under the image: which side is which, and the viewer's own maximum.
        Assert.Contains("(Inainte)", tab.ViewerHelp);
        Assert.Contains("(Dupa)", tab.ViewerHelp);
        Assert.Contains($"{ZoomPanViewer.MaxScale:0}:1", tab.ViewerHelp);

        // Another image empties the gallery; the box goes back to its default, on.
        tab.ShowOutside = false;
        tab.Populate(Result(ws, withProfile: true));
        Assert.False(tab.HasGallery);
        Assert.Null(tab.Before);
        Assert.True(tab.ShowOutside);

        // The new image starts at the first variant, wherever the last one was left (Lina),
        // and a gallery made with the box off is made with no layer shown.
        tab.ShowOutside = false;
        tab.Compute();
        Assert.Equal("Exacta", tab.Current!.Name);
        Assert.Equal("1 / 3", tab.Position);
        Assert.Null(tab.Before!.Overlay);
        Assert.All(tab.Items, i => Assert.False(i.ShowOutside));
    }

    [Fact]
    public async Task R188_TheGalleryIsTheWholeImageAndItsSentencesSaySo()
    {
        using var ws = new TempWorkspace();
        var tab = new GamutFitTabViewModel();
        AnalysisResult result = Result(ws, withProfile: true);
        int w = result.Raster.W, h = result.Raster.H;
        tab.Populate(result);
        // The computation MainViewModel starts: no area is asked, and none can be reported.
        await tab.ComputeAsync();
        Assert.False(tab.IsBusy);
        Assert.True(tab.HasGallery, tab.Message);
        Assert.False(tab.IsReducedCopy);
        foreach (GamutFitGalleryItem i in tab.Items.Append(tab.Before!))
        {
            Assert.Equal(w, i.Width);
            Assert.Equal(h, i.Height);
            Assert.Equal(w, i.Image.PixelWidth);
            Assert.Equal(h, i.Image.PixelHeight);
            Assert.True(i.Image.IsFrozen);   // made on the computation's thread, shown on the window's
        }
        Assert.Equal(w, tab.Before!.Overlay!.PixelWidth);
        Assert.True(tab.Before.Overlay.IsFrozen);

        // A whole image is not called a copy, under the gallery or where the zoom is explained.
        Assert.Equal(GamutFitTexts.Note(reduced: false), tab.Note);
        Assert.Contains("un pixel al imaginii devine", tab.ViewerHelp);
        Assert.DoesNotContain("copie", tab.Note + tab.ViewerHelp);
        // An image over WholeImageMaxPixels is a reduced copy, and both sentences say it.
        Assert.Contains("un pixel al copiei devine", GamutFitTabViewModel.ViewerHelpFor(reduced: true));
        Assert.Equal(tab.ViewerHelp, GamutFitTabViewModel.ViewerHelpFor(reduced: false));
    }

    [Fact]
    public async Task R188_TheFitStartsOnlyWhenTheComputationBeforeItHasEnded()
    {
        using var ws = new TempWorkspace();
        using LogCapture capture = LogCapture.Start();
        var tab = new GamutFitTabViewModel();
        tab.Populate(Result(ws, withProfile: true));
        // The soft proof's computation, still running: MainViewModel hands it over (ADR 0118).
        var proof = new TaskCompletionSource();
        Task fit = tab.ComputeAfterAsync(proof.Task);
        Assert.Same(fit, tab.Computation);
        Assert.True(tab.IsBusy);   // "computing" from the start, not an empty tab
        Assert.Equal(GamutFitTexts.Computing, tab.Message);
        Assert.False(tab.HasGallery);
        Assert.DoesNotContain(capture.Messages, m => m.StartsWith("gamutfit start"));

        proof.SetResult();
        await fit;
        Assert.Single(capture.Messages, m => m.StartsWith("gamutfit start"));
        Assert.True(tab.HasGallery, tab.Message);
        Assert.False(tab.IsBusy);

        // Another image opened while the one before still runs: the wait is nobody's any more,
        // and the core is never started for it.
        var late = new TaskCompletionSource();
        Task abandoned = tab.ComputeAfterAsync(late.Task);
        tab.Populate(Result(ws, withProfile: true));
        late.SetResult();
        await abandoned;
        Assert.Single(capture.Messages, m => m.StartsWith("gamutfit start"));
        Assert.False(tab.HasGallery);
        Assert.False(tab.IsBusy);
    }

    [Fact]
    public async Task R188_TheWindowStartsTheFitWhenTheProofIsShownNotBesideIt()
    {
        using var ws = new TempWorkspace();
        string icc = ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20));
        string tif = WindowSmokeTests.DarkTiff(ws);
        using LogCapture capture = LogCapture.Start();
        var vm = new MainViewModel();
        Assert.True(vm.Parameters.UseProfile(new IccProfileRow(IccPaperBlack.Read(icc))));

        await vm.OpenAsync(tif);
        Assert.True(vm.HasResult, vm.Error);
        await vm.GamutFitComputation;
        Assert.True(vm.ProofComputation.IsCompleted);   // the fit's task holds the wait for the proof
        Assert.True(vm.SoftProof.HasProof, vm.SoftProof.Message);
        Assert.True(vm.GamutFit.HasGallery, vm.GamutFit.Message);

        // The core's fit starts after the proof was rendered: started beside it, its start line
        // would come first, the proof's table taking far longer than a log line.
        List<string> log = capture.Messages.ToList();
        int rendered = log.FindIndex(m => m.StartsWith("proof rendered"));
        int fit = log.FindIndex(m => m.StartsWith("gamutfit start"));
        Assert.True(rendered >= 0, "the proof was rendered");
        Assert.True(fit > rendered, $"the fit started at line {fit}, the proof was rendered at line {rendered}");
    }

    [Fact]
    public void R178_SaveAsProposesTheInputsKindAndWritesTheCoresFile()
    {
        using var ws = new TempWorkspace();
        var tab = new GamutFitTabViewModel();
        tab.Populate(Result(ws, withProfile: true, inputName: "calea-lactee.tif"));
        tab.Compute();
        Assert.True(tab.HasGallery, tab.Message);
        Assert.Equal(ws.Dir, tab.InputFolder);

        string? proposed = null;
        tab.SaveRequested += name => proposed = name;
        tab.SaveAs.Execute(null);
        Assert.Equal("calea-lactee_Exacta.tif", proposed);

        string target = ws.PathTo(proposed!);
        tab.Save(target);
        Assert.True(File.Exists(target));
        Assert.Null(tab.SaveError);
        Assert.Contains(target, tab.SaveMessage);
        Raster back = ImageLoader.Load(target);
        Assert.Equal("TIFF", back.Source.Format);
        Assert.Equal(16, back.Source.BitDepth);
        Assert.Equal(IccTestData.V2("sRGB test"), back.Source.Icc);   // the input's own profile, embedded

        string fits = ws.PathTo("calea-lactee_Exacta.fit");
        tab.Save(fits);
        Assert.Equal("FITS", ImageLoader.Load(fits).Source.Format);

        string input = ws.PathTo("calea-lactee.tif");
        byte[] before = File.ReadAllBytes(input);
        tab.Save(input);
        Assert.Contains("imaginea de intrare", tab.SaveError);
        Assert.Null(tab.SaveMessage);
        Assert.Equal(before, File.ReadAllBytes(input));
    }

    [Fact]
    public void R178_WithoutAProfileTheTabShowsTheSentenceAndNothingElse()
    {
        using var ws = new TempWorkspace();
        var tab = new GamutFitTabViewModel();
        tab.Populate(Result(ws, withProfile: false));
        tab.Compute();
        Assert.False(tab.HasGallery);
        Assert.True(tab.IsProblem);
        Assert.Equal(ProofTexts.NoProfile, tab.Message);
        Assert.Contains("Din profil ICC...", tab.Message);
    }

    [Fact]
    public void R178_TheSentenceWithoutAProfileWrapsInsideTheTab()
        => _sta.Invoke(() =>
        {
            using var ws = new TempWorkspace();
            var tab = new GamutFitTabViewModel();
            tab.Populate(Result(ws, withProfile: false));
            tab.Compute();
            Assert.True(tab.IsProblem);

            // The tab's width in the window at its default size: the sentence ends inside it.
            var view = new GamutFitView { DataContext = tab };
            view.Measure(new System.Windows.Size(1084, 600));
            view.Arrange(new System.Windows.Rect(0, 0, 1084, 600));
            view.UpdateLayout();
            var block = (System.Windows.FrameworkElement)view.FindName("TabMessage");
            System.Windows.Rect sentence = block.TransformToAncestor(view).TransformBounds(new System.Windows.Rect(block.RenderSize));
            Assert.True(sentence.Width > 100);
            Assert.True(sentence.Right <= 1084, $"the sentence ends at {sentence.Right:0}, past the tab's 1084");
        });

    [Fact]
    public void R178_TheTabRendersWithNoBindingErrors()
        => _sta.Invoke(() =>
        {
            using var ws = new TempWorkspace();
            using var errors = new WindowSmokeTests.BindingErrors();
            var tab = new GamutFitTabViewModel();
            tab.Populate(Result(ws, withProfile: true));
            tab.Compute();
            tab.ShowOutside = true;
            var view = new GamutFitView { DataContext = tab };
            WindowSmokeTests.Render(view, name: "tab-culori-hartie");
            Assert.Equal("", errors.Text);

            // The comparison is wired to the tab: the original left, the variant shown right,
            // each with its layer, and a frame smaller than the area kept at its own size.
            var comparison = Assert.IsType<BeforeAfterViewer>(view.GalleryArea.Child);
            Assert.Same(tab.Before!.Image, comparison.Before);
            Assert.NotNull(comparison.BeforeOverlay);
            Assert.Same(tab.Before.Overlay, comparison.BeforeOverlay);
            Assert.Same(tab.Current!.Image, comparison.After);
            // Nothing is left outside after the fit on this frame, so the variant's layer is
            // null on both ends: the binding itself is what says the layer would be shown.
            Assert.Null(tab.Current.Overlay);
            Assert.Null(comparison.AfterOverlay);
            Assert.Equal("Current.Overlay", System.Windows.Data.BindingOperations.GetBinding(comparison, BeforeAfterViewer.AfterOverlayProperty)?.Path.Path);
            Assert.Equal(tab.Current.Width, comparison.ContentWidth);
            Assert.Equal(tab.Current.Height, comparison.ContentHeight);
            Assert.Equal("Inainte", comparison.BeforeText);
            Assert.Equal("Dupa", comparison.AfterText);
            // Nothing is ever enlarged to fill the area (R-188): the view says so itself, bound to nothing.
            Assert.Null(System.Windows.Data.BindingOperations.GetBinding(comparison, BeforeAfterViewer.FitEnlargesProperty));
            Assert.False(comparison.FitEnlarges);
            Assert.Equal(1, comparison.Viewer.Scale, 9);

            // The buttons change the right side only.
            tab.Next.Execute(null);
            Assert.Same(tab.Current.Image, comparison.After);
            Assert.Same(tab.Before.Image, comparison.Before);
            tab.ShowOutside = false;
            Assert.Null(comparison.BeforeOverlay);

            // Another image opened: the tab empties through its bindings, and the next gallery,
            // of the same size, starts whole with the line in the middle (the controls keep
            // nothing between images, R-178).
            comparison.MoveDividerTo(100);
            comparison.Viewer.ZoomAt(new System.Windows.Point(0, 0), 2);
            Assert.Equal(2, comparison.Viewer.Zoom);
            tab.Populate(Result(ws, withProfile: true));
            Assert.Null(comparison.After);
            Assert.Null(comparison.Before);
            Assert.Equal(0.5, comparison.DividerPosition);
            tab.Compute();
            view.UpdateLayout();
            Assert.Same(tab.Current!.Image, comparison.After);
            Assert.Equal(1, comparison.Viewer.Zoom);
            Assert.Equal(0.5, comparison.DividerPosition);
        });
}
