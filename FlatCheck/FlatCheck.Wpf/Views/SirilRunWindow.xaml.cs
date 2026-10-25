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

// SirilRunWindow.xaml.cs - the window that runs a Siril script.
//
// Everything that is state sits in SirilRunViewModel. Here remain the things that really
// need a window: the start on loading, scrolling the log to its end, the confirmation on
// closing during the run, and the dark title bar.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The modal window that runs one Siril script: the current command and its progress, the
    /// log, and the run's verdict at the end, bound to <see cref="SirilRunViewModel"/>.
    /// </summary>
    public partial class SirilRunWindow : Window
    {
        readonly SirilRunViewModel _vm;
        readonly SirilRunner _runner;

        /// <summary>
        /// Builds the window over the run's view-model; the run starts once the window has
        /// loaded.
        /// </summary>
        /// <param name="vm">The run's state: the script, the commands and the log.</param>
        /// <param name="runner">What starts siril-cli; a fake one in the tests.</param>
        internal SirilRunWindow(SirilRunViewModel vm, SirilRunner runner)
        {
            InitializeComponent();
            _vm = vm;
            _runner = runner;
            DataContext = vm;

            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            Loaded += (_, _) => _ = _vm.RunAsync(_runner);
            Closing += OnClosing;
        }

        /// <summary>
        /// Closing during the run means stopping Siril in the middle, so it asks. "Nu"
        /// leaves the window open; "Da" kills the process and closes - the cancellation
        /// verdict reaches the view-model, which nobody sees any more.
        /// </summary>
        /// <param name="sender">The window.</param>
        /// <param name="e">The close; cancelled when the user answers "Nu".</param>
        void OnClosing(object sender, CancelEventArgs e)
        {
            if (!_vm.IsRunning) return;

            var answer = MessageBox.Show(this,
                T("The script is still running. Stop it? The half-written files stay in process/ "
                  + "and a new run overwrites them.",
                  "Scriptul inca ruleaza. Il opresc? Fisierele scrise pe jumatate raman in process/ "
                  + "si o rulare noua le suprascrie."),
                _vm.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            if (answer != MessageBoxResult.Yes) { e.Cancel = true; return; }
            _vm.Cancel();
        }

        /// <summary>The stop button's handler: cancels the run.</summary>
        /// <param name="sender">The button.</param>
        /// <param name="e">The click.</param>
        void CancelRun(object sender, RoutedEventArgs e) => _vm.Cancel();

        /// <summary>The close button's handler, once the run has ended.</summary>
        /// <param name="sender">The button.</param>
        /// <param name="e">The click.</param>
        void CloseWindow(object sender, RoutedEventArgs e) => Close();

        /// <summary>The log follows the last line, like Siril's panel.</summary>
        /// <param name="sender">The log's text box.</param>
        /// <param name="e">The change.</param>
        void ScrollToEnd(object sender, TextChangedEventArgs e) => ((TextBox)sender).ScrollToEnd();
    }
}
