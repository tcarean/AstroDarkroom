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
/// One labelled line of the report; Verdict is the word the summary rows open with (GOOD,
/// MARGINAL, NO, ...; BUN, LA LIMITA, NU, ... in Romanian), null elsewhere.
/// </summary>
/// <param name="Label">The label left of the colon.</param>
/// <param name="Value">The text right of the colon, after the verdict when there is one.</param>
/// <param name="Verdict">The verdict word the value opens with; null for a row without one.</param>
internal sealed record ReportRow(string Label, string Value, string? Verdict = null);

/// <summary>A rejected frame: its index, its file and its reasons in words.</summary>
/// <param name="Index">The frame's index in the session, from 1.</param>
/// <param name="FileName">The raw's file name, without its folder.</param>
/// <param name="Reasons">Why it is left out, in words; several reasons joined by "; ".</param>
internal sealed record RejectedRow(int Index, string FileName, string Reasons);

/// <summary>
/// One paragraph of an explanation the window shows; Emphasis marks the one to set in bold
/// (R-036).
/// </summary>
/// <param name="Text">The paragraph, in the run's language.</param>
/// <param name="Emphasis">True for the paragraph set in bold.</param>
internal sealed record ReportParagraph(string Text, bool Emphasis);

/// <summary>
/// A table the window lays out: a title, the column headers and the rows, every cell already a
/// string (R-036).
/// </summary>
/// <param name="Title">The table's title.</param>
/// <param name="Columns">The column headers, left to right.</param>
/// <param name="Rows">The rows, each with one cell per column.</param>
internal sealed record ReportTable(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// One frame of the table, every cell already formatted; "-" where a figure is missing.
/// </summary>
/// <param name="Index">The frame's index in the session, from 1.</param>
/// <param name="FileName">
/// The raw's file name, whole (the console cuts it to 20 characters).
/// </param>
/// <param name="Time">The time of capture, HH:mm:ss.</param>
/// <param name="Class">The frame's class in words (<see cref="SeriesReport.ClassWord"/>).</param>
/// <param name="Elongation">The median elongation of the stars, two decimals.</param>
/// <param name="Fwhm">The median FWHM in pixels, two decimals.</param>
/// <param name="Stars">How many stars were detected.</param>
/// <param name="Background">The median green background in ADU above black, no decimals.</param>
/// <param name="Pause">The pause before the frame, in whole seconds with " s".</param>
/// <param name="Step">The registered step from the previous frame in pixels, one decimal.</param>
/// <param name="Dx">
/// The registered x offset from the reference frame in pixels, one decimal.
/// </param>
/// <param name="Dy">
/// The registered y offset from the reference frame in pixels, one decimal.
/// </param>
/// <param name="Move">The `dithering` cell, the mount's move on each axis.</param>
/// <param name="Spot">The cell of the frame's run at 10 px, `k/n from f`.</param>
/// <param name="Reasons">The frame's flags in words; empty when it has none.</param>
/// <param name="StepTip">The tooltip of the step cell; null when the frame has no step.</param>
internal sealed record FrameTableRow(int Index, string FileName, string Time, string Class, string Elongation, string Fwhm, string Stars, string Background, string Pause, string Step, string Dx, string Dy, string Move, string Spot, string Reasons, string? StepTip);

/// <summary>
/// The whole series as the console prints it (R-027, in the run's language), built from labelled rows that
/// the window shows as they are (R-029): words only, every number comes from the analyzers.
/// </summary>
internal static class SeriesReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Writes the whole series report: the session lines and the scale, the sections SUMMARY,
    /// FOCUS, EXPOSURE AND NOISE, DEW, TRAILED FRAMES, DRIFT AND ROTATION and DITHERING, the
    /// rejected frames, then the table of every frame.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <param name="w">Where the lines go; not null.</param>
    internal static void Write(SeriesResult r, TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(w);
        SeriesAnalysis s = r.Series;
        DitherAnalysis d = r.Dither;
        List<FrameInfo> infos = r.Frames.Select(f => f.Info).ToList();
        InventoryReport.WriteHeader(new Inventory(r.Session, infos, r.Cadence), w);
        w.WriteLine($"    {ScaleLabel,-19}: {ScaleLine(r)}");

        Section(w, T("SUMMARY", "REZUMAT"), SummaryRows(r));
        Section(w, "FOCUS", FocusRows(r));
        Section(w, T("EXPOSURE AND NOISE", "EXPUNERE SI ZGOMOT"), NoiseRows(r));
        Section(w, T("DEW", "ROUA"), DewRows(r));
        Section(w, T("TRAILED FRAMES", "CADRE MISCATE"), TrailRows(r));
        Section(w, T("DRIFT AND ROTATION", "DRIFT SI ROTATIE"), DriftRows(r));
        Section(w, "DITHERING", DitherRows(r));

        w.WriteLine();
        w.WriteLine($"  {T("REJECTED", "RESPINSE")} ({RejectedHeading(r)})");
        foreach (RejectedRow row in RejectedRows(r))
        {
            w.WriteLine($"    {row.Index.ToString(Inv),3}  {row.FileName,-20}  {row.Reasons}");
        }

        w.WriteLine();
        w.WriteLine(T("  FRAME BY FRAME", "  TABEL PE CADRE"));
        w.WriteLine(T(
            $"    {"no",3}  {"file",-20}  {"time",-8}  {"class",-8}  {"elong",5}  {"FWHM",5}  {"stars",6}  {"bg",5}  {"pause",5}  {"step",5}  {"dX",6}  {"dY",6}  {"dithering",-42}  {"same spot",-14}  reasons",
            $"     nr  fisier                ora       clasa     alung  FWHM   stele   fond  pauza    pas     dX      dY  {"dithering",-42}  acelasi loc     motive"));
        foreach (FrameTableRow f in FrameRows(r))
        {
            string name = f.FileName.Length > 20 ? f.FileName[..20] : f.FileName;
            w.WriteLine($"    {f.Index.ToString(Inv),3}  {name,-20}  {f.Time,-8}  {f.Class,-8}  {f.Elongation,5}  {f.Fwhm,5}  {f.Stars,6}  {f.Background,5}  {f.Pause,5}  {f.Step,5}  {f.Dx,6}  {f.Dy,6}  {f.Move,-42}  {f.Spot,-14}  {f.Reasons}");
        }
    }

    /// <summary>
    /// The text in the REJECTED (RESPINSE) heading's parentheses: the counts and the rule for the marginal
    /// frames.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The bad count, the unread count when there are any, and the marginal frames' rule.
    /// </returns>
    internal static string RejectedHeading(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        string bad = s.BadCount.ToString(Inv), unread = s.UnknownCount.ToString(Inv);
        return T(
            $"{bad} bad {(s.BadCount == 1 ? "frame" : "frames")}{(s.UnknownCount > 0 ? ", " + unread + " unread" : "")}; the marginal ones stay, your choice",
            $"{bad} cadre slabe{(s.UnknownCount > 0 ? ", " + unread + " necitite" : "")}; cele la limita raman la alegerea ta");
    }

    /// <summary>
    /// The table, one row per frame, the cells formatted as the console prints them (the window
    /// shows the same cells).
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>One row per frame, in the session's order.</returns>
    internal static IReadOnlyList<FrameTableRow> FrameRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        DitherAnalysis d = r.Dither;
        List<FrameTableRow> rows = [];
        for (int i = 0; i < r.Frames.Count; i++)
        {
            AnalyzedFrame f = r.Frames[i];
            FrameMeasurement? m = f.Measurement;
            RegistrationResult? g = f.Registration is { Ok: true } rg ? rg : null;
            string step = Opt(d.Step[i], 1), move = MoveCell(s.MountMoves[i], r.Arrows);
            rows.Add(new FrameTableRow(f.Info.Index, f.Info.FileName,
                f.Info.Timestamp is DateTime t ? t.ToString("HH:mm:ss", Inv) : "-",
                ClassWord(s.Classes[i]), Opt(m?.ElongationMedian, 2), Opt(m?.FwhmMedian, 2),
                m is null ? "-" : m.Detected.ToString(Inv), Opt(m?.BackgroundMedian, 0),
                r.Cadence.PauseSeconds[i] is double p ? RunReport.F(p, 0) + " s" : "-",
                step, Opt(g?.Dx, 1), Opt(g?.Dy, 1), move, SpotCell(d.SpotRun[i]), Reasons(s.Flags[i]),
                d.Step[i] is null ? null : T($"frame {f.Info.Index.ToString(Inv)}: step {step} px; {move}", $"cadrul {f.Info.Index.ToString(Inv)}: pas {step} px; {move}")));
        }

        return rows;
    }

    /// <summary>
    /// The `dithering` cell (R-038, R-040): the mount's move on each axis in pixels, with the
    /// SynScan arrow under the given keys and the sense letter when the move is signed
    /// (`RA 15 px ← W`; `← V` in Romanian), the pixels alone otherwise; a component under Th.AxisStep is left
    /// out; `-` for no move.
    /// </summary>
    /// <param name="m">The mount's move before the frame; null when there was none.</param>
    /// <param name="keys">
    /// Which SynScan key moves the mount to each side of the sky (ADR 0014).
    /// </param>
    /// <returns>
    /// The components joined by ", ", or `-` when there is no move or none clears the step.
    /// </returns>
    internal static string MoveCell(MountMove? m, ArrowKeys keys)
    {
        if (m is null)
        {
            return "-";
        }

        List<string> parts = MoveComponents(m, keys).Select(c => $"{c.Axis} {RunReport.F(c.Px, 0)} px" + (m.Signed ? $" {c.Arrow} {c.Sense}" : "")).ToList();
        return parts.Count == 0 ? "-" : string.Join(", ", parts);
    }

    /// <summary>
    /// The SynScan arrows of a signed move, `←, ↓`, for the CSV; empty otherwise (R-038, R-040).
    /// </summary>
    /// <param name="m">The mount's move before the frame; null when there was none.</param>
    /// <param name="keys">
    /// Which SynScan key moves the mount to each side of the sky (ADR 0014).
    /// </param>
    /// <returns>
    /// The arrow of each component that clears the step, joined by ", "; empty for no move or an
    /// unsigned one.
    /// </returns>
    internal static string MoveArrows(MountMove? m, ArrowKeys keys)
        => m is null || !m.Signed ? "" : string.Join(", ", MoveComponents(m, keys).Select(c => c.Arrow));

    /// <summary>
    /// The components of a move that clear Th.AxisStep, each with its axis word, its pixels,
    /// its sense letter and the SynScan arrow that moves the mount that way under the given
    /// keys (ADR 0014); the sense and the arrow mean nothing when the move is unsigned.
    /// </summary>
    /// <param name="m">The mount's move, its RA and DEC parts in pixels; not null.</param>
    /// <param name="keys">Which SynScan key moves the mount to each side of the sky.</param>
    /// <returns>
    /// The RA component first, then the DEC one, each only when its size reaches
    /// <see cref="Th.AxisStep"/>; the pixels are the absolute size, the sign is in the sense
    /// letter (E or W on RA, N or S on DEC; V for west in Romanian).
    /// </returns>
    private static IEnumerable<(string Axis, double Px, string Sense, string Arrow)> MoveComponents(MountMove m, ArrowKeys keys)
    {
        if (Math.Abs(m.RaPx) >= Th.AxisStep)
        {
            yield return ("RA", Math.Abs(m.RaPx), m.RaPx > 0 ? "E" : T("W", "V"), ArrowKeys.Symbol(m.RaPx > 0 ? keys.East : keys.West));
        }

        if (Math.Abs(m.DecPx) >= Th.AxisStep)
        {
            yield return ("DEC", Math.Abs(m.DecPx), m.DecPx > 0 ? "N" : "S", ArrowKeys.Symbol(m.DecPx > 0 ? keys.North : keys.South));
        }
    }

    /// <summary>
    /// The cell `k/n from f` (`k/n de la f`) of a frame's run at 10 px, `-` outside one (R-034).
    /// </summary>
    /// <param name="run">The frame's place in its run; null when the frame is in none.</param>
    /// <returns>
    /// The frame's ordinal in the run, the run's length and the index of its first frame, or `-`.
    /// </returns>
    internal static string SpotCell(SpotRun? run)
        => run is null ? "-" : $"{run.Ordinal.ToString(Inv)}/{run.Frames.ToString(Inv)} {T("from", "de la")} {run.FirstIndex.ToString(Inv)}";

    /// <summary>
    /// What the Rejected tab says under its heading, one paragraph per line: the elongation, where
    /// the marginal frames are, the trail and its axis, and in bold what each axis points to
    /// (R-035, R-036). Built when read, in the language of that moment.
    /// </summary>
    internal static IReadOnlyList<ReportParagraph> RejectedExplanation => IsRomanian
    ?
    [
        new($"Alungirea e raportul dintre axa lunga si axa scurta a stelelor (1 = rotunde), mediana pe cadru: de la {RunReport.F(Th.ElongationBad, 2)} cadrul e slab si iese din stiva.", false),
        new($"Intre {RunReport.F(Th.ElongationMarginal, 2)} si {RunReport.F(Th.ElongationBad, 2)} cadrul e la limita si ramane, la alegerea ta: lista lui e randul 'la limita' din CADRE MISCATE, tabul Rezumat; in tabul Cadre are clasa galbena.", false),
        new("Dara e directia in care s-au intins stelele in timpul expunerii: unghiul fata de orizontala imaginii si, cand se stie care axa a imaginii e RA si stelele cadrului sunt de acord intre ele (coerenta), axa monturii pe care cade.", false),
        new("Dara pe RA vine din urmarire: eroarea periodica, viteza, backlash-ul pe RA. Dara pe DEC vine din alinierea polara, vant sau o atingere a monturii. Oblica: amandoua, sau o lovitura.", true),
    ]
    :
    [
        new($"The elongation is the ratio of the stars' long axis to their short axis (1 = round), the median over the frame: from {RunReport.F(Th.ElongationBad, 2)} the frame is bad and leaves the stack.", false),
        new($"Between {RunReport.F(Th.ElongationMarginal, 2)} and {RunReport.F(Th.ElongationBad, 2)} the frame is marginal and stays, your choice: its list is the 'marginal' row of TRAILED FRAMES, on the Summary tab; on the Frames tab its class is yellow.", false),
        new("The trail is the direction the stars were stretched in during the exposure: its angle to the image's horizontal and, when it is known which image axis is RA and the frame's stars agree with one another (coherence), the mount axis it falls on.", false),
        new("A trail on RA comes from the tracking: periodic error, rate, backlash on RA. A trail on DEC comes from the polar alignment, wind or a touch on the mount. Oblique: both, or a knock.", true),
    ];

    /// <summary>
    /// The caption of a star cut-out: where it was taken and the star's three figures.
    /// </summary>
    /// <param name="c">The cut-out and its star; not null.</param>
    /// <returns>The place, the FWHM in pixels, the elongation and the angle in degrees.</returns>
    internal static string CropCaption(StarCrop c)
    {
        ArgumentNullException.ThrowIfNull(c);
        string fwhm = RunReport.F(c.Star.Fwhm, 1), elongation = RunReport.F(c.Star.Elongation, 2), angle = RunReport.F(c.Star.AngleDeg, 0);
        return T(
            $"{c.Where}: FWHM {fwhm} px, elongation {elongation}, angle {angle}",
            $"{c.Where}: FWHM {fwhm} px, alungire {elongation}, unghi {angle}");
    }

    /// <summary>
    /// Writes one section: a blank line, its title, then each row as its label padded to the
    /// colon and its value, the verdict first when the row has one.
    /// </summary>
    /// <param name="w">Where the lines go.</param>
    /// <param name="title">The section's title, in capitals.</param>
    /// <param name="rows">The rows of the section.</param>
    private static void Section(TextWriter w, string title, IReadOnlyList<ReportRow> rows)
    {
        w.WriteLine();
        w.WriteLine("  " + title);
        foreach (ReportRow row in rows)
        {
            string value = row.Verdict is null ? row.Value : row.Verdict + " - " + row.Value;
            w.WriteLine($"    {row.Label,-19}: {value}");
        }
    }

    /// <summary>The label of the session's scale line; the window's session card reuses it.</summary>
    internal static string ScaleLabel => T("scale", "scara");

    /// <summary>
    /// The `scale` (`scara`) line: the image scale, the pixel size and its source, the reference frame.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The scale in arcseconds per pixel (unknown without a focal length), the pixel size in
    /// micrometres, and the reference frame's index, or `none` when none was chosen.
    /// </returns>
    internal static string ScaleLine(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        string scale = r.ArcsecPerPixel is double sc ? RunReport.F(sc, 2) + " \"/px" : T("unknown (no focal length)", "necunoscuta (fara focala)");
        string reference = r.ReferenceIndex > 0 ? r.ReferenceIndex.ToString(Inv) : T("none", "niciunul");
        return T(
            $"{scale}; pixel {RunReport.F(r.PixelSizeUm, 2)} um ({r.PixelSizeSource}); reference frame {reference}",
            $"{scale}; pixel {RunReport.F(r.PixelSizeUm, 2)} um ({r.PixelSizeSource}); referinta cadrul {reference}");
    }

    // ------------------------------------------------------------------ the sections

    /// <summary>
    /// The SUMMARY section: focus, exposure, dew, the trailed frames, dithering, polar alignment,
    /// each with its verdict, and the frames to leave out of the stack.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The seven rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> SummaryRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        DitherAnalysis d = r.Dither;
        (string fv, string ft) = Focus(s, r);
        (string ev, string et) = Exposure(s, r);
        (string dv, string dt) = Dew(s);
        (string dither, string ditherText) = DitherVerdict(r);
        (string? polarVerdict, string polarText) = PolarSummary(r);
        return
        [
            new("focus", ft, fv),
            new(T("exposure", "expunere"), et, ev),
            new(T("dew", "roua"), dt, dv),
            new(T("trailed frames", "cadre miscate"), T(
                $"{s.BadCount.ToString(Inv)} bad, {s.MarginalCount.ToString(Inv)} marginal, {s.GoodCount.ToString(Inv)} good{(s.UnknownCount > 0 ? ", " + s.UnknownCount.ToString(Inv) + " unread" : "")} (of {r.Frames.Count.ToString(Inv)}); {AxisLine(s)}",
                $"{s.BadCount.ToString(Inv)} slabe, {s.MarginalCount.ToString(Inv)} la limita, {s.GoodCount.ToString(Inv)} bune{(s.UnknownCount > 0 ? ", " + s.UnknownCount.ToString(Inv) + " necitite" : "")} (din {r.Frames.Count.ToString(Inv)}); {AxisLine(s)}")),
            new("dithering", ditherText, dither),
            new(T("polar alignment", "aliniere polara"), polarText, polarVerdict),
            new(T("leave out of stack", "de scos din stiva"), Keep(r)),
        ];
    }

    /// <summary>
    /// The FOCUS section: the session's median FWHM, the first against the last good frames and
    /// whether the focus held, the zone medians, and the verdict.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The four rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> FocusRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        (string fv, string ft) = Focus(s, r);
        return
        [
            new(T("median FWHM (good frames)", "FWHM median (cadre bune)"), Px(s.SessionFwhm, r)),
            new(T($"first / last {s.Fwhm.Count.ToString(Inv)} good frames", $"primele / ultimele {s.Fwhm.Count.ToString(Inv)} bune"), $"{TrendPx(s.Fwhm)} -> {(s.FocusHeld is bool fh ? (fh ? T("the focus held", "focusul a tinut") : T("the focus moved", "focusul s-a miscat")) : T("too few good frames", "prea putine cadre bune"))}"),
            new(T("centre / middle / edge", "centru / mijloc / margine"), $"{ZoneMedians(r)} {T("(medians over the good frames)", "(mediane pe cadrele bune)")}"),
            new("verdict", ft, fv),
        ];
    }

    /// <summary>
    /// The map's axis labels: which image axis is RA and which DEC, plain `X` / `Y` without an
    /// axis (R-036).
    /// </summary>
    /// <param name="s">The series analysis, whose RA axis decides the labels; not null.</param>
    /// <returns>The label of the horizontal axis and that of the vertical one.</returns>
    internal static (string X, string Y) AxisLabels(SeriesAnalysis s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return s.RaAxis switch
        {
            Axis.Y => ("X = DEC", "Y = RA"),
            Axis.X => ("X = RA", "Y = DEC"),
            _ => ("X", "Y"),
        };
    }

    /// <summary>
    /// The headers of the position columns of the frame table: `dX (DEC)`, `dY (RA)`, plain
    /// without an axis (R-036).
    /// </summary>
    /// <param name="s">The series analysis, whose RA axis decides the headers; not null.</param>
    /// <returns>The header of the dX column and that of the dY column.</returns>
    internal static (string Dx, string Dy) PositionHeaders(SeriesAnalysis s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return s.RaAxis switch
        {
            Axis.Y => ("dX (DEC)", "dY (RA)"),
            Axis.X => ("dX (RA)", "dY (DEC)"),
            _ => ("dX", "dY"),
        };
    }

    /// <summary>
    /// The EXPOSURE AND NOISE section: the sky background of the first and last good frames,
    /// the read noise per channel from the bias pair, then per sampled frame the sky, the
    /// shot-to-read ratio and the noise penalty of each channel, and the row that explains the
    /// ratio's scale.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The rows in the console's order; only the background and a `not measured` read-noise row
    /// when the session has no two biases.
    /// </returns>
    internal static IReadOnlyList<ReportRow> NoiseRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        List<ReportRow> rows =
        [
            new(T("sky background (ADU above black, good frames, first -> last)", "fond de cer (ADU peste black, cadrele bune, primele -> ultimele)"), $"{Opt(s.Background.First, 0)} -> {Opt(s.Background.Last, 0)} {T("on green", "pe verde")}{(s.Background.RelativeChange is double bc ? " (" + Signed(100 * bc, 0) + " %)" : "")}"),
        ];
        if (s.ReadNoise is null)
        {
            rows.Add(new(T("read noise", "zgomot de citire"), T("not measured (no two biases in biases\\)", "nemasurat (fara doua bias-uri in biases\\)")));
            return rows;
        }

        rows.Add(new(T("read noise", "zgomot de citire"), string.Join("  ", s.ReadNoise.Select(n => $"{n.Name} {RunReport.F(n.PairSigma, 1)}")) + T(" ADU (from the pair of biases)", " ADU (din perechea de bias-uri)")));
        for (int i = 0; i < s.SwampingFrames.Count; i++)
        {
            rows.Add(new($"{T("frame", "cadrul")} {s.SwampingFrames[i].ToString(Inv),3}", string.Join("  ", s.Swamping![i].Select(c =>
            {
                string sky = RunReport.F(c.Sky, 0), ratio = RunReport.F(c.SwampingRatio, 1), penalty = double.IsNaN(c.PenaltyPct) ? "-" : RunReport.F(c.PenaltyPct, 1) + " %";
                return T($"{c.Name}: sky {sky}, shot/read {ratio}, penalty {penalty}", $"{c.Name}: cer {sky}, shot/citire {ratio}, penalizare {penalty}");
            }))));
        }

        string ok = RunReport.F(Th.SwampingRatioOk, 0);
        rows.Add(new(ScaleLabel, T(
            $"shot/read above {ok} = the sky swamps the read noise (penalty under 5 %); below = the channel is read-noise limited, longer subs would help",
            $"shot/citire peste {ok} = cerul ineaca zgomotul de citire (penalizare sub 5 %); sub = canalul e limitat de citire, subexpuneri mai lungi ar ajuta")));
        return rows;
    }

    /// <summary>
    /// The DEW section: the halo, the detected stars and the FWHM of the first against the last
    /// good frames, and the dew verdict.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The two rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> DewRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        (string dv, string dt) = Dew(s);
        return
        [
            new(T("halo first / last", "halo primele / ultimele"), $"{Opt(s.Halo.First, 3)} / {Opt(s.Halo.Last, 3)}; {T("stars detected", "stele detectate")} {Opt(s.Stars.First, 0)} -> {Opt(s.Stars.Last, 0)}{(s.Stars.RelativeChange is double sr ? " (" + Signed(100 * sr, 0) + " %)" : "")}; FWHM {TrendPx(s.Fwhm)}"),
            new("verdict", dt, dv),
        ];
    }

    /// <summary>
    /// The TRAILED FRAMES section: the bad frames, the marginal frames (each row naming its
    /// elongation bounds, <see cref="Th.ElongationBad"/> and <see cref="Th.ElongationMarginal"/>),
    /// and the direction of the trails with the mount axis it falls on.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The three rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> TrailRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        return
        [
            new(T($"bad (elongation >= {RunReport.F(Th.ElongationBad, 2)})", $"slabe (alungire >= {RunReport.F(Th.ElongationBad, 2)})"), List(r, FrameClass.Bad)),
            new(T($"marginal ({RunReport.F(Th.ElongationMarginal, 2)} .. {RunReport.F(Th.ElongationBad, 2)})", $"la limita ({RunReport.F(Th.ElongationMarginal, 2)} .. {RunReport.F(Th.ElongationBad, 2)})"), List(r, FrameClass.Marginal)),
            new(T("trail direction", "directia darelor"), $"{(s.TrailAngleDeg is double ta ? RunReport.F(ta, 0) + T(" degrees from the horizontal on the bad frames", " grade fata de orizontala pe cadrele slabe") + (s.TrailOn == TrailAxis.Unknown ? "" : " = " + AxisWord(s.TrailOn)) : T("undetermined (too few bad frames with a coherent direction)", "nedeterminata (prea putine cadre slabe cu directie coerenta)"))}; {AxisLine(s)}"),
        ];
    }

    /// <summary>
    /// The DRIFT AND ROTATION section: the drift between consecutive undithered frames in pixels
    /// per minute (and arcseconds per minute when the scale is known), the drift over one
    /// exposure, the field rotation, the polar alignment and the RA drift.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The five rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> DriftRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        return
        [
            new(T("drift between consecutive undithered frames", "drift intre cadre consecutive fara dithering"), $"X {Signed(s.DriftXPerMin, 2)} px/min, Y {Signed(s.DriftYPerMin, 2)} px/min ({PairsWord(s)}){(r.ArcsecPerPixel is double a1 && s.DriftXPerMin is double dxm && s.DriftYPerMin is double dym ? $" = {Signed(dxm * a1, 1)} / {Signed(dym * a1, 1)} \"/min" : "")}"),
            new(DriftPerExposureLabel, DriftPerExposure(r)),
            new(T("field rotation", "rotatie de camp"), $"{(s.RotationTotalDeg is double rt ? RunReport.F(rt * 60, 1) + " arcmin in total" : "-")}{(s.RotationPerHourDeg is double rh ? ", " + RunReport.F(rh * 60, 1) + " arcmin/h" : "")}{(s.CornerShiftPerExposurePx is double cs ? T("; in the corner, over one exposure: ", "; in colt, intr-o expunere: ") + RunReport.F(cs, 2) + " px" : "")}"),
            new(T("polar alignment", "aliniere polara"), PolarRow(r)),
            new(T("RA drift", "drift pe RA"), RaDriftRow(r)),
        ];
    }

    /// <summary>
    /// `39 pairs without a pause`, or `29 pairs with a pause, approximate: the small dither steps
    /// are in the figure` when the drift fell back to the paused pairs (R-049).
    /// </summary>
    /// <param name="s">The series analysis, with the drift's pair count; not null.</param>
    /// <returns>The pair count and the kind of pairs the drift was measured on.</returns>
    private static string PairsWord(SeriesAnalysis s)
        => s.DriftApproximate
            ? T($"{s.DriftPairs.ToString(Inv)} pairs with a pause, approximate: the small dither steps are in the figure", $"{s.DriftPairs.ToString(Inv)} perechi cu pauza, aproximativ: pasii mici de dithering intra in cifra")
            : T($"{s.DriftPairs.ToString(Inv)} pairs without a pause", $"{s.DriftPairs.ToString(Inv)} perechi fara pauza");

    /// <summary>
    /// Why the polar alignment is not measured, or null when it is (R-050): no registered pairs,
    /// under three drift pairs, no RA axis, no scale, no exposure.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The cell to show instead of the alignment, the first missing piece in the order above;
    /// null when the lower bound of the polar error was measured.
    /// </returns>
    private static string? PolarReason(SeriesResult r)
    {
        SeriesAnalysis s = r.Series;
        return s.PolarErrorMinArcmin is not null ? null
            : r.Dither.Registered < 2 ? T("- (frames not registered)", "- (cadre neinregistrate)")
            : s.StarDriftPerMin is null && (s.DriftApproximate || s.DriftXPerMin is null || s.DriftPairs < Th.DriftMinPairs) ? T(
                $"not measurable (under {Th.DriftMinPairs.ToString(Inv)} pairs of consecutive frames with no pause and no move between them; a run of 10+ frames without dithering would measure it)",
                $"nemasurabila (sub {Th.DriftMinPairs.ToString(Inv)} perechi de cadre consecutive fara pauza si fara miscare intre ele; o serie de 10+ cadre fara dithering ar masura-o)")
            : s.RaAxis == Axis.Unknown ? T("- (no RA axis)", "- (fara axa RA)")
            : r.ArcsecPerPixel is null ? T("- (no scale)", "- (fara scara)")
            : T("- (no exposure)", "- (fara expunere)");
    }

    /// <summary>
    /// Why there is no upper bound when the lower one exists (R-050): no solve for the
    /// declination, or a span under Th.PolarRotationMinHours.
    /// </summary>
    /// <param name="s">The series analysis; not null.</param>
    /// <returns>
    /// The no-solve sentence when the declination is unknown, else the short-span one.
    /// </returns>
    private static string NoUpperBound(SeriesAnalysis s)
        => s.DeclinationDeg is null
            ? T("without a plate solve the declination for the upper bound is not known", "fara plate solve nu se stie declinatia pentru limita de sus")
            : T($"session under {RunReport.F(Th.PolarRotationMinHours, 0)} h: the rotation gives no upper bound yet", $"sesiune sub {RunReport.F(Th.PolarRotationMinHours, 0)} h: rotatia nu da inca limita de sus");

    /// <summary>
    /// `error between 4.2' and 10.4'`, `error of 5.7'` when the bounds meet, `error of at least
    /// 5.7'` without an upper bound (`eroare intre ...`, `eroare de ...` in Romanian).
    /// </summary>
    /// <param name="s">
    /// The series analysis, with the polar error's bounds in arcminutes; not null.
    /// </param>
    /// <returns>
    /// The bracket of the polar error, one decimal; the bounds meet when they print the same.
    /// </returns>
    private static string PolarBounds(SeriesAnalysis s)
    {
        string min = RunReport.F(s.PolarErrorMinArcmin ?? 0, 1);
        return s.PolarErrorMaxArcmin is double max
            ? (RunReport.F(max, 1) == min ? T($"error of {min}'", $"eroare de {min}'") : T($"error between {min}' and {RunReport.F(max, 1)}'", $"eroare intre {min}' si {RunReport.F(max, 1)}'"))
            : T($"error of at least {min}'", $"eroare de cel putin {min}'");
    }

    /// <summary>
    /// The `polar alignment` row of DRIFT AND ROTATION (R-050): the bracket with what gave each
    /// bound, the drift the alignment causes in one exposure and the elongation it adds; the
    /// lower bound alone without a solve; or why nothing is measured.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The row's value; the drifts in pixels per exposure, the DEC drift in "/min.
    /// </returns>
    private static string PolarRow(SeriesResult r)
    {
        if (PolarReason(r) is string why)
        {
            return why;
        }

        SeriesAnalysis s = r.Series;
        string seconds = RunReport.F(s.MedianExposureSeconds ?? 0, 0);
        string exposure = T($"over an exposure of {seconds} s", $"intr-o expunere de {seconds} s");
        string decDrift = RunReport.F(Math.Abs(s.StarDriftPerMin!.DecPxPerMin) * (r.ArcsecPerPixel ?? 0), 1);
        string dec = T($"the DEC drift of {decDrift}\"/min", $"driftul pe DEC de {decDrift}\"/min");
        string driftMin = RunReport.F(s.PolarDriftMinPx ?? 0, 2);
        if (s.PolarErrorMaxArcmin is null)
        {
            return T(
                $"{PolarBounds(s)} ({dec}; {NoUpperBound(s)}); drift from the alignment at least {driftMin} px {exposure}",
                $"{PolarBounds(s)} ({dec}; {NoUpperBound(s)}); drift din aliniere cel putin {driftMin} px {exposure}");
        }

        string perHour = RunReport.F(Math.Abs(s.PolarRotationPerHourDeg ?? 0) * 60, 1), decDeg = Signed(s.DeclinationDeg, 1);
        string rotation = T($"the rotation of {perHour}'/h without dithering at DEC {decDeg} degrees", $"rotatia de {perHour}'/h fara dithering la DEC {decDeg} grade");
        string elongation = s.PolarElongation is double el
            ? T($", elongation added at most {RunReport.F(el, 2)} on a FWHM of {RunReport.F(s.SessionFwhm ?? 0, 2)} px", $", alungire adaugata cel mult {RunReport.F(el, 2)} pe FWHM {RunReport.F(s.SessionFwhm ?? 0, 2)} px")
            : "";
        string driftMax = RunReport.F(s.PolarDriftMaxPx ?? 0, 2);
        return T(
            $"{PolarBounds(s)} ({dec}; {rotation}); drift from the alignment at most {driftMax} px {exposure} ({driftMin} px for certain){elongation}",
            $"{PolarBounds(s)} ({dec}; {rotation}); drift din aliniere cel mult {driftMax} px {exposure} ({driftMin} px sigur){elongation}");
    }

    /// <summary>
    /// The `RA drift` (`drift pe RA`) row (R-050): the RA drift in arcseconds per minute with the stars' sense
    /// when the solve gives it, how much of it the alignment can explain (the rotation times
    /// tan DEC) and that the rest is the tracking; `-` when the alignment is not measured.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The row's value; without the alignment's RA share, the drift and why that share is not
    /// known yet.
    /// </returns>
    private static string RaDriftRow(SeriesResult r)
    {
        SeriesAnalysis s = r.Series;
        if (PolarReason(r) is not null)
        {
            return "-";
        }

        StarDrift d = s.StarDriftPerMin!;
        double ra = Math.Abs(d.RaPxPerMin) * (r.ArcsecPerPixel ?? 0);
        string sense = d.Signed ? (d.RaPxPerMin >= 0 ? T(", the stars towards the east", ", stelele spre est") : T(", the stars towards the west", ", stelele spre vest")) : "";
        if (s.PolarRaDriftMaxArcsecPerMin is not double raMax)
        {
            string unknown = s.DeclinationDeg is null
                ? T("without a plate solve it is not known how much of it comes from the alignment", "fara plate solve nu se stie cat e din aliniere")
                : T($"session under {RunReport.F(Th.PolarRotationMinHours, 0)} h: it is not known yet how much of it comes from the alignment", $"sesiune sub {RunReport.F(Th.PolarRotationMinHours, 0)} h: nu se stie inca cat e din aliniere");
            return $"{RunReport.F(ra, 1)}\"/min{sense} ({unknown})";
        }

        string share = T($"from the alignment at most {RunReport.F(raMax, 1)}\"/min", $"din aliniere cel mult {RunReport.F(raMax, 1)}\"/min");
        return ra > raMax
            ? $"{RunReport.F(ra, 1)}\"/min{sense}; {share}{T(", the rest is the tracking (rate, periodic error, flexure), not the alignment", ", restul e urmarirea (viteza, eroarea periodica, flexiune), nu alinierea")}"
            : $"{RunReport.F(ra, 1)}\"/min{sense}; {share}{T(": it may all be the alignment", ": poate fi toata din aliniere")}";
    }

    /// <summary>
    /// The SUMMARY row `polar alignment` (R-050): the verdict word (`OK`, `MARGINAL`, `WARNING`;
    /// `OK`, `LA LIMITA`, `ATENTIE` in Romanian; none when unknown) and the bracket with what it costs in one exposure, or why nothing
    /// is measured.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The verdict word, null when there is none, and the text: the error's bracket and its cost
    /// in pixels per exposure, at least the lower drift for WARNING or without an upper bound,
    /// both drifts for MARGINAL, otherwise at most the upper one.
    /// </returns>
    private static (string? Verdict, string Text) PolarSummary(SeriesResult r)
    {
        if (PolarReason(r) is string why)
        {
            return (null, why);
        }

        SeriesAnalysis s = r.Series;
        string min = RunReport.F(s.PolarDriftMinPx ?? 0, 2), max = RunReport.F(s.PolarDriftMaxPx ?? 0, 2);
        string cost = s.PolarAlignment == PolarAlignment.Attention || s.PolarDriftMaxPx is null ? T($"costs at least {min} px over one exposure", $"costa cel putin {min} px intr-o expunere")
            : s.PolarAlignment == PolarAlignment.Marginal ? T($"costs between {min} and {max} px over one exposure", $"costa intre {min} si {max} px intr-o expunere")
            : T($"costs at most {max} px over one exposure", $"costa cel mult {max} px intr-o expunere");
        string suffix = s.PolarErrorMaxArcmin is null ? $" ({NoUpperBound(s)})" : "";
        string? verdict = s.PolarAlignment switch
        {
            PolarAlignment.Ok => "OK",
            PolarAlignment.Marginal => T("MARGINAL", "LA LIMITA"),
            PolarAlignment.Attention => T("WARNING", "ATENTIE"),
            _ => null,
        };
        return (verdict, $"{PolarBounds(s)}, {cost}{suffix}");
    }

    /// <summary>
    /// The drift over one exposure and the walking-noise dash it draws inside the longest run at
    /// 10 px (R-043).
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The drift, the median exposure in seconds, the axis it runs along and the dash; a `-`
    /// sentence when there are no consecutive undithered frames.
    /// </returns>
    private static string DriftPerExposure(SeriesResult r)
    {
        SeriesAnalysis s = r.Series;
        if (s.DriftPerExposurePx is not double d)
        {
            return T("- (no pairs of consecutive undithered frames)", "- (fara perechi de cadre consecutive fara dithering)");
        }

        string seconds = RunReport.F(s.MedianExposureSeconds ?? 0, 0);
        return T($"{Px(d, r)} over {seconds} s, along {DriftAxisWord(s)}; {Streak(r)}", $"{Px(d, r)} pe {seconds} s, pe {DriftAxisWord(s)}; {Streak(r)}");
    }

    /// <summary>
    /// The label of the drift-per-exposure row of DRIFT AND ROTATION, which the Dithering tab's
    /// explanation quotes.
    /// </summary>
    private static string DriftPerExposureLabel => T("drift over one exposure", "drift intr-o expunere");

    /// <summary>
    /// `walking-noise streaks of up to 11 px (frames 61-70, 10 frames within 10 px)`, the dash of
    /// R-043 (`dare de walking noise de pana la ...` in Romanian); without a stretch of two frames
    /// there is no dash.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The dash's length in pixels and its stretch; an unknown dash without a drift; no dash
    /// without a stretch.
    /// </returns>
    private static string Streak(SeriesResult r)
    {
        SeriesAnalysis s = r.Series;
        return s.WalkingStreakPx is not double st ? T("walking-noise streak unknown (no drift)", "dara de walking noise necunoscuta (fara drift)")
            : r.Dither.WalkingStretch is not FrameStretch w ? T($"no walking-noise streak ({NoRun()})", $"nicio dara de walking noise ({NoRun()})")
            : T($"walking-noise streaks of up to {RunReport.F(st, 1)} px ({StretchWord(w)})", $"dare de walking noise de pana la {RunReport.F(st, 1)} px ({StretchWord(w)})");
    }

    /// <summary>
    /// `frames 61-70, 10 frames within 10 px` (`cadrele 61-70, 10 cadre la 10 px`): the stretch of consecutive frames whose span is the
    /// dash.
    /// </summary>
    /// <param name="w">The stretch: its first and last frame index and its frame count.</param>
    /// <returns>The stretch in words, at <see cref="Th.CoverageLoose"/> px.</returns>
    private static string StretchWord(FrameStretch w)
    {
        string range = $"{w.FirstIndex.ToString(Inv)}-{w.LastIndex.ToString(Inv)}", radius = RunReport.F(Th.CoverageLoose, 0);
        return T($"frames {range}, {FramesWord(w.Frames)} within {radius} px", $"cadrele {range}, {FramesWord(w.Frames)} la {radius} px");
    }

    /// <summary>
    /// `no run of 2+ consecutive frames within 10 px`: the frames that would share the dash do
    /// not exist.
    /// </summary>
    /// <returns>The sentence, at <see cref="Th.CoverageLoose"/> px.</returns>
    private static string NoRun()
        => T($"no run of 2+ consecutive frames within {RunReport.F(Th.CoverageLoose, 0)} px", $"niciun grup de 2+ cadre consecutive la {RunReport.F(Th.CoverageLoose, 0)} px");

    /// <summary>
    /// The axis the drift mostly runs along: RA or DEC when the RA axis is known, else the image
    /// axis.
    /// </summary>
    /// <param name="s">
    /// The series analysis, with the drift's axis and the RA axis; not null.
    /// </param>
    /// <returns>"RA", "DEC", "X", "Y", or "-" when the drift has no axis.</returns>
    private static string DriftAxisWord(SeriesAnalysis s) => s.DriftAxis switch
    {
        Axis.Unknown => "-",
        _ when s.RaAxis == Axis.Unknown => s.DriftAxis == Axis.X ? "X" : "Y",
        _ => s.DriftAxis == s.RaAxis ? "RA" : "DEC",
    };

    /// <summary>
    /// The DITHERING section: the pauses and moves, how often and how far the mount moved, the
    /// directions, the coverage, the distinct positions, the groups at
    /// <see cref="Th.CoverageMid"/> and <see cref="Th.CoverageLoose"/> px, the scale on the sky,
    /// the pattern left in the stack, the sensor pattern, and the verdict.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The thirteen rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> DitherRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        DitherAnalysis d = r.Dither;
        (string verdict, string text) = DitherVerdict(r);
        return
        [
            PausesRow(r, d),
            new(HowOftenLabel, HowOften(d)),
            LongestRow(r, d),
            new(HowFarLabel, $"median {Px(d.MedianMove, r)}; " + string.Join("; ", d.Sizes.Select(c => $"{SizeWord(c)}: {c.Count.ToString(Inv)}"))),
            new(DirectionLabel, T(
                $"X only: {d.MovesOnXOnly.ToString(Inv)}; Y only: {d.MovesOnYOnly.ToString(Inv)}; both: {d.MovesOnBoth.ToString(Inv)}; sign X: +{d.MovesXPlus.ToString(Inv)} / -{d.MovesXMinus.ToString(Inv)}; sign Y: +{d.MovesYPlus.ToString(Inv)} / -{d.MovesYMinus.ToString(Inv)}; {AxisLine(s)}",
                $"numai X: {d.MovesOnXOnly.ToString(Inv)}; numai Y: {d.MovesOnYOnly.ToString(Inv)}; ambele: {d.MovesOnBoth.ToString(Inv)}; semn X: +{d.MovesXPlus.ToString(Inv)} / -{d.MovesXMinus.ToString(Inv)}; semn Y: +{d.MovesYPlus.ToString(Inv)} / -{d.MovesYMinus.ToString(Inv)}; {AxisLine(s)}")),
            new(CoverageLabel, T(
                $"nearest other frame median {Opt(Median(d.NearestOther), 1)} px; under {RunReport.F(Th.CoverageTight, 0)} px: {d.WithinTight.ToString(Inv)}, under {RunReport.F(Th.CoverageMid, 0)}: {d.WithinMid.ToString(Inv)}, under {RunReport.F(Th.CoverageLoose, 0)}: {d.WithinLoose.ToString(Inv)}; isolated (>= {RunReport.F(Th.CoverageLoose, 0)} px from any): {d.IsolatedLoose.ToString(Inv)}",
                $"cel mai apropiat alt cadru median {Opt(Median(d.NearestOther), 1)} px; sub {RunReport.F(Th.CoverageTight, 0)} px: {d.WithinTight.ToString(Inv)}, sub {RunReport.F(Th.CoverageMid, 0)}: {d.WithinMid.ToString(Inv)}, sub {RunReport.F(Th.CoverageLoose, 0)}: {d.WithinLoose.ToString(Inv)}; izolate (>= {RunReport.F(Th.CoverageLoose, 0)} px de orice): {d.IsolatedLoose.ToString(Inv)}")),
            new(T("distinct positions", "pozitii distincte"), T(
                $"{d.DistinctMid.ToString(Inv)} within {RunReport.F(Th.CoverageMid, 0)} px, {d.DistinctLoose.ToString(Inv)} within {RunReport.F(Th.CoverageLoose, 0)} px; span X {RunReport.F(d.SpanX, 0)} px, Y {RunReport.F(d.SpanY, 0)} px",
                $"{d.DistinctMid.ToString(Inv)} la {RunReport.F(Th.CoverageMid, 0)} px, {d.DistinctLoose.ToString(Inv)} la {RunReport.F(Th.CoverageLoose, 0)} px; intindere X {RunReport.F(d.SpanX, 0)} px, Y {RunReport.F(d.SpanY, 0)} px")),
            new(T($"runs within {RunReport.F(Th.CoverageMid, 0)} px", $"grupuri in {RunReport.F(Th.CoverageMid, 0)} px"), Runs(d.RunsMid, r)),
            new(T($"runs within {RunReport.F(Th.CoverageLoose, 0)} px", $"grupuri in {RunReport.F(Th.CoverageLoose, 0)} px"), Runs(d.RunsLoose, r)),
            ScaleRow(r),
            SurvivingRow(r),
            PatternRow(r),
            new("verdict", text, verdict),
        ];
    }

    /// <summary>
    /// The DITHERING rows that are not tables, for the window's Dithering tab beside
    /// <see cref="DitherTables"/> (R-036).
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The nine rows, the coverage cut to the median nearest frame and the isolated ones.
    /// </returns>
    internal static IReadOnlyList<ReportRow> DitherScalarRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DitherAnalysis d = r.Dither;
        (string verdict, string text) = DitherVerdict(r);
        return
        [
            PausesRow(r, d),
            LongestRow(r, d),
            new(CoverageLabel, T(
                $"nearest other frame median {Opt(Median(d.NearestOther), 1)} px; isolated (>= {RunReport.F(Th.CoverageLoose, 0)} px from any): {d.IsolatedLoose.ToString(Inv)}",
                $"cel mai apropiat alt cadru median {Opt(Median(d.NearestOther), 1)} px; izolate (>= {RunReport.F(Th.CoverageLoose, 0)} px de orice): {d.IsolatedLoose.ToString(Inv)}")),
            new(T("span", "intindere"), $"X {RunReport.F(d.SpanX, 0)} px, Y {RunReport.F(d.SpanY, 0)} px"),
            new(T("axes", "axe"), AxisLine(r.Series)),
            ScaleRow(r),
            SurvivingRow(r),
            PatternRow(r),
            new("verdict", text, verdict),
        ];
    }

    /// <summary>
    /// What a move means on the sky at this focal length: `1 px = 5.99"; a move of 5 px (the
    /// threshold) takes 30" on the sky, one of 10 px 60"` (R-044).
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The `scale` row; `unknown (no focal length)` when the scale is unknown.</returns>
    private static ReportRow ScaleRow(SeriesResult r)
    {
        if (r.ArcsecPerPixel is not double a)
        {
            return new(ScaleLabel, T("unknown (no focal length)", "necunoscuta (fara focala)"));
        }

        string px = RunReport.F(a, 2), step = RunReport.F(Th.DitherStep, 0), stepSky = RunReport.F(Th.DitherStep * a, 0);
        string loose = RunReport.F(Th.CoverageLoose, 0), looseSky = RunReport.F(Th.CoverageLoose * a, 0);
        return new(ScaleLabel, T(
            $"1 px = {px}\"; a move of {step} px (the threshold) takes {stepSky}\" on the sky, one of {loose} px {looseSky}\"",
            $"1 px = {px}\"; o miscare de {step} px (pragul) cere {stepSky}\" pe cer, una de {loose} px {looseSky}\""));
    }

    /// <summary>
    /// How much of one frame's pattern the mean of the stack keeps (R-048): `1.6 % of one
    /// frame's stays in the mean (ideally 1.1 % with 87 frames; pairs of frames under 1.5 px: 27)`.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The `pattern in the stack` (`pattern in stiva`) row.</returns>
    private static ReportRow SurvivingRow(SeriesResult r)
    {
        DitherAnalysis d = r.Dither;
        if (d.PatternSurviving is not double f)
        {
            return new(SurvivingLabel, T("- (frames not registered)", "- (cadre neinregistrate)"));
        }

        string kept = RunReport.F(100 * f, 1), ideal = RunReport.F(100.0 / d.Registered, 1), share = RunReport.F(Th.PatternSharePx, 1), pairs = d.SharingPairs.ToString(Inv);
        return new(SurvivingLabel, T(
            $"{kept} % of one frame's pattern stays in the mean (ideally {ideal} % with {FramesWord(d.Registered)}; pairs of frames under {share} px: {pairs})",
            $"{kept} % din al unui cadru ramane in medie (ideal {ideal} % la {FramesWord(d.Registered)}; perechi de cadre la sub {share} px: {pairs})"));
    }

    /// <summary>
    /// The sensor's pattern read from the lights, and whether the session's darks will take it
    /// out of the stack (R-046, R-042): `sigma 1.4 ADU on R (12 % of one frame's noise), 2.1 on
    /// G1 (10 %), 2.1 on G2 (10 %), 2.3 on B (10 %); 4 hot pixels / Mpx above 10 sigma (median of
    /// 87 unregistered frames, 512 x 512 px from the centre); no darks in the session: they stay
    /// in the frames`.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The `fixed pattern` row: the figures when the pattern was measured, else why not, and in
    /// both cases what the session's darks do.
    /// </returns>
    private static ReportRow PatternRow(SeriesResult r)
    {
        SensorPattern p = r.Pattern;
        string darks = r.Session.DarksDir is null
            ? T("no darks in the session: the pattern stays in the frames", "fara dark-uri in sesiune: raman in cadre")
            : T($"{r.Session.Darks.Count.ToString(Inv)} darks in {SessionFolder.DarksName}\\, Siril subtracts the pattern", $"{r.Session.Darks.Count.ToString(Inv)} dark-uri in {SessionFolder.DarksName}\\, Siril le scade");
        string value = p.Status switch
        {
            PatternStatus.Measured => MeasuredPattern(p),
            PatternStatus.TooFewFrames => T($"undetermined (under {Th.PatternMinFrames.ToString(Inv)} frames measured)", $"nedeterminat (sub {Th.PatternMinFrames.ToString(Inv)} cadre masurate)"),
            PatternStatus.Unregistered => T("undetermined (frames not registered)", "nedeterminat (cadre neinregistrate)"),
            PatternStatus.FramesDidNotMove => T(
                $"undetermined (the frames stayed in place: the stars stay in the median; the largest group within {RunReport.F(Th.CoverageTight, 0)} px has {FramesWord(r.Dither.LargestClusterTight)} of {r.Dither.Registered.ToString(Inv)})",
                $"nedeterminat (cadrele au stat pe loc: stelele raman in mediana; grupul cel mai mare la {RunReport.F(Th.CoverageTight, 0)} px are {FramesWord(r.Dither.LargestClusterTight)} din {r.Dither.Registered.ToString(Inv)})"),
            _ => T("undetermined (no frame decoded)", "nedeterminat (niciun cadru decodat)"),
        };
        return new(PatternLabel, $"{value}; {darks}");
    }

    /// <summary>
    /// The figures of a measured sensor pattern: its sigma per CFA position with its share of one
    /// frame's noise, the hot pixels per megapixel and where it was read (R-046).
    /// </summary>
    /// <param name="p">The measured pattern; not null.</param>
    /// <returns>The figures, the first position carrying the units and the noun.</returns>
    private static string MeasuredPattern(SensorPattern p)
    {
        IEnumerable<string> sigmas = Enumerable.Range(0, p.PatternSigma.Count).Select(i =>
        {
            string sigma = (double.IsNaN(p.PatternSigma[i]) ? "-" : RunReport.F(p.PatternSigma[i], 1)) + (i == 0 ? " ADU" : "");
            string share = double.IsNaN(p.FractionOfFrame(i)) ? "-" : RunReport.F(100 * p.FractionOfFrame(i), 0) + " %";
            return T(
                $"{sigma} on {p.Names[i]} ({share}{(i == 0 ? " of one frame's noise" : "")})",
                $"{sigma} pe {p.Names[i]} ({share}{(i == 0 ? " din zgomotul unui cadru" : "")})");
        });
        string hot = RunReport.F(p.HotPerMpx, 0), hotSigma = RunReport.F(Th.PatternHotSigma, 0), crop = p.CropSize.ToString(Inv);
        return T(
            $"sigma {string.Join(", ", sigmas)}; {hot} hot pixels / Mpx above {hotSigma} sigma (median of {FramesWord(p.Frames)}, not registered, {crop} x {crop} px from the centre)",
            $"sigma {string.Join(", ", sigmas)}; {hot} pixeli fierbinti / Mpx peste {hotSigma} sigma (mediana a {FramesWord(p.Frames)} neinregistrate, {crop} x {crop} px din centru)");
    }

    /// <summary>
    /// What the Dithering tab says under its card: what walking noise is, why the focal
    /// length decides how much the same drift and the same dither are in pixels, what breaks
    /// it (bold), how to take darks without touching the lens, and where the figures come from
    /// (R-045). Built from Th so the thresholds it quotes follow a change there, and when read,
    /// in the language of that moment.
    /// </summary>
    internal static IReadOnlyList<ReportParagraph> DitherExplanation => IsRomanian
    ?
    [
        new($"Walking noise-ul e pattern-ul fix al senzorului (pixelii calzi, restul de dark) tras in dare: driftul il muta de la un cadru la altul, iar stiva aduna copiile pe traseul lui. Rezulta un zgomot corelat, alungit pe directia driftului, in grupurile de cadre consecutive ramase in acelasi loc la {RunReport.F(Th.CoverageLoose, 0)} px.", false),
        new("Aceeasi deriva a monturii si aceeasi miscare de dithering inseamna de atatea ori mai multi pixeli cu cat focala e mai lunga: la 20 mm un drift de 3\"/min sta sub un pixel pe toata sesiunea si nimic nu merge, la 135 mm e peste un pixel pe expunere. Focala scurta iarta; focala lunga cere dithering la fiecare cadru.", false),
        new($"Ce rupe walking noise-ul: o miscare la fiecare cadru, pe ambele axe, in directie aleatoare. Frecventa conteaza mai mult decat marimea: {RunReport.F(Th.DitherStep, 0)} px ajung cand doua cadre vecine nu mai impart acelasi loc; un salt mare la 2 cadre lasa grupuri in care driftul trage liniute.", true),
        new("Dark-urile scot pattern-ul inainte sa apuce sa mearga, si nu cer demontarea obiectivului sau a filtrului: scoti obiectivul cu tot cu filtru, pui capacul de body, inchizi ocularul vizorului si tragi 20 de cadre la acelasi ISO, acelasi timp si aceeasi temperatura, la finalul sesiunii. Flat-urile raman valabile.", false),
        new($"Cifrele: randul '{DriftPerExposureLabel}' din DRIFT SI ROTATIE da driftul pe un cadru si dara de walking noise, cat de intins e cel mai intins grup de cadre consecutive ramase la {RunReport.F(Th.CoverageLoose, 0)} px; sub {RunReport.F(Th.WalkingStreakVisible, 0)} px nu e walking noise, oricum ar arata harta. Randul '{SurvivingLabel}' spune cat din pattern-ul unui cadru ramane in medie dupa stivuire (1 / cadre cand niciun cadru nu sta peste altul), iar '{PatternLabel}' cat de mare e pattern-ul senzorului fata de zgomotul unui cadru si daca sesiunea are dark-uri care sa-l scoata.", false),
    ]
    :
    [
        new($"Walking noise is the sensor's fixed pattern (the hot pixels, what is left of the dark) dragged into streaks: the drift moves it from one frame to the next, and the stack adds up the copies along its path. The result is a correlated noise, stretched along the drift's direction, in the runs of consecutive frames that stayed in the same spot within {RunReport.F(Th.CoverageLoose, 0)} px.", false),
        new("The same mount drift and the same dither move cover more pixels the longer the focal length: at 20 mm a drift of 3\"/min stays under one pixel over the whole session and nothing walks, at 135 mm it is over one pixel per exposure. A short focal length forgives; a long one asks for a dither on every frame.", false),
        new($"What breaks walking noise: a move on every frame, on both axes, in a random direction. How often matters more than how far: {RunReport.F(Th.DitherStep, 0)} px are enough once two neighbouring frames no longer share the same spot; a big jump every 2 frames leaves runs in which the drift draws dashes.", true),
        new("Darks take the pattern out before it gets to walk, and they do not ask you to take the filter off the lens: take the lens off with its filter on, put the body cap on, close the viewfinder's eyepiece and shoot 20 frames at the same ISO, the same exposure and the same temperature, at the end of the session. The flats stay valid.", false),
        new($"The figures: the '{DriftPerExposureLabel}' row of DRIFT AND ROTATION gives the drift over one frame and the walking-noise streak, how far the most spread-out run of consecutive frames that stayed within {RunReport.F(Th.CoverageLoose, 0)} px stretches; under {RunReport.F(Th.WalkingStreakVisible, 0)} px there is no walking noise, whatever the map looks like. The '{SurvivingLabel}' row says how much of one frame's pattern stays in the mean after stacking (1 / frames when no frame sits on another), and '{PatternLabel}' how large the sensor's pattern is against one frame's noise and whether the session has darks to take it out.", false),
    ];

    /// <summary>The label of the DITHERING row and table of how often the mount moved.</summary>
    private static string HowOftenLabel => T("how often", "cat de des");

    /// <summary>The label of the DITHERING row and table of how far the mount moved.</summary>
    private static string HowFarLabel => T("how far", "cat de mult");

    /// <summary>The label of the DITHERING row and table of the moves' directions.</summary>
    private static string DirectionLabel => T("direction", "directie");

    /// <summary>The label of the DITHERING row and table of the coverage.</summary>
    private static string CoverageLabel => T("coverage", "acoperire");

    /// <summary>The label of the DITHERING row of the pattern left in the stack (R-048).</summary>
    private static string SurvivingLabel => T("pattern in the stack", "pattern in stiva");

    /// <summary>The label of the DITHERING row of the sensor's pattern (R-046).</summary>
    private static string PatternLabel => T("fixed pattern", "pattern fix");

    /// <summary>
    /// The DITHERING figures that are tables, for the window (R-036): how often, how far,
    /// direction, coverage, runs; the console keeps its rows (ADR 0012).
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The five tables in that order; how often has a single row with the console's words when
    /// no interval between moves was counted, the runs table one row per run and radius.
    /// </returns>
    internal static IReadOnlyList<ReportTable> DitherTables(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DitherAnalysis d = r.Dither;
        (string xAxis, string yAxis) = AxisLabels(r.Series);
        List<IReadOnlyList<string>> often = d.FramesBetweenMoves.OrderBy(k => k.Key)
            .Select(kv => (IReadOnlyList<string>)[FramesWord(kv.Key), TimesWord(kv.Value)])
            .ToList();
        if (often.Count == 0)
        {
            often.Add([HowOften(d), "-"]);   // "no move" or "a single move", the console's words
        }

        List<IReadOnlyList<string>> sizes = [["median", Px(d.MedianMove, r)]];
        sizes.AddRange(d.Sizes.Select(c => (IReadOnlyList<string>)[SizeWord(c), c.Count.ToString(Inv)]));
        List<IReadOnlyList<string>> runs = [.. RunRows(d.RunsMid, Th.CoverageMid, r), .. RunRows(d.RunsLoose, Th.CoverageLoose, r)];
        return
        [
            new(HowOftenLabel, [T("every", "la fiecare"), T("times", "de cate ori")], often),
            new(HowFarLabel, [T("size", "marime"), T("moves", "miscari")], sizes),
            new(DirectionLabel, [T("axis", "axa"), T("on it alone", "numai pe ea"), T("sign +", "semn +"), T("sign -", "semn -")],
            [
                [xAxis, d.MovesOnXOnly.ToString(Inv), d.MovesXPlus.ToString(Inv), d.MovesXMinus.ToString(Inv)],
                [yAxis, d.MovesOnYOnly.ToString(Inv), d.MovesYPlus.ToString(Inv), d.MovesYMinus.ToString(Inv)],
                [T("both", "ambele"), d.MovesOnBoth.ToString(Inv), "", ""],
            ]),
            new(CoverageLabel, [T("radius", "raza"), T("with another frame within it", "cu alt cadru sub raza"), T("distinct positions", "pozitii distincte"), T($"runs of {Th.RunMinFrames.ToString(Inv)}+ consecutive", $"grupuri de {Th.RunMinFrames.ToString(Inv)}+ consecutive")],
            [
                [PxWord(Th.CoverageTight), d.WithinTight.ToString(Inv), "-", "-"],
                [PxWord(Th.CoverageMid), d.WithinMid.ToString(Inv), d.DistinctMid.ToString(Inv), d.RunsMid.Count.ToString(Inv)],
                [PxWord(Th.CoverageLoose), d.WithinLoose.ToString(Inv), d.DistinctLoose.ToString(Inv), d.RunsLoose.Count.ToString(Inv)],
            ]),
            new(T("runs in the same spot", "grupuri in acelasi loc"), [T("radius", "raza"), T("frames", "cadre"), T("how many", "cate"), T("minutes", "minute")], runs),
        ];
    }

    /// <summary>
    /// The first DITHERING row: the pauses longer than <see cref="Th.DitherPause"/>, the moves of
    /// at least <see cref="Th.DitherStep"/>, the pauses without a move, the moves without a
    /// pause, and how many frames were registered.
    /// </summary>
    /// <param name="r">The result of the series run, for its frame count.</param>
    /// <param name="d">The dither analysis of the series.</param>
    /// <returns>The row, labelled with the pause threshold.</returns>
    private static ReportRow PausesRow(SeriesResult r, DitherAnalysis d)
        => new(T($"pauses > {RunReport.F(Th.DitherPause, 0)} s", $"pauze > {RunReport.F(Th.DitherPause, 0)} s"), T(
            $"{d.Pauses.ToString(Inv)}; moves >= {RunReport.F(Th.DitherStep, 0)} px: {d.Moves.ToString(Inv)}; pauses without a move: {d.PausesWithoutMove.ToString(Inv)}; moves without a pause: {d.MovesWithoutPause.ToString(Inv)}; frames registered: {d.Registered.ToString(Inv)} of {r.Frames.Count.ToString(Inv)}",
            $"{d.Pauses.ToString(Inv)}; miscari >= {RunReport.F(Th.DitherStep, 0)} px: {d.Moves.ToString(Inv)}; pauze fara miscare: {d.PausesWithoutMove.ToString(Inv)}; miscari fara pauza: {d.MovesWithoutPause.ToString(Inv)}; cadre inregistrate: {d.Registered.ToString(Inv)} din {r.Frames.Count.ToString(Inv)}"));

    /// <summary>
    /// The row of the longest stretch between two moves: its first and last frame, its frame
    /// count, and its duration when the frames carry a time.
    /// </summary>
    /// <param name="r">The result of the series run, for the times of the frames.</param>
    /// <param name="d">The dither analysis of the series.</param>
    /// <returns>The `longest stretch between two moves` row.</returns>
    private static ReportRow LongestRow(SeriesResult r, DitherAnalysis d)
    {
        string range = $"{d.LongestStretchFirst.ToString(Inv)}-{d.LongestStretchLast.ToString(Inv)}", frames = d.LongestStretchFrames.ToString(Inv);
        string minutes = Minutes(r, d.LongestStretchFirst, d.LongestStretchLast);
        return new(T("longest stretch between two moves", "cel mai lung interval intre doua miscari"), T(
            $"frames {range} ({frames} frames from one move to the next{minutes})",
            $"cadrele {range} ({frames} cadre de la o miscare la urmatoarea{minutes})"));
    }

    /// <summary>A frame count with its noun in the right number.</summary>
    /// <param name="frames">The count.</param>
    /// <returns>"1 frame" ("1 cadru"), or "N frames" ("N cadre") for any other count.</returns>
    private static string FramesWord(int frames) => $"{frames.ToString(Inv)} {(frames == 1 ? T("frame", "cadru") : T("frames", "cadre"))}";

    /// <summary>A number of times with its noun in the right number.</summary>
    /// <param name="times">The count.</param>
    /// <returns>"1 time" ("1 data"), or "N times" ("N ori") for any other count.</returns>
    private static string TimesWord(int times) => $"{times.ToString(Inv)} {(times == 1 ? T("time", "data") : T("times", "ori"))}";

    /// <summary>A size class of the moves in pixels.</summary>
    /// <param name="c">
    /// The class, with its lower bound and its upper bound (null for the last).
    /// </param>
    /// <returns>"5-10 px" for a closed class, "20+ px" for the open last one.</returns>
    private static string SizeWord(SizeClass c) => $"{RunReport.F(c.Lower, 0)}{(c.Upper is double u ? "-" + RunReport.F(u, 0) : "+")} px";

    /// <summary>A radius in whole pixels.</summary>
    /// <param name="px">The radius in pixels.</param>
    /// <returns>"10 px".</returns>
    private static string PxWord(double px) => RunReport.F(px, 0) + " px";

    /// <summary>
    /// The rows of one radius in the runs table: one per run, or a single `none` row.
    /// </summary>
    /// <param name="runs">The runs of consecutive frames found at that radius.</param>
    /// <param name="radius">The radius in pixels.</param>
    /// <param name="r">The result of the series run, for the runs' durations.</param>
    /// <returns>
    /// Per row: the radius, the run's first-last frames, its frame count, its duration.
    /// </returns>
    private static List<IReadOnlyList<string>> RunRows(IReadOnlyList<FrameStretch> runs, double radius, SeriesResult r)
    {
        if (runs.Count == 0)
        {
            return [[PxWord(radius), T("none", "niciunul"), "-", "-"]];
        }

        return runs.Select(x => (IReadOnlyList<string>)[PxWord(radius), $"{x.FirstIndex.ToString(Inv)}-{x.LastIndex.ToString(Inv)}", x.Frames.ToString(Inv), MinutesCell(r, x.FirstIndex, x.LastIndex)]).ToList();
    }

    /// <summary>
    /// The duration of a stretch as a cell: <see cref="Minutes"/> without its leading separator,
    /// `-` when the frames carry no time.
    /// </summary>
    /// <param name="r">The result of the series run, for the times of the frames.</param>
    /// <param name="first">The stretch's first frame, 1-based.</param>
    /// <param name="last">The stretch's last frame, 1-based.</param>
    /// <returns>The duration ("12 min"), or `-`.</returns>
    private static string MinutesCell(SeriesResult r, int first, int last)
    {
        string m = Minutes(r, first, last);
        return m.Length > 2 ? m[2..] : "-";
    }

    /// <summary>
    /// The frames to leave out of the stack: the bad and the unreadable ones, with their reasons.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>One row per frame classed bad or unknown, in the session's order.</returns>
    internal static IReadOnlyList<RejectedRow> RejectedRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        List<RejectedRow> rows = [];
        for (int i = 0; i < r.Frames.Count; i++)
        {
            if (r.Series.Classes[i] is FrameClass.Bad or FrameClass.Unknown)
            {
                rows.Add(new RejectedRow(r.Frames[i].Info.Index, r.Frames[i].Info.FileName, Reasons(r.Series.Flags[i])));
            }
        }

        return rows;
    }

    // ------------------------------------------------------------------ the words

    /// <summary>The words of a flag: the kind, the figure and the threshold.</summary>
    /// <param name="f">The flag; not null.</param>
    /// <returns>The flag in words; the kind's name for a kind without words.</returns>
    internal static string Reason(FrameFlag f)
    {
        ArgumentNullException.ThrowIfNull(f);
        return f.Kind switch
        {
            FlagKind.Trailed => T($"elongation {RunReport.F(f.Value, 2)} (threshold {RunReport.F(f.Threshold, 2)}){TrailSuffix(f)}", $"alungire {RunReport.F(f.Value, 2)} (prag {RunReport.F(f.Threshold, 2)}){TrailSuffix(f)}"),
            FlagKind.MarginalTrail => T($"elongation {RunReport.F(f.Value, 2)} marginal (threshold {RunReport.F(f.Threshold, 2)}){TrailSuffix(f)}", $"alungire {RunReport.F(f.Value, 2)} la limita (prag {RunReport.F(f.Threshold, 2)}){TrailSuffix(f)}"),
            FlagKind.Soft => T($"FWHM {RunReport.F(f.Value, 2)} px, above {RunReport.F(f.Threshold, 2)} ({RunReport.F(Th.SoftFwhmFactor, 1)} x the session's median)", $"FWHM {RunReport.F(f.Value, 2)} px, peste {RunReport.F(f.Threshold, 2)} ({RunReport.F(Th.SoftFwhmFactor, 1)} x mediana sesiunii)"),
            FlagKind.Clouds => T($"stars {RunReport.F(f.Value, 0)}, under {RunReport.F(f.Threshold, 0)} ({RunReport.F(100 * Th.CloudStarFraction, 0)} % of the median): clouds or haze", $"stele {RunReport.F(f.Value, 0)}, sub {RunReport.F(f.Threshold, 0)} ({RunReport.F(100 * Th.CloudStarFraction, 0)} % din mediana): nori sau ceata"),
            FlagKind.BackgroundJump => T($"background jump {Signed(f.Value, 0)} % relative to the neighbours (threshold {RunReport.F(f.Threshold, 0)} %)", $"salt de fond {Signed(f.Value, 0)} % fata de vecini (prag {RunReport.F(f.Threshold, 0)} %)"),
            FlagKind.Trails => T($"{RunReport.F(f.Value, 0)} {(f.Value == 1 ? "trail (satellite/plane; sigma clipping removes it)" : "trails (satellite/plane; sigma clipping removes them)")}", $"{RunReport.F(f.Value, 0)} dara (satelit/avion; sigma clipping o scoate)"),
            FlagKind.Unreadable => T("could not be read", "nu s-a putut citi") + (f.Detail is null ? "" : ": " + f.Detail),
            FlagKind.Unregistered => T("not registered (too few stars matched with the reference)", "neinregistrat (prea putine stele potrivite cu referinta)"),
            _ => f.Kind.ToString(),
        };
    }

    /// <summary>
    /// `, trail on RA (-90 degrees)` when the trail's axis is known, `, trail at -90 degrees`
    /// when only its angle is, nothing without an angle (R-033).
    /// </summary>
    /// <param name="f">
    /// The trail flag; its Detail holds the angle in degrees, already formatted.
    /// </param>
    /// <returns>The suffix, starting with ", ", or "".</returns>
    private static string TrailSuffix(FrameFlag f)
        => f.Detail is null ? ""
            : f.On == TrailAxis.Unknown ? T($", trail at {f.Detail} degrees", $", dara la {f.Detail} grade")
            : T($", trail {AxisWord(f.On)} ({f.Detail} degrees)", $", dara {AxisWord(f.On)} ({f.Detail} grade)");

    /// <summary>The mount axis a trail falls on, as the report's sentences say it.</summary>
    /// <param name="on">The trail's axis.</param>
    /// <returns>"on RA", "on DEC", "oblique" ("pe RA", "pe DEC", "oblica"), or "" for an unknown axis.</returns>
    internal static string AxisWord(TrailAxis on) => on switch
    {
        TrailAxis.Ra => T("on RA", "pe RA"),
        TrailAxis.Dec => T("on DEC", "pe DEC"),
        TrailAxis.Oblique => T("oblique", "oblica"),
        _ => "",
    };

    /// <summary>A frame's flags in words, in their order.</summary>
    /// <param name="flags">The flags of one frame; not null.</param>
    /// <returns>Each flag's <see cref="Reason"/> joined by "; ", or "" without flags.</returns>
    internal static string Reasons(IReadOnlyList<FrameFlag> flags)
    {
        ArgumentNullException.ThrowIfNull(flags);
        return flags.Count == 0 ? "" : string.Join("; ", flags.Select(Reason));
    }

    /// <summary>A frame's class as the table and the CSV write it.</summary>
    /// <param name="c">The class.</param>
    /// <returns>
    /// "good", "marginal", "BAD", or "unread" for an unknown class ("bun", "la limita", "SLAB",
    /// "necitit" in Romanian).
    /// </returns>
    internal static string ClassWord(FrameClass c) => c switch
    {
        FrameClass.Good => T("good", "bun"),
        FrameClass.Marginal => T("marginal", "la limita"),
        FrameClass.Bad => T("BAD", "SLAB"),
        _ => T("unread", "necitit"),
    };

    /// <summary>
    /// The focus verdict and its text: the session's FWHM and whether the focus held.
    /// </summary>
    /// <param name="s">The series analysis.</param>
    /// <param name="r">The result of the series run, for the scale.</param>
    /// <returns>
    /// UNDETERMINED without a good frame, WARNING when the focus moved, GOOD otherwise (also when
    /// too few good frames show a trend; NEDETERMINAT, ATENTIE, BUN in Romanian); the text gives
    /// the median FWHM and the trend.
    /// </returns>
    private static (string Verdict, string Text) Focus(SeriesAnalysis s, SeriesResult r)
    {
        if (s.SessionFwhm is null)
        {
            return (T("UNDETERMINED", "NEDETERMINAT"), T("no good frame", "niciun cadru bun"));
        }

        string held = s.FocusHeld is bool h
            ? (h ? T("it held the whole session", "a tinut toata sesiunea") : T("it moved", "s-a miscat"))
            : T("too few good frames for a trend", "prea putine cadre bune pentru tendinta");
        return (s.FocusHeld is false ? T("WARNING", "ATENTIE") : T("GOOD", "BUN"), T(
            $"median FWHM {Px(s.SessionFwhm, r)} on the good frames, {TrendPx(s.Fwhm)} at the start / end: {held}",
            $"FWHM median {Px(s.SessionFwhm, r)} pe cadrele bune, {TrendPx(s.Fwhm)} la inceput / sfarsit: {held}"));
    }

    /// <summary>
    /// The exposure verdict and its text, from the channel whose shot-to-read ratio is the lowest
    /// of all the sampled frames.
    /// </summary>
    /// <param name="s">The series analysis.</param>
    /// <param name="r">The result of the series run (not read).</param>
    /// <returns>
    /// GOOD (BUNA) when that ratio reaches <see cref="Th.SwampingRatioOk"/>, MARGINAL (LA LIMITA)
    /// under it, NOT MEASURED (NEMASURAT) without biases or without a finite ratio; the text opens
    /// with the green background of the first and last good frames in ADU.
    /// </returns>
    private static (string Verdict, string Text) Exposure(SeriesAnalysis s, SeriesResult r)
    {
        string bg = s.Background.First is double bf && s.Background.Last is double bl
            ? T($"G background {RunReport.F(bf, 0)} -> {RunReport.F(bl, 0)} ADU", $"fond G {RunReport.F(bf, 0)} -> {RunReport.F(bl, 0)} ADU")
            : T("background unknown", "fond necunoscut");
        if (s.Swamping is null || s.Swamping.Count == 0)
        {
            return (T("NOT MEASURED", "NEMASURAT"), bg + T("; read noise not measured (no biases)", "; zgomotul de citire nemasurat (fara bias-uri)"));
        }

        ChannelNoise? worst = s.Swamping.SelectMany(x => x).Where(c => !double.IsNaN(c.SwampingRatio) && !double.IsInfinity(c.SwampingRatio)).MinBy(c => c.SwampingRatio);
        if (worst is null)
        {
            return (T("NOT MEASURED", "NEMASURAT"), bg + T("; the shot/read ratio could not be measured (noise window too small)", "; raportul shot/citire nu s-a putut masura (fereastra de zgomot prea mica)"));
        }

        string ratio = RunReport.F(worst.SwampingRatio, 1), penalty = RunReport.F(worst.PenaltyPct, 0);
        return (worst.SwampingRatio >= Th.SwampingRatioOk ? T("GOOD", "BUNA") : T("MARGINAL", "LA LIMITA"), T(
            $"{bg}; weakest channel {worst.Name}: shot/read {ratio}, penalty {penalty} %",
            $"{bg}; canalul cel mai slab {worst.Name}: shot/citire {ratio}, penalizare {penalty} %"));
    }

    /// <summary>
    /// The dew verdict and its text: the change of the FWHM in percent and of the halo.
    /// </summary>
    /// <param name="s">The series analysis.</param>
    /// <returns>
    /// POSSIBLE (POSIBIL) when dew is suspected, NO (NU) when not, UNDETERMINED (NEDETERMINAT)
    /// with too few good frames.
    /// </returns>
    private static (string Verdict, string Text) Dew(SeriesAnalysis s) => s.DewSuspected switch
    {
        true => (T("POSSIBLE", "POSIBIL"), T(
            $"FWHM rose by {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} % and the halo by {Signed(s.Halo.Change ?? 0, 3)}",
            $"FWHM a crescut cu {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} % si halo-ul cu {Signed(s.Halo.Change ?? 0, 3)}")),
        false => (T("NO", "NU"), $"FWHM {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} %, halo {Signed(s.Halo.Change ?? 0, 3)}"),
        null => (T("UNDETERMINED", "NEDETERMINAT"), T("too few good frames", "prea putine cadre bune")),
    };

    /// <summary>Which image axis is RA and which DEC, and where that came from.</summary>
    /// <param name="s">The series analysis, with the RA axis and its source.</param>
    /// <returns>The sentence, or `RA axis undetermined` when the RA axis is unknown.</returns>
    private static string AxisLine(SeriesAnalysis s) => s.RaAxis switch
    {
        Axis.X => T($"image axis X = RA, Y = DEC ({s.AxisSource})", $"axa X a imaginii = RA, Y = DEC ({s.AxisSource})"),
        Axis.Y => T($"image axis Y = RA, X = DEC ({s.AxisSource})", $"axa Y a imaginii = RA, X = DEC ({s.AxisSource})"),
        _ => T("RA axis undetermined", "axa RA nedeterminata"),
    };

    /// <summary>
    /// The two dithering verdicts in one word pair, the worse first so the chip takes its
    /// colour (R-021, R-043): walking noise `OK` by the dither rule, `NO` (`FARA`) when the drift
    /// draws a dash under Th.WalkingStreakVisible in the longest run at 10 px, `INSUFFICIENT`
    /// (`INSUFICIENT`) otherwise; hot pixels `OK` or `WARNING` (`ATENTIE`). The words stay short for the chip; the dash,
    /// the drift, the run and the hot-pixel rule open the text.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The two words joined by ", " and the text, the counts of the moves and positions last;
    /// UNDETERMINED (NEDETERMINAT) when fewer than two frames were registered.
    /// </returns>
    private static (string Verdict, string Text) DitherVerdict(SeriesResult r)
    {
        DitherAnalysis d = r.Dither;
        SeriesAnalysis s = r.Series;
        if (d.Registered < 2)
        {
            return (T("UNDETERMINED", "NEDETERMINAT"), T("frames not registered", "cadre neinregistrate"));
        }

        (int Severity, string Word, string? Detail) hot = d.HotPixelsOk
            ? (0, T("OK for hot pixels", "OK pentru pixeli calzi"), null)
            : (2, T("WARNING on hot pixels", "ATENTIE la pixeli calzi"), T(
                $"hot pixels: over {Th.HotPixelRunMax.ToString(Inv)} consecutive frames within {RunReport.F(Th.CoverageTight, 0)} px",
                $"pixeli calzi: peste {Th.HotPixelRunMax.ToString(Inv)} cadre consecutive in {RunReport.F(Th.CoverageTight, 0)} px"));
        string run = d.WalkingStretch is FrameStretch w ? StretchWord(w) : NoRun();
        string drift = s.DriftPerExposurePx is double dpe
            ? T($"drift {RunReport.F(dpe, 2)} px per exposure, {run}", $"drift {RunReport.F(dpe, 2)} px pe expunere, {run}")
            : T($"drift not measured, {run}", $"drift nemasurat, {run}");
        (int Severity, string Word, string? Detail) walk = s.WalkingNoise switch
        {
            WalkingNoise.Ok => (0, T("OK for walking noise", "OK pentru walking noise"), null),
            WalkingNoise.Static => (0, T("NO walking noise", "FARA walking noise"), d.WalkingStretch is null
                ? T($"no walking-noise streak: {drift}", $"nicio dara de walking noise: {drift}")
                : T($"walking-noise streaks of at most {RunReport.F(s.WalkingStreakPx ?? 0, 1)} px: {drift}", $"dare de walking noise de cel mult {RunReport.F(s.WalkingStreakPx ?? 0, 1)} px: {drift}")),
            _ => (3, T("INSUFFICIENT for walking noise", "INSUFICIENT pentru walking noise"), s.WalkingStreakPx is double st
                ? T($"walking-noise streaks of up to {RunReport.F(st, 1)} px along {DriftAxisWord(s)}: {drift}", $"dare de walking noise de pana la {RunReport.F(st, 1)} px pe {DriftAxisWord(s)}: {drift}")
                : $"walking noise: {drift}"),
        };
        (var first, var second) = walk.Severity >= hot.Severity ? (walk, hot) : (hot, walk);
        string counts = T(
            $"{d.Moves.ToString(Inv)} moves over {d.Registered.ToString(Inv)} frames, {d.DistinctLoose.ToString(Inv)} distinct positions within {RunReport.F(Th.CoverageLoose, 0)} px, {d.IsolatedLoose.ToString(Inv)} isolated, {d.MovesOnBoth.ToString(Inv)} moves on both axes",
            $"{d.Moves.ToString(Inv)} miscari la {d.Registered.ToString(Inv)} cadre, {d.DistinctLoose.ToString(Inv)} pozitii distincte la {RunReport.F(Th.CoverageLoose, 0)} px, {d.IsolatedLoose.ToString(Inv)} izolate, {d.MovesOnBoth.ToString(Inv)} miscari pe ambele axe");
        return ($"{first.Word}, {second.Word}", string.Join("; ", new[] { first.Detail, second.Detail, counts }.Where(x => x is not null)));
    }

    /// <summary>
    /// The `leave out of stack` (`de scos din stiva`) cell: how many frames to leave out (bad and unreadable), how many
    /// stay (good and marginal) and their total exposure.
    /// </summary>
    /// <param name="r">The result of the series run.</param>
    /// <returns>The cell; frames without an exposure time add nothing to the duration.</returns>
    private static string Keep(SeriesResult r)
    {
        SeriesAnalysis s = r.Series;
        double kept = 0;
        for (int i = 0; i < r.Frames.Count; i++)
        {
            if (s.Classes[i] is FrameClass.Good or FrameClass.Marginal && r.Frames[i].Info.ExposureSeconds is double e)
            {
                kept += e;
            }
        }

        string leave = (s.BadCount + s.UnknownCount).ToString(Inv), stay = (s.GoodCount + s.MarginalCount).ToString(Inv);
        return T(
            $"{leave} frames (the list under REJECTED); {stay} stay = {RunReport.Duration(kept)}",
            $"{leave} cadre (lista la RESPINSE); raman {stay} = {RunReport.Duration(kept)}");
    }

    /// <summary>
    /// How often the mount moved: each interval between moves in frames and how many times.
    /// </summary>
    /// <param name="d">The dither analysis of the series.</param>
    /// <returns>
    /// The intervals, shortest first, joined by "; "; `no move` without a move, `a single move`
    /// when no interval was counted.
    /// </returns>
    private static string HowOften(DitherAnalysis d)
    {
        if (d.Moves == 0)
        {
            return T("no move", "nicio miscare");
        }

        List<string> parts = [];
        foreach (KeyValuePair<int, int> kv in d.FramesBetweenMoves.OrderBy(k => k.Key))
        {
            parts.Add(T($"every {FramesWord(kv.Key)}: {TimesWord(kv.Value)}", $"la fiecare {FramesWord(kv.Key)}: {TimesWord(kv.Value)}"));
        }

        return parts.Count == 0 ? T("a single move", "o singura miscare") : string.Join("; ", parts);
    }

    /// <summary>
    /// The duration of a stretch of frames, from the start of its first to the end of its last
    /// (the last frame's time of capture plus its exposure), as a suffix.
    /// </summary>
    /// <param name="r">The result of the series run, for the times of the frames.</param>
    /// <param name="first">The stretch's first frame, 1-based.</param>
    /// <param name="last">The stretch's last frame, 1-based.</param>
    /// <returns>
    /// ", " and the duration; "" when the range is not inside the series, or either end has no
    /// time, or the last frame has no exposure.
    /// </returns>
    private static string Minutes(SeriesResult r, int first, int last)
    {
        if (first < 1 || last > r.Frames.Count || last < first)
        {
            return "";
        }

        DateTime? t0 = r.Frames[first - 1].Info.Timestamp, t1 = r.Frames[last - 1].Info.Timestamp;
        double? e = r.Frames[last - 1].Info.ExposureSeconds;
        return t0 is DateTime a && t1 is DateTime b && e is double ex ? ", " + RunReport.Duration((b - a).TotalSeconds + ex) : "";
    }

    /// <summary>The runs of one radius as a single cell of the console's rows.</summary>
    /// <param name="runs">The runs of consecutive frames found at that radius.</param>
    /// <param name="r">The result of the series run, for the runs' durations.</param>
    /// <returns>Each run's first-last frames, frame count and duration, or `none`.</returns>
    private static string Runs(IReadOnlyList<FrameStretch> runs, SeriesResult r)
        => runs.Count == 0 ? T("none", "niciunul") : string.Join(", ", runs.Select(x => $"{x.FirstIndex.ToString(Inv)}-{x.LastIndex.ToString(Inv)} ({x.Frames.ToString(Inv)}{Minutes(r, x.FirstIndex, x.LastIndex)})"));

    /// <summary>The indices of the frames of one class.</summary>
    /// <param name="r">The result of the series run.</param>
    /// <param name="c">The class to list.</param>
    /// <returns>The indices joined by ", ", or `none`.</returns>
    private static string List(SeriesResult r, FrameClass c)
    {
        List<int> idx = Enumerable.Range(0, r.Frames.Count).Where(i => r.Series.Classes[i] == c).Select(i => r.Frames[i].Info.Index).ToList();
        return idx.Count == 0 ? T("none", "niciunul") : string.Join(", ", idx.Select(i => i.ToString(Inv)));
    }

    /// <summary>
    /// The median FWHM of the centre, middle and edge zones over the good frames.
    /// </summary>
    /// <param name="r">The result of the series run.</param>
    /// <returns>"2.10 / 2.25 / 2.60 px", a zone without any figure written "-".</returns>
    private static string ZoneMedians(SeriesResult r)
    {
        List<FrameMeasurement> good = Enumerable.Range(0, r.Frames.Count).Where(i => r.Series.Classes[i] == FrameClass.Good).Select(i => r.Frames[i].Measurement!).ToList();
        string Z(Func<FrameMeasurement, double?> pick)
        {
            List<double> v = good.Select(pick).Where(x => x is not null).Select(x => x!.Value).ToList();
            return v.Count > 0 ? RunReport.F(Raster.Percentile(v, 50), 2) : "-";
        }

        return $"{Z(m => m.Centre.Fwhm)} / {Z(m => m.Mid.Fwhm)} / {Z(m => m.Edge.Fwhm)} px";
    }

    /// <summary>
    /// A length in pixels, two decimals, and on the sky in arcseconds when the scale is known.
    /// </summary>
    /// <param name="v">The length in pixels; null when unknown.</param>
    /// <param name="r">The result of the series run, for the scale in arcseconds per pixel.</param>
    /// <returns>`2.31 px = 13.8"`, `2.31 px` without a scale, or `-`.</returns>
    private static string Px(double? v, SeriesResult r)
        => v is double d ? RunReport.F(d, 2) + " px" + (r.ArcsecPerPixel is double a ? " = " + RunReport.F(d * a, 1) + "\"" : "") : "-";

    /// <summary>
    /// A trend in pixels: the first and the last value, and the relative change in percent.
    /// </summary>
    /// <param name="t">The trend; any of its parts may be null.</param>
    /// <returns>
    /// "2.10 / 2.40 px (+14 %)"; a missing value is "-", a missing change left out.
    /// </returns>
    private static string TrendPx(Trend t)
        => $"{Opt(t.First, 2)} / {Opt(t.Last, 2)} px" + (t.RelativeChange is double rc ? " (" + Signed(100 * rc, 0) + " %)" : "");

    /// <summary>The median of the known values.</summary>
    /// <param name="v">The values, null where unknown.</param>
    /// <returns>The 50th percentile of the non-null values, or null when there are none.</returns>
    private static double? Median(IReadOnlyList<double?> v)
    {
        List<double> x = v.Where(a => a is not null).Select(a => a!.Value).ToList();
        return x.Count > 0 ? Raster.Percentile(x, 50) : null;
    }

    /// <summary>An optional figure with a fixed number of decimals, or "-".</summary>
    /// <param name="v">The figure; null when unknown.</param>
    /// <param name="decimals">How many decimals to write.</param>
    /// <returns>The formatted figure, or "-" when <paramref name="v"/> is null.</returns>
    private static string Opt(double? v, int decimals) => v is double d ? RunReport.F(d, decimals) : "-";

    /// <summary>An optional figure with its sign always written.</summary>
    /// <param name="v">The figure; null when unknown.</param>
    /// <param name="decimals">How many decimals to write.</param>
    /// <returns>
    /// "+1.25" or "-1.25" ("+" also for zero), or "-" when <paramref name="v"/> is null.
    /// </returns>
    private static string Signed(double? v, int decimals) => v is double d ? (d >= 0 ? "+" : "") + RunReport.F(d, decimals) : "-";
}
