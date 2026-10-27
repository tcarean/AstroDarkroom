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

public class BackgroundAnalyzerTests
{
    const float Sky = 0.08f;

    [Fact]
    public void ANeutralSkyHasNoCastAndNoFlaggedRegion()
    {
        var b = BackgroundAnalyzer.Analyze(SyntheticRasters.Flat(120, 120, Sky));

        Assert.True(b.ColorApplicable);
        Assert.All(b.Levels, l => Assert.Equal(Sky * 255, l, 3));
        Assert.Equal(0, b.GreenExcess, 6);
        Assert.Equal(0, b.RedMinusBlue, 6);
        Assert.False(b.HasCast);
        Assert.Equal(8, b.Regions.Count);
        Assert.Empty(b.FlaggedRegions);
    }

    [Fact]
    public void AGreenTintOfTwoLevelsIsMeasuredAsSuch()
    {
        var r = SyntheticRasters.Flat(120, 120, Sky, Sky + 2f / 255, Sky);
        var b = BackgroundAnalyzer.Analyze(r);

        Assert.Equal(2.0, b.GreenExcess, 3);
        Assert.Equal(0, b.RedMinusBlue, 3);
        Assert.True(b.HasCast);
    }

    [Fact]
    public void AHalfLevelTintIsBelowTheWarning()
    {
        var r = SyntheticRasters.Flat(120, 120, Sky, Sky + 0.5f / 255, Sky);

        Assert.False(BackgroundAnalyzer.Analyze(r).HasCast);
    }

    [Fact]
    public void AWarmCornerIsFlaggedByItsChannelsNotItsBrightness()
    {
        // The bottom-right corner (the last 15 % of rows, 20 % of columns): R +10, B -8. The
        // mean luminance barely moves; the channels do. It is the case of the acceptance image.
        var r = SyntheticRasters.From(200, 200, 3, (x, y, p) =>
        {
            bool corner = y >= 170 && x >= 160;
            if (!corner) return Sky;
            return p == 0 ? Sky + 10f / 255 : p == 2 ? Sky - 8f / 255 : Sky;
        });
        var b = BackgroundAnalyzer.Analyze(r);

        // The background is not contaminated: the bottom band is excluded from it.
        Assert.All(b.Levels, l => Assert.Equal(Sky * 255, l, 2));
        Assert.False(b.HasCast);

        var flagged = Assert.Single(b.FlaggedRegions);
        Assert.Equal("colt dreapta-jos", flagged.Name);
        Assert.Equal(10, flagged.Deltas[0], 1);
        Assert.Equal(0, flagged.Deltas[1], 1);
        Assert.Equal(-8, flagged.Deltas[2], 1);
        Assert.Equal(10, flagged.MaxAbsDelta, 1);
        Assert.True(flagged.Coloured);
    }

    /// <summary>
    /// A corner brightened by the same amount on every channel is a NEUTRAL deviation - the
    /// channels rise together. It is flagged, but not as a gradient. On this flat scene the
    /// brightening is classed nebula and fills the corner, so the region is measured on every
    /// pixel (under the quarter of sky, R-061).
    /// </summary>
    [Fact]
    public void ANeutralBrighteningIsFlaggedButNotColoured()
    {
        var r = SyntheticRasters.From(200, 200, 3, (x, y, p) =>
            y < 30 && x < 40 ? Sky + (p == 0 ? 11f : p == 1 ? 10f : 16f) / 255 : Sky);
        var b = BackgroundAnalyzer.Analyze(r);

        var flagged = Assert.Single(b.FlaggedRegions);
        Assert.Equal("colt stanga-sus", flagged.Name);
        Assert.False(flagged.Coloured);
    }

    [Fact]
    public void ABrightEdgeIsFlaggedToo()
    {
        var r = SyntheticRasters.From(200, 200, 3, (x, y, p) => y < 30 ? Sky + 5f / 255 : Sky);
        var b = BackgroundAnalyzer.Analyze(r);

        var names = b.FlaggedRegions.Select(x => x.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "colt dreapta-sus", "colt stanga-sus", "latura de sus" }, names);
        Assert.All(b.FlaggedRegions, x => Assert.Equal(5, x.MaxAbsDelta, 1));
        Assert.All(b.FlaggedRegions, x => Assert.False(x.Coloured));
    }

    [Fact]
    public void MonoHasLevelsButNoCastMetrics()
    {
        var b = BackgroundAnalyzer.Analyze(SyntheticRasters.Flat(120, 120, Sky, planes: 1));

        Assert.False(b.ColorApplicable);
        Assert.False(b.HasCast);
        Assert.Single(b.Levels);
        Assert.Equal(8, b.Regions.Count);
    }

    [Fact]
    public void TheBackgroundIsTheSkyNotTheObject()
    {
        // A bright core over a third of the image must not enter the "background".
        var r = SyntheticRasters.From(320, 320, 3, (x, y, p) => x >= 96 && x < 192 ? 0.6f : Sky);
        var b = BackgroundAnalyzer.Analyze(r);

        Assert.Equal(SkyColourSource.SkyPatches, b.Source);
        Assert.All(b.Levels, l => Assert.Equal(Sky * 255, l, 2));
    }

    /// <summary>
    /// The case of the 2026-08-12 core (ADR 0055): a brown dust lane, darker than the sky, is
    /// the darkest part of the frame, and the sky's colour must still be the sky's, not the
    /// lane's - against the lane every sky edge read "bluer than the background".
    /// </summary>
    static Raster SkyWithABrownLane(Func<int, int, int, float?>? over = null)
        => SyntheticRasters.From(320, 320, 3, (x, y, p) =>
        {
            float? o = over?.Invoke(x, y, p);
            if (o.HasValue) return o.Value;
            bool lane = x >= 128 && x < 192;
            if (!lane) return Sky;
            return p == 0 ? Sky - 8f / 255 : p == 1 ? Sky - 7f / 255 : Sky - 12f / 255;
        });

    [Fact]
    public void R060_TheSkyColourIsTheSkyPatchesNotTheDarkestQuarter()
    {
        var b = BackgroundAnalyzer.Analyze(SkyWithABrownLane());

        Assert.Equal(SkyColourSource.SkyPatches, b.Source);
        Assert.All(b.Levels, l => Assert.Equal(Sky * 255, l, 2));
        Assert.False(b.HasCast);
        Assert.Equal("dalele de cer de deasupra benzii de jos", b.SourceText);
    }

    [Fact]
    public void R060_WithoutASkyPatchAboveTheBandEveryPatchThereIsTheReference()
    {
        Raster r = SkyWithABrownLane();
        DustMap map = DustAnalyzer.Build(r.W, r.H, null, DustAnalyzer.PatchLstars(RasterOps.Subsample(r, Thresholds.TonalStride)));
        Array.Fill(map.Classes, PatchClass.Band);
        var b = BackgroundAnalyzer.Analyze(r, map);

        // Every patch above the band: the lane is a fifth of the width, so the medians are
        // still the sky's.
        Assert.Equal(SkyColourSource.AllPatches, b.Source);
        Assert.All(b.Levels, l => Assert.Equal(Sky * 255, l, 2));
        Assert.Equal("toate dalele de deasupra benzii de jos (nicio dala de cer acolo)", b.SourceText);
    }

    [Fact]
    public void R060_AFrameSmallerThanAPatchStillHasASkyPatch()
    {
        // The map keeps a grid of at least one patch, however small the frame (its side shrinks
        // to the frame), so the sky patches are never missing for want of size.
        var b = BackgroundAnalyzer.Analyze(SyntheticRasters.Flat(20, 20, Sky, Sky + 2f / 255, Sky));

        Assert.Equal(SkyColourSource.SkyPatches, b.Source);
        Assert.Equal(2.0, b.GreenExcess, 3);
    }

    /// <summary>
    /// A brown lane over the top edge, x 64..192 of a region that spans x 64..256: the lane's
    /// patches are dust, a third of the region is sky, and the medians come from that third.
    /// </summary>
    static Raster SkyWithALaneOnTheTopEdge(int laneRight)
        => SyntheticRasters.From(320, 320, 3, (x, y, p) =>
        {
            bool lane = y < 48 && x >= 64 && x < laneRight;
            if (!lane) return Sky;
            return p == 0 ? Sky - 8f / 255 : p == 1 ? Sky - 7f / 255 : Sky - 12f / 255;
        });

    [Fact]
    public void R061_ARegionIsMeasuredOnItsSkyPixelsWhenTheyAreAQuarterOrMore()
    {
        var b = BackgroundAnalyzer.Analyze(SkyWithALaneOnTheTopEdge(192));

        EdgeRegion top = b.Regions.Single(x => x.Name == "latura de sus");
        Assert.True(top.OnSkyPixels);
        Assert.Equal(1 / 3.0, top.SkyFraction, 0.05);
        Assert.All(top.Deltas, d => Assert.Equal(0, d, 1));
        Assert.False(top.Flagged);
        Assert.Equal("", top.PixelsNote);
    }

    [Fact]
    public void R061_ARegionWithUnderAQuarterOfSkyIsMeasuredOnEveryPixelAndSaysSo()
    {
        // The lane covers x 64..224 of the region, to a patch's edge (a patch half covered is
        // dust too): a sixth of it is sky, under the quarter, so the lane itself is the region's
        // median and the brown departure is reported as coloured.
        var b = BackgroundAnalyzer.Analyze(SkyWithALaneOnTheTopEdge(224));

        EdgeRegion top = b.Regions.Single(x => x.Name == "latura de sus");
        Assert.False(top.OnSkyPixels);
        Assert.Equal(1 / 6.0, top.SkyFraction, 0.05);
        Assert.Equal(-8, top.Deltas[0], 0);
        Assert.Equal(-12, top.Deltas[2], 0);
        Assert.True(top.Flagged);
        Assert.True(top.Coloured);
        Assert.Equal("  (cer 17 %: toti pixelii)", top.PixelsNote);
    }

    [Fact]
    public void R061_SkyEdgesBesideADustLaneAreNotFlaggedButAWarmCornerStillIs()
    {
        // The lane crosses the top and bottom edges; the sky edges read 0 against the sky, and
        // the horizon corner (R +10, B -8, bottom right) keeps its warning.
        Raster r = SkyWithABrownLane((x, y, p) => y >= 272 && x >= 256
            ? (p == 0 ? Sky + 10f / 255 : p == 2 ? Sky - 8f / 255 : Sky)
            : null);
        var b = BackgroundAnalyzer.Analyze(r);

        var flagged = Assert.Single(b.FlaggedRegions);
        Assert.Equal("colt dreapta-jos", flagged.Name);
        Assert.True(flagged.Coloured);
        Assert.All(b.Regions.Where(x => x.Name != "colt dreapta-jos"), x => Assert.True(x.MaxAbsDelta < 1, x.Name + " " + x.MaxAbsDelta));
    }
}
