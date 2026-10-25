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

namespace LightsCheck.Core;

/// <summary>
/// One labelled line of the report; Verdict is the word the summary rows open with (BUN, LA
/// LIMITA, NU, ...), null elsewhere.
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
/// <param name="Text">The paragraph, in Romanian.</param>
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
/// <param name="Class">The frame's class in words (bun, la limita, SLAB, necitit).</param>
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
/// <param name="Spot">The cell of the frame's run at 10 px, `k/n de la f`.</param>
/// <param name="Reasons">The frame's flags in words; empty when it has none.</param>
/// <param name="StepTip">The tooltip of the step cell; null when the frame has no step.</param>
internal sealed record FrameTableRow(int Index, string FileName, string Time, string Class, string Elongation, string Fwhm, string Stars, string Background, string Pause, string Step, string Dx, string Dy, string Move, string Spot, string Reasons, string? StepTip);

/// <summary>
/// The whole series as the console prints it (Romanian, R-027), built from labelled rows that
/// the window shows as they are (R-029): words only, every number comes from the analyzers.
/// </summary>
internal static class SeriesReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Writes the whole series report: the session lines and the scale, the sections REZUMAT,
    /// FOCUS, EXPUNERE SI ZGOMOT, ROUA, CADRE MISCATE, DRIFT SI ROTATIE and DITHERING, the
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
        w.WriteLine($"    {ScaleLabel}              : {ScaleLine(r)}");

        Section(w, "REZUMAT", SummaryRows(r));
        Section(w, "FOCUS", FocusRows(r));
        Section(w, "EXPUNERE SI ZGOMOT", NoiseRows(r));
        Section(w, "ROUA", DewRows(r));
        Section(w, "CADRE MISCATE", TrailRows(r));
        Section(w, "DRIFT SI ROTATIE", DriftRows(r));
        Section(w, "DITHERING", DitherRows(r));

        w.WriteLine();
        w.WriteLine($"  RESPINSE ({RejectedHeading(r)})");
        foreach (RejectedRow row in RejectedRows(r))
        {
            w.WriteLine($"    {row.Index.ToString(Inv),3}  {row.FileName,-20}  {row.Reasons}");
        }

        w.WriteLine();
        w.WriteLine("  TABEL PE CADRE");
        w.WriteLine($"     nr  fisier                ora       clasa     alung  FWHM   stele   fond  pauza    pas     dX      dY  {"dithering",-42}  acelasi loc     motive");
        foreach (FrameTableRow f in FrameRows(r))
        {
            string name = f.FileName.Length > 20 ? f.FileName[..20] : f.FileName;
            w.WriteLine($"    {f.Index.ToString(Inv),3}  {name,-20}  {f.Time,-8}  {f.Class,-8}  {f.Elongation,5}  {f.Fwhm,5}  {f.Stars,6}  {f.Background,5}  {f.Pause,5}  {f.Step,5}  {f.Dx,6}  {f.Dy,6}  {f.Move,-42}  {f.Spot,-14}  {f.Reasons}");
        }
    }

    /// <summary>
    /// The text in the RESPINSE heading's parentheses: the counts and the rule for the marginal
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
        return $"{s.BadCount.ToString(Inv)} cadre slabe{(s.UnknownCount > 0 ? ", " + s.UnknownCount.ToString(Inv) + " necitite" : "")}; cele la limita raman la alegerea ta";
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
                d.Step[i] is null ? null : $"cadrul {f.Info.Index.ToString(Inv)}: pas {step} px; {move}"));
        }

        return rows;
    }

    /// <summary>
    /// The `dithering` cell (R-038, R-040): the mount's move on each axis in pixels, with the
    /// SynScan arrow under the given keys and the sense letter when the move is signed
    /// (`RA 15 px ← V`), the pixels alone otherwise; a component under Th.AxisStep is left
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
    /// letter (E or V on RA, N or S on DEC).
    /// </returns>
    private static IEnumerable<(string Axis, double Px, string Sense, string Arrow)> MoveComponents(MountMove m, ArrowKeys keys)
    {
        if (Math.Abs(m.RaPx) >= Th.AxisStep)
        {
            yield return ("RA", Math.Abs(m.RaPx), m.RaPx > 0 ? "E" : "V", ArrowKeys.Symbol(m.RaPx > 0 ? keys.East : keys.West));
        }

        if (Math.Abs(m.DecPx) >= Th.AxisStep)
        {
            yield return ("DEC", Math.Abs(m.DecPx), m.DecPx > 0 ? "N" : "S", ArrowKeys.Symbol(m.DecPx > 0 ? keys.North : keys.South));
        }
    }

    /// <summary>
    /// The cell `k/n de la f` of a frame's run at 10 px, `-` outside one (R-034).
    /// </summary>
    /// <param name="run">The frame's place in its run; null when the frame is in none.</param>
    /// <returns>
    /// The frame's ordinal in the run, the run's length and the index of its first frame, or `-`.
    /// </returns>
    internal static string SpotCell(SpotRun? run)
        => run is null ? "-" : $"{run.Ordinal.ToString(Inv)}/{run.Frames.ToString(Inv)} de la {run.FirstIndex.ToString(Inv)}";

    /// <summary>
    /// What the Respinse tab says under its heading, one paragraph per line: the elongation, where
    /// the marginal frames are, the trail and its axis, and in bold what each axis points to
    /// (R-035, R-036).
    /// </summary>
    internal static IReadOnlyList<ReportParagraph> RejectedExplanation { get; } =
    [
        new($"Alungirea e raportul dintre axa lunga si axa scurta a stelelor (1 = rotunde), mediana pe cadru: de la {RunReport.F(Th.ElongationBad, 2)} cadrul e slab si iese din stiva.", false),
        new($"Intre {RunReport.F(Th.ElongationMarginal, 2)} si {RunReport.F(Th.ElongationBad, 2)} cadrul e la limita si ramane, la alegerea ta: lista lui e randul 'la limita' din CADRE MISCATE, tabul Rezumat; in tabul Cadre are clasa galbena.", false),
        new("Dara e directia in care s-au intins stelele in timpul expunerii: unghiul fata de orizontala imaginii si, cand se stie care axa a imaginii e RA si stelele cadrului sunt de acord intre ele (coerenta), axa monturii pe care cade.", false),
        new("Dara pe RA vine din urmarire: eroarea periodica, viteza, backlash-ul pe RA. Dara pe DEC vine din alinierea polara, vant sau o atingere a monturii. Oblica: amandoua, sau o lovitura.", true),
    ];

    /// <summary>
    /// The caption of a star cut-out: where it was taken and the star's three figures.
    /// </summary>
    /// <param name="c">The cut-out and its star; not null.</param>
    /// <returns>The place, the FWHM in pixels, the elongation and the angle in degrees.</returns>
    internal static string CropCaption(StarCrop c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return $"{c.Where}: FWHM {RunReport.F(c.Star.Fwhm, 1)} px, alungire {RunReport.F(c.Star.Elongation, 2)}, unghi {RunReport.F(c.Star.AngleDeg, 0)}";
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
    internal const string ScaleLabel = "scara";

    /// <summary>
    /// The `scara` line: the image scale, the pixel size and its source, the reference frame.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The scale in arcseconds per pixel (unknown without a focal length), the pixel size in
    /// micrometres, and the reference frame's index, or `niciunul` when none was chosen.
    /// </returns>
    internal static string ScaleLine(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return $"{(r.ArcsecPerPixel is double sc ? RunReport.F(sc, 2) + " \"/px" : "necunoscuta (fara focala)")}; pixel {RunReport.F(r.PixelSizeUm, 2)} um ({r.PixelSizeSource}); referinta cadrul {(r.ReferenceIndex > 0 ? r.ReferenceIndex.ToString(Inv) : "niciunul")}";
    }

    // ------------------------------------------------------------------ the sections

    /// <summary>
    /// The REZUMAT section: focus, exposure, dew, the moved frames, dithering, polar alignment,
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
            new("expunere", et, ev),
            new("roua", dt, dv),
            new("cadre miscate", $"{s.BadCount.ToString(Inv)} slabe, {s.MarginalCount.ToString(Inv)} la limita, {s.GoodCount.ToString(Inv)} bune{(s.UnknownCount > 0 ? ", " + s.UnknownCount.ToString(Inv) + " necitite" : "")} (din {r.Frames.Count.ToString(Inv)}); {AxisLine(s)}"),
            new("dithering", ditherText, dither),
            new("aliniere polara", polarText, polarVerdict),
            new("de scos din stiva", Keep(r)),
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
            new("FWHM median (cadre bune)", Px(s.SessionFwhm, r)),
            new($"primele / ultimele {s.Fwhm.Count.ToString(Inv)} bune", $"{TrendPx(s.Fwhm)} -> {(s.FocusHeld is bool fh ? (fh ? "focusul a tinut" : "focusul s-a miscat") : "prea putine cadre bune")}"),
            new("centru / mijloc / margine", $"{ZoneMedians(r)} (mediane pe cadrele bune)"),
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
    /// The EXPUNERE SI ZGOMOT section: the sky background of the first and last good frames,
    /// the read noise per channel from the bias pair, then per sampled frame the sky, the
    /// shot-to-read ratio and the noise penalty of each channel, and the row that explains the
    /// ratio's scale.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The rows in the console's order; only the background and a `nemasurat` read-noise row
    /// when the session has no two biases.
    /// </returns>
    internal static IReadOnlyList<ReportRow> NoiseRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        List<ReportRow> rows =
        [
            new("fond de cer (ADU peste black, cadrele bune, primele -> ultimele)", $"{Opt(s.Background.First, 0)} -> {Opt(s.Background.Last, 0)} pe verde{(s.Background.RelativeChange is double bc ? " (" + Signed(100 * bc, 0) + " %)" : "")}"),
        ];
        if (s.ReadNoise is null)
        {
            rows.Add(new("zgomot de citire", "nemasurat (fara doua bias-uri in biases\\)"));
            return rows;
        }

        rows.Add(new("zgomot de citire", string.Join("  ", s.ReadNoise.Select(n => $"{n.Name} {RunReport.F(n.PairSigma, 1)}")) + " ADU (din perechea de bias-uri)"));
        for (int i = 0; i < s.SwampingFrames.Count; i++)
        {
            rows.Add(new($"cadrul {s.SwampingFrames[i].ToString(Inv),3}", string.Join("  ", s.Swamping![i].Select(c => $"{c.Name}: cer {RunReport.F(c.Sky, 0)}, shot/citire {RunReport.F(c.SwampingRatio, 1)}, penalizare {(double.IsNaN(c.PenaltyPct) ? "-" : RunReport.F(c.PenaltyPct, 1) + " %")}"))));
        }

        rows.Add(new("scara", $"shot/citire peste {RunReport.F(Th.SwampingRatioOk, 0)} = cerul ineaca zgomotul de citire (penalizare sub 5 %); sub = canalul e limitat de citire, subexpuneri mai lungi ar ajuta"));
        return rows;
    }

    /// <summary>
    /// The ROUA section: the halo, the detected stars and the FWHM of the first against the last
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
            new("halo primele / ultimele", $"{Opt(s.Halo.First, 3)} / {Opt(s.Halo.Last, 3)}; stele detectate {Opt(s.Stars.First, 0)} -> {Opt(s.Stars.Last, 0)}{(s.Stars.RelativeChange is double sr ? " (" + Signed(100 * sr, 0) + " %)" : "")}; FWHM {TrendPx(s.Fwhm)}"),
            new("verdict", dt, dv),
        ];
    }

    /// <summary>
    /// The CADRE MISCATE section: the bad frames, the marginal frames (each row naming its
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
            new($"slabe (alungire >= {RunReport.F(Th.ElongationBad, 2)})", List(r, FrameClass.Bad)),
            new($"la limita ({RunReport.F(Th.ElongationMarginal, 2)} .. {RunReport.F(Th.ElongationBad, 2)})", List(r, FrameClass.Marginal)),
            new("directia darelor", $"{(s.TrailAngleDeg is double ta ? RunReport.F(ta, 0) + " grade fata de orizontala pe cadrele slabe" + (s.TrailOn == TrailAxis.Unknown ? "" : " = " + AxisWord(s.TrailOn)) : "nedeterminata (prea putine cadre slabe cu directie coerenta)")}; {AxisLine(s)}"),
        ];
    }

    /// <summary>
    /// The DRIFT SI ROTATIE section: the drift between consecutive undithered frames in pixels
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
            new("drift intre cadre consecutive fara dithering", $"X {Signed(s.DriftXPerMin, 2)} px/min, Y {Signed(s.DriftYPerMin, 2)} px/min ({PairsWord(s)}){(r.ArcsecPerPixel is double a1 && s.DriftXPerMin is double dxm && s.DriftYPerMin is double dym ? $" = {Signed(dxm * a1, 1)} / {Signed(dym * a1, 1)} \"/min" : "")}"),
            new("drift intr-o expunere", DriftPerExposure(r)),
            new("rotatie de camp", $"{(s.RotationTotalDeg is double rt ? RunReport.F(rt * 60, 1) + " arcmin in total" : "-")}{(s.RotationPerHourDeg is double rh ? ", " + RunReport.F(rh * 60, 1) + " arcmin/h" : "")}{(s.CornerShiftPerExposurePx is double cs ? "; in colt, intr-o expunere: " + RunReport.F(cs, 2) + " px" : "")}"),
            new("aliniere polara", PolarRow(r)),
            new("drift pe RA", RaDriftRow(r)),
        ];
    }

    /// <summary>
    /// `39 perechi fara pauza`, or `29 perechi cu pauza, aproximativ: pasii mici de dithering
    /// intra in cifra` when the drift fell back to the paused pairs (R-049).
    /// </summary>
    /// <param name="s">The series analysis, with the drift's pair count; not null.</param>
    /// <returns>The pair count and the kind of pairs the drift was measured on.</returns>
    private static string PairsWord(SeriesAnalysis s)
        => s.DriftApproximate ? $"{s.DriftPairs.ToString(Inv)} perechi cu pauza, aproximativ: pasii mici de dithering intra in cifra" : $"{s.DriftPairs.ToString(Inv)} perechi fara pauza";

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
            : r.Dither.Registered < 2 ? "- (cadre neinregistrate)"
            : s.StarDriftPerMin is null && (s.DriftApproximate || s.DriftXPerMin is null || s.DriftPairs < Th.DriftMinPairs) ? $"nemasurabila (sub {Th.DriftMinPairs.ToString(Inv)} perechi de cadre consecutive fara pauza si fara miscare intre ele; o serie de 10+ cadre fara dithering ar masura-o)"
            : s.RaAxis == Axis.Unknown ? "- (fara axa RA)"
            : r.ArcsecPerPixel is null ? "- (fara scara)"
            : "- (fara expunere)";
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
        => s.DeclinationDeg is null ? "fara plate solve nu se stie declinatia pentru limita de sus" : $"sesiune sub {RunReport.F(Th.PolarRotationMinHours, 0)} h: rotatia nu da inca limita de sus";

    /// <summary>
    /// `eroare intre 4.2' si 10.4'`, `eroare de 5.7'` when the bounds meet, `eroare de cel putin
    /// 5.7'` without an upper bound.
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
            ? (RunReport.F(max, 1) == min ? $"eroare de {min}'" : $"eroare intre {min}' si {RunReport.F(max, 1)}'")
            : $"eroare de cel putin {min}'";
    }

    /// <summary>
    /// The `aliniere polara` row of DRIFT SI ROTATIE (R-050): the bracket with what gave each
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
        string exposure = $"intr-o expunere de {RunReport.F(s.MedianExposureSeconds ?? 0, 0)} s";
        string dec = $"driftul pe DEC de {RunReport.F(Math.Abs(s.StarDriftPerMin!.DecPxPerMin) * (r.ArcsecPerPixel ?? 0), 1)}\"/min";
        if (s.PolarErrorMaxArcmin is null)
        {
            return $"{PolarBounds(s)} ({dec}; {NoUpperBound(s)}); drift din aliniere cel putin {RunReport.F(s.PolarDriftMinPx ?? 0, 2)} px {exposure}";
        }

        string rotation = $"rotatia de {RunReport.F(Math.Abs(s.PolarRotationPerHourDeg ?? 0) * 60, 1)}'/h fara dithering la DEC {Signed(s.DeclinationDeg, 1)} grade";
        string elongation = s.PolarElongation is double el ? $", alungire adaugata cel mult {RunReport.F(el, 2)} pe FWHM {RunReport.F(s.SessionFwhm ?? 0, 2)} px" : "";
        return $"{PolarBounds(s)} ({dec}; {rotation}); drift din aliniere cel mult {RunReport.F(s.PolarDriftMaxPx ?? 0, 2)} px {exposure} ({RunReport.F(s.PolarDriftMinPx ?? 0, 2)} px sigur){elongation}";
    }

    /// <summary>
    /// The `drift pe RA` row (R-050): the RA drift in arcseconds per minute with the stars' sense
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
        string sense = d.Signed ? (d.RaPxPerMin >= 0 ? ", stelele spre est" : ", stelele spre vest") : "";
        if (s.PolarRaDriftMaxArcsecPerMin is not double raMax)
        {
            return $"{RunReport.F(ra, 1)}\"/min{sense} ({(s.DeclinationDeg is null ? "fara plate solve nu se stie cat e din aliniere" : $"sesiune sub {RunReport.F(Th.PolarRotationMinHours, 0)} h: nu se stie inca cat e din aliniere")})";
        }

        string share = $"din aliniere cel mult {RunReport.F(raMax, 1)}\"/min";
        return ra > raMax
            ? $"{RunReport.F(ra, 1)}\"/min{sense}; {share}, restul e urmarirea (viteza, eroarea periodica, flexiune), nu alinierea"
            : $"{RunReport.F(ra, 1)}\"/min{sense}; {share}: poate fi toata din aliniere";
    }

    /// <summary>
    /// The REZUMAT row `aliniere polara` (R-050): the verdict word (`OK`, `LA LIMITA`, `ATENTIE`,
    /// none when unknown) and the bracket with what it costs in one exposure, or why nothing
    /// is measured.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The verdict word, null when there is none, and the text: the error's bracket and its cost
    /// in pixels per exposure, at least the lower drift for ATENTIE or without an upper bound,
    /// both drifts for LA LIMITA, otherwise at most the upper one.
    /// </returns>
    private static (string? Verdict, string Text) PolarSummary(SeriesResult r)
    {
        if (PolarReason(r) is string why)
        {
            return (null, why);
        }

        SeriesAnalysis s = r.Series;
        string min = RunReport.F(s.PolarDriftMinPx ?? 0, 2), max = RunReport.F(s.PolarDriftMaxPx ?? 0, 2);
        string cost = s.PolarAlignment == PolarAlignment.Attention || s.PolarDriftMaxPx is null ? $"costa cel putin {min} px intr-o expunere"
            : s.PolarAlignment == PolarAlignment.Marginal ? $"costa intre {min} si {max} px intr-o expunere"
            : $"costa cel mult {max} px intr-o expunere";
        string suffix = s.PolarErrorMaxArcmin is null ? $" ({NoUpperBound(s)})" : "";
        string? verdict = s.PolarAlignment switch
        {
            PolarAlignment.Ok => "OK",
            PolarAlignment.Marginal => "LA LIMITA",
            PolarAlignment.Attention => "ATENTIE",
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
            return "- (fara perechi de cadre consecutive fara dithering)";
        }

        return $"{Px(d, r)} pe {RunReport.F(s.MedianExposureSeconds ?? 0, 0)} s, pe {DriftAxisWord(s)}; {Streak(r)}";
    }

    /// <summary>
    /// `dare de walking noise de pana la 11 px (cadrele 61-70, 10 cadre la 10 px)`, the dash of
    /// R-043; without a stretch of two frames there is no dash.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The dash's length in pixels and its stretch; an unknown dash without a drift; no dash
    /// without a stretch.
    /// </returns>
    private static string Streak(SeriesResult r)
    {
        SeriesAnalysis s = r.Series;
        return s.WalkingStreakPx is not double st ? "dara de walking noise necunoscuta (fara drift)"
            : r.Dither.WalkingStretch is not FrameStretch w ? $"nicio dara de walking noise ({NoRun()})"
            : $"dare de walking noise de pana la {RunReport.F(st, 1)} px ({StretchWord(w)})";
    }

    /// <summary>
    /// `cadrele 61-70, 10 cadre la 10 px`: the stretch of consecutive frames whose span is the
    /// dash.
    /// </summary>
    /// <param name="w">The stretch: its first and last frame index and its frame count.</param>
    /// <returns>The stretch in words, at <see cref="Th.CoverageLoose"/> px.</returns>
    private static string StretchWord(FrameStretch w) => $"cadrele {w.FirstIndex.ToString(Inv)}-{w.LastIndex.ToString(Inv)}, {FramesWord(w.Frames)} la {RunReport.F(Th.CoverageLoose, 0)} px";

    /// <summary>
    /// `niciun grup de 2+ cadre consecutive la 10 px`: the frames that would share the dash do
    /// not exist.
    /// </summary>
    /// <returns>The sentence, at <see cref="Th.CoverageLoose"/> px.</returns>
    private static string NoRun() => $"niciun grup de 2+ cadre consecutive la {RunReport.F(Th.CoverageLoose, 0)} px";

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
            new("cat de des", HowOften(d)),
            LongestRow(r, d),
            new("cat de mult", $"median {Px(d.MedianMove, r)}; " + string.Join("; ", d.Sizes.Select(c => $"{SizeWord(c)}: {c.Count.ToString(Inv)}"))),
            new("directie", $"numai X: {d.MovesOnXOnly.ToString(Inv)}; numai Y: {d.MovesOnYOnly.ToString(Inv)}; ambele: {d.MovesOnBoth.ToString(Inv)}; semn X: +{d.MovesXPlus.ToString(Inv)} / -{d.MovesXMinus.ToString(Inv)}; semn Y: +{d.MovesYPlus.ToString(Inv)} / -{d.MovesYMinus.ToString(Inv)}; {AxisLine(s)}"),
            new("acoperire", $"cel mai apropiat alt cadru median {Opt(Median(d.NearestOther), 1)} px; sub {RunReport.F(Th.CoverageTight, 0)} px: {d.WithinTight.ToString(Inv)}, sub {RunReport.F(Th.CoverageMid, 0)}: {d.WithinMid.ToString(Inv)}, sub {RunReport.F(Th.CoverageLoose, 0)}: {d.WithinLoose.ToString(Inv)}; izolate (>= {RunReport.F(Th.CoverageLoose, 0)} px de orice): {d.IsolatedLoose.ToString(Inv)}"),
            new("pozitii distincte", $"{d.DistinctMid.ToString(Inv)} la {RunReport.F(Th.CoverageMid, 0)} px, {d.DistinctLoose.ToString(Inv)} la {RunReport.F(Th.CoverageLoose, 0)} px; intindere X {RunReport.F(d.SpanX, 0)} px, Y {RunReport.F(d.SpanY, 0)} px"),
            new($"grupuri in {RunReport.F(Th.CoverageMid, 0)} px", Runs(d.RunsMid, r)),
            new($"grupuri in {RunReport.F(Th.CoverageLoose, 0)} px", Runs(d.RunsLoose, r)),
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
            new("acoperire", $"cel mai apropiat alt cadru median {Opt(Median(d.NearestOther), 1)} px; izolate (>= {RunReport.F(Th.CoverageLoose, 0)} px de orice): {d.IsolatedLoose.ToString(Inv)}"),
            new("intindere", $"X {RunReport.F(d.SpanX, 0)} px, Y {RunReport.F(d.SpanY, 0)} px"),
            new("axe", AxisLine(r.Series)),
            ScaleRow(r),
            SurvivingRow(r),
            PatternRow(r),
            new("verdict", text, verdict),
        ];
    }

    /// <summary>
    /// What a move means on the sky at this focal length: `1 px = 5.99"; o miscare de 5 px
    /// (pragul) cere 30" pe cer, una de 10 px 60"` (R-044).
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The `scara` row; `necunoscuta (fara focala)` when the scale is unknown.</returns>
    private static ReportRow ScaleRow(SeriesResult r)
        => new("scara", r.ArcsecPerPixel is double a
            ? $"1 px = {RunReport.F(a, 2)}\"; o miscare de {RunReport.F(Th.DitherStep, 0)} px (pragul) cere {RunReport.F(Th.DitherStep * a, 0)}\" pe cer, una de {RunReport.F(Th.CoverageLoose, 0)} px {RunReport.F(Th.CoverageLoose * a, 0)}\""
            : "necunoscuta (fara focala)");

    /// <summary>
    /// How much of one frame's pattern the mean of the stack keeps (R-048): `1.6 % din al unui
    /// cadru (ideal 1.1 % la 87 cadre; perechi de cadre la sub 1.5 px: 27)`.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The `pattern in stiva` row.</returns>
    private static ReportRow SurvivingRow(SeriesResult r)
    {
        DitherAnalysis d = r.Dither;
        return new("pattern in stiva", d.PatternSurviving is double f
            ? $"{RunReport.F(100 * f, 1)} % din al unui cadru ramane in medie (ideal {RunReport.F(100.0 / d.Registered, 1)} % la {FramesWord(d.Registered)}; perechi de cadre la sub {RunReport.F(Th.PatternSharePx, 1)} px: {d.SharingPairs.ToString(Inv)})"
            : "- (cadre neinregistrate)");
    }

    /// <summary>
    /// The sensor's pattern read from the lights, and whether the session's darks will take it
    /// out of the stack (R-046, R-042): `sigma 1.4 ADU pe R (12 % din zgomotul unui cadru), 2.1
    /// pe G1 (10 %), 2.1 pe G2 (10 %), 2.3 pe B (10 %); 4 pixeli fierbinti / Mpx peste 10 sigma
    /// (mediana a 87 cadre neinregistrate, 512 x 512 px din centru); fara dark-uri in sesiune:
    /// raman in cadre`.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The `pattern fix` row: the figures when the pattern was measured, else why not, and in
    /// both cases what the session's darks do.
    /// </returns>
    private static ReportRow PatternRow(SeriesResult r)
    {
        SensorPattern p = r.Pattern;
        string darks = r.Session.DarksDir is null
            ? "fara dark-uri in sesiune: raman in cadre"
            : $"{r.Session.Darks.Count.ToString(Inv)} dark-uri in {SessionFolder.DarksName}\\, Siril le scade";
        string value = p.Status switch
        {
            PatternStatus.Measured => $"sigma {string.Join(", ", Enumerable.Range(0, p.PatternSigma.Count).Select(i => $"{(double.IsNaN(p.PatternSigma[i]) ? "-" : RunReport.F(p.PatternSigma[i], 1))}{(i == 0 ? " ADU" : "")} pe {p.Names[i]} ({(double.IsNaN(p.FractionOfFrame(i)) ? "-" : RunReport.F(100 * p.FractionOfFrame(i), 0) + " %")}{(i == 0 ? " din zgomotul unui cadru" : "")})"))}; {RunReport.F(p.HotPerMpx, 0)} pixeli fierbinti / Mpx peste {RunReport.F(Th.PatternHotSigma, 0)} sigma (mediana a {FramesWord(p.Frames)} neinregistrate, {p.CropSize.ToString(Inv)} x {p.CropSize.ToString(Inv)} px din centru)",
            PatternStatus.TooFewFrames => $"nedeterminat (sub {Th.PatternMinFrames.ToString(Inv)} cadre masurate)",
            PatternStatus.Unregistered => "nedeterminat (cadre neinregistrate)",
            PatternStatus.FramesDidNotMove => $"nedeterminat (cadrele au stat pe loc: stelele raman in mediana; grupul cel mai mare la {RunReport.F(Th.CoverageTight, 0)} px are {FramesWord(r.Dither.LargestClusterTight)} din {r.Dither.Registered.ToString(Inv)})",
            _ => "nedeterminat (niciun cadru decodat)",
        };
        return new("pattern fix", $"{value}; {darks}");
    }

    /// <summary>
    /// What the Dithering tab says under its card: what walking noise is, why the focal
    /// length decides how much the same drift and the same dither are in pixels, what breaks
    /// it (bold), how to take darks without touching the lens, and where the figures come from
    /// (R-045). Built from Th so the thresholds it quotes follow a change there.
    /// </summary>
    internal static IReadOnlyList<ReportParagraph> DitherExplanation { get; } =
    [
        new($"Walking noise-ul e pattern-ul fix al senzorului (pixelii calzi, restul de dark) tras in dare: driftul il muta de la un cadru la altul, iar stiva aduna copiile pe traseul lui. Rezulta un zgomot corelat, alungit pe directia driftului, in grupurile de cadre consecutive ramase in acelasi loc la {RunReport.F(Th.CoverageLoose, 0)} px.", false),
        new("Aceeasi deriva a monturii si aceeasi miscare de dithering inseamna de atatea ori mai multi pixeli cu cat focala e mai lunga: la 20 mm un drift de 3\"/min sta sub un pixel pe toata sesiunea si nimic nu merge, la 135 mm e peste un pixel pe expunere. Focala scurta iarta; focala lunga cere dithering la fiecare cadru.", false),
        new($"Ce rupe walking noise-ul: o miscare la fiecare cadru, pe ambele axe, in directie aleatoare. Frecventa conteaza mai mult decat marimea: {RunReport.F(Th.DitherStep, 0)} px ajung cand doua cadre vecine nu mai impart acelasi loc; un salt mare la 2 cadre lasa grupuri in care driftul trage liniute.", true),
        new("Dark-urile scot pattern-ul inainte sa apuce sa mearga, si nu cer demontarea obiectivului sau a filtrului: scoti obiectivul cu tot cu filtru, pui capacul de body, inchizi ocularul vizorului si tragi 20 de cadre la acelasi ISO, acelasi timp si aceeasi temperatura, la finalul sesiunii. Flat-urile raman valabile.", false),
        new($"Cifrele: randul 'drift intr-o expunere' din DRIFT SI ROTATIE da driftul pe un cadru si dara de walking noise, cat de intins e cel mai intins grup de cadre consecutive ramase la {RunReport.F(Th.CoverageLoose, 0)} px; sub {RunReport.F(Th.WalkingStreakVisible, 0)} px nu e walking noise, oricum ar arata harta. Randul 'pattern in stiva' spune cat din pattern-ul unui cadru ramane in medie dupa stivuire (1 / cadre cand niciun cadru nu sta peste altul), iar 'pattern fix' cat de mare e pattern-ul senzorului fata de zgomotul unui cadru si daca sesiunea are dark-uri care sa-l scoata.", false),
    ];

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
            often.Add([HowOften(d), "-"]);   // "nicio miscare" or "o singura miscare", the console's words
        }

        List<IReadOnlyList<string>> sizes = [["median", Px(d.MedianMove, r)]];
        sizes.AddRange(d.Sizes.Select(c => (IReadOnlyList<string>)[SizeWord(c), c.Count.ToString(Inv)]));
        List<IReadOnlyList<string>> runs = [.. RunRows(d.RunsMid, Th.CoverageMid, r), .. RunRows(d.RunsLoose, Th.CoverageLoose, r)];
        return
        [
            new("cat de des", ["la fiecare", "de cate ori"], often),
            new("cat de mult", ["marime", "miscari"], sizes),
            new("directie", ["axa", "numai pe ea", "semn +", "semn -"],
            [
                [xAxis, d.MovesOnXOnly.ToString(Inv), d.MovesXPlus.ToString(Inv), d.MovesXMinus.ToString(Inv)],
                [yAxis, d.MovesOnYOnly.ToString(Inv), d.MovesYPlus.ToString(Inv), d.MovesYMinus.ToString(Inv)],
                ["ambele", d.MovesOnBoth.ToString(Inv), "", ""],
            ]),
            new("acoperire", ["raza", "cu alt cadru sub raza", "pozitii distincte", $"grupuri de {Th.RunMinFrames.ToString(Inv)}+ consecutive"],
            [
                [PxWord(Th.CoverageTight), d.WithinTight.ToString(Inv), "-", "-"],
                [PxWord(Th.CoverageMid), d.WithinMid.ToString(Inv), d.DistinctMid.ToString(Inv), d.RunsMid.Count.ToString(Inv)],
                [PxWord(Th.CoverageLoose), d.WithinLoose.ToString(Inv), d.DistinctLoose.ToString(Inv), d.RunsLoose.Count.ToString(Inv)],
            ]),
            new("grupuri in acelasi loc", ["raza", "cadre", "cate", "minute"], runs),
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
        => new($"pauze > {RunReport.F(Th.DitherPause, 0)} s", $"{d.Pauses.ToString(Inv)}; miscari >= {RunReport.F(Th.DitherStep, 0)} px: {d.Moves.ToString(Inv)}; pauze fara miscare: {d.PausesWithoutMove.ToString(Inv)}; miscari fara pauza: {d.MovesWithoutPause.ToString(Inv)}; cadre inregistrate: {d.Registered.ToString(Inv)} din {r.Frames.Count.ToString(Inv)}");

    /// <summary>
    /// The row of the longest stretch between two moves: its first and last frame, its frame
    /// count, and its duration when the frames carry a time.
    /// </summary>
    /// <param name="r">The result of the series run, for the times of the frames.</param>
    /// <param name="d">The dither analysis of the series.</param>
    /// <returns>The `cel mai lung interval intre doua miscari` row.</returns>
    private static ReportRow LongestRow(SeriesResult r, DitherAnalysis d)
        => new("cel mai lung interval intre doua miscari", $"cadrele {d.LongestStretchFirst.ToString(Inv)}-{d.LongestStretchLast.ToString(Inv)} ({d.LongestStretchFrames.ToString(Inv)} cadre de la o miscare la urmatoarea{Minutes(r, d.LongestStretchFirst, d.LongestStretchLast)})");

    /// <summary>A frame count with its noun in the right number.</summary>
    /// <param name="frames">The count.</param>
    /// <returns>"1 cadru", or "N cadre" for any other count.</returns>
    private static string FramesWord(int frames) => $"{frames.ToString(Inv)} {(frames == 1 ? "cadru" : "cadre")}";

    /// <summary>A number of times with its noun in the right number.</summary>
    /// <param name="times">The count.</param>
    /// <returns>"1 data", or "N ori" for any other count.</returns>
    private static string TimesWord(int times) => $"{times.ToString(Inv)} {(times == 1 ? "data" : "ori")}";

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
    /// The rows of one radius in the runs table: one per run, or a single `niciunul` row.
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
            return [[PxWord(radius), "niciunul", "-", "-"]];
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
            FlagKind.Trailed => $"alungire {RunReport.F(f.Value, 2)} (prag {RunReport.F(f.Threshold, 2)}){TrailSuffix(f)}",
            FlagKind.MarginalTrail => $"alungire {RunReport.F(f.Value, 2)} la limita (prag {RunReport.F(f.Threshold, 2)}){TrailSuffix(f)}",
            FlagKind.Soft => $"FWHM {RunReport.F(f.Value, 2)} px, peste {RunReport.F(f.Threshold, 2)} ({RunReport.F(Th.SoftFwhmFactor, 1)} x mediana sesiunii)",
            FlagKind.Clouds => $"stele {RunReport.F(f.Value, 0)}, sub {RunReport.F(f.Threshold, 0)} ({RunReport.F(100 * Th.CloudStarFraction, 0)} % din mediana): nori sau ceata",
            FlagKind.BackgroundJump => $"salt de fond {Signed(f.Value, 0)} % fata de vecini (prag {RunReport.F(f.Threshold, 0)} %)",
            FlagKind.Trails => $"{RunReport.F(f.Value, 0)} dara (satelit/avion; sigma clipping o scoate)",
            FlagKind.Unreadable => "nu s-a putut citi" + (f.Detail is null ? "" : ": " + f.Detail),
            FlagKind.Unregistered => "neinregistrat (prea putine stele potrivite cu referinta)",
            _ => f.Kind.ToString(),
        };
    }

    /// <summary>
    /// `, dara pe RA (-90 grade)` when the trail's axis is known, `, dara la -90 grade` when only
    /// its angle is, nothing without an angle (R-033).
    /// </summary>
    /// <param name="f">
    /// The trail flag; its Detail holds the angle in degrees, already formatted.
    /// </param>
    /// <returns>The suffix, starting with ", ", or "".</returns>
    private static string TrailSuffix(FrameFlag f)
        => f.Detail is null ? "" : f.On == TrailAxis.Unknown ? $", dara la {f.Detail} grade" : $", dara {AxisWord(f.On)} ({f.Detail} grade)";

    /// <summary>The mount axis a trail falls on, as the report's sentences say it.</summary>
    /// <param name="on">The trail's axis.</param>
    /// <returns>"pe RA", "pe DEC", "oblica", or "" for an unknown axis.</returns>
    internal static string AxisWord(TrailAxis on) => on switch
    {
        TrailAxis.Ra => "pe RA",
        TrailAxis.Dec => "pe DEC",
        TrailAxis.Oblique => "oblica",
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
    /// <returns>"bun", "la limita", "SLAB", or "necitit" for an unknown class.</returns>
    internal static string ClassWord(FrameClass c) => c switch
    {
        FrameClass.Good => "bun",
        FrameClass.Marginal => "la limita",
        FrameClass.Bad => "SLAB",
        _ => "necitit",
    };

    /// <summary>
    /// The focus verdict and its text: the session's FWHM and whether the focus held.
    /// </summary>
    /// <param name="s">The series analysis.</param>
    /// <param name="r">The result of the series run, for the scale.</param>
    /// <returns>
    /// NEDETERMINAT without a good frame, ATENTIE when the focus moved, BUN otherwise (also when
    /// too few good frames show a trend); the text gives the median FWHM and the trend.
    /// </returns>
    private static (string Verdict, string Text) Focus(SeriesAnalysis s, SeriesResult r)
    {
        if (s.SessionFwhm is null)
        {
            return ("NEDETERMINAT", "niciun cadru bun");
        }

        string held = s.FocusHeld is bool h ? (h ? "a tinut toata sesiunea" : "s-a miscat") : "prea putine cadre bune pentru tendinta";
        return (s.FocusHeld is false ? "ATENTIE" : "BUN", $"FWHM median {Px(s.SessionFwhm, r)} pe cadrele bune, {TrendPx(s.Fwhm)} la inceput / sfarsit: {held}");
    }

    /// <summary>
    /// The exposure verdict and its text, from the channel whose shot-to-read ratio is the lowest
    /// of all the sampled frames.
    /// </summary>
    /// <param name="s">The series analysis.</param>
    /// <param name="r">The result of the series run (not read).</param>
    /// <returns>
    /// BUNA when that ratio reaches <see cref="Th.SwampingRatioOk"/>, LA LIMITA under it,
    /// NEMASURAT without biases or without a finite ratio; the text opens with the green
    /// background of the first and last good frames in ADU.
    /// </returns>
    private static (string Verdict, string Text) Exposure(SeriesAnalysis s, SeriesResult r)
    {
        string bg = s.Background.First is double bf && s.Background.Last is double bl ? $"fond G {RunReport.F(bf, 0)} -> {RunReport.F(bl, 0)} ADU" : "fond necunoscut";
        if (s.Swamping is null || s.Swamping.Count == 0)
        {
            return ("NEMASURAT", bg + "; zgomotul de citire nemasurat (fara bias-uri)");
        }

        ChannelNoise? worst = s.Swamping.SelectMany(x => x).Where(c => !double.IsNaN(c.SwampingRatio) && !double.IsInfinity(c.SwampingRatio)).MinBy(c => c.SwampingRatio);
        if (worst is null)
        {
            return ("NEMASURAT", bg + "; raportul shot/citire nu s-a putut masura (fereastra de zgomot prea mica)");
        }

        return (worst.SwampingRatio >= Th.SwampingRatioOk ? "BUNA" : "LA LIMITA", $"{bg}; canalul cel mai slab {worst.Name}: shot/citire {RunReport.F(worst.SwampingRatio, 1)}, penalizare {RunReport.F(worst.PenaltyPct, 0)} %");
    }

    /// <summary>
    /// The dew verdict and its text: the change of the FWHM in percent and of the halo.
    /// </summary>
    /// <param name="s">The series analysis.</param>
    /// <returns>
    /// POSIBIL when dew is suspected, NU when not, NEDETERMINAT with too few good frames.
    /// </returns>
    private static (string Verdict, string Text) Dew(SeriesAnalysis s) => s.DewSuspected switch
    {
        true => ("POSIBIL", $"FWHM a crescut cu {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} % si halo-ul cu {Signed(s.Halo.Change ?? 0, 3)}"),
        false => ("NU", $"FWHM {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} %, halo {Signed(s.Halo.Change ?? 0, 3)}"),
        null => ("NEDETERMINAT", "prea putine cadre bune"),
    };

    /// <summary>Which image axis is RA and which DEC, and where that came from.</summary>
    /// <param name="s">The series analysis, with the RA axis and its source.</param>
    /// <returns>The sentence, or `axa RA nedeterminata` when the RA axis is unknown.</returns>
    private static string AxisLine(SeriesAnalysis s) => s.RaAxis switch
    {
        Axis.X => $"axa X a imaginii = RA, Y = DEC ({s.AxisSource})",
        Axis.Y => $"axa Y a imaginii = RA, X = DEC ({s.AxisSource})",
        _ => "axa RA nedeterminata",
    };

    /// <summary>
    /// The two dithering verdicts in one word pair, the worse first so the chip takes its
    /// colour (R-021, R-043): walking noise `OK` by the dither rule, `FARA` when the drift
    /// draws a dash under Th.WalkingStreakVisible in the longest run at 10 px, `INSUFICIENT`
    /// otherwise; hot pixels `OK` or `ATENTIE`. The words stay short for the chip; the dash,
    /// the drift, the run and the hot-pixel rule open the text.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The two words joined by ", " and the text, the counts of the moves and positions last;
    /// NEDETERMINAT when fewer than two frames were registered.
    /// </returns>
    private static (string Verdict, string Text) DitherVerdict(SeriesResult r)
    {
        DitherAnalysis d = r.Dither;
        SeriesAnalysis s = r.Series;
        if (d.Registered < 2)
        {
            return ("NEDETERMINAT", "cadre neinregistrate");
        }

        (int Severity, string Word, string? Detail) hot = d.HotPixelsOk
            ? (0, "OK pentru pixeli calzi", null)
            : (2, "ATENTIE la pixeli calzi", $"pixeli calzi: peste {Th.HotPixelRunMax.ToString(Inv)} cadre consecutive in {RunReport.F(Th.CoverageTight, 0)} px");
        string run = d.WalkingStretch is FrameStretch w ? StretchWord(w) : NoRun();
        string drift = s.DriftPerExposurePx is double dpe ? $"drift {RunReport.F(dpe, 2)} px pe expunere, {run}" : $"drift nemasurat, {run}";
        (int Severity, string Word, string? Detail) walk = s.WalkingNoise switch
        {
            WalkingNoise.Ok => (0, "OK pentru walking noise", null),
            WalkingNoise.Static => (0, "FARA walking noise", d.WalkingStretch is null ? $"nicio dara de walking noise: {drift}" : $"dare de walking noise de cel mult {RunReport.F(s.WalkingStreakPx ?? 0, 1)} px: {drift}"),
            _ => (3, "INSUFICIENT pentru walking noise", s.WalkingStreakPx is double st ? $"dare de walking noise de pana la {RunReport.F(st, 1)} px pe {DriftAxisWord(s)}: {drift}" : $"walking noise: {drift}"),
        };
        (var first, var second) = walk.Severity >= hot.Severity ? (walk, hot) : (hot, walk);
        string counts = $"{d.Moves.ToString(Inv)} miscari la {d.Registered.ToString(Inv)} cadre, {d.DistinctLoose.ToString(Inv)} pozitii distincte la {RunReport.F(Th.CoverageLoose, 0)} px, {d.IsolatedLoose.ToString(Inv)} izolate, {d.MovesOnBoth.ToString(Inv)} miscari pe ambele axe";
        return ($"{first.Word}, {second.Word}", string.Join("; ", new[] { first.Detail, second.Detail, counts }.Where(x => x is not null)));
    }

    /// <summary>
    /// The `de scos din stiva` cell: how many frames to leave out (bad and unreadable), how many
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

        return $"{(s.BadCount + s.UnknownCount).ToString(Inv)} cadre (lista la RESPINSE); raman {(s.GoodCount + s.MarginalCount).ToString(Inv)} = {RunReport.Duration(kept)}";
    }

    /// <summary>
    /// How often the mount moved: each interval between moves in frames and how many times.
    /// </summary>
    /// <param name="d">The dither analysis of the series.</param>
    /// <returns>
    /// The intervals, shortest first, joined by "; "; `nicio miscare` without a move, `o singura
    /// miscare` when no interval was counted.
    /// </returns>
    private static string HowOften(DitherAnalysis d)
    {
        if (d.Moves == 0)
        {
            return "nicio miscare";
        }

        List<string> parts = [];
        foreach (KeyValuePair<int, int> kv in d.FramesBetweenMoves.OrderBy(k => k.Key))
        {
            parts.Add($"la fiecare {FramesWord(kv.Key)}: {TimesWord(kv.Value)}");
        }

        return parts.Count == 0 ? "o singura miscare" : string.Join("; ", parts);
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
    /// <returns>Each run's first-last frames, frame count and duration, or `niciunul`.</returns>
    private static string Runs(IReadOnlyList<FrameStretch> runs, SeriesResult r)
        => runs.Count == 0 ? "niciunul" : string.Join(", ", runs.Select(x => $"{x.FirstIndex.ToString(Inv)}-{x.LastIndex.ToString(Inv)} ({x.Frames.ToString(Inv)}{Minutes(r, x.FirstIndex, x.LastIndex)})"));

    /// <summary>The indices of the frames of one class.</summary>
    /// <param name="r">The result of the series run.</param>
    /// <param name="c">The class to list.</param>
    /// <returns>The indices joined by ", ", or `niciunul`.</returns>
    private static string List(SeriesResult r, FrameClass c)
    {
        List<int> idx = Enumerable.Range(0, r.Frames.Count).Where(i => r.Series.Classes[i] == c).Select(i => r.Frames[i].Info.Index).ToList();
        return idx.Count == 0 ? "niciunul" : string.Join(", ", idx.Select(i => i.ToString(Inv)));
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
