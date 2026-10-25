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
    /// <summary>
    /// The Split tab (ADR 0054): the choice of parts and script, the run with Siril's log, then
    /// the table of parts and their maps, bound to <see cref="SplitTabViewModel"/>.
    /// </summary>
    public partial class SplitView : UserControl
    {
        /// <summary>Loads the XAML.</summary>
        public SplitView() => InitializeComponent();

        /// <summary>Keeps Siril's log on its last line as it grows.</summary>
        /// <param name="sender">The log's text box.</param>
        /// <param name="e">The change.</param>
        void ScrollToEnd(object sender, TextChangedEventArgs e) => ((TextBox)sender).ScrollToEnd();

        /// <summary>
        /// The table's horizontal ScrollViewer catches the wheel even when it has nothing to
        /// scroll vertically, and the page stays put with the mouse over the table. The wheel
        /// is passed on to the parent, as a new event.
        /// </summary>
        /// <param name="sender">The table's horizontal scroll viewer.</param>
        /// <param name="e">The wheel; marked handled once passed on.</param>
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
