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
}
