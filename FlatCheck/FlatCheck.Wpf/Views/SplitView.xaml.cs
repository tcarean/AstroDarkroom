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

namespace FlatCheck.Wpf
{
    public partial class SplitView : UserControl
    {
        public SplitView() => InitializeComponent();

        void ScrollToEnd(object sender, TextChangedEventArgs e) => ((TextBox)sender).ScrollToEnd();

        /// <summary>
        /// ScrollViewer-ul orizontal al tabelului prinde rotita chiar daca n-are ce derula pe
        /// verticala, si pagina ramane pe loc cu mausul peste tabel. Rotita se trimite mai
        /// departe, parintelui, ca eveniment nou.
        /// </summary>
        void ForwardWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            e.Handled = true;
            var forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source = sender,
            };
            (((FrameworkElement)sender).Parent as UIElement)?.RaiseEvent(forwarded);
        }
    }
}
