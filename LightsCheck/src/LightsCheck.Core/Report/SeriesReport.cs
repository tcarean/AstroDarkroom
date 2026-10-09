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

/// <summary>The whole series as the console prints it (Romanian, R-027). Words only; every number comes from the analyzers.</summary>
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
        w.WriteLine($"    scara              : {(r.ArcsecPerPixel is double sc ? RunReport.F(sc, 2) + " \"/px" : "necunoscuta (fara focala)")}; pixel {RunReport.F(r.PixelSizeUm, 2)} um ({r.PixelSizeSource}); referinta cadrul {r.ReferenceIndex.ToString(Inv)}");

        w.WriteLine();
        w.WriteLine("  REZUMAT");
        w.WriteLine($"    focus              : {Focus(s, r)}");
        w.WriteLine($"    expunere           : {Exposure(s, r)}");
        w.WriteLine($"    roua               : {Dew(s)}");
        w.WriteLine($"    cadre miscate      : {s.BadCount.ToString(Inv)} slabe, {s.MarginalCount.ToString(Inv)} la limita, {s.GoodCount.ToString(Inv)} bune{(s.UnknownCount > 0 ? ", " + s.UnknownCount.ToString(Inv) + " necitite" : "")} (din {r.Frames.Count.ToString(Inv)}); {AxisLine(s)}");
        w.WriteLine($"    dithering          : {DitherVerdict(d)}");
        w.WriteLine($"    de scos din stiva  : {Keep(r)}");

        w.WriteLine();
        w.WriteLine("  FOCUS");
        w.WriteLine($"    FWHM median (cadre bune)   : {Px(s.SessionFwhm, r)}");
        w.WriteLine($"    primele / ultimele {s.Fwhm.Count.ToString(Inv)} bune : {TrendPx(s.Fwhm, r)} -> {(s.FocusHeld is bool fh ? (fh ? "focusul a tinut" : "focusul s-a miscat") : "prea putine cadre bune")}");
        w.WriteLine($"    centru / mijloc / margine  : {ZoneMedians(r)} (mediane pe cadrele bune)");

        w.WriteLine();
        w.WriteLine("  EXPUNERE SI ZGOMOT");
        w.WriteLine($"    fond de cer (ADU peste black, cadrele bune, primele -> ultimele): {Opt(s.Background.First, 0)} -> {Opt(s.Background.Last, 0)} pe verde{(s.Background.RelativeChange is double bc ? " (" + Signed(100 * bc, 0) + " %)" : "")}");
        if (s.ReadNoise is null)
        {
            w.WriteLine("    zgomot de citire   : nemasurat (fara doua bias-uri in biases\\)");
        }
        else
        {
            w.WriteLine("    zgomot de citire   : " + string.Join("  ", s.ReadNoise.Select(n => $"{n.Name} {RunReport.F(n.PairSigma, 1)}")) + " ADU (din perechea de bias-uri)");
            for (int i = 0; i < s.SwampingFrames.Count; i++)
            {
                w.WriteLine($"    cadrul {s.SwampingFrames[i].ToString(Inv),3}         : " + string.Join("  ", s.Swamping![i].Select(c => $"{c.Name}: cer {RunReport.F(c.Sky, 0)}, shot/citire {RunReport.F(c.SwampingRatio, 1)}, penalizare {(double.IsNaN(c.PenaltyPct) ? "-" : RunReport.F(c.PenaltyPct, 1) + " %")}")));
            }

            w.WriteLine($"    scara              : shot/citire peste {RunReport.F(Th.SwampingRatioOk, 0)} = cerul ineaca zgomotul de citire (penalizare sub 5 %); sub = canalul e limitat de citire, subexpuneri mai lungi ar ajuta");
        }

        w.WriteLine();
        w.WriteLine("  ROUA");
        w.WriteLine($"    halo primele / ultimele    : {Opt(s.Halo.First, 3)} / {Opt(s.Halo.Last, 3)}; stele detectate {Opt(s.Stars.First, 0)} -> {Opt(s.Stars.Last, 0)}{(s.Stars.RelativeChange is double sr ? " (" + Signed(100 * sr, 0) + " %)" : "")}; FWHM {TrendPx(s.Fwhm, r)}");
        w.WriteLine($"    verdict            : {Dew(s)}");

        w.WriteLine();
        w.WriteLine("  CADRE MISCATE");
        w.WriteLine($"    slabe (alungire >= {RunReport.F(Th.ElongationBad, 2)})    : {List(r, FrameClass.Bad)}");
        w.WriteLine($"    la limita ({RunReport.F(Th.ElongationMarginal, 2)} .. {RunReport.F(Th.ElongationBad, 2)}) : {List(r, FrameClass.Marginal)}");
        w.WriteLine($"    directia darelor   : {(s.TrailAngleDeg is double ta ? RunReport.F(ta, 0) + " grade fata de orizontala pe cadrele slabe" : "nedeterminata (prea putine cadre slabe cu directie coerenta)")}; {AxisLine(s)}");

        w.WriteLine();
        w.WriteLine("  DRIFT SI ROTATIE");
        w.WriteLine($"    drift intre cadre consecutive fara dithering: X {Signed(s.DriftXPerMin, 2)} px/min, Y {Signed(s.DriftYPerMin, 2)} px/min ({s.DriftPairs.ToString(Inv)} perechi){(r.ArcsecPerPixel is double a1 && s.DriftXPerMin is double dxm && s.DriftYPerMin is double dym ? $" = {Signed(dxm * a1, 1)} / {Signed(dym * a1, 1)} \"/min" : "")}");
        w.WriteLine($"    rotatie de camp    : {(s.RotationTotalDeg is double rt ? RunReport.F(rt * 60, 1) + " arcmin in total" : "-")}{(s.RotationPerHourDeg is double rh ? ", " + RunReport.F(rh * 60, 1) + " arcmin/h" : "")}{(s.CornerShiftPerExposurePx is double cs ? "; in colt, intr-o expunere: " + RunReport.F(cs, 2) + " px" : "")}");

        w.WriteLine();
        w.WriteLine("  DITHERING");
        w.WriteLine($"    pauze > {RunReport.F(Th.DitherPause, 0)} s : {d.Pauses.ToString(Inv)}; miscari >= {RunReport.F(Th.DitherStep, 0)} px: {d.Moves.ToString(Inv)}; pauze fara miscare: {d.PausesWithoutMove.ToString(Inv)}; miscari fara pauza: {d.MovesWithoutPause.ToString(Inv)}; cadre inregistrate: {d.Registered.ToString(Inv)} din {r.Frames.Count.ToString(Inv)}");
        w.WriteLine("    cat de des         : " + HowOften(d));
        w.WriteLine($"    cel mai lung interval intre doua miscari: cadrele {d.LongestStretchFirst.ToString(Inv)}-{d.LongestStretchLast.ToString(Inv)} ({d.LongestStretchFrames.ToString(Inv)} cadre de la o miscare la urmatoarea{Minutes(r, d.LongestStretchFirst, d.LongestStretchLast)})");
        w.WriteLine($"    cat de mult        : median {Px(d.MedianMove, r)}; " + string.Join("; ", d.Sizes.Select(c => $"{RunReport.F(c.Lower, 0)}{(c.Upper is double u ? "-" + RunReport.F(u, 0) : "+")} px: {c.Count.ToString(Inv)}")));
        w.WriteLine($"    directie           : numai X: {d.MovesOnXOnly.ToString(Inv)}; numai Y: {d.MovesOnYOnly.ToString(Inv)}; ambele: {d.MovesOnBoth.ToString(Inv)}; semn X: +{d.MovesXPlus.ToString(Inv)} / -{d.MovesXMinus.ToString(Inv)}; semn Y: +{d.MovesYPlus.ToString(Inv)} / -{d.MovesYMinus.ToString(Inv)}; {AxisLine(s)}");
        w.WriteLine($"    acoperire          : cel mai apropiat alt cadru median {Opt(Median(d.NearestOther), 1)} px; sub {RunReport.F(Th.CoverageTight, 0)} px: {d.WithinTight.ToString(Inv)}, sub {RunReport.F(Th.CoverageMid, 0)}: {d.WithinMid.ToString(Inv)}, sub {RunReport.F(Th.CoverageLoose, 0)}: {d.WithinLoose.ToString(Inv)}; izolate (>= {RunReport.F(Th.CoverageLoose, 0)} px de orice): {d.IsolatedLoose.ToString(Inv)}");
        w.WriteLine($"                         pozitii distincte: {d.DistinctMid.ToString(Inv)} la {RunReport.F(Th.CoverageMid, 0)} px, {d.DistinctLoose.ToString(Inv)} la {RunReport.F(Th.CoverageLoose, 0)} px; intindere X {RunReport.F(d.SpanX, 0)} px, Y {RunReport.F(d.SpanY, 0)} px");
        w.WriteLine($"    grupuri in {RunReport.F(Th.CoverageMid, 0)} px    : {Runs(d.RunsMid, r)}");
        w.WriteLine($"    grupuri in {RunReport.F(Th.CoverageLoose, 0)} px   : {Runs(d.RunsLoose, r)}");
        w.WriteLine($"    verdict            : {DitherVerdict(d)}");

        w.WriteLine();
        w.WriteLine($"  RESPINSE ({s.BadCount.ToString(Inv)} cadre slabe; cele la limita raman la alegerea ta)");
        for (int i = 0; i < r.Frames.Count; i++)
        {
            if (s.Classes[i] == FrameClass.Bad || s.Classes[i] == FrameClass.Unknown)
            {
                w.WriteLine($"    {r.Frames[i].Info.Index.ToString(Inv),3}  {r.Frames[i].Info.FileName,-20}  {Reasons(s.Flags[i])}");
            }
        }

        w.WriteLine();
        w.WriteLine("  TABEL PE CADRE");
        w.WriteLine("     nr  fisier                ora       clasa     alung  FWHM   stele   fond  pauza    pas     dX      dY  motive");
        for (int i = 0; i < r.Frames.Count; i++)
        {
            AnalyzedFrame f = r.Frames[i];
            FrameMeasurement? m = f.Measurement;
            string name = f.Info.FileName.Length > 20 ? f.Info.FileName[..20] : f.Info.FileName;
            string time = f.Info.Timestamp is DateTime t ? t.ToString("HH:mm:ss", Inv) : "-";
            double? pause = r.Cadence.PauseSeconds[i];
            w.WriteLine($"    {f.Info.Index.ToString(Inv),3}  {name,-20}  {time,-8}  {ClassWord(s.Classes[i]),-8}  {Opt(m?.ElongationMedian, 2),5}  {Opt(m?.FwhmMedian, 2),5}  {(m is null ? "-" : m.Detected.ToString(Inv)),6}  {Opt(m?.BackgroundMedian, 0),5}  {(pause is double p ? RunReport.F(p, 0) + " s" : "-"),5}  {Opt(d.Step[i], 1),5}  {Opt(f.Registration is { Ok: true } rg ? rg.Dx : null, 1),6}  {Opt(f.Registration is { Ok: true } rg2 ? rg2.Dy : null, 1),6}  {Reasons(s.Flags[i])}");
        }
    }

    /// <summary>The words of a flag: the kind, the figure and the threshold.</summary>
    internal static string Reason(FrameFlag f)
    {
        ArgumentNullException.ThrowIfNull(f);
        return f.Kind switch
        {
            FlagKind.Trailed => $"alungire {RunReport.F(f.Value, 2)} (prag {RunReport.F(f.Threshold, 2)}){(f.Detail is null ? "" : ", dara la " + f.Detail + " grade")}",
            FlagKind.MarginalTrail => $"alungire {RunReport.F(f.Value, 2)} la limita (prag {RunReport.F(f.Threshold, 2)})",
            FlagKind.Soft => $"FWHM {RunReport.F(f.Value, 2)} px, peste {RunReport.F(f.Threshold, 2)} ({RunReport.F(Th.SoftFwhmFactor, 1)} x mediana sesiunii)",
            FlagKind.Clouds => $"stele {RunReport.F(f.Value, 0)}, sub {RunReport.F(f.Threshold, 0)} ({RunReport.F(100 * Th.CloudStarFraction, 0)} % din mediana): nori sau ceata",
            FlagKind.BackgroundJump => $"salt de fond {Signed(f.Value, 0)} % fata de vecini (prag {RunReport.F(f.Threshold, 0)} %)",
            FlagKind.Trails => $"{RunReport.F(f.Value, 0)} dara (satelit/avion; sigma clipping o scoate)",
            FlagKind.Unreadable => "nu s-a putut citi" + (f.Detail is null ? "" : ": " + f.Detail),
            FlagKind.Unregistered => "neinregistrat (prea putine stele potrivite cu referinta)",
            _ => f.Kind.ToString(),
        };
    }

    internal static string Reasons(IReadOnlyList<FrameFlag> flags) => flags.Count == 0 ? "" : string.Join("; ", flags.Select(Reason));

    internal static string ClassWord(FrameClass c) => c switch
    {
        FrameClass.Good => "bun",
        FrameClass.Marginal => "la limita",
        FrameClass.Bad => "SLAB",
        _ => "necitit",
    };

    private static string Focus(SeriesAnalysis s, SeriesResult r)
    {
        if (s.SessionFwhm is null)
        {
            return "nedeterminat (niciun cadru bun)";
        }

        string held = s.FocusHeld is bool h ? (h ? "a tinut toata sesiunea" : "s-a miscat") : "prea putine cadre bune pentru tendinta";
        return $"{(s.FocusHeld is false ? "ATENTIE" : "BUN")} - FWHM median {Px(s.SessionFwhm, r)} pe cadrele bune, {TrendPx(s.Fwhm, r)} la inceput / sfarsit: {held}";
    }

    private static string Exposure(SeriesAnalysis s, SeriesResult r)
    {
        string bg = s.Background.First is double bf && s.Background.Last is double bl ? $"fond G {RunReport.F(bf, 0)} -> {RunReport.F(bl, 0)} ADU" : "fond necunoscut";
        if (s.Swamping is null || s.Swamping.Count == 0)
        {
            return bg + "; zgomotul de citire nemasurat (fara bias-uri)";
        }

        ChannelNoise? worst = s.Swamping.SelectMany(x => x).Where(c => !double.IsNaN(c.SwampingRatio) && !double.IsInfinity(c.SwampingRatio)).MinBy(c => c.SwampingRatio);
        if (worst is null)
        {
            return bg + "; raportul shot/citire nu s-a putut masura (fereastra de zgomot prea mica)";
        }

        string verdict = worst.SwampingRatio >= Th.SwampingRatioOk ? "BUNA" : "LA LIMITA";
        return $"{verdict} - {bg}; canalul cel mai slab {worst.Name}: shot/citire {RunReport.F(worst.SwampingRatio, 1)}, penalizare {RunReport.F(worst.PenaltyPct, 0)} %";
    }

    private static string Dew(SeriesAnalysis s) => s.DewSuspected switch
    {
        true => $"POSIBIL - FWHM a crescut cu {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} % si halo-ul cu {Signed(s.Halo.Change ?? 0, 3)}",
        false => $"NU - FWHM {Signed(100 * (s.Fwhm.RelativeChange ?? 0), 0)} %, halo {Signed(s.Halo.Change ?? 0, 3)}",
        null => "nedeterminat (prea putine cadre bune)",
    };

    private static string AxisLine(SeriesAnalysis s) => s.RaAxis switch
    {
        Axis.X => $"axa X a imaginii = RA, Y = DEC ({s.AxisSource})",
        Axis.Y => $"axa Y a imaginii = RA, X = DEC ({s.AxisSource})",
        _ => "axa RA nedeterminata",
    };

    private static string DitherVerdict(DitherAnalysis d)
    {
        if (d.Registered < 2)
        {
            return "nedeterminat (cadre neinregistrate)";
        }

        string hot = d.HotPixelsOk ? "OK pentru pixeli calzi" : $"ATENTIE la pixeli calzi (peste {Th.HotPixelRunMax.ToString(Inv)} cadre consecutive in {RunReport.F(Th.CoverageTight, 0)} px)";
        string walk = d.WalkingNoiseOk ? "OK pentru walking noise" : "INSUFICIENT pentru walking noise";
        return $"{walk}, {hot} - {d.Moves.ToString(Inv)} miscari la {d.Registered.ToString(Inv)} cadre, {d.DistinctLoose.ToString(Inv)} pozitii distincte la {RunReport.F(Th.CoverageLoose, 0)} px, {d.IsolatedLoose.ToString(Inv)} izolate, {d.MovesOnBoth.ToString(Inv)} miscari pe ambele axe";
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
            parts.Add($"la fiecare {kv.Key.ToString(Inv)} {(kv.Key == 1 ? "cadru" : "cadre")}: {kv.Value.ToString(Inv)} {(kv.Value == 1 ? "data" : "ori")}");
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

    private static string TrendPx(Trend t, SeriesResult r)
        => $"{Opt(t.First, 2)} / {Opt(t.Last, 2)} px" + (t.RelativeChange is double rc ? " (" + Signed(100 * rc, 0) + " %)" : "");

    private static double? Median(IReadOnlyList<double?> v)
    {
        List<double> x = v.Where(a => a is not null).Select(a => a!.Value).ToList();
        return x.Count > 0 ? Raster.Percentile(x, 50) : null;
    }

    private static string Opt(double? v, int decimals) => v is double d ? RunReport.F(d, decimals) : "-";

    private static string Signed(double? v, int decimals) => v is double d ? (d >= 0 ? "+" : "") + RunReport.F(d, decimals) : "-";
}
