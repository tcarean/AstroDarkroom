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

// Recommendations.cs - the engine: from the results, a list of recommendations in the order of
// the decisions. Each rule is a small function over a record; the thresholds are in Thresholds,
// the wordings are here. Nothing here is rewritten in the window: the window shows the list, it
// does not deduce it.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// Turns an analysis's results into the report's recommendations, each with its "because"
    /// (ADR 0011), in the interface language.
    /// </summary>
    internal static class Recommendations
    {
        /// <summary>The recommendations of the six sections, in decision order.</summary>
        /// <param name="r">
        /// The analysis, every result filled; the recommendations and the report body are not
        /// read.
        /// </param>
        /// <returns>
        /// The list, sorted by <see cref="Topic"/>, each section's in the order its rules add them.
        /// </returns>
        internal static IReadOnlyList<Recommendation> Build(AnalysisResult r)
        {
            var list = new List<Recommendation>();
            Format(r, list);
            Background(r, list);
            Colour(r, list);
            Size(r, list);
            Denoise(r, list);
            Warnings(r, list);
            return list.OrderBy(x => x.Topic).ToList();
        }

        // ------------------------------------------------------------ 1. format

        /// <summary>
        /// Section 1, the format to send: the 8-bit LZW TIFF (with the rounding on a source over 8
        /// bits), JPEG q100 when the lab accepts only JPEG (the qualities under it that fail are a
        /// fact of the Export tab, ADR 0061), and the banding risk, from the share of the frame
        /// with a visible plateau at the reference row's dpi (ADR 0060) or from the noise alone.
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="o">The list the recommendations are added to.</param>
        static void Format(AnalysisResult r, List<Recommendation> o)
        {
            QuantizationResult q = r.Quantization;
            JpegRow q100 = r.Jpeg.Rows.FirstOrDefault(x => x.Quality == 100);
            SizeRow size = r.Size.ReferenceRow;

            if (q.Applicable)
                o.Add(new Recommendation(Topic.Format, Verdict.Ok,
                    Tr.Pick($"Send the lab an 8-bit TIFF with LZW compression, which loses nothing ({Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}). Going from 16 to 8 bits only rounds the values; nothing is lost.", $"Trimite laboratorului un TIFF pe 8 biti, cu compresie LZW, fara pierderi ({Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}). Trecerea de la 16 la 8 biti doar rotunjeste valorile; nu se pierde nimic."),
                    Tr.Pick($"in the rounding a pixel moves by {Fmt.F2(q.RoundingMeanAbs)} of a level on average and by {Fmt.F2(q.RoundingMax)} at most (a rounding cannot move it by more than half a level)", $"la rotunjire, un pixel se muta in medie cu {Fmt.F2(q.RoundingMeanAbs)} dintr-un nivel si cel mult cu {Fmt.F2(q.RoundingMax)} (o rotunjire nu poate muta mai mult de jumatate de nivel)")));
            else
                o.Add(new Recommendation(Topic.Format, Verdict.Ok,
                    Tr.Pick($"Send the lab an 8-bit TIFF with LZW compression, which loses nothing ({Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}). The file is already 8-bit; there is nothing to convert.", $"Trimite laboratorului un TIFF pe 8 biti, cu compresie LZW, fara pierderi ({Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}). Fisierul e deja pe 8 biti; nu e nimic de convertit."),
                    Tr.Pick($"the file has {r.File.BitDepth} bits on each channel", $"fisierul are {r.File.BitDepth} biti pe fiecare canal")));

            if (q100 != null)
            {
                Verdict v = q100.Acceptable ? (q100.PctOverThreshold > Thresholds.JpegOverThresholdWarnPct ? Verdict.Attention : Verdict.Ok) : Verdict.Attention;
                o.Add(new Recommendation(Topic.Format, v,
                    Tr.Pick($"If the lab takes only JPEG: quality 100, with the colour kept at full resolution (the 4:4:4 setting) ({Fmt.Mb(q100.Megabytes)}).", $"Daca laboratorul primeste doar JPEG: calitate 100, cu culoarea pastrata la rezolutie intreaga (setarea 4:4:4) ({Fmt.Mb(q100.Megabytes)})."),
                    Tr.Pick($"against the original, the JPEG changes a pixel by {Fmt.F2(q100.MeanAbsError)} levels on average and by {Fmt.F1(q100.MaxError)} at most; {Fmt.Pct(q100.PctOverThreshold)} of the values change by more than {Fmt.F0(Thresholds.JpegErrorCountLevels)} levels, mostly around level {Fmt.F0(q100.MeanLevelOverThreshold)}", $"fata de original, JPEG-ul schimba un pixel in medie cu {Fmt.F2(q100.MeanAbsError)} niveluri si cel mult cu {Fmt.F1(q100.MaxError)}; {Fmt.Pct(q100.PctOverThreshold)} din valori se schimba cu mai mult de {Fmt.F0(Thresholds.JpegErrorCountLevels)} niveluri, mai ales in jurul nivelului {Fmt.F0(q100.MeanLevelOverThreshold)}")
                    + (q100.PctOverThreshold > 0 && q100.MeanLevelOverThreshold < Thresholds.DarkLevelB ? Tr.Pick(" (in the dark sky, where JPEG does worst)", " (in cerul intunecat, unde JPEG-ul se descurca cel mai prost)") : "")));

                // The qualities under 100 that fail are a fact of the Export tab, not a warning
                // here (ADR 0061): the lab gets the TIFF of the first line.
            }

            if (q.PlateauKnown && size != null)
            {
                // The verdict is the share of the frame in clean tiles whose plateau passes the
                // visible length at the print's dpi (ADR 0060); the mean plateau is the typical
                // step, told for information. Where it was measured matters: on an AI-denoised
                // frame the only tiles clean enough are the patches the denoiser wiped, a few
                // tenths of a percent of the frame at a sigma near zero, while the noise section's
                // median says the sky is fine. The reader must see that the two speak of
                // different places.
                double flat = q.PlateauVisibleFramePctAt(size.Dpi);
                string share = Fmt.Strong(Fmt.F2(flat) + " %"), limit = Fmt.Strong(Fmt.F2(Thresholds.PlateauFlatFrameWarnPct) + " %");
                string steps = Tr.Pick($"at {Fmt.F0(size.Dpi)} dpi, flat areas, with no change of tone, wider than {Fmt.F1(Thresholds.PlateauVisibleMm)} mm cover {share} of the frame (threshold {limit})",
                    $"la {Fmt.F0(size.Dpi)} dpi, zonele plate, fara nicio trecere de ton, mai late de {Fmt.F1(Thresholds.PlateauVisibleMm)} mm acopera {share} din cadru (prag {limit})");
                string where = Tr.Pick($"; in the {Fmt.Int(q.PlateauTiles)} small areas of smooth sky ({Fmt.Pct(q.PlateauFramePct)} of the frame) a flat area is usually {Fmt.F1(q.PlateauPx)} px = {Fmt.F2(q.PlateauMmAt(size.Dpi))} mm, and the noise left there is {Fmt.F2(q.PlateauSigmaLsb)} levels",
                    $"; in cele {Fmt.RoCount(Fmt.Int(q.PlateauTiles))}zone mici de cer neted ({Fmt.Pct(q.PlateauFramePct)} din cadru), o zona plata are de obicei {Fmt.F1(q.PlateauPx)} px = {Fmt.F2(q.PlateauMmAt(size.Dpi))} mm, iar zgomotul ramas acolo e de {Fmt.F2(q.PlateauSigmaLsb)} niveluri");
                if (q.PlateauVisibleAt(size.Dpi))
                    o.Add(new Recommendation(Topic.Format, Verdict.Attention,
                        Tr.Pick("At 8 bits visible steps may appear in the sky (banding): send the file at 16 bits if the lab takes it, or add a little noise at the export (dither).", "Pe 8 biti pot aparea trepte vizibile in cer (banding): trimite fisierul pe 16 biti daca laboratorul il primeste, sau adauga putin zgomot la export (dither)."),
                        steps + where));
                else
                    o.Add(new Recommendation(Topic.Format, Verdict.Ok,
                        Tr.Pick("At 8 bits no visible steps appear in the sky: the flat areas cover too little of the frame to show.", "Pe 8 biti nu apar trepte vizibile in cer: zonele plate acopera prea putin din cadru ca sa se vada."),
                        steps + where));
            }
            else if (r.Noise.BandingRisk)
                o.Add(new Recommendation(Topic.Format, Verdict.Attention,
                    Tr.Pick("At 8 bits steps may appear in the sky (banding), though the flat areas could not be measured: send the file at 16 bits if the lab takes it.", "Pe 8 biti pot aparea trepte in cer (banding), desi zonele plate nu s-au putut masura: trimite fisierul pe 16 biti daca laboratorul il primeste."),
                    Tr.Pick($"the noise left in the sky is {Fmt.F2(r.Noise.SigmaMedianLsb)} levels, under {Fmt.F1(Thresholds.NoiseBandingLsb)}, where it no longer hides the steps", $"zgomotul ramas in cer e de {Fmt.F2(r.Noise.SigmaMedianLsb)} niveluri, sub {Fmt.F1(Thresholds.NoiseBandingLsb)}, unde nu mai ascunde treptele")));
        }

        // ------------------------------------------------------------ 2. background

        /// <summary>
        /// Section 2, the background and the paper. The verdict: the dust (the darkest extended
        /// area: the dust class or the darkest sky, ADR 0033) against paper black, and the sky
        /// against it (ADR 0025); then the sky against the visibility threshold (ADR 0029). The
        /// "because" says what the dust is, which are the darkest clouds, where the black is, from
        /// which Dmax the dust reaches black (and that a sky under the threshold passes on no
        /// paper, ADR 0043) and how much is under the threshold.
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="o">The list the one recommendation is added to.</param>
        static void Background(AnalysisResult r, List<Recommendation> o)
        {
            SimulationResult s = r.Simulation;
            SimulationRow asIs = s.AsIs;
            string pass = ReportWriter.DustDmaxText(s);
            string clouds = s.Clouds.Count == 0 ? "" : Tr.Pick("; the darkest clouds: ", "; norii cei mai intunecati: ") + CloudList(s);
            string note = s.MethodNote == null ? "" : "; " + s.MethodNote;
            string because = Tr.Pick(
                $"the dust at L* {Fmt.F1(asIs.LstarDust)} ({s.DustDescription}{note}){clouds}; "
                + $"paper black is L* {Fmt.F1(s.PaperBlackLstar)} at Dmax {Fmt.Dmax(s.Dmax)}; "
                + $"the sky at L* {Fmt.F1(asIs.LstarSky)} ({s.SkyDescription}); the background level {Fmt.Level(s.BackgroundMeasured)}; {pass}; "
                + $"{Fmt.Pct(asIs.PctBelowLowSeparation)} of the pixels are under L* {Fmt.F0(Thresholds.LowSeparationLstar)}, where the eye barely tells the tones apart",
                $"praful la L* {Fmt.F1(asIs.LstarDust)} ({s.DustDescription}{note}){clouds}; "
                + $"negrul hartiei e L* {Fmt.F1(s.PaperBlackLstar)} la Dmax {Fmt.Dmax(s.Dmax)}; "
                + $"cerul la L* {Fmt.F1(asIs.LstarSky)} ({s.SkyDescription}); nivelul fundalului {Fmt.Level(s.BackgroundMeasured)}; {pass}; "
                + $"{Fmt.Pct(asIs.PctBelowLowSeparation)} din pixeli sunt sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}, unde ochiul abia mai deosebeste tonurile");
            string threshold = "L* " + Fmt.F0(Thresholds.LowSeparationLstar);

            if (!s.DustBelowPaperBlack && !s.SkyBelowVisibility)
            {
                SimulationRow rec = s.Recommended;
                string text;
                if (s.RecommendedIsAsIs)
                {
                    // The target right under the measured level is the one that fails; it is what
                    // tells the user how close to the edge it is.
                    SimulationRow lower = s.Rows.LastOrDefault(x => x.Target < asIs.Target);
                    text = lower == null
                        ? Tr.Pick("The background is print-safe as it is; the simulation goes no lower than its level.", "Fundalul e print-safe asa cum e; simularea nu coboara sub nivelul lui.")
                        : !lower.DustOnPaperBlack
                            ? Tr.Pick($"The background is print-safe as it is; if you lowered it to {Fmt.Level(lower.Target)}, the dust would reach L* {Fmt.F1(lower.LstarDust)}, under paper black.", $"Fundalul e print-safe asa cum e; daca l-ai cobori la {Fmt.Level(lower.Target)}, praful ar ajunge la L* {Fmt.F1(lower.LstarDust)}, sub negrul hartiei.")
                            : Tr.Pick($"The background is print-safe as it is; if you lowered it to {Fmt.Level(lower.Target)}, the sky would reach L* {Fmt.F1(lower.LstarSky)}, under {threshold}, where the eye barely tells the tones apart.", $"Fundalul e print-safe asa cum e; daca l-ai cobori la {Fmt.Level(lower.Target)}, cerul ar ajunge la L* {Fmt.F1(lower.LstarSky)}, sub {threshold}, unde ochiul abia mai deosebeste tonurile.");
                }
                else
                {
                    bool floor = ReferenceEquals(rec, s.Rows[0]);
                    text = Tr.Pick($"The background is print-safe as it is; you can lower it down to {Fmt.Level(rec.Target)} (then the dust would be at L* {Fmt.F1(rec.LstarDust)}, the sky at L* {Fmt.F1(rec.LstarSky)})"
                                   + (floor ? "; the simulation goes no lower." : "."),
                                   $"Fundalul e print-safe asa cum e; il poti cobori pana la {Fmt.Level(rec.Target)} (atunci praful ar fi la L* {Fmt.F1(rec.LstarDust)}, cerul la L* {Fmt.F1(rec.LstarSky)})"
                                   + (floor ? "; simularea nu coboara mai jos." : "."));
                }
                o.Add(new Recommendation(Topic.Background, Verdict.Ok, text, because));
                return;
            }

            Verdict v = s.SkyBelowPaperBlack ? Verdict.Problem : Verdict.Attention;
            string what = s.SkyBelowPaperBlack
                ? Tr.Pick("The sky is darker than paper black: the sky and the dust would come out as the SAME black.", "Cerul e mai intunecat decat negrul hartiei: cerul si praful ar iesi cu ACELASI negru.")
                : s.DustBelowPaperBlack
                    ? Tr.Pick("The dust is darker than paper black: the dust clouds would come out pure black, without detail.", "Praful e mai intunecat decat negrul hartiei: norii de praf ar iesi negru pur, fara detalii.")
                    : Tr.Pick($"The sky is under {threshold}: on paper it is not black, but in ordinary room light it would look almost black.", $"Cerul e sub {threshold}: pe hartie nu e negru, dar in lumina obisnuita de camera ar parea aproape negru.");
            string fix;
            if (s.Recommended != null)
            {
                string exact = s.DustBelowPaperBlack
                    ? (s.ExactTarget.HasValue ? Tr.Pick($"; the dust reaches black exactly at {ReportWriter.TargetText(s.ExactTarget)}", $"; praful ajunge exact pe negru la {ReportWriter.TargetText(s.ExactTarget)}") : "")
                    : Tr.Pick($"; the sky reaches {threshold} from {ReportWriter.TargetText(s.VisibilityTarget)} up", $"; cerul ajunge la {threshold} de la {ReportWriter.TargetText(s.VisibilityTarget)} in sus");
                fix = Tr.Pick($" Redo the stretch from the linear data up to a background of {Fmt.Level(s.Recommended.Target)} (in the simulation: the dust at L* {Fmt.F1(s.Recommended.LstarDust)}, the sky at L* {Fmt.F1(s.Recommended.LstarSky)}{exact}).", $" Refa stretch-ul din datele liniare pana la fundalul {Fmt.Level(s.Recommended.Target)} (in simulare: praful la L* {Fmt.F1(s.Recommended.LstarDust)}, cerul la L* {Fmt.F1(s.Recommended.LstarSky)}{exact}).");
            }
            else if (!s.DustBelowPaperBlack || s.Rows.Any(x => x.DustOnPaperBlack))
            {
                // The dust reaches black on the grid, but the sky does not reach the threshold on
                // it: a sky much darker than the measured background, beside a bright nebula.
                string top = Fmt.Level(s.Rows.Where(x => !x.AsIs).Select(x => x.Target).DefaultIfEmpty(s.AsIs.Target).Max());
                fix = Tr.Pick($" No target of the simulation (up to {top}) brings the sky to {threshold}; it would take {ReportWriter.TargetText(s.VisibilityTarget)}.", $" Nicio tinta din simulare (pana la {top}) nu duce cerul la {threshold}; ar fi nevoie de {ReportWriter.TargetText(s.VisibilityTarget)}.");
            }
            else
            {
                string top = Fmt.Level(s.Rows.Where(x => !x.AsIs).Select(x => x.Target).DefaultIfEmpty(s.AsIs.Target).Max());
                // Appendix B lists the clouds by name only when the catalogue found some (a WCS).
                string appendixB = s.Clouds.Count > 0 ? Tr.Pick("their list is in Appendix B", "lista lor e in Anexa B") : Tr.Pick("see Appendix B", "vezi Anexa B");
                fix = s.ExactTarget.HasValue
                    ? Tr.Pick($" No target of the simulation (up to {top}) lifts the dust to paper black; it would take ~{Fmt.Level(s.ExactTarget.Value)}, too much for a night sky: accept that the darkest clouds come out black ({appendixB}) or choose another paper.", $" Nicio tinta din simulare (pana la {top}) nu ridica praful pana la negrul hartiei; ar fi nevoie de ~{Fmt.Level(s.ExactTarget.Value)}, prea mult pentru un cer de noapte: accepta ca norii cei mai intunecati sa iasa negri ({appendixB}) sau alege alta hartie.")
                    : Tr.Pick($" No target of the simulation (up to {top}) lifts the dust to paper black, not even at {Fmt.Level(Thresholds.ExactTargetHi)}: the dust is practically black; accept that the darkest clouds come out black ({appendixB}) or choose another paper.", $" Nicio tinta din simulare (pana la {top}) nu ridica praful pana la negrul hartiei, nici macar la {Fmt.Level(Thresholds.ExactTargetHi)}: praful e practic negru; accepta ca norii cei mai intunecati sa iasa negri ({appendixB}) sau alege alta hartie.");
            }
            o.Add(new Recommendation(Topic.Background, v, what + fix, because));
        }

        /// <summary>
        /// "LDN 42 (B78) L* 0.9, LDN 1769 (B275, B278) L* 3.1, ..." - at most
        /// <see cref="Thresholds.DustCloudReportRows"/> clouds, from the darkest.
        /// </summary>
        /// <param name="s">The simulation, with at least one cloud row.</param>
        /// <returns>The clouds with their L*, followed by ", ..." when some are left out.</returns>
        internal static string CloudList(SimulationResult s)
        {
            var rows = s.Clouds.Take(Thresholds.DustCloudReportRows).Select(c => $"{c.Name} L* {Fmt.F1(c.Lstar)}");
            return string.Join(", ", rows) + (s.Clouds.Count > Thresholds.DustCloudReportRows ? ", ..." : "");
        }

        // ------------------------------------------------------------ 3. colour

        /// <summary>
        /// Section 3, the background's colour and the frame's edges (the gradients): the cast (or that the image is
        /// mono, or that the background is neutral), one recommendation per flagged coloured edge
        /// region, and one note for all the flagged neutral ones (ADR 0018).
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="o">The list the recommendations are added to.</param>
        static void Colour(AnalysisResult r, List<Recommendation> o)
        {
            BackgroundResult b = r.Background;
            if (!b.ColorApplicable)
            {
                o.Add(new Recommendation(Topic.Colour, Verdict.Ok, Tr.Pick("A black-and-white image: it has no colour tint to measure.", "Imagine alb-negru: nu are o tenta de culoare de masurat."), Tr.Pick("a single channel", "un singur canal")));
            }
            else if (b.HasCast)
            {
                string kind = Math.Abs(b.GreenExcess) >= Math.Abs(b.RedMinusBlue)
                    ? (b.GreenExcess > 0 ? Tr.Pick("green", "verde") : "magenta")
                    : (b.RedMinusBlue > 0 ? Tr.Pick("warm", "calda") : Tr.Pick("cool", "rece"));
                o.Add(new Recommendation(Topic.Colour, Verdict.Attention,
                    Tr.Pick($"The background has a {kind} tint: neutralize it before printing (SCNR or BackgroundNeutralization). On paper the tint shows, even with no grey beside it to compare it with.", $"Fundalul are o tenta {kind}: neutralizeaz-o inainte de print (SCNR sau BackgroundNeutralization). Pe hartie tenta se vede, chiar fara un gri alaturi cu care s-o compari."),
                    CastBecause(b, true)));
            }
            else
            {
                o.Add(new Recommendation(Topic.Colour, Verdict.Ok, Tr.Pick("The background is neutral.", "Fundalul e neutru."), CastBecause(b, false)));
            }

            // The COLOURED deviations, each with its own recommendation: light pollution, the
            // horizon, a tinted gradient - things to correct on linear data. An edge flagged
            // with the same colour as a flagged corner beside it, and less than it, is that
            // corner's glow reaching along the edge (ADR 0058): one phenomenon, one line, the
            // edge named as the corner's extension and its figures in the "because".
            var folded = new Dictionary<EdgeRegion, EdgeRegion>();
            foreach (EdgeRegion corner in b.Regions.Where(x => x.Flagged && x.Coloured).Take(4))
                foreach (EdgeRegion edge in Adjacent(b.Regions, corner))
                    if (edge.Flagged && edge.Coloured && !folded.ContainsKey(edge)
                        && ColourOf(edge) == ColourOf(corner) && edge.MaxAbsDelta < corner.MaxAbsDelta)
                        folded[edge] = corner;
            foreach (EdgeRegion e in b.FlaggedRegions.Where(x => x.Coloured && !folded.ContainsKey(x)))
            {
                string deltas = $"R {Fmt.S1(e.Deltas[0])}, G {Fmt.S1(e.Deltas[1])}, B {Fmt.S1(e.Deltas[2])}";
                // A region UNDER the sky on every channel is a darkening, not a glow: dust at the
                // frame's edge, or vignetting and a flattening's edge left over. Its colour says
                // which channel is missing most; the advice is the eye's, not GraXpert's by
                // default. The exception is the bottom band, where the horizon sits (ADR 0014):
                // red over blue there is the horizon's signature, brighter or not.
                bool warm = ColourOf(e) == RegionColour.Warm, cool = ColourOf(e) == RegionColour.Cool;
                bool darker = e.Deltas.Max() < 0 && !(e.AtBottom && warm);
                var extensions = folded.Where(kv => kv.Value == e).Select(kv => kv.Key).ToList();
                string extended = extensions.Count == 0 ? "" : Tr.Pick(", reaching along the ", ", intins si pe ") + string.Join(Tr.Pick(" and the ", " si pe "), extensions.Select(x => x.Name));
                string extendedBecause = string.Concat(extensions.Select(x => $"; {x.Name}: R {Fmt.S1(x.Deltas[0])}, G {Fmt.S1(x.Deltas[1])}, B {Fmt.S1(x.Deltas[2])}"));
                string kind = darker
                    ? (warm ? Tr.Pick("darker and browner than the sky (the blue missing most)", "mai intunecat si mai brun decat cerul (albastrul lipseste cel mai mult)")
                        : cool ? Tr.Pick("darker and bluer than the sky (the red missing most)", "mai intunecat si mai albastru decat cerul (rosul lipseste cel mai mult)")
                        : Tr.Pick("darker than the sky, with a colour of its own", "mai intunecat decat cerul, cu o culoare a lui"))
                    : (warm ? (e.AtBottom ? Tr.Pick("warmer than the rest of the sky (glow from the horizon or light pollution)", "mai cald decat restul cerului (lumina de la orizont sau poluare luminoasa)") : Tr.Pick("warmer, reddish, than the rest of the sky", "mai cald, rosiatic, decat restul cerului"))
                        : cool ? Tr.Pick("cooler, bluish, than the rest of the sky", "mai rece, albastrui, decat restul cerului")
                        : ColourOf(e) == RegionColour.Greenish ? Tr.Pick("greener than the rest of the sky", "mai verzui decat restul cerului") : Tr.Pick("of another colour than the rest of the sky", "cu alta culoare decat restul cerului"));
                string advice = darker
                    ? Tr.Pick("It may be dust at the edge of the frame, or a difference in brightness left over (vignetting, the edge of the background correction): look at the edge yourself. A difference left over is corrected on the linear data (GraXpert); dust is part of the object.", "Poate fi praf la marginea cadrului, sau o diferenta de luminozitate ramasa (vinietare, marginea corectiei de fundal): uita-te la margine. O diferenta ramasa se corecteaza pe datele liniare (GraXpert); praful face parte din obiect.")
                    : Tr.Pick("Correct it on the linear data (GraXpert) or keep it as part of the composition; do not correct it with a local curve on the image already stretched.", "Corecteaz-o pe datele liniare (GraXpert) sau las-o ca parte a compozitiei; nu o corecta cu o curba locala pe imaginea deja intinsa.");
                o.Add(new Recommendation(Topic.Colour, Verdict.Attention,
                    $"{Cap(e.Name)}: {kind}{extended}. {advice}",
                    Tr.Pick($"against the rest of the sky, measured {(e.OnSkyPixels ? "on the zone's sky pixels" : $"on every pixel of the zone (only {Fmt.F0(e.SkyFraction * 100)} % of them are sky)")}: {deltas} levels (threshold {Fmt.F1(Thresholds.CornerGradientLevels)}); the channels do not change alike, so it is a colour{extendedBecause}",
                        $"fata de restul cerului, masurat {(e.OnSkyPixels ? "pe pixelii de cer ai zonei" : $"pe toti pixelii zonei (doar {Fmt.F0(e.SkyFraction * 100)} % din ei sunt cer)")}: {deltas} niveluri (prag {Fmt.F1(Thresholds.CornerGradientLevels)}); canalele nu se schimba la fel, deci e o culoare{extendedBecause}")));
            }

            // The NEUTRAL deviations, all in a single note: an edge brighter by the same amount on
            // every channel is most often the object itself (the Milky Way band entering the
            // corner), not a gradient. The tool cannot tell them apart; it says so and lets the
            // user look at the edges.
            var neutral = b.FlaggedRegions.Where(x => !x.Coloured).ToList();
            if (neutral.Count > 0)
            {
                string list = string.Join("; ", neutral.Select(e =>
                    $"{e.Name} {(b.ColorApplicable ? string.Join("/", e.Deltas.Select(Fmt.S1)) : Fmt.S1(e.Deltas[0]))}"));
                bool brighter = neutral.All(e => e.Deltas.Average() > 0);
                o.Add(new Recommendation(Topic.Colour, Verdict.Ok,
                    Tr.Pick((brighter ? "Some edges are brighter than the rest of the sky, but without a colour of their own: " : "Some edges differ from the rest of the sky, but without a colour of their own: ")
                            + "most likely it is the object itself (the Milky Way entering a corner, say), not a difference in the background; look at the edges before you correct anything.",
                            (brighter ? "Unele margini sunt mai luminoase decat restul cerului, dar fara o culoare a lor: " : "Unele margini difera de restul cerului, dar fara o culoare a lor: ")
                            + "cel mai probabil e chiar obiectul (de exemplu Calea Lactee intrand in colt), nu o diferenta de fundal; uita-te la margini inainte sa corectezi ceva."),
                    Tr.Pick($"against the rest of the sky, in levels: {list} (threshold {Fmt.F1(Thresholds.CornerGradientLevels)}); all the channels change alike, so it is not a colour", $"fata de restul cerului, in niveluri: {list} (prag {Fmt.F1(Thresholds.CornerGradientLevels)}); toate canalele se schimba la fel, deci nu e o culoare")));
            }
        }

        /// <summary>The colour of a region's departure, by its deltas alone (R-093).</summary>
        enum RegionColour
        {
            /// <summary>Red over blue by more than <see cref="Thresholds.CornerGradientLevels"/>.</summary>
            Warm,

            /// <summary>Blue over red by more than the same.</summary>
            Cool,

            /// <summary>Neither, and green over the mean of red and blue.</summary>
            Greenish,

            /// <summary>Neither, and green under that mean.</summary>
            Other,
        }

        /// <summary>The colour of a region's departure, used to name it and to match an edge with a corner.</summary>
        /// <param name="e">The region, on colour (three deltas).</param>
        /// <returns>Warm, Cool, Greenish or Other.</returns>
        static RegionColour ColourOf(EdgeRegion e)
        {
            double grad = Thresholds.CornerGradientLevels;
            return e.Deltas[0] > e.Deltas[2] + grad ? RegionColour.Warm
                : e.Deltas[2] > e.Deltas[0] + grad ? RegionColour.Cool
                : e.Deltas[1] > (e.Deltas[0] + e.Deltas[2]) / 2 ? RegionColour.Greenish : RegionColour.Other;
        }

        /// <summary>
        /// The two edges that touch a corner: the top or the bottom edge and the left or the
        /// right one. The regions come in the analyzer's order, the four corners (top-left,
        /// top-right, bottom-left, bottom-right) then the four edges (top, bottom, left, right);
        /// a list with a region left out (a frame too small for one) pairs nothing.
        /// </summary>
        /// <param name="regions">The eight regions, in the analyzer's order.</param>
        /// <param name="corner">One of the first four.</param>
        /// <returns>The two edges beside the corner, or none.</returns>
        static IEnumerable<EdgeRegion> Adjacent(IReadOnlyList<EdgeRegion> regions, EdgeRegion corner)
        {
            int i = -1;
            for (int k = 0; k < regions.Count; k++) if (regions[k] == corner) i = k;
            if (regions.Count != 8 || i < 0 || i > 3) yield break;
            yield return regions[i < 2 ? 4 : 5];          // the top edge for the upper corners, the bottom for the lower
            yield return regions[i % 2 == 0 ? 6 : 7];     // the left edge for the left corners, the right for the right
        }

        /// <summary>
        /// The evidence of the cast verdict: the sky's colour, its chroma in L*a*b* with a* and
        /// b*, the threshold, and the two level figures that name the direction.
        /// </summary>
        /// <param name="b">The background measurement, on colour.</param>
        /// <param name="attention">Whether the verdict is a cast (the text then names the threshold as passed).</param>
        /// <returns>The "because" text in the current language.</returns>
        static string CastBecause(BackgroundResult b, bool attention)
        {
            string ab = $"a* {Fmt.S1(b.Lab.A)}, b* {Fmt.S1(b.Lab.B)}";
            return Tr.Pick(
                $"the background is R {Fmt.F1(b.Levels[0])}, G {Fmt.F1(b.Levels[1])}, B {Fmt.F1(b.Levels[2])}; how far it is from a grey (chroma): {Fmt.F1(b.Chroma)}, threshold {Fmt.F1(Thresholds.CastWarnChroma)}{(attention ? ", passed" : "")} ({ab}); green stands over red and blue by {Fmt.S1(b.GreenExcess)} levels, red over blue by {Fmt.S1(b.RedMinusBlue)}",
                $"fundalul are R {Fmt.F1(b.Levels[0])}, G {Fmt.F1(b.Levels[1])}, B {Fmt.F1(b.Levels[2])}; cat de departe e de un gri (croma): {Fmt.F1(b.Chroma)}, prag {Fmt.F1(Thresholds.CastWarnChroma)}{(attention ? ", depasit" : "")} ({ab}); verdele iese peste rosu si albastru cu {Fmt.S1(b.GreenExcess)} niveluri, rosul peste albastru cu {Fmt.S1(b.RedMinusBlue)}");
        }

        /// <summary>
        /// A region's name with its first letter in upper case, to open a sentence.
        /// </summary>
        /// <param name="s">The name, not empty.</param>
        /// <returns>The name, capitalised.</returns>
        static string Cap(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ------------------------------------------------------------ 4. size

        /// <summary>
        /// Section 4, the print's size: without enough stars only the pixel grid, and no size;
        /// with a given distance the recommended size, or that the stars are discs at every
        /// size; without one, that the wall chooses when the stars are points, otherwise that they
        /// are discs at every size (ADR 0017); then the soft corners, the crop and the sizes over
        /// <see cref="Thresholds.OversizeCm"/>.
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="o">The list the recommendations are added to.</param>
        static void Size(AnalysisResult r, List<Recommendation> o)
        {
            StarResult st = r.Stars;
            PrintSizeResult ps = r.Size;
            SizeRow row = ps.ReferenceRow;
            if (st.Insufficient)
            {
                // Without a FWHM the detail is not measured, only whether the pixels show - a
                // limit of the file. It says how far the file reaches; no row is recommended.
                string acuity = Fmt.F2(Thresholds.AcuityMmAt1m);
                string rule = ps.DistanceIsExplicit
                    ? Tr.Pick($"from {Fmt.F1(row.ViewingDistanceM)} m the pixels no longer show from {Fmt.F0(ps.FallbackDpi)} dpi up", $"de la {Fmt.F1(row.ViewingDistanceM)} m pixelii nu se mai vad de la {Fmt.F0(ps.FallbackDpi)} dpi in sus")
                    : Tr.Pick($"without a given distance the convention of {Fmt.F0(ps.FallbackDpi)} dpi applies (the pixels no longer show from ~0.5 m, when you step closer)", $"fara distanta data se cere conventia de {Fmt.F0(ps.FallbackDpi)} dpi (pixelii nu se mai vad de la ~0.5 m, cand te apropii)");
                string reach = row.PointAtViewingDistance
                    ? Tr.Pick($"the file can be printed up to {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm ({Fmt.F0(row.Dpi)} dpi)", $"fisierul poate fi tiparit pana la {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm ({Fmt.F0(row.Dpi)} dpi)")
                    : Tr.Pick($"not even the smallest width of the list ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm, {Fmt.F0(row.Dpi)} dpi) gets to the threshold", $"nici cea mai mica latime din lista ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm, {Fmt.F0(row.Dpi)} dpi) nu ajunge la prag");
                string threshold = ps.DistanceIsExplicit
                    ? Tr.Pick($"the threshold is {Fmt.F0(ps.FallbackDpi)} dpi = 25.4 / ({acuity} mm/m x {Fmt.F1(row.ViewingDistanceM)} m), by how finely the eye sees from {Fmt.F1(row.ViewingDistanceM)} m", $"pragul e {Fmt.F0(ps.FallbackDpi)} dpi = 25.4 / ({acuity} mm/m x {Fmt.F1(row.ViewingDistanceM)} m), dupa cat de fin vede ochiul de la {Fmt.F1(row.ViewingDistanceM)} m")
                    : Tr.Pick($"the threshold is {Fmt.F0(ps.FallbackDpi)} dpi (convention: pixels of {Fmt.F2(25.4 / ps.FallbackDpi)} mm, which no longer show from {Fmt.F2(25.4 / ps.FallbackDpi / Thresholds.AcuityMmAt1m)} m)", $"pragul e {Fmt.F0(ps.FallbackDpi)} dpi (conventie: pixeli de {Fmt.F2(25.4 / ps.FallbackDpi)} mm, care nu se mai vad de la {Fmt.F2(25.4 / ps.FallbackDpi / Thresholds.AcuityMmAt1m)} m)");
                o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                    Tr.Pick($"Without stars it cannot be measured how much detail the image holds; it is only checked that the pixels do not show: {rule}, and {reach}. It is a limit of the file, not of the detail (a soft nebula may look fine even larger), so no size is recommended."
                            + (ps.DistanceIsExplicit ? "" : " Give the viewing distance, so that the threshold is the one for your distance, not the convention."),
                            $"Fara stele nu se poate masura cat detaliu are imaginea; se verifica doar sa nu se vada pixelii: {rule}, si {reach}. E o limita a fisierului, nu a detaliului (o nebuloasa moale poate arata bine si mai mare), asa ca nu se recomanda o marime."
                            + (ps.DistanceIsExplicit ? "" : " Da distanta de privire, ca pragul sa fie cel pentru distanta ta, nu conventia.")),
                    Tr.Pick($"only {Fmt.Int(st.Count)} stars measured, {Fmt.F1(st.DensityPerMp)} per megapixel (at least {Thresholds.StarMinCount} are needed, and {Fmt.F0(Thresholds.StarMinPerMegapixel)} per megapixel); at {Fmt.F0(row.WidthCm)} cm a pixel is {Fmt.F2(row.PitchMm)} mm and no longer shows from {Fmt.F2(row.PixelDistanceM)} m; {threshold}",
                            $"doar {Fmt.RoCount(Fmt.Int(st.Count))}stele masurate, {Fmt.F1(st.DensityPerMp)} pe megapixel (e nevoie de cel putin {Thresholds.StarMinCount}, si de {Fmt.F0(Thresholds.StarMinPerMegapixel)} pe megapixel); la {Fmt.F0(row.WidthCm)} cm un pixel are {Fmt.F2(row.PitchMm)} mm si nu se mai vede de la {Fmt.F2(row.PixelDistanceM)} m; {threshold}")));
            }
            else
            {
                // The stars' figures the two branches share: their width, how many fit side by
                // side across the frame, and how large one is on paper at the reference row.
                string stars = Tr.Pick(
                    $"the stars' width (FWHM) is {Fmt.F2(st.FwhmMedian)} px (a quarter of the stars under {Fmt.F2(st.FwhmQ1)}, a quarter over {Fmt.F2(st.FwhmQ3)}; {Fmt.Int(st.Count)} stars), so ~{Fmt.F0(ps.EffectiveDetailPx)} stars fit side by side across the width; at {Fmt.F0(row.WidthCm)} cm a star is {Fmt.F2(row.StarMm)} mm",
                    $"latimea stelelor (FWHM) e {Fmt.F2(st.FwhmMedian)} px (un sfert din stele sub {Fmt.F2(st.FwhmQ1)}, un sfert peste {Fmt.F2(st.FwhmQ3)}; {Fmt.RoCount(Fmt.Int(st.Count))}stele), deci pe latime incap ~{Fmt.RoCount(Fmt.F0(ps.EffectiveDetailPx))}stele una langa alta; la {Fmt.F0(row.WidthCm)} cm o stea are {Fmt.F2(row.StarMm)} mm");
                if (ps.DistanceIsExplicit)
                {
                    string evidence = stars + Tr.Pick($" and looks like a point from {Fmt.F2(row.MinDistanceM)} m; the given distance {Fmt.F1(row.ViewingDistanceM)} m",
                                                      $" si se vede ca punct de la {Fmt.F2(row.MinDistanceM)} m; distanta data {Fmt.F1(row.ViewingDistanceM)} m");
                    if (row.PointAtViewingDistance)
                        o.Add(new Recommendation(Topic.Size, Verdict.Ok,
                            Tr.Pick($"Recommended: a {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm image, ~{Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)} cm with the passepartout and the frame.", $"Recomandat: imaginea de {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm, ~{Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)} cm cu passepartout si rama."),
                            evidence));
                    else
                        o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                            Tr.Pick($"From {Fmt.F1(row.ViewingDistanceM)} m the stars look like discs, not points, at every size of the list; the smallest ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm) makes them least visible. Try smaller widths, or a longer distance if the viewer really stands further away.", $"De la {Fmt.F1(row.ViewingDistanceM)} m stelele se vad ca discuri, nu ca puncte, la orice marime din lista; cea mai mica ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm) le face cel mai putin vizibile. Incearca latimi mai mici, sau o distanta mai mare daca privitorul chiar sta mai departe."),
                            evidence));
                }
                else
                {
                    // Without a distance, the relative sharpness is the same at any size (see
                    // PrintSizeAnalyzer): the answer is yes/no for the whole image, and the wall
                    // chooses the size. It is said as such; no choice is pretended.
                    bool sharp = row.PointAtViewingDistance;
                    string tolerance = Fmt.F0((Thresholds.NaturalDistanceTolerance - 1) * 100);
                    string factor = Fmt.F1(Thresholds.ViewingDistanceFactor);
                    o.Add(new Recommendation(Topic.Size, sharp ? Verdict.Ok : Verdict.Attention,
                        sharp
                            ? Tr.Pick($"Without a viewing distance no size is recommended: up to {Fmt.F0(row.WidthCm)} cm (a {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm image, ~{Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)} cm with the passepartout and the frame) it looks equally sharp, because a larger print is viewed from farther away; choose the size by the wall. If it will be seen from close up (a hallway, an office), give the distance: then the program can choose a size.", $"Fara distanta de privire nu se recomanda o marime: pana la {Fmt.F0(row.WidthCm)} cm (imaginea de {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm, ~{Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)} cm cu passepartout si rama) arata la fel de clara, fiindca un print mai mare se priveste de mai departe; alege marimea dupa perete. Daca va fi privita de aproape (hol, birou), da distanta: atunci programul poate alege o marime.")
                            : Tr.Pick($"Seen from the usual distance for their size, the stars look like small discs at every size: the image is soft for the pixels it has. The smallest size ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm) makes them least visible. Give the viewing distance, so that it is judged on that.", $"Privite de la distanta obisnuita pentru marimea lor, stelele se vad ca mici discuri la orice marime: imaginea e moale fata de cati pixeli are. Cea mai mica marime ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm) le face cel mai putin vizibile. Da distanta de privire, ca judecata sa se faca pe ea."),
                        stars + Tr.Pick($" and looks like a point {(sharp ? "" : "only ")}from {Fmt.F2(row.MinDistanceM)} m, while the print is viewed from the usual distance {Fmt.F2(row.NaturalDistanceM)} m ({factor} x the diagonal; tolerance {tolerance} %); the ratio of the two distances is the same at every size",
                                        $" si se vede ca punct {(sharp ? "" : "abia ")}de la {Fmt.F2(row.MinDistanceM)} m, iar printul se priveste de la distanta obisnuita {Fmt.F2(row.NaturalDistanceM)} m ({factor} x diagonala; toleranta {tolerance} %); raportul dintre cele doua distante e acelasi la orice marime")));
                }
            }

            if (st.SoftCorners)
                o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                    Tr.Pick("The stars in the corners are wider than those in the centre: do not enlarge the whole frame to the maximum; consider a crop that leaves the corners out.", "Stelele din colturi sunt mai late decat cele din centru: nu mari cadrul intreg la maximum; gandeste-te la un decupaj care lasa colturile afara."),
                    Tr.Pick($"the stars' width (FWHM) in the weakest corner divided by the centre's: {Fmt.F2(st.CornerToCenterRatio)} (threshold {Fmt.F1(Thresholds.CornerSoftRatio)}); in the centre {Fmt.F2(st.Regions[0].FwhmMedian)} px", $"latimea stelelor (FWHM) in cel mai slab colt impartita la cea din centru: {Fmt.F2(st.CornerToCenterRatio)} (prag {Fmt.F1(Thresholds.CornerSoftRatio)}); in centru {Fmt.F2(st.Regions[0].FwhmMedian)} px")));

            if (r.Crop.Needed)
                o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                    Tr.Pick($"The proportion asked for the print needs a crop: it cuts {ReportWriter.CropCutText(r.Crop)} ({Fmt.Pct(r.Crop.CutPct)}); choose yourself what stays in the frame, not the lab.", $"Proportia ceruta pentru print cere un decupaj: se taie {ReportWriter.CropCutText(r.Crop)} ({Fmt.Pct(r.Crop.CutPct)}); alege tu ce ramane in cadru, nu laboratorul."),
                    Tr.Pick($"the image's proportion {Fmt.F3(r.Crop.NativeAspect)}, the one asked {Fmt.F3(r.Crop.TargetAspect.Value)}; {Fmt.Dim(r.Crop.W, r.Crop.H)} px remain", $"proportia imaginii {Fmt.F3(r.Crop.NativeAspect)}, cea ceruta {Fmt.F3(r.Crop.TargetAspect.Value)}; raman {Fmt.Dim(r.Crop.W, r.Crop.H)} px")));

            if (ps.Rows.Any(x => x.Oversize && x.PointAtViewingDistance))
                o.Add(new Recommendation(Topic.Size, Verdict.Ok,
                    Tr.Pick($"Over {Fmt.F0(Thresholds.OversizeCm)} cm the limit is not the file but the glass, the weight and the transport.", $"Peste {Fmt.F0(Thresholds.OversizeCm)} cm nu fisierul e limita, ci sticla, greutatea si transportul."),
                    Tr.Pick($"the sizes over {Fmt.F0(Thresholds.OversizeCm)} cm have enough detail, but are not recommended", $"marimile peste {Fmt.F0(Thresholds.OversizeCm)} cm au destul detaliu, dar nu se recomanda")));
        }

        // ------------------------------------------------------------ 5. denoise

        /// <summary>
        /// Section 5, the denoise for the print version: too little smooth sky, or the noise
        /// against the print target (pressed too hard, visible on screen only, right, or
        /// acceptable), with the noise after the recommended re-stretch in the "because".
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="o">The list the one recommendation is added to.</param>
        static void Denoise(AnalysisResult r, List<Recommendation> o)
        {
            NoiseResult n = r.Noise;
            if (n.Insufficient)
            {
                o.Add(new Recommendation(Topic.Denoise, Verdict.Attention,
                    Tr.Pick("Too little smooth sky to measure the noise left.", "Prea putin cer neted ca sa se poata masura zgomotul ramas."),
                    Tr.Pick($"only {Fmt.Int(n.SmoothTiles)} small areas of smooth sky of {Fmt.Int(n.Candidates)} tried (at least {Thresholds.NoiseMinTiles} are needed)", $"doar {Fmt.RoCount(Fmt.Int(n.SmoothTiles))}zone mici de cer neted din {Fmt.Int(n.Candidates)} incercate (e nevoie de cel putin {Thresholds.NoiseMinTiles})")));
                return;
            }

            string target = Tr.Pick($"the target for print: {Fmt.F1(Thresholds.NoisePrintLoLsb)}-{Fmt.F1(Thresholds.NoisePrintHiLsb)} levels", $"tinta pentru print: {Fmt.F1(Thresholds.NoisePrintLoLsb)}-{Fmt.F1(Thresholds.NoisePrintHiLsb)} niveluri");
            string after = "";
            SimulationRow row = r.Simulation.Recommended;
            if (row != null && !row.AsIs)
            {
                // The gamma curve's slope at the background: what the noise is multiplied by when
                // the background rises (or falls: the recommendation goes both ways, ADR 0025).
                double bg = r.Simulation.BackgroundMeasured;
                double slope = row.Gamma * Math.Pow(bg, row.Gamma - 1);
                after = Tr.Pick($"; after you redo the stretch to {Fmt.Level(row.Target)}, the noise {(slope >= 1 ? "grows" : "shrinks")} by ~{Fmt.F2(slope)} times (how steeply the curve climbs at the background), to ~{Fmt.F2(n.SigmaMedianLsb * slope)} levels", $"; dupa ce refaci stretch-ul la {Fmt.Level(row.Target)}, zgomotul {(slope >= 1 ? "creste" : "scade")} de ~{Fmt.F2(slope)} ori (cat de tare urca curba in dreptul fundalului), la ~{Fmt.F2(n.SigmaMedianLsb * slope)} niveluri");
            }
            // The share of the smooth tiles under the banding sigma is always told: an AI denoiser
            // can leave the median where it should be and still wipe a tail of tiles, where the
            // 8-bit plateau of section 1 is then measured.
            string because = Tr.Pick($"the noise left in the sky is {Fmt.F2(n.SigmaMedianLsb)} levels (sigma; the middle value over {Fmt.Int(n.SmoothTiles)} small areas of smooth sky; in the smoothest 10 % under {Fmt.F2(n.Sigma10thLsb)}, the lowest {Fmt.F2(n.SigmaMinLsb)}); {Fmt.Pct(n.PctTilesBelowBanding)} of the areas are under {Fmt.F1(Thresholds.NoiseBandingLsb)} levels, where the noise no longer hides the steps; {target}{after}",
                $"zgomotul ramas in cer e de {Fmt.F2(n.SigmaMedianLsb)} niveluri (sigma; valoarea din mijloc pe {Fmt.RoCount(Fmt.Int(n.SmoothTiles))}zone mici de cer neted; in cele mai netede 10 % sub {Fmt.F2(n.Sigma10thLsb)}, cel mai mic {Fmt.F2(n.SigmaMinLsb)}); {Fmt.Pct(n.PctTilesBelowBanding)} din zone sunt sub {Fmt.F1(Thresholds.NoiseBandingLsb)} niveluri, unde zgomotul nu mai ascunde treptele; {target}{after}");

            if (n.BandingRisk)
                o.Add(new Recommendation(Topic.Denoise, Verdict.Attention,
                    Tr.Pick("Denoise is too strong for print: too little noise is left to hide the steps between levels (banding). For the print version, a weaker denoise keeps the sky's texture and the faint stars.", "Denoise-ul e prea puternic pentru print: a ramas prea putin zgomot ca sa ascunda treptele dintre niveluri (banding). Pentru versiunea de print, un denoise mai slab pastreaza textura cerului si stelele slabe."),
                    because));
            else if (n.UnderDenoised)
                o.Add(new Recommendation(Topic.Denoise, Verdict.Attention,
                    Tr.Pick("The noise shows on screen at 100 %; on paper, at the sizes of the table, it does not. Denoise only if you also want a clean version for the screen.", "Zgomotul se vede pe ecran la 100 %; pe hartie, la marimile din tabel, nu se vede. Fa denoise doar daca vrei si o versiune curata pentru ecran."),
                    because + Tr.Pick($"; over {Fmt.F1(Thresholds.NoiseUnderDenoisedLsb)} levels the noise shows on screen", $"; peste {Fmt.F1(Thresholds.NoiseUnderDenoisedLsb)} niveluri zgomotul se vede pe ecran")));
            else if (n.GoodForPrint)
                o.Add(new Recommendation(Topic.Denoise, Verdict.Ok, Tr.Pick("The noise left is exactly what print needs.", "Zgomotul ramas e exact cat trebuie pentru print."), because));
            else
                o.Add(new Recommendation(Topic.Denoise, Verdict.Ok, Tr.Pick("The noise left is acceptable for print.", "Zgomotul ramas e acceptabil pentru print."), because));
        }

        // ------------------------------------------------------------ 6. warnings

        /// <summary>
        /// Section 6, the warnings: an 8-bit file in disguise, no ICC profile or one that is not
        /// sRGB, and the reader's note (the 16-bit master to keep is a fact of the Export tab,
        /// ADR 0061).
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="o">The list the recommendations are added to.</param>
        static void Warnings(AnalysisResult r, List<Recommendation> o)
        {
            FileFactsResult f = r.File;
            BitDepthResult b = r.BitDepth;

            if (b.IsUpscaled)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Attention,
                    Tr.Pick("The file holds only 8 bits of information, saved as 16 bits: you lose nothing going to 8 bits, but you gain nothing from the 16 either.", "Fisierul are doar 8 biti de informatie, salvati ca 16 biti: nu pierzi nimic la trecerea pe 8 biti, dar nici nu castigi ceva din cei 16."),
                    Tr.Pick($"{Fmt.Pct(b.UpscaledFraction * 100)} of the values are an 8-bit level copied into 16 bits (the value = the level x 257; threshold {Fmt.F0(Thresholds.UpscaledFraction * 100)} %); different levels used on each channel: {string.Join(" / ", b.DistinctLevels.Select(n => Fmt.Int(n)))}", $"{Fmt.Pct(b.UpscaledFraction * 100)} din valori sunt un nivel de 8 biti copiat in 16 biti (valoarea = nivelul x 257; prag {Fmt.F0(Thresholds.UpscaledFraction * 100)} %); niveluri diferite folosite pe fiecare canal: {string.Join(" / ", b.DistinctLevels.Select(n => Fmt.Int(n)))}")));

            if (!f.HasIcc)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Attention,
                    Tr.Pick("The file has no colour profile (ICC): the lab will assume sRGB. Export with the sRGB profile included in the file, so that nobody assumes anything.", "Fisierul nu are profil de culoare (ICC): laboratorul va presupune sRGB. Exporta cu profilul sRGB inclus in fisier, ca sa nu presupuna nimeni nimic."),
                    Tr.Pick("the file has no profile; the program measured assuming sRGB", "fisierul nu are niciun profil; programul a masurat presupunand sRGB")));
            else if (f.IccLooksSrgb == false)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Attention,
                    Tr.Pick($"The file's colour profile is not sRGB ({f.IccDescription}): tell the lab, and keep in mind that the program's L* measurements assume sRGB.", $"Profilul de culoare al fisierului nu e sRGB ({f.IccDescription}): spune-i laboratorului, si tine minte ca masuratorile de L* din program presupun sRGB."),
                    Tr.Pick("the profile's name does not contain 'sRGB'", "numele profilului nu contine 'sRGB'")));

            // Keeping the 16-bit master is a fact of the Export tab's stress test, not a warning
            // here (ADR 0061).

            if (f.ReaderNote != null)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Ok, Tr.Pick("Note from reading the file: ", "Observatie de la citirea fisierului: ") + f.ReaderNote + ".", Tr.Pick("an observation made while reading the file", "observatie la citirea fisierului")));
        }
    }
}
