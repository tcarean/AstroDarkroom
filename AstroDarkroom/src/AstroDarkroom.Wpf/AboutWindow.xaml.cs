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
using AstroDarkroom.Core;
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
/// release.json is not shown (ADR 0007).
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>
    /// The window, filled; not yet shown.
    /// </summary>
    /// <param name="release">What release.json says, or null when the folder has none the launcher could read.</param>
    public AboutWindow(ReleaseInfo? release)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
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
        ThirdPartyLead.Text = T(
            "Each licence's whole text is in THIRD-PARTY-NOTICES.md, beside the source.",
            "Textul intreg al fiecarei licente e in THIRD-PARTY-NOTICES.md, langa sursa.");
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

    /// <summary>Closes the window.</summary>
    /// <param name="sender">The button; not read.</param>
    /// <param name="e">The click; not read.</param>
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
