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
/// failed); <see cref="Rest"/> is the value without a leading word. A row may also carry its
/// statements as <see cref="Lines"/>, its figures as a <see cref="Table"/>, and whether the
/// Rezumat's card shows it (<see cref="Summary"/>); the console prints the value alone.
/// </summary>
/// <param name="Label">The row's label, in the run's language.</param>
/// <param name="Value">The row's text, in the run's language, often opening with a verdict
/// word.</param>
/// <param name="Bold">The substrings of the value the window renders bold, or null for
/// none.</param>
internal sealed record ReportRow(string Label, string Value, IReadOnlyList<string>? Bold = null)
{
    // Both languages' words, so a row is read the same whatever language wrote it (ADR 0021).
    private static readonly string[] Words = ["NEMASURAT", "UNMEASURED", "ATENTIE", "WARNING", "REFA", "REDO", "OK"];

    /// <summary>
    /// The statements of <see cref="Rest"/> (the value without its verdict word), for the window
    /// to show one under the other (R-031, R-063, and the verdicts of checks 3 and 4, R-071); null
    /// when the value is one statement. The value holds them joined by "; " for the console.
    /// </summary>
    internal IReadOnlyList<string>? Lines { get; init; }

    /// <summary>
    /// The value's figures as a small table for the window (R-024, R-051); null when the row has
    /// none. The console prints the value and never the table.
    /// </summary>
    internal ReportTable? Table { get; init; }

    /// <summary>
    /// Whether the Rezumat's card of the row's check shows the row (R-071): false for the rows of
    /// check 6 that only the Pixeli fierbinti tab shows, true for every other row.
    /// </summary>
    internal bool Summary { get; init; } = true;

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
    /// OK, WARNING, REDO, UNMEASURED, ATENTIE, REFA or NEMASURAT when the text opens with it as a
    /// word, else null.
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

    /// <summary>
    /// The row with its <see cref="Rest"/> cut into statements as <see cref="Lines"/>, when it
    /// holds more than one (ADR 0047); the value, and so the console's text, stays as it is.
    /// </summary>
    /// <returns>This row with its lines set, or this row unchanged for a one-statement text.</returns>
    internal ReportRow WithStatements() => StatementsOf(Rest) is { Count: > 1 } lines ? this with { Lines = lines } : this;

    /// <summary>
    /// A text cut into its statements: at every "; " that stands outside round and square
    /// brackets, so "(outside 0.7 .. 1.4; the farthest 1.93)" stays whole.
    /// </summary>
    /// <param name="text">The text; not null.</param>
    /// <returns>The statements in order, without their separators; the text alone when it has no
    /// such "; ".</returns>
    internal static IReadOnlyList<string> StatementsOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<string> parts = [];
        int depth = 0;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is '(' or '[')
            {
                depth++;
            }
            else if (c is ')' or ']')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (c == ';' && depth == 0 && i + 1 < text.Length && text[i + 1] == ' ')
            {
                parts.Add(text[start..i]);
                start = i + 2;
                i++;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }
}

/// <summary>
/// Which check a section of the report is (ADR 0046): the window and the tests find a section by
/// it, never by the number in its title.
/// </summary>
internal enum ReportCheck
{
    /// <summary>Check 2, how well the dark fits the lights.</summary>
    Lights,

    /// <summary>Check 3, light leaks and differences across the frame.</summary>
    Leaks,

    /// <summary>Check 4, the camera's warming during the series.</summary>
    Warming,

    /// <summary>Check 5, the noise of each frame.</summary>
    Noise,

    /// <summary>Check 6, hot pixels.</summary>
    HotPixels,

    /// <summary>Check 7, the fixed pattern.</summary>
    Pattern,

    /// <summary>Check 8, dead and saturated pixels.</summary>
    DeadSaturated,

    /// <summary>Check 9, how many darks are enough.</summary>
    Enough,

    /// <summary>The INDEX block: the index, the label and the partner.</summary>
    Index,
}

/// <summary>
/// A row's figures as a small table the window draws under the row's label (R-071): the column
/// names, the rows of formatted cells, and the note said under it, the bracket hint of the
/// console's line without its brackets.
/// </summary>
/// <param name="Header">The column names, in the run's language.</param>
/// <param name="Rows">The rows, each with one formatted cell per column.</param>
/// <param name="Note">What the table shows, said under it; empty for none.</param>
internal sealed record ReportTable(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows, string Note);

/// <summary>A titled group of rows: one check of the report, or its INDEX block.</summary>
/// <param name="Check">Which check the section is.</param>
/// <param name="Title">The section's title, as the console prints it ("3. LIGHT LEAKS AND
/// DIFFERENCES ACROSS THE FRAME").</param>
/// <param name="Rows">The section's rows, in order.</param>
internal sealed record ReportSection(ReportCheck Check, string Title, IReadOnlyList<ReportRow> Rows);

/// <summary>
/// One dark of the frames table, every cell already formatted; Skipped or Error set when the frame
/// has no figures; End is the frame's degrees from the end of the series of R-023 (`-` without an end) and
/// Note the reason a frame off the end is one to discard (null for every other frame), printed
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
/// <param name="End">The degrees from the end of the series, signed, or "-".</param>
/// <param name="Light">The light above the warming line of R-028, signed, in ADU-eq (ADU when the
/// factor is 1), or "-".</param>
/// <param name="Side">The offset of check 3's worst side from the centre, minus the bias's, signed,
/// in ADU-eq, or "-".</param>
/// <param name="Above">How many pixels of the reference plane lie above the hot level.</param>
/// <param name="Note">Why a frame off the end of the series is one to discard, or null.</param>
internal sealed record DarkTableRow(int Index, string FileName, string? Skipped, string? Error, string Time, string Mean, string Sigma, string Residual, string Bands, string Amp, string End, string Light, string Side, string Above, string? Note = null);

/// <summary>
/// One light of the lights table, every cell already formatted; Error set when the light has no
/// figures (every light is read, R-050).
/// </summary>
/// <param name="Index">The light's number, from 1.</param>
/// <param name="FileName">The file's name.</param>
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
internal sealed record LightTableRow(int Index, string FileName, string? Error, string Time, string Sky, string LocalSigma, string Candidates, string Isolated, string Amp, string Q25, string Q75);
