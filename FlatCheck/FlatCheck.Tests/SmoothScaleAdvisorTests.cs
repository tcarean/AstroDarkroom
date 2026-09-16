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

using static FlatCheck.Tests.SmoothAnalyzerTests;

namespace FlatCheck.Tests;

/// <summary>
/// The texture metric and the scale advisor, on the analyzer tests' synthetic scenes. The
/// ladder is scaled to the 1024-px fixture (25 / 50 / 100 px = sigma 1 / 2 / 4 tiles),
/// the same geometry the real 150 / 300 / 600 has on a 6016-px sensor.
/// </summary>
public class SmoothScaleAdvisorTests
{
    static readonly double[] FixtureLadder = { Tile, 2 * Tile, 4 * Tile };

    // ---------------------------------------------------------------- FineTexture

    [Fact]
    public void FineTexture_ReadsUnderTheThresholdOnACleanNoisyField()
    {
        Img prepared = Img.Prepare(Observed(Truth(blobs: true), Noise()));

        double t = Metrics.FineTexture(prepared);

        Assert.InRange(t, 0, Th.SmoothTextureMin / 2);
    }

    [Fact]
    public void FineTexture_ReadsWellOverTheThresholdOnTheCells()
    {
        Img prepared = Img.Prepare(Observed(Truth(), Cells(), Noise()));

        double t = Metrics.FineTexture(prepared);

        // the fixture's cells are a fraction of a box wide, so only part of them counts as
        // "fine"; on the real master the gap is 12x, here it is what it is - but clearly over
        Assert.True(t > Th.SmoothTextureMin, "texture " + t);
    }

    /// <summary>Every figure in the tool is a ratio: the level of the file cannot matter.</summary>
    [Fact]
    public void FineTexture_IsScaleInvariant()
    {
        Img a = Img.Prepare(Observed(Truth(), Cells(), Noise()));
        var b = new Img(a.H, a.W);
        for (int i = 0; i < a.D.Length; i++) b.D[i] = a.D[i] * 1000;

        Assert.Equal(Metrics.FineTexture(a), Metrics.FineTexture(b), 9);
    }

    [Fact]
    public void BoxBlur_IsTheMeanOfTheBox_TruncatedAtTheEdges()
    {
        var img = TestImages.From(6, 1, (x, y) => x);   // 0 1 2 3 4 5

        Img b = Metrics.BoxBlur(img, 3);

        Assert.Equal(0.5, b.At(0, 0), 9);      // (0+1)/2
        Assert.Equal(2.0, b.At(0, 2), 9);      // (1+2+3)/3
        Assert.Equal(4.5, b.At(0, 5), 9);      // (4+5)/2
    }

    // ---------------------------------------------------------------- the advisor

    [Fact]
    public void ACleanMasterGetsNoTexture_AndNoRunIsMade()
    {
        var a = SmoothScaleAdvisor.Advise(Observed(Truth(blobs: true), Noise()), FixtureLadder);

        Assert.Equal(SmoothAdviceKind.NoTexture, a.Kind);
        Assert.Empty(a.Trials);
        Assert.True(double.IsNaN(a.ScalePx));
        Assert.InRange(a.InputTexturePct, 0, Th.SmoothTextureMin);
    }

    /// <summary>
    /// Cells of the real kind: the first rung leaves texture, the second clears it with
    /// the guards silent, so the second is proposed - the pe-monitor case at fixture scale.
    /// </summary>
    [Fact]
    public void CellsGetTheSmallestRungThatClearsThem()
    {
        var a = SmoothScaleAdvisor.Advise(Observed(Truth(blobs: true), Cells(), Dust(), Noise()), FixtureLadder);

        Assert.Equal(SmoothAdviceKind.Proposed, a.Kind);
        Assert.Equal(3, a.Trials.Count);
        Assert.Equal(FixtureLadder, a.Trials.Select(t => t.ScalePx));
        Assert.True(a.InputTexturePct > Th.SmoothTextureMin);
        Assert.False(a.Trials[0].TextureGone, "rung 1 texture " + a.Trials[0].TextureAfterPct);
        Assert.True(a.Trials[1].TextureGone, "rung 2 texture " + a.Trials[1].TextureAfterPct);
        Assert.All(a.Trials, t => Assert.False(t.StructureLost, $"guard at {t.ScalePx}: az {t.AzimuthalDelta} tilt {t.TiltDelta}"));
        Assert.Equal(FixtureLadder[1], a.ScalePx);
    }

    /// <summary>
    /// Cells plus a real, narrow, off-centre ring of the same size: every rung that clears
    /// the cells takes the ring, the azimuthal asymmetry drops by more than the guard, and
    /// the answer is "undetermined" - not a scale that quietly removes the lens.
    /// </summary>
    [Fact]
    public void CellsAndARealFeatureOfTheSameSizeAreUndetermined()
    {
        double cx = W / 2.0 + 150, cy = H / 2.0 - 90, r0 = 220;
        Func<int, int, double> ring = (x, y) =>
        {
            double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return 1 + 0.04 * Math.Exp(-(r - r0) * (r - r0) / (2 * 12.0 * 12.0));
        };
        Img obs = Observed(Truth(), ring, Cells(), Noise());

        var a = SmoothScaleAdvisor.Advise(obs, FixtureLadder);

        Assert.Equal(SmoothAdviceKind.Undetermined, a.Kind);
        Assert.True(double.IsNaN(a.ScalePx));
        Assert.Contains(a.Trials, t => t.TextureGone);
        Assert.All(a.Trials.Where(t => t.TextureGone), t => Assert.True(t.StructureLost));
    }

    [Fact]
    public void TheTrialsHoldNoRaster()
    {
        var a = SmoothScaleAdvisor.Advise(Observed(Truth(), Cells(), Noise()), FixtureLadder);

        // nothing on the trial is an Img: the advice is figures only (reflection keeps the check honest)
        Assert.DoesNotContain(typeof(SmoothScaleTrial).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public),
            f => f.FieldType == typeof(Img));
        Assert.Equal(3, a.Trials.Count);
    }

    // ---------------------------------------------------------------- Decide, on hand-made trials

    static SmoothScaleTrial T(double scale, double texture, double az, double tilt) => new()
    {
        ScalePx = scale, TextureAfterPct = texture, AzimuthalDelta = az, TiltDelta = tilt,
        TextureGone = texture < Th.SmoothTextureGone,
        StructureLost = !(az > -Th.SmoothScaleLoss) || !(tilt > -Th.SmoothScaleLoss),
    };

    [Fact]
    public void Decide_PicksTheSmallestAcceptableRung()
    {
        var a = SmoothScaleAdvisor.Decide(0.7, new[] { T(150, 0.09, -0.2, -0.1), T(300, 0.02, 0.0, 0.0), T(600, 0.01, -0.2, 0.1) });

        Assert.Equal(SmoothAdviceKind.Proposed, a.Kind);
        Assert.Equal(300, a.ScalePx);
    }

    [Fact]
    public void Decide_IsUndeterminedWhenEveryClearingRungLosesStructure()
    {
        var a = SmoothScaleAdvisor.Decide(0.7, new[] { T(150, 0.09, -0.2, -0.1), T(300, 0.02, -0.8, 0.0), T(600, 0.01, -0.9, -0.6) });

        Assert.Equal(SmoothAdviceKind.Undetermined, a.Kind);
        Assert.True(double.IsNaN(a.ScalePx));
    }

    /// <summary>NaN in a guard figure counts as lost: the conservative side, as everywhere in the tool.</summary>
    [Fact]
    public void ANaNGuardCountsAsStructureLost()
    {
        Assert.True(T(300, 0.02, double.NaN, 0).StructureLost);
        Assert.True(T(300, 0.02, 0, double.NaN).StructureLost);
        Assert.False(T(300, 0.02, -0.49, 0.49).StructureLost);
    }

    [Fact]
    public void TheLadderIsTheThreeChips()
    {
        double d = SmoothAnalyzer.DefaultScalePx;
        Assert.Equal(new[] { d / 2, d, d * 2 }, SmoothScaleAdvisor.Ladder);
    }
}
