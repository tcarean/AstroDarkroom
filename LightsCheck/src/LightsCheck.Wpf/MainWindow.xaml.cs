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
using NLog;

namespace LightsCheck.Wpf;

/// <summary>
/// The main window: the top bar and the tabs over one <see cref="MainViewModel"/>; its code only
/// opens the dialogs and hands the chosen folders to the view-model.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>The diagnostic log: the dialogs and the buttons the user pressed (R-071).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    private readonly MainViewModel _vm;

    /// <summary>The window over a new view-model, as the application opens it.</summary>
    public MainWindow() : this(new MainViewModel())
    {
    }

    /// <summary>
    /// The window over a given view-model (the tests pass their own): binds it, asks for the dark
    /// title bar once the window has a handle, and stops the run in flight when the window closes.
    /// </summary>
    /// <param name="vm">The view-model the window binds.</param>
    internal MainWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = _vm;
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        Closed += (_, _) => _vm.CancelPending();
    }

    /// <summary>Starts on the folder from the command line, or asks for one at once.</summary>
    /// <param name="folder">
    /// The folder to open; null or empty opens the folder dialog once the window is shown.
    /// </param>
    internal void Start(string? folder)
    {
        if (!string.IsNullOrEmpty(folder))
        {
            _ = _vm.OpenAsync(folder);
            return;
        }

        Dispatcher.BeginInvoke(new Action(Choose));
    }

    /// <summary>
    /// The "Alege folder..." and "Alege alt folder..." buttons: asks for a session folder and opens
    /// it.
    /// </summary>
    /// <param name="sender">The button clicked.</param>
    /// <param name="e">The click's arguments, not used.</param>
    private void ChooseFolder(object sender, RoutedEventArgs e) => Choose();

    /// <summary>The "Opreste" buttons: stops the run in flight, if any.</summary>
    /// <param name="sender">The button clicked.</param>
    /// <param name="e">The click's arguments, not used.</param>
    private void Stop(object sender, RoutedEventArgs e)
    {
        Log.Info("stop pressed");
        _vm.CancelPending();
    }

    /// <summary>
    /// The Help command (the "Despre" button or F1): shows the About window, modal.
    /// </summary>
    /// <param name="sender">The element the command was executed on.</param>
    /// <param name="e">The command's arguments, not used.</param>
    private void ShowAbout(object sender, ExecutedRoutedEventArgs e)
    {
        Log.Info("About opened");
        new AboutWindow { Owner = this }.ShowDialog();
    }

    /// <summary>
    /// The export folder is asked for, the session root proposed (ADR 0002: beside the session,
    /// never in lights/), and the files are written there.
    /// </summary>
    /// <param name="sender">The "Scrie fisierele..." button.</param>
    /// <param name="e">The click's arguments, not used.</param>
    private void ExportFiles(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.OpenFolderDialog dlg = new() { Title = WindowTexts.ExportTitle, Multiselect = false };
        if (_vm.HasFolder)
        {
            dlg.InitialDirectory = _vm.Folder;
        }

        if (dlg.ShowDialog(this) == true)
        {
            Log.Info("export dialog: chose " + dlg.FolderName);
            _vm.Export(dlg.FolderName);
        }
        else
        {
            Log.Info("export dialog: cancelled");
        }
    }

    /// <summary>
    /// Asks for a session folder and opens it; nothing when the dialog is cancelled.
    /// </summary>
    private void Choose()
    {
        Microsoft.Win32.OpenFolderDialog dlg = new() { Title = WindowTexts.ChooseTitle, Multiselect = false };
        if (dlg.ShowDialog(this) == true)
        {
            Log.Info("folder dialog: chose " + dlg.FolderName);
            _ = _vm.OpenAsync(dlg.FolderName);
        }
        else
        {
            Log.Info("folder dialog: cancelled");
        }
    }
}
