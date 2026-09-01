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
using System.Windows.Media;
using MindfulnessReminder.Core;
using Key = System.Windows.Input.Key;
using Point = System.Windows.Point;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Screen = System.Windows.Forms.Screen;
using WinFormsControl = System.Windows.Forms.Control;

namespace MindfulnessReminder.App;

public partial class BreakWindow : Window
{
    public BreakWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        MoveToActiveScreen();

        // Windows only grants foreground activation to a process with recent input, and a
        // freshly unlocked desktop has none. Re-asserting Topmost after the handle exists is
        // what reliably puts the window in front of whatever was left open before the lock.
        Topmost = true;
        Activate();
        CloseButton.Focus();
    }

    /// <summary>
    /// Puts the reminder on the monitor holding the mouse pointer -- the best available guess
    /// at where the user is actually looking after unlocking.
    /// </summary>
    private void MoveToActiveScreen()
    {
        if (PresentationSource.FromVisual(this) is not { } source)
        {
            return;
        }

        // Screen reports physical pixels; WPF positions in device-independent units.
        var toDip = source.CompositionTarget.TransformFromDevice;
        var bounds = Screen.FromPoint(WinFormsControl.MousePosition).WorkingArea;

        var origin = toDip.Transform(new Point(bounds.Left, bounds.Top));
        var size = toDip.Transform(new Point(bounds.Width, bounds.Height));

        var (left, top) = WindowPlacement.CenterIn(
            new WorkArea(origin.X, origin.Y, size.X, size.Y),
            Width,
            Height);

        Left = left;
        Top = top;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
