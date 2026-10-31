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
using DarksCheck.Core;
using NLog;
using static DarksCheck.Core.Ui;

namespace DarksCheck.Cli;

/// <summary>
/// The entry point of <c>darkscheck.exe</c>: reads the options, calls the core's runs and
/// writers, and turns the outcome into the exit code. It computes nothing itself.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The diagnostic log's console lines (R-101): the mode and the options, the refusals, the files
    /// written, a run that failed. Fetched on use, not held in a static field: this is the type
    /// whose <see cref="Main"/> configures the log, and a logger its type initialiser created first
    /// could make NLog load a stray <c>NLog.config</c> (ADR 0025, after FlatCheck's ADR 0072).
    /// </summary>
    private static Logger Log => LogManager.GetLogger("Cli");

    /// <summary>
    /// Runs the console on the process's own output and error streams, between the diagnostic
    /// log's first and last lines. The log is configured here and not in <see cref="Run"/>, which
    /// the tests call in-process (R-100 to R-103).
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The exit code: 0 on a clean run or for --help, 1 otherwise.</returns>
    private static int Main(string[] args)
    {
        // Whatever Apply throws, the run goes on, with the log as far as it was set up, stdout and
        // stderr untouched (R-001, R-101). A folder that cannot be written does not end here (Apply
        // falls back to %LOCALAPPDATA% itself), and NLog's own assembly is no such case: like the
        // core's it must be there, since without it the core's first logger throws.
        try
        {
            LogSetup.Apply("darkscheck", args);
        }
        catch (Exception)
        {
            // the run goes on without a log
        }

        // Run's exit code, for the log's last line. A run that throws past RunInLanguage's catch
        // crashes: the hook's Fatal line, written first, is then the crash's line.
        int code = 1;
        try
        {
            code = Run(args, Console.Out, Console.Error);
            return code;
        }
        finally
        {
            // Guarded like Apply: whatever Close throws, the exit code is kept.
            try
            {
                LogSetup.Close(code);
            }
            catch (Exception)
            {
                // the log's last line is lost, the exit code is not
            }
        }
    }

    /// <summary>
    /// The whole console, testable: the tests call this with a StringWriter and compare the
    /// text. Exit 0 on a clean run and for --help, 1 on an error or when a frame could not be
    /// read (R-002, R-003, R-004, R-025).
    /// </summary>
    /// <param name="args">The command-line arguments; not null.</param>
    /// <param name="stdout">Where the banner, the usage and the report go; not null.</param>
    /// <param name="stderr">Where the progress line and the error messages go; not null.</param>
    /// <returns>
    /// 0 when every frame was read (or for --help); 1 on a bad argument, on an error the run
    /// reported, or when a frame could not be read.
    /// </returns>
    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        RunReport.WriteBanner(stdout);

        // --lang first, so the usage and every error after it are in the language (R-090); a
        // value it does not know is said in English (R-092). With no --lang the run keeps the
        // process's language, English (Ui.Default).
        LanguageArgument lang = LanguageArgument.Extract(args);
        if (lang.BadValue is not null)
        {
            Log.Warn("refused: " + LanguageArgument.BadValueMessage(lang.BadValue));
            stderr.WriteLine("ERROR: " + LanguageArgument.BadValueMessage(lang.BadValue));
            return 1;
        }

        using (lang.Language is UiLanguage chosen ? Ui.Use(chosen) : null)
        {
            return RunInLanguage(lang.Rest, stdout, stderr);
        }
    }

    /// <summary>
    /// The console after the banner and <c>--lang</c>: parses the options, runs and reports, every
    /// text in <see cref="Ui.Current"/>, and writes the mode, the refusal, the files written or the
    /// failure to the diagnostic log (R-101).
    /// </summary>
    /// <param name="args">The arguments without <c>--lang</c> and its value; not null.</param>
    /// <param name="stdout">Where the usage and the report go; not null.</param>
    /// <param name="stderr">Where the progress line and the error messages go; not null.</param>
    /// <returns>The exit code of <see cref="Run"/>.</returns>
    private static int RunInLanguage(string[] args, TextWriter stdout, TextWriter stderr)
    {
        Options o = Options.Parse(args);
        if (o.Help)
        {
            Log.Info("mode=help");
            WriteUsage(stdout);
            return 0;
        }

        if (o.Error is not null)
        {
            Log.Warn("refused the arguments: " + o.Error);
            stderr.WriteLine(T("ERROR: ", "EROARE: ") + o.Error);
            return 1;
        }

        string mode = o.Inventory ? "inventory" : "darks";
        Log.Info("mode=" + mode + " input=" + o.Folder + " against=" + LogText.Str(o.Against) + " out=" + LogText.Str(o.Out)
            + " threads=" + (o.Threads > 0 ? LogText.Int(o.Threads) : "auto") + " maxFrames=" + LogText.Int(o.MaxFrames)
            + " lang=" + (Ui.Current == UiLanguage.Romanian ? "ro" : "en"));

        // Progress on stderr, one line rewritten, so stdout stays the report alone. Written
        // synchronously on the reporting thread under a lock, since the frames finish in
        // parallel (Progress<T> would post to the thread pool and could print after the report).
        ConsoleProgress progress = new(stderr);
        try
        {
            if (o.Inventory)
            {
                Inventory inv = InventoryRun.Run(o.Folder!);
                InventoryReport.Write(inv, stdout);
                return inv.HasErrors ? 1 : 0;
            }

            // a bad --against or --out is refused before the minutes of decoding, not after (R-026,
            // R-063)
            string? partnerRoot = o.Against is null ? null : SessionFolder.Open(o.Against).Root;
            if (o.Out is not null)
            {
                DarkOutputs.CheckOutDir(SessionFolder.Open(o.Folder!, allowLightsOnly: true).Root, o.Out);
                if (partnerRoot is not null)
                {
                    DarkOutputs.CheckOutDir(partnerRoot, o.Out, partner: true);
                }
            }

            DarkSeriesResult r = DarkSeriesRun.Run(o.Folder!, new DarkOptions(o.Threads, o.MaxFrames, o.Against), progress);
            progress.Clear();
            DarkReport.Write(r, stdout);
            if (o.Out is not null)
            {
                // Each file's line as soon as the file is written, so an export that fails
                // part-way still names what it wrote (R-101).
                IReadOnlyList<string> written = DarkOutputs.WriteAll(r, o.Out, p => Log.Info(LogText.Wrote(p)));
                stdout.WriteLine();
                foreach (string p in written)
                {
                    stdout.WriteLine(T("    -> written: ", "    -> scris: ") + p);
                }
            }

            return r.HasErrors ? 1 : 0;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException or AggregateException)
        {
            // The analysis runs frames in parallel; a failure inside one lands here wrapped.
            // The progress line, if one was being written, is cleared first.
            string message = e is AggregateException a ? string.Join("; ", a.Flatten().InnerExceptions.Select(x => x.Message).Distinct(StringComparer.Ordinal)) : e.Message;

            // The stack lives in the log; stderr keeps its one line, the same the window shows.
            Log.Error(e, "run failed: mode=" + mode);
            progress.Clear();
            stderr.WriteLine(T("ERROR: ", "EROARE: ") + message);
            return 1;
        }
    }

    /// <summary>
    /// Progress written as it happens, on the calling thread, serialised by a lock.
    /// </summary>
    /// <param name="stderr">The stream the progress line is rewritten on.</param>
    private sealed class ConsoleProgress(TextWriter stderr) : IProgress<RunProgress>
    {
        private readonly object _lock = new();
        private bool _shown;

        /// <summary>Rewrites the progress line with the phase and its count.</summary>
        /// <param name="value">The phase and how many of its items are done out of how
        /// many.</param>
        public void Report(RunProgress value)
        {
            lock (_lock)
            {
                _shown = true;
                stderr.Write($"\r    {value.Phase}: {value.Done.ToString(CultureInfo.InvariantCulture)} / {value.Total.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        /// <summary>
        /// Wipes the progress line, when one was written, so the report or an error starts on a
        /// clean line.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                if (_shown)
                {
                    stderr.Write("\r                                        \r");
                    _shown = false;
                }
            }
        }
    }

    /// <summary>
    /// Writes the help text in the run's language: the usage lines, what each option does, the exit
    /// codes and the warranty lines. Each language is written whole, since its columns are aligned
    /// by hand.
    /// </summary>
    /// <param name="w">The writer the text goes to.</param>
    private static void WriteUsage(TextWriter w)
    {
        string raws = string.Join(", ", RawReader.RawExtensions);
        string files = DarkOutputs.ReportName + ", " + DarkOutputs.DarksCsvName + ",";
        string moreFiles = DarkOutputs.BiasesCsvName + ", " + DarkOutputs.LightsCsvName + ", " + DarkOutputs.DarkMapName + ", ...).";
        string maxFrames = Th.MaxFramesInMemory.ToString(CultureInfo.InvariantCulture);
        w.WriteLine();
        if (Ui.Current == UiLanguage.Romanian)
        {
            w.WriteLine("Folosire: darkscheck <folder sesiune sau folder de dark-uri> [--against DIR] [--out DIR] [--threads N] [--max-frames N]");
            w.WriteLine("          darkscheck <folder> --inventory");
            w.WriteLine();
            w.WriteLine("  Folderul sesiunii Siril: dark-urile se iau din darks\\ (sau direct din folder,");
            w.WriteLine("  daca nu exista darks\\ si nici alt subfolder de sesiune), bias-urile din biases\\");
            w.WriteLine("  si lights-urile din lights\\, amandoua optionale. Raw de aparat");
            w.WriteLine("  (" + raws + ") sau FITS. Nimic nu e scris in folder.");
            w.WriteLine();
            w.WriteLine("  Fara optiuni: decodeaza bias-urile, dark-urile si lights-urile si tipareste raportul:");
            w.WriteLine("                scurgeri de lumina, deriva termica, zgomot, pixeli fierbinti,");
            w.WriteLine("                model fix, pixeli morti, cate dark-uri ajung, lights-urile fata de");
            w.WriteLine("                dark, indexul si eticheta, cate un tabel pe dark-uri si pe lights.");
            w.WriteLine("  --against DIR alt folder de dark-uri (o biblioteca sau o sesiune): indexul lui si");
            w.WriteLine("                raportul dark-urilor sesiunii fata de el, in grade echivalente; o sesiune");
            w.WriteLine("                cu lights dar fara dark-uri ia dark-urile lui drept ale ei.");
            w.WriteLine("  --out DIR     scrie in DIR raportul, cate un CSV cu toate cifrele pe dark-uri, pe");
            w.WriteLine("                bias-uri si pe lights si hartile (" + files);
            w.WriteLine("                " + moreFiles);
            w.WriteLine("  --threads N   cate cadre se decodeaza in paralel (implicit jumatate din nuclee).");
            w.WriteLine("  --max-frames N  cate cadre se tin in memorie (implicit " + maxFrames + ", 48 MB fiecare la 24 MP) si cate lights se masoara;");
            w.WriteLine("                peste, se decodeaza unul din k si restul sunt listate ca sarite.");
            w.WriteLine("  --inventory   doar metadatele cadrelor (fara decodare): ora, expunere, ISO,");
            w.WriteLine("                pauzele si cadenta fiecarui grup, potrivirea dark-urilor cu");
            w.WriteLine("                lights-urile.");
            w.WriteLine("  --lang en|ro  limba textelor: en (engleza, implicit) sau ro (romana).");
            w.WriteLine("  -h, --help    acest text");
            w.WriteLine();
            w.WriteLine("Cod de iesire: 0 daca toate cadrele s-au citit, 1 la eroare sau cand un cadru nu s-a putut citi.");
        }
        else
        {
            w.WriteLine("Usage: darkscheck <session folder or folder of darks> [--against DIR] [--out DIR] [--threads N] [--max-frames N]");
            w.WriteLine("       darkscheck <folder> --inventory");
            w.WriteLine();
            w.WriteLine("  The Siril session folder: the darks are taken from darks\\ (or from the folder");
            w.WriteLine("  itself, when there is no darks\\ and no other session subfolder), the biases");
            w.WriteLine("  from biases\\ and the lights from lights\\, both optional. Camera raw");
            w.WriteLine("  (" + raws + ") or FITS. Nothing is written into the folder.");
            w.WriteLine();
            w.WriteLine("  No options:   decodes the biases, the darks and the lights and prints the report:");
            w.WriteLine("                light leaks, thermal drift, noise, hot pixels, fixed pattern,");
            w.WriteLine("                dead pixels, how many darks are enough, the lights against the");
            w.WriteLine("                dark, the index and the label, one table for the darks and one for the lights.");
            w.WriteLine("  --against DIR another folder of darks (a library or a session): its index and");
            w.WriteLine("                the report of the session's darks against it, in equivalent degrees;");
            w.WriteLine("                a session with lights but no darks takes its darks as its own.");
            w.WriteLine("  --out DIR     writes into DIR the report, a CSV with every figure for the darks,");
            w.WriteLine("                the biases and the lights, and the maps (" + files);
            w.WriteLine("                " + moreFiles);
            w.WriteLine("  --threads N   how many frames are decoded in parallel (default: half the cores).");
            w.WriteLine("  --max-frames N  how many frames are held in memory (default " + maxFrames + ", 48 MB each at 24 MP) and how many lights are measured;");
            w.WriteLine("                above that, one in k is decoded and the rest are listed as skipped.");
            w.WriteLine("  --inventory   the frames' metadata only (no decoding): time, exposure, ISO,");
            w.WriteLine("                the gaps and the cadence of each group, how the darks match");
            w.WriteLine("                the lights.");
            w.WriteLine("  --lang en|ro  the language of the texts: en (English, the default) or ro (Romanian).");
            w.WriteLine("  -h, --help    this text");
            w.WriteLine();
            w.WriteLine("Exit code: 0 when every frame was read, 1 on an error or when a frame could not be read.");
        }

        w.WriteLine();
        foreach (string line in RunReport.WarrantyLines)
        {
            w.WriteLine(line);
        }
    }
}
