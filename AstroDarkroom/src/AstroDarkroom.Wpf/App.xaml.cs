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

using System.IO;
using System.Windows;
using AstroDarkroom.Core;

namespace AstroDarkroom.Wpf;

/// <summary>
/// The application: it sets the interface language from the remembered setting, then opens the
/// main window on the folder the executable runs from.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Reads the language the last run chose (R-007), English when there is none, then opens
    /// the main window on the suite folder, the launcher's own base directory (R-002).
    /// </summary>
    /// <param name="e">The start-up arguments; not read, the launcher takes none.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroDarkroom", "settings.json");
        Ui.Default = LanguageSetting.TryRead(settingsPath) ?? Ui.StartupDefault;
        MainWindow window = new(new SuiteFolder(AppContext.BaseDirectory), settingsPath);
        MainWindow = window;
        window.Show();
    }
}
