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

using static CopyWcs.Ui;

namespace CopyWcs;

/// <summary>The console's options, parsed from the command line after <c>--lang</c> was taken out.</summary>
internal sealed class Options
{
    /// <summary>The path of the plate-solved source FITS.</summary>
    public string Source = "";

    /// <summary>The path of the destination FITS.</summary>
    public string Dest = "";

    /// <summary>The path of a new file to write (<c>-o</c>); null to rewrite the destination in place.</summary>
    public string? Output;

    /// <summary>True for <c>--dry-run</c>: list the cards, write nothing.</summary>
    public bool DryRun;

    /// <summary>True for <c>--force</c>: copy even when the dimensions differ.</summary>
    public bool Force;

    /// <summary>True for <c>--no-optical</c>: the WCS keys only.</summary>
    public bool NoOptical;

    /// <summary>True for <c>-h</c> or <c>--help</c>; parsing stops at it.</summary>
    public bool ShowHelp;

    /// <summary>
    /// Parses the arguments: two positional paths, the source then the destination, and the
    /// flags in any position. <c>-h</c> stops the parsing once the parser reaches it.
    /// </summary>
    /// <param name="args">The arguments, without <c>--lang</c> and its value; not null.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentException">An unknown option, <c>-o</c> without a value, or
    /// not exactly two paths; the message is one line in the run's language.</exception>
    public static Options Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

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
                    if (++i >= args.Length)
                    {
                        throw new ArgumentException(T("--output needs a file name.", "--output cere un nume de fisier."));
                    }

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
                    {
                        throw new ArgumentException(T("Unknown option: ", "Optiune necunoscuta: ") + args[i]);
                    }

                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count != 2)
        {
            throw new ArgumentException(T(
                "Exactly two files are needed: the source and the destination.",
                "Sunt necesare exact doua fisiere: sursa si destinatie."));
        }

        o.Source = positional[0];
        o.Dest = positional[1];
        return o;
    }

    /// <summary>
    /// Writes the usage in the run's language, each language whole since its columns are
    /// aligned by hand, then the warranty notice GPLv3 section 5(d) asks for. The notice
    /// lives here and not under the banner on purpose: five lines on top of every run would
    /// drown the actual output, and the person who reads the usage reads it too.
    /// </summary>
    /// <param name="writer">Where the usage goes; stdout.</param>
    public static void PrintUsage(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (Current == UiLanguage.Romanian)
        {
            writer.WriteLine("""
                Copiaza WCS + metadate optice dintr-un FITS sursa in unul destinatie.

                  copywcs <sursa.fit> <destinatie.fit> [optiuni]

                Optiuni:
                  -o, --output <fisier>  Scrie intr-un fisier nou in loc sa modifice destinatia in loc
                      --dry-run          Arata ce s-ar copia, fara sa scrie nimic
                      --force            Continua chiar daca dimensiunile imaginilor difera
                      --no-optical       Copiaza doar cheile WCS, fara metadatele optice
                      --lang en|ro       Limba textelor: en (engleza, implicit) sau ro (romana)
                  -h, --help             Afiseaza acest mesaj
                """);

            writer.WriteLine();
            writer.WriteLine("""
                Acest program vine FARA NICIO GARANTIE, nici macar cea implicita de
                vandabilitate sau potrivire pentru un scop anume. E software liber:
                il poti redistribui si modifica in termenii GPLv3 (sau, la alegerea
                ta, orice versiune ulterioara). Textul complet e in fisierul LICENSE,
                sau la <https://www.gnu.org/licenses/gpl-3.0.html>.
                """);
            return;
        }

        writer.WriteLine("""
            Copies the WCS and the optical metadata from a source FITS into a destination one.

              copywcs <source.fit> <destination.fit> [options]

            Options:
              -o, --output <file>    Write a new file instead of changing the destination in place
                  --dry-run          Show what would be copied, without writing anything
                  --force            Continue even when the images' dimensions differ
                  --no-optical       Copy only the WCS keys, not the optical metadata
                  --lang en|ro       The language of the texts: en (English, the default) or ro (Romanian)
              -h, --help             Show this message
            """);

        writer.WriteLine();
        writer.WriteLine("""
            This program comes with ABSOLUTELY NO WARRANTY, not even the implied
            warranty of merchantability or fitness for a particular purpose. It is
            free software: you may redistribute and modify it under the terms of
            the GPLv3 (or, at your option, any later version). The full text is in
            the LICENSE file, or at <https://www.gnu.org/licenses/gpl-3.0.html>.
            """);
    }
}
