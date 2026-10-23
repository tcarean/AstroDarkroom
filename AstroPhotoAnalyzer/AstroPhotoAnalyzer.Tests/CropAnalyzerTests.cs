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

/// <summary>R-032: the centred crop to a target aspect, decided on the pixels it removes.</summary>
public class CropAnalyzerTests
{
    /// <summary>The acceptance figures: 5938 x 3888 at 50:35 -> 5554 x 3888, 192 px on each side.</summary>
    [Fact]
    public void R032_TheAcceptanceCropIsReproduced()
    {
        var c = CropAnalyzer.Analyze(5938, 3888, 50.0 / 35.0);

        Assert.True(c.Needed);
        Assert.Equal("latime", c.Axis);
        Assert.Equal(5554, c.W);
        Assert.Equal(3888, c.H);
        Assert.Equal(192, c.X0);
        Assert.Equal(0, c.Y0);
        Assert.Equal(192, c.CutStartPx);
        Assert.Equal(192, c.CutEndPx);
        Assert.Equal(6.47, c.CutPct, 2);
        Assert.Equal(5938 / 3888.0, c.NativeAspect, 6);
        Assert.Equal("192 px pe fiecare parte din latime", ReportWriter.CropCutText(c));
    }

    /// <summary>
    /// A crop is needed only when a pixel is removed (ADR 0043): 4961 x 3508 at 297:210 differs
    /// from the ratio by more than 1e-6 and rounds to its own width, so no crop; 3508 x 2480 at
    /// the same ratio loses one pixel, on the right side alone, and the text names both sides.
    /// </summary>
    [Fact]
    public void R032_ACropIsNeededOnlyWhenAPixelIsRemovedAndNamesBothSidesWhenTheyDiffer()
    {
        double a4 = 297.0 / 210.0;
        var none = CropAnalyzer.Analyze(4961, 3508, a4);
        Assert.True(Math.Abs(a4 - 4961 / 3508.0) > 1e-6);
        Assert.False(none.Needed);
        Assert.Null(none.Axis);
        Assert.Equal(4961, none.W);
        Assert.Equal(0, none.CutStartPx + none.CutEndPx);

        var one = CropAnalyzer.Analyze(3508, 2480, a4);
        Assert.True(one.Needed);
        Assert.Equal("latime", one.Axis);
        Assert.Equal(3507, one.W);
        Assert.Equal(0, one.X0);
        Assert.Equal(0, one.CutStartPx);
        Assert.Equal(1, one.CutEndPx);
        Assert.Equal("0 px la stanga si 1 px la dreapta din latime", ReportWriter.CropCutText(one));

        // An odd cut of the height: the bottom loses the extra pixel.
        var odd = CropAnalyzer.Analyze(1000, 801, 1.25);   // 801 -> 800
        Assert.Equal("inaltime", odd.Axis);
        Assert.Equal(0, odd.CutStartPx);
        Assert.Equal(1, odd.CutEndPx);
        Assert.Equal("0 px sus si 1 px jos din inaltime", ReportWriter.CropCutText(odd));
        var three = CropAnalyzer.Analyze(1000, 803, 1.25);   // 803 -> 800: 1 above, 2 below
        Assert.Equal(1, three.Y0);
        Assert.Equal(1, three.CutStartPx);
        Assert.Equal(2, three.CutEndPx);
    }

    [Fact]
    public void R032_NoTargetMeansNoCrop()
    {
        var c = CropAnalyzer.Analyze(5938, 3888, null);

        Assert.False(c.Needed);
        Assert.Null(c.Axis);
        Assert.Equal(5938, c.W);
        Assert.Equal(3888, c.H);
        Assert.Equal(0, c.CutPct);
    }

    [Fact]
    public void R032_AWiderTargetCutsTheHeight()
    {
        var c = CropAnalyzer.Analyze(1000, 800, 2.0);

        Assert.True(c.Needed);
        Assert.Equal("inaltime", c.Axis);
        Assert.Equal(1000, c.W);
        Assert.Equal(500, c.H);
        Assert.Equal(150, c.Y0);
        Assert.Equal(150, c.CutStartPx);
        Assert.Equal(150, c.CutEndPx);
        Assert.Equal(37.5, c.CutPct, 6);
    }

    [Fact]
    public void R032_TheNativeAspectNeedsNoCrop()
    {
        Assert.False(CropAnalyzer.Analyze(3000, 2000, 1.5).Needed);
    }
}
