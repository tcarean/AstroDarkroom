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

// SplitReport.cs - SplitResult -> tabelul cu N + 1 (+ 1) coloane, textul raportului,
// etichetele si titlul mozaicului. Doar formatare: cifrele sunt ale rularilor de
// structura, verdictele ale lor.
//
// Coloanele: partile in ordine, intregul, si - cand intregul s-a putut compara - profilul
// de referinta, cu adancimea lui pe randul de vignetare si liniute in rest: un profil
// n-are forma de testat. Fiecare celula de test poarta verdictul ei; ultimul rand e
// verdictul fiecarei coloane, cel mai rau dintre forma si adancimea fata de profilul de
// referinta (SplitColumn.Verdict) - acelasi profil ca randul de deasupra lui.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FlatCheck
{
    /// <summary>O celula: textul valorii si verdictul ei (null cand n-are unul).</summary>
    internal sealed record SplitCell(string Value, string Verdict);

    /// <summary>Un rand al tabelului: cheia metricii, eticheta, o celula pe coloana, nota de sub rand.</summary>
    internal sealed record SplitRow(string Key, string Label, IReadOnlyList<SplitCell> Cells, string Note, bool IsKey);

    internal static class SplitReport
    {
        internal const string WholeLabel = "intreg";
        internal const string PngLabelWhole = "INTREG";
        internal const string Dash = "-";

        internal static string PartLabel(int index, int count) => $"partea {index} din {count}";
        internal static string PngLabel(int index, int count) => $"PARTEA {index} DIN {count}";

        internal static string DateLine(DateTime when) => when.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        /// <summary>Titlul mozaicului: versiunea, data si capetele scarii; doar caractere cu glifa in BitmapFont.</summary>
        internal static string PngTitle(DateTime when, PngWriter.MapRange range)
        {
            string lo = ((range.Lo - PngWriter.Mid) * 100).ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
            string hi = ((range.Hi - PngWriter.Mid) * 100).ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
            return $"FLATCHECK {VersionInfo.Value}  {DateLine(when)}  ALBASTRU {lo} %  ALB 0  ROSU {hi} %".ToUpperInvariant();
        }

        /// <summary>Antetele coloanelor, in ordinea celulelor din fiecare rand.</summary>
        internal static IReadOnlyList<string> Headers(SplitResult r)
        {
            var h = r.Columns.Select(c => c.Label).ToList();
            if (r.HasReference) h.Add(ProfileHeader(r));
            return h;
        }

        internal static string ProfileHeader(SplitResult r) => $"profil {r.ReferenceProfile.Lens.SourceLabel}";

        /// <summary>Profilul de referinta, intreg: sursa, obiectiv, setarile profilului.</summary>
        internal static string ReferenceLine(SplitResult r)
            => r.HasReference
                ? $"{r.ReferenceProfile.Lens.SourceLabel} {r.ReferenceProfile.Lens.Title}, {r.ReferenceProfile.Settings} (cel mai apropiat de intreg, acelasi pe toate coloanele)"
                : "niciunul: intregul nu s-a putut compara cu profilele obiectivului (fara raw langa master, sau fara profile)";

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
                rows.Add(new SplitRow(key, StructureReport.TestLabel(key), cells, RowNote(key), key == "ResidualPtpPct"));
            }

            var depth = columns.Select(c => new SplitCell(Stops(c.Outcome.Structure.VignetteStops), null)).ToList();
            if (r.HasReference) depth.Add(new SplitCell(Stops(r.ReferenceDepth.CornerStops), null));
            rows.Add(new SplitRow("VignetteStops", "vignetare centru -> colt", depth, RowNote("VignetteStops"), false));

            if (r.HasReference)
            {
                var diff = columns.Select(c => c.Reference == null
                    ? new SplitCell(Dash, null)
                    : new SplitCell($"{c.Reference.DiffStops:+0.00;-0.00} stopuri = {c.Reference.ErrorPct:F1} %", c.Reference.Verdict)).ToList();
                diff.Add(new SplitCell(Dash, null));
                rows.Add(new SplitRow("DiffStops", "diferenta fata de profil", diff, RowNote("DiffStops"), false));
            }

            var verdict = columns.Select(c => new SplitCell(c.Verdict, c.Verdict)).ToList();
            if (r.HasReference) verdict.Add(new SplitCell(Dash, null));
            rows.Add(new SplitRow("Verdict", "verdict", verdict, RowNote("Verdict"), false));

            return rows;
        }

        internal static string RowNote(string key) => key switch
        {
            "CornerSpreadPct" => "cat difera cele patru colturi intre ele; pe o parte, colturi inegale sunt gradientul sursei din orientarea ei",
            "AzimuthalMaxPct" => "cat difera cele 12 sectoare ale aceluiasi inel; ce e mare pe parti si mic pe intreg e ce a anulat rotatia",
            "ResidualPtpPct" => "cat NU se explica printr-un model radial; cel mai important. O parte are mai putine cadre, deci si mai mult zgomot",
            "TiltPct" => "gradientul liniar; la 180 de grade partile il au cu semne opuse si intregul nu-l mai are",
            "MaxTilePct" => "dala cea mai abatuta; pe o parte cu putine cadre, zgomotul singur o ridica",
            "VignetteStops" => "adancimea fiecarui master; in coloana profilului, adancimea modelului obiectivului la aceleasi setari",
            "DiffStops" => "cat cade fiecare master fata de profilul de referinta (pozitiv = mai mult spre colt), ca eroare in coltul unui light calibrat cu el",
            "Verdict" => "cel mai rau dintre cele cinci teste si adancimea fata de profilul de referinta (randul de mai sus), pe fiecare coloana",
            _ => "",
        };

        internal static string Pct(double v) => double.IsNaN(v) ? "n/a" : $"{v:F2} %";
        static string Stops(double s) => double.IsNaN(s) ? "n/a" : $"{s:F2} stopuri";

        internal static string PartsLine(SplitResult r)
            => r.FramesPerPart > 0
                ? $"{r.Parts.Count} x {r.FramesPerPart} cadre, blocuri consecutive in ordinea numelor din flats/"
                : $"{r.Parts.Count} parti, blocuri consecutive in ordinea numelor din flats/";

        internal static string MapOrder(int parts)
            => $"harta: partile 1..{parts} de la stanga la dreapta, apoi intregul, toate pe aceeasi scara de culoare";

        internal static string Meaning()
            => "Fiecare parte e stiva unei singure orientari, cu mai putine cadre deci mai zgomotoasa; intregul e\n"
             + "media lor. Ce e mare pe parti si mic pe intreg e ce a anulat rotatia: la 180 de grade partea\n"
             + "impara (gradientul), la 90 de grade si saua. Ce au partile in comun cu intregul e fix pe aparat\n"
             + "sau radial, si nicio rotatie nu-l scoate; coloana profilului spune daca adancimea e a obiectivului.";

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

        static string Cell(SplitCell cell)
            => cell.Verdict == null || cell.Value == cell.Verdict ? cell.Value : $"{cell.Value} [{cell.Verdict}]";
    }
}
