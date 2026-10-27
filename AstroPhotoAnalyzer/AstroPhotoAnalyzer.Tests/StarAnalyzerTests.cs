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

public class StarAnalyzerTests
{
    const float Sky = 0.08f;

    /// <summary>A 512 x 512 sky with stars on an 8 x 8 grid, 55 px apart; half of them are centred between pixels.</summary>
    static Raster StarField(double fwhm, float amplitude = 0.3f, int count = 64)
    {
        Raster r = SyntheticRasters.Flat(512, 512, Sky);
        int placed = 0;
        for (int j = 0; j < 8 && placed < count; j++)
            for (int i = 0; i < 8 && placed < count; i++, placed++)
            {
                double cx = 40 + i * 55 + ((i + j) % 2 == 0 ? 0 : 0.5);
                r = SyntheticRasters.AddStar(r, cx, 40 + j * 55, fwhm, amplitude);
            }
        return SyntheticRasters.AddNoise(r, 0.5 / 255, 11);
    }

    [Fact]
    public void R070_RecoversTheFwhmOfSyntheticStars()
    {
        var s = StarAnalyzer.Analyze(StarField(3.0));

        Assert.False(s.Insufficient);
        Assert.InRange(s.Count, 55, 70);
        // The linear interpolation at the half overestimates a sampled Gaussian by about
        // 2.5 % (between 1 and 2 px the curve is convex). It is the bias of the acceptance
        // script's method, kept for parity: a true 3.0 reads about 3.08.
        Assert.InRange(s.FwhmMedian, 2.95, 3.2);
        Assert.InRange(s.FwhmQ1, 2.8, 3.2);
        Assert.InRange(s.FwhmQ3, 2.9, 3.3);
        Assert.Equal(512 / s.FwhmMedian, s.EffectiveDetailPx(512), 6);
        Assert.False(s.SoftCorners);
    }

    /// <summary>
    /// The grain of a noisy sky is not a star (ADR 0054): on a flat sky with Gaussian noise of
    /// sigma 0.02 the window maxima stand about 3 sigmas over the border median, past the
    /// absolute floor of 3000/65535, and without the sigma clause 429 of them pass as stars on
    /// this 600 x 600 frame (measured 2026-10-27 with StarMinSnr set to 0); a star must be
    /// StarMinSnr sigmas over its base.
    /// </summary>
    [Fact]
    public void R070_NoiseGrainIsNotAStar()
    {
        var s = StarAnalyzer.Analyze(SyntheticRasters.AddNoise(SyntheticRasters.Flat(600, 600, 0.2f), 0.02, 7));

        Assert.True(s.Insufficient, $"{s.Count} grain bumps passed as stars");
        Assert.True(double.IsNaN(s.FwhmMedian));
    }

    /// <summary>
    /// Thirty stars pass the count floor, but on a 2000 x 2000 frame they are 7.5 per
    /// megapixel, under the density floor: a starless frame on which a denoise left a few dozen
    /// soft blobs. The same thirty on 1000 x 1000 are 30 per megapixel: a sparse field, measured.
    /// </summary>
    [Fact]
    public void R070_TooFewStarsPerMegapixelAreInsufficientEvenOverTheCountFloor()
    {
        var sparse = StarAnalyzer.Analyze(Scenes.WithStars(SyntheticRasters.Flat(2000, 2000, 0.2f), count: 30, fwhm: 3.0, amplitude: 0.3f));
        Assert.InRange(sparse.Count, 25, 30);
        Assert.InRange(sparse.DensityPerMp, 6.0, 7.6);
        Assert.True(sparse.Insufficient);
        Assert.True(double.IsNaN(sparse.FwhmMedian));

        var dense = StarAnalyzer.Analyze(Scenes.WithStars(SyntheticRasters.Flat(1000, 1000, 0.2f), count: 30, fwhm: 3.0, amplitude: 0.3f));
        Assert.InRange(dense.Count, 25, 30);
        Assert.InRange(dense.DensityPerMp, 25.0, 30.0);
        Assert.False(dense.Insufficient);
    }

    /// <summary>
    /// The sigma clause keeps the stars: the same noisy sky (sigma 0.02) with stars of 0.3
    /// (15 sigmas) over it is still measured as before, and the grain between them is not.
    /// </summary>
    [Fact]
    public void R070_StarsOverGrainAreStillMeasured()
    {
        Raster r = SyntheticRasters.AddNoise(Scenes.WithStars(SyntheticRasters.Flat(600, 600, 0.2f), fwhm: 3.0, amplitude: 0.3f), 0.02, 7);
        var s = StarAnalyzer.Analyze(r);

        Assert.False(s.Insufficient);
        Assert.InRange(s.Count, 55, 70);
        Assert.InRange(s.FwhmMedian, 2.8, 3.4);
    }

    /// <summary>
    /// The sigma is measured about a plane fitted to the border, so a star on a nebula's slope is
    /// judged against the grain and not the slope (ADR 0054): the DarkSky scene's nebula (0.4
    /// over 60 px) carries its 64 stars of 0.3 over 0.3 LSB of noise; the raw deviation about the
    /// border median read the slope as a sigma of about 0.03 and kept 50 of them. The scene
    /// itself is analysed, so an edit of it reaches this test.
    /// </summary>
    [Fact]
    public void R070_StarsOnANebulasSlopeAreKept()
    {
        var s = StarAnalyzer.Analyze(Scenes.DarkSky());

        Assert.InRange(s.Count, 60, 70);
        Assert.InRange(s.FwhmMedian, 2.95, 3.2);
    }

    /// <summary>
    /// A wide star lays its own wings on the window border (a Gaussian of FWHM 14, sigma 5.9,
    /// puts 13 % of its amplitude on the border's middle pixels and 2 % on its corners), and the
    /// plane cannot take a radial wing out; the spread of those wings is the border's "sigma".
    /// Up to the accepted width (StarFwhmMax 15) that sigma stays well under a twelfth of the
    /// amplitude, so the star does not reject itself (ADR 0054, the review of 2026-10-27: all 64
    /// kept at 12, 13 and 14). The same wings lift the border median, the base, so a wide star
    /// reads narrower: 12.50 at 13 and 13.19 at 14 (measured 2026-10-27), a bias of the
    /// estimator older than the clause, within 7 % here.
    /// </summary>
    [Theory]
    [InlineData(12.0)]
    [InlineData(13.0)]
    [InlineData(14.0)]
    public void R070_AWideStarDoesNotRejectItself(double fwhm)
    {
        var s = StarAnalyzer.Analyze(StarField(fwhm));

        Assert.InRange(s.Count, 55, 70);
        Assert.InRange(s.FwhmMedian, fwhm * 0.93, fwhm * 1.06);
    }

    /// <summary>
    /// A flat two-pixel peak (two adjacent pixels at the same value, each the maximum of its
    /// window) is one star, at its first pixel in scan order (ADR 0041); before it both pixels
    /// passed and the star was counted twice.
    /// </summary>
    [Fact]
    public void R070_AFlatTwoPixelPeakIsMeasuredOnce()
    {
        Raster r = SyntheticRasters.AddStar(SyntheticRasters.Flat(128, 128, Sky), 60, 60, 3.0, 0.3f);
        foreach (float[] plane in r.Planes) plane[60 * 128 + 61] = plane[60 * 128 + 60];   // the peak's right neighbour equals it
        var s = StarAnalyzer.Analyze(r);

        Star star = Assert.Single(s.Stars);
        Assert.Equal(60, star.X);
        Assert.Equal(60, star.Y);

        // A single-pixel peak is one star too, and a flat peak down a column counts once, at the top.
        Raster column = SyntheticRasters.AddStar(SyntheticRasters.Flat(128, 128, Sky), 60, 60, 3.0, 0.3f);
        foreach (float[] plane in column.Planes) plane[61 * 128 + 60] = plane[60 * 128 + 60];
        Star top = Assert.Single(StarAnalyzer.Analyze(column).Stars);
        Assert.Equal(60, top.Y);

        // A flat peak whose first pixel lies in the border the scan leaves out: the second pixel,
        // the first the scan reaches, is the star; a tied pixel outside the scan disqualifies nothing.
        int half = Thresholds.StarWindow / 2;
        Raster edge = SyntheticRasters.AddStar(SyntheticRasters.Flat(128, 128, Sky), half, 60, 3.0, 0.3f);
        foreach (float[] plane in edge.Planes) plane[60 * 128 + half - 1] = plane[60 * 128 + half];
        Star kept = Assert.Single(StarAnalyzer.Analyze(edge).Stars);
        Assert.Equal(half, kept.X);
    }

    /// <summary>
    /// The window is Thresholds.StarWindow (25): two equal stars 12 px apart share a window and
    /// count once, 13 px apart they are two; a star within 12 px of the border is not looked at.
    /// </summary>
    [Fact]
    public void R070_TheWindowIsTheThresholdsStarWindow()
    {
        int half = Thresholds.StarWindow / 2;
        Assert.Equal(12, half);
        Raster inside = SyntheticRasters.AddStar(SyntheticRasters.AddStar(SyntheticRasters.Flat(128, 128, Sky), 60, 60, 3.0, 0.3f), 60 + half, 60, 3.0, 0.3f);
        Assert.Single(StarAnalyzer.Analyze(inside).Stars);
        Raster apart = SyntheticRasters.AddStar(SyntheticRasters.AddStar(SyntheticRasters.Flat(128, 128, Sky), 60, 60, 3.0, 0.3f), 60 + half + 1, 60, 3.0, 0.3f);
        Assert.Equal(2, StarAnalyzer.Analyze(apart).Stars.Count);
        Raster border = SyntheticRasters.AddStar(SyntheticRasters.Flat(128, 128, Sky), half - 1, 60, 3.0, 0.3f);
        Assert.Empty(StarAnalyzer.Analyze(border).Stars);
    }

    [Fact]
    public void WiderStarsGiveAWiderFwhm()
    {
        var s = StarAnalyzer.Analyze(StarField(5.0));

        Assert.InRange(s.FwhmMedian, 4.9, 5.3);
    }

    [Fact]
    public void SoftCornersAreSeenAgainstTheCentre()
    {
        // The centre (40 % of the side, 154..358): 16 stars of 3.0. The corners (20 %, 102 px):
        // 9 stars of 4.6 each. The ratio 1.53 passes 1.4.
        Raster r = SyntheticRasters.Flat(512, 512, Sky);
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                r = SyntheticRasters.AddStar(r, 175 + i * 50, 175 + j * 50, 3.0, 0.3f);
        foreach (int ox in new[] { 0, 410 })
            foreach (int oy in new[] { 0, 410 })
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        r = SyntheticRasters.AddStar(r, ox + 20 + i * 30, oy + 20 + j * 30, 4.6, 0.3f);

        var s = StarAnalyzer.Analyze(r);

        Assert.Equal("centru", s.Regions[0].Name);
        Assert.InRange(s.Regions[0].FwhmMedian, 2.95, 3.2);
        Assert.All(s.Regions.Skip(1), c => Assert.InRange(c.FwhmMedian, 4.5, 4.9));
        Assert.InRange(s.CornerToCenterRatio, 1.42, 1.62);
        Assert.True(s.SoftCorners);
    }

    [Fact]
    public void SaturatedAndFaintStarsAreNotMeasured()
    {
        Raster r = SyntheticRasters.Flat(256, 256, Sky);
        r = SyntheticRasters.AddStar(r, 60, 60, 3.0, 0.95f);    // peak at 1.0: saturated
        r = SyntheticRasters.AddStar(r, 180, 180, 3.0, 0.03f);  // under the minimum amplitude

        var s = StarAnalyzer.Analyze(r);

        Assert.Equal(0, s.Count);
        Assert.True(s.Insufficient);
        Assert.True(double.IsNaN(s.FwhmMedian));
        Assert.True(double.IsNaN(s.CornerToCenterRatio));
        Assert.False(s.SoftCorners);
    }

    [Fact]
    public void AnEmptySkyIsInsufficientNotAnError()
    {
        var s = StarAnalyzer.Analyze(SyntheticRasters.AddNoise(SyntheticRasters.Flat(200, 200, Sky), 1.0 / 255, 2));

        Assert.True(s.Insufficient);
        Assert.Equal(5, s.Regions.Count);
        Assert.All(s.Regions, reg => Assert.False(reg.Known));
    }

    [Fact]
    public void TheHalfWidthOfATriangleIsExact()
    {
        // A triangle with its peak of 1 at the centre and a slope of 1/3: at the half it is exactly 3.0 wide.
        var line = new double[25];
        for (int i = 0; i < 25; i++) line[i] = Math.Max(0, 1 - Math.Abs(i - 12) / 3.0);

        Assert.Equal(3.0, StarAnalyzer.WidthAtHalf(line, 0.5), 3);
    }
}
