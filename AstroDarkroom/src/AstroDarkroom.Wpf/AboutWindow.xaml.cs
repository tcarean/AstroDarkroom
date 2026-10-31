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

using System.Windows;
using System.Windows.Input;
using AstroDarkroom.Core;
using NLog;
using static AstroDarkroom.Core.Ui;

namespace AstroDarkroom.Wpf;

/// <summary>
/// One line of the component list in the about window.
/// </summary>
/// <param name="Name">The component's folder name.</param>
/// <param name="Version">Its version as release.json records it, or "?" when unknown.</param>
public sealed record ComponentLine(string Name, string Version);

/// <summary>
/// The about window: the banner, the copyright, the disclaimer of warranty GPLv3 section 5(d)
/// asks for, the components written by others that the launcher ships (R-015), what the suite
/// needs on the machine (R-009), the suite's date and each component's version, all from the
/// core and from release.json, worded in the language current when it opens (R-006); it is
/// modal and made anew at every open, so it never has to switch. The unreleased flag of
/// release.json is not shown (ADR 0007). It fits the work area of its screen: no taller than it,
/// its cards scrolling with the wheel and the keys when they need more, kept inside it once laid
/// out (R-016).
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>The diagnostic log: the window's size and place once shown, whether its cards scroll (R-012).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    /// <summary>
    /// The window, filled; not yet shown.
    /// </summary>
    /// <param name="release">What release.json says, or null when the folder has none the launcher could read.</param>
    /// <param name="workArea">
    /// The work area of the screen the window opens on, in device-independent pixels: the window
    /// is no taller than it and is kept inside it (R-016).
    /// </param>
    public AboutWindow(ReleaseInfo? release, Rect workArea)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        MaxHeight = workArea.Height;
        Loaded += (_, _) => FitInto(workArea);
        PreviewKeyDown += ScrollCards;
        Title = T("About Astro Darkroom", "Despre Astro Darkroom");
        Logo.Source = Icons.LargestFrame(Icons.Launcher);
        Tagline.Text = T(
            "The launcher and the shared package of the astrophotography tools.",
            "Lansatorul si pachetul comun al uneltelor de astrofoto.");
        Banner.Text = VersionInfo.Banner;
        Copyright.Text = VersionInfo.Copyright;
        LicenceHeading.Text = T("LICENCE", "LICENTA");
        Warranty.Text = LicenseInfo.Warranty;
        ThirdPartyHeading.Text = T("THIRD-PARTY COMPONENTS", "COMPONENTE TERTE");
        ThirdPartyLead.Text = ThirdParty.Lead;
        ThirdPartyList.ItemsSource = ThirdParty.Components;
        PrerequisitesHeading.Text = T("REQUIREMENTS", "CERINTE");
        PrerequisitesList.ItemsSource = Prerequisites.Details;
        ComponentsHeading.Text = T("COMPONENTS", "COMPONENTE");
        CloseButton.Content = T("Close", "Inchide");
        // The list the file gives, without the launcher's own entry, each under the catalogue's
        // title (the file names a component by its folder, WCS_Copy for CopyWcs); a file that
        // names no component (hand-edited, truncated) falls back to the catalogue with unknown
        // versions, the same thing the rows of the main window say.
        List<ComponentLine> listed = release is null
            ? []
            : release.Components
                .Where(c => c.Name != release.Name)
                .Select(c => new ComponentLine(TitleOf(c.Name), c.Version.Length > 0 ? c.Version : "?"))
                .ToList();
        bool known = listed.Count > 0;
        ReleaseLine.Text = known && release is { Date.Length: > 0 }
            ? T($"Release of {release.Date}; the components' versions as they went into the package.",
                $"Release din {release.Date}; versiunile componentelor asa cum au intrat in pachet.")
            : known
                ? T("The components' versions as they went into the package.",
                    "Versiunile componentelor asa cum au intrat in pachet.")
                : T("No release.json with components beside the program: the components' versions are not known.",
                    "Fara un release.json cu componente langa program: versiunile componentelor nu se cunosc.");
        Components.ItemsSource = known
            ? listed
            : AppCatalogue.Apps.Select(a => new ComponentLine(a.Title, "?")).ToList();
    }

    /// <summary>
    /// The title the launcher shows for a component of release.json, which names it by its
    /// folder (R-004): the catalogue's title for that folder, or the name itself when the
    /// catalogue has no such app.
    /// </summary>
    /// <param name="name">The component's name in release.json; not null.</param>
    /// <returns>The catalogue's title, else the name unchanged.</returns>
    private static string TitleOf(string name)
        => AppCatalogue.Apps.FirstOrDefault(a => a.Folder == name)?.Title ?? name;

    /// <summary>
    /// Keeps the laid-out window inside its screen's work area (R-016), and writes its size and
    /// place and whether its cards scroll to the diagnostic log. WPF has placed it there already
    /// (a window centred on its owner is clamped into the owner screen's work area); the move
    /// holds that place as a guard, so the requirement does not rest on WPF's placement alone.
    /// </summary>
    /// <param name="workArea">The work area of the window's screen, in device-independent pixels.</param>
    private void FitInto(Rect workArea)
    {
        Top = WindowFit.Top(Top, ActualHeight, workArea.Top, workArea.Height);
        if (Log.IsDebugEnabled)
        {
            Log.Debug(FormattableString.Invariant(
                $"About shown {ActualWidth:0}x{ActualHeight:0} at ({Left:0},{Top:0}); work area {workArea.Width:0}x{workArea.Height:0} at ({workArea.Left:0},{workArea.Top:0}); ")
                + "cards scroll: " + (Cards.ScrollableHeight > 0 ? "yes" : "no"));
        }
    }

    /// <summary>
    /// Scrolls the cards from the keyboard (R-016): nothing in them takes the focus, which stays
    /// on the Close button, so the keys a scroll viewer answers are passed to it here. Enter and
    /// Esc go on to the button as before.
    /// </summary>
    /// <param name="sender">The window; not read.</param>
    /// <param name="e">The key; marked handled when it scrolled the cards.</param>
    private void ScrollCards(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                Cards.LineDown();
                break;
            case Key.Up:
                Cards.LineUp();
                break;
            case Key.PageDown:
                Cards.PageDown();
                break;
            case Key.PageUp:
                Cards.PageUp();
                break;
            case Key.End:
                Cards.ScrollToBottom();
                break;
            case Key.Home:
                Cards.ScrollToTop();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>Closes the window.</summary>
    /// <param name="sender">The button; not read.</param>
    /// <param name="e">The click; not read.</param>
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
