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
using static LightsCheck.Core.Ui;

namespace LightsCheck.Core;

/// <summary>
/// The inventory as the console prints it (R-003, R-006), in the run's language. Words only; the
/// numbers come from the run.
/// </summary>
internal static class InventoryReport
{
    /// <summary>
    /// Writes the whole inventory: the session lines, then the table of the lights.
    /// </summary>
    /// <param name="inv">The inventory of the session; not null.</param>
    /// <param name="w">Where the lines go; not null.</param>
    internal static void Write(Inventory inv, TextWriter w)
    {
        WriteHeader(inv, w);
        WriteTable(inv, w);
    }

    /// <summary>
    /// Writes the session lines: folders, camera, lens, exposure, interval, and a warning line
    /// when some lights could not be read; the series report reuses them.
    /// </summary>
    /// <param name="inv">The inventory of the session; not null.</param>
    /// <param name="w">Where the lines go; not null.</param>
    internal static void WriteHeader(Inventory inv, TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(inv);
        ArgumentNullException.ThrowIfNull(w);
        SessionFolder s = inv.Session;
        w.WriteLine();
        string missing = T("(missing)", "(lipsa)");
        w.WriteLine(T($"=== SESSION: {s.Root} ===", $"=== SESIUNE: {s.Root} ==="));
        w.WriteLine($"    lights             : {FolderLine(s.Root, s.LightsDir, s.Lights)}");
        w.WriteLine($"    biases             : {(s.BiasesDir is null ? missing : FolderLine(s.Root, s.BiasesDir, s.Biases))}");
        w.WriteLine($"    darks              : {(s.DarksDir is null ? missing : FolderLine(s.Root, s.DarksDir, s.Darks))}");
        w.WriteLine(T($"    camera             : {Distinct(inv.Lights.Select(f => f.Camera))}", $"    aparat             : {Distinct(inv.Lights.Select(f => f.Camera))}"));
        w.WriteLine(T($"    lens               : {Lens(inv.Lights)}", $"    obiectiv           : {Lens(inv.Lights)}"));
        w.WriteLine(T($"    exposure           : {Exposure(inv.Lights)}", $"    expunere           : {Exposure(inv.Lights)}"));
        w.WriteLine($"    interval           : {Interval(inv)}");
        if (inv.HasErrors)
        {
            int n = inv.Lights.Count(f => f.Error is not null);
            w.WriteLine(T(
                $"    WARNING            : {n} {(n == 1 ? "frame could not be read" : "frames could not be read")} (see the list of frames)",
                $"    ATENTIE            : {n} {(n == 1 ? "cadru nu s-a putut citi" : "cadre nu s-au putut citi")} (vezi lista cadrelor)"));
        }
    }

    /// <summary>
    /// Writes one row per light: the metadata and the pause before it; a light that could not
    /// be read gets its error instead. File names are cut to 20 characters.
    /// </summary>
    /// <param name="inv">The inventory of the session; not null.</param>
    /// <param name="w">Where the lines go; not null.</param>
    internal static void WriteTable(Inventory inv, TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(inv);
        ArgumentNullException.ThrowIfNull(w);
        w.WriteLine();
        w.WriteLine(T("  FRAMES", "  CADRE"));
        w.WriteLine(T(
            $"    {"no",3}  {"file",-20}  {"time",-8}  {"exposure",8}  {"ISO",5}  {"f/",4}  {"focal",7}  {"pause",6}",
            "     nr  fisier                ora       expunere   ISO    f/   focala   pauza"));
        for (int i = 0; i < inv.Lights.Count; i++)
        {
            FrameInfo f = inv.Lights[i];
            string name = f.FileName.Length > 20 ? f.FileName[..20] : f.FileName;
            if (f.Error is not null)
            {
                w.WriteLine($"    {f.Index,3}  {name,-20}  {T("error", "eroare")}: {f.Error}");
                continue;
            }

            double? pause = inv.Cadence.PauseSeconds[i];
            w.WriteLine(
                $"    {f.Index,3}  {name,-20}  {Time(f.Timestamp),-8}  {Opt(f.ExposureSeconds, 1),8}  {Opt(f.Iso, 0),5}  {Opt(f.FNumber, 1),4}  {Opt(f.FocalMm, 0),4} mm  {(pause is double p ? RunReport.F(p, 0) + " s" : "-"),6}");
        }
    }

    /// <summary>
    /// One folder's cell: its path relative to the session, its frame count and extension.
    /// </summary>
    /// <param name="root">The session folder.</param>
    /// <param name="dir">
    /// The folder of the frames; the session folder itself is written ".".
    /// </param>
    /// <param name="frames">The frames found there; the first one's extension is shown.</param>
    /// <returns>"lights\ (87 frames .NEF)", or ". (1 frame .NEF)" for frames at the root.</returns>
    private static string FolderLine(string root, string dir, IReadOnlyList<string> frames)
    {
        string rel = dir == root ? "." : Path.GetRelativePath(root, dir) + "\\";
        string ext = frames.Count > 0 ? Path.GetExtension(frames[0]) : "";
        return $"{rel} ({frames.Count} {(frames.Count == 1 ? T("frame", "cadru") : T("frames", "cadre"))} {ext})";
    }

    /// <summary>The distinct non-blank values (compared ordinally), joined by " / ".</summary>
    /// <param name="values">The values, one per frame; null and blank ones are skipped.</param>
    /// <returns>The joined values, or "unknown" when none is left.</returns>
    private static string Distinct(IEnumerable<string?> values)
    {
        string[] v = values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.Ordinal).ToArray();
        return v.Length == 0 ? T("unknown", "necunoscut") : string.Join(" / ", v);
    }

    /// <summary>
    /// The lens cell, from the first readable frame that has a focal length, a lens ID or a lens
    /// name: the shot's focal length and f-number, the lens's own range and widest aperture, and
    /// the lens as the raw names it (R-065): its name in quotes with the ID and the mount in one
    /// parenthesis, or the ID with the mount when the raw gives no name; a mount alone is
    /// nothing, since without an ID or a name it says nothing about the lens.
    /// </summary>
    /// <param name="frames">The lights of the session.</param>
    /// <returns>
    /// The parts joined by ", ", or "unknown" when no frame carries any of them.
    /// </returns>
    private static string Lens(IReadOnlyList<FrameInfo> frames)
    {
        FrameInfo? f = frames.FirstOrDefault(x => x.Error is null && (x.FocalMm is not null || x.LensId is not null || x.LensName is not null));
        if (f is null)
        {
            return T("unknown", "necunoscut");
        }

        List<string> parts = [];
        if (f.FocalMm is double focal)
        {
            parts.Add(RunReport.F(focal, 0) + " mm" + (f.FNumber is double fn ? " f/" + RunReport.F(fn, 1) : "") + T(" (when shot)", " (la fotografiere)"));
        }

        // The lens as its chip declares it: "135 mm f/2.0 lens" ("obiectiv de 135 mm f/2.0") for
        // a prime, "11-20 mm f/2.8 lens" for a zoom; no English article, which would depend on
        // how the number is said ("an 11-20 mm").
        if (f.LensMinFocalMm is double lo && f.LensMaxFocalMm is double hi)
        {
            string range = lo == hi ? RunReport.F(lo, 0) : RunReport.F(lo, 0) + "-" + RunReport.F(hi, 0);
            string lens = range + " mm" + (f.LensMaxAperture is double ap ? " f/" + RunReport.F(ap, 1) : "");
            parts.Add(T($"{lens} lens", $"obiectiv de {lens}"));
        }

        // The lens as the raw names it: "EF50mm f/1.4 USM" (ID 198, Canon EF mount), ID 74 (Nikon F
        // mount), "FE 16-35mm F2.8 GM" (Sony E mount). "ID" is one word in both languages.
        string id = f.LensId is long n ? "ID " + n.ToString(CultureInfo.InvariantCulture) : "";
        string? mount = f.LensMount is string m ? T($"{m} mount", $"montura {m}") : null;
        if (f.LensName is string name)
        {
            string inside = string.Join(", ", new[] { id, mount }.Where(p => !string.IsNullOrEmpty(p)));
            parts.Add("\"" + name + "\"" + (inside.Length > 0 ? " (" + inside + ")" : ""));
        }
        else if (id.Length > 0)
        {
            parts.Add(id + (mount is not null ? " (" + mount + ")" : ""));
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// The exposure cell: each distinct pair of exposure time and ISO of the readable lights.
    /// </summary>
    /// <param name="frames">The lights of the session; the unreadable ones are skipped.</param>
    /// <returns>
    /// One pair marked as shared by every frame, several pairs marked as different exposures,
    /// or "unknown" when no light could be read.
    /// </returns>
    private static string Exposure(IReadOnlyList<FrameInfo> frames)
    {
        string[] combos = frames.Where(f => f.Error is null)
            .Select(f => (f.ExposureSeconds is double e ? RunReport.F(e, 1) + " s" : "? s") + (f.Iso is double i ? T(" at ISO ", " la ISO ") + RunReport.F(i, 0) : ""))
            .Distinct(StringComparer.Ordinal).ToArray();
        return combos.Length switch
        {
            0 => T("unknown", "necunoscuta"),
            1 => combos[0] + T(" (all frames)", " (toate cadrele)"),
            _ => string.Join("; ", combos) + T(" (different exposures)", " (expuneri diferite)"),
        };
    }

    /// <summary>
    /// The interval cell, in plain words (R-078): first and last time of capture, the span, the
    /// total exposure, the timed frame count and the share of the span spent exposing.
    /// </summary>
    /// <param name="inv">The inventory, whose cadence holds the figures.</param>
    /// <returns>
    /// `from ... to ... (3 h 22 min); total exposure 2 h 54 min, from 87 frames (the camera was
    /// exposing 86 % of the time)`, or an "unknown" sentence when the frames carry no time.
    /// </returns>
    private static string Interval(Inventory inv)
    {
        Cadence c = inv.Cadence;
        if (c.Start is not DateTime start || c.End is not DateTime end)
        {
            return T("unknown (the frames carry no time)", "necunoscut (cadrele nu au ora)");
        }

        string from = start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), to = end.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string length = RunReport.Duration(c.SpanSeconds), total = RunReport.Duration(c.IntegrationSeconds), duty = RunReport.F(c.DutyPct, 0);
        string frames = c.TimedFrames == 1 ? T("1 frame", "1 cadru") : T($"{c.TimedFrames} frames", $"{c.TimedFrames} cadre");
        return T(
            $"from {from} to {to} ({length}); total exposure {total}, from {frames} (the camera was exposing {duty} % of the time)",
            $"de la {from} pana la {to} ({length}); expunere totala {total}, din {frames} (aparatul a expus {duty} % din timp)");
    }

    /// <summary>A time of capture as HH:mm:ss, or "-".</summary>
    /// <param name="t">The time of capture; null when the frame has none.</param>
    /// <returns>The time of day, or "-" when <paramref name="t"/> is null.</returns>
    private static string Time(DateTime? t) => t is DateTime d ? d.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "-";

    /// <summary>An optional figure with a fixed number of decimals, or "-".</summary>
    /// <param name="v">The figure; null when the frame does not carry it.</param>
    /// <param name="decimals">How many decimals to write.</param>
    /// <returns>The formatted figure, or "-" when <paramref name="v"/> is null.</returns>
    private static string Opt(double? v, int decimals) => v is double d ? RunReport.F(d, decimals) : "-";
}
