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
using MindfulnessReminder.Core;

namespace MindfulnessReminder.Tests;

/// <summary>
/// The settings and where they are kept. Most of these are about reading a registry someone
/// else may have written: a fresh machine, a half-written key, a value edited by hand. None
/// of those may stop the app starting, and none may produce an interval the scheduler cannot
/// live with.
/// </summary>
public class ReminderSettingsStoreTests
{
    private readonly FakeRegistryStore _registry = new();

    private RegistrySettingsStore Store() => new(_registry);

    [Fact]
    public void The_defaults_are_on_and_thirty_minutes()
    {
        Assert.True(ReminderSettings.Default.PeriodicEnabled);
        Assert.Equal(30, ReminderSettings.Default.IntervalMinutes);
    }

    [Fact]
    public void An_empty_registry_gives_the_defaults()
    {
        Assert.Equal(ReminderSettings.Default, Store().Load());
    }

    [Theory]
    [InlineData(true, 15)]
    [InlineData(false, 30)]
    [InlineData(true, ReminderSettings.MinIntervalMinutes)]
    [InlineData(true, ReminderSettings.MaxIntervalMinutes)]
    public void Settings_round_trip(bool enabled, int minutes)
    {
        var settings = new ReminderSettings(enabled, minutes);

        Store().Save(settings);

        Assert.Equal(settings, Store().Load());
    }

    [Fact]
    public void The_settings_live_under_the_key_the_autostart_marker_already_uses()
    {
        Store().Save(new ReminderSettings(true, 15));

        Assert.Equal(StartupRegistrar.SettingsKeyPath, RegistrySettingsStore.KeyPath);
        Assert.True(_registry.Contains(
            StartupRegistrar.SettingsKeyPath, RegistrySettingsStore.IntervalValueName));
    }

    [Fact]
    public void Saving_the_settings_does_not_disturb_the_autostart_marker()
    {
        _registry.SetString(
            StartupRegistrar.SettingsKeyPath, StartupRegistrar.RegisteredMarkerValueName, "1");

        Store().Save(new ReminderSettings(false, 60));

        Assert.Equal(
            "1",
            _registry.GetString(
                StartupRegistrar.SettingsKeyPath, StartupRegistrar.RegisteredMarkerValueName));
    }

    [Fact]
    public void A_corrupt_interval_falls_back_to_the_default_rather_than_failing()
    {
        _registry.SetString(
            RegistrySettingsStore.KeyPath, RegistrySettingsStore.IntervalValueName, "half an hour");

        Assert.Equal(ReminderSettings.DefaultIntervalMinutes, Store().Load().IntervalMinutes);
    }

    [Fact]
    public void An_unrecognised_enabled_flag_falls_back_to_the_default()
    {
        _registry.SetString(
            RegistrySettingsStore.KeyPath, RegistrySettingsStore.EnabledValueName, "yes please");

        Assert.Equal(ReminderSettings.Default.PeriodicEnabled, Store().Load().PeriodicEnabled);
    }

    /// <summary>
    /// A hand-edited zero would otherwise mean a reminder on every tick of the timer.
    /// </summary>
    [Theory]
    [InlineData("0", ReminderSettings.MinIntervalMinutes)]
    [InlineData("-5", ReminderSettings.MinIntervalMinutes)]
    [InlineData("99999", ReminderSettings.MaxIntervalMinutes)]
    public void An_out_of_range_interval_in_the_registry_is_clamped(string stored, int expected)
    {
        _registry.SetString(
            RegistrySettingsStore.KeyPath, RegistrySettingsStore.IntervalValueName, stored);

        Assert.Equal(expected, Store().Load().IntervalMinutes);
    }

    [Fact]
    public void Saving_clamps_too_so_the_registry_never_holds_an_impossible_value()
    {
        Store().Save(new ReminderSettings(true, 0));

        Assert.Equal(
            ReminderSettings.MinIntervalMinutes.ToString(),
            _registry.GetString(
                RegistrySettingsStore.KeyPath, RegistrySettingsStore.IntervalValueName));
    }

    [Fact]
    public void The_enabled_flag_survives_a_disabled_save()
    {
        Store().Save(new ReminderSettings(false, 30));

        Assert.False(Store().Load().PeriodicEnabled);
    }

    [Theory]
    [InlineData(ReminderSettings.MinIntervalMinutes - 1, false)]
    [InlineData(ReminderSettings.MinIntervalMinutes, true)]
    [InlineData(ReminderSettings.MaxIntervalMinutes, true)]
    [InlineData(ReminderSettings.MaxIntervalMinutes + 1, false)]
    public void The_dialog_range_check_matches_the_clamp(int minutes, bool inRange)
    {
        Assert.Equal(inRange, ReminderSettings.IsIntervalInRange(minutes));
    }

    // ------------------------------------------------------------------- the two pauses

    [Fact]
    public void Both_pauses_and_the_retry_gap_survive_a_round_trip()
    {
        var store = new RegistrySettingsStore(_registry);

        var saved = new ReminderSettings(true, 45)
        {
            PauseWhileMicrophoneInUse = false,
            PauseWhileFullScreen = true,
            RetrySeconds = 90,
        };

        store.Save(saved);

        Assert.Equal(saved, store.Load());
    }

    /// <summary>An upgrade from a version that never wrote them: both on, 20 seconds.</summary>
    [Fact]
    public void A_registry_without_the_pause_values_lands_on_the_defaults()
    {
        var store = new RegistrySettingsStore(_registry);
        store.Save(new ReminderSettings(true, 30));

        _registry.DeleteValue(RegistrySettingsStore.KeyPath, RegistrySettingsStore.PauseMicrophoneValueName);
        _registry.DeleteValue(RegistrySettingsStore.KeyPath, RegistrySettingsStore.PauseFullScreenValueName);
        _registry.DeleteValue(RegistrySettingsStore.KeyPath, RegistrySettingsStore.RetryValueName);

        var loaded = store.Load();

        Assert.True(loaded.PauseWhileMicrophoneInUse);
        Assert.True(loaded.PauseWhileFullScreen);
        Assert.Equal(ReminderSettings.DefaultRetrySeconds, loaded.RetrySeconds);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("")]
    [InlineData("2")]
    public void A_hand_edited_pause_flag_falls_back_to_the_default(string written)
    {
        _registry.SetString(
            RegistrySettingsStore.KeyPath,
            RegistrySettingsStore.PauseMicrophoneValueName,
            written);

        Assert.True(new RegistrySettingsStore(_registry).Load().PauseWhileMicrophoneInUse);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("")]
    public void A_corrupt_retry_gap_falls_back_to_the_default(string written)
    {
        _registry.SetString(
            RegistrySettingsStore.KeyPath,
            RegistrySettingsStore.RetryValueName,
            written);

        Assert.Equal(
            ReminderSettings.DefaultRetrySeconds,
            new RegistrySettingsStore(_registry).Load().RetrySeconds);
    }

    /// <summary>
    /// Clamped, not rejected -- the same rule the interval follows. A hand-edited 0 would
    /// otherwise read the registry on every tick of the timer.
    /// </summary>
    [Theory]
    [InlineData("0", ReminderSettings.MinRetrySeconds)]
    [InlineData("1", ReminderSettings.MinRetrySeconds)]
    [InlineData("-40", ReminderSettings.MinRetrySeconds)]
    [InlineData("99999", ReminderSettings.MaxRetrySeconds)]
    public void An_out_of_range_retry_gap_is_clamped(string written, int expected)
    {
        _registry.SetString(
            RegistrySettingsStore.KeyPath,
            RegistrySettingsStore.RetryValueName,
            written);

        Assert.Equal(expected, new RegistrySettingsStore(_registry).Load().RetrySeconds);
    }

    [Fact]
    public void Saving_clamps_the_retry_gap_before_it_reaches_the_registry()
    {
        var store = new RegistrySettingsStore(_registry);

        store.Save(new ReminderSettings(true, 30) { RetrySeconds = 9999 });

        Assert.Equal(
            ReminderSettings.MaxRetrySeconds.ToString(CultureInfo.InvariantCulture),
            _registry.GetString(RegistrySettingsStore.KeyPath, RegistrySettingsStore.RetryValueName));
    }

    // ------------------------------------------------- the unlock switch and the two messages

    [Fact]
    public void The_defaults_have_the_unlock_reminder_on_and_the_same_message_on_both()
    {
        Assert.True(ReminderSettings.Default.UnlockEnabled);
        Assert.Equal(ReminderSettings.DefaultMessage, ReminderSettings.Default.UnlockMessage);
        Assert.Equal(ReminderSettings.DefaultMessage, ReminderSettings.Default.PeriodicMessage);
    }

    [Fact]
    public void The_unlock_switch_and_both_messages_survive_a_round_trip()
    {
        var saved = new ReminderSettings(true, 30)
        {
            UnlockEnabled = false,
            UnlockMessage = "Welcome back",
            PeriodicMessage = "Look away from the screen",
        };

        Store().Save(saved);

        Assert.Equal(saved, Store().Load());
    }

    /// <summary>An upgrade from a build that never wrote them: on, and the old text on both.</summary>
    [Fact]
    public void A_registry_without_the_unlock_values_lands_on_the_defaults()
    {
        Store().Save(new ReminderSettings(true, 30)
        {
            UnlockEnabled = false,
            UnlockMessage = "a",
            PeriodicMessage = "b",
        });

        _registry.DeleteValue(RegistrySettingsStore.KeyPath, RegistrySettingsStore.UnlockEnabledValueName);
        _registry.DeleteValue(RegistrySettingsStore.KeyPath, RegistrySettingsStore.UnlockMessageValueName);
        _registry.DeleteValue(RegistrySettingsStore.KeyPath, RegistrySettingsStore.PeriodicMessageValueName);

        var loaded = Store().Load();

        Assert.True(loaded.UnlockEnabled);
        Assert.Equal(ReminderSettings.DefaultMessage, loaded.UnlockMessage);
        Assert.Equal(ReminderSettings.DefaultMessage, loaded.PeriodicMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n")]
    public void A_blank_message_in_the_registry_reads_as_the_default(string written)
    {
        _registry.SetString(
            RegistrySettingsStore.KeyPath, RegistrySettingsStore.PeriodicMessageValueName, written);

        Assert.Equal(ReminderSettings.DefaultMessage, Store().Load().PeriodicMessage);
    }

    /// <summary>Cut, not rejected: a hand-edited message cannot push the text into the Close button.</summary>
    [Fact]
    public void An_over_long_message_in_the_registry_is_cut_at_the_limit()
    {
        _registry.SetString(
            RegistrySettingsStore.KeyPath,
            RegistrySettingsStore.UnlockMessageValueName,
            new string('x', ReminderSettings.MaxMessageLength + 20));

        Assert.Equal(
            new string('x', ReminderSettings.MaxMessageLength),
            Store().Load().UnlockMessage);
    }

    [Fact]
    public void Saving_normalizes_the_messages_before_they_reach_the_registry()
    {
        Store().Save(new ReminderSettings(true, 30)
        {
            UnlockMessage = "  Breathe\r\nout  ",
            PeriodicMessage = " ",
        });

        Assert.Equal(
            "Breathe  out",
            _registry.GetString(RegistrySettingsStore.KeyPath, RegistrySettingsStore.UnlockMessageValueName));
        Assert.Equal(
            ReminderSettings.DefaultMessage,
            _registry.GetString(RegistrySettingsStore.KeyPath, RegistrySettingsStore.PeriodicMessageValueName));
    }

    [Theory]
    [InlineData("Ground yourself", "Ground yourself")]
    [InlineData("  Breathe  ", "Breathe")]
    [InlineData("one\ttwo", "one two")]
    [InlineData(null, "Ground yourself")]
    public void Message_normalization_keeps_what_it_can(string? written, string expected)
    {
        Assert.Equal(expected, ReminderSettings.NormalizeMessage(written));
    }

    /// <summary>A message cut at the limit does not end in the space it was cut after.</summary>
    [Fact]
    public void A_message_cut_at_the_limit_is_trimmed_after_the_cut()
    {
        var written = new string('x', ReminderSettings.MaxMessageLength - 1) + " yyyy";

        Assert.Equal(
            new string('x', ReminderSettings.MaxMessageLength - 1),
            ReminderSettings.NormalizeMessage(written));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("x", true)]
    [InlineData("  x  ", true)]
    public void The_dialog_message_check_refuses_blank_and_accepts_text(string? message, bool acceptable)
    {
        Assert.Equal(acceptable, ReminderSettings.IsMessageAcceptable(message));
    }

    [Fact]
    public void The_dialog_message_check_matches_the_length_limit()
    {
        Assert.True(ReminderSettings.IsMessageAcceptable(new string('x', ReminderSettings.MaxMessageLength)));
        Assert.False(ReminderSettings.IsMessageAcceptable(new string('x', ReminderSettings.MaxMessageLength + 1)));
    }

    [Fact]
    public void The_unlock_switch_is_written_as_the_same_one_and_zero_strings_as_the_others()
    {
        Store().Save(new ReminderSettings(true, 30) { UnlockEnabled = false });

        Assert.Equal(
            "0",
            _registry.GetString(RegistrySettingsStore.KeyPath, RegistrySettingsStore.UnlockEnabledValueName));
    }

    /// <summary>Strings, not DWORDs, so IRegistryStore stays the four methods it had.</summary>
    [Fact]
    public void The_pause_flags_are_written_as_the_same_one_and_zero_strings_as_the_others()
    {
        var store = new RegistrySettingsStore(_registry);

        store.Save(new ReminderSettings(true, 30)
        {
            PauseWhileMicrophoneInUse = true,
            PauseWhileFullScreen = false,
        });

        Assert.Equal(
            "1",
            _registry.GetString(RegistrySettingsStore.KeyPath, RegistrySettingsStore.PauseMicrophoneValueName));
        Assert.Equal(
            "0",
            _registry.GetString(RegistrySettingsStore.KeyPath, RegistrySettingsStore.PauseFullScreenValueName));
    }
}
