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

// ReportPanel.xaml.cs - only the link to the XAML; the behaviour is in the view-model.

using System.Windows.Controls;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The folded raw report of a tab, the text the console would print, selectable to copy,
    /// bound to the tab's <c>Report</c>.
    /// </summary>
    public partial class ReportPanel : UserControl
    {
        /// <summary>Loads the XAML.</summary>
        public ReportPanel() => InitializeComponent();
    }
}
