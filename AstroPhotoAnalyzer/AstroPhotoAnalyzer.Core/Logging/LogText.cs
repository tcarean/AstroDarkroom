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
// missing value as "none", sizes in KiB and MiB, an enum by its name, a result record by one
// rule. The log's lines are composed at the seams (the four readers of Input/, Runs/, the two
// applications) from these pieces, never through Tr.Pick, so the log's own words read the same
// whatever --lang says (R-171). Nothing here decides anything. ADR 0069; the pieces are
// DarksCheck's (its ADR 0025), themselves LightsCheck's and FlatCheck's (FlatCheck's ADR 0070).

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace AstroPhotoAnalyzer
{
    /// <summary>The diagnostic log's way of writing values.</summary>
    internal static class LogText
    {
        /// <summary>The invariant culture, for every number in the log.</summary>
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// How many items of a list <see cref="Figures"/> writes whole: the simulation's ladder,
        /// the size table, the JPEG table and the regions fit; a longer list (a histogram, the
        /// patches, the stars, the tiles) is written as its first item, its last and its count
        /// (ADR 0069).
        /// </summary>
        const int ListItems = 24;

        /// <summary>How deep <see cref="Figures"/> goes into records held by records before it writes <c>(...)</c>.</summary>
        const int Depth = 6;

        /// <summary>Each type's members as <see cref="Figures"/> writes them, read once per type.</summary>
        static readonly ConcurrentDictionary<Type, MemberInfo[]> Members = new ConcurrentDictionary<Type, MemberInfo[]>();

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

        /// <summary>
        /// A text that may be missing or empty: itself, its control characters written <c>?</c>
        /// (<see cref="Printable"/>), or <c>none</c>.
        /// </summary>
        /// <param name="s">The text, or null.</param>
        /// <returns>The text.</returns>
        internal static string Str(string s) => string.IsNullOrEmpty(s) ? "none" : Printable(s);

        /// <summary>
        /// A text with every control character written <c>?</c>, so a value read from a damaged
        /// file (a FITS card, an ICC profile's name) breaks no line of the log and puts no control
        /// byte in it (the review of 2026-10-31).
        /// </summary>
        /// <param name="s">The text; null gives null.</param>
        /// <returns>The text, itself when it holds no control character.</returns>
        internal static string Printable(string s)
        {
            if (s == null || !s.Any(IsControl)) return s;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(IsControl(c) ? '?' : c);
            return sb.ToString();
        }

        /// <summary>Whether a character is a control character, C0 or DEL.</summary>
        /// <param name="c">The character.</param>
        /// <returns>True under the space and for DEL (127).</returns>
        static bool IsControl(char c) => c < ' ' || c == (char)127;

        /// <summary>A flag as <c>yes</c> or <c>no</c>.</summary>
        /// <param name="b">The flag.</param>
        /// <returns>The text.</returns>
        internal static string Yes(bool b) => b ? "yes" : "no";

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
        internal static string List(IEnumerable<string> items) => "[" + string.Join(", ", items ?? Enumerable.Empty<string>()) + "]";

        /// <summary>A list of numbers as <c>[a, b, c]</c>, each as <see cref="Num(double)"/> writes it.</summary>
        /// <param name="values">The numbers; null gives <c>[]</c>.</param>
        /// <returns>The text.</returns>
        internal static string Nums(IEnumerable<double> values) => List(values?.Select(Num));

        /// <summary>The process's working set, the memory it holds, in mebibytes: <c>1712.3 MiB</c>.</summary>
        /// <returns>The text.</returns>
        internal static string WorkingSet() => MiB(Environment.WorkingSet);

        /// <summary>A file written, with its size: <c>wrote D:\poze\Report.txt (12.3 KiB)</c>.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The text.</returns>
        internal static string Wrote(string path) => "wrote " + path + " (" + FileKiB(path) + ")";

        /// <summary>
        /// What a reader recorded about the file it read: the format, the bit depth, whether the
        /// samples are floating point, the planes in the file, the compression, the embedded ICC
        /// profile's name and size, whether a WCS was read, and the reader's note as the user is
        /// shown it (so in the run's language).
        /// </summary>
        /// <param name="s">The raster's source; null gives <c>none</c>.</param>
        /// <returns>The text.</returns>
        internal static string Source(SourceInfo s)
        {
            if (s == null) return "none";
            string icc = s.Icc == null ? "none" : Str(s.IccDescription) + " (" + Int(s.Icc.Length) + " bytes)";
            return "format=" + Str(s.Format) + " bitDepth=" + Int(s.BitDepth) + " float=" + Yes(s.IsFloat)
                + " planesInFile=" + Int(s.PlanesInFile) + " compression=" + Str(s.Compression)
                + " icc=" + icc + " wcs=" + Yes(s.Wcs != null) + " note=" + Str(s.Note);
        }

        /// <summary>
        /// The FITS keywords that decide how an image is read and where it is on the sky, as
        /// <c>KEY=value</c>, <c>none</c> for the missing ones.
        /// </summary>
        /// <param name="header">The header's keywords; null gives every key as <c>none</c>.</param>
        /// <returns>The text.</returns>
        internal static string FitsKeys(IReadOnlyDictionary<string, string> header)
        {
            string[] keys =
            {
                "SIMPLE", "BITPIX", "NAXIS", "NAXIS1", "NAXIS2", "NAXIS3", "BZERO", "BSCALE", "ROWORDER", "DATAMIN",
                "DATAMAX", "CTYPE1", "CTYPE2", "CRPIX1", "CRPIX2", "CRVAL1", "CRVAL2", "CD1_1", "CD1_2", "CD2_1",
                "CD2_2", "CDELT1", "CDELT2", "CROTA2", "PC1_1", "A_ORDER", "OBJECT", "INSTRUME", "EXPTIME", "STACKCNT",
            };
            return string.Join(" ", keys.Select(k => k + "=" + (header != null && header.TryGetValue(k, out string v) ? Str(v?.Trim()) : "none")));
        }

        /// <summary>
        /// Every figure of a result record, by one rule (ADR 0069, after DarksCheck's ADR 0025): each
        /// instance property, then each instance field, that is not private, by its name in
        /// camelCase, then <c>=</c> and its value: a number with six significant digits, an
        /// integer, a flag as <c>yes</c> or <c>no</c>, an enum by its name, a time, a stored text as
        /// it is (a path, a format, a note as the reader wrote it), a missing value as <c>none</c>,
        /// a tuple or a record held by the record in parentheses, a list in brackets, whole when it
        /// has at most <see cref="ListItems"/> items, else its first and last and its count. Two
        /// guards keep the log's own words English and its lines short: a property with a
        /// hand-written getter is left out when it gives a text or texts, since every such getter
        /// of this core is a sentence through <c>Tr.Pick</c> (a computed number, flag or enum is
        /// written); a value of a type from another assembly than the record's, past the simple
        /// ones and the lists, is written as its type's name in angle brackets, never walked (the
        /// packages' objects stay out). A member that throws is written <c>?</c>: the log never
        /// throws for a figure.
        /// </summary>
        /// <param name="value">The record; null gives <c>none</c>.</param>
        /// <param name="skip">
        /// The names of the members left out, at every depth: a text stored in the run's language
        /// (a region's name, the crop's axis, a note written on a line of its own), a list too long
        /// to be read.
        /// </param>
        /// <returns>The text.</returns>
        internal static string Figures(object value, params string[] skip)
        {
            ArgumentNullException.ThrowIfNull(skip);
            if (value == null) return "none";

            var sb = new StringBuilder();
            WriteMembers(sb, value, skip, 0, value.GetType().Assembly);
            return sb.ToString();
        }

        /// <summary>Writes a record's members as <c>name=value</c>, separated by spaces.</summary>
        /// <param name="sb">The text being built.</param>
        /// <param name="value">The record.</param>
        /// <param name="skip">The members left out.</param>
        /// <param name="depth">How many records hold this one.</param>
        /// <param name="home">The assembly of the record <see cref="Figures"/> was given, whose types alone are walked.</param>
        static void WriteMembers(StringBuilder sb, object value, string[] skip, int depth, Assembly home)
        {
            bool first = true;
            foreach (MemberInfo m in MembersOf(value.GetType()))
            {
                if (Array.IndexOf(skip, m.Name) >= 0) continue;

                if (!first) sb.Append(' ');
                first = false;
                sb.Append(char.ToLowerInvariant(m.Name[0])).Append(m.Name.AsSpan(1)).Append('=');
                object v;
                try
                {
                    v = m is PropertyInfo p ? p.GetValue(value) : ((FieldInfo)m).GetValue(value);
                }
                catch (TargetInvocationException)
                {
                    sb.Append('?');
                    continue;
                }

                WriteValue(sb, v, skip, depth, home);
            }
        }

        /// <summary>
        /// A type's members as <see cref="Figures"/> writes them: its instance properties that are
        /// readable, take no index and have a getter that is not private or protected (so neither
        /// a record's <c>EqualityContract</c>), in declaration order, without the hand-written
        /// getters of texts; then its instance fields that are not private and not
        /// compiler-generated; no member holding a delegate.
        /// </summary>
        /// <param name="t">The type.</param>
        /// <returns>The members.</returns>
        static MemberInfo[] MembersOf(Type t) => Members.GetOrAdd(t, static x =>
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            IEnumerable<MemberInfo> properties = x.GetProperties(flags)
                .Where(p => p.GetMethod != null && p.GetIndexParameters().Length == 0)
                .Where(p => p.GetMethod.IsPublic || p.GetMethod.IsAssembly || p.GetMethod.IsFamilyOrAssembly)
                .Where(p => !IsDelegate(p.PropertyType))
                .Where(p => p.GetMethod.IsDefined(typeof(CompilerGeneratedAttribute), false) || !IsText(p.PropertyType))
                .OrderBy(p => p.MetadataToken);
            IEnumerable<MemberInfo> fields = x.GetFields(flags)
                .Where(f => !f.IsPrivate && !f.IsFamily && !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                .Where(f => !IsDelegate(f.FieldType))
                .OrderBy(f => f.MetadataToken);
            return properties.Concat(fields).ToArray();
        });

        /// <summary>Whether a member's type is a text or a sequence of texts.</summary>
        /// <param name="t">The member's type.</param>
        /// <returns>True for <c>string</c> and for every <c>IEnumerable&lt;string&gt;</c>.</returns>
        static bool IsText(Type t) => t == typeof(string) || typeof(IEnumerable<string>).IsAssignableFrom(t);

        /// <summary>Whether a member's type is a delegate, which has no figure to write.</summary>
        /// <param name="t">The member's type.</param>
        /// <returns>True for any delegate type.</returns>
        static bool IsDelegate(Type t) => typeof(Delegate).IsAssignableFrom(t);

        /// <summary>Writes one value by the rule of <see cref="Figures"/>.</summary>
        /// <param name="sb">The text being built.</param>
        /// <param name="v">The value; null allowed.</param>
        /// <param name="skip">The members left out, passed down to the records held.</param>
        /// <param name="depth">How many records hold the value.</param>
        /// <param name="home">The assembly whose types alone are walked.</param>
        static void WriteValue(StringBuilder sb, object v, string[] skip, int depth, Assembly home)
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
                case char c:
                    // never a NUL byte in the file (FlatCheck's ADR 0072)
                    sb.Append(c == '\0' ? "none" : c.ToString());
                    break;
                case Enum e:
                    sb.Append(e.ToString());
                    break;
                case DateTime t:
                    sb.Append(t.ToString("yyyy-MM-dd HH:mm:ss", Inv));
                    break;
                case IFormattable n when v.GetType().IsPrimitive:
                    sb.Append(n.ToString(null, Inv));
                    break;
                case ITuple tuple:
                    sb.Append('(');
                    for (int i = 0; i < tuple.Length; i++)
                    {
                        sb.Append(i > 0 ? ", " : "");
                        WriteValue(sb, tuple[i], skip, depth + 1, home);
                    }

                    sb.Append(')');
                    break;
                case IEnumerable items:
                    WriteSequence(sb, items, skip, depth, home);
                    break;
                default:
                    if (v.GetType().Assembly != home)
                    {
                        sb.Append('<').Append(v.GetType().Name).Append('>');
                        break;
                    }

                    if (depth >= Depth)
                    {
                        sb.Append("(...)");
                        break;
                    }

                    sb.Append('(');
                    WriteMembers(sb, v, skip, depth + 1, home);
                    sb.Append(')');
                    break;
            }
        }

        /// <summary>
        /// Writes a list: <c>[a, b, c]</c> when it has at most <see cref="ListItems"/> items, else
        /// <c>[first, ..., last] (n items)</c>, so a histogram of 101 bins or a list of 3000 stars
        /// costs two values.
        /// </summary>
        /// <param name="sb">The text being built.</param>
        /// <param name="items">The list.</param>
        /// <param name="skip">The members left out, passed down to the records held.</param>
        /// <param name="depth">How many records hold the list.</param>
        /// <param name="home">The assembly whose types alone are walked.</param>
        static void WriteSequence(StringBuilder sb, IEnumerable items, string[] skip, int depth, Assembly home)
        {
            if (items is not IList list)
            {
                // A sequence that is no list is read once; the records hold lists, so it is short.
                var read = new List<object>();
                foreach (object o in items) read.Add(o);
                list = read;
            }

            sb.Append('[');
            if (list.Count <= ListItems)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    sb.Append(i > 0 ? ", " : "");
                    WriteValue(sb, list[i], skip, depth + 1, home);
                }

                sb.Append(']');
                return;
            }

            WriteValue(sb, list[0], skip, depth + 1, home);
            sb.Append(", ..., ");
            WriteValue(sb, list[list.Count - 1], skip, depth + 1, home);
            sb.Append("] (").Append(Int(list.Count)).Append(" items)");
        }

        /// <summary>A file's size in the given unit, or a question mark when it cannot be read: the log never throws for a size.</summary>
        /// <param name="path">The file.</param>
        /// <param name="unit">How the bytes are written: <see cref="KiB"/> or <see cref="MiB"/>.</param>
        /// <returns>The text.</returns>
        static string FileSize(string path, Func<long, string> unit)
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
}
