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
//   siril-cli.exe -d <folderul sesiunii> -s <script> -i <copia setarilor lui Siril>
//
// SETARILE. siril-cli citeste la pornire fisierul de setari al lui Siril si il rescrie
// intreg la sfarsitul fiecarui script, reusit sau nu, cu folderul de la -d drept "wd".
// Masurat pe 30/09/2026, Siril 1.4.4, pe copii: pe masina autorului intre 5 si 10 la suta
// din salvari esueaza ("g_rename() failed: Permission denied"), si fiecare esec lasa
// fisierul STERS, cu continutul intr-un <fisier>.XXXXXX alaturi. Rularea urmatoare fara -i
// nu mai gaseste nimic, porneste cu valorile implicite si pe ele le salveaza. Asa si-a
// pierdut omul setarile pe 26/09/2026 - scripturile bifate, caile StarNet si GraXpert,
// focala - din testele de integrare, care rulau siril-cli de doua ori la fiecare verificare.
//
// De aceea fiecare rulare primeste cu -i o copie a lor, intr-un folder numai al ei,
// %TEMP%\FlatCheck\siril-<guid>\siril.ini, sters dupa ce siril-cli a iesit. Config-ul
// omului doar se citeste, si nimic din ce schimba o rulare in copie (wd, un set* din
// script) nu se intoarce in el. Folder, nu fisier: salvarea lui Siril lasa langa copie un
// siril.ini.XXXXXX, care pleaca odata cu folderul. Fisierul dat cu -i trebuie sa existe -
// lipsa inseamna iesire cu 1 si "Could not load or create settings file" pe stderr - iar
// unul gol inseamna valorile implicite ("nothing could be read"), exact ce ar folosi Siril
// fara niciun config. Un config care exista dar nu se poate citi opreste rularea inainte
// de pornire: fara copie, Siril ar lucra cu valorile implicite si nimeni n-ar sti. De ce
// asa, si ce s-a respins: ADR 0055.
//
// Masurat: log-ul iese pe stdout linie cu linie, pe masura ce se intampla, si prin pipe -
// pauzele dintre linii coincid cu munca, nu vin toate la sfarsit. Codul de iesire e 0
// la succes si 1 cand scriptul se opreste la o comanda; o salvare esuata a copiei nu il
// schimba, apare doar in log. stderr se citeste pe acelasi canal: acolo scrie Siril cand
// nu-si poate incarca setarile.
//
// Anularea omoara procesul (si copiii lui - Siril porneste ajutoare gspawn), si se
// raporteaza ca OperationCanceledException, nu ca un cod de iesire: un cod ar arata ca
// un esec al scriptului, si nu e.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    /// <summary>Ce ruleaza fereastra: primeste liniile, intoarce codul de iesire. Se inlocuieste in teste.</summary>
    internal delegate Task<int> SirilRunner(Action<string> onLine, CancellationToken ct);

    internal static class SirilScriptRun
    {
        /// <summary>Unde stau copiile setarilor, cate un folder siril-&lt;guid&gt; pe rulare - langa split-urile tabului Split.</summary>
        static string PrivateRoot => Path.Combine(Path.GetTempPath(), "FlatCheck");

        /// <summary>
        /// Un runner adevarat, peste siril-cli, pe o copie a config-ului din care vine si lista
        /// de scripturi. Config-ul se cauta abia la rulare, ca orice eroare sa ajunga in
        /// verdictul ei. Liniile vin de pe firele de citire ale procesului.
        /// </summary>
        internal static SirilRunner For(string cliPath, string folder, string scriptPath)
            => (onLine, ct) => ExecuteAsync(cliPath, folder, scriptPath, SirilInstall.FindConfig(), PrivateRoot, onLine, ct);

        /// <summary>
        /// Aceeasi rulare, cu config-ul de copiat si locul copiei date din afara - pentru teste.
        /// Un config null inseamna o copie goala: valorile implicite ale lui Siril.
        /// </summary>
        internal static SirilRunner For(string cliPath, string folder, string scriptPath, string settings, string privateRoot)
            => (onLine, ct) => ExecuteAsync(cliPath, folder, scriptPath, settings, privateRoot, onLine, ct);

        static async Task<int> ExecuteAsync(string cliPath, string folder, string scriptPath, string settings,
                                            string privateRoot, Action<string> onLine, CancellationToken ct)
        {
            // intai citirea: daca nu merge, nu s-a creat nimic si nu porneste nimic
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
            finally
            {
                // si dupa Opreste: siril-cli a iesit deja, deci copia nu mai e a nimanui
                DeleteQuietly(dir);
            }
        }

        /// <summary>
        /// Octetii config-ului lui Siril, sau niciunul cand nu e niciun config. ReadAllBytes
        /// imparte fisierul doar la citire. Dupa sursa GLib, o salvare a lui Siril care nimereste
        /// exact atunci esueaza si lasa fisierul intreg; daca citirea ar imparti si stergerea,
        /// Siril l-ar putea sterge si ramane fara el. Deci nu FileShare.Delete, nici File.Copy,
        /// al carui mod de impartire nu e verificat.
        /// </summary>
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
        /// Folderul copiei, cu tot ce a lasat Siril in el. Masurat: se sterge din prima imediat
        /// dupa iesire (100 de rulari din 100); daca totusi nu, raman cativa KB in %TEMP%.
        /// </summary>
        static void DeleteQuietly(string dir)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static void Kill(Process p)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
}
