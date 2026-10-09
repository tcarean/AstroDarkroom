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
