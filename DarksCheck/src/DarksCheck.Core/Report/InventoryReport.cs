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
using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// The inventory as the console prints it (in the run's language, R-003, R-006, R-007). Words only;
/// the numbers come from the run.
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
        w.WriteLine(T($"=== SESSION: {inv.Session.Root} ===", $"=== SESIUNE: {inv.Session.Root} ==="));
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
            new("darks", noDarks ? (partner is null ? None : T($"(none; the darks of the partner {partner} are used)", $"(lipsa; se folosesc dark-urile partenerului {partner})")) : FolderLine(s.Root, s.DarksDir, s.Darks)),
            new("biases", s.BiasesDir is null ? None : FolderLine(s.Root, s.BiasesDir, s.Biases)),
            new("lights", s.LightsDir is null ? None : s.LightsChosen ? ChosenLightsLine(s.LightsDir, s.Lights) : FolderLine(s.Root, s.LightsDir, s.Lights)),
            new(T("camera", "aparat"), Distinct(inv.Darks.Concat(inv.PartnerDarks ?? []).Concat(inv.Biases).Concat(inv.Lights).Select(f => f.Camera))),
        ];
        if (noDarks && partner is null)
        {
            rows.Add(new(T("darks exposure", "expunere darks"), None));
        }
        else
        {
            rows.Add(new(noDarks ? T("partner exposure", "expunere partener") : T("darks exposure", "expunere darks"), Settings(m.Darks)));
        }

        if (m.Biases is not null)
        {
            rows.Add(new(T("biases exposure", "expunere biases"), Settings(m.Biases)));
        }

        if (m.Lights is not null)
        {
            rows.Add(new(T("lights exposure", "expunere lights"), Settings(m.Lights)));
        }

        rows.Add(new(T("darks interval", "interval darks"), noDarks ? None : Interval(inv.DarksCadence)));
        if (m.Lights is not null)
        {
            rows.Add(new(T("lights interval", "interval lights"), Interval(inv.LightsCadence)));
        }

        rows.Add(new(T("match", "potrivire"), DarksVsLights(m, partner is not null)));
        rows.Add(new("bias", Bias(m)));
        if (inv.HasErrors)
        {
            int n = inv.ErrorCount;
            rows.Add(new(T("WARNING", "ATENTIE"), n == 1
                ? T("1 frame could not be read (see the tables)", "1 cadru nu s-a putut citi (vezi tabelele)")
                : T($"{n} frames could not be read (see the tables)", $"{RoCount(n)}cadre nu s-au putut citi (vezi tabelele)")));
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
        // The headers keep the columns of the rows below them in both languages.
        w.WriteLine(T("     no  file                  time      exposure   ISO", "     nr  fisier                ora       expunere   ISO") + (cadence is null ? "" : T("     gap", "   pauza")));
        for (int i = 0; i < frames.Count; i++)
        {
            FrameInfo f = frames[i];
            string name = f.FileName.Length > 20 ? f.FileName[..20] : f.FileName;
            if (f.Error is not null)
            {
                w.WriteLine($"    {f.Index,3}  {name,-20}  " + T("error: ", "eroare: ") + f.Error);
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
    /// <returns>The line, as "darks\ (20 frames .NEF)".</returns>
    private static string FolderLine(string root, string dir, IReadOnlyList<string> frames)
    {
        string rel = dir == root ? "." : Path.GetRelativePath(root, dir) + "\\";
        string ext = frames.Count > 0 ? Path.GetExtension(frames[0]) : "";
        return $"{rel} ({RunReport.Frames(frames.Count)} {ext})";
    }

    /// <summary>
    /// The `lights` row for lights chosen from another folder (R-055): the folder's full path,
    /// since it is not under the session, the count and the extension, and that it was the
    /// owner's choice.
    /// </summary>
    /// <param name="dir">The folder the lights were listed in, full.</param>
    /// <param name="frames">The lights.</param>
    /// <returns>The line, as "E:\...\lights (87 frames .NEF; chosen by you)".</returns>
    private static string ChosenLightsLine(string dir, IReadOnlyList<string> frames)
    {
        string ext = frames.Count > 0 ? Path.GetExtension(frames[0]) : "";
        return $"{dir} ({RunReport.Frames(frames.Count)} {ext}; " + T("chosen by you", "alese de tine") + ")";
    }

    /// <summary>The distinct non-blank values joined by " / ".</summary>
    /// <param name="values">The values; null and blank ones are skipped.</param>
    /// <returns>The joined values, or "unknown" when there are none.</returns>
    private static string Distinct(IEnumerable<string?> values)
    {
        string[] v = values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.Ordinal).ToArray();
        return v.Length == 0 ? T("unknown", "necunoscut") : string.Join(" / ", v);
    }

    /// <summary>
    /// "119.2 s at ISO 400 (all frames)" or "... (17 of 20 frames; different exposures or ISOs)".
    /// </summary>
    /// <param name="g">The group's settings, or null.</param>
    /// <returns>The settings in words; "unknown" when no frame states them.</returns>
    private static string Settings(GroupSettings? g)
    {
        if (g is null || g.Readable == 0)
        {
            return T("unknown", "necunoscuta");
        }

        string combo = (g.ExposureSeconds is double e ? Exposure(e) + " s" : "? s") + (g.Iso is double i ? T(" at ISO ", " la ISO ") + RunReport.F(i, 0) : "");
        return g.Uniform
            ? combo + T(" (all frames)", " (toate cadrele)")
            : combo + T($" ({g.Frames} of {RunReport.Frames(g.Readable)}; the others have another exposure or ISO)", $" ({g.Frames} din {RunReport.Frames(g.Readable)}; celelalte au alta expunere sau alt ISO)");
    }

    /// <summary>A group's time span, its median interval and its long pauses.</summary>
    /// <param name="c">The group's cadence.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string Interval(Cadence c)
    {
        if (c.Start is not DateTime start || c.End is not DateTime end)
        {
            return T("unknown (the frames carry no time or exposure)", "necunoscut (cadrele nu au ora sau expunerea)");
        }

        string interval = c.MedianIntervalSeconds is double mi ? $"; {RunReport.Frames(c.TimedFrames)}" + T(", one every ", ", cate unul la ") + $"{RunReport.F(mi, 1)} s" : $"; {RunReport.Frames(c.TimedFrames)}";
        string pauses = c.LongPauses.Count switch
        {
            0 => T($"no gap over {RunReport.F(Th.PauseSeconds, 0)} s", $"fara pauze peste {RunReport.F(Th.PauseSeconds, 0)} s"),
            1 => T($"1 gap over {RunReport.F(Th.PauseSeconds, 0)} s ({RunReport.F(c.LongPauses[0].Seconds, 0)} s before frame {c.LongPauses[0].FrameIndex})", $"1 pauza peste {RunReport.F(Th.PauseSeconds, 0)} s ({RunReport.F(c.LongPauses[0].Seconds, 0)} s inainte de cadrul {c.LongPauses[0].FrameIndex})"),
            _ => Longest(c),
        };
        return $"{start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} -> {end.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} "
             + $"({RunReport.Duration(c.SpanSeconds)}){interval}; {pauses}";
    }

    /// <summary>The count of the long pauses and the longest of them.</summary>
    /// <param name="c">A cadence with at least one long pause.</param>
    /// <returns>The words, in the run's language.</returns>
    private static string Longest(Cadence c)
    {
        LongPause longest = c.LongPauses.MaxBy(p => p.Seconds)!;
        return T(
            $"{c.LongPauses.Count} gaps over {RunReport.F(Th.PauseSeconds, 0)} s (the longest {RunReport.F(longest.Seconds, 0)} s before frame {longest.FrameIndex})",
            $"{RoCount(c.LongPauses.Count)}pauze peste {RunReport.F(Th.PauseSeconds, 0)} s (cea mai lunga {RunReport.F(longest.Seconds, 0)} s inainte de cadrul {longest.FrameIndex})");
    }

    /// <summary>
    /// Check 1's match: whether the darks have the lights' exposure and ISO; the darks are the
    /// session's or, in a lights-only run, the partner's.
    /// </summary>
    /// <param name="m">Check 1's match.</param>
    /// <param name="partner">True when the darks compared are the partner's (R-054).</param>
    /// <returns>The verdict's words, opening with OK, WARNING or UNMEASURED (OK, ATENTIE or
    /// NEMASURAT in Romanian).</returns>
    private static string DarksVsLights(MatchAnalysis m, bool partner)
    {
        switch (m.DarksVsLights)
        {
            case Verdict.Unmeasured:
                return m.Darks is null ? T("UNMEASURED: there are no darks", "NEMASURAT: nu exista darks")
                    : m.Lights is null ? T("UNMEASURED: there are no lights", "NEMASURAT: nu exista lights")
                    : T("UNMEASURED: the frames do not state the exposure or the ISO", "NEMASURAT: cadrele nu spun expunerea sau ISO-ul");
            case Verdict.Ok:
                string exposure = Exposure(m.Darks!.ExposureSeconds);
                string iso = Opt(m.Darks.Iso, 0);
                return partner
                    ? T($"OK: the partner's darks have the same exposure ({exposure} s) and the same ISO ({iso}) as the lights", $"OK: dark-urile partenerului au aceeasi expunere ({exposure} s) si acelasi ISO ({iso}) ca lights-urile")
                    : T($"OK: the darks have the same exposure ({exposure} s) and the same ISO ({iso}) as the lights", $"OK: dark-urile au aceeasi expunere ({exposure} s) si acelasi ISO ({iso}) ca lights-urile");
            default:
                string settings = $"{Exposure(m.Darks!.ExposureSeconds)} s";
                string darkIso = Opt(m.Darks.Iso, 0);
                string lights = $"{Exposure(m.Lights!.ExposureSeconds)} s";
                string lightIso = Opt(m.Lights.Iso, 0);
                return partner
                    ? T($"WARNING: the partner's darks ({settings}, ISO {darkIso}) do not have the lights' settings ({lights}, ISO {lightIso})", $"ATENTIE: dark-urile partenerului ({settings}, ISO {darkIso}) nu au setarile lights-urilor ({lights}, ISO {lightIso})")
                    : T($"WARNING: the darks ({settings}, ISO {darkIso}) do not have the lights' settings ({lights}, ISO {lightIso})", $"ATENTIE: dark-urile ({settings}, ISO {darkIso}) nu au setarile lights-urilor ({lights}, ISO {lightIso})");
        }
    }

    /// <summary>
    /// The <c>bias</c> row: whether the biases' exposure is short enough to be a bias.
    /// </summary>
    /// <param name="m">Check 1's match.</param>
    /// <returns>The verdict's words, or "(none)" without biases.</returns>
    private static string Bias(MatchAnalysis m)
    {
        switch (m.Bias)
        {
            case Verdict.Unmeasured:
                return m.Biases is null ? None : T("UNMEASURED: the frames do not state the exposure", "NEMASURAT: cadrele nu spun expunerea");
            case Verdict.Ok:
                return T($"OK: {Exposure(m.Biases!.ExposureSeconds)} s, short enough for a bias", $"OK: {Exposure(m.Biases!.ExposureSeconds)} s, destul de scurt pentru un bias");
            default:
                return T(
                    $"WARNING: {Exposure(m.Biases!.ExposureSeconds)} s is too long for a bias (at most {RunReport.F(Th.BiasMaxExposureSeconds, 0)} s)",
                    $"ATENTIE: {Exposure(m.Biases!.ExposureSeconds)} s e prea lung pentru un bias (cel mult {RunReport.F(Th.BiasMaxExposureSeconds, 0)} s)");
        }
    }

    /// <summary>The value of a group that is absent: "(none)", "(lipsa)".</summary>
    private static string None => T("(none)", "(lipsa)");

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
