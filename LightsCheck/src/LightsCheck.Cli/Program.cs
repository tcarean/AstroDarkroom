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
using System.Text;
using LightsCheck.Core;

namespace LightsCheck.Cli;

/// <summary>
/// The console, <c>lightscheck.exe</c>: parses the options, calls the core's runs and writes their
/// reports; it computes nothing itself.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The entry point: switches the console's output to UTF-8 for the run, runs
    /// <see cref="Run"/> on the real console, and puts the code page back.
    /// </summary>
    /// <param name="args">The command line, as <see cref="Options.Parse"/> reads it.</param>
    /// <returns>
    /// The exit code of <see cref="Run"/>: 0 on a clean run or for --help, 1 on an error.
    /// </returns>
    private static int Main(string[] args)
    {
        // The report carries the SynScan arrows as symbols (R-040). A console window's own code
        // page would print them as '?', so it is switched to UTF-8 for the run and put back
        // after (the code page outlives the process otherwise); a run without a console, or
        // with its output in a file or a pipe, writes UTF-8 bytes either way.
        Encoding? previous = null;
        try
        {
            previous = Console.OutputEncoding;
            Console.OutputEncoding = new UTF8Encoding(false);
        }
        catch (IOException)
        {
            // no console attached: nothing to switch
        }

        try
        {
            return Run(args, Console.Out, Console.Error);
        }
        finally
        {
            if (previous is not null)
            {
                try
                {
                    Console.OutputEncoding = previous;
                }
                catch (IOException)
                {
                    // the console went away during the run
                }
            }
        }
    }

    /// <summary>
    /// The whole console, testable: the tests call this with a StringWriter and compare the
    /// text. Exit 0 on a clean run and for --help, 1 on an error or when a frame could not be
    /// read (R-002, R-004, R-017, R-026).
    /// </summary>
    /// <param name="args">The command line, without the program's name; not null.</param>
    /// <param name="stdout">Where the banner, the help and the report go; not null.</param>
    /// <param name="stderr">Where the errors and the progress line go; not null.</param>
    /// <returns>The exit code: 0 or 1.</returns>
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
            stderr.WriteLine(RunReport.Error(o.Error));
            return 1;
        }

        try
        {
            if (o.Frame is int frame)
            {
                FrameReport.Write(FrameRun.Run(o.Folder!, frame), stdout);
                return 0;
            }

            if (o.Inventory)
            {
                Inventory inv = InventoryRun.Run(o.Folder!);
                InventoryReport.Write(inv, stdout);
                return inv.HasErrors ? 1 : 0;
            }

            // Progress on stderr, one line rewritten, so stdout stays the report alone. Written
            // synchronously on the reporting thread (Progress<T> would post to the thread pool and
            // could print after the report), under a lock since the frames finish in parallel.
            SeriesResult r = SeriesRun.Run(o.Folder!, new SeriesOptions(o.PixelSizeUm, o.Threads, o.Arrows), new ConsoleProgress(stderr));
            stderr.Write("\r                        \r");
            SeriesReport.Write(r, stdout);
            if (o.Out is not null)
            {
                IReadOnlyList<string> written = SeriesOutputs.WriteAll(r, o.Out);
                stdout.WriteLine();
                foreach (string p in written)
                {
                    stdout.WriteLine($"    -> scris: {p}");
                }
            }

            return r.HasErrors ? 1 : 0;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        {
            stderr.WriteLine(RunReport.Error(e.Message));
            return 1;
        }
    }

    /// <summary>
    /// Progress written as it happens, on the calling thread, serialised by a lock.
    /// </summary>
    /// <param name="stderr">The writer the progress line is rewritten on.</param>
    private sealed class ConsoleProgress(TextWriter stderr) : IProgress<(int Done, int Total)>
    {
        private readonly object _lock = new();

        /// <summary>Rewrites the progress line with the frames measured so far.</summary>
        /// <param name="value">The frames done and the frames in all.</param>
        public void Report((int Done, int Total) value)
        {
            lock (_lock)
            {
                stderr.Write($"\r    cadrul {value.Done.ToString(CultureInfo.InvariantCulture)} / {value.Total.ToString(CultureInfo.InvariantCulture)}");
            }
        }
    }

    /// <summary>
    /// Writes the Romanian help text: the usage, the options, the exit code and the warranty
    /// notice.
    /// </summary>
    /// <param name="w">The writer the help goes to.</param>
    private static void WriteUsage(TextWriter w)
    {
        w.WriteLine();
        w.WriteLine("Folosire: lightscheck <folder sesiune> [--out DIR] [--threads N] [--pixel UM] [--est stanga|dreapta] [--nord sus|jos]");
        w.WriteLine("          lightscheck <folder sesiune> --inventory");
        w.WriteLine("          lightscheck <folder sesiune> --frame N");
        w.WriteLine();
        w.WriteLine("  Folderul sesiunii Siril: cadrele se iau din lights\\ (sau direct din folder,");
        w.WriteLine("  daca nu exista lights\\), iar bias-urile, optional, din biases\\. Raw de aparat");
        w.WriteLine("  (" + string.Join(", ", RawReader.RawExtensions) + ") sau FITS. Nimic nu e scris in folder.");
        w.WriteLine();
        w.WriteLine("  Fara optiuni: masoara toate cadrele si tipareste raportul: focus, expunere si");
        w.WriteLine("                zgomot, roua, cadre miscate, drift, dithering, lista de respins.");
        w.WriteLine("  --out DIR     scrie in DIR raportul, un CSV cu toate cifrele si lista de respins");
        w.WriteLine("                (" + SeriesOutputs.ReportName + ", " + SeriesOutputs.CsvName + ", " + SeriesOutputs.RejectedName + ").");
        w.WriteLine("  --threads N   cate cadre se masoara in paralel (implicit jumatate din nuclee).");
        w.WriteLine("  --pixel UM    marimea pixelului in microni, cand fisierele n-o spun (implicit " + RunReport.F(SeriesOptions.D3300PixelUm, 2) + ", Nikon D3300).");
        w.WriteLine("  --est stanga|dreapta  sageata SynScan care muta montura spre est (implicit dreapta); cealalta e vest.");
        w.WriteLine("  --nord sus|jos        sageata SynScan care muta montura spre nord (implicit sus); cealalta e sud.");
        w.WriteLine("  --inventory   doar metadatele cadrelor (fara decodare): ora, expunere, ISO, pauze.");
        w.WriteLine("  --frame N     masoara un singur cadru (1 = primul din lights\\).");
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
