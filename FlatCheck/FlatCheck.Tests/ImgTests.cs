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

namespace FlatCheck.Tests;

/// <summary>
/// Phase 4: the raster type itself and <see cref="Img.Divide"/>.
/// The block-mean and downsample transforms live in <see cref="PreprocessingTests"/>.
/// </summary>
public class ImgTests
{
    // ---------------------------------------------------------------- the raster

    /// <summary>
    /// The constructor takes (height, width) in that order, while almost everything else
    /// in the codebase talks about images as w x h. Easy to transpose by accident.
    /// </summary>
    [Fact]
    public void Constructor_TakesHeightFirstThenWidth()
    {
        var img = new Img(3, 7);

        Assert.Equal(3, img.H);
        Assert.Equal(7, img.W);
        Assert.Equal(21, img.D.Length);
    }

    [Fact]
    public void AtAndSet_AreRowMajor()
    {
        var img = new Img(3, 4);

        img.Set(2, 1, 99);

        Assert.Equal(99, img.At(2, 1), 9);
        Assert.Equal(99, img.D[2 * 4 + 1], 9);
    }

    [Fact]
    public void NewRaster_StartsZeroed()
    {
        var img = new Img(2, 2);

        Assert.All(img.D, v => Assert.Equal(0, v, 9));
    }

    // ---------------------------------------------------------------- Divide

    [Fact]
    public void Divide_DividesElementwise()
    {
        var a = Filled(2, 2, 10);
        var b = Filled(2, 2, 4);

        var result = Img.Divide(a, b);

        Assert.Equal(2.5, result.At(0, 0), 9);
        Assert.Equal(2.5, result.At(1, 1), 9);
    }

    [Fact]
    public void Divide_KeepsTheShapeOfTheNumerator()
    {
        var result = Img.Divide(new Img(3, 5), new Img(3, 5));

        Assert.Equal(3, result.H);
        Assert.Equal(5, result.W);
    }

    /// <summary>
    /// The denominator is floored at 1e-12 (mirroring numpy's np.maximum(model, 1e-12)),
    /// so a zero divisor yields a huge finite number rather than an infinity.
    /// </summary>
    [Fact]
    public void Divide_FloorsAZeroDenominator_SoTheResultStaysFinite()
    {
        var a = Filled(1, 1, 1);
        var b = Filled(1, 1, 0);

        double v = Img.Divide(a, b).At(0, 0);

        Assert.Equal(1e12, v, 0);
        Assert.False(double.IsInfinity(v));
    }

    /// <summary>
    /// The floor is a max, not an abs: a negative denominator is clamped up to 1e-12, so
    /// the quotient comes out positive and huge. Same as numpy - preserved for parity.
    /// </summary>
    [Fact]
    public void Divide_ClampsANegativeDenominatorUpToTheFloor()
    {
        var a = Filled(1, 1, 1);
        var b = Filled(1, 1, -5);

        double v = Img.Divide(a, b).At(0, 0);

        Assert.True(v > 0);
        Assert.Equal(1e12, v, 0);
    }

    [Fact]
    public void Divide_OfAFrameByItself_IsAllOnes()
    {
        var a = new Img(4, 4);
        for (int i = 0; i < a.D.Length; i++) a.D[i] = i + 1;

        var result = Img.Divide(a, a);

        Assert.All(result.D, v => Assert.Equal(1.0, v, 9));
    }

    static Img Filled(int h, int w, double v)
    {
        var img = new Img(h, w);
        Array.Fill(img.D, v);
        return img;
    }

    // ---------------------------------------------------------------- Symmetrise

    /// <summary>
    /// The defining property, and the one the radial model relies on: whatever goes in,
    /// what comes out is exactly even under a 180 degree rotation. That is what makes a
    /// ring's pixel distribution symmetric, so its median stops falling into the gap
    /// between two modes and stops jumping from bin to bin.
    /// </summary>
    [Fact]
    public void Symmetrise_OutputIsExactlyInvariantUnderRotation()
    {
        var tilted = TestImages.From(64, 48,
            FitsTestData.WithLinearGradient(FitsTestData.Radial(64, 48), 64, 0.20));

        var sym = Img.Symmetrise(tilted);

        for (int y = 0; y < sym.H; y++)
            for (int x = 0; x < sym.W; x++)
                Assert.Equal(sym.At(y, x), sym.At(sym.H - 1 - y, sym.W - 1 - x), 12);
    }

    /// <summary>
    /// A field that is already even about the ROTATION centre comes back untouched, bit
    /// for bit - so the radial model gives up nothing it was entitled to keep.
    ///
    /// Note the centre: the 180-degree rotation mirrors about ((H-1)/2, (W-1)/2), the true middle of
    /// the pixel indices, so the scene here is built about that and not about (w/2, h/2)
    /// like <see cref="FitsTestData.Radial"/>. That half-pixel difference is known issue
    /// 5, and the next test measures what it costs.
    /// </summary>
    [Fact]
    public void Symmetrise_LeavesAFieldAlreadyEvenAboutTheRotationCentreUntouched()
    {
        const int w = 64, h = 48;
        double cx = (w - 1) / 2.0, cy = (h - 1) / 2.0;
        var even = TestImages.From(w, h, (x, y) =>
        {
            double dx = x - cx, dy = y - cy;
            return 30000 * (1 - 0.45 * (dx * dx + dy * dy) / (cx * cx + cy * cy));
        });

        var sym = Img.Symmetrise(even);

        for (int i = 0; i < even.D.Length; i++)
            Assert.Equal(even.D[i], sym.D[i], 12);
    }

    /// <summary>
    /// On the fixture generator's radial field, centred on the geometric centre like the
    /// analysis (R-014), symmetrising changes nothing: the raster is its own 180-degree
    /// rotation. Until 2026-11-09 both sat on the index (w/2, h/2), half a pixel off the
    /// rotation centre, and symmetrising cost 2.81 % worst case on 64x48 and 0.32 % on
    /// 512x512, the artefact of known issue 5 (closed by ADR 0095). Pinned so that a change of
    /// the centring convention shows up here rather than as a mystery in the residual.
    /// </summary>
    [Fact]
    public void Symmetrise_OnTheFixtureFieldChangesNothing()
    {
        var small = TestImages.From(64, 48, FitsTestData.Radial(64, 48));
        var large = TestImages.From(512, 512, FitsTestData.Radial(512, 512));

        Assert.Equal(0, WorstRelativeChangePct(small), 9);
        Assert.Equal(0, WorstRelativeChangePct(large), 9);
    }

    static double WorstRelativeChangePct(Img a)
    {
        var sym = Img.Symmetrise(a);
        double worst = 0;
        for (int i = 0; i < a.D.Length; i++)
            worst = Math.Max(worst, Math.Abs(sym.D[i] - a.D[i]) / a.D[i] * 100);
        return worst;
    }

    /// <summary>The mean of two symmetric values, on the smallest possible case.</summary>
    [Fact]
    public void Symmetrise_AveragesEachPixelWithItsOpposite()
    {
        var a = new Img(1, 2);
        a.Set(0, 0, 10);
        a.Set(0, 1, 20);

        var sym = Img.Symmetrise(a);

        Assert.Equal(15.0, sym.At(0, 0), 9);
        Assert.Equal(15.0, sym.At(0, 1), 9);
    }
}
