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

public class CropAnalyzerTests
{
    /// <summary>Cifrele de acceptanta: 5938 x 3888 la 50:35 -> 5554 x 3888, 192 px de fiecare parte.</summary>
    [Fact]
    public void TheAcceptanceCropIsReproduced()
    {
        var c = CropAnalyzer.Analyze(5938, 3888, 50.0 / 35.0);

        Assert.True(c.Needed);
        Assert.Equal("latime", c.Axis);
        Assert.Equal(5554, c.W);
        Assert.Equal(3888, c.H);
        Assert.Equal(192, c.X0);
        Assert.Equal(0, c.Y0);
        Assert.Equal(192, c.CutPerSidePx);
        Assert.Equal(6.47, c.CutPct, 2);
        Assert.Equal(5938 / 3888.0, c.NativeAspect, 6);
    }

    [Fact]
    public void NoTargetMeansNoCrop()
    {
        var c = CropAnalyzer.Analyze(5938, 3888, null);

        Assert.False(c.Needed);
        Assert.Null(c.Axis);
        Assert.Equal(5938, c.W);
        Assert.Equal(3888, c.H);
        Assert.Equal(0, c.CutPct);
    }

    [Fact]
    public void AWiderTargetCutsTheHeight()
    {
        var c = CropAnalyzer.Analyze(1000, 800, 2.0);

        Assert.True(c.Needed);
        Assert.Equal("inaltime", c.Axis);
        Assert.Equal(1000, c.W);
        Assert.Equal(500, c.H);
        Assert.Equal(150, c.Y0);
        Assert.Equal(37.5, c.CutPct, 6);
    }

    [Fact]
    public void TheNativeAspectNeedsNoCrop()
    {
        Assert.False(CropAnalyzer.Analyze(3000, 2000, 1.5).Needed);
    }
}
