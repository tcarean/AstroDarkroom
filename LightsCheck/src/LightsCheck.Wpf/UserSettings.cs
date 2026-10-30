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

using System.Text.Encodings.Web;
using System.Text.Json;
using LightsCheck.Core;
using NLog;

namespace LightsCheck.Wpf;

/// <summary>
/// The window's one stored setting, the SynScan arrow keys (R-041, ADR 0014):
/// <c>%LOCALAPPDATA%\LightsCheck\settings.json</c>, two strings the view-model reads as
/// symbols through the core's <c>ArrowKeys</c>. This class stores and fetches text and
/// judges nothing: a missing or unreadable file gives nulls, a failed write is ignored. The
/// tests point <see cref="Path"/> at a file of their own.
/// </summary>
public static class UserSettings
{
    /// <summary>The diagnostic log: the settings file written, and one that could not be read or written (R-071).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    /// <summary>
    /// The file read and written; null = the default location under the local application data.
    /// </summary>
    public static string? Path { get; set; }

    private static string FilePath => Path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LightsCheck", "settings.json");

    /// <summary>The file's content, as JSON reads and writes it.</summary>
    /// <param name="East">The arrow symbol stored for east; null when absent.</param>
    /// <param name="North">The arrow symbol stored for north; null when absent.</param>
    private sealed record Stored(string? East, string? North);

    /// <summary>
    /// The two stored strings, null each when the file is missing, unreadable or lacks them.
    /// </summary>
    /// <returns>
    /// The east and north strings as stored, unjudged; (null, null) when the file is missing, not
    /// readable or not valid JSON.
    /// </returns>
    public static (string? East, string? North) Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return (null, null);
            }

            Stored? s = JsonSerializer.Deserialize<Stored>(File.ReadAllText(FilePath), Json);
            return (s?.East, s?.North);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Debug(e, "the settings " + FilePath + " could not be read; the arrows take their defaults");
            return (null, null);
        }
    }

    /// <summary>
    /// Writes the two strings to the file, creating its folder; a write that fails for IO or
    /// access is ignored.
    /// </summary>
    /// <param name="east">The arrow symbol chosen for east.</param>
    /// <param name="north">The arrow symbol chosen for north.</param>
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
            Log.Debug("wrote " + FilePath + " (" + LogText.FileKiB(FilePath) + ")");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A setting that cannot be written is picked again next time; nothing else depends
            // on it.
            Log.Debug(e, "the settings " + FilePath + " could not be written; the arrows are asked again next time");
        }
    }

    // The arrows are written as themselves, not as \u escapes, so the file reads as the README
    // says.
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
