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

// IccPickerWindow.xaml.cs - the link with the XAML of the "Din profil ICC..." window (R-132).
// The behaviour is in IccPickerViewModel; here are the file dialog (the view-model opens no
// dialogs), the closing with a result and the dark title bar.

using System.Windows;
using System.Windows.Input;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The profile window (R-132), modal: closes with true when the user takes a readable
    /// profile, with false or null otherwise; the caller reads the view-model's selection.
    /// </summary>
    public partial class IccPickerWindow : Window
    {
        /// <summary>
        /// Loads the XAML, asks for the dark title bar once there is a handle; Esc closes.
        /// </summary>
        public IccPickerWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }

        IccPickerViewModel Vm => (IccPickerViewModel)DataContext;

        /// <summary>The diagnostic log: the file dialog and its answer (R-171, ADR 0069).</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

        /// <summary>
        /// "Alege fisier...": the open dialog for a profile from anywhere, added to the list.
        /// </summary>
        /// <param name="sender">The button; not read.</param>
        /// <param name="e">The click; not read.</param>
        void ChooseFile(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = Tr.Pick("Choose the ICC profile of the paper on its printer", "Alege profilul ICC al hartiei pe imprimanta ei"),
                Filter = Tr.Pick("ICC profiles (*.icc;*.icm)|*.icc;*.icm|All files|*.*", "Profile ICC (*.icc;*.icm)|*.icc;*.icm|Toate fisierele|*.*"),
                Multiselect = false,
            };
            if (dlg.ShowDialog(this) == true)
            {
                Log.Info("profile file dialog: chose " + dlg.FileName);
                Vm.AddFile(dlg.FileName);
            }
            else Log.Info("profile file dialog: cancelled");
        }

        /// <summary>
        /// "Foloseste": closes with true, only when the selected profile is readable.
        /// </summary>
        /// <param name="sender">The button; not read.</param>
        /// <param name="e">The click; not read.</param>
        void Use(object sender, RoutedEventArgs e)
        {
            if (Vm.CanUse) DialogResult = true;
        }

        /// <summary>
        /// A double-click on a row is "Foloseste"; on the scroll bar or the list's blank it is
        /// nothing.
        /// </summary>
        /// <param name="sender">The list; not read.</param>
        /// <param name="e">The click; its button and original source are read.</param>
        void UseOnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // A double-click on a row, not on the scroll bar or on the list's empty space.
            if (e.ChangedButton == MouseButton.Left && e.OriginalSource is DependencyObject d && ItemsControl_ContainerOf(d) && Vm.CanUse) DialogResult = true;
        }

        /// <summary>Whether an element sits inside one of the list's item containers.</summary>
        /// <param name="d">The element clicked.</param>
        /// <returns>True inside a row of the list; false elsewhere in it.</returns>
        bool ItemsControl_ContainerOf(DependencyObject d)
            => System.Windows.Controls.ItemsControl.ContainerFromElement(List, d) != null;
    }
}
