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

using DarksCheck.Core;

namespace DarksCheck.Cli;

internal static class Program
{
    private static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>
    /// The whole console, testable: the tests call this with a StringWriter and compare the
    /// text. Exit 0 on a clean run and for --help, 1 on an error or when a frame could not be
    /// read (R-002, R-003, R-004).
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
            // Until the analysis of the frames exists (plan steps 2 and 3), a run without
            // options is the inventory; --inventory will keep the metadata-only run after.
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
        w.WriteLine("Folosire: darkscheck <folder sesiune sau folder de dark-uri>");
        w.WriteLine("          darkscheck <folder> --inventory");
        w.WriteLine();
        w.WriteLine("  Folderul sesiunii Siril: dark-urile se iau din darks\\ (sau direct din folder,");
        w.WriteLine("  daca nu exista darks\\ si nici alt subfolder de sesiune), bias-urile din biases\\");
        w.WriteLine("  si lights-urile din lights\\, amandoua optionale. Raw de aparat");
        w.WriteLine("  (" + string.Join(", ", RawReader.RawExtensions) + ") sau FITS. Nimic nu e scris in folder.");
        w.WriteLine();
        w.WriteLine("  --inventory   doar metadatele cadrelor (fara decodare): ora, expunere, ISO,");
        w.WriteLine("                pauzele si cadenta fiecarui grup, potrivirea dark-urilor cu");
        w.WriteLine("                lights-urile. Fara optiuni, deocamdata, acelasi lucru.");
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
