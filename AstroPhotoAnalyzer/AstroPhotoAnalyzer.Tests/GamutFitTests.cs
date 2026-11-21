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

// GamutFitTests - the paper fit (R-175), its variants and evidence (R-176) and the files it
// writes (R-177, ADR 0083). The fit's rule is injected as a function of L*, a*, b*, so the
// tests know the largest chroma the "paper" holds; the real rule comes from the profile
// (GamutAnalyzer) and is measured on the reference image (V-016).

using System.Text;

namespace AstroPhotoAnalyzer.Tests;

public class GamutFitTests
{
    /// <summary>
    /// A paper that holds a chroma of 10 from L* 30 up, 5 from L* 10 up, next to nothing (0.02)
    /// between L* 8 and 10, and cannot reach any tone under L* 8: the whole difference is 0
    /// inside, 4 plus the excess outside, 6 for a tone out of reach.
    /// </summary>
    static double FakeRule(double l, double a, double b)
    {
        double c = Math.Sqrt(a * a + b * b);
        if (l < 8) return 6;
        double cmax = Cmax(l);
        return c > cmax ? 4 + (c - cmax) : 0;
    }

    static double Cmax(double l) => l >= 30 ? 10 : l >= 10 ? 5 : 0.02;

    /// <summary>A one-row raster whose pixels are the given D50 Lab colours.</summary>
    static Raster RasterOf(params (double L, double A, double B)[] colours)
    {
        var r = new Raster(1, colours.Length, 3);
        for (int i = 0; i < colours.Length; i++)
        {
            var (red, green, blue) = ColorMath.SrgbOfLabD50(colours[i].L, colours[i].A, colours[i].B);
            r.Planes[0][i] = (float)red; r.Planes[1][i] = (float)green; r.Planes[2][i] = (float)blue;
        }
        return r;
    }

    static (double L, double A, double B) LabAt(Raster r, int i) => ColorMath.LabD50OfSrgb(r.Planes[0][i], r.Planes[1][i], r.Planes[2][i]);

    static double ChromaOf((double L, double A, double B) lab) => Math.Sqrt(lab.A * lab.A + lab.B * lab.B);

    static double HueOf((double L, double A, double B) lab) => Math.Atan2(lab.B, lab.A) * 180 / Math.PI;

    [Fact]
    public void R175_TheFitReducesTheChromaAloneToWhatThePaperHoldsAndCountsTheToneOutOfReach()
    {
        GamutFitTable t = GamutFitTable.Build(FakeRule);
        Assert.Null(t.Problem);
        // Inside, outside above and under L* 30, hues around the circle, one tone out of reach,
        // and one whose edge is next to nothing but whose grey the paper keeps: fitted to grey,
        // not left as out of reach (the second review of 2026-11-06).
        Raster r = RasterOf((50, 4, 3), (50, 16, 0), (50, 0, -18), (50, -12, 12), (20, 6, 6), (20, -2, 1), (5, 10, 0), (9, 0.5, 0));
        GamutFitOutcome o = GamutFit.Apply(r, t, knee: 1, keepFacts: true);
        Raster f = o.Image;
        for (int i = 0; i < r.W; i++)
        {
            var before = LabAt(r, i);
            var after = LabAt(f, i);
            Assert.InRange(after.L, before.L - 0.15, before.L + 0.15);
            if (before.L < 8)
            {
                // Out of reach: untouched.
                Assert.InRange(ChromaOf(after), ChromaOf(before) - 0.15, ChromaOf(before) + 0.15);
                continue;
            }
            double want = Math.Min(ChromaOf(before), Cmax(before.L));
            Assert.InRange(ChromaOf(after), want - 0.3, want + 0.3);
            if (ChromaOf(before) > 1) Assert.InRange(HueOf(after), HueOf(before) - 1, HueOf(before) + 1);
        }
        Assert.Equal(new[] { false, true, true, true, true, false, true, true }, o.OutsideBefore);
        Assert.Equal(new[] { false, false, false, false, false, false, true, false }, o.OutsideAfter);
        Assert.Equal(100.0 * 6 / 8, o.PctOutsideBefore, 3);
        Assert.Equal(100.0 / 8, o.PctOutsideAfter, 3);
        Assert.Equal(100.0 / 8, o.PctUnreachable, 3);
        Assert.Equal(100.0 * 5 / 8, o.PctTouched, 3);
    }

    [Fact]
    public void R175_TheTableIsReadBetweenItsNodesAndTheExactRuleDecidesNearTheEdge()
    {
        // A paper whose largest chroma grows with the hue, 5 at 0 degrees to 15 at 360, and with
        // L* by a tenth, plus a wave of 0.3 whose zeros fall on the table's nodes (whatever their
        // count), so the table sees a straight edge and misses the true one by up to 0.3 between
        // its nodes: a pixel landed on the edge must be decided by the rule, not by the table.
        static double Cmax(double l, double hDeg) => 5 + 10 * hDeg / 360 + l / 10 + 0.3 * Math.Sin(l * Math.PI * (Thresholds.GamutFitLstarNodes - 1) / 100);
        static double Rule(double l, double a, double b)
        {
            double h = Math.Atan2(b, a) * 180 / Math.PI; if (h < 0) h += 360;
            double cmax = Cmax(l, h), c = Math.Sqrt(a * a + b * b);
            return c > cmax ? 4 + (c - cmax) : 0;
        }
        GamutFitTable t = GamutFitTable.Build(Rule);
        var rng = new Random(7);
        var colours = new List<(double, double, double)>();
        for (int i = 0; i < 400; i++)
        {
            double l = 10 + 70 * rng.NextDouble(), h = 360 * rng.NextDouble() * Math.PI / 180;
            double cmax = Cmax(l, h * 180 / Math.PI), c = cmax * (0.8 + 0.4 * rng.NextDouble());
            colours.Add((l, c * Math.Cos(h), c * Math.Sin(h)));
        }
        Raster r = RasterOf(colours.ToArray());
        GamutFitOutcome o = GamutFit.Apply(r, t, knee: 1, keepFacts: true);
        Assert.Equal(0, o.PctOutsideAfter);
        for (int i = 0; i < r.W; i++)
        {
            if (!o.OutsideBefore[i]) continue;
            var after = LabAt(o.Image, i);
            // Fitted onto the edge, not far inside it: the rule of the pixel itself.
            Assert.InRange(Rule(after.L, after.A, after.B), 0, 0.001);
            double h = Math.Atan2(after.B, after.A) * 180 / Math.PI; if (h < 0) h += 360;
            double cmax = Cmax(after.L, h);
            Assert.InRange(ChromaOf(after), cmax - 0.04, cmax + 0.001);   // the search's last step is 0.02
        }
    }

    /// <summary>
    /// CR-001: the judge, a step function of the 8-bit colour in the run, can disagree with the
    /// table both ways, and it is the judge the Soft proof tab shows. A pixel it rejects far
    /// inside the table's edge was never asked about, so it was not counted, not moved and not
    /// counted after; a pixel it keeps past the edge was counted and cut.
    /// </summary>
    [Fact]
    public void R175_TheJudgeDecidesEveryPixelWhateverTheTableSays()
    {
        // The table: a paper that holds a chroma of 10 from L* 30 up. The judge agrees, except
        // around L* 50, where it rejects every chroma over 6, and around L* 70, where it keeps
        // every chroma up to 12.
        static bool Judge(double l, double a, double b)
        {
            double c = Math.Sqrt(a * a + b * b);
            if (l > 45 && l < 55) return c > 6;
            if (l > 65 && l < 75) return c > 12;
            return c > 10;
        }
        GamutFitTable t = GamutFitTable.Build(FakeRule);
        t.Judge = Judge;
        // As the run sets it: the judge asked about the pixel's own sRGB.
        t.JudgeOfPixel = (red, green, blue) =>
        {
            var (l, a, b) = ColorMath.LabD50OfSrgb(red, green, blue);
            return Judge(l, a, b);
        };
        // Rejected inside the table; kept past it; kept inside it; rejected past it.
        Raster r = RasterOf((50, 8, 0), (70, 11, 0), (60, 8, 0), (60, 11, 0));

        GamutFitOutcome o = GamutFit.Apply(r, t, knee: 1, keepFacts: true);

        Assert.Equal(new[] { true, false, false, true }, o.OutsideBefore);
        Assert.Equal(new[] { false, false, false, false }, o.OutsideAfter);
        Assert.Equal(50, o.PctOutsideBefore, 3);
        Assert.Equal(50, o.PctTouched, 3);
        Assert.Equal(0, o.PctUnreachable);
        // Landed where the judge keeps it, just under its own edge of 6 (the search's last step
        // from zero is a tenth), with the tone and the hue as they were.
        var landed = LabAt(o.Image, 0);
        Assert.InRange(ChromaOf(landed), 5.8, 6.0);
        Assert.InRange(landed.L, 49.85, 50.15);
        Assert.InRange(HueOf(landed), -1, 1);
        Assert.InRange(ChromaOf(LabAt(o.Image, 3)), 9.9, 10.0);
        // The two the judge keeps are the input's own samples, the one past the table's edge too.
        foreach (int i in new[] { 1, 2 })
            for (int p = 0; p < 3; p++) Assert.Equal(r.Planes[p][i], o.Image.Planes[p][i]);

        // The recipe counts the same two pixels outside, at L* 50 and L* 60: its band ends over
        // L* 60 and holds three of the four pixels (the one at L* 70 is not outside).
        GamutFitRecipe recipe = GamutFit.Recipe(r, t);
        Assert.InRange(recipe.LstarCeiling, 60, 62);
        Assert.Equal(75, recipe.PctInBand, 3);
    }

    /// <summary>
    /// CR-002: a grey pixel has no chroma to reduce, so the fit copies it; it was copied before
    /// anything was asked about its tone, and so counted nowhere, while the Soft proof tab
    /// paints it red and a neighbour with some chroma at the same tone was counted three times.
    /// </summary>
    [Fact]
    public void R175_AGreyPixelWhoseToneIsOutOfReachIsCountedAndLeftAsItIs()
    {
        // The fake paper reaches no tone under L* 8. A grey and a coloured pixel under it, a grey
        // and a coloured pixel well inside.
        GamutFitTable t = GamutFitTable.Build(FakeRule);
        Raster r = RasterOf((5, 0, 0), (5, 10, 0), (50, 0, 0), (50, 4, 3));
        Assert.True(ChromaOf(LabAt(r, 0)) < Thresholds.GamutFitMinChroma, "the first pixel is grey for the fit");

        GamutFitOutcome o = GamutFit.Apply(r, t, knee: 1, keepFacts: true);

        Assert.Equal(new[] { true, true, false, false }, o.OutsideBefore);
        Assert.Equal(new[] { true, true, false, false }, o.OutsideAfter);
        Assert.Equal(50, o.PctOutsideBefore, 3);
        Assert.Equal(50, o.PctOutsideAfter, 3);
        Assert.Equal(50, o.PctUnreachable, 3);
        Assert.Equal(0, o.PctTouched);
        Assert.Equal(100, o.PctOutsideUnderLowSeparation, 3);
        for (int p = 0; p < 3; p++) Assert.Equal(r.Planes[p], o.Image.Planes[p]);

        // The recipe: both are beyond a mask, so the factor stays at 1 and all of them are left.
        GamutFitRecipe recipe = GamutFit.Recipe(r, t);
        Assert.Equal(1, recipe.Factor);
        Assert.Equal(100, recipe.PctLeft, 3);

        // Without the facts (the save of a whole image) the pixel is copied all the same.
        Raster saved = GamutFit.Apply(r, t, knee: 1, keepFacts: false).Image;
        for (int p = 0; p < 3; p++) Assert.Equal(r.Planes[p], saved.Planes[p]);
    }

    /// <summary>
    /// The review of the branch (2026-11-14): a pixel the judge keeps at a tone whose grey it
    /// rejects has no edge under it (the search answers -1), so no chroma nearer the grey is
    /// known to be inside. The soft knees took its own chroma as the edge and pulled it toward
    /// that grey: inside before, moved for nothing.
    /// </summary>
    [Fact]
    public void R175_APixelTheJudgeKeepsOverAGreyItRejectsIsLeftByEveryVariant()
    {
        // Under L* 8 this judge rejects the grey and every chroma under 3, and keeps the rest.
        static bool Judge(double l, double a, double b)
        {
            double c = Math.Sqrt(a * a + b * b);
            return l < 8 ? c < 3 : c > Cmax(l);
        }
        GamutFitTable t = GamutFitTable.Build(FakeRule);
        t.Judge = Judge;
        Raster r = RasterOf((5, 10, 0), (50, 4, 3));

        foreach (double knee in GamutFitRun.Knees)
        {
            GamutFitOutcome o = GamutFit.Apply(r, t, knee, keepFacts: true);

            Assert.Equal(new[] { false, false }, o.OutsideBefore);
            Assert.Equal(new[] { false, false }, o.OutsideAfter);
            Assert.Equal(0, o.PctTouched);
            for (int p = 0; p < 3; p++) Assert.Equal(r.Planes[p], o.Image.Planes[p]);
        }
    }

    /// <summary>
    /// The second review of the branch (2026-11-14): a pixel without an edge (positive infinity,
    /// from <c>Decide</c>) keeps its chroma under every knee the function documents, 0 included,
    /// where 0 x infinity gave NaN.
    /// </summary>
    [Fact]
    public void R175_WithoutAnEdgeTheKneeKeepsTheChromaAtEveryKnee()
    {
        foreach (double knee in new[] { 0, 0.5, 0.8, 0.9, 1 })
            Assert.Equal(10, GamutFit.Knee(10, double.PositiveInfinity, knee));
    }

    [Fact]
    public void R176_TheThreeVariantsTouchMoreAsTheKneeComesDownAndTheEvidenceSaysHowMuch()
    {
        GamutFitTable t = GamutFitTable.Build(FakeRule);
        // At L* 50 the paper holds 10: a ramp of chroma from 0.05 to 20.05, 101 pixels, none
        // exactly on the edge (a pixel on it is decided by the search's last step).
        var colours = new List<(double, double, double)>();
        for (int i = 0; i <= 100; i++) colours.Add((50, 0.2 * i + 0.05, 0));
        Raster r = RasterOf(colours.ToArray());

        GamutFitOutcome exact = GamutFit.Apply(r, t, Thresholds.GamutFitKneeExact, keepFacts: true);
        GamutFitOutcome soft = GamutFit.Apply(r, t, Thresholds.GamutFitKneeSoft, keepFacts: true);
        GamutFitOutcome softer = GamutFit.Apply(r, t, Thresholds.GamutFitKneeSofter, keepFacts: true);

        // Exact touches the 51 pixels over 10 (from 10.05); soft from 9 (from 9.05: 56), softer from 8 (61).
        Assert.InRange(exact.PctTouched, 100.0 * 50 / 101, 100.0 * 52 / 101);
        Assert.InRange(soft.PctTouched, 100.0 * 55 / 101, 100.0 * 57 / 101);
        Assert.InRange(softer.PctTouched, 100.0 * 60 / 101, 100.0 * 62 / 101);
        foreach (GamutFitOutcome o in new[] { exact, soft, softer })
        {
            Assert.Equal(100.0 * 51 / 101, o.PctOutsideBefore, 2);
            Assert.Equal(0, o.PctOutsideAfter);
            // Nothing leaves the edge: the soft knees stay under it, and a ramp stays a ramp.
            double last = -1;
            for (int i = 0; i <= 100; i++)
            {
                double c = ChromaOf(LabAt(o.Image, i));
                Assert.InRange(c, 0, 10.3);
                Assert.True(c >= last - 0.05, $"the chroma falls back at pixel {i}: {c} after {last}");
                last = c;
            }
        }
        // The soft knee keeps steps just past the edge where the hard one flattens: the pixels
        // at 10.05 and 11.05 differ in chroma under the knee, not under the hard fit. Far past
        // the edge every variant ends flat: the knee has only a tenth of the edge to spend.
        double hardSpread = ChromaOf(LabAt(exact.Image, 55)) - ChromaOf(LabAt(exact.Image, 50));
        double softSpread = ChromaOf(LabAt(soft.Image, 55)) - ChromaOf(LabAt(soft.Image, 50));
        Assert.InRange(hardSpread, -0.05, 0.05);
        Assert.True(softSpread > 0.15, $"the soft knee spread is {softSpread}");

        // The evidence: the chroma given up and its 99 % mark, the dark share, the recipe.
        Assert.InRange(exact.ChromaLostMedian, 4.5, 5.5);     // the touched pixels lose 0.2..10, median about 5
        Assert.InRange(exact.ChromaLostP99, 9.5, 10.2);     // 20.05 - 10, within the search's last step
        Assert.Equal(0, exact.PctOutsideUnderLowSeparation);  // every pixel outside sits at L* 50
        GamutFitRecipe recipe = GamutFit.Recipe(r, t);
        Assert.InRange(recipe.LstarCeiling, 50, 51);          // the band holding 90 % of the pixels outside
        Assert.InRange(recipe.Factor, 0.45, 0.50);            // 20.05 x 0.5 = 10.03 sits on the edge: 0.50 or the step under it
        Assert.InRange(recipe.PctLeft, 0, 1);
    }

    /// <summary>
    /// A paper whose whole difference grows with the chroma alone, 0.4 per unit: the warning's
    /// limit (4) is passed at a chroma of 10, the fit's target (3.5) at 8.75.
    /// </summary>
    static double GradedRule(double l, double a, double b) => 0.4 * Math.Sqrt(a * a + b * b);

    /// <summary>Sets on a table the warning a rule gives at the warning's own limit, as the run does.</summary>
    static void WarnBy(GamutFitTable t, Func<double, double, double, double> rule)
    {
        t.Warning = (l, a, b) => rule(l, a, b) > Thresholds.CurveGamutDeltaE;
        t.WarningOfPixel = (red, green, blue) =>
        {
            var (l, a, b) = ColorMath.LabD50OfSrgb(red, green, blue);
            return rule(l, a, b) > Thresholds.CurveGamutDeltaE;
        };
    }

    [Fact]
    public void R187_TheFitLandsInsideItsTargetAndTheSharesStayTheWarnings()
    {
        Assert.True(Thresholds.GamutFitTargetDeltaE < Thresholds.CurveGamutDeltaE, "the target is a reserve under the warning's limit");
        double targetEdge = Thresholds.GamutFitTargetDeltaE / 0.4, warningEdge = Thresholds.CurveGamutDeltaE / 0.4;
        GamutFitTable t = GamutFitTable.Build(GradedRule);
        Assert.Equal(Thresholds.GamutFitTargetDeltaE, t.Limit);
        WarnBy(t, GradedRule);
        // Well inside; under the target but past the soft knee's start; between the target and
        // the warning; past the warning.
        double between = (targetEdge + warningEdge) / 2;
        Raster r = RasterOf((50, 5, 0), (50, targetEdge - 0.5, 0), (50, between, 0), (50, warningEdge + 2, 0));

        GamutFitOutcome exact = GamutFit.Apply(r, t, Thresholds.GamutFitKneeExact, keepFacts: true);

        // The shares and the masks are the warning's: only the last pixel is outside before.
        Assert.Equal(new[] { false, false, false, true }, exact.OutsideBefore);
        Assert.Equal(new[] { false, false, false, false }, exact.OutsideAfter);
        Assert.Equal(25, exact.PctOutsideBefore, 3);
        Assert.Equal(0, exact.PctOutsideAfter);
        Assert.Equal(0, exact.PctUnreachable);
        // The fit moves the two pixels over the target, the one the warning keeps too, and lands
        // both on the target's edge (the search's last step is 0.02), tone and hue as they were.
        Assert.Equal(50, exact.PctTouched, 3);
        foreach (int i in new[] { 2, 3 })
        {
            var landed = LabAt(exact.Image, i);
            Assert.InRange(ChromaOf(landed), targetEdge - 0.05, targetEdge + 0.01);
            Assert.InRange(landed.L, 49.85, 50.15);
            Assert.InRange(HueOf(landed), -1, 1);
        }
        foreach (int i in new[] { 0, 1 })
            for (int p = 0; p < 3; p++) Assert.Equal(r.Planes[p][i], exact.Image.Planes[p][i]);

        // The knees compress towards the target's edge, not the warning's: the pixel just under
        // the target is eased by the soft knee, and nothing ends past the target.
        GamutFitOutcome soft = GamutFit.Apply(r, t, Thresholds.GamutFitKneeSoft, keepFacts: true);
        Assert.True(ChromaOf(LabAt(soft.Image, 1)) < targetEdge - 0.5 - 0.01, "the soft knee eases the pixel under the target: " + ChromaOf(LabAt(soft.Image, 1)));
        for (int i = 0; i < r.W; i++) Assert.InRange(ChromaOf(LabAt(soft.Image, i)), 0, targetEdge + 0.01);
        Assert.Equal(new[] { false, false, false, true }, soft.OutsideBefore);
        Assert.Equal(75, soft.PctTouched, 3);

        // The save of a whole image, without the facts, writes the same colours.
        Raster saved = GamutFit.Apply(r, t, Thresholds.GamutFitKneeExact, keepFacts: false).Image;
        for (int p = 0; p < 3; p++) Assert.Equal(exact.Image.Planes[p], saved.Planes[p]);

        // The recipe for a hand-made mask aims at the target too: its factor brings the last
        // pixel within the target's edge, a step or less under what that needs, where the
        // warning's edge would be reached two steps earlier.
        GamutFitRecipe recipe = GamutFit.Recipe(r, t);
        double needed = targetEdge / (warningEdge + 2);
        Assert.InRange(recipe.Factor, needed - Thresholds.GamutFitRecipeStep, needed);
        Assert.True(recipe.Factor < warningEdge / (warningEdge + 2) - Thresholds.GamutFitRecipeStep + 1e-9, "the warning's edge would take a larger factor: " + recipe.Factor);
        Assert.InRange(recipe.LstarCeiling, 50, 52);

        // What the recipe would leave is a share of the pixels past the target, not of those the
        // warning marks: ten in the band (five between the two limits, five past the warning)
        // and one bright pixel above it, which no mask on the band reaches. One of eleven is
        // left; of the six the warning marks it would be one of six.
        var many = new List<(double, double, double)>();
        for (int i = 0; i < 5; i++) { many.Add((50, between, 0)); many.Add((50, warningEdge + 2, 0)); }
        many.Add((90, warningEdge + 2, 0));
        GamutFitRecipe banded = GamutFit.Recipe(RasterOf(many.ToArray()), t);
        Assert.InRange(banded.LstarCeiling, 50, 52);
        Assert.Equal(100.0 / 11, banded.PctLeft, 3);
    }

    [Fact]
    public void R187_AToneTheTargetCannotReachIsStillBroughtInsideTheWarning()
    {
        // Under L* 10 the grey itself is past the warning (4.5); from 10 to 20 it sits between
        // the target and the warning (3.8); above, the paper holds every grey. The chroma adds
        // 0.1 per unit everywhere.
        static double Rule(double l, double a, double b) => (l < 10 ? 4.5 : l < 20 ? 3.8 : 0) + 0.1 * Math.Sqrt(a * a + b * b);
        GamutFitTable t = GamutFitTable.Build(Rule);
        WarnBy(t, Rule);
        // At L* 15: 3.9, which the warning keeps, and 4.3, which it marks and a chroma of 2 would
        // bring back to 4. At L* 5: nothing brings it in. At L* 50: inside. Then two greys: at
        // L* 15, past the target alone, and at L* 5, past the warning.
        Raster r = RasterOf((15, 1, 0), (15, 5, 0), (5, 5, 0), (50, 5, 0), (15, 0, 0), (5, 0, 0));
        Assert.True(ChromaOf(LabAt(r, 4)) < Thresholds.GamutFitMinChroma && ChromaOf(LabAt(r, 5)) < Thresholds.GamutFitMinChroma, "the last two pixels are grey for the fit");

        foreach (double knee in GamutFitRun.Knees)
        {
            GamutFitOutcome o = GamutFit.Apply(r, t, knee, keepFacts: true);

            // Counted by the warning: the grey past the target alone is counted nowhere.
            Assert.Equal(new[] { false, true, true, false, false, true }, o.OutsideBefore);
            Assert.Equal(new[] { false, false, true, false, false, true }, o.OutsideAfter);
            Assert.Equal(100.0 * 2 / 6, o.PctUnreachable, 3);
            Assert.Equal(100.0 / 6, o.PctTouched, 3);
            // The one the warning marks lands inside the warning's own edge, a chroma of 2 (the
            // search from zero ends within 0.1 of it); the soft knees end under it.
            var landed = LabAt(o.Image, 1);
            Assert.InRange(ChromaOf(landed), knee >= 1 ? 1.85 : 1.0, 2.01);
            Assert.InRange(landed.L, 14.85, 15.15);
            // The others are the input's own samples: the one past the target alone is not moved
            // toward a grey that is no better, and is counted nowhere.
            foreach (int i in new[] { 0, 2, 3, 4, 5 })
                for (int p = 0; p < 3; p++) Assert.Equal(r.Planes[p][i], o.Image.Planes[p][i]);

            // The save of a whole image, without the facts, asks the warning about these tones
            // all the same: the file holds the preview's colours.
            Raster saved = GamutFit.Apply(r, t, knee, keepFacts: false).Image;
            for (int p = 0; p < 3; p++) Assert.Equal(o.Image.Planes[p], saved.Planes[p]);
        }
    }

    [Fact]
    public void R187_AGamutTableAnswersAtItsOwnLimitWithACacheOfItsOwn()
    {
        // Every colour comes back 3.8 lighter: inside the warning's 4, past the target.
        var warning = new GamutTable { N = 2, Dl = new float[8], Da = new float[8], Db = new float[8] };
        Array.Fill(warning.Dl, 3.8f);
        Assert.Equal(Thresholds.CurveGamutDeltaE, warning.Limit);
        GamutTable target = warning.WithLimit(Thresholds.GamutFitTargetDeltaE);
        Assert.Equal(Thresholds.GamutFitTargetDeltaE, target.Limit);
        Assert.Same(warning.Dl, target.Dl);
        Assert.NotSame(warning.Cache, target.Cache);

        Assert.False(GamutAnalyzer.Outside(warning, 0.5f, 0.4f, 0.3f));
        Assert.True(GamutAnalyzer.Outside(target, 0.5f, 0.4f, 0.3f));

        // With a profile behind it the exact trip decides near each limit, once per 8-bit colour:
        // the same colour is kept in one cache and rejected in the other.
        int trips = 0;
        warning.Exact = (red, green, blue) => { Interlocked.Increment(ref trips); return (3.8, 0, 0); };
        target = warning.WithLimit(Thresholds.GamutFitTargetDeltaE);
        Assert.False(GamutAnalyzer.Outside(warning, 0.5f, 0.4f, 0.3f));
        Assert.True(GamutAnalyzer.Outside(target, 0.5f, 0.4f, 0.3f));
        Assert.True(GamutAnalyzer.Outside(target, 0.5f, 0.4f, 0.3f));
        Assert.Equal(2, trips);
        Assert.False(Assert.Single(warning.Cache).Value);
        Assert.True(Assert.Single(target.Cache).Value);
    }

    [Fact]
    public void R187_TheRunFitsToTheTargetAndCountsByTheWarning()
    {
        using var ws = new TempWorkspace();
        string icc = ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20));
        // The hand-built paper prints every colour as the grey of its tone, so the whole
        // difference is about the chroma. A tint whose difference lies between the target and
        // the warning, chosen among the 8-bit colours (the judge decides on those near a limit).
        GamutTable gamut = GamutAnalyzer.Build(icc);
        Assert.Null(gamut.Problem);
        (double R, double G, double B)? tint = null;
        for (int red = 120; red <= 140 && tint == null; red++)
            for (int green = 110; green <= 119 && tint == null; green++)
            {
                var (l, a, b) = ColorMath.LabD50OfSrgb(red / 255.0, green / 255.0, 119 / 255.0);
                double d = gamut.ExactLab(l, a, b);
                if (d > Thresholds.GamutFitTargetDeltaE + 0.1 && d < Thresholds.CurveGamutDeltaE - 0.1) tint = (red / 255.0, green / 255.0, 119 / 255.0);
            }
        Assert.True(tint != null, "an 8-bit tint between the target and the warning exists on the test paper");
        var (tr, tg, tb) = tint.Value;
        // Three bands: saturated (past the warning), the tint, grey.
        string input = ws.WriteFile("benzi.fit", FitsTestData.Build(96, 64, 3,
            (x, y, c) => x < 32 ? (c == 0 ? 0.6 : 0.2) : x < 64 ? (c == 0 ? tr : c == 1 ? tg : tb) : 0.3, bitpix: -32));
        Parameters p = Scenes.OnBuiltForPaper().WithDmaxFromProfile(icc, out string error) ?? throw new Exception(error);
        AnalysisResult analysis = AnalysisRun.Execute(input, p);

        GamutFitSet set = GamutFitRun.Execute(analysis);

        Assert.Null(set.Problem);
        Assert.Equal(Thresholds.GamutFitTargetDeltaE, set.Table.Limit);
        // The red layer of the original is the warning's: the saturated band, not the tint.
        int w = set.OriginalPreview.W, mid = set.OriginalPreview.H / 2 * w;
        Assert.True(set.OriginalOutside[mid + w / 6]);
        Assert.False(set.OriginalOutside[mid + w / 2]);
        Assert.False(set.OriginalOutside[mid + 5 * w / 6]);
        // The share before is the Soft proof tab's own, a third; the hard fit changes two thirds.
        GamutFitVariant exact = set.Variants[0];
        Assert.Equal(ProofRun.Prepare(analysis).PctPaper, exact.Evidence.PctOutsideBefore, 1);
        Assert.InRange(exact.Evidence.PctOutsideBefore, 32, 35);
        Assert.InRange(exact.Evidence.PctTouched, 65, 68);
        Assert.Equal(0, exact.Evidence.PctOutsideAfter);
        Assert.Equal(0, exact.Evidence.PctUnreachable);

        // The saved file: both coloured bands end within the target by the profile's own rule,
        // the tint moved although the warning never marked it; the grey band is untouched.
        string target = ws.PathTo("benzi_Exacta.fit");
        GamutFitExport.Save(analysis, set, exact, target);
        Raster saved = ImageLoader.Load(target);
        foreach (int x in new[] { 16, 48 })
        {
            int i = 32 * saved.W + x;
            var (l, a, b) = ColorMath.LabD50OfSrgb(saved.Planes[0][i], saved.Planes[1][i], saved.Planes[2][i]);
            Assert.InRange(gamut.ExactLab(l, a, b), 0, Thresholds.GamutFitTargetDeltaE + 0.05);
        }
        int tinted = 32 * saved.W + 48;
        Assert.True(Math.Abs(saved.Planes[0][tinted] - tr) > 1e-4 || Math.Abs(saved.Planes[1][tinted] - tg) > 1e-4, "the tint was moved");
        Assert.Equal(0.3f, saved.Planes[0][32 * saved.W + 80], 3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R187_TheEvidenceSaysTheFitStopsALittleInsideThePapersLimit(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        var evidence = new GamutFitEvidence { PctOutsideBefore = 3.04, PctOutsideAfter = 0, PctTouched = 5.52, ChromaLostMedian = 1.43, ChromaLostP99 = 13.2, PctOutsideUnderLowSeparation = 90 };
        var variant = new GamutFitVariant { Name = GamutFitTexts.Name(Thresholds.GamutFitKneeExact), Knee = Thresholds.GamutFitKneeExact, Evidence = evidence };

        IReadOnlyList<(string Text, bool Warning)> rows = GamutFitTexts.EvidenceRows(variant, new GamutFitRecipe { Factor = 1 });

        // The hard fit's own line no longer says that only the colours outside are changed.
        Assert.Contains(english ? "or holds just barely" : "sau le tine la limita", rows[0].Text);
        Assert.Contains(english ? "a little inside the paper's limit" : "putin sub limita hartiei", rows[0].Text);
        // The row after "pixels changed" says where the fit stops and where the warning starts,
        // both numbers from the thresholds, and why more pixels change than the warning marks.
        int changed = rows.ToList().FindIndex(row => row.Text.StartsWith(english ? "pixels changed" : "pixeli schimbati"));
        Assert.True(changed >= 0);
        (string Text, bool Warning) reserve = rows[changed + 1];
        Assert.False(reserve.Warning);
        Assert.Contains(english ? "the fit stops a little inside the paper's limit" : "potrivirea se opreste putin sub limita hartiei", reserve.Text);
        Assert.Contains((english ? "(at " : "(la ") + Fmt.F1(Thresholds.GamutFitTargetDeltaE) + ",", reserve.Text);
        Assert.Contains((english ? "starts at " : "incepe la ") + Fmt.F0(Thresholds.CurveGamutDeltaE) + ")", reserve.Text);
        Assert.Contains("Lightroom", reserve.Text);

        // The recipe's row says its share is of the colours past the fit's limit, not of the
        // warning's count in the rows above.
        string recipeRow = GamutFitTexts.RecipeLine(new GamutFitRecipe { LstarCeiling = 12, Factor = 0.65, PctLeft = 8, PctInBand = 10 });
        Assert.EndsWith(english ? "it would leave 8.0 % of the colours past the limit the fit stops at" : "ar lasa 8.0 % din culorile aflate peste limita la care se opreste potrivirea", recipeRow);

        // When no more pixels changed than the warning marks, the sentence would not be true
        // and is not shown: nothing changed at all, say.
        var still = new GamutFitVariant { Name = variant.Name, Knee = variant.Knee, Evidence = new GamutFitEvidence() };
        Assert.DoesNotContain(GamutFitTexts.EvidenceRows(still, new GamutFitRecipe { Factor = 1 }), row => row.Text.Contains("Lightroom"));
        // Fewer changed than the warning marks (an image of tones out of reach), and a difference
        // the two printed shares do not show.
        foreach (var (touched, before) in new[] { (1.0, 3.0), (1.93, 1.92) })
        {
            var fewer = new GamutFitVariant { Name = variant.Name, Knee = variant.Knee, Evidence = new GamutFitEvidence { PctTouched = touched, PctOutsideBefore = before } };
            Assert.DoesNotContain(GamutFitTexts.EvidenceRows(fewer, new GamutFitRecipe { Factor = 1 }), row => row.Text.Contains("Lightroom"));
        }
    }

    [Fact]
    public void R177_TheTiffWriterWritesSixteenBitsWithTheProfileAndReadsBackToTheSameSamples()
    {
        using var ws = new TempWorkspace();
        var rng = new Random(3);
        // 130 rows: three strips, the last one short, so the offsets and counts live in the heap.
        var r = new Raster(130, 64, 3);
        foreach (float[] plane in r.Planes) for (int i = 0; i < plane.Length; i++) plane[i] = (float)rng.NextDouble();
        byte[] icc = IccTestData.V2("sRGB test");
        string target = ws.PathTo("scris.tif");
        TiffWriter.Save(r, icc, "AstroPhotoAnalyzer: Exacta", target);

        // The tool reads it back: the same samples within one 16-bit level, the same profile.
        Raster back = ImageLoader.Load(target);
        Assert.Equal("TIFF", back.Source.Format);
        Assert.Equal(16, back.Source.BitDepth);
        Assert.Equal(icc, back.Source.Icc);
        for (int p = 0; p < 3; p++)
            for (int i = 0; i < r.W * r.H; i++)
                Assert.InRange(back.Planes[p][i], r.Planes[p][i] - 1.0 / 65535, r.Planes[p][i] + 1.0 / 65535);

        // An independent reader: the tags by hand, little-endian classic TIFF.
        byte[] bytes = File.ReadAllBytes(target);
        Assert.Equal(new byte[] { 0x49, 0x49, 42, 0 }, bytes[..4]);
        var tags = ReadTags(bytes);
        Assert.Equal(64u, tags[256].Value);          // ImageWidth
        Assert.Equal(130u, tags[257].Value);         // ImageLength
        Assert.Equal(new ushort[] { 16, 16, 16 }, Shorts(bytes, tags[258]));   // BitsPerSample
        Assert.Equal(1u, tags[259].Value);           // Compression: none
        Assert.Equal(2u, tags[262].Value);           // Photometric: RGB
        Assert.Equal(3u, tags[277].Value);           // SamplesPerPixel
        Assert.Equal(1u, tags[284].Value);           // PlanarConfiguration: chunky
        Assert.Equal(icc, Bytes(bytes, tags[34675]));   // the ICC profile
        Assert.Equal("AstroPhotoAnalyzer: Exacta", Encoding.ASCII.GetString(Bytes(bytes, tags[270])).TrimEnd('\0'));
        // Three strips of 64, 64 and 2 rows, their offsets and counts in the heap; the first
        // sample of each strip is the raster's red at that strip's first row, as a 16-bit level.
        Assert.Equal(3u, tags[273].Count);
        uint[] offsets = Longs(bytes, tags[273]), counts = Longs(bytes, tags[279]);
        uint rowBytes = (uint)r.W * 6;
        Assert.Equal(new uint[] { TiffWriter.RowsPerStrip * rowBytes, TiffWriter.RowsPerStrip * rowBytes, (130 - 2 * TiffWriter.RowsPerStrip) * rowBytes }, counts);
        for (int s = 0; s < 3; s++)
        {
            ushort red = BitConverter.ToUInt16(bytes, (int)offsets[s]);
            Assert.Equal((int)Math.Round(r.Planes[0][s * TiffWriter.RowsPerStrip * r.W] * 65535), red);
        }
        Assert.Equal(offsets[0] + counts[0], offsets[1]);
        Assert.Equal(offsets[2] + counts[2], (uint)bytes.Length);
    }

    [Fact]
    public void R177_SavingAVariantWritesAFitsOrATiffByTheExtensionAndNeverTheInput()
    {
        using var ws = new TempWorkspace();
        string icc = ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20));
        // A colour FITS with a saturated patch: on the hand-built profile every chroma over 4 is
        // outside (a*, b* print as grey), so the fit has something to do.
        string input = ws.WriteFile("nebuloasa.fit", FitsTestData.Build(96, 64, 3, (x, y, c) => x < 48 ? (c == 0 ? 0.6 : 0.2) : 0.3,
            bitpix: -32, extraCards: new[] { FitsTestData.StringCard("OBJECT", "M8") }));
        Parameters p = Scenes.OnBuiltForPaper().WithDmaxFromProfile(icc, out string error) ?? throw new Exception(error);
        AnalysisResult a = AnalysisRun.Execute(input, p);
        GamutFitSet set = GamutFitRun.Execute(a);
        Assert.Null(set.Problem);
        Assert.Equal(new[] { "Exacta", "Lina", "Foarte lina" }, set.Variants.Select(v => v.Name).ToArray());
        GamutFitVariant v = set.Variants[0];
        Assert.True(v.Evidence.PctOutsideBefore > 40, "the saturated half is outside: " + v.Evidence.PctOutsideBefore);
        Assert.Equal(0, v.Evidence.PctOutsideAfter);

        string fits = ws.PathTo("nebuloasa_Exacta.fit");
        GamutFitExport.Save(a, set, v, fits);
        string header = Encoding.ASCII.GetString(File.ReadAllBytes(fits), 0, 2880 * 2);
        Assert.Contains("OBJECT  = 'M8", header);
        Assert.Contains("BITPIX  =                  -32", header);
        Assert.Contains("HISTORY", header);
        Assert.Contains("Exacta", header);
        Raster fromFits = ImageLoader.Load(fits);
        Assert.Equal(3, fromFits.Planes.Length);
        Assert.True(fromFits.Planes[0][0] < 0.6 && fromFits.Planes[0][0] > 0.2, "the red of the saturated patch was pulled in: " + fromFits.Planes[0][0]);
        Assert.Equal(0.3f, fromFits.Planes[0][95], 3);   // the grey half untouched

        string tiff = ws.PathTo("nebuloasa_Exacta.tif");
        GamutFitExport.Save(a, set, v, tiff);
        Raster fromTiff = ImageLoader.Load(tiff);
        Assert.Equal("TIFF", fromTiff.Source.Format);
        Assert.Equal(16, fromTiff.Source.BitDepth);
        Assert.Equal("sRGB", fromTiff.Source.IccDescription);   // the standard sRGB profile: the FITS had none
        for (int i = 0; i < fromFits.W * fromFits.H; i++)
            Assert.InRange(fromTiff.Planes[0][i], fromFits.Planes[0][i] - 1.0 / 65535, fromFits.Planes[0][i] + 1.0 / 65535);

        byte[] before = File.ReadAllBytes(input);
        var e = Assert.Throws<Exception>(() => GamutFitExport.Save(a, set, v, input));
        Assert.Contains("imaginea de intrare", e.Message);
        Assert.Equal(before, File.ReadAllBytes(input));
        // The proposed name carries the input's kind.
        Assert.Equal("nebuloasa_Exacta.fit", GamutFitTexts.ProposedFileName(input, v.Name));
        Assert.Equal("poza_Lina.tif", GamutFitTexts.ProposedFileName(@"C:\x\poza.tif", "Lina"));
    }

    [Fact]
    public void R188_TheFitsImagesAreTheWholeImageAndTheNoteCallsItACopyOnlyWhenReduced()
    {
        using var ws = new TempWorkspace();
        string icc = ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20));
        string input = ws.WriteFile("nebuloasa.fit", FitsTestData.Build(96, 64, 3, (x, y, c) => x < 48 ? (c == 0 ? 0.6 : 0.2) : 0.3, bitpix: -32));
        Parameters p = Scenes.OnBuiltForPaper().WithDmaxFromProfile(icc, out string error) ?? throw new Exception(error);

        // No area is asked: the images are the image's own size.
        GamutFitSet set = GamutFitRun.Execute(AnalysisRun.Execute(input, p));

        Assert.Null(set.Problem);
        Assert.Equal(1, set.Stride);
        Assert.Equal(96, set.OriginalPreview.W);
        Assert.Equal(64, set.OriginalPreview.H);
        Assert.Equal(96 * 64, set.OriginalOutside.Length);
        Assert.All(set.Variants, v =>
        {
            Assert.Equal(96, v.Preview.W);
            Assert.Equal(64, v.Preview.H);
            Assert.Equal(96 * 64, v.Outside.Length);
        });

        // The note under the gallery: a whole image is not called a copy.
        Assert.EndsWith("(tabul Tonuri). Programul presupune ca pixelii sunt sRGB.", GamutFitTexts.Note(reduced: false));
        Assert.DoesNotContain("copie", GamutFitTexts.Note(reduced: false));
        Assert.EndsWith("Imaginea de aici e o copie micsorata; programul presupune ca pixelii sunt sRGB.", GamutFitTexts.Note(reduced: true));
    }

    [Fact]
    public void R188_AFrameWiderThanAnyAreaIsFittedWholeAndOverTheCapOnItsReduction()
    {
        using var ws = new TempWorkspace();
        string icc = ws.WriteFile("hartie.icc", IccPrinterTestData.V2Lut16("Hartie de test", 20));
        Parameters p = Scenes.OnBuiltForPaper().WithDmaxFromProfile(icc, out string error) ?? throw new Exception(error);
        // 3200 pixels wide: until R-188 the gallery's area (1600 x 900 before the tab is shown)
        // had this frame taken at every second pixel, 1600 wide.
        Raster wide = SyntheticRasters.From(64, 3200, 3, (x, y, c) => x < 1600 ? (c == 0 ? 0.6f : 0.2f) : 0.3f);
        AnalysisResult a = AnalysisRun.Execute(wide, "memorie", p);

        GamutFitSet whole = GamutFitRun.Execute(a);
        Assert.Null(whole.Problem);
        Assert.Equal(1, whole.Stride);
        Assert.Equal(3200, whole.OriginalPreview.W);
        Assert.Equal(64, whole.OriginalPreview.H);
        Assert.All(whole.Variants, v => Assert.Equal(3200, v.Preview.W));
        // The saturated half is outside on the original, and its mask is the image's own.
        Assert.Equal(3200 * 64, whole.OriginalOutside.Length);
        Assert.True(whole.OriginalOutside[10]);
        Assert.False(whole.OriginalOutside[3190]);

        // The cap lowered so that this frame is over it: 204 800 pixels, a step of 2 leaves
        // 1600 x 32, and the previews and the masks are the reduction's.
        GamutFitSet reduced = GamutFitRun.Execute(a, maxPixels: 100_000);
        Assert.Null(reduced.Problem);
        Assert.Equal(2, reduced.Stride);
        Assert.Equal(1600, reduced.OriginalPreview.W);
        Assert.Equal(32, reduced.OriginalPreview.H);
        Assert.Equal(1600 * 32, reduced.OriginalOutside.Length);
        Assert.All(reduced.Variants, v =>
        {
            Assert.Equal(1600, v.Preview.W);
            Assert.Equal(32, v.Preview.H);
            Assert.Equal(1600 * 32, v.Outside.Length);
        });
        // The evidence is measured on the tonal subsample either way: the step does not move it.
        Assert.Equal(whole.Variants[0].Evidence.PctOutsideBefore, reduced.Variants[0].Evidence.PctOutsideBefore);
    }

    static Dictionary<ushort, (ushort Type, uint Count, uint Value, int Offset)> ReadTags(byte[] b)
    {
        int ifd = BitConverter.ToInt32(b, 4);
        int n = BitConverter.ToUInt16(b, ifd);
        var tags = new Dictionary<ushort, (ushort, uint, uint, int)>();
        for (int i = 0; i < n; i++)
        {
            int e = ifd + 2 + 12 * i;
            ushort tag = BitConverter.ToUInt16(b, e), type = BitConverter.ToUInt16(b, e + 2);
            uint count = BitConverter.ToUInt32(b, e + 4);
            uint value = type == 3 && count == 1 ? BitConverter.ToUInt16(b, e + 8) : BitConverter.ToUInt32(b, e + 8);
            tags[tag] = (type, count, value, e + 8);
        }
        return tags;
    }

    static ushort[] Shorts(byte[] b, (ushort Type, uint Count, uint Value, int Offset) t)
    {
        int at = t.Count * 2 <= 4 ? t.Offset : (int)t.Value;
        var o = new ushort[t.Count];
        for (int i = 0; i < t.Count; i++) o[i] = BitConverter.ToUInt16(b, at + 2 * i);
        return o;
    }

    static uint[] Longs(byte[] b, (ushort Type, uint Count, uint Value, int Offset) t)
    {
        if (t.Count == 1) return new[] { t.Value };
        var o = new uint[t.Count];
        for (int i = 0; i < t.Count; i++) o[i] = BitConverter.ToUInt32(b, (int)t.Value + 4 * i);
        return o;
    }

    static byte[] Bytes(byte[] b, (ushort Type, uint Count, uint Value, int Offset) t)
    {
        int at = t.Count <= 4 ? t.Offset : (int)t.Value;
        return b[at..(at + (int)t.Count)];
    }
}
