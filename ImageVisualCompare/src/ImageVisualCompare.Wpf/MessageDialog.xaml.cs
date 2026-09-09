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
using System.Windows.Input;
using ImageVisualCompare.Wpf.Interop;

namespace ImageVisualCompare.Wpf;

/// <summary>
/// The application's own message box.
///
/// <c>MessageBox.Show</c> opens a Win32 window that cannot be themed at all — white
/// background, black text, native buttons — which on a tool that exists to judge dark
/// images is not a cosmetic complaint: it is a bright rectangle over the picture being
/// compared. This is a plain WPF window instead, so it takes the palette like everything
/// else, and it opts into the dark caption the same way the main window does.
/// </summary>
public partial class MessageDialog : Window
{
    private MessageDialog()
    {
        InitializeComponent();

        // Before the window is shown, so the caption is never white for a frame.
        DarkTitleBar.Apply(this);
    }

    /// <summary>Says something and waits for it to be acknowledged.</summary>
    public static void Show(Window owner, string title, string message)
    {
        MessageDialog dialog = Create(owner, title, message);
        dialog.PrimaryButton.Content = "OK";
        dialog.ShowDialog();
    }

    /// <summary>Asks a yes or no question. Escape and the close button both count as no.</summary>
    public static bool Confirm(Window owner, string title, string message)
    {
        MessageDialog dialog = Create(owner, title, message);
        dialog.PrimaryButton.Content = "Yes";
        dialog.SecondaryButton.Content = "No";
        dialog.SecondaryButton.Visibility = Visibility.Visible;

        return dialog.ShowDialog() == true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Escape rather than IsCancel on a button: the acknowledge-only dialog has no second
        // button to hang it on, and a button that is both the default and the cancel would
        // have two answers racing to close the window.
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private static MessageDialog Create(Window owner, string title, string message)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var dialog = new MessageDialog
        {
            Owner = owner,
            Title = title,
            MessageText = { Text = message },
        };

        dialog.PrimaryButton.Click += (_, _) => dialog.DialogResult = true;
        dialog.SecondaryButton.Click += (_, _) => dialog.DialogResult = false;
        return dialog;
    }
}
