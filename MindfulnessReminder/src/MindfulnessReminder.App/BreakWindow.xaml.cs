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
using MindfulnessReminder.Core;
using Key = System.Windows.Input.Key;
using Point = System.Windows.Point;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Screen = System.Windows.Forms.Screen;
using WinFormsControl = System.Windows.Forms.Control;

namespace MindfulnessReminder.App;

/// <summary>
/// The reminder window: a light card with the message and a Close button, topmost, centred on
/// the primary monitor's work area. The button, Esc and Space close it; Enter does not.
/// </summary>
public partial class BreakWindow : Window
{
    /// <summary>A reminder window that says <paramref name="message"/>, not yet shown.</summary>
    /// <param name="message">
    /// What the window says. It comes from the coordinator, which is the one thing that knows
    /// whether this is the unlock reminder or the periodic one; the window itself has no idea,
    /// and does not need one.
    /// </param>
    public BreakWindow(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        InitializeComponent();

        MessageText.Text = message;
    }

    /// <summary>
    /// Once the window has a handle and before it is first drawn: puts it on the primary
    /// monitor, brings it to the front and gives the focus to Close.
    /// </summary>
    /// <param name="e">Passed on to the base class.</param>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        MoveToPrimaryScreen();

        // Windows only grants foreground activation to a process with recent input, and a
        // freshly unlocked desktop has none. Re-asserting Topmost after the handle exists is
        // what reliably puts the window in front of whatever was left open before the lock.
        Topmost = true;
        Activate();
        CloseButton.Focus();
    }

    /// <summary>
    /// Puts the reminder on the primary monitor, always the same one.
    ///
    /// Following the mouse pointer instead sounds friendlier but is not reproducible: the
    /// pointer sits wherever it was left before the lock, or wherever the logon screen parked
    /// it, so the reminder surfaced on a different monitor each time.
    /// </summary>
    private void MoveToPrimaryScreen()
    {
        if (PresentationSource.FromVisual(this) is not { } source)
        {
            return;
        }

        // Screen reports physical pixels; WPF positions in device-independent units.
        var toDip = source.CompositionTarget.TransformFromDevice;

        // PrimaryScreen is null only if the desktop has no monitor at all; the pointer's
        // screen is then as good a guess as any and keeps the window on something visible.
        var screen = Screen.PrimaryScreen ?? Screen.FromPoint(WinFormsControl.MousePosition);
        var bounds = screen.WorkingArea;

        var origin = toDip.Transform(new Point(bounds.Left, bounds.Top));
        var size = toDip.Transform(new Point(bounds.Width, bounds.Height));

        var (left, top) = WindowPlacement.CenterIn(
            new WorkArea(origin.X, origin.Y, size.X, size.Y),
            Width,
            Height);

        Left = left;
        Top = top;
    }

    /// <summary>Closes the window.</summary>
    /// <param name="sender">The Close button; not read.</param>
    /// <param name="e">The click; not read.</param>
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Swallows Enter before anything can act on it.
    ///
    /// Since the reminder became periodic it arrives while the user is working, and it takes
    /// the focus when it does. Enter is the keystroke most likely to be already on its way,
    /// and closing on it would both dismiss a reminder nobody read and lose the keypress from
    /// whatever was being typed. It has to be Preview: KeyDown bubbles up from the focused
    /// Close button, which would already have handled the key by the time the window saw it.
    ///
    /// Esc and Space still close -- Esc deliberately, Space because a focused button responds
    /// to it. Both are far less likely mid-sentence than Enter.
    /// </summary>
    /// <param name="e">The key; Enter is marked handled, and every other key goes on.</param>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key is Key.Enter)
        {
            e.Handled = true;
        }
    }

    /// <summary>Closes the window on Esc.</summary>
    /// <param name="e">The key that was pressed.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
