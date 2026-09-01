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

namespace CopyWcs;

internal sealed class Options
{
    public string Source = "";
    public string Dest = "";
    public string? Output;
    public bool DryRun;
    public bool Force;
    public bool NoOptical;
    public bool ShowHelp;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        var positional = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h":
                case "--help":
                    o.ShowHelp = true;
                    return o;
                case "-o":
                case "--output":
                    if (++i >= args.Length) throw new ArgumentException("--output cere un nume de fisier.");
                    o.Output = args[i];
                    break;
                case "--dry-run":
                    o.DryRun = true;
                    break;
                case "--force":
                    o.Force = true;
                    break;
                case "--no-optical":
                    o.NoOptical = true;
                    break;
                default:
                    if (args[i].StartsWith('-'))
                        throw new ArgumentException($"Optiune necunoscuta: {args[i]}");
                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count != 2)
            throw new ArgumentException("Sunt necesare exact doua fisiere: sursa si destinatie.");

        o.Source = positional[0];
        o.Dest = positional[1];
        return o;
    }

    public static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("""
            Copiaza WCS + metadate optice dintr-un FITS sursa in unul destinatie.

              copywcs <sursa.fit> <destinatie.fit> [optiuni]

            Optiuni:
              -o, --output <fisier>  Scrie intr-un fisier nou in loc sa modifice destinatia in loc
                  --dry-run          Arata ce s-ar copia, fara sa scrie nimic
                  --force            Continua chiar daca dimensiunile imaginilor difera
                  --no-optical       Copiaza doar cheile WCS, fara metadatele optice
              -h, --help             Afiseaza acest mesaj
            """);

        // GPLv3 sectiunea 5(d) - notita de garantie. Prima linie a fiecarei rulari poarta
        // doar copyright-ul; disclaimerul intreg ar ingrosa fiecare iesire degeaba, deci
        // sta aici, unde omul vine oricum sa citeasca.
        writer.WriteLine();
        writer.WriteLine("""
            Acest program vine FARA NICIO GARANTIE, nici macar cea implicita de
            vandabilitate sau potrivire pentru un scop anume. E software liber:
            il poti redistribui si modifica in termenii GPLv3 (sau, la alegerea
            ta, orice versiune ulterioara). Textul complet e in fisierul LICENSE,
            sau la <https://www.gnu.org/licenses/gpl-3.0.html>.
            """);
    }
}
