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
using System.Windows.Input;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf.Tests;

/// <summary>
/// R-163: the Soft proof tab and its full-screen window. The tab computes nothing: every
/// figure and sentence asserted here is read back from the core's <c>ProofRun</c>.
/// </summary>
[Collection("fereastra")]
public class SoftProofTabTests
{
    readonly StaFixture _sta;

    public SoftProofTabTests(StaFixture sta) => _sta = sta;

    static readonly double[] TintedWhite = { 0.90, 0.93, 0.75 };

    static string Profile(TempWorkspace ws) => ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20, white: TintedWhite));

    static AnalysisResult Result(string? icc)
    {
        Parameters p = Scenes.OnBuiltForPaper();
        if (icc != null) p = p.WithDmaxFromProfile(icc, out string error) ?? throw new Exception(error);
        return AnalysisRun.Execute(Scenes.DarkSky(), "memorie", p);
    }

    static Color ColourOf(Brush b) => ((SolidColorBrush)b).Color;

    [Fact]
    public void R163_TheTabStartsOnPerceptualWithEverythingOffAndSaysWhenThereIsNoProfile()
    {
        var tab = new SoftProofTabViewModel();
        Assert.True(tab.IsPerceptual);
        Assert.False(tab.IsRelative);
        Assert.False(tab.IsRelativeNoBpc);
        Assert.False(tab.ShowPaperGamut);
        Assert.False(tab.ShowOriginal);
        Assert.Equal(ProofMarginChoice.PaperWhite, tab.Margin);
        Assert.Equal(Colors.White, ColourOf(tab.MarginBrush));
        Assert.False(tab.HasProof);
        Assert.False(tab.FullScreen.CanExecute(null));
        Assert.Equal("Relativ, fara compensarea negrului", tab.RelativeNoBpcName);

        tab.Populate(Result(null));
        tab.Compute();
        Assert.True(tab.HasResult);
        Assert.False(tab.HasProof);
        Assert.True(tab.IsProblem);
        Assert.Equal(ProofTexts.NoProfile, tab.Message);
        Assert.Contains("Din profil ICC...", tab.Message);
        Assert.Null(tab.Image);
        Assert.False(tab.FullScreen.CanExecute(null));
    }

    [Fact]
    public async Task R163_TheProofFollowsTheControlsAndTheMarginIsThePaperWhiteUnlessChosen()
    {
        using var ws = new TempWorkspace();
        string icc = Profile(ws);
        AnalysisResult result = Result(icc);
        var tab = new SoftProofTabViewModel();
        tab.Populate(result);
        tab.Compute();

        Assert.True(tab.HasProof, tab.Message);
        Assert.Null(tab.Message);
        Assert.NotNull(tab.Image);
        Assert.Equal(512, tab.Width);
        Assert.Equal(512, tab.Height);
        Assert.True(tab.FullScreen.CanExecute(null));
        Assert.Contains("Perceptual", tab.Caption);
        Assert.Contains("Hartie de test", tab.ProfileLine);

        // The figures are the core's own.
        ProofSet set = ProofRun.Prepare(result);
        SoftProofResult core = ProofRun.Render(set, ProofIntent.Perceptual, simulate: true);
        Assert.Equal(ProofTexts.PaperGamutLegend(set.PctPaper), tab.PaperGamutLegend);
        Assert.Equal(ProofTexts.PaperWhiteLine(set.Profile), tab.PaperWhiteLine);
        Assert.Equal(ProofTexts.PaperBlackLine(set.Profile), tab.PaperBlackLine);
        Assert.Equal(ProofTexts.Caption(set.Profile, ProofIntent.Perceptual), tab.Caption);

        // The intent renders again; the proof image is a new bitmap.
        var proofImage = tab.Image;
        tab.IsRelative = true;
        await tab.ProofComputation;
        Assert.True(tab.IsRelative);
        Assert.False(tab.IsPerceptual);
        Assert.Contains("Relativ", tab.Caption);
        Assert.NotSame(proofImage, tab.Image);
        Assert.False(tab.IsBusy);

        // The paper's white and black are always as they are, and the margin is the paper's white.
        Assert.Equal(ProofTexts.Caption(set.Profile, ProofIntent.Relative), tab.Caption);
        Assert.Contains("simulate", tab.Caption);
        Color paper = tab.PaperWhiteColour;
        Assert.NotEqual(Colors.White, paper);
        float[] w = set.Profile.PaperWhiteSrgb;
        Assert.Equal(Color.FromRgb((byte)RasterOps.Level8(w[0]), (byte)RasterOps.Level8(w[1]), (byte)RasterOps.Level8(w[2])), paper);
        Assert.Equal(paper, ColourOf(tab.MarginBrush));
        Assert.True(paper.B < paper.R, "a yellowish paper white");

        // A chosen margin replaces the paper's white, and the default brings it back.
        tab.Margin = ProofMarginChoice.White;
        Assert.Equal(Colors.White, ColourOf(tab.MarginBrush));
        tab.Margin = ProofMarginChoice.Black;
        Assert.Equal(Colors.Black, ColourOf(tab.MarginBrush));
        tab.Margin = ProofMarginChoice.PaperWhite;
        Assert.Equal(paper, ColourOf(tab.MarginBrush));

        // The original swaps the image inside the same margin and hides the warning.
        var proof = tab.Image;
        tab.ShowPaperGamut = true;
        Assert.True(tab.ShowPaperLayer);
        tab.ShowOriginal = true;
        Assert.NotSame(proof, tab.Image);
        Assert.False(tab.ShowPaperLayer);
        Assert.Equal(paper, ColourOf(tab.MarginBrush));
        tab.ShowOriginal = false;
        Assert.Same(proof, tab.Image);

        // The warning shows its layer, the same bitmap through a change of intent.
        Assert.True(tab.ShowPaperLayer);
        var layer = tab.PaperLayer;
        Assert.NotNull(layer);
        tab.ShowPaperGamut = false;
        Assert.False(tab.ShowPaperLayer);
        tab.IsRelativeNoBpc = true;
        await tab.ProofComputation;
        Assert.Same(layer, tab.PaperLayer);
        tab.IsRelative = true;
        await tab.ProofComputation;

        // The window's keys; P and M, the dropped boxes', are no keys any more.
        Assert.False(tab.HandleKey(Key.P));
        Assert.False(tab.HandleKey(Key.M));
        Assert.True(tab.HandleKey(Key.G));
        Assert.True(tab.ShowPaperGamut);
        Assert.True(tab.HandleKey(Key.O));
        Assert.True(tab.ShowOriginal);
        Assert.True(tab.HandleKey(Key.I));
        await tab.ProofComputation;
        Assert.True(tab.IsRelativeNoBpc);
        Assert.True(tab.HandleKey(Key.I));
        await tab.ProofComputation;
        Assert.True(tab.IsPerceptual);
        Assert.False(tab.HandleKey(Key.X));
    }

    [Fact]
    public async Task R163_TheProofComesWithTheAnalysisWithoutAPressAndALateResultNeverReachesAnotherImage()
    {
        using var ws = new TempWorkspace();
        string icc = Profile(ws);
        string tif = WindowSmokeTests.DarkTiff(ws);
        var vm = new MainViewModel();
        Assert.True(vm.ProofComputation.IsCompleted);
        Assert.True(vm.Parameters.UseProfile(new IccProfileRow(IccPaperBlack.Read(icc))));

        await vm.OpenAsync(tif);
        Assert.True(vm.HasResult, vm.Error);
        await vm.ProofComputation;
        Assert.True(vm.SoftProof.HasProof, vm.SoftProof.Message);
        Assert.False(vm.SoftProof.IsBusy);
        Assert.Contains("Hartie de test", vm.SoftProof.ProfileLine);

        // Without the profile the same run leaves the tab with the core's sentence.
        await vm.ClearProfile();
        await vm.ProofComputation;
        Assert.False(vm.SoftProof.HasProof);
        Assert.Equal(ProofTexts.NoProfile, vm.SoftProof.Message);

        // A control changed while the first computation runs is rendered right after it.
        var early = new SoftProofTabViewModel();
        early.Populate(Result(icc));
        Task first = early.ComputeAsync();
        Assert.Same(first, early.ProofComputation);
        early.IsRelative = true;
        await first;
        await early.ProofComputation;
        Assert.True(early.HasProof, early.Message);
        Assert.Equal(ProofTexts.Caption(ProofRun.Prepare(Result(icc)).Profile, ProofIntent.Relative), early.Caption);
        Assert.False(early.IsBusy);

        // A computation abandoned by Clear touches nothing when it ends; the controls keep
        // their state from one image to the next.
        var tab = new SoftProofTabViewModel { Margin = ProofMarginChoice.Grey };
        tab.Populate(Result(icc));
        Task late = tab.ComputeAsync();
        tab.Clear();
        await late;
        Assert.False(tab.HasProof);
        Assert.Null(tab.Message);
        Assert.False(tab.IsBusy);
        tab.Populate(Result(icc));
        Assert.Equal(ProofMarginChoice.Grey, tab.Margin);
        tab.Compute();
        Assert.True(tab.HasProof);
    }

    [Fact]
    public void R163_TheTabAndTheWindowRenderWithNoBindingErrors()
        => _sta.Invoke(() =>
        {
            using var ws = new TempWorkspace();
            using var errors = new WindowSmokeTests.BindingErrors();
            var tab = new SoftProofTabViewModel();
            tab.Populate(Result(null));
            tab.Compute();
            var view = new SoftProofView { DataContext = tab };
            Layout(view);
            Assert.True(view.ActualWidth > 0);

            tab.Populate(Result(Profile(ws)));
            tab.Compute();
            Layout(view);

            // The three radio buttons follow the intent, both ways, without a GroupName.
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
            Assert.Equal(new[] { "Perceptual", "Relativ", "Relativ, fara compensarea negrului" }, radios.Select(r => (string)r.Content).ToArray());
            Assert.All(radios, r => Assert.True(string.IsNullOrEmpty(r.GroupName)));
            Assert.True(radios[0].IsChecked);
            radios[1].IsChecked = true;
            Assert.True(tab.IsRelative);
            Assert.False(radios[0].IsChecked);
            tab.Intent = ProofIntent.Perceptual;
            Assert.True(radios[0].IsChecked);

            SoftProofTabViewModel? asked = null;
            tab.ProofRequested += p => asked = p;
            tab.FullScreen.Execute(null);
            Assert.Same(tab, asked);
            var window = new SoftProofWindow { DataContext = tab };
            try
            {
                Layout((FrameworkElement)window.Content);
                Assert.StartsWith("Soft proof - ", window.Title);
            }
            finally
            {
                window.DataContext = null;
            }
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
            tab.Clear();   // the renders the radio buttons started are abandoned, not left to the next test
        });

    [Fact]
    public async Task R163_TheControlsStayWhileARenderRunsAndNoneShowWithoutASet()
    {
        // Without a usable set there are no controls; with one they stay while a render of
        // another intent runs, "computing" shown meanwhile and the earlier proof still there.
        // (A table the core refuses at build time, which would keep the controls with no image,
        // is not reachable from the hand-built profile; that path is read, not tested.)
        var tab = new SoftProofTabViewModel();
        tab.Populate(Result(null));
        tab.Compute();
        Assert.False(tab.HasControls);

        using var ws = new TempWorkspace();
        var usable = new SoftProofTabViewModel();
        usable.Populate(Result(Profile(ws)));
        usable.Compute();
        Assert.True(usable.HasControls);
        Assert.True(usable.HasProof);
        usable.IsRelative = true;
        Assert.True(usable.IsBusy);
        Assert.True(usable.HasControls);
        Assert.True(usable.HasProof);
        Assert.Equal(ProofTexts.Computing, usable.Message);
        Assert.False(usable.IsProblem);
        await usable.ProofComputation;
        Assert.True(usable.HasControls);
        Assert.True(usable.HasProof);
        Assert.Null(usable.Message);
        Assert.Contains("Relativ", usable.Caption);
    }

    // ---- R-166: the comparison with another image

    static AnalysisResult ResultAt(string icc, string path)
    {
        Parameters p = Scenes.OnBuiltForPaper().WithDmaxFromProfile(icc, out string error) ?? throw new Exception(error);
        return AnalysisRun.Execute(Scenes.DarkSky(), path, p);
    }

    /// <summary>A TIFF whose left half is a grey and whose right half is a saturated red, outside the test paper's gamut.</summary>
    static string OtherTiff(TempWorkspace ws, string name = "alta.tif", int w = 512, int h = 512, float grey = 0.5f)
        => ws.WriteFile(name, TiffTestData.Build16Rgb(w, h,
            (x, _, c) => RasterOps.Level16(x < w / 2 ? grey : c == 0 ? 0.8f : 0.2f), IccTestData.V2("sRGB test")));

    [Fact]
    public async Task R166_TheOtherImageIsComparedAndEveryControlActsOnBothSides()
    {
        using var ws = new TempWorkspace();
        AnalysisResult result = Result(Profile(ws));
        var tab = new SoftProofTabViewModel();
        Assert.False(tab.CanCompare);
        tab.Populate(result);
        tab.Compute();
        Assert.True(tab.CanCompare);
        Assert.False(tab.IsComparing);
        Assert.Null(tab.OtherImage);
        Assert.False(tab.StopComparing.CanExecute(null));
        Assert.Equal("memorie", tab.ImageName);

        string other = OtherTiff(ws);
        Task comparing = tab.CompareAsync(other);
        Assert.Same(comparing, tab.ProofComputation);
        Assert.True(tab.IsBusy);
        Assert.False(tab.CanCompare);
        Assert.Equal(ProofTexts.Computing, tab.Message);
        await comparing;
        Assert.True(tab.IsComparing, tab.Message);
        Assert.Null(tab.Message);
        Assert.False(tab.IsBusy);
        Assert.NotNull(tab.OtherImage);
        Assert.Equal("alta.tif", tab.OtherName);
        Assert.True(tab.StopComparing.CanExecute(null));
        Assert.True(tab.CanCompare);

        // The shares and the sentences are the core's, one legend per image.
        ProofSet first = ProofRun.Prepare(result);
        ProofSet second = ProofRun.PrepareOther(first, other);
        Assert.Null(second.Problem);
        Assert.Equal(ProofTexts.PaperGamutLegend("memorie", first.PctPaper), tab.PaperGamutLegend);
        Assert.Equal(ProofTexts.PaperGamutLegend("alta.tif", second.PctPaper), tab.OtherPaperGamutLegend);
        Assert.Equal(ProofTexts.CompareSides("memorie", "alta.tif"), tab.CompareSides);

        // The warning paints each side by its own pixels, only while its box is on.
        Assert.Null(tab.PaperOverlay);
        Assert.Null(tab.OtherPaperOverlay);
        tab.ShowPaperGamut = true;
        Assert.Same(tab.PaperLayer, tab.PaperOverlay);
        Assert.NotNull(tab.OtherPaperOverlay);
        Assert.NotSame(tab.PaperOverlay, tab.OtherPaperOverlay);

        // "Originalul" shows both as they are, with no red on either.
        var left = tab.Image;
        var right = tab.OtherImage;
        Assert.True(tab.HandleKey(Key.O));
        Assert.NotSame(left, tab.Image);
        Assert.NotSame(right, tab.OtherImage);
        Assert.Null(tab.PaperOverlay);
        Assert.Null(tab.OtherPaperOverlay);
        tab.ShowOriginal = false;
        Assert.Same(left, tab.Image);
        Assert.Same(right, tab.OtherImage);
        Assert.NotNull(tab.OtherPaperOverlay);

        // A change of intent renders both again, the comparison kept, the red layers the same.
        var redRight = tab.OtherPaperOverlay;
        tab.IsRelative = true;
        await tab.ProofComputation;
        Assert.True(tab.IsComparing);
        Assert.NotSame(left, tab.Image);
        Assert.NotSame(right, tab.OtherImage);
        Assert.Same(redRight, tab.OtherPaperOverlay);
        SoftProofResult core = ProofRun.Render(second, ProofIntent.Relative, simulate: true);
        Assert.Equal(core.Image.W, tab.Width);
        Assert.Equal(core.Image.H, tab.Height);

        // "Schimba imaginea...": another file replaces the right side.
        right = tab.OtherImage;
        await tab.CompareAsync(OtherTiff(ws, "a-treia.tif", grey: 0.3f));
        Assert.True(tab.IsComparing, tab.Message);
        Assert.Equal("a-treia.tif", tab.OtherName);
        Assert.NotSame(right, tab.OtherImage);
        Assert.Equal(ProofTexts.CompareSides("memorie", "a-treia.tif"), tab.CompareSides);

        // "Opreste comparatia": the single image of R-163 again, its legend without a name.
        tab.StopComparing.Execute(null);
        Assert.False(tab.IsComparing);
        Assert.Null(tab.OtherImage);
        Assert.Null(tab.OtherPaperOverlay);
        Assert.Equal("", tab.OtherPaperGamutLegend);
        Assert.Equal("", tab.CompareSides);
        Assert.Equal(ProofTexts.PaperGamutLegend(first.PctPaper), tab.PaperGamutLegend);
        Assert.True(tab.HasProof);
        Assert.False(tab.StopComparing.CanExecute(null));
        Assert.True(tab.CanCompare);
    }

    [Fact]
    public void R166_ARefusedFileLeavesWhatWasShownAndAnotherImageEndsTheComparison()
    {
        using var ws = new TempWorkspace();
        string icc = Profile(ws);
        var tab = new SoftProofTabViewModel();
        tab.Populate(ResultAt(icc, "prima.tif"));
        tab.Compute();

        // Refused as the first choice: the single image stays, the reason in red.
        string small = OtherTiff(ws, "mica.tif", w: 256);
        tab.Compare(small);
        Assert.False(tab.IsComparing);
        Assert.True(tab.HasProof);
        Assert.NotNull(tab.Image);
        Assert.True(tab.IsProblem);
        Assert.Equal(ProofTexts.SizeMismatch(512, 512, 256, 512), tab.Message);

        // A file the core takes clears the reason.
        string good = OtherTiff(ws);
        tab.Compare(good);
        Assert.True(tab.IsComparing, tab.Message);
        Assert.Null(tab.Message);
        Assert.False(tab.IsProblem);

        // Refused as a replacement: the earlier comparison stays.
        var right = tab.OtherImage;
        tab.Compare(small);
        Assert.True(tab.IsComparing);
        Assert.Same(right, tab.OtherImage);
        Assert.Equal("alta.tif", tab.OtherName);
        Assert.True(tab.IsProblem);
        Assert.Equal(ProofTexts.SizeMismatch(512, 512, 256, 512), tab.Message);

        // The same image analysed again keeps the comparison and proofs the other file again.
        tab.Clear();
        Assert.False(tab.IsComparing);
        tab.Populate(ResultAt(icc, "prima.tif"));
        tab.Compute();
        Assert.True(tab.IsComparing, tab.Message);
        Assert.Equal("alta.tif", tab.OtherName);
        Assert.NotNull(tab.OtherImage);
        Assert.NotSame(right, tab.OtherImage);
        Assert.Null(tab.Message);

        // The other file gone by then: the comparison ends, with the reason.
        File.Delete(good);
        tab.Clear();
        tab.Populate(ResultAt(icc, "prima.tif"));
        tab.Compute();
        Assert.False(tab.IsComparing);
        Assert.True(tab.HasProof);
        Assert.True(tab.IsProblem);
        Assert.StartsWith("fisierul nu exista: ", tab.Message);
        // ... and is not tried again at the next run.
        tab.Clear();
        tab.Populate(ResultAt(icc, "prima.tif"));
        tab.Compute();
        Assert.False(tab.IsComparing);
        Assert.Null(tab.Message);

        // Another image ends the comparison without a word.
        tab.Compare(OtherTiff(ws, "din-nou.tif"));
        Assert.True(tab.IsComparing, tab.Message);
        tab.Clear();
        tab.Populate(ResultAt(icc, "a-doua.tif"));
        tab.Compute();
        Assert.False(tab.IsComparing);
        Assert.Null(tab.OtherImage);
        Assert.Null(tab.Message);
        Assert.True(tab.HasProof);
        Assert.Equal("a-doua.tif", tab.ImageName);
    }

    [Fact]
    public void R166_TheTabAndTheFullScreenWindowShowTheComparisonAndItsButtons()
        => _sta.Invoke(() =>
        {
            using var ws = new TempWorkspace();
            using var errors = new WindowSmokeTests.BindingErrors();
            var tab = new SoftProofTabViewModel();
            tab.Populate(ResultAt(Profile(ws), "prima.tif"));
            tab.Compute();
            var view = new SoftProofView { DataContext = tab };
            Layout(view);

            // One image: the one button, the single viewer.
            SoftProofControls controls = view.Controls;
            Assert.Equal("Compara cu alta imagine...", (string)controls.CompareButton.Content);
            Assert.Equal(Visibility.Visible, controls.CompareButton.Visibility);
            Assert.True(controls.CompareButton.IsEnabled);
            Assert.Equal(Visibility.Collapsed, controls.ChangeButton.Visibility);
            Assert.Equal(Visibility.Collapsed, controls.StopButton.Visibility);
            Assert.Equal(Visibility.Visible, view.Single.Visibility);
            Assert.Equal(Visibility.Collapsed, view.Comparison.Visibility);

            // Comparing: the other two buttons, the line between the two proofs, named by their files.
            tab.Compare(OtherTiff(ws));
            Assert.True(tab.IsComparing, tab.Message);
            Layout(view);
            Assert.Equal(Visibility.Collapsed, controls.CompareButton.Visibility);
            Assert.Equal("Schimba imaginea...", (string)controls.ChangeButton.Content);
            Assert.Equal(Visibility.Visible, controls.ChangeButton.Visibility);
            Assert.Equal("Opreste comparatia", (string)controls.StopButton.Content);
            Assert.Equal(Visibility.Visible, controls.StopButton.Visibility);
            Assert.Equal(Visibility.Collapsed, view.Single.Visibility);
            Assert.Equal(Visibility.Visible, view.Comparison.Visibility);
            Assert.Same(tab.Image, view.Comparison.Before);
            Assert.Same(tab.OtherImage, view.Comparison.After);
            Assert.Equal("prima.tif", view.Comparison.BeforeText);
            Assert.Equal("alta.tif", view.Comparison.AfterText);
            Assert.Equal(512.0, view.Comparison.ContentWidth);
            Assert.Equal(512.0, view.Comparison.ContentHeight);
            Assert.Equal(0.5, view.Comparison.DividerPosition);

            // The red layer on each side, its own.
            Assert.Null(view.Comparison.BeforeOverlay);
            Assert.Null(view.Comparison.AfterOverlay);
            tab.ShowPaperGamut = true;
            Assert.NotNull(view.Comparison.BeforeOverlay);
            Assert.Same(tab.PaperOverlay, view.Comparison.BeforeOverlay);
            Assert.Same(tab.OtherPaperOverlay, view.Comparison.AfterOverlay);
            WindowSmokeTests.Render(view, name: "soft-proof-comparatie");   // saved with APA_DUMP_SCREENS, to look at

            // The full-screen window shows the same comparison, with the same buttons in its panel.
            var window = new SoftProofWindow { DataContext = tab };
            try
            {
                Layout((FrameworkElement)window.Content);
                Assert.Equal(Visibility.Visible, window.Comparison.Visibility);
                Assert.Equal(Visibility.Collapsed, window.Single.Visibility);
                Assert.Same(tab.Image, window.Comparison.Before);
                Assert.Same(tab.OtherImage, window.Comparison.After);
                Assert.Equal("alta.tif", window.Comparison.AfterText);
                Assert.Equal(Visibility.Visible, window.ProofControls.ChangeButton.Visibility);
                Assert.Equal(Visibility.Visible, window.ProofControls.StopButton.Visibility);
                Assert.True(window.HoverPanel.IsAncestorOf(window.ProofControls.StopButton));

                // "Opreste comparatia" brings both back to the single image.
                tab.StopComparing.Execute(null);
                Layout((FrameworkElement)window.Content);
                Layout(view);
                Assert.Equal(Visibility.Collapsed, window.Comparison.Visibility);
                Assert.Equal(Visibility.Visible, window.Single.Visibility);
                Assert.Equal(Visibility.Visible, window.ProofControls.CompareButton.Visibility);
                Assert.Equal(Visibility.Collapsed, view.Comparison.Visibility);
                Assert.Equal(Visibility.Visible, view.Single.Visibility);
                Assert.Null(view.Comparison.After);
            }
            finally
            {
                window.DataContext = null;
            }
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);

            using (Tr.Use(UiLanguage.English))
            {
                var english = new SoftProofControls { DataContext = tab };
                Assert.Equal("Compare with another image...", (string)english.CompareButton.Content);
                Assert.Equal("Change the image...", (string)english.ChangeButton.Content);
                Assert.Equal("Stop comparing", (string)english.StopButton.Content);
            }
            tab.Clear();
        });

    static void Layout(FrameworkElement el)
    {
        el.Measure(new Size(1400, 860));
        el.Arrange(new Rect(0, 0, 1400, 860));
        el.UpdateLayout();
    }
}
