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

namespace LightsCheck.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow() : this(new MainViewModel())
    {
    }

    internal MainWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = _vm;
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        Closed += (_, _) => _vm.CancelPending();
    }

    /// <summary>Starts on the folder from the command line, or asks for one at once.</summary>
    internal void Start(string? folder)
    {
        if (!string.IsNullOrEmpty(folder))
        {
            _ = _vm.OpenAsync(folder);
            return;
        }

        Dispatcher.BeginInvoke(new Action(Choose));
    }

    private void ChooseFolder(object sender, RoutedEventArgs e) => Choose();

    private void Stop(object sender, RoutedEventArgs e) => _vm.CancelPending();

    private void ShowAbout(object sender, ExecutedRoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    /// <summary>The export folder is asked for, the session root proposed (ADR 0002: beside the session, never in lights/).</summary>
    private void ExportFiles(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.OpenFolderDialog dlg = new() { Title = WindowTexts.ExportTitle, Multiselect = false };
        if (_vm.HasFolder)
        {
            dlg.InitialDirectory = _vm.Folder;
        }

        if (dlg.ShowDialog(this) == true)
        {
            _vm.Export(dlg.FolderName);
        }
    }

    private void Choose()
    {
        Microsoft.Win32.OpenFolderDialog dlg = new() { Title = WindowTexts.ChooseTitle, Multiselect = false };
        if (dlg.ShowDialog(this) == true)
        {
            _ = _vm.OpenAsync(dlg.FolderName);
        }
    }
}
