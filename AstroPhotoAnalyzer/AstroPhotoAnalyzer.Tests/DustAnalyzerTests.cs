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
/// R-044 and R-045: the five classes of the patches (ADR 0026) on a synthetic wide field
/// across the galactic plane, the catalogue's clouds, the fallbacks without a WCS, without a
/// band and without sky, and the preview classes.
/// </summary>
public class DustAnalyzerTests
{
    static float[] Patches(Raster r) => DustAnalyzer.PatchLstars(RasterOps.Subsample(r, Thresholds.TonalStride));

    static DustMap Build(Raster r, Wcs? wcs) => DustAnalyzer.Build(r.W, r.H, wcs, Patches(r));

    [Fact]
    public void R044_TheFiveClassesFollowTheBandTheLocalLevelAndTheSkyModel()
    {
        Raster r = WcsFixtures.BandScene();
        DustMap map = Build(r, WcsFixtures.OnThePlane());
        float[] L = Patches(r);

        Assert.Equal(DustMethod.Classes, map.Method);
        Assert.Equal(16 * 16, map.PatchCount);
        Assert.NotNull(map.GalacticLatitude);
        Assert.NotNull(map.SkyModel);
        Assert.NotNull(map.BandLevel);

        // The band is the strip |b| < 10 degrees: about two thirds of a 31 degree field.
        double band = map.FractionOf(PatchClass.Band) + map.FractionOf(PatchClass.Dust) + map.FractionOf(PatchClass.BrightCloud) + map.FractionOf(PatchClass.Peak);
        Assert.InRange(band, 0.45, 0.85);
        Assert.InRange(map.FractionOf(PatchClass.Sky), 0.15, 0.55);

        for (int i = 0; i < map.PatchCount; i++)
        {
            bool inBand = Math.Abs(map.GalacticLatitude[i]) < Thresholds.BandGalacticLatitudeDeg;
            if (map.Classes[i] == PatchClass.Dust)
                Assert.True(L[i] < map.BandLevel[i] - Thresholds.DustDepthLstar || L[i] < map.SkyModel[i] - Thresholds.DustDepthLstar);
            if (map.Classes[i] == PatchClass.BrightCloud)
                Assert.True(inBand && L[i] > map.BandLevel[i] + Thresholds.BrightCloudLstar);
            if (map.Classes[i] == PatchClass.Sky)
                Assert.False(inBand);
        }
        Assert.Contains(PatchClass.Dust, map.Classes);
        Assert.Contains(PatchClass.BrightCloud, map.Classes);
        Assert.Contains(PatchClass.Peak, map.Classes);
        Assert.InRange(map.FractionOf(PatchClass.Peak), 0.0005, 0.02);

        // The dust blob (0.05 on a 0.35 band) and the bright blob (0.6): the patches inside them.
        Assert.Equal(PatchClass.Dust, map.Classes[WcsFixtures.DustPatchIndex(map)]);
        Assert.Equal(PatchClass.BrightCloud, map.Classes[WcsFixtures.BrightPatchIndex(map)]);
    }

    [Fact]
    public void R044_TheDustIsTheQuarterPercentileOfItsClassAndTheSkyTheMedianOfItsOwn()
    {
        Raster r = WcsFixtures.BandScene();
        DustMap map = Build(r, WcsFixtures.OnThePlane());
        float[] L = Patches(r);
        DustStats d = DustAnalyzer.Measure(L, map, ColorMath.PaperBlackLstar(2.0));

        var dust = Enumerable.Range(0, L.Length).Where(i => map.Classes[i] == PatchClass.Dust).Select(i => L[i]).ToArray();
        var sky = Enumerable.Range(0, L.Length).Where(i => map.Classes[i] == PatchClass.Sky).Select(i => L[i]).ToArray();
        Assert.Equal(Stats.PercentileSorted(Stats.Sorted(dust), Thresholds.DustClassFraction * 100), d.LstarDust, 6);
        Assert.Equal(Stats.Median(sky), d.LstarSky, 6);
        Assert.Equal(dust.Length, d.DustPatchCount);
        // The blob is at 0.05 (L* 3.6), the outside sky at 0.25 (L* 27).
        Assert.InRange(d.LstarDust, 3, 12);
        Assert.Equal(ColorMath.LstarOfSrgb(0.25, 0.25, 0.25), d.LstarSky, 0);
        Assert.Equal(map.Classes, DustAnalyzer.Classes(map, d));
    }

    [Fact]
    public void R044_WithoutAWcsTheDustIsTheDarkestPercentAndTheSkyTheMedian()
    {
        Raster r = WcsFixtures.SkyWithDisc(0.25f, 0.05f, 70);
        DustMap map = Build(r, null);
        Assert.Equal(DustMethod.NoWcs, map.Method);
        Assert.Empty(map.Clouds);
        Assert.Null(map.GalacticLatitude);

        float[] patches = Patches(r);
        DustStats d = DustAnalyzer.Measure(patches, map, ColorMath.PaperBlackLstar(2.0));

        Assert.Equal(Stats.PercentileSorted(Stats.Sorted(patches), 1), d.LstarDust, 6);
        Assert.Equal(ColorMath.LstarOfSrgb(0.25, 0.25, 0.25), d.LstarSky, 1);
        Assert.Equal(map.PatchCount, d.DustPatchCount);

        PatchClass[] classes = DustAnalyzer.Classes(map, d);
        Assert.Contains(PatchClass.Dust, classes);
        Assert.Contains(PatchClass.Sky, classes);
        Assert.DoesNotContain(PatchClass.Band, classes);
        Assert.InRange(classes.Count(c => c == PatchClass.Sky), map.PatchCount / 3, map.PatchCount);
    }

    [Fact]
    public void R044_AFrameFarFromThePlaneOrWhollyInsideItFallsBackAndSaysWhy()
    {
        // The north galactic pole: no band at all.
        Raster r = WcsFixtures.SkyWithDisc(0.25f, 0.05f, 70);
        DustMap pole = Build(r, WcsFixtures.SyntheticWcs(512, 512, 192.86, 27.13, 0.01158));
        Assert.Equal(DustMethod.NoBand, pole.Method);
        Assert.False(pole.UsesClasses);

        // LDN 42 at 9"/px: a 1.3 degree field wholly inside the band, the catalogue still naming the cloud.
        Raster ldn = WcsFixtures.Ldn42Scene();
        DustMap inside = Build(ldn, ldn.Source.Wcs);
        Assert.Equal(DustMethod.AllBand, inside.Method);
        Assert.Contains(inside.Clouds, c => c.Nebula.Ldn == 42);

        // Both fall back to the frame rule, and the clouds keep their rows' figures.
        float[] L = Patches(ldn);
        DustStats d = DustAnalyzer.Measure(L, inside, 9);
        Assert.Equal(Stats.PercentileSorted(Stats.Sorted(L), 1), d.LstarDust, 6);
        int c42 = inside.Clouds.ToList().FindIndex(c => c.Nebula.Ldn == 42);
        Assert.Equal(ColorMath.LstarOfSrgb(0.05, 0.05, 0.05), d.CloudLstars[c42], 1);
    }

    [Fact]
    public void R044_TheCatalogueNamesTheCloudsInTheFrame()
    {
        Raster ldn = WcsFixtures.Ldn42Scene();
        DustMap map = Build(ldn, ldn.Source.Wcs);
        DustCloud cloud = Assert.Single(map.Clouds, c => c.Nebula.Ldn == 42);

        // The WCS is centred on the rounded 263.27, -25.70; the catalogue's centre is 1.3 px away.
        Assert.InRange(cloud.CentreX, 252, 259);
        Assert.InRange(cloud.CentreY, 252, 259);
        Assert.Equal(cloud.Nebula.RadiusDeg / WcsFixtures.NarrowScale, cloud.RadiusPx, 0);
        // Patches whose centre is within 272 px of the middle: about pi * 272^2 / 32^2 = 227 of 256.
        Assert.InRange(cloud.Patches.Length, 200, 240);
    }

    [Fact]
    public void R044_ThePatchIsTheLstarOfTheMeanLinearLuminanceNotOfPixels()
    {
        // Half the pixels of every patch at 0 and half at 0.5: the mean LINEAR luminance is
        // Y(0.5) / 2, far above the L* of the median pixel (0).
        Raster r = SyntheticRasters.From(64, 64, 3, (x, y, p) => (x / Thresholds.TonalStride) % 2 == 0 ? 0f : 0.5f);
        float[] patches = Patches(r);

        Assert.Equal(2 * 2, patches.Length);
        Assert.All(patches, l => Assert.Equal(ColorMath.LstarOfY(ColorMath.SrgbToLinear(0.5) / 2), l, 2));
    }

    [Fact]
    public void R044_TheSkyModelFollowsAGradientAndTheMedianFilterIgnoresASpeck()
    {
        // A 48 x 48 grid of patches with a left-to-right gradient outside the band (the middle
        // rows), and the band itself bright: the model interpolates the gradient under the band.
        int cols = 48, rows = 48;
        var L = new float[cols * rows]; var band = new bool[cols * rows];
        for (int by = 0; by < rows; by++)
            for (int bx = 0; bx < cols; bx++)
            {
                band[by * cols + bx] = by >= 18 && by < 30;
                L[by * cols + bx] = band[by * cols + bx] ? 60 : 10 + bx / 4f;
            }
        float[] model = DustAnalyzer.SkyModel(L, band, cols, rows);
        Assert.InRange(model[24 * cols + 4], 9, 14);     // under the band, left: near 11
        Assert.InRange(model[24 * cols + 43], 18, 23);   // under the band, right: near 21
        Assert.All(model, v => Assert.InRange(v, 9, 23));

        var speck = new float[cols * rows]; Array.Fill(speck, 20f); speck[5 * cols + 5] = 0;
        float[] med = DustAnalyzer.MedianFilter(speck, cols, rows, 5);
        Assert.Equal(20, med[5 * cols + 5]);
        float[] smooth = DustAnalyzer.Smooth(speck, cols, rows, 1);
        Assert.InRange(smooth[5 * cols + 5], 17, 18);
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
