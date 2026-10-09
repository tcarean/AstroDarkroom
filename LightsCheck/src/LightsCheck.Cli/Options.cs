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

namespace LightsCheck.Cli;

/// <summary>
/// The console's arguments: one folder, optionally <c>--frame N</c>, or --help. Anything else
/// is an error naming the argument (R-004).
/// </summary>
internal sealed class Options
{
    internal string? Folder { get; private set; }
    internal int? Frame { get; private set; }
    internal bool Help { get; private set; }
    internal string? Error { get; private set; }

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

            if (a == "--frame")
            {
                if (i + 1 >= args.Length || !int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 1)
                {
                    o.Error = "--frame cere numarul cadrului (1 = primul din lights\\)";
                    return o;
                }

                o.Frame = n;
                i++;
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

        return o;
    }
}
