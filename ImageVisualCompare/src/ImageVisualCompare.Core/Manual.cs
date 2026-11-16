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

namespace ImageVisualCompare.Core;

/// <summary>A section of the window's manual in the suite's wiki that Help and F1 can open (R-066).</summary>
public enum ManualSection
{
    /// <summary>"Opening a pair": no pair shown yet, or the loading veil on screen, which that
    /// section explains.</summary>
    OpeningAPair,

    /// <summary>"The window": a pair shown in <see cref="DisplayMode.Linear"/>.</summary>
    TheWindow,

    /// <summary>"Histogram": a pair shown in <see cref="DisplayMode.Histogram"/>.</summary>
    Histogram,

    /// <summary>"False colours": a pair shown in <see cref="DisplayMode.FalseColours"/>.</summary>
    FalseColours,
}

/// <summary>
/// Where Help and F1 send the browser (R-066, ADR 0056): the window's page of the suite's wiki,
/// English in both languages, at the section of what is on screen. The anchors are the ones
/// GitHub makes from the page's headings; a heading renamed in the wiki is renamed here too, or
/// the browser lands at the top of the page.
/// </summary>
public static class Manual
{
    /// <summary>The window's page of the Astro Darkroom wiki.</summary>
    public const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/ImageVisualCompare";

    /// <summary>The address of a section of the manual.</summary>
    /// <param name="section">The section.</param>
    /// <returns><see cref="Page"/> followed by the section's anchor, such as <c>#histogram</c>.</returns>
    public static string Url(ManualSection section) => Page + "#" + Anchor(section);

    /// <summary>
    /// The section of what the window shows: "Opening a pair" while the loading veil is on screen
    /// (a pair being opened, changed or switched, R-006) and while no pair is shown; otherwise
    /// the section of the display mode the pair is shown in.
    /// </summary>
    /// <param name="opening">Whether a pair is being opened, changed or switched.</param>
    /// <param name="pairShown">Whether a pair is on screen.</param>
    /// <param name="mode">The display mode of the pair on screen; not read without one.</param>
    /// <returns>The section to open.</returns>
    public static ManualSection SectionFor(bool opening, bool pairShown, DisplayMode mode) =>
        opening || !pairShown ? ManualSection.OpeningAPair
        : mode switch
        {
            DisplayMode.Histogram => ManualSection.Histogram,
            DisplayMode.FalseColours => ManualSection.FalseColours,
            _ => ManualSection.TheWindow,
        };

    /// <summary>The anchor GitHub makes from the section's heading in the page.</summary>
    /// <param name="section">The section.</param>
    /// <returns>The anchor, without the <c>#</c>.</returns>
    private static string Anchor(ManualSection section) => section switch
    {
        ManualSection.TheWindow => "the-window",
        ManualSection.Histogram => "histogram",
        ManualSection.FalseColours => "false-colours",
        _ => "opening-a-pair",
    };
}
