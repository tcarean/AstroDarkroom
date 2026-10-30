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
        Assert.False(tab.ShowScreenGamut);
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
    public async Task R163_TheProofFollowsTheControlsAndTheMarginFollowsTheSimulation()
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
        Assert.Equal(ProofTexts.PaperGamutLegend(core.PctPaper), tab.PaperGamutLegend);
        Assert.Equal(ProofTexts.ScreenGamutLegend(core.PctScreen), tab.ScreenGamutLegend);
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

        // A chosen margin wins over the automatic one.
        tab.Margin = ProofMarginChoice.White;
        Assert.Equal(Colors.White, ColourOf(tab.MarginBrush));
        tab.Margin = ProofMarginChoice.Black;
        Assert.Equal(Colors.Black, ColourOf(tab.MarginBrush));
        tab.Margin = ProofMarginChoice.PaperWhite;
        Assert.Equal(paper, ColourOf(tab.MarginBrush));

        // The original swaps the image inside the same margin and hides the warnings.
        tab.Margin = ProofMarginChoice.PaperWhite;
        var proof = tab.Image;
        tab.ShowPaperGamut = true;
        Assert.True(tab.ShowPaperLayer);
        tab.ShowOriginal = true;
        Assert.NotSame(proof, tab.Image);
        Assert.False(tab.ShowPaperLayer);
        Assert.Equal(paper, ColourOf(tab.MarginBrush));
        tab.ShowOriginal = false;
        Assert.Same(proof, tab.Image);

        // Each warning shows its own layer; both show the third as well.
        Assert.True(tab.ShowPaperLayer);
        Assert.False(tab.ShowScreenLayer);
        Assert.False(tab.ShowBothLayer);
        tab.ShowScreenGamut = true;
        Assert.True(tab.ShowScreenLayer);
        Assert.True(tab.ShowBothLayer);
        tab.ShowPaperGamut = false;
        Assert.False(tab.ShowPaperLayer);
        Assert.False(tab.ShowBothLayer);
        Assert.NotNull(tab.PaperLayer);
        Assert.NotNull(tab.ScreenLayer);
        Assert.NotNull(tab.BothLayer);

        // The window's keys; P, the simulation's, is no key any more.
        Assert.False(tab.HandleKey(Key.P));
        Assert.True(tab.HandleKey(Key.G));
        Assert.True(tab.ShowPaperGamut);
        Assert.True(tab.HandleKey(Key.M));
        Assert.False(tab.ShowScreenGamut);   // it was on from the layers check above
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

    static void Layout(FrameworkElement el)
    {
        el.Measure(new Size(1400, 860));
        el.Arrange(new Rect(0, 0, 1400, 860));
        el.UpdateLayout();
    }
}
