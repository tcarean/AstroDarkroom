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

namespace LightsCheck.Core;

/// <summary>
/// The files a series run writes on request (R-028): the report as the console prints it,
/// a CSV with every figure per frame, and the plain list of the file names to leave out of
/// the stack. Never inside lights/ or biases/ (ADR 0002): the caller names the folder.
/// </summary>
internal static class SeriesOutputs
{
    internal const string ReportName = "lightscheck-raport.txt";
    internal const string CsvName = "lightscheck-cadre.csv";
    internal const string RejectedName = "lightscheck-respinse.txt";

    /// <summary>Writes the three files into <paramref name="dir"/> and returns their paths.</summary>
    internal static IReadOnlyList<string> WriteAll(SeriesResult r, string dir)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentException.ThrowIfNullOrWhiteSpace(dir);
        Directory.CreateDirectory(dir);
        string report = Path.Combine(dir, ReportName), csv = Path.Combine(dir, CsvName), rejected = Path.Combine(dir, RejectedName);
        using (StreamWriter w = new(report, false, new UTF8Encoding(false)))
        {
            RunReport.WriteBanner(w);
            SeriesReport.Write(r, w);
        }

        File.WriteAllText(csv, Csv(r), new UTF8Encoding(false));
        File.WriteAllText(rejected, Rejected(r), new UTF8Encoding(false));
        return [report, csv, rejected];
    }

    /// <summary>One line per frame, the file names of the bad and unreadable frames, in order.</summary>
    internal static string Rejected(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        StringBuilder sb = new();
        for (int i = 0; i < r.Frames.Count; i++)
        {
            if (r.Series.Classes[i] is FrameClass.Bad or FrameClass.Unknown)
            {
                sb.Append(r.Frames[i].Info.FileName).Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>Every figure per frame, comma-separated, the invariant culture, a header row.</summary>
    internal static string Csv(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        CultureInfo inv = CultureInfo.InvariantCulture;
        StringBuilder sb = new();
        sb.Append("index,file,time,exposure_s,iso,fnumber,focal_mm,class,reasons,pause_s,step_px,step_x,step_y,dx,dy,rotation_deg,matches,rms,")
          .Append("detected,bright,saturated_stars,measured,fwhm_med,fwhm_p90,fwhm_centre,fwhm_mid,fwhm_edge,elong_med,elong_p90,elongated_frac,angle_deg,coherence,trail_on,halo,")
          .Append("bg_median,bg_centre,bg_min,bg_max,sigma_green,ch0_name,ch0_median,ch0_sat,ch0_hp_sigma,ch1_name,ch1_median,ch1_sat,ch1_hp_sigma,ch2_name,ch2_median,ch2_sat,ch2_hp_sigma,ch3_name,ch3_median,ch3_sat,ch3_hp_sigma,trails,nearest_other_px,spot_run_index,spot_run_frames,spot_run_first,move_ra_px,move_dec_px,move_arrows,error\n");
        for (int i = 0; i < r.Frames.Count; i++)
        {
            AnalyzedFrame f = r.Frames[i];
            FrameMeasurement? m = f.Measurement;
            RegistrationResult? g = f.Registration is { Ok: true } rg ? rg : null;
            FrameFlag? trail = r.Series.Flags[i].FirstOrDefault(fl => fl.Kind is FlagKind.Trailed or FlagKind.MarginalTrail);
            SpotRun? spot = r.Dither.SpotRun[i];
            List<string> cells =
            [
                f.Info.Index.ToString(inv), Q(f.Info.FileName), f.Info.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ss", inv) ?? "",
                N(f.Info.ExposureSeconds), N(f.Info.Iso), N(f.Info.FNumber), N(f.Info.FocalMm),
                SeriesReport.ClassWord(r.Series.Classes[i]), Q(SeriesReport.Reasons(r.Series.Flags[i])),
                N(r.Cadence.PauseSeconds[i]), N(r.Dither.Step[i]), N(r.Dither.StepX[i]), N(r.Dither.StepY[i]),
                N(g?.Dx), N(g?.Dy), N(g?.RotationDeg), g is null ? "" : g.Matches.ToString(inv), N(g?.Rms),
                m is null ? "" : m.Detected.ToString(inv), m is null ? "" : m.DetectedBright.ToString(inv), m is null ? "" : m.SaturatedStars.ToString(inv), m is null ? "" : m.Shapes.Count.ToString(inv),
                N(m?.FwhmMedian), N(m?.FwhmP90), N(m?.Centre.Fwhm), N(m?.Mid.Fwhm), N(m?.Edge.Fwhm), N(m?.ElongationMedian), N(m?.ElongationP90), N(m?.ElongatedFraction), N(m?.AngleDeg), N(m?.AngleCoherence), TrailWord(trail?.On ?? TrailAxis.Unknown), N(m?.Halo),
                N(m?.BackgroundMedian), N(m?.BackgroundCentre), N(m?.BackgroundMin), N(m?.BackgroundMax), N(m?.NoiseSigma),
            ];
            for (int c = 0; c < 4; c++)
            {
                ChannelLevel? ch = m is not null && c < m.Channels.Count ? m.Channels[c] : null;
                cells.Add(ch?.Name ?? "");
                cells.Add(N(ch?.Median));
                cells.Add(ch is null ? "" : ch.Saturated.ToString(inv));
                cells.Add(N(ch?.HighPassSigma));
            }

            cells.Add(m is null ? "" : m.Trails.Count.ToString(inv));
            cells.Add(N(r.Dither.NearestOther[i]));
            cells.Add(spot is null ? "" : spot.Ordinal.ToString(inv));
            cells.Add(spot is null ? "" : spot.Frames.ToString(inv));
            cells.Add(spot is null ? "" : spot.FirstIndex.ToString(inv));
            MountMove? move = r.Series.MountMoves[i];
            cells.Add(move is { Signed: true } ? N(move.RaPx) : "");
            cells.Add(move is { Signed: true } ? N(move.DecPx) : "");
            string arrows = SeriesReport.MoveArrows(move, r.Arrows);
            cells.Add(arrows.Length == 0 ? "" : Q(arrows));   // two arrows are joined with a comma
            cells.Add(Q(f.Error ?? f.Info.Error ?? ""));
            sb.Append(string.Join(",", cells)).Append('\n');
        }

        return sb.ToString();
    }

    private static string N(double? v) => v is double d && !double.IsNaN(d) ? d.ToString("R", CultureInfo.InvariantCulture) : "";

    private static string Q(string s) => "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>The `trail_on` cell: the axis word without the preposition, empty when unknown (R-033).</summary>
    private static string TrailWord(TrailAxis on) => on switch
    {
        TrailAxis.Ra => "RA",
        TrailAxis.Dec => "DEC",
        TrailAxis.Oblique => "oblic",
        _ => "",
    };
}
