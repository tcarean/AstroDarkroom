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

/// <summary>The second-moment shape of a single drawn Gaussian, noise-free (R-014).</summary>
public class StarShapeTests
{
    private static Raster Gaussian(int size, double cx, double cy, double sa, double sb, double angleDeg, double peak)
    {
        Raster r = new(size, size);
        double ca = Math.Cos(angleDeg * Math.PI / 180), sn = Math.Sin(angleDeg * Math.PI / 180);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double dx = x - cx, dy = y - cy;
                double u = dx * ca + dy * sn, v = -dx * sn + dy * ca;
                r.D[y * size + x] = (float)(peak * Math.Exp(-0.5 * (u * u / (sa * sa) + v * v / (sb * sb))));
            }
        }

        return r;
    }

    [Fact]
    public void R014_ARoundGaussianGivesItsFwhmAndElongationOne()
    {
        double sigma = 1.3;
        Raster r = Gaussian(41, 20.3, 19.6, sigma, sigma, 0, 1000);
        StarShape s = StarShapes.Measure(r, 19, 20, 6, 3, 1000, 30)!;

        Assert.Equal(20.3, s.X, 0.02);
        Assert.Equal(19.6, s.Y, 0.02);
        // The window of radius 6 truncates the tails: a few percent under 2.3548 x sigma.
        Assert.Equal(2.3548 * sigma, s.Fwhm, 0.15);
        Assert.Equal(1.0, s.Elongation, 0.02);
    }

    [Fact]
    public void R014_AnElongatedGaussianGivesTheRatioAndTheAngleOfItsMajorAxis()
    {
        Raster r = Gaussian(41, 20, 20, 2.0, 1.0, 60, 1000);
        StarShape s = StarShapes.Measure(r, 20, 20, 6, 3, 1000, 30)!;

        Assert.InRange(s.Elongation, 1.7, 2.05);
        Assert.InRange(s.AngleDeg, 58, 62);
        Assert.True(s.FwhmMajor > s.FwhmMinor);
    }

    [Fact]
    public void R014_AStarTooCloseToTheBorderIsNotMeasured()
    {
        Raster r = Gaussian(21, 3, 10, 1.2, 1.2, 0, 500);
        Assert.Null(StarShapes.Measure(r, 10, 3, 6, 3, 500, 20));
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
}
