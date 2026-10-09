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

using LightsCheck.Core;

namespace LightsCheck.Cli;

internal static class Program
{
    private static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>
    /// The whole console, testable: the tests call this with a StringWriter and compare the
    /// text. Exit 0 on a clean run and for --help, 1 on an error or when a frame could not be
    /// read (R-002, R-004, R-017).
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

        try
        {
            if (o.Frame is int frame)
            {
                FrameReport.Write(FrameRun.Run(o.Folder!, frame), stdout);
                return 0;
            }

            Inventory inv = InventoryRun.Run(o.Folder!);
            InventoryReport.Write(inv, stdout);
            return inv.HasErrors ? 1 : 0;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        {
            stderr.WriteLine("EROARE: " + e.Message);
            return 1;
        }
    }

    private static void WriteUsage(TextWriter w)
    {
        w.WriteLine();
        w.WriteLine("Folosire: lightscheck <folder sesiune> [--frame N]");
        w.WriteLine();
        w.WriteLine("  Folderul sesiunii Siril: cadrele se iau din lights\\ (sau direct din folder,");
        w.WriteLine("  daca nu exista lights\\), iar bias-urile, optional, din biases\\. Raw de aparat");
        w.WriteLine("  (" + string.Join(", ", RawReader.RawExtensions) + ") sau FITS. Nimic nu e scris in folder.");
        w.WriteLine();
        w.WriteLine("  Fara optiuni: inventarul cadrelor (metadate, interval, pauze).");
        w.WriteLine("  --frame N     masoara cadrul N (1 = primul din lights\\): nivele, fond, zgomot,");
        w.WriteLine("                stele (FWHM, alungire, unghi), halo, dare.");
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
