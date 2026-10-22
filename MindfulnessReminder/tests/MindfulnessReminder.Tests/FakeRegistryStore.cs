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

/// <summary>An in-memory HKCU. Keys are compared case-insensitively, as the real one does.</summary>
internal sealed class FakeRegistryStore : IRegistryStore
{
    private readonly Dictionary<(string Key, string Value), object> _values =
        new(new CaseInsensitivePathComparer());

    public int WriteCount { get; private set; }

    /// <summary>
    /// When set, every read throws it, as a key the account may not read does. A write to such
    /// a key throws it too: the real store opens a key it writes with read access as well.
    /// </summary>
    public Exception? ReadRefusal { get; set; }

    /// <summary>When set, every write throws it, as a key the account may not write does.</summary>
    public Exception? WriteRefusal { get; set; }

    /// <summary>
    /// When set, <see cref="ReadRefusal"/> and <see cref="WriteRefusal"/> apply to this key
    /// only, compared without regard to case; when null, to every key.
    /// </summary>
    public string? RefusedKeyPath { get; set; }

    public string? GetString(string keyPath, string valueName) =>
        RefusalFor(ReadRefusal, keyPath) is { } refusal
            ? throw refusal
            : _values.TryGetValue((keyPath, valueName), out var value) ? value as string : null;

    public byte[]? GetBinary(string keyPath, string valueName) =>
        RefusalFor(ReadRefusal, keyPath) is { } refusal
            ? throw refusal
            : _values.TryGetValue((keyPath, valueName), out var value) ? value as byte[] : null;

    public void SetString(string keyPath, string valueName, string value)
    {
        if (WriteRefusalFor(keyPath) is { } refusal)
        {
            throw refusal;
        }

        _values[(keyPath, valueName)] = value;
        WriteCount++;
    }

    public void SetBinary(string keyPath, string valueName, byte[] value) =>
        _values[(keyPath, valueName)] = value;

    public void DeleteValue(string keyPath, string valueName)
    {
        if (WriteRefusalFor(keyPath) is { } refusal)
        {
            throw refusal;
        }

        _values.Remove((keyPath, valueName));
    }

    public bool Contains(string keyPath, string valueName) =>
        _values.ContainsKey((keyPath, valueName));

    private Exception? WriteRefusalFor(string keyPath) =>
        RefusalFor(WriteRefusal, keyPath) ?? RefusalFor(ReadRefusal, keyPath);

    private Exception? RefusalFor(Exception? refusal, string keyPath) =>
        RefusedKeyPath is null || string.Equals(RefusedKeyPath, keyPath, StringComparison.OrdinalIgnoreCase)
            ? refusal
            : null;

    private sealed class CaseInsensitivePathComparer : IEqualityComparer<(string Key, string Value)>
    {
        public bool Equals((string Key, string Value) x, (string Key, string Value) y) =>
            string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Key, string Value) obj) =>
            HashCode.Combine(
                obj.Key.ToUpperInvariant(),
                obj.Value.ToUpperInvariant());
    }
}
