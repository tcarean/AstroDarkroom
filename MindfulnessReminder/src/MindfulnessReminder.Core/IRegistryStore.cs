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
/// The slice of the per-user registry this application needs. Keeping it behind an
/// interface is what lets the auto-start logic be unit tested off a real machine.
/// All key paths are relative to HKEY_CURRENT_USER.
/// </summary>
public interface IRegistryStore
{
    /// <summary>Reads a string value. Throws when this account may not read the key.</summary>
    /// <param name="keyPath">The key, relative to HKEY_CURRENT_USER.</param>
    /// <param name="valueName">The value under the key.</param>
    /// <returns>
    /// The string, or <c>null</c> when the key or the value is missing, or the value is not a
    /// string.
    /// </returns>
    string? GetString(string keyPath, string valueName);

    /// <summary>Reads a binary value. Throws when this account may not read the key.</summary>
    /// <param name="keyPath">The key, relative to HKEY_CURRENT_USER.</param>
    /// <param name="valueName">The value under the key.</param>
    /// <returns>
    /// The bytes, or <c>null</c> when the key or the value is missing, or the value is not
    /// binary.
    /// </returns>
    byte[]? GetBinary(string keyPath, string valueName);

    /// <summary>
    /// Writes a string value, creating the key when it is missing. Throws when this account may
    /// not write the key.
    /// </summary>
    /// <param name="keyPath">The key, relative to HKEY_CURRENT_USER.</param>
    /// <param name="valueName">The value under the key; replaced when it exists.</param>
    /// <param name="value">The text to store.</param>
    void SetString(string keyPath, string valueName, string value);

    /// <summary>
    /// Removes the value if present; does nothing when the key or value is missing. Throws
    /// when this account may not write the key.
    /// </summary>
    /// <param name="keyPath">The key, relative to HKEY_CURRENT_USER.</param>
    /// <param name="valueName">The value to remove.</param>
    void DeleteValue(string keyPath, string valueName);
}
