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
/// it is one of the verdict words of either language (OK, WARNING or UNMEASURED; OK, ATENTIE or
/// NEMASURAT), or WARNING / ATENTIE when that is the row's label (the warnings about frames that
/// failed); <see cref="Rest"/> is the value without a leading word.
/// </summary>
/// <param name="Label">The row's label, in the run's language.</param>
/// <param name="Value">The row's text, in the run's language, often opening with a verdict
/// word.</param>
/// <param name="Bold">The substrings of the value the window renders bold, or null for
/// none.</param>
internal sealed record ReportRow(string Label, string Value, IReadOnlyList<string>? Bold = null)
{
    // Both languages' words, so a row is read the same whatever language wrote it (ADR 0021).
    private static readonly string[] Words = ["NEMASURAT", "UNMEASURED", "ATENTIE", "WARNING", "OK"];

    /// <summary>
    /// The value's substrings the window renders bold (ADR 0016): the masters' noise and the light
    /// found; empty for most rows. The console prints the value as it is.
    /// </summary>
    internal IReadOnlyList<string> BoldParts => Bold ?? [];

    internal string? Verdict => VerdictOf(Value) ?? (Label is "ATENTIE" or "WARNING" ? Label : null);

    /// <summary>
    /// The value without its verdict word and the ": " or " " that follows it; the whole value when
    /// there is no word.
    /// </summary>
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

    /// <summary>
    /// OK, WARNING, UNMEASURED, ATENTIE or NEMASURAT when the text opens with it as a word, else
    /// null.
    /// </summary>
    /// <param name="value">The text; not null.</param>
    /// <returns>The verdict word the text opens with, followed by its end, ':' or ' '; null when it
    /// opens with none.</returns>
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
/// <param name="Title">The section's title, as the console prints it ("2. LIGHT LEAKS AND
/// GRADIENT").</param>
/// <param name="Rows">The section's rows, in order.</param>
internal sealed record ReportSection(string Title, IReadOnlyList<ReportRow> Rows);

/// <summary>
/// One dark of the frames table, every cell already formatted; Skipped or Error set when the frame
/// has no figures; Plateau is the frame's degrees from the plateau of R-023 (`-` without a fit) and
/// Note the reason a frame off the plateau is one to discard (null for every other frame), printed
/// after the last column and shown by the window in the attention colour.
/// </summary>
/// <param name="Index">The frame's number in the series, from 1.</param>
/// <param name="FileName">The file's name.</param>
/// <param name="Skipped">Why the frame was skipped (the frame cap), or null.</param>
/// <param name="Error">Why the frame could not be decoded, or null.</param>
/// <param name="Time">The time of day, HH:mm:ss, or "-".</param>
/// <param name="Mean">The mean above the black, in raw ADU.</param>
/// <param name="Sigma">The frame's noise on the reference plane, in raw ADU.</param>
/// <param name="Residual">The sigma of the frame minus the master, in raw ADU.</param>
/// <param name="Bands">The row banding over the white noise.</param>
/// <param name="Amp">The median of the master's hot pixels in this frame over the master, or
/// "-".</param>
/// <param name="Plateau">The degrees from the series' plateau, signed, or "-".</param>
/// <param name="Light">The light above the warming line of R-028, signed, in ADU-eq (ADU when the
/// factor is 1), or "-".</param>
/// <param name="Side">The offset of check 2's worst side from the centre, minus the bias's, signed,
/// in ADU-eq, or "-".</param>
/// <param name="Above">How many pixels of the reference plane lie above the hot level.</param>
/// <param name="Note">Why a frame off the plateau is one to discard, or null.</param>
internal sealed record DarkTableRow(int Index, string FileName, string? Skipped, string? Error, string Time, string Mean, string Sigma, string Residual, string Bands, string Amp, string Plateau, string Light, string Side, string Above, string? Note = null);

/// <summary>
/// One light of the lights table, every cell already formatted; Skipped or Error set when the light
/// has no figures.
/// </summary>
/// <param name="Index">The light's number, from 1.</param>
/// <param name="FileName">The file's name.</param>
/// <param name="Skipped">Why the light was skipped (the frame cap), or null.</param>
/// <param name="Error">Why the light could not be decoded, or null.</param>
/// <param name="Time">The time of day, HH:mm:ss, or "-".</param>
/// <param name="Sky">The sky's median above the black on the reference plane, in ADU.</param>
/// <param name="LocalSigma">The sigma of the local excess, the detector's scale.</param>
/// <param name="Candidates">How many pixels passed the detector's threshold.</param>
/// <param name="Isolated">How many of them are isolated hot pixels.</param>
/// <param name="Amp">The median ratio of the light to the dark at the master's hot pixels, or
/// "-".</param>
/// <param name="Q25">The first quartile of that ratio, or "-".</param>
/// <param name="Q75">The third quartile of that ratio, or "-".</param>
internal sealed record LightTableRow(int Index, string FileName, string? Skipped, string? Error, string Time, string Sky, string LocalSigma, string Candidates, string Isolated, string Amp, string Q25, string Q75);
