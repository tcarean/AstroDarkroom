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
/// <param name="Exposure">The exposure in seconds, one decimal, with ` s`; `-` without one (R-085).</param>
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
internal sealed record FrameTableRow(int Index, string FileName, string Time, string Exposure, string Class, string Elongation, string Fwhm, string Stars, string Background, string Pause, string Step, string Dx, string Dy, string Move, string Spot, string Reasons, string? StepTip);

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
            $"    {"no",3}  {"file",-20}  {"time",-8}  {"exposure",8}  {"class",-8}  {"elong",5}  {"FWHM",5}  {"stars",6}  {"bg",5}  {"pause",5}  {"step",5}  {"dX",6}  {"dY",6}  {"dithering",-42}  {"same spot",-14}  reasons",
            $"     nr  fisier                ora       expunere  clasa     alung  FWHM   stele   fond  pauza    pas     dX      dY  {"dithering",-42}  acelasi loc     motive"));
        foreach (FrameTableRow f in FrameRows(r))
        {
            string name = f.FileName.Length > 20 ? f.FileName[..20] : f.FileName;
            w.WriteLine($"    {f.Index.ToString(Inv),3}  {name,-20}  {f.Time,-8}  {f.Exposure,8}  {f.Class,-8}  {f.Elongation,5}  {f.Fwhm,5}  {f.Stars,6}  {f.Background,5}  {f.Pause,5}  {f.Step,5}  {f.Dx,6}  {f.Dy,6}  {f.Move,-42}  {f.Spot,-14}  {f.Reasons}");
        }
    }

    /// <summary>
    /// The text in the REJECTED (RESPINSE) heading's parentheses, in plain words (R-081): the bad
    /// frames, the unread ones when there are any, each count singular for one, and that the
    /// marginal frames stay in the stack.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// `2 bad frames; the marginal ones stay in the stack, your choice`, with ` and 1 unread frame`
    /// after the bad count when frames went unread (`2 cadre slabe; cele la limita raman in stiva,
    /// la alegerea ta`, ` si 1 cadru necitit`).
    /// </returns>
    internal static string RejectedHeading(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        string bad = Count(s.BadCount, ("bad frame", "cadru slab"), ("bad frames", "cadre slabe"));
        string unread = s.UnknownCount > 0
            ? T(" and ", " si ") + Count(s.UnknownCount, ("unread frame", "cadru necitit"), ("unread frames", "cadre necitite"))
            : "";
        return bad + unread + T("; the marginal ones stay in the stack, your choice", "; cele la limita raman in stiva, la alegerea ta");
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
                f.Info.ExposureSeconds is double ex ? RunReport.F(ex, 1) + " s" : "-",
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
    /// The SynScan arrows of a signed move, `←, ↑`, for the CSV; empty otherwise (R-038, R-040).
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
    /// What the Rejected tab says under its heading, one paragraph per line, in plain words
    /// (R-081): the elongation, where the marginal frames are, the trail and its axis, in bold
    /// what each axis points to (R-035, R-036), and which exposure leaves the stack (R-085).
    /// Built when read, in the language of that moment.
    /// </summary>
    internal static IReadOnlyList<ReportParagraph> RejectedExplanation => IsRomanian
    ?
    [
        new($"Alungirea spune de cate ori sunt stelele mai lungi decat late, de obicei, intr-un cadru (1 = rotunde). De la {RunReport.F(Th.ElongationBad, 2)} cadrul e slab si iese din stiva.", false),
        new($"Intre {RunReport.F(Th.ElongationMarginal, 2)} si {RunReport.F(Th.ElongationBad, 2)} cadrul e la limita si ramane in stiva, daca nu hotarasti altfel. Cadrele la limita sunt pe randul 'la limita' din CADRE MISCATE, pe tabul Rezumat, iar pe tabul Cadre au clasa galbena.", false),
        new("Dara e directia in care s-au intins stelele in timpul expunerii, data ca unghi fata de orizontala imaginii. Cand se stie care axa a imaginii e RA si stelele cadrului s-au intins toate cam in aceeasi directie, motivul spune si pe ce axa a monturii cade dara.", false),
        new("O dara pe RA vine de la urmarire: eroarea periodica a monturii, o viteza de urmarire gresita sau jocul din angrenaje (backlash) pe RA. O dara pe DEC vine de la alinierea polara, de la vant sau de la o atingere a monturii. Una oblica vine de la amandoua sau de la o lovitura.", true),
        new($"Un cadru expus sub {ShortFractionWord} din timpul obisnuit e slab si iese din stiva: aduce aproape deloc semnal, iar Siril ar trebui sa-i intinda zgomotul pana la nivelul celorlalte. Un cadru cu alta expunere, mai lunga sau putin mai scurta, ramane: Siril egalizeaza nivelurile la stivuire, iar motivul doar o noteaza.", false),
    ]
    :
    [
        new($"The elongation says how many times longer than wide the stars are, typically, in a frame (1 = round). From {RunReport.F(Th.ElongationBad, 2)} the frame is bad and leaves the stack.", false),
        new($"Between {RunReport.F(Th.ElongationMarginal, 2)} and {RunReport.F(Th.ElongationBad, 2)} the frame is marginal and stays in the stack unless you decide otherwise. The marginal frames are on the 'marginal' row of TRAILED FRAMES, on the Summary tab, and on the Frames tab their class is yellow.", false),
        new("The trail is the direction the stars were stretched in during the exposure, given as an angle to the image's horizontal. When it is known which image axis is RA and the frame's stars were all stretched about the same way, the reason also says which mount axis the trail falls on.", false),
        new("A trail on RA comes from the tracking: the mount's periodic error, a wrong tracking rate or the play in the gears (backlash) on RA. A trail on DEC comes from the polar alignment, the wind or a touch on the mount. An oblique one comes from both, or from a knock.", true),
        new($"A frame exposed under {ShortFractionWord} the usual time is bad and leaves the stack: it brings almost no signal, and Siril would have to stretch its noise up to the others' level. A frame with another exposure, longer or a little shorter, stays in: Siril evens the levels out when stacking, and the reason only notes it.", false),
    ];

    /// <summary>
    /// The caption of a star cut-out: where it was taken and the star's three figures.
    /// </summary>
    /// <param name="c">The cut-out and its star; not null.</param>
    /// <returns>
    /// The place, the width (FWHM) in pixels, the elongation and the angle in degrees, each
    /// named in plain words (R-079).
    /// </returns>
    internal static string CropCaption(StarCrop c)
    {
        ArgumentNullException.ThrowIfNull(c);
        string fwhm = RunReport.F(c.Star.Fwhm, 1), elongation = RunReport.F(c.Star.Elongation, 2), angle = RunReport.F(c.Star.AngleDeg, 0);
        return T(
            $"{c.Where}: width (FWHM) {fwhm} px, elongation {elongation}, angle {angle} degrees",
            $"{c.Where}: latime (FWHM) {fwhm} px, alungire {elongation}, unghi {RoCount(angle)}grade");
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
    /// The `scale` (`scara`) line, in plain words (R-078): the image scale, the pixel size and
    /// its source, the frame the others are aligned on.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// `1 px = 6.11" on the sky; the pixel is 4.00 um (worked out from the raw, ...); the frames
    /// are aligned on frame 1`; the scale `not known` without a focal length, and `the frames were
    /// not aligned` when no reference was chosen.
    /// </returns>
    internal static string ScaleLine(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        string scale = r.ArcsecPerPixel is double sc ? T($"1 px = {RunReport.F(sc, 2)}\" on the sky", $"1 px = {RunReport.F(sc, 2)}\" pe cer") : T("not known (no focal length)", "nu se stie (lipseste focala)");
        string pixel = RunReport.F(r.PixelSizeUm, 2);
        string reference = r.ReferenceIndex > 0
            ? T($"the frames are aligned on frame {r.ReferenceIndex.ToString(Inv)}", $"cadrele sunt aliniate dupa cadrul {r.ReferenceIndex.ToString(Inv)}")
            : T("the frames were not aligned", "cadrele nu s-au aliniat");
        return T(
            $"{scale}; the pixel is {pixel} um ({r.PixelSizeSource}); {reference}",
            $"{scale}; pixelul are {pixel} um ({r.PixelSizeSource}); {reference}");
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
                $"{RoCount(s.BadCount)}slabe, {s.MarginalCount.ToString(Inv)} la limita, {RoCount(s.GoodCount)}bune{(s.UnknownCount > 0 ? ", " + RoCount(s.UnknownCount) + "necitite" : "")} (din {r.Frames.Count.ToString(Inv)}); {AxisLine(s)}")),
            new("dithering", ditherText, dither),
            new(T("polar alignment", "aliniere polara"), polarText, polarVerdict),
            new(T("leave out of stack", "de scos din stiva"), Keep(r)),
        ];
    }

    /// <summary>
    /// The FOCUS section, in plain words (R-078): the stars' width on the good frames, the first
    /// against the last good frames and whether the focus held, the width in three zones, and the
    /// verdict.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The four rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> FocusRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        (string fv, string ft) = Focus(s, r);
        string held = s.FocusHeld is bool fh ? (fh ? T("the focus held", "focusul a tinut") : T("the focus changed", "focusul s-a schimbat")) : T("too few good frames", "prea putine cadre bune");
        string typically = T("(typically, on the good frames)", "(de obicei, pe cadrele bune)");
        return
        [
            new(T("the stars' width (FWHM)", "latimea stelelor (FWHM)"), $"{Px(s.SessionFwhm, r)} {typically}"),
            new(T($"first / last {s.Fwhm.Count.ToString(Inv)} good frames", $"primele / ultimele {RoCount(s.Fwhm.Count)}cadre bune"), $"{Opt(s.Fwhm.First, 2)} px / {Opt(s.Fwhm.Last, 2)} px{ChangeWord(s.Fwhm)}: {held}"),
            new(T("in the centre / in the middle / at the edge", "in centru / la mijloc / la margine"), T($"the stars' width {ZoneMedians(r)} {typically}", $"latimea stelelor {ZoneMedians(r)} {typically}")),
            new("verdict", ft, fv),
        ];
    }

    /// <summary>The relative change of a trend as a suffix: ` (+14 %)`.</summary>
    /// <param name="t">The trend; its change may be null.</param>
    /// <returns>The signed change in percent in brackets after a space, or "" when unknown.</returns>
    private static string ChangeWord(Trend t) => t.RelativeChange is double rc ? " (" + Signed(100 * rc, 0) + " %)" : "";

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
    /// The EXPOSURE AND NOISE section, in plain words (R-078): the sky background of the first
    /// and last good frames, the read noise per channel from the bias pair, then per sampled frame
    /// the sky, the shot-to-read ratio and the extra noise of each channel, and the row that says
    /// what the ratio means.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The rows in the console's order; only the background and a `could not be measured`
    /// read-noise row when the session has no two biases.
    /// </returns>
    internal static IReadOnlyList<ReportRow> NoiseRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        string first = Opt(s.Background.First, 0), last = Opt(s.Background.Last, 0);
        List<ReportRow> rows =
        [
            new(T("sky background (good frames, first -> last)", "fondul cerului (cadrele bune, primele -> ultimele)"), T(
                $"{first} -> {last} ADU on green, above the black level{ChangeWord(s.Background)}",
                $"{first} -> {last} ADU pe verde, peste nivelul de negru{ChangeWord(s.Background)}")),
        ];
        string readNoise = T("read noise", "zgomot de citire");
        if (s.ReadNoise is null)
        {
            rows.Add(new(readNoise, T("could not be measured (at least two biases are needed in biases\\)", "nu s-a putut masura (trebuie cel putin doua bias-uri in biases\\)")));
            return rows;
        }

        rows.Add(new(readNoise, string.Join("  ", s.ReadNoise.Select(n => $"{n.Name} {RunReport.F(n.PairSigma, 1)}")) + T(" ADU (the noise the sensor adds to every frame, measured from two biases)", " ADU (zgomotul pe care il adauga senzorul la fiecare cadru, masurat din doua bias-uri)")));
        for (int i = 0; i < s.SwampingFrames.Count; i++)
        {
            rows.Add(new($"{T("frame", "cadrul")} {s.SwampingFrames[i].ToString(Inv),3}", string.Join("  ", s.Swamping![i].Select(c =>
            {
                string sky = RunReport.F(c.Sky, 0), ratio = RunReport.F(c.SwampingRatio, 1), extra = double.IsNaN(c.PenaltyPct) ? "-" : RunReport.F(c.PenaltyPct, 1) + " %";
                return T($"{c.Name}: sky {sky}, shot/read {ratio}, extra noise {extra}", $"{c.Name}: cer {sky}, shot/citire {ratio}, zgomot in plus {extra}");
            }))));
        }

        string ok = RunReport.F(Th.SwampingRatioOk, 0);
        rows.Add(new(T("what it means", "ce inseamna"), T(
            $"shot/read from {ok} up: the sky covers the read noise, which adds under 5 %; under {ok}: the channel suffers from the read noise, and longer exposures would help",
            $"shot/citire de la {ok} in sus: cerul acopera zgomotul de citire, care adauga sub 5 %; sub {ok}: canalul sufera de zgomotul de citire, iar expuneri mai lungi ar ajuta")));
        return rows;
    }

    /// <summary>
    /// The DEW section, in plain words (R-078): the light around the bright stars (the halo), the
    /// stars found and the stars' width of the first against the last good frames, and the dew
    /// verdict.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The two rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> DewRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        (string dv, string dt) = Dew(s);
        string halo = $"{Opt(s.Halo.First, 3)} / {Opt(s.Halo.Last, 3)}", stars = $"{Opt(s.Stars.First, 0)} -> {Opt(s.Stars.Last, 0)}{ChangeWord(s.Stars)}";
        return
        [
            new(T("halo (the light around the stars), first / last", "halo (lumina din jurul stelelor), primele / ultimele"), T(
                $"{halo}; stars found {stars}; the stars' width {TrendPx(s.Fwhm)}",
                $"{halo}; stele gasite {stars}; latimea stelelor {TrendPx(s.Fwhm)}")),
            new("verdict", dt, dv),
        ];
    }

    /// <summary>
    /// The TRAILED FRAMES section, in plain words (R-078): the trailed frames (bad by their
    /// elongation; a frame bad for its exposure alone is not listed, R-085), the marginal frames
    /// (each row naming its elongation bounds, <see cref="Th.ElongationBad"/> and
    /// <see cref="Th.ElongationMarginal"/>), and the direction of the trails with the mount axis it
    /// falls on.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The three rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> TrailRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        string bad = RunReport.F(Th.ElongationBad, 2), marginal = RunReport.F(Th.ElongationMarginal, 2);
        string direction = s.TrailAngleDeg is double ta
            ? T(RunReport.F(ta, 0) + " degrees from the horizontal, on the bad frames", RoCount(RunReport.F(ta, 0)) + "grade fata de orizontala, pe cadrele slabe") + (s.TrailOn == TrailAxis.Unknown ? "" : T(", that is ", ", adica ") + AxisWord(s.TrailOn))
            : T("not known (too few bad frames with trails in the same direction)", "nu se stie (prea putine cadre slabe cu dare in aceeasi directie)");
        return
        [
            new(T($"bad (elongation of at least {bad})", $"slabe (alungire de cel putin {bad})"), List(r, FlagKind.Trailed)),
            new(T($"marginal (elongation between {marginal} and {bad})", $"la limita (alungire intre {marginal} si {bad})"), List(r, FrameClass.Marginal)),
            new(T("trail direction", "directia darelor"), $"{direction}; {AxisLine(s)}"),
        ];
    }

    /// <summary>
    /// The DRIFT AND ROTATION section, in plain words (R-078): the drift between frames in a row
    /// without dithering in pixels per minute (and arcseconds per minute when the scale is known),
    /// the drift over one exposure, the field rotation, the polar alignment and the RA drift.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The five rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> DriftRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        string arcsec = r.ArcsecPerPixel is double a1 && s.DriftXPerMin is double dxm && s.DriftYPerMin is double dym ? $" = {Signed(dxm * a1, 1)} / {Signed(dym * a1, 1)} \"/min" : "";
        return
        [
            new(T("drift between frames in a row, without dithering", "drift intre cadre la rand, fara dithering"), $"X {Signed(s.DriftXPerMin, 2)} px/min, Y {Signed(s.DriftYPerMin, 2)} px/min{arcsec} ({PairsWord(s)})"),
            new(DriftPerExposureLabel, DriftPerExposure(r)),
            new(T("field rotation", "rotatie de camp"), RotationWords(s)),
            new(T("polar alignment", "aliniere polara"), PolarRow(r)),
            new(T("RA drift", "drift pe RA"), RaDriftRow(r)),
        ];
    }

    /// <summary>
    /// The field rotation in plain words (R-078): `the image turned by 8.5' in all, 2.5' an hour;
    /// because of this, a star in the corner moves 0.09 px over one exposure`.
    /// </summary>
    /// <param name="s">The series analysis, with the rotation and the corner shift; not null.</param>
    /// <returns>The parts that are known; `-` in place of the total when it is not.</returns>
    private static string RotationWords(SeriesAnalysis s)
    {
        string total = s.RotationTotalDeg is double rt
            ? T($"the image turned by {RunReport.F(rt * 60, 1)}' in all", $"imaginea s-a rotit cu {RunReport.F(rt * 60, 1)}' in total")
            : "-";
        string perHour = s.RotationPerHourDeg is double rh ? T($", {RunReport.F(rh * 60, 1)}' an hour", $", {RunReport.F(rh * 60, 1)}' pe ora") : "";
        string corner = s.CornerShiftPerExposurePx is double cs
            ? T($"; because of this, a star in the corner moves {RunReport.F(cs, 2)} px over one exposure", $"; din cauza asta, o stea din colt se misca {RunReport.F(cs, 2)} px intr-o expunere")
            : "";
        return total + perHour + corner;
    }

    /// <summary>
    /// The pairs the drift was measured on (R-049, R-078): `from 39 pairs of frames without a pause
    /// between them`, or `from 29 pairs of frames with a pause; approximate, since the small dither
    /// moves count in the figure` when the drift fell back to the paused pairs.
    /// </summary>
    /// <param name="s">The series analysis, with the drift's pair count; not null.</param>
    /// <returns>The pair count, singular for one, and the kind of pairs.</returns>
    private static string PairsWord(SeriesAnalysis s)
    {
        string pairs = s.DriftPairs == 1 ? T("1 pair", "1 pereche") : T($"{s.DriftPairs.ToString(Inv)} pairs", RoCount(s.DriftPairs) + "perechi");
        return s.DriftApproximate
            ? T($"from {pairs} of frames with a pause; approximate, since the small dither moves count in the figure", $"din {pairs} de cadre cu pauza; aproximativ, fiindca in cifra intra si miscarile mici de dithering")
            : T($"from {pairs} of frames without a pause between them", $"din {pairs} de cadre fara pauza intre ele");
    }

    /// <summary>
    /// Why the polar alignment is not measured, or null when it is (R-050, in the words of
    /// R-078): the frames not aligned, under three drift pairs, no RA axis, no scale, no exposure.
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
            : r.Dither.Registered < 2 ? T("- (the frames could not be aligned)", "- (cadrele nu s-au putut alinia)")
            : s.StarDriftPerMin is null && (s.DriftApproximate || s.DriftXPerMin is null || s.DriftPairs < Th.DriftMinPairs) ? T(
                $"cannot be measured (under {Th.DriftMinPairs.ToString(Inv)} pairs of frames in a row, with no pause and no move between them; 10 or more frames in a row without dithering would do)",
                $"nu se poate masura (sub {RoCount(Th.DriftMinPairs)}perechi de cadre la rand, fara pauza si fara miscare intre ele; 10 sau mai multe cadre la rand fara dithering ar ajunge)")
            : s.RaAxis == Axis.Unknown ? T("- (it is not known which axis is RA)", "- (nu se stie care axa e RA)")
            : r.ArcsecPerPixel is null ? T("- (the scale is not known)", "- (nu se stie scara)")
            : T("- (the exposure is not known)", "- (nu se stie expunerea)");
    }

    /// <summary>
    /// Why there is no upper bound when the lower one exists (R-050, R-078): no solve for the
    /// declination, or a span under Th.PolarRotationMinHours.
    /// </summary>
    /// <param name="s">The series analysis; not null.</param>
    /// <returns>
    /// The no-solve sentence when the declination is unknown, else the short-span one.
    /// </returns>
    private static string NoUpperBound(SeriesAnalysis s)
        => s.DeclinationDeg is null
            ? T("without a plate solve the declination is not known, so neither is how large it can be", "fara plate solve nu se stie declinatia, deci nici cat poate fi cel mult")
            : T($"the session is under {RunReport.F(Th.PolarRotationMinHours, 0)} h, too short to find how large it can be", $"sesiunea are sub {RunReport.F(Th.PolarRotationMinHours, 0)} h, prea putin ca sa se afle cat poate fi cel mult");

    /// <summary>
    /// How far the mount's axis is off the pole (R-050, R-078): `the mount's axis is off the pole
    /// by 4.2' to 10.4'`, `by 5.7'` when the bounds meet, `by at least 5.7'` without an upper bound
    /// (`axa monturii e deviata de la pol cu ...` in Romanian).
    /// </summary>
    /// <param name="s">
    /// The series analysis, with the polar error's bounds in arcminutes; not null.
    /// </param>
    /// <returns>
    /// The bracket of the polar error, one decimal; the bounds meet when they print the same.
    /// </returns>
    private static string PolarBounds(SeriesAnalysis s)
    {
        string axis = T("the mount's axis is off the pole by", "axa monturii e deviata de la pol cu");
        string min = RunReport.F(s.PolarErrorMinArcmin ?? 0, 1);
        return s.PolarErrorMaxArcmin is double max
            ? (RunReport.F(max, 1) == min ? $"{axis} {min}'" : T($"{axis} {min}' to {RunReport.F(max, 1)}'", $"{axis} {min}' pana la {RunReport.F(max, 1)}'"))
            : T($"{axis} at least {min}'", $"{axis} cel putin {min}'");
    }

    /// <summary>
    /// The `polar alignment` row of DRIFT AND ROTATION (R-050, in the words of R-078): how far the
    /// mount's axis is off the pole with what gave each bound, how far that moves the stars in
    /// one exposure and the elongation it makes; the lower bound alone without a solve; or why
    /// nothing is measured.
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
        string driftMin = RunReport.F(s.PolarDriftMinPx ?? 0, 2);
        if (s.PolarErrorMaxArcmin is null)
        {
            return T(
                $"{PolarBounds(s)} (found from the DEC drift, {decDrift}\"/min; {NoUpperBound(s)}); because of this the stars move at least {driftMin} px {exposure}",
                $"{PolarBounds(s)} (aflat din driftul pe DEC, {decDrift}\"/min; {NoUpperBound(s)}); din cauza asta stelele se misca cel putin {driftMin} px {exposure}");
        }

        string perHour = RunReport.F(Math.Abs(s.PolarRotationPerHourDeg ?? 0) * 60, 1), decDeg = Signed(s.DeclinationDeg, 1);
        string elongation = s.PolarElongation is double el
            ? T($", and the elongation it makes is at most {RunReport.F(el, 2)} (1 = round), at a star width (FWHM) of {RunReport.F(s.SessionFwhm ?? 0, 2)} px", $", iar alungirea pe care o produce e cel mult {RunReport.F(el, 2)} (1 = rotund), la o latime a stelelor (FWHM) de {RunReport.F(s.SessionFwhm ?? 0, 2)} px")
            : "";
        string driftMax = RunReport.F(s.PolarDriftMaxPx ?? 0, 2);
        return T(
            $"{PolarBounds(s)} (found from the DEC drift, {decDrift}\"/min, and from the image's rotation, {perHour}'/h without dithering, at DEC {decDeg} degrees); because of this the stars move at most {driftMax} px {exposure} ({driftMin} px for certain){elongation}",
            $"{PolarBounds(s)} (aflat din driftul pe DEC, {decDrift}\"/min, si din rotatia imaginii, {perHour}'/h fara dithering, la DEC {RoCount(decDeg)}grade); din cauza asta stelele se misca cel mult {driftMax} px {exposure} (sigur {driftMin} px){elongation}");
    }

    /// <summary>
    /// The `RA drift` (`drift pe RA`) row (R-050, in the words of R-078): the RA drift in
    /// arcseconds per minute with the stars' sense when the solve gives it, how much of it can
    /// come from the alignment (the rotation times tan DEC) and that the rest comes from the
    /// tracking; `-` when the alignment is not measured.
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
                ? T("without a plate solve it is not known how much of it comes from the alignment", "fara plate solve nu se stie cat din el vine din aliniere")
                : T($"the session is under {RunReport.F(Th.PolarRotationMinHours, 0)} h: it is not known yet how much comes from the alignment", $"sesiunea are sub {RunReport.F(Th.PolarRotationMinHours, 0)} h: nu se stie inca cat vine din aliniere");
            return $"{RunReport.F(ra, 1)}\"/min{sense} ({unknown})";
        }

        string share = T($"at most {RunReport.F(raMax, 1)}\"/min comes from the alignment", $"din aliniere vine cel mult {RunReport.F(raMax, 1)}\"/min");
        return ra > raMax
            ? $"{RunReport.F(ra, 1)}\"/min{sense}; {share}{T(", the rest comes from the tracking (rate, periodic error, flexure)", ", restul vine din urmarire (viteza, eroarea periodica, flexiune)")}"
            : $"{RunReport.F(ra, 1)}\"/min{sense}; {share}{T(": it may all come from the alignment", ": poate veni tot din aliniere")}";
    }

    /// <summary>
    /// The SUMMARY row `polar alignment` (R-050, in the words of R-078): the verdict word (`OK`,
    /// `MARGINAL`, `WARNING`; `OK`, `LA LIMITA`, `ATENTIE` in Romanian; none when unknown), how far
    /// the mount's axis is off the pole and how far that moves the stars in one exposure, or why
    /// nothing is measured.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The verdict word, null when there is none, and the text: the error's bracket and what it
    /// moves the stars by in pixels per exposure, at least the lower drift for WARNING or without
    /// an upper bound, both drifts for MARGINAL, otherwise at most the upper one.
    /// </returns>
    private static (string? Verdict, string Text) PolarSummary(SeriesResult r)
    {
        if (PolarReason(r) is string why)
        {
            return (null, why);
        }

        SeriesAnalysis s = r.Series;
        string min = RunReport.F(s.PolarDriftMinPx ?? 0, 2), max = RunReport.F(s.PolarDriftMaxPx ?? 0, 2);
        string cost = s.PolarAlignment == PolarAlignment.Attention || s.PolarDriftMaxPx is null ? T($"this moves the stars by at least {min} px over one exposure", $"asta muta stelele cu cel putin {min} px intr-o expunere")
            : s.PolarAlignment == PolarAlignment.Marginal ? T($"this moves the stars by {min} to {max} px over one exposure", $"asta muta stelele cu {min} pana la {max} px intr-o expunere")
            : T($"this moves the stars by at most {max} px over one exposure", $"asta muta stelele cu cel mult {max} px intr-o expunere");
        string suffix = s.PolarErrorMaxArcmin is null ? $" ({NoUpperBound(s)})" : "";
        string? verdict = s.PolarAlignment switch
        {
            PolarAlignment.Ok => "OK",
            PolarAlignment.Marginal => T("MARGINAL", "LA LIMITA"),
            PolarAlignment.Attention => T("WARNING", "ATENTIE"),
            _ => null,
        };
        return (verdict, $"{PolarBounds(s)}; {cost}{suffix}");
    }

    /// <summary>
    /// The drift over one exposure and the walking-noise dash it draws inside the longest run at
    /// 10 px (R-043, in the words of R-078).
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The drift, the median exposure in seconds, the axis it runs along and the dash; a `-`
    /// sentence when there is no pair of frames in a row without dithering.
    /// </returns>
    private static string DriftPerExposure(SeriesResult r)
    {
        SeriesAnalysis s = r.Series;
        if (s.DriftPerExposurePx is not double d)
        {
            return T("- (no pair of frames in a row without dithering)", "- (nicio pereche de cadre la rand fara dithering)");
        }

        string seconds = RunReport.F(s.MedianExposureSeconds ?? 0, 0);
        return T($"{Px(d, r)} in {seconds} s, along {DriftAxisWord(s)}; {Streak(r)}", $"{Px(d, r)} in {seconds} s, pe {DriftAxisWord(s)}; {Streak(r)}");
    }

    /// <summary>
    /// The label of the drift-per-exposure row of DRIFT AND ROTATION, which the Dithering tab's
    /// explanation quotes.
    /// </summary>
    private static string DriftPerExposureLabel => T("drift over one exposure", "drift intr-o expunere");

    /// <summary>
    /// `walking-noise streaks of up to 11 px (frames 61-70, 10 frames in a row in the same spot,
    /// within 10 px)`, the dash of R-043 (`dare de walking noise de pana la ...` in Romanian);
    /// without a stretch of two frames there is no dash.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The dash's length in pixels and its stretch; an unknown dash without a drift; no dash
    /// without a stretch.
    /// </returns>
    private static string Streak(SeriesResult r)
    {
        SeriesAnalysis s = r.Series;
        return s.WalkingStreakPx is not double st ? T("the walking-noise streak is not known (the drift could not be measured)", "nu se stie dara de walking noise (driftul nu s-a putut masura)")
            : r.Dither.WalkingStretch is not FrameStretch w ? T($"no walking-noise streak ({NoRun()})", $"nicio dara de walking noise ({NoRun()})")
            : T($"walking-noise streaks of up to {RunReport.F(st, 1)} px ({StretchWord(w)})", $"dare de walking noise de pana la {RunReport.F(st, 1)} px ({StretchWord(w)})");
    }

    /// <summary>
    /// `frames 61-70, 10 frames in a row in the same spot, within 10 px` (`cadrele 61-70, 10 cadre
    /// la rand in acelasi loc, la 10 px`): the stretch of consecutive frames whose span is the
    /// dash.
    /// </summary>
    /// <param name="w">The stretch: its first and last frame index and its frame count.</param>
    /// <returns>The stretch in words, at <see cref="Th.CoverageLoose"/> px.</returns>
    private static string StretchWord(FrameStretch w)
    {
        string range = $"{w.FirstIndex.ToString(Inv)}-{w.LastIndex.ToString(Inv)}", radius = RunReport.F(Th.CoverageLoose, 0);
        return T($"frames {range}, {FramesWord(w.Frames)} in a row in the same spot, within {radius} px", $"cadrele {range}, {FramesWord(w.Frames)} la rand in acelasi loc, la {radius} px");
    }

    /// <summary>
    /// `never 2 frames in a row in the same spot, within 10 px`: the frames that would share the
    /// dash do not exist. No parentheses of its own: the DRIFT row puts it inside its own.
    /// </summary>
    /// <returns>The sentence, at <see cref="Th.CoverageLoose"/> px.</returns>
    private static string NoRun()
        => T($"never 2 frames in a row in the same spot, within {RunReport.F(Th.CoverageLoose, 0)} px", $"niciodata 2 cadre la rand in acelasi loc, la {RunReport.F(Th.CoverageLoose, 0)} px");

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
    /// directions, how close the frames are, the different spots, the runs under
    /// <see cref="Th.CoverageMid"/> and <see cref="Th.CoverageLoose"/> px, the scale on the sky,
    /// the pattern left in the stack, the sensor pattern, and the verdict, in plain words
    /// (R-077).
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The thirteen rows, in the console's order.</returns>
    internal static IReadOnlyList<ReportRow> DitherRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        DitherAnalysis d = r.Dither;
        (string verdict, string text) = DitherVerdict(r);
        string tight = RunReport.F(Th.CoverageTight, 0), mid = RunReport.F(Th.CoverageMid, 0), loose = RunReport.F(Th.CoverageLoose, 0);
        return
        [
            PausesRow(r, d),
            new(HowOftenLabel, HowOften(d)),
            LongestRow(r, d),
            new(HowFarLabel, $"{TypicallyWord} {Px(d.MedianMove, r)}; " + string.Join("; ", d.Sizes.Select(c => $"{SizeWord(c)}: {c.Count.ToString(Inv)}"))),
            new(DirectionLabel, T(
                $"on X only: {d.MovesOnXOnly.ToString(Inv)}; on Y only: {d.MovesOnYOnly.ToString(Inv)}; on both: {d.MovesOnBoth.ToString(Inv)}; on X towards + / towards -: {d.MovesXPlus.ToString(Inv)} / {d.MovesXMinus.ToString(Inv)}; on Y towards + / towards -: {d.MovesYPlus.ToString(Inv)} / {d.MovesYMinus.ToString(Inv)}; {AxisLine(s)}",
                $"doar pe X: {d.MovesOnXOnly.ToString(Inv)}; doar pe Y: {d.MovesOnYOnly.ToString(Inv)}; pe ambele: {d.MovesOnBoth.ToString(Inv)}; pe X spre + / spre -: {d.MovesXPlus.ToString(Inv)} / {d.MovesXMinus.ToString(Inv)}; pe Y spre + / spre -: {d.MovesYPlus.ToString(Inv)} / {d.MovesYMinus.ToString(Inv)}; {AxisLine(s)}")),
            new(CoverageLabel, T(
                $"{NearestWords(d)}; with another frame under {tight} px: {d.WithinTight.ToString(Inv)}, under {mid}: {d.WithinMid.ToString(Inv)}, under {loose}: {d.WithinLoose.ToString(Inv)}; {AloneWords(d)}",
                $"{NearestWords(d)}; cu alt cadru la sub {tight} px: {d.WithinTight.ToString(Inv)}, la sub {mid}: {d.WithinMid.ToString(Inv)}, la sub {loose}: {d.WithinLoose.ToString(Inv)}; {AloneWords(d)}")),
            new(T("different spots", "locuri diferite"), T(
                $"{d.DistinctMid.ToString(Inv)} within {mid} px, {d.DistinctLoose.ToString(Inv)} within {loose} px; area of the positions {AreaWords(d)}",
                $"{d.DistinctMid.ToString(Inv)} la {mid} px, {d.DistinctLoose.ToString(Inv)} la {loose} px; zona pozitiilor {AreaWords(d)}")),
            new(T($"runs under {mid} px", $"grupuri sub {mid} px"), Runs(d.RunsMid, r)),
            new(T($"runs under {loose} px", $"grupuri sub {loose} px"), Runs(d.RunsLoose, r)),
            ScaleRow(r),
            SurvivingRow(r),
            PatternRow(r),
            new("verdict", text, verdict),
        ];
    }

    /// <summary>
    /// The DITHERING rows that are not tables, for the window's Dithering tab beside
    /// <see cref="DitherTables"/> (R-036), in the words of <see cref="DitherRows"/>.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The nine rows, how close the frames are cut to the nearest frame and the frames alone.
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
            new(CoverageLabel, $"{NearestWords(d)}; {AloneWords(d)}"),
            new(T("area of the positions", "zona pozitiilor"), AreaWords(d)),
            new(T("axes", "axe"), AxisLine(r.Series)),
            ScaleRow(r),
            SurvivingRow(r),
            PatternRow(r),
            new("verdict", text, verdict),
        ];
    }

    /// <summary>
    /// What a move means on the sky at this focal length: `1 px = 5.99" on the sky; to move the
    /// frame 5 px (the smallest move counted) takes 30" on the sky, and 10 px, 60"` (R-044).
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
            $"1 px = {px}\" on the sky; to move the frame {step} px (the smallest move counted) takes {stepSky}\" on the sky, and {loose} px, {looseSky}\"",
            $"1 px = {px}\" pe cer; ca sa muti cadrul {step} px (cea mai mica miscare numarata) trebuie {stepSky}\" pe cer, iar pentru {loose} px, {looseSky}\""));
    }

    /// <summary>
    /// How much of one frame's pattern the mean of the stack keeps (R-048): `1.6 % of one frame's
    /// pattern stays in the stack (ideally 1.1 %, with 87 frames; 16 pairs of frames sat under
    /// 1.5 px from each other, and their pattern adds up)`.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>The `pattern in the stack` (`pattern in stiva`) row.</returns>
    private static ReportRow SurvivingRow(SeriesResult r)
    {
        DitherAnalysis d = r.Dither;
        if (d.PatternSurviving is not double f)
        {
            return new(SurvivingLabel, T("- (the frames could not be aligned)", "- (cadrele nu s-au putut alinia)"));
        }

        string kept = RunReport.F(100 * f, 1), ideal = RunReport.F(100.0 / d.Registered, 1), frames = FramesWord(d.Registered);
        return new(SurvivingLabel, T(
            $"{kept} % of one frame's pattern stays in the stack (ideally {ideal} %, with {frames}; {SharingPairsWords(d.SharingPairs)})",
            $"in stiva ramane {kept} % din pattern-ul unui cadru (ideal {ideal} %, cu {frames}; {SharingPairsWords(d.SharingPairs)})"));
    }

    /// <summary>
    /// The pairs of frames that share the sensor's pattern pixel for pixel, in words that fit
    /// their count (R-048).
    /// </summary>
    /// <param name="pairs">The pairs of registered frames closer than <see cref="Th.PatternSharePx"/>.</param>
    /// <returns>
    /// `4 pairs of frames sat under 1.5 px from each other, and their pattern adds up`; the
    /// singular for one pair; `no two frames sat under 1.5 px from each other` for none.
    /// </returns>
    private static string SharingPairsWords(int pairs)
    {
        string share = RunReport.F(Th.PatternSharePx, 1), n = pairs.ToString(Inv);
        return pairs switch
        {
            0 => T($"no two frames sat under {share} px from each other", $"nicio pereche de cadre n-a stat la sub {share} px una de alta"),
            1 => T($"1 pair of frames sat under {share} px from each other, and its pattern adds up", $"1 pereche de cadre a stat la sub {share} px una de alta, iar pattern-ul ei se aduna"),
            _ => T($"{n} pairs of frames sat under {share} px from each other, and their pattern adds up", $"{RoCount(n)}perechi de cadre au stat la sub {share} px una de alta, iar pattern-ul lor se aduna"),
        };
    }

    /// <summary>
    /// The sensor's pattern read from the lights, and whether the session's darks will take it
    /// out of the stack (R-046, R-042): `strength 1.4 ADU on R (12 % of one frame's noise), 2.1
    /// on G1 (10 %), 2.1 on G2 (10 %), 2.3 on B (10 %); 4 hot pixels per million pixels, that is,
    /// those above 10 times one frame's noise (from 87 frames, on 512 x 512 px from the centre);
    /// no darks in the session, the pattern stays in the frames`.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The `fixed pattern` row: the figures when the pattern was measured, else why it cannot be,
    /// and in both cases what the session's darks do.
    /// </returns>
    private static ReportRow PatternRow(SeriesResult r)
    {
        SensorPattern p = r.Pattern;
        string darks = r.Session.DarksDir is null
            ? T("no darks in the session, the pattern stays in the frames", "fara dark-uri in sesiune, pattern-ul ramane in cadre")
            : T($"{r.Session.Darks.Count.ToString(Inv)} darks in {SessionFolder.DarksName}\\, with which Siril takes the pattern out", $"{RoCount(r.Session.Darks.Count)}dark-uri in {SessionFolder.DarksName}\\, cu care Siril scoate pattern-ul");
        string value = p.Status switch
        {
            PatternStatus.Measured => MeasuredPattern(p),
            PatternStatus.TooFewFrames => T($"cannot be measured (fewer than {Th.PatternMinFrames.ToString(Inv)} frames measured)", $"nu se poate masura (mai putin de {RoCount(Th.PatternMinFrames)}cadre masurate)"),
            PatternStatus.Unregistered => T("cannot be measured (the frames could not be aligned)", "nu se poate masura (cadrele nu s-au putut alinia)"),
            PatternStatus.FramesDidNotMove => T(
                $"cannot be measured (the frames stayed in place too much and the stars cannot be told apart from the pattern; the largest group in the same spot (within {RunReport.F(Th.CoverageTight, 0)} px) has {FramesWord(r.Dither.LargestClusterTight)} of {r.Dither.Registered.ToString(Inv)})",
                $"nu se poate masura (cadrele au stat prea mult pe loc si stelele nu se pot desparti de pattern; cel mai mare grup in acelasi loc (la {RunReport.F(Th.CoverageTight, 0)} px) are {FramesWord(r.Dither.LargestClusterTight)} din {r.Dither.Registered.ToString(Inv)})"),
            _ => T("cannot be measured (no frame read)", "nu se poate masura (niciun cadru citit)"),
        };
        return new(PatternLabel, $"{value}; {darks}");
    }

    /// <summary>
    /// The figures of a measured sensor pattern: its strength per CFA position with its share of
    /// one frame's noise, the hot pixels per million pixels and where it was read (R-046).
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
            $"strength {string.Join(", ", sigmas)}; {hot} hot pixels per million pixels, that is, those above {hotSigma} times one frame's noise (from {FramesWord(p.Frames)}, on {crop} x {crop} px from the centre)",
            $"tarie {string.Join(", ", sigmas)}; {RoCount(hot)}pixeli calzi la un milion de pixeli, adica cei de peste {RoCount(hotSigma)}ori zgomotul unui cadru (din {FramesWord(p.Frames)}, pe {crop} x {crop} px din centru)");
    }

    /// <summary>
    /// What the Dithering tab says under its card, in plain words (R-045, R-077): what walking
    /// noise is, why the focal length decides how many pixels the same drift and the same dither
    /// make (the figures of paragraph 2 computed for the D3300's 3.92 um pixel, ADR 0032), what
    /// stops it (bold), how to take darks without touching the lens, and where the figures are.
    /// Built from Th so the thresholds it quotes follow a change there, and when read, in the
    /// language of that moment.
    /// </summary>
    internal static IReadOnlyList<ReportParagraph> DitherExplanation => IsRomanian
    ?
    [
        new($"Walking noise-ul inseamna dungi fine, toate in aceeasi directie, in imaginea stivuita. Le face pattern-ul fix al senzorului: pixelii calzi si urmele slabe pe care senzorul le are mereu in acelasi loc, adica ce scoate un dark. Montura aluneca putin de la un cadru la altul (driftul). Cand stiva aliniaza cadrele dupa stele, pattern-ul se muta cu fiecare cadru, iar copiile lui se insira intr-o dunga. Asta se intampla in grupurile de cadre la rand care au stat in acelasi loc (la mai putin de {RunReport.F(Th.CoverageLoose, 0)} px).", false),
        new("Cu cat focala e mai lunga, cu atat aceeasi alunecare a monturii si aceeasi miscare de dithering inseamna mai multi pixeli. Un drift de 3\"/min muta cadrul cam 1 px intr-o expunere de 2 minute la 135 mm, dar numai 0.15 px la 20 mm. De aceea focala scurta iarta, iar focala lunga cere dithering dupa fiecare cadru.", false),
        new($"Ce opreste walking noise-ul: muti montura dupa fiecare cadru, pe ambele axe, de fiecare data in alta directie, la intamplare. Conteaza mai mult cat de des muti decat cat de mult: {RunReport.F(Th.DitherStep, 0)} px ajung, cat timp doua cadre vecine nu stau in acelasi loc. Un salt mare la fiecare 2 cadre lasa perechi de cadre in acelasi loc, iar in ele driftul trage dungi scurte.", true),
        new("Dark-urile scot pattern-ul inainte sa apuce sa faca dungi. Nu trebuie sa scoti filtrul de pe obiectiv: dai jos obiectivul cu filtrul pe el, pui capacul de body, acoperi ocularul vizorului si faci 20 de cadre cu acelasi ISO, acelasi timp de expunere si la aceeasi temperatura, la sfarsitul sesiunii. Flat-urile raman bune.", false),
        new($"Unde gasesti cifrele: randul '{DriftPerExposureLabel}' din DRIFT SI ROTATIE (tabul Rezumat) spune cat aluneca un cadru intr-o expunere si cat de lunga e cea mai lunga dunga de walking noise. Sub {RunReport.F(Th.WalkingStreakVisible, 0)} px dunga nu se vede, oricum ar arata harta. Randul '{SurvivingLabel}' spune cat din pattern-ul unui cadru ramane dupa stivuire; ideal e 1 impartit la numarul de cadre, cand niciun cadru nu sta peste altul. Randul '{PatternLabel}' spune cat de tare e pattern-ul senzorului fata de zgomotul unui cadru si daca ai dark-uri care sa-l scoata.", false),
    ]
    :
    [
        new($"Walking noise means fine streaks, all in the same direction, in the stacked image. The sensor's fixed pattern makes them: the hot pixels and the faint marks the sensor always has in the same place, that is, what a dark takes out. The mount slides a little from one frame to the next (the drift). When the stack aligns the frames on the stars, the pattern moves with every frame, and its copies line up into a streak. This happens in the runs of frames in a row that stayed in the same spot (less than {RunReport.F(Th.CoverageLoose, 0)} px).", false),
        new("The longer the focal length, the more pixels the same slide of the mount and the same dither move make. A drift of 3\"/min moves the frame about 1 px in a 2-minute exposure at 135 mm, but only 0.15 px at 20 mm. That is why a short focal length forgives, and a long one asks for a dither after every frame.", false),
        new($"What stops walking noise: move the mount after every frame, on both axes, each time in another direction, at random. How often you move matters more than how far: {RunReport.F(Th.DitherStep, 0)} px are enough, as long as two neighbouring frames do not stay in the same spot. A big jump every 2 frames leaves pairs of frames in the same spot, and in them the drift draws short streaks.", true),
        new("Darks take the pattern out before it gets to make streaks. You do not need to take the filter off the lens: take the lens off with the filter on it, put the body cap on, cover the viewfinder's eyepiece and shoot 20 frames at the same ISO, with the same exposure time and at the same temperature, at the end of the session. The flats stay good.", false),
        new($"Where to find the figures: the '{DriftPerExposureLabel}' row of DRIFT AND ROTATION (the Summary tab) says how far a frame slides in one exposure and how long the longest walking-noise streak is. Under {RunReport.F(Th.WalkingStreakVisible, 0)} px the streak does not show, whatever the map looks like. The '{SurvivingLabel}' row says how much of one frame's pattern stays after stacking; the ideal is 1 divided by the number of frames, when no frame sits on another. The '{PatternLabel}' row says how strong the sensor's pattern is against one frame's noise and whether you have darks to take it out.", false),
    ];

    /// <summary>The label of the DITHERING row and table of how often the mount moved.</summary>
    private static string HowOftenLabel => T("how often", "cat de des");

    /// <summary>The label of the DITHERING row and table of how far the mount moved.</summary>
    private static string HowFarLabel => T("how far", "cat de mult");

    /// <summary>The label of the DITHERING row and table of the moves' directions.</summary>
    private static string DirectionLabel => T("direction", "directie");

    /// <summary>The label of the DITHERING row and table of how close the frames are to each other.</summary>
    private static string CoverageLabel => T("how close the frames are", "apropierea cadrelor");

    /// <summary>The word before the median move, in the how-far row and as the table's first row.</summary>
    private static string TypicallyWord => T("typically", "de obicei");

    /// <summary>
    /// How close half the frames have another frame: the median distance to the nearest other
    /// frame, `half the frames have another frame within 0.6 px`.
    /// </summary>
    /// <param name="d">The dither analysis of the series.</param>
    /// <returns>The words with the median, `-` for it without a registered frame.</returns>
    private static string NearestWords(DitherAnalysis d)
    {
        string near = Opt(Median(d.NearestOther), 1);
        return T($"half the frames have another frame within {near} px", $"jumatate din cadre au alt cadru la cel mult {near} px");
    }

    /// <summary>
    /// The frames with no other frame within <see cref="Th.CoverageLoose"/> px: `frames alone
    /// (10 px or more from any other): 0`.
    /// </summary>
    /// <param name="d">The dither analysis of the series.</param>
    /// <returns>The words with the count.</returns>
    private static string AloneWords(DitherAnalysis d)
    {
        string loose = RunReport.F(Th.CoverageLoose, 0), alone = d.IsolatedLoose.ToString(Inv);
        return T($"frames alone ({loose} px or more from any other): {alone}", $"cadre singure (la {loose} px sau mai mult de oricare altul): {alone}");
    }

    /// <summary>The area the positions spread over: `40 px on X, 16 px on Y`.</summary>
    /// <param name="d">The dither analysis of the series.</param>
    /// <returns>The span on each image axis, in whole pixels.</returns>
    private static string AreaWords(DitherAnalysis d)
    {
        string x = RunReport.F(d.SpanX, 0), y = RunReport.F(d.SpanY, 0);
        return T($"{x} px on X, {y} px on Y", $"{x} px pe X, {y} px pe Y");
    }

    /// <summary>The label of the DITHERING row of the pattern left in the stack (R-048).</summary>
    private static string SurvivingLabel => T("pattern in the stack", "pattern in stiva");

    /// <summary>The label of the DITHERING row of the sensor's pattern (R-046).</summary>
    private static string PatternLabel => T("fixed pattern", "pattern fix");

    /// <summary>
    /// The DITHERING figures that are tables, for the window (R-036), in plain words (R-077): how
    /// often, how far, direction, how close the frames are, runs; the console keeps its rows
    /// (ADR 0012).
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The five tables in that order; how often has a single row with the console's words when
    /// no interval between moves was counted, the runs table one row per run and distance.
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

        List<IReadOnlyList<string>> sizes = [[TypicallyWord, Px(d.MedianMove, r)]];
        sizes.AddRange(d.Sizes.Select(c => (IReadOnlyList<string>)[SizeWord(c), c.Count.ToString(Inv)]));
        List<IReadOnlyList<string>> runs = [.. RunRows(d.RunsMid, Th.CoverageMid, r), .. RunRows(d.RunsLoose, Th.CoverageLoose, r)];
        return
        [
            new(HowOftenLabel, [T("a move every", "o miscare la fiecare"), T("times", "de cate ori")], often),
            new(HowFarLabel, [T("how big", "cat de mare"), T("how many moves", "cate miscari")], sizes),
            new(DirectionLabel, [T("axis", "axa"), T("on it only", "doar pe ea"), T("towards +", "spre +"), T("towards -", "spre -")],
            [
                [xAxis, d.MovesOnXOnly.ToString(Inv), d.MovesXPlus.ToString(Inv), d.MovesXMinus.ToString(Inv)],
                [yAxis, d.MovesOnYOnly.ToString(Inv), d.MovesYPlus.ToString(Inv), d.MovesYMinus.ToString(Inv)],
                [T("on both", "pe ambele"), d.MovesOnBoth.ToString(Inv), "", ""],
            ]),
            new(CoverageLabel, [T("distance", "distanta"), T("with another frame closer", "cu alt cadru mai aproape"), T("different spots", "locuri diferite"), T($"runs of {Th.RunMinFrames.ToString(Inv)}+ frames in a row", $"grupuri de {Th.RunMinFrames.ToString(Inv)}+ cadre la rand")],
            [
                [PxWord(Th.CoverageTight), d.WithinTight.ToString(Inv), "-", "-"],
                [PxWord(Th.CoverageMid), d.WithinMid.ToString(Inv), d.DistinctMid.ToString(Inv), d.RunsMid.Count.ToString(Inv)],
                [PxWord(Th.CoverageLoose), d.WithinLoose.ToString(Inv), d.DistinctLoose.ToString(Inv), d.RunsLoose.Count.ToString(Inv)],
            ]),
            new(T("runs in the same spot", "grupuri in acelasi loc"), [T("distance", "distanta"), T("frames", "cadrele"), T("how many", "cate"), T("how long", "cat timp")], runs),
        ];
    }

    /// <summary>
    /// The first DITHERING row: the pauses longer than <see cref="Th.DitherPause"/>, the moves of
    /// at least <see cref="Th.DitherStep"/>, the pauses after which the frame stayed put, the
    /// moves without a pause before them, and how many frames were aligned.
    /// </summary>
    /// <param name="r">The result of the series run, for its frame count.</param>
    /// <param name="d">The dither analysis of the series.</param>
    /// <returns>The row, labelled with the pause threshold.</returns>
    private static ReportRow PausesRow(SeriesResult r, DitherAnalysis d)
        => new(T($"pauses over {RunReport.F(Th.DitherPause, 0)} s", $"pauze de peste {RunReport.F(Th.DitherPause, 0)} s"), T(
            $"{d.Pauses.ToString(Inv)}; moves of at least {RunReport.F(Th.DitherStep, 0)} px: {d.Moves.ToString(Inv)}; pauses after which the frame stayed put: {d.PausesWithoutMove.ToString(Inv)}; moves without a pause before: {d.MovesWithoutPause.ToString(Inv)}; frames aligned: {d.Registered.ToString(Inv)} of {r.Frames.Count.ToString(Inv)}",
            $"{d.Pauses.ToString(Inv)}; miscari de cel putin {RunReport.F(Th.DitherStep, 0)} px: {d.Moves.ToString(Inv)}; pauze dupa care cadrul a ramas pe loc: {d.PausesWithoutMove.ToString(Inv)}; miscari fara pauza inainte: {d.MovesWithoutPause.ToString(Inv)}; cadre aliniate: {d.Registered.ToString(Inv)} din {r.Frames.Count.ToString(Inv)}"));

    /// <summary>
    /// The row of the longest stretch between two moves: its first and last frame, its frame
    /// count, and its duration when the frames carry a time.
    /// </summary>
    /// <param name="r">The result of the series run, for the times of the frames.</param>
    /// <param name="d">The dither analysis of the series.</param>
    /// <returns>The `longest stretch without a move` row.</returns>
    private static ReportRow LongestRow(SeriesResult r, DitherAnalysis d)
    {
        string range = $"{d.LongestStretchFirst.ToString(Inv)}-{d.LongestStretchLast.ToString(Inv)}", frames = FramesWord(d.LongestStretchFrames);
        string minutes = Minutes(r, d.LongestStretchFirst, d.LongestStretchLast);
        return new(T("longest stretch without a move", "cel mai lung sir fara miscare"), T(
            $"frames {range} ({frames} in a row{minutes})",
            $"cadrele {range} ({frames} la rand{minutes})"));
    }

    /// <summary>A frame count with its noun in the right number.</summary>
    /// <param name="frames">The count.</param>
    /// <returns>
    /// "1 frame" ("1 cadru"), or "N frames" ("N cadre", "20 de cadre" from 20 up as
    /// <see cref="Ui.RoCount(long)"/> writes it) for any other count.
    /// </returns>
    private static string FramesWord(int frames) => T($"{frames.ToString(Inv)} {(frames == 1 ? "frame" : "frames")}", RoCount(frames) + (frames == 1 ? "cadru" : "cadre"));

    /// <summary>A number of times with its noun in the right number.</summary>
    /// <param name="times">The count.</param>
    /// <returns>
    /// "1 time" ("1 data"), or "N times" ("N ori", "22 de ori" from 20 up as
    /// <see cref="Ui.RoCount(long)"/> writes it) for any other count.
    /// </returns>
    private static string TimesWord(int times) => T($"{times.ToString(Inv)} {(times == 1 ? "time" : "times")}", RoCount(times) + (times == 1 ? "data" : "ori"));

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

    /// <summary>
    /// The words of a flag, plain (R-079): what was found, its figure and the value from which it
    /// counts, said as what it means for the frame.
    /// </summary>
    /// <param name="f">The flag; not null.</param>
    /// <returns>The flag in words; the kind's name for a kind without words.</returns>
    internal static string Reason(FrameFlag f)
    {
        ArgumentNullException.ThrowIfNull(f);
        return f.Kind switch
        {
            FlagKind.Trailed => T($"elongation {RunReport.F(f.Value, 2)} (from {RunReport.F(f.Threshold, 2)} the frame is bad){TrailSuffix(f)}", $"alungire {RunReport.F(f.Value, 2)} (de la {RunReport.F(f.Threshold, 2)} cadrul e slab){TrailSuffix(f)}"),
            FlagKind.MarginalTrail => T($"elongation {RunReport.F(f.Value, 2)} (from {RunReport.F(f.Threshold, 2)} the frame is marginal){TrailSuffix(f)}", $"alungire {RunReport.F(f.Value, 2)} (de la {RunReport.F(f.Threshold, 2)} cadrul e la limita){TrailSuffix(f)}"),
            FlagKind.Soft => T($"wide stars: width (FWHM) {RunReport.F(f.Value, 2)} px, over {RunReport.F(f.Threshold, 2)}, that is {RunReport.F(Th.SoftFwhmFactor, 1)} times the session's usual width", $"stele late: latimea (FWHM) {RunReport.F(f.Value, 2)} px, peste {RunReport.F(f.Threshold, 2)}, adica de {RunReport.F(Th.SoftFwhmFactor, 1)} ori latimea obisnuita din sesiune"),
            FlagKind.Clouds => T($"too few stars: {RunReport.F(f.Value, 0)}, under {RunReport.F(f.Threshold, 0)} ({RunReport.F(100 * Th.CloudStarFraction, 0)} % of a frame's usual count): clouds or haze", $"prea putine stele: {RunReport.F(f.Value, 0)}, sub {RunReport.F(f.Threshold, 0)} ({RunReport.F(100 * Th.CloudStarFraction, 0)} % din cate are de obicei un cadru): nori sau ceata"),
            FlagKind.BackgroundJump => T($"the sky background jumps by {Signed(f.Value, 0)} % against the neighbouring frames (flagged from {RunReport.F(f.Threshold, 0)} %)", $"fondul cerului sare cu {Signed(f.Value, 0)} % fata de cadrele vecine (se semnaleaza de la {RunReport.F(f.Threshold, 0)} %)") + ExposureSuffix(f),
            FlagKind.Trails => Count((int)f.Value, ("satellite or plane trail (Siril takes it out when stacking)", "dara de satelit sau avion (Siril o scoate la stivuire)"), ("satellite or plane trails (Siril takes them out when stacking)", "dare de satelit sau avion (Siril le scoate la stivuire)")),
            FlagKind.Unreadable => T("could not be read", "nu s-a putut citi") + (f.Detail is null ? "" : ": " + f.Detail),
            FlagKind.Unregistered => T("could not be aligned (too few stars in common with the reference frame)", "nu s-a putut alinia (prea putine stele comune cu cadrul de referinta)"),
            FlagKind.ShortExposure => T(
                $"exposure {RunReport.F(f.Value, 1)} s, under {ShortFractionWord} the usual {RunReport.F(f.Threshold / Th.ExposureShortFraction, 1)} s: too short to be worth stacking",
                $"expunere {RunReport.F(f.Value, 1)} s, sub {ShortFractionWord} din cea obisnuita, {RunReport.F(f.Threshold / Th.ExposureShortFraction, 1)} s: prea scurta ca sa merite stivuita"),
            FlagKind.OtherExposure => T(
                $"exposure {RunReport.F(f.Value, 1)} s instead of the usual {RunReport.F(f.Threshold, 1)} s (Siril evens it out when stacking)",
                $"expunere {RunReport.F(f.Value, 1)} s in loc de cea obisnuita, {RunReport.F(f.Threshold, 1)} s (Siril o egalizeaza la stivuire)"),
            _ => f.Kind.ToString(),
        };
    }

    /// <summary>
    /// The fraction of the usual exposure under which a frame is too short, in words: `half`
    /// (`jumatate`) for Th.ExposureShortFraction at 0.5, else the percentage, `30 % of` in
    /// English (the Romanian sentence carries its own `din`) (R-085).
    /// </summary>
    private static string ShortFractionWord => Th.ExposureShortFraction == 0.5 ? T("half", "jumatate") : T(RunReport.F(100 * Th.ExposureShortFraction, 0) + " % of", RunReport.F(100 * Th.ExposureShortFraction, 0) + " %");

    /// <summary>
    /// `, which its exposure explains` (`, din cauza expunerii`) after a background jump that the
    /// frame's exposure predicts, empty otherwise (R-085).
    /// </summary>
    /// <param name="f">The background-jump flag.</param>
    /// <returns>The tail, or an empty string.</returns>
    private static string ExposureSuffix(FrameFlag f) => f.Detail == FrameFlag.ExposureDetail ? T(", which its exposure explains", ", din cauza expunerii") : "";

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
            : f.On == TrailAxis.Unknown ? T($", trail at {f.Detail} degrees", $", dara la {RoCount(f.Detail)}grade")
            : T($", trail {AxisWord(f.On)} ({f.Detail} degrees)", $", dara {AxisWord(f.On)} ({RoCount(f.Detail)}grade)");

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
    /// The focus verdict and its text, in plain words (R-078): the stars' width on the good frames
    /// and whether the focus held.
    /// </summary>
    /// <param name="s">The series analysis.</param>
    /// <param name="r">The result of the series run, for the scale.</param>
    /// <returns>
    /// UNDETERMINED without a good frame, WARNING when the focus changed, GOOD otherwise (also when
    /// too few good frames show a trend; NEDETERMINAT, ATENTIE, BUN in Romanian); the text gives
    /// the stars' width and its start and end.
    /// </returns>
    private static (string Verdict, string Text) Focus(SeriesAnalysis s, SeriesResult r)
    {
        if (s.SessionFwhm is null)
        {
            return (T("UNDETERMINED", "NEDETERMINAT"), T("no good frame", "niciun cadru bun"));
        }

        string held = s.FocusHeld is bool h
            ? (h ? T("the focus held the whole session", "focusul a tinut toata sesiunea") : T("the focus changed", "focusul s-a schimbat"))
            : T("too few good frames to see whether it changed", "prea putine cadre bune ca sa se vada daca s-a schimbat");
        string first = Opt(s.Fwhm.First, 2), last = Opt(s.Fwhm.Last, 2);
        return (s.FocusHeld is false ? T("WARNING", "ATENTIE") : T("GOOD", "BUN"), T(
            $"the stars' width (FWHM) is typically {Px(s.SessionFwhm, r)} on the good frames; {first} px at the start and {last} px at the end{ChangeWord(s.Fwhm)}: {held}",
            $"latimea stelelor (FWHM) e de obicei {Px(s.SessionFwhm, r)} pe cadrele bune; {first} px la inceput si {last} px la sfarsit{ChangeWord(s.Fwhm)}: {held}"));
    }

    /// <summary>
    /// The exposure verdict and its text, in plain words (R-078), from the channel whose
    /// shot-to-read ratio is the lowest of all the sampled frames.
    /// </summary>
    /// <param name="s">The series analysis.</param>
    /// <param name="r">The result of the series run (not read).</param>
    /// <returns>
    /// GOOD (BUNA) when that ratio reaches <see cref="Th.SwampingRatioOk"/>, MARGINAL (LA LIMITA)
    /// under it, NOT MEASURED (NEMASURAT) without biases or without a finite ratio; the text opens
    /// with the sky on green of the first and last good frames in ADU.
    /// </returns>
    private static (string Verdict, string Text) Exposure(SeriesAnalysis s, SeriesResult r)
    {
        string bg = s.Background.First is double bf && s.Background.Last is double bl
            ? T($"the sky on green: {RunReport.F(bf, 0)} -> {RunReport.F(bl, 0)} ADU", $"cerul pe verde: {RunReport.F(bf, 0)} -> {RunReport.F(bl, 0)} ADU")
            : T("the sky could not be measured", "cerul nu s-a putut masura");
        if (s.Swamping is null || s.Swamping.Count == 0)
        {
            return (T("NOT MEASURED", "NEMASURAT"), bg + T("; the read noise could not be measured (the biases are missing)", "; zgomotul de citire nu s-a putut masura (lipsesc bias-urile)"));
        }

        ChannelNoise? worst = s.Swamping.SelectMany(x => x).Where(c => !double.IsNaN(c.SwampingRatio) && !double.IsInfinity(c.SwampingRatio)).MinBy(c => c.SwampingRatio);
        if (worst is null)
        {
            return (T("NOT MEASURED", "NEMASURAT"), bg + T("; the shot/read ratio could not be measured (the measured area is too small)", "; raportul shot/citire nu s-a putut masura (zona masurata e prea mica)"));
        }

        string ratio = RunReport.F(worst.SwampingRatio, 1), extra = RunReport.F(worst.PenaltyPct, 0), ok = RunReport.F(Th.SwampingRatioOk, 0);
        return (worst.SwampingRatio >= Th.SwampingRatioOk ? T("GOOD", "BUNA") : T("MARGINAL", "LA LIMITA"), T(
            $"{bg}; the weakest is channel {worst.Name}: shot/read {ratio} (good from {ok}), the read noise adds {extra} %",
            $"{bg}; cel mai slab e canalul {worst.Name}: shot/citire {ratio} (bine e de la {ok}), zgomotul de citire adauga {extra} %"));
    }

    /// <summary>
    /// The dew verdict and its text, in plain words (R-078): the change of the stars' width in
    /// percent and of the light around them (the halo).
    /// </summary>
    /// <param name="s">The series analysis.</param>
    /// <returns>
    /// POSSIBLE (POSIBIL) when dew is suspected, NO (NU) when not, UNDETERMINED (NEDETERMINAT)
    /// with too few good frames.
    /// </returns>
    private static (string Verdict, string Text) Dew(SeriesAnalysis s) => s.DewSuspected switch
    {
        true => (T("POSSIBLE", "POSIBIL"), T(
            $"the stars' width rose by {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} %, and the light around them (halo) by {Signed(s.Halo.Change ?? 0, 3)}",
            $"latimea stelelor a crescut cu {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} %, iar lumina din jurul lor (halo) cu {Signed(s.Halo.Change ?? 0, 3)}")),
        false => (T("NO", "NU"), T(
            $"the stars' width {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} %, the light around the stars (halo) {Signed(s.Halo.Change ?? 0, 3)}",
            $"latimea stelelor {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} %, lumina din jurul stelelor (halo) {Signed(s.Halo.Change ?? 0, 3)}")),
        null => (T("UNDETERMINED", "NEDETERMINAT"), T("too few good frames", "prea putine cadre bune")),
    };

    /// <summary>Which image axis is RA and which DEC, and where that came from (R-077).</summary>
    /// <param name="s">The series analysis, with the RA axis and its source.</param>
    /// <returns>
    /// `RA is the image's Y axis, DEC the X axis (found from plate solve (WCS))`, or `it is not
    /// known which axis is RA` when the RA axis is unknown.
    /// </returns>
    private static string AxisLine(SeriesAnalysis s) => s.RaAxis switch
    {
        Axis.X => T($"RA is the image's X axis, DEC the Y axis (found from {s.AxisSource})", $"RA e axa X a imaginii, DEC axa Y (aflat din {s.AxisSource})"),
        Axis.Y => T($"RA is the image's Y axis, DEC the X axis (found from {s.AxisSource})", $"RA e axa Y a imaginii, DEC axa X (aflat din {s.AxisSource})"),
        _ => T("it is not known which axis is RA", "nu se stie care axa e RA"),
    };

    /// <summary>
    /// The two dithering verdicts in one word pair, the worse first so the chip takes its
    /// colour (R-021, R-043, R-084): walking noise `INSUFFICIENT` (`INSUFICIENT`) when the frames
    /// whose streak would show reach Th.WalkingSharedFraction, `NO` (`FARA`) when the pattern
    /// piles in place instead, `OK` otherwise; hot pixels `OK` or `WARNING` (`ATENTIE`). The
    /// words stay short for the chip; the dash with the frames that share it, the drift, the
    /// run, the pattern kept and the hot-pixel rule open the text, in plain words (R-077).
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <returns>
    /// The two words joined by ", " and the text, the counts of the moves and spots last;
    /// UNDETERMINED (NEDETERMINAT) when fewer than two frames were aligned.
    /// </returns>
    private static (string Verdict, string Text) DitherVerdict(SeriesResult r)
    {
        DitherAnalysis d = r.Dither;
        SeriesAnalysis s = r.Series;
        if (d.Registered < 2)
        {
            return (T("UNDETERMINED", "NEDETERMINAT"), T("the frames could not be aligned", "cadrele nu s-au putut alinia"));
        }

        (int Severity, string Word, string? Detail) hot = d.HotPixelsOk
            ? (0, T("OK for hot pixels", "OK pentru pixeli calzi"), null)
            : (2, T("WARNING on hot pixels", "ATENTIE la pixeli calzi"), T(
                $"hot pixels: more than {Th.HotPixelRunMax.ToString(Inv)} frames in a row in the same spot (within {RunReport.F(Th.CoverageTight, 0)} px)",
                $"pixeli calzi: mai mult de {RoCount(Th.HotPixelRunMax)}cadre la rand in acelasi loc (la {RunReport.F(Th.CoverageTight, 0)} px)"));
        string run = d.WalkingStretch is FrameStretch w ? StretchWord(w) : NoRun();
        string drift = s.DriftPerExposurePx is double dpe
            ? T($"the drift is {RunReport.F(dpe, 2)} px over one exposure; {run}", $"driftul e de {RunReport.F(dpe, 2)} px intr-o expunere; {run}")
            : T($"the drift could not be measured; {run}", $"driftul nu s-a putut masura; {run}");
        string detail = string.Join("; ", new[] { StreakClause(r, drift), PatternClause(r) }.Where(x => x is not null));
        (int Severity, string Word, string? Detail) walk = s.WalkingNoise switch
        {
            WalkingNoise.Ok => (0, T("OK for walking noise", "OK pentru walking noise"), detail),
            WalkingNoise.Static => (0, T("NO walking noise", "FARA walking noise"), detail),
            _ => (3, T("INSUFFICIENT for walking noise", "INSUFICIENT pentru walking noise"), detail),
        };
        (var first, var second) = walk.Severity >= hot.Severity ? (walk, hot) : (hot, walk);
        string loose = RunReport.F(Th.CoverageLoose, 0), frames = d.Registered.ToString(Inv);
        string moves = Count(d.Moves, ("move", "miscare"), ("moves", "miscari"));
        string spots = Count(d.DistinctLoose, ("spot", "loc"), ("different spots", "locuri diferite"));
        string alone = Count(d.IsolatedLoose, ("frame alone", "cadru singur"), ("frames alone", "cadre singure"));
        string both = Count(d.MovesOnBoth, ("move on both axes", "miscare pe ambele axe"), ("moves on both axes", "miscari pe ambele axe"));
        string counts = T(
            $"{moves} over {frames} frames, {spots} (within {loose} px), {alone}, {both}",
            $"{moves} la {RoCount(frames)}cadre, {spots} (la {loose} px), {alone}, {both}");
        return ($"{first.Word}, {second.Word}", string.Join("; ", new[] { first.Detail, second.Detail, counts }.Where(x => x is not null)));
    }

    /// <summary>
    /// How the verdict's text opens (R-084): the streaks with the frames that share one when
    /// a stretch's dash shows, `walking-noise streaks of up to 5.9 px along Y, in 7 frames of 85
    /// (8.2 %; from 25 % they show in the stack): the drift ...`; the longest dash with the size
    /// under which it does not show when none does, `walking-noise streaks of at most 1.9 px
    /// (under 3 px they do not show): ...`; `no walking-noise streak: ...` without a stretch;
    /// `walking noise: the drift could not be measured; ...` without a drift.
    /// </summary>
    /// <param name="r">The result of the series run; not null.</param>
    /// <param name="drift">The drift and the stretch in words, which every form ends with.</param>
    /// <returns>The opening clause, in the language of the moment.</returns>
    private static string StreakClause(SeriesResult r, string drift)
    {
        SeriesAnalysis s = r.Series;
        if (s.WalkingStreakPx is not double st)
        {
            return T($"walking noise: {drift}", $"walking noise: {drift}");
        }

        if (r.Dither.WalkingStretch is null)
        {
            return T($"no walking-noise streak: {drift}", $"nicio dara de walking noise: {drift}");
        }

        string px = RunReport.F(st, 1), visible = RunReport.F(Th.WalkingStreakVisible, 0);
        if (s.WalkingSharedFrames == 0)
        {
            return T($"walking-noise streaks of at most {px} px (under {visible} px they do not show): {drift}", $"dare de walking noise de cel mult {px} px (sub {visible} px nu se vad): {drift}");
        }

        string shared = s.WalkingSharedFrames.ToString(Inv), frames = r.Dither.Registered.ToString(Inv);
        // One decimal on the share, so a share just under the limit never prints as the limit.
        string pct = RunReport.F(100 * s.WalkingSharedFraction, 1), from = RunReport.F(100 * Th.WalkingSharedFraction, 0);
        return T(
            $"walking-noise streaks of up to {px} px along {DriftAxisWord(s)}, in {shared} frames of {frames} ({pct} %; from {from} % they show in the stack): {drift}",
            $"dare de walking noise de pana la {px} px pe {DriftAxisWord(s)}, in {RoCount(shared)}cadre din {frames} ({pct} %; de la {from} % se vad in stiva): {drift}");
    }

    /// <summary>
    /// What the stack keeps of one frame's pattern, against the ideal and the limit over which
    /// it counts as piling (R-084): `in the stack 1.4 % of one frame's pattern stays (ideal
    /// 1.2 %, fine up to 1.8 %)`, or `the pattern adds up in place: 23.4 % of one frame's
    /// pattern stays in the stack (ideal 3.1 %, fine up to 4.7 %)` over the limit.
    /// </summary>
    /// <param name="r">The result of the series run, with the pattern kept, the registered count and whether it piles.</param>
    /// <returns>The clause, or null when no frame was registered.</returns>
    private static string? PatternClause(SeriesResult r)
    {
        DitherAnalysis d = r.Dither;
        if (d.PatternSurviving is not double f || d.Registered == 0)
        {
            return null;
        }

        string kept = RunReport.F(100 * f, 1), ideal = RunReport.F(100.0 / d.Registered, 1), limit = RunReport.F(100 * Th.PatternPilingFactor / d.Registered, 1);
        return r.Series.PatternPiles
            ? T($"the pattern adds up in place: {kept} % of one frame's pattern stays in the stack (ideal {ideal} %, fine up to {limit} %)", $"pattern-ul se aduna pe loc: in stiva ramane {kept} % din pattern-ul unui cadru (ideal {ideal} %, bine pana la {limit} %)")
            : T($"in the stack {kept} % of one frame's pattern stays (ideal {ideal} %, fine up to {limit} %)", $"in stiva ramane {kept} % din pattern-ul unui cadru (ideal {ideal} %, bine pana la {limit} %)");
    }

    /// <summary>A count and its noun, the noun singular for one, in the language of the moment.</summary>
    /// <param name="n">The count, 0 or more.</param>
    /// <param name="one">The noun for a count of one, in English and in Romanian.</param>
    /// <param name="many">The noun for any other count, in English and in Romanian.</param>
    /// <returns>
    /// `1 frame alone` (`1 cadru singur`), or `3 frames alone` (`3 cadre singure`; `20 de cadre
    /// singure` from 20 up, as <see cref="Ui.RoCount(long)"/> writes it).
    /// </returns>
    private static string Count(int n, (string En, string Ro) one, (string En, string Ro) many)
        => T($"{n.ToString(Inv)} {(n == 1 ? one.En : many.En)}", RoCount(n) + (n == 1 ? one.Ro : many.Ro));

    /// <summary>
    /// The `leave out of stack` (`de scos din stiva`) cell, in plain words (R-078): how many
    /// frames to leave out (bad and unreadable), how many stay (good and marginal) and their total
    /// exposure.
    /// </summary>
    /// <param name="r">The result of the series run.</param>
    /// <returns>
    /// `22 frames (the list is under REJECTED); 65 frames stay, that is 2 h 10 min of exposure`;
    /// frames without an exposure time add nothing to the duration.
    /// </returns>
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

        int stayCount = s.GoodCount + s.MarginalCount;
        string leave = FramesWord(s.BadCount + s.UnknownCount), stay = FramesWord(stayCount), duration = RunReport.Duration(kept);
        return T(
            $"{leave} (the list is under REJECTED); {stay} {(stayCount == 1 ? "stays" : "stay")}, that is {duration} of exposure",
            $"{leave} (lista e la RESPINSE); {(stayCount == 1 ? "ramane" : "raman")} {stay}, adica {duration} de expunere");
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

    /// <summary>
    /// The indices of the frames carrying a flag of one kind: the trailed frames for the `bad`
    /// row of TRAILED FRAMES, since a frame can be bad for its exposure alone (R-085).
    /// </summary>
    /// <param name="r">The result of the series run.</param>
    /// <param name="k">The kind of flag to list.</param>
    /// <returns>The indices joined by ", ", or `none`.</returns>
    private static string List(SeriesResult r, FlagKind k)
        => Indices(Enumerable.Range(0, r.Frames.Count).Where(i => r.Series.Flags[i].Any(f => f.Kind == k)), r);

    /// <summary>
    /// The indices of the frames of one class: the marginal frames for the `marginal` row of
    /// TRAILED FRAMES, which the R-081 explanation points at for every marginal frame.
    /// </summary>
    /// <param name="r">The result of the series run.</param>
    /// <param name="c">The class to list.</param>
    /// <returns>The indices joined by ", ", or `none`.</returns>
    private static string List(SeriesResult r, FrameClass c)
        => Indices(Enumerable.Range(0, r.Frames.Count).Where(i => r.Series.Classes[i] == c), r);

    /// <summary>The frames' indices in words, for the two listers above.</summary>
    /// <param name="positions">The 0-based positions of the frames to list, in series order.</param>
    /// <param name="r">The result of the series run.</param>
    /// <returns>The indices joined by ", ", or `none`.</returns>
    private static string Indices(IEnumerable<int> positions, SeriesResult r)
    {
        List<int> idx = positions.Select(i => r.Frames[i].Info.Index).ToList();
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
