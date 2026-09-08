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

using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

public class AspectFitTests
{
    [Fact]
    public void WideContentIsLimitedByTheWidth()
    {
        SizeD box = AspectFit.Fit(2000, 1000, 190, 150);

        Assert.Equal(190, box.Width, 6);
        Assert.Equal(95, box.Height, 6);
    }

    [Fact]
    public void TallContentIsLimitedByTheHeight()
    {
        SizeD box = AspectFit.Fit(1000, 2000, 190, 150);

        Assert.Equal(75, box.Width, 6);
        Assert.Equal(150, box.Height, 6);
    }

    [Fact]
    public void TheAspectRatioIsPreserved()
    {
        SizeD box = AspectFit.Fit(5794, 3637, 190, 150);

        Assert.Equal(5794.0 / 3637, box.Width / box.Height, 6);
    }

    [Fact]
    public void ContentSmallerThanTheBoxIsEnlargedToFillIt()
    {
        SizeD box = AspectFit.Fit(50, 50, 190, 150);

        Assert.Equal(150, box.Width, 6);
        Assert.Equal(150, box.Height, 6);
    }

    [Theory]
    [InlineData(0, 100, 190, 150)]
    [InlineData(100, 0, 190, 150)]
    [InlineData(100, 100, 0, 150)]
    [InlineData(100, 100, 190, 0)]
    public void DegenerateInputsGiveAnEmptyBoxRatherThanInfinity(
        double contentWidth, double contentHeight, double maxWidth, double maxHeight)
    {
        SizeD box = AspectFit.Fit(contentWidth, contentHeight, maxWidth, maxHeight);

        Assert.Equal(0, box.Width, 6);
        Assert.Equal(0, box.Height, 6);
    }
}

public class RectDTests
{
    [Fact]
    public void RightAndBottomAreDerivedFromTheOrigin()
    {
        var rect = new RectD(10, 20, 30, 40);

        Assert.Equal(40, rect.Right, 6);
        Assert.Equal(60, rect.Bottom, 6);
    }
}
