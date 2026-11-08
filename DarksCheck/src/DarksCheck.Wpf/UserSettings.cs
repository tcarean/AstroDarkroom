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
using NLog;

namespace DarksCheck.Wpf;

/// <summary>
/// The window's stored settings as text, exactly as typed (R-075): the thread count, the frame
/// cap, the gain per ISO stop, the degrees per doubling. The partner and the lights folder are
/// not stored (ADR 0041); an older file's `partner` key is ignored.
/// </summary>
/// <param name="Threads">The thread count as typed; null when never stored.</param>
/// <param name="MaxFrames">The cap on frames held in memory as typed; null when never
/// stored.</param>
/// <param name="IsoFactor">The signal factor per ISO stop as typed; null when never stored.</param>
/// <param name="Doubling">The degrees Celsius per doubling of the dark current as typed; null when
/// never stored.</param>
public sealed record StoredSettings(string? Threads, string? MaxFrames, string? IsoFactor, string? Doubling);

/// <summary>
/// The window's settings that outlive a run (R-075):
/// <c>%LOCALAPPDATA%\DarksCheck\settings.json</c>, the four strings of
/// <see cref="StoredSettings"/>, which the view-model parses with the invariant culture and falls
/// back from. This class stores and fetches text and judges nothing: a missing or unreadable file
/// gives nulls, a failed write is ignored. The tests point <see cref="Path"/> at a file of their
/// own.
/// </summary>
public static class UserSettings
{
    /// <summary>
    /// The file read and written; null = the default location under the local application data.
    /// </summary>
    public static string? Path { get; set; }

    private static string FilePath => Path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DarksCheck", "settings.json");

    /// <summary>
    /// The stored strings, null each when the file is missing, unreadable or lacks them.
    /// </summary>
    /// <returns>The settings; never null, all four null when the file is missing, unreadable or not
    /// JSON.</returns>
    public static StoredSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new StoredSettings(null, null, null, null);
            }

            return JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(FilePath), Json) ?? new StoredSettings(null, null, null, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Debug(e, "the settings " + FilePath + " could not be read; the defaults are used");
            return new StoredSettings(null, null, null, null);
        }
    }

    /// <summary>
    /// Writes the settings as indented JSON, creating the folder; a write the file system refuses
    /// is ignored.
    /// </summary>
    /// <param name="settings">The four strings to store; not null.</param>
    public static void Save(StoredSettings settings)
    {
        try
        {
            string? dir = System.IO.Path.GetDirectoryName(FilePath);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A setting that cannot be written is picked again next time; nothing else depends on
            // it.
            Log.Debug(e, "the settings " + FilePath + " could not be written; they are picked again next time");
        }
    }

    /// <summary>
    /// The diagnostic log: a settings file that could not be read or written (R-101). A save that
    /// works writes nothing, since the boxes save on every keystroke.
    /// </summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    // The folder path is written as itself, not as \u escapes, so the file reads as the README
    // says.
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };
}
