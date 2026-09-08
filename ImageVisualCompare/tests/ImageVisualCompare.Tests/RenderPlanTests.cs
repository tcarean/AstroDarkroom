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

public class RenderPlanTests
{
    private static Viewport Create()
    {
        var viewport = new Viewport();
        viewport.SetContentSize(1000, 1000);
        viewport.SetViewportSize(400, 400);
        return viewport;
    }

    [Fact]
    public void TheTranslationIsTheNegatedOffset()
    {
        Viewport viewport = Create();
        viewport.SetOffset(120, 240);

        RenderPlan plan = RenderPlanner.For(viewport, deviceScale: 1);

        Assert.Equal(-120, plan.TranslateX, 6);
        Assert.Equal(-240, plan.TranslateY, 6);
    }

    [Theory]
    [InlineData(1.0, 2.0)]
    [InlineData(1.25, 1.6)]
    [InlineData(1.5, 4.0 / 3)]
    public void TheRenderScaleDividesOutTheDisplayScaling(double deviceScale, double expected)
    {
        Viewport viewport = Create();
        viewport.ZoomTo(2.0, 200, 200);

        RenderPlan plan = RenderPlanner.For(viewport, deviceScale);

        Assert.Equal(expected, plan.RenderScale, 6);
    }

    [Fact]
    public void OnAScaledDisplayOneHundredPercentStillMeansOneImagePixelPerScreenPixel()
    {
        Viewport viewport = Create();
        viewport.ZoomToActualSize();

        RenderPlan plan = RenderPlanner.For(viewport, deviceScale: 1.5);

        // The image is laid out at 1/1.5 device-independent pixels per image pixel, which the
        // display scaling then multiplies straight back to one physical pixel.
        Assert.Equal(1.0, plan.RenderScale * 1.5, 6);
    }

    [Theory]
    [InlineData(0.5, ImageScalingMode.Smooth)]
    [InlineData(1.0, ImageScalingMode.Smooth)]
    [InlineData(3.99, ImageScalingMode.Smooth)]
    [InlineData(4.0, ImageScalingMode.Pixelated)]
    [InlineData(8.0, ImageScalingMode.Pixelated)]
    public void DeepZoomSwitchesToShowingThePixelsThemselves(double scale, ImageScalingMode expected)
    {
        Viewport viewport = Create();
        viewport.ZoomTo(scale, 200, 200);

        RenderPlan plan = RenderPlanner.For(viewport, deviceScale: 1);

        Assert.Equal(expected, plan.ScalingMode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveDeviceScaleIsRejected(double deviceScale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RenderPlanner.For(Create(), deviceScale));
    }
}
