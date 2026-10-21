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

namespace MindfulnessReminder.Core;

/// <summary>
/// Auto-start backed by HKCU\...\CurrentVersion\Run. No elevation is needed, and the
/// entry is one value that can be added or removed at will.
///
/// The Run key alone is not the whole truth: Task Manager's Startup tab does not delete
/// the value, it records a veto in Explorer\StartupApproved\Run. A checkbox that only
/// looked at the Run key would happily report "on" for an entry Windows is ignoring, so
/// <see cref="GetStatus"/> reads both.
/// </summary>
public sealed class RegistryAutoStartManager : IAutoStartManager
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string StartupApprovedKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private readonly IRegistryStore _registry;
    private readonly string _valueName;
    private readonly string _executablePath;

    /// <summary>
    /// A manager for the Run value <paramref name="valueName"/>, which starts
    /// <paramref name="executablePath"/>. Neither string may be blank.
    /// </summary>
    /// <param name="registry">The per-user registry to read and write.</param>
    /// <param name="valueName">
    /// The value's name under the Run key, which Task Manager also shows.
    /// </param>
    /// <param name="executablePath">
    /// The full path of the executable; written between quotes.
    /// </param>
    public RegistryAutoStartManager(IRegistryStore registry, string valueName, string executablePath)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        _registry = registry;
        _valueName = valueName;
        _executablePath = executablePath;
    }

    /// <summary>The exact string this manager writes to the Run key.</summary>
    public string Command => Quote(_executablePath);

    /// <inheritdoc/>
    public AutoStartStatus GetStatus()
    {
        var command = _registry.GetString(RunKeyPath, _valueName);
        if (string.IsNullOrWhiteSpace(command))
        {
            return AutoStartStatus.Disabled;
        }

        return IsApprovalRevoked() ? AutoStartStatus.BlockedByWindows : AutoStartStatus.Enabled;
    }

    /// <inheritdoc/>
    public void Enable()
    {
        _registry.SetString(RunKeyPath, _valueName, Command);

        // Turning the checkbox back on has to clear a Task Manager veto too, otherwise the
        // entry is written and still ignored. Deleting the approval value is what Windows
        // treats as "never vetoed".
        _registry.DeleteValue(StartupApprovedKeyPath, _valueName);
    }

    /// <inheritdoc/>
    public void Disable() => _registry.DeleteValue(RunKeyPath, _valueName);

    /// <inheritdoc/>
    public bool RepairPathIfNeeded()
    {
        var command = _registry.GetString(RunKeyPath, _valueName);
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        if (string.Equals(command, Command, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _registry.SetString(RunKeyPath, _valueName, Command);
        return true;
    }

    /// <summary>
    /// Whether Task Manager has switched the entry off.
    ///
    /// The approval value is a 12-byte blob whose first byte carries the flag: an even
    /// value means enabled (0x02), an odd one means the user disabled it (0x03).
    /// A missing value means the entry was never vetoed.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the first byte of the approval value is odd; <c>false</c> when it is
    /// even, or the value is empty or missing.
    /// </returns>
    private bool IsApprovalRevoked()
    {
        var approval = _registry.GetBinary(StartupApprovedKeyPath, _valueName);
        return approval is { Length: > 0 } && (approval[0] & 0x01) != 0;
    }

    /// <summary>Puts a path between double quotes, so one with spaces stays one command.</summary>
    /// <param name="path">The executable's path.</param>
    /// <returns>The path between double quotes.</returns>
    private static string Quote(string path) => $"\"{path}\"";
}
