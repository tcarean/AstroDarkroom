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
using System.Windows.Controls;
using System.Windows.Input;
using NLog;

namespace LightsCheck.Wpf;

/// <summary>
/// The main window: the top bar and the tabs over one <see cref="MainViewModel"/>; its code only
/// opens the dialogs, hands the chosen folders to the view-model, and opens the manual in the
/// browser (Help, F1).
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>The diagnostic log: the dialogs and the buttons the user pressed (R-071).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    private readonly MainViewModel _vm;

    /// <summary>
    /// Whether a key-down of F1 went on, not taken as a repeat, since the window last came to the
    /// front: only then is a key-down that WPF marks a repeat a repeat of that press.
    /// </summary>
    private bool _f1Through;

    /// <summary>The window over a new view-model, as the application opens it.</summary>
    public MainWindow() : this(new MainViewModel())
    {
    }

    /// <summary>
    /// The window over a given view-model (the tests pass their own): binds it, asks for the dark
    /// title bar once the window has a handle, keeps F1 held down from opening the manual again
    /// and again, and stops the run in flight when the window closes.
    /// </summary>
    /// <param name="vm">The view-model the window binds.</param>
    internal MainWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = _vm;
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        PreviewKeyDown += IgnoreHeldF1;
        Activated += (_, _) => _f1Through = false;
        Closed += (_, _) => _vm.CancelPending();
    }

    /// <summary>
    /// Takes the repeats of F1 held down (R-091): a held key repeats some thirty times a second,
    /// and WPF turns each repeat into the Help command, which would open the browser again each
    /// time. Marked handled here, WPF raises no KeyDown for a repeat, so the command never sees
    /// it; the first press goes on to the command. WPF forgets a held key only when it sees the
    /// key released, and the browser a press brings to the front may take that release: the
    /// first F1 after the window comes back to the front goes through though WPF calls it a
    /// repeat.
    /// </summary>
    /// <param name="sender">The window; not used.</param>
    /// <param name="e">The key pressed; marked handled when it is a repeat of an F1 that went
    /// through.</param>
    private void IgnoreHeldF1(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F1)
        {
            return;
        }

        if (e.IsRepeat && _f1Through)
        {
            e.Handled = true;
        }
        else
        {
            _f1Through = true;
        }
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
    /// The "Choose folder..." and "Choose another folder..." buttons: asks for a session folder
    /// and opens it.
    /// </summary>
    /// <param name="sender">The button clicked.</param>
    /// <param name="e">The click's arguments, not used.</param>
    private void ChooseFolder(object sender, RoutedEventArgs e) => Choose();

    /// <summary>The "Stop" buttons: stops the run in flight, if any.</summary>
    /// <param name="sender">The button clicked.</param>
    /// <param name="e">The click's arguments, not used.</param>
    private void Stop(object sender, RoutedEventArgs e)
    {
        Log.Info("stop pressed");
        _vm.CancelPending();
    }

    /// <summary>
    /// Opens an address in the browser: <see cref="ExternalLink.Open"/>, replaced in the tests so
    /// that no browser starts.
    /// </summary>
    internal Action<Uri> OpenAddress { get; set; } = ExternalLink.Open;

    /// <summary>
    /// The Help command (the "Help" button or F1, see MainWindow.xaml): opens the manual at the
    /// section of what is on screen, the selected tab's read from its Tag, and logs the address
    /// (R-091, ADR 0064).
    /// </summary>
    /// <param name="sender">The window, where the command is bound.</param>
    /// <param name="e">The command's arguments, not used.</param>
    private void ShowHelp(object sender, ExecutedRoutedEventArgs e)
    {
        ManualSection? tab = (Tabs.SelectedItem as TabItem)?.Tag as ManualSection?;
        string address = Manual.Url(Manual.SectionFor(_vm.HasResult, _vm.IsRunning, tab));
        Log.Info("Help opened: " + address);
        OpenAddress(new Uri(address));
    }

    /// <summary>
    /// The "About" button: shows the About window, modal, fitted to the work area of this window's
    /// screen (R-076).
    /// </summary>
    /// <param name="sender">The button clicked.</param>
    /// <param name="e">The click's arguments, not used.</param>
    private void ShowAbout(object sender, RoutedEventArgs e)
    {
        Log.Info("About opened");
        new AboutWindow(ScreenArea.WorkAreaOf(this)) { Owner = this }.ShowDialog();
    }

    /// <summary>
    /// The export folder is asked for, the session root proposed (ADR 0002: beside the session,
    /// never in lights/), and the files are written there.
    /// </summary>
    /// <param name="sender">The "Write the files..." button.</param>
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
