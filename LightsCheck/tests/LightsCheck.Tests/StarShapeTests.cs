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
/// The shape of drawn stars (R-014): the core by adaptive moments, the whole star by the moments
/// of its pixels above the noise, at any width in pixels.
/// </summary>
public class StarShapeTests
{
    /// <summary>
    /// A raster with one Gaussian sampled at the pixel centres, and a second copy of it
    /// <paramref name="copyDx"/> pixels to the right at <paramref name="copy"/> times its peak.
    /// </summary>
    private static Raster Gaussian(int size, double cx, double cy, double sa, double sb, double angleDeg, double peak, double copy = 0, double copyDx = 0)
    {
        Raster r = new(size, size);
        Add(r, cx, cy, sa, sb, angleDeg, peak);
        if (copy > 0)
        {
            Add(r, cx + copyDx, cy, sa, sb, angleDeg, peak * copy);
        }

        return r;
    }

    private static void Add(Raster r, double cx, double cy, double sa, double sb, double angleDeg, double peak)
    {
        double ca = Math.Cos(angleDeg * Math.PI / 180), sn = Math.Sin(angleDeg * Math.PI / 180);
        int reach = (int)Math.Ceiling(6 * Math.Max(sa, sb)) + 1;
        for (int y = Math.Max(0, (int)cy - reach); y < Math.Min(r.H, (int)cy + reach + 1); y++)
        {
            for (int x = Math.Max(0, (int)cx - reach); x < Math.Min(r.W, (int)cx + reach + 1); x++)
            {
                double dx = x - cx, dy = y - cy;
                double u = dx * ca + dy * sn, v = -dx * sn + dy * ca;
                r.D[y * r.W + x] += (float)(peak * Math.Exp(-0.5 * (u * u / (sa * sa) + v * v / (sb * sb))));
            }
        }
    }

    [Fact]
    public void R014_ARoundGaussianGivesItsFwhmAndElongationOne()
    {
        double sigma = 1.3;
        Raster r = Gaussian(41, 20.3, 19.6, sigma, sigma, 0, 1000);
        StarShape s = StarShapes.Measure(r, 19, 20, 1000, 30)!;

        Assert.Equal(20.3, s.X, 0.02);
        Assert.Equal(19.6, s.Y, 0.02);
        Assert.Equal(2.3548 * sigma, s.Fwhm, 0.02);
        Assert.Equal(1.0, s.Elongation, 0.02);
        Assert.Equal(1.0, s.CoreElongation, 0.02);
    }

    /// <summary>
    /// A star is read at its width whatever that is in pixels, and whatever area its component
    /// had, which only starts the measurement (R-014, ADR 0056). The moments in a fixed circle
    /// of 6 px, those of the first versions, read 4.82, 5.89, 6.52 and 6.75 for the last four.
    /// </summary>
    [Theory]
    [InlineData(2.0)]
    [InlineData(3.0)]
    [InlineData(4.0)]
    [InlineData(5.0)]
    [InlineData(7.06)]
    [InlineData(10.0)]
    [InlineData(15.0)]
    public void R014_AStarIsReadAtItsWidthWhateverItsWidthInPixels(double fwhm)
    {
        double sigma = fwhm / 2.3548;
        int half = (int)Math.Ceiling(6 * fwhm) + 20;
        Raster r = Gaussian(2 * half + 1, half + 0.3, half - 0.4, sigma, sigma, 0, 1000);

        StarShape fromSmall = StarShapes.Measure(r, half, half, 1000, 30)!;
        StarShape fromLarge = StarShapes.Measure(r, half, half, 1000, (int)(Math.PI * fwhm * fwhm))!;

        // Within 1 % from 3 px up; at 2 px the sampling at the pixel centres adds a little.
        double slack = fwhm < 3 ? 0.015 : 0.01;
        Assert.InRange(fromSmall.Fwhm, (1 - slack) * fwhm, (1 + slack) * fwhm);
        Assert.Equal(fromSmall.Fwhm, fromLarge.Fwhm, 0.005 * fwhm);
        Assert.Equal(1.0, fromSmall.Elongation, 0.03);
    }

    /// <summary>
    /// Under the noise of the sky a faint star and a bright one of the same width read the same
    /// (R-014): the moments of the first versions, cut at zero, read the faint one 30 % wider.
    /// </summary>
    [Fact]
    public void R014_AFaintStarAndABrightOneOfTheSameWidthReadTheSame()
    {
        const double Fwhm = 3.0, Noise = 14;
        double sigma = Fwhm / 2.3548;
        Random rng = new(11);
        double Width(double peak)
        {
            const int Step = 48, Side = 7;
            Raster r = new(Step * (Side + 1), Step * (Side + 1));
            for (int j = 1; j <= Side; j++)
            {
                for (int i = 1; i <= Side; i++)
                {
                    Add(r, i * Step + rng.NextDouble(), j * Step + rng.NextDouble(), sigma, sigma, 0, peak);
                }
            }

            for (int k = 0; k < r.D.Length; k++)
            {
                double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
                r.D[k] += (float)(Noise * Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
            }

            List<double> widths = [];
            for (int j = 1; j <= Side; j++)
            {
                for (int i = 1; i <= Side; i++)
                {
                    if (StarShapes.Measure(r, j * Step, i * Step, peak, 40) is StarShape s)
                    {
                        widths.Add(s.Fwhm);
                    }
                }
            }

            Assert.True(widths.Count >= 45, $"{widths.Count} of 49 stars measured");
            return Raster.Percentile(widths, 50);
        }

        double faint = Width(25 * Noise), bright = Width(200 * Noise);

        Assert.InRange(faint, 0.96 * Fwhm, 1.04 * Fwhm);
        Assert.InRange(bright, 0.98 * Fwhm, 1.02 * Fwhm);
        Assert.Equal(bright, faint, 0.03 * Fwhm);
    }

    [Fact]
    public void R014_AnElongatedGaussianGivesTheRatioAndTheAngleOfItsMajorAxis()
    {
        Raster r = Gaussian(41, 20, 20, 2.0, 1.0, 60, 1000);
        StarShape s = StarShapes.Measure(r, 20, 20, 1000, 30)!;

        Assert.InRange(s.Elongation, 1.9, 2.05);
        Assert.InRange(s.CoreElongation, 1.97, 2.03);
        Assert.InRange(s.AngleDeg, 59, 61);
        Assert.True(s.FwhmMajor > s.FwhmMinor);
        Assert.Equal(2.3548 * 2.0, s.FwhmMajor, 0.05);
        Assert.Equal(2.3548 * 1.0, s.FwhmMinor, 0.05);
    }

    /// <summary>
    /// A wide star reads its ratio and angle as a narrow one does (R-014); the first versions
    /// read a star of 10 px stretched 1.6 times as 1.10.
    /// </summary>
    [Fact]
    public void R014_AWideElongatedStarGivesItsRatioAndAngle()
    {
        double minor = 10 / 2.3548, width = 10 * Math.Sqrt((1.6 * 1.6 + 1) / 2);
        Raster r = Gaussian(241, 120.2, 119.7, 1.6 * minor, minor, 30, 1000);
        StarShape s = StarShapes.Measure(r, 120, 120, 1000, 400)!;

        Assert.InRange(s.Elongation, 1.55, 1.65);
        Assert.InRange(s.CoreElongation, 1.58, 1.62);
        Assert.InRange(s.AngleDeg, 29, 31);
        Assert.InRange(s.Fwhm, 0.99 * width, 1.01 * width);
    }

    /// <summary>
    /// A star with a second copy a third as bright 7 px away: the core is that of one copy, round
    /// and at its width, and the whole star is long (R-014; the class of a frame reads both,
    /// R-090).
    /// </summary>
    [Fact]
    public void R014_ADoubledStarReadsASharpCoreAndALongWholeStar()
    {
        double sigma = 3.0 / 2.3548;
        Raster r = Gaussian(81, 40.2, 39.8, sigma, sigma, 0, 1000, copy: 0.33, copyDx: 7);
        StarShape s = StarShapes.Measure(r, 40, 41, 1000, 60)!;

        Assert.InRange(s.Fwhm, 2.9, 3.2);
        Assert.InRange(s.CoreElongation, 1.0, 1.2);
        Assert.True(s.Elongation > 1.7, $"whole-star elongation {s.Elongation}");
        Assert.InRange(Math.Abs(s.AngleDeg), 0, 3);
    }

    /// <summary>
    /// A star smeared along a segment 2.5 times its width, a real trail, reads a core as long
    /// as the whole star, with the noise of the sky and without, so its frame is trailed and
    /// not a tail (R-090); and a faint round star stays round under the noise.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(14.0)]
    public void R090_ASmearedStarReadsACoreAsLongAsTheWholeStar(double noise)
    {
        const double Fwhm = 3.0;
        double sigma = Fwhm / 2.3548;
        Random rng = new(5);
        List<double> whole = [], core = [], round = [];
        for (int rep = 0; rep < (noise > 0 ? 30 : 1); rep++)
        {
            Raster smeared = new(121, 121), plain = new(121, 121);
            const int Steps = 30;
            for (int k = 0; k < Steps; k++)
            {
                Add(smeared, 60.3, 59.6 - 1.25 * Fwhm + 2.5 * Fwhm * (k + 0.5) / Steps, sigma, sigma, 0, 60 * 14.0 / 2.6);
            }

            for (int i = 0; i < smeared.D.Length; i++)
            {
                smeared.D[i] /= Steps / 2.6f;
            }

            Add(plain, 60.3, 59.6, sigma, sigma, 0, 25 * 14.0);
            for (int i = 0; noise > 0 && i < smeared.D.Length; i++)
            {
                double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
                double g = noise * Math.Sqrt(-2 * Math.Log(u1));
                smeared.D[i] += (float)(g * Math.Cos(2 * Math.PI * u2));
                plain.D[i] += (float)(g * Math.Sin(2 * Math.PI * u2));
            }

            StarShape s = StarShapes.Measure(smeared, 60, 60, 800, 60)!;
            whole.Add(s.Elongation);
            core.Add(s.CoreElongation);
            round.Add(StarShapes.Measure(plain, 60, 60, 350, 30)!.Elongation);
        }

        double w = Raster.Percentile(whole, 50), c = Raster.Percentile(core, 50);
        Assert.True(w >= Th.ElongationBad, $"whole star {w}");
        Assert.True(c >= Th.ElongationBad, $"core {c}");
        Assert.True(w < Th.TailCoreRatio * c, $"whole star {w} against core {c}");
        Assert.InRange(Raster.Percentile(round, 50), 1.0, 1.15);
    }

    [Fact]
    public void R014_AStarTooCloseToTheBorderIsNotMeasured()
    {
        Raster r = Gaussian(21, 3, 10, 1.2, 1.2, 0, 500);
        Assert.Null(StarShapes.Measure(r, 10, 3, 500, 20));
        Assert.Null(StarShapes.Halo(r, 10, 3, 3, 5, 10));
    }

    [Fact]
    public void R014_HaloOfATightGaussianIsNearZeroAndOfAWideOneLarge()
    {
        Raster tight = Gaussian(41, 20, 20, 1.2, 1.2, 0, 1000);
        Raster wide = Gaussian(41, 20, 20, 4.0, 4.0, 0, 1000);
        Assert.InRange(StarShapes.Halo(tight, 20, 20, 3, 5, 10)!.Value, 0, 0.02);
        Assert.True(StarShapes.Halo(wide, 20, 20, 3, 5, 10)!.Value > 1.0);
    }

    /// <summary>
    /// In rings of the star's own width (<see cref="Th.HaloCoreWidths"/>,
    /// <see cref="Th.HaloInnerWidths"/>, <see cref="Th.HaloOuterWidths"/>) a Gaussian reads the
    /// same halo, near zero, whatever its width in pixels (R-014, ADR 0059).
    /// </summary>
    [Theory]
    [InlineData(2.5)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    [InlineData(20.0)]
    public void R014_HaloInRingsOfTheStarsOwnWidthIsNearZeroAtAnyWidth(double fwhm)
    {
        double sigma = fwhm / 2.3548;
        Raster r = Gaussian(201, 100, 100, sigma, sigma, 0, 1000);
        double halo = StarShapes.Halo(r, 100, 100, Th.HaloCoreWidths * fwhm, Th.HaloInnerWidths * fwhm, Th.HaloOuterWidths * fwhm)!.Value;
        Assert.InRange(halo, 0, 0.001);
    }
}
