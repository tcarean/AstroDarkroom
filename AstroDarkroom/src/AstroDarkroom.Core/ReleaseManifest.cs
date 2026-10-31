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
/// One component as release.json records it (R-004): the kit's release.ps1 writes one entry
/// per project it bundled, the launcher's own first.
/// </summary>
/// <param name="Name">The component's folder name ("FlatCheck").</param>
/// <param name="Version">Its &lt;Version&gt; when it was bundled; "" when the file does not say.</param>
/// <param name="Commit">The last commit of its folder, full hash; "" when the file does not say.</param>
/// <param name="Unreleased">True when its CHANGELOG had content under [Unreleased] at the time: changes its version does not name yet.</param>
/// <param name="Clean">True when its folder had no uncommitted changes at the time.</param>
public sealed record ComponentInfo(string Name, string Version, string Commit, bool Unreleased, bool Clean);

/// <summary>
/// What release.json says about the suite folder (R-004).
/// </summary>
/// <param name="Name">The releasing project's folder name ("AstroDarkroom").</param>
/// <param name="Version">The suite's version.</param>
/// <param name="Date">The day of the release, "YYYY-MM-DD".</param>
/// <param name="Commit">The commit the release was cut at, full hash; "" when the file does not say.</param>
/// <param name="Components">Every component bundled, the releasing project first.</param>
public sealed record ReleaseInfo(string Name, string Version, string Date, string Commit, IReadOnlyList<ComponentInfo> Components)
{
    /// <summary>
    /// The entry of one component.
    /// </summary>
    /// <param name="name">The component's folder name, as SuiteApp.Folder gives it; letter case counts.</param>
    /// <returns>Its entry, or null when the file lists no such component.</returns>
    public ComponentInfo? Find(string name) => Components.FirstOrDefault(c => c.Name == name);
}

/// <summary>
/// Reads release.json tolerantly (R-004): the launcher must run the same from a folder that
/// has none, or one a hand edit broke, so nothing here throws for a bad file; the diagnostic log
/// says what the file gave, or why it gave nothing (R-012).
/// </summary>
public static class ReleaseManifest
{
    /// <summary>The diagnostic log: the file read with every version, or why it gave nothing (R-012).</summary>
    private static readonly Logger Log = LogManager.GetLogger(nameof(ReleaseManifest));

    /// <summary>
    /// Reads the file. Every key is optional but the components array; a string key that is
    /// missing or not a string reads as "", a boolean one as false. One line of the diagnostic
    /// log says what came of it: the versions at Info, a missing file at Info, a file that gives
    /// nothing at Warn, with the exception when it could not be read.
    /// </summary>
    /// <param name="path">The full path of release.json; may name a file that does not exist.</param>
    /// <returns>What the file says, or null when it is missing, unreadable, not a JSON object or has no components array.</returns>
    public static ReleaseInfo? TryRead(string path)
    {
        if (!File.Exists(path))
        {
            Log.Info("no file at " + path + "; the versions are unknown");
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("components", out JsonElement components)
                || components.ValueKind != JsonValueKind.Array)
            {
                Log.Warn(path + " is not a release manifest ("
                    + (root.ValueKind == JsonValueKind.Object ? "no components array" : "no JSON object") + "); the versions are unknown");
                return null;
            }

            List<ComponentInfo> list = [];
            foreach (JsonElement c in components.EnumerateArray())
            {
                if (c.ValueKind == JsonValueKind.Object)
                {
                    list.Add(new ComponentInfo(Text(c, "name"), Text(c, "version"), Text(c, "commit"), Flag(c, "unreleased"), Flag(c, "clean")));
                }
            }

            ReleaseInfo info = new(Text(root, "name"), Text(root, "version"), Text(root, "date"), Text(root, "commit"), list);
            Log.Info(LogText.Release(path, info));
            return info;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Warn(e, path + " could not be read; the versions are unknown");
            return null;
        }
    }

    /// <summary>
    /// A string property; the one tolerant read of a key this file and the language setting share.
    /// </summary>
    /// <param name="element">The object to read from; a JSON object.</param>
    /// <param name="name">The property name.</param>
    /// <returns>Its text, or "" when it is missing or not a string.</returns>
    internal static string Text(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    /// <summary>
    /// A boolean property.
    /// </summary>
    /// <param name="element">The object to read from.</param>
    /// <param name="name">The property name.</param>
    /// <returns>Its value, or false when it is missing or not a boolean.</returns>
    private static bool Flag(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}
