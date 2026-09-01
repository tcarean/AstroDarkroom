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

namespace FocalLenghChecker.Tests;

public class FocalLengthConverterTests
{
    private const double Crop = 1.5;

    [Theory]
    [InlineData(18, 27)]
    [InlineData(35, 52.5)]
    [InlineData(10.5, 15.75)]
    [InlineData(200, 300)]
    public void A_DX_focal_length_is_multiplied_by_the_crop_factor(double dx, double expectedFf)
    {
        Assert.Equal(expectedFf, FocalLengthConverter.ToFullFrame(dx, sourceIsFullFrame: false, Crop), 10);
        Assert.Equal(dx, FocalLengthConverter.ToDx(dx, sourceIsFullFrame: false, Crop), 10);
    }

    [Theory]
    [InlineData(27, 18)]
    [InlineData(52.5, 35)]
    [InlineData(24, 16)]
    public void A_full_frame_focal_length_is_divided_by_the_crop_factor_for_DX(double ff, double expectedDx)
    {
        Assert.Equal(ff, FocalLengthConverter.ToFullFrame(ff, sourceIsFullFrame: true, Crop), 10);
        Assert.Equal(expectedDx, FocalLengthConverter.ToDx(ff, sourceIsFullFrame: true, Crop), 10);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_pair_of_conversions_always_differs_by_the_crop_factor(bool sourceIsFullFrame)
    {
        var ff = FocalLengthConverter.ToFullFrame(40, sourceIsFullFrame, Crop);
        var dx = FocalLengthConverter.ToDx(40, sourceIsFullFrame, Crop);

        Assert.Equal(Crop, ff / dx, 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void An_unusable_crop_factor_is_rejected(double cropFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FocalLengthConverter.ToFullFrame(18, false, cropFactor));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FocalLengthConverter.ToDx(18, true, cropFactor));
    }
}
