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

// MainWindow.xaml.cs - the window: the dialogs and the dark title bar. Everything that means a
// decision sits in MainViewModel.

using System.Linq;
using System.Windows;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The main window (R-110): owns the <see cref="MainViewModel"/>, opens the file and save
    /// dialogs and the secondary windows the view-models ask for, and forwards the gallery keys.
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly MainViewModel _vm = new();

        /// <summary>
        /// Builds the window over a new view-model, asks for the dark title bar once there is a
        /// handle, and subscribes to the requests of the Tonuri and Curbe tabs.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            // The viewing windows of the Tonuri tab (R-130, R-131): the view-model asks, the
            // window opens. Non-modal, maximised, as many as the user wants.
            _vm.Tones.MapRequested += m => new MapWindow { Owner = this, DataContext = m }.Show();
            _vm.Tones.PreviewRequested += p => new PreviewWindow { Owner = this, DataContext = p }.Show();
            // The Curbe tab (R-146, R-147): the curve window, the save dialog and the arrow keys.
            _vm.Curves.CurveRequested += c => new CurveWindow { Owner = this, DataContext = c }.Show();
            _vm.Curves.SaveRequested += SaveCurve;
            PreviewKeyDown += GalleryKeys;
        }

        /// <summary>
        /// The start: with a file from the command line it opens it; otherwise it asks at once.
        /// </summary>
        /// <param name="file">
        /// The file to open; null or empty shows the open dialog instead.
        /// </param>
        internal void Start(string file)
        {
            if (!string.IsNullOrEmpty(file)) { _ = _vm.OpenAsync(file); return; }
            Dispatcher.BeginInvoke(new System.Action(() => Choose()));
        }

        /// <summary>"Deschide imagine...": the open dialog.</summary>
        /// <param name="sender">The button; not read.</param>
        /// <param name="e">The click; not read.</param>
        void ChooseImage(object sender, RoutedEventArgs e) => Choose();

        /// <summary>"Analizeaza": runs the analysis again with the fields as they are.</summary>
        /// <param name="sender">The button; not read.</param>
        /// <param name="e">The click; not read.</param>
        void Analyze(object sender, RoutedEventArgs e) => _ = _vm.RunAsync();

        /// <summary>
        /// "Din profil ICC...": the window with the installed profiles; "Foloseste" writes the
        /// profile's Dmax into the field (R-132).
        /// </summary>
        /// <param name="sender">The button; not read.</param>
        /// <param name="e">The click; not read.</param>
        void ChooseProfile(object sender, RoutedEventArgs e)
        {
            var picker = new IccPickerViewModel();
            if (new IccPickerWindow { Owner = this, DataContext = picker }.ShowDialog() == true)
                _vm.Parameters.UseProfile(picker.Selected);
        }

        /// <summary>"Despre", the button or F1: the about window, modal (R-119).</summary>
        /// <param name="sender">The command's source; not read.</param>
        /// <param name="e">The command's arguments; not read.</param>
        void ShowAbout(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
            => new AboutWindow { Owner = this }.ShowDialog();

        /// <summary>
        /// The open dialog, filtered to the extensions the core reads; a chosen file is opened
        /// and analysed at once.
        /// </summary>
        void Choose()
        {
            string patterns = string.Join(";", ImageLoader.Extensions.Select(x => "*" + x));
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Alege imaginea finita",
                Filter = $"Imagini ({patterns})|{patterns}|Toate fisierele|*.*",
                Multiselect = false,
            };
            if (dlg.ShowDialog(this) == true) _ = _vm.OpenAsync(dlg.FileName);
        }

        /// <summary>
        /// The Left and Right arrows browse the gallery while the Curbe tab is the one selected
        /// (R-146). They are caught at the window, before the TabControl, which would otherwise
        /// use them to change the tab; in a text box they stay the box's.
        /// </summary>
        /// <param name="sender">The window; not read.</param>
        /// <param name="e">
        /// The key; marked handled whenever the gallery is the target, also at its ends, where
        /// nothing moves, so the TabControl never gets the arrow.
        /// </param>
        void GalleryKeys(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Left && e.Key != System.Windows.Input.Key.Right) return;
            if (!_vm.HasResult || !CurvesTabItem.IsSelected || !_vm.Curves.HasGallery) return;
            if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) return;
            System.Windows.Input.ICommand move = e.Key == System.Windows.Input.Key.Left ? _vm.Curves.Previous : _vm.Curves.Next;
            if (move.CanExecute(null)) move.Execute(null);
            e.Handled = true;
        }

        /// <summary>
        /// Asks where the FITS of the curve shown is saved (R-145); the core does the writing,
        /// under a wait cursor.
        /// </summary>
        /// <param name="proposedName">The file name the core proposes, shown in the dialog.</param>
        void SaveCurve(string proposedName)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Salveaza imaginea cu curba aplicata",
                FileName = proposedName,
                InitialDirectory = _vm.Curves.InputFolder,
                Filter = "FITS|*.fit",
                DefaultExt = ".fit",
            };
            if (dlg.ShowDialog(this) != true) return;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            try { _vm.Curves.Save(dlg.FileName); }
            finally { System.Windows.Input.Mouse.OverrideCursor = null; }
        }

        /// <summary>
        /// "Salveaza raportul...": the save dialog, proposing Raport.txt beside the image.
        /// </summary>
        /// <param name="sender">The button; not read.</param>
        /// <param name="e">The click; not read.</param>
        void SaveReport(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Salveaza raportul",
                FileName = System.IO.Path.GetFileName(_vm.DefaultReportPath),
                InitialDirectory = System.IO.Path.GetDirectoryName(_vm.DefaultReportPath),
                Filter = "Text|*.txt",
                DefaultExt = ".txt",
            };
            if (dlg.ShowDialog(this) == true) _vm.SaveReport(dlg.FileName);
        }
    }
}
