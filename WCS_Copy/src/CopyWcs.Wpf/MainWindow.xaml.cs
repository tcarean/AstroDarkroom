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
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NLog;
using static CopyWcs.Ui;

namespace CopyWcs.Wpf;

/// <summary>
/// The main window: the band at the top, the two files, the output choice, the options, the two
/// commands and the core's outcome, over one <see cref="MainViewModel"/>. It owns what needs a
/// window (the file dialogs, the About window, the manual opened by Help and F1, a close held
/// while a run is in flight) and computes nothing (ADR 0007, ADR 0008, ADR 0016, ADR 0019).
/// </summary>
public partial class MainWindow : Window
{
    // The dialogs' filter: FITS first, everything else second; the FITS label reads the same
    // in both languages.
    private const string FitsPattern = "*.fit;*.fits;*.fts";

    /// <summary>The window's lines in the diagnostic log: the files picked in the dialogs, the About window, the manual opened, a close held during a run (R-043).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    private readonly MainViewModel _vm;

    /// <summary>True once a close was asked while a run was in flight: the window closes when the run ends (R-050).</summary>
    private bool _closeWhenTheRunEnds;

    /// <summary>
    /// Opens an address in the system's browser: <see cref="ExternalLink.Open(string)"/>, which a
    /// test replaces so that no browser starts.
    /// </summary>
    internal Action<string> OpenAddress { get; set; } = ExternalLink.Open;

    /// <summary>
    /// Shows the About window, modal, over the window given: a new <see cref="AboutWindow"/> each
    /// time, which a test replaces so that no modal window blocks it.
    /// </summary>
    internal Action<Window> OpenAbout { get; set; } = owner => new AboutWindow { Owner = owner }.ShowDialog();

    /// <summary>The window over a new view-model.</summary>
    public MainWindow() : this(new MainViewModel())
    {
    }

    /// <summary>
    /// The window over a given view-model (the tests pass their own): binds it, asks for the
    /// dark title bar once the window has a handle, and follows the view-model's running state,
    /// so that a close held during a run is done when the run ends (R-050).
    /// </summary>
    /// <param name="vm">The view-model the window binds; not null.</param>
    internal MainWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = _vm;
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        _vm.PropertyChanged += CloseWhenTheRunEnds;
    }

    /// <summary>
    /// Holds a close asked while a Preview or a Copy is in flight (R-050): the run goes on to its
    /// end, its file whole and its lines in the log, and the window closes then; a close with no
    /// run closes at once. A close Windows itself forces (a logoff) is not held.
    /// </summary>
    /// <param name="e">The close; cancelled while a run is in flight.</param>
    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (_vm.IsRunning)
        {
            e.Cancel = true;
            if (!_closeWhenTheRunEnds)
            {
                _closeWhenTheRunEnds = true;
                Log.Info("close asked while a run is in flight: the window closes when it ends");
            }

            return;
        }

        base.OnClosing(e);
    }

    /// <summary>Closes the window once the run ends, when a close was held during it (R-050).</summary>
    /// <param name="sender">The view-model; not read.</param>
    /// <param name="e">The property that changed; only <see cref="MainViewModel.IsRunning"/> is looked at.</param>
    private void CloseWhenTheRunEnds(object? sender, PropertyChangedEventArgs e)
    {
        if (_closeWhenTheRunEnds && e.PropertyName == nameof(MainViewModel.IsRunning) && !_vm.IsRunning)
        {
            _closeWhenTheRunEnds = false;
            Log.Info("the run ended: the window closes");
            Close();
        }
    }

    /// <summary>Fills the boxes from the command line (R-034); a box whose file is absent stays empty.</summary>
    /// <param name="source">The path of the plate-solved source, or null.</param>
    /// <param name="dest">The path of the destination, or null.</param>
    internal void Start(string? source, string? dest)
    {
        if (source is not null)
        {
            _vm.Source = source;
        }

        if (dest is not null)
        {
            _vm.Dest = dest;
        }
    }

    /// <summary>The source's Browse button: asks for the plate-solved file.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void BrowseSource(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dlg = Open(T("Plate-solved source (FITS)", "Sursa plate-solved (FITS)"), _vm.Source);
        if (dlg.ShowDialog(this) == true)
        {
            Log.Info("source picked in the dialog: " + dlg.FileName);
            _vm.Source = dlg.FileName;
        }
        else
        {
            Log.Debug("source dialog closed without a file");
        }
    }

    /// <summary>The destination's Browse button: asks for the file that receives the cards.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void BrowseDest(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dlg = Open(T("Destination (FITS)", "Destinatia (FITS)"), _vm.Dest);
        if (dlg.ShowDialog(this) == true)
        {
            Log.Info("destination picked in the dialog: " + dlg.FileName);
            _vm.Dest = dlg.FileName;
        }
        else
        {
            Log.Debug("destination dialog closed without a file");
        }
    }

    /// <summary>The new file's Browse button: asks where to write, starting beside the destination.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void BrowseOutput(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dlg = new()
        {
            Title = T("Write a new file", "Scrie un fisier nou"),
            Filter = Filter(),
            DefaultExt = ".fit",
            AddExtension = true,
            OverwritePrompt = true,
        };
        string? beside = Path.GetDirectoryName(_vm.Dest);
        if (!string.IsNullOrEmpty(beside) && Directory.Exists(beside))
        {
            dlg.InitialDirectory = beside;
        }

        if (dlg.ShowDialog(this) == true)
        {
            Log.Info("new file picked in the dialog: " + dlg.FileName);
            _vm.Output = dlg.FileName;
        }
        else
        {
            Log.Debug("new file dialog closed without a file");
        }
    }

    /// <summary>The Preview button: the core's dry run, off this thread; the buttons are off until it ends.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private async void Preview(object sender, RoutedEventArgs e) => await _vm.Preview().ConfigureAwait(true);

    /// <summary>The Copy button: the core's copy, off this thread; the buttons are off until it ends.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private async void CopyCards(object sender, RoutedEventArgs e) => await _vm.Copy().ConfigureAwait(true);

    /// <summary>The band's About button, and only it: the About window, modal, over this one (R-035, R-038).</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void ShowAbout(object sender, RoutedEventArgs e)
    {
        Log.Info("About opened");
        OpenAbout(this);
    }

    /// <summary>The band's Help button: the manual at the section of what the card shows (R-039).</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void HelpClick(object sender, RoutedEventArgs e) => ShowHelp();

    /// <summary>
    /// Takes F1 wherever the focus is in the window, before any control sees it (R-039): the
    /// first press opens the manual, as the Help button does; a repeat of the key held down,
    /// some thirty a second, is taken and does nothing, since each would open the browser again.
    /// Any other key, and F1 with a modifier, go on as before.
    /// </summary>
    /// <param name="sender">The window itself; not read.</param>
    /// <param name="e">The key; marked handled when it is F1 alone (<see cref="IsHelpKey"/>).</param>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsHelpKey(e.Key, Keyboard.Modifiers))
        {
            e.Handled = true;
            if (!e.IsRepeat)
            {
                ShowHelp();
            }
        }
    }

    /// <summary>Whether a key pressed with these modifiers is Help's: F1 alone (R-039).</summary>
    /// <param name="key">The key pressed.</param>
    /// <param name="modifiers">The modifier keys held with it, as <see cref="Keyboard.Modifiers"/> reads them.</param>
    /// <returns>True for F1 with no modifier; false for any other key and for F1 with Shift, Ctrl, Alt or Windows.</returns>
    internal static bool IsHelpKey(Key key, ModifierKeys modifiers) => key == Key.F1 && modifiers == ModifierKeys.None;

    /// <summary>
    /// The Help button and F1 (R-039, ADR 0016): the manual in the browser, at the section that
    /// <see cref="Manual.SectionFor"/> picks from what the outcome card shows, and the address in
    /// the log.
    /// </summary>
    private void ShowHelp()
    {
        string address = Manual.Url(Manual.SectionFor(_vm.HasOutcome, _vm.HasError, _vm.HasWarnings));
        Log.Info("Help opened: " + address);
        OpenAddress(address);
    }

    /// <summary>An open-file dialog for FITS files, starting in the folder of a path when it has one.</summary>
    /// <param name="title">The dialog's title, in the run's language.</param>
    /// <param name="current">The path in the box, whose folder is proposed when it exists; may be empty.</param>
    /// <returns>The dialog, not yet shown.</returns>
    private static OpenFileDialog Open(string title, string current)
    {
        OpenFileDialog dlg = new() { Title = title, Filter = Filter(), CheckFileExists = true };
        string? folder = Path.GetDirectoryName(current);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            dlg.InitialDirectory = folder;
        }

        return dlg;
    }

    /// <summary>The dialogs' filter, worded in the run's language.</summary>
    /// <returns>Two entries: the FITS extensions, then every file.</returns>
    private static string Filter()
        => $"FITS ({FitsPattern})|{FitsPattern}|" + T("All files", "Toate fisierele") + "|*.*";
}
