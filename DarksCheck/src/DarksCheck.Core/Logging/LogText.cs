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

// LogText.cs - how a value is written in the diagnostic log: English, invariant numbers, a missing
// value as "none", sizes in KiB and MiB, an enum by its name. The log's lines are composed at the
// seams (the two adapted readers, Session/, Runs/, the two decoding loops, the applications) from
// these pieces, never through T(english, romanian), so the log reads the same whatever --lang
// says (R-101). Nothing here decides anything. ADR 0025; the pieces are LightsCheck's (its ADR
// 0029), themselves FlatCheck's (its ADR 0070).

using System.Globalization;

namespace DarksCheck.Core;

/// <summary>The diagnostic log's way of writing values.</summary>
internal static class LogText
{
    /// <summary>The invariant culture, for every number in the log.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A number with six significant digits, invariant: <c>0.123457</c>, <c>30000</c>, <c>NaN</c>.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The text.</returns>
    internal static string Num(double v) => v.ToString("G6", Inv);

    /// <summary>A number that may be missing: <see cref="Num(double)"/>, or <c>none</c>.</summary>
    /// <param name="v">The value, or null.</param>
    /// <returns>The text.</returns>
    internal static string Num(double? v) => v is double d ? Num(d) : "none";

    /// <summary>An integer, invariant.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The text.</returns>
    internal static string Int(long v) => v.ToString(Inv);

    /// <summary>An integer that may be missing: <see cref="Int(long)"/>, or <c>none</c>.</summary>
    /// <param name="v">The value, or null.</param>
    /// <returns>The text.</returns>
    internal static string Int(long? v) => v is long x ? Int(x) : "none";

    /// <summary>An elapsed time in milliseconds: <c>1141 ms</c>. The log's short form; the long one is <see cref="Seconds"/>.</summary>
    /// <param name="ms">The milliseconds.</param>
    /// <returns>The text.</returns>
    internal static string Ms(long ms) => Int(ms) + " ms";

    /// <summary>
    /// An elapsed time in seconds with one decimal: <c>12.9 s</c>, never whole seconds, so
    /// <c>0.8 s</c> does not read as nothing.
    /// </summary>
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

    /// <summary>A flag that may be missing: <see cref="Yes(bool)"/>, or <c>none</c>.</summary>
    /// <param name="b">The flag, or null.</param>
    /// <returns>The text.</returns>
    internal static string Yes(bool? b) => b is bool x ? Yes(x) : "none";

    /// <summary>A time as <c>2026-08-12 23:35:03</c>, invariant, or <c>none</c>.</summary>
    /// <param name="t">The time, as the file gave it (UTC for a FITS, the camera's clock for a raw), or null.</param>
    /// <returns>The text.</returns>
    internal static string Time(DateTime? t) => t is DateTime d ? d.ToString("yyyy-MM-dd HH:mm:ss", Inv) : "none";

    /// <summary>A size in mebibytes with one decimal: <c>23.9 MiB</c>.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The text.</returns>
    internal static string MiB(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F1", Inv) + " MiB";

    /// <summary>A size in kibibytes with one decimal: <c>12.3 KiB</c>.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The text.</returns>
    internal static string KiB(long bytes) => (bytes / 1024.0).ToString("F1", Inv) + " KiB";

    /// <summary>A file's size in mebibytes, or a question mark when it cannot be read.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The text.</returns>
    internal static string FileMiB(string path) => FileSize(path, MiB);

    /// <summary>A file's size in kibibytes, or a question mark when it cannot be read.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The text.</returns>
    internal static string FileKiB(string path) => FileSize(path, KiB);

    /// <summary>A list of values as <c>[a, b, c]</c>.</summary>
    /// <param name="items">The values; null gives <c>[]</c>.</param>
    /// <returns>The text.</returns>
    internal static string List(IEnumerable<string>? items) => "[" + string.Join(", ", items ?? []) + "]";

    /// <summary>A file's size in the given unit, or a question mark when it cannot be read: the log never throws for a size.</summary>
    /// <param name="path">The file.</param>
    /// <param name="unit">How the bytes are written: <see cref="KiB"/> or <see cref="MiB"/>.</param>
    /// <returns>The text.</returns>
    private static string FileSize(string path, Func<long, string> unit)
    {
        try
        {
            return unit(new FileInfo(path).Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return "?";
        }
    }
}
