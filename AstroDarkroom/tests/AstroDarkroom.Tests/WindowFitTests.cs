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

using System.Xml.Linq;
using AstroDarkroom.Core;

namespace AstroDarkroom.Tests;

/// <summary>
/// R-016 and R-020: where "Despre" and the main window go so that they stay on their screen.
/// Each window caps its height at the work area's and asks the core where its top goes; these
/// are the core's answers. The windows have no test project (ADR 0001), so of the main window
/// only its layout is read here, as the XML its XAML is: the rows in a scroll viewer between
/// the two bars, without which a capped window would cut its bottom bar off (R-020).
/// </summary>
public class WindowFitTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// CR-005: the main window was three rows as tall as their content and scrolled nothing, so
    /// on a screen lower than its 848 DIP its bottom bar, and at 150 % its last row, were off
    /// the screen. The rows now sit in a scroll viewer in the one row that stretches, and each
    /// bar keeps a row of its own height.
    /// </summary>
    [Fact]
    public void R020_TheRowsScrollBetweenTheTwoBars()
    {
        XElement window = XDocument.Load(Path.Combine(TestData.FindRepoRoot(), "src", "AstroDarkroom.Wpf", "MainWindow.xaml")).Root!;
        Assert.Equal("Height", window.Attribute("SizeToContent")?.Value);   // as tall as its content where that fits

        XElement grid = window.Element(Wpf + "Grid")!;
        Assert.Equal(["Auto", "*", "Auto"],
            grid.Element(Wpf + "Grid.RowDefinitions")!.Elements(Wpf + "RowDefinition").Select(r => r.Attribute("Height")?.Value));

        XElement rows = window.Descendants().Single(e => e.Attribute(Xaml + "Name")?.Value == "Rows");
        XElement scroll = rows.Parent!;
        Assert.Equal(Wpf + "ScrollViewer", scroll.Name);
        Assert.Same(grid, scroll.Parent);
        Assert.Equal("1", scroll.Attribute("Grid.Row")?.Value);
        Assert.Equal("Auto", scroll.Attribute("VerticalScrollBarVisibility")?.Value);

        // The bars: the only other children of the grid, in the rows of their own height.
        Assert.Equal(["0", "2"], grid.Elements(Wpf + "Border").Select(b => b.Attribute("Grid.Row")?.Value));
        XElement bottom = grid.Elements(Wpf + "Border").Last();
        Assert.Contains(bottom.Descendants(), e => e.Attribute(Xaml + "Name")?.Value == "AboutButton");
        Assert.Contains(bottom.Descendants(), e => e.Attribute(Xaml + "Name")?.Value == "RomanianChoice");
    }

    [Theory]
    [InlineData(96.0, 848.0, 0.0, 1040.0, 96.0)]    // it fits, centred on a 1080p screen at 100 %: unchanged
    [InlineData(-11.5, 825.0, 0.0, 825.0, 0.0)]     // 125 %: capped to the area's 825, centred at 848 it started above the area
    [InlineData(-79.0, 690.0, 0.0, 690.0, 0.0)]     // 150 %: capped to 690 (R-020's example)
    [InlineData(28.5, 825.0, 40.0, 825.0, 40.0)]    // a taskbar at the top: the area starts under it
    public void R020_TheMainWindowsTopKeepsItInsideTheWorkArea(double top, double height, double areaTop, double areaHeight, double expected)
    {
        Assert.Equal(expected, WindowFit.Top(top, height, areaTop, areaHeight));
    }

    [Theory]
    [InlineData(100.0, 600.0, 0.0, 800.0, 100.0)]   // it fits where it opens: unchanged
    [InlineData(300.0, 600.0, 0.0, 800.0, 200.0)]   // past the bottom: up until its bottom is the area's (R-016's example)
    [InlineData(-60.0, 600.0, 0.0, 800.0, 0.0)]     // past the top: down to the area's top
    [InlineData(-60.0, 946.0, 0.0, 800.0, 0.0)]     // taller than the area: at its top (the window is cut to the area's height)
    [InlineData(-60.0, 800.0, 0.0, 800.0, 0.0)]     // exactly as tall as the area: at its top
    [InlineData(500.0, 600.0, 40.0, 800.0, 240.0)]  // an area that does not start at 0, a taskbar above it
    [InlineData(100.0, 600.0, -1080.0, 1040.0, -640.0)]   // a screen above the primary, at negative coordinates
    public void R016_TheWindowsTopKeepsItInsideTheWorkArea(double top, double height, double areaTop, double areaHeight, double expected)
    {
        Assert.Equal(expected, WindowFit.Top(top, height, areaTop, areaHeight));
    }
}
