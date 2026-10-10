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

using System.Text.Json;

namespace LightsCheck.Wpf;

/// <summary>
/// The window's one stored setting, the SynScan arrow keys (R-041, ADR 0014):
/// <c>%LOCALAPPDATA%\LightsCheck\settings.json</c>, two symbols. A missing or unreadable
/// file, or an unknown symbol, means the defaults; a failed write is ignored. The tests
/// point <see cref="Path"/> at a file of their own.
/// </summary>
public static class UserSettings
{
    private const string DefaultEast = "→";
    private const string DefaultNorth = "↑";

    /// <summary>The file read and written; null = the default location under the local application data.</summary>
    public static string? Path { get; set; }

    private static string FilePath => Path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LightsCheck", "settings.json");

    private sealed record Stored(string? East, string? North);

    public static (string East, string North) Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return (DefaultEast, DefaultNorth);
            }

            Stored? s = JsonSerializer.Deserialize<Stored>(File.ReadAllText(FilePath), Json);
            string east = s?.East is "→" or "←" ? s.East : DefaultEast;
            string north = s?.North is "↑" or "↓" ? s.North : DefaultNorth;
            return (east, north);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return (DefaultEast, DefaultNorth);
        }
    }

    public static void Save(string east, string north)
    {
        try
        {
            string? dir = System.IO.Path.GetDirectoryName(FilePath);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Stored(east, north), Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A setting that cannot be written is typed again next time; nothing else depends on it.
        }
    }

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
