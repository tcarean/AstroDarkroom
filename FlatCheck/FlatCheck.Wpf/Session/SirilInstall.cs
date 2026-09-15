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

// SirilInstall.cs - unde e Siril pe masina asta, si ce scripturi stie.
//
// Aplicatia nu aduce niciun script cu ea si nu lanseaza nimic de-al ei: ruleaza EXACT
// fisierul pe care Siril l-ar rula din meniul lui de scripturi, cu executabilul de
// consola din instalarea utilizatorului. Deci tot ce e aici e cautare, nu decizie:
// unde e siril-cli.exe, si ce fisiere .ssf a bifat omul in Siril.
//
// DE UNDE VIN SCRIPTURILE. Nu din folderul de instalare, cum ar parea firesc: acolo sunt
// doar cele sase de baza, iar OSC_Preprocessing_WithoutDark.ssf - cel folosit de fapt -
// sta in depozitul de scripturi pe care Siril il cloneaza in %LOCALAPPDATA%\siril-scripts.
// Ce leaga cele doua e fisierul de configurare, config.<versiune>.ini, in care sectiunea
// [gui] tine "script_path" (folderele scanate) si "selected_scripts" (fisierele bifate in
// meniu). Se citesc amandoua, deci lista de aici e lista din meniul Siril.
//
// Partea care parseaza primeste TEXTUL config-ului si doua functii de disc, ca sa se
// poata testa fara registru, fara Siril si fara un %LOCALAPPDATA% adevarat. Locate() e
// singura care atinge masina.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck.Wpf
{
    internal sealed class SirilInstall
    {
        /// <summary>
        /// Scriptul propus implicit: cel care tine tot in process/, inclusiv masterul de
        /// flat. Vezi nota de la SirilSession.PreprocessingScript.
        /// </summary>
        internal const string DefaultScript = SirilSession.PreprocessingScript;

        /// <summary>Numele proceselor Siril cu fereastra. siril-cli, al nostru, nu e "siril".</summary>
        const string GuiProcessName = "siril";

        internal SirilInstall(string cliPath, IReadOnlyList<string> scripts, string configPath = null)
        {
            CliPath = cliPath;
            Scripts = scripts ?? Array.Empty<string>();
            ConfigPath = configPath;
        }

        /// <summary>Calea catre siril-cli.exe, sau null cand nu s-a gasit.</summary>
        internal string CliPath { get; }

        internal bool HasCli => !string.IsNullOrEmpty(CliPath);

        /// <summary>Scripturile .ssf gasite, cai complete, sortate dupa numele fisierului.</summary>
        internal IReadOnlyList<string> Scripts { get; }

        /// <summary>Fisierul de configurare citit, sau null. Doar pentru mesaje.</summary>
        internal string ConfigPath { get; }

        /// <summary>
        /// Scriptul de propus la deschidere: cel implicit daca e in lista, altfel primul.
        /// Null cand nu e niciunul.
        /// </summary>
        internal string DefaultScriptPath
            => Scripts.FirstOrDefault(p => string.Equals(Path.GetFileName(p), DefaultScript,
                                                         StringComparison.OrdinalIgnoreCase))
               ?? Scripts.FirstOrDefault();

        // ------------------------------------------------------------------ pe masina

        /// <summary>Cauta instalarea de pe masina asta. Nu arunca: ce nu se gaseste e null sau gol.</summary>
        internal static SirilInstall Locate()
        {
            string cli = FindCli();
            string config = FindConfig();
            string text = null;
            if (config != null)
                try { text = File.ReadAllText(config); } catch (IOException) { } catch (UnauthorizedAccessException) { }

            var scripts = ScriptsFrom(text, File.Exists, ListScripts);
            if (scripts.Count == 0) scripts = ScriptsFrom(FallbackScriptDirs(cli), ListScripts);

            return new SirilInstall(cli, scripts, config);
        }

        /// <summary>
        /// siril-cli.exe: din intrarea de dezinstalare din registru (instalatorul Inno
        /// scrie InstallLocation), apoi din calea implicita. Nu e in PATH, deci PATH nu
        /// se incearca.
        /// </summary>
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

        static IEnumerable<string> InstallLocationsFromRegistry()
        {
            var found = new List<string>();
            const string sub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            foreach (var hive in new[] { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser })
            {
                try
                {
                    using var key = hive.OpenSubKey(sub);
                    if (key == null) continue;
                    foreach (string name in key.GetSubKeyNames())
                    {
                        using var k = key.OpenSubKey(name);
                        if (k?.GetValue("DisplayName") is not string display) continue;
                        if (!display.StartsWith("Siril", StringComparison.OrdinalIgnoreCase)) continue;
                        if (k.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc))
                            found.Add(loc);
                    }
                }
                catch (System.Security.SecurityException) { }
                catch (IOException) { }
            }

            return found;
        }

        /// <summary>
        /// Config-ul lui Siril: %LOCALAPPDATA%\siril\config.<versiune>.ini. Numele poarta
        /// versiunea, deci se ia cel mai nou dupa data - Siril isi migreaza setarile
        /// intr-un fisier nou la o versiune majora si il lasa pe cel vechi acolo.
        /// </summary>
        internal static string FindConfig()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "siril");
            if (!Directory.Exists(dir)) return null;

            try
            {
                return Directory.EnumerateFiles(dir, "config*.ini")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (IOException) { return null; }
        }

        static IEnumerable<string> ListScripts(string dir)
        {
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            try { return Directory.EnumerateFiles(dir, "*.ssf", SearchOption.AllDirectories).ToList(); }
            catch (IOException) { return Array.Empty<string>(); }
            catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
        }

        /// <summary>
        /// Fara config, sau cu unul care nu numeste niciun script: folderul de scripturi
        /// al instalarii si depozitul clonat de Siril.
        /// </summary>
        static IEnumerable<string> FallbackScriptDirs(string cli)
        {
            if (cli != null)
            {
                string install = Path.GetDirectoryName(Path.GetDirectoryName(cli));
                if (install != null) yield return Path.Combine(install, "scripts");
            }
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "siril-scripts");
        }

        // ------------------------------------------------------------------ pur

        /// <summary>
        /// Scripturile pe care le stie config-ul: fisierele din "selected_scripts" care
        /// exista, plus tot ce e .ssf in folderele din "script_path". Doar .ssf - cele .py
        /// au nevoie de mediul Python al lui Siril si nu sunt scripturi de preprocesare.
        /// Ordinea e dupa numele fisierului, ca in meniul Siril.
        /// </summary>
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

        static IReadOnlyList<string> ScriptsFrom(IEnumerable<string> dirs, Func<string, IEnumerable<string>> listScripts)
            => Distinct(dirs.SelectMany(listScripts).Where(IsSsf).ToList());

        static bool IsSsf(string path)
            => string.Equals(Path.GetExtension(path), ".ssf", StringComparison.OrdinalIgnoreCase);

        static IReadOnlyList<string> Distinct(IEnumerable<string> paths)
            => paths.Select(Normalise)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();

        static string Normalise(string p)
        {
            try { return Path.GetFullPath(p); } catch (ArgumentException) { return p; }
        }

        /// <summary>
        /// Cele doua chei din [gui]. Fisierul e un GKeyFile: "\\" inseamna "\", iar
        /// listele sunt despartite de ";" si se termina cu unul. Cheile din alte sectiuni
        /// cu acelasi nume nu conteaza - se citeste doar [gui].
        /// </summary>
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

        static IEnumerable<string> SplitList(string value)
            => value.Split(';')
                    .Select(v => v.Trim().Replace("\\\\", "\\"))
                    .Where(v => v.Length > 0);

        // ------------------------------------------------------------------ Siril deschis

        /// <summary>
        /// Daca ruleaza Siril-ul cu fereastra. Doua procese care scriu in acelasi process/
        /// - fereastra lui si siril-cli al nostru - strica fisierele; nu se poate sti pe
        /// ce folder e deschis celalalt, deci se avertizeaza, nu se refuza.
        /// </summary>
        internal static bool IsGuiRunning()
        {
            try { return System.Diagnostics.Process.GetProcessesByName(GuiProcessName).Length > 0; }
            catch (InvalidOperationException) { return false; }
        }
    }
}
