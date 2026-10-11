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

        Assert.Equal(DustMethod.Band, map.Method);
        Assert.Equal(SkyReason.None, map.SkyReason);
        Assert.Equal(new[] { "praf", "cer", "banda", "nori luminosi", "varfuri" }, map.ClassNames);
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
        Assert.Equal(DustSource.DustClass, map.DustSource);
    }

    /// <summary>A smooth band with nothing under its local level: the classes stay the band's, and the dust is the darkest sky.</summary>
    [Fact]
    public void R044_ABandWithoutDustTakesTheDarkestSkyAndKeepsItsClasses()
    {
        Raster r = WcsFixtures.BandScene(withDust: false);
        DustMap map = Build(r, WcsFixtures.OnThePlane());
        float[] L = Patches(r);

        Assert.Equal(DustMethod.Band, map.Method);
        Assert.DoesNotContain(PatchClass.Dust, map.Classes);
        Assert.Contains(PatchClass.Band, map.Classes);

        DustStats d = DustAnalyzer.Measure(L, map, 9);
        var sky = Enumerable.Range(0, L.Length).Where(i => map.Classes[i] == PatchClass.Sky).Select(i => L[i]).ToArray();
        Assert.Equal(DustSource.DarkestSky, map.DustSource);
        Assert.Equal(Stats.PercentileSorted(Stats.Sorted(sky), Thresholds.SkyDarkestFraction * 100), d.LstarDust, 6);
        Assert.Equal(Stats.Median(sky), d.LstarSky, 6);
        Assert.Equal(0, d.DustPatchCount);
    }

    /// <summary>Without a WCS the classes are relative to the modelled sky, with the thresholds taken from the image (ADR 0033).</summary>
    [Fact]
    public void R044_WithoutTheBandTheClassesAreRelativeToTheModelledSkyWithThresholdsFromTheImage()
    {
        Raster r = WcsFixtures.NebulaScene();
        DustMap map = Build(r, null);
        float[] L = Patches(r);

        Assert.Equal(DustMethod.Sky, map.Method);
        Assert.Equal(SkyReason.NoWcs, map.SkyReason);
        Assert.Equal(new[] { "praf", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri" }, map.ClassNames);
        Assert.Empty(map.Clouds);
        Assert.Null(map.GalacticLatitude);
        Assert.Null(map.BandLevel);
        Assert.NotNull(map.SkyModel);
        Assert.True(map.SkySigma > 0);
        Assert.Equal(Thresholds.SkyNoiseK * map.SkySigma, map.NoiseThreshold, 9);
        Assert.True(map.NoiseThreshold > 0 && map.NoiseThreshold < 5);   // a few tenths of an L* of patch noise, times 4
        Assert.True(map.BrightThreshold > map.NoiseThreshold);

        // The flat sky is sky, the halo nebula, the core bright nebula, the single brightest patch the peak.
        for (int i = 0; i < map.PatchCount; i++)
        {
            double over = L[i] - map.SkyModel[i];
            switch (map.Classes[i])
            {
                case PatchClass.Sky: Assert.InRange(over, -map.NoiseThreshold, map.NoiseThreshold); break;
                case PatchClass.Band: Assert.True(over > map.NoiseThreshold && over <= map.BrightThreshold); break;
                case PatchClass.BrightCloud: Assert.True(over > map.BrightThreshold); break;
                case PatchClass.Peak: break;
                default: Assert.Fail("unexpected class " + map.Classes[i]); break;
            }
        }
        Assert.InRange(map.FractionOf(PatchClass.Sky), 0.6, 0.95);
        Assert.InRange(map.FractionOf(PatchClass.Band), 0.03, 0.3);
        Assert.InRange(map.FractionOf(PatchClass.BrightCloud), 0.005, 0.1);
        Assert.Equal(1, map.Classes.Count(c => c == PatchClass.Peak));
        // The core sits on the corner of four patches: the peak is one of them.
        int peakAt = Array.IndexOf(map.Classes, PatchClass.Peak);
        Assert.Contains(peakAt, new[] { 7 * map.Cols + 7, 7 * map.Cols + 8, 8 * map.Cols + 7, 8 * map.Cols + 8 });
        Assert.Equal(0, map.FractionOf(PatchClass.Dust));

        // The nebula threshold is the sky's own noise: twice the pixel noise, about twice the threshold.
        DustMap noisier = Build(WcsFixtures.NebulaScene(noise: 8.0 / 255), null);
        Assert.InRange(noisier.NoiseThreshold / map.NoiseThreshold, 1.5, 2.7);

        // Two star patches far above a faint nebula: one is the peak (0.1 % of 256 patches is one
        // patch), the other stays in the Otsu tail, and the split may not leave under 5 % of the
        // tail above it, so the bright threshold lands inside the nebula, not at the star.
        Raster faint = SyntheticRasters.AddNoise(SyntheticRasters.From(512, 512, 3, (x, y, p) =>
        {
            if (x >= 32 && x < 64 && y >= 32 && y < 64) return 0.9f;
            if (x >= 448 && x < 480 && y >= 448 && y < 480) return 0.9f;
            double d2 = (x - 255.5) * (x - 255.5) + (y - 255.5) * (y - 255.5);
            return (float)(0.15 + 0.08 * Math.Exp(-d2 / (2 * 90.0 * 90.0)));
        }), 4.0 / 255, 7);
        DustMap stars = Build(faint, null);
        Assert.Equal(1, stars.Classes.Count(c => c == PatchClass.Peak));
        Assert.True(stars.FractionOf(PatchClass.BrightCloud) > 0.01);
        Assert.True(stars.BrightThreshold < 30, $"bright threshold {stars.BrightThreshold}");   // inside the nebula, not at the star (L* 96 over a sky of 15)
    }

    /// <summary>On both paths the verdict is one measurement: the sky's median, and the darkest extended area for the dust (ADR 0033).</summary>
    [Fact]
    public void R044_TheVerdictIsTheSkysMedianAndTheDarkestExtendedAreaOnEitherPath()
    {
        // 100 patches by hand: 10 of "dust" and 90 of sky between L* 20 and 29 (p1 of the sky 20.1).
        var map = new DustMap { Cols = 10, Rows = 10, Side = 8, Stride = 4, Classes = new PatchClass[100], Clouds = Array.Empty<DustCloud>() };
        var L = new float[100];
        for (int i = 0; i < 100; i++) { map.Classes[i] = i < 10 ? PatchClass.Dust : PatchClass.Sky; L[i] = i < 10 ? 5 + i * 0.5f : 20 + (i - 10) / 10f; }
        map.DustSource = DustAnalyzer.DecideDustSource(L, map);
        DustStats d = DustAnalyzer.Measure(L, map, 9);
        Assert.Equal(DustSource.DustClass, map.DustSource);
        Assert.Equal(Stats.PercentileSorted(Stats.Sorted(L.Take(10).ToArray()), Thresholds.DustClassFraction * 100), d.LstarDust, 6);
        Assert.Equal(Stats.Median(L.Skip(10).ToArray()), d.LstarSky, 6);
        Assert.Equal(10, d.DustPatchCount);

        // The same dust, brighter than the darkest sky: the sky's darkest 1 % wins.
        for (int i = 0; i < 10; i++) L[i] = 25 + i * 0.5f;
        map.DustSource = DustAnalyzer.DecideDustSource(L, map);
        d = DustAnalyzer.Measure(L, map, 9);
        Assert.Equal(DustSource.DarkestSky, map.DustSource);
        Assert.Equal(Stats.PercentileSorted(Stats.Sorted(L.Skip(10).ToArray()), Thresholds.SkyDarkestFraction * 100), d.LstarDust, 6);

        // A dust class under DustClassMinFraction of the patches (4 of 100) does not decide, however dark.
        for (int i = 0; i < 10; i++) { L[i] = i < 4 ? 1 : 24; map.Classes[i] = i < 4 ? PatchClass.Dust : PatchClass.Sky; }
        map.DustSource = DustAnalyzer.DecideDustSource(L, map);
        d = DustAnalyzer.Measure(L, map, 9);
        Assert.Equal(DustSource.DarkestSky, map.DustSource);
        Assert.Equal(4, d.DustPatchCount);
        Assert.True(d.LstarDust > 19);

        // The source is decided once, as is: a lift that brightens the dust class past the
        // darkest sky still measures the dust class, so the ladder's rows and the definition agree.
        for (int i = 0; i < 100; i++) { map.Classes[i] = i < 10 ? PatchClass.Dust : PatchClass.Sky; L[i] = i < 10 ? 5 + i * 0.5f : 20 + (i - 10) / 10f; }
        map.DustSource = DustAnalyzer.DecideDustSource(L, map);
        var lifted = (float[])L.Clone();
        for (int i = 0; i < 10; i++) lifted[i] = 30 + i * 0.5f;
        Assert.Equal(DustSource.DustClass, map.DustSource);
        Assert.Equal(Stats.PercentileSorted(Stats.Sorted(lifted.Take(10).ToArray()), Thresholds.DustClassFraction * 100), DustAnalyzer.Measure(lifted, map, 9).LstarDust, 6);

        // No sky patch at all: the sky is the median of every patch, and the map says so.
        for (int i = 0; i < 100; i++) map.Classes[i] = i < 10 ? PatchClass.Dust : PatchClass.Band;
        map.DustSource = DustAnalyzer.DecideDustSource(L, map);
        Assert.True(map.SkyClassEmpty);
        Assert.Equal(Stats.Median(L), DustAnalyzer.Measure(L, map, 9).LstarSky, 6);

        // On the nebula scene: a dark lane of 12.5 % of the patches is the dust; without it, the darkest sky.
        Raster lane = WcsFixtures.NebulaScene(withLane: true);
        DustMap laneMap = Build(lane, null);
        Assert.InRange(laneMap.FractionOf(PatchClass.Dust), 0.1, 0.2);
        DustStats withLane = DustAnalyzer.Measure(Patches(lane), laneMap, 9);
        Assert.Equal(DustSource.DustClass, laneMap.DustSource);
        Assert.InRange(withLane.LstarDust, 0.5, 4);    // the lane at 0.03 is L* 2
        DustMap noLaneMap = Build(WcsFixtures.NebulaScene(), null);
        DustStats noLane = DustAnalyzer.Measure(Patches(WcsFixtures.NebulaScene()), noLaneMap, 9);
        Assert.Equal(DustSource.DarkestSky, noLaneMap.DustSource);
        Assert.InRange(noLane.LstarDust, 13, 17);       // the sky at 0.15 is L* 15.3
    }

    [Fact]
    public void R044_AFrameFarFromThePlaneOrWhollyInsideItTakesTheSkyPathAndSaysWhy()
    {
        // The north galactic pole: no band at all. The disc (6 % of the frame plus its edge
        // patches) is the dust class under the modelled sky, and it decides.
        Raster r = WcsFixtures.SkyWithDisc(0.25f, 0.05f, 70);
        DustMap pole = Build(r, WcsFixtures.SyntheticWcs(512, 512, 192.86, 27.13, 0.01158));
        Assert.Equal(DustMethod.Sky, pole.Method);
        Assert.Equal(SkyReason.NoBand, pole.SkyReason);
        Assert.NotNull(pole.GalacticLatitude);
        float[] L = Patches(r);
        DustStats d = DustAnalyzer.Measure(L, pole, 9);
        Assert.InRange(pole.FractionOf(PatchClass.Dust), Thresholds.DustClassMinFraction, 0.15);
        Assert.Equal(DustSource.DustClass, pole.DustSource);
        Assert.Equal(ColorMath.LstarOfSrgb(0.05, 0.05, 0.05), d.LstarDust, 1);
        Assert.Equal(ColorMath.LstarOfSrgb(0.25, 0.25, 0.25), d.LstarSky, 0);

        // LDN 42 at 9"/px: a 1.3 degree field wholly inside the band, the catalogue still naming
        // the cloud. Its disc covers nine tenths of the frame, so the modelled sky IS the cloud:
        // the sky class is the disc, the plain sky around it reads as bright nebula, the dust
        // class is empty, and the verdict's dust is the darkest sky, the cloud's own level.
        Raster ldn = WcsFixtures.Ldn42Scene();
        DustMap inside = Build(ldn, ldn.Source.Wcs);
        Assert.Equal(DustMethod.Sky, inside.Method);
        Assert.Equal(SkyReason.AllBand, inside.SkyReason);
        Assert.Contains(inside.Clouds, c => c.Nebula.Ldn == 42);
        Assert.Equal(0, inside.FractionOf(PatchClass.Dust));
        Assert.InRange(inside.FractionOf(PatchClass.Sky), 0.5, 0.95);
        float[] Lldn = Patches(ldn);
        DustStats dl = DustAnalyzer.Measure(Lldn, inside, 9);
        Assert.Equal(DustSource.DarkestSky, inside.DustSource);
        Assert.Equal(ColorMath.LstarOfSrgb(0.05, 0.05, 0.05), dl.LstarDust, 1);
        int c42 = inside.Clouds.ToList().FindIndex(c => c.Nebula.Ldn == 42);
        Assert.Equal(ColorMath.LstarOfSrgb(0.05, 0.05, 0.05), dl.CloudLstars[c42], 1);
    }

    [Fact]
    public void R044_AFrameNarrowerThanTheBandTakesTheSkyPathAndSaysWhy()
    {
        // The same scene as the band scene (a brighter strip where |b| < 10, a dark blob, a
        // bright blob, a peak), drawn on a 10 degree field near the Veil: both sides of |b| = 10
        // hold over 10 % of the patches, so only the frame's span in galactic latitude says
        // this is a deep-sky frame inside the band and not a landscape with it (ADR 0037).
        Raster r = WcsFixtures.BandScene(WcsFixtures.NearTheVeil(), withDust: true);
        DustMap map = Build(r, WcsFixtures.NearTheVeil());

        int inBand = map.GalacticLatitude.Count(b => Math.Abs(b) < Thresholds.BandGalacticLatitudeDeg);
        Assert.InRange(inBand / (double)map.PatchCount, Thresholds.BandMinFraction, 1 - Thresholds.BandMinFraction);
        Assert.InRange(map.LatitudeSpanDeg, 8, Thresholds.BandMinLatitudeSpanDeg - 1);

        Assert.Equal(DustMethod.Sky, map.Method);
        Assert.Equal(SkyReason.NarrowField, map.SkyReason);
        Assert.Null(map.BandLevel);
        Assert.Equal(new[] { "praf", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri" }, map.ClassNames);
        // On the sky path the strip is nebula over the modelled sky, not a class of its own by geometry.
        for (int i = 0; i < map.PatchCount; i++)
            if (map.Classes[i] == PatchClass.Sky)
                Assert.True(Patches(r)[i] <= map.SkyModel[i] + map.NoiseThreshold);

        // The wide band scene keeps the band path: its span is the whole 31 degree field.
        DustMap wide = Build(WcsFixtures.BandScene(), WcsFixtures.OnThePlane());
        Assert.InRange(wide.LatitudeSpanDeg, Thresholds.BandMinLatitudeSpanDeg, 90);
        Assert.Equal(DustMethod.Band, wide.Method);
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
        float[] model = DustAnalyzer.SkyModel(L, band, cols, rows, 50);
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
