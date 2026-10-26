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

namespace AstroDarkroom.Core;

/// <summary>
/// The one remembered setting, the interface language (R-007): a JSON file with the key
/// <c>language</c> holding a code of <see cref="Ui.Code"/>. The window names the file
/// (<c>%LOCALAPPDATA%\AstroDarkroom\settings.json</c>); this class reads and writes any path
/// and judges nothing: what it cannot read is no language, what it cannot write is forgotten.
/// </summary>
public static class LanguageSetting
{
    /// <summary>The key of the language in the file.</summary>
    private const string Key = "language";

    /// <summary>Reads the language the file remembers.</summary>
    /// <param name="path">The file's full path; it need not exist.</param>
    /// <returns>
    /// The language of the file's <c>language</c> code; null when the file is missing, not
    /// readable, not a JSON object, without the key, or with a value that is not a known code.
    /// </returns>
    public static UiLanguage? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object
                   && root.TryGetProperty(Key, out JsonElement code)
                   && code.ValueKind == JsonValueKind.String
                ? Ui.TryParse(code.GetString())
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the language to the file, creating its folder; a write that fails for IO or
    /// access is ignored, so the choice holds for the run and is asked again at the next.
    /// </summary>
    /// <param name="path">The file's full path.</param>
    /// <param name="language">The language to remember.</param>
    public static void Write(string path, UiLanguage language)
    {
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, string> { [Key] = Ui.Code(language) }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nothing else depends on the file: the language is already set for this run.
        }
    }
}
