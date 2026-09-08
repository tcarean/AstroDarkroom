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
using TextBox = System.Windows.Controls.TextBox;
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

        PauseMicrophoneCheck.IsChecked = current.PauseWhileMicrophoneInUse;
        PauseFullScreenCheck.IsChecked = current.PauseWhileFullScreen;
        RetryBox.Text = current.RetrySeconds.ToString(CultureInfo.InvariantCulture);

        SyncRetryAvailability();
    }

    /// <summary>Raised with the edited settings when Save succeeds. Never raised on Cancel.</summary>
    public event EventHandler<ReminderSettings>? Accepted;

    /// <summary>
    /// The title bar is the one part of this window WPF does not paint, so the dark theme
    /// merged in the XAML stops at the client area and the caption has to be asked for
    /// separately. This is the first moment there is an HWND to ask about, and it is still
    /// before the first paint, so the caption is never seen light.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        ImmersiveDarkMode.Apply(this);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var enabled = EnabledCheck.IsChecked == true;
        var pauseForMicrophone = PauseMicrophoneCheck.IsChecked == true;
        var pauseForFullScreen = PauseFullScreenCheck.IsChecked == true;

        if (!TryReadInterval(out var minutes))
        {
            // Only an enabled feature needs a usable number. Switching the feature off with
            // rubbish left in the box would otherwise be impossible, which is the one moment
            // the user is least interested in the interval.
            if (enabled)
            {
                Refuse(
                    "Enter a whole number of minutes between "
                    + ReminderSettings.MinIntervalMinutes
                    + " and "
                    + ReminderSettings.MaxIntervalMinutes
                    + ".",
                    IntervalBox);

                return;
            }

            minutes = ReminderSettings.DefaultIntervalMinutes;
        }

        if (!TryReadRetry(out var seconds))
        {
            // Same rule as the interval, for the same reason: the number only has to be
            // usable if something is going to use it.
            if (pauseForMicrophone || pauseForFullScreen)
            {
                Refuse(
                    "Enter a whole number of seconds between "
                    + ReminderSettings.MinRetrySeconds
                    + " and "
                    + ReminderSettings.MaxRetrySeconds
                    + ".",
                    RetryBox);

                return;
            }

            seconds = ReminderSettings.DefaultRetrySeconds;
        }

        var settings = new ReminderSettings(enabled, minutes)
        {
            PauseWhileMicrophoneInUse = pauseForMicrophone,
            PauseWhileFullScreen = pauseForFullScreen,
            RetrySeconds = seconds,
        };

        Accepted?.Invoke(this, settings.Normalize());
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// The retry gap is shared by the two pauses, so it stays available while either is on.
    /// An OR of two checkboxes is not something a plain binding can express, and a
    /// MultiBinding would mean a converter class for one line of logic.
    /// </summary>
    private void OnPauseToggled(object sender, RoutedEventArgs e) => SyncRetryAvailability();

    private void SyncRetryAvailability()
        => RetryPanel.IsEnabled =
            PauseMicrophoneCheck.IsChecked == true || PauseFullScreenCheck.IsChecked == true;

    private bool TryReadInterval(out int minutes)
        => int.TryParse(
               IntervalBox.Text.Trim(),
               NumberStyles.Integer,
               CultureInfo.InvariantCulture,
               out minutes)
           && ReminderSettings.IsIntervalInRange(minutes);

    private bool TryReadRetry(out int seconds)
        => int.TryParse(
               RetryBox.Text.Trim(),
               NumberStyles.Integer,
               CultureInfo.InvariantCulture,
               out seconds)
           && ReminderSettings.IsRetryInRange(seconds);

    /// <summary>Says what is wrong and puts the cursor where it can be fixed.</summary>
    private void Refuse(string message, TextBox box)
    {
        ShowError(message);
        box.Focus();
        box.SelectAll();
    }

    /// <summary>
    /// Keeps non-digits out of the box. This is a convenience, not the validation: a paste
    /// goes around it, which is why Save parses the text again rather than trusting this.
    /// </summary>
    private void OnIntervalPreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(char.IsAsciiDigit);

    /// <summary>Shared by both boxes: any edit clears the complaint about either.</summary>
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
