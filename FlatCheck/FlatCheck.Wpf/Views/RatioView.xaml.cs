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

// RatioView.xaml.cs - the link to the XAML, plus the two file dialogs.
//
// The dialog opens from here and not from the view-model on purpose: a view-model that opens
// windows can no longer be tested. Here a path is chosen, nothing more; its validation -
// including the explanation when it is not a flat master - is in RatioTabViewModel.Choose,
// where a test can check it.

using System.Windows;
using System.Windows.Controls;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The Ratio tab: the two masters chosen explicitly, one per orientation, then the split
    /// into the camera-fixed and world-fixed parts, bound to <see cref="RatioTabViewModel"/>.
    /// </summary>
    public partial class RatioView : UserControl
    {
        /// <summary>Loads the XAML.</summary>
        public RatioView() => InitializeComponent();

        /// <summary>The button that chooses the master of set A.</summary>
        /// <param name="sender">The button.</param>
        /// <param name="e">The click.</param>
        void ChooseA(object sender, RoutedEventArgs e) => Choose(isA: true);

        /// <summary>The button that chooses the master of set B.</summary>
        /// <param name="sender">The button.</param>
        /// <param name="e">The click.</param>
        void ChooseB(object sender, RoutedEventArgs e) => Choose(isA: false);

        /// <summary>
        /// Opens the file dialog for one set's master and hands the chosen path to
        /// <see cref="RatioTabViewModel.Choose"/>, which validates it; a cancelled dialog
        /// changes nothing.
        /// </summary>
        /// <param name="isA">True for set A, false for set B.</param>
        void Choose(bool isA)
        {
            if (DataContext is not RatioTabViewModel vm) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = isA ? "Masterul setului A" : "Masterul setului B",
                // Only a starting point. Set A is often in the open session, but set B is
                // by definition another night, so another folder - and after the first
                // choice the dialog remembers by itself where it was.
                InitialDirectory = isA ? vm.InitialDirectory : null,
                // The pattern comes from SirilSession, not written a second time here: the
                // dialog must offer exactly what the validation accepts, otherwise the user
                // picks from the list something refused right after. The filter still
                // guarantees nothing - one can always switch to "Toate fisierele" - so the
                // real check stays in the view-model.
                Filter = $"Master de flat ({SirilSession.RatioPattern})|{SirilSession.RatioPattern}"
                         + "|Toate fisierele|*.*",
                CheckFileExists = true,
            };

            if (dlg.ShowDialog() == true) vm.Choose(isA, dlg.FileName);
        }
    }
}
