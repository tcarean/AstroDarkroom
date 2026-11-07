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
    /// refusing a folder inside the session or the partner (the rule "Read-only input"). Existing
    /// files of the same names are overwritten.
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
        CheckOutDir(r.Inventory.Session.Root, dir, OutFolder.Session);
        if (r.Partner is PartnerAnalysis partner)
        {
            CheckOutDir(partner.Folder, dir, OutFolder.Partner);
        }

        if (r.DarksFromPartner is string darksPartner)
        {
            // a lights-only run: the partner's folder is the one whose darks were read (R-054)
            CheckOutDir(darksPartner, dir, OutFolder.Partner);
        }

        if (r.Inventory.Session.ChosenLightsRoot is string chosenLights)
        {
            // lights from another folder (R-055): nothing is written there either
            CheckOutDir(chosenLights, dir, OutFolder.ChosenLights);
        }

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
    /// The rule "Read-only input": the output folder may not be the session folder or one under
    /// it. Called before the analysis runs (the console) and again by <see cref="WriteAll"/>.
    /// </summary>
    /// <param name="sessionRoot">The folder nothing may be written into; not null or blank.</param>
    /// <param name="dir">The output folder asked for; not null or blank.</param>
    /// <param name="which">Whose folder it is, which names it in the refusal; the session's by
    /// default.</param>
    /// <exception cref="InvalidOperationException">The output folder is that folder or lies inside
    /// it (compared without case).</exception>
    internal static void CheckOutDir(string sessionRoot, string dir, OutFolder which = OutFolder.Session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(dir);
        string full = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string root = Path.GetFullPath(sessionRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase) || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(which switch
            {
                OutFolder.Partner => T($"--out cannot be the partner's folder or one inside it ({root}): nothing is written there", $"--out nu poate fi folderul partenerului sau unul din el ({root}): acolo nu se scrie nimic"),
                OutFolder.ChosenLights => T($"--out cannot be the chosen lights' folder or one inside it ({root}): nothing is written there", $"--out nu poate fi folderul lights-urilor alese sau unul din el ({root}): acolo nu se scrie nimic"),
                _ => T($"--out cannot be the session's folder or one inside it ({root}): nothing is written there", $"--out nu poate fi folderul sesiunii sau unul din el ({root}): acolo nu se scrie nimic"),
            });
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
        List<string> head = ["index", "file", "time", "exposure_s", "iso", "error", "skipped"];
        foreach (string p in planeNames)
        {
            head.AddRange([$"{p}_sky_median", $"{p}_sky_sigma", $"{p}_local_sigma"]);
        }

        head.AddRange(["candidates", "isolated_hot", "amp_ratio_median", "amp_ratio_q25", "amp_ratio_q75"]);
        sb.AppendLine(string.Join(",", head));
        foreach (LightFigures f in l.Frames)
        {
            List<string> row = [f.Info.Index.ToString(CultureInfo.InvariantCulture), Q(f.Info.FileName), f.Info.Timestamp is DateTime t ? t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "", N(f.Info.ExposureSeconds), N(f.Info.Iso), Q(f.Skipped ? "" : f.Error ?? ""), Q(f.Skipped ? f.Error ?? "" : "")];
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
