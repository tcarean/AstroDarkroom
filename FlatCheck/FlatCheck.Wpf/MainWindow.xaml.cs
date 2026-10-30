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

// MainWindow.xaml.cs - the window: the choice of the folder and the dark title bar.
//
// Everything that means a decision sits in MainViewModel. Here remain only the main window's
// own dialogs and child windows (the folder dialog, the "Despre" window, the Siril run window,
// the question when closing during a split), the links the tabs show, opened in the browser,
// and the request to DWM to darken the title bar.
// The other dialogs live in their views (SirilRunWindow's stop question), or reach a
// view-model only as a delegate it is given (the Netezire and Sintetic
// tabs' overwrite question, MainViewModel.AskOnScreen by default), so that a test can answer
// it.

using System.Windows;
using NLog;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The main window: hosts the tabs bound to <see cref="MainViewModel"/>, opens the folder
    /// dialog, the "Despre" and Siril run windows, and guards the close during a split.
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly MainViewModel _vm = new();

        /// <summary>Builds the window over a new <see cref="MainViewModel"/> and wires the
        /// title bar and the close.</summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            // The title bar can be coloured only after the window has a handle.
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);

            // Without this, closing the window in the middle of a series of 32 frames would
            // let the computing thread carry the work to the end for nothing.
            Closing += OnClosing;
            Closed += (_, _) => _vm.CancelPending();
        }

        /// <summary>
        /// The start: if a folder was given on the command line it is used, otherwise it is
        /// asked for at once. The application has nothing to show without a folder, so waiting
        /// for one more click would only be an empty step.
        /// </summary>
        /// <param name="folder">The folder to open, or null or empty to ask.</param>
        internal void Start(string folder)
        {
            if (!string.IsNullOrEmpty(folder)) { _ = _vm.OpenAsync(folder); return; }

            Dispatcher.BeginInvoke(new System.Action(() => Choose()));
        }

        /// <summary>The "choose folder" button's handler.</summary>
        /// <param name="sender">The button.</param>
        /// <param name="e">The click.</param>
        void ChooseFolder(object sender, RoutedEventArgs e) => Choose();

        bool _closeAfterSplit;

        /// <summary>
        /// A split under way: Siril runs and the temp folder is on disk. Closed directly, the
        /// window would kill Siril through CancelPending, but the folder's deletion is a
        /// continuation on the dispatcher that is just closing, and it would never run: ~17 GB
        /// would stay in %TEMP% without any note. So it asks, stops, and the window closes by
        /// itself once the run has had time to delete.
        /// </summary>
        /// <param name="sender">The window.</param>
        /// <param name="e">The close; cancelled while a split runs.</param>
        void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SplitTabViewModel split = _vm.Split;
            if (!split.IsRunning) return;

            // already stopped and waiting for the deletion: a second click on X closes nothing
            // sooner - exactly that way the temp folder would be lost
            e.Cancel = true;
            if (_closeAfterSplit) return;

            var answer = MessageBox.Show(this, SessionMessages.SplitCloseQuestion(), "FlatCheck",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            Log.Info("closing during a split: the user answered " + answer);
            if (answer != MessageBoxResult.Yes) return;

            _closeAfterSplit = true;
            split.StopRun();
            _ = split.Pending.ContinueWith(_ => Dispatcher.BeginInvoke(new System.Action(() =>
            {
                _closeAfterSplit = false;
                if (!split.IsRunning) Close();   // otherwise another started meanwhile: we stay, and X asks again
            })));
        }

        /// <summary>The "Despre" button and F1, through ApplicationCommands.Help - see
        /// MainWindow.xaml.</summary>
        /// <param name="sender">The window.</param>
        /// <param name="e">The command.</param>
        void ShowAbout(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            Log.Info("About opened");
            new AboutWindow { Owner = this }.ShowDialog();
        }

        /// <summary>A link clicked in a tab - the Structure tab's link to Adobe's DNG Converter
        /// (R-113) - opened in the browser through <see cref="ExternalLink.Open"/>.</summary>
        /// <param name="sender">The window, where the link's event arrives.</param>
        /// <param name="e">The navigation; marked handled.</param>
        void OpenLink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) => ExternalLink.Open(e);

        /// <summary>The diagnostic log of the user's actions in the main window (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

        /// <summary>
        /// The button on the problem screen: starts the chosen script in siril-cli, modal, with
        /// the log live. When the window closes after a successful run, the folder is reopened
        /// - Check() says whether the master appeared, not the run window. The state is checked
        /// again on the press: Siril may have been opened meanwhile.
        /// </summary>
        /// <param name="sender">The button.</param>
        /// <param name="e">The click.</param>
        void RunPreprocessing(object sender, RoutedEventArgs e)
        {
            PreprocessViewModel p = _vm.Preprocess;
            p.Refresh();
            Log.Info("Siril run asked: script=" + LogText.Str(p.SelectedPath) + " siril-cli=" + LogText.Str(p.CliPath)
                     + " canRun=" + LogText.Yes(p.CanRun) + " blocked=" + LogText.Yes(p.HasBlocker));
            if (p.CanRun == false) return;

            string folder = _vm.Folder;
            var run = new SirilRunViewModel(folder, p.SelectedPath) { Warning = p.Warning };
            var window = new SirilRunWindow(run, SirilScriptRun.For(p.CliPath, folder, p.SelectedPath)) { Owner = this };
            window.ShowDialog();

            Log.Info("Siril run window closed: succeeded=" + (run.Succeeded?.ToString() ?? "not run"));
            if (run.Succeeded == true) _ = _vm.ReopenAsync();
        }

        /// <summary>Asks for a session folder and opens it; does nothing on Cancel.</summary>
        void Choose()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = T("Choose the working folder of the Siril session", "Alege folderul de lucru al sesiunii Siril"),
                Multiselect = false,
            };

            bool chosen = dlg.ShowDialog(this) == true;
            Log.Info("folder dialog: " + (chosen ? "chose " + dlg.FolderName : "cancelled"));
            if (chosen) _ = _vm.OpenAsync(dlg.FolderName);
        }
    }
}
