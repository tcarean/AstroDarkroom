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
using System.Text;
using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// The files `--out DIR` writes (R-026): the report as the console prints it, a CSV with a row
/// per dark, one per bias and one per light with every figure, and the binned maps of the
/// reference plane as PNG (the dark master, the bias master, dark minus bias), each block
/// drawn as a 4 x 4 square, stretched between its 1st and 99th percentile. Nothing is written
/// into the session.
/// </summary>
internal static class DarkOutputs
{
    // The names are English in both languages (ADR 0021).
    internal const string ReportName = "darkscheck-report.txt";
    internal const string DarksCsvName = "darkscheck-darks.csv";
    internal const string BiasesCsvName = "darkscheck-biases.csv";
    internal const string LightsCsvName = "darkscheck-lights.csv";
    internal const string DarkMapName = "darkscheck-map-dark.png";
    internal const string BiasMapName = "darkscheck-map-bias.png";
    internal const string DarkMinusBiasMapName = "darkscheck-map-dark-minus-bias.png";
    internal const int MapScale = 4;

    /// <summary>
    /// Writes every output file into <paramref name="dir"/>, creating it when missing, after
    /// refusing a folder inside the session, the partner or the chosen lights' folder
    /// (<see cref="CheckOut"/>, the rule "Read-only input"); the partner's also when the partner
    /// could not be used (<see cref="DarkSeriesResult.PartnerFolder"/>). Existing files of the same
    /// names are overwritten.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <param name="dir">The output folder.</param>
    /// <param name="wrote">
    /// Told each file's path as soon as that file is written, so a caller can say which files an
    /// export that fails part-way did write (the diagnostic log, R-101); null for none.
    /// </param>
    /// <returns>The paths written, in order; the CSVs of biases and lights and the maps only when
    /// there is something to write.</returns>
    /// <exception cref="InvalidOperationException">The folder is the session's, the partner's or
    /// the chosen lights', or lies inside one.</exception>
    internal static IReadOnlyList<string> WriteAll(DarkSeriesResult r, string dir, Action<string>? wrote = null)
    {
        ArgumentNullException.ThrowIfNull(r);

        // the partner as the run was given it, also when it failed and the result holds none
        // (R-074); the one matched, or whose darks were read (R-054), for a result made without it
        CheckOut(r.Inventory.Session, r.PartnerFolder ?? r.Partner?.Folder ?? r.DarksFromPartner, dir);
        Directory.CreateDirectory(dir);
        List<string> written = [];

        string report = Path.Combine(dir, ReportName);
        using (StreamWriter w = new(report, false, new UTF8Encoding(false)))
        {
            RunReport.WriteBanner(w);
            DarkReport.Write(r, w);
        }

        Add(written, report, wrote);

        string darksCsv = Path.Combine(dir, DarksCsvName);
        File.WriteAllText(darksCsv, DarksCsv(r), new UTF8Encoding(false));
        Add(written, darksCsv, wrote);

        if (r.Biases is BiasAnalysis b)
        {
            string biasesCsv = Path.Combine(dir, BiasesCsvName);
            File.WriteAllText(biasesCsv, BiasesCsv(b), new UTF8Encoding(false));
            Add(written, biasesCsv, wrote);
        }

        if (r.Lights is LightsAnalysis l)
        {
            string lightsCsv = Path.Combine(dir, LightsCsvName);
            File.WriteAllText(lightsCsv, LightsCsv(l, r.Darks.PlaneNames), new UTF8Encoding(false));
            Add(written, lightsCsv, wrote);
        }

        int rp = r.ReferencePlane;
        Add(written, WriteMap(Path.Combine(dir, DarkMapName), r.Darks.MeanMaps[rp]), wrote);
        if (r.Biases is BiasAnalysis bb)
        {
            Add(written, WriteMap(Path.Combine(dir, BiasMapName), bb.MeanMaps[rp]), wrote);
        }

        if (r.DarkCurrent is DarkCurrent dc)
        {
            Add(written, WriteMap(Path.Combine(dir, DarkMinusBiasMapName), dc.Maps[rp]), wrote);
        }

        return written;
    }

    /// <summary>
    /// The rule "Read-only input" for a run's output folder (R-026, R-055, R-074): it may not be
    /// one of <see cref="ReadOnlyFolders"/> or lie inside one. Called before the analysis runs (the
    /// console) and again by <see cref="WriteAll"/>, so both refuse the same folders.
    /// </summary>
    /// <param name="session">The session or library opened, with its chosen lights; not null.</param>
    /// <param name="partner">The partner's folder as it was given; null without one.</param>
    /// <param name="dir">The output folder asked for; not null or blank.</param>
    /// <exception cref="InvalidOperationException">The output folder is one of those folders or
    /// lies inside one; the message names whose and which.</exception>
    internal static void CheckOut(SessionFolder session, string? partner, string dir)
    {
        foreach ((string folder, OutFolder which) in ReadOnlyFolders(session, partner))
        {
            CheckOutDir(folder, dir, which);
        }
    }

    /// <summary>
    /// The folders nothing is written into, each with whose it is: the folder opened, the
    /// partner's and the chosen lights' (R-055), and for each of them that is a session's
    /// subfolder, the folders around it (<see cref="AroundBoth"/>): a library opened as
    /// <c>name\darks</c>, a partner given so, and lights given as <c>name\lights</c>, by the name
    /// given and by the folder it really is (ADR 0069). After those come the same three folders
    /// and their session subfolders by the paths they really have (<see cref="AddResolved"/>): a
    /// <c>darks\</c>, <c>biases\</c>, <c>lights\</c> or <c>flats\</c> may be a link to a
    /// folder elsewhere, which the folder itself does not cover.
    /// </summary>
    /// <param name="session">The session or library opened; not null.</param>
    /// <param name="partner">The partner's folder as it was given; null or blank without one. A
    /// path that cannot be made full names no folder and is left out.</param>
    /// <returns>The folders as full paths: first as they were given, the session's first, so a
    /// refusal names a folder that was typed (these may repeat: a partner that is the session's
    /// own folder); then the resolved ones that are not in the list yet.</returns>
    internal static IReadOnlyList<(string Folder, OutFolder Which)> ReadOnlyFolders(SessionFolder session, string? partner)
    {
        ArgumentNullException.ThrowIfNull(session);
        List<(string Folder, OutFolder Which)> folders = [(session.Root, OutFolder.Session)];
        List<(string Folder, OutFolder Which)> resolved = [];

        // the darks listed in the folder opened, and that folder named darks: the library around it
        if (session.DarksDir == session.Root)
        {
            folders.AddRange(AroundBoth(session.Root, SessionFolder.DarksName).Select(f => (f, OutFolder.Session)));
        }

        AddResolved(resolved, session.Root, OutFolder.Session);
        if (!string.IsNullOrWhiteSpace(partner))
        {
            try
            {
                string full = FolderIdentity.Full(partner);
                folders.Add((full, OutFolder.Partner));
                folders.AddRange(AroundBoth(full, SessionFolder.DarksName).Select(f => (f, OutFolder.Partner)));
                AddResolved(resolved, full, OutFolder.Partner);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException)
            {
                // a partner whose path is no path was never opened: there is no folder to keep clear
            }
        }

        if (session.ChosenLightsRoot is string lights)
        {
            folders.Add((lights, OutFolder.ChosenLights));

            // the lights listed in the folder given, and that folder named lights: the session around it
            if (session.LightsDir == lights)
            {
                folders.AddRange(AroundBoth(lights, SessionFolder.LightsName).Select(f => (f, OutFolder.ChosenLights)));
            }

            AddResolved(resolved, lights, OutFolder.ChosenLights);
        }

        // after the folders as they were given, so a refusal names a folder that was typed
        foreach ((string folder, OutFolder which) in resolved)
        {
            if (!folders.Exists(f => string.Equals(f.Folder, folder, StringComparison.OrdinalIgnoreCase)))
            {
                folders.Add((folder, which));
            }
        }

        return folders;
    }

    /// <summary>
    /// The rule "Read-only input" for one folder: the output folder may not be that folder or one
    /// under it, whichever name of the folder it was reached through
    /// (<see cref="FolderIdentity.IsOrInside"/>).
    /// </summary>
    /// <param name="sessionRoot">The folder nothing may be written into; not null or blank.</param>
    /// <param name="dir">The output folder asked for; not null or blank.</param>
    /// <param name="which">Whose folder it is, which names it in the refusal; the session's by
    /// default.</param>
    /// <exception cref="InvalidOperationException">The output folder is that folder or lies inside
    /// it.</exception>
    internal static void CheckOutDir(string sessionRoot, string dir, OutFolder which = OutFolder.Session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(dir);
        string root = FolderIdentity.Full(sessionRoot);
        if (FolderIdentity.IsOrInside(dir, root))
        {
            throw new InvalidOperationException(which switch
            {
                OutFolder.Partner => T($"--out cannot be the partner's folder or one inside it ({root}): nothing is written there", $"--out nu poate fi folderul partenerului sau unul din el ({root}): acolo nu se scrie nimic"),
                OutFolder.ChosenLights => T($"--out cannot be the chosen lights' folder or one inside it ({root}): nothing is written there", $"--out nu poate fi folderul lights-urilor alese sau unul din el ({root}): acolo nu se scrie nimic"),
                _ => T($"--out cannot be the session's folder or one inside it ({root}): nothing is written there", $"--out nu poate fi folderul sesiunii sau unul din el ({root}): acolo nu se scrie nimic"),
            });
        }
    }

    /// <summary>
    /// The folder a folder named as a session's subfolder stands for (R-026, ADR 0069): the folder
    /// around it. A library kept as <c>name\darks</c> is the library <c>name</c> (R-061), and
    /// lights given as <c>name\lights</c> are the session <c>name</c>'s, so nothing is written
    /// into <c>name</c> either, as when <c>name</c> itself is given.
    /// </summary>
    /// <param name="folder">The folder, by the path whose last name is to be read: as given, or
    /// as it really is (<see cref="FolderIdentity.Real"/>); not null or blank.</param>
    /// <param name="subfolder">The name that makes it a session's subfolder, <c>darks</c> or
    /// <c>lights</c>, compared without case.</param>
    /// <returns>The full path of the folder around it; null when the folder has another name, or
    /// when the folder around it is a drive's root, which is no session.</returns>
    internal static string? Around(string folder, string subfolder)
    {
        string full = FolderIdentity.Full(folder);
        if (!string.Equals(Path.GetFileName(full), subfolder, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? parent = Path.GetDirectoryName(full);
        return parent is null || string.Equals(Path.GetPathRoot(parent), parent, StringComparison.OrdinalIgnoreCase) ? null : parent;
    }

    /// <summary>
    /// Adds a folder and its session subfolders by the paths they really have (CR-080): the
    /// folder itself, each of <see cref="SessionFolder.SubfolderNames"/> that exists under it
    /// (<see cref="FolderIdentity.Real"/>, every link followed), and for a <c>darks</c> or
    /// <c>lights</c> subfolder that really has that name the folder around it. Every subfolder
    /// counts, whether the run reads frames from it or not: a session's own <c>lights\</c> with
    /// the lights chosen elsewhere, its <c>flats\</c> and a <c>biases\</c> without a frame are
    /// the session's all the same. A subfolder that is no link is added by its own path too: where
    /// the system cannot name the folder a link leads to, the real path is the typed one, and the
    /// entry still tells that folder by its identity; and where a folder has no identity, the
    /// path is all that tells it.
    /// </summary>
    /// <param name="resolved">The list to add to, which may come to hold a folder twice; not
    /// null.</param>
    /// <param name="root">The full path of the folder opened, the partner's or the chosen
    /// lights'.</param>
    /// <param name="which">Whose folder it is, for the refusal.</param>
    private static void AddResolved(List<(string Folder, OutFolder Which)> resolved, string root, OutFolder which)
    {
        resolved.Add((FolderIdentity.Real(root), which));
        foreach (string name in SessionFolder.SubfolderNames)
        {
            string sub = Path.Combine(root, name);
            if (!Directory.Exists(sub))
            {
                continue;
            }

            string real = FolderIdentity.Real(sub);
            resolved.Add((real, which));

            // the biases and the flats stand for no folder around them (ADR 0069)
            if ((name is SessionFolder.DarksName or SessionFolder.LightsName) && Around(real, name) is string around)
            {
                resolved.Add((around, which));
            }
        }
    }

    /// <summary>
    /// The folders around a folder that is a session's subfolder, by both of its names: the one it
    /// was given by (a link named <c>darks</c> in a session's layout stands for that session, as
    /// R-061 labels it) and the one it really has (a library's <c>darks</c> folder reached through
    /// a link of another name still stands for the library). One took the other's place for a
    /// commit, and each time the folder left out was written into.
    /// </summary>
    /// <param name="folder">The folder given; not null or blank.</param>
    /// <param name="subfolder">The name that makes it a session's subfolder, <c>darks</c> or
    /// <c>lights</c>.</param>
    /// <returns>None, one or two full paths, the one around the name as given first.</returns>
    private static IEnumerable<string> AroundBoth(string folder, string subfolder)
    {
        string? typed = Around(folder, subfolder);
        string? real = Around(FolderIdentity.Real(folder), subfolder);
        if (typed is not null)
        {
            yield return typed;
        }

        if (real is not null && !string.Equals(real, typed, StringComparison.OrdinalIgnoreCase))
        {
            yield return real;
        }
    }

    /// <summary>Whose folder <see cref="CheckOutDir"/> refuses to write into; it names the folder in the refusal.</summary>
    internal enum OutFolder
    {
        /// <summary>The session or library opened.</summary>
        Session,

        /// <summary>The partner of `--against` (R-063), or the one whose darks stand in (R-054).</summary>
        Partner,

        /// <summary>The folder the lights were chosen from (R-055).</summary>
        ChosenLights,
    }

    /// <summary>
    /// One row per dark: the metadata, then per plane the levels and the residual figures, then the
    /// amplitude ratio and the edges; a row without figures is padded to the header's width.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The CSV text, a header line then one line per dark, invariant culture, text
    /// quoted.</returns>
    internal static string DarksCsv(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DarkSeriesAnalysis d = r.Darks;
        StringBuilder sb = new();
        List<string> head = ["index", "file", "time", "exposure_s", "iso", "error", "skipped", "mean_above_black"];
        foreach (string p in d.PlaneNames)
        {
            head.AddRange([$"{p}_median", $"{p}_sigma", $"{p}_mean", $"{p}_p999", $"{p}_above10", $"{p}_above20", $"{p}_above50", $"{p}_above100", $"{p}_above500", $"{p}_saturated", $"{p}_low", $"{p}_min", $"{p}_max",
                $"{p}_resid_sigma", $"{p}_resid_median", $"{p}_resid_mean", $"{p}_row_std", $"{p}_row_expected", $"{p}_col_std", $"{p}_col_expected"]);
        }

        head.AddRange(["amp_ratio_median", "amp_ratio_q25", "amp_ratio_q75", "edge_left", "edge_right", "edge_top", "edge_bottom", "edge_top_left", "edge_top_right", "edge_bottom_left", "edge_bottom_right", "excess_light"]);
        sb.AppendLine(string.Join(",", head));
        IReadOnlyList<double>? excess = r.Verdicts.Leak.Frames?.ExcessAdu;
        for (int k = 0; k < d.Frames.Count; k++)
        {
            DarkFrameFigures f = d.Frames[k];
            List<string> row = [f.Info.Index.ToString(CultureInfo.InvariantCulture), Q(f.Info.FileName), f.Info.Timestamp is DateTime t ? t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "", N(f.Info.ExposureSeconds), N(f.Info.Iso), Q(f.Skipped ? "" : f.Error ?? ""), Q(f.Skipped ? f.Error ?? "" : ""), N(f.MeanAboveBlack)];
            if (f.Error is null)
            {
                for (int p = 0; p < 4; p++)
                {
                    PlaneFigures pf = f.Planes[p];
                    ResidualFigures rf = f.Residuals[p];
                    row.AddRange([N(pf.Median), N(pf.Sigma), N(pf.Mean), N(pf.P999), .. pf.Above.Select(a => a.ToString(CultureInfo.InvariantCulture)), pf.Saturated.ToString(CultureInfo.InvariantCulture), pf.Low.ToString(CultureInfo.InvariantCulture), pf.Min.ToString(CultureInfo.InvariantCulture), pf.Max.ToString(CultureInfo.InvariantCulture),
                        N(rf.Sigma), N(rf.Median), N(rf.Mean), N(rf.RowStd), N(rf.RowExpected), N(rf.ColStd), N(rf.ColExpected)]);
                }

                row.AddRange([N(f.AmpRatioMedian), N(f.AmpRatioQ25), N(f.AmpRatioQ75), N(f.Edges?.Left), N(f.Edges?.Right), N(f.Edges?.Top), N(f.Edges?.Bottom), N(f.Edges?.TopLeft), N(f.Edges?.TopRight), N(f.Edges?.BottomLeft), N(f.Edges?.BottomRight), N(excess?[k])]);
            }

            Pad(row, head.Count);
            sb.AppendLine(string.Join(",", row));
        }

        return sb.ToString();
    }

    /// <summary>
    /// One row per bias: the metadata, then per plane the levels; a row without figures is padded
    /// to the header's width.
    /// </summary>
    /// <param name="b">The biases' figures; not null.</param>
    /// <returns>The CSV text, a header line then one line per bias, invariant culture, text
    /// quoted.</returns>
    internal static string BiasesCsv(BiasAnalysis b)
    {
        ArgumentNullException.ThrowIfNull(b);
        StringBuilder sb = new();
        List<string> head = ["index", "file", "time", "exposure_s", "iso", "error", "skipped"];
        foreach (string p in b.PlaneNames)
        {
            head.AddRange([$"{p}_median", $"{p}_sigma", $"{p}_mean", $"{p}_p999", $"{p}_saturated", $"{p}_low", $"{p}_min", $"{p}_max"]);
        }

        sb.AppendLine(string.Join(",", head));
        foreach (BiasFrameFigures f in b.Frames)
        {
            List<string> row = [f.Info.Index.ToString(CultureInfo.InvariantCulture), Q(f.Info.FileName), f.Info.Timestamp is DateTime t ? t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "", N(f.Info.ExposureSeconds), N(f.Info.Iso), Q(f.Skipped ? "" : f.Error ?? ""), Q(f.Skipped ? f.Error ?? "" : "")];
            if (f.Error is null)
            {
                foreach (PlaneFigures pf in f.Planes)
                {
                    row.AddRange([N(pf.Median), N(pf.Sigma), N(pf.Mean), N(pf.P999), pf.Saturated.ToString(CultureInfo.InvariantCulture), pf.Low.ToString(CultureInfo.InvariantCulture), pf.Min.ToString(CultureInfo.InvariantCulture), pf.Max.ToString(CultureInfo.InvariantCulture)]);
                }
            }

            Pad(row, head.Count);
            sb.AppendLine(string.Join(",", row));
        }

        return sb.ToString();
    }

    /// <summary>
    /// One row per light: the metadata, the sky per plane, the candidates, the isolated hot pixels,
    /// the amplitude ratio and its quartiles.
    /// </summary>
    /// <param name="l">The lights' figures; not null.</param>
    /// <param name="planeNames">The names of the four CFA positions, for the column names; not
    /// null.</param>
    /// <returns>The CSV text, a header line then one line per light, invariant culture, text
    /// quoted.</returns>
    internal static string LightsCsv(LightsAnalysis l, IReadOnlyList<string> planeNames)
    {
        ArgumentNullException.ThrowIfNull(l);
        ArgumentNullException.ThrowIfNull(planeNames);
        StringBuilder sb = new();
        List<string> head = ["index", "file", "time", "exposure_s", "iso", "error"];
        foreach (string p in planeNames)
        {
            head.AddRange([$"{p}_sky_median", $"{p}_sky_sigma", $"{p}_local_sigma"]);
        }

        head.AddRange(["candidates", "isolated_hot", "amp_ratio_median", "amp_ratio_q25", "amp_ratio_q75"]);
        sb.AppendLine(string.Join(",", head));
        foreach (LightFigures f in l.Frames)
        {
            List<string> row = [f.Info.Index.ToString(CultureInfo.InvariantCulture), Q(f.Info.FileName), f.Info.Timestamp is DateTime t ? t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "", N(f.Info.ExposureSeconds), N(f.Info.Iso), Q(f.Error ?? "")];
            if (f.Error is null)
            {
                for (int p = 0; p < f.SkyMedian.Count; p++)
                {
                    row.AddRange([N(f.SkyMedian[p]), N(f.SkySigma[p]), N(f.LocalSigma[p])]);
                }

                row.AddRange([f.Candidates.ToString(CultureInfo.InvariantCulture), f.IsolatedHot.ToString(CultureInfo.InvariantCulture), N(f.AmpRatioMedian), N(f.AmpRatioQ25), N(f.AmpRatioQ75)]);
            }

            Pad(row, head.Count);
            sb.AppendLine(string.Join(",", row));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Adds a written path to the list and tells the caller; a null path (nothing written) is left
    /// out.
    /// </summary>
    /// <param name="written">The list of paths written.</param>
    /// <param name="path">The path, or null.</param>
    /// <param name="wrote">Told the path when there is one; null for none.</param>
    private static void Add(List<string> written, string? path, Action<string>? wrote)
    {
        if (path is not null)
        {
            written.Add(path);
            wrote?.Invoke(path);
        }
    }

    /// <summary>Appends empty cells until the row has as many as the header.</summary>
    /// <param name="row">The row's cells.</param>
    /// <param name="columns">The header's number of columns.</param>
    private static void Pad(List<string> row, int columns)
    {
        while (row.Count < columns)
        {
            row.Add("");
        }
    }

    /// <summary>
    /// A binned map as a grey PNG (8-bit RGB, the three components equal), each block a MapScale x
    /// MapScale square, black at the 1st percentile and white at the 99th; null (nothing written)
    /// for a map without blocks.
    /// </summary>
    /// <param name="path">The PNG's path; an existing file is overwritten.</param>
    /// <param name="m">The binned map; not null.</param>
    /// <returns>The path written, or null.</returns>
    internal static string? WriteMap(string path, BinnedMap m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (MapRaster(m) is not (byte[] grey, int w, int h))
        {
            return null;
        }

        File.WriteAllBytes(path, PngWriter.EncodeGrey(grey, w, h));
        return path;
    }

    /// <summary>
    /// The map as an 8-bit grey raster, each block a <see cref="MapScale"/> x
    /// <see cref="MapScale"/> square stretched from the 1st to the 99th percentile (what the PNG
    /// holds and what the window shows); null for a map without blocks.
    /// </summary>
    /// <param name="m">The binned map; not null.</param>
    /// <returns>The grey bytes, row-major, with the raster's width and height in pixels; null for
    /// an empty map.</returns>
    internal static (byte[] Grey, int W, int H)? MapRaster(BinnedMap m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (m.W == 0 || m.H == 0)
        {
            return null;
        }

        float[] v = [.. m.Values];
        double lo = Planes.Percentile(v, 1), hi = Planes.Percentile(v, 99);
        if (hi <= lo)
        {
            hi = lo + 1;
        }

        int w = m.W * MapScale, h = m.H * MapScale;
        byte[] grey = new byte[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double t = (m.Values[(y / MapScale) * m.W + x / MapScale] - lo) / (hi - lo);
                grey[y * w + x] = (byte)Math.Clamp(Math.Round(t * 255), 0, 255);
            }
        }

        return (grey, w, h);
    }

    /// <summary>
    /// A CSV number: round-trip format, invariant culture; empty for null or NaN.
    /// </summary>
    /// <param name="v">The number, or null.</param>
    /// <returns>The cell's text.</returns>
    private static string N(double? v) => v is double d && !double.IsNaN(d) ? d.ToString("R", CultureInfo.InvariantCulture) : "";

    /// <summary>A CSV text cell: in double quotes, an inner quote doubled.</summary>
    /// <param name="s">The text.</param>
    /// <returns>The quoted cell.</returns>
    private static string Q(string s) => "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
