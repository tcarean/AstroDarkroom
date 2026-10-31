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

// LogText.cs - how a value is written in the diagnostic log: English, invariant numbers, a
// missing value as "none", sizes in KiB and MiB, a card as its text with each byte outside
// printable ASCII as \xNN. The log's lines are composed
// at the seams (FitsHeader, WcsCopy, the applications) from these pieces, never through
// T(english, romanian): the log reads the same whatever --lang says (R-046). The general pieces
// are FlatCheck's (its Logging/LogText.cs); the card, the value and the keys are CopyWcs's own.
// Nothing here decides anything, and nothing here feeds the copy, which stays the raw card (R-010).

using System.Globalization;
using System.Text;

namespace CopyWcs;

/// <summary>The diagnostic log's way of writing values.</summary>
internal static class LogText
{
    /// <summary>The invariant culture, for every number in the log.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// The header keys a read line names (R-042): what the image is, whether its data sits in the
    /// primary HDU, whether its rows were flipped, and what solve it holds.
    /// </summary>
    private static readonly string[] DecidingKeys =
        ["BITPIX", "NAXIS", "NAXIS1", "NAXIS2", "NAXIS3", "EXTEND", "CTYPE1", "CTYPE2", "ROWORDER", "A_ORDER", "PLTSOLVD"];

    /// <summary>An integer, invariant.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The text: <c>2880</c>, <c>-1</c>.</returns>
    internal static string Int(long v) => v.ToString(Inv);

    /// <summary>An integer that may be unknown: <see cref="Int(long)"/>, or <c>none</c>.</summary>
    /// <param name="v">The value, or null when it cannot be known (the length of a pipe).</param>
    /// <returns>The text.</returns>
    internal static string Int(long? v) => v.HasValue ? Int(v.Value) : "none";

    /// <summary>An elapsed time in milliseconds: <c>1141 ms</c>. The log's short form; the long one is <see cref="Seconds"/>.</summary>
    /// <param name="ms">The milliseconds.</param>
    /// <returns>The text.</returns>
    internal static string Ms(long ms) => Int(ms) + " ms";

    /// <summary>An elapsed time in seconds with one decimal: <c>12.9 s</c>, never whole seconds, so <c>0.8 s</c> does not read as nothing.</summary>
    /// <param name="elapsed">The time.</param>
    /// <returns>The text.</returns>
    internal static string Seconds(TimeSpan elapsed) => elapsed.TotalSeconds.ToString("F1", Inv) + " s";

    /// <summary>A text that may be missing or empty: itself, or <c>none</c>.</summary>
    /// <param name="s">The text, or null.</param>
    /// <returns>The text.</returns>
    internal static string Str(string? s) => string.IsNullOrEmpty(s) ? "none" : s;

    /// <summary>A flag as <c>yes</c> or <c>no</c>.</summary>
    /// <param name="b">The flag.</param>
    /// <returns>The text.</returns>
    internal static string Yes(bool b) => b ? "yes" : "no";

    /// <summary>A size in kibibytes with one decimal: <c>3.2 KiB</c>.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The text.</returns>
    internal static string KiB(long bytes) => (bytes / 1024.0).ToString("F1", Inv) + " KiB";

    /// <summary>A size in mebibytes with one decimal: <c>276.5 MiB</c>.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The text.</returns>
    internal static string MiB(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F1", Inv) + " MiB";

    /// <summary>A file's size in the unit that reads best: <see cref="KiB"/> below 1 MiB, <see cref="MiB"/> from there.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The text: <c>3.2 KiB</c>, <c>276.5 MiB</c>.</returns>
    internal static string Size(long bytes) => bytes < 1024 * 1024 ? KiB(bytes) : MiB(bytes);

    /// <summary>An image's size as two header integers: <c>100x80</c>; the reader's -1 for a missing key stays -1.</summary>
    /// <param name="width">The <c>NAXIS1</c> value.</param>
    /// <param name="height">The <c>NAXIS2</c> value.</param>
    /// <returns>The text.</returns>
    internal static string Dimensions(int width, int height) => Int(width) + "x" + Int(height);

    /// <summary>
    /// A header card as the log writes it, which is as the header holds it: its text without the
    /// trailing spaces, each control character and each byte above 0x7F written as <c>\xNN</c>,
    /// so a malformed header never puts a NUL byte in the log, and a header holding a byte the
    /// standard forbids, which the copy keeps as it is (R-010), is logged as that byte's code with
    /// the log itself staying ASCII (ADR 0010).
    /// </summary>
    /// <param name="card">The card, normally 80 characters.</param>
    /// <returns>The text.</returns>
    internal static string Card(string card) => Escape(card.TrimEnd(' '));

    /// <summary>
    /// A card's value as written, for the log only: when columns 9 and 10 hold <c>= </c>, the text
    /// from column 11, its leading spaces trimmed, a quoted string up to and including its closing
    /// quote (a doubled quote inside is part of it), anything else up to the first <c>/</c>,
    /// trimmed; otherwise the text from column 9, trimmed. Written as <see cref="Card"/> writes a
    /// card: control characters and bytes above 0x7F as <c>\xNN</c>.
    /// </summary>
    /// <param name="card">The card; null when the header has none.</param>
    /// <returns>The value's text, or <c>none</c> for a missing card or an empty value.</returns>
    internal static string Value(string? card)
    {
        if (card is null)
        {
            return "none";
        }

        if (card.Length < 10 || card[8] != '=' || card[9] != ' ')
        {
            return Str(Escape(card.Length > 8 ? card[8..].Trim(' ') : ""));
        }

        string rest = card[10..].TrimStart(' ');
        if (rest.StartsWith('\''))
        {
            for (int i = 1; i < rest.Length; i++)
            {
                if (rest[i] != '\'')
                {
                    continue;
                }

                if (i + 1 < rest.Length && rest[i + 1] == '\'')
                {
                    i++;   // a doubled quote is a quote inside the string
                    continue;
                }

                return Escape(rest[..(i + 1)]);
            }

            return Escape(rest.TrimEnd(' '));   // no closing quote: the text as written
        }

        int slash = rest.IndexOf('/', StringComparison.Ordinal);
        return Str(Escape((slash >= 0 ? rest[..slash] : rest).Trim(' ')));
    }

    /// <summary>The keys a header read names, as <c>KEY=value</c> through <see cref="Value"/>, <c>none</c> for the missing ones.</summary>
    /// <param name="header">The header read.</param>
    /// <returns>The text: <c>BITPIX=16 NAXIS=2 NAXIS1=100 ... PLTSOLVD=T</c>.</returns>
    internal static string Keys(FitsHeader header) =>
        string.Join(" ", DecidingKeys.Select(k => k + "=" + Value(header.GetCard(k))));

    /// <summary>A copy's request: the three paths and the three options.</summary>
    /// <param name="r">The request.</param>
    /// <returns>The text: <c>source=a.fit dest=b.fit output=in place optical=yes force=no dryRun=no</c>.</returns>
    internal static string Request(WcsCopyRequest r) =>
        "source=" + r.Source + " dest=" + r.Dest + " output=" + (r.Output ?? "in place")
        + " optical=" + Yes(r.IncludeOptical) + " force=" + Yes(r.Force) + " dryRun=" + Yes(r.DryRun);

    /// <summary>
    /// A card's text as the header holds it: each character below 0x20, 0x7F, and each one above
    /// 0x7F written as <c>\xNN</c>, the code of the byte the file holds (a card's characters are
    /// its bytes one to one, R-010), so the log stays ASCII whatever a header holds.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <returns>The text, unchanged when it holds no such character.</returns>
    private static string Escape(string s)
    {
        if (!s.Any(IsEscaped))
        {
            return s;
        }

        var sb = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            if (IsEscaped(c))
            {
                sb.Append("\\x").Append(((int)c).ToString("X2", Inv));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>Whether a character is one the log writes as <c>\xNN</c>: a control character or one above 0x7F.</summary>
    /// <param name="c">The character.</param>
    /// <returns>True below 0x20, for 0x7F and above it.</returns>
    private static bool IsEscaped(char c) => c < 0x20 || c >= 0x7F;
}
