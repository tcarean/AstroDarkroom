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

public class ImagePairValidatorTests
{
    private static ImageInfo Info(string path, int width, int height) =>
        new(path, width, height, 16, ColorChannels.Rgb);

    [Fact]
    public void R004_MatchingDimensionsNeedNoPadding()
    {
        PadPlan plan = ImagePairValidator.Inspect(
            Info(@"C:\before.png", 5794, 3637),
            Info(@"C:\after.tif", 5794, 3637));

        Assert.False(plan.RequiresPadding);
        Assert.Equal(5794, plan.Width);
        Assert.Equal(3637, plan.Height);
    }

    [Theory]
    [InlineData(5794, 3637, 6000, 3637, 6000, 3637)]
    [InlineData(5794, 3637, 5794, 4000, 5794, 4000)]
    [InlineData(5794, 3637, 3637, 5794, 5794, 5794)]
    [InlineData(2000, 1000, 1800, 1200, 2000, 1200)]
    public void R004_TheCanvasIsTheLargerExtentOnEachAxis(
        int widthA, int heightA, int widthB, int heightB, int expectedWidth, int expectedHeight)
    {
        PadPlan plan = ImagePairValidator.Inspect(
            Info(@"C:\before.png", widthA, heightA),
            Info(@"C:\after.png", widthB, heightB));

        Assert.True(plan.RequiresPadding);
        Assert.Equal(expectedWidth, plan.Width);
        Assert.Equal(expectedHeight, plan.Height);
    }

    [Fact]
    public void R004_TheCanvasIsBigEnoughForEitherImageWhicheverWayRoundTheyAreGiven()
    {
        ImageInfo small = Info(@"C:\a.png", 1800, 1200);
        ImageInfo large = Info(@"C:\b.png", 2000, 1000);

        Assert.Equal(ImagePairValidator.Inspect(small, large), ImagePairValidator.Inspect(large, small));
    }

    [Fact]
    public void R003_TheQuestionNamesBothFilesBothResolutionsAndTheResult()
    {
        // Sized so that the padded canvas matches neither original outright, which is the case
        // where a message echoing only one of them would mislead.
        ImageInfo before = Info(@"C:\astro\before.png", 5794, 4000);
        ImageInfo after = Info(@"C:\astro\after.png", 6000, 3637);
        PadPlan plan = ImagePairValidator.Inspect(before, after);

        string message = ImagePairValidator.DescribeMismatch(before, after, plan);

        Assert.Contains("before.png", message);
        Assert.Contains("after.png", message);
        Assert.Contains("5794 \u00d7 4000", message);
        Assert.Contains("6000 \u00d7 3637", message);
        Assert.Contains("6000 \u00d7 4000", message);
        Assert.Contains("black", message);
    }

    [Fact]
    public void R003_APadRequestCarriesItsOwnWording()
    {
        ImageInfo before = Info(@"C:\a.png", 100, 100);
        ImageInfo after = Info(@"C:\b.png", 120, 100);
        var request = new PadRequest(before, after, ImagePairValidator.Inspect(before, after));

        Assert.Equal(ImagePairValidator.DescribeMismatch(before, after, request.Plan), request.Message);
        Assert.Equal("120 \u00d7 100", request.Plan.Dimensions);
    }

    [Fact]
    public void R012_BitDepthAndChannelLayoutDoNotHaveToMatch()
    {
        PadPlan plan = ImagePairValidator.Inspect(
            new ImageInfo(@"C:\a.png", 100, 100, 16, ColorChannels.Rgb),
            new ImageInfo(@"C:\b.png", 100, 100, 8, ColorChannels.Rgba));

        Assert.False(plan.RequiresPadding);
    }
}
