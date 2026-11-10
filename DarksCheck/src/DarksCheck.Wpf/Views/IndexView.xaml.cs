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

namespace DarksCheck.Wpf;

/// <summary>
/// The Index tab; its three buttons reach the main window's handlers, which own the dialogs and
/// the clipboard. A choice of partner runs the folder again by itself (ADR 0048).
/// </summary>
public partial class IndexView : UserControl
{
    /// <summary>The tab, its bindings resolved against the main view-model it inherits.</summary>
    public IndexView() => InitializeComponent();

    private MainWindow? Owner => Window.GetWindow(this) as MainWindow;

    /// <summary>The copy button: the window puts the label in the clipboard.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void CopyLabel(object sender, RoutedEventArgs e) => Owner?.CopyLabelFromView();

    /// <summary>The partner button: the window asks for the partner folder, then the folder runs again.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void ChoosePartner(object sender, RoutedEventArgs e) => Owner?.ChoosePartnerFromView();

    /// <summary>The clear button: the window forgets the partner folder, then the folder runs again.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void ClearPartner(object sender, RoutedEventArgs e) => Owner?.ClearPartnerFromView();
}
