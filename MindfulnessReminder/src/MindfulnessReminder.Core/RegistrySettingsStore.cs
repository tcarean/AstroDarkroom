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

    private readonly IRegistryStore _registry;

    public RegistrySettingsStore(IRegistryStore registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    public ReminderSettings Load()
    {
        var enabled = _registry.GetString(KeyPath, EnabledValueName) switch
        {
            "0" => false,
            "1" => true,

            // Absent on a first run, and the feature ships switched on. Anything else was
            // written by someone other than Save, and the default is the safer reading.
            _ => ReminderSettings.Default.PeriodicEnabled,
        };

        var minutes = int.TryParse(
            _registry.GetString(KeyPath, IntervalValueName),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : ReminderSettings.DefaultIntervalMinutes;

        // Normalize rather than reject: an out-of-range value still says what was wanted, and
        // clamping is what stops a hand-edited 0 from becoming a reminder on every tick.
        return new ReminderSettings(enabled, minutes).Normalize();
    }

    public void Save(ReminderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Normalize();

        _registry.SetString(KeyPath, EnabledValueName, normalized.PeriodicEnabled ? "1" : "0");
        _registry.SetString(
            KeyPath,
            IntervalValueName,
            normalized.IntervalMinutes.ToString(CultureInfo.InvariantCulture));
    }
}
