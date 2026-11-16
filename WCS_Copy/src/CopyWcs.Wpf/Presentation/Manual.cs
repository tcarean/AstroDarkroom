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

// Manual.cs - where the Help button and F1 send the browser: CopyWcs's page of the suite's wiki,
// at the section of what the outcome card shows (R-039, ADR 0016), after FlatCheck's. The
// anchors are the ones GitHub makes from the page's headings; a heading renamed in the wiki is
// renamed here too, or the browser lands at the top of the page.

namespace CopyWcs.Wpf;

/// <summary>A section of CopyWcs's manual in the wiki that the window can open.</summary>
internal enum ManualSection
{
    /// <summary>"The window": no Preview or Copy has run yet, so the card is not shown.</summary>
    TheWindow,

    /// <summary>"The result": the card shows a preview's or a copy's cards and lines.</summary>
    TheResult,

    /// <summary>"When something goes wrong": the card shows a refusal or a warning, each a row there.</summary>
    WhenSomethingGoesWrong,
}

/// <summary>The addresses of CopyWcs's manual, and which section the window is on.</summary>
internal static class Manual
{
    /// <summary>CopyWcs's page of the Astro Darkroom wiki, English only.</summary>
    internal const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/CopyWcs";

    /// <summary>The address of a section of the manual.</summary>
    /// <param name="section">The section.</param>
    /// <returns><see cref="Page"/> followed by the section's anchor, such as <c>#the-result</c>.</returns>
    internal static string Url(ManualSection section) => Page + "#" + Anchor(section);

    /// <summary>
    /// The section of what the outcome card shows: none yet, a result, or a refusal or a warning.
    /// The card keeps the last outcome while a run goes, so the section does too.
    /// </summary>
    /// <param name="hasOutcome">Whether a preview or a copy has run, so the card is shown.</param>
    /// <param name="hasError">Whether the card shows a refusal.</param>
    /// <param name="hasWarnings">Whether the card shows a warning.</param>
    /// <returns>The section to open.</returns>
    internal static ManualSection SectionFor(bool hasOutcome, bool hasError, bool hasWarnings)
    {
        if (hasError || hasWarnings)
        {
            return ManualSection.WhenSomethingGoesWrong;
        }

        return hasOutcome ? ManualSection.TheResult : ManualSection.TheWindow;
    }

    /// <summary>The anchor GitHub makes from the section's heading in the wiki page.</summary>
    /// <param name="section">The section.</param>
    /// <returns>The anchor, without the <c>#</c>.</returns>
    private static string Anchor(ManualSection section) => section switch
    {
        ManualSection.TheResult => "the-result",
        ManualSection.WhenSomethingGoesWrong => "when-something-goes-wrong",
        _ => "the-window",
    };
}
