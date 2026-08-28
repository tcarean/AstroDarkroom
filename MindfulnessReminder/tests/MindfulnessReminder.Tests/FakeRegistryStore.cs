using MindfulnessReminder.Core;

namespace MindfulnessReminder.Tests;

/// <summary>An in-memory HKCU. Keys are compared case-insensitively, as the real one does.</summary>
internal sealed class FakeRegistryStore : IRegistryStore
{
    private readonly Dictionary<(string Key, string Value), object> _values =
        new(new CaseInsensitivePathComparer());

    public int WriteCount { get; private set; }

    public string? GetString(string keyPath, string valueName) =>
        _values.TryGetValue((keyPath, valueName), out var value) ? value as string : null;

    public byte[]? GetBinary(string keyPath, string valueName) =>
        _values.TryGetValue((keyPath, valueName), out var value) ? value as byte[] : null;

    public void SetString(string keyPath, string valueName, string value)
    {
        _values[(keyPath, valueName)] = value;
        WriteCount++;
    }

    public void SetBinary(string keyPath, string valueName, byte[] value) =>
        _values[(keyPath, valueName)] = value;

    public void DeleteValue(string keyPath, string valueName) =>
        _values.Remove((keyPath, valueName));

    public bool Contains(string keyPath, string valueName) =>
        _values.ContainsKey((keyPath, valueName));

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
