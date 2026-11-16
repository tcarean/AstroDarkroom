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

namespace LightsCheck.Wpf;

/// <summary>A section of LightsCheck's manual in the wiki that the window can open (R-091).</summary>
public enum ManualSection
{
    /// <summary>"Opening a session": every screen shown instead of the tabs - no folder chosen yet,
    /// an analysis running, a stopped one, or the problem card, all of which that section explains.</summary>
    OpeningASession,

    /// <summary>The Summary tab.</summary>
    Summary,

    /// <summary>The Frames tab.</summary>
    Frames,

    /// <summary>The Dithering tab.</summary>
    Dithering,

    /// <summary>The Quality tab.</summary>
    Quality,

    /// <summary>The Rejected tab.</summary>
    Rejected,

    /// <summary>The Report tab.</summary>
    Report,
}

/// <summary>
/// Where the Help button and F1 send the browser: LightsCheck's page of the suite's wiki, at the
/// section of what the window shows (R-091, ADR 0060). The anchors are the ones GitHub makes from
/// the page's headings; a heading renamed in the wiki is renamed here too.
/// </summary>
internal static class Manual
{
    /// <summary>LightsCheck's page of the Astro Darkroom wiki, English only.</summary>
    internal const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/LightsCheck";

    /// <summary>The address of a section of the manual.</summary>
    /// <param name="section">The section.</param>
    /// <returns><see cref="Page"/> followed by the section's anchor, such as <c>#dithering</c>.</returns>
    internal static string Url(ManualSection section) => Page + "#" + Anchor(section);

    /// <summary>
    /// The section of what the window shows: the selected tab's when the tabs show (a result, and
    /// no wait screen over it, as MainWindow.xaml shows them); "Opening a session" for every
    /// screen shown instead of them.
    /// </summary>
    /// <param name="hasResult">Whether the window holds a result, which shows the tabs.</param>
    /// <param name="isRunning">Whether an analysis runs, its wait screen over everything.</param>
    /// <param name="tab">The selected tab's section, or null when no tab is selected.</param>
    /// <returns>The section to open.</returns>
    internal static ManualSection SectionFor(bool hasResult, bool isRunning, ManualSection? tab)
    {
        bool tabsShow = hasResult && !isRunning;
        return tabsShow && tab.HasValue ? tab.Value : ManualSection.OpeningASession;
    }

    /// <summary>The anchor GitHub makes from the section's heading in the wiki page.</summary>
    /// <param name="section">The section.</param>
    /// <returns>The anchor, without the <c>#</c>.</returns>
    private static string Anchor(ManualSection section) => section switch
    {
        ManualSection.Summary => "summary",
        ManualSection.Frames => "frames",
        ManualSection.Dithering => "dithering",
        ManualSection.Quality => "quality",
        ManualSection.Rejected => "rejected",
        ManualSection.Report => "report",
        _ => "opening-a-session",
    };
}
