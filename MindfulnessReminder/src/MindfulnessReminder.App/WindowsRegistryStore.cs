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

using Microsoft.Win32;
using MindfulnessReminder.Core;

namespace MindfulnessReminder.App;

/// <summary>The real HKEY_CURRENT_USER behind <see cref="IRegistryStore"/>.</summary>
internal sealed class WindowsRegistryStore : IRegistryStore
{
    /// <inheritdoc/>
    public string? GetString(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(valueName) as string;
    }

    /// <inheritdoc/>
    public byte[]? GetBinary(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(valueName) as byte[];
    }

    /// <inheritdoc/>
    public void SetString(string keyPath, string valueName, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(valueName, value, RegistryValueKind.String);
    }

    /// <inheritdoc/>
    public void DeleteValue(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
