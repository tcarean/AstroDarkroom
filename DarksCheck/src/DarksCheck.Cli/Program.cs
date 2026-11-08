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
        Log.Info("mode=" + mode + " input=" + o.Folder + " against=" + LogText.Str(o.Against) + " lights=" + LogText.Str(o.Lights) + " out=" + LogText.Str(o.Out)
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
                Inventory inv = InventoryRun.Run(o.Folder!, o.Lights);
                InventoryReport.Write(inv, stdout);
                return inv.HasErrors ? 1 : 0;
            }

            // a bad --against, --lights or --out is refused before the minutes of decoding, not
            // after (R-026, R-055, R-063)
            string? partnerRoot = o.Against is null ? null : SessionFolder.Open(o.Against).Root;
            if (o.Lights is not null || o.Out is not null)
            {
                // one open, with the chosen lights: a folder that is lights-only by those lights
                // alone would otherwise be refused here as "lights\ but no darks\"
                SessionFolder session = SessionFolder.Open(o.Folder!, allowLightsOnly: true, o.Lights);
                if (o.Out is not null)
                {
                    DarkOutputs.CheckOutDir(session.Root, o.Out);
                    if (partnerRoot is not null)
                    {
                        DarkOutputs.CheckOutDir(partnerRoot, o.Out, DarkOutputs.OutFolder.Partner);
                    }

                    if (session.ChosenLightsRoot is string chosenLightsRoot)
                    {
                        DarkOutputs.CheckOutDir(chosenLightsRoot, o.Out, DarkOutputs.OutFolder.ChosenLights);
                    }
                }
            }

            DarkSeriesResult r = DarkSeriesRun.Run(o.Folder!, new DarkOptions(o.Threads, o.MaxFrames, o.Against, LightsFrom: o.Lights), progress);
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

        // the longest line written since the last wipe, so a shorter one and the wipe cover it all
        private int _width;

        /// <summary>
        /// Rewrites the progress line with the phase and its count, padded with spaces to the
        /// longest line before it.
        /// </summary>
        /// <param name="value">The phase and how many of its items are done out of how
        /// many.</param>
        public void Report(RunProgress value)
        {
            lock (_lock)
            {
                _shown = true;
                string line = $"    {value.Phase}: {value.Done.ToString(CultureInfo.InvariantCulture)} / {value.Total.ToString(CultureInfo.InvariantCulture)}";
                stderr.Write("\r" + line.PadRight(_width));
                _width = Math.Max(_width, line.Length);
            }
        }

        /// <summary>
        /// Wipes the progress line, when one was written, as wide as the longest line, so the report
        /// or an error starts on a clean line.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                if (_shown)
                {
                    stderr.Write("\r" + new string(' ', _width) + "\r");
                    _shown = false;
                    _width = 0;
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
            w.WriteLine("Folosire: darkscheck <folder sesiune sau folder de dark-uri> [--against DIR] [--lights DIR] [--out DIR] [--threads N] [--max-frames N]");
            w.WriteLine("          darkscheck <folder> --inventory [--lights DIR]");
            w.WriteLine();
            w.WriteLine("  Folderul sesiunii Siril: dark-urile se iau din darks\\ (sau direct din folder,");
            w.WriteLine("  daca nu exista darks\\ si nici alt subfolder de sesiune), bias-urile din biases\\");
            w.WriteLine("  si lights-urile din lights\\, amandoua optionale. Raw de aparat");
            w.WriteLine("  (" + raws + ") sau FITS. Nimic nu e scris in folder.");
            w.WriteLine();
            w.WriteLine("  Fara optiuni: citeste si masoara bias-urile, dark-urile si lights-urile si tipareste");
            w.WriteLine("                raportul: scurgeri de lumina, incalzirea camerei, zgomotul fiecarui");
            w.WriteLine("                cadru, pixeli fierbinti, ce e la fel in toate cadrele, pixeli morti si");
            w.WriteLine("                saturati, cate dark-uri ajung, cat de bine se potriveste dark-ul cu");
            w.WriteLine("                lights-urile, indexul si eticheta, cate un tabel pe dark-uri si pe lights.");
            w.WriteLine("  --against DIR alt folder de dark-uri (o biblioteca sau o sesiune): indexul lui si cu");
            w.WriteLine("                cate grade sunt dark-urile sesiunii mai calde sau mai reci decat el; o");
            w.WriteLine("                sesiune cu lights dar fara dark-uri ia dark-urile lui drept ale ei.");
            w.WriteLine("  --lights DIR  lights-urile din alt folder (o sesiune, din lights\\ al ei, sau un folder");
            w.WriteLine("                de lights): se compara cu dark-urile deschise, in locul celor din lights\\.");
            w.WriteLine("  --out DIR     scrie in DIR raportul, cate un CSV cu toate cifrele pe dark-uri, pe");
            w.WriteLine("                bias-uri si pe lights si hartile (" + files);
            w.WriteLine("                " + moreFiles);
            w.WriteLine("  --threads N   cate cadre se citesc in acelasi timp (implicit jumatate din");
            w.WriteLine("                nucleele procesorului).");
            w.WriteLine("  --max-frames N  cate cadre dintr-un grup se tin in memorie (implicit " + maxFrames + ", 48 MB fiecare la 24 MP);");
            w.WriteLine("                daca sunt mai multe, se citesc doar unele, la distante egale (de exemplu");
            w.WriteLine("                unul din doua), iar restul apar in tabel ca sarite; lights-urile se masoara toate.");
            w.WriteLine("  --inventory   doar ce scrie in fisiere, fara sa citeasca pixelii: ora, expunerea si");
            w.WriteLine("                ISO-ul fiecarui cadru, cat de des s-au facut cadrele si pauzele dintre");
            w.WriteLine("                ele, daca dark-urile se potrivesc cu lights-urile.");
            w.WriteLine("  --lang en|ro  limba textelor: en (engleza, implicit) sau ro (romana).");
            w.WriteLine("  -h, --help    acest text");
            w.WriteLine();
            w.WriteLine("Cod de iesire: 0 daca toate cadrele s-au citit, 1 la eroare sau cand un cadru nu s-a putut citi.");
        }
        else
        {
            w.WriteLine("Usage: darkscheck <session folder or folder of darks> [--against DIR] [--lights DIR] [--out DIR] [--threads N] [--max-frames N]");
            w.WriteLine("       darkscheck <folder> --inventory [--lights DIR]");
            w.WriteLine();
            w.WriteLine("  The Siril session folder: the darks are taken from darks\\ (or from the folder");
            w.WriteLine("  itself, when there is no darks\\ and no other session subfolder), the biases");
            w.WriteLine("  from biases\\ and the lights from lights\\, both optional. Camera raw");
            w.WriteLine("  (" + raws + ") or FITS. Nothing is written into the folder.");
            w.WriteLine();
            w.WriteLine("  No options:   reads and measures the biases, the darks and the lights and prints the");
            w.WriteLine("                report: light leaks, the camera's warming, the noise of each frame, hot");
            w.WriteLine("                pixels, what is the same in every frame, dead and saturated pixels, how");
            w.WriteLine("                many darks are enough, how well the dark fits the lights, the index and");
            w.WriteLine("                the label, one table for the darks and one for the lights.");
            w.WriteLine("  --against DIR another folder of darks (a library or a session): its index and by how");
            w.WriteLine("                many degrees the session's darks are warmer or colder than it; a");
            w.WriteLine("                session with lights but no darks takes its darks as its own.");
            w.WriteLine("  --lights DIR  the lights of another folder (a session, from its lights\\, or a folder");
            w.WriteLine("                of lights): compared with the open darks instead of the ones in lights\\.");
            w.WriteLine("  --out DIR     writes into DIR the report, a CSV with every figure for the darks,");
            w.WriteLine("                the biases and the lights, and the maps (" + files);
            w.WriteLine("                " + moreFiles);
            w.WriteLine("  --threads N   how many frames are read at the same time (default: half the");
            w.WriteLine("                cores of the processor).");
            w.WriteLine("  --max-frames N  how many frames of a group are held in memory (default " + maxFrames + ", 48 MB each at 24 MP);");
            w.WriteLine("                if there are more, only some are read, evenly spaced (one in two, for");
            w.WriteLine("                example), and the rest are in the table as skipped; every light is measured.");
            w.WriteLine("  --inventory   only what the files say, without reading the pixels: the time, the");
            w.WriteLine("                exposure and the ISO of each frame, how often the frames were taken and");
            w.WriteLine("                the gaps between them, whether the darks match the lights.");
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
