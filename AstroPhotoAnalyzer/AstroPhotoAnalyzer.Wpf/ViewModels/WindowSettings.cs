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

// WindowSettings.cs - what the window remembers between its runs (R-156): one JSON file in the
// user's local application data, today with one key, the ICC profile's path. Window state,
// nothing computed, so it lives here and not in the core (ADR 0051). It judges nothing: what
// cannot be read is no setting, what cannot be written is forgotten at the next start.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The remembered settings of the window (R-156): read once at the start from a file, written
    /// whole whenever one changes. A settings object without a path (<see cref="None"/>, the
    /// parameterless constructors' and the tests') remembers nothing.
    /// </summary>
    public sealed class WindowSettings
    {
        /// <summary>The key of the ICC profile's path in the file.</summary>
        const string IccProfileKey = "iccProfile";

        readonly string _path;

        /// <summary>Settings bound to a file, with nothing read yet.</summary>
        /// <param name="path">The file's full path; null remembers nothing.</param>
        WindowSettings(string path) => _path = path;

        /// <summary>Settings that are neither read nor written: for tests and for a window built without a file.</summary>
        public static WindowSettings None => new(null);

        /// <summary>
        /// The user's settings, <c>%LOCALAPPDATA%\AstroPhotoAnalyzer\settings.json</c>, read.
        /// </summary>
        /// <returns>The settings as the file holds them, or empty when it is missing or unreadable.</returns>
        public static WindowSettings InLocalAppData()
            => Read(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroPhotoAnalyzer", "settings.json"));

        /// <summary>
        /// Reads the settings of a file: missing, not readable, not a JSON object, without the
        /// key, or with a value that is not a string or is blank all mean no profile, never an
        /// exception.
        /// </summary>
        /// <param name="path">The file's full path; it need not exist; null remembers nothing.</param>
        /// <returns>The settings, bound to that path for the writes to come.</returns>
        public static WindowSettings Read(string path)
        {
            var settings = new WindowSettings(path);
            if (path == null) return settings;
            try
            {
                if (!File.Exists(path)) return settings;
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
                // A blank path (a hand edit) is no profile: the core would show an empty name
                // with the system's complaint about an empty path.
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty(IccProfileKey, out JsonElement value)
                    && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                    settings.IccProfilePath = value.GetString();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                // No setting: the window starts as if the file were absent.
            }
            return settings;
        }

        /// <summary>The file the settings are read from and written to; null when nothing is remembered.</summary>
        public string FilePath => _path;

        /// <summary>
        /// The full path of the ICC profile in use (R-155), or null for none; the window reads
        /// the profile again from it at the start (R-156).
        /// </summary>
        public string IccProfilePath { get; set; }

        /// <summary>
        /// Writes the settings to their file, creating its folder: the text goes to a temporary
        /// file beside it, which then replaces the file in one move, so an interrupted write
        /// leaves the last good file and never a truncated one. A write that fails for IO or
        /// access is ignored: the setting holds for this run and is lost at the next. Nothing is
        /// written without a path.
        /// </summary>
        public void Write()
        {
            if (_path == null) return;
            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var values = new Dictionary<string, string>();
                if (IccProfilePath != null) values[IccProfileKey] = IccProfilePath;
                string temp = _path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(values));
                File.Move(temp, _path, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Nothing else depends on the file: the choice is already applied for this run.
            }
        }
    }
}
