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

public class RasterOpsTests
{
    [Fact]
    public void ARasterHasOneOrThreePlanes()
    {
        Assert.Throws<ArgumentException>(() => new Raster(2, 2, 2));
        Assert.False(new Raster(2, 2, 1).IsColor);
        Assert.True(new Raster(2, 2, 3).IsColor);
    }

    [Fact]
    public void ReferenceIsGreenOnColourAndTheOnlyPlaneOnMono()
    {
        var c = SyntheticRasters.Flat(1, 1, 0.1f, 0.2f, 0.3f);
        var m = SyntheticRasters.Flat(1, 1, 0.4f, planes: 1);

        Assert.Equal(0.2f, c.Reference[0]);
        Assert.Equal(0.4f, m.Reference[0]);
    }

    [Fact]
    public void SubsampleIsAnchoredTopLeftLikeNumpySlicing()
    {
        // a[::2, ::2] on 5x5 gives 3x3, with the pixels (0,0), (0,2), (0,4), (2,0) ...
        var r = SyntheticRasters.From(5, 5, 1, (x, y, p) => (y * 10 + x) / 100f);
        var s = RasterOps.Subsample(r, 2);

        Assert.Equal(3, s.H);
        Assert.Equal(3, s.W);
        Assert.Equal(0.00f, s.At(0, 0, 0), 6);
        Assert.Equal(0.02f, s.At(0, 0, 1), 6);
        Assert.Equal(0.04f, s.At(0, 0, 2), 6);
        Assert.Equal(0.20f, s.At(0, 1, 0), 6);
        Assert.Equal(0.44f, s.At(0, 2, 2), 6);
        Assert.Same(r, RasterOps.Subsample(r, 1));
    }

    [Fact]
    public void GammaForMapsTheBackgroundOntoTheTarget()
    {
        double g = RasterOps.GammaFor(0.10, 0.19);

        Assert.Equal(0.721, g, 3);
        Assert.Equal(0.19, Math.Pow(0.10, g), 9);
        Assert.Throws<ArgumentException>(() => RasterOps.GammaFor(0, 0.19));
    }

    /// <summary>A NaN sample (a raster built in memory; the readers never let one through) is lifted as 0, not as an impossible table index.</summary>
    [Fact]
    public void R013_GammaTreatsANaNSampleAsBlack()
    {
        Raster r = SyntheticRasters.Flat(4, 4, 0.2f);
        r.Planes[1][5] = float.NaN;

        Raster lifted = RasterOps.Gamma(r, 0.7);

        Assert.Equal(0f, lifted.Planes[1][5]);
        Assert.Equal((float)Math.Pow(0.2, 0.7), lifted.Planes[1][0], 5);
    }

    [Fact]
    public void GammaLiftsEveryChannelAlikeSoNeutralStaysNeutral()
    {
        var r = SyntheticRasters.Flat(2, 2, 0.10f, 0.10f, 0.10f);
        var l = RasterOps.Gamma(r, RasterOps.GammaFor(0.10, 0.19));

        for (int p = 0; p < 3; p++) Assert.Equal(0.19f, l.At(p, 1, 1), 5);
        Assert.Equal(0.10f, r.At(0, 0, 0));   // the input is untouched
    }

    [Theory]
    [InlineData(0f, 0, 0)]
    [InlineData(1f, 255, 65535)]
    [InlineData(0.5f, 128, 32768)]
    [InlineData(31.4f / 255f, 31, 8070)]
    public void LevelsRoundToTheNearestInteger(float v, int level8, int level16)
    {
        Assert.Equal(level8, RasterOps.Level8(v));
        Assert.Equal(level16, RasterOps.Level16(v));
    }

    [Fact]
    public void EverySixteenBitLevelSurvivesTheFloatRoundTrip()
    {
        for (int n = 0; n <= 65535; n++)
            Assert.Equal(n, RasterOps.Level16(n / 65535f));
    }

    [Fact]
    public void LstarPlaneOfMidGreyIsAboutFiftyThree()
    {
        var r = SyntheticRasters.Flat(4, 4, 0.5f);
        float[] l = RasterOps.LstarPlane(r, 2);

        Assert.Equal(4, l.Length);
        Assert.Equal(53.39, l[0], 1);
    }

    [Fact]
    public void LooksLinearBelowTheThresholdAndNotAbove()
    {
        Assert.True(RasterOps.LooksLinear(SyntheticRasters.Flat(16, 16, 0.005f), out double m1));
        Assert.Equal(0.005, m1, 4);
        Assert.False(RasterOps.LooksLinear(SyntheticRasters.Flat(16, 16, 0.10f), out double m2));
        Assert.Equal(0.10, m2, 4);
    }
}
