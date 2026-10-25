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

using System.Globalization;

namespace DarksCheck.Core;

/// <summary>
/// The inventory as the console prints it (Romanian, R-003, R-006, R-007). Words only; the numbers
/// come from the run.
/// </summary>
internal static class InventoryReport
{
    /// <summary>
    /// Prints the whole inventory of <c>--inventory</c>: the session lines, then a table for the
    /// darks, the biases and the lights.
    /// </summary>
    /// <param name="inv">The inventory; not null.</param>
    /// <param name="w">The writer the text goes to; not null.</param>
    internal static void Write(Inventory inv, TextWriter w)
    {
        WriteHeader(inv, w);
        WriteTable(inv, "DARKS", inv.Darks, inv.DarksCadence, w);
        WriteTable(inv, "BIASES", inv.Biases, null, w);
        WriteTable(inv, "LIGHTS", inv.Lights, inv.LightsCadence, w);
    }

    /// <summary>
    /// The session lines: the `=== SESIUNE` line, then <see cref="HeaderRows"/> as the console
    /// prints them.
    /// </summary>
    /// <param name="inv">The inventory; not null.</param>
    /// <param name="w">The writer the lines go to; not null.</param>
    internal static void WriteHeader(Inventory inv, TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(inv);
        ArgumentNullException.ThrowIfNull(w);
        w.WriteLine();
        w.WriteLine($"=== SESIUNE: {inv.Session.Root} ===");
        foreach (ReportRow row in HeaderRows(inv))
        {
            w.WriteLine($"    {row.Label,-19}: {row.Value}");
        }
    }

    /// <summary>
    /// The session rows (ADR 0009): folders, camera, the settings of each group, the cadences, the
    /// two verdicts, the unreadable frames.
    /// </summary>
    /// <param name="inv">The inventory; not null.</param>
    /// <returns>The rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> HeaderRows(Inventory inv)
    {
        ArgumentNullException.ThrowIfNull(inv);
        SessionFolder s = inv.Session;
        MatchAnalysis m = inv.Match;
        bool noDarks = !s.HasDarks;
        string? partner = inv.DarksFromPartner;
        List<ReportRow> rows =
        [
            new("darks", noDarks ? (partner is null ? "(lipsa)" : $"(lipsa; se folosesc dark-urile partenerului {partner})") : FolderLine(s.Root, s.DarksDir, s.Darks)),
            new("biases", s.BiasesDir is null ? "(lipsa)" : FolderLine(s.Root, s.BiasesDir, s.Biases)),
            new("lights", s.LightsDir is null ? "(lipsa)" : FolderLine(s.Root, s.LightsDir, s.Lights)),
            new("aparat", Distinct(inv.Darks.Concat(inv.PartnerDarks ?? []).Concat(inv.Biases).Concat(inv.Lights).Select(f => f.Camera))),
        ];
        if (noDarks && partner is null)
        {
            rows.Add(new("expunere darks", "(lipsa)"));
        }
        else
        {
            rows.Add(new(noDarks ? "expunere partener" : "expunere darks", Settings(m.Darks)));
        }

        if (m.Biases is not null)
        {
            rows.Add(new("expunere biases", Settings(m.Biases)));
        }

        if (m.Lights is not null)
        {
            rows.Add(new("expunere lights", Settings(m.Lights)));
        }

        rows.Add(new("interval darks", noDarks ? "(lipsa)" : Interval(inv.DarksCadence)));
        if (m.Lights is not null)
        {
            rows.Add(new("interval lights", Interval(inv.LightsCadence)));
        }

        rows.Add(new("potrivire", DarksVsLights(m, partner is null ? "dark-urile" : "dark-urile partenerului")));
        rows.Add(new("bias", Bias(m)));
        if (inv.HasErrors)
        {
            int n = inv.ErrorCount;
            rows.Add(new("ATENTIE", $"{n} {(n == 1 ? "cadru nu s-a putut citi" : "cadre nu s-au putut citi")} (vezi tabelele)"));
        }

        return rows;
    }

    /// <summary>
    /// One row per frame of a group: the metadata and the pause before it; nothing for an empty
    /// group.
    /// </summary>
    /// <param name="inv">The inventory; not null.</param>
    /// <param name="title">The table's title, as "DARKS".</param>
    /// <param name="frames">The group's frames; not null.</param>
    /// <param name="cadence">The group's cadence, whose pauses fill the last column; null for no
    /// pause column.</param>
    /// <param name="w">The writer the table goes to; not null.</param>
    internal static void WriteTable(Inventory inv, string title, IReadOnlyList<FrameInfo> frames, Cadence? cadence, TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(inv);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(w);
        if (frames.Count == 0)
        {
            return;
        }

        w.WriteLine();
        w.WriteLine("  " + title);
        w.WriteLine("     nr  fisier                ora       expunere   ISO" + (cadence is null ? "" : "   pauza"));
        for (int i = 0; i < frames.Count; i++)
        {
            FrameInfo f = frames[i];
            string name = f.FileName.Length > 20 ? f.FileName[..20] : f.FileName;
            if (f.Error is not null)
            {
                w.WriteLine($"    {f.Index,3}  {name,-20}  eroare: {f.Error}");
                continue;
            }

            double? pause = cadence?.PauseSeconds[i];
            string pauseText = cadence is null ? "" : (pause is double p ? RunReport.F(p, 0) + " s" : "-");
            w.WriteLine($"    {f.Index,3}  {name,-20}  {Time(f.Timestamp),-8}  {Exposure(f.ExposureSeconds),8}  {Opt(f.Iso, 0),5}  {pauseText,6}".TrimEnd());
        }
    }

    /// <summary>
    /// An exposure as the user set it: "120.2", "0.05", a 1/4000 s "0.0003"; "-" when unknown.
    /// </summary>
    /// <param name="seconds">The exposure in seconds, or null.</param>
    /// <returns>One decimal from 1 s up, at most four below; "-" for null.</returns>
    internal static string Exposure(double? seconds)
    {
        if (seconds is not double s)
        {
            return "-";
        }

        return s >= 1 ? RunReport.F(s, 1) : s.ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A group's folder relative to the session, with its frame count and the first frame's
    /// extension.
    /// </summary>
    /// <param name="root">The session's folder.</param>
    /// <param name="dir">The group's folder; "." when it is the session's.</param>
    /// <param name="frames">The group's frames.</param>
    /// <returns>The line, as "darks\ (20 cadre .NEF)".</returns>
    private static string FolderLine(string root, string dir, IReadOnlyList<string> frames)
    {
        string rel = dir == root ? "." : Path.GetRelativePath(root, dir) + "\\";
        string ext = frames.Count > 0 ? Path.GetExtension(frames[0]) : "";
        return $"{rel} ({frames.Count} {(frames.Count == 1 ? "cadru" : "cadre")} {ext})";
    }

    /// <summary>The distinct non-blank values joined by " / ".</summary>
    /// <param name="values">The values; null and blank ones are skipped.</param>
    /// <returns>The joined values, or "necunoscut" when there are none.</returns>
    private static string Distinct(IEnumerable<string?> values)
    {
        string[] v = values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.Ordinal).ToArray();
        return v.Length == 0 ? "necunoscut" : string.Join(" / ", v);
    }

    /// <summary>
    /// "119.2 s la ISO 400 (toate cadrele)" or "... (17 din 20 cadre; expuneri sau ISO diferite)".
    /// </summary>
    /// <param name="g">The group's settings, or null.</param>
    /// <returns>The settings in words; "necunoscuta" when no frame states them.</returns>
    private static string Settings(GroupSettings? g)
    {
        if (g is null || g.Readable == 0)
        {
            return "necunoscuta";
        }

        string combo = (g.ExposureSeconds is double e ? Exposure(e) + " s" : "? s") + (g.Iso is double i ? " la ISO " + RunReport.F(i, 0) : "");
        return g.Uniform
            ? combo + " (toate cadrele)"
            : combo + $" ({g.Frames} din {RunReport.Frames(g.Readable)}; expuneri sau ISO diferite)";
    }

    /// <summary>A group's time span, its median interval and its long pauses.</summary>
    /// <param name="c">The group's cadence.</param>
    /// <returns>The row's value, in Romanian.</returns>
    private static string Interval(Cadence c)
    {
        if (c.Start is not DateTime start || c.End is not DateTime end)
        {
            return "necunoscut (cadrele nu au ora sau expunerea)";
        }

        string interval = c.MedianIntervalSeconds is double mi ? $"; {RunReport.Frames(c.TimedFrames)} la {RunReport.F(mi, 1)} s" : $"; {RunReport.Frames(c.TimedFrames)}";
        string pauses = c.LongPauses.Count switch
        {
            0 => $"fara pauze peste {RunReport.F(Th.PauseSeconds, 0)} s",
            1 => $"1 pauza peste {RunReport.F(Th.PauseSeconds, 0)} s ({RunReport.F(c.LongPauses[0].Seconds, 0)} s inainte de cadrul {c.LongPauses[0].FrameIndex})",
            _ => Longest(c),
        };
        return $"{start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} -> {end.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} "
             + $"({RunReport.Duration(c.SpanSeconds)}){interval}; {pauses}";
    }

    /// <summary>The count of the long pauses and the longest of them.</summary>
    /// <param name="c">A cadence with at least one long pause.</param>
    /// <returns>The words, in Romanian.</returns>
    private static string Longest(Cadence c)
    {
        LongPause longest = c.LongPauses.MaxBy(p => p.Seconds)!;
        return $"{c.LongPauses.Count} pauze peste {RunReport.F(Th.PauseSeconds, 0)} s (cea mai lunga {RunReport.F(longest.Seconds, 0)} s inainte de cadrul {longest.FrameIndex})";
    }

    /// <summary>
    /// <paramref name="darks"/> is "dark-urile" or "dark-urile partenerului", whose settings the
    /// lights are compared with.
    /// </summary>
    /// <param name="m">Check 1's match.</param>
    /// <param name="darks">The words for the darks compared: "dark-urile" or "dark-urile
    /// partenerului".</param>
    /// <returns>The verdict's words, opening with OK, ATENTIE or NEMASURAT.</returns>
    private static string DarksVsLights(MatchAnalysis m, string darks)
    {
        switch (m.DarksVsLights)
        {
            case Verdict.Unmeasured:
                return m.Darks is null ? "NEMASURAT: nu exista darks" : m.Lights is null ? "NEMASURAT: nu exista lights" : "NEMASURAT: cadrele nu spun expunerea sau ISO-ul";
            case Verdict.Ok:
                return $"OK: {darks} au expunerea ({Exposure(m.Darks!.ExposureSeconds)} s) si ISO-ul ({Opt(m.Darks.Iso, 0)}) lights-urilor";
            default:
                return $"ATENTIE: {(darks == "dark-urile" ? "dark-uri" : "dark-uri ale partenerului")} de {Exposure(m.Darks!.ExposureSeconds)} s la ISO {Opt(m.Darks.Iso, 0)}, lights de {Exposure(m.Lights!.ExposureSeconds)} s la ISO {Opt(m.Lights.Iso, 0)}";
        }
    }

    /// <summary>
    /// The <c>bias</c> row: whether the biases' exposure is short enough to be a bias.
    /// </summary>
    /// <param name="m">Check 1's match.</param>
    /// <returns>The verdict's words, or "(lipsa)" without biases.</returns>
    private static string Bias(MatchAnalysis m)
    {
        switch (m.Bias)
        {
            case Verdict.Unmeasured:
                return m.Biases is null ? "(lipsa)" : "NEMASURAT: cadrele nu spun expunerea";
            case Verdict.Ok:
                return $"OK: {Exposure(m.Biases!.ExposureSeconds)} s";
            default:
                return $"ATENTIE: {Exposure(m.Biases!.ExposureSeconds)} s nu e un bias (peste {RunReport.F(Th.BiasMaxExposureSeconds, 0)} s)";
        }
    }

    /// <summary>A frame's time of day as HH:mm:ss, or "-" without a timestamp.</summary>
    /// <param name="t">The timestamp, or null.</param>
    /// <returns>The formatted time.</returns>
    private static string Time(DateTime? t) => t is DateTime d ? d.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "-";

    /// <summary>A number with fixed decimals, or "-" when null.</summary>
    /// <param name="v">The number, or null.</param>
    /// <param name="decimals">The number of decimals.</param>
    /// <returns>The formatted number.</returns>
    private static string Opt(double? v, int decimals) => v is double d ? RunReport.F(d, decimals) : "-";
}
