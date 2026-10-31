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

// SessionSettings.cs - what the window remembers per session folder (R-119, ADR 0078): one
// JSON file in the session root, flatcheck.json, with the two switches the user sets by hand -
// "no filters on the lens" on the Structure tab and "sky flat" on the Exposure tab. State of
// the window, nothing computed, so it lives here and not in the core. It judges nothing: what
// cannot be read is the default, what cannot be written is a line in the log and holds for
// this run.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NLog;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The two switches remembered in a session folder's <c>flatcheck.json</c> (R-119): read
    /// once when the folder opens, written whole whenever one changes. Settings without a path
    /// (<see cref="None"/>: the tests, a tab before any folder) remember nothing and write
    /// nowhere.
    /// </summary>
    internal sealed class SessionSettings
    {
        /// <summary>The file's name in the session root.</summary>
        internal const string FileName = "flatcheck.json";

        /// <summary>The key of the Structure tab's "no filters on the lens" box (R-118).</summary>
        const string NoFiltersKey = "noFilters";

        /// <summary>The key of the Exposure tab's "sky flat" box (R-043).</summary>
        const string SkyFlatKey = "skyFlat";

        /// <summary>The window's diagnostic log (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Window + ".Session");

        readonly string _path;

        /// <summary>Settings bound to a file, on the defaults, nothing read yet.</summary>
        /// <param name="path">The file's full path; null remembers nothing.</param>
        SessionSettings(string path) => _path = path;

        /// <summary>Settings neither read nor written: for the tests and for a tab that has no
        /// folder yet.</summary>
        internal static SessionSettings None => new(null);

        /// <summary>The file the settings are read from and written to; null when nothing is
        /// remembered.</summary>
        internal string FilePath => _path;

        /// <summary>The Structure tab's box: true (the default) when no filter was on the lens,
        /// so the vignetting is compared with the lens profile (R-118).</summary>
        internal bool NoFilters { get; set; } = true;

        /// <summary>The Exposure tab's box: true for a sky flat, whose bands differ from a
        /// panel's (R-043); false by default.</summary>
        internal bool SkyFlat { get; set; }

        /// <summary>
        /// Reads a session's settings. A missing or unreadable file, one that is not a JSON
        /// object, a key absent or holding something other than a boolean: the default for that
        /// key, never an exception and never a message - a hand edit must not stop the folder
        /// from opening.
        /// </summary>
        /// <param name="path">The file's full path; it need not exist; null remembers
        /// nothing.</param>
        /// <returns>The settings, bound to that path for the writes to come.</returns>
        internal static SessionSettings Read(string path)
        {
            var settings = new SessionSettings(path);
            if (path == null) return settings;
            try
            {
                if (!File.Exists(path)) return settings;
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return settings;
                settings.NoFilters = ReadBool(doc.RootElement, NoFiltersKey, settings.NoFilters);
                settings.SkyFlat = ReadBool(doc.RootElement, SkyFlatKey, settings.SkyFlat);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                Log.Debug(e, "settings " + path + ": could not be read; the defaults are used");
            }
            return settings;
        }

        /// <summary>One boolean key of the file's object.</summary>
        /// <param name="root">The file's object.</param>
        /// <param name="key">The key.</param>
        /// <param name="fallback">What an absent key, or one of another type, reads as.</param>
        /// <returns>The key's boolean, or the fallback.</returns>
        static bool ReadBool(JsonElement root, string key, bool fallback)
            => root.TryGetProperty(key, out JsonElement value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                ? value.GetBoolean()
                : fallback;

        /// <summary>
        /// Writes both switches to the file: the text goes to a temporary file beside it, which
        /// then replaces the file in one move, so an interrupted write leaves the last good file
        /// and never a truncated one. A write that fails for IO or access is a Warn line naming
        /// the file: the switch holds for this run and is lost at the next. Nothing is written
        /// without a path.
        /// </summary>
        internal void Write()
        {
            if (_path == null) return;
            try
            {
                var values = new Dictionary<string, bool> { [NoFiltersKey] = NoFilters, [SkyFlatKey] = SkyFlat };
                string temp = _path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(values));
                File.Move(temp, _path, overwrite: true);
                Log.Info("wrote " + _path + " (" + NoFiltersKey + "=" + LogText.Yes(NoFilters) + " " + SkyFlatKey + "=" + LogText.Yes(SkyFlat) + ")");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Warn(e, "settings " + _path + ": could not be written; the switches hold for this run only");
                // The temporary file, when the write got that far and the move failed: nothing
                // is left in the session folder that the user did not ask for.
                try { File.Delete(_path + ".tmp"); }
                catch (Exception d) when (d is IOException || d is UnauthorizedAccessException) { }
            }
        }
    }
}
