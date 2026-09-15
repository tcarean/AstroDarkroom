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

// SirilLogLine.cs - o linie de pe stdout-ul lui siril-cli, citita.
//
// Masurat pe Siril 1.4.4, cu scriptul WithoutDark pe o sesiune de 3+3+3 cadre: 1735 de
// linii, din care 1324 sunt actualizari de progres. Vin in patru feluri:
//
//   log: <text>                       - ce arata si panoul de log din fereastra Siril
//   progress: <eticheta>, 33.33%      - inceputul unei operatii cu progres
//   ESC[A ESC[2K Tprogress: 66.67%    - actualizarea ei, cu "urca un rand, sterge-l"
//                                       in fata, ca in consola sa se suprascrie
//   <orice altceva>                   - tipariri de depanare (blocuri, "running command"
//                                       cu un numar, "seqfile ... already exists")
//
// Fereastra arata doar primul fel, ca Siril, si transforma al doilea si al treilea intr-o
// bara. Al patrulea nu se arata: sunt sute, si nu spun omului nimic ce n-ar spune log-ul.
// Parsarea e pura si pinuita pe linii inregistrate, nu pe ce ar trebui sa scrie Siril.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FlatCheck.Wpf
{
    internal enum SirilLineKind { Log, Progress, Other }

    internal readonly record struct SirilLogLine(SirilLineKind Kind, string Text, string Label, double Percent)
    {
        /// <summary>Prefixul cu care Siril anunta comanda urmatoare, in liniile de log.</summary>
        internal const string RunningCommand = "Running command: ";

        static readonly Regex Ansi = new(@"\x1b\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
        static readonly Regex Progress = new(@"^T?progress: (?:(?<label>.*), )?(?<pct>[0-9]+(?:\.[0-9]+)?)%$",
                                             RegexOptions.Compiled);

        internal static SirilLogLine Parse(string raw)
        {
            if (raw == null) return new SirilLogLine(SirilLineKind.Other, "", "", double.NaN);

            string line = Ansi.Replace(raw, "").TrimEnd('\r', '\n');

            if (line.StartsWith("log: ", StringComparison.Ordinal))
                return new SirilLogLine(SirilLineKind.Log, line.Substring(5), "", double.NaN);

            Match m = Progress.Match(line);
            if (m.Success)
            {
                double pct = double.Parse(m.Groups["pct"].Value, CultureInfo.InvariantCulture);
                return new SirilLogLine(SirilLineKind.Progress, line, m.Groups["label"].Value, pct);
            }

            return new SirilLogLine(SirilLineKind.Other, line, "", double.NaN);
        }

        /// <summary>true cand linia de log anunta o comanda; verbul ei e in <paramref name="verb"/>.</summary>
        internal bool IsCommandStart(out string verb)
        {
            verb = null;
            if (Kind != SirilLineKind.Log || !Text.StartsWith(RunningCommand, StringComparison.Ordinal)) return false;
            verb = Text.Substring(RunningCommand.Length).Trim();
            return true;
        }
    }
}
