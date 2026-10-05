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

// MainWindow.xaml.cs - fereastra: dialogele si bara de titlu intunecata. Tot ce inseamna
// decizie sta in MainViewModel.

using System.Linq;
using System.Windows;

namespace AstroPhotoAnalyzer.Wpf
{
    public partial class MainWindow : Window
    {
        readonly MainViewModel _vm = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            // Ferestrele de vizualizare ale tabului Tonuri (R-130, R-131): view-model-ul cere,
            // fereastra deschide. Nemodale, maximizate, cate vrea omul.
            _vm.Tones.MapRequested += m => new MapWindow { Owner = this, DataContext = m }.Show();
            _vm.Tones.PreviewRequested += p => new PreviewWindow { Owner = this, DataContext = p }.Show();
            // Tabul Curbe (R-146, R-147): fereastra curbei, dialogul de salvare si sagetile.
            _vm.Curves.CurveRequested += c => new CurveWindow { Owner = this, DataContext = c }.Show();
            _vm.Curves.SaveRequested += SaveCurve;
            PreviewKeyDown += GalleryKeys;
        }

        /// <summary>Pornirea: cu un fisier din linia de comanda il deschide; altfel intreaba imediat.</summary>
        internal void Start(string file)
        {
            if (!string.IsNullOrEmpty(file)) { _ = _vm.OpenAsync(file); return; }
            Dispatcher.BeginInvoke(new System.Action(() => Choose()));
        }

        void ChooseImage(object sender, RoutedEventArgs e) => Choose();

        void Analyze(object sender, RoutedEventArgs e) => _ = _vm.RunAsync();

        /// <summary>"Din profil ICC...": fereastra cu profilele instalate; "Foloseste" scrie Dmax-ul profilului in camp (R-132).</summary>
        void ChooseProfile(object sender, RoutedEventArgs e)
        {
            var picker = new IccPickerViewModel();
            if (new IccPickerWindow { Owner = this, DataContext = picker }.ShowDialog() == true)
                _vm.Parameters.UseProfile(picker.Selected);
        }

        void ShowAbout(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
            => new AboutWindow { Owner = this }.ShowDialog();

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
        /// Sagetile stanga / dreapta rasfoiesc galeria cat timp tabul Curbe e cel ales (R-146).
        /// Se prind la fereastra, inainte de TabControl, care altfel le-ar folosi ca sa schimbe
        /// tabul; intr-un camp de text raman ale campului.
        /// </summary>
        void GalleryKeys(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Left && e.Key != System.Windows.Input.Key.Right) return;
            if (!_vm.HasResult || !CurvesTabItem.IsSelected || !_vm.Curves.HasGallery) return;
            if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) return;
            System.Windows.Input.ICommand move = e.Key == System.Windows.Input.Key.Left ? _vm.Curves.Previous : _vm.Curves.Next;
            if (move.CanExecute(null)) move.Execute(null);
            e.Handled = true;
        }

        /// <summary>Intreaba unde se salveaza FITS-ul curbei aratate (R-145); scrierea o face nucleul.</summary>
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
