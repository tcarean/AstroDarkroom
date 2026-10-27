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

// SirilScriptRun.cs - launching siril-cli on a script, with its output read live.
//
//   siril-cli.exe -d <the session folder> -s <script> -i <the copy of Siril's settings>
//
// THE SETTINGS. siril-cli reads Siril's settings file at start-up and rewrites it whole at
// the end of every script, successful or not, with the folder from -d as "wd". Measured on
// 30/09/2026, Siril 1.4.4, on copies: on the author's machine between 5 and 10 percent of the
// saves fail ("g_rename() failed: Permission denied"), and each failure leaves the file
// DELETED, with its content in a <file>.XXXXXX beside it. The next run without -i finds
// nothing any more, starts with the default values and saves those. That is how the user lost
// the settings on 26/09/2026 - the ticked scripts, the StarNet and GraXpert paths, the focal
// length - from the integration tests, which ran siril-cli twice at every check.
//
// That is why each run gets with -i a copy of them, in a folder of its own only,
// %TEMP%\FlatCheck\siril-<guid>\siril.ini, deleted after siril-cli has exited. The user's
// config is only read, and nothing a run changes in the copy (wd, a set* from the script)
// goes back into it. A folder, not a file: Siril's save leaves a siril.ini.XXXXXX beside the
// copy, which goes with the folder. The file given with -i must exist - its absence means an
// exit with 1 and "Could not load or create settings file" on stderr - and an empty one means
// the default values ("nothing could be read"), exactly what Siril would use with no config at
// all. A config that exists but cannot be read stops the run before it starts: without a
// copy, Siril would work with the default values and nobody would know. Why so, and what was
// rejected: ADR 0055.
//
// Measured: the log comes out on stdout line by line, as it happens, and through a pipe - the
// pauses between lines coincide with the work, they do not all come at the end. The exit code
// is 0 on success and 1 when the script stops at a command; a failed save of the copy does not
// change it, it shows only in the log. stderr is read on the same channel: that is where Siril
// writes when it cannot load its settings.
//
// Cancellation kills the process (and its children - Siril starts gspawn helpers), and is
// reported as an OperationCanceledException, not as an exit code: a code would look like a
// failure of the script, and it is not one.

using System;
using NLog;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    /// <summary>What the window runs: it receives the lines, it returns the exit code. Replaced
    /// in the tests.</summary>
    /// <param name="onLine">Called for each stdout or stderr line, on a reader thread of the
    /// process, not the UI thread.</param>
    /// <param name="ct">Cancels the run by killing the process.</param>
    /// <returns>siril-cli's exit code: 0 on success, 1 when the script stopped at a
    /// command.</returns>
    internal delegate Task<int> SirilRunner(Action<string> onLine, CancellationToken ct);

    /// <summary>
    /// Builds the <see cref="SirilRunner"/> that starts siril-cli on a session folder and a
    /// script, with a private copy of Siril's settings (ADR 0055).
    /// </summary>
    internal static class SirilScriptRun
    {
        /// <summary>Where the copies of the settings sit, one siril-&lt;guid&gt; folder per
        /// run - beside the Split tab's splits.</summary>
        static string PrivateRoot => Path.Combine(Path.GetTempPath(), "FlatCheck");

        /// <summary>
        /// A real runner, over siril-cli, on a copy of the config the list of scripts comes
        /// from too. The config is looked up only at run time, so that any error reaches its
        /// verdict. The lines come from the process's reader threads.
        /// </summary>
        /// <param name="cliPath">The full path of siril-cli.exe.</param>
        /// <param name="folder">The session folder, passed with -d and used as the working
        /// directory.</param>
        /// <param name="scriptPath">The .ssf to run, passed with -s.</param>
        /// <returns>The runner; nothing starts until it is called.</returns>
        internal static SirilRunner For(string cliPath, string folder, string scriptPath)
            => (onLine, ct) => ExecuteAsync(cliPath, folder, scriptPath, SirilInstall.FindConfig(), PrivateRoot, onLine, ct);

        /// <summary>
        /// The same run, with the config to copy and the copy's place given from outside - for
        /// the tests. A null config means an empty copy: Siril's default values.
        /// </summary>
        /// <param name="cliPath">The full path of siril-cli.exe.</param>
        /// <param name="folder">The session folder.</param>
        /// <param name="scriptPath">The .ssf to run.</param>
        /// <param name="settings">The settings file to copy, or null for an empty
        /// copy.</param>
        /// <param name="privateRoot">The folder under which the run's private folder is
        /// made.</param>
        /// <returns>The runner; nothing starts until it is called.</returns>
        internal static SirilRunner For(string cliPath, string folder, string scriptPath, string settings, string privateRoot)
            => (onLine, ct) => ExecuteAsync(cliPath, folder, scriptPath, settings, privateRoot, onLine, ct);

        /// <summary>
        /// Copies the settings into a private folder, starts siril-cli on the script, forwards
        /// every output line, waits for the exit and deletes the folder. Throws an
        /// <see cref="IOException"/> before starting anything when the settings cannot be
        /// read, and an <see cref="OperationCanceledException"/> after the process was killed
        /// on cancellation.
        /// </summary>
        /// <param name="cliPath">The full path of siril-cli.exe.</param>
        /// <param name="folder">The session folder.</param>
        /// <param name="scriptPath">The .ssf to run.</param>
        /// <param name="settings">The settings file to copy, or null for an empty
        /// copy.</param>
        /// <param name="privateRoot">The folder under which the private folder is
        /// made.</param>
        /// <param name="onLine">Called for each stdout and stderr line, on a reader
        /// thread.</param>
        /// <param name="ct">Kills the process tree when cancelled.</param>
        /// <returns>siril-cli's exit code.</returns>
        static async Task<int> ExecuteAsync(string cliPath, string folder, string scriptPath, string settings,
                                            string privateRoot, Action<string> onLine, CancellationToken ct)
        {
            // the reading first: if it fails, nothing has been created and nothing starts
            byte[] copy = ReadSettings(settings);

            string dir = Path.Combine(privateRoot, "siril-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string config = Path.Combine(dir, "siril.ini");
                File.WriteAllBytes(config, copy);

                var psi = new ProcessStartInfo
                {
                    FileName = cliPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = folder,
                };
                psi.ArgumentList.Add("-d");
                psi.ArgumentList.Add(folder);
                psi.ArgumentList.Add("-s");
                psi.ArgumentList.Add(scriptPath);
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(config);

                using var process = new Process { StartInfo = psi };
                process.OutputDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };

                Log.Info("starting " + cliPath + " -d \"" + folder + "\" -s \"" + scriptPath + "\" -i \"" + config
                         + "\" (settings copied from " + LogText.Str(settings) + ", " + LogText.Int(copy.Length) + " bytes)");
                process.Start();
                Log.Debug("siril-cli started, pid " + LogText.Int(process.Id));
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (ct.Register(() => Kill(process)))
                {
                    // No token here: on cancellation the process is killed through Register, it
                    // exits, and the wait ends normally - only then is the cancellation
                    // reported.
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }

                ct.ThrowIfCancellationRequested();
                return process.ExitCode;
            }
            finally
            {
                // after Opreste too: siril-cli has already exited, so the copy is nobody's any
                // more
                DeleteQuietly(dir);
            }
        }

        /// <summary>
        /// The bytes of Siril's config, or none when there is no config. ReadAllBytes shares
        /// the file for reading only. By the GLib source, a Siril save that lands exactly then
        /// fails and leaves the file whole; if the read shared deletion too, Siril could delete
        /// it and be left without it. So not FileShare.Delete, nor File.Copy, whose sharing
        /// mode is not checked.
        /// </summary>
        /// <param name="path">The config file, or null.</param>
        /// <returns>The file's bytes, or an empty array for null. Throws an
        /// <see cref="IOException"/> with the window's message when the file cannot be
        /// read.</returns>
        static byte[] ReadSettings(string path)
        {
            if (path == null) return Array.Empty<byte>();
            try { return File.ReadAllBytes(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new IOException(SessionMessages.SirilSettingsUnreadable(path, e.Message), e);
            }
        }

        /// <summary>
        /// The copy's folder, with everything Siril left in it. Measured: it is deleted at the
        /// first try right after the exit (100 runs out of 100); if not after all, a few KB
        /// stay in %TEMP%.
        /// </summary>
        /// <param name="dir">The private folder to delete; a failure is ignored.</param>
        static void DeleteQuietly(string dir)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Debug(e, "the private settings folder " + dir + " could not be deleted; it stays in %TEMP%");
            }
        }

        /// <summary>The diagnostic log: the command line started and what could not be
        /// cleaned up (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Siril);

        /// <summary>Kills the process and its children, if it is still running; never
        /// throws.</summary>
        /// <param name="p">The siril-cli process.</param>
        static void Kill(Process p)
        {
            try
            {
                if (!p.HasExited)
                {
                    Log.Info("killing siril-cli and its children, the run was stopped");
                    p.Kill(entireProcessTree: true);
                }
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.ComponentModel.Win32Exception)
            {
                Log.Debug(e, "siril-cli could not be killed; it had probably exited");
            }
        }
    }
}
