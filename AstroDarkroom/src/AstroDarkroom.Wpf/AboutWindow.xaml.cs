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

namespace AstroDarkroom.Wpf;

/// <summary>
/// One line of the component list in "Despre".
/// </summary>
/// <param name="Name">The component's folder name.</param>
/// <param name="Version">Its version as release.json records it, or "?" when unknown.</param>
/// <param name="Note">"modificari nelansate" when it was bundled with unreleased changes; else "".</param>
public sealed record ComponentLine(string Name, string Version, string Note);

/// <summary>
/// "Despre": the banner, the copyright, the disclaimer of warranty GPLv3 section 5(d) asks for,
/// the suite's date and each component's version, all from the core and from release.json.
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
        Logo.Source = App.LargestIconFrame();
        Banner.Text = VersionInfo.Banner;
        Copyright.Text = VersionInfo.Copyright;
        Warranty.Text = LicenseInfo.Warranty;
        // The list the file gives, without the launcher's own entry; a file that names no
        // component (hand-edited, truncated) falls back to the catalogue with unknown versions,
        // the same thing the rows of the main window say.
        List<ComponentLine> listed = release is null
            ? []
            : release.Components
                .Where(c => c.Name != release.Name)
                .Select(c => new ComponentLine(c.Name, c.Version.Length > 0 ? c.Version : "?", c.Unreleased ? "modificari nelansate" : ""))
                .ToList();
        bool known = listed.Count > 0;
        ReleaseLine.Text = known && release is { Date.Length: > 0 }
            ? $"Release din {release.Date}; versiunile componentelor asa cum au intrat in pachet."
            : known
                ? "Versiunile componentelor asa cum au intrat in pachet."
                : "Fara un release.json cu componente langa program: versiunile componentelor nu se cunosc.";
        Components.ItemsSource = known
            ? listed
            : AppCatalogue.Apps.Select(a => new ComponentLine(a.Folder, "?", "")).ToList();
    }

    /// <summary>Closes the window.</summary>
    /// <param name="sender">The button; not read.</param>
    /// <param name="e">The click; not read.</param>
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
