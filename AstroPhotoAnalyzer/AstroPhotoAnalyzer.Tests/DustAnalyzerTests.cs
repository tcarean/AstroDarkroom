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
/// R-044 and R-045: the dust and the sky on 32 px patches, with a synthetic WCS pointed at a
/// catalogued cloud, and the fallback without one; the preview classes.
/// </summary>
public class DustAnalyzerTests
{
    static Wcs OnLdn42() => WcsFixtures.OnLdn42();
    static Raster SkyWithDisc(float sky, float dust, double rPx) => WcsFixtures.SkyWithDisc(sky, dust, rPx);
    static readonly DarkNebula Ldn42 = DarkNebulaCatalogue.All.Single(n => n.Ldn == 42);
    const double NarrowScale = WcsFixtures.NarrowScale, DarkDiscPx = WcsFixtures.DarkDiscPx;

    [Fact]
    public void R044_WithAWcsTheCatalogueNamesTheCloudAndTheSkyIsOutsideIt()
    {
        Wcs wcs = OnLdn42();
        DustMap map = DustAnalyzer.Build(512, 512, wcs);

        Assert.Equal(DustMethod.Catalogue, map.Method);
        Assert.Equal(16 * 16, map.PatchCount);   // 512 / 4 = 128 samples, / 8 per patch = 16
        DustCloud cloud = Assert.Single(map.Clouds, c => c.Nebula.Ldn == 42);
        // The WCS is centred on the rounded 263.27, -25.70; the catalogue's centre is 1.3 px away.
        Assert.InRange(cloud.CentreX, 252, 259);
        Assert.InRange(cloud.CentreY, 252, 259);
        Assert.Equal(Ldn42.RadiusDeg / NarrowScale, cloud.RadiusPx, 0);
        // Patches whose centre is within 272 px of the middle: about pi * 272^2 / 32^2 = 227 of 256.
        Assert.InRange(cloud.Patches.Length, 200, 240);

        Raster sub = RasterOps.Subsample(SkyWithDisc(0.25f, 0.05f, DarkDiscPx), Thresholds.TonalStride);
        DustStats d = DustAnalyzer.Measure(DustAnalyzer.PatchLstars(sub, map), map, ColorMath.PaperBlackLstar(2.0));

        Assert.Equal(ColorMath.LstarOfSrgb(0.05, 0.05, 0.05), d.LstarDust, 1);
        Assert.Equal(ColorMath.LstarOfSrgb(0.25, 0.25, 0.25), d.LstarSky, 1);
        Assert.Equal(ColorMath.LstarOfSrgb(0.05, 0.05, 0.05), d.CloudLstars[map.Clouds.ToList().IndexOf(cloud)], 1);
        Assert.InRange(d.PctPatchesBelowPaperBlack, 50, 75);   // the 240 px disc: ~154 of 256 patches fully dark
        Assert.Equal(map.CloudPatchCount, d.DustPatchCount);
        Assert.True(d.DustPatchCount >= cloud.Patches.Length);
    }

    [Fact]
    public void R044_WithoutAWcsTheDustIsTheDarkestPercentAndTheSkyTheMedian()
    {
        DustMap map = DustAnalyzer.Build(512, 512, null);
        Assert.Equal(DustMethod.NoWcs, map.Method);
        Assert.Empty(map.Clouds);

        Raster sub = RasterOps.Subsample(SkyWithDisc(0.25f, 0.05f, 70), Thresholds.TonalStride);
        float[] patches = DustAnalyzer.PatchLstars(sub, map);
        DustStats d = DustAnalyzer.Measure(patches, map, ColorMath.PaperBlackLstar(2.0));

        Assert.Equal(Stats.PercentileSorted(Stats.Sorted(patches), 1), d.LstarDust, 6);
        Assert.Equal(ColorMath.LstarOfSrgb(0.25, 0.25, 0.25), d.LstarSky, 1);
        Assert.Equal(map.PatchCount, d.DustPatchCount);

        PatchClass[] classes = DustAnalyzer.Classes(map, d);
        Assert.Contains(PatchClass.Dust, classes);
        Assert.Contains(PatchClass.Sky, classes);
        // The dust patches are those at or under the 1 % level: the disc's patches, all tied at 0.05.
        Assert.InRange(classes.Count(c => c == PatchClass.Dust), 1, map.PatchCount / 4);
    }

    [Fact]
    public void R044_AFrameWithoutACatalogueCloudFallsBackAndSaysSo()
    {
        // The north galactic pole: no LDN cloud within a 512 x 512 field at 42"/px.
        Wcs wcs = WcsFixtures.SyntheticWcs(512, 512, 192.86, 27.13, 0.01158);
        DustMap map = DustAnalyzer.Build(512, 512, wcs);

        Assert.Equal(DustMethod.NoClouds, map.Method);
        Assert.Empty(map.Clouds);
        Assert.False(map.UsesCatalogue);
    }

    [Fact]
    public void R044_ThePatchIsTheLstarOfTheMeanLinearLuminanceNotOfPixels()
    {
        // Half the pixels of every patch at 0 and half at 0.5: the mean LINEAR luminance is
        // Y(0.5) / 2, far above the L* of the median pixel (0).
        Raster r = SyntheticRasters.From(64, 64, 3, (x, y, p) => (x / Thresholds.TonalStride) % 2 == 0 ? 0f : 0.5f);
        DustMap map = DustAnalyzer.Build(64, 64, null);
        float[] patches = DustAnalyzer.PatchLstars(RasterOps.Subsample(r, Thresholds.TonalStride), map);

        Assert.Equal(2 * 2, patches.Length);
        Assert.All(patches, l => Assert.Equal(ColorMath.LstarOfY(ColorMath.SrgbToLinear(0.5) / 2), l, 2));
    }

    [Fact]
    public void R044_ClassesWithACatalogueAreCloudAgainstSky()
    {
        DustMap map = DustAnalyzer.Build(512, 512, OnLdn42());
        Raster sub = RasterOps.Subsample(SkyWithDisc(0.25f, 0.05f, DarkDiscPx), Thresholds.TonalStride);
        DustStats d = DustAnalyzer.Measure(DustAnalyzer.PatchLstars(sub, map), map, 9);

        PatchClass[] classes = DustAnalyzer.Classes(map, d);
        Assert.Equal(map.CloudPatchCount, classes.Count(c => c == PatchClass.Dust));
        Assert.Equal(map.PatchCount - map.CloudPatchCount, classes.Count(c => c == PatchClass.Sky));
    }

    [Fact]
    public void R045_ThePreviewClassesFollowPaperBlackAndTheSeparationThreshold()
    {
        Raster flat = SyntheticRasters.Flat(64, 64, 0.08f);
        PrintPreviewResult asIs = PrintPreview.Render(flat, 1, 2.0);

        Assert.Equal(64 * 64, asIs.Classes.Length);
        Assert.All(asIs.Classes, c => Assert.Equal(PreviewClass.UnderPaperBlack, c));
        Assert.Equal(100, asIs.PctUnderPaperBlack);
        Assert.Equal(100, asIs.PctUnderLowSeparation);
        Assert.Equal(flat.Planes[0].Length, asIs.Image.Planes[0].Length);

        // Lifted to 0.19: L* ~20, above paper black and above L* 15.
        PrintPreviewResult lifted = PrintPreview.Render(flat, RasterOps.GammaFor(0.08, 0.19), 2.0);
        Assert.All(lifted.Classes, c => Assert.Equal(PreviewClass.None, c));
        Assert.Equal(0.19f, lifted.Image.Planes[1][0], 3);

        // Lifted to 0.12: L* ~12, above paper black but under 15.
        PrintPreviewResult between = PrintPreview.Render(flat, RasterOps.GammaFor(0.08, 0.12), 2.0);
        Assert.All(between.Classes, c => Assert.Equal(PreviewClass.UnderLowSeparation, c));
        Assert.Equal(0, between.PctUnderPaperBlack);
        Assert.Equal(100, between.PctUnderLowSeparation);
    }

    [Fact]
    public void R045_ThePreviewIsReducedUnderTheMaximumSide()
    {
        Raster big = SyntheticRasters.Flat(Thresholds.PreviewMaxSide * 2 + 10, 100, 0.3f);
        PrintPreviewResult p = PrintPreview.Render(big, 1, 2.0);

        Assert.True(p.Image.W <= Thresholds.PreviewMaxSide);
        Assert.Equal(p.Image.W * p.Image.H, p.Classes.Length);
    }
}
