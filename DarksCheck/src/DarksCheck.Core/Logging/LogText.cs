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

using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace DarksCheck.Core;

/// <summary>The diagnostic log's way of writing values.</summary>
internal static class LogText
{
    /// <summary>The invariant culture, for every number in the log.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// How many items of a list <see cref="Figures"/> writes whole; a longer list is written as its
    /// first item, its last and its count (ADR 0025).
    /// </summary>
    private const int ListItems = 12;

    /// <summary>How deep <see cref="Figures"/> goes into records held by records before it writes <c>(...)</c>.</summary>
    private const int Depth = 6;

    /// <summary>Each type's public properties as <see cref="Figures"/> writes them, in declaration order, read once per type.</summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();

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

    /// <summary>The process's working set, the memory it holds, in mebibytes: <c>4712.3 MiB</c>.</summary>
    /// <returns>The text.</returns>
    internal static string WorkingSet() => MiB(Environment.WorkingSet);

    /// <summary>A file written, with its size: <c>wrote D:\out\darkscheck-report.txt (12.3 KiB)</c>.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The text.</returns>
    internal static string Wrote(string path) => "wrote " + path + " (" + FileKiB(path) + ")";

    /// <summary>
    /// What a loaded frame is, in one line: its size and reader, the raster, the planes, BITPIX,
    /// the CFA order and where it was read, the scale the file carries, the camera, the shot, the
    /// pixel size and the lens. LightsCheck's line, without the fields of its own pixel-size
    /// feature, which DarksCheck's <see cref="LoadedFrame"/> does not have.
    /// </summary>
    /// <param name="path">The file read.</param>
    /// <param name="f">The frame; its image is null after a metadata read.</param>
    /// <param name="reader"><c>raw</c> or <c>fits</c>.</param>
    /// <returns>The text, without the leading verb.</returns>
    internal static string Frame(string path, LoadedFrame f, string reader)
    {
        ArgumentNullException.ThrowIfNull(f);
        string raster = f.Image is not null ? Int(f.Image.W) + "x" + Int(f.Image.H) : "no pixels";
        string cfa = f.Cfa is not null ? f.Cfa.Letters + " (" + f.Cfa.Source + ")" : "none";
        // A FITS carries no scale (FrameStack assumes one from BITPIX, in the run's language); a raw
        // carries libraw's, whose source is not translated.
        string scale = f.Scale is not null ? Num(f.Scale.White) + "/" + Num(f.Scale.Black) + " (" + f.Scale.Source + ")" : "none";
        return path + " (" + FileMiB(path) + ") reader=" + reader + " " + raster
            + " planes=" + Int(f.Planes) + " bitpix=" + Int(f.Bitpix) + " cfa=" + cfa + " scale=" + scale
            + " camera=" + Str(f.Camera) + " focal=" + Num(f.FocalMm) + " fnumber=" + Num(f.FNumber)
            + " exposure=" + Num(f.ExposureSeconds) + " iso=" + Num(f.Iso) + " time=" + Time(f.Timestamp)
            + " pixel=" + Num(f.PixelSizeUm) + " lens=" + Lens(f) + " range=" + LensRange(f);
    }

    /// <summary>
    /// The FITS keywords that decide how a frame is read and what the run knows of its shot, as
    /// <c>KEY=value</c>, <c>none</c> for the missing ones.
    /// </summary>
    /// <param name="header">The header's keywords; null gives every key as <c>none</c>.</param>
    /// <returns>The text.</returns>
    internal static string FitsKeys(IReadOnlyDictionary<string, string>? header)
    {
        string[] keys =
        [
            "XTENSION", "NAXIS", "NAXIS1", "NAXIS2", "NAXIS3", "BITPIX", "BZERO", "BSCALE", "BAYERPAT", "ROWORDER",
            "XBAYROFF", "YBAYROFF", "INSTRUME", "FOCALLEN", "XPIXSZ", "XBINNING", "EXPTIME", "ISOSPEED", "DATE-OBS",
            "STACKCNT",
        ];
        return string.Join(" ", keys.Select(k => k + "=" + (header is not null && header.TryGetValue(k, out string? v) ? Str(v?.Trim()) : "none")));
    }

    /// <summary>
    /// A folder as it was opened: its root, what it is (a session, a library of darks, or lights
    /// without darks), and each group's folder and count.
    /// </summary>
    /// <param name="s">The folder; not null.</param>
    /// <returns>The text.</returns>
    internal static string Session(SessionFolder s)
    {
        ArgumentNullException.ThrowIfNull(s);
        string kind = s.DarksDir == s.Root ? "library" : s.HasDarks ? "session" : "lights without darks";
        return "root=" + s.Root + " kind=" + kind
            + " darks=" + s.DarksDir + " (" + Int(s.Darks.Count) + " frames, " + Kind(s.Darks) + ")"
            + " biases=" + Str(s.BiasesDir) + " (" + Int(s.Biases.Count) + ")"
            + " lights=" + Str(s.LightsDir) + " (" + Int(s.Lights.Count) + ")";
    }

    /// <summary>
    /// Every figure of a result record, by one rule (ADR 0025): each public property by its name in
    /// camelCase, then <c>=</c> and its value: a number with six significant digits, an integer, a
    /// flag as <c>yes</c> or <c>no</c>, an enum by its name, a time, a text as it is, a missing value
    /// as <c>none</c>, a record held by the record in parentheses, a list in brackets, whole when it
    /// has at most twelve items, else its first and last and its count. The records are the
    /// analyzers' own, so a figure they gain is written without a change here. A property that
    /// throws is written <c>?</c>: the log never throws for a figure.
    /// </summary>
    /// <param name="value">The record; null gives <c>none</c>.</param>
    /// <param name="skip">The names of the record's own properties left out (the frame's
    /// metadata, written on its own line; a text in the run's language; a list too long to be read).</param>
    /// <returns>The text.</returns>
    internal static string Figures(object? value, params string[] skip)
    {
        ArgumentNullException.ThrowIfNull(skip);
        if (value is null)
        {
            return "none";
        }

        StringBuilder sb = new();
        Members(sb, value, skip, 0);
        return sb.ToString();
    }

    /// <summary>The kind of a group's files: the extension of the first, or <c>none</c>.</summary>
    /// <param name="paths">The group's files.</param>
    /// <returns>The text.</returns>
    private static string Kind(IReadOnlyList<string> paths) => paths.Count > 0 ? Path.GetExtension(paths[0]) : "none";

    /// <summary>Writes a record's properties as <c>name=value</c>, separated by spaces.</summary>
    /// <param name="sb">The text being built.</param>
    /// <param name="value">The record.</param>
    /// <param name="skip">The properties left out.</param>
    /// <param name="depth">How many records hold this one.</param>
    private static void Members(StringBuilder sb, object value, string[] skip, int depth)
    {
        bool first = true;
        foreach (PropertyInfo p in PropertiesOf(value.GetType()))
        {
            if (Array.IndexOf(skip, p.Name) >= 0)
            {
                continue;
            }

            if (!first)
            {
                sb.Append(' ');
            }

            first = false;
            sb.Append(char.ToLowerInvariant(p.Name[0])).Append(p.Name.AsSpan(1)).Append('=');
            object? v;
            try
            {
                v = p.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                sb.Append('?');
                continue;
            }

            Value(sb, v, depth);
        }
    }

    /// <summary>A type's public instance properties that take no index, in declaration order.</summary>
    /// <param name="t">The type.</param>
    /// <returns>The properties.</returns>
    private static PropertyInfo[] PropertiesOf(Type t) => Properties.GetOrAdd(t, static x => x
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
        .OrderBy(p => p.MetadataToken)
        .ToArray());

    /// <summary>Writes one value by the rule of <see cref="Figures"/>.</summary>
    /// <param name="sb">The text being built.</param>
    /// <param name="v">The value; null allowed.</param>
    /// <param name="depth">How many records hold the value.</param>
    private static void Value(StringBuilder sb, object? v, int depth)
    {
        switch (v)
        {
            case null:
                sb.Append("none");
                break;
            case string s:
                sb.Append(Str(s));
                break;
            case double d:
                sb.Append(Num(d));
                break;
            case float f:
                sb.Append(Num(f));
                break;
            case bool b:
                sb.Append(Yes(b));
                break;
            case Enum e:
                sb.Append(e.ToString());
                break;
            case DateTime t:
                sb.Append(Time(t));
                break;
            case IFormattable n when v.GetType().IsPrimitive:
                sb.Append(n.ToString(null, Inv));
                break;
            case ITuple tuple:
                sb.Append('(');
                for (int i = 0; i < tuple.Length; i++)
                {
                    sb.Append(i > 0 ? ", " : "");
                    Value(sb, tuple[i], depth + 1);
                }

                sb.Append(')');
                break;
            case IEnumerable items:
                Sequence(sb, items, depth);
                break;
            default:
                if (depth >= Depth)
                {
                    sb.Append("(...)");
                    break;
                }

                sb.Append('(');
                Members(sb, v, [], depth + 1);
                sb.Append(')');
                break;
        }
    }

    /// <summary>
    /// Writes a list: <c>[a, b, c]</c> when it has at most <see cref="ListItems"/> items, else
    /// <c>[first, ..., last] (n items)</c>, so a master of 24 million pixels costs two values.
    /// </summary>
    /// <param name="sb">The text being built.</param>
    /// <param name="items">The list.</param>
    /// <param name="depth">How many records hold the list.</param>
    private static void Sequence(StringBuilder sb, IEnumerable items, int depth)
    {
        if (items is not IList list)
        {
            // A sequence that is no list is read once; the records hold lists, so it is short.
            List<object?> read = [];
            foreach (object? o in items)
            {
                read.Add(o);
            }

            list = read;
        }

        sb.Append('[');
        if (list.Count <= ListItems)
        {
            for (int i = 0; i < list.Count; i++)
            {
                sb.Append(i > 0 ? ", " : "");
                Value(sb, list[i], depth + 1);
            }

            sb.Append(']');
            return;
        }

        Value(sb, list[0], depth + 1);
        sb.Append(", ..., ");
        Value(sb, list[list.Count - 1], depth + 1);
        sb.Append("] (").Append(Int(list.Count)).Append(" items)");
    }

    /// <summary>
    /// A lens as a frame names it: <c>id/mount/name</c>, each <c>none</c> when missing; <c>none</c>
    /// alone when the frame names no lens at all.
    /// </summary>
    /// <param name="f">The frame.</param>
    /// <returns>The text.</returns>
    private static string Lens(LoadedFrame f) =>
        f.LensId is not null || f.LensMount is not null || f.LensName is not null
            ? Int(f.LensId) + "/" + Str(f.LensMount) + "/" + Str(f.LensName)
            : "none";

    /// <summary>A lens's range as a frame carries it: <c>18-55 mm f/3.5</c>; <c>none</c> when the frame carries no range.</summary>
    /// <param name="f">The frame.</param>
    /// <returns>The text.</returns>
    private static string LensRange(LoadedFrame f) =>
        f.LensMinFocalMm is not null || f.LensMaxFocalMm is not null
            ? Num(f.LensMinFocalMm) + "-" + Num(f.LensMaxFocalMm) + " mm f/" + Num(f.LensMaxAperture)
            : "none";

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
