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

// SirilInstall.cs - where Siril is on this machine, and which scripts it knows.
//
// The application brings no script with it and launches nothing of its own: it runs EXACTLY
// the file Siril would run from its scripts menu, with the console executable of the user's
// installation. So everything here is lookup, not decision: where siril-cli.exe is, and which
// .ssf files the user ticked in Siril.
//
// WHERE THE SCRIPTS COME FROM. Not from the installation folder, as would seem natural: only
// the six basic ones are there, and OSC_Preprocessing_WithoutDark.ssf - the one actually used
// - sits in the scripts repository Siril clones into %LOCALAPPDATA%\siril-scripts. What links
// the two is the configuration file, config.<version>.ini, whose [gui] section holds
// "script_path" (the folders scanned) and "selected_scripts" (the files ticked in the menu).
// Both are read, so the list here is the list in Siril's menu.
//
// The parsing part receives the config's TEXT and two disk functions, so it can be tested
// without the registry, without Siril and without a real %LOCALAPPDATA%. Locate() is the only
// one that touches the machine.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The user's Siril installation as found: the path of siril-cli.exe and the .ssf scripts
    /// Siril's menu lists; a lookup, never a decision.
    /// </summary>
    internal sealed class SirilInstall
    {
        /// <summary>
        /// The script proposed by default: the one that keeps everything in process/, the flat
        /// master included. See the note at SirilSession.PreprocessingScript.
        /// </summary>
        internal const string DefaultScript = SirilSession.PreprocessingScript;

        /// <summary>The name of the Siril processes with a window. siril-cli, ours, is not
        /// "siril".</summary>
        const string GuiProcessName = "siril";

        /// <summary>Holds what was found.</summary>
        /// <param name="cliPath">The path of siril-cli.exe, or null when not found.</param>
        /// <param name="scripts">The .ssf scripts, full paths; null for none.</param>
        /// <param name="configPath">The configuration file read, or null; for the messages
        /// only.</param>
        internal SirilInstall(string cliPath, IReadOnlyList<string> scripts, string configPath = null)
        {
            CliPath = cliPath;
            Scripts = scripts ?? Array.Empty<string>();
            ConfigPath = configPath;
        }

        /// <summary>The path of siril-cli.exe, or null when it was not found.</summary>
        internal string CliPath { get; }

        internal bool HasCli => !string.IsNullOrEmpty(CliPath);

        /// <summary>The .ssf scripts found, full paths, sorted by file name.</summary>
        internal IReadOnlyList<string> Scripts { get; }

        /// <summary>The configuration file read, or null. For the messages only.</summary>
        internal string ConfigPath { get; }

        /// <summary>
        /// The script to propose at opening: the default one if it is in the list, otherwise the
        /// first. Null when there is none.
        /// </summary>
        internal string DefaultScriptPath
            => Scripts.FirstOrDefault(p => string.Equals(Path.GetFileName(p), DefaultScript,
                                                         StringComparison.OrdinalIgnoreCase))
               ?? Scripts.FirstOrDefault();

        // ------------------------------------------------------------------ on the machine

        /// <summary>Looks up the installation on this machine. Does not throw: what is not
        /// found is null or empty.</summary>
        /// <returns>The installation: the CLI, the scripts of the config (or, when it names
        /// none, of the fallback folders) and the config's path.</returns>
        internal static SirilInstall Locate()
        {
            string cli = FindCli();
            string config = FindConfig();
            string text = null;
            if (config != null)
            {
                try { text = File.ReadAllText(config); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Log.Debug(e, "Siril's config " + config + " could not be read; its script folders are not known");
                }
            }

            var scripts = ScriptsFrom(text, File.Exists, ListScripts);
            bool fromConfig = scripts.Count > 0;
            if (!fromConfig) scripts = ScriptsFrom(FallbackScriptDirs(cli), ListScripts);

            Log.Info("Siril: siril-cli=" + LogText.Str(cli) + " config=" + LogText.Str(config) + " scripts=" + LogText.Int(scripts.Count)
                     + (fromConfig ? " from the config's folders" : " from the fallback folders"));
            if (Log.IsDebugEnabled) Log.Debug("Siril scripts " + LogText.List(scripts));
            return new SirilInstall(cli, scripts, config);
        }

        /// <summary>The diagnostic log: where Siril was found, and what could not be read
        /// (R-110).</summary>
        static readonly NLog.Logger Log = NLog.LogManager.GetLogger("Siril");

        /// <summary>
        /// siril-cli.exe: from the uninstall entry in the registry (the Inno installer writes
        /// InstallLocation), then from the default path. It is not in PATH, so PATH is not
        /// tried.
        /// </summary>
        /// <returns>The full path of siril-cli.exe, or null when neither place has
        /// it.</returns>
        internal static string FindCli()
        {
            foreach (string root in InstallLocationsFromRegistry())
            {
                string p = Path.Combine(root, "bin", "siril-cli.exe");
                if (File.Exists(p)) return p;
            }

            string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string fallback = Path.Combine(programs, "Siril", "bin", "siril-cli.exe");
            return File.Exists(fallback) ? fallback : null;
        }

        /// <summary>The InstallLocation of every uninstall entry whose name starts with
        /// "Siril", machine-wide then per user; a hive or an entry that cannot be read is
        /// skipped, the other entries still count.</summary>
        /// <returns>The installation folders, possibly empty.</returns>
        static IEnumerable<string> InstallLocationsFromRegistry()
        {
            var found = new List<string>();
            const string sub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            foreach (var hive in new[] { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser })
            {
                string[] names;
                try
                {
                    using var key = hive.OpenSubKey(sub);
                    if (key == null) continue;
                    names = key.GetSubKeyNames();
                }
                catch (Exception e) when (IsUnreadable(e))
                {
                    Log.Debug(e, "the uninstall entries of " + hive.Name + " could not be read");
                    continue;
                }

                foreach (string name in names)
                {
                    try
                    {
                        using var k = hive.OpenSubKey(sub + @"\" + name);
                        if (k?.GetValue("DisplayName") is not string display) continue;
                        if (!display.StartsWith("Siril", StringComparison.OrdinalIgnoreCase)) continue;
                        if (k.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc))
                            found.Add(loc);
                    }
                    catch (Exception e) when (IsUnreadable(e))
                    {
                        Log.Debug(e, "the uninstall entry " + name + " could not be read");
                    }
                }
            }

            return found;
        }

        /// <summary>The exceptions a registry read throws for a key the user may not read or
        /// that vanished meanwhile.</summary>
        /// <param name="e">The exception caught.</param>
        /// <returns>True for a security, access or I/O error; false for anything else, which
        /// is a bug and propagates.</returns>
        static bool IsUnreadable(Exception e)
            => e is System.Security.SecurityException || e is UnauthorizedAccessException || e is IOException;

        /// <summary>
        /// Siril's config: %LOCALAPPDATA%\siril\config.&lt;version&gt;.ini. The name carries the
        /// version, so the newest by date is taken - Siril migrates its settings into a new
        /// file at a major version and leaves the old one there.
        /// </summary>
        /// <returns>The config file's path, or null when there is none.</returns>
        internal static string FindConfig()
            => FindConfig(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "siril"));

        /// <summary>The newest config*.ini of a folder, by last-write time.</summary>
        /// <param name="dir">The folder Siril keeps its settings in.</param>
        /// <returns>The config file's path, or null when the folder is missing, holds none or
        /// cannot be read.</returns>
        internal static string FindConfig(string dir)
        {
            if (!Directory.Exists(dir)) return null;

            try
            {
                return Directory.EnumerateFiles(dir, "config*.ini")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Debug(e, "Siril's settings folder " + dir + " could not be listed");
                return null;
            }
        }

        /// <summary>The .ssf files of a folder and its subfolders; never throws.</summary>
        /// <param name="dir">The folder to scan.</param>
        /// <returns>The full paths, or empty when the folder is missing or cannot be
        /// read.</returns>
        static IEnumerable<string> ListScripts(string dir)
        {
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            try { return Directory.EnumerateFiles(dir, "*.ssf", SearchOption.AllDirectories).ToList(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Debug(e, "the script folder " + dir + " could not be listed");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Without a config, or with one that names no script: the installation's scripts
        /// folder and the repository Siril cloned.
        /// </summary>
        /// <param name="cli">The path of siril-cli.exe (in the installation's bin/), or
        /// null.</param>
        /// <returns>The folders to scan, the installation's first when known.</returns>
        static IEnumerable<string> FallbackScriptDirs(string cli)
        {
            if (cli != null)
            {
                string install = Path.GetDirectoryName(Path.GetDirectoryName(cli));
                if (install != null) yield return Path.Combine(install, "scripts");
            }
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "siril-scripts");
        }

        // ------------------------------------------------------------------ pure

        /// <summary>
        /// The scripts the config knows: the files of "selected_scripts" that exist, plus every
        /// .ssf in the folders of "script_path". Only .ssf - the .py ones need Siril's Python
        /// environment and are not preprocessing scripts. The order is by file name, as in
        /// Siril's menu.
        /// </summary>
        /// <param name="configText">The config file's text, or null (gives an empty
        /// list).</param>
        /// <param name="fileExists">Whether a path exists on disk.</param>
        /// <param name="listScripts">The files under a folder.</param>
        /// <returns>The .ssf paths, normalised, each once, ordered by file name.</returns>
        internal static IReadOnlyList<string> ScriptsFrom(string configText, Func<string, bool> fileExists,
                                                           Func<string, IEnumerable<string>> listScripts)
        {
            var found = new List<string>();
            if (configText == null) return found;

            var (dirs, files) = ParseConfig(configText);

            foreach (string f in files)
                if (IsSsf(f) && fileExists(f)) found.Add(f);

            found.AddRange(ScriptsFrom(dirs, listScripts));
            return Distinct(found);
        }

        /// <summary>The .ssf files under some folders.</summary>
        /// <param name="dirs">The folders to scan.</param>
        /// <param name="listScripts">The files under a folder.</param>
        /// <returns>The .ssf paths, normalised, each once, ordered by file name.</returns>
        static IReadOnlyList<string> ScriptsFrom(IEnumerable<string> dirs, Func<string, IEnumerable<string>> listScripts)
            => Distinct(dirs.SelectMany(listScripts).Where(IsSsf).ToList());

        /// <summary>Whether a path has the .ssf extension, ignoring case.</summary>
        /// <param name="path">A file path.</param>
        /// <returns>True for a Siril script file.</returns>
        static bool IsSsf(string path)
            => string.Equals(Path.GetExtension(path), ".ssf", StringComparison.OrdinalIgnoreCase);

        /// <summary>Normalises the paths, drops duplicates ignoring case, and orders by file
        /// name, then by full path.</summary>
        /// <param name="paths">The paths found.</param>
        /// <returns>The list as the window shows it.</returns>
        static IReadOnlyList<string> Distinct(IEnumerable<string> paths)
            => paths.Select(Normalise)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();

        /// <summary>The full path of a path.</summary>
        /// <param name="p">A path, relative or absolute.</param>
        /// <returns>The full path, or the path unchanged when it is not valid.</returns>
        static string Normalise(string p)
        {
            try { return Path.GetFullPath(p); } catch (ArgumentException) { return p; }
        }

        /// <summary>
        /// The two keys of [gui]. The file is a GKeyFile: "\\" means "\", and the lists are
        /// separated by ";" and end with one. Keys with the same name in other sections do not
        /// count - only [gui] is read.
        /// </summary>
        /// <param name="text">The config file's text; not null.</param>
        /// <returns>The folders of "script_path" and the files of "selected_scripts", as
        /// written, unescaped.</returns>
        internal static (IReadOnlyList<string> ScriptDirs, IReadOnlyList<string> SelectedScripts) ParseConfig(string text)
        {
            var dirs = new List<string>();
            var files = new List<string>();
            bool inGui = false;

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                if (line[0] == '[') { inGui = line.Equals("[gui]", StringComparison.OrdinalIgnoreCase); continue; }
                if (!inGui) continue;

                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                if (key == "script_path") dirs.AddRange(SplitList(value));
                else if (key == "selected_scripts") files.AddRange(SplitList(value));
            }

            return (dirs, files);
        }

        /// <summary>Splits a GKeyFile list value and unescapes its backslashes.</summary>
        /// <param name="value">The value after "=".</param>
        /// <returns>The non-empty items, trimmed.</returns>
        static IEnumerable<string> SplitList(string value)
            => value.Split(';')
                    .Select(v => v.Trim().Replace("\\\\", "\\"))
                    .Where(v => v.Length > 0);

        // ------------------------------------------------------------------ Siril open

        /// <summary>
        /// Whether the Siril with a window is running. Two processes writing into the same
        /// process/ - its window and our siril-cli - spoil the files; one cannot know which
        /// folder the other has open, so it warns, it does not refuse.
        /// </summary>
        /// <returns>True when a process named "siril" runs; false when the process list cannot
        /// be read.</returns>
        internal static bool IsGuiRunning()
        {
            try { return System.Diagnostics.Process.GetProcessesByName(GuiProcessName).Length > 0; }
            catch (InvalidOperationException e)
            {
                Log.Debug(e, "the process list could not be read; Siril's window is taken as closed");
                return false;
            }
        }
    }
}
