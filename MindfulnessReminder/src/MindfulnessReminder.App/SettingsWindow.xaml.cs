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
    /// <summary>
    /// A Settings window filled in from <paramref name="current"/>, not yet shown.
    /// </summary>
    /// <param name="current">The settings in force, which the boxes start from.</param>
    public SettingsWindow(ReminderSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);

        InitializeComponent();

        UnlockCheck.IsChecked = current.UnlockEnabled;
        UnlockMessageBox.Text = current.UnlockMessage;

        EnabledCheck.IsChecked = current.PeriodicEnabled;
        IntervalBox.Text = current.IntervalMinutes.ToString(CultureInfo.InvariantCulture);
        PeriodicMessageBox.Text = current.PeriodicMessage;

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
    /// <param name="e">Passed on to the base class.</param>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        ImmersiveDarkMode.Apply(this);
    }

    /// <summary>
    /// Reads every box and, when what is needed is usable, raises <see cref="Accepted"/> with
    /// the settings, normalized, and closes. Otherwise says what is wrong and stays open.
    /// </summary>
    /// <param name="sender">The Save button; not read.</param>
    /// <param name="e">The click; not read.</param>
    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var unlockEnabled = UnlockCheck.IsChecked == true;
        var enabled = EnabledCheck.IsChecked == true;
        var pauseForMicrophone = PauseMicrophoneCheck.IsChecked == true;
        var pauseForFullScreen = PauseFullScreenCheck.IsChecked == true;

        // The messages follow the rule the numbers do, below: only a reminder that is
        // switched on needs a usable one. A blank box with its reminder switched off falls
        // back to the default text rather than standing in the way of the save.
        if (!TryReadMessage(UnlockMessageBox, out var unlockMessage))
        {
            if (unlockEnabled)
            {
                Refuse(MessageComplaint, UnlockMessageBox);
                return;
            }

            unlockMessage = ReminderSettings.DefaultMessage;
        }

        if (!TryReadMessage(PeriodicMessageBox, out var periodicMessage))
        {
            if (enabled)
            {
                Refuse(MessageComplaint, PeriodicMessageBox);
                return;
            }

            periodicMessage = ReminderSettings.DefaultMessage;
        }

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
            UnlockEnabled = unlockEnabled,
            UnlockMessage = unlockMessage,
            PeriodicMessage = periodicMessage,
            PauseWhileMicrophoneInUse = pauseForMicrophone,
            PauseWhileFullScreen = pauseForFullScreen,
            RetrySeconds = seconds,
        };

        Accepted?.Invoke(this, settings.Normalize());
        Close();
    }

    /// <summary>Closes the window without saving anything.</summary>
    /// <param name="sender">The Cancel button; not read.</param>
    /// <param name="e">The click; not read.</param>
    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// The retry gap is shared by the two pauses, so it stays available while either is on.
    /// An OR of two checkboxes is not something a plain binding can express, and a
    /// MultiBinding would mean a converter class for one line of logic.
    /// </summary>
    /// <param name="sender">The pause checkbox that changed; not read.</param>
    /// <param name="e">The change; not read.</param>
    private void OnPauseToggled(object sender, RoutedEventArgs e) => SyncRetryAvailability();

    /// <summary>
    /// Enables the retry gap's row while either pause is ticked, and greys it out otherwise.
    /// </summary>
    private void SyncRetryAvailability()
        => RetryPanel.IsEnabled =
            PauseMicrophoneCheck.IsChecked == true || PauseFullScreenCheck.IsChecked == true;

    private static string MessageComplaint
        => "Enter what the reminder should say, up to "
           + ReminderSettings.MaxMessageLength
           + " characters.";

    /// <summary>
    /// Reads a message box. Trimming and the rest are Normalize's job on the way out. Only
    /// whether it is acceptable is decided here, because that is the part with a user to
    /// tell.
    /// </summary>
    /// <param name="box">The message box to read.</param>
    /// <param name="message">The text as typed, acceptable or not.</param>
    /// <returns><c>true</c> when the dialog may save the text.</returns>
    private static bool TryReadMessage(TextBox box, out string message)
    {
        message = box.Text;
        return ReminderSettings.IsMessageAcceptable(message);
    }

    /// <summary>Reads the interval box.</summary>
    /// <param name="minutes">
    /// The number typed, when it is a whole number, in range or not; 0 when it is not.
    /// </param>
    /// <returns><c>true</c> when the box holds a whole number of minutes in range.</returns>
    private bool TryReadInterval(out int minutes)
        => int.TryParse(
               IntervalBox.Text.Trim(),
               NumberStyles.Integer,
               CultureInfo.InvariantCulture,
               out minutes)
           && ReminderSettings.IsIntervalInRange(minutes);

    /// <summary>Reads the retry box.</summary>
    /// <param name="seconds">
    /// The number typed, when it is a whole number, in range or not; 0 when it is not.
    /// </param>
    /// <returns><c>true</c> when the box holds a whole number of seconds in range.</returns>
    private bool TryReadRetry(out int seconds)
        => int.TryParse(
               RetryBox.Text.Trim(),
               NumberStyles.Integer,
               CultureInfo.InvariantCulture,
               out seconds)
           && ReminderSettings.IsRetryInRange(seconds);

    /// <summary>Says what is wrong and puts the cursor where it can be fixed.</summary>
    /// <param name="message">What is wrong, as the window shows it.</param>
    /// <param name="box">The box at fault; it gets the focus, with its text selected.</param>
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
    /// <param name="sender">The interval box or the retry box; not read.</param>
    /// <param name="e">
    /// The text being typed; marked handled, and so dropped, when it holds a non-digit.
    /// </param>
    private void OnIntervalPreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(char.IsAsciiDigit);

    /// <summary>Shared by every box: any edit clears the complaint about any of them.</summary>
    /// <param name="sender">The box that changed; not read.</param>
    /// <param name="e">The change; not read.</param>
    private void OnAnyTextChanged(object sender, TextChangedEventArgs e) => ClearError();

    /// <summary>Shows a complaint in the window's error line.</summary>
    /// <param name="message">What is wrong.</param>
    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    /// <summary>Hides the complaint, if one is showing.</summary>
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
