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

namespace DarksCheck.Cli;

internal static class Program
{
    private static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>
    /// The whole console, testable: the tests call this with a StringWriter and compare the
    /// text. Exit 0 on a clean run and for --help, 1 on an error or when a frame could not be
    /// read (R-002, R-003, R-004, R-025).
    /// </summary>
    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        RunReport.WriteBanner(stdout);

        Options o = Options.Parse(args);
        if (o.Help)
        {
            WriteUsage(stdout);
            return 0;
        }

        if (o.Error is not null)
        {
            stderr.WriteLine("EROARE: " + o.Error);
            return 1;
        }

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

            if (o.Out is not null)
            {
                // refused before the minutes of decoding, not after (R-026)
                DarkOutputs.CheckOutDir(SessionFolder.Open(o.Folder!).Root, o.Out);
            }

            DarkSeriesResult r = DarkSeriesRun.Run(o.Folder!, new DarkOptions(o.Threads, o.MaxFrames), progress);
            progress.Clear();
            DarkReport.Write(r, stdout);
            if (o.Out is not null)
            {
                IReadOnlyList<string> written = DarkOutputs.WriteAll(r, o.Out);
                stdout.WriteLine();
                foreach (string p in written)
                {
                    stdout.WriteLine($"    -> scris: {p}");
                }
            }

            return r.HasErrors ? 1 : 0;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException or AggregateException)
        {
            // The analysis runs frames in parallel; a failure inside one lands here wrapped.
            // The progress line, if one was being written, is cleared first.
            string message = e is AggregateException a ? string.Join("; ", a.Flatten().InnerExceptions.Select(x => x.Message).Distinct(StringComparer.Ordinal)) : e.Message;
            progress.Clear();
            stderr.WriteLine("EROARE: " + message);
            return 1;
        }
    }

    /// <summary>Progress written as it happens, on the calling thread, serialised by a lock.</summary>
    private sealed class ConsoleProgress(TextWriter stderr) : IProgress<RunProgress>
    {
        private readonly Lock _lock = new();
        private bool _shown;

        public void Report(RunProgress value)
        {
            lock (_lock)
            {
                _shown = true;
                stderr.Write($"\r    {value.Phase}: {value.Done.ToString(CultureInfo.InvariantCulture)} / {value.Total.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        /// <summary>Wipes the progress line, when one was written, so the report or an error starts on a clean line.</summary>
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

    private static void WriteUsage(TextWriter w)
    {
        w.WriteLine();
        w.WriteLine("Folosire: darkscheck <folder sesiune sau folder de dark-uri> [--out DIR] [--threads N] [--max-frames N]");
        w.WriteLine("          darkscheck <folder> --inventory");
        w.WriteLine();
        w.WriteLine("  Folderul sesiunii Siril: dark-urile se iau din darks\\ (sau direct din folder,");
        w.WriteLine("  daca nu exista darks\\ si nici alt subfolder de sesiune), bias-urile din biases\\");
        w.WriteLine("  si lights-urile din lights\\, amandoua optionale. Raw de aparat");
        w.WriteLine("  (" + string.Join(", ", RawReader.RawExtensions) + ") sau FITS. Nimic nu e scris in folder.");
        w.WriteLine();
        w.WriteLine("  Fara optiuni: decodeaza dark-urile (si bias-urile) si tipareste raportul:");
        w.WriteLine("                scurgeri de lumina, deriva termica, zgomot, pixeli fierbinti,");
        w.WriteLine("                model fix, pixeli morti, cate dark-uri ajung, un tabel pe cadre.");
        w.WriteLine("  --out DIR     scrie in DIR raportul, cate un CSV cu toate cifrele pe dark-uri si");
        w.WriteLine("                pe bias-uri si hartile (" + DarkOutputs.ReportName + ", " + DarkOutputs.DarksCsvName + ",");
        w.WriteLine("                " + DarkOutputs.BiasesCsvName + ", " + DarkOutputs.DarkMapName + ", ...).");
        w.WriteLine("  --threads N   cate cadre se decodeaza in paralel (implicit jumatate din nuclee).");
        w.WriteLine("  --max-frames N  cate cadre se tin in memorie (implicit " + Th.MaxFramesInMemory.ToString(CultureInfo.InvariantCulture) + ", 48 MB fiecare la 24 MP);");
        w.WriteLine("                peste, se decodeaza unul din k si restul sunt listate ca sarite.");
        w.WriteLine("  --inventory   doar metadatele cadrelor (fara decodare): ora, expunere, ISO,");
        w.WriteLine("                pauzele si cadenta fiecarui grup, potrivirea dark-urilor cu");
        w.WriteLine("                lights-urile.");
        w.WriteLine("  -h, --help    acest text");
        w.WriteLine();
        w.WriteLine("Cod de iesire: 0 daca toate cadrele s-au citit, 1 la eroare sau cand un cadru nu s-a putut citi.");
        w.WriteLine();
        foreach (string line in RunReport.WarrantyLines)
        {
            w.WriteLine(line);
        }
    }
}
