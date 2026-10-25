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

using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AstroDarkroom.Core;

namespace AstroDarkroom.Wpf;

/// <summary>
/// The launcher's window: a row per app of the catalogue, each with its "Deschide" button, and
/// "Despre". It shows what the core says about the suite folder and starts what the core plans
/// (R-002, R-003); it decides nothing itself.
/// </summary>
public partial class MainWindow : Window
{
    private readonly SuiteFolder _folder;
    private readonly ReleaseInfo? _release;

    /// <summary>
    /// The window on one suite folder, with its rows built once.
    /// </summary>
    /// <param name="folder">The suite folder the apps are looked up in: the launcher's own folder.</param>
    public MainWindow(SuiteFolder folder)
    {
        InitializeComponent();
        _folder = folder;
        _release = ReleaseManifest.TryRead(folder.ManifestPath);
        Logo.Source = App.LargestIconFrame();
        Banner.Text = VersionInfo.Banner;
        FolderLine.Text = _release is { Date.Length: > 0 } r ? $"{folder.BaseDirectory}  -  release din {r.Date}" : folder.BaseDirectory;
        Rows.ItemsSource = AppCatalogue.Apps
            .Select(app => new AppRow(app, folder.Locate(app), _release?.Find(app.Folder)))
            .ToList();
    }

    /// <summary>
    /// Starts the app of the clicked row, from the suite folder, through the shell (R-003). A
    /// start that fails (the file went away since the window opened, or Windows refused it) is
    /// told in a message box; the launcher stays open.
    /// </summary>
    /// <param name="sender">The row's button; its DataContext is the row.</param>
    /// <param name="e">The click; not read.</param>
    private void OpenApp(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AppRow row })
        {
            return;
        }

        try
        {
            LaunchPlan plan = Launcher.Plan(row.App, _folder);
            Process.Start(new ProcessStartInfo(plan.FileName) { UseShellExecute = true, WorkingDirectory = plan.WorkingDirectory });
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, row.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Win32Exception ex)
        {
            MessageBox.Show(this, $"{row.App.WindowExe} nu a putut porni: {ex.Message}", row.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Opens "Despre" (the Help command, F1 or the button).</summary>
    /// <param name="sender">The window; not read.</param>
    /// <param name="e">The command; not read.</param>
    private void ShowAbout(object sender, ExecutedRoutedEventArgs e)
    {
        AboutWindow about = new(_release) { Owner = this };
        about.ShowDialog();
    }
}
