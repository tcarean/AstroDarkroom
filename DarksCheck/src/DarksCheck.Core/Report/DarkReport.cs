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
using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// The darks report (in the run's language, R-025 to R-065): the header rows of the darks' stack, the
/// sections of checks 2 to 9 and INDEX as labelled rows (<see cref="Sections"/>), and the
/// two tables as rows of formatted cells (<see cref="FrameRows"/>, <see cref="LightRows"/>);
/// <see cref="Write"/> prints exactly those for the console, the window binds them
/// (ADR 0009). Words only; the numbers come from the run.
/// </summary>
internal static class DarkReport
{
    /// <summary>
    /// Prints the whole darks report for the console: the inventory's header, the darks' header
    /// line and rows, every section, the frames table, the lights table when there are lights, and
    /// the ADU scale's explanation.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <param name="w">The writer the report goes to; not null.</param>
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
        w.WriteLine(T("  FRAMES TABLE", "  TABEL PE CADRE"));
        w.WriteLine(FrameTableHeader(r));
        foreach (DarkTableRow f in FrameRows(r))
        {
            w.WriteLine(FrameLine(f));
        }

        if (r.Lights is not null)
        {
            w.WriteLine();
            w.WriteLine(T("  LIGHTS TABLE", "  TABEL PE LIGHTS"));
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
                    w.WriteLine($"    {f.Index,3}  {name,-20}  {T("error", "eroare")}: {f.Error}");
                }
                else
                {
                    w.WriteLine($"    {f.Index,3}  {name,-20}  {f.Time,-8}  {f.Sky,6}  {f.LocalSigma,6}  {f.Candidates,9}  {f.Isolated,7}  {f.Amp,7}  {f.Q25,5}  {f.Q75,5}");
                }
            }
        }

        ScaleReport.Write(r, w);
    }

    /// <summary>
    /// Prints one row as <c>label : value</c>, the label padded to 19 characters.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="row">The row to print.</param>
    private static void WriteRow(TextWriter w, ReportRow row) => w.WriteLine($"    {row.Label,-19}: {row.Value}");

    /// <summary>Cuts a file name to the 20 characters of the tables' name column.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The name, at most 20 characters long.</returns>
    private static string Trim20(string name) => name.Length > 20 ? name[..20] : name;

    /// <summary>The `=== DARK-URI ... ===` line: frames decoded, size, scale and CFA.</summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The line, in the run's language, with the partner's folder named when its darks
    /// stand in for the session's.</returns>
    internal static string HeaderLine(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DarkSeriesAnalysis d = r.Darks;
        string used = d.Used.ToString(CultureInfo.InvariantCulture);
        return T(
            $"=== DARKS{(r.DarksFromPartner is string pe ? $" (the partner's, {pe})" : "")}: {(d.Used == 1 ? "1 frame decoded" : used + " frames decoded")} of {d.Frames.Count}, {d.Width} x {d.Height} px, scale {F(d.Black, 0)} .. {F(d.White, 0)} ADU ({d.ScaleSource}), CFA {d.CfaLetters ?? "unknown"} ===",
            $"=== DARK-URI{(r.DarksFromPartner is string pr ? $" (ale partenerului {pr})" : "")}: {(d.Used == 1 ? "1 cadru decodat" : used + " cadre decodate")} din {d.Frames.Count}, {d.Width} x {d.Height} px, scara {F(d.Black, 0)} .. {F(d.White, 0)} ADU ({d.ScaleSource}), CFA {d.CfaLetters ?? "necunoscut"} ===");
    }

    /// <summary>
    /// The rows under the header line: the normalisation, the quantisation, the biases' warnings.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The normalisation and quantisation rows, then a row for each problem with the
    /// biases.</returns>
    internal static IReadOnlyList<ReportRow> HeaderRows(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        DarkSeriesAnalysis d = r.Darks;
        List<ReportRow> rows =
        [
            new(T("normalisation", "normalizare"), Normalisation(d)),
            new(T("quantisation", "cuantizare"), T(
                $"step {d.QuantStep} ADU near the black; {d.UniqueValues} distinct values in the first frame ({d.PlaneNames[r.ReferencePlane]})",
                $"pas {d.QuantStep} ADU langa negru; {d.UniqueValues} valori distincte in primul cadru ({d.PlaneNames[r.ReferencePlane]})")),
        ];
        if (r.BiasError is not null)
        {
            rows.Add(new(T("WARNING", "ATENTIE"), T($"the biases could not be used: {r.BiasError}", $"bias-urile nu s-au putut folosi: {r.BiasError}")));
        }

        BiasFrameFigures[] badBiases = r.Biases?.Frames.Where(f => f.Error is not null && !f.Skipped).ToArray() ?? [];
        if (badBiases.Length > 0)
        {
            rows.Add(new(T("WARNING", "ATENTIE"), T(
                $"{badBiases.Length} {(badBiases.Length == 1 ? "bias could not be decoded" : "biases could not be decoded")} (the first: {badBiases[0].Info.FileName}: {badBiases[0].Error}); see {DarkOutputs.BiasesCsvName} with --out",
                $"{badBiases.Length} {(badBiases.Length == 1 ? "bias nu s-a putut decoda" : "bias-uri nu s-au putut decoda")} (primul: {badBiases[0].Info.FileName}: {badBiases[0].Error}); vezi {DarkOutputs.BiasesCsvName} cu --out")));
        }

        int skippedBiases = r.Biases?.Frames.Count(f => f.Skipped) ?? 0;
        if (skippedBiases > 0)
        {
            rows.Add(new(T("skipped biases", "bias-uri sarite"), T(
                $"{skippedBiases} of {r.Biases!.Frames.Count} (the frame cap; {r.Biases.Used} used)",
                $"{skippedBiases} din {r.Biases!.Frames.Count} (plafonul de cadre; folosite {r.Biases.Used})")));
        }

        return rows;
    }

    /// <summary>Checks 2 to 9 and INDEX, each as its titled rows, in the console's order.</summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The nine sections (checks 2 to 9, then INDEX), titled as the console prints
    /// them.</returns>
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
        // "master dark", "master bias" and "dark - bias" are the same in both languages: the
        // window finds the maps' rows by them.
        List<ReportRow> leak = [new("master dark", RegionsLine(d.MeanMaps[rp].Regions, n) + T(
            $"  [{g}, {eq} above the black, mean of {RunReport.Frames(d.Used)}, blocks of {Th.BinPx * 2} px]",
            $"  [{g}, {eq} peste negru, media a {RunReport.Frames(d.Used)}, blocuri de {Th.BinPx * 2} px]"))];
        if (r.Biases is BiasAnalysis b)
        {
            leak.Add(new("master bias", RegionsLine(b.MeanMaps[rp].Regions, 1) + T(
                $"  [{g}, ADU above the black, mean of {RunReport.Frames(b.Used)}]",
                $"  [{g}, ADU peste negru, media a {RunReport.Frames(b.Used)}]")));
        }

        if (r.DarkCurrent is DarkCurrent dc)
        {
            leak.Add(new("dark - bias", $"{RegionsLine(dc.Maps[rp].Regions, n)}; {T("median", "mediana")} {F(dc.Median / n, 2)}, {F(dc.P1 / n, 2)} .. {F(dc.P99 / n, 2)} (p1 .. p99)"));
        }

        leak.Add(FrameLightRow(v.Leak, eq, n, masterNoise, stack));
        leak.Add(new("glow / constant", SplitRow(v.Leak, eq, n)));
        leak.Add(SidesRow(v.Leak, eq, n, masterNoise, stack));
        leak.Add(LeakVerdictRow(v.Leak, eq, n, masterNoise));

        return
        [
            new(T("2. LIGHT LEAKS AND GRADIENT", "2. SCURGERI DE LUMINA SI GRADIENT"), leak),
            new(T("3. THERMAL DRIFT IN THE SERIES", "3. DERIVA TERMICA IN SERIE"),
            [
                new(T("amplitude", "amplitudine"), Amplitude(d)),
                new(T("plateau", "platou"), PlateauRow(d.Warming, d.Normalisation)),
                new(T("to discard", "de aruncat"), OffPlateauRow(d.Warming)),
                new("verdict", Drift(v.Drift, d.Normalisation)),
            ]),
            new(T("4. NOISE PER FRAME", "4. ZGOMOT PE CADRU"),
            [
                new(T("residual sigma", "sigma rezidual"), ResidualRange(d, rp) + T("  [ADU, frame - master]", "  [ADU, cadru - master]")),
                new(T("read noise", "zgomot de citire"), ReadNoise(r, rp)),
                new(T("random banding", "benzi aleatoare"), Banding(v.Noise)),
                new("verdict", Noise(v.Noise)),
            ]),
            new(T("5. HOT PIXELS", "5. PIXELI FIERBINTI"),
            [
                new(T("master above level", "master peste nivel"), Levels(d)),
                new(T($"per plane at {F(Th.HotAdu, 0)}", $"pe plan la {F(Th.HotAdu, 0)}"), PerPlane(d, Th.HotLevelIndex)),
                new(T("amplitude", "amplitudine"), Percentiles(d.Hot, n, eq)),
                new(T("stability", "stabilitate"), Stability(d, rp)),
                new("verdict", T(
                    $"OK (informative): {d.Hot.Hot} pixels above {F(Th.HotAdu, 0)} {eq} in {F(mpx, mpx < 1 ? 2 : 1)} Mpx",
                    $"OK (informativ): {d.Hot.Hot} pixeli peste {F(Th.HotAdu, 0)} {eq} in {F(mpx, mpx < 1 ? 2 : 1)} Mpx")),
            ]),
            new(T("6. FIXED PATTERN", "6. MODEL FIX"),
            [
                new(T("structured banding", "benzi structurate"), StructuredBanding(d)),
                new("glow", Glow(v.Leak, v.Pattern, eq)),
                new("verdict", Pattern(v.Pattern, eq)),
            ]),
            new(T("7. DEAD AND SATURATED PIXELS", "7. PIXELI MORTI SI SATURATI"),
            [
                new("master", T(
                    $"{v.DeadSat.Dead} below the black - {F(Th.DeadBelowAdu, 0)} ADU, {v.DeadSat.Saturated} at the white ({F(d.White, 0)})",
                    $"{v.DeadSat.Dead} sub negru - {F(Th.DeadBelowAdu, 0)} ADU, {v.DeadSat.Saturated} la alb ({F(d.White, 0)})")),
                new("verdict", DeadSat(v.DeadSat)),
            ]),
            new(T("8. HOW MANY DARKS ARE ENOUGH", "8. CATE DARK-URI AJUNG"),
            [
                new(T("noise of the mean", "zgomotul mediei"), Curve(d.Noise) + T(
                    $"  [{g}, the central {d.Noise.CropPx} px, ADU, temporal sigma = frame against frame]",
                    $"  [{g}, centrul de {d.Noise.CropPx} px, ADU, sigma temporala = cadru fata de cadru]")),
                new("master", Enough(d, v.Enough), double.IsNaN(v.Enough.MasterNoise) ? null : [$"{F(v.Enough.MasterNoise, 2)} ADU"]),
                new("verdict", EnoughVerdictText(v.Enough)),
            ]),
            new(T("9. LIGHTS AGAINST THE DARK", "9. LIGHTS FATA DE DARK"), LightsRows(r, eq)),
            new("INDEX", IndexRows(r)),
        ];
    }

    /// <summary>
    /// The rows of check 9 (R-050 onwards): the lights' warnings, the persistent hot pixels, their
    /// overlap with the master in both directions, the misses, the amplitude and the verdict; a
    /// single unmeasured verdict row without lights.
    /// </summary>
    /// <param name="r">The run's result.</param>
    /// <param name="eq">The unit word of the figures, "ADU" or "ADU-eq".</param>
    /// <returns>The rows, in the console's order.</returns>
    private static List<ReportRow> LightsRows(DarkSeriesResult r, string eq)
    {
        if (r.Lights is not LightsAnalysis l)
        {
            return [new("verdict", r.LightsError is null
                ? T("UNMEASURED: the session has no lights", "NEMASURAT: sesiunea nu are lights")
                : T("UNMEASURED: the lights could not be used: ", "NEMASURAT: lights-urile nu s-au putut folosi: ") + r.LightsError)];
        }

        List<ReportRow> rows = [];
        LightFigures[] bad = l.Frames.Where(f => f.Error is not null && !f.Skipped).ToArray();
        if (bad.Length > 0)
        {
            rows.Add(new(T("WARNING", "ATENTIE"), T(
                $"{bad.Length} {(bad.Length == 1 ? "light could not be decoded" : "lights could not be decoded")} (the first: {bad[0].Info.FileName}: {bad[0].Error}); see the table",
                $"{bad.Length} {(bad.Length == 1 ? "light nu s-a putut decoda" : "lights nu s-au putut decoda")} (primul: {bad[0].Info.FileName}: {bad[0].Error}); vezi tabelul")));
        }

        int skipped = l.Frames.Count(f => f.Skipped);
        if (skipped > 0)
        {
            rows.Add(new(T("skipped lights", "lights sarite"), T(
                $"{skipped} of {l.Frames.Count} (the frame cap; {l.Used} used)",
                $"{skipped} din {l.Frames.Count} (plafonul de cadre; folosite {l.Used})")));
        }

        if (l.AssumedSettings > 0)
        {
            rows.Add(new(T("WARNING", "ATENTIE"), T(
                $"{l.AssumedSettings} {(l.AssumedSettings == 1 ? "light does not give its ISO or exposure; the darks' are assumed" : "lights do not give their ISO or exposure; the darks' are assumed")} (normalisation x{F(r.Darks.Norm, 2)})",
                $"{l.AssumedSettings} {(l.AssumedSettings == 1 ? "light nu spune ISO-ul sau expunerea; presupuse ale dark-urilor" : "lights nu spun ISO-ul sau expunerea; presupuse ale dark-urilor")} (normalizare x{F(r.Darks.Norm, 2)})")));
        }

        rows.Add(new(T("hot pixels", "pixeli fierbinti"), T(
            $"{Pixels(l.Persistent)} persistent in the lights (isolated, above {F(Th.LightHotSigmas, 0)} local sigma, in at least {l.PersistentThreshold} of {RunReport.Frames(l.Used)})",
            $"{Pixels(l.Persistent)} persistenti in lights (izolati, peste {F(Th.LightHotSigmas, 0)} sigma locale, in cel putin {l.PersistentThreshold} din {RunReport.Frames(l.Used)})")));
        string above = T("above", "peste");
        string of = T("of", "din");
        rows.Add(new(T("in the dark master", "in masterul dark"), $"{string.Join("; ", l.Overlap.Select(o => $"{above} {F(o.LevelAdu, 0)}: {o.Both} {of} {l.Persistent}" + (l.Persistent > 0 ? $" ({F(100.0 * o.Both / l.Persistent, 0)} %)" : "")))}  [{eq}]"));
        rows.Add(new("master in lights", string.Join("; ", l.Overlap.Select(o => $"{above} {F(o.LevelAdu, 0)}: {o.Both} {of} {o.DarkCount}" + (o.DarkCount > 0 ? $" ({F(100.0 * o.Both / o.DarkCount, 0)} %)" : "")))));
        rows.Add(new(T("unmatched", "nepotriviti"), l.Missed == 0
            ? T("no persistent pixel in the lights under the master's threshold", "niciun pixel persistent in lights sub pragul masterului")
            : T(
                $"{l.Missed} {(l.Missed == 1 ? "persistent pixel" : "persistent pixels")} in the lights under {F(Th.HotAdu, 0)} {eq} in the master (their median in the master {F(l.MissedDarkExcessMedian, 0)} {eq})",
                $"{l.Missed} {(l.Missed == 1 ? "pixel persistent" : "pixeli persistenti")} in lights sub {F(Th.HotAdu, 0)} {eq} in master (mediana lor in master {F(l.MissedDarkExcessMedian, 0)} {eq})")));
        MatchFigures m = l.LightsOverDark;
        rows.Add(new(T("amplitude", "amplitudine"), m.Verdict == MatchVerdict.Unmeasured
            ? T("UNMEASURED: no hot pixel under saturation in the master", "NEMASURAT: niciun pixel fierbinte sub saturatie in master")
            : T(
                $"lights / dark {F(m.Ratio, 2)} over the night (median over {RunReport.Frames(m.Pixels)}; the first {F(l.RatioFirst, 2)}, the last {F(l.RatioLast, 2)}; {Pixels(l.AmpPixels)})",
                $"lights / dark {F(m.Ratio, 2)} pe noapte (mediana pe {RunReport.Frames(m.Pixels)}; primul {F(l.RatioFirst, 2)}, ultimul {F(l.RatioLast, 2)}; {Pixels(l.AmpPixels)})")));
        rows.Add(new("verdict", MatchText(m, partner: false, T("the lights", "lights-urile"))));
        return rows;
    }

    /// <summary>
    /// The rows of the INDEX block (R-060 to R-064): the darks' index and label, the lights'
    /// equivalent index, and, with a partner, its index, the darks over it, the shared hot pixels
    /// and the verdict against it.
    /// </summary>
    /// <param name="r">The run's result.</param>
    /// <returns>The rows, in the console's order.</returns>
    private static List<ReportRow> IndexRows(DarkSeriesResult r)
    {
        DarkSeriesAnalysis d = r.Darks;
        IndexFigures ix = r.Index;
        double mpx = d.Width * (double)d.Height / 1e6;
        List<ReportRow> rows =
        [
            new(r.DarksFromPartner is null ? T("darks", "dark-uri") : T("darks (partner)", "dark-uri (partener)"), T(
                $"{ix.Tag} ({F(ix.Index, 1)} ADU-eq: the median of the pixels ranked {F(Th.IndexRankLowPerMpx, 0)}-{F(Th.IndexRankHighPerMpx, 0)} per Mpx, ranks {ix.RankLow + 1} .. {ix.RankHigh} of {F(mpx, mpx < 1 ? 2 : 1)} Mpx, normalised to ISO {F(Th.NormIso, 0)} and {F(Th.NormExposureSeconds, 0)} s)",
                $"{ix.Tag} ({F(ix.Index, 1)} ADU-eq: mediana pixelilor de pe rangurile {F(Th.IndexRankLowPerMpx, 0)}-{F(Th.IndexRankHighPerMpx, 0)} pe Mpx, al {ix.RankLow + 1}-lea .. al {ix.RankHigh}-lea din {F(mpx, mpx < 1 ? 2 : 1)} Mpx, normalizat la ISO {F(Th.NormIso, 0)} si {F(Th.NormExposureSeconds, 0)} s)")),
            new(T("label", "eticheta"), r.Label),
        ];
        if (r.Lights is LightsAnalysis l && !double.IsNaN(r.LightsIndex))
        {
            rows.Add(new("lights", T(
                $"equivalent idx{F(Math.Round(r.LightsIndex, MidpointRounding.AwayFromZero), 0)} (dark x {F(l.LightsOverDark.Ratio, 2)}, that is {Signed(l.LightsOverDark.DeltaCelsius, 1)} degrees against the dark)",
                $"echivalent idx{F(Math.Round(r.LightsIndex, MidpointRounding.AwayFromZero), 0)} (dark x {F(l.LightsOverDark.Ratio, 2)}, adica {Signed(l.LightsOverDark.DeltaCelsius, 1)} grade fata de dark)")));
        }

        if (r.PartnerError is not null)
        {
            rows.Add(new(T("partner", "partener"), T("WARNING: ", "ATENTIE: ") + r.PartnerError));
        }

        if (r.Partner is PartnerAnalysis p)
        {
            MatchFigures m = p.OwnOverPartner;
            string settings = p.Iso is double iso && p.ExposureSeconds is double e ? $"ISO {F(iso, 0)}, {InventoryReport.Exposure(e)} s" : T("unknown settings", "setari necunoscute");
            rows.Add(new(T("partner", "partener"), T(
                $"{p.Folder}: {p.Index.Tag} ({F(p.Index.Index, 1)} ADU-eq; {RunReport.Frames(p.Used)} of {p.Frames}, {settings}); label {p.Label}",
                $"{p.Folder}: {p.Index.Tag} ({F(p.Index.Index, 1)} ADU-eq; {RunReport.Frames(p.Used)} din {p.Frames}, {settings}); eticheta {p.Label}")));
            if (p.Failed > 0)
            {
                rows.Add(new(T("WARNING", "ATENTIE"), T(
                    $"{p.Failed} {(p.Failed == 1 ? "dark of the partner could not be used" : "darks of the partner could not be used")} (the first: {p.FirstError})",
                    $"{p.Failed} {(p.Failed == 1 ? "dark al partenerului nu s-a putut folosi" : "dark-uri ale partenerului nu s-au putut folosi")} (primul: {p.FirstError})")));
            }

            rows.Add(new(T("darks / partner", "dark-uri / partener"), m.Verdict == MatchVerdict.Unmeasured
                ? T("UNMEASURED: no shared pixel", "NEMASURAT: niciun pixel comun")
                : T(
                    $"{F(m.Ratio, 2)} (q25 {F(m.Q25, 2)}, q75 {F(m.Q75, 2)}, {(m.Pixels == 1 ? "1 shared pixel" : $"{m.Pixels} shared pixels")}) after normalisation, that is the session's darks run {Degrees(m)} against the partner",
                    $"{F(m.Ratio, 2)} (q25 {F(m.Q25, 2)}, q75 {F(m.Q75, 2)}, {(m.Pixels == 1 ? "1 pixel comun" : $"{m.Pixels} pixeli comuni")}) dupa normalizare, adica dark-urile sesiunii sunt cu {Degrees(m)} fata de partener")));
            string lvl = F(Th.PartnerOverlapAdu, 0);
            string ownShare = p.OwnHot50 > 0 ? $" ({F(100.0 * p.OwnHot50InPartner50 / p.OwnHot50, 0)} %)" : "";
            string partnerShare = p.PartnerHot50 > 0 ? $" ({F(100.0 * p.PartnerHot50InOwn20 / p.PartnerHot50, 0)} %)" : "";
            rows.Add(new(T("shared pixels", "pixeli comuni"), T(
                $"{p.OwnHot50InPartner50} of the session's {p.OwnHot50} above {lvl} ADU-eq are above {lvl} at the partner too{ownShare}; {p.PartnerHot50InOwn20} of the partner's {p.PartnerHot50} above {lvl} are above {F(Th.HotAdu, 0)} in the session{partnerShare}",
                $"{p.OwnHot50InPartner50} din {p.OwnHot50} ai sesiunii peste {lvl} ADU-eq sunt peste {lvl} si la partener{ownShare}; {p.PartnerHot50InOwn20} din {p.PartnerHot50} ai partenerului peste {lvl} sunt peste {F(Th.HotAdu, 0)} in sesiune{partnerShare}")));
            if (p.LightsOverPartner.Verdict != MatchVerdict.Unmeasured)
            {
                // lights over the partner, through the session's darks: (lights / dark) x (dark /
                // partner)
                MatchFigures chained = p.LightsOverPartner;
                rows.Add(new(T("lights / partner", "lights / partener"), T(
                    $"{F(chained.Ratio, 2)} (through the session's darks), that is the lights run {Degrees(chained)} against the partner",
                    $"{F(chained.Ratio, 2)} (prin dark-urile sesiunii), adica lights-urile sunt cu {Degrees(chained)} fata de partener")));
                rows.Add(new(T("partner verdict", "verdict partener"), MatchText(chained, partner: true, T("the lights", "lights-urile"))));
            }
            else
            {
                rows.Add(new(T("partner verdict", "verdict partener"), MatchText(m, partner: true, T("the session's darks", "dark-urile sesiunii")) + T(" (the session's darks against the partner)", " (dark-urile sesiunii fata de partener)")));
            }
        }

        return rows;
    }

    /// <summary>
    /// The frames table's header line: the column names, then <see cref="FrameTableNote"/>.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The header line, in the run's language; each column name ends where the
    /// Romanian one ends, so the right-aligned figures stay under it.</returns>
    internal static string FrameTableHeader(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return T(
            $"     no  file                  time       mean   sigma  resid.   bands    ampl. plateau   light    side  >{F(Th.HotAdu, 0)}   {FrameTableNote(r)}",
            $"     nr  fisier                ora       medie   sigma  rezid.   benzi  amplit.  platou  lumina  latura  >{F(Th.HotAdu, 0)}   {FrameTableNote(r)}");
    }

    /// <summary>
    /// One line of the frames table as the console prints it: the cells in their columns, the note
    /// of a frame off the plateau after the last one; a skipped or failed frame prints its reason
    /// instead of cells.
    /// </summary>
    /// <param name="f">The formatted row; not null.</param>
    /// <returns>The line, without a line break.</returns>
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
            return $"    {f.Index,3}  {name,-20}  {T("error", "eroare")}: {f.Error}";
        }

        string note = f.Note is null ? "" : $"   {f.Note}";
        return $"    {f.Index,3}  {name,-20}  {f.Time,-8}  {f.Mean,6}  {f.Sigma,6}  {f.Residual,6}  {f.Bands,6}  {f.Amp,7}  {f.Plateau,6}  {f.Light,6}  {f.Side,6}  {f.Above,4}{note}";
    }

    /// <summary>The unit note of the frames table: the plane and what the columns mean.</summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The note in square brackets, in the run's language.</returns>
    internal static string FrameTableNote(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        string eq = Eq(r.Darks);
        string side = r.Verdicts.Leak.WorstSide is SideLight w
            ? T(
                $"side = {SideName(w.Side)} - centre{(w.RemainderAdu is null ? " (no biases, glow included)" : " after the bias (glow included)")}, {eq}",
                $"latura = {SideName(w.Side)} - centru{(w.RemainderAdu is null ? " (fara bias, glow inclus)" : " dupa bias (glow inclus)")}, {eq}")
            : T("side = the side with the most constant light - centre (UNMEASURED)", "latura = latura cu cea mai multa lumina constanta - centru (NEMASURAT)");
        return T(
            $"[{r.Darks.PlaneNames[r.ReferencePlane]}; mean = ADU above the black; bands = rows / white noise; ampl. = the hot pixels against the master; plateau = degrees from the series' plateau; to discard = more than {F(Th.OffPlateauCelsius, 1)} degrees from the plateau (check 3); light = {eq} above the line of the mean against the warming; {side}]",
            $"[{r.Darks.PlaneNames[r.ReferencePlane]}; medie = ADU peste negru; benzi = randuri / zgomot alb; amplit. = pixelii fierbinti fata de master; platou = grade fata de platoul seriei; de aruncat = la peste {F(Th.OffPlateauCelsius, 1)} grade de platou (verificarea 3); lumina = {eq} peste dreapta mediei pe incalzire; {side}]");
    }

    /// <summary>
    /// The hover hint of the frames table's `platou` column (R-071): what the figure is and when a
    /// frame is one to discard. A property, so it is worded in the language current when it is
    /// read.
    /// </summary>
    internal static string PlateauColumnTip => T(
        $"the frame's degrees from the series' plateau, read from the hot pixels; a frame more than {F(Th.OffPlateauCelsius, 1)} degrees from the plateau is one to discard and has its reason on the line under its cells (check 3)",
        $"gradele cadrului fata de platoul seriei, citite din pixelii fierbinti; un cadru la peste {F(Th.OffPlateauCelsius, 1)} grade de platou e de aruncat si are motivul pe randul de sub celule (verificarea 3)");

    /// <summary>
    /// The sentence under the window's warming chart (R-071): what the points are, and with a fit
    /// the plateau and the band outside which a frame is one to discard.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The sentence, in the run's language.</returns>
    internal static string WarmingChartNote(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Warming w = r.Darks.Warming;
        string what = T(
            "The amplitude of the master's hot pixels in each frame, against the master: the slope is the camera warming during the series (check 3).",
            "Amplitudinea pixelilor fierbinti ai masterului in fiecare cadru, fata de master: panta e incalzirea camerei in timpul seriei (verificarea 3).");
        return w.HasFit
            ? T(
                $"{what} The series' plateau is at {F(w.Plateau, 2)} of the master; the frames under {F(w.OffPlateauLow, 2)} or over {F(w.OffPlateauHigh, 2)} (more than {F(Th.OffPlateauCelsius, 1)} degrees from the plateau) are to discard and show in the attention colour.",
                $"{what} Platoul seriei e la {F(w.Plateau, 2)} din master; cadrele sub {F(w.OffPlateauLow, 2)} sau peste {F(w.OffPlateauHigh, 2)} (la peste {F(Th.OffPlateauCelsius, 1)} grade de platou) sunt de aruncat si apar in culoarea de atentie.")
            : T(
                $"{what} With fewer than {Th.WarmingFitMinFrames} frames with hot pixels no plateau is fitted.",
                $"{what} Sub {Th.WarmingFitMinFrames} cadre cu pixeli fierbinti nu se potriveste un platou.");
    }

    /// <summary>
    /// One row per dark, the cells formatted as the console prints them, on the reference plane.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>One row per dark in series order; a skipped or failed frame has its reason and
    /// empty cells.</returns>
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
                note = T(
                    $"to discard: ampl. {Ratio(f.AmpRatioMedian)} {(under ? "below" : "above")} {F(under ? wm.OffPlateauLow : wm.OffPlateauHigh, 2)} (more than {F(Th.OffPlateauCelsius, 1)} degrees {(under ? "below" : "above")} the plateau {F(wm.Plateau, 2)})",
                    $"de aruncat: amplit. {Ratio(f.AmpRatioMedian)} {(under ? "sub" : "peste")} {F(under ? wm.OffPlateauLow : wm.OffPlateauHigh, 2)} (peste {F(Th.OffPlateauCelsius, 1)} grade {(under ? "sub" : "peste")} platoul {F(wm.Plateau, 2)})");
            }

            rows.Add(new(f.Info.Index, f.Info.FileName, null, null, Time(f.Info.Timestamp), F(f.MeanAboveBlack, 2), F(p.Sigma, 2), F(res.Sigma, 2), F(res.RowBandingRatio, 1), Ratio(f.AmpRatioMedian), plateau, double.IsNaN(light) ? "-" : Signed(light, 2), side, p.Above[Th.HotLevelIndex].ToString(CultureInfo.InvariantCulture), note));
        }

        return rows;
    }

    /// <summary>
    /// The lights table's header line: the column names, then <see cref="LightTableNote"/>.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The header line, in the run's language; each column name ends where the
    /// Romanian one ends, so the right-aligned figures stay under it.</returns>
    internal static string LightTableHeader(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return T(
            $"     no  file                  time        sky   sigma candidates isolated    ampl.    q25    q75   {LightTableNote(r)}",
            $"     nr  fisier                ora         cer   sigma  candidati  izolati  amplit.    q25    q75   {LightTableNote(r)}");
    }

    /// <summary>The unit note of the lights table: the plane and what the columns mean.</summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The note in square brackets, in the run's language.</returns>
    internal static string LightTableNote(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return T(
            $"[{r.Darks.PlaneNames[r.ReferencePlane]}; sky = ADU above the black; sigma = of the local excess, the detector's scale; ampl. = lights / dark at the master's hot pixels]",
            $"[{r.Darks.PlaneNames[r.ReferencePlane]}; cer = ADU peste negru; sigma = a excesului local, scara detectorului; amplit. = lights / dark la pixelii fierbinti ai masterului]");
    }

    /// <summary>
    /// One row per light, the cells formatted as the console prints them, on the reference plane;
    /// empty without lights.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>One row per light in file order; a skipped or failed light has its reason and empty
    /// cells.</returns>
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

    /// <summary>
    /// "+2.3 degrees", or "0.0 degrees" when the ratio is not positive (no degrees from a ratio of
    /// 0); "grade" in Romanian.
    /// </summary>
    /// <param name="m">The match, whose temperature offset is printed.</param>
    /// <returns>The signed degrees with one decimal and the word for degrees.</returns>
    private static string Degrees(MatchFigures m) => (double.IsNaN(m.DeltaCelsius) ? "0.0" : Signed(m.DeltaCelsius, 1)) + T(" degrees", " grade");

    /// <summary>
    /// The words of a match verdict, the master being the session's dark or the partner and
    /// <paramref name="set"/> the set measured against it ("the lights", "the session's darks"):
    /// what Siril can do with it.
    /// </summary>
    /// <param name="m">The match; not null.</param>
    /// <param name="partner">True when the master is the partner, false when it is the session's
    /// dark.</param>
    /// <param name="set">The words for the set measured against the master, in the run's
    /// language.</param>
    /// <returns>The verdict's words, opening with OK, WARNING or UNMEASURED (OK, ATENTIE or
    /// NEMASURAT in Romanian).</returns>
    internal static string MatchText(MatchFigures m, bool partner, string set)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (m.Verdict == MatchVerdict.Unmatched && (m.Ratio <= 0 || m.Q25 <= 0))
        {
            // the ratios sit around zero: the set shows none of the master's hot pixels
            return T(
                $"WARNING: mismatched: {set} show none of the {(partner ? "partner's" : "dark's")} hot pixels (ratio {F(m.Ratio, 2)}; in-camera noise reduction, another camera or another orientation?)",
                $"ATENTIE: nepotrivit: {set} nu arata pixelii fierbinti ai {(partner ? "partenerului" : "dark-ului")} (raport {F(m.Ratio, 2)}; reducere de zgomot in aparat, alt aparat sau alta orientare?)");
        }

        string master = partner ? T("the partner", "partenerul") : T("the dark", "dark-ul");
        string where = Math.Abs(m.DeltaCelsius) < Th.SameTemperatureCelsius
            ? T($"{master} is at the same temperature", $"{master} e la aceeasi temperatura")
            : T(
                $"{master} is {(m.DeltaCelsius < 0 ? "warmer" : "colder")} by {F(Math.Abs(m.DeltaCelsius), 1)} degrees",
                $"{master} e {(m.DeltaCelsius < 0 ? "mai cald" : "mai rece")} cu {F(Math.Abs(m.DeltaCelsius), 1)} grade");
        return m.Verdict switch
        {
            MatchVerdict.Unmeasured => T("UNMEASURED", "NEMASURAT"),
            MatchVerdict.Direct => T(
                $"OK: direct subtraction (ratio {F(m.Ratio, 2)}, in {F(Th.MatchDirectLow, 1)} .. {F(Th.MatchDirectHigh, 1)}; {where})",
                $"OK: scadere directa (raport {F(m.Ratio, 2)}, in {F(Th.MatchDirectLow, 1)} .. {F(Th.MatchDirectHigh, 1)}; {where})"),
            MatchVerdict.WithOpt => T(
                $"WARNING: subtract with -opt (ratio {F(m.Ratio, 2)}, in {F(Th.MatchOptLow, 1)} .. {F(Th.MatchOptHigh, 1)}; {where}, -opt scales the amplitude)",
                $"ATENTIE: scade cu -opt (raport {F(m.Ratio, 2)}, in {F(Th.MatchOptLow, 1)} .. {F(Th.MatchOptHigh, 1)}; {where}, -opt scaleaza amplitudinea)"),
            MatchVerdict.CosmeticOnly => T(
                $"WARNING: cosmetic correction only (-cc=dark): ratio {F(m.Ratio, 2)}, {where} and still finds every hot pixel, but subtracting it would overcorrect",
                $"ATENTIE: doar corectie cosmetica (-cc=dark): raport {F(m.Ratio, 2)}, {where} si vede toti pixelii fierbinti, dar scaderea l-ar supracorecta"),
            _ => T(
                $"WARNING: mismatched (ratio {F(m.Ratio, 2)}: {where}; {(m.Ratio > 1 ? "too cold, it misses the faint hot pixels" : "too warm")})",
                $"ATENTIE: nepotrivit (raport {F(m.Ratio, 2)}: {where}; {(m.Ratio > 1 ? "prea rece, pierde pixelii fierbinti slabi" : "prea cald")})"),
        };
    }

    /// <summary>The report's word for a verdict, in the run's language.</summary>
    /// <param name="v">The verdict.</param>
    /// <returns>"OK", "WARNING" ("ATENTIE"), or "UNMEASURED" ("NEMASURAT") for an unmeasured
    /// one.</returns>
    internal static string VerdictWord(Verdict v) => v switch
    {
        Verdict.Ok => "OK",
        Verdict.Warning => T("WARNING", "ATENTIE"),
        _ => T("UNMEASURED", "NEMASURAT"),
    };

    /// <summary>
    /// "ADU" when the normalisation is 1, "ADU-eq" otherwise: the unit the figures carry.
    /// </summary>
    /// <param name="d">The dark series, whose normalisation factor decides.</param>
    /// <returns>"ADU" or "ADU-eq".</returns>
    internal static string Eq(DarkSeriesAnalysis d) => Math.Abs(d.Norm - 1) < 1e-9 ? "ADU" : "ADU-eq";

    /// <summary>
    /// The value of the <c>normalizare</c> row: the settings of the first readable dark, the
    /// factor to the reference ISO and exposure, and the gain per ISO stop when it is not the
    /// default 2.
    /// </summary>
    /// <param name="d">The dark series; not null.</param>
    /// <returns>The row's value, in the run's language.</returns>
    internal static string Normalisation(DarkSeriesAnalysis d)
    {
        ArgumentNullException.ThrowIfNull(d);
        FrameInfo? first = d.Frames.FirstOrDefault(f => f.Error is null)?.Info;
        string from = first?.Iso is double iso && first.ExposureSeconds is double e
            ? $"ISO {F(iso, 0)}, {InventoryReport.Exposure(e)} s"
            : T("ISO or exposure unknown", "ISO sau expunere necunoscute");
        string gain = d.Normalisation.IsoFactorPerStop == 2 ? "" : T(
            $"; gain x{F(d.Normalisation.IsoFactorPerStop, 2)} per ISO stop",
            $"; castig x{F(d.Normalisation.IsoFactorPerStop, 2)} pe treapta de ISO");
        string against = T("relative to", "fata de");
        string andWord = T("and", "si");
        return Math.Abs(d.Norm - 1) < 1e-9
            ? $"{from} -> x1.00 {against} ISO {F(Th.NormIso, 0)} {andWord} {F(Th.NormExposureSeconds, 0)} s (ADU-eq = ADU){gain}"
            : $"{from} -> x{F(d.Norm, 2)} {against} ISO {F(Th.NormIso, 0)} {andWord} {F(Th.NormExposureSeconds, 0)} s (1 ADU-eq = {F(d.Norm, 2)} ADU){gain}";
    }

    /// <summary>The name of a map region (a corner or an edge), in the run's language.</summary>
    /// <param name="r">The region.</param>
    /// <returns>The name, as "top-left" or "bottom" ("stanga-sus" or "jos" in Romanian).</returns>
    internal static string RegionName(MapRegion r) => r switch
    {
        MapRegion.TopLeft => T("top-left", "stanga-sus"),
        MapRegion.TopRight => T("top-right", "dreapta-sus"),
        MapRegion.BottomLeft => T("bottom-left", "stanga-jos"),
        MapRegion.BottomRight => T("bottom-right", "dreapta-jos"),
        MapRegion.Top => T("top", "sus"),
        MapRegion.Bottom => T("bottom", "jos"),
        MapRegion.Left => T("left", "stanga"),
        _ => T("right", "dreapta"),
    };

    /// <summary>
    /// The centre, the four corners and the four edges of a binned map, each divided by the
    /// normalisation.
    /// </summary>
    /// <param name="r">The map's region values, in raw ADU above the black.</param>
    /// <param name="n">The normalisation factor the values are divided by; 1 for raw ADU.</param>
    /// <returns>The line, in the run's language.</returns>
    private static string RegionsLine(Regions r, double n) => T(
        $"centre {F(r.Centre / n, 2)}, corners {F(r.TopLeft / n, 2)} / {F(r.TopRight / n, 2)} / {F(r.BottomLeft / n, 2)} / {F(r.BottomRight / n, 2)} (TL / TR / BL / BR), edges top {F(r.Top / n, 2)} bottom {F(r.Bottom / n, 2)} left {F(r.Left / n, 2)} right {F(r.Right / n, 2)}",
        $"centru {F(r.Centre / n, 2)}, colturi {F(r.TopLeft / n, 2)} / {F(r.TopRight / n, 2)} / {F(r.BottomLeft / n, 2)} / {F(r.BottomRight / n, 2)} (SS / DS / SJ / DJ), margini sus {F(r.Top / n, 2)} jos {F(r.Bottom / n, 2)} stanga {F(r.Left / n, 2)} dreapta {F(r.Right / n, 2)}");

    /// <summary>
    /// Check 2's verdict row: the words of <see cref="Leak"/>, with the constant light and the
    /// master's noise marked bold when the verdict names them (ADR 0016).
    /// </summary>
    /// <param name="l">Check 2's verdict.</param>
    /// <param name="eq">The unit word of the figures.</param>
    /// <param name="norm">The normalisation factor, which turns the constant light back into raw
    /// ADU.</param>
    /// <param name="masterNoise">The dark master's noise in raw ADU.</param>
    /// <returns>The row, with the parts to mark bold, or none.</returns>
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

    /// <summary>
    /// The verdict's words; <paramref name="against"/> is the constant light's ratio to the
    /// master's noise, already worded, or empty.
    /// </summary>
    /// <param name="l">Check 2's verdict.</param>
    /// <param name="eq">The unit word of the figures.</param>
    /// <param name="against">The constant light set against the masters' noise, already worded, or
    /// empty.</param>
    /// <returns>The verdict's words, opening with OK, WARNING or UNMEASURED (OK, ATENTIE or
    /// NEMASURAT in Romanian).</returns>
    private static string Leak(LeakVerdict l, string eq, string against)
    {
        string source = l.FromDarkMinusBias ? T("on dark - bias", "pe dark - bias") : T("on the dark master (no biases)", "pe masterul dark (fara bias)");
        string framesTail = l.Frames is null
            ? T("light per frame UNMEASURED", "lumina pe cadre NEMASURATA")
            : T("no frame with light above the series' warming", "niciun cadru cu lumina peste incalzirea seriei");
        if (l.Verdict == Verdict.Unmeasured)
        {
            return T("UNMEASURED: the map has no values (frames smaller than a block)", "NEMASURAT: harta nu are valori (cadre mai mici de un bloc)");
        }

        if (l.Verdict == Verdict.Ok)
        {
            string sidesTail = l.SidesAfterBias
                ? T(
                    $"; no side with constant light above {F(Th.LeakConstantAdu, 2)} and {F(Th.LeakConstantSigmas, 0)} x the error",
                    $"; nicio latura cu lumina constanta peste {F(Th.LeakConstantAdu, 2)} si {F(Th.LeakConstantSigmas, 0)} x eroarea")
                : "";
            return T(
                $"OK: no edge or corner more than {F(Th.LeakAdu, 1)} {eq} from the centre (the farthest {Signed(l.OffsetAdu, 2)} at the {RegionName(l.Where)}, {source}); {framesTail}{sidesTail}",
                $"OK: nicio margine sau colt la mai mult de {F(Th.LeakAdu, 1)} {eq} de centru (cel mai departe {Signed(l.OffsetAdu, 2)} la {RegionName(l.Where)}, {source}); {framesTail}{sidesTail}");
        }

        SideLight? constant = l.ConstantLight;
        List<string> parts = [];
        if (l.Frames is FrameLight fl && fl.LitFrames.Count > 0)
        {
            int n = fl.LitFrames.Count;
            parts.Add(T(
                $"{n} {(n == 1 ? "frame has" : "frames have")} more light than the rest of the series ({LitList(fl, ": ")} {eq}); {(constant is null ? "take them out of darks\\ or redo the series" : "redo the series")}",
                $"{n} {(n == 1 ? "cadru are" : "cadre au")} lumina in plus fata de restul seriei ({LitList(fl, ": ")} {eq}); {(constant is null ? "scoate-le din darks\\ sau refa seria" : "refa seria")}"));
        }

        if (constant is SideLight c)
        {
            parts.Add(T(
                $"constant light in every frame: {Signed(c.LightAdu, 2)} {eq} (±{F(c.ErrorAdu, 2)}) on the {SideName(c.Side)} side against the centre, not in the bias: a light leak (eyepiece, mount)",
                $"lumina constanta in toate cadrele: {Signed(c.LightAdu, 2)} {eq} (±{F(c.ErrorAdu, 2)}) pe latura de {SideName(c.Side)} fata de centru, nu e in bias: scurgere de lumina (ocular, montura)")
                + (against.Length == 0 ? "" : T("; that is, ", "; adica ") + against));
        }

        if (Math.Abs(l.OffsetAdu) > Th.LeakAdu)
        {
            string cause = l.OffsetAdu > 0
                ? CauseWords(l)
                : T("an edge darker than the centre, a readout offset or a mismatched bias", "margine mai intunecata decat centrul, offset de citire sau bias nepotrivit");
            parts.Add(T(
                $"the {RegionName(l.Where)} is at {Signed(l.OffsetAdu, 2)} {eq} from the centre ({source}); above {F(Th.LeakAdu, 1)}: {cause}",
                $"{RegionName(l.Where)} e la {Signed(l.OffsetAdu, 2)} {eq} de centru ({source}); peste {F(Th.LeakAdu, 1)}: {cause}"));
        }

        return T("WARNING: ", "ATENTIE: ") + string.Join(T("; also ", "; in plus "), parts);
    }

    /// <summary>
    /// The row `lumina constanta` (R-029, ADR 0014): the four sides after the bias, or their
    /// constants without biases; a side over the threshold against the masters' noise, bold
    /// (ADR 0016).
    /// </summary>
    /// <param name="l">Check 2's verdict.</param>
    /// <param name="eq">The unit word of the figures.</param>
    /// <param name="norm">The normalisation factor, which turns the light back into raw
    /// ADU.</param>
    /// <param name="masterNoise">The dark master's noise in raw ADU.</param>
    /// <param name="stack">The lights' sky figures for the comparison with the stack, or null
    /// without lights.</param>
    /// <returns>The row; the side over the threshold and its comparison are bold.</returns>
    private static ReportRow SidesRow(LeakVerdict l, string eq, double norm, double masterNoise, StackNoise? stack)
    {
        string label = T("constant light", "lumina constanta");
        if (SplitUnmeasured(l, Th.LeakSidesMinFrames) is string why)
        {
            return new(label, why);
        }

        if (l.Sides.Count == 0)
        {
            return new(label, T("UNMEASURED", "NEMASURAT"));
        }

        MapSide[] order = [MapSide.Top, MapSide.Bottom, MapSide.Left, MapSide.Right];
        if (!l.SidesAfterBias)
        {
            return new(label, T("without biases it cannot be told from the readout offset: constant ", "fara bias nu se desparte de offsetul de citire: constant ") + string.Join(", ", order.Select(s => $"{SideName(s)} {Signed(l.Sides.First(x => x.Side == s).ConstantAdu, 2)}")) + $" {eq}");
        }

        string sides = string.Join(", ", order.Select(s => l.Sides.First(x => x.Side == s)).Select(s => $"{SideName(s.Side)} {Signed(s.LightAdu, 2)} (±{F(s.ErrorAdu, 2)})"));
        if (l.ConstantLight is not SideLight c)
        {
            return new(label, T(
                $"after the bias, against the centre: {sides} {eq}; none above {F(Th.LeakConstantAdu, 2)} and above {F(Th.LeakConstantSigmas, 0)} x the error; a uniform light over the whole frame, in every frame, has no reference",
                $"dupa bias, fata de centru: {sides} {eq}; niciuna peste {F(Th.LeakConstantAdu, 2)} si peste {F(Th.LeakConstantSigmas, 0)} x eroarea; o lumina uniforma pe tot cadrul, in toate cadrele, nu are reper"));
        }

        (string against, IReadOnlyList<string> bold) = ScaleReport.AgainstMasters(c.LightAdu * norm, masterNoise, stack);
        string tail = against.Length == 0 ? "" : $"; {against}";
        return new(label, T(
            $"after the bias, against the centre: {sides} {eq}; above {F(Th.LeakConstantAdu, 2)} and above {F(Th.LeakConstantSigmas, 0)} x the error on the {SideName(c.Side)} side{tail}",
            $"dupa bias, fata de centru: {sides} {eq}; peste {F(Th.LeakConstantAdu, 2)} si peste {F(Th.LeakConstantSigmas, 0)} x eroarea la {SideName(c.Side)}{tail}"), [$"{Signed(c.LightAdu, 2)} (±{F(c.ErrorAdu, 2)})", .. bold]);
    }

    /// <summary>The name of a side of the frame, in the run's language.</summary>
    /// <param name="s">The side.</param>
    /// <returns>"top", "bottom", "left" or "right" ("sus", "jos", "stanga" or "dreapta" in
    /// Romanian).</returns>
    internal static string SideName(MapSide s) => s switch
    {
        MapSide.Top => T("top", "sus"),
        MapSide.Bottom => T("bottom", "jos"),
        MapSide.Left => T("left", "stanga"),
        _ => T("right", "dreapta"),
    };

    /// <summary>
    /// The words of R-021's brighter case, from R-029's cause of the verdict region.
    /// </summary>
    /// <param name="l">Check 2's verdict; its split must be set for every cause but the
    /// fallback.</param>
    /// <returns>The words of the cause, in the run's language.</returns>
    private static string CauseWords(LeakVerdict l)
    {
        EdgeSplit? s = l.Split;
        return l.Cause switch
        {
            LeakCause.Glow => T(
                $"it grows with the warming (glow {Signed(s!.GlowAdu, 2)}, constant {Signed(s.ConstantAdu, 2)}): the sensor's glow, the darks stay good",
                $"creste cu incalzirea (glow {Signed(s!.GlowAdu, 2)}, constant {Signed(s.ConstantAdu, 2)}): glow al senzorului, dark-urile raman bune"),
            LeakCause.Leak => T(
                $"it does not grow with the warming and is not in the bias (constant {Signed(s!.RemainderAdu, 2)}, glow {Signed(s.GlowAdu, 2)}): a light leak, redo the darks",
                $"nu creste cu incalzirea si nu e in bias (constant {Signed(s!.RemainderAdu, 2)}, glow {Signed(s.GlowAdu, 2)}): scurgere de lumina, refa dark-urile"),
            LeakCause.Readout => T(
                $"it is in the bias too (readout offset {Signed(s!.ReadoutAdu!.Value, 2)}, glow {Signed(s.GlowAdu, 2)}): a readout offset, not light",
                $"e si in bias (offset de citire {Signed(s!.ReadoutAdu!.Value, 2)}, glow {Signed(s.GlowAdu, 2)}): offset de citire, nu lumina"),
            LeakCause.Constant => T(
                $"it does not grow with the warming (constant {Signed(s!.ConstantAdu, 2)}, glow {Signed(s.GlowAdu, 2)}): a light leak or a readout offset; without biases the two cannot be told apart",
                $"nu creste cu incalzirea (constant {Signed(s!.ConstantAdu, 2)}, glow {Signed(s.GlowAdu, 2)}): scurgere de lumina sau offset de citire, fara bias-uri nu se pot deosebi"),
            _ => T("a light leak or glow", "scurgere de lumina sau glow"),
        };
    }

    /// <summary>The lit frames with their excess, at most eight, then how many more.</summary>
    /// <param name="fl">The per-frame light figures.</param>
    /// <param name="sep">What goes between a frame's number and its excess.</param>
    /// <returns>The list, as "3 +0.45, 7 +0.52 and 2 more" ("si inca 2" in Romanian).</returns>
    private static string LitList(FrameLight fl, string sep)
    {
        const int shown = 8;
        List<string> parts = [];
        for (int k = 0; k < Math.Min(shown, fl.LitFrames.Count); k++)
        {
            parts.Add($"{fl.LitFrames[k].ToString(CultureInfo.InvariantCulture)}{sep}{Signed(fl.LitExcessAdu[k], 2)}");
        }

        int more = fl.LitFrames.Count - shown;
        string count = more.ToString(CultureInfo.InvariantCulture);
        return string.Join(", ", parts) + (more > 0 ? T($" and {count} more", $" si inca {count}") : "");
    }

    /// <summary>
    /// The row `lumina pe cadre` (R-028); with lit frames, the largest excess against the masters'
    /// noise, both bold (ADR 0016).
    /// </summary>
    /// <param name="l">Check 2's verdict.</param>
    /// <param name="eq">The unit word of the figures.</param>
    /// <param name="norm">The normalisation factor, which turns the excess back into raw
    /// ADU.</param>
    /// <param name="masterNoise">The dark master's noise in raw ADU.</param>
    /// <param name="stack">The lights' sky figures for the comparison with the stack, or null
    /// without lights.</param>
    /// <returns>The row.</returns>
    private static ReportRow FrameLightRow(LeakVerdict l, string eq, double norm, double masterNoise, StackNoise? stack)
    {
        string label = T("light per frame", "lumina pe cadre");
        if (l.Frames is not FrameLight fl)
        {
            return new(label, T($"UNMEASURED: fewer than {Th.LeakFitMinFrames} frames with hot pixels", $"NEMASURAT: sub {Th.LeakFitMinFrames} cadre cu pixeli fierbinti"));
        }

        if (fl.LitFrames.Count == 0)
        {
            return new(label, T(
                $"no frame above {F(Th.LeakFrameSigmas, 0)} x the typical deviation ({F(fl.ScaleAdu, 2)} {eq}) and above {F(Th.LeakFrameAdu, 1)} {eq} from the line of the mean against the warming",
                $"niciun cadru peste {F(Th.LeakFrameSigmas, 0)} x abaterea tipica ({F(fl.ScaleAdu, 2)} {eq}) si peste {F(Th.LeakFrameAdu, 1)} {eq} fata de dreapta mediei pe incalzire"));
        }

        int n = fl.LitFrames.Count;
        double max = fl.LitExcessAdu.Max();
        (string against, IReadOnlyList<string> bold) = ScaleReport.AgainstMasters(max * norm, masterNoise, stack);
        string tail = against.Length == 0 ? "" : T($"; the brightest, {Signed(max, 2)} {eq}: {against}", $"; cel mai luminat, {Signed(max, 2)} {eq}: {against}");
        return new(label, T(
            $"{n} {(n == 1 ? "frame" : "frames")}: {LitList(fl, " ")} {eq} above the line of the mean against the warming (typical deviation {F(fl.ScaleAdu, 2)}){tail}",
            $"{n} {(n == 1 ? "cadru" : "cadre")}: {LitList(fl, " ")} {eq} peste dreapta mediei pe incalzire (abaterea tipica {F(fl.ScaleAdu, 2)}){tail}"), bold.Count == 0 ? null : [$"{Signed(max, 2)} {eq}", .. bold]);
    }

    /// <summary>
    /// Why the split (or the sides, which need <paramref name="minFrames"/> frames left) is
    /// unmeasured, or null when it is measured (R-028, R-029).
    /// </summary>
    /// <param name="l">Check 2's verdict.</param>
    /// <param name="minFrames">How many frames must be left once the lit ones are dropped.</param>
    /// <returns>The UNMEASURED words (NEMASURAT in Romanian), or null.</returns>
    private static string? SplitUnmeasured(LeakVerdict l, int minFrames)
    {
        if (l.Frames is not FrameLight fl)
        {
            return T($"UNMEASURED: fewer than {Th.LeakFitMinFrames} frames with hot pixels", $"NEMASURAT: sub {Th.LeakFitMinFrames} cadre cu pixeli fierbinti");
        }

        if (fl.RatioSpan < Th.LeakFitMinSpan)
        {
            return T(
                $"UNMEASURED: the series warmed too little to separate the glow from the constant (the amplitude ratio spans {F(fl.RatioSpan, 2)}, under {F(Th.LeakFitMinSpan, 1)})",
                $"NEMASURAT: seria s-a incalzit prea putin ca sa desparta glow-ul de constant (raportul de amplitudine se intinde pe {F(fl.RatioSpan, 2)}, sub {F(Th.LeakFitMinSpan, 1)})");
        }

        if (fl.FramesKept < minFrames)
        {
            return T(
                $"UNMEASURED: too few frames left once the lit ones are out ({fl.FramesKept}, under {minFrames})",
                $"NEMASURAT: prea putine cadre ramase dupa cele luminate ({fl.FramesKept}, sub {minFrames})");
        }

        return null;
    }

    /// <summary>
    /// The row `glow / constant` (R-029): the verdict region's split, with the raw ADU beside the
    /// equivalent when the two differ.
    /// </summary>
    /// <param name="l">Check 2's verdict.</param>
    /// <param name="eq">The unit word of the figures.</param>
    /// <param name="norm">The normalisation factor; when not 1 the raw ADU are added.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string SplitRow(LeakVerdict l, string eq, double norm)
    {
        if (SplitUnmeasured(l, 3) is string why)
        {
            return why;
        }

        if (l.Split is not EdgeSplit s)
        {
            return T("UNMEASURED", "NEMASURAT");
        }

        string raw = Math.Abs(norm - 1) < 1e-9 ? "" : $" ({T("raw", "brut")}: glow {Signed(s.GlowAdu * norm, 2)}, constant {Signed(s.ConstantAdu * norm, 2)} ADU)";
        return s.ReadoutAdu is double ro
            ? T(
                $"at the {RegionName(s.Where)}: glow {Signed(s.GlowAdu, 2)} (grows with the warming), constant {Signed(s.ConstantAdu, 2)}, of which in the bias {Signed(ro, 2)}, leaving {Signed(s.RemainderAdu, 2)} {eq}{raw}",
                $"la {RegionName(s.Where)}: glow {Signed(s.GlowAdu, 2)} (creste cu incalzirea), constant {Signed(s.ConstantAdu, 2)}, din care in bias {Signed(ro, 2)}, ramane {Signed(s.RemainderAdu, 2)} {eq}{raw}")
            : T(
                $"at the {RegionName(s.Where)}: glow {Signed(s.GlowAdu, 2)} (grows with the warming), constant {Signed(s.ConstantAdu, 2)} {eq} (no biases: light or a readout offset){raw}",
                $"la {RegionName(s.Where)}: glow {Signed(s.GlowAdu, 2)} (creste cu incalzirea), constant {Signed(s.ConstantAdu, 2)} {eq} (fara bias: lumina sau offset de citire){raw}");
    }

    /// <summary>
    /// The <c>amplitudine</c> row of check 3: the hot pixels' level in the first and the last frame
    /// against the master.
    /// </summary>
    /// <param name="d">The dark series.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string Amplitude(DarkSeriesAnalysis d) =>
        double.IsNaN(d.Warming.FirstRatio)
            ? T("UNMEASURED: too few frames or no hot pixel", "NEMASURAT: prea putine cadre sau niciun pixel fierbinte")
            : T(
                $"the hot pixels at {F(d.Warming.FirstRatio, 2)} of the master in the first frame, {F(d.Warming.LastRatio, 2)} in the last ({d.Warming.Pixels} pixels above {F(Th.HotAdu, 0)} {Eq(d)})",
                $"pixelii fierbinti la {F(d.Warming.FirstRatio, 2)} din master in primul cadru, {F(d.Warming.LastRatio, 2)} in ultimul ({d.Warming.Pixels} pixeli peste {F(Th.HotAdu, 0)} {Eq(d)})");

    /// <summary>The words of a series too short for a plateau, in the run's language.</summary>
    private static string NoPlateau => T(
        $"UNMEASURED: fewer than {Th.WarmingFitMinFrames} frames with hot pixels",
        $"NEMASURAT: sub {Th.WarmingFitMinFrames} cadre cu pixeli fierbinti");

    /// <summary>
    /// The `platou` row of check 3: the plateau, the time constant, the master's and the last
    /// frame's distance to it; a flat series says so.
    /// </summary>
    /// <param name="w">The series' warming fit.</param>
    /// <param name="nz">The normalisation, which turns ratios into degrees.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string PlateauRow(Warming w, Normalisation nz)
    {
        if (!w.HasFit)
        {
            return NoPlateau;
        }

        double firstOffset = Math.Abs(nz.Degrees(w.FirstRatio / w.Plateau));
        if (firstOffset < Th.SettledCelsius && Math.Abs(w.SettleCelsius) < Th.SettledCelsius)
        {
            return T(
                $"the series is flat: the plateau at {F(w.Plateau, 2)} of the master, the first frame less than {F(Th.SettledCelsius, 1)} degrees from it",
                $"seria e plata: platoul la {F(w.Plateau, 2)} din master, primul cadru la mai putin de {F(Th.SettledCelsius, 1)} grade de el");
        }

        string unit = w.TimeFromStamps ? "min" : T("frames", "cadre");
        string tau = w.TauAtGridTop
            ? T(
                $"a time constant of at least {F(w.TauMinutes, 0)} {unit}, the series is too short to measure it",
                $"constanta de timp de cel putin {F(w.TauMinutes, 0)} {unit}, seria e prea scurta ca s-o masoare")
            : T($"time constant {F(w.TauMinutes, 0)} {unit}", $"constanta de timp {F(w.TauMinutes, 0)} {unit}");
        return T(
            $"the series approaches a plateau at {F(w.Plateau, 2)} of the master ({tau}); the master, the series' median, is {FromPlateau(w.PlateauCelsius)}; at the last frame it is still {Signed(w.SettleCelsius, 1)} degrees from the plateau",
            $"seria se apropie de un platou la {F(w.Plateau, 2)} din master ({tau}); masterul, mediana seriei, e {FromPlateau(w.PlateauCelsius)}; la ultimul cadru mai are {Signed(w.SettleCelsius, 1)} grade pana la platou");
    }

    /// <summary>
    /// The `de aruncat` row of check 3: the frames more than Th.OffPlateauCelsius from the plateau,
    /// the band's bounds, and where the master would sit without them.
    /// </summary>
    /// <param name="w">The series' warming fit.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string OffPlateauRow(Warming w)
    {
        if (!w.HasFit)
        {
            return NoPlateau;
        }

        string deg = F(Th.OffPlateauCelsius, 1);
        if (w.OffPlateauFrames.Count == 0)
        {
            return T(
                $"no frame more than {deg} degrees from the plateau (ampl. between {F(w.OffPlateauLow, 2)} and {F(w.OffPlateauHigh, 2)})",
                $"niciun cadru la peste {deg} grade de platou (amplit. intre {F(w.OffPlateauLow, 2)} si {F(w.OffPlateauHigh, 2)})");
        }

        string without = double.IsNaN(w.MasterWithoutOffCelsius)
            ? T("without them no frame is left", "fara ele nu ramane niciun cadru")
            : T(
                $"without them the master would be {FromPlateau(w.MasterWithoutOffCelsius)}, but with {F((w.NoiseFactorWithoutOff - 1) * 100, 0)} % more noise",
                $"fara ele masterul ar fi {FromPlateau(w.MasterWithoutOffCelsius)}, dar cu {F((w.NoiseFactorWithoutOff - 1) * 100, 0)} % mai mult zgomot");
        return T(
            $"{RunReport.Frames(w.OffPlateauFrames.Count)} ({Ranges(w.OffPlateauFrames)}) more than {deg} degrees from the plateau (ampl. below {F(w.OffPlateauLow, 2)} or above {F(w.OffPlateauHigh, 2)}; the plateau {F(w.Plateau, 2)}); {without}",
            $"{RunReport.Frames(w.OffPlateauFrames.Count)} ({Ranges(w.OffPlateauFrames)}) la peste {deg} grade de platou (amplit. sub {F(w.OffPlateauLow, 2)} sau peste {F(w.OffPlateauHigh, 2)}; platoul {F(w.Plateau, 2)}); {without}");
    }

    /// <summary>
    /// The verdict row of check 3: whether the series settled, and the frames outside the range
    /// -opt corrects.
    /// </summary>
    /// <param name="v">Check 3's verdict.</param>
    /// <param name="nz">The normalisation, whose doubling rule the words name.</param>
    /// <returns>The verdict's words, opening with OK, WARNING or UNMEASURED (OK, ATENTIE or
    /// NEMASURAT in Romanian).</returns>
    private static string Drift(DriftVerdict v, Normalisation nz)
    {
        Warming w = v.Warming;
        string span = T($"{F(Th.MatchOptLow, 1)} .. {F(Th.MatchOptHigh, 1)} of the master", $"{F(Th.MatchOptLow, 1)} .. {F(Th.MatchOptHigh, 1)} din master");
        if (v.Verdict == Verdict.Unmeasured)
        {
            return T("UNMEASURED", "NEMASURAT");
        }

        if (v.Verdict == Verdict.Ok)
        {
            if (!w.HasFit)
            {
                return T(
                    $"OK: every frame lies in {span} (fewer than {Th.WarmingFitMinFrames} frames, no plateau)",
                    $"OK: toate cadrele stau in {span} (sub {Th.WarmingFitMinFrames} cadre, fara platou)");
            }

            string rule = T($"the doubling rule at {F(nz.DoublingCelsius, 0)} degrees", $"regula dublarii la {F(nz.DoublingCelsius, 0)} grade");
            string minutes = double.IsNaN(w.Minutes) ? "" : $" in {F(w.Minutes, 0)} min";
            string warmup = Math.Abs(w.DeltaCelsius) < 0.05
                ? T($"the series is flat ({rule})", $"seria e plata ({rule})")
                : T(
                    $"the {(w.DeltaCelsius > 0 ? "warming" : "cooling")} of {F(Math.Abs(w.DeltaCelsius), 1)} degrees{minutes} is at the start ({rule})",
                    $"{(w.DeltaCelsius > 0 ? "incalzirea" : "racirea")} de {F(Math.Abs(w.DeltaCelsius), 1)} grade{minutes} e la inceput ({rule})");
            string off = w.OffPlateauFrames.Count == 0
                ? T("no frame to discard", "niciun cadru de aruncat")
                : T("the frames to discard are in the row above", "cadrele de aruncat sunt in randul de mai sus");
            return T(
                $"OK: the series settled (at the last frame it is still {Signed(w.SettleCelsius, 1)} degrees from the plateau, under {F(Th.SettledCelsius, 1)}) and every frame lies in {span}; {warmup}, {off}",
                $"OK: seria s-a asezat (la ultimul cadru mai are {Signed(w.SettleCelsius, 1)} grade pana la platou, sub {F(Th.SettledCelsius, 1)}) si toate cadrele stau in {span}; {warmup}, {off}");
        }

        List<string> parts = [];
        if (!v.Settled)
        {
            string extrapolated = w.TauAtGridTop ? T(", the plateau is extrapolated", ", platoul e extrapolat") : "";
            string advice = w.OffPlateauFrames.Count > 0
                ? T("continue the series or discard the frames in the row above", "continua seria sau arunca cadrele din randul de mai sus")
                : T("continue the series", "continua seria");
            parts.Add(T(
                $"the series is still {(w.SettleCelsius > 0 ? "warming" : "cooling")} at the last frame (still {Signed(w.SettleCelsius, 1)} degrees from the plateau, above {F(Th.SettledCelsius, 1)}{extrapolated}): the master belongs to no single temperature; {advice}",
                $"seria inca se {(w.SettleCelsius > 0 ? "incalzeste" : "raceste")} la ultimul cadru (mai are {Signed(w.SettleCelsius, 1)} grade pana la platou, peste {F(Th.SettledCelsius, 1)}{extrapolated}): masterul nu apartine unei temperaturi; {advice}"));
        }

        if (v.OutsideFrames.Count > 0)
        {
            int n = v.OutsideFrames.Count;
            parts.Add(T(
                $"{RunReport.Frames(n)} ({Ranges(v.OutsideFrames)}) {(n == 1 ? "falls" : "fall")} outside {span} (the farthest {F(v.FarthestRatio, 2)}): the series started {(v.FarthestRatio < 1 ? "cold" : "warm")}; discard {(n == 1 ? "it" : "them")}",
                $"{RunReport.Frames(n)} ({Ranges(v.OutsideFrames)}) {(n == 1 ? "iese" : "ies")} din {span} (cel mai departe {F(v.FarthestRatio, 2)}): seria a pornit {(v.FarthestRatio < 1 ? "rece" : "calda")}; arunca-{(n == 1 ? "l" : "le")}"));
        }

        return T("WARNING: ", "ATENTIE: ") + string.Join("; ", parts);
    }

    /// <summary>
    /// A distance to the plateau in words: "at the plateau", or the degrees under or over it.
    /// </summary>
    /// <param name="degrees">The distance in degrees, negative under the plateau.</param>
    /// <returns>The words, in the run's language.</returns>
    private static string FromPlateau(double degrees) =>
        Math.Abs(degrees) < 0.05
            ? T("at the plateau", "la platou")
            : T(
                $"{F(Math.Abs(degrees), 1)} degrees {(degrees < 0 ? "below" : "above")} the plateau",
                $"cu {F(Math.Abs(degrees), 1)} grade {(degrees < 0 ? "sub" : "peste")} platou");

    /// <summary>
    /// Frame numbers as ranges: `1 .. 11`, `1 .. 3, 7`; a run of two prints both numbers.
    /// </summary>
    /// <param name="frames">The frame numbers, ascending; not null.</param>
    /// <returns>The ranges joined by ", "; empty for no frames.</returns>
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

    /// <summary>
    /// The <c>sigma rezidual</c> row of check 4: each plane's smallest and largest
    /// frame-minus-master sigma, with the median on the reference plane.
    /// </summary>
    /// <param name="d">The dark series.</param>
    /// <param name="rp">The reference plane.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string ResidualRange(DarkSeriesAnalysis d, int rp)
    {
        if (d.Used < 2)
        {
            return T("UNMEASURED: a single frame has no residual against its own median", "NEMASURAT: un singur cadru nu are rezidual fata de propria mediana");
        }

        List<string> parts = [];
        for (int p = 0; p < 4; p++)
        {
            double[] s = d.Frames.Where(f => f.Error is null).Select(f => f.Residuals[p].Sigma).ToArray();
            if (s.Length == 0)
            {
                continue;
            }

            parts.Add($"{d.PlaneNames[p]} {F(s.Min(), 2)} .. {F(s.Max(), 2)}" + (p == rp ? $" ({T("median", "mediana")} {F(Planes.Median([.. s.Select(x => (float)x)]), 2)})" : ""));
        }

        return parts.Count == 0 ? T("UNMEASURED", "NEMASURAT") : string.Join("; ", parts);
    }

    /// <summary>
    /// The <c>zgomot de citire</c> row of check 4: the biases' read noise per plane and the
    /// temporal noise above it.
    /// </summary>
    /// <param name="r">The run's result.</param>
    /// <param name="rp">The reference plane.</param>
    /// <returns>The row's value, in the run's language; UNMEASURED (NEMASURAT) without
    /// biases.</returns>
    private static string ReadNoise(DarkSeriesResult r, int rp)
    {
        if (r.Biases is not BiasAnalysis b)
        {
            return T("UNMEASURED: no biases", "NEMASURAT: fara bias-uri");
        }

        string planes = string.Join("  ", Enumerable.Range(0, 4).Select(p => $"{b.PlaneNames[p]} {F(b.ReadNoise[p], 2)}"));
        string shot = r.Verdicts.Noise.NoiseOverRead is double s
            ? T(
                $"; temporal noise above the read noise on {b.PlaneNames[rp]} ~{F(s, 2)} ADU (sqrt(sigma_t^2 - read^2): dark current and quantisation)",
                $"; zgomot temporal peste citire pe {b.PlaneNames[rp]} ~{F(s, 2)} ADU (sqrt(sigma_t^2 - citire^2): curent de intuneric si cuantizare)")
            : "";
        return T(
            $"{planes} ADU (from {Math.Max(b.Used - 1, 0)} pairs of consecutive biases){shot}",
            $"{planes} ADU (din {Math.Max(b.Used - 1, 0)} perechi de bias-uri consecutive){shot}");
    }

    /// <summary>
    /// The <c>benzi aleatoare</c> row of check 4: the random row banding, its ratio to the white
    /// noise, and the columns' ratio.
    /// </summary>
    /// <param name="v">Check 4's verdict.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string Banding(NoiseVerdict v) =>
        double.IsNaN(v.RowBandingAdu)
            ? T("UNMEASURED", "NEMASURAT")
            : T(
                $"horizontal {F(v.RowBandingAdu, 3)} ADU rms per row ({F(v.RowBandingRatio, 1)} times the white noise), vertical {F(v.ColBandingRatio, 1)} times",
                $"orizontale {F(v.RowBandingAdu, 3)} ADU rms pe rand (de {F(v.RowBandingRatio, 1)} ori zgomotul alb), verticale de {F(v.ColBandingRatio, 1)} ori");

    /// <summary>The verdict row of check 4.</summary>
    /// <param name="v">Check 4's verdict.</param>
    /// <returns>The verdict's words, opening with OK, WARNING or UNMEASURED (OK, ATENTIE or
    /// NEMASURAT in Romanian).</returns>
    private static string Noise(NoiseVerdict v)
    {
        string frames = string.Join(", ", v.OutlierFrames.Select(i => i.ToString(CultureInfo.InvariantCulture)));
        return v.Verdict switch
        {
            Verdict.Unmeasured => T("UNMEASURED", "NEMASURAT"),
            Verdict.Ok => T(
                $"OK: no frame with noise above {F(Th.NoiseOutlierFactor, 1)} x the median ({F(v.MedianSigma, 2)} ADU)",
                $"OK: niciun cadru cu zgomot peste {F(Th.NoiseOutlierFactor, 1)} x mediana ({F(v.MedianSigma, 2)} ADU)"),
            _ => T(
                $"WARNING: {v.OutlierFrames.Count} {(v.OutlierFrames.Count == 1 ? "frame" : "frames")} with noise above {F(Th.NoiseOutlierFactor, 1)} x the median ({F(v.MedianSigma, 2)} ADU): {frames}",
                $"ATENTIE: {v.OutlierFrames.Count} {(v.OutlierFrames.Count == 1 ? "cadru" : "cadre")} cu zgomot peste {F(Th.NoiseOutlierFactor, 1)} x mediana ({F(v.MedianSigma, 2)} ADU): {frames}"),
        };
    }

    /// <summary>
    /// The <c>master peste nivel</c> row of check 5: how many master pixels lie above each hot
    /// level, all planes together.
    /// </summary>
    /// <param name="d">The dark series.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string Levels(DarkSeriesAnalysis d)
    {
        List<string> parts = [];
        for (int k = 0; k < Th.HotLevelsAdu.Count; k++)
        {
            int total = d.Master.Sum(m => m.Above[k]);
            parts.Add($"{F(Th.HotLevelsAdu[k], 0)}: {total}");
        }

        return string.Join("; ", parts) + T($"  [{Eq(d)} above the black, the series' median, all planes]", $"  [{Eq(d)} peste negru, mediana seriei, toate planurile]");
    }

    /// <summary>How many master pixels of each plane lie above one hot level.</summary>
    /// <param name="d">The dark series.</param>
    /// <param name="level">The index of the level in <see cref="Th.HotLevelsAdu"/>.</param>
    /// <returns>The counts, as "R 12  G1 9 ...".</returns>
    private static string PerPlane(DarkSeriesAnalysis d, int level) =>
        string.Join("  ", d.Master.Select(m => $"{m.Name} {m.Above[level]}"));

    /// <summary>
    /// The <c>amplitudine</c> row of check 5: the hot pixels' percentiles above the black.
    /// </summary>
    /// <param name="h">The hot pixels' figures, in raw ADU.</param>
    /// <param name="n">The normalisation factor the percentiles are divided by.</param>
    /// <param name="eq">The unit word of the figures.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string Percentiles(HotStability h, double n, string eq) =>
        h.Hot == 0
            ? T("UNMEASURED: no hot pixel", "NEMASURAT: niciun pixel fierbinte")
            : T(
                $"median {F(h.P50 / n, 0)}, p10 {F(h.P10 / n, 0)}, p90 {F(h.P90 / n, 0)}, p99 {F(h.P99 / n, 0)} {eq} (of {h.Hot})",
                $"mediana {F(h.P50 / n, 0)}, p10 {F(h.P10 / n, 0)}, p90 {F(h.P90 / n, 0)}, p99 {F(h.P99 / n, 0)} {eq} (din {h.Hot})");

    /// <summary>
    /// The <c>stabilitate</c> row of check 5: the hot pixels present in every frame, the flickering
    /// ones, their temporal noise and the RTS candidates.
    /// </summary>
    /// <param name="d">The dark series.</param>
    /// <param name="rp">The reference plane.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string Stability(DarkSeriesAnalysis d, int rp)
    {
        HotStability h = d.Hot;
        if (h.Hot == 0)
        {
            return T("UNMEASURED: no hot pixel", "NEMASURAT: niciun pixel fierbinte");
        }

        if (d.Used < 2)
        {
            return T("UNMEASURED: the stability needs at least 2 frames", "NEMASURAT: stabilitatea cere cel putin 2 cadre");
        }

        return T(
            $"{h.Always} of {h.Hot} above half the threshold in all {RunReport.Frames(d.Used)}, {h.Flicker} missing from 2 or more; temporal sigma {F(h.HotTemporalStd, 2)} ADU on the hot ones, {F(h.QuietTemporalStd, 2)} on the rest; {h.RtsCandidates} RTS candidates on {d.PlaneNames[rp]} (temporal sigma above {F(Th.RtsSigmaFactor, 0)} x the noise of one frame, {F(d.Noise.SigmaFrame, 2)})",
            $"{h.Always} din {h.Hot} peste jumatate din prag in toate cele {RunReport.Frames(d.Used)}, {h.Flicker} lipsesc din 2 sau mai multe; sigma temporala {F(h.HotTemporalStd, 2)} ADU la cei fierbinti, {F(h.QuietTemporalStd, 2)} la restul; {h.RtsCandidates} candidati RTS pe {d.PlaneNames[rp]} (sigma temporala peste {F(Th.RtsSigmaFactor, 0)} x zgomotul unui cadru, {F(d.Noise.SigmaFrame, 2)})");
    }

    /// <summary>
    /// The <c>benzi structurate</c> row of check 6: the worst planes' row and column profiles of
    /// the master.
    /// </summary>
    /// <param name="d">The dark series.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string StructuredBanding(DarkSeriesAnalysis d)
    {
        BandingFigures worstRow = d.Banding.MaxBy(b => b.RowP2p)!;
        BandingFigures worstCol = d.Banding.MaxBy(b => b.ColP2p)!;
        double n = d.Norm;
        return T(
            $"rows {F(worstRow.RowStd / n, 3)} {Eq(d)} rms ({F(worstRow.RowP2p / n, 2)} peak to peak, {worstRow.Name}), columns {F(worstCol.ColStd / n, 3)} ({F(worstCol.ColP2p / n, 2)}, {worstCol.Name})  [the master's profile minus a running median over {Th.BandingWindow}]",
            $"randuri {F(worstRow.RowStd / n, 3)} {Eq(d)} rms ({F(worstRow.RowP2p / n, 2)} varf la varf, {worstRow.Name}), coloane {F(worstCol.ColStd / n, 3)} ({F(worstCol.ColP2p / n, 2)}, {worstCol.Name})  [profilul masterului minus mediana glisanta pe {Th.BandingWindow}]");
    }

    /// <summary>
    /// The <c>glow</c> row of check 6: the glow at check 2's region, with the constant beside it
    /// when the split is measured.
    /// </summary>
    /// <param name="l">Check 2's verdict.</param>
    /// <param name="p">Check 6's verdict.</param>
    /// <param name="eq">The unit word of the figures.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string Glow(LeakVerdict l, PatternVerdict p, string eq)
    {
        string source = l.FromDarkMinusBias ? "dark - bias" : T("the dark master", "masterul dark");
        if (double.IsNaN(p.GlowAdu))
        {
            return T("UNMEASURED", "NEMASURAT");
        }

        return l.Split is EdgeSplit s
            ? T(
                $"{Signed(p.GlowAdu, 2)} {eq} at the {RegionName(l.Where)}, the part that grows with the warming (constant {Signed(s.ConstantAdu, 2)} besides it; {source})",
                $"{Signed(p.GlowAdu, 2)} {eq} la {RegionName(l.Where)}, partea care creste cu incalzirea (constant {Signed(s.ConstantAdu, 2)} in afara ei; {source})")
            : T(
                $"{Signed(p.GlowAdu, 2)} {eq} at the {RegionName(l.Where)} against the centre ({source})",
                $"{Signed(p.GlowAdu, 2)} {eq} la {RegionName(l.Where)} fata de centru ({source})");
    }

    /// <summary>The verdict row of check 6.</summary>
    /// <param name="p">Check 6's verdict.</param>
    /// <param name="eq">The unit word of the figures.</param>
    /// <returns>The verdict's words, opening with OK, WARNING or UNMEASURED (OK, ATENTIE or
    /// NEMASURAT in Romanian).</returns>
    private static string Pattern(PatternVerdict p, string eq)
    {
        bool banding = p.BandingP2pAdu > Th.BandingAdu;
        bool glow = Math.Abs(p.GlowAdu) > Th.LeakAdu;
        return p.Verdict switch
        {
            Verdict.Unmeasured => T("UNMEASURED: the map or the profiles have no values", "NEMASURAT: harta sau profilele nu au valori"),
            Verdict.Ok => T(
                $"OK: banding under {F(Th.BandingAdu, 1)} {eq} peak to peak and edges under {F(Th.LeakAdu, 1)}",
                $"OK: benzi sub {F(Th.BandingAdu, 1)} {eq} varf la varf si margini sub {F(Th.LeakAdu, 1)}"),
            _ => T(
                $"WARNING: {(banding ? $"banding of {F(p.BandingP2pAdu, 2)} {eq} peak to peak" : "")}{(banding && glow ? " and " : "")}{(glow ? $"glow of {Signed(p.GlowAdu, 2)} {eq}" : "")}; the pattern stays in lights calibrated without darks",
                $"ATENTIE: {(banding ? $"benzi de {F(p.BandingP2pAdu, 2)} {eq} varf la varf" : "")}{(banding && glow ? " si " : "")}{(glow ? $"glow de {Signed(p.GlowAdu, 2)} {eq}" : "")}; modelul ramane in lights fara dark-uri"),
        };
    }

    /// <summary>The verdict row of check 7.</summary>
    /// <param name="v">Check 7's verdict.</param>
    /// <returns>The verdict's words, opening with OK or WARNING (OK or ATENTIE in
    /// Romanian).</returns>
    private static string DeadSat(DeadSatVerdict v) => v.Verdict == Verdict.Ok
        ? T("OK: no dead or saturated pixel in the master", "OK: niciun pixel mort sau saturat pe master")
        : T(
            $"WARNING: {v.Dead} {(v.Dead == 1 ? "dead pixel" : "dead pixels")}, {v.Saturated} saturated",
            $"ATENTIE: {v.Dead} {(v.Dead == 1 ? "pixel mort" : "pixeli morti")}, {v.Saturated} {(v.Saturated == 1 ? "saturat" : "saturati")}");

    /// <summary>
    /// The <c>zgomotul mediei</c> row of check 8: the measured and the expected noise of the mean
    /// of n frames.
    /// </summary>
    /// <param name="nv">The noise-against-n figures.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string Curve(NoiseVsN nv)
    {
        string expected = T("expected", "asteptat");
        return nv.Curve.Count == 0
            ? T("UNMEASURED: fewer than 2 frames", "NEMASURAT: sub 2 cadre")
            : string.Join("; ", nv.Curve.Select(p => $"n={p.N} {F(p.Measured, 2)} ({expected} {F(p.Expected, 2)})"));
    }

    /// <summary>
    /// The <c>master</c> row of check 8: the median master's noise and what it is computed from.
    /// </summary>
    /// <param name="d">The dark series.</param>
    /// <param name="e">Check 8's verdict.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string Enough(DarkSeriesAnalysis d, EnoughVerdict e)
    {
        if (double.IsNaN(e.MasterNoise))
        {
            return T("UNMEASURED", "NEMASURAT");
        }

        string basis = double.IsNaN(d.Noise.TemporalSigma)
            ? T($"sigma {F(d.Noise.SigmaFrame, 2)}, a single frame", $"sigma {F(d.Noise.SigmaFrame, 2)}, un singur cadru")
            : T($"temporal sigma {F(d.Noise.TemporalSigma, 2)}", $"sigma temporala {F(d.Noise.TemporalSigma, 2)}");
        string half = e.Frames >= 2
            ? T(
                $"; the median of {e.Frames - e.Frames / 2} against the mean of {e.Frames / 2}: {F(d.Noise.MedianHalfVsMeanHalf, 2)}",
                $"; mediana a {e.Frames - e.Frames / 2} fata de media a {e.Frames / 2}: {F(d.Noise.MedianHalfVsMeanHalf, 2)}")
            : "";
        return T(
            $"{RunReport.Frames(e.Frames)} -> {F(e.MasterNoise, 2)} ADU noise of the median master ({F(Th.MedianNoiseFactor, 2)} x {basis} / sqrt({e.Frames})){half}",
            $"{RunReport.Frames(e.Frames)} -> {F(e.MasterNoise, 2)} ADU zgomot al masterului median ({F(Th.MedianNoiseFactor, 2)} x {basis} / sqrt({e.Frames})){half}");
    }

    /// <summary>The verdict row of check 8.</summary>
    /// <param name="e">Check 8's verdict.</param>
    /// <returns>The verdict's words, opening with OK, WARNING or UNMEASURED (OK, ATENTIE or
    /// NEMASURAT in Romanian).</returns>
    private static string EnoughVerdictText(EnoughVerdict e) => e.Verdict switch
    {
        Verdict.Unmeasured => T("UNMEASURED: a single frame is not a series", "NEMASURAT: un singur cadru nu e o serie"),
        Verdict.Ok => T(
            $"OK: {RunReport.Frames(e.Frames)} are enough; the median master's noise ({F(e.MasterNoise, 2)} ADU) is under the quantisation step ({e.QuantStep} ADU) and adds in quadrature in every calibrated light",
            $"OK: {RunReport.Frames(e.Frames)} ajung; zgomotul masterului median ({F(e.MasterNoise, 2)} ADU) e sub pasul de cuantizare ({e.QuantStep} ADU) si intra in cuadratura in fiecare light calibrat"),
        _ when e.TooFew => T(
            $"WARNING: only {RunReport.Frames(e.Frames)}; the median needs at least {Th.MinDarks} so that one odd frame does not pull it (the master's noise would be {F(e.MasterNoise, 2)} ADU)",
            $"ATENTIE: doar {RunReport.Frames(e.Frames)}; mediana cere cel putin {Th.MinDarks} ca un cadru ciudat sa n-o traga (zgomotul masterului ar fi {F(e.MasterNoise, 2)} ADU)"),
        _ => T(
            $"WARNING: the median master's noise ({F(e.MasterNoise, 2)} ADU) is above the quantisation step ({e.QuantStep} ADU); it would drop under the step from {e.FramesNeeded} frames",
            $"ATENTIE: zgomotul masterului median ({F(e.MasterNoise, 2)} ADU) e peste pasul de cuantizare ({e.QuantStep} ADU); ar cobori sub pas de la {e.FramesNeeded} cadre"),
    };

    /// <summary>A ratio with three decimals, or "-" when NaN.</summary>
    /// <param name="v">The ratio.</param>
    /// <returns>The formatted ratio.</returns>
    private static string Ratio(double v) => double.IsNaN(v) ? "-" : F(v, 3);

    /// <summary>
    /// A figure with its sign always written, rounded half away from zero; zero prints as "+0.00".
    /// </summary>
    /// <param name="v">The figure.</param>
    /// <param name="decimals">The number of decimals.</param>
    /// <returns>The formatted figure.</returns>
    internal static string Signed(double v, int decimals)
    {
        double r = Math.Round(v, decimals, MidpointRounding.AwayFromZero);
        return (r < 0 ? "-" : "+") + F(Math.Abs(r), decimals);
    }

    /// <summary>A pixel count in words: "1 pixel", "20 pixels" ("20 pixeli" in Romanian).</summary>
    /// <param name="n">The count.</param>
    /// <returns>The words.</returns>
    private static string Pixels(int n) => n == 1 ? "1 pixel" : n.ToString(CultureInfo.InvariantCulture) + T(" pixels", " pixeli");

    /// <summary>A frame's time of day as HH:mm:ss, or "-" without a timestamp.</summary>
    /// <param name="t">The timestamp, or null.</param>
    /// <returns>The formatted time.</returns>
    private static string Time(DateTime? t) => t is DateTime d ? d.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "-";

    /// <summary>
    /// A figure with fixed decimals; a value that rounds to zero prints without a minus sign.
    /// </summary>
    /// <param name="v">The figure; NaN prints "-".</param>
    /// <param name="decimals">The number of decimals.</param>
    /// <returns>The formatted figure, with the invariant culture.</returns>
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
