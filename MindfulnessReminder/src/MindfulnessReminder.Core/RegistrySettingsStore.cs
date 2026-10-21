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

    public const string UnlockEnabledValueName = "UnlockReminderEnabled";

    public const string UnlockMessageValueName = "UnlockMessage";

    public const string PeriodicMessageValueName = "PeriodicMessage";

    private readonly IRegistryStore _registry;

    /// <summary>A store that keeps the settings under <see cref="KeyPath"/>.</summary>
    /// <param name="registry">The per-user registry to read and write.</param>
    public RegistrySettingsStore(IRegistryStore registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc/>
    public ReminderSettings Load()
    {
        try
        {
            return Read();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // A key this account may not read (a policy, security software) is no more a
            // reason to fail to start than a missing one: the defaults stand.
            return ReminderSettings.Default;
        }
    }

    /// <summary>
    /// Reads the eight values, each falling back to its default when absent or unparseable,
    /// and normalizes the result. A registry error is left for <see cref="Load"/> to catch.
    /// </summary>
    /// <returns>The settings as stored, normalized.</returns>
    private ReminderSettings Read()
    {
        var defaults = ReminderSettings.Default;

        var enabled = ReadFlag(EnabledValueName, defaults.PeriodicEnabled);
        var minutes = ReadNumber(IntervalValueName, ReminderSettings.DefaultIntervalMinutes);

        // Normalize rather than reject: an out-of-range value still says what was wanted, and
        // clamping is what stops a hand-edited 0 from becoming a reminder on every tick. The
        // messages go through the same door: a missing one is a build that never wrote it,
        // an empty one is a wiped value, and both land on the default text.
        return new ReminderSettings(enabled, minutes)
        {
            UnlockEnabled = ReadFlag(UnlockEnabledValueName, defaults.UnlockEnabled),
            UnlockMessage = ReadText(UnlockMessageValueName, defaults.UnlockMessage),
            PeriodicMessage = ReadText(PeriodicMessageValueName, defaults.PeriodicMessage),
            PauseWhileMicrophoneInUse =
                ReadFlag(PauseMicrophoneValueName, defaults.PauseWhileMicrophoneInUse),
            PauseWhileFullScreen =
                ReadFlag(PauseFullScreenValueName, defaults.PauseWhileFullScreen),
            RetrySeconds = ReadNumber(RetryValueName, ReminderSettings.DefaultRetrySeconds),
        }.Normalize();
    }

    /// <inheritdoc/>
    public void Save(ReminderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Normalize();

        WriteFlag(EnabledValueName, normalized.PeriodicEnabled);
        WriteNumber(IntervalValueName, normalized.IntervalMinutes);
        WriteFlag(UnlockEnabledValueName, normalized.UnlockEnabled);
        _registry.SetString(KeyPath, UnlockMessageValueName, normalized.UnlockMessage);
        _registry.SetString(KeyPath, PeriodicMessageValueName, normalized.PeriodicMessage);
        WriteFlag(PauseMicrophoneValueName, normalized.PauseWhileMicrophoneInUse);
        WriteFlag(PauseFullScreenValueName, normalized.PauseWhileFullScreen);
        WriteNumber(RetryValueName, normalized.RetrySeconds);
    }

    /// <summary>
    /// Reads a switch, stored as "0" or "1".
    ///
    /// Absent on a first run, so the default stands. Anything other than the two strings
    /// Save writes came from somewhere else, and the default is the safer reading of it.
    /// </summary>
    /// <param name="valueName">The value under <see cref="KeyPath"/>.</param>
    /// <param name="fallback">What an absent or unrecognised value reads as.</param>
    /// <returns>
    /// <c>false</c> for "0", <c>true</c> for "1", and <paramref name="fallback"/> for anything
    /// else.
    /// </returns>
    private bool ReadFlag(string valueName, bool fallback)
        => _registry.GetString(KeyPath, valueName) switch
        {
            "0" => false,
            "1" => true,
            _ => fallback,
        };

    /// <summary>
    /// Reads a message, as stored.
    ///
    /// Only absence falls back here. What the value SAYS is left to
    /// <see cref="ReminderSettings.NormalizeMessage"/>, which is where "empty means the
    /// default" and the length cap live, so that the rule is written once for the registry
    /// and the dialog alike.
    /// </summary>
    /// <param name="valueName">The value under <see cref="KeyPath"/>.</param>
    /// <param name="fallback">What an absent value reads as.</param>
    /// <returns>
    /// The stored text unchanged, or <paramref name="fallback"/> when the value is absent.
    /// </returns>
    private string ReadText(string valueName, string fallback)
        => _registry.GetString(KeyPath, valueName) ?? fallback;

    /// <summary>
    /// Reads a whole number, stored as an invariant-culture string. Its range is left to
    /// <see cref="ReminderSettings.Normalize"/>.
    /// </summary>
    /// <param name="valueName">The value under <see cref="KeyPath"/>.</param>
    /// <param name="fallback">What an absent or unparseable value reads as.</param>
    /// <returns>The number as stored, or <paramref name="fallback"/>.</returns>
    private int ReadNumber(string valueName, int fallback)
        => int.TryParse(
            _registry.GetString(KeyPath, valueName),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;

    /// <summary>Writes a switch as "1" or "0".</summary>
    /// <param name="valueName">The value under <see cref="KeyPath"/>.</param>
    /// <param name="value">The switch to store.</param>
    private void WriteFlag(string valueName, bool value)
        => _registry.SetString(KeyPath, valueName, value ? "1" : "0");

    /// <summary>Writes a whole number as an invariant-culture string.</summary>
    /// <param name="valueName">The value under <see cref="KeyPath"/>.</param>
    /// <param name="value">The number to store.</param>
    private void WriteNumber(string valueName, int value)
        => _registry.SetString(KeyPath, valueName, value.ToString(CultureInfo.InvariantCulture));
}
