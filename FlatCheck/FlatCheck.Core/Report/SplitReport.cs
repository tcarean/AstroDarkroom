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

// SplitReport.cs - SplitResult -> the table with N + 1 (+ 1) columns, the report's text,
// the labels and the mosaic's title. Formatting only: the figures belong to the structure
// runs, and so do the verdicts.
//
// The columns: the parts in order, the whole, and - when the whole could be compared - the
// reference profile, with its depth on the vignetting row and dashes elsewhere: a profile
// has no shape to test. Each test cell carries its verdict; the last row is the verdict of
// each column, the worst of the shape and the depth against the reference profile
// (SplitColumn.Verdict) - the same profile as the row above it.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>A cell: the value's text and its verdict (null when it has none).</summary>
    /// <param name="Value">The value as displayed.</param>
    /// <param name="Verdict">The cell's verdict word; null when the cell has none.</param>
    internal sealed record SplitCell(string Value, string Verdict);

    /// <summary>
    /// A row of the table: the metric's key, the label, one cell per column, the note under the
    /// row.
    /// </summary>
    /// <param name="Key">The metric's key: a <see cref="StructureReport.TestKeys"/> entry,
    /// "VignetteStops", <see cref="SplitReport.DiffKey"/> or "Verdict".</param>
    /// <param name="Label">The row's label, as displayed.</param>
    /// <param name="Cells">One cell per column, in the order of
    /// <see cref="SplitReport.Headers"/>.</param>
    /// <param name="Note">The explanation under the row.</param>
    /// <param name="IsKey">True for the residual row, the most important one.</param>
    /// <param name="IsReference">True for the reference profile's row.</param>
    internal sealed record SplitRow(string Key, string Label, IReadOnlyList<SplitCell> Cells, string Note, bool IsKey, bool IsReference);

    /// <summary>
    /// The words of the Split tab and its console report: the table of the parts against the
    /// whole, the labels, the cost line and the mosaic's title.
    /// </summary>
    internal static class SplitReport
    {
        /// <summary>The whole master's column label.</summary>
        internal static string WholeLabel => T("whole", "intreg");

        /// <summary>The whole master's label on the mosaic, in capitals for <see cref="BitmapFont"/>.</summary>
        internal static string PngLabelWhole => T("WHOLE", "INTREG");

        internal const string Dash = "-";

        /// <summary>A part's column label.</summary>
        /// <param name="index">The part's number, from 1.</param>
        /// <param name="count">How many parts there are.</param>
        /// <returns>"part i of n".</returns>
        internal static string PartLabel(int index, int count) => T($"part {index} of {count}", $"partea {index} din {count}");

        /// <summary>
        /// A part's label on the mosaic, in capitals for <see cref="BitmapFont"/>.
        /// </summary>
        /// <param name="index">The part's number, from 1.</param>
        /// <param name="count">How many parts there are.</param>
        /// <returns>"PART i OF n".</returns>
        internal static string PngLabel(int index, int count) => T($"PART {index} OF {count}", $"PARTEA {index} DIN {count}");

        /// <summary>The date and time of a run, as the report and the title print it.</summary>
        /// <param name="when">The moment of the run.</param>
        /// <returns>"yyyy-MM-dd HH:mm", invariant.</returns>
        internal static string DateLine(DateTime when) => when.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        /// <summary>
        /// The mosaic's title: the version, the date and the ends of the scale; only characters
        /// with a glyph in BitmapFont.
        /// </summary>
        /// <param name="when">The moment of the run.</param>
        /// <param name="range">The colour scale the mosaic was drawn with.</param>
        /// <returns>The title, in capitals.</returns>
        internal static string PngTitle(DateTime when, PngWriter.MapRange range)
        {
            string lo = Numbers.Signed((range.Lo - PngWriter.Mid) * 100, 1, CultureInfo.InvariantCulture);
            string hi = Numbers.Signed((range.Hi - PngWriter.Mid) * 100, 1, CultureInfo.InvariantCulture);
            return T($"FLATCHECK {VersionInfo.Value}  {DateLine(when)}  BLUE {lo} %  WHITE 0  RED {hi} %",
                     $"FLATCHECK {VersionInfo.Value}  {DateLine(when)}  ALBASTRU {lo} %  ALB 0  ROSU {hi} %").ToUpperInvariant();
        }

        /// <summary>The column headers, in the order of the cells in every row.</summary>
        /// <param name="r">The split result.</param>
        /// <returns>The parts, the whole, and the profile's header when there is a
        /// reference.</returns>
        internal static IReadOnlyList<string> Headers(SplitResult r)
        {
            var h = r.Columns.Select(c => c.Label).ToList();
            if (r.HasReference) h.Add(ProfileHeader);
            return h;
        }

        /// <summary>The reference profile's column header.</summary>
        internal static string ProfileHeader => T("vignetting profile", "profil vignetare");

        /// <summary>
        /// How much room the split needs: the copies exactly, process/ estimated, and how much is
        /// free on the temporary disk when known.
        /// </summary>
        /// <param name="e">The estimate.</param>
        /// <param name="freeBytes">The free bytes on the temporary disk; null when unknown.</param>
        /// <returns>One sentence.</returns>
        internal static string CostLine(SplitCostEstimate e, long? freeBytes)
        {
            string line = e.ProcessBytes > 0
                ? T($"Disk space: {Gb(e.CopiedBytes)} copied + ~{Gb(e.ProcessBytes)} written by Siril in the temporary process/ = ~{Gb(e.TotalBytes)}",
                    $"Loc pe disc: {Gb(e.CopiedBytes)} copiate + ~{Gb(e.ProcessBytes)} scrise de Siril in process/ temporar = ~{Gb(e.TotalBytes)}")
                : T($"Disk space: {Gb(e.CopiedBytes)} copied, plus what Siril writes in the temporary process/ (the sensor size is not known)",
                    $"Loc pe disc: {Gb(e.CopiedBytes)} copiate, plus ce scrie Siril in process/ temporar (marimea senzorului nu se stie)");
            if (freeBytes.HasValue) line += T($"; free on the temporary disk: {Gb(freeBytes.Value)}", $"; liber pe discul temporar: {Gb(freeBytes.Value)}");
            return line + T(". The time depends on Siril and on the machine.", ". Timpul depinde de Siril si de masina.");
        }

        /// <summary>
        /// In the units Explorer shows (1 GB = 1024^3), so that the figure here is the one there.
        /// </summary>
        /// <param name="bytes">The size, bytes.</param>
        /// <returns>"N.N GB" from 1 GiB up, else "N MB".</returns>
        internal static string Gb(long bytes)
            => bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):F1} GB" : $"{bytes / (double)(1L << 20):F0} MB";

        /// <summary>
        /// The reference profile, whole: the source, the lens, the profile's settings.
        /// </summary>
        /// <param name="r">The split result.</param>
        /// <returns>The profile's line, or why there is none.</returns>
        internal static string ReferenceLine(SplitResult r)
            => r.ReferenceByAdobe
                ? T($"{r.ReferenceProfile.Lens.SourceLabel} {r.ReferenceProfile.Lens.Title}, {r.ReferenceProfile.Settings} (the whole's Adobe profile, the same on every column)",
                    $"{r.ReferenceProfile.Lens.SourceLabel} {r.ReferenceProfile.Lens.Title}, {r.ReferenceProfile.Settings} (profilul Adobe al intregului, acelasi pe toate coloanele)")
                : r.HasReference
                ? T($"{r.ReferenceProfile.Lens.SourceLabel} {r.ReferenceProfile.Lens.Title}, {r.ReferenceProfile.Settings} (the nearest to the whole, the same on every column)",
                    $"{r.ReferenceProfile.Lens.SourceLabel} {r.ReferenceProfile.Lens.Title}, {r.ReferenceProfile.Settings} (cel mai apropiat de intreg, acelasi pe toate coloanele)")
                : T("none: the whole could not be compared with the lens profiles (no raw beside the master, or no profiles)",
                    "niciunul: intregul nu s-a putut compara cu profilele obiectivului (fara raw langa master, sau fara profile)");

        /// <summary>
        /// The table's rows: the five structure tests, the vignetting depth, one row per profile
        /// (each column's difference against it) and the verdict.
        /// </summary>
        /// <param name="r">The split result.</param>
        /// <returns>The rows, in display order.</returns>
        internal static IReadOnlyList<SplitRow> Rows(SplitResult r)
        {
            var columns = r.Columns.ToList();
            var rows = new List<SplitRow>();

            foreach (string key in StructureReport.TestKeys)
            {
                var cells = columns.Select(c =>
                {
                    var (v, verdict) = StructureReport.Test(c.Outcome.Structure, key);
                    return new SplitCell(Pct(v), verdict);
                }).ToList();
                if (r.HasReference) cells.Add(new SplitCell(Dash, null));
                rows.Add(new SplitRow(key, StructureReport.TestLabel(key), cells, RowNote(key), key == "ResidualPtpPct", false));
            }

            var depth = columns.Select(c => new SplitCell(Stops(c.Outcome.Structure.VignetteStops), null)).ToList();
            if (r.HasReference) depth.Add(new SplitCell(Dash, null));
            rows.Add(new SplitRow("VignetteStops", T("vignetting centre -> corner", "vignetare centru -> colt"), depth, RowNote("VignetteStops"), false, false));

            // one row per profile, as on the Structura tab: each column's difference against it,
            // and in the profile's column its depth; the reference one (the row that gives the
            // whole's verdict, R-128) is marked
            foreach (ProfileDepth p in r.Profiles)
            {
                bool isReference = ReferenceEquals(p, r.ReferenceDepth);
                var diff = columns.Select(c =>
                {
                    ProfileDepth d = SplitRun.Against(c.Outcome, p.Profile);
                    return d == null ? new SplitCell(Dash, null)
                        : new SplitCell(T($"{Numbers.Signed(d.DiffStops, 2)} stops = {d.ErrorPct:F1} %", $"{Numbers.Signed(d.DiffStops, 2)} stopuri = {d.ErrorPct:F1} %"), d.Verdict);
                }).ToList();
                diff.Add(new SplitCell(Stops(p.CornerStops), null));
                rows.Add(new SplitRow(DiffKey, ProfileLabel(p.Profile), diff, ProfileNote(p, isReference, r.ReferenceByAdobe), false, isReference));
            }

            // The verdict row's value is the verdict itself, worded; its Verdict stays the key
            // the colours and the severity read.
            var verdict = columns.Select(c => new SplitCell(VerdictWords.Of(c.Verdict), c.Verdict)).ToList();
            if (r.HasReference) verdict.Add(new SplitCell(Dash, null));
            rows.Add(new SplitRow("Verdict", "verdict", verdict, RowNote("Verdict"), false, false));

            return rows;
        }

        internal const string DiffKey = "DiffStops";

        /// <summary>The label of a profile's row.</summary>
        /// <param name="p">The profile.</param>
        /// <returns>"fata de" and the profile's source and lens.</returns>
        internal static string ProfileLabel(LensProfile p)
            => T($"against {p.Lens.SourceLabel} {p.Lens.Title}", $"fata de {p.Lens.SourceLabel} {p.Lens.Title}");

        /// <summary>The note under a profile's row.</summary>
        /// <param name="p">The profile's row against the whole.</param>
        /// <param name="isReference">True for the reference profile, which gives the depth
        /// verdict.</param>
        /// <param name="byAdobe">True when the reference is the whole's Adobe row (R-128), false
        /// when it is the whole's nearest row.</param>
        /// <returns>The profile's settings and where it comes from, then the row's
        /// meaning.</returns>
        internal static string ProfileNote(ProfileDepth p, bool isReference, bool byAdobe)
            => T($"profile {p.Profile.Settings} [{p.Profile.Lens.Where}]: ", $"profil {p.Profile.Settings} [{p.Profile.Lens.Where}]: ")
             + (!isReference ? ""
                : byAdobe
                ? T("the whole's Adobe profile - the REFERENCE, gives the depth verdict on every column. ",
                    "profilul Adobe al intregului - REFERINTA, da verdictul adancimii pe fiecare coloana. ")
                : T("the nearest to the whole - the REFERENCE, gives the depth verdict on every column. ",
                    "cel mai apropiat de intreg - REFERINTA, da verdictul adancimii pe fiecare coloana. "))
             + RowNote(DiffKey);

        /// <summary>What a row means when the columns are parts of one master.</summary>
        /// <param name="key">The row's key.</param>
        /// <returns>The note; "" for an unknown key.</returns>
        internal static string RowNote(string key) => key switch
        {
            "CornerSpreadPct" => T("how much the four corners differ from each other; on a part, uneven corners mean the source was brighter on one side, in that orientation",
                                   "cat difera cele patru colturi intre ele; pe o parte, colturi inegale inseamna ca sursa era mai luminoasa pe o latura, in orientarea aceea"),
            "AzimuthalMaxPct" => T("how much the 12 slices of the same ring differ; what is large on the parts and small on the whole is what the rotation cancelled",
                                   "cat difera cele 12 felii ale aceluiasi inel; ce e mare pe parti si mic pe intreg e ce a anulat rotatia"),
            "ResidualPtpPct" => T("how much of the frame does NOT look like a usual vignetting; the most important. A part has fewer frames, so more noise too",
                                  "cat din cadru NU arata ca o vignetare obisnuita; cel mai important. O parte are mai putine cadre, deci si mai mult zgomot"),
            "TiltPct" => T("how much brighter one edge is than the opposite one; at 180 degrees the parts have it with opposite signs and the whole no longer has it",
                           "cu cat e o margine mai luminoasa decat cea opusa; la 180 de grade partile il au cu semne opuse si intregul nu-l mai are"),
            "MaxTilePct" => T("the small square that stands out most; on a part with few frames, the noise alone raises it",
                              "patratelul care iese cel mai mult din rand; pe o parte cu putine cadre, il ridica si zgomotul singur"),
            "VignetteStops" => T("the depth of each master, in stops", "adancimea fiecarui master, in stopuri"),
            "DiffStops" => T("how much darker each master gets toward the corner against the profile (plus = more than the profile), as the error in the corner of a light calibrated with it; in the profile's column, its depth",
                             "cu cat se intuneca fiecare master spre colt fata de profil (plus = mai mult decat profilul), ca eroare in coltul unui light calibrat cu el; in coloana profilului, adancimea lui"),
            "Verdict" => T("the worst of the five tests and the depth against the reference profile (the row above), on each column",
                           "cel mai rau dintre cele cinci teste si adancimea fata de profilul de referinta (randul de mai sus), pe fiecare coloana"),
            _ => "",
        };

        /// <summary>A percentage cell.</summary>
        /// <param name="v">The value, percent; NaN when not measured.</param>
        /// <returns>"N.NN %", or "n/a" for NaN.</returns>
        internal static string Pct(double v) => double.IsNaN(v) ? "n/a" : $"{v:F2} %";

        /// <summary>A depth cell.</summary>
        /// <param name="s">The depth, stops; NaN when not measured.</param>
        /// <returns>"N.NN stopuri", or "n/a" for NaN.</returns>
        static string Stops(double s) => double.IsNaN(s) ? "n/a" : T($"{s:F2} stops", $"{s:F2} stopuri");

        /// <summary>How the frames were split into parts.</summary>
        /// <param name="r">The split result.</param>
        /// <returns>The part count, with the frames per part when known.</returns>
        internal static string PartsLine(SplitResult r)
            => r.FramesPerPart > 0
                ? T($"{r.Parts.Count} x {r.FramesPerPart} frames, consecutive blocks in the order of the names in flats/",
                    $"{r.Parts.Count} x {r.FramesPerPart} cadre, blocuri consecutive in ordinea numelor din flats/")
                : T($"{r.Parts.Count} parts, consecutive blocks in the order of the names in flats/",
                    $"{r.Parts.Count} parti, blocuri consecutive in ordinea numelor din flats/");

        /// <summary>The order of the maps in the mosaic.</summary>
        /// <param name="parts">How many parts there are.</param>
        /// <returns>One sentence.</returns>
        internal static string MapOrder(int parts)
            => T($"map: the parts 1..{parts} from left to right, then the whole, all on the same colour scale",
                 $"harta: partile 1..{parts} de la stanga la dreapta, apoi intregul, toate pe aceeasi scara de culoare");

        /// <summary>How to read the parts against the whole.</summary>
        /// <returns>Five lines joined by '\n'.</returns>
        internal static string Meaning()
            => T("Each part is the master of a single orientation, with fewer frames, so noisier; the whole is\n"
                 + "their mean. What is large on the parts and small on the whole is what the rotation cancelled: at 180 degrees\n"
                 + "one side of the frame brighter than the other, at 90 degrees a four-lobed shape too. What the parts share with\n"
                 + "the whole belongs to the camera or is symmetric about the centre, and no rotation removes it; the profile's\n"
                 + "column says whether the depth is the lens's.",
                 "Fiecare parte e masterul unei singure orientari, cu mai putine cadre, deci mai zgomotos; intregul e\n"
                 + "media lor. Ce e mare pe parti si mic pe intreg e ce a anulat rotatia: la 180 de grade o parte a\n"
                 + "cadrului mai luminoasa decat cealalta, la 90 de grade si o forma in patru lobi. Ce au partile in comun\n"
                 + "cu intregul tine de aparat sau e simetric in jurul centrului, si nicio rotatie nu il scoate; coloana\n"
                 + "profilului spune daca adancimea e a obiectivului.");

        /// <summary>
        /// Writes the report: the run, the parts and their files, the table in padded columns,
        /// the notes of every row, the map's order and legend, and how to read it.
        /// </summary>
        /// <param name="r">The split result.</param>
        /// <param name="wholePath">The whole master's path, as the header prints it.</param>
        /// <param name="w">Where to write.</param>
        internal static void Write(SplitResult r, string wholePath, TextWriter w)
        {
            w.WriteLine($"\n=== SPLIT: {wholePath} ===");
            w.WriteLine(T($"    date and time      : {DateLine(r.When)}", $"    data si ora        : {DateLine(r.When)}"));
            w.WriteLine(T($"    parts              : {PartsLine(r)}", $"    parti              : {PartsLine(r)}"));
            w.WriteLine(T($"    reference profile  : {ReferenceLine(r)}", $"    profil de referinta: {ReferenceLine(r)}"));
            for (int i = 0; i < r.Parts.Count; i++)
                w.WriteLine($"    {r.Parts[i].Label,-19}: {r.Parts[i].Path}");

            w.WriteLine(T("\n  TABLE", "\n  TABEL"));
            IReadOnlyList<string> headers = Headers(r);
            IReadOnlyList<SplitRow> rows = Rows(r);
            int labelW = Math.Max(rows.Max(x => x.Label.Length), 8);
            var widths = new int[headers.Count];
            for (int c = 0; c < headers.Count; c++)
                widths[c] = Math.Max(headers[c].Length, rows.Max(x => Cell(x.Cells[c]).Length));

            w.WriteLine("    " + "".PadRight(labelW) + string.Concat(headers.Select((h, c) => "  " + h.PadRight(widths[c]))));
            foreach (SplitRow row in rows)
            {
                w.WriteLine("    " + row.Label.PadRight(labelW)
                            + string.Concat(row.Cells.Select((cell, c) => "  " + Cell(cell).PadRight(widths[c])))
                            + (row.IsKey ? T("  <- THE MOST IMPORTANT", "  <- CEL MAI IMPORTANT") : ""));
            }

            w.WriteLine();
            foreach (SplitRow row in rows)
                w.WriteLine($"    {row.Label}: {row.Note}");

            w.WriteLine($"\n  {MapOrder(r.Parts.Count)}");
            w.WriteLine($"    ({RunReport.MapLegend(r.MapRange)})");
            foreach (string line in Meaning().Split('\n')) w.WriteLine("  " + line);
        }

        /// <summary>A cell as the console prints it.</summary>
        /// <param name="cell">The cell.</param>
        /// <returns>The value, followed by its verdict in brackets when it has one that
        /// differs.</returns>
        static string Cell(SplitCell cell)
        {
            if (cell.Verdict == null) return cell.Value;
            string word = VerdictWords.Of(cell.Verdict);
            return cell.Value == word ? cell.Value : $"{cell.Value} [{word}]";
        }
    }
}
