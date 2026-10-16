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
        w.WriteLine($"=== DARK-URI: {(d.Used == 1 ? "1 cadru decodat" : d.Used.ToString(CultureInfo.InvariantCulture) + " cadre decodate")} din {d.Frames.Count}, {d.Width} x {d.Height} px, scara {F(d.Black, 0)} .. {F(d.White, 0)} ADU ({d.ScaleSource}), CFA {d.CfaLetters ?? "necunoscut"} ===");
        w.WriteLine($"    normalizare        : {Normalisation(d)}");
        w.WriteLine($"    cuantizare         : pas {d.QuantStep} ADU langa negru; {d.UniqueValues} valori distincte in primul cadru ({g})");
        if (r.BiasError is not null)
        {
            w.WriteLine($"    ATENTIE            : bias-urile nu s-au putut folosi: {r.BiasError}");
        }

        BiasFrameFigures[] badBiases = r.Biases?.Frames.Where(f => f.Error is not null && !f.Skipped).ToArray() ?? [];
        if (badBiases.Length > 0)
        {
            w.WriteLine($"    ATENTIE            : {badBiases.Length} {(badBiases.Length == 1 ? "bias nu s-a putut decoda" : "bias-uri nu s-au putut decoda")} (primul: {badBiases[0].Info.FileName}: {badBiases[0].Error}); vezi {DarkOutputs.BiasesCsvName} cu --out");
        }

        int skippedBiases = r.Biases?.Frames.Count(f => f.Skipped) ?? 0;
        if (skippedBiases > 0)
        {
            w.WriteLine($"    bias-uri sarite    : {skippedBiases} din {r.Biases!.Frames.Count} (plafonul de cadre; folosite {r.Biases.Used})");
        }

        // 2
        w.WriteLine();
        w.WriteLine("  2. SCURGERI DE LUMINA SI GRADIENT");
        w.WriteLine($"    master dark        : {RegionsLine(d.MeanMaps[rp].Regions, n)}  [{g}, {eq} peste negru, media a {RunReport.Frames(d.Used)}, blocuri de {Th.BinPx * 2} px]");
        if (r.Biases is BiasAnalysis b)
        {
            w.WriteLine($"    master bias        : {RegionsLine(b.MeanMaps[rp].Regions, 1)}  [{g}, ADU peste negru, media a {RunReport.Frames(b.Used)}]");
        }

        if (r.DarkCurrent is DarkCurrent dc)
        {
            w.WriteLine($"    dark - bias        : {RegionsLine(dc.Maps[rp].Regions, n)}; mediana {F(dc.Median / n, 2)}, {F(dc.P1 / n, 2)} .. {F(dc.P99 / n, 2)} (p1 .. p99)");
        }

        w.WriteLine($"    verdict            : {Leak(v.Leak, eq)}");

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
        w.WriteLine($"    glow               : {Glow(v.Leak, v.Pattern, eq)}");
        w.WriteLine($"    verdict            : {Pattern(v.Pattern, eq)}");

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

        // 9
        w.WriteLine();
        w.WriteLine("  9. LIGHTS FATA DE DARK");
        WriteLights(r, w, eq);

        // the index
        w.WriteLine();
        w.WriteLine("  INDEX");
        WriteIndex(r, w);

        // the tables
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

        if (r.Lights is LightsAnalysis lights)
        {
            WriteLightsTable(lights, w, g, rp);
        }
    }

    private static void WriteLights(DarkSeriesResult r, TextWriter w, string eq)
    {
        if (r.Lights is not LightsAnalysis l)
        {
            w.WriteLine($"    verdict            : {(r.LightsError is null ? "NEMASURAT: sesiunea nu are lights" : "NEMASURAT: lights-urile nu s-au putut folosi: " + r.LightsError)}");
            return;
        }

        LightFigures[] bad = l.Frames.Where(f => f.Error is not null && !f.Skipped).ToArray();
        if (bad.Length > 0)
        {
            w.WriteLine($"    ATENTIE            : {bad.Length} {(bad.Length == 1 ? "light nu s-a putut decoda" : "lights nu s-au putut decoda")} (primul: {bad[0].Info.FileName}: {bad[0].Error}); vezi tabelul");
        }

        int skipped = l.Frames.Count(f => f.Skipped);
        if (skipped > 0)
        {
            w.WriteLine($"    lights sarite      : {skipped} din {l.Frames.Count} (plafonul de cadre; folosite {l.Used})");
        }

        if (l.AssumedSettings > 0)
        {
            w.WriteLine($"    ATENTIE            : {l.AssumedSettings} {(l.AssumedSettings == 1 ? "light nu spune ISO-ul sau expunerea; presupuse ale dark-urilor" : "lights nu spun ISO-ul sau expunerea; presupuse ale dark-urilor")} (normalizare x{F(r.Darks.Norm, 2)})");
        }

        w.WriteLine($"    pixeli fierbinti   : {Pixels(l.Persistent)} persistenti in lights (izolati, peste {F(Th.LightHotSigmas, 0)} sigma locale, in cel putin {l.PersistentThreshold} din {RunReport.Frames(l.Used)})");
        w.WriteLine($"    in masterul dark   : {string.Join("; ", l.Overlap.Select(o => $"peste {F(o.LevelAdu, 0)}: {o.Both} din {l.Persistent}" + (l.Persistent > 0 ? $" ({F(100.0 * o.Both / l.Persistent, 0)} %)" : "")))}  [{eq}]");
        w.WriteLine($"    master in lights   : {string.Join("; ", l.Overlap.Select(o => $"peste {F(o.LevelAdu, 0)}: {o.Both} din {o.DarkCount}" + (o.DarkCount > 0 ? $" ({F(100.0 * o.Both / o.DarkCount, 0)} %)" : "")))}");
        w.WriteLine($"    nepotriviti        : {(l.Missed == 0 ? "niciun pixel persistent in lights sub pragul masterului" : $"{l.Missed} {(l.Missed == 1 ? "pixel persistent" : "pixeli persistenti")} in lights sub {F(Th.HotAdu, 0)} {eq} in master (mediana lor in master {F(l.MissedDarkExcessMedian, 0)} {eq})")}");
        MatchFigures m = l.LightsOverDark;
        w.WriteLine($"    amplitudine        : {(m.Verdict == MatchVerdict.Unmeasured ? "NEMASURAT: niciun pixel fierbinte sub saturatie in master" : $"lights / dark {F(m.Ratio, 2)} pe noapte (mediana pe {RunReport.Frames(m.Pixels)}; primul {F(l.RatioFirst, 2)}, ultimul {F(l.RatioLast, 2)}; {Pixels(l.AmpPixels)})")}");
        w.WriteLine($"    verdict            : {MatchText(m, "dark-ul", "lights-urile")}");
    }

    private static void WriteIndex(DarkSeriesResult r, TextWriter w)
    {
        DarkSeriesAnalysis d = r.Darks;
        IndexFigures ix = r.Index;
        double mpx = d.Width * (double)d.Height / 1e6;
        w.WriteLine($"    dark-uri           : {ix.Tag} ({F(ix.Index, 1)} ADU-eq: mediana pixelilor de pe rangurile {F(Th.IndexRankLowPerMpx, 0)}-{F(Th.IndexRankHighPerMpx, 0)} pe Mpx, al {ix.RankLow + 1}-lea .. al {ix.RankHigh}-lea din {F(mpx, mpx < 1 ? 2 : 1)} Mpx, normalizat la ISO {F(Th.NormIso, 0)} si {F(Th.NormExposureSeconds, 0)} s)");
        w.WriteLine($"    eticheta           : {r.Label}");
        if (r.Lights is LightsAnalysis l && !double.IsNaN(r.LightsIndex))
        {
            w.WriteLine($"    lights             : echivalent idx{F(Math.Round(r.LightsIndex, MidpointRounding.AwayFromZero), 0)} (dark x {F(l.LightsOverDark.Ratio, 2)}, adica {Signed(l.LightsOverDark.DeltaCelsius, 1)} grade fata de dark)");
        }

        if (r.PartnerError is not null)
        {
            w.WriteLine($"    partener           : ATENTIE: {r.PartnerError}");
        }

        if (r.Partner is PartnerAnalysis p)
        {
            MatchFigures m = p.OwnOverPartner;
            string settings = p.Iso is double iso && p.ExposureSeconds is double e ? $"ISO {F(iso, 0)}, {InventoryReport.Exposure(e)} s" : "setari necunoscute";
            w.WriteLine($"    partener           : {p.Folder}: {p.Index.Tag} ({F(p.Index.Index, 1)} ADU-eq; {RunReport.Frames(p.Used)} din {p.Frames}, {settings}); eticheta {p.Label}");
            if (p.Failed > 0)
            {
                w.WriteLine($"    ATENTIE            : {p.Failed} {(p.Failed == 1 ? "dark al partenerului nu s-a putut folosi" : "dark-uri ale partenerului nu s-au putut folosi")} (primul: {p.FirstError})");
            }

            w.WriteLine($"    dark-uri / partener: {(m.Verdict == MatchVerdict.Unmeasured ? "NEMASURAT: niciun pixel comun" : $"{F(m.Ratio, 2)} (q25 {F(m.Q25, 2)}, q75 {F(m.Q75, 2)}, {(m.Pixels == 1 ? "1 pixel comun" : $"{m.Pixels} pixeli comuni")}) dupa normalizare, adica dark-urile sesiunii sunt cu {Degrees(m)} fata de partener")}");
            string lvl = F(Th.PartnerOverlapAdu, 0);
            w.WriteLine($"    pixeli comuni      : {p.OwnHot50InPartner50} din {p.OwnHot50} ai sesiunii peste {lvl} ADU-eq sunt peste {lvl} si la partener{(p.OwnHot50 > 0 ? $" ({F(100.0 * p.OwnHot50InPartner50 / p.OwnHot50, 0)} %)" : "")}; {p.PartnerHot50InOwn20} din {p.PartnerHot50} ai partenerului peste {lvl} sunt peste {F(Th.HotAdu, 0)} in sesiune{(p.PartnerHot50 > 0 ? $" ({F(100.0 * p.PartnerHot50InOwn20 / p.PartnerHot50, 0)} %)" : "")}");
            if (p.LightsOverPartner.Verdict != MatchVerdict.Unmeasured)
            {
                // lights over the partner, through the session's darks: (lights / dark) x (dark / partner)
                MatchFigures chained = p.LightsOverPartner;
                w.WriteLine($"    lights / partener  : {F(chained.Ratio, 2)} (prin dark-urile sesiunii), adica lights-urile sunt cu {Degrees(chained)} fata de partener");
                w.WriteLine($"    verdict partener   : {MatchText(chained, "partenerul", "lights-urile")}");
            }
            else
            {
                w.WriteLine($"    verdict partener   : {MatchText(m, "partenerul", "dark-urile sesiunii")} (dark-urile sesiunii fata de partener)");
            }
        }
    }

    /// <summary>"+2.3 grade", or "0.0 grade" when the ratio is not positive (no degrees from a ratio of 0).</summary>
    private static string Degrees(MatchFigures m) => double.IsNaN(m.DeltaCelsius) ? "0.0 grade" : Signed(m.DeltaCelsius, 1) + " grade";

    /// <summary>The words of a match verdict, <paramref name="master"/> being "dark-ul" or "partenerul" and <paramref name="set"/> the set measured against it ("lights-urile", "dark-urile sesiunii"): what Siril can do with it.</summary>
    internal static string MatchText(MatchFigures m, string master, string set)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (m.Verdict == MatchVerdict.Unmatched && (m.Ratio <= 0 || m.Q25 <= 0))
        {
            // the ratios sit around zero: the set shows none of the master's hot pixels
            return $"ATENTIE: nepotrivit: {set} nu arata pixelii fierbinti ai {(master == "dark-ul" ? "dark-ului" : "partenerului")} (raport {F(m.Ratio, 2)}; reducere de zgomot in aparat, alt aparat sau alta orientare?)";
        }

        string where = Math.Abs(m.DeltaCelsius) < Th.SameTemperatureCelsius
            ? $"{master} e la aceeasi temperatura"
            : $"{master} e {(m.DeltaCelsius < 0 ? "mai cald" : "mai rece")} cu {F(Math.Abs(m.DeltaCelsius), 1)} grade";
        return m.Verdict switch
        {
            MatchVerdict.Unmeasured => "NEMASURAT",
            MatchVerdict.Direct => $"OK: scadere directa (raport {F(m.Ratio, 2)}, in {F(Th.MatchDirectLow, 1)} .. {F(Th.MatchDirectHigh, 1)}; {where})",
            MatchVerdict.WithOpt => $"ATENTIE: scade cu -opt (raport {F(m.Ratio, 2)}, in {F(Th.MatchOptLow, 1)} .. {F(Th.MatchOptHigh, 1)}; {where}, -opt scaleaza amplitudinea)",
            MatchVerdict.CosmeticOnly => $"ATENTIE: doar corectie cosmetica (-cc=dark): raport {F(m.Ratio, 2)}, {where} si vede toti pixelii fierbinti, dar scaderea l-ar supracorecta",
            _ => $"ATENTIE: nepotrivit (raport {F(m.Ratio, 2)}: {where}; {(m.Ratio > 1 ? "prea rece, pierde pixelii fierbinti slabi" : "prea cald")})",
        };
    }

    private static void WriteLightsTable(LightsAnalysis l, TextWriter w, string g, int rp)
    {
        w.WriteLine();
        w.WriteLine("  TABEL PE LIGHTS");
        w.WriteLine($"     nr  fisier                ora         cer   sigma  candidati  izolati  amplit.    q25    q75   [{g}; cer = ADU peste negru; sigma = a excesului local, scara detectorului; amplit. = lights / dark la pixelii fierbinti ai masterului]");
        foreach (LightFigures f in l.Frames)
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

            w.WriteLine($"    {f.Info.Index,3}  {name,-20}  {Time(f.Info.Timestamp),-8}  {F(f.SkyMedian[rp], 1),6}  {F(f.LocalSigma[rp], 2),6}  {f.Candidates,9}  {f.IsolatedHot,7}  {Ratio(f.AmpRatioMedian),7}  {Ratio(f.AmpRatioQ25),5}  {Ratio(f.AmpRatioQ75),5}");
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

    private static string Leak(LeakVerdict l, string eq)
    {
        string source = l.FromDarkMinusBias ? "pe dark - bias" : "pe masterul dark (fara bias)";
        return l.Verdict switch
        {
            Verdict.Unmeasured => "NEMASURAT: harta nu are valori (cadre mai mici de un bloc)",
            Verdict.Ok => $"OK: nicio margine sau colt la mai mult de {F(Th.LeakAdu, 1)} {eq} de centru (cel mai departe {Signed(l.OffsetAdu, 2)} la {RegionName(l.Where)}, {source})",
            _ => $"ATENTIE: {RegionName(l.Where)} e la {Signed(l.OffsetAdu, 2)} {eq} de centru ({source}); peste {F(Th.LeakAdu, 1)}: {(l.OffsetAdu > 0 ? "scurgere de lumina sau glow" : "margine mai intunecata decat centrul, offset de citire sau bias nepotrivit")}",
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
        if (d.Used < 2)
        {
            return "NEMASURAT: un singur cadru nu are rezidual fata de propria mediana";
        }

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
            return "NEMASURAT: niciun pixel fierbinte";
        }

        if (d.Used < 2)
        {
            return "NEMASURAT: stabilitatea cere cel putin 2 cadre";
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

    private static string Glow(LeakVerdict l, PatternVerdict p, string eq) =>
        double.IsNaN(p.GlowAdu) ? "NEMASURAT" : $"{Signed(p.GlowAdu, 2)} {eq} la {RegionName(l.Where)} fata de centru ({(l.FromDarkMinusBias ? "dark - bias" : "masterul dark")})";

    private static string Pattern(PatternVerdict p, string eq) => p.Verdict switch
    {
        Verdict.Unmeasured => "NEMASURAT: harta sau profilele nu au valori",
        Verdict.Ok => $"OK: benzi sub {F(Th.BandingAdu, 1)} {eq} varf la varf si margini sub {F(Th.LeakAdu, 1)}",
        _ => $"ATENTIE: {(p.BandingP2pAdu > Th.BandingAdu ? $"benzi de {F(p.BandingP2pAdu, 2)} {eq} varf la varf" : "")}{(p.BandingP2pAdu > Th.BandingAdu && Math.Abs(p.GlowAdu) > Th.LeakAdu ? " si " : "")}{(Math.Abs(p.GlowAdu) > Th.LeakAdu ? $"glow de {Signed(p.GlowAdu, 2)} {eq}" : "")}; modelul ramane in lights fara dark-uri",
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

    private static string Signed(double v, int decimals)
    {
        double r = Math.Round(v, decimals, MidpointRounding.AwayFromZero);
        return (r < 0 ? "-" : "+") + F(Math.Abs(r), decimals);
    }

    private static string Pixels(int n) => n == 1 ? "1 pixel" : n.ToString(CultureInfo.InvariantCulture) + " pixeli";

    private static string Time(DateTime? t) => t is DateTime d ? d.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "-";

    /// <summary>A figure with fixed decimals; a value that rounds to zero prints without a minus sign.</summary>
    private static string F(double v, int decimals)
    {
        if (double.IsNaN(v))
        {
            return "-";
        }

        double r = Math.Round(v, decimals);
        return RunReport.F(r == 0 ? 0 : v, decimals);
    }
}
