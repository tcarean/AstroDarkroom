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

/// <summary>One labelled line of the report; Verdict is the word the summary rows open with (BUN, LA LIMITA, NU, ...), null elsewhere.</summary>
internal sealed record ReportRow(string Label, string Value, string? Verdict = null);

/// <summary>A rejected frame: its index, its file and its reasons in words.</summary>
internal sealed record RejectedRow(int Index, string FileName, string Reasons);

/// <summary>One paragraph of an explanation the window shows; Emphasis marks the one to set in bold (R-036).</summary>
internal sealed record ReportParagraph(string Text, bool Emphasis);

/// <summary>A table the window lays out: a title, the column headers and the rows, every cell already a string (R-036).</summary>
internal sealed record ReportTable(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>One frame of the table, every cell already formatted; "-" where a figure is missing.</summary>
internal sealed record FrameTableRow(int Index, string FileName, string Time, string Class, string Elongation, string Fwhm, string Stars, string Background, string Pause, string Step, string Dx, string Dy, string Move, string Spot, string Reasons, string? StepTip);

/// <summary>
/// The whole series as the console prints it (Romanian, R-027), built from labelled rows that
/// the window shows as they are (R-029): words only, every number comes from the analyzers.
/// </summary>
internal static class SeriesReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    internal static void Write(SeriesResult r, TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(w);
        SeriesAnalysis s = r.Series;
        DitherAnalysis d = r.Dither;
        List<FrameInfo> infos = r.Frames.Select(f => f.Info).ToList();
        InventoryReport.WriteHeader(new Inventory(r.Session, infos, r.Cadence), w);
        w.WriteLine($"    scara              : {ScaleLine(r)}");

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

    /// <summary>The text in the RESPINSE heading's parentheses: the counts and the rule for the marginal frames.</summary>
    internal static string RejectedHeading(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        return $"{s.BadCount.ToString(Inv)} cadre slabe{(s.UnknownCount > 0 ? ", " + s.UnknownCount.ToString(Inv) + " necitite" : "")}; cele la limita raman la alegerea ta";
    }

    /// <summary>The table, one row per frame, the cells formatted as the console prints them (the window shows the same cells).</summary>
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
    /// The `dithering` cell (R-038): the mount's move on each axis in pixels, with its sense
    /// and the SynScan default arrow when the move is signed (`RA 15 px V (dreapta)`), the
    /// pixels alone otherwise; a component under Th.AxisStep is left out; `-` for no move.
    /// </summary>
    internal static string MoveCell(MountMove? m, ArrowKeys keys)
    {
        if (m is null)
        {
            return "-";
        }

        List<string> parts = MoveComponents(m, keys).Select(c => $"{c.Axis} {RunReport.F(c.Px, 0)} px" + (m.Signed ? $" {c.Arrow} {c.Sense}" : "")).ToList();
        return parts.Count == 0 ? "-" : string.Join(", ", parts);
    }

    /// <summary>The SynScan arrows of a signed move, `←, ↓`, for the CSV; empty otherwise (R-038, R-040).</summary>
    internal static string MoveArrows(MountMove? m, ArrowKeys keys)
        => m is null || !m.Signed ? "" : string.Join(", ", MoveComponents(m, keys).Select(c => c.Arrow));

    /// <summary>
    /// The components of a move that clear Th.AxisStep, each with its axis word, its pixels,
    /// its sense letter and the SynScan arrow that moves the mount that way under the given
    /// keys (ADR 0014); the sense and the arrow mean nothing when the move is unsigned.
    /// </summary>
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

    /// <summary>The cell `k/n de la f` of a frame's run at 10 px, `-` outside one (R-034).</summary>
    internal static string SpotCell(SpotRun? run)
        => run is null ? "-" : $"{run.Ordinal.ToString(Inv)}/{run.Frames.ToString(Inv)} de la {run.FirstIndex.ToString(Inv)}";

    /// <summary>What the Respinse tab says under its heading, one paragraph per line: the elongation, where the marginal frames are, the trail and its axis, and in bold what each axis points to (R-035, R-036).</summary>
    internal static IReadOnlyList<ReportParagraph> RejectedExplanation { get; } =
    [
        new($"Alungirea e raportul dintre axa lunga si axa scurta a stelelor (1 = rotunde), mediana pe cadru: de la {RunReport.F(Th.ElongationBad, 2)} cadrul e slab si iese din stiva.", false),
        new($"Intre {RunReport.F(Th.ElongationMarginal, 2)} si {RunReport.F(Th.ElongationBad, 2)} cadrul e la limita si ramane, la alegerea ta: lista lui e randul 'la limita' din CADRE MISCATE, tabul Rezumat; in tabul Cadre are clasa galbena.", false),
        new("Dara e directia in care s-au intins stelele in timpul expunerii: unghiul fata de orizontala imaginii si, cand se stie care axa a imaginii e RA si stelele cadrului sunt de acord intre ele (coerenta), axa monturii pe care cade.", false),
        new("Dara pe RA vine din urmarire: eroarea periodica, viteza, backlash-ul pe RA. Dara pe DEC vine din alinierea polara, vant sau o atingere a monturii. Oblica: amandoua, sau o lovitura.", true),
    ];

    /// <summary>The caption of a star cut-out: where it was taken and the star's three figures.</summary>
    internal static string CropCaption(StarCrop c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return $"{c.Where}: FWHM {RunReport.F(c.Star.Fwhm, 1)} px, alungire {RunReport.F(c.Star.Elongation, 2)}, unghi {RunReport.F(c.Star.AngleDeg, 0)}";
    }

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

    internal static string ScaleLine(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return $"{(r.ArcsecPerPixel is double sc ? RunReport.F(sc, 2) + " \"/px" : "necunoscuta (fara focala)")}; pixel {RunReport.F(r.PixelSizeUm, 2)} um ({r.PixelSizeSource}); referinta cadrul {(r.ReferenceIndex > 0 ? r.ReferenceIndex.ToString(Inv) : "niciunul")}";
    }

    // ------------------------------------------------------------------ the sections

    internal static IReadOnlyList<ReportRow> SummaryRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        DitherAnalysis d = r.Dither;
        (string fv, string ft) = Focus(s, r);
        (string ev, string et) = Exposure(s, r);
        (string dv, string dt) = Dew(s);
        (string dither, string ditherText) = DitherVerdict(d);
        return
        [
            new("focus", ft, fv),
            new("expunere", et, ev),
            new("roua", dt, dv),
            new("cadre miscate", $"{s.BadCount.ToString(Inv)} slabe, {s.MarginalCount.ToString(Inv)} la limita, {s.GoodCount.ToString(Inv)} bune{(s.UnknownCount > 0 ? ", " + s.UnknownCount.ToString(Inv) + " necitite" : "")} (din {r.Frames.Count.ToString(Inv)}); {AxisLine(s)}"),
            new("dithering", ditherText, dither),
            new("de scos din stiva", Keep(r)),
        ];
    }

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

    /// <summary>The map's axis labels: which image axis is RA and which DEC, plain `X` / `Y` without an axis (R-036).</summary>
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

    /// <summary>The headers of the position columns of the frame table: `dX (DEC)`, `dY (RA)`, plain without an axis (R-036).</summary>
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

    internal static IReadOnlyList<ReportRow> DriftRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        return
        [
            new("drift intre cadre consecutive fara dithering", $"X {Signed(s.DriftXPerMin, 2)} px/min, Y {Signed(s.DriftYPerMin, 2)} px/min ({s.DriftPairs.ToString(Inv)} perechi){(r.ArcsecPerPixel is double a1 && s.DriftXPerMin is double dxm && s.DriftYPerMin is double dym ? $" = {Signed(dxm * a1, 1)} / {Signed(dym * a1, 1)} \"/min" : "")}"),
            new("rotatie de camp", $"{(s.RotationTotalDeg is double rt ? RunReport.F(rt * 60, 1) + " arcmin in total" : "-")}{(s.RotationPerHourDeg is double rh ? ", " + RunReport.F(rh * 60, 1) + " arcmin/h" : "")}{(s.CornerShiftPerExposurePx is double cs ? "; in colt, intr-o expunere: " + RunReport.F(cs, 2) + " px" : "")}"),
        ];
    }

    internal static IReadOnlyList<ReportRow> DitherRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        SeriesAnalysis s = r.Series;
        DitherAnalysis d = r.Dither;
        (string verdict, string text) = DitherVerdict(d);
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
            new("verdict", text, verdict),
        ];
    }

    /// <summary>The DITHERING rows that are not tables, for the window's Dithering tab beside <see cref="DitherTables"/> (R-036).</summary>
    internal static IReadOnlyList<ReportRow> DitherScalarRows(SeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DitherAnalysis d = r.Dither;
        (string verdict, string text) = DitherVerdict(d);
        return
        [
            PausesRow(r, d),
            LongestRow(r, d),
            new("acoperire", $"cel mai apropiat alt cadru median {Opt(Median(d.NearestOther), 1)} px; izolate (>= {RunReport.F(Th.CoverageLoose, 0)} px de orice): {d.IsolatedLoose.ToString(Inv)}"),
            new("intindere", $"X {RunReport.F(d.SpanX, 0)} px, Y {RunReport.F(d.SpanY, 0)} px"),
            new("axe", AxisLine(r.Series)),
            new("verdict", text, verdict),
        ];
    }

    /// <summary>The DITHERING figures that are tables, for the window (R-036): how often, how far, direction, coverage, runs; the console keeps its rows (ADR 0012).</summary>
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

    private static ReportRow PausesRow(SeriesResult r, DitherAnalysis d)
        => new($"pauze > {RunReport.F(Th.DitherPause, 0)} s", $"{d.Pauses.ToString(Inv)}; miscari >= {RunReport.F(Th.DitherStep, 0)} px: {d.Moves.ToString(Inv)}; pauze fara miscare: {d.PausesWithoutMove.ToString(Inv)}; miscari fara pauza: {d.MovesWithoutPause.ToString(Inv)}; cadre inregistrate: {d.Registered.ToString(Inv)} din {r.Frames.Count.ToString(Inv)}");

    private static ReportRow LongestRow(SeriesResult r, DitherAnalysis d)
        => new("cel mai lung interval intre doua miscari", $"cadrele {d.LongestStretchFirst.ToString(Inv)}-{d.LongestStretchLast.ToString(Inv)} ({d.LongestStretchFrames.ToString(Inv)} cadre de la o miscare la urmatoarea{Minutes(r, d.LongestStretchFirst, d.LongestStretchLast)})");

    private static string FramesWord(int frames) => $"{frames.ToString(Inv)} {(frames == 1 ? "cadru" : "cadre")}";

    private static string TimesWord(int times) => $"{times.ToString(Inv)} {(times == 1 ? "data" : "ori")}";

    private static string SizeWord(SizeClass c) => $"{RunReport.F(c.Lower, 0)}{(c.Upper is double u ? "-" + RunReport.F(u, 0) : "+")} px";

    private static string PxWord(double px) => RunReport.F(px, 0) + " px";

    /// <summary>The rows of one radius in the runs table: one per run, or a single `niciunul` row.</summary>
    private static List<IReadOnlyList<string>> RunRows(IReadOnlyList<FrameStretch> runs, double radius, SeriesResult r)
    {
        if (runs.Count == 0)
        {
            return [[PxWord(radius), "niciunul", "-", "-"]];
        }

        return runs.Select(x => (IReadOnlyList<string>)[PxWord(radius), $"{x.FirstIndex.ToString(Inv)}-{x.LastIndex.ToString(Inv)}", x.Frames.ToString(Inv), MinutesCell(r, x.FirstIndex, x.LastIndex)]).ToList();
    }

    /// <summary>The duration of a stretch as a cell: <see cref="Minutes"/> without its leading separator, `-` when the frames carry no time.</summary>
    private static string MinutesCell(SeriesResult r, int first, int last)
    {
        string m = Minutes(r, first, last);
        return m.Length > 2 ? m[2..] : "-";
    }

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

    /// <summary>`, dara pe RA (-90 grade)` when the trail's axis is known, `, dara la -90 grade` when only its angle is, nothing without an angle (R-033).</summary>
    private static string TrailSuffix(FrameFlag f)
        => f.Detail is null ? "" : f.On == TrailAxis.Unknown ? $", dara la {f.Detail} grade" : $", dara {AxisWord(f.On)} ({f.Detail} grade)";

    internal static string AxisWord(TrailAxis on) => on switch
    {
        TrailAxis.Ra => "pe RA",
        TrailAxis.Dec => "pe DEC",
        TrailAxis.Oblique => "oblica",
        _ => "",
    };

    internal static string Reasons(IReadOnlyList<FrameFlag> flags)
    {
        ArgumentNullException.ThrowIfNull(flags);
        return flags.Count == 0 ? "" : string.Join("; ", flags.Select(Reason));
    }

    internal static string ClassWord(FrameClass c) => c switch
    {
        FrameClass.Good => "bun",
        FrameClass.Marginal => "la limita",
        FrameClass.Bad => "SLAB",
        _ => "necitit",
    };

    private static (string Verdict, string Text) Focus(SeriesAnalysis s, SeriesResult r)
    {
        if (s.SessionFwhm is null)
        {
            return ("NEDETERMINAT", "niciun cadru bun");
        }

        string held = s.FocusHeld is bool h ? (h ? "a tinut toata sesiunea" : "s-a miscat") : "prea putine cadre bune pentru tendinta";
        return (s.FocusHeld is false ? "ATENTIE" : "BUN", $"FWHM median {Px(s.SessionFwhm, r)} pe cadrele bune, {TrendPx(s.Fwhm)} la inceput / sfarsit: {held}");
    }

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

    private static (string Verdict, string Text) Dew(SeriesAnalysis s) => s.DewSuspected switch
    {
        true => ("POSIBIL", $"FWHM a crescut cu {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} % si halo-ul cu {Signed(s.Halo.Change ?? 0, 3)}"),
        false => ("NU", $"FWHM {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} %, halo {Signed(s.Halo.Change ?? 0, 3)}"),
        null => ("NEDETERMINAT", "prea putine cadre bune"),
    };

    private static string AxisLine(SeriesAnalysis s) => s.RaAxis switch
    {
        Axis.X => $"axa X a imaginii = RA, Y = DEC ({s.AxisSource})",
        Axis.Y => $"axa Y a imaginii = RA, X = DEC ({s.AxisSource})",
        _ => "axa RA nedeterminata",
    };

    private static (string Verdict, string Text) DitherVerdict(DitherAnalysis d)
    {
        if (d.Registered < 2)
        {
            return ("NEDETERMINAT", "cadre neinregistrate");
        }

        string hot = d.HotPixelsOk ? "OK pentru pixeli calzi" : $"ATENTIE la pixeli calzi (peste {Th.HotPixelRunMax.ToString(Inv)} cadre consecutive in {RunReport.F(Th.CoverageTight, 0)} px)";
        string walk = d.WalkingNoiseOk ? "OK pentru walking noise" : "INSUFICIENT pentru walking noise";
        return ($"{walk}, {hot}", $"{d.Moves.ToString(Inv)} miscari la {d.Registered.ToString(Inv)} cadre, {d.DistinctLoose.ToString(Inv)} pozitii distincte la {RunReport.F(Th.CoverageLoose, 0)} px, {d.IsolatedLoose.ToString(Inv)} izolate, {d.MovesOnBoth.ToString(Inv)} miscari pe ambele axe");
    }

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

    private static string Runs(IReadOnlyList<FrameStretch> runs, SeriesResult r)
        => runs.Count == 0 ? "niciunul" : string.Join(", ", runs.Select(x => $"{x.FirstIndex.ToString(Inv)}-{x.LastIndex.ToString(Inv)} ({x.Frames.ToString(Inv)}{Minutes(r, x.FirstIndex, x.LastIndex)})"));

    private static string List(SeriesResult r, FrameClass c)
    {
        List<int> idx = Enumerable.Range(0, r.Frames.Count).Where(i => r.Series.Classes[i] == c).Select(i => r.Frames[i].Info.Index).ToList();
        return idx.Count == 0 ? "niciunul" : string.Join(", ", idx.Select(i => i.ToString(Inv)));
    }

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

    private static string Px(double? v, SeriesResult r)
        => v is double d ? RunReport.F(d, 2) + " px" + (r.ArcsecPerPixel is double a ? " = " + RunReport.F(d * a, 1) + "\"" : "") : "-";

    private static string TrendPx(Trend t)
        => $"{Opt(t.First, 2)} / {Opt(t.Last, 2)} px" + (t.RelativeChange is double rc ? " (" + Signed(100 * rc, 0) + " %)" : "");

    private static double? Median(IReadOnlyList<double?> v)
    {
        List<double> x = v.Where(a => a is not null).Select(a => a!.Value).ToList();
        return x.Count > 0 ? Raster.Percentile(x, 50) : null;
    }

    private static string Opt(double? v, int decimals) => v is double d ? RunReport.F(d, decimals) : "-";

    private static string Signed(double? v, int decimals) => v is double d ? (d >= 0 ? "+" : "") + RunReport.F(d, decimals) : "-";
}
