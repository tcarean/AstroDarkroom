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

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// The zoom state, without the control: the steps, the limits, the label, the reset. What
/// the control does with it (panning, the wheel, "potrivit" from the window's size) is
/// checked by eye.
/// </summary>
public class ImageZoomViewModelTests
{
    [Fact]
    public void StartsFitted()
    {
        var z = new ImageZoomViewModel();

        Assert.True(z.IsFit);
        Assert.Equal(1.0, z.Zoom);
        Assert.StartsWith("potrivit", z.Label);
    }

    /// <summary>A step in from "fit" starts where the eye is, not at 100 %.</summary>
    [Fact]
    public void ZoomingInFromFitStartsAtTheEffectiveZoom()
    {
        var z = new ImageZoomViewModel { EffectiveZoom = 0.4 };

        z.ZoomIn.Execute(null);

        Assert.False(z.IsFit);
        Assert.Equal(0.4 * ImageZoomViewModel.Step, z.Zoom, 9);
        Assert.Equal("50 %", z.Label);
    }

    [Fact]
    public void ZoomClampsAtBothEnds()
    {
        var z = new ImageZoomViewModel();

        z.Manual(100);
        Assert.Equal(ImageZoomViewModel.Max, z.Zoom);
        z.Manual(0.0001);
        Assert.Equal(ImageZoomViewModel.Min, z.Zoom);

        z.EffectiveZoom = ImageZoomViewModel.Max;
        z.ZoomIn.Execute(null);
        Assert.Equal(ImageZoomViewModel.Max, z.Zoom);
    }

    [Fact]
    public void ActualIsOneHundredPercent_AndFitGoesBack()
    {
        var z = new ImageZoomViewModel();

        z.Actual.Execute(null);
        Assert.False(z.IsFit);
        Assert.Equal(1.0, z.Zoom);
        Assert.Equal("100 %", z.Label);

        z.Fit.Execute(null);
        Assert.True(z.IsFit);
    }

    [Fact]
    public void TheLabelReportsWhatIsOnScreen()
    {
        var z = new ImageZoomViewModel { EffectiveZoom = 0.13 };
        Assert.Equal("potrivit (13 %)", z.Label);

        z.Manual(2.5);
        Assert.Equal("250 %", z.Label);
    }

    [Fact]
    public void ResetReturnsToFit()
    {
        var z = new ImageZoomViewModel();
        z.Manual(3);

        z.Reset();

        Assert.True(z.IsFit);
        Assert.Equal(1.0, z.Zoom);
    }

    [Fact]
    public void ChangesRaiseTheLabel()
    {
        var z = new ImageZoomViewModel();
        var raised = new List<string?>();
        z.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        z.Manual(2);
        z.EffectiveZoom = 2;

        Assert.Contains(nameof(z.Label), raised);
        Assert.Contains(nameof(z.IsFit), raised);
        Assert.Contains(nameof(z.Zoom), raised);
    }
}
