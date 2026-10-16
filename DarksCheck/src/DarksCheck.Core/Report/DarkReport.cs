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

namespace DarksCheck.Core;

/// <summary>The darks report as the console prints it (Romanian, R-025 to R-041): checks 2 to 8, each with its figures and its verdict line, then a table per frame. Words only; the numbers come from the run.</summary>
internal static class DarkReport
{
    internal static void Write(DarkSeriesResult r, TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(w);
        InventoryReport.WriteHeader(r.Inventory, w);
        DarkSeriesAnalysis d = r.Darks;
        DarkVerdicts v = r.Verdicts;
        int rp = r.ReferencePlane;
        string g = d.PlaneNames[rp];
        string eq = Eq(d);
        double n = d.Norm;

        w.WriteLine();
        w.WriteLine($"=== DARK-URI: {RunReport.Frames(d.Used)} decodate din {d.Frames.Count}, {d.Width} x {d.Height} px, scara {F(d.Black, 0)} .. {F(d.White, 0)} ADU ({d.ScaleSource}), CFA {d.CfaLetters ?? "necunoscut"} ===");
        w.WriteLine($"    normalizare        : {Normalisation(d)}");
        w.WriteLine($"    cuantizare         : pas {d.QuantStep} ADU langa negru; {d.UniqueValues} valori distincte in primul cadru ({g})");
        if (r.BiasError is not null)
        {
            w.WriteLine($"    ATENTIE            : bias-urile nu s-au putut folosi: {r.BiasError}");
        }

        // 2
        w.WriteLine();
        w.WriteLine("  2. SCURGERI DE LUMINA SI GRADIENT");
        w.WriteLine($"    master dark        : {RegionsLine(d.MeanMaps[rp].Regions, n)}  [{g}, {eq} peste negru, media a {d.Used} cadre, blocuri de {Th.BinPx * 2} px]");
        if (r.Biases is BiasAnalysis b)
        {
            w.WriteLine($"    master bias        : {RegionsLine(b.MeanMaps[rp].Regions, 1)}  [{g}, ADU peste negru, media a {b.Used} cadre]");
        }

        if (r.DarkCurrent is DarkCurrent dc)
        {
            w.WriteLine($"    dark - bias        : {RegionsLine(dc.Maps[rp].Regions, n)}; mediana {F(dc.Median / n, 2)}, {F(dc.P1 / n, 2)} .. {F(dc.P99 / n, 2)} (p1 .. p99)");
        }

        w.WriteLine($"    verdict            : {Leak(v.Leak)}");

        // 3
        w.WriteLine();
        w.WriteLine("  3. DERIVA TERMICA IN SERIE");
        w.WriteLine($"    amplitudine        : {Amplitude(d, v.Drift)}");
        w.WriteLine($"    verdict            : {Drift(v.Drift)}");

        // 4
        w.WriteLine();
        w.WriteLine("  4. ZGOMOT PE CADRU");
        w.WriteLine($"    sigma rezidual     : {ResidualRange(d, rp)}  [ADU, cadru - master]");
        w.WriteLine($"    zgomot de citire   : {ReadNoise(r, rp)}");
        w.WriteLine($"    benzi aleatoare    : {Banding(v.Noise)}");
        w.WriteLine($"    verdict            : {Noise(v.Noise)}");

        // 5
        w.WriteLine();
        w.WriteLine("  5. PIXELI FIERBINTI");
        w.WriteLine($"    master peste nivel : {Levels(d)}");
        w.WriteLine($"    pe plan la {F(Th.HotAdu, 0)}      : {PerPlane(d, Th.HotLevelIndex)}");
        w.WriteLine($"    amplitudine        : {Percentiles(d.Hot, n, eq)}");
        w.WriteLine($"    stabilitate        : {Stability(d, rp)}");
        double mpx = d.Width * (double)d.Height / 1e6;
        w.WriteLine($"    verdict            : OK (informativ): {d.Hot.Hot} pixeli peste {F(Th.HotAdu, 0)} {eq} in {F(mpx, mpx < 1 ? 2 : 1)} Mpx");

        // 6
        w.WriteLine();
        w.WriteLine("  6. MODEL FIX");
        w.WriteLine($"    benzi structurate  : {StructuredBanding(d)}");
        w.WriteLine($"    glow               : {Glow(v.Leak, v.Pattern)}");
        w.WriteLine($"    verdict            : {Pattern(v.Pattern)}");

        // 7
        w.WriteLine();
        w.WriteLine("  7. PIXELI MORTI SI SATURATI");
        w.WriteLine($"    master             : {v.DeadSat.Dead} sub negru - {F(Th.DeadBelowAdu, 0)} ADU, {v.DeadSat.Saturated} la alb ({F(d.White, 0)})");
        w.WriteLine($"    verdict            : {DeadSat(v.DeadSat)}");

        // 8
        w.WriteLine();
        w.WriteLine("  8. CATE DARK-URI AJUNG");
        w.WriteLine($"    zgomotul mediei    : {Curve(d.Noise)}  [{g}, centrul de {d.Noise.CropPx} px, ADU, sigma temporala = cadru fata de cadru]");
        w.WriteLine($"    master             : {Enough(d, v.Enough)}");
        w.WriteLine($"    verdict            : {EnoughVerdictText(v.Enough)}");

        // the table
        w.WriteLine();
        w.WriteLine("  TABEL PE CADRE");
        w.WriteLine($"     nr  fisier                ora       medie   sigma  rezid.   benzi  amplit.  >{F(Th.HotAdu, 0)}   [{g}; medie = ADU peste negru; benzi = randuri / zgomot alb; amplit. = pixelii fierbinti fata de master]");
        foreach (DarkFrameFigures f in d.Frames)
        {
            string name = f.Info.FileName.Length > 20 ? f.Info.FileName[..20] : f.Info.FileName;
            if (f.Skipped)
            {
                w.WriteLine($"    {f.Info.Index,3}  {name,-20}  {f.Error}");
                continue;
            }

            if (f.Error is not null)
            {
                w.WriteLine($"    {f.Info.Index,3}  {name,-20}  eroare: {f.Error}");
                continue;
            }

            PlaneFigures p = f.Planes[rp];
            ResidualFigures res = f.Residuals[rp];
            double bands = res.RowExpected > 0 ? res.RowStd / res.RowExpected : double.NaN;
            w.WriteLine($"    {f.Info.Index,3}  {name,-20}  {Time(f.Info.Timestamp),-8}  {F(f.MeanAboveBlack, 2),6}  {F(p.Sigma, 2),6}  {F(res.Sigma, 2),6}  {F(bands, 1),6}  {Ratio(f.AmpRatioMedian),7}  {p.Above[Th.HotLevelIndex],4}");
        }
    }

    internal static string VerdictWord(Verdict v) => v switch
    {
        Verdict.Ok => "OK",
        Verdict.Warning => "ATENTIE",
        _ => "NEMASURAT",
    };

    /// <summary>"ADU" when the normalisation is 1, "ADU-eq" otherwise: the unit the figures carry.</summary>
    internal static string Eq(DarkSeriesAnalysis d) => Math.Abs(d.Norm - 1) < 1e-9 ? "ADU" : "ADU-eq";

    internal static string Normalisation(DarkSeriesAnalysis d)
    {
        ArgumentNullException.ThrowIfNull(d);
        FrameInfo? first = d.Frames.FirstOrDefault(f => f.Error is null)?.Info;
        string from = first?.Iso is double iso && first.ExposureSeconds is double e
            ? $"ISO {F(iso, 0)}, {InventoryReport.Exposure(e)} s"
            : "ISO sau expunere necunoscute";
        return Math.Abs(d.Norm - 1) < 1e-9
            ? $"{from} -> x1.00 fata de ISO {F(Th.NormIso, 0)} si {F(Th.NormExposureSeconds, 0)} s (ADU-eq = ADU)"
            : $"{from} -> x{F(d.Norm, 2)} fata de ISO {F(Th.NormIso, 0)} si {F(Th.NormExposureSeconds, 0)} s (1 ADU-eq = {F(d.Norm, 2)} ADU)";
    }

    internal static string RegionName(MapRegion r) => r switch
    {
        MapRegion.TopLeft => "stanga-sus",
        MapRegion.TopRight => "dreapta-sus",
        MapRegion.BottomLeft => "stanga-jos",
        MapRegion.BottomRight => "dreapta-jos",
        MapRegion.Top => "sus",
        MapRegion.Bottom => "jos",
        MapRegion.Left => "stanga",
        _ => "dreapta",
    };

    private static string RegionsLine(Regions r, double n) =>
        $"centru {F(r.Centre / n, 2)}, colturi {F(r.TopLeft / n, 2)} / {F(r.TopRight / n, 2)} / {F(r.BottomLeft / n, 2)} / {F(r.BottomRight / n, 2)} (SS / DS / SJ / DJ), margini sus {F(r.Top / n, 2)} jos {F(r.Bottom / n, 2)} stanga {F(r.Left / n, 2)} dreapta {F(r.Right / n, 2)}";

    private static string Leak(LeakVerdict l)
    {
        string source = l.FromDarkMinusBias ? "pe dark - bias" : "pe masterul dark (fara bias)";
        return l.Verdict switch
        {
            Verdict.Unmeasured => "NEMASURAT: harta nu are valori",
            Verdict.Ok => $"OK: nicio margine sau colt la mai mult de {F(Th.LeakAdu, 1)} ADU-eq de centru (cel mai departe {Signed(l.OffsetAdu, 2)} la {RegionName(l.Where)}, {source})",
            _ => $"ATENTIE: {RegionName(l.Where)} e la {Signed(l.OffsetAdu, 2)} ADU-eq de centru ({source}); peste {F(Th.LeakAdu, 1)}: {(l.OffsetAdu > 0 ? "scurgere de lumina sau glow" : "margine mai intunecata decat centrul, offset de citire sau bias nepotrivit")}",
        };
    }

    private static string Amplitude(DarkSeriesAnalysis d, DriftVerdict v) =>
        double.IsNaN(v.FirstRatio)
            ? "NEMASURAT: prea putine cadre sau niciun pixel fierbinte"
            : $"pixelii fierbinti la {F(v.FirstRatio, 2)} din master in primul cadru, {F(v.LastRatio, 2)} in ultimul ({d.Warming.Pixels} pixeli peste {F(Th.HotAdu, 0)} {Eq(d)})";

    private static string Drift(DriftVerdict v) => v.Verdict switch
    {
        Verdict.Unmeasured => "NEMASURAT",
        Verdict.Ok => $"OK: senzorul s-a miscat cu {Signed(v.DeltaCelsius, 1)} grade in {F(v.Minutes, 0)} min (regula dublarii la {F(Th.DoublingCelsius, 0)} grade), sub {F(Th.DriftCelsius, 1)}",
        _ => $"ATENTIE: senzorul s-a {(v.DeltaCelsius > 0 ? "incalzit" : "racit")} cu {F(Math.Abs(v.DeltaCelsius), 1)} grade in {F(v.Minutes, 0)} min (regula dublarii la {F(Th.DoublingCelsius, 0)} grade); masterul, mediana seriei, corespunde mijlocului ei",
    };

    private static string ResidualRange(DarkSeriesAnalysis d, int rp)
    {
        List<string> parts = [];
        for (int p = 0; p < 4; p++)
        {
            double[] s = d.Frames.Where(f => f.Error is null).Select(f => f.Residuals[p].Sigma).ToArray();
            if (s.Length == 0)
            {
                continue;
            }

            parts.Add($"{d.PlaneNames[p]} {F(s.Min(), 2)} .. {F(s.Max(), 2)}" + (p == rp ? $" (mediana {F(Planes.Median([.. s.Select(x => (float)x)]), 2)})" : ""));
        }

        return parts.Count == 0 ? "NEMASURAT" : string.Join("; ", parts);
    }

    private static string ReadNoise(DarkSeriesResult r, int rp)
    {
        if (r.Biases is not BiasAnalysis b)
        {
            return "NEMASURAT: fara bias-uri";
        }

        string planes = string.Join("  ", Enumerable.Range(0, 4).Select(p => $"{b.PlaneNames[p]} {F(b.ReadNoise[p], 2)}"));
        string shot = r.Verdicts.Noise.NoiseOverRead is double s ? $"; zgomot temporal peste citire pe {b.PlaneNames[rp]} ~{F(s, 2)} ADU (sqrt(sigma_t^2 - citire^2): curent de intuneric si cuantizare)" : "";
        return $"{planes} ADU (din {Math.Max(b.Used - 1, 0)} perechi de bias-uri consecutive){shot}";
    }

    private static string Banding(NoiseVerdict v) =>
        double.IsNaN(v.RowBandingAdu)
            ? "NEMASURAT"
            : $"orizontale {F(v.RowBandingAdu, 3)} ADU rms pe rand (de {F(v.RowBandingRatio, 1)} ori zgomotul alb), verticale de {F(v.ColBandingRatio, 1)} ori";

    private static string Noise(NoiseVerdict v) => v.Verdict switch
    {
        Verdict.Unmeasured => "NEMASURAT",
        Verdict.Ok => $"OK: niciun cadru cu zgomot peste {F(Th.NoiseOutlierFactor, 1)} x mediana ({F(v.MedianSigma, 2)} ADU)",
        _ => $"ATENTIE: {v.OutlierFrames.Count} {(v.OutlierFrames.Count == 1 ? "cadru" : "cadre")} cu zgomot peste {F(Th.NoiseOutlierFactor, 1)} x mediana ({F(v.MedianSigma, 2)} ADU): {string.Join(", ", v.OutlierFrames.Select(i => i.ToString(CultureInfo.InvariantCulture)))}",
    };

    private static string Levels(DarkSeriesAnalysis d)
    {
        List<string> parts = [];
        for (int k = 0; k < Th.HotLevelsAdu.Count; k++)
        {
            int total = d.Master.Sum(m => m.Above[k]);
            parts.Add($"{F(Th.HotLevelsAdu[k], 0)}: {total}");
        }

        return string.Join("; ", parts) + $"  [{Eq(d)} peste negru, mediana seriei, toate planurile]";
    }

    private static string PerPlane(DarkSeriesAnalysis d, int level) =>
        string.Join("  ", d.Master.Select(m => $"{m.Name} {m.Above[level]}"));

    private static string Percentiles(HotStability h, double n, string eq) =>
        h.Hot == 0 ? "NEMASURAT: niciun pixel fierbinte" : $"mediana {F(h.P50 / n, 0)}, p10 {F(h.P10 / n, 0)}, p90 {F(h.P90 / n, 0)}, p99 {F(h.P99 / n, 0)} {eq} (din {h.Hot})";

    private static string Stability(DarkSeriesAnalysis d, int rp)
    {
        HotStability h = d.Hot;
        if (h.Hot == 0)
        {
            return "NEMASURAT";
        }

        return $"{h.Always} din {h.Hot} peste jumatate din prag in toate cele {RunReport.Frames(d.Used)}, {h.Flicker} lipsesc din 2 sau mai multe; sigma temporala {F(h.HotTemporalStd, 2)} ADU la cei fierbinti, {F(h.QuietTemporalStd, 2)} la restul; {h.RtsCandidates} candidati RTS pe {d.PlaneNames[rp]} (sigma temporala peste {F(Th.RtsSigmaFactor, 0)} x zgomotul unui cadru, {F(d.Noise.SigmaFrame, 2)})";
    }

    private static string StructuredBanding(DarkSeriesAnalysis d)
    {
        BandingFigures worstRow = d.Banding.MaxBy(b => b.RowP2p)!;
        BandingFigures worstCol = d.Banding.MaxBy(b => b.ColP2p)!;
        double n = d.Norm;
        return $"randuri {F(worstRow.RowStd / n, 3)} {Eq(d)} rms ({F(worstRow.RowP2p / n, 2)} varf la varf, {worstRow.Name}), coloane {F(worstCol.ColStd / n, 3)} ({F(worstCol.ColP2p / n, 2)}, {worstCol.Name})  [profilul masterului minus mediana glisanta pe {Th.BandingWindow}]";
    }

    private static string Glow(LeakVerdict l, PatternVerdict p) =>
        double.IsNaN(p.GlowAdu) ? "NEMASURAT" : $"{Signed(p.GlowAdu, 2)} ADU-eq la {RegionName(l.Where)} fata de centru ({(l.FromDarkMinusBias ? "dark - bias" : "masterul dark")})";

    private static string Pattern(PatternVerdict p) => p.Verdict switch
    {
        Verdict.Unmeasured => "NEMASURAT: harta sau profilele nu au valori",
        Verdict.Ok => $"OK: benzi sub {F(Th.BandingAdu, 1)} ADU-eq varf la varf si margini sub {F(Th.LeakAdu, 1)}",
        _ => $"ATENTIE: {(p.BandingP2pAdu > Th.BandingAdu ? $"benzi de {F(p.BandingP2pAdu, 2)} ADU-eq varf la varf" : "")}{(p.BandingP2pAdu > Th.BandingAdu && Math.Abs(p.GlowAdu) > Th.LeakAdu ? " si " : "")}{(Math.Abs(p.GlowAdu) > Th.LeakAdu ? $"glow de {Signed(p.GlowAdu, 2)} ADU-eq" : "")}; modelul ramane in lights fara dark-uri",
    };

    private static string DeadSat(DeadSatVerdict v) => v.Verdict == Verdict.Ok
        ? "OK: niciun pixel mort sau saturat pe master"
        : $"ATENTIE: {v.Dead} {(v.Dead == 1 ? "pixel mort" : "pixeli morti")}, {v.Saturated} {(v.Saturated == 1 ? "saturat" : "saturati")}";

    private static string Curve(NoiseVsN nv) =>
        nv.Curve.Count == 0
            ? "NEMASURAT: sub 2 cadre"
            : string.Join("; ", nv.Curve.Select(p => $"n={p.N} {F(p.Measured, 2)} (asteptat {F(p.Expected, 2)})"));

    private static string Enough(DarkSeriesAnalysis d, EnoughVerdict e)
    {
        if (double.IsNaN(e.MasterNoise))
        {
            return "NEMASURAT";
        }

        string basis = double.IsNaN(d.Noise.TemporalSigma) ? $"sigma {F(d.Noise.SigmaFrame, 2)}, un singur cadru" : $"sigma temporala {F(d.Noise.TemporalSigma, 2)}";
        string half = e.Frames >= 2 ? $"; mediana a {e.Frames - e.Frames / 2} fata de media a {e.Frames / 2}: {F(d.Noise.MedianHalfVsMeanHalf, 2)}" : "";
        return $"{RunReport.Frames(e.Frames)} -> {F(e.MasterNoise, 2)} ADU zgomot al masterului median ({F(Th.MedianNoiseFactor, 2)} x {basis} / sqrt({e.Frames})){half}";
    }

    private static string EnoughVerdictText(EnoughVerdict e) => e.Verdict switch
    {
        Verdict.Unmeasured => "NEMASURAT: un singur cadru nu e o serie",
        Verdict.Ok => $"OK: {RunReport.Frames(e.Frames)} ajung; zgomotul masterului median ({F(e.MasterNoise, 2)} ADU) e sub pasul de cuantizare ({e.QuantStep} ADU) si intra in cuadratura in fiecare light calibrat",
        _ when e.TooFew => $"ATENTIE: doar {RunReport.Frames(e.Frames)}; mediana cere cel putin {Th.MinDarks} ca un cadru ciudat sa n-o traga (zgomotul masterului ar fi {F(e.MasterNoise, 2)} ADU)",
        _ => $"ATENTIE: zgomotul masterului median ({F(e.MasterNoise, 2)} ADU) e peste pasul de cuantizare ({e.QuantStep} ADU); ar cobori sub pas de la {e.FramesNeeded} cadre",
    };

    private static string Ratio(double v) => double.IsNaN(v) ? "-" : F(v, 3);

    private static string Signed(double v, int decimals) => (v >= 0 ? "+" : "") + F(v, decimals);

    private static string Time(DateTime? t) => t is DateTime d ? d.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "-";

    private static string F(double v, int decimals) => double.IsNaN(v) ? "-" : RunReport.F(v, decimals);
}
