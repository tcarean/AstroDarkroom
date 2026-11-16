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

// Manual.cs - where the Help button and F1 send the browser: DarksCheck's page of the suite's
// wiki, at the section of what the window shows (R-125, ADR 0061), after FlatCheck's (its
// R-141). The anchors are the ones GitHub makes from the page's headings; a heading renamed in
// the wiki is renamed here too.

namespace DarksCheck.Wpf;

/// <summary>A section of DarksCheck's manual in the wiki that the window can open.</summary>
public enum ManualSection
{
    /// <summary>"Opening a folder": no folder chosen yet, the wait screen, or the empty window
    /// after a stopped run.</summary>
    OpeningAFolder,

    /// <summary>"When something goes wrong": the problem card, whose messages that table
    /// explains.</summary>
    WhenSomethingGoesWrong,

    /// <summary>The Summary tab.</summary>
    Summary,

    /// <summary>The Frames tab.</summary>
    Frames,

    /// <summary>The Maps tab.</summary>
    Maps,

    /// <summary>The Hot pixels tab.</summary>
    HotPixels,

    /// <summary>The Match with lights tab.</summary>
    MatchWithLights,

    /// <summary>The Index tab.</summary>
    Index,

    /// <summary>The ADU scale explained tab.</summary>
    AduScaleExplained,

    /// <summary>The Report tab.</summary>
    Report,
}

/// <summary>The addresses of DarksCheck's manual, and which section the window is on.</summary>
internal static class Manual
{
    /// <summary>DarksCheck's page of the Astro Darkroom wiki, English only.</summary>
    internal const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/DarksCheck";

    /// <summary>The address of a section of the manual.</summary>
    /// <param name="section">The section.</param>
    /// <returns><see cref="Page"/> followed by the section's anchor, such as
    /// <c>#maps</c>.</returns>
    internal static string Url(ManualSection section) => Page + "#" + Anchor(section);

    /// <summary>The anchor GitHub makes from the section's heading in the wiki page.</summary>
    /// <param name="section">The section.</param>
    /// <returns>The anchor, without the <c>#</c>.</returns>
    private static string Anchor(ManualSection section) => section switch
    {
        ManualSection.WhenSomethingGoesWrong => "when-something-goes-wrong",
        ManualSection.Summary => "summary",
        ManualSection.Frames => "frames",
        ManualSection.Maps => "maps",
        ManualSection.HotPixels => "hot-pixels",
        ManualSection.MatchWithLights => "match-with-lights",
        ManualSection.Index => "index",
        ManualSection.AduScaleExplained => "adu-scale-explained",
        ManualSection.Report => "report",
        _ => "opening-a-folder",
    };

    /// <summary>
    /// The section of what the window shows, as MainWindow.xaml shows it: the wait screen covers
    /// everything; the problem card goes to the table of what to do; the selected tab's section
    /// while the tabs show a result; "Opening a folder" for the rest (no folder chosen yet, the
    /// empty window after a stopped run).
    /// </summary>
    /// <param name="hasProblem">Whether the problem card shows.</param>
    /// <param name="hasResult">Whether a result is there, which shows the tabs.</param>
    /// <param name="isRunning">Whether an analysis runs, under the wait screen.</param>
    /// <param name="tab">The selected tab's section, or null when no tab is selected.</param>
    /// <returns>The section to open.</returns>
    internal static ManualSection SectionFor(bool hasProblem, bool hasResult, bool isRunning, ManualSection? tab)
    {
        if (isRunning)
        {
            return ManualSection.OpeningAFolder;
        }

        if (hasProblem)
        {
            return ManualSection.WhenSomethingGoesWrong;
        }

        return hasResult && tab.HasValue ? tab.Value : ManualSection.OpeningAFolder;
    }
}
