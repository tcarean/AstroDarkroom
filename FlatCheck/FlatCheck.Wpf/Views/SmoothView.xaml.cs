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

using System.Windows.Controls;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The Netezire tab: the guide, the scale and the button that writes the smoothed master;
    /// after the run, what was removed and the structure tests before and after, bound to
    /// <see cref="SmoothTabViewModel"/>.
    /// </summary>
    public partial class SmoothView : UserControl
    {
        /// <summary>Loads the XAML.</summary>
        public SmoothView() => InitializeComponent();
    }
}
