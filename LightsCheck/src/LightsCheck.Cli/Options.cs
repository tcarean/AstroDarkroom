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
using LightsCheck.Core;

namespace LightsCheck.Cli;

/// <summary>
/// The console's arguments: one folder, the mode (the whole series by default,
/// <c>--inventory</c>, <c>--frame N</c>), <c>--out DIR</c>, <c>--threads N</c>,
/// <c>--pixel UM</c>, <c>--est stanga|dreapta</c>, <c>--nord sus|jos</c>, or --help. Anything else
/// is an error naming the argument (R-004).
/// </summary>
internal sealed class Options
{
    internal string? Folder { get; private set; }
    internal int? Frame { get; private set; }
    internal bool Inventory { get; private set; }
    internal string? Out { get; private set; }
    internal int Threads { get; private set; }
    internal double PixelSizeUm { get; private set; } = SeriesOptions.D3300PixelUm;
    internal ArrowKeys Arrows { get; private set; } = ArrowKeys.Default;
    internal bool Help { get; private set; }
    internal string? Error { get; private set; }

    /// <summary>
    /// Reads the command line. No argument, <c>-h</c> or <c>--help</c> sets
    /// <see cref="Help"/>; the first wrong argument stops the reading and sets
    /// <see cref="Error"/> to a Romanian message naming it. Never throws for a bad argument.
    /// </summary>
    /// <param name="args">The command line, without the program's name; not null.</param>
    /// <returns>
    /// The options read, with <see cref="Help"/> or <see cref="Error"/> set when the run must not
    /// start.
    /// </returns>
    internal static Options Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        Options o = new();
        if (args.Length == 0)
        {
            o.Help = true;
            return o;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-h" or "--help")
            {
                o.Help = true;
                return o;
            }

            if (a == "--inventory")
            {
                o.Inventory = true;
                continue;
            }

            if (a is "--frame" or "--threads" or "--pixel" or "--out" or "--est" or "--nord")
            {
                if (i + 1 >= args.Length)
                {
                    o.Error = $"{a} cere o valoare";
                    return o;
                }

                string v = args[++i];
                switch (a)
                {
                    case "--frame":
                        if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 1)
                        {
                            o.Error = "--frame cere numarul cadrului (1 = primul din lights\\)";
                            return o;
                        }

                        o.Frame = n;
                        break;
                    case "--threads":
                        if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int t) || t < 1)
                        {
                            o.Error = "--threads cere un numar de fire, cel putin 1";
                            return o;
                        }

                        o.Threads = t;
                        break;
                    case "--pixel":
                        if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double p) || p <= 0)
                        {
                            o.Error = "--pixel cere marimea pixelului in microni, de exemplu 3.92";
                            return o;
                        }

                        o.PixelSizeUm = p;
                        break;
                    case "--est":
                        if (v is not ("stanga" or "dreapta"))
                        {
                            o.Error = "--est cere stanga sau dreapta: sageata SynScan care muta montura spre est";
                            return o;
                        }

                        o.Arrows = o.Arrows with { East = v == "stanga" ? Arrow.Left : Arrow.Right };
                        break;
                    case "--nord":
                        if (v is not ("sus" or "jos"))
                        {
                            o.Error = "--nord cere sus sau jos: sageata SynScan care muta montura spre nord";
                            return o;
                        }

                        o.Arrows = o.Arrows with { North = v == "sus" ? Arrow.Up : Arrow.Down };
                        break;
                    default:
                        if (string.IsNullOrWhiteSpace(v))
                        {
                            o.Error = "--out cere un folder";
                            return o;
                        }

                        o.Out = v;
                        break;
                }

                continue;
            }

            if (a.StartsWith('-'))
            {
                o.Error = $"optiune necunoscuta: {a}";
                return o;
            }

            if (string.IsNullOrWhiteSpace(a))
            {
                o.Error = "folderul sesiunii e gol; da calea folderului";
                return o;
            }

            if (o.Folder is not null)
            {
                o.Error = $"un singur folder se poate da; al doilea a fost {a}";
                return o;
            }

            o.Folder = a;
        }

        if (o.Folder is null)
        {
            o.Error = "lipseste folderul sesiunii";
        }
        else if (o.Inventory && o.Frame is not null)
        {
            o.Error = "--inventory si --frame nu merg impreuna";
        }

        return o;
    }
}
