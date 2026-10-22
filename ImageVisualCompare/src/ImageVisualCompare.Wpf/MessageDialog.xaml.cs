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
    /// <summary>
    /// Builds an empty dialog with a dark caption, for <see cref="Create"/> to fill.
    /// </summary>
    private MessageDialog()
    {
        InitializeComponent();

        // Before the window is shown, so the caption is never white for a frame.
        DarkTitleBar.Apply(this);
    }

    /// <summary>Says something and waits for it to be acknowledged.</summary>
    /// <param name="owner">The window the dialog is centred on and blocks until it closes.</param>
    /// <param name="title">The dialog's caption.</param>
    /// <param name="message">The text; it may run over several lines.</param>
    public static void Show(Window owner, string title, string message)
    {
        MessageDialog dialog = Create(owner, title, message);
        dialog.PrimaryButton.Content = "OK";
        dialog.ShowDialog();
    }

    /// <summary>Asks a yes or no question and waits for the answer.</summary>
    /// <param name="owner">The window the dialog is centred on and blocks until it closes.</param>
    /// <param name="title">The dialog's caption.</param>
    /// <param name="message">The question; it may run over several lines.</param>
    /// <returns>True for yes; false for no, Escape or the close button.</returns>
    public static bool Confirm(Window owner, string title, string message)
    {
        MessageDialog dialog = Create(owner, title, message);
        dialog.PrimaryButton.Content = "Yes";
        dialog.SecondaryButton.Content = "No";
        dialog.SecondaryButton.Visibility = Visibility.Visible;

        return dialog.ShowDialog() == true;
    }

    /// <summary>Answers no and closes the dialog when Escape is pressed.</summary>
    /// <param name="e">The key; marked handled when it is Escape.</param>
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

    /// <summary>
    /// Builds a dialog with its owner, caption and text; its first button closes it with true,
    /// the second with false.
    /// </summary>
    /// <param name="owner">The window the dialog is centred on, as its owner.</param>
    /// <param name="title">The dialog's caption.</param>
    /// <param name="message">The text; it may run over several lines.</param>
    /// <returns>The dialog, not shown yet; the caller labels its buttons.</returns>
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
