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

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MindfulnessReminder.Core;
using TextCompositionEventArgs = System.Windows.Input.TextCompositionEventArgs;

namespace MindfulnessReminder.App;

/// <summary>
/// The Settings window, opened from the tray menu.
///
/// It edits a <see cref="ReminderSettings"/> and hands it back through
/// <see cref="Accepted"/> rather than exposing a result property, because it is modeless:
/// there is no ShowDialog call to return to. Saving is refused, with the reason spelled out
/// in the window, rather than silently clamping a number the user typed -- clamping happens
/// on the way out of the registry, where there is nobody to tell.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(ReminderSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);

        InitializeComponent();

        EnabledCheck.IsChecked = current.PeriodicEnabled;
        IntervalBox.Text = current.IntervalMinutes.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Raised with the edited settings when Save succeeds. Never raised on Cancel.</summary>
    public event EventHandler<ReminderSettings>? Accepted;

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var enabled = EnabledCheck.IsChecked == true;

        if (!TryReadInterval(out var minutes))
        {
            // Only an enabled feature needs a usable number. Switching the feature off with
            // rubbish left in the box would otherwise be impossible, which is the one moment
            // the user is least interested in the interval.
            if (enabled)
            {
                ShowError(
                    "Enter a whole number of minutes between "
                    + ReminderSettings.MinIntervalMinutes
                    + " and "
                    + ReminderSettings.MaxIntervalMinutes
                    + ".");

                IntervalBox.Focus();
                IntervalBox.SelectAll();
                return;
            }

            minutes = ReminderSettings.DefaultIntervalMinutes;
        }

        Accepted?.Invoke(this, new ReminderSettings(enabled, minutes).Normalize());
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private bool TryReadInterval(out int minutes)
        => int.TryParse(
               IntervalBox.Text.Trim(),
               NumberStyles.Integer,
               CultureInfo.InvariantCulture,
               out minutes)
           && ReminderSettings.IsIntervalInRange(minutes);

    /// <summary>
    /// Keeps non-digits out of the box. This is a convenience, not the validation: a paste
    /// goes around it, which is why Save parses the text again rather than trusting this.
    /// </summary>
    private void OnIntervalPreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(char.IsAsciiDigit);

    private void OnIntervalTextChanged(object sender, TextChangedEventArgs e) => ClearError();

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void ClearError()
    {
        // ErrorText is null while the constructor is still running InitializeComponent and
        // the initial Text assignment fires this handler.
        if (ErrorText is not null)
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }
}
