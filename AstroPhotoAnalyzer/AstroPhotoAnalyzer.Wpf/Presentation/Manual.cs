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

// Manual.cs - where the Help button and F1 send the browser: AstroPhotoAnalyzer's page of the
// suite's wiki, at the section of what the window shows (R-186, ADR 0102). The anchors are the
// ones GitHub makes from the page's headings; a heading renamed in the wiki is renamed here too.

using System;
using System.Windows;
using System.Windows.Input;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>A section of AstroPhotoAnalyzer's manual in the wiki that a window can open.</summary>
    public enum ManualSection
    {
        /// <summary>"Opening an image": no image analysed yet, or an analysis running.</summary>
        OpeningAnImage,

        /// <summary>"When something goes wrong": the card "The analysis failed", whose messages
        /// that section's table lists.</summary>
        WhenSomethingGoesWrong,

        /// <summary>The Report tab.</summary>
        Report,

        /// <summary>The Tones tab.</summary>
        Tones,

        /// <summary>The Export tab.</summary>
        Export,

        /// <summary>The Resolution tab.</summary>
        Resolution,

        /// <summary>The Colour tab.</summary>
        Colour,

        /// <summary>The Curves Editing tab.</summary>
        CurvesEditing,

        /// <summary>The Colours for paper tab.</summary>
        ColoursForPaper,

        /// <summary>The Soft proof tab, and its full-screen window, which that section explains.</summary>
        SoftProof,

        /// <summary>The Crop with aspect ratio tab.</summary>
        CropWithAspectRatio,

        /// <summary>The Dust / sky map window, opened from the Tones tab.</summary>
        DustSkyMapWindow,

        /// <summary>The Preview window of a simulation row, opened from the Tones tab.</summary>
        PreviewWindow,

        /// <summary>The Curve window, opened from the Curves Editing tab.</summary>
        CurveWindow,
    }

    /// <summary>The addresses of AstroPhotoAnalyzer's manual, which section a window is on, and
    /// the opening of one.</summary>
    internal static class Manual
    {
        /// <summary>AstroPhotoAnalyzer's page of the Astro Darkroom wiki, English only.</summary>
        internal const string Page = "https://github.com/tcarean/AstroDarkroom/wiki/AstroPhotoAnalyzer";

        /// <summary>The diagnostic log: the address each press of Help or F1 opened (R-171).</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

        /// <summary>The address of a section of the manual.</summary>
        /// <param name="section">The section.</param>
        /// <returns><see cref="Page"/> followed by the section's anchor, such as
        /// <c>#export</c>.</returns>
        internal static string Url(ManualSection section) => Page + "#" + Anchor(section);

        /// <summary>The anchor GitHub makes from the section's heading in the wiki page: lower
        /// case, the spaces made hyphens, the other signs dropped ("The Dust / sky map window"
        /// keeps two hyphens where its slash was).</summary>
        /// <param name="section">The section.</param>
        /// <returns>The anchor, without the <c>#</c>.</returns>
        static string Anchor(ManualSection section) => section switch
        {
            ManualSection.WhenSomethingGoesWrong => "when-something-goes-wrong",
            ManualSection.Report => "report",
            ManualSection.Tones => "tones",
            ManualSection.Export => "export",
            ManualSection.Resolution => "resolution",
            ManualSection.Colour => "colour",
            ManualSection.CurvesEditing => "curves-editing",
            ManualSection.ColoursForPaper => "colours-for-paper",
            ManualSection.SoftProof => "soft-proof",
            ManualSection.CropWithAspectRatio => "crop-with-aspect-ratio",
            ManualSection.DustSkyMapWindow => "the-dust--sky-map-window",
            ManualSection.PreviewWindow => "the-preview-window",
            ManualSection.CurveWindow => "the-curve-window",
            _ => "opening-an-image",
        };

        /// <summary>
        /// The section of what the main window shows, in the order MainWindow.xaml stacks its
        /// right side: the busy overlay over all of it, then the tabs, then the error card,
        /// then the invitation to open an image. The tabs come before the card because they are
        /// drawn over it when both show (a field refused while a result is on screen, CR-040).
        /// </summary>
        /// <param name="isBusy">Whether an analysis runs.</param>
        /// <param name="hasResult">Whether a result is on screen, which shows the tabs.</param>
        /// <param name="hasError">Whether the error card shows.</param>
        /// <param name="tab">The selected tab's section, or null when no tab is selected.</param>
        /// <returns>The section to open.</returns>
        internal static ManualSection SectionFor(bool isBusy, bool hasResult, bool hasError, ManualSection? tab)
        {
            if (isBusy) return ManualSection.OpeningAnImage;
            if (hasResult && tab.HasValue) return tab.Value;
            return hasError ? ManualSection.WhenSomethingGoesWrong : ManualSection.OpeningAnImage;
        }

        /// <summary>
        /// Makes F1 held down open the manual once in <paramref name="window"/> (R-186): the
        /// key-downs Windows repeats while the key is held are taken in the preview pass, before
        /// WPF turns each of them into the Help command and opens one browser tab per repeat.
        /// The window remembers that F1 is down from its first key-down until its key-up, or
        /// until the window loses the focus, since the browser coming to the front can take the
        /// key-up. WPF's own IsRepeat is not used: it calls a key-down a repeat whenever the last
        /// key-down it saw was the same key, so after a key-up the browser took, it would swallow
        /// the next press. Every window that has the Help command calls this once.
        /// </summary>
        /// <param name="window">The window; not null.</param>
        internal static void IgnoreHeldF1(Window window)
        {
            bool down = false;
            window.PreviewKeyDown += (_, e) =>
            {
                if (e.Key != Key.F1) return;
                if (down) e.Handled = true;
                down = true;
            };
            window.PreviewKeyUp += (_, e) => { if (e.Key == Key.F1) down = false; };
            window.Deactivated += (_, _) => down = false;
        }

        /// <summary>
        /// Opens a section of the manual and writes its address to the diagnostic log at Info,
        /// as <c>Help opened: &lt;address&gt;</c>: what the Help button and F1 do in every window
        /// that has them.
        /// </summary>
        /// <param name="section">The section.</param>
        /// <param name="openAddress">What opens the address: <see cref="ExternalLink.Open(string)"/>
        /// outside the tests, a stand-in that keeps it in them; not null.</param>
        internal static void Open(ManualSection section, Action<string> openAddress)
        {
            string address = Url(section);
            Log.Info("Help opened: " + address);
            openAddress(address);
        }
    }
}
