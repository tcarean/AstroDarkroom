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

/// <summary>
/// Where Help and F1 send the browser (R-066, ADR 0056): the window's page of the suite's wiki,
/// at the section of what is on screen. The anchors are the ones GitHub makes from the page's
/// headings; the wiki is outside the repository, so no test here can read them.
/// </summary>
public class ManualTests
{
    private const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/ImageVisualCompare";

    [Theory]
    [InlineData(ManualSection.OpeningAPair, "#opening-a-pair")]
    [InlineData(ManualSection.TheWindow, "#the-window")]
    [InlineData(ManualSection.Histogram, "#histogram")]
    [InlineData(ManualSection.FalseColours, "#false-colours")]
    public void R066_EachSection_IsAnAnchorOfTheWindowsPage(ManualSection section, string anchor)
    {
        Assert.Equal(Page, Manual.Page);
        Assert.Equal(Page + anchor, Manual.Url(section));
    }

    [Theory]
    [InlineData(false, false, DisplayMode.Linear, ManualSection.OpeningAPair)]
    [InlineData(true, false, DisplayMode.Linear, ManualSection.OpeningAPair)]
    [InlineData(true, true, DisplayMode.Linear, ManualSection.OpeningAPair)]
    [InlineData(true, true, DisplayMode.Histogram, ManualSection.OpeningAPair)]
    [InlineData(false, true, DisplayMode.Linear, ManualSection.TheWindow)]
    [InlineData(false, true, DisplayMode.Histogram, ManualSection.Histogram)]
    [InlineData(false, true, DisplayMode.FalseColours, ManualSection.FalseColours)]
    public void R066_TheSection_IsTheVeilsWhileItShows_OtherwiseTheModesOnScreen(
        bool opening, bool pairShown, DisplayMode mode, ManualSection expected)
    {
        Assert.Equal(expected, Manual.SectionFor(opening, pairShown, mode));
    }
}
