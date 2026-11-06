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

// GamutFitTabTests - the Culori pentru hartie tab (R-178, ADR 0083): the gallery of the
// original and the three variants, the red layer, the save, the sentence without a profile.

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
    public void R178_TheGalleryHoldsTheOriginalAndTheThreeVariantsAndTheRedLayerFollowsTheBox()
    {
        using var ws = new TempWorkspace();
        var tab = new GamutFitTabViewModel();
        Assert.False(tab.HasGallery);
        Assert.True(tab.ShowOutside);   // on by default (R-178)
        tab.Populate(Result(ws, withProfile: true));
        Assert.False(tab.HasGallery);   // filled by the computation MainViewModel starts, not by Populate

        tab.Compute(400, 300);
        Assert.True(tab.HasGallery, tab.Message);
        Assert.Equal(new[] { "Originalul", "Exacta", "Lina", "Foarte lina" }, tab.Items.Select(i => i.Name).ToArray());
        Assert.Equal("1 / 4", tab.Position);
        Assert.Equal("Originalul", tab.Current!.Name);
        Assert.False(tab.SaveAs.CanExecute(null));
        Assert.NotNull(tab.Current.Overlay);   // the box is on: the original's warning of R-161 over the patch
        Assert.Equal(tab.Current.Image.PixelWidth, tab.Current.Overlay!.PixelWidth);
        // The original carries the figures of the image as it is: the share outside, the dark
        // share and the recipe, in plain words.
        Assert.Contains(tab.Current.EvidenceRows, r => r.Text.StartsWith("culori pe care hartia nu le poate reda:") && r.Text.Contains("%"));
        Assert.Contains(tab.Current.EvidenceRows, r => r.Text.Contains("tonuri inchise"));
        Assert.Contains(tab.Current.EvidenceRows, r => r.Text.StartsWith("de mana"));
        Assert.DoesNotContain(tab.Current.EvidenceRows, r => r.Text.Contains("dupa"));

        tab.ShowOutside = false;
        Assert.Null(tab.Current.Overlay);   // the box off: no layer
        tab.ShowOutside = true;

        tab.Next.Execute(null);
        Assert.Equal("Exacta", tab.Current.Name);
        Assert.Equal("2 / 4", tab.Position);
        Assert.True(tab.SaveAs.CanExecute(null));
        Assert.Null(tab.Current.Overlay);   // nothing is left outside after the fit
        Assert.Contains(tab.Current.EvidenceRows, r => r.Text.Contains("inainte") && r.Text.Contains("%"));
        Assert.Contains(tab.Current.EvidenceRows, r => r.Text.Contains("dupa"));
        Assert.DoesNotContain(tab.Current.EvidenceRows, r => r.Text.Contains("median") || r.Text.Contains("percentil") || r.Text.Contains("croma"));

        tab.Next.Execute(null); tab.Next.Execute(null);
        Assert.Equal("Foarte lina", tab.Current.Name);
        Assert.False(tab.Next.CanExecute(null));
        tab.Previous.Execute(null);
        Assert.Equal("Lina", tab.Current.Name);

        // Another image empties the gallery; the box goes back to its default, on.
        tab.ShowOutside = false;
        tab.Populate(Result(ws, withProfile: true));
        Assert.False(tab.HasGallery);
        Assert.True(tab.ShowOutside);
    }

    [Fact]
    public async Task R178_ALargerAreaRecomputesTheGalleryByItselfAndTheImageCoversIt()
    {
        using var ws = new TempWorkspace();
        var tab = new GamutFitTabViewModel();
        AnalysisResult result = Result(ws, withProfile: true);
        int w = result.Raster.W, h = result.Raster.H;
        tab.Populate(result);
        tab.Compute(w / 4, h / 4);
        Assert.True(tab.HasGallery, tab.Message);
        Assert.True(tab.Current!.Width >= w / 4 && tab.Current.Height >= h / 4);
        int before = tab.Current.Width;

        tab.SetGallerySize(w / 4, h / 4);
        tab.SetGallerySize(w / 6, h / 6);
        Assert.False(tab.IsBusy);
        Assert.Equal(before, tab.Current.Width);

        tab.ShowOutside = true;
        tab.SetGallerySize(w, h);
        Assert.True(tab.IsBusy);
        Assert.True(tab.HasGallery);
        await tab.Computation;
        Assert.False(tab.IsBusy);
        Assert.Equal(w, tab.Current!.Width);
        Assert.True(tab.ShowOutside);                 // the box survives the recompute
        Assert.NotNull(tab.Current.Overlay);           // and its layer is on the new original
    }

    [Fact]
    public void R178_SaveAsProposesTheInputsKindAndWritesTheCoresFile()
    {
        using var ws = new TempWorkspace();
        var tab = new GamutFitTabViewModel();
        tab.Populate(Result(ws, withProfile: true, inputName: "calea-lactee.tif"));
        tab.Compute(400, 300);
        Assert.True(tab.HasGallery, tab.Message);
        Assert.Equal(ws.Dir, tab.InputFolder);
        tab.Next.Execute(null);

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
        tab.Compute(400, 300);
        Assert.False(tab.HasGallery);
        Assert.True(tab.IsProblem);
        Assert.Equal(ProofTexts.NoProfile, tab.Message);
        Assert.Contains("Din profil ICC...", tab.Message);

        // A larger area does not run the computation again: nothing to refine (ADR 0084). The
        // refusal is computed at a size whose step is 4 on the 256 px frame, and the area then
        // allows a step of 1: without the rule a computation would start here.
        tab.Compute(64, 48);
        Assert.True(tab.IsProblem);
        tab.SetGallerySize(256, 192);
        Assert.False(tab.IsBusy);
        Assert.True(tab.IsProblem);
    }

    [Fact]
    public void R178_TheTabRendersWithNoBindingErrors()
        => _sta.Invoke(() =>
        {
            using var ws = new TempWorkspace();
            using var errors = new WindowSmokeTests.BindingErrors();
            var tab = new GamutFitTabViewModel();
            tab.Populate(Result(ws, withProfile: true));
            tab.Compute(400, 300);
            tab.ShowOutside = true;
            WindowSmokeTests.Render(new GamutFitView { DataContext = tab }, name: "tab-culori-hartie");
            Assert.Equal("", errors.Text);
        });
}
