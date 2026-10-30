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
/// R-160 to R-162: the soft proof (ADR 0066) on the hand-built printer profile, which now
/// carries the tables towards the printer: a grey comes back to itself under the relative
/// intent with the black compensated, the paper's white and black appear under the simulation,
/// the third intent crushes the tones under the paper's black, the perceptual intent uses the
/// profile's own table; the two warnings on a hand-built table; the problems.
/// </summary>
public class SoftProofTests
{
    const double Black = 20;
    static readonly double[] TintedWhite = { 0.90, 0.93, 0.75 };

    static string Profile(TempWorkspace ws, bool withB2A = true, double[]? white = null, bool withPerceptual = true, string name = "hartie.icc")
        => ws.WriteFile(name, IccPrinterTestData.V2Lut16("Hartie de test", Black, withB2A: withB2A, white: white, withPerceptualB2A: withPerceptual));

    static Raster Pixels(params (float R, float G, float B)[] px)
        => SyntheticRasters.From(1, px.Length, 3, (x, _, p) => p == 0 ? px[x].R : p == 1 ? px[x].G : px[x].B);

    static float Grey(double lstar) => (float)(ColorMath.GrayLevelOfLstar(lstar) / 255);

    static (double L, double A, double B) LabOf(Raster r, int x) => ColorMath.LabOfSrgb(r.At(0, 0, x), r.At(1, 0, x), r.At(2, 0, x));

    [Fact]
    public void R160_UnderTheRelativeIntentWithTheSimulationOffAGreyRampComesBackToItself()
    {
        using var ws = new TempWorkspace();
        ProofProfile p = ProofProfile.Open(Profile(ws));
        Assert.Null(p.Problem);
        ProofTable t = p.Table(ProofIntent.Relative);
        Assert.Null(t.Problem);
        Assert.Equal(Thresholds.ProofGrid, t.N);

        Raster ramp = SyntheticRasters.Ramp(1, 256, 0, 1);
        SoftProofResult r = SoftProof.Render(p, t, ramp, simulate: false);
        Assert.Equal(3, r.Image.Planes.Length);
        for (int x = 0; x < 256; x++)
            for (int plane = 0; plane < 3; plane++)
                Assert.True(Math.Abs(r.Image.At(plane, 0, x) - ramp.At(0, 0, x)) <= 1 / 255.0 + 1e-6, $"level {x}, plane {plane}: {r.Image.At(plane, 0, x)}");
    }

    [Fact]
    public void R160_UnderTheSimulationTheWhiteIsThePapersAndTheBlackItsOwn()
    {
        using var ws = new TempWorkspace();
        ProofProfile p = ProofProfile.Open(Profile(ws, white: TintedWhite));
        // The media white's L* is that of its Y, 0.93; a Z lower than D50's makes it yellowish.
        Assert.Equal(ColorMath.LstarOfY(0.93), p.PaperWhiteLab.L, 1);
        Assert.True(p.PaperWhiteLab.B > 0.5, "b* " + p.PaperWhiteLab.B);
        Assert.Equal(Black, p.PaperBlackLstar, 1);
        Assert.Equal(1.52, p.Dmax);   // what IccPaperBlack reads from the same profile (R-028)

        SoftProofResult r = SoftProof.Render(p, p.Table(ProofIntent.Relative), Pixels((1, 1, 1), (0, 0, 0)), simulate: true);
        var white = LabOf(r.Image, 0);
        var black = LabOf(r.Image, 1);
        Assert.Equal(p.PaperWhiteLab.L, white.L, 0.5);
        Assert.True(white.B > 0.5, "the proofed white's b* " + white.B);
        // The paper's black under the absolute scaling: its relative Y times the media white's.
        Assert.Equal(ColorMath.LstarOfY(ColorMath.YOfLstar(Black) * TintedWhite[1]), black.L, tolerance: 0.5);
        // The margin takes the same colour as the proofed white.
        for (int plane = 0; plane < 3; plane++) Assert.Equal(r.Image.At(plane, 0, 0), p.PaperWhiteSrgb[plane], 2);

        // Off, the paper's white and black are the screen's.
        SoftProofResult off = SoftProof.Render(p, p.Table(ProofIntent.Relative), Pixels((1, 1, 1), (0, 0, 0)), simulate: false);
        for (int plane = 0; plane < 3; plane++)
        {
            Assert.Equal(1, off.Image.At(plane, 0, 0), 2);
            Assert.Equal(0, off.Image.At(plane, 0, 1), 2);
        }
    }

    [Fact]
    public void R160_WithoutTheCompensationATonesUnderThePaperBlackIsThatBlack()
    {
        using var ws = new TempWorkspace();
        ProofProfile p = ProofProfile.Open(Profile(ws));
        ProofTable t = p.Table(ProofIntent.RelativeNoBpc);
        Raster px = Pixels((Grey(10), Grey(10), Grey(10)), (0, 0, 0), (Grey(50), Grey(50), Grey(50)));

        SoftProofResult on = SoftProof.Render(p, t, px, simulate: true);
        Assert.Equal(Black, LabOf(on.Image, 0).L, 1);
        Assert.Equal(Black, LabOf(on.Image, 1).L, 1);
        Assert.Equal(50, LabOf(on.Image, 2).L, 1);   // over the black: as it is

        SoftProofResult off = SoftProof.Render(p, t, px, simulate: false);
        Assert.Equal(LabOf(off.Image, 1).L, LabOf(off.Image, 0).L, 1);   // both the same black, the screen's
        Assert.True(LabOf(off.Image, 0).L < 1, "L* " + LabOf(off.Image, 0).L);

        // With the compensation the same grey keeps its place over the black.
        SoftProofResult bpc = SoftProof.Render(p, p.Table(ProofIntent.Relative), px, simulate: false);
        Assert.Equal(10, LabOf(bpc.Image, 0).L, 1);
    }

    [Fact]
    public void R160_ThePerceptualIntentUsesTheProfilesOwnTable()
    {
        using var ws = new TempWorkspace();
        ProofProfile p = ProofProfile.Open(Profile(ws));
        Raster grey = Pixels((Grey(50), Grey(50), Grey(50)));
        // The fixture's perceptual table puts the black at L* 0, so L* 50 lands at 0.5 of the
        // device and prints at 20 + 0.8 x 50 = 60; the compensated relative trip returns ~50.
        double perceptual = LabOf(SoftProof.Render(p, p.Table(ProofIntent.Perceptual), grey, simulate: true).Image, 0).L;
        double relative = LabOf(SoftProof.Render(p, p.Table(ProofIntent.Relative), grey, simulate: true).Image, 0).L;
        Assert.Equal(60, perceptual, tolerance: 0.5);
        Assert.Equal(52.8, relative, tolerance: 0.5);
        Assert.NotSame(p.Table(ProofIntent.Perceptual), p.Table(ProofIntent.Relative));
        Assert.Same(p.Table(ProofIntent.Perceptual), p.Table(ProofIntent.Perceptual));   // built once
    }

    [Fact]
    public void R160_TheTrilinearReadIsExactAtTheNodes()
    {
        Span<int> index = stackalloc int[8];
        Span<float> weight = stackalloc float[8];
        Trilinear.Corners(65, 1, 0, 0.5f, index, weight);
        float sum = 0;
        foreach (float w in weight) sum += w;
        Assert.Equal(1, sum, 5);
        Assert.Contains(weight.ToArray(), w => Math.Abs(w - 1) < 1e-6);
        Assert.Equal(((64 * 65) + 0) * 65 + 32, index[weight.ToArray().ToList().FindIndex(w => Math.Abs(w - 1) < 1e-6)]);
    }

    [Fact]
    public void R160_TheDisplayStepIsTheSrgbOfD50Xyz()
    {
        // A grey of D50 at Y 0.2 is the grey of that Y on screen, neutral; the matrix is the
        // Bradford-adapted sRGB one.
        var p = ProofProfile.ForTests(new[] { 0.9642, 1.0, 0.8249 }, new[] { 0.0, 0.0, 0.0 });
        var (r, g, b) = SoftProof.Display(p, 0.9642 * 0.2, 0.2, 0.8249 * 0.2, simulate: false);
        // A colour the screen cannot show is clipped, never left outside 0..1.
        var (cr, cg, cb) = SoftProof.Display(p, 0.2, 0.6, 0.05, simulate: false);
        Assert.InRange(cr, 0, 1); Assert.InRange(cg, 0, 1); Assert.InRange(cb, 0, 1);
        Assert.Equal(0, cr, 3);
        Assert.Equal(ColorMath.LinearToSrgb(0.2), r, tolerance: 1e-3);
        Assert.Equal(r, g, tolerance: 1e-3);   // neutral within 0.3 of an 8-bit level
        Assert.Equal(r, b, tolerance: 1e-3);
    }

    [Fact]
    public void R161_ThePaperWarningIsTheCurvesTabsRule()
    {
        using var ws = new TempWorkspace();
        string icc = Profile(ws);
        // The fixture prints every colour as the grey of its L*: a saturated red leaves the
        // paper's gamut, a grey does not.
        Raster scene = SyntheticRasters.From(64, 64, 3, (x, y, pl) => x < 32 ? (pl == 0 ? 1f : 0f) : 0.3f);
        Parameters p = Scenes.OnBuiltForPaper().WithDmaxFromProfile(icc, out string error) ?? throw new Exception(error);
        AnalysisResult a = AnalysisRun.Execute(scene, "memorie", p);
        ProofSet set = ProofRun.Prepare(a);
        Assert.Null(set.Problem);
        Assert.Equal(50, set.PctPaper, 6);
        Assert.Equal(scene.W * scene.H, set.PaperOutside.Length);

        Assert.True(set.PaperOutside[0]);
        Assert.False(set.PaperOutside[63]);
        SoftProofResult r = ProofRun.Render(set, ProofIntent.Perceptual, simulate: false);
        Assert.Equal(ProofIntent.Perceptual, r.Intent);
        Assert.False(r.Simulated);
    }

    [Fact]
    public void R160_TheProofIsMadeOnAReductionWithinProofMaxSide()
    {
        using var ws = new TempWorkspace();
        Parameters p = Scenes.OnBuiltForPaper().WithDmaxFromProfile(Profile(ws), out string error) ?? throw new Exception(error);
        Raster wide = SyntheticRasters.Flat(64, Thresholds.ProofMaxSide + 1, 0.3f);
        ProofSet set = ProofRun.Prepare(AnalysisRun.Execute(wide, "memorie", p));
        Assert.Equal((Thresholds.ProofMaxSide + 1) / 2 + 1, set.Reduced.W);   // stride 2
        Assert.Equal(32, set.Reduced.H);
    }

    [Fact]
    public void R162_WithoutAProfileThePrepareSaysHowToGetOne()
    {
        AnalysisResult a = AnalysisRun.Execute(SyntheticRasters.Flat(64, 64, 0.3f), "memorie", Scenes.OnBuiltForPaper());
        ProofSet set = ProofRun.Prepare(a);
        Assert.Contains("Din profil ICC...", set.Problem);
        Assert.Null(set.Profile);
        Assert.Equal(set.Problem, ProofTexts.NoProfile);
    }

    [Fact]
    public void R162_AProfileWithoutATableTowardsThePrinterIsRefusedWithTheReason()
    {
        using var ws = new TempWorkspace();
        ProofProfile p = ProofProfile.Open(Profile(ws, withB2A: false));
        Assert.Contains("B2A", p.Problem);

        Parameters prm = Scenes.OnBuiltForPaper().WithDmaxFromProfile(Profile(ws, withB2A: false, name: "fara.icc"), out string error) ?? throw new Exception(error);
        ProofSet set = ProofRun.Prepare(AnalysisRun.Execute(SyntheticRasters.Flat(64, 64, 0.3f), "memorie", prm));
        Assert.Contains("B2A", set.Problem);

        ProofProfile junk = ProofProfile.Open(ws.WriteFile("junk.icc", new byte[300]));
        Assert.NotNull(junk.Problem);
    }

    [Fact]
    public void R162_AProfileWithoutThePerceptualTableIsRefusedLikeOneWithoutTheColorimetricOne()
    {
        // B2A0 is mandatory in a printer profile and the package refuses a profile without it;
        // the tab says which table is missing rather than proofing on the other one.
        using var ws = new TempWorkspace();
        ProofProfile p = ProofProfile.Open(Profile(ws, withPerceptual: false));
        Assert.Contains("B2A0", p.Problem);
        Assert.Contains("B2A0", p.Table(ProofIntent.Relative).Problem);
        Assert.Throws<ArgumentException>(() => SoftProof.Render(p, p.Table(ProofIntent.Relative), Pixels((0.5f, 0.5f, 0.5f)), false));
    }

    [Fact]
    public void R162_TheFactsAndTheLegendsAreTheCores()
    {
        using var ws = new TempWorkspace();
        ProofProfile p = ProofProfile.Open(Profile(ws, white: TintedWhite));
        Assert.Contains("Hartie de test", ProofTexts.ProfileLine(p));
        Assert.Contains("L* " + Fmt.F1(p.PaperWhiteLab.L), ProofTexts.PaperWhiteLine(p));
        // A chroma that rounds to zero is written 0.0, never -0.0 (the lab's baryta has a* -0.01).
        ProofProfile grey = ProofProfile.Open(Profile(ws, white: new[] { 0.9642 * 0.93 * 0.9999, 0.93, 0.8249 * 0.93 }, name: "gri.icc"));
        Assert.InRange(grey.PaperWhiteLab.A, -0.05, 0);
        Assert.Contains("a* 0.0,", ProofTexts.PaperWhiteLine(grey));
        Assert.DoesNotContain("-0.0", ProofTexts.PaperWhiteLine(grey));
        Assert.Contains("Dmax 1.52", ProofTexts.PaperBlackLine(p));
        Assert.Contains("L* 20.0", ProofTexts.PaperBlackLine(p));
        Assert.Contains("1.4 %", ProofTexts.PaperGamutLegend(1.4));
        Assert.Contains(Fmt.F0(Thresholds.CurveGamutDeltaAb), ProofTexts.PaperGamutLegend(1.4));
        Assert.Equal("Relativ, fara compensarea negrului", ProofTexts.IntentName(ProofIntent.RelativeNoBpc));
        Assert.Contains("Tonuri", ProofTexts.IntentHelp(ProofIntent.RelativeNoBpc));
    }
}
