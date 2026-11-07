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

namespace DarksCheck.Wpf;

/// <summary>
/// The main window: the top bar and the four states over one <see cref="MainViewModel"/>. It
/// owns what needs a window (the folder dialogs, the clipboard, the About window) and
/// computes nothing.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>The diagnostic log: the dialogs and the buttons the user pressed (R-101).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    private readonly MainViewModel _vm;

    /// <summary>The window over a new view-model, which reads the stored settings.</summary>
    public MainWindow() : this(new MainViewModel())
    {
    }

    /// <summary>
    /// The window over a given view-model (the tests pass their own): binds it, asks for the dark
    /// title bar once the window has a handle, and cancels a run in flight when the window closes.
    /// </summary>
    /// <param name="vm">The view-model the window binds; not null.</param>
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
    /// The folder to open; null or empty opens the folder dialog once the window has been shown.
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
    /// The folder buttons (top bar, empty state, problem card): asks for a folder and opens it.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void ChooseFolder(object sender, RoutedEventArgs e) => Choose();

    /// <summary>The stop buttons (top bar, wait screen): cancels the run in flight.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void Stop(object sender, RoutedEventArgs e)
    {
        Log.Info("stop pressed");
        _vm.CancelPending();
    }

    // The Index tab's buttons reach these: the dialogs and the clipboard belong to the window.
    /// <summary>The Index tab's copy button: puts the label in the clipboard.</summary>
    internal void CopyLabelFromView() => CopyLabel(this, new RoutedEventArgs());

    /// <summary>The Index tab's partner button: asks for the partner folder.</summary>
    internal void ChoosePartnerFromView() => ChoosePartner(this, new RoutedEventArgs());

    /// <summary>The Index tab's clear button: forgets the partner folder.</summary>
    internal void ClearPartnerFromView() => ClearPartner(this, new RoutedEventArgs());

    /// <summary>The Lights tab's lights button (R-122): asks for the folder of lights.</summary>
    internal void ChooseLightsFromView() => ChooseLights();

    /// <summary>The Lights tab's clear button (R-122): back to the open folder's own lights.</summary>
    internal void ClearLightsFromView() => _vm.LightsFrom = null;

    /// <summary>
    /// Asks for the folder of lights (R-055, R-122), starting in the current choice when it still
    /// exists, and sets it on the view-model; a cancelled dialog changes nothing.
    /// </summary>
    private void ChooseLights()
    {
        Microsoft.Win32.OpenFolderDialog dlg = new() { Title = WindowTexts.ChooseLightsTitle, Multiselect = false };
        if (_vm.LightsFrom is string l && Directory.Exists(l))
        {
            dlg.InitialDirectory = l;
        }

        if (dlg.ShowDialog(this) != true)
        {
            Log.Info("lights dialog: cancelled");
            return;
        }

        Log.Info("lights dialog: chose " + dlg.FolderName);
        _vm.LightsFrom = dlg.FolderName;
    }

    /// <summary>
    /// The Index tab's rerun button: runs the same folder again with the settings and the partner
    /// as they are now.
    /// </summary>
    internal void RerunFromView()
    {
        Log.Info("rerun pressed");
        _ = _vm.RerunAsync();
    }

    /// <summary>
    /// Shows the About window, modal, over this one, fitted to the work area of this window's
    /// screen (R-121).
    /// </summary>
    /// <param name="sender">The command's source.</param>
    /// <param name="e">The executed command.</param>
    private void ShowAbout(object sender, ExecutedRoutedEventArgs e)
    {
        Log.Info("About opened");
        new AboutWindow(ScreenArea.WorkAreaOf(this)) { Owner = this }.ShowDialog();
    }

    /// <summary>
    /// The partner folder (R-073): a library or another session; the current one proposed.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void ChoosePartner(object sender, RoutedEventArgs e) => ChoosePartner();

    /// <summary>
    /// Asks for the partner folder, starting in the current partner when it still exists, and sets
    /// it on the view-model.
    /// </summary>
    /// <returns>True when a folder was chosen (the same one again counts); false when the dialog
    /// was cancelled.</returns>
    private bool ChoosePartner()
    {
        Microsoft.Win32.OpenFolderDialog dlg = new() { Title = WindowTexts.ChoosePartnerTitle, Multiselect = false };
        if (_vm.Partner is string p && Directory.Exists(p))
        {
            dlg.InitialDirectory = p;
        }

        if (dlg.ShowDialog(this) != true)
        {
            Log.Info("partner dialog: cancelled");
            return false;
        }

        Log.Info("partner dialog: chose " + dlg.FolderName);
        _vm.Partner = dlg.FolderName;
        return true;
    }

    /// <summary>Forgets the partner folder; the next run has none.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void ClearPartner(object sender, RoutedEventArgs e) => _vm.Partner = null;

    /// <summary>
    /// The problem card's way out for a session without darks (R-077): choose the partner, run the
    /// same folder again; a cancelled dialog does nothing.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void ChoosePartnerAndRerun(object sender, RoutedEventArgs e)
    {
        if (ChoosePartner())
        {
            _ = _vm.RerunAsync();
        }
    }

    /// <summary>
    /// The label goes to the clipboard; the folder is the owner's to rename (ADR 0002). Nothing
    /// happens without a label, or when another process holds the clipboard.
    /// </summary>
    /// <param name="sender">The button, or this window when the Index tab calls.</param>
    /// <param name="e">The click.</param>
    private void CopyLabel(object sender, RoutedEventArgs e)
    {
        if (_vm.Label.Length == 0)
        {
            return;
        }

        try
        {
            Clipboard.SetText(_vm.Label);
            _vm.LabelCopied();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            // The clipboard is held by another process: the label stays on screen to be selected by
            // hand.
            Log.Debug(ex, "the label could not be copied: the clipboard is held by another process");
        }
    }

    /// <summary>
    /// The export folder is asked for, the opened folder proposed (ADR 0002: beside the frames,
    /// never among them); the view-model writes the files and reports where.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
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
    /// Asks for a session or library folder and opens it; a cancelled dialog does nothing.
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
