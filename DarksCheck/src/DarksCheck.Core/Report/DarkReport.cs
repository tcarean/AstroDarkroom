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

/// <summary>
/// The darks report (Romanian, R-025 to R-065): the header rows of the darks' stack, the
/// sections of checks 2 to 9 and INDEX as labelled rows (<see cref="Sections"/>), and the
/// two tables as rows of formatted cells (<see cref="FrameRows"/>, <see cref="LightRows"/>);
/// <see cref="Write"/> prints exactly those for the console, the window binds them
/// (ADR 0009). Words only; the numbers come from the run.
/// </summary>
internal static class DarkReport
{
    internal static void Write(DarkSeriesResult r, TextWriter w)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(w);
        InventoryReport.WriteHeader(r.Inventory, w);
        w.WriteLine();
        w.WriteLine(HeaderLine(r));
        foreach (ReportRow row in HeaderRows(r))
        {
            WriteRow(w, row);
        }

        foreach (ReportSection section in Sections(r))
        {
            w.WriteLine();
            w.WriteLine("  " + section.Title);
            foreach (ReportRow row in section.Rows)
            {
                WriteRow(w, row);
            }
        }

        w.WriteLine();
        w.WriteLine("  TABEL PE CADRE");
        w.WriteLine(FrameTableHeader(r));
        foreach (DarkTableRow f in FrameRows(r))
        {
            w.WriteLine(FrameLine(f));
        }

        if (r.Lights is not null)
        {
            w.WriteLine();
            w.WriteLine("  TABEL PE LIGHTS");
            w.WriteLine(LightTableHeader(r));
            foreach (LightTableRow f in LightRows(r))
            {
                string name = Trim20(f.FileName);
                if (f.Skipped is not null)
                {
                    w.WriteLine($"    {f.Index,3}  {name,-20}  {f.Skipped}");
                }
                else if (f.Error is not null)
                {
                    w.WriteLine($"    {f.Index,3}  {name,-20}  eroare: {f.Error}");
                }
                else
                {
                    w.WriteLine($"    {f.Index,3}  {name,-20}  {f.Time,-8}  {f.Sky,6}  {f.LocalSigma,6}  {f.Candidates,9}  {f.Isolated,7}  {f.Amp,7}  {f.Q25,5}  {f.Q75,5}");
                }
            }
        }

        ScaleReport.Write(r, w);
    }

    private static void WriteRow(TextWriter w, ReportRow row) => w.WriteLine($"    {row.Label,-19}: {row.Value}");

    private static string Trim20(string name) => name.Length > 20 ? name[..20] : name;

    /// <summary>The `=== DARK-URI ... ===` line: frames decoded, size, scale and CFA.</summary>
    internal static string HeaderLine(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DarkSeriesAnalysis d = r.Darks;
        string whose = r.DarksFromPartner is string p ? $" (ale partenerului {p})" : "";
        return $"=== DARK-URI{whose}: {(d.Used == 1 ? "1 cadru decodat" : d.Used.ToString(CultureInfo.InvariantCulture) + " cadre decodate")} din {d.Frames.Count}, {d.Width} x {d.Height} px, scara {F(d.Black, 0)} .. {F(d.White, 0)} ADU ({d.ScaleSource}), CFA {d.CfaLetters ?? "necunoscut"} ===";
    }

    /// <summary>The rows under the header line: the normalisation, the quantisation, the biases' warnings.</summary>
    internal static IReadOnlyList<ReportRow> HeaderRows(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DarkSeriesAnalysis d = r.Darks;
        List<ReportRow> rows =
        [
            new("normalizare", Normalisation(d)),
            new("cuantizare", $"pas {d.QuantStep} ADU langa negru; {d.UniqueValues} valori distincte in primul cadru ({d.PlaneNames[r.ReferencePlane]})"),
        ];
        if (r.BiasError is not null)
        {
            rows.Add(new("ATENTIE", $"bias-urile nu s-au putut folosi: {r.BiasError}"));
        }

        BiasFrameFigures[] badBiases = r.Biases?.Frames.Where(f => f.Error is not null && !f.Skipped).ToArray() ?? [];
        if (badBiases.Length > 0)
        {
            rows.Add(new("ATENTIE", $"{badBiases.Length} {(badBiases.Length == 1 ? "bias nu s-a putut decoda" : "bias-uri nu s-au putut decoda")} (primul: {badBiases[0].Info.FileName}: {badBiases[0].Error}); vezi {DarkOutputs.BiasesCsvName} cu --out"));
        }

        int skippedBiases = r.Biases?.Frames.Count(f => f.Skipped) ?? 0;
        if (skippedBiases > 0)
        {
            rows.Add(new("bias-uri sarite", $"{skippedBiases} din {r.Biases!.Frames.Count} (plafonul de cadre; folosite {r.Biases.Used})"));
        }

        return rows;
    }

    /// <summary>Checks 2 to 9 and INDEX, each as its titled rows, in the console's order.</summary>
    internal static IReadOnlyList<ReportSection> Sections(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DarkSeriesAnalysis d = r.Darks;
        DarkVerdicts v = r.Verdicts;
        int rp = r.ReferencePlane;
        string g = d.PlaneNames[rp];
        string eq = Eq(d);
        double n = d.Norm;
        double mpx = d.Width * (double)d.Height / 1e6;

        double masterNoise = d.Noise.MasterNoise;
        StackNoise? stack = StackNoise.Of(r.Lights, rp, masterNoise);
        List<ReportRow> leak = [new("master dark", $"{RegionsLine(d.MeanMaps[rp].Regions, n)}  [{g}, {eq} peste negru, media a {RunReport.Frames(d.Used)}, blocuri de {Th.BinPx * 2} px]")];
        if (r.Biases is BiasAnalysis b)
        {
            leak.Add(new("master bias", $"{RegionsLine(b.MeanMaps[rp].Regions, 1)}  [{g}, ADU peste negru, media a {RunReport.Frames(b.Used)}]"));
        }

        if (r.DarkCurrent is DarkCurrent dc)
        {
            leak.Add(new("dark - bias", $"{RegionsLine(dc.Maps[rp].Regions, n)}; mediana {F(dc.Median / n, 2)}, {F(dc.P1 / n, 2)} .. {F(dc.P99 / n, 2)} (p1 .. p99)"));
        }

        leak.Add(FrameLightRow(v.Leak, eq, n, masterNoise, stack));
        leak.Add(new("glow / constant", SplitRow(v.Leak, eq, n)));
        leak.Add(SidesRow(v.Leak, eq, n, masterNoise, stack));
        leak.Add(LeakVerdictRow(v.Leak, eq, n, masterNoise));

        return
        [
            new("2. SCURGERI DE LUMINA SI GRADIENT", leak),
            new("3. DERIVA TERMICA IN SERIE",
            [
                new("amplitudine", Amplitude(d)),
                new("platou", PlateauRow(d.Warming, d.Normalisation)),
                new("de aruncat", OffPlateauRow(d.Warming)),
                new("verdict", Drift(v.Drift, d.Normalisation)),
            ]),
            new("4. ZGOMOT PE CADRU",
            [
                new("sigma rezidual", $"{ResidualRange(d, rp)}  [ADU, cadru - master]"),
                new("zgomot de citire", ReadNoise(r, rp)),
                new("benzi aleatoare", Banding(v.Noise)),
                new("verdict", Noise(v.Noise)),
            ]),
            new("5. PIXELI FIERBINTI",
            [
                new("master peste nivel", Levels(d)),
                new($"pe plan la {F(Th.HotAdu, 0)}", PerPlane(d, Th.HotLevelIndex)),
                new("amplitudine", Percentiles(d.Hot, n, eq)),
                new("stabilitate", Stability(d, rp)),
                new("verdict", $"OK (informativ): {d.Hot.Hot} pixeli peste {F(Th.HotAdu, 0)} {eq} in {F(mpx, mpx < 1 ? 2 : 1)} Mpx"),
            ]),
            new("6. MODEL FIX",
            [
                new("benzi structurate", StructuredBanding(d)),
                new("glow", Glow(v.Leak, v.Pattern, eq)),
                new("verdict", Pattern(v.Pattern, eq)),
            ]),
            new("7. PIXELI MORTI SI SATURATI",
            [
                new("master", $"{v.DeadSat.Dead} sub negru - {F(Th.DeadBelowAdu, 0)} ADU, {v.DeadSat.Saturated} la alb ({F(d.White, 0)})"),
                new("verdict", DeadSat(v.DeadSat)),
            ]),
            new("8. CATE DARK-URI AJUNG",
            [
                new("zgomotul mediei", $"{Curve(d.Noise)}  [{g}, centrul de {d.Noise.CropPx} px, ADU, sigma temporala = cadru fata de cadru]"),
                new("master", Enough(d, v.Enough), double.IsNaN(v.Enough.MasterNoise) ? null : [$"{F(v.Enough.MasterNoise, 2)} ADU"]),
                new("verdict", EnoughVerdictText(v.Enough)),
            ]),
            new("9. LIGHTS FATA DE DARK", LightsRows(r, eq)),
            new("INDEX", IndexRows(r)),
        ];
    }

    private static List<ReportRow> LightsRows(DarkSeriesResult r, string eq)
    {
        if (r.Lights is not LightsAnalysis l)
        {
            return [new("verdict", r.LightsError is null ? "NEMASURAT: sesiunea nu are lights" : "NEMASURAT: lights-urile nu s-au putut folosi: " + r.LightsError)];
        }

        List<ReportRow> rows = [];
        LightFigures[] bad = l.Frames.Where(f => f.Error is not null && !f.Skipped).ToArray();
        if (bad.Length > 0)
        {
            rows.Add(new("ATENTIE", $"{bad.Length} {(bad.Length == 1 ? "light nu s-a putut decoda" : "lights nu s-au putut decoda")} (primul: {bad[0].Info.FileName}: {bad[0].Error}); vezi tabelul"));
        }

        int skipped = l.Frames.Count(f => f.Skipped);
        if (skipped > 0)
        {
            rows.Add(new("lights sarite", $"{skipped} din {l.Frames.Count} (plafonul de cadre; folosite {l.Used})"));
        }

        if (l.AssumedSettings > 0)
        {
            rows.Add(new("ATENTIE", $"{l.AssumedSettings} {(l.AssumedSettings == 1 ? "light nu spune ISO-ul sau expunerea; presupuse ale dark-urilor" : "lights nu spun ISO-ul sau expunerea; presupuse ale dark-urilor")} (normalizare x{F(r.Darks.Norm, 2)})"));
        }

        rows.Add(new("pixeli fierbinti", $"{Pixels(l.Persistent)} persistenti in lights (izolati, peste {F(Th.LightHotSigmas, 0)} sigma locale, in cel putin {l.PersistentThreshold} din {RunReport.Frames(l.Used)})"));
        rows.Add(new("in masterul dark", $"{string.Join("; ", l.Overlap.Select(o => $"peste {F(o.LevelAdu, 0)}: {o.Both} din {l.Persistent}" + (l.Persistent > 0 ? $" ({F(100.0 * o.Both / l.Persistent, 0)} %)" : "")))}  [{eq}]"));
        rows.Add(new("master in lights", string.Join("; ", l.Overlap.Select(o => $"peste {F(o.LevelAdu, 0)}: {o.Both} din {o.DarkCount}" + (o.DarkCount > 0 ? $" ({F(100.0 * o.Both / o.DarkCount, 0)} %)" : "")))));
        rows.Add(new("nepotriviti", l.Missed == 0 ? "niciun pixel persistent in lights sub pragul masterului" : $"{l.Missed} {(l.Missed == 1 ? "pixel persistent" : "pixeli persistenti")} in lights sub {F(Th.HotAdu, 0)} {eq} in master (mediana lor in master {F(l.MissedDarkExcessMedian, 0)} {eq})"));
        MatchFigures m = l.LightsOverDark;
        rows.Add(new("amplitudine", m.Verdict == MatchVerdict.Unmeasured ? "NEMASURAT: niciun pixel fierbinte sub saturatie in master" : $"lights / dark {F(m.Ratio, 2)} pe noapte (mediana pe {RunReport.Frames(m.Pixels)}; primul {F(l.RatioFirst, 2)}, ultimul {F(l.RatioLast, 2)}; {Pixels(l.AmpPixels)})"));
        rows.Add(new("verdict", MatchText(m, "dark-ul", "lights-urile")));
        return rows;
    }

    private static List<ReportRow> IndexRows(DarkSeriesResult r)
    {
        DarkSeriesAnalysis d = r.Darks;
        IndexFigures ix = r.Index;
        double mpx = d.Width * (double)d.Height / 1e6;
        List<ReportRow> rows =
        [
            new(r.DarksFromPartner is null ? "dark-uri" : "dark-uri (partener)", $"{ix.Tag} ({F(ix.Index, 1)} ADU-eq: mediana pixelilor de pe rangurile {F(Th.IndexRankLowPerMpx, 0)}-{F(Th.IndexRankHighPerMpx, 0)} pe Mpx, al {ix.RankLow + 1}-lea .. al {ix.RankHigh}-lea din {F(mpx, mpx < 1 ? 2 : 1)} Mpx, normalizat la ISO {F(Th.NormIso, 0)} si {F(Th.NormExposureSeconds, 0)} s)"),
            new("eticheta", r.Label),
        ];
        if (r.Lights is LightsAnalysis l && !double.IsNaN(r.LightsIndex))
        {
            rows.Add(new("lights", $"echivalent idx{F(Math.Round(r.LightsIndex, MidpointRounding.AwayFromZero), 0)} (dark x {F(l.LightsOverDark.Ratio, 2)}, adica {Signed(l.LightsOverDark.DeltaCelsius, 1)} grade fata de dark)"));
        }

        if (r.PartnerError is not null)
        {
            rows.Add(new("partener", $"ATENTIE: {r.PartnerError}"));
        }

        if (r.Partner is PartnerAnalysis p)
        {
            MatchFigures m = p.OwnOverPartner;
            string settings = p.Iso is double iso && p.ExposureSeconds is double e ? $"ISO {F(iso, 0)}, {InventoryReport.Exposure(e)} s" : "setari necunoscute";
            rows.Add(new("partener", $"{p.Folder}: {p.Index.Tag} ({F(p.Index.Index, 1)} ADU-eq; {RunReport.Frames(p.Used)} din {p.Frames}, {settings}); eticheta {p.Label}"));
            if (p.Failed > 0)
            {
                rows.Add(new("ATENTIE", $"{p.Failed} {(p.Failed == 1 ? "dark al partenerului nu s-a putut folosi" : "dark-uri ale partenerului nu s-au putut folosi")} (primul: {p.FirstError})"));
            }

            rows.Add(new("dark-uri / partener", m.Verdict == MatchVerdict.Unmeasured ? "NEMASURAT: niciun pixel comun" : $"{F(m.Ratio, 2)} (q25 {F(m.Q25, 2)}, q75 {F(m.Q75, 2)}, {(m.Pixels == 1 ? "1 pixel comun" : $"{m.Pixels} pixeli comuni")}) dupa normalizare, adica dark-urile sesiunii sunt cu {Degrees(m)} fata de partener"));
            string lvl = F(Th.PartnerOverlapAdu, 0);
            rows.Add(new("pixeli comuni", $"{p.OwnHot50InPartner50} din {p.OwnHot50} ai sesiunii peste {lvl} ADU-eq sunt peste {lvl} si la partener{(p.OwnHot50 > 0 ? $" ({F(100.0 * p.OwnHot50InPartner50 / p.OwnHot50, 0)} %)" : "")}; {p.PartnerHot50InOwn20} din {p.PartnerHot50} ai partenerului peste {lvl} sunt peste {F(Th.HotAdu, 0)} in sesiune{(p.PartnerHot50 > 0 ? $" ({F(100.0 * p.PartnerHot50InOwn20 / p.PartnerHot50, 0)} %)" : "")}"));
            if (p.LightsOverPartner.Verdict != MatchVerdict.Unmeasured)
            {
                // lights over the partner, through the session's darks: (lights / dark) x (dark / partner)
                MatchFigures chained = p.LightsOverPartner;
                rows.Add(new("lights / partener", $"{F(chained.Ratio, 2)} (prin dark-urile sesiunii), adica lights-urile sunt cu {Degrees(chained)} fata de partener"));
                rows.Add(new("verdict partener", MatchText(chained, "partenerul", "lights-urile")));
            }
            else
            {
                rows.Add(new("verdict partener", $"{MatchText(m, "partenerul", "dark-urile sesiunii")} (dark-urile sesiunii fata de partener)"));
            }
        }

        return rows;
    }

    /// <summary>The frames table's header line: the column names, then <see cref="FrameTableNote"/>.</summary>
    internal static string FrameTableHeader(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return $"     nr  fisier                ora       medie   sigma  rezid.   benzi  amplit.  platou  lumina  latura  >{F(Th.HotAdu, 0)}   {FrameTableNote(r)}";
    }

    /// <summary>One line of the frames table as the console prints it: the cells in their columns, the note of a frame off the plateau after the last one; a skipped or failed frame prints its reason instead of cells.</summary>
    internal static string FrameLine(DarkTableRow f)
    {
        ArgumentNullException.ThrowIfNull(f);
        string name = Trim20(f.FileName);
        if (f.Skipped is not null)
        {
            return $"    {f.Index,3}  {name,-20}  {f.Skipped}";
        }

        if (f.Error is not null)
        {
            return $"    {f.Index,3}  {name,-20}  eroare: {f.Error}";
        }

        string note = f.Note is null ? "" : $"   {f.Note}";
        return $"    {f.Index,3}  {name,-20}  {f.Time,-8}  {f.Mean,6}  {f.Sigma,6}  {f.Residual,6}  {f.Bands,6}  {f.Amp,7}  {f.Plateau,6}  {f.Light,6}  {f.Side,6}  {f.Above,4}{note}";
    }

    /// <summary>The unit note of the frames table: the plane and what the columns mean.</summary>
    internal static string FrameTableNote(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        string eq = Eq(r.Darks);
        string side = r.Verdicts.Leak.WorstSide is SideLight w
            ? $"latura = {SideName(w.Side)} - centru{(w.RemainderAdu is null ? " (fara bias, glow inclus)" : " dupa bias (glow inclus)")}, {eq}"
            : "latura = latura cu cea mai multa lumina constanta - centru (NEMASURAT)";
        return $"[{r.Darks.PlaneNames[r.ReferencePlane]}; medie = ADU peste negru; benzi = randuri / zgomot alb; amplit. = pixelii fierbinti fata de master; platou = grade fata de platoul seriei; de aruncat = la peste {F(Th.OffPlateauCelsius, 1)} grade de platou (verificarea 3); lumina = {eq} peste dreapta mediei pe incalzire; {side}]";
    }

    /// <summary>The sentence under the window's warming chart (R-071): what the points are, and with a fit the plateau and the band outside which a frame is one to discard.</summary>
    internal static string WarmingChartNote(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Warming w = r.Darks.Warming;
        const string what = "Amplitudinea pixelilor fierbinti ai masterului in fiecare cadru, fata de master: panta e incalzirea camerei in timpul seriei (verificarea 3).";
        return w.HasFit
            ? $"{what} Platoul seriei e la {F(w.Plateau, 2)} din master; cadrele sub {F(w.OffPlateauLow, 2)} sau peste {F(w.OffPlateauHigh, 2)} (la peste {F(Th.OffPlateauCelsius, 1)} grade de platou) sunt de aruncat si apar in culoarea de atentie."
            : $"{what} Sub {Th.WarmingFitMinFrames} cadre cu pixeli fierbinti nu se potriveste un platou.";
    }

    /// <summary>One row per dark, the cells formatted as the console prints them, on the reference plane.</summary>
    internal static IReadOnlyList<DarkTableRow> FrameRows(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        int rp = r.ReferencePlane;
        List<DarkTableRow> rows = [];
        IReadOnlyList<double>? excess = r.Verdicts.Leak.Frames?.ExcessAdu;
        SideLight? worst = r.Verdicts.Leak.WorstSide;
        Regions? biasRegions = r.Biases?.MeanMaps[rp].Regions;
        double biasSide = worst is null || biasRegions is null ? 0 : LeakAnalyzer.SideOffset(biasRegions, worst.Side);
        Warming wm = r.Darks.Warming;
        Normalisation nz = r.Darks.Normalisation;
        for (int k = 0; k < r.Darks.Frames.Count; k++)
        {
            DarkFrameFigures f = r.Darks.Frames[k];
            if (f.Skipped)
            {
                rows.Add(new(f.Info.Index, f.Info.FileName, f.Error, null, "", "", "", "", "", "", "", "", "", ""));
                continue;
            }

            if (f.Error is not null)
            {
                rows.Add(new(f.Info.Index, f.Info.FileName, null, f.Error, "", "", "", "", "", "", "", "", "", ""));
                continue;
            }

            PlaneFigures p = f.Planes[rp];
            ResidualFigures res = f.Residuals[rp];
            double light = excess is null ? double.NaN : excess[k];
            string side = worst is null || f.Edges is null ? "-" : Signed((LeakAnalyzer.SideOffset(f.Edges, worst.Side) - biasSide) / r.Darks.Norm, 2);
            bool measured = wm.HasFit && !double.IsNaN(f.AmpRatioMedian) && f.AmpRatioMedian > 0;
            string plateau = measured ? Signed(nz.Degrees(f.AmpRatioMedian / wm.Plateau), 1) : "-";
            string? note = null;
            if (measured && (f.AmpRatioMedian < wm.OffPlateauLow || f.AmpRatioMedian > wm.OffPlateauHigh))
            {
                bool under = f.AmpRatioMedian < wm.OffPlateauLow;
                note = $"de aruncat: amplit. {Ratio(f.AmpRatioMedian)} {(under ? "sub" : "peste")} {F(under ? wm.OffPlateauLow : wm.OffPlateauHigh, 2)} (peste {F(Th.OffPlateauCelsius, 1)} grade {(under ? "sub" : "peste")} platoul {F(wm.Plateau, 2)})";
            }

            rows.Add(new(f.Info.Index, f.Info.FileName, null, null, Time(f.Info.Timestamp), F(f.MeanAboveBlack, 2), F(p.Sigma, 2), F(res.Sigma, 2), F(res.RowBandingRatio, 1), Ratio(f.AmpRatioMedian), plateau, double.IsNaN(light) ? "-" : Signed(light, 2), side, p.Above[Th.HotLevelIndex].ToString(CultureInfo.InvariantCulture), note));
        }

        return rows;
    }

    /// <summary>The lights table's header line: the column names, then <see cref="LightTableNote"/>.</summary>
    internal static string LightTableHeader(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return $"     nr  fisier                ora         cer   sigma  candidati  izolati  amplit.    q25    q75   {LightTableNote(r)}";
    }

    /// <summary>The unit note of the lights table: the plane and what the columns mean.</summary>
    internal static string LightTableNote(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return $"[{r.Darks.PlaneNames[r.ReferencePlane]}; cer = ADU peste negru; sigma = a excesului local, scara detectorului; amplit. = lights / dark la pixelii fierbinti ai masterului]";
    }

    /// <summary>One row per light, the cells formatted as the console prints them, on the reference plane; empty without lights.</summary>
    internal static IReadOnlyList<LightTableRow> LightRows(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (r.Lights is not LightsAnalysis l)
        {
            return [];
        }

        int rp = r.ReferencePlane;
        List<LightTableRow> rows = [];
        foreach (LightFigures f in l.Frames)
        {
            if (f.Skipped)
            {
                rows.Add(new(f.Info.Index, f.Info.FileName, f.Error, null, "", "", "", "", "", "", "", ""));
                continue;
            }

            if (f.Error is not null)
            {
                rows.Add(new(f.Info.Index, f.Info.FileName, null, f.Error, "", "", "", "", "", "", "", ""));
                continue;
            }

            rows.Add(new(f.Info.Index, f.Info.FileName, null, null, Time(f.Info.Timestamp), F(f.SkyMedian[rp], 1), F(f.LocalSigma[rp], 2), f.Candidates.ToString(CultureInfo.InvariantCulture), f.IsolatedHot.ToString(CultureInfo.InvariantCulture), Ratio(f.AmpRatioMedian), Ratio(f.AmpRatioQ25), Ratio(f.AmpRatioQ75)));
        }

        return rows;
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
        string gain = d.Normalisation.IsoFactorPerStop == 2 ? "" : $"; castig x{F(d.Normalisation.IsoFactorPerStop, 2)} pe treapta de ISO";
        return Math.Abs(d.Norm - 1) < 1e-9
            ? $"{from} -> x1.00 fata de ISO {F(Th.NormIso, 0)} si {F(Th.NormExposureSeconds, 0)} s (ADU-eq = ADU){gain}"
            : $"{from} -> x{F(d.Norm, 2)} fata de ISO {F(Th.NormIso, 0)} si {F(Th.NormExposureSeconds, 0)} s (1 ADU-eq = {F(d.Norm, 2)} ADU){gain}";
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

    /// <summary>Check 2's verdict row: the words of <see cref="Leak"/>, with the constant light and the master's noise marked bold when the verdict names them (ADR 0016).</summary>
    private static ReportRow LeakVerdictRow(LeakVerdict l, string eq, double norm, double masterNoise)
    {
        string against = "";
        List<string> bold = [];
        if (l.Verdict == Verdict.Warning && l.ConstantLight is SideLight c)
        {
            (against, IReadOnlyList<string> b) = ScaleReport.AgainstMasters(c.LightAdu * norm, masterNoise, null);
            bold.Add($"{Signed(c.LightAdu, 2)} {eq}");
            bold.AddRange(b);
        }

        return new("verdict", Leak(l, eq, against), bold.Count == 0 ? null : bold);
    }

    /// <summary>The verdict's words; <paramref name="against"/> is the constant light's ratio to the master's noise, already worded, or empty.</summary>
    private static string Leak(LeakVerdict l, string eq, string against)
    {
        string source = l.FromDarkMinusBias ? "pe dark - bias" : "pe masterul dark (fara bias)";
        string framesTail = l.Frames is null ? "lumina pe cadre NEMASURATA" : "niciun cadru cu lumina peste incalzirea seriei";
        if (l.Verdict == Verdict.Unmeasured)
        {
            return "NEMASURAT: harta nu are valori (cadre mai mici de un bloc)";
        }

        if (l.Verdict == Verdict.Ok)
        {
            string sidesTail = l.SidesAfterBias ? $"; nicio latura cu lumina constanta peste {F(Th.LeakConstantAdu, 2)} si {F(Th.LeakConstantSigmas, 0)} x eroarea" : "";
            return $"OK: nicio margine sau colt la mai mult de {F(Th.LeakAdu, 1)} {eq} de centru (cel mai departe {Signed(l.OffsetAdu, 2)} la {RegionName(l.Where)}, {source}); {framesTail}{sidesTail}";
        }

        SideLight? constant = l.ConstantLight;
        List<string> parts = [];
        if (l.Frames is FrameLight fl && fl.LitFrames.Count > 0)
        {
            int n = fl.LitFrames.Count;
            parts.Add($"{n} {(n == 1 ? "cadru are" : "cadre au")} lumina in plus fata de restul seriei ({LitList(fl, ": ")} {eq}); {(constant is null ? "scoate-le din darks\\ sau refa seria" : "refa seria")}");
        }

        if (constant is SideLight c)
        {
            parts.Add($"lumina constanta in toate cadrele: {Signed(c.LightAdu, 2)} {eq} (±{F(c.ErrorAdu, 2)}) pe latura de {SideName(c.Side)} fata de centru, nu e in bias: scurgere de lumina (ocular, montura)" + (against.Length == 0 ? "" : $"; adica {against}"));
        }

        if (Math.Abs(l.OffsetAdu) > Th.LeakAdu)
        {
            parts.Add($"{RegionName(l.Where)} e la {Signed(l.OffsetAdu, 2)} {eq} de centru ({source}); peste {F(Th.LeakAdu, 1)}: {(l.OffsetAdu > 0 ? CauseWords(l) : "margine mai intunecata decat centrul, offset de citire sau bias nepotrivit")}");
        }

        return "ATENTIE: " + string.Join("; in plus ", parts);
    }

    /// <summary>The row `lumina constanta` (R-029, ADR 0014): the four sides after the bias, or their constants without biases; a side over the threshold against the masters' noise, bold (ADR 0016).</summary>
    private static ReportRow SidesRow(LeakVerdict l, string eq, double norm, double masterNoise, StackNoise? stack)
    {
        const string label = "lumina constanta";
        if (SplitUnmeasured(l, Th.LeakSidesMinFrames) is string why)
        {
            return new(label, why);
        }

        if (l.Sides.Count == 0)
        {
            return new(label, "NEMASURAT");
        }

        MapSide[] order = [MapSide.Top, MapSide.Bottom, MapSide.Left, MapSide.Right];
        if (!l.SidesAfterBias)
        {
            return new(label, "fara bias nu se desparte de offsetul de citire: constant " + string.Join(", ", order.Select(s => $"{SideName(s)} {Signed(l.Sides.First(x => x.Side == s).ConstantAdu, 2)}")) + $" {eq}");
        }

        string sides = string.Join(", ", order.Select(s => l.Sides.First(x => x.Side == s)).Select(s => $"{SideName(s.Side)} {Signed(s.LightAdu, 2)} (±{F(s.ErrorAdu, 2)})"));
        if (l.ConstantLight is not SideLight c)
        {
            return new(label, $"dupa bias, fata de centru: {sides} {eq}; niciuna peste {F(Th.LeakConstantAdu, 2)} si peste {F(Th.LeakConstantSigmas, 0)} x eroarea; o lumina uniforma pe tot cadrul, in toate cadrele, nu are reper");
        }

        (string against, IReadOnlyList<string> bold) = ScaleReport.AgainstMasters(c.LightAdu * norm, masterNoise, stack);
        string tail = against.Length == 0 ? "" : $"; {against}";
        return new(label, $"dupa bias, fata de centru: {sides} {eq}; peste {F(Th.LeakConstantAdu, 2)} si peste {F(Th.LeakConstantSigmas, 0)} x eroarea la {SideName(c.Side)}{tail}", [$"{Signed(c.LightAdu, 2)} (±{F(c.ErrorAdu, 2)})", .. bold]);
    }

    internal static string SideName(MapSide s) => s switch
    {
        MapSide.Top => "sus",
        MapSide.Bottom => "jos",
        MapSide.Left => "stanga",
        _ => "dreapta",
    };

    /// <summary>The words of R-021's brighter case, from R-029's cause of the verdict region.</summary>
    private static string CauseWords(LeakVerdict l)
    {
        EdgeSplit? s = l.Split;
        return l.Cause switch
        {
            LeakCause.Glow => $"creste cu incalzirea (glow {Signed(s!.GlowAdu, 2)}, constant {Signed(s.ConstantAdu, 2)}): glow al senzorului, dark-urile raman bune",
            LeakCause.Leak => $"nu creste cu incalzirea si nu e in bias (constant {Signed(s!.RemainderAdu, 2)}, glow {Signed(s.GlowAdu, 2)}): scurgere de lumina, refa dark-urile",
            LeakCause.Readout => $"e si in bias (offset de citire {Signed(s!.ReadoutAdu!.Value, 2)}, glow {Signed(s.GlowAdu, 2)}): offset de citire, nu lumina",
            LeakCause.Constant => $"nu creste cu incalzirea (constant {Signed(s!.ConstantAdu, 2)}, glow {Signed(s.GlowAdu, 2)}): scurgere de lumina sau offset de citire, fara bias-uri nu se pot deosebi",
            _ => "scurgere de lumina sau glow",
        };
    }

    /// <summary>The lit frames with their excess, at most eight, then how many more.</summary>
    private static string LitList(FrameLight fl, string sep)
    {
        const int shown = 8;
        List<string> parts = [];
        for (int k = 0; k < Math.Min(shown, fl.LitFrames.Count); k++)
        {
            parts.Add($"{fl.LitFrames[k].ToString(CultureInfo.InvariantCulture)}{sep}{Signed(fl.LitExcessAdu[k], 2)}");
        }

        int more = fl.LitFrames.Count - shown;
        return string.Join(", ", parts) + (more > 0 ? $" si inca {more.ToString(CultureInfo.InvariantCulture)}" : "");
    }

    /// <summary>The row `lumina pe cadre` (R-028); with lit frames, the largest excess against the masters' noise, both bold (ADR 0016).</summary>
    private static ReportRow FrameLightRow(LeakVerdict l, string eq, double norm, double masterNoise, StackNoise? stack)
    {
        const string label = "lumina pe cadre";
        if (l.Frames is not FrameLight fl)
        {
            return new(label, $"NEMASURAT: sub {Th.LeakFitMinFrames} cadre cu pixeli fierbinti");
        }

        if (fl.LitFrames.Count == 0)
        {
            return new(label, $"niciun cadru peste {F(Th.LeakFrameSigmas, 0)} x abaterea tipica ({F(fl.ScaleAdu, 2)} {eq}) si peste {F(Th.LeakFrameAdu, 1)} {eq} fata de dreapta mediei pe incalzire");
        }

        int n = fl.LitFrames.Count;
        double max = fl.LitExcessAdu.Max();
        (string against, IReadOnlyList<string> bold) = ScaleReport.AgainstMasters(max * norm, masterNoise, stack);
        string tail = against.Length == 0 ? "" : $"; cel mai luminat, {Signed(max, 2)} {eq}: {against}";
        return new(label, $"{n} {(n == 1 ? "cadru" : "cadre")}: {LitList(fl, " ")} {eq} peste dreapta mediei pe incalzire (abaterea tipica {F(fl.ScaleAdu, 2)}){tail}", bold.Count == 0 ? null : [$"{Signed(max, 2)} {eq}", .. bold]);
    }

    /// <summary>Why the split (or the sides, which need <paramref name="minFrames"/> frames left) is unmeasured, or null when it is measured (R-028, R-029).</summary>
    private static string? SplitUnmeasured(LeakVerdict l, int minFrames)
    {
        if (l.Frames is not FrameLight fl)
        {
            return $"NEMASURAT: sub {Th.LeakFitMinFrames} cadre cu pixeli fierbinti";
        }

        if (fl.RatioSpan < Th.LeakFitMinSpan)
        {
            return $"NEMASURAT: seria s-a incalzit prea putin ca sa desparta glow-ul de constant (raportul de amplitudine se intinde pe {F(fl.RatioSpan, 2)}, sub {F(Th.LeakFitMinSpan, 1)})";
        }

        if (fl.FramesKept < minFrames)
        {
            return $"NEMASURAT: prea putine cadre ramase dupa cele luminate ({fl.FramesKept}, sub {minFrames})";
        }

        return null;
    }

    /// <summary>The row `glow / constant` (R-029): the verdict region's split, with the raw ADU beside the equivalent when the two differ.</summary>
    private static string SplitRow(LeakVerdict l, string eq, double norm)
    {
        if (SplitUnmeasured(l, 3) is string why)
        {
            return why;
        }

        if (l.Split is not EdgeSplit s)
        {
            return "NEMASURAT";
        }

        string raw = Math.Abs(norm - 1) < 1e-9 ? "" : $" (brut: glow {Signed(s.GlowAdu * norm, 2)}, constant {Signed(s.ConstantAdu * norm, 2)} ADU)";
        return s.ReadoutAdu is double ro
            ? $"la {RegionName(s.Where)}: glow {Signed(s.GlowAdu, 2)} (creste cu incalzirea), constant {Signed(s.ConstantAdu, 2)}, din care in bias {Signed(ro, 2)}, ramane {Signed(s.RemainderAdu, 2)} {eq}{raw}"
            : $"la {RegionName(s.Where)}: glow {Signed(s.GlowAdu, 2)} (creste cu incalzirea), constant {Signed(s.ConstantAdu, 2)} {eq} (fara bias: lumina sau offset de citire){raw}";
    }

    private static string Amplitude(DarkSeriesAnalysis d) =>
        double.IsNaN(d.Warming.FirstRatio)
            ? "NEMASURAT: prea putine cadre sau niciun pixel fierbinte"
            : $"pixelii fierbinti la {F(d.Warming.FirstRatio, 2)} din master in primul cadru, {F(d.Warming.LastRatio, 2)} in ultimul ({d.Warming.Pixels} pixeli peste {F(Th.HotAdu, 0)} {Eq(d)})";

    private static readonly string NoPlateau = $"NEMASURAT: sub {Th.WarmingFitMinFrames} cadre cu pixeli fierbinti";

    /// <summary>The `platou` row of check 3: the plateau, the time constant, the master's and the last frame's distance to it; a flat series says so.</summary>
    private static string PlateauRow(Warming w, Normalisation nz)
    {
        if (!w.HasFit)
        {
            return NoPlateau;
        }

        double firstOffset = Math.Abs(nz.Degrees(w.FirstRatio / w.Plateau));
        if (firstOffset < Th.OffPlateauCelsius && Math.Abs(w.SettleCelsius) < Th.OffPlateauCelsius)
        {
            return $"seria e plata: platoul la {F(w.Plateau, 2)} din master, primul cadru la mai putin de {F(Th.OffPlateauCelsius, 1)} grade de el";
        }

        string unit = w.TimeFromStamps ? "min" : "cadre";
        string tau = w.TimeFromStamps && w.TauMinutes >= 3 * w.Minutes - 1e-9
            ? $"constanta de timp de cel putin {F(w.TauMinutes, 0)} {unit}, seria e prea scurta ca s-o masoare"
            : $"constanta de timp {F(w.TauMinutes, 0)} {unit}";
        return $"seria se apropie de un platou la {F(w.Plateau, 2)} din master ({tau}); masterul, mediana seriei, e {FromPlateau(w.PlateauCelsius)}; la ultimul cadru mai are {F(Math.Abs(w.SettleCelsius), 1)} grade pana la platou";
    }

    /// <summary>The `de aruncat` row of check 3: the frames more than Th.OffPlateauCelsius from the plateau, the band's bounds, and where the master would sit without them.</summary>
    private static string OffPlateauRow(Warming w)
    {
        if (!w.HasFit)
        {
            return NoPlateau;
        }

        string deg = F(Th.OffPlateauCelsius, 1);
        if (w.OffPlateauFrames.Count == 0)
        {
            return $"niciun cadru la peste {deg} grade de platou (amplit. intre {F(w.OffPlateauLow, 2)} si {F(w.OffPlateauHigh, 2)})";
        }

        string without = double.IsNaN(w.MasterWithoutOffCelsius) ? "fara ele nu ramane niciun cadru" : $"fara ele masterul ar fi {FromPlateau(w.MasterWithoutOffCelsius)}";
        return $"{RunReport.Frames(w.OffPlateauFrames.Count)} ({Ranges(w.OffPlateauFrames)}) la peste {deg} grade de platou (amplit. sub {F(w.OffPlateauLow, 2)} sau peste {F(w.OffPlateauHigh, 2)}; platoul {F(w.Plateau, 2)}); {without}";
    }

    private static string Drift(DriftVerdict v, Normalisation nz)
    {
        Warming w = v.Warming;
        string span = $"{F(Th.MatchOptLow, 1)} .. {F(Th.MatchOptHigh, 1)} din master";
        if (v.Verdict == Verdict.Unmeasured)
        {
            return "NEMASURAT";
        }

        if (v.Verdict == Verdict.Ok)
        {
            if (!w.HasFit)
            {
                return $"OK: toate cadrele stau in {span} (sub {Th.WarmingFitMinFrames} cadre, fara platou)";
            }

            string rule = $"regula dublarii la {F(nz.DoublingCelsius, 0)} grade";
            string minutes = double.IsNaN(w.Minutes) ? "" : $" in {F(w.Minutes, 0)} min";
            string warmup = Math.Abs(w.DeltaCelsius) < 0.05
                ? $"seria e plata ({rule})"
                : $"{(w.DeltaCelsius > 0 ? "incalzirea" : "racirea")} de {F(Math.Abs(w.DeltaCelsius), 1)} grade{minutes} e la inceput ({rule})";
            string off = w.OffPlateauFrames.Count == 0 ? "niciun cadru de aruncat" : "cadrele de aruncat sunt in randul de mai sus";
            return $"OK: seria s-a asezat (la sfarsit mai are {F(Math.Abs(w.SettleCelsius), 1)} grade pana la platou, sub {F(Th.SettledCelsius, 1)}) si toate cadrele stau in {span}; {warmup}, {off}";
        }

        List<string> parts = [];
        if (!v.Settled)
        {
            parts.Add($"seria inca se {(w.SettleCelsius > 0 ? "incalzeste" : "raceste")} la sfarsit (mai are {F(Math.Abs(w.SettleCelsius), 1)} grade pana la platou, peste {F(Th.SettledCelsius, 1)}): masterul nu apartine unei temperaturi; continua seria sau arunca cadrele din randul de mai sus");
        }

        if (v.OutsideFrames.Count > 0)
        {
            int n = v.OutsideFrames.Count;
            string discard = w.HasFit ? "arunca cadrele din randul de mai sus" : "arunca-le";
            parts.Add($"{RunReport.Frames(n)} {(n == 1 ? "iese" : "ies")} din {span} (cel mai departe {F(v.FarthestRatio, 2)}): seria a pornit {(v.FarthestRatio < 1 ? "rece" : "calda")}; {discard}");
        }

        return "ATENTIE: " + string.Join("; ", parts);
    }

    private static string FromPlateau(double degrees) =>
        Math.Abs(degrees) < 0.05 ? "la platou" : $"cu {F(Math.Abs(degrees), 1)} grade {(degrees < 0 ? "sub" : "peste")} platou";

    /// <summary>Frame numbers as ranges: `1 .. 11`, `1 .. 3, 7`; a run of two prints both numbers.</summary>
    internal static string Ranges(IReadOnlyList<int> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        List<string> parts = [];
        int i = 0;
        while (i < frames.Count)
        {
            int j = i;
            while (j + 1 < frames.Count && frames[j + 1] == frames[j] + 1)
            {
                j++;
            }

            if (j - i >= 2)
            {
                parts.Add($"{frames[i].ToString(CultureInfo.InvariantCulture)} .. {frames[j].ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                for (int k = i; k <= j; k++)
                {
                    parts.Add(frames[k].ToString(CultureInfo.InvariantCulture));
                }
            }

            i = j + 1;
        }

        return string.Join(", ", parts);
    }

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

    private static string Glow(LeakVerdict l, PatternVerdict p, string eq)
    {
        string source = l.FromDarkMinusBias ? "dark - bias" : "masterul dark";
        if (double.IsNaN(p.GlowAdu))
        {
            return "NEMASURAT";
        }

        return l.Split is EdgeSplit s
            ? $"{Signed(p.GlowAdu, 2)} {eq} la {RegionName(l.Where)}, partea care creste cu incalzirea (constant {Signed(s.ConstantAdu, 2)} in afara ei; {source})"
            : $"{Signed(p.GlowAdu, 2)} {eq} la {RegionName(l.Where)} fata de centru ({source})";
    }

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

    internal static string Signed(double v, int decimals)
    {
        double r = Math.Round(v, decimals, MidpointRounding.AwayFromZero);
        return (r < 0 ? "-" : "+") + F(Math.Abs(r), decimals);
    }

    private static string Pixels(int n) => n == 1 ? "1 pixel" : n.ToString(CultureInfo.InvariantCulture) + " pixeli";

    private static string Time(DateTime? t) => t is DateTime d ? d.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "-";

    /// <summary>A figure with fixed decimals; a value that rounds to zero prints without a minus sign.</summary>
    internal static string F(double v, int decimals)
    {
        if (double.IsNaN(v))
        {
            return "-";
        }

        double r = Math.Round(v, decimals);
        return RunReport.F(r == 0 ? 0 : v, decimals);
    }
}
