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

using AstroDarkroom.Core;

namespace AstroDarkroom.Wpf;

/// <summary>
/// One row of the main window, bound by the XAML: the catalogue's texts, what the suite folder
/// says about the app and what release.json says about its version. Built once at start-up;
/// the window does not watch the folder.
/// </summary>
/// <param name="App">The app of the catalogue this row shows.</param>
/// <param name="Location">Where its window executable is, and whether it exists (R-002).</param>
/// <param name="Component">Its entry in release.json, or null when the file does not list it.</param>
public sealed record AppRow(SuiteApp App, AppLocation Location, ComponentInfo? Component)
{
    /// <summary>The title, as the catalogue gives it.</summary>
    public string Title => App.Title;

    /// <summary>The one-line description, as the catalogue gives it.</summary>
    public string Description => App.Description;

    /// <summary>Whether the app can be started from this folder.</summary>
    public bool IsAvailable => Location.IsAvailable;

    /// <summary>The tooltip of the button: why the app cannot start, or the file it starts.</summary>
    public string ButtonHint => Location.IsAvailable ? App.WindowExe : Location.Reason ?? "";

    /// <summary>The console beside the window ("consola: darkscheck.exe"), or "" for an app without one.</summary>
    public string ConsoleHint => App.ConsoleExe is null ? "" : "consola: " + App.ConsoleExe;

    /// <summary>The component's version from release.json ("versiunea 2.3.0"), or that it is unknown.</summary>
    public string VersionHint => Component is { Version.Length: > 0 } c ? "versiunea " + c.Version : "versiune necunoscuta";

    /// <summary>"modificari nelansate" when release.json says the component was bundled with changes its version does not name yet; else "".</summary>
    public string UnreleasedHint => Component is { Unreleased: true } ? "modificari nelansate" : "";
}
