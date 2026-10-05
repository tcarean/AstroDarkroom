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

public class ColorMathTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(0.5, 0.21404)]
    [InlineData(0.04045, 0.0031308)]
    public void SrgbToLinearMatchesTheStandard(double srgb, double linear)
    {
        Assert.Equal(linear, ColorMath.SrgbToLinear(srgb), 4);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.001)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.999)]
    public void LinearToSrgbIsTheInverse(double v)
    {
        Assert.Equal(v, ColorMath.LinearToSrgb(ColorMath.SrgbToLinear(v)), 9);
    }

    [Theory]
    [InlineData(1.0, 100.0)]
    [InlineData(0.18, 49.5)]
    [InlineData(0.01, 8.99)]
    [InlineData(0.0, 0.0)]
    public void LstarOfYMatchesTheStandard(double y, double lstar)
    {
        Assert.Equal(lstar, ColorMath.LstarOfY(y), 1);
    }

    [Fact]
    public void TheLstarCurveIsContinuousAtTheSegmentJoin()
    {
        double below = ColorMath.LstarOfY(0.008856 - 1e-9);
        double above = ColorMath.LstarOfY(0.008856 + 1e-9);

        Assert.Equal(below, above, 3);
    }

    /// <summary>
    /// Negrul hartiei: baryta lucios (2.4) ~3.6, canvas mat (1.5) ~21, C-print sub acril
    /// (2.2) ~6; 2.0 (~9) e baryta de dinainte de ADR 0028, pe care stau scenele. Pragurile
    /// din raport pleaca de aici.
    /// </summary>
    [Theory]
    [InlineData(2.4, 3.60)]
    [InlineData(2.0, 8.99)]
    [InlineData(1.5, 20.68)]
    [InlineData(2.2, 5.70)]
    public void PaperBlackLstarPerMedium(double dmax, double lstar)
    {
        Assert.Equal(lstar, ColorMath.PaperBlackLstar(dmax), 1);
    }

    [Fact]
    public void AGreyPixelHasTheSameLstarAsItsValue()
    {
        double fromRgb = ColorMath.LstarOfSrgb(0.3, 0.3, 0.3);
        double fromY = ColorMath.LstarOfY(ColorMath.SrgbToLinear(0.3));

        Assert.Equal(fromY, fromRgb, 9);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(5.0)]
    [InlineData(7.99)]
    [InlineData(8.0)]
    [InlineData(20.7)]
    [InlineData(100.0)]
    public void YOfLstarIsTheInverseOnBothSegments(double lstar)
    {
        Assert.Equal(lstar, ColorMath.LstarOfY(ColorMath.YOfLstar(lstar)), 9);
    }

    /// <summary>
    /// Perechile din panoul Tonuri: negrul baryta (L* 9) e nivelul ~25 al unui gri,
    /// nivelul 32 e L* ~12, 64 e L* ~27, L* 50 e nivelul ~119.
    /// </summary>
    [Theory]
    [InlineData(8.99, 25.5)]
    [InlineData(15.0, 37.7)]
    [InlineData(50.0, 118.9)]
    public void AGreyLevelAndItsLstarAreOneCurve(double lstar, double level)
    {
        Assert.Equal(level, ColorMath.GrayLevelOfLstar(lstar), 1);
        Assert.Equal(lstar, ColorMath.LstarOfGrayLevel(level), 1);
    }

    [Fact]
    public void TheDarkLevelThresholdsInLstar()
    {
        Assert.Equal(12.3, ColorMath.LstarOfGrayLevel(32), 1);
        Assert.Equal(27.1, ColorMath.LstarOfGrayLevel(64), 1);
    }

    [Fact]
    public void LuminanceWeightsSumToOne()
    {
        Assert.Equal(1.0, ColorMath.Luminance(1, 1, 1), 9);
        Assert.Equal(0.7152, ColorMath.Luminance(0, 1, 0), 9);
    }
}
