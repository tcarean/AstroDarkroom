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

namespace MindfulnessReminder.Core;

/// <summary>
/// Settings in HKCU, under the same key the auto-start marker already uses -- one key for
/// everything this app remembers.
///
/// Both values are written as strings even though the interval is a number, so that
/// <see cref="IRegistryStore"/> stays the four methods it already had; a DWORD would have
/// meant a wider interface for the sake of one value. Reading is deliberately forgiving: a
/// missing value is a fresh machine and a corrupt one is a hand-edited registry, and neither
/// is a reason to fail to start. Both land on the defaults.
/// </summary>
public sealed class RegistrySettingsStore : ISettingsStore
{
    /// <summary>The key <see cref="StartupRegistrar"/> keeps its marker under.</summary>
    public const string KeyPath = StartupRegistrar.SettingsKeyPath;

    public const string EnabledValueName = "PeriodicRemindersEnabled";

    public const string IntervalValueName = "PeriodicIntervalMinutes";

    public const string PauseMicrophoneValueName = "PauseWhileMicrophoneInUse";

    public const string PauseFullScreenValueName = "PauseWhileFullScreen";

    public const string RetryValueName = "PauseRetrySeconds";

    private readonly IRegistryStore _registry;

    public RegistrySettingsStore(IRegistryStore registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    public ReminderSettings Load()
    {
        var defaults = ReminderSettings.Default;

        var enabled = ReadFlag(EnabledValueName, defaults.PeriodicEnabled);
        var minutes = ReadNumber(IntervalValueName, ReminderSettings.DefaultIntervalMinutes);

        // Normalize rather than reject: an out-of-range value still says what was wanted, and
        // clamping is what stops a hand-edited 0 from becoming a reminder on every tick.
        return new ReminderSettings(enabled, minutes)
        {
            PauseWhileMicrophoneInUse =
                ReadFlag(PauseMicrophoneValueName, defaults.PauseWhileMicrophoneInUse),
            PauseWhileFullScreen =
                ReadFlag(PauseFullScreenValueName, defaults.PauseWhileFullScreen),
            RetrySeconds = ReadNumber(RetryValueName, ReminderSettings.DefaultRetrySeconds),
        }.Normalize();
    }

    public void Save(ReminderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Normalize();

        WriteFlag(EnabledValueName, normalized.PeriodicEnabled);
        WriteNumber(IntervalValueName, normalized.IntervalMinutes);
        WriteFlag(PauseMicrophoneValueName, normalized.PauseWhileMicrophoneInUse);
        WriteFlag(PauseFullScreenValueName, normalized.PauseWhileFullScreen);
        WriteNumber(RetryValueName, normalized.RetrySeconds);
    }

    /// <summary>
    /// Absent on a first run, so the default stands. Anything other than the two strings
    /// Save writes came from somewhere else, and the default is the safer reading of it.
    /// </summary>
    private bool ReadFlag(string valueName, bool fallback)
        => _registry.GetString(KeyPath, valueName) switch
        {
            "0" => false,
            "1" => true,
            _ => fallback,
        };

    private int ReadNumber(string valueName, int fallback)
        => int.TryParse(
            _registry.GetString(KeyPath, valueName),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;

    private void WriteFlag(string valueName, bool value)
        => _registry.SetString(KeyPath, valueName, value ? "1" : "0");

    private void WriteNumber(string valueName, int value)
        => _registry.SetString(KeyPath, valueName, value.ToString(CultureInfo.InvariantCulture));
}
