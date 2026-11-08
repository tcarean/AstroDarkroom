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
/// The Lights tab: the lights of another folder (R-122), check 9, the report's rows, the
/// lights-over-dark ratio per light and the lights table; the code-behind loads the XAML and
/// hands the card's buttons to the window, which owns the dialogs.
/// </summary>
public partial class LightsView : UserControl
{
    /// <summary>The tab, its bindings resolved against the main view-model it inherits.</summary>
    public LightsView() => InitializeComponent();

    private MainWindow? Owner => Window.GetWindow(this) as MainWindow;

    /// <summary>The lights button: the window asks for the folder of lights.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void ChooseLights(object sender, RoutedEventArgs e) => Owner?.ChooseLightsFromView();

    /// <summary>The clear button: the window goes back to the open folder's lights.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void ClearLights(object sender, RoutedEventArgs e) => Owner?.ClearLightsFromView();

    /// <summary>The rerun button: the window runs the same folder again.</summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The click.</param>
    private void Rerun(object sender, RoutedEventArgs e) => Owner?.RerunFromView();
}
