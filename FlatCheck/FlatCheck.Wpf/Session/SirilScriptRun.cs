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

// SirilScriptRun.cs - lansarea lui siril-cli pe un script, cu iesirea citita live.
//
//   siril-cli.exe -d <folderul sesiunii> -s <script>
//
// Masurat: log-ul iese pe stdout linie cu linie, pe masura ce se intampla, si prin pipe -
// pauzele dintre linii coincid cu munca, nu vin toate la sfarsit. Codul de iesire e 0
// la succes si 1 cand scriptul se opreste la o comanda. stderr ramane gol, dar se
// citeste oricum, pe acelasi canal, ca sa nu se piarda nimic daca o versiune viitoare
// scrie acolo.
//
// Anularea omoara procesul (si copiii lui - Siril porneste ajutoare gspawn), si se
// raporteaza ca OperationCanceledException, nu ca un cod de iesire: un cod ar arata ca
// un esec al scriptului, si nu e.

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    /// <summary>Ce ruleaza fereastra: primeste liniile, intoarce codul de iesire. Se inlocuieste in teste.</summary>
    internal delegate Task<int> SirilRunner(Action<string> onLine, CancellationToken ct);

    internal static class SirilScriptRun
    {
        /// <summary>Un runner adevarat, peste siril-cli. Liniile vin de pe firele de citire ale procesului.</summary>
        internal static SirilRunner For(string cliPath, string folder, string scriptPath)
            => (onLine, ct) => ExecuteAsync(cliPath, folder, scriptPath, onLine, ct);

        internal static async Task<int> ExecuteAsync(string cliPath, string folder, string scriptPath,
                                                     Action<string> onLine, CancellationToken ct)
        {
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

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using (ct.Register(() => Kill(process)))
            {
                // Fara jeton aici: la anulare procesul e omorat prin Register, iese, si
                // asteptarea se termina normal - abia apoi se raporteaza anularea.
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();
            return process.ExitCode;
        }

        static void Kill(Process p)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
}
