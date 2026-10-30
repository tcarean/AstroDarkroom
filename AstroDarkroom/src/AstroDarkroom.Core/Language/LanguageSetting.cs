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
using NLog;

namespace AstroDarkroom.Core;

/// <summary>
/// The one remembered setting, the interface language (R-007): a JSON file with the key
/// <c>language</c> holding a code of <see cref="Ui.Code"/>. The window names the file
/// (<c>%LOCALAPPDATA%\AstroDarkroom\settings.json</c>); this class reads and writes any path
/// and judges nothing: what it cannot read is no language, what it cannot write is forgotten,
/// and the diagnostic log says which (R-012).
/// </summary>
public static class LanguageSetting
{
    /// <summary>The key of the language in the file.</summary>
    private const string Key = "language";

    /// <summary>The diagnostic log: what the file gave or why it gave nothing, a write or why it failed (R-012).</summary>
    private static readonly Logger Log = LogManager.GetLogger(nameof(LanguageSetting));

    /// <summary>
    /// Reads the language the file remembers. One line of the diagnostic log says what came of
    /// it: the language or a missing file at Debug, a file that gives none at Warn, with the
    /// exception when it could not be read.
    /// </summary>
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
                Log.Debug("no setting file at " + path);
                return null;
            }

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                Log.Warn(path + " is not a JSON object; no language is taken from it");
                return null;
            }

            string code = ReleaseManifest.Text(root, Key);
            UiLanguage? language = Ui.TryParse(code);
            if (language is null)
            {
                Log.Warn(path + " holds " + Key + "=" + LogText.Str(code) + ", which is neither en nor ro; no language is taken from it");
                return null;
            }

            Log.Debug("setting read from " + path + ": " + Key + "=" + Ui.Code(language.Value));
            return language;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warn(e, path + " could not be read; no language is taken from it");
            return null;
        }
    }

    /// <summary>
    /// Writes the language to the file, creating its folder: the text goes to a temporary file
    /// beside it, which then replaces the file in one move, so an interrupted write leaves the
    /// last good file and never a truncated one. A write that fails for IO or access is
    /// ignored, so the choice holds for the run and is asked again at the next; the diagnostic
    /// log has the write at Debug, or its failure at Warn with the exception.
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

            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new Dictionary<string, string> { [Key] = Ui.Code(language) }));
            File.Move(temp, path, overwrite: true);
            Log.Debug("setting written to " + path + ": " + Key + "=" + Ui.Code(language));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nothing else depends on the file: the language is already set for this run.
            Log.Warn(e, "the setting could not be written to " + path + "; the language holds for this run");
        }
    }
}
