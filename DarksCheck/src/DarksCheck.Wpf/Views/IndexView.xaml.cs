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

/// <summary>The Index tab; its three buttons reach the main window's handlers, which own the dialogs and the clipboard.</summary>
public partial class IndexView : UserControl
{
    public IndexView() => InitializeComponent();

    private MainWindow? Owner => Window.GetWindow(this) as MainWindow;

    private void CopyLabel(object sender, RoutedEventArgs e) => Owner?.CopyLabelFromView();

    private void ChoosePartner(object sender, RoutedEventArgs e) => Owner?.ChoosePartnerFromView();

    private void ClearPartner(object sender, RoutedEventArgs e) => Owner?.ClearPartnerFromView();

    private void Rerun(object sender, RoutedEventArgs e) => Owner?.RerunFromView();
}
