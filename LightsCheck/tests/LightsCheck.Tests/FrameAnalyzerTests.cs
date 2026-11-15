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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// The frame analyzer on synthetic frames with known answers (R-012 to R-016). Absolute
/// tolerances are loose where the method itself biases a figure (the green interpolation and
/// the finite moment window broaden a FWHM by a few tenths); the exact figures against the
/// prototype are the validation cases on the real frames.
/// </summary>
public class FrameAnalyzerTests
{
    private static FrameMeasurement Analyze(SyntheticFrame f) =>
        FrameAnalyzer.Analyze(f.Render(), f.Scale, f.Mosaic ? SyntheticFrame.Rggb : null);

    [Fact]
    public void R012_ChannelLevelsAreTheMosaicsBackgroundsAboveBlackWithSaturatedCounts()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 400, Height = 300, NoiseSigma = 0 }.WithStarGrid(4, 3.0, 20000, 20000);
        FrameMeasurement m = Analyze(f);

        Assert.Equal(["R", "G1", "G2", "B"], m.Channels.Select(c => c.Name).ToArray());
        Assert.Equal(30, m.Channels[0].Median, 0.5);
        Assert.Equal(150, m.Channels[1].Median, 0.5);
        Assert.Equal(150, m.Channels[2].Median, 0.5);
        Assert.Equal(170, m.Channels[3].Median, 0.5);
        Assert.Equal("B", m.BrightestChannel.Name);
        Assert.True(m.Channels.Sum(c => c.Saturated) > 4, "four stars peaking far above the scale saturate pixels");
        Assert.Equal(4, m.SaturatedStars);
        Assert.All(m.Channels, c => Assert.True(c.Min >= 0));
    }

    /// <summary>A Siril light is a float FITS on a 0..1 scale: a 1 ADU margin would saturate every pixel.</summary>
    [Fact]
    public void R012_AFloatScaleFrameHasNoSpuriousSaturation()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 800, Height = 600 }.WithStarGrid(30, 3.0, 400, 2500);
        f.Stars.Add(SyntheticStar.Round(400, 300, 20000, 3.0));
        Img img = f.Render();
        Raster scaled = new(img.H, img.W);
        for (int i = 0; i < img.D.Length; i++)
        {
            scaled.D[i] = (float)(img.D[i] / f.White);
        }

        FrameMeasurement m = FrameAnalyzer.Analyze(scaled, new AduScale(1.0, 0, "test"), SyntheticFrame.Rggb);
        Assert.Equal(1, m.SaturatedStars);
        Assert.True(m.Channels.Sum(c => c.Saturated) < 100, "only the one star at the top of the scale saturates pixels");
        Assert.InRange(m.Detected, 29, 33);
        Assert.InRange(m.FwhmMedian!.Value, 2.8, 3.6);
    }

    /// <summary>A frame smaller than one background tile is one tile, not an empty grid.</summary>
    [Fact]
    public void R013_AFrameSmallerThanATileIsStillMeasured()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 100, Height = 80 }.WithStarGrid(3, 3.0, 800, 1000);
        FrameMeasurement m = Analyze(f);
        Assert.InRange(m.BackgroundMedian, 140, 160);
        Assert.InRange(m.Detected, 2, 4);
    }

    [Fact]
    public void R013_BackgroundAndNoiseAreReadFromTheGreenChannel()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 1024, Height = 768, NoiseSigma = 12 }.WithStarGrid(30, 3.0, 300, 1500);
        FrameMeasurement m = Analyze(f);

        Assert.InRange(m.BackgroundMedian, 148, 152);
        Assert.InRange(m.BackgroundCentre, 148, 152);
        Assert.All(m.BackgroundCorners, c => Assert.InRange(c, 148, 152));
        Assert.InRange(m.BackgroundMin, 146, 150);
        Assert.InRange(m.BackgroundMax, 150, 154);
        // The interpolated green is half original pixels, half means of four: its noise is below the pixel noise.
        Assert.InRange(m.NoiseSigma, 12 * 0.6, 12 * 1.05);
    }

    [Fact]
    public void R014_RoundStarsAreCountedAndMeasuredRoundWithTheirFwhm()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 1200, Height = 800 }.WithStarGrid(60, 3.0, 400, 2500);
        FrameMeasurement m = Analyze(f);

        Assert.InRange(m.Detected, 58, 64);
        Assert.InRange(m.DetectedBright, 50, 64);
        Assert.Equal(0, m.SaturatedStars);
        Assert.InRange(m.Shapes.Count, 55, 60);
        Assert.InRange(m.FwhmMedian!.Value, 2.8, 3.6);
        Assert.InRange(m.ElongationMedian!.Value, 1.0, 1.12);
        Assert.InRange(m.ElongatedFraction!.Value, 0, 0.05);
        Assert.Null(m.AngleDeg);
        Assert.True(m.Centre.Stars + m.Mid.Stars + m.Edge.Stars == m.Shapes.Count);
        Assert.InRange(m.Halo!.Value, -0.05, 0.1);
        Assert.Empty(m.Trails);
    }

    [Fact]
    public void R014_ElongatedStarsGiveTheRatioTheAngleAndACoherentVote()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 1200, Height = 800 }.WithElongatedGrid(60, 2.8, 1.8, 80, 400, 2500);
        FrameMeasurement m = Analyze(f);

        Assert.InRange(m.ElongationMedian!.Value, 1.6, 1.95);
        Assert.InRange(m.ElongatedFraction!.Value, 0.9, 1.0);
        Assert.NotNull(m.AngleDeg);
        Assert.InRange(m.AngleDeg!.Value, 75, 85);
        Assert.InRange(m.AngleCoherence!.Value, 0.95, 1.0);
        Assert.InRange(m.Edge.Elongation!.Value, 1.6, 1.95);
    }

    [Fact]
    public void R014_AMonoFrameIsMeasuredWithoutInterpolation()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 800, Height = 600, Mosaic = false }.WithStarGrid(30, 3.0, 400, 2500);
        FrameMeasurement m = Analyze(f);

        Assert.All(m.Channels, c => Assert.Equal(150, c.Median, 1));
        Assert.StartsWith("CFA[", m.Channels[0].Name, StringComparison.Ordinal);
        Assert.InRange(m.FwhmMedian!.Value, 2.8, 3.3);
        Assert.InRange(m.Detected, 29, 32);
    }

    /// <summary>
    /// Stars 10, 15 and 20 px wide at a peak of 200 sigma cover 500 to 2200 px each: all are
    /// counted, bright, measured at their width and kept for registration, since the limits of
    /// area and of width follow the stars' width (R-014, ADR 0059). With the limits of 400 px
    /// and 15 px none was counted.
    /// </summary>
    [Theory]
    [InlineData(10.0)]
    [InlineData(15.0)]
    [InlineData(20.0)]
    public void R014_BrightWideStarsAreCountedBrightAndMeasuredAtTheirWidth(double fwhm)
    {
        SyntheticFrame f = new SyntheticFrame { Width = 2400, Height = 1600 }.WithStarGrid(40, fwhm, 2400, 2400);
        FrameMeasurement m = Analyze(f);

        Assert.InRange(m.Detected, 40, 41);
        Assert.InRange(m.DetectedBright, 40, 41);
        Assert.Equal(0, m.SaturatedStars);
        Assert.Equal(40, m.Shapes.Count);
        Assert.InRange(m.FwhmMedian!.Value, 0.97 * fwhm, 1.05 * fwhm);
        Assert.InRange(m.ElongationMedian!.Value, 1.0, 1.1);
        Assert.InRange(m.RegistrationStars.Count, 40, 41);
        Assert.Empty(m.Trails);
    }

    /// <summary>
    /// A knot of nebula four star widths across, at 25 times the sky's noise, among stars 10 px
    /// wide. It covers about 4700 px, over the 20 widths squared a star may cover: the stars
    /// are all counted and the knot is not, nor is it measured or kept for registration (R-014).
    /// The noise may leave a speck or two on the knot's rim, which are counted as any speck is.
    /// </summary>
    [Fact]
    public void R014_AKnotOfNebulaAmongWideStarsIsNotAStar()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 2400, Height = 1600 }.WithStarGrid(40, 10.0, 2400, 2400);
        f.Stars.Add(SyntheticStar.Round(330, 344, 300, 40.0));
        FrameMeasurement m = Analyze(f);

        Assert.InRange(m.Detected, 40, 44);
        Assert.Equal(40, m.DetectedBright);
        Assert.Equal(40, m.Shapes.Count);
        Assert.DoesNotContain(m.RegistrationStars, s => Math.Abs(s.X - 330) < 20 && Math.Abs(s.Y - 344) < 20);
        Assert.DoesNotContain(m.Shapes, s => Math.Abs(s.X - 330) < 60 && Math.Abs(s.Y - 344) < 60);
    }

    /// <summary>
    /// Under 4.5 px of width the limits are the 400 px and the 15 px they were: among stars
    /// 3 px wide a round patch 11 px wide, about 330 px above the threshold, is over 20 widths
    /// squared and over three widths, and is counted and measured all the same (R-014, the two
    /// floors of ADR 0059).
    /// </summary>
    [Fact]
    public void R014_UnderFourAndAHalfPixelsTheLimitsAreThoseInPixels()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 2400, Height = 1600 }.WithStarGrid(40, 3.0, 2400, 2400);
        f.Stars.Add(SyntheticStar.Round(330, 344, 230, 11.0));
        FrameMeasurement m = Analyze(f);

        Assert.InRange(m.FwhmMedian!.Value, 2.8, 3.6);
        Assert.InRange(m.Detected, 41, 42);
        Assert.Contains(m.RegistrationStars, s => Math.Abs(s.X - 330) < 5 && Math.Abs(s.Y - 344) < 5);
        StarShape patch = Assert.Single(m.Shapes, s => Math.Abs(s.X - 330) < 5 && Math.Abs(s.Y - 344) < 5);
        Assert.InRange(patch.Fwhm, Th.MaxFwhmWidths * m.FwhmMedian.Value, Th.MaxFwhmFloor);
    }

    /// <summary>
    /// A star with a broad skirt covers about three and a half times the area of a plain one,
    /// some 15 squares of its width as read: it is a star, under the 20 the limit allows, and every drawn star
    /// is kept for registration (R-014, ADR 0059). The noise leaves specks on the skirts' rims,
    /// which are counted as any speck is.
    /// </summary>
    [Fact]
    public void R014_StarsWithABroadSkirtAreCounted()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 2400, Height = 1600 }.WithStarGrid(40, 10.0, 2000, 2000);
        foreach (SyntheticStar s in f.Stars.ToList())
        {
            f.Stars.Add(SyntheticStar.Round(s.X, s.Y, 150, 30.0));
        }

        FrameMeasurement m = Analyze(f);
        Assert.Equal(40, m.DetectedBright);
        Assert.All(f.Stars.Take(40), d => Assert.Contains(m.RegistrationStars, s => Math.Abs(s.X - d.X) < 3 && Math.Abs(s.Y - d.Y) < 3));
    }

    /// <summary>
    /// A frame whose stars are all saturated has no star to read a width on, so the limit of
    /// area stays the 400 px it was: stars 10 px wide, saturated, are over it and are not
    /// counted (R-014: a frame without a first width keeps the limits in pixels).
    /// </summary>
    [Fact]
    public void R014_AFrameWithoutAnUnsaturatedBrightStarKeepsTheLimitInPixels()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 2400, Height = 1600 }.WithStarGrid(40, 10.0, 20000, 20000);
        FrameMeasurement m = Analyze(f);

        Assert.Equal(40, m.SaturatedStars);
        Assert.Empty(m.Shapes);
        Assert.Null(m.FwhmMedian);
        Assert.Equal(0, m.Detected);
    }

    /// <summary>
    /// The frame's first width is the middle value of what is read, so a few bright patches
    /// among more stars do not move it: six stars 3 px wide and three round patches 20 px wide
    /// read the stars' width, and the patches, some 1300 px each, are no stars (R-014). Their
    /// mean width would let the patches in.
    /// </summary>
    [Fact]
    public void R014_AFewBrightPatchesDoNotSetTheStarsWidth()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 1600, Height = 1200 };
        (double X, double Y)[] stars = [(300, 300), (800, 300), (1300, 300), (300, 900), (800, 900), (1300, 900)];
        (double X, double Y)[] patches = [(550, 600), (1050, 600), (800, 600)];
        foreach ((double x, double y) in stars)
        {
            f.Stars.Add(SyntheticStar.Round(x, y, 2000, 3.0));
        }

        foreach ((double x, double y) in patches)
        {
            f.Stars.Add(SyntheticStar.Round(x, y, 400, 20.0));
        }

        FrameMeasurement m = Analyze(f);
        Assert.InRange(m.FwhmMedian!.Value, 2.8, 3.6);
        Assert.Equal(6, m.Shapes.Count);
        Assert.InRange(m.Detected, 6, 8);
        Assert.All(patches, p => Assert.DoesNotContain(m.RegistrationStars, s => Math.Abs(s.X - p.X) < 10 && Math.Abs(s.Y - p.Y) < 10));
    }

    /// <summary>
    /// The rings are each star's own, not the frame's: in a frame of 39 stars 5 px wide and 21
    /// stars 10 px wide, where the frame's width is the narrow stars' and most of the 40
    /// brightest are the wide ones, the halo still reads about zero (R-014, ADR 0059). Rings of
    /// the frame's one width read this frame about 0.07.
    /// </summary>
    [Fact]
    public void R014_TheHaloIsReadInRingsOfEachStarsOwnWidth()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 2400, Height = 1600 }.WithStarGrid(60, 5.0, 2400, 2400);
        for (int i = 0; i < 21; i++)
        {
            SyntheticStar s = f.Stars[i * 60 / 21];
            f.Stars[i * 60 / 21] = SyntheticStar.Round(s.X, s.Y, 1000, 10.0);
        }

        FrameMeasurement m = Analyze(f);
        Assert.InRange(m.FwhmMedian!.Value, 4.8, 5.6);
        Assert.Equal(60, m.Shapes.Count);
        Assert.Equal(21, m.Shapes.Take(Th.HaloStars).Count(s => s.Fwhm > 8));
        Assert.InRange(m.Halo!.Value, -0.02, 0.02);
    }

    /// <summary>
    /// The halo is read in rings that follow each star's width: plain stars read about zero
    /// whether 3 or 10 px wide, and the same stars with a tenth of their light in a skirt five
    /// widths wide read the same halo at both widths (R-014, ADR 0059). Rings of 3, 5 and 10 px
    /// read a plain star of 10 px 2.0.
    /// </summary>
    [Theory]
    [InlineData(3.0)]
    [InlineData(10.0)]
    public void R014_TheHaloReadsTheSameOnNarrowAndOnWideStars(double fwhm)
    {
        SyntheticFrame plain = new SyntheticFrame { Width = 2400, Height = 1600 }.WithStarGrid(40, fwhm, 2400, 2400);
        Assert.InRange(Analyze(plain).Halo!.Value, -0.03, 0.03);

        SyntheticFrame skirt = new SyntheticFrame { Width = 2400, Height = 1600 }.WithStarGrid(40, fwhm, 2160, 2160);
        foreach (SyntheticStar s in skirt.Stars.ToList())
        {
            // A skirt 5 widths wide is 25 times the core's area: a tenth of the light at 1/225 of the peak.
            skirt.Stars.Add(SyntheticStar.Round(s.X, s.Y, 2400 * 0.1 / 25, 5 * fwhm));
        }

        Assert.InRange(Analyze(skirt).Halo!.Value, 0.02, 0.07);
    }

    /// <summary>
    /// A faint streak about 90 px long is a trail of 251 to 400 px, an area a star may have: it
    /// is a trail and nothing else, and the frame counts the stars it counts without the streak
    /// (R-015; CR-013 of the review of 2026-11-13).
    /// </summary>
    [Fact]
    public void R015_AShortThinStreakIsATrailAndNotAStar()
    {
        SyntheticFrame without = new SyntheticFrame { Width = 1000, Height = 700 }.WithStarGrid(20, 3.0, 400, 2000);
        SyntheticFrame with = new SyntheticFrame { Width = 1000, Height = 700, TrailLine = (300, 300, 362, 362, 70) }.WithStarGrid(20, 3.0, 400, 2000);
        FrameMeasurement m0 = Analyze(without), m = Analyze(with);

        Trail t = Assert.Single(m.Trails);
        Assert.InRange(t.Area, Th.TrailMinArea + 1, 400);
        Assert.Empty(m0.Trails);
        Assert.Equal(m0.Detected, m.Detected);
        Assert.Equal(m0.RegistrationStars.Count, m.RegistrationStars.Count);
        Assert.DoesNotContain(m.RegistrationStars, s => s.X > 295 && s.X < 367 && Math.Abs(s.X - s.Y) < 5);
    }

    /// <summary>
    /// Among stars 10 px wide a component may cover about 2000 px and be a star; a bright
    /// streak of some 670 px that fades along its length is under that, and the shape
    /// measurement reads it, about 27 px wide, under the three widths a shape may have. It is a
    /// trail and nothing else: the frame counts and measures what it does without the streak,
    /// and the streak is not kept for registration (R-015).
    /// </summary>
    [Fact]
    public void R015_ABrightStreakAmongWideStarsIsATrailAndNotAStar()
    {
        SyntheticFrame without = new SyntheticFrame { Width = 2400, Height = 1600 }.WithStarGrid(40, 10.0, 2400, 2400);
        SyntheticFrame with = new SyntheticFrame { Width = 2400, Height = 1600 }.WithStarGrid(40, 10.0, 2400, 2400);
        with.Stars.Add(SyntheticStar.Elongated(331, 331, 1500, 3.0, 12.5, 45));
        FrameMeasurement m0 = Analyze(without), m = Analyze(with);

        Trail t = Assert.Single(m.Trails);
        Assert.InRange(t.Area, 401, 1500);
        Assert.Empty(m0.Trails);
        Assert.Equal(m0.Detected, m.Detected);
        Assert.Equal(40, m.Shapes.Count);
        Assert.DoesNotContain(m.Shapes, s => Math.Abs(s.X - 331) < 40 && Math.Abs(s.Y - 331) < 40);
        Assert.DoesNotContain(m.RegistrationStars, s => Math.Abs(s.X - 331) < 40 && Math.Abs(s.Y - 331) < 40);
    }

    /// <summary>
    /// A shape is kept up to three times the frame's first width, never under 15 px. Among
    /// stars 3 px wide a streak along a row that reads about 17 px, and among stars 10 px wide
    /// one that reads about 34 px, over the 30 px three widths allow: each is counted and kept
    /// for registration, being under the limit of area and no trail, and is not among the
    /// measured stars (R-014).
    /// </summary>
    [Theory]
    [InlineData(3.0, 1000.0, 2.0, 12.0)]
    [InlineData(10.0, 2000.0, 6.0, 8.0)]
    public void R014_AShapeWiderThanTheLimitOfWidthIsNotKept(double fwhm, double peak, double fwhmMinor, double elongation)
    {
        SyntheticFrame f = new SyntheticFrame { Width = 2400, Height = 1600 }.WithStarGrid(40, fwhm, 2400, 2400);
        f.Stars.Add(SyntheticStar.Elongated(330, 344, peak, fwhmMinor, elongation, 0));
        FrameMeasurement m = Analyze(f);

        Assert.Empty(m.Trails);
        Assert.InRange(m.Detected, 41, 42);
        Assert.Contains(m.RegistrationStars, s => Math.Abs(s.X - 330) < 8 && Math.Abs(s.Y - 344) < 8);
        Assert.Equal(40, m.Shapes.Count);
        Assert.DoesNotContain(m.Shapes, s => Math.Abs(s.X - 330) < 40 && Math.Abs(s.Y - 344) < 40);
    }

    [Fact]
    public void R015_ALongThinStreakIsATrailNotAStar()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 1000, Height = 700, TrailLine = (100, 100, 700, 400, 900) }.WithStarGrid(20, 3.0, 400, 2000);
        FrameMeasurement m = Analyze(f);

        Trail t = Assert.Single(m.Trails);
        Assert.InRange(t.Length, 600, 700);
        Assert.InRange(t.X, 95, 105);
        Assert.InRange(t.Y, 95, 105);
        Assert.InRange(m.Detected, 18, 22);
    }

    [Fact]
    public void R016_TheRegistrationListHoldsTheBrightestStarsByFluxSaturatedIncluded()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 1000, Height = 700 }.WithStarGrid(40, 3.0, 400, 2000);
        f.Stars.Add(SyntheticStar.Round(500, 350, 20000, 3.0));
        FrameMeasurement m = Analyze(f);

        Assert.InRange(m.RegistrationStars.Count, 39, 42);
        Assert.Equal(m.RegistrationStars.OrderByDescending(s => s.Flux).Select(s => s.Flux), m.RegistrationStars.Select(s => s.Flux));
        RegistrationStar brightest = m.RegistrationStars[0];
        Assert.Equal(500, brightest.X, 1.0);
        Assert.Equal(350, brightest.Y, 1.0);
        Assert.Equal(1, m.SaturatedStars);
        Assert.DoesNotContain(m.Shapes, s => Math.Abs(s.X - 500) < 2 && Math.Abs(s.Y - 350) < 2);
    }

    /// <summary>
    /// Under a sky at 22 % of the scale a clipped star's peak above the sky is under 0.8 of the
    /// scale; it is saturated all the same and must not be measured for shape (R-014).
    /// </summary>
    [Fact]
    public void R014_ASaturatedStarIsNotMeasuredForShapeUnderABrightSky()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 1000, Height = 700, BackgroundG = 900 }.WithStarGrid(40, 3.0, 400, 2000);
        f.Stars.Add(SyntheticStar.Round(500, 350, 20000, 3.0));
        FrameMeasurement m = Analyze(f);

        Assert.Equal(1, m.SaturatedStars);
        Assert.InRange(m.Shapes.Count, 35, 40);
        Assert.DoesNotContain(m.Shapes, s => Math.Abs(s.X - 500) < 2 && Math.Abs(s.Y - 350) < 2);
    }

    [Fact]
    public void R016_StarCentroidsLandOnTheDrawnPositions()
    {
        SyntheticFrame f = new SyntheticFrame { Width = 800, Height = 600, NoiseSigma = 3 }.WithStarGrid(12, 3.0, 2000, 2500);
        FrameMeasurement m = Analyze(f);
        foreach (SyntheticStar s in f.Stars)
        {
            RegistrationStar r = m.RegistrationStars.MinBy(r => (r.X - s.X) * (r.X - s.X) + (r.Y - s.Y) * (r.Y - s.Y))!;
            Assert.Equal(s.X, r.X, 0.3);
            Assert.Equal(s.Y, r.Y, 0.3);
        }
    }
}
