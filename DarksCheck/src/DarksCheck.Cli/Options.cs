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
using static DarksCheck.Core.Ui;

namespace DarksCheck.Cli;

/// <summary>
/// The console's arguments: one folder, the mode (the darks analysis by default,
/// <c>--inventory</c> for the metadata alone), <c>--out DIR</c>, <c>--threads N</c>,
/// <c>--max-frames N</c>, <c>--against DIR</c>, or --help. Anything else is an error naming
/// the argument (R-004).
/// </summary>
internal sealed class Options
{
    internal string? Folder { get; private set; }
    internal bool Inventory { get; private set; }
    internal string? Out { get; private set; }
    internal int Threads { get; private set; }
    internal int MaxFrames { get; private set; } = DarksCheck.Core.Th.MaxFramesInMemory;
    internal string? Against { get; private set; }
    internal bool Help { get; private set; }
    internal string? Error { get; private set; }

    /// <summary>
    /// Reads the command line into options. Stops at the first fault: the result then carries
    /// <see cref="Error"/> (a message for the user, in the run's language) and the fields read so far. No
    /// argument at all, <c>-h</c> or <c>--help</c> sets <see cref="Help"/> and nothing else is
    /// checked.
    /// </summary>
    /// <param name="args">The arguments as the process received them; not null.</param>
    /// <returns>
    /// The parsed options, never null: <see cref="Help"/>, or <see cref="Error"/>, or a folder with
    /// its flags.
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

            if (a is "--out" or "--threads" or "--max-frames" or "--against")
            {
                if (i + 1 >= args.Length)
                {
                    o.Error = T($"{a} expects a value", $"{a} cere o valoare");
                    return o;
                }

                string v = args[++i];
                if (a == "--threads")
                {
                    if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int t) || t < 1)
                    {
                        o.Error = T("--threads expects a number of threads, at least 1", "--threads cere un numar de fire, cel putin 1");
                        return o;
                    }

                    o.Threads = t;
                }
                else if (a == "--max-frames")
                {
                    if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int m) || m < 1)
                    {
                        o.Error = T("--max-frames expects a number of frames, at least 1", "--max-frames cere un numar de cadre, cel putin 1");
                        return o;
                    }

                    o.MaxFrames = m;
                }
                else if (a == "--against")
                {
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        o.Error = T("--against expects a folder of darks", "--against cere un folder de dark-uri");
                        return o;
                    }

                    o.Against = v;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        o.Error = T("--out expects a folder", "--out cere un folder");
                        return o;
                    }

                    o.Out = v;
                }

                continue;
            }

            if (a.StartsWith('-'))
            {
                o.Error = T($"unknown option: {a}", $"optiune necunoscuta: {a}");
                return o;
            }

            if (string.IsNullOrWhiteSpace(a))
            {
                o.Error = T("the folder is blank; give the path of the session folder or of the folder of darks", "folderul e gol; da calea folderului sesiunii sau al dark-urilor");
                return o;
            }

            if (o.Folder is not null)
            {
                o.Error = T($"only one folder can be given; the second one was {a}", $"un singur folder se poate da; al doilea a fost {a}");
                return o;
            }

            o.Folder = a;
        }

        if (o.Folder is null)
        {
            o.Error = T("the session folder or the folder of darks is missing", "lipseste folderul sesiunii sau al dark-urilor");
        }
        else if (o.Inventory && (o.Out is not null || o.Against is not null))
        {
            o.Error = T("--inventory does not go with --out or --against", "--inventory nu merge cu --out sau --against");
        }

        return o;
    }
}
