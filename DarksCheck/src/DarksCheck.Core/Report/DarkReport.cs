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
                if (f.Error is not null)
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
        return T(
            $"=== DARKS{(r.DarksFromPartner is string pe ? $" (the partner's, {pe})" : "")}: frames read {d.Used} of {d.Frames.Count}, {d.Width} x {d.Height} px, values from {F(d.Black, 0)} (black) to {F(d.White, 0)} ADU (white){ScaleSource(d)}, {(d.CfaLetters is string cfaEn ? "colour filters " + cfaEn : "colour filter order unknown")} ===",
            $"=== DARK-URI{(r.DarksFromPartner is string pr ? $" (ale partenerului {pr})" : "")}: cadre citite {d.Used} din {d.Frames.Count}, {d.Width} x {d.Height} px, valori de la {F(d.Black, 0)} (negru) la {F(d.White, 0)} ADU (alb){ScaleSource(d)}, {(d.CfaLetters is string cfaRo ? "filtre de culoare " + cfaRo : "ordinea filtrelor de culoare necunoscuta")} ===");
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
            new("1 ADU-eq", Normalisation(d)),
            new(T("value step", "pasul valorilor"), T(
                $"near the black, the file's values go up {d.QuantStep} by {d.QuantStep} ADU; the first frame has {d.UniqueValues} different values ({ScaleReport.PlaneWords(d.PlaneNames[r.ReferencePlane])})",
                $"langa negru, valorile din fisier cresc din {d.QuantStep} in {d.QuantStep} ADU; primul cadru are {RoCount(d.UniqueValues)}valori diferite ({ScaleReport.PlaneWords(d.PlaneNames[r.ReferencePlane])})")),
        ];
        if (r.BiasError is not null)
        {
            rows.Add(new(T("WARNING", "ATENTIE"), T($"the biases could not be used: {r.BiasError}", $"bias-urile nu s-au putut folosi: {r.BiasError}")));
        }

        BiasFrameFigures[] badBiases = r.Biases?.Frames.Where(f => f.Error is not null && !f.Skipped).ToArray() ?? [];
        if (badBiases.Length > 0)
        {
            rows.Add(new(T("WARNING", "ATENTIE"), T(
                $"{badBiases.Length} {(badBiases.Length == 1 ? "bias could not be read" : "biases could not be read")} (the first: {badBiases[0].Info.FileName}: {badBiases[0].Error}); the list is in {DarkOutputs.BiasesCsvName}, written with --out",
                $"{RoCount(badBiases.Length)}{(badBiases.Length == 1 ? "bias nu s-a putut citi" : "bias-uri nu s-au putut citi")} (primul: {badBiases[0].Info.FileName}: {badBiases[0].Error}); lista e in {DarkOutputs.BiasesCsvName}, scris cu --out")));
        }

        int skippedBiases = r.Biases?.Frames.Count(f => f.Skipped) ?? 0;
        if (skippedBiases > 0)
        {
            rows.Add(new(T("skipped biases", "bias-uri sarite"), T(
                $"{skippedBiases} of {r.Biases!.Frames.Count} (over the maximum number of frames; {r.Biases.Used} used)",
                $"{skippedBiases} din {r.Biases!.Frames.Count} (peste numarul maxim de cadre; s-au folosit {r.Biases.Used})")));
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
            $"  [{ScaleReport.PlaneWords(g)}, {eq} above the black, mean of {RunReport.Frames(d.Used)}, in squares of {Th.BinPx * 2} px]",
            $"  [{ScaleReport.PlaneWords(g)}, {eq} peste negru, media a {RunReport.Frames(d.Used)}, pe patrate de {Th.BinPx * 2} px]"))];
        if (r.Biases is BiasAnalysis b)
        {
            leak.Add(new("master bias", RegionsLine(b.MeanMaps[rp].Regions, 1) + T(
                $"  [{ScaleReport.PlaneWords(g)}, ADU above the black, mean of {RunReport.Frames(b.Used)}]",
                $"  [{ScaleReport.PlaneWords(g)}, ADU peste negru, media a {RunReport.Frames(b.Used)}]")));
        }

        if (r.DarkCurrent is DarkCurrent dc)
        {
            leak.Add(new("dark - bias", RegionsLine(dc.Maps[rp].Regions, n) + T(
                $"; the average pixel {F(dc.Median / n, 2)}, nearly all (98 %) between {F(dc.P1 / n, 2)} and {F(dc.P99 / n, 2)}",
                $"; pixelul mediu {F(dc.Median / n, 2)}, aproape toti (98 %) intre {F(dc.P1 / n, 2)} si {F(dc.P99 / n, 2)}")));
        }

        leak.Add(FrameLightRow(v.Leak, eq, n, masterNoise, stack));
        leak.Add(new("glow / constant", SplitRow(v.Leak, eq, n)));
        leak.Add(SidesRow(v.Leak, eq, n, masterNoise, stack));
        leak.Add(LeakVerdictRow(v.Leak, eq, n, masterNoise));

        return
        [
            new(T("2. LIGHT LEAKS AND DIFFERENCES ACROSS THE FRAME", "2. SCURGERI DE LUMINA SI DIFERENTE PE CADRU"), leak),
            new(T("3. THE CAMERA'S WARMING DURING THE SERIES", "3. INCALZIREA CAMEREI IN TIMPUL SERIEI"),
            [
                new(T("end of series", "sfarsitul seriei"), EndRow(d.Warming)),
                new(T("to discard", "de aruncat"), OffEndRow(d.Warming)),
                new("verdict", Drift(v.Drift, d.Normalisation)),
            ]),
            new(T("4. THE NOISE OF EACH FRAME", "4. ZGOMOTUL FIECARUI CADRU"),
            [
                new(T("against the master", "fata de master"), ResidualRange(d, rp) + T("  [ADU: how much each frame differs from the master, from the quietest frame to the noisiest]", "  [ADU: cat difera fiecare cadru de master, de la cel mai linistit cadru la cel mai zgomotos]")),
                new(T("read noise", "zgomot de citire"), ReadNoise(r, rp)),
                new(T("random banding", "benzi aleatoare"), Banding(v.Noise)),
                new("verdict", Noise(v.Noise)),
            ]),
            new(T("5. HOT PIXELS", "5. PIXELI FIERBINTI"),
            [
                // the three count rows stay on the Pixeli fierbinti tab; the Rezumat's card keeps the
                // stability and the verdict (R-071, ADR 0041)
                new(T("above the threshold", "pixeli peste prag"), Levels(d)) { Summary = false },
                new(T($"by colour, above {F(Th.HotAdu, 0)}", $"pe culori, peste {F(Th.HotAdu, 0)}"), PerPlane(d, Th.HotLevelIndex)) { Summary = false },
                new(T("how hot", "cat de fierbinti"), Percentiles(d.Hot, n, eq)) { Summary = false },
                StabilityRow(d, rp),
                new("verdict", T(
                    $"OK (informative): {d.Hot.Hot} pixels above {F(Th.HotAdu, 0)} {eq} in {F(mpx, mpx < 1 ? 2 : 1)} Mpx",
                    $"OK (informativ): {RoCount(d.Hot.Hot)}pixeli peste {F(Th.HotAdu, 0)} {eq} in {F(mpx, mpx < 1 ? 2 : 1)} Mpx")),
            ]),
            new(T("6. FIXED PATTERN: WHAT IS THE SAME IN EVERY FRAME", "6. MODEL FIX: CE E LA FEL IN TOATE CADRELE"),
            [
                new(T("fixed banding", "benzi fixe"), StructuredBanding(d)),
                new("glow", Glow(v.Leak, v.Pattern, eq)),
                new("verdict", Pattern(v.Pattern, eq)),
            ]),
            new(T("7. DEAD AND SATURATED PIXELS", "7. PIXELI MORTI SI SATURATI"),
            [
                new("master", T(
                    $"{v.DeadSat.Dead} {(v.DeadSat.Dead == 1 ? "dead pixel" : "dead pixels")} (below the black minus {F(Th.DeadBelowAdu, 0)} ADU), {v.DeadSat.Saturated} saturated (at the white, {F(d.White, 0)})",
                    $"{RoCount(v.DeadSat.Dead)}{(v.DeadSat.Dead == 1 ? "pixel mort" : "pixeli morti")} (sub negru minus {F(Th.DeadBelowAdu, 0)} ADU), {RoCount(v.DeadSat.Saturated)}{(v.DeadSat.Saturated == 1 ? "saturat" : "saturati")} (la alb, {F(d.White, 0)})")),
                new("verdict", DeadSat(v.DeadSat)),
            ]),
            new(T("8. HOW MANY DARKS ARE ENOUGH", "8. CATE DARK-URI AJUNG"),
            [
                CurveRow(d.Noise, g),
                new("master", Enough(d, v.Enough), double.IsNaN(v.Enough.MasterNoise) ? null : [$"{F(v.Enough.MasterNoise, 2)} ADU"]),
                new("verdict", EnoughVerdictText(v.Enough)),
            ]),
            new(T("9. HOW WELL THE DARK FITS THE LIGHTS", "9. CAT DE BINE SE POTRIVESTE DARK-UL CU LIGHTS-URILE"), LightsRows(r, eq)),
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
        LightFigures[] bad = l.Frames.Where(f => f.Error is not null).ToArray();
        if (bad.Length > 0)
        {
            rows.Add(new(T("WARNING", "ATENTIE"), T(
                $"{bad.Length} {(bad.Length == 1 ? "light could not be read" : "lights could not be read")} (the first: {bad[0].Info.FileName}: {bad[0].Error}); {(bad.Length == 1 ? "it is" : "they are")} in the lights table",
                $"{RoCount(bad.Length)}{(bad.Length == 1 ? "light nu s-a putut citi" : "lights nu s-au putut citi")} (primul: {bad[0].Info.FileName}: {bad[0].Error}); {(bad.Length == 1 ? "e" : "sunt")} in tabelul de lights")));
        }

        if (l.AssumedSettings > 0)
        {
            rows.Add(new(T("WARNING", "ATENTIE"), T(
                $"{l.AssumedSettings} {(l.AssumedSettings == 1 ? "light does not give its ISO or exposure; the darks' were taken" : "lights do not give their ISO or exposure; the darks' were taken")} (1 ADU-eq = {F(r.Darks.Norm, 2)} ADU)",
                $"{RoCount(l.AssumedSettings)}{(l.AssumedSettings == 1 ? "light nu spune ISO-ul sau expunerea; s-au luat cele ale dark-urilor" : "lights nu spun ISO-ul sau expunerea; s-au luat cele ale dark-urilor")} (1 ADU-eq = {F(r.Darks.Norm, 2)} ADU)")));
        }

        bool one = l.Persistent == 1;
        string seen = one ? T("1 hot pixel shows", "1 pixel fierbinte se vede") : T($"{l.Persistent} hot pixels show", $"{RoCount(l.Persistent)}pixeli fierbinti se vad");
        rows.Add(new(T("hot pixels", "pixeli fierbinti"), T(
            $"{seen} in the lights at the same place, in at least {l.PersistentThreshold} of {RunReport.Frames(l.Used)} ({(one ? "a single pixel" : "single pixels")}, not part of a star, brighter than {(one ? "its" : "their")} surroundings by more than {F(Th.LightHotSigmas, 0)} times the noise there)",
            $"{seen} in lights in acelasi loc, in cel putin {l.PersistentThreshold} din {RunReport.Frames(l.Used)} ({(one ? "un pixel singur, nu parte dintr-o stea, mai luminos" : "pixeli singuri, nu parte dintr-o stea, mai luminosi")} decat imprejurimea cu peste {RoCount(F(Th.LightHotSigmas, 0))}ori zgomotul ei)")));
        rows.Add(OverlapRow(T("in the dark master", "in masterul dark"), l.Overlap, _ => l.Persistent,
            T($"how many of these pixels are in the dark master too, above each threshold in {eq}", $"cati dintre acesti pixeli sunt si in masterul dark, peste fiecare prag in {eq}")));
        rows.Add(OverlapRow(T("master in lights", "masterul in lights"), l.Overlap, o => o.DarkCount,
            T("how many of the master's pixels above each threshold show in the lights too", "cati dintre pixelii masterului de peste fiecare prag se vad si in lights")));
        string hot = $"{F(Th.HotAdu, 0)} {eq}";
        rows.Add(new(T("not in the master", "lipsesc din master"), l.Missed switch
        {
            0 => T($"none: every hot pixel of the lights is above {hot} in the master too", $"niciunul: toti pixelii fierbinti din lights sunt peste {hot} si in master"),
            1 => T(
                $"1 hot pixel of the lights is under {hot} in the master (it has {F(l.MissedDarkExcessMedian, 0)} {eq} there)",
                $"1 pixel fierbinte din lights e sub {hot} in master (acolo are {F(l.MissedDarkExcessMedian, 0)} {eq})"),
            _ => T(
                $"{l.Missed} hot pixels of the lights are under {hot} in the master (there they have {F(l.MissedDarkExcessMedian, 0)} {eq} on average)",
                $"{RoCount(l.Missed)}pixeli fierbinti din lights sunt sub {hot} in master (acolo au in medie {F(l.MissedDarkExcessMedian, 0)} {eq})"),
        }));
        MatchFigures m = l.LightsOverDark;
        string over = m.Pixels == 1 ? T("over the one frame", "pe singurul cadru") : T($"over the {RunReport.Frames(m.Pixels)}", $"pe cele {RunReport.Frames(m.Pixels)}");
        rows.Add(new(T("lights against dark", "lights fata de dark"), m.Verdict == MatchVerdict.Unmeasured
            ? T("UNMEASURED: the master has no hot pixel that is not saturated", "NEMASURAT: masterul nu are niciun pixel fierbinte care sa nu fie saturat")
            : T(
                $"in the lights, the hot pixels are on average {F(m.Ratio, 2)} times what they are in the dark ({over}; in the first {F(l.RatioFirst, 2)}, in the last {F(l.RatioLast, 2)}; on {Pixels(l.AmpPixels)})",
                $"in lights, pixelii fierbinti sunt in medie de {F(m.Ratio, 2)} ori cat in dark ({over}; in primul {F(l.RatioFirst, 2)}, in ultimul {F(l.RatioLast, 2)}; pe {Pixels(l.AmpPixels)})")));
        if (TraceRow(m, l.Traces) is string left)
        {
            rows.Add(new(T("what is left", "ce ramane"), left));
        }

        // both indexes, each on its own row (R-056): the darks' tag (the partner's, when the darks
        // are the partner's, R-054), the lights' equivalent tag
        rows.Add(new(r.DarksFromPartner is null ? T("index darks", "index dark-uri") : T("index partner", "index partener"), r.Index.Tag));
        if (LightsLike(r) is (string tag, string about))
        {
            rows.Add(new("index lights", tag + about));
        }

        rows.Add(new("verdict", MatchText(m, partner: false, T("in the lights", "in lights"), l.Traces)));
        return rows;
    }

    /// <summary>
    /// The `ce ramane` row of check 9 (R-067): what a plain subtraction of the dark leaves at a hot
    /// pixel, against the noise of a light, and at how many pixels it passes that noise. A ratio
    /// under 1 takes too much and leaves a hole, one over 1 too little and leaves a rest.
    /// </summary>
    /// <param name="m">The lights over the dark.</param>
    /// <param name="t">The traces.</param>
    /// <returns>The row's value, in the run's language; null when there is nothing to say: the
    /// ratio is unmeasured, the lights show none of the dark's hot pixels (ratios around zero), or no
    /// pixel was read.</returns>
    private static string? TraceRow(MatchFigures m, TraceFigures t)
    {
        // ratios that sit around zero are the "hot pixels do not show" verdict of MatchText: no subtraction to speak of
        bool absent = m.Verdict == MatchVerdict.Unmatched && (m.Ratio <= 0 || m.Q25 <= 0);
        if (m.Verdict == MatchVerdict.Unmeasured || absent || !(m.Ratio > 0) || t.Pixels == 0 || double.IsNaN(t.Median))
        {
            return null;
        }

        string median = F(t.Median, 2);
        if (t.Median < 0.005)
        {
            return T(
                $"after the subtraction nothing is left to see at the hot pixels: on average {median} of the noise of a light",
                $"dupa scadere nu ramane nimic de vazut la pixelii fierbinti: in medie {median} din zgomotul unui light");
        }

        bool tooMuch = m.Ratio < 1;
        string strong = F(Th.TraceStrongFactor, 0);
        // a trace under the noise is a part of it; from the noise up it is so many times the noise,
        // as the -opt verdict under the row says it
        string size = t.Median < 1
            ? T($"{median} of the noise of a light", $"{median} din zgomotul unui light")
            : T($"{median} times the noise of a light", $"{median} ori cat zgomotul unui light");
        string about = T(
            "(the light's noise on the pixel's colour; with dithering the traces spread, without it they stay in the same place in the stack)",
            "(zgomotul light-ului pe culoarea pixelului; cu dithering urmele se imprastie, fara el raman in acelasi loc in stack)");
        return tooMuch
            ? T(
                $"the subtraction takes too much: a hot pixel keeps on average a hole of {size}; at {t.AboveNoise} of {t.Pixels} the hole is over the noise, at {t.AboveStrong} over {strong} times the noise {about}",
                $"scaderea ia prea mult: la un pixel fierbinte ramane in medie o gaura de {size}; la {t.AboveNoise} din {t.Pixels} gaura trece de zgomot, la {t.AboveStrong} de {strong} ori zgomotul {about}")
            : T(
                $"the subtraction takes too little: a hot pixel keeps on average a rest of {size}; at {t.AboveNoise} of {t.Pixels} the rest is over the noise, at {t.AboveStrong} over {strong} times the noise {about}",
                $"scaderea ia prea putin: la un pixel fierbinte ramane in medie un rest de {size}; la {t.AboveNoise} din {t.Pixels} restul trece de zgomot, la {t.AboveStrong} de {strong} ori zgomotul {about}");
    }

    /// <summary>
    /// One overlap row of check 9 (R-051): the console's line, the levels joined by "; " and the
    /// note in brackets, and the same figures as a table for the window.
    /// </summary>
    /// <param name="label">The row's label.</param>
    /// <param name="overlap">The overlap at each hot level.</param>
    /// <param name="of">The total a level's shared count is read against: the persistent pixels,
    /// or the master's pixels above the level.</param>
    /// <param name="note">What the row shows, without brackets.</param>
    /// <returns>The row, with its table.</returns>
    private static ReportRow OverlapRow(string label, IReadOnlyList<OverlapAtLevel> overlap, Func<OverlapAtLevel, int> of, string note)
    {
        string above = T("above", "peste");
        string ofWord = T("of", "din");
        List<string> parts = [];
        List<IReadOnlyList<string>> cells = [];
        foreach (OverlapAtLevel o in overlap)
        {
            int total = of(o);
            string share = total > 0 ? $"{F(100.0 * o.Both / total, 0)} %" : "";
            parts.Add($"{above} {F(o.LevelAdu, 0)}: {o.Both} {ofWord} {total}" + (total > 0 ? $" ({share})" : ""));
            cells.Add([$"{above} {F(o.LevelAdu, 0)}", $"{o.Both} {ofWord} {total}", share]);
        }

        return new(label, string.Join("; ", parts) + "  [" + note + "]")
        {
            Table = new([T("threshold", "prag"), T("pixels", "pixeli"), T("share", "procent")], cells, note),
        };
    }

    /// <summary>
    /// The lights' equivalent index (R-064) as its tag and the parenthesis that follows it in
    /// check 9's <c>index lights</c> row and INDEX's <c>lights</c> row: the ratio to the dark and
    /// the degrees.
    /// </summary>
    /// <param name="r">The run's result.</param>
    /// <returns>The tag (<c>idx157</c>) and the parenthesis with its leading space; null without
    /// lights or when the lights' index is not measured.</returns>
    private static (string Tag, string About)? LightsLike(DarkSeriesResult r)
    {
        if (r.Lights is not LightsAnalysis l || double.IsNaN(r.LightsIndex))
        {
            return null;
        }

        string warmth = Warmth(l.LightsOverDark.DeltaCelsius, "", T("it", "el"));
        return (IndexFigures.TagFor(r.LightsIndex), T(
            $" ({F(l.LightsOverDark.Ratio, 2)} times the dark{warmth})",
            $" (de {F(l.LightsOverDark.Ratio, 2)} ori cat dark-ul{warmth})"));
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
                $"{ix.Tag} ({F(ix.Index, 1)} ADU-eq: the average value of the hot pixels at places {ix.RankLow + 1} .. {ix.RankHigh}, counting from the hottest, that is {F(Th.IndexRankLowPerMpx, 0)}-{F(Th.IndexRankHighPerMpx, 0)} per Mpx of {F(mpx, mpx < 1 ? 2 : 1)} Mpx; worked out for ISO {F(Th.NormIso, 0)} and {F(Th.NormExposureSeconds, 0)} s)",
                $"{ix.Tag} ({F(ix.Index, 1)} ADU-eq: valoarea medie a pixelilor fierbinti de pe locurile {ix.RankLow + 1} .. {ix.RankHigh}, numarand de la cel mai fierbinte, adica {F(Th.IndexRankLowPerMpx, 0)}-{F(Th.IndexRankHighPerMpx, 0)} pe fiecare Mpx din {F(mpx, mpx < 1 ? 2 : 1)} Mpx; socotita pentru ISO {F(Th.NormIso, 0)} si {F(Th.NormExposureSeconds, 0)} s)")
                // the partner's label stands on the partner's row, the session's own on `eticheta` (R-066)
                + (r.DarksFromPartnerLabel is string partnerLabel ? T($"; label {partnerLabel}", $"; eticheta {partnerLabel}") : "")),
            new(T("label", "eticheta"), r.Label),
        ];
        if (LightsLike(r) is (string tag, string about))
        {
            rows.Add(new("lights", T("like a dark of ", "ca un dark cu ") + tag + about));
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
                $"{p.Folder}: {p.Index.Tag} ({F(p.Index.Index, 1)} ADU-eq; frames read {p.Used} of {p.Frames}, {settings}); label {p.Label}",
                $"{p.Folder}: {p.Index.Tag} ({F(p.Index.Index, 1)} ADU-eq; cadre citite {p.Used} din {p.Frames}, {settings}); eticheta {p.Label}")));
            if (p.Failed > 0)
            {
                rows.Add(new(T("WARNING", "ATENTIE"), T(
                    $"{p.Failed} {(p.Failed == 1 ? "dark of the partner could not be used" : "darks of the partner could not be used")} (the first: {p.FirstError})",
                    $"{RoCount(p.Failed)}{(p.Failed == 1 ? "dark al partenerului nu s-a putut folosi" : "dark-uri ale partenerului nu s-au putut folosi")} (primul: {p.FirstError})")));
            }

            string partnerWords = T("the partner", "partenerul");
            string ownWarmth = Warmth(m.DeltaCelsius, T("the session's darks are ", "dark-urile sesiunii sunt "), partnerWords);
            rows.Add(new(T("darks / partner", "dark-uri / partener"), m.Verdict == MatchVerdict.Unmeasured
                ? T("UNMEASURED: no pixel is hot in both", "NEMASURAT: niciun pixel nu e fierbinte in amandoua")
                : T(
                    $"in the session's darks the hot pixels are {F(m.Ratio, 2)} times what they are at the partner, at the same settings (half of them between {F(m.Q25, 2)} and {F(m.Q75, 2)}; {(m.Pixels == 1 ? "1 pixel hot in both" : $"{m.Pixels} pixels hot in both")}){ownWarmth}",
                    $"pixelii fierbinti sunt in dark-urile sesiunii de {F(m.Ratio, 2)} ori cat la partener, la aceleasi setari (jumatate dintre ei intre {F(m.Q25, 2)} si {F(m.Q75, 2)}; {(m.Pixels == 1 ? "1 pixel fierbinte in amandoua" : $"{RoCount(m.Pixels)}pixeli fierbinti in amandoua")}){ownWarmth}")));
            string lvl = F(Th.PartnerOverlapAdu, 0);
            string ownShare = p.OwnHot50 > 0 ? $" ({F(100.0 * p.OwnHot50InPartner50 / p.OwnHot50, 0)} %)" : "";
            string partnerShare = p.PartnerHot50 > 0 ? $" ({F(100.0 * p.PartnerHot50InOwn20 / p.PartnerHot50, 0)} %)" : "";
            // two statements, one under the other in the window (R-063)
            string[] shared =
            [
                T($"{p.OwnHot50InPartner50} of the session's {p.OwnHot50} above {lvl} ADU-eq are above {lvl} at the partner too{ownShare}", $"{p.OwnHot50InPartner50} din {p.OwnHot50} ai sesiunii peste {lvl} ADU-eq sunt peste {lvl} si la partener{ownShare}"),
                T($"{p.PartnerHot50InOwn20} of the partner's {p.PartnerHot50} above {lvl} are above {F(Th.HotAdu, 0)} in the session{partnerShare}", $"{p.PartnerHot50InOwn20} din {p.PartnerHot50} ai partenerului peste {lvl} sunt peste {F(Th.HotAdu, 0)} in sesiune{partnerShare}"),
            ];
            rows.Add(new(T("shared pixels", "pixeli comuni"), string.Join("; ", shared)) { Lines = shared });
            if (p.LightsOverPartner.Verdict != MatchVerdict.Unmeasured)
            {
                // lights over the partner, through the session's darks: (lights / dark) x (dark /
                // partner)
                MatchFigures chained = p.LightsOverPartner;
                string lightsWarmth = Warmth(chained.DeltaCelsius, T("the lights are ", "lights-urile sunt "), partnerWords);
                rows.Add(new(T("lights / partner", "lights / partener"), T(
                    $"in the lights the hot pixels are {F(chained.Ratio, 2)} times what they are at the partner (worked out through the session's darks){lightsWarmth}",
                    $"pixelii fierbinti sunt in lights de {F(chained.Ratio, 2)} ori cat la partener (socotit prin dark-urile sesiunii){lightsWarmth}")));
                rows.Add(new(T("partner verdict", "verdict partener"), MatchText(chained, partner: true, T("in the lights", "in lights"))));
            }
            else
            {
                rows.Add(new(T("partner verdict", "verdict partener"), MatchText(m, partner: true, T("in the session's darks", "in dark-urile sesiunii"))));
            }
        }

        return rows;
    }

    /// <summary>
    /// The frames table's header line: the column names, then <see cref="FrameTableNote"/>.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The header line, in the run's language; each column name ends in the column where
    /// <see cref="FrameLine"/>'s figures under it end, in both languages.</returns>
    internal static string FrameTableHeader(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return T(
            $"     no  file                  time        mean   noise   diff.   bands      hot     end   light    side   >{F(Th.HotAdu, 0)}   {FrameTableNote(r)}",
            $"     nr  fisier                ora        medie  zgomot    dif.   benzi   fierb. sfarsit  lumina  latura   >{F(Th.HotAdu, 0)}   {FrameTableNote(r)}");
    }

    /// <summary>
    /// One line of the frames table as the console prints it: the cells in their columns, the note
    /// of a frame off the end of the series after the last one; a skipped or failed frame prints its reason
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
        return $"    {f.Index,3}  {name,-20}  {f.Time,-8}  {f.Mean,6}  {f.Sigma,6}  {f.Residual,6}  {f.Bands,6}  {f.Amp,7}  {f.End,6}  {f.Light,6}  {f.Side,6}  {f.Above,4}{note}";
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
                $"side = {SideWithPreposition(w.Side)} side against the centre{(w.RemainderAdu is null ? " (no biases, glow included)" : ", once the bias is taken off (glow included)")}, {eq}",
                $"latura = latura {SideWithPreposition(w.Side)} fata de centru{(w.RemainderAdu is null ? " (fara bias-uri, cu glow)" : ", dupa scaderea bias-ului (cu glow)")}, {eq}")
            : T("side = the side with the most constant light against the centre (UNMEASURED)", "latura = latura cu cea mai multa lumina constanta fata de centru (NEMASURAT)");
        return T(
            $"[{ScaleReport.PlaneWords(r.Darks.PlaneNames[r.ReferencePlane])}; mean = how far the frame is above the black, ADU; noise = the noise of the frame, ADU; diff. = how much the frame differs from the master, ADU; bands = how many times more the rows differ from one another than the noise alone would give (1 = no banding); hot = the hot pixels against the master (1 = the same); end = how many degrees the frame is from the end of the series; to discard = more than {F(Th.OffEndCelsius, 1)} degrees from it (check 3); light = the light above what the warming explains, {eq}; {side}]",
            $"[{ScaleReport.PlaneWords(r.Darks.PlaneNames[r.ReferencePlane])}; medie = cat e cadrul peste negru, ADU; zgomot = zgomotul cadrului, ADU; dif. = cat difera cadrul de master, ADU; benzi = de cate ori difera randurile intre ele mai mult decat ar da zgomotul singur (1 = fara benzi); fierb. = pixelii fierbinti fata de master (1 = la fel); sfarsit = cu cate grade e cadrul fata de sfarsitul seriei; de aruncat = la peste {F(Th.OffEndCelsius, 1)} grade de el (verificarea 3); lumina = lumina in plus fata de cat explica incalzirea, {eq}; {side}]");
    }

    /// <summary>
    /// The hover hint of the frames table's <c>zgomot</c> column (R-083): what the figure is, and
    /// on which pixels of the series, since the table's note that says so is often out of view.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The hint, in the run's language.</returns>
    internal static string NoiseColumnTip(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        string plane = ScaleReport.PlaneWords(r.Darks.PlaneNames[r.ReferencePlane]);
        return T($"the noise of the frame, in ADU, on {plane}", $"zgomotul cadrului, in ADU, pe {plane}");
    }

    /// <summary>
    /// The hover hint of the frames table's <c>&gt;20</c> column (R-083): how many of the frame's
    /// pixels go over the hot threshold, on which pixels of the series.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The hint, in the run's language.</returns>
    internal static string HotColumnTip(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        string plane = ScaleReport.PlaneWords(r.Darks.PlaneNames[r.ReferencePlane]);
        return T($"how many pixels of the frame go over {F(Th.HotAdu, 0)} ADU-eq, on {plane}", $"cati pixeli din cadru trec de {F(Th.HotAdu, 0)} ADU-eq, pe {plane}");
    }

    /// <summary>
    /// The hover hint of the frames table's `sfarsit` column (R-071): what the figure is and when a
    /// frame is one to discard. A property, so it is worded in the language current when it is
    /// read.
    /// </summary>
    internal static string EndColumnTip => T(
        $"how many degrees the frame is from the end of the series ({EndWords}), by the hot pixels; a frame more than {F(Th.OffEndCelsius, 1)} degrees from it is one to discard and has its reason on the line under it (check 3)",
        $"cu cate grade e cadrul fata de sfarsitul seriei ({EndWords}), dupa pixelii fierbinti; un cadru la peste {F(Th.OffEndCelsius, 1)} grade de el e de aruncat si are motivul pe randul de sub el (verificarea 3)");

    /// <summary>
    /// What the end of the series is, for the texts that name it: the average of the last frames
    /// (a median, said as an average, ADR 0035).
    /// </summary>
    private static string EndWords => T(
        $"the average of the last {Th.SeriesEndFrames} frames",
        $"media ultimelor {RoCount(Th.SeriesEndFrames)}cadre");

    /// <summary>
    /// The sentence under the window's warming chart (R-071): what the points are, and with an end
    /// of the series, where it is and the band outside which a frame is one to discard.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The sentence, in the run's language.</returns>
    internal static string WarmingChartNote(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Warming w = r.Darks.Warming;
        string what = T(
            "How hot the master's hot pixels are in each frame, against the master: when the curve climbs, the camera warms during the series; when it falls, the camera cools (check 3).",
            "Cat de fierbinti sunt pixelii fierbinti ai masterului in fiecare cadru, fata de master: cand curba urca, aparatul se incalzeste in timpul seriei; cand coboara, se raceste (verificarea 3).");
        if (w.HasEnd)
        {
            return T(
                $"{what} The end of the series ({EndWords}) is at {F(w.EndRatio, 2)} of the master; the frames under {F(w.OffEndLow, 2)} or over {F(w.OffEndHigh, 2)} (more than {F(Th.OffEndCelsius, 1)} degrees from it) are to discard and show in yellow.",
                $"{what} Sfarsitul seriei ({EndWords}) e la {F(w.EndRatio, 2)} din master; cadrele sub {F(w.OffEndLow, 2)} sau peste {F(w.OffEndHigh, 2)} (la peste {F(Th.OffEndCelsius, 1)} grade de el) sunt de aruncat si apar in galben.");
        }

        return w.Measured < Th.WarmingFitMinFrames
            ? T(
                $"{what} With fewer than {Th.WarmingFitMinFrames} frames with hot pixels the end of the series is not measured.",
                $"{what} Cu mai putin de {RoCount(Th.WarmingFitMinFrames)}cadre cu pixeli fierbinti sfarsitul seriei nu se masoara.")
            : T(
                $"{what} At the end of the series the master's hot pixels do not show, so it is not measured.",
                $"{what} La sfarsitul seriei pixelii fierbinti ai masterului nu se vad, deci el nu se masoara.");
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
            bool measured = wm.HasEnd && !double.IsNaN(f.AmpRatioMedian) && f.AmpRatioMedian > 0;
            string end = measured ? Signed(nz.Degrees(f.AmpRatioMedian / wm.EndRatio), 1) : "-";
            string? note = null;
            if (measured && (f.AmpRatioMedian < wm.OffEndLow || f.AmpRatioMedian > wm.OffEndHigh))
            {
                bool under = f.AmpRatioMedian < wm.OffEndLow;
                note = T(
                    $"to discard: the hot pixels are at {Ratio(f.AmpRatioMedian)} of the master, {(under ? "below" : "above")} {F(under ? wm.OffEndLow : wm.OffEndHigh, 2)}, that is more than {F(Th.OffEndCelsius, 1)} degrees {(under ? "below" : "above")} the end of the series ({F(wm.EndRatio, 2)})",
                    $"de aruncat: pixelii fierbinti sunt la {Ratio(f.AmpRatioMedian)} din master, {(under ? "sub" : "peste")} {F(under ? wm.OffEndLow : wm.OffEndHigh, 2)}, adica la peste {F(Th.OffEndCelsius, 1)} grade {(under ? "sub" : "peste")} sfarsitul seriei ({F(wm.EndRatio, 2)})");
            }
            else if (wm.HasEnd && f.AmpRatioMedian <= 0)
            {
                // the analysis puts such a frame off the end too (WarmingOf), with no degrees to give
                note = T("to discard: the hot pixels of the master do not show in this frame", "de aruncat: pixelii fierbinti ai masterului nu se vad in acest cadru");
            }

            rows.Add(new(f.Info.Index, f.Info.FileName, null, null, Time(f.Info.Timestamp), F(f.MeanAboveBlack, 2), F(p.Sigma, 2), F(res.Sigma, 2), F(res.RowBandingRatio, 1), Ratio(f.AmpRatioMedian), end, double.IsNaN(light) ? "-" : Signed(light, 2), side, p.Above[Th.HotLevelIndex].ToString(CultureInfo.InvariantCulture), note));
        }

        return rows;
    }

    /// <summary>
    /// The lights table's header line: the column names, then <see cref="LightTableNote"/>.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The header line, in the run's language; each right-aligned column name ends where
    /// its figures end and the file and the time start where theirs start (R-086), at the same
    /// columns in both languages.</returns>
    internal static string LightTableHeader(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return T(
            $"     no  file                  time         sky   noise     bright   single      hot    low   high   {LightTableNote(r)}",
            $"     nr  fisier                ora          cer  zgomot   luminosi  singuri   fierb.    jos    sus   {LightTableNote(r)}");
    }

    /// <summary>
    /// The note of the lights table (R-086): the pixels it reads and what each column means, in the
    /// window's header row too.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>The note in square brackets, in the run's language.</returns>
    internal static string LightTableNote(DarkSeriesResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        string plane = ScaleReport.PlaneWords(r.Darks.PlaneNames[r.ReferencePlane]);
        return T(
            $"[{plane}; sky = how bright the sky is, ADU; noise = how much a pixel differs from its neighbours, ADU; bright = brighter than their neighbours by more than {F(Th.LightHotSigmas, 0)} times the noise; "
            + "single = with no bright neighbour, that is hot pixels; hot = the hot pixels of the master against the master (1 = the same); low, high = half of them are between these two values]",
            $"[{plane}; cer = cat de luminos e cerul, ADU; zgomot = cat difera un pixel de vecinii lui, ADU; luminosi = mai luminosi decat vecinii cu peste {RoCount(F(Th.LightHotSigmas, 0))}ori zgomotul; "
            + "singuri = fara vecini luminosi, adica pixeli fierbinti; fierb. = pixelii fierbinti ai masterului fata de master (1 = la fel); jos, sus = jumatate dintre ei sunt intre aceste doua valori]");
    }

    /// <summary>
    /// One row per light, the cells formatted as the console prints them, on the reference plane;
    /// empty without lights.
    /// </summary>
    /// <param name="r">The run's result; not null.</param>
    /// <returns>One row per light in file order; a failed light has its reason and empty
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
            if (f.Error is not null)
            {
                rows.Add(new(f.Info.Index, f.Info.FileName, f.Error, "", "", "", "", "", "", "", ""));
                continue;
            }

            rows.Add(new(f.Info.Index, f.Info.FileName, null, Time(f.Info.Timestamp), F(f.SkyMedian[rp], 1), F(f.LocalSigma[rp], 2), f.Candidates.ToString(CultureInfo.InvariantCulture), f.IsolatedHot.ToString(CultureInfo.InvariantCulture), Ratio(f.AmpRatioMedian), Ratio(f.AmpRatioQ25), Ratio(f.AmpRatioQ75)));
        }

        return rows;
    }

    /// <summary>
    /// The clause after a ratio that says how much warmer or colder a set is than the master it is
    /// measured against: ", adica cu 7.2 grade mai reci decat el"; at the same temperature under
    /// <see cref="Th.SameTemperatureCelsius"/>; nothing when the ratio gives no degrees.
    /// </summary>
    /// <param name="deltaCelsius">The set's degrees against the master, positive when the set is
    /// warmer; NaN when the ratio is not positive.</param>
    /// <param name="subject">The set's words before the comparison, with their verb and a trailing
    /// space ("dark-urile sesiunii sunt "), or empty when the row names the set already.</param>
    /// <param name="than">The master's words after "than" ("el", "partenerul"), in the run's
    /// language.</param>
    /// <returns>The clause, starting with its comma, in the run's language; empty for NaN.</returns>
    private static string Warmth(double deltaCelsius, string subject, string than)
    {
        if (double.IsNaN(deltaCelsius))
        {
            return "";
        }

        if (Math.Abs(deltaCelsius) < Th.SameTemperatureCelsius)
        {
            return T($", that is {subject}at the same temperature", $", adica {subject}la aceeasi temperatura");
        }

        string degrees = F(Math.Abs(deltaCelsius), 1);
        return deltaCelsius < 0
            ? T($", that is {subject}{degrees} degrees colder than {than}", $", adica {subject}cu {degrees} grade mai reci decat {than}")
            : T($", that is {subject}{degrees} degrees warmer than {than}", $", adica {subject}cu {degrees} grade mai calde decat {than}");
    }

    /// <summary>
    /// The words of a match verdict, the master being the session's dark or the partner and
    /// <paramref name="where"/> naming the set measured against it ("in the lights", "in the
    /// session's darks"): what Siril can do with the master, and why.
    /// </summary>
    /// <param name="m">The match; not null.</param>
    /// <param name="partner">True when the master is the partner, false when it is the session's
    /// dark.</param>
    /// <param name="where">Where the set's hot pixels are, as the sentence names it ("in lights",
    /// "in dark-urile sesiunii"), in the run's language.</param>
    /// <param name="traces">What a plain subtraction leaves, for a set of lights against its
    /// darks (R-067): a verdict the traces relaxed says so, and an -opt verdict says how much is
    /// left; null for a partner, whose verdict is the ratio's alone.</param>
    /// <returns>The verdict's words, opening with OK, WARNING or UNMEASURED (OK, ATENTIE or
    /// NEMASURAT in Romanian).</returns>
    internal static string MatchText(MatchFigures m, bool partner, string where, TraceFigures? traces = null)
    {
        ArgumentNullException.ThrowIfNull(m);
        string atMaster = partner ? T("at the partner", "la partener") : T("in the dark", "in dark");
        if (m.Verdict == MatchVerdict.Unmatched && (m.Ratio <= 0 || m.Q25 <= 0))
        {
            // the ratios sit around zero: the set shows none of the master's hot pixels
            return T(
                $"WARNING: they do not match: {where} the {(partner ? "partner's" : "dark's")} hot pixels do not show ({F(m.Ratio, 2)} times what they are {atMaster}; perhaps the camera's noise reduction, another camera or rotated frames?)",
                $"ATENTIE: nu se potrivesc: {where} nu se vad pixelii fierbinti ai {(partner ? "partenerului" : "dark-ului")} (de {F(m.Ratio, 2)} ori cat {atMaster}; poate reducerea de zgomot din aparat, alt aparat sau cadre rotite?)");
        }

        string master = partner ? T("the partner", "partenerul") : T("the dark", "dark-ul");
        string temperature = Math.Abs(m.DeltaCelsius) < Th.SameTemperatureCelsius
            ? T($"{master} is at the same temperature", $"{master} e la aceeasi temperatura")
            : T(
                $"{master} is {(m.DeltaCelsius < 0 ? "warmer" : "colder")} by {F(Math.Abs(m.DeltaCelsius), 1)} degrees",
                $"{master} e {(m.DeltaCelsius < 0 ? "mai cald" : "mai rece")} cu {F(Math.Abs(m.DeltaCelsius), 1)} grade");
        string ratio = T(
            $"{where} the hot pixels are {F(m.Ratio, 2)} times what they are {atMaster}",
            $"{where} pixelii fierbinti sunt de {F(m.Ratio, 2)} ori cat {atMaster}");
        // what is left after a plain subtraction, when it was measured: said after the -opt band
        string left = traces is { Pixels: > 0 } t && !double.IsNaN(t.Median)
            ? T(
                $", and what the subtraction leaves is on average {F(t.Median, 2)} times the noise of a light",
                $", iar ce ramane dupa scadere e in medie de {F(t.Median, 2)} ori zgomotul unui light")
            : "";
        return m.Verdict switch
        {
            MatchVerdict.Unmeasured => T("UNMEASURED", "NEMASURAT"),
            // inside the band the traces widened (R-067): outside 0.9 .. 1.1, and the words say why it is still as it is
            MatchVerdict.Direct when traces is { Relaxed: true } r => T(
                $"OK: {master} can be subtracted as it is; {ratio}, outside {F(Th.MatchDirectLow, 1)} .. {F(Th.MatchDirectHigh, 1)}, but what the subtraction leaves is on average under the noise of a light ({F(r.Median, 2)} of it; on these lights that holds between {F(r.DirectLow, 2)} and {F(r.DirectHigh, 2)}): {temperature}",
                $"OK: {master} se poate scadea asa cum e; {ratio}, in afara {F(Th.MatchDirectLow, 1)} .. {F(Th.MatchDirectHigh, 1)}, dar ce ramane dupa scadere e in medie sub zgomotul unui light ({F(r.Median, 2)} din el; pe aceste lights, asa e intre {F(r.DirectLow, 2)} si {F(r.DirectHigh, 2)}): {temperature}"),
            MatchVerdict.Direct => T(
                $"OK: {master} can be subtracted as it is; {ratio}, between {F(Th.MatchDirectLow, 1)} and {F(Th.MatchDirectHigh, 1)}: {temperature}",
                $"OK: {master} se poate scadea asa cum e; {ratio}, intre {F(Th.MatchDirectLow, 1)} si {F(Th.MatchDirectHigh, 1)}: {temperature}"),
            MatchVerdict.WithOpt => T(
                $"WARNING: subtract {master} with -opt (-opt: Siril multiplies it so that it matches the lights); {ratio}, between {F(Th.MatchOptLow, 1)} and {F(Th.MatchOptHigh, 1)}{left}: {temperature}",
                $"ATENTIE: scade {master} cu -opt (-opt: Siril il inmulteste cat sa se potriveasca cu lights-urile); {ratio}, intre {F(Th.MatchOptLow, 1)} si {F(Th.MatchOptHigh, 1)}{left}: {temperature}"),
            MatchVerdict.CosmeticOnly => T(
                $"WARNING: use {master} for the cosmetic correction only (-cc=dark: Siril finds the hot pixels in it and repairs them in the lights); {ratio}: {temperature}, it sees them all, but subtracted it would take out too much and leave black dots",
                $"ATENTIE: foloseste {master} doar pentru corectia cosmetica (-cc=dark: Siril gaseste in el pixelii fierbinti si ii repara in lights); {ratio}: {temperature}, ii vede pe toti, dar scazut ar scoate prea mult si ar lasa puncte negre"),
            _ => T(
                $"WARNING: they do not match; {ratio}: {temperature}; {(m.Ratio > 1 ? "it is too cold and misses the faint ones" : "it is too warm")}",
                $"ATENTIE: nu se potrivesc; {ratio}: {temperature}; {(m.Ratio > 1 ? "e prea rece si ii pierde pe cei slabi" : "e prea cald")}"),
        };
    }

    /// <summary>The report's word for a verdict, in the run's language.</summary>
    /// <param name="v">The verdict.</param>
    /// <returns>"OK", "WARNING" ("ATENTIE"), "REDO" ("REFA"), or "UNMEASURED" ("NEMASURAT") for an
    /// unmeasured one.</returns>
    internal static string VerdictWord(Verdict v) => v switch
    {
        Verdict.Ok => "OK",
        Verdict.Warning => T("WARNING", "ATENTIE"),
        Verdict.Redo => T("REDO", "REFA"),
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
        string gain = d.Normalisation.IsoFactorPerStop == 2 ? "" : T(
            $"; one ISO stop multiplies the signal by {F(d.Normalisation.IsoFactorPerStop, 2)} (your setting)",
            $"; o treapta de ISO inmulteste semnalul cu {F(d.Normalisation.IsoFactorPerStop, 2)} (setarea ta)");
        if (first?.Iso is not double iso || first.ExposureSeconds is not double e)
        {
            return T("the files do not give the ISO or the exposure, so 1 ADU-eq is taken equal to 1 ADU in the file", "fisierele nu spun ISO-ul sau expunerea, deci 1 ADU-eq e luat egal cu 1 ADU din fisier") + gain;
        }

        string settings = $"ISO {F(iso, 0)}, {InventoryReport.Exposure(e)} s";
        string reference = $"ISO {F(Th.NormIso, 0)}" + T(" and ", " si ") + $"{F(Th.NormExposureSeconds, 0)} s";
        return (AtReference(iso, e)
            ? T($"1 ADU in the file: the set was taken at the reference settings themselves, {settings}", $"1 ADU din fisier: setul e facut chiar la setarile de referinta, {settings}")
            : T($"{F(d.Norm, 2)} ADU in the file at {settings} (ADU-eq is the ADU as it would be at {reference})", $"{F(d.Norm, 2)} ADU din fisier la {settings} (ADU-eq e ADU-ul recalculat ca pentru {reference})")) + gain;
    }

    /// <summary>
    /// Whether a frame was taken at the reference settings themselves, ISO `Th.NormIso` and
    /// `Th.NormExposureSeconds`, and not at other settings whose factor happens to be 1 too
    /// (ISO 800 and 60 s).
    /// </summary>
    /// <param name="iso">The frame's ISO.</param>
    /// <param name="exposureSeconds">The frame's exposure, in seconds.</param>
    /// <returns>True at ISO 400 within half a stop's rounding and 120 s within 0.05 s.</returns>
    internal static bool AtReference(double iso, double exposureSeconds) =>
        Math.Abs(iso - Th.NormIso) < 0.5 && Math.Abs(exposureSeconds - Th.NormExposureSeconds) < 0.05;

    /// <summary>
    /// The scale's source as the header line writes it after "(white)": the raw's words as they
    /// stand, a FITS's in brackets.
    /// </summary>
    /// <param name="d">The dark series.</param>
    /// <returns>The words with their leading space.</returns>
    private static string ScaleSource(DarkSeriesAnalysis d) =>
        d.ScaleSource.StartsWith("FITS", StringComparison.Ordinal) ? $" ({d.ScaleSource})" : $" {d.ScaleSource}";

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
        $"centre {F(r.Centre / n, 2)}, corners {F(r.TopLeft / n, 2)} / {F(r.TopRight / n, 2)} / {F(r.BottomLeft / n, 2)} / {F(r.BottomRight / n, 2)} (top-left / top-right / bottom-left / bottom-right), edges top {F(r.Top / n, 2)} bottom {F(r.Bottom / n, 2)} left {F(r.Left / n, 2)} right {F(r.Right / n, 2)}",
        $"centru {F(r.Centre / n, 2)}, colturi {F(r.TopLeft / n, 2)} / {F(r.TopRight / n, 2)} / {F(r.BottomLeft / n, 2)} / {F(r.BottomRight / n, 2)} (stanga-sus / dreapta-sus / stanga-jos / dreapta-jos), margini sus {F(r.Top / n, 2)} jos {F(r.Bottom / n, 2)} stanga {F(r.Left / n, 2)} dreapta {F(r.Right / n, 2)}");

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
        string source = l.FromDarkMinusBias ? T("on dark minus bias", "pe dark minus bias") : T("on the dark master (no biases)", "pe masterul dark (fara bias-uri)");
        string framesTail = l.Frames is null
            ? T("the light per frame could not be measured", "lumina pe cadre nu s-a putut masura")
            : T("no frame has more light", "niciun cadru nu are lumina in plus");
        if (l.Verdict == Verdict.Unmeasured)
        {
            return T($"UNMEASURED: the frames are too small for the map (under a square of {Th.BinPx * 2} px)", $"NEMASURAT: cadrele sunt prea mici pentru harta (sub un patrat de {Th.BinPx * 2} px)");
        }

        if (l.Verdict == Verdict.Ok)
        {
            string sidesTail = l.SidesAfterBias
                ? T(
                    $"; no side has constant light above {F(Th.LeakConstantAdu, 2)} {eq}",
                    $"; nicio latura nu are lumina constanta peste {F(Th.LeakConstantAdu, 2)} {eq}")
                : "";
            return T(
                $"OK: no edge and no corner differs from the centre by more than {F(Th.LeakAdu, 1)} {eq} (the most {Signed(l.OffsetAdu, 2)}, at the {RegionName(l.Where)}, {source}); {framesTail}{sidesTail}",
                $"OK: nicio margine si niciun colt nu difera de centru cu peste {F(Th.LeakAdu, 1)} {eq} (cel mai mult {Signed(l.OffsetAdu, 2)}, la {RegionName(l.Where)}, {source}); {framesTail}{sidesTail}");
        }

        SideLight? constant = l.ConstantLight;
        List<string> parts = [];
        if (l.Frames is FrameLight fl && fl.LitFrames.Count > 0)
        {
            int n = fl.LitFrames.Count;
            parts.Add(T(
                $"{n} {(n == 1 ? "frame has" : "frames have")} more light than the rest of the series ({LitList(fl, ": ")} {eq}); {(constant is null ? "take them out of darks\\ or redo the series" : "redo the series")}",
                $"{RoCount(n)}{(n == 1 ? "cadru are" : "cadre au")} lumina in plus fata de restul seriei ({LitList(fl, ": ")} {eq}); {(constant is null ? "scoate-le din darks\\ sau refa seria" : "refa seria")}"));
        }

        if (constant is SideLight c)
        {
            parts.Add(T(
                $"in every frame {SideWithPreposition(c.Side)} side has {Signed(c.LightAdu, 2)} {eq} (+/-{F(c.ErrorAdu, 2)}) of constant light against the centre, which does not come from the bias: light gets into the camera (through the eyepiece or the lens mount)",
                $"in toate cadrele latura {SideWithPreposition(c.Side)} are {Signed(c.LightAdu, 2)} {eq} (+/-{F(c.ErrorAdu, 2)}) lumina constanta fata de centru, care nu vine din bias: lumina intra in aparat (prin ocular sau prin montura obiectivului)")
                + (against.Length == 0 ? "" : T("; that is, ", "; adica ") + against));
        }

        if (Math.Abs(l.OffsetAdu) > Th.LeakAdu)
        {
            string cause = l.OffsetAdu > 0
                ? CauseWords(l)
                : T("the edge is darker than the centre: a level difference of the readout or a mismatched bias", "marginea e mai intunecata decat centrul: o diferenta de nivel din citire sau un bias nepotrivit");
            parts.Add(T(
                $"the {RegionName(l.Where)} differs from the centre by {Signed(l.OffsetAdu, 2)} {eq} ({source}), over the threshold of {F(Th.LeakAdu, 1)}: {cause}",
                $"{RegionName(l.Where)} difera de centru cu {Signed(l.OffsetAdu, 2)} {eq} ({source}), peste pragul de {F(Th.LeakAdu, 1)}: {cause}"));
        }

        return T("WARNING: ", "ATENTIE: ") + string.Join(T("; also, ", "; in plus, "), parts);
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
            return new(label, T("without biases it cannot be told from a level difference of the readout: constant ", "fara bias-uri nu se poate desparti de diferenta de nivel din citire: constant ") + string.Join(", ", order.Select(s => $"{SideName(s)} {Signed(l.Sides.First(x => x.Side == s).ConstantAdu, 2)}")) + $" {eq}");
        }

        string sides = string.Join(", ", order.Select(s => l.Sides.First(x => x.Side == s)).Select(s => $"{SideName(s.Side)} {Signed(s.LightAdu, 2)} (+/-{F(s.ErrorAdu, 2)})"));
        if (l.ConstantLight is not SideLight c)
        {
            return new(label, T(
                $"against the centre, once the bias is taken off: {sides} {eq}; no side goes over {F(Th.LeakConstantAdu, 2)} and over {F(Th.LeakConstantSigmas, 0)} times what the measurement can be off by; a light even over the whole frame does not show here, as there is nothing to compare it with",
                $"fata de centru, dupa scaderea bias-ului: {sides} {eq}; nicio latura nu trece de {F(Th.LeakConstantAdu, 2)} si de {RoCount(F(Th.LeakConstantSigmas, 0))}ori cat poate gresi masuratoarea; o lumina egala pe tot cadrul nu se vede aici, pentru ca nu are cu ce fi comparata"));
        }

        (string against, IReadOnlyList<string> bold) = ScaleReport.AgainstMasters(c.LightAdu * norm, masterNoise, stack);
        string tail = against.Length == 0 ? "" : $"; {against}";
        return new(label, T(
            $"against the centre, once the bias is taken off: {sides} {eq}; {SideWithPreposition(c.Side)} side goes over {F(Th.LeakConstantAdu, 2)} and over {F(Th.LeakConstantSigmas, 0)} times what the measurement can be off by{tail}",
            $"fata de centru, dupa scaderea bias-ului: {sides} {eq}; latura {SideWithPreposition(c.Side)} trece de {F(Th.LeakConstantAdu, 2)} si de {RoCount(F(Th.LeakConstantSigmas, 0))}ori cat poate gresi masuratoarea{tail}"), [$"{Signed(c.LightAdu, 2)} (+/-{F(c.ErrorAdu, 2)})", .. bold]);
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
    /// A side as it follows "latura" in Romanian, with its preposition ("de sus", "din dreapta"),
    /// or as it goes before "side" in English ("the top", "the right"): the words of every side text,
    /// check 2's, the frames table's note and the ADU scale section's.
    /// </summary>
    /// <param name="s">The side.</param>
    /// <returns>The words, in the run's language.</returns>
    internal static string SideWithPreposition(MapSide s) => s switch
    {
        MapSide.Top => T("the top", "de sus"),
        MapSide.Bottom => T("the bottom", "de jos"),
        MapSide.Left => T("the left", "din stanga"),
        _ => T("the right", "din dreapta"),
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
                $"it grows as the sensor warms (glow {Signed(s!.GlowAdu, 2)}, constant {Signed(s.ConstantAdu, 2)}): it is the sensor's glow, the darks stay good",
                $"creste cand senzorul se incalzeste (glow {Signed(s!.GlowAdu, 2)}, constant {Signed(s.ConstantAdu, 2)}): e glow-ul senzorului, dark-urile raman bune"),
            LeakCause.Leak => T(
                $"it does not grow with the warming and does not come from the bias (constant {Signed(s!.RemainderAdu, 2)}, glow {Signed(s.GlowAdu, 2)}): it is light that got into the camera, redo the darks",
                $"nu creste cu incalzirea si nu vine din bias (constant {Signed(s!.RemainderAdu, 2)}, glow {Signed(s.GlowAdu, 2)}): e lumina intrata in aparat, refa dark-urile"),
            LeakCause.Readout => T(
                $"it shows in the bias too ({Signed(s!.ReadoutAdu!.Value, 2)}, glow {Signed(s.GlowAdu, 2)}): it is a level difference of the readout, not light",
                $"apare si in bias ({Signed(s!.ReadoutAdu!.Value, 2)}, glow {Signed(s.GlowAdu, 2)}): e o diferenta de nivel din citire, nu lumina"),
            LeakCause.Constant => T(
                $"it does not grow with the warming (constant {Signed(s!.ConstantAdu, 2)}, glow {Signed(s.GlowAdu, 2)}): light that got in or a level difference of the readout; without biases the two cannot be told apart",
                $"nu creste cu incalzirea (constant {Signed(s!.ConstantAdu, 2)}, glow {Signed(s.GlowAdu, 2)}): lumina intrata sau o diferenta de nivel din citire; fara bias-uri nu se pot deosebi"),
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
            return new(label, T($"UNMEASURED: there are fewer than {Th.LeakFitMinFrames} frames with hot pixels", $"NEMASURAT: sunt mai putin de {RoCount(Th.LeakFitMinFrames)}cadre cu pixeli fierbinti"));
        }

        if (fl.LitFrames.Count == 0)
        {
            return new(label, T(
                $"no frame is brighter than its warming explains (threshold: above {F(Th.LeakFrameAdu, 1)} {eq} and more than {F(Th.LeakFrameSigmas, 0)} times the average spread of the frames, {F(fl.ScaleAdu, 2)} {eq})",
                $"niciun cadru nu e mai luminos decat ar explica incalzirea (prag: peste {F(Th.LeakFrameAdu, 1)} {eq} si de peste {RoCount(F(Th.LeakFrameSigmas, 0))}ori variatia medie a cadrelor, {F(fl.ScaleAdu, 2)} {eq})"));
        }

        int n = fl.LitFrames.Count;
        double max = fl.LitExcessAdu.Max();
        (string against, IReadOnlyList<string> bold) = ScaleReport.AgainstMasters(max * norm, masterNoise, stack);
        string tail = against.Length == 0 ? "" : T($"; the brightest, {Signed(max, 2)} {eq}: {against}", $"; cel mai luminat, {Signed(max, 2)} {eq}: {against}");
        return new(label, T(
            $"{n} {(n == 1 ? "frame is brighter than its" : "frames are brighter than their")} warming explains: {LitList(fl, ": ")} {eq} (average spread {F(fl.ScaleAdu, 2)}){tail}",
            $"{RoCount(n)}{(n == 1 ? "cadru e mai luminos" : "cadre sunt mai luminoase")} decat ar explica incalzirea: {LitList(fl, ": ")} {eq} (variatia medie {F(fl.ScaleAdu, 2)}){tail}"), bold.Count == 0 ? null : [$"{Signed(max, 2)} {eq}", .. bold]);
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
            return T($"UNMEASURED: there are fewer than {Th.LeakFitMinFrames} frames with hot pixels", $"NEMASURAT: sunt mai putin de {RoCount(Th.LeakFitMinFrames)}cadre cu pixeli fierbinti");
        }

        if (fl.RatioSpan < Th.LeakFitMinSpan)
        {
            return T(
                $"UNMEASURED: the series warmed too little to tell the glow from the constant light (the hot pixels vary by only {F(fl.RatioSpan, 2)} of the master, at least {F(Th.LeakFitMinSpan, 1)} is needed)",
                $"NEMASURAT: seria s-a incalzit prea putin ca sa se poata desparti glow-ul de lumina constanta (pixelii fierbinti variaza doar cu {F(fl.RatioSpan, 2)} din master, trebuie cel putin {F(Th.LeakFitMinSpan, 1)})");
        }

        if (fl.FramesKept < minFrames)
        {
            return T(
                $"UNMEASURED: without the lit frames too few are left ({fl.FramesKept}, at least {minFrames} are needed)",
                $"NEMASURAT: fara cadrele luminate raman prea putine ({fl.FramesKept}, trebuie cel putin {minFrames})");
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

        string raw = Math.Abs(norm - 1) < 1e-9 ? "" : $" ({T("in the file", "in fisier")}: glow {Signed(s.GlowAdu * norm, 2)}, constant {Signed(s.ConstantAdu * norm, 2)} ADU)";
        return s.ReadoutAdu is double ro
            ? T(
                $"at the {RegionName(s.Where)}: glow {Signed(s.GlowAdu, 2)} (grows as the sensor warms), constant {Signed(s.ConstantAdu, 2)}, of which {Signed(ro, 2)} comes from the bias, leaving {Signed(s.RemainderAdu, 2)} {eq}{raw}",
                $"la {RegionName(s.Where)}: glow {Signed(s.GlowAdu, 2)} (creste cand senzorul se incalzeste), constant {Signed(s.ConstantAdu, 2)}, din care {Signed(ro, 2)} vine din bias, raman {Signed(s.RemainderAdu, 2)} {eq}{raw}")
            : T(
                $"at the {RegionName(s.Where)}: glow {Signed(s.GlowAdu, 2)} (grows as the sensor warms), constant {Signed(s.ConstantAdu, 2)} {eq} (without biases there is no telling light from a level difference of the sensor's readout){raw}",
                $"la {RegionName(s.Where)}: glow {Signed(s.GlowAdu, 2)} (creste cand senzorul se incalzeste), constant {Signed(s.ConstantAdu, 2)} {eq} (fara bias-uri nu se stie daca e lumina sau o diferenta de nivel din citirea senzorului){raw}");
    }

    /// <summary>
    /// The words of a series without an end to read it against, in the run's language: too few
    /// frames, or hot pixels that do not show in its last frames.
    /// </summary>
    /// <param name="w">The series' warming, without an end.</param>
    /// <returns>The words, opening with UNMEASURED (NEMASURAT).</returns>
    private static string NoEnd(Warming w) => w.Measured < Th.WarmingFitMinFrames
        ? T(
            $"UNMEASURED: there are fewer than {Th.WarmingFitMinFrames} frames with hot pixels",
            $"NEMASURAT: sunt mai putin de {RoCount(Th.WarmingFitMinFrames)}cadre cu pixeli fierbinti")
        : T(
            "UNMEASURED: at the end of the series the master's hot pixels do not show",
            "NEMASURAT: la sfarsitul seriei pixelii fierbinti ai masterului nu se vad");

    /// <summary>
    /// The `sfarsitul seriei` row of check 3: which way the camera moved, from the hot pixels of
    /// the first frame to the end of the series, by how many degrees, and where the master sits
    /// against that end, never as a signed figure (ADR 0038, ADR 0043); a series that stayed
    /// within <see cref="Th.MasterNearEndCelsius"/> says so.
    /// </summary>
    /// <param name="w">The series' warming.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string EndRow(Warming w)
    {
        if (double.IsNaN(w.FirstRatio))
        {
            return T("UNMEASURED: too few frames or no hot pixel", "NEMASURAT: prea putine cadre sau niciun pixel fierbinte");
        }

        if (!w.HasEnd)
        {
            return NoEnd(w);
        }

        string first = F(w.FirstRatio, 2), end = F(w.EndRatio, 2);
        string master = FromEnd(w.MasterCelsius);
        string limit = F(Th.MasterNearEndCelsius, 1);
        if (Math.Abs(w.FirstCelsius) < Th.MasterNearEndCelsius)
        {
            return T(
                $"the camera stayed at the same temperature: the hot pixels are at {first} of the master in the first frame and at {end} at the end of the series ({EndWords}), less than {limit} degrees apart; the master is {master}",
                $"aparatul a stat la aceeasi temperatura: pixelii fierbinti sunt la {first} din master in primul cadru si la {end} la sfarsitul seriei ({EndWords}), mai putin de {limit} grade intre ele; masterul e {master}");
        }

        bool warming = !w.Cooling;
        // a first frame whose hot pixels do not show has no degrees to the end
        string degrees = double.IsNaN(w.FirstCelsius) ? "" : T($", that is {F(Math.Abs(w.FirstCelsius), 1)} degrees", $", adica {F(Math.Abs(w.FirstCelsius), 1)} grade");
        string minutes = double.IsNaN(w.FirstCelsius) || double.IsNaN(w.Minutes) ? "" : $" in {F(w.Minutes, 0)} min";
        return T(
            $"the camera {(warming ? "warmed" : "cooled")} during the series: the hot pixels {(warming ? "rise" : "fall")} from {first} of the master in the first frame to {end} at the end of the series ({EndWords}){degrees}{minutes}; the master is {master}",
            $"aparatul s-a {(warming ? "incalzit" : "racit")} in timpul seriei: pixelii fierbinti {(warming ? "cresc" : "scad")} de la {first} din master in primul cadru la {end} la sfarsitul seriei ({EndWords}){degrees}{minutes}; masterul e {master}");
    }

    /// <summary>
    /// The `de aruncat` row of check 3: the frames more than Th.OffEndCelsius from the end of the
    /// series, the band's bounds, and where the master would sit without them.
    /// </summary>
    /// <param name="w">The series' warming.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string OffEndRow(Warming w)
    {
        if (!w.HasEnd)
        {
            return NoEnd(w);
        }

        string deg = F(Th.OffEndCelsius, 1);
        if (w.OffEndFrames.Count == 0)
        {
            return T(
                $"no frame is more than {deg} degrees from the end of the series (the hot pixels between {F(w.OffEndLow, 2)} and {F(w.OffEndHigh, 2)} of the master)",
                $"niciun cadru nu e la mai mult de {deg} grade de sfarsitul seriei (pixelii fierbinti intre {F(w.OffEndLow, 2)} si {F(w.OffEndHigh, 2)} din master)");
        }

        string without = double.IsNaN(w.MasterWithoutOffCelsius)
            ? T("without them no frame is left", "fara ele nu ramane niciun cadru")
            : T(
                $"without them the master would be {FromEnd(w.MasterWithoutOffCelsius)}, but with {F((w.NoiseFactorWithoutOff - 1) * 100, 0)} % more noise",
                $"fara ele masterul ar fi {FromEnd(w.MasterWithoutOffCelsius)}, dar cu {F((w.NoiseFactorWithoutOff - 1) * 100, 0)} % mai mult zgomot");
        return T(
            $"{RunReport.Frames(w.OffEndFrames.Count)} ({Ranges(w.OffEndFrames)}) {(w.OffEndFrames.Count == 1 ? "is" : "are")} more than {deg} degrees from the end of the series (the hot pixels below {F(w.OffEndLow, 2)} or above {F(w.OffEndHigh, 2)} of the master; the end of the series {F(w.EndRatio, 2)}); {without}",
            $"{RunReport.Frames(w.OffEndFrames.Count)} ({Ranges(w.OffEndFrames)}) {(w.OffEndFrames.Count == 1 ? "e" : "sunt")} la mai mult de {deg} grade de sfarsitul seriei (pixelii fierbinti sub {F(w.OffEndLow, 2)} sau peste {F(w.OffEndHigh, 2)} din master; sfarsitul seriei {F(w.EndRatio, 2)}); {without}");
    }

    /// <summary>
    /// The verdict row of check 3: whether the master is near the end of the series, whether most
    /// frames are off it, and the frames outside the range -opt corrects.
    /// </summary>
    /// <param name="v">Check 3's verdict.</param>
    /// <param name="nz">The normalisation, whose doubling rule the words name.</param>
    /// <returns>The verdict's words, opening with OK, WARNING, REDO or UNMEASURED (OK, ATENTIE,
    /// REFA or NEMASURAT in Romanian).</returns>
    private static string Drift(DriftVerdict v, Normalisation nz)
    {
        Warming w = v.Warming;
        string within = T($"(between {F(Th.MatchOptLow, 1)} and {F(Th.MatchOptHigh, 1)})", $"(intre {F(Th.MatchOptLow, 1)} si {F(Th.MatchOptHigh, 1)})");
        if (v.Verdict == Verdict.Unmeasured)
        {
            return T("UNMEASURED", "NEMASURAT");
        }

        string limit = F(Th.MasterNearEndCelsius, 1);
        bool discard = w.OffEndFrames.Count > 0;

        if (v.Verdict == Verdict.Ok)
        {
            if (!w.HasEnd)
            {
                // an OK without an end is a short series: an end that is not positive has frames outside the band
                return T(
                    $"OK: every frame is close enough to the master for Siril to match it with -opt {within}; there are fewer than {Th.WarmingFitMinFrames} frames, so the end of the series is not measured",
                    $"OK: toate cadrele sunt destul de aproape de master ca Siril sa le potriveasca cu -opt {within}; sunt mai putin de {RoCount(Th.WarmingFitMinFrames)}cadre, deci sfarsitul seriei nu se masoara");
            }

            string rule = T(
                $"the degrees are reckoned with the rule that the thermal signal doubles every {F(nz.DoublingCelsius, 0)} degrees",
                $"gradele sunt socotite cu regula: semnalul termic se dubleaza la fiecare {RoCount(F(nz.DoublingCelsius, 0))}grade");
            string off = discard
                ? T("the frames to discard are in the row above", "cadrele de aruncat sunt in randul de mai sus")
                : T("no frame to discard", "niciun cadru de aruncat");
            return T(
                $"OK: the master is close to the end of the series ({FromIt(w.MasterCelsius)}, under {limit}) and every frame is close enough to the master for Siril to match it with -opt {within}; {rule}; {off}",
                $"OK: masterul e aproape de sfarsitul seriei ({FromIt(w.MasterCelsius)}, mai putin de {limit}) si toate cadrele sunt destul de aproape de master ca Siril sa le potriveasca cu -opt {within}; {rule}; {off}");
        }

        List<string> parts = [];
        if (v.MostlyOff)
        {
            // three time constants bring the camera within 5 % of where it settles; rounded up to 5 minutes (or frames)
            string tau = F(w.TauMinutes, 0);
            string rest = F(Math.Ceiling(3 * w.TauMinutes / 5) * 5, 0);
            // a cooling series names its cause; every series warms itself, so a warming one has none (ADR 0017)
            string advice = !w.Cooling
                ? T("take a longer series: the first frames are the warm-up", "refa seria mai lunga: primele cadre sunt incalzirea")
                : T("the camera started warmer than the place it was in: ", "aparatul a pornit mai cald decat locul in care a stat: ") + (double.IsNaN(w.TauMinutes)
                    ? T(
                        "take the series again after the camera has sat longer where it shoots",
                        "refa seria dupa ce aparatul a stat mai mult in locul unde se trag dark-urile")
                    : w.TimeFromStamps
                        ? T(
                            $"take the series again after the camera has sat where it shoots for about {rest} min (three times the {tau} min of this series)",
                            $"refa seria dupa ce aparatul a stat vreo {rest} min in locul unde se trag dark-urile (de trei ori cele {tau} min ale seriei)")
                        : T(
                            $"take the series again after the camera has sat where it shoots for the time of about {rest} frames (three times the {tau} frames of this series)",
                            $"refa seria dupa ce aparatul a stat in locul unde se trag dark-urile cat timp iau vreo {RoCount(rest)}cadre (de trei ori cele {RoCount(tau)}cadre ale seriei)"));
            parts.Add(T(
                $"{w.OffEndFrames.Count} of {v.FramesWithRatio} frames are more than {F(Th.OffEndCelsius, 1)} degrees from the end of the series, so the master belongs to no temperature; {advice}",
                $"{w.OffEndFrames.Count} din {RoCount(v.FramesWithRatio)}cadre sunt la peste {F(Th.OffEndCelsius, 1)} grade de sfarsitul seriei, deci masterul nu apartine niciunei temperaturi; {advice}"));
        }
        else if (!v.MasterNearEnd)
        {
            // the master under the end: the colder frames of the start pull it down; above it, the
            // warmer ones pull it up, and only then is there a cause to name (ADR 0017)
            bool under = w.MasterCelsius < 0;
            string advice = discard
                ? T("discard the frames in the row above", "arunca cadrele din randul de mai sus")
                : T("take a longer series", "fa seria mai lunga");
            parts.Add(under
                ? T(
                    $"the master is {FromEnd(w.MasterCelsius)}, more than {limit}: the frames from the start, colder, pull it down; {advice}",
                    $"masterul e {FromEnd(w.MasterCelsius)}, mai mult de {limit}: cadrele de la inceput, mai reci, il trag in jos; {advice}")
                : T(
                    $"the master is {FromEnd(w.MasterCelsius)}, more than {limit}: the frames from the start, warmer, pull it up (the camera started warmer than the place it was in); {advice}",
                    $"masterul e {FromEnd(w.MasterCelsius)}, mai mult de {limit}: cadrele de la inceput, mai calde, il trag in sus (aparatul a pornit mai cald decat locul in care a stat); {advice}"));
        }

        if (v.OutsideFrames.Count > 0)
        {
            int n = v.OutsideFrames.Count;
            parts.Add(T(
                $"{RunReport.Frames(n)} ({Ranges(v.OutsideFrames)}) {(n == 1 ? "is" : "are")} too far from the master for Siril to match {(n == 1 ? "it" : "them")} with -opt (outside {F(Th.MatchOptLow, 1)} .. {F(Th.MatchOptHigh, 1)}; the farthest {F(v.FarthestRatio, 2)}): the series started {(v.FarthestRatio < 1 ? "cold" : "warm")}; discard {(n == 1 ? "it" : "them")}",
                $"{RunReport.Frames(n)} ({Ranges(v.OutsideFrames)}) {(n == 1 ? "e prea departe" : "sunt prea departe")} de master ca Siril sa {(n == 1 ? "il" : "le")} potriveasca cu -opt (in afara {F(Th.MatchOptLow, 1)} .. {F(Th.MatchOptHigh, 1)}; cel mai departe {F(v.FarthestRatio, 2)}): seria a pornit {(v.FarthestRatio < 1 ? "rece" : "calda")}; arunca-{(n == 1 ? "l" : "le")}"));
        }

        return VerdictWord(v.Verdict) + ": " + string.Join("; ", parts);
    }

    /// <summary>
    /// A distance to the end of the series in words: "level with the end of the series", or the
    /// degrees under or over it.
    /// </summary>
    /// <param name="degrees">The distance in degrees, negative under the end.</param>
    /// <returns>The words, in the run's language.</returns>
    internal static string FromEnd(double degrees) =>
        Math.Abs(degrees) < 0.05
            ? T("level with the end of the series", "la nivelul sfarsitului seriei")
            : T(
                $"{EndDegrees(degrees)} degrees {(degrees < 0 ? "below" : "above")} the end of the series",
                $"cu {EndDegrees(degrees)} grade {(degrees < 0 ? "sub" : "peste")} sfarsitul seriei");

    /// <summary>
    /// The size of a distance to the end of the series as its words print it: one decimal, or two
    /// when one decimal would show the limit itself (<see cref="Th.MasterNearEndCelsius"/>), so a
    /// distance of 0.99 does not stand as "1.0" beside "under 1.0".
    /// </summary>
    /// <param name="degrees">The distance in degrees, of either sign.</param>
    /// <returns>The figure, unsigned.</returns>
    private static string EndDegrees(double degrees)
    {
        double size = Math.Abs(degrees);
        string one = F(size, 1);
        return one == F(Th.MasterNearEndCelsius, 1) && size != Th.MasterNearEndCelsius ? F(size, 2) : one;
    }

    /// <summary>
    /// <see cref="FromEnd"/> for a sentence that has just named the end of the series: "0.7
    /// degrees below it", "level with it".
    /// </summary>
    /// <param name="degrees">The distance in degrees, negative under the end.</param>
    /// <returns>The words, in the run's language.</returns>
    internal static string FromIt(double degrees) =>
        Math.Abs(degrees) < 0.05
            ? T("level with it", "la nivelul lui")
            : T(
                $"{EndDegrees(degrees)} degrees {(degrees < 0 ? "below" : "above")} it",
                $"cu {EndDegrees(degrees)} grade {(degrees < 0 ? "sub" : "peste")} el");

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
    /// The <c>fata de master</c> row of check 4: each plane's smallest and largest
    /// frame-minus-master sigma, with the median on the reference plane.
    /// </summary>
    /// <param name="d">The dark series.</param>
    /// <param name="rp">The reference plane.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string ResidualRange(DarkSeriesAnalysis d, int rp)
    {
        if (d.Used < 2)
        {
            return T("UNMEASURED: a single frame has nothing to be compared with", "NEMASURAT: un singur cadru nu are cu ce fi comparat");
        }

        List<string> parts = [];
        for (int p = 0; p < 4; p++)
        {
            double[] s = d.Frames.Where(f => f.Error is null).Select(f => f.Residuals[p].Sigma).ToArray();
            if (s.Length == 0)
            {
                continue;
            }

            parts.Add($"{d.PlaneNames[p]} {F(s.Min(), 2)} .. {F(s.Max(), 2)}" + (p == rp ? $" ({T("on average", "in medie")} {F(Planes.Median([.. s.Select(x => (float)x)]), 2)})" : ""));
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
            return T("UNMEASURED: the session has no biases", "NEMASURAT: sesiunea nu are bias-uri");
        }

        string planes = string.Join("  ", Enumerable.Range(0, 4).Select(p => $"{b.PlaneNames[p]} {F(b.ReadNoise[p], 2)}"));
        string shot = r.Verdicts.Noise.NoiseOverRead is double s
            ? T(
                $"; the noise the exposure adds above the read noise, on {ScaleReport.PlaneWords(b.PlaneNames[rp])}: ~{F(s, 2)} ADU (it comes from the sensor's heat and from the value step)",
                $"; zgomotul adaugat in timpul expunerii, peste cel de citire, pe {ScaleReport.PlaneWords(b.PlaneNames[rp])}: ~{F(s, 2)} ADU (vine din caldura senzorului si din pasul valorilor)")
            : "";
        return T(
            $"{planes} ADU (from {Math.Max(b.Used - 1, 0)} pairs of biases taken one after the other){shot}",
            $"{planes} ADU (din {RoCount(Math.Max(b.Used - 1, 0))}perechi de bias-uri luate unul dupa altul){shot}");
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
                $"horizontal: the rows differ from one another by {F(v.RowBandingAdu, 3)} ADU, {F(v.RowBandingRatio, 1)} times what the noise alone would give; vertical: {F(v.ColBandingRatio, 1)} times (1 means no banding)",
                $"orizontale: randurile difera intre ele cu {F(v.RowBandingAdu, 3)} ADU, de {F(v.RowBandingRatio, 1)} ori cat ar da zgomotul singur; verticale: de {F(v.ColBandingRatio, 1)} ori (1 inseamna fara benzi)");

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
                $"OK: no frame has noise more than {F(Th.NoiseOutlierFactor, 1)} times the average noise ({F(v.MedianSigma, 2)} ADU)",
                $"OK: niciun cadru nu are zgomotul de peste {F(Th.NoiseOutlierFactor, 1)} ori zgomotul mediu ({F(v.MedianSigma, 2)} ADU)"),
            _ => T(
                $"WARNING: {v.OutlierFrames.Count} {(v.OutlierFrames.Count == 1 ? "frame has" : "frames have")} noise more than {F(Th.NoiseOutlierFactor, 1)} times the average noise ({F(v.MedianSigma, 2)} ADU): {frames}",
                $"ATENTIE: {RoCount(v.OutlierFrames.Count)}{(v.OutlierFrames.Count == 1 ? "cadru are" : "cadre au")} zgomotul de peste {F(Th.NoiseOutlierFactor, 1)} ori zgomotul mediu ({F(v.MedianSigma, 2)} ADU): {frames}"),
        };
    }

    /// <summary>
    /// The <c>pixeli peste prag</c> row of check 5: how many master pixels lie above each hot
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
            parts.Add(T($"above {F(Th.HotLevelsAdu[k], 0)}: {total}", $"peste {F(Th.HotLevelsAdu[k], 0)}: {total}"));
        }

        return string.Join("; ", parts) + T($"  [the master's pixels, {Eq(d)} above the black, all colours]", $"  [pixeli ai masterului, {Eq(d)} peste negru, toate culorile]");
    }

    /// <summary>How many master pixels of each plane lie above one hot level.</summary>
    /// <param name="d">The dark series.</param>
    /// <param name="level">The index of the level in <see cref="Th.HotLevelsAdu"/>.</param>
    /// <returns>The counts, as "R 12  G1 9 ...".</returns>
    private static string PerPlane(DarkSeriesAnalysis d, int level) =>
        string.Join("  ", d.Master.Select(m => $"{m.Name} {m.Above[level]}"));

    /// <summary>
    /// The <c>cat de fierbinti</c> row of check 5: the hot pixels' percentiles above the black.
    /// </summary>
    /// <param name="h">The hot pixels' figures, in raw ADU.</param>
    /// <param name="n">The normalisation factor the percentiles are divided by.</param>
    /// <param name="eq">The unit word of the figures.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string Percentiles(HotStability h, double n, string eq) =>
        h.Hot == 0
            ? T("UNMEASURED: no hot pixel", "NEMASURAT: niciun pixel fierbinte")
            : T(
                $"on average {F(h.P50 / n, 0)} {eq}; 80 % of them between {F(h.P10 / n, 0)} and {F(h.P90 / n, 0)}; the hottest 1 % above {F(h.P99 / n, 0)} (of {h.Hot} {(h.Hot == 1 ? "pixel" : "pixels")})",
                $"in medie {F(h.P50 / n, 0)} {eq}; 80 % dintre ei intre {F(h.P10 / n, 0)} si {F(h.P90 / n, 0)}; cei mai fierbinti 1 % peste {F(h.P99 / n, 0)} (din {RoCount(h.Hot)}{(h.Hot == 1 ? "pixel" : "pixeli")})");

    /// <summary>
    /// The <c>stabilitate</c> row of check 5: the hot pixels present in every frame, the flickering
    /// ones, their temporal noise and the RTS candidates.
    /// </summary>
    /// <param name="d">The dark series.</param>
    /// <param name="rp">The reference plane.</param>
    /// <returns>The row's statements, in the run's language: one unmeasured line, or four (how
    /// many show in every frame, how many miss two or more, how much they vary against the quiet
    /// pixels, the RTS candidates).</returns>
    private static IReadOnlyList<string> Stability(DarkSeriesAnalysis d, int rp)
    {
        HotStability h = d.Hot;
        if (h.Hot == 0)
        {
            return [T("UNMEASURED: no hot pixel", "NEMASURAT: niciun pixel fierbinte")];
        }

        if (d.Used < 2)
        {
            return [T("UNMEASURED: the stability needs at least 2 frames", "NEMASURAT: stabilitatea cere cel putin 2 cadre")];
        }

        string plane = d.PlaneNames[rp];
        bool green = plane.StartsWith('G');
        string rts = h.RtsCandidates == 1
            ? T($"1 {(green ? "green " : "")}pixel {plane} jumps", $"1 pixel {(green ? "verde " : "")}{plane} sare")
            : T($"{h.RtsCandidates} {(green ? "green " : "")}pixels {plane} jump", $"{RoCount(h.RtsCandidates)}pixeli {(green ? "verzi " : "")}{plane} sar");
        return
        [
            T($"{h.Always} of {h.Hot} show (above half the threshold) in all {RunReport.Frames(d.Used)}", $"{h.Always} din {h.Hot} se vad (peste jumatate din prag) in toate cele {RunReport.Frames(d.Used)}"),
            T($"{h.Flicker} are missing from 2 or more", $"{h.Flicker} lipsesc din 2 sau mai multe"),
            T($"from one frame to the next they vary by {F(h.HotTemporalStd, 2)} ADU, the other pixels by {F(h.QuietTemporalStd, 2)}", $"de la un cadru la altul variaza cu {F(h.HotTemporalStd, 2)} ADU, ceilalti pixeli cu {F(h.QuietTemporalStd, 2)}"),
            T($"{rts} between two levels (RTS: they vary more than {F(Th.RtsSigmaFactor, 0)} times the noise of one frame, {F(d.Noise.SigmaFrame, 2)})", $"{rts} intre doua niveluri (RTS: variaza de peste {RoCount(F(Th.RtsSigmaFactor, 0))}ori cat zgomotul unui cadru, {F(d.Noise.SigmaFrame, 2)})"),
        ];
    }

    /// <summary>
    /// The <c>stabilitate</c> row of check 5 (R-031): the statements of <see cref="Stability"/>
    /// joined by "; " for the console and carried as lines for the window; one unmeasured line
    /// stands alone.
    /// </summary>
    /// <param name="d">The dark series.</param>
    /// <param name="rp">The reference plane.</param>
    /// <returns>The row.</returns>
    private static ReportRow StabilityRow(DarkSeriesAnalysis d, int rp)
    {
        IReadOnlyList<string> lines = Stability(d, rp);
        string label = T("stability", "stabilitate");
        return lines.Count == 1 ? new(label, lines[0]) : new(label, string.Join("; ", lines)) { Lines = lines };
    }

    /// <summary>
    /// The <c>benzi fixe</c> row of check 6: the worst planes' row and column profiles of
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
            $"the master's rows differ from one another by {F(worstRow.RowStd / n, 3)} {Eq(d)} ({F(worstRow.RowP2p / n, 2)} between the brightest and the darkest, on {worstRow.Name}), the columns by {F(worstCol.ColStd / n, 3)} ({F(worstCol.ColP2p / n, 2)}, on {worstCol.Name})  [once the slow changes are taken out, over {Th.BandingWindow} rows or columns at a time]",
            $"randurile masterului difera intre ele cu {F(worstRow.RowStd / n, 3)} {Eq(d)} ({F(worstRow.RowP2p / n, 2)} intre cel mai luminos si cel mai intunecat, pe {worstRow.Name}), coloanele cu {F(worstCol.ColStd / n, 3)} ({F(worstCol.ColP2p / n, 2)}, pe {worstCol.Name})  [dupa ce se scot variatiile lente, pe cate {RoCount(Th.BandingWindow)}randuri sau coloane]");
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
        string source = l.FromDarkMinusBias ? T("on dark minus bias", "pe dark minus bias") : T("on the dark master", "pe masterul dark");
        if (double.IsNaN(p.GlowAdu))
        {
            return T("UNMEASURED", "NEMASURAT");
        }

        return l.Split is EdgeSplit s
            ? T(
                $"{Signed(p.GlowAdu, 2)} {eq} at the {RegionName(l.Where)}, the part that grows as the sensor warms (besides it, constant {Signed(s.ConstantAdu, 2)}; {source})",
                $"{Signed(p.GlowAdu, 2)} {eq} la {RegionName(l.Where)}, partea care creste cand senzorul se incalzeste (in afara ei, constant {Signed(s.ConstantAdu, 2)}; {source})")
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
            Verdict.Unmeasured => T("UNMEASURED: the frames are too small for the map or the banding", "NEMASURAT: cadrele sunt prea mici pentru harta sau pentru benzi"),
            Verdict.Ok => T(
                $"OK: the fixed banding stays under {F(Th.BandingAdu, 1)} {eq} between the brightest and the darkest row, and the edges under {F(Th.LeakAdu, 1)} from the centre",
                $"OK: benzile fixe raman sub {F(Th.BandingAdu, 1)} {eq} intre cel mai luminos si cel mai intunecat rand, iar marginile sub {F(Th.LeakAdu, 1)} fata de centru"),
            _ => T(
                $"WARNING: {(banding ? $"fixed banding of {F(p.BandingP2pAdu, 2)} {eq} between the brightest and the darkest row" : "")}{(banding && glow ? " and " : "")}{(glow ? $"glow of {Signed(p.GlowAdu, 2)} {eq}" : "")}; without darks this pattern stays in the lights",
                $"ATENTIE: {(banding ? $"benzi fixe de {F(p.BandingP2pAdu, 2)} {eq} intre cel mai luminos si cel mai intunecat rand" : "")}{(banding && glow ? " si " : "")}{(glow ? $"glow de {Signed(p.GlowAdu, 2)} {eq}" : "")}; fara dark-uri, desenul acesta ramane in lights"),
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
            $"ATENTIE: {RoCount(v.Dead)}{(v.Dead == 1 ? "pixel mort" : "pixeli morti")}, {RoCount(v.Saturated)}{(v.Saturated == 1 ? "saturat" : "saturati")}");

    /// <summary>
    /// The <c>zgomot dupa medie</c> row of check 8: the measured and the expected noise of the mean
    /// of n frames.
    /// </summary>
    /// <param name="nv">The noise-against-n figures.</param>
    /// <returns>The row's value, in the run's language.</returns>
    private static string Curve(NoiseVsN nv)
    {
        string expected = T("expected", "se astepta");
        return nv.Curve.Count == 0
            ? T("UNMEASURED: there are fewer than 2 frames", "NEMASURAT: sunt mai putin de 2 cadre")
            : string.Join("; ", nv.Curve.Select(p => $"{RunReport.Frames(p.N)}: {F(p.Measured, 2)} ({expected} {F(p.Expected, 2)})"));
    }

    /// <summary>
    /// The <c>zgomot dupa medie</c> row of check 8 (R-024): the console's line, the points joined
    /// by "; " and the note in brackets, and the same points as a table for the window; without a
    /// curve the unmeasured line alone, the note still in brackets.
    /// </summary>
    /// <param name="nv">The noise-against-n figures.</param>
    /// <param name="g">The reference plane's name.</param>
    /// <returns>The row, with its table when there is a curve.</returns>
    private static ReportRow CurveRow(NoiseVsN nv, string g)
    {
        string note = T(
            $"{ScaleReport.PlaneWords(g)}, the central {nv.CropPx} px, ADU; measured from one frame to the next",
            $"{ScaleReport.PlaneWords(g)}, centrul de {nv.CropPx} px, ADU; masurat de la un cadru la altul");
        return new(T("noise when averaged", "zgomot dupa medie"), Curve(nv) + "  [" + note + "]")
        {
            Table = nv.Curve.Count == 0 ? null : new(
                [T("frames", "cadre"), T("measured", "masurat"), T("expected", "se astepta")],
                nv.Curve.Select(p => (IReadOnlyList<string>)[RunReport.Frames(p.N), F(p.Measured, 2), F(p.Expected, 2)]).ToList(),
                note),
        };
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
            ? T($"from the frame's noise, {F(d.Noise.SigmaFrame, 2)}, as it is the only one", $"din zgomotul cadrului, {F(d.Noise.SigmaFrame, 2)}, fiind unul singur")
            : T($"from the noise from one frame to the next, {F(d.Noise.TemporalSigma, 2)}", $"din zgomotul de la un cadru la altul, {F(d.Noise.TemporalSigma, 2)}");
        int byMedian = e.Frames - e.Frames / 2;
        int byMean = e.Frames / 2;
        string half = e.Frames >= 2
            ? T(
                $"; a master of {RunReport.Frames(byMedian)} made with the median and one of {(byMean == 1 ? "the frame left" : $"the other {byMean}")} made with the mean differ by {F(d.Noise.MedianHalfVsMeanHalf, 2)} ADU",
                $"; un master din {RunReport.Frames(byMedian)} facut cu mediana si unul din {(byMean == 1 ? "cadrul ramas" : $"celelalte {byMean}")} facut cu media difera cu {F(d.Noise.MedianHalfVsMeanHalf, 2)} ADU")
            : "";
        return T(
            $"{RunReport.Frames(e.Frames)} {(e.Frames == 1 ? "gives" : "give")} a master with {F(e.MasterNoise, 2)} ADU of noise per pixel ({basis}){half}",
            $"{RunReport.Frames(e.Frames)} {(e.Frames == 1 ? "da" : "dau")} un master cu {F(e.MasterNoise, 2)} ADU de zgomot pe pixel ({basis}){half}");
    }

    /// <summary>The verdict row of check 8.</summary>
    /// <param name="e">Check 8's verdict.</param>
    /// <returns>The verdict's words, opening with OK, WARNING or UNMEASURED (OK, ATENTIE or
    /// NEMASURAT in Romanian).</returns>
    private static string EnoughVerdictText(EnoughVerdict e) => e.Verdict switch
    {
        Verdict.Unmeasured => T("UNMEASURED: a single frame is not a series", "NEMASURAT: un singur cadru nu e o serie"),
        Verdict.Ok => T(
            $"OK: {RunReport.Frames(e.Frames)} are enough; the master's noise ({F(e.MasterNoise, 2)} ADU) is under the file's value step ({e.QuantStep} ADU), and it adds to every calibrated light",
            $"OK: {RunReport.Frames(e.Frames)} ajung; zgomotul masterului ({F(e.MasterNoise, 2)} ADU) e sub pasul valorilor din fisier ({e.QuantStep} ADU), iar el se adauga la fiecare light calibrat"),
        _ when e.TooFew => T(
            $"WARNING: only {RunReport.Frames(e.Frames)}; at least {Th.MinDarks} are needed, so that one odd frame does not spoil the master (the master's noise would be {F(e.MasterNoise, 2)} ADU)",
            $"ATENTIE: doar {RunReport.Frames(e.Frames)}; trebuie cel putin {Th.MinDarks}, ca un cadru ciudat sa nu strice masterul (zgomotul masterului ar fi {F(e.MasterNoise, 2)} ADU)"),
        _ => T(
            $"WARNING: the master's noise ({F(e.MasterNoise, 2)} ADU) is above the file's value step ({e.QuantStep} ADU); it would drop under it from {e.FramesNeeded} frames",
            $"ATENTIE: zgomotul masterului ({F(e.MasterNoise, 2)} ADU) e peste pasul valorilor din fisier ({e.QuantStep} ADU); ar cobori sub el de la {RoCount(e.FramesNeeded)}cadre"),
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

    /// <summary>A pixel count in words: "1 pixel", "20 pixels" ("20 de pixeli" in Romanian, R-095).</summary>
    /// <param name="n">The count.</param>
    /// <returns>The words.</returns>
    private static string Pixels(int n) => n == 1 ? "1 pixel" : T(n.ToString(CultureInfo.InvariantCulture) + " pixels", RoCount(n) + "pixeli");

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
