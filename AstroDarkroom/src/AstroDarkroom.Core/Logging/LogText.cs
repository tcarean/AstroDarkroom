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

using System.Globalization;

namespace AstroDarkroom.Core;

/// <summary>
/// The words of the diagnostic log (R-012): English whatever the interface language, numbers in
/// the invariant culture, a missing value as <c>none</c>, never through <see cref="Ui.T"/>. The
/// core's own lines take their words here, and so do the window's lines about the suite
/// (<see cref="Presence"/>, <see cref="Open"/>), so the tests pin those too; FlatCheck's and
/// LightsCheck's <c>LogText</c>, reduced to what the launcher writes.
/// </summary>
public static class LogText
{
    /// <summary>An integer, invariant.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    internal static string Int(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// An elapsed time in seconds with one decimal: <c>12.9 s</c>, never whole seconds, so
    /// <c>0.8 s</c> does not read as nothing.
    /// </summary>
    /// <param name="elapsed">The time.</param>
    /// <returns>The text.</returns>
    internal static string Seconds(TimeSpan elapsed) => elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s";

    /// <summary>A text that may be missing or empty: itself, or <c>none</c>.</summary>
    /// <param name="value">The text, or null.</param>
    /// <returns>The text.</returns>
    internal static string Str(string? value) => string.IsNullOrEmpty(value) ? "none" : value;

    /// <summary>A flag as <c>yes</c> or <c>no</c>.</summary>
    /// <param name="value">The flag.</param>
    /// <returns>The text.</returns>
    internal static string Yes(bool value) => value ? "yes" : "no";

    /// <summary>
    /// The line of a release.json read (R-004, R-012): the suite's name, version, date and commit,
    /// then each component's name, version, commit and two flags, as the file gives them; a key
    /// the file lacks reads as <c>none</c> or <c>no</c>, as <see cref="ReleaseManifest.TryRead"/>
    /// read it.
    /// </summary>
    /// <param name="path">The file read.</param>
    /// <param name="info">What it gave; not null.</param>
    /// <returns>The line, without the logger's name.</returns>
    internal static string Release(string path, ReleaseInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        int n = info.Components.Count;
        string count = n == 1 ? "1 component" : Int(n) + " components";
        return "read " + path + ": name=" + Str(info.Name) + " version=" + Str(info.Version) + " date=" + Str(info.Date)
            + " commit=" + Str(info.Commit) + "; " + count
            + (n == 0 ? "" : ": " + string.Join("; ", info.Components.Select(Component)));
    }

    /// <summary>
    /// The window's line about the suite folder (R-012): how many of the apps have their window
    /// exe in it, and which exes are missing, by file name. The reason of R-002 is not used: it
    /// is worded in the interface language.
    /// </summary>
    /// <param name="folder">The suite folder; not null.</param>
    /// <param name="apps">The apps of the catalogue; not null.</param>
    /// <returns>The line, <c>suite folder &lt;dir&gt;: 2 of 6 apps present; missing ...</c>.</returns>
    public static string Presence(SuiteFolder folder, IReadOnlyList<SuiteApp> apps)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(apps);
        List<string> missing = apps.Where(a => !folder.Locate(a).IsAvailable).Select(a => a.WindowExe).ToList();
        string line = "suite folder " + folder.BaseDirectory + ": " + Int(apps.Count - missing.Count) + " of " + Int(apps.Count) + " apps present";
        return missing.Count == 0 ? line : line + "; missing " + string.Join(", ", missing);
    }

    /// <summary>
    /// The window's line before it starts an app (R-012): the app's title, then the file, the
    /// arguments and the working folder of the plan, as the window hands them to the shell.
    /// </summary>
    /// <param name="app">The app the user asked for; not null.</param>
    /// <param name="plan">Its start, as <see cref="Launcher.Plan"/> gave it; not null.</param>
    /// <returns>The line, <c>open FlatCheck: &lt;file&gt; --lang=ro in &lt;dir&gt;</c>.</returns>
    public static string Open(SuiteApp app, LaunchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(plan);
        return "open " + app.Title + ": " + plan.FileName + " " + plan.Arguments + " in " + plan.WorkingDirectory;
    }

    /// <summary>
    /// The launcher's line about the Visual C++ runtime at start (R-018): its four DLLs loading,
    /// or those that do not, and what that stops.
    /// </summary>
    /// <param name="missing">The DLLs that do not load, as <see cref="Core.VcRuntime.Missing"/> gave them; not null, empty when all load.</param>
    /// <returns>The line, <c>Visual C++ runtime: missing vcomp140.dll; DarksCheck, ...</c>.</returns>
    public static string VcRuntime(IReadOnlyList<string> missing)
    {
        ArgumentNullException.ThrowIfNull(missing);
        return missing.Count == 0
            ? "Visual C++ runtime: " + string.Join(", ", global::AstroDarkroom.Core.VcRuntime.Files) + " load"
            : "Visual C++ runtime: missing " + string.Join(", ", missing) + "; DarksCheck, FlatCheck and LightsCheck cannot read camera raw files";
    }

    /// <summary>One component of release.json, for <see cref="Release"/>.</summary>
    /// <param name="c">The component.</param>
    /// <returns>Its name, then <c>version=</c>, <c>commit=</c>, <c>unreleased=</c> and <c>clean=</c>.</returns>
    private static string Component(ComponentInfo c) => Str(c.Name) + " version=" + Str(c.Version) + " commit=" + Str(c.Commit)
        + " unreleased=" + Yes(c.Unreleased) + " clean=" + Yes(c.Clean);
}
