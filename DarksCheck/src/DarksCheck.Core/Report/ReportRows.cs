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

namespace DarksCheck.Core;

/// <summary>
/// One labelled line of the report, as the console prints it (`label : value`) and as the
/// window binds it (ADR 0009): <see cref="Verdict"/> is the word the value opens with when
/// it is one of the three verdict words, or ATENTIE when that is the row's label (the
/// warnings about frames that failed); <see cref="Rest"/> is the value without a leading word.
/// </summary>
internal sealed record ReportRow(string Label, string Value, IReadOnlyList<string>? Bold = null)
{
    private static readonly string[] Words = ["NEMASURAT", "ATENTIE", "OK"];

    /// <summary>The value's substrings the window renders bold (ADR 0016): the masters' noise and the light found; empty for most rows. The console prints the value as it is.</summary>
    internal IReadOnlyList<string> BoldParts => Bold ?? [];

    internal string? Verdict => VerdictOf(Value) ?? (Label == "ATENTIE" ? "ATENTIE" : null);

    /// <summary>The value without its verdict word and the ": " or " " that follows it; the whole value when there is no word.</summary>
    internal string Rest
    {
        get
        {
            string? word = VerdictOf(Value);
            if (word is null)
            {
                return Value;
            }

            string rest = Value[word.Length..];
            return rest.StartsWith(": ", StringComparison.Ordinal) ? rest[2..] : rest.TrimStart();
        }
    }

    /// <summary>OK, ATENTIE or NEMASURAT when the text opens with it as a word, else null.</summary>
    internal static string? VerdictOf(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        foreach (string w in Words)
        {
            if (value.StartsWith(w, StringComparison.Ordinal) && (value.Length == w.Length || value[w.Length] is ':' or ' '))
            {
                return w;
            }
        }

        return null;
    }
}

/// <summary>A titled group of rows: one check of the report, or its INDEX block.</summary>
internal sealed record ReportSection(string Title, IReadOnlyList<ReportRow> Rows);

/// <summary>One dark of the frames table, every cell already formatted; Skipped or Error set when the frame has no figures.</summary>
internal sealed record DarkTableRow(int Index, string FileName, string? Skipped, string? Error, string Time, string Mean, string Sigma, string Residual, string Bands, string Amp, string Light, string Side, string Above);

/// <summary>One light of the lights table, every cell already formatted; Skipped or Error set when the light has no figures.</summary>
internal sealed record LightTableRow(int Index, string FileName, string? Skipped, string? Error, string Time, string Sky, string LocalSigma, string Candidates, string Isolated, string Amp, string Q25, string Q75);
