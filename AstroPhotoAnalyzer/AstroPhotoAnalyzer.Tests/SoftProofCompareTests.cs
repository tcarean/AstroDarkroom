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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// R-165: the soft proof of a second image, for the comparison of the Soft proof tab (ADR 0091):
/// the same profile and the same tables as the analysed image's, its own mask and share, and the
/// refusals, each a sentence. On the hand-built profile every grey is inside the paper's gamut
/// and every colour outside, which is how the second image's own mask is told from the first's.
/// </summary>
public class SoftProofCompareTests
{
    static string Profile(TempWorkspace ws) => ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20));

    static AnalysisResult Result(string icc)
    {
        Parameters p = Scenes.OnBuiltForPaper().WithDmaxFromProfile(icc, out string error) ?? throw new Exception(error);
        return AnalysisRun.Execute(Scenes.DarkSky(), "memorie", p);
    }

    /// <summary>Left half a mid grey, right half a saturated red: half the pixels outside the fixture's gamut.</summary>
    static Raster HalfGreyHalfRed(int h = 512, int w = 512)
        => SyntheticRasters.From(h, w, 3, (x, _, plane) => x < w / 2 ? 0.5f : plane == 0 ? 0.8f : 0.2f);

    [Fact]
    public void R165_TheOtherImageGoesThroughTheSameProfileWithItsOwnMask()
    {
        using var ws = new TempWorkspace();
        ProofSet first = ProofRun.Prepare(Result(Profile(ws)));
        Assert.Null(first.Problem);

        Raster pixels = HalfGreyHalfRed();
        ProofSet other = ProofRun.PrepareOther(first, pixels, "alta.tif");
        Assert.Null(other.Problem);
        Assert.Same(first.Profile, other.Profile);   // opened once, so each intent's grid is built once
        Assert.Equal(first.Reduced.W, other.Reduced.W);
        Assert.Equal(first.Reduced.H, other.Reduced.H);

        // Its own mask and share: the grey half inside, the red half outside.
        Assert.Equal(512 * 512, other.PaperOutside.Length);
        Assert.False(other.PaperOutside[0]);
        Assert.True(other.PaperOutside[511]);
        Assert.Equal(50.0, other.PctPaper, 6);

        // Proofed by the same table as those pixels rendered directly.
        foreach (ProofIntent intent in Enum.GetValues<ProofIntent>())
        {
            SoftProofResult direct = SoftProof.Render(first.Profile, first.Profile.Table(intent), pixels, simulate: true);
            SoftProofResult run = ProofRun.Render(other, intent, simulate: true);
            Assert.Equal(intent, run.Intent);
            for (int plane = 0; plane < 3; plane++) Assert.Equal(direct.Image.Planes[plane], run.Image.Planes[plane]);
        }

        // A mono image is compared as its grey: nothing outside, three planes in the proof.
        ProofSet mono = ProofRun.PrepareOther(first, SyntheticRasters.Flat(512, 512, 0.5f, planes: 1), "mono.tif");
        Assert.Null(mono.Problem);
        Assert.Equal(0.0, mono.PctPaper);
        Assert.Equal(3, ProofRun.Render(mono, ProofIntent.Relative, simulate: true).Image.Planes.Length);
    }

    [Fact]
    public void R165_TheSamePixelsReadFromAFileGiveTheSameProofAndTheSameShare()
    {
        using var ws = new TempWorkspace();
        ProofSet first = ProofRun.Prepare(Result(Profile(ws)));
        string png = ImageFiles.SavePng16Rgb(Scenes.DarkSky(), ws.PathTo("aceeasi.png"));

        ProofSet other = ProofRun.PrepareOther(first, png);
        Assert.Null(other.Problem);
        Assert.Same(first.Profile, other.Profile);
        Assert.Equal(first.PctPaper, other.PctPaper, 1);

        // The file holds the scene at 16 bits, so the proofs agree within that step's effect.
        SoftProofResult a = ProofRun.Render(first, ProofIntent.Perceptual, simulate: true);
        SoftProofResult b = ProofRun.Render(other, ProofIntent.Perceptual, simulate: true);
        Assert.Equal(a.Image.W, b.Image.W);
        Assert.Equal(a.Image.H, b.Image.H);
        double worst = 0;
        for (int plane = 0; plane < 3; plane++)
            for (int i = 0; i < a.Image.Planes[plane].Length; i++)
                worst = Math.Max(worst, Math.Abs(a.Image.Planes[plane][i] - b.Image.Planes[plane][i]));
        Assert.True(worst < 1e-3, "largest difference " + worst);
    }

    [Fact]
    public void R165_AnotherSizeALinearImageAndAFileThatCannotBeReadAreRefusedWithASentence()
    {
        using var ws = new TempWorkspace();
        ProofSet first = ProofRun.Prepare(Result(Profile(ws)));

        // Another size: both sizes named, the analysed image's first.
        ProofSet small = ProofRun.PrepareOther(first, HalfGreyHalfRed(h: 512, w: 256), "mica.tif");
        Assert.Equal("cele doua imagini nu au aceeasi marime (512 x 512 si 256 x 512 pixeli): comparatia merge doar intre imagini de aceeasi marime", small.Problem);
        Assert.Equal(ProofTexts.SizeMismatch(512, 512, 256, 512), small.Problem);
        Assert.Null(small.Reduced);
        using (Tr.Use(UiLanguage.English))
            Assert.Equal("the two images are not the same size (512 x 512 and 256 x 512 pixels): only images of the same size can be compared",
                ProofTexts.SizeMismatch(512, 512, 256, 512));

        // A linear image: the rule and the sentence of R-015.
        ProofSet linear = ProofRun.PrepareOther(first, SyntheticRasters.Flat(512, 512, 0.001f), "liniara.fit");
        Assert.StartsWith("imaginea pare LINIARA (mediana 0.0010, sub 0.02)", linear.Problem);

        // A file that is not there, and one of a kind the program does not read: the loader's sentences.
        ProofSet missing = ProofRun.PrepareOther(first, ws.PathTo("lipsa.tif"));
        Assert.StartsWith("fisierul nu exista: ", missing.Problem);
        ProofSet unknown = ProofRun.PrepareOther(first, ws.WriteFile("poza.psd", new byte[] { 1, 2, 3 }));
        Assert.StartsWith("format neacceptat: '.psd'", unknown.Problem);

        // A file its reader cannot read: the reader's reason, not an exception.
        ProofSet broken = ProofRun.PrepareOther(first, ws.WriteFile("stricata.tif", new byte[] { 1, 2, 3 }));
        Assert.False(string.IsNullOrWhiteSpace(broken.Problem));

        // The analysed image's set is untouched by any of them.
        Assert.Null(first.Problem);
        Assert.NotNull(first.Reduced);
    }

    [Fact]
    public void R166_TheLegendNamesItsImageAndTheSentenceSaysWhichSideIsWhich()
    {
        Assert.Equal("A.fit: " + ProofTexts.PaperGamutLegend(1.9), ProofTexts.PaperGamutLegend("A.fit", 1.9));
        Assert.StartsWith("A.fit: culori pe care hartia nu le poate reda (gamutul): 1.9 % din pixeli", ProofTexts.PaperGamutLegend("A.fit", 1.9));
        Assert.Equal("In stanga liniei: A.fit, imaginea analizata; in dreapta: B.tif, trecuta prin acelasi profil de hartie, cu aceleasi setari. Tragi de linie ca s-o muti.",
            ProofTexts.CompareSides("A.fit", "B.tif"));
        using (Tr.Use(UiLanguage.English))
            Assert.Equal("Left of the line: A.fit, the image analysed; right of it: B.tif, put through the same paper profile with the same settings. Drag the line to move it.",
                ProofTexts.CompareSides("A.fit", "B.tif"));
    }
}
