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
        internal const string WholeLabel = "intreg";
        internal const string PngLabelWhole = "INTREG";
        internal const string Dash = "-";

        /// <summary>A part's column label.</summary>
        /// <param name="index">The part's number, from 1.</param>
        /// <param name="count">How many parts there are.</param>
        /// <returns>"partea i din n".</returns>
        internal static string PartLabel(int index, int count) => $"partea {index} din {count}";

        /// <summary>
        /// A part's label on the mosaic, in capitals for <see cref="BitmapFont"/>.
        /// </summary>
        /// <param name="index">The part's number, from 1.</param>
        /// <param name="count">How many parts there are.</param>
        /// <returns>"PARTEA i DIN n".</returns>
        internal static string PngLabel(int index, int count) => $"PARTEA {index} DIN {count}";

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
            string lo = ((range.Lo - PngWriter.Mid) * 100).ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
            string hi = ((range.Hi - PngWriter.Mid) * 100).ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
            return $"FLATCHECK {VersionInfo.Value}  {DateLine(when)}  ALBASTRU {lo} %  ALB 0  ROSU {hi} %".ToUpperInvariant();
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

        internal const string ProfileHeader = "profil vignetare";

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
                ? $"Loc pe disc: {Gb(e.CopiedBytes)} copiate + ~{Gb(e.ProcessBytes)} scrise de Siril in process/ temporar = ~{Gb(e.TotalBytes)}"
                : $"Loc pe disc: {Gb(e.CopiedBytes)} copiate, plus ce scrie Siril in process/ temporar (marimea senzorului nu se stie)";
            if (freeBytes.HasValue) line += $"; liber pe discul temporar: {Gb(freeBytes.Value)}";
            return line + ". Timpul depinde de Siril si de masina.";
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
            => r.HasReference
                ? $"{r.ReferenceProfile.Lens.SourceLabel} {r.ReferenceProfile.Lens.Title}, {r.ReferenceProfile.Settings} (cel mai apropiat de intreg, acelasi pe toate coloanele)"
                : "niciunul: intregul nu s-a putut compara cu profilele obiectivului (fara raw langa master, sau fara profile)";

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
            rows.Add(new SplitRow("VignetteStops", "vignetare centru -> colt", depth, RowNote("VignetteStops"), false, false));

            // one row per profile, as on the Structura tab: each column's difference against it,
            // and
            // in the profile's column its depth; the reference one (the nearest to the whole) is
            // marked
            foreach (ProfileDepth p in r.Profiles)
            {
                bool isReference = ReferenceEquals(p, r.ReferenceDepth);
                var diff = columns.Select(c =>
                {
                    ProfileDepth d = SplitRun.Against(c.Outcome, p.Profile);
                    return d == null ? new SplitCell(Dash, null)
                        : new SplitCell($"{d.DiffStops:+0.00;-0.00} stopuri = {d.ErrorPct:F1} %", d.Verdict);
                }).ToList();
                diff.Add(new SplitCell(Stops(p.CornerStops), null));
                rows.Add(new SplitRow(DiffKey, ProfileLabel(p.Profile), diff, ProfileNote(p, isReference), false, isReference));
            }

            var verdict = columns.Select(c => new SplitCell(c.Verdict, c.Verdict)).ToList();
            if (r.HasReference) verdict.Add(new SplitCell(Dash, null));
            rows.Add(new SplitRow("Verdict", "verdict", verdict, RowNote("Verdict"), false, false));

            return rows;
        }

        internal const string DiffKey = "DiffStops";

        /// <summary>The label of a profile's row.</summary>
        /// <param name="p">The profile.</param>
        /// <returns>"fata de" and the profile's source and lens.</returns>
        internal static string ProfileLabel(LensProfile p) => $"fata de {p.Lens.SourceLabel} {p.Lens.Title}";

        /// <summary>The note under a profile's row.</summary>
        /// <param name="p">The profile's row against the whole.</param>
        /// <param name="isReference">True for the reference profile, which gives the depth
        /// verdict.</param>
        /// <returns>The profile's settings and where it comes from, then the row's
        /// meaning.</returns>
        internal static string ProfileNote(ProfileDepth p, bool isReference)
            => $"profil {p.Profile.Settings} [{p.Profile.Lens.Where}]: "
             + (isReference ? "cel mai apropiat de intreg - REFERINTA, da verdictul adancimii pe fiecare coloana. " : "")
             + RowNote(DiffKey);

        /// <summary>What a row means when the columns are parts of one master.</summary>
        /// <param name="key">The row's key.</param>
        /// <returns>The note; "" for an unknown key.</returns>
        internal static string RowNote(string key) => key switch
        {
            "CornerSpreadPct" => "cat difera cele patru colturi intre ele; pe o parte, colturi inegale sunt gradientul sursei din orientarea ei",
            "AzimuthalMaxPct" => "cat difera cele 12 sectoare ale aceluiasi inel; ce e mare pe parti si mic pe intreg e ce a anulat rotatia",
            "ResidualPtpPct" => "cat NU se explica printr-un model radial; cel mai important. O parte are mai putine cadre, deci si mai mult zgomot",
            "TiltPct" => "gradientul liniar; la 180 de grade partile il au cu semne opuse si intregul nu-l mai are",
            "MaxTilePct" => "dala cea mai abatuta; pe o parte cu putine cadre, zgomotul singur o ridica",
            "VignetteStops" => "adancimea fiecarui master, in stopuri",
            "DiffStops" => "cat cade fiecare master fata de profil (pozitiv = mai mult spre colt), ca eroare in coltul unui light calibrat cu el; in coloana profilului, adancimea lui",
            "Verdict" => "cel mai rau dintre cele cinci teste si adancimea fata de profilul de referinta (randul de mai sus), pe fiecare coloana",
            _ => "",
        };

        /// <summary>A percentage cell.</summary>
        /// <param name="v">The value, percent; NaN when not measured.</param>
        /// <returns>"N.NN %", or "n/a" for NaN.</returns>
        internal static string Pct(double v) => double.IsNaN(v) ? "n/a" : $"{v:F2} %";

        /// <summary>A depth cell.</summary>
        /// <param name="s">The depth, stops; NaN when not measured.</param>
        /// <returns>"N.NN stopuri", or "n/a" for NaN.</returns>
        static string Stops(double s) => double.IsNaN(s) ? "n/a" : $"{s:F2} stopuri";

        /// <summary>How the frames were split into parts.</summary>
        /// <param name="r">The split result.</param>
        /// <returns>The part count, with the frames per part when known.</returns>
        internal static string PartsLine(SplitResult r)
            => r.FramesPerPart > 0
                ? $"{r.Parts.Count} x {r.FramesPerPart} cadre, blocuri consecutive in ordinea numelor din flats/"
                : $"{r.Parts.Count} parti, blocuri consecutive in ordinea numelor din flats/";

        /// <summary>The order of the maps in the mosaic.</summary>
        /// <param name="parts">How many parts there are.</param>
        /// <returns>One sentence.</returns>
        internal static string MapOrder(int parts)
            => $"harta: partile 1..{parts} de la stanga la dreapta, apoi intregul, toate pe aceeasi scara de culoare";

        /// <summary>How to read the parts against the whole.</summary>
        /// <returns>Four lines joined by '\n'.</returns>
        internal static string Meaning()
            => "Fiecare parte e stiva unei singure orientari, cu mai putine cadre deci mai zgomotoasa; intregul e\n"
             + "media lor. Ce e mare pe parti si mic pe intreg e ce a anulat rotatia: la 180 de grade partea\n"
             + "impara (gradientul), la 90 de grade si saua. Ce au partile in comun cu intregul e fix pe aparat\n"
             + "sau radial, si nicio rotatie nu-l scoate; coloana profilului spune daca adancimea e a obiectivului.";

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
            w.WriteLine($"    data si ora        : {DateLine(r.When)}");
            w.WriteLine($"    parti              : {PartsLine(r)}");
            w.WriteLine($"    profil de referinta: {ReferenceLine(r)}");
            for (int i = 0; i < r.Parts.Count; i++)
                w.WriteLine($"    {r.Parts[i].Label,-19}: {r.Parts[i].Path}");

            w.WriteLine("\n  TABEL");
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
                            + (row.IsKey ? "  <- CEL MAI IMPORTANT" : ""));
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
            => cell.Verdict == null || cell.Value == cell.Verdict ? cell.Value : $"{cell.Value} [{cell.Verdict}]";
    }
}
