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

// Manual.cs - where the Help button and F1 send the browser: FlatCheck's page of the suite's
// wiki, at the section of what the window shows (R-141, ADR 0112). The anchors are the ones
// GitHub makes from the page's headings; a heading renamed in the wiki is renamed here too.

namespace FlatCheck.Wpf
{
    /// <summary>A section of FlatCheck's manual in the wiki that the window can open.</summary>
    public enum ManualSection
    {
        /// <summary>"Opening a session": no folder chosen yet, or the opening's analyses running.</summary>
        OpeningASession,

        /// <summary>"The session folder": the problem card of a folder nothing can start on.</summary>
        TheSessionFolder,

        /// <summary>The Structure tab.</summary>
        Structure,

        /// <summary>The End to end tab.</summary>
        EndToEnd,

        /// <summary>The Exposure tab.</summary>
        Exposure,

        /// <summary>The Histogram tab.</summary>
        Histogram,

        /// <summary>The Synthetic tab.</summary>
        Synthetic,

        /// <summary>The Split tab.</summary>
        Split,
    }

    /// <summary>The addresses of FlatCheck's manual, and which section the window is on.</summary>
    internal static class Manual
    {
        /// <summary>FlatCheck's page of the Astro Darkroom wiki, English only.</summary>
        internal const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck";

        /// <summary>The address of a section of the manual.</summary>
        /// <param name="section">The section.</param>
        /// <returns><see cref="Page"/> followed by the section's anchor, such as
        /// <c>#exposure</c>.</returns>
        internal static string Url(ManualSection section) => Page + "#" + Anchor(section);

        /// <summary>The anchor GitHub makes from the section's heading in the wiki page.</summary>
        /// <param name="section">The section.</param>
        /// <returns>The anchor, without the <c>#</c>.</returns>
        static string Anchor(ManualSection section) => section switch
        {
            ManualSection.TheSessionFolder => "the-session-folder",
            ManualSection.Structure => "structure",
            ManualSection.EndToEnd => "end-to-end",
            ManualSection.Exposure => "exposure",
            ManualSection.Histogram => "histogram",
            ManualSection.Synthetic => "synthetic",
            ManualSection.Split => "split",
            _ => "opening-a-session",
        };

        /// <summary>
        /// The section of what the window shows, in the order the window stacks its panels: the
        /// opening panel while the opening runs, over everything; then the problem card; then the
        /// "choose a folder" panel; then the tabs.
        /// </summary>
        /// <param name="hasFolder">Whether a folder was chosen.</param>
        /// <param name="hasProblem">Whether the folder shows the problem card instead of the tabs.</param>
        /// <param name="isRunning">Whether the analyses started by opening the folder run.</param>
        /// <param name="tab">The selected tab's section, or null when no tab is selected.</param>
        /// <returns>The section to open.</returns>
        internal static ManualSection SectionFor(bool hasFolder, bool hasProblem, bool isRunning, ManualSection? tab)
        {
            if (isRunning) return ManualSection.OpeningASession;
            if (hasProblem) return ManualSection.TheSessionFolder;
            if (!hasFolder) return ManualSection.OpeningASession;
            return tab ?? ManualSection.OpeningASession;
        }
    }
}
