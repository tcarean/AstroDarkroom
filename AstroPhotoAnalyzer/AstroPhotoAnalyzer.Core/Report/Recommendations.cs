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
        /// bits), JPEG q100 when the lab accepts only JPEG and the advice not to go below it, and
        /// the banding risk, from the plateau at the reference row's dpi or from the noise alone.
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
                    Tr.Pick($"Send an 8-bit LZW TIFF ({Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}). The 16 -> 8 bit conversion is pure rounding, not a loss.", $"Trimite TIFF 8-bit LZW ({Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}). Conversia 16 -> 8 biti e rotunjire pura, nu pierdere."),
                    Tr.Pick($"mean rounding error {Fmt.F2(q.RoundingMeanAbs)}, max {Fmt.F2(q.RoundingMax)} levels (half a level is the most possible)", $"eroare de rotunjire medie {Fmt.F2(q.RoundingMeanAbs)}, maxima {Fmt.F2(q.RoundingMax)} niveluri (jumatate de nivel e maximul posibil)")));
            else
                o.Add(new Recommendation(Topic.Format, Verdict.Ok,
                    Tr.Pick($"Send an 8-bit LZW TIFF ({Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}). The source is already 8-bit; there is nothing to convert.", $"Trimite TIFF 8-bit LZW ({Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}). Sursa e deja pe 8 biti; nu e nimic de convertit."),
                    Tr.Pick($"the source has {r.File.BitDepth} bits per sample", $"sursa are {r.File.BitDepth} biti pe esantion")));

            if (q100 != null)
            {
                Verdict v = q100.Acceptable ? (q100.PctOverThreshold > Thresholds.JpegOverThresholdWarnPct ? Verdict.Attention : Verdict.Ok) : Verdict.Attention;
                o.Add(new Recommendation(Topic.Format, v,
                    Tr.Pick($"If the lab accepts only JPEG: q100, 4:4:4 ({Fmt.Mb(q100.Megabytes)}).", $"Daca laboratorul accepta doar JPEG: q100, 4:4:4 ({Fmt.Mb(q100.Megabytes)})."),
                    Tr.Pick($"mean error {Fmt.F2(q100.MeanAbsError)}, max {Fmt.F1(q100.MaxError)} levels; {Fmt.Pct(q100.PctOverThreshold)} of the samples with an error > {Fmt.F0(Thresholds.JpegErrorCountLevels)}, at a mean level of {Fmt.F0(q100.MeanLevelOverThreshold)}", $"eroare medie {Fmt.F2(q100.MeanAbsError)}, maxima {Fmt.F1(q100.MaxError)} niveluri; {Fmt.Pct(q100.PctOverThreshold)} din esantioane cu eroare > {Fmt.F0(Thresholds.JpegErrorCountLevels)}, la nivelul mediu {Fmt.F0(q100.MeanLevelOverThreshold)}")
                    + (q100.PctOverThreshold > 0 && q100.MeanLevelOverThreshold < Thresholds.DarkLevelB ? Tr.Pick(" (in the dark sky, where JPEG does worst)", " (in cerul intunecat, unde JPEG-ul lucreaza cel mai prost)") : "")));

                JpegRow worst = r.Jpeg.Rows.Where(x => x.Quality < 100 && !x.Acceptable).OrderByDescending(x => x.Quality).FirstOrDefault();
                if (worst != null)
                    o.Add(new Recommendation(Topic.Format, Verdict.Attention,
                        Tr.Pick("Do not go below q100.", $"Nu cobori sub q100."),
                        Tr.Pick($"q{worst.Quality} has a max error of {Fmt.F1(worst.MaxError)} levels (threshold {Fmt.F0(Thresholds.JpegMaxErrorReject)}), on the stars and on the edges", $"q{worst.Quality} are eroare maxima {Fmt.F1(worst.MaxError)} niveluri (prag {Fmt.F0(Thresholds.JpegMaxErrorReject)}), pe stele si pe muchii")));
            }

            if (q.PlateauKnown && size != null)
            {
                double mm = q.PlateauMmAt(size.Dpi);
                // Where the plateau was measured: on an AI-denoised frame the only tiles clean
                // enough are the patches the denoiser wiped, a few tenths of a percent of the
                // frame at a sigma near zero, while the noise section's median says the sky is
                // fine. The reader must see that the two speak of different places.
                string where = Tr.Pick($"; on {q.PlateauTiles} clean tiles, {Fmt.Pct(q.PlateauFramePct)} of the frame, where the noise left is sigma {Fmt.F2(q.PlateauSigmaLsb)} LSB",
                    $"; pe {q.PlateauTiles} dale curate, {Fmt.Pct(q.PlateauFramePct)} din cadru, unde zgomotul ramas e sigma {Fmt.F2(q.PlateauSigmaLsb)} LSB");
                if (q.PlateauVisibleAt(size.Dpi))
                    o.Add(new Recommendation(Topic.Format, Verdict.Attention,
                        Tr.Pick("Banding risk at 8 bits: send 16-bit if the lab accepts it, or add dither at the export.", "Risc de banding pe 8 biti: trimite 16-bit daca laboratorul accepta, sau adauga dither la export."),
                        Tr.Pick($"a plateau of {Fmt.F1(q.PlateauPx)} px = {Fmt.F2(mm)} mm at {Fmt.F0(size.Dpi)} dpi, over {Fmt.F1(Thresholds.PlateauVisibleMm)} mm", $"palier de {Fmt.F1(q.PlateauPx)} px = {Fmt.F2(mm)} mm la {Fmt.F0(size.Dpi)} dpi, peste {Fmt.F1(Thresholds.PlateauVisibleMm)} mm") + where));
                else
                    o.Add(new Recommendation(Topic.Format, Verdict.Ok,
                        Tr.Pick("No visible banding at 8 bits: the residual noise dithers the quantization.", "Fara banding vizibil pe 8 biti: zgomotul rezidual dither-eaza cuantizarea."),
                        Tr.Pick($"a plateau of {Fmt.F1(q.PlateauPx)} px = {Fmt.F2(mm)} mm at {Fmt.F0(size.Dpi)} dpi, under {Fmt.F1(Thresholds.PlateauVisibleMm)} mm", $"palier de {Fmt.F1(q.PlateauPx)} px = {Fmt.F2(mm)} mm la {Fmt.F0(size.Dpi)} dpi, sub {Fmt.F1(Thresholds.PlateauVisibleMm)} mm") + where));
            }
            else if (r.Noise.BandingRisk)
                o.Add(new Recommendation(Topic.Format, Verdict.Attention,
                    Tr.Pick("Banding risk at 8 bits (the plateau could not be measured): send 16-bit if the lab accepts it.", "Risc de banding pe 8 biti (nu s-a putut masura palierul): trimite 16-bit daca laboratorul accepta."),
                    Tr.Pick($"residual noise sigma {Fmt.F2(r.Noise.SigmaMedianLsb)} LSB, under {Fmt.F1(Thresholds.NoiseBandingLsb)}", $"zgomot rezidual sigma {Fmt.F2(r.Noise.SigmaMedianLsb)} LSB, sub {Fmt.F1(Thresholds.NoiseBandingLsb)}")));
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
            string clouds = s.Clouds.Count == 0 ? "" : Tr.Pick("; the darkest: ", "; cei mai intunecati: ") + CloudList(s);
            string note = s.MethodNote == null ? "" : "; " + s.MethodNote;
            string because = Tr.Pick(
                $"the dust ({s.DustDescription}{note}) at L* {Fmt.F1(asIs.LstarDust)}{clouds}; "
                + $"paper black L* {Fmt.F1(s.PaperBlackLstar)} at Dmax {Fmt.Dmax(s.Dmax)}; "
                + $"the sky ({s.SkyDescription}) at L* {Fmt.F1(asIs.LstarSky)}; the background level {Fmt.Level(s.BackgroundMeasured)}; {pass}; "
                + $"the visibility threshold L* {Fmt.F0(Thresholds.LowSeparationLstar)}, under it {Fmt.Pct(asIs.PctBelowLowSeparation)} of the pixels",
                $"praful ({s.DustDescription}{note}) la L* {Fmt.F1(asIs.LstarDust)}{clouds}; "
                + $"negrul hartiei L* {Fmt.F1(s.PaperBlackLstar)} la Dmax {Fmt.Dmax(s.Dmax)}; "
                + $"cerul ({s.SkyDescription}) la L* {Fmt.F1(asIs.LstarSky)}; nivelul fundalului {Fmt.Level(s.BackgroundMeasured)}; {pass}; "
                + $"pragul de vizibilitate L* {Fmt.F0(Thresholds.LowSeparationLstar)}, sub el {Fmt.Pct(asIs.PctBelowLowSeparation)} din pixeli");
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
                        ? Tr.Pick("The background is print-safe as it is; the grid goes no lower than its level.", "Fundalul e print-safe asa cum e; grila nu coboara sub nivelul lui.")
                        : !lower.DustOnPaperBlack
                            ? Tr.Pick($"The background is print-safe as it is; at {Fmt.Level(lower.Target)} the dust would drop to L* {Fmt.F1(lower.LstarDust)}, under paper black.", $"Fundalul e print-safe asa cum e; la {Fmt.Level(lower.Target)} praful ar cobori la L* {Fmt.F1(lower.LstarDust)}, sub negrul hartiei.")
                            : Tr.Pick($"The background is print-safe as it is; at {Fmt.Level(lower.Target)} the sky would drop to L* {Fmt.F1(lower.LstarSky)}, under the visibility threshold {threshold}.", $"Fundalul e print-safe asa cum e; la {Fmt.Level(lower.Target)} cerul ar cobori la L* {Fmt.F1(lower.LstarSky)}, sub pragul de vizibilitate {threshold}.");
                }
                else
                {
                    bool floor = ReferenceEquals(rec, s.Rows[0]);
                    text = Tr.Pick($"The background is print-safe as it is; you can go down to {Fmt.Level(rec.Target)} (the dust at L* {Fmt.F1(rec.LstarDust)}, the sky at L* {Fmt.F1(rec.LstarSky)})"
                                   + (floor ? "; the grid goes no lower." : "."),
                                   $"Fundalul e print-safe asa cum e; poti cobori pana la {Fmt.Level(rec.Target)} (praful la L* {Fmt.F1(rec.LstarDust)}, cerul la L* {Fmt.F1(rec.LstarSky)})"
                                   + (floor ? "; grila nu coboara mai jos." : "."));
                }
                o.Add(new Recommendation(Topic.Background, Verdict.Ok, text, because));
                return;
            }

            Verdict v = s.SkyBelowPaperBlack ? Verdict.Problem : Verdict.Attention;
            string what = s.SkyBelowPaperBlack
                ? Tr.Pick("The sky is under paper black: the sky and the dust would print as the SAME black.", "Cerul e sub negrul hartiei: cerul si praful s-ar tipari cu ACELASI negru.")
                : s.DustBelowPaperBlack
                    ? Tr.Pick("The dust is under paper black: the dust clouds would print pure black.", "Praful e sub negrul hartiei: norii de praf s-ar tipari negru pur.")
                    : Tr.Pick($"The sky is under {threshold}: the paper keeps it apart from black, but in room light it would look almost black.", $"Cerul e sub {threshold}: hartia il separa de negru, dar in lumina de camera s-ar vedea aproape negru.");
            string fix;
            if (s.Recommended != null)
            {
                string exact = s.DustBelowPaperBlack
                    ? (s.ExactTarget.HasValue ? Tr.Pick($"; exactly on black at {ReportWriter.TargetText(s.ExactTarget)}", $"; exact pe negru la {ReportWriter.TargetText(s.ExactTarget)}") : "")
                    : Tr.Pick($"; the sky at {threshold} from {ReportWriter.TargetText(s.VisibilityTarget)}", $"; cerul la {threshold} de la {ReportWriter.TargetText(s.VisibilityTarget)}");
                fix = Tr.Pick($" Re-stretch from linear to a background of {Fmt.Level(s.Recommended.Target)} (simulated: the dust at L* {Fmt.F1(s.Recommended.LstarDust)}, the sky at L* {Fmt.F1(s.Recommended.LstarSky)}{exact}).", $" Re-stretch din liniar la fundal {Fmt.Level(s.Recommended.Target)} (simulat: praful la L* {Fmt.F1(s.Recommended.LstarDust)}, cerul la L* {Fmt.F1(s.Recommended.LstarSky)}{exact}).");
            }
            else if (!s.DustBelowPaperBlack || s.Rows.Any(x => x.DustOnPaperBlack))
            {
                // The dust reaches black on the grid, but the sky does not reach the threshold on
                // it: a sky much darker than the measured background, beside a bright nebula.
                string top = Fmt.Level(s.Rows.Where(x => !x.AsIs).Select(x => x.Target).DefaultIfEmpty(s.AsIs.Target).Max());
                fix = Tr.Pick($" No target up to {top} brings the sky to {threshold}; it would take {ReportWriter.TargetText(s.VisibilityTarget)}.", $" Nicio tinta pana la {top} nu aduce cerul la {threshold}; ar trebui {ReportWriter.TargetText(s.VisibilityTarget)}.");
            }
            else
            {
                string top = Fmt.Level(s.Rows.Where(x => !x.AsIs).Select(x => x.Target).DefaultIfEmpty(s.AsIs.Target).Max());
                fix = s.ExactTarget.HasValue
                    ? Tr.Pick($" No target up to {top} brings the dust onto paper black; it would take ~{Fmt.Level(s.ExactTarget.Value)}, too high for a night sky: accept the darkest clouds as black (Appendix B) or choose another medium.", $" Nicio tinta pana la {top} nu aduce praful pe negrul hartiei; ar trebui ~{Fmt.Level(s.ExactTarget.Value)}, prea sus pentru un cer de noapte: accepta norii cei mai intunecati negri (Anexa B) sau alege alt mediu.")
                    : Tr.Pick($" No target up to {top} brings the dust onto paper black, not even {Fmt.Level(Thresholds.ExactTargetHi)}: the dust is practically black; accept the darkest clouds as black (Appendix B) or choose another medium.", $" Nicio tinta pana la {top} nu aduce praful pe negrul hartiei, nici {Fmt.Level(Thresholds.ExactTargetHi)}: praful e practic negru; accepta norii cei mai intunecati negri (Anexa B) sau alege alt mediu.");
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
        /// Section 3, the background's colour and the gradients: the cast (or that the image is
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
                o.Add(new Recommendation(Topic.Colour, Verdict.Ok, Tr.Pick("A mono image: there is no cast to measure.", "Imagine mono: nu exista tenta de masurat."), Tr.Pick("a single plane", "un singur plan")));
            }
            else if (b.HasCast)
            {
                string kind = Math.Abs(b.GreenExcess) >= Math.Abs(b.RedMinusBlue)
                    ? (b.GreenExcess > 0 ? Tr.Pick("green", "verde") : "magenta")
                    : (b.RedMinusBlue > 0 ? Tr.Pick("warm", "calda") : Tr.Pick("cool", "rece"));
                o.Add(new Recommendation(Topic.Colour, Verdict.Attention,
                    Tr.Pick($"A {kind} cast on the background: neutralize it (SCNR / BackgroundNeutralization) before printing - on paper, with no grey beside it, it still reads.", $"Tenta {kind} pe fundal: neutralizeaza (SCNR / BackgroundNeutralization) inainte de print - pe hartie, fara un gri alaturi, tot se citeste."),
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
                    : (warm ? (e.AtBottom ? Tr.Pick("warm (horizon glow / light pollution)", "cald (lumina de orizont / poluare)") : Tr.Pick("warm (reddish)", "cald (rosiatic)"))
                        : cool ? Tr.Pick("cool (bluish)", "rece (albastrui)")
                        : ColourOf(e) == RegionColour.Greenish ? Tr.Pick("greenish", "verzui") : Tr.Pick("of another colour than the background", "cu alta culoare decat fundalul"));
                string advice = darker
                    ? Tr.Pick("Dust at the edge of the frame, or a gradient left over (vignetting, the edge of the flattening): check in Preview; a gradient is corrected on the linear data (GraXpert), dust is the object.", "Praf la marginea cadrului, sau un rest de gradient (vinietare, marginea aplatizarii): verifica in Preview; un gradient se corecteaza pe liniar (GraXpert), praful e obiectul.")
                    : Tr.Pick("Correct it on the linear data (GraXpert) or accept it as part of the composition - not with a local curve on the stretched image.", "Corecteaza pe liniar (GraXpert) sau accepta ca element de compozitie - nu cu o curba locala pe imaginea stretch-uita.");
                o.Add(new Recommendation(Topic.Colour, Verdict.Attention,
                    $"{Cap(e.Name)}: {kind}{extended}. {advice}",
                    Tr.Pick($"against the background, {(e.OnSkyPixels ? "on the region's sky pixels" : $"on every pixel of the region (sky {Fmt.F0(e.SkyFraction * 100)} %)")}: {deltas} levels (threshold {Fmt.F1(Thresholds.CornerGradientLevels)}); the channels move differently{extendedBecause}",
                        $"fata de fundal, {(e.OnSkyPixels ? "pe pixelii de cer ai regiunii" : $"pe toti pixelii regiunii (cer {Fmt.F0(e.SkyFraction * 100)} %)")}: {deltas} niveluri (prag {Fmt.F1(Thresholds.CornerGradientLevels)}); canalele se misca diferit{extendedBecause}")));
            }

            // The NEUTRAL deviations, all in a single note: an edge brighter by the same amount on
            // every channel is most often the object itself (the Milky Way band entering the
            // corner), not a gradient. The tool cannot tell them apart; it says so and lets the
            // user look in Preview.
            var neutral = b.FlaggedRegions.Where(x => !x.Coloured).ToList();
            if (neutral.Count > 0)
            {
                string list = string.Join("; ", neutral.Select(e =>
                    $"{e.Name} {(b.ColorApplicable ? string.Join("/", e.Deltas.Select(Fmt.S1)) : Fmt.S1(e.Deltas[0]))}"));
                bool brighter = neutral.All(e => e.Deltas.Average() > 0);
                o.Add(new Recommendation(Topic.Colour, Verdict.Ok,
                    Tr.Pick((brighter ? "Edges brighter than the background, without a cast: " : "Edges that differ from the background, without a cast: ")
                            + "most likely the object itself, not a gradient - check in Preview before you correct anything.",
                            (brighter ? "Margini mai luminoase decat fundalul, fara tenta: " : "Margini care difera de fundal, fara tenta: ")
                            + "cel mai probabil obiectul insusi, nu un gradient - verifica in Preview inainte sa corectezi ceva."),
                    Tr.Pick($"against the background, in levels: {list} (threshold {Fmt.F1(Thresholds.CornerGradientLevels)}); the channels move together", $"fata de fundal, in niveluri: {list} (prag {Fmt.F1(Thresholds.CornerGradientLevels)}); canalele se misca impreuna")));
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
            string chroma = $"{Fmt.F1(b.Chroma)} (a* {Fmt.S1(b.Lab.A)}, b* {Fmt.S1(b.Lab.B)}";
            return Tr.Pick(
                $"background R {Fmt.F1(b.Levels[0])} G {Fmt.F1(b.Levels[1])} B {Fmt.F1(b.Levels[2])}: chroma {chroma}; threshold {Fmt.F1(Thresholds.CastWarnChroma)}{(attention ? ", passed" : "")}); G - (R+B)/2 = {Fmt.S1(b.GreenExcess)}, R - B = {Fmt.S1(b.RedMinusBlue)} levels",
                $"fundal R {Fmt.F1(b.Levels[0])} G {Fmt.F1(b.Levels[1])} B {Fmt.F1(b.Levels[2])}: croma {chroma}; prag {Fmt.F1(Thresholds.CastWarnChroma)}{(attention ? ", depasit" : "")}); G - (R+B)/2 = {Fmt.S1(b.GreenExcess)}, R - B = {Fmt.S1(b.RedMinusBlue)} niveluri");
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
            string dist = ps.DistanceIsExplicit ? Tr.Pick($"the given distance {Fmt.F1(row.ViewingDistanceM)} m", $"distanta data {Fmt.F1(row.ViewingDistanceM)} m") : Tr.Pick($"the natural distance {Fmt.F2(row.NaturalDistanceM)} m (1.5 x the diagonal)", $"distanta naturala {Fmt.F2(row.NaturalDistanceM)} m (1.5 x diagonala)");

            if (st.Insufficient)
            {
                // Without a FWHM the detail is not measured, only the pixel grid - a limit of the
                // file. It says how far the file reaches; no row is recommended.
                string rule = ps.DistanceIsExplicit
                    ? Tr.Pick($"from {Fmt.F1(row.ViewingDistanceM)} m the grid disappears from {Fmt.F0(ps.FallbackDpi)} dpi up", $"de la {Fmt.F1(row.ViewingDistanceM)} m grila dispare de la {Fmt.F0(ps.FallbackDpi)} dpi in sus")
                    : Tr.Pick($"without a given distance the convention of {Fmt.F0(ps.FallbackDpi)} dpi applies (the grid invisible from ~0.5 m, when you step closer)", $"fara distanta data se cere conventia de {Fmt.F0(ps.FallbackDpi)} dpi (grila invizibila de la ~0.5 m, cand te apropii)");
                string reach = row.PointAtViewingDistance
                    ? Tr.Pick($"the file reaches {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm ({Fmt.F0(row.Dpi)} dpi)", $"fisierul ajunge pana la {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm ({Fmt.F0(row.Dpi)} dpi)")
                    : Tr.Pick($"not even the smallest width of the list ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm, {Fmt.F0(row.Dpi)} dpi) reaches the threshold", $"nici cea mai mica latime din lista ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm, {Fmt.F0(row.Dpi)} dpi) nu atinge pragul");
                string threshold = ps.DistanceIsExplicit
                    ? Tr.Pick($"threshold {Fmt.F0(ps.FallbackDpi)} dpi = 25.4 / (0.29 mm/m x {Fmt.F1(row.ViewingDistanceM)} m)", $"prag {Fmt.F0(ps.FallbackDpi)} dpi = 25.4 / (0.29 mm/m x {Fmt.F1(row.ViewingDistanceM)} m)")
                    : Tr.Pick($"threshold {Fmt.F0(ps.FallbackDpi)} dpi (convention: a pitch of {Fmt.F2(25.4 / ps.FallbackDpi)} mm, invisible from {Fmt.F2(25.4 / ps.FallbackDpi / Thresholds.AcuityMmAt1m)} m)", $"prag {Fmt.F0(ps.FallbackDpi)} dpi (conventie: pas {Fmt.F2(25.4 / ps.FallbackDpi)} mm, invizibil de la {Fmt.F2(25.4 / ps.FallbackDpi / Thresholds.AcuityMmAt1m)} m)");
                o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                    Tr.Pick($"Without stars the real resolution is not measured; only the pixel grid is judged: {rule}, and {reach}. It is a limit of the file, not of the detail - a soft nebula may need much less - so no size is recommended."
                            + (ps.DistanceIsExplicit ? "" : " Give the viewing distance, so that the threshold is the physical one, not the convention."),
                            $"Fara stele rezolutia reala nu se masoara; se judeca doar grila de pixeli: {rule}, si {reach}. E o limita a fisierului, nu a detaliului - o nebuloasa moale poate cere mult mai putin - deci nu se recomanda o marime."
                            + (ps.DistanceIsExplicit ? "" : " Da distanta de privire ca pragul sa fie cel fizic, nu conventia.")),
                    Tr.Pick($"{st.Count} stars measured (minimum {Thresholds.StarMinCount}); at {Fmt.F0(row.WidthCm)} cm the pixel pitch is {Fmt.F2(row.PitchMm)} mm, invisible from {Fmt.F2(row.PixelDistanceM)} m; {threshold}", $"{st.Count} stele masurate (minim {Thresholds.StarMinCount}); la {Fmt.F0(row.WidthCm)} cm pasul pixelului e {Fmt.F2(row.PitchMm)} mm, invizibil de la {Fmt.F2(row.PixelDistanceM)} m; {threshold}")));
            }
            else if (ps.DistanceIsExplicit)
            {
                string evidence = Tr.Pick($"median FWHM {Fmt.F2(st.FwhmMedian)} px (Q1 {Fmt.F2(st.FwhmQ1)}, Q3 {Fmt.F2(st.FwhmQ3)}, {st.Count} stars) = real detail ~{Fmt.F0(ps.EffectiveDetailPx)} px across the width; at {Fmt.F0(row.WidthCm)} cm a star is {Fmt.F2(row.StarMm)} mm and a point from {Fmt.F2(row.MinDistanceM)} m; {dist}", $"FWHM median {Fmt.F2(st.FwhmMedian)} px (Q1 {Fmt.F2(st.FwhmQ1)}, Q3 {Fmt.F2(st.FwhmQ3)}, {st.Count} stele) = detaliu real ~{Fmt.F0(ps.EffectiveDetailPx)} px pe latime; la {Fmt.F0(row.WidthCm)} cm steaua are {Fmt.F2(row.StarMm)} mm si e punct de la {Fmt.F2(row.MinDistanceM)} m; {dist}");
                if (row.PointAtViewingDistance)
                    o.Add(new Recommendation(Topic.Size, Verdict.Ok,
                        Tr.Pick($"Recommended: a {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm image, ~{Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)} cm with the passepartout and the frame.", $"Recomandat: {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm imagine, ~{Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)} cm cu passepartout si rama."),
                        evidence));
                else
                    o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                        Tr.Pick($"From {Fmt.F1(row.ViewingDistanceM)} m the stars are discs at every size of the list; the smallest ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm) minimizes the effect. Try smaller widths, or a longer distance if the viewer really stands further away.", $"De la {Fmt.F1(row.ViewingDistanceM)} m stelele sunt discuri la orice marime din lista; cea mai mica ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm) minimizeaza efectul. Incearca latimi mai mici, sau o distanta mai mare daca privitorul chiar sta mai departe."),
                        evidence));
            }
            else
            {
                // Without a distance, the relative sharpness is the same at any size (see
                // PrintSizeAnalyzer): the answer is yes/no for the whole image, and the wall
                // chooses the size. It is said as such; no choice is pretended.
                bool sharp = row.PointAtViewingDistance;
                o.Add(new Recommendation(Topic.Size, sharp ? Verdict.Ok : Verdict.Attention,
                    sharp
                        ? Tr.Pick($"Without a viewing distance no size is recommended: up to {Fmt.F0(row.WidthCm)} cm (a {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm image, ~{Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)} cm with the passepartout and the frame) the image is equally sharp from its natural distance - choose the size by the wall. If it is seen from close up (a hallway, an office), give the distance: then the sizes separate.", $"Fara distanta de privire nu se recomanda o marime: pana la {Fmt.F0(row.WidthCm)} cm ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm imagine, ~{Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)} cm cu passepartout si rama) imaginea e la fel de clara de la distanta ei naturala - alege marimea dupa perete. Daca se priveste de aproape (hol, birou), da distanta: atunci marimile se despart.")
                        : Tr.Pick($"From the natural distance the stars are slight discs at every size - the image is soft for its resolution; the smallest ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm) minimizes the effect. Give the viewing distance, so that it is judged on that.", $"De la distanta naturala stelele sunt usor discuri la orice marime - imaginea e moale pentru rezolutia ei; cea mai mica ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm) minimizeaza efectul. Da distanta de privire ca sa se judece pe ea."),
                    Tr.Pick($"median FWHM {Fmt.F2(st.FwhmMedian)} px (Q1 {Fmt.F2(st.FwhmQ1)}, Q3 {Fmt.F2(st.FwhmQ3)}, {st.Count} stars) = real detail ~{Fmt.F0(ps.EffectiveDetailPx)} px across the width; at {Fmt.F0(row.WidthCm)} cm a star is {Fmt.F2(row.StarMm)} mm and a point from {Fmt.F2(row.MinDistanceM)} m, against {dist} (tolerance {Fmt.F0((Thresholds.NaturalDistanceTolerance - 1) * 100)} %); the ratio is the same at every size", $"FWHM median {Fmt.F2(st.FwhmMedian)} px (Q1 {Fmt.F2(st.FwhmQ1)}, Q3 {Fmt.F2(st.FwhmQ3)}, {st.Count} stele) = detaliu real ~{Fmt.F0(ps.EffectiveDetailPx)} px pe latime; la {Fmt.F0(row.WidthCm)} cm steaua are {Fmt.F2(row.StarMm)} mm si e punct de la {Fmt.F2(row.MinDistanceM)} m fata de {dist} (toleranta {Fmt.F0((Thresholds.NaturalDistanceTolerance - 1) * 100)} %); raportul e acelasi la orice marime")));
            }

            if (st.SoftCorners)
                o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                    Tr.Pick("The corners are softer than the centre: do not enlarge the whole frame to the maximum; consider a crop that leaves them out.", "Colturile sunt mai moi decat centrul: nu mari cadrul intreg la maximum; considera un decupaj care le lasa afara."),
                    Tr.Pick($"FWHM corner / centre = {Fmt.F2(st.CornerToCenterRatio)} (threshold {Fmt.F1(Thresholds.CornerSoftRatio)}); centre {Fmt.F2(st.Regions[0].FwhmMedian)} px", $"FWHM colt / centru = {Fmt.F2(st.CornerToCenterRatio)} (prag {Fmt.F1(Thresholds.CornerSoftRatio)}); centru {Fmt.F2(st.Regions[0].FwhmMedian)} px")));

            if (r.Crop.Needed)
                o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                    Tr.Pick($"The target ratio needs a crop: it cuts {ReportWriter.CropCutText(r.Crop)} ({Fmt.Pct(r.Crop.CutPct)}); choose the framing yourself, not the lab.", $"Raportul tinta cere decupaj: se taie {ReportWriter.CropCutText(r.Crop)} ({Fmt.Pct(r.Crop.CutPct)}); alege tu cadrul, nu laboratorul."),
                    Tr.Pick($"native {Fmt.F3(r.Crop.NativeAspect)}, target {Fmt.F3(r.Crop.TargetAspect.Value)}; {Fmt.Dim(r.Crop.W, r.Crop.H)} px remain", $"nativ {Fmt.F3(r.Crop.NativeAspect)}, tinta {Fmt.F3(r.Crop.TargetAspect.Value)}; ramane {Fmt.Dim(r.Crop.W, r.Crop.H)} px")));

            if (ps.Rows.Any(x => x.Oversize && x.PointAtViewingDistance))
                o.Add(new Recommendation(Topic.Size, Verdict.Ok,
                    Tr.Pick($"Over {Fmt.F0(Thresholds.OversizeCm)} cm the limit is not the file but the glass, the weight and the transport.", $"Peste {Fmt.F0(Thresholds.OversizeCm)} cm nu fisierul e limita, ci sticla, greutatea si transportul."),
                    Tr.Pick($"the rows over {Fmt.F0(Thresholds.OversizeCm)} cm pass the resolution test but are not recommended", $"randurile peste {Fmt.F0(Thresholds.OversizeCm)} cm trec testul de rezolutie dar nu se recomanda")));
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
                    Tr.Pick("Too little smooth sky to measure the residual noise.", "Prea putin cer neted pentru a masura zgomotul rezidual."),
                    Tr.Pick($"{n.SmoothTiles} smooth tiles of {n.Candidates} tried (minimum {Thresholds.NoiseMinTiles})", $"{n.SmoothTiles} dale netede din {n.Candidates} incercate (minim {Thresholds.NoiseMinTiles})")));
                return;
            }

            string target = Tr.Pick($"the target for the print version: {Fmt.F1(Thresholds.NoisePrintLoLsb)}-{Fmt.F1(Thresholds.NoisePrintHiLsb)} LSB", $"tinta pentru versiunea de print: {Fmt.F1(Thresholds.NoisePrintLoLsb)}-{Fmt.F1(Thresholds.NoisePrintHiLsb)} LSB");
            string after = "";
            SimulationRow row = r.Simulation.Recommended;
            if (row != null && !row.AsIs)
            {
                // The gamma curve's slope at the background: what the noise is multiplied by when
                // the background rises (or falls: the recommendation goes both ways, ADR 0025).
                double bg = r.Simulation.BackgroundMeasured;
                double slope = row.Gamma * Math.Pow(bg, row.Gamma - 1);
                after = Tr.Pick($"; after the re-stretch to {Fmt.Level(row.Target)} the noise {(slope >= 1 ? "grows" : "shrinks")} by ~{Fmt.F2(slope)} x (the slope of the curve at the background), to ~{Fmt.F2(n.SigmaMedianLsb * slope)} LSB", $"; dupa re-stretch la {Fmt.Level(row.Target)} zgomotul {(slope >= 1 ? "creste" : "scade")} de ~{Fmt.F2(slope)} x (panta curbei la fundal), la ~{Fmt.F2(n.SigmaMedianLsb * slope)} LSB");
            }
            // The share of the smooth tiles under the banding sigma is always told: an AI denoiser
            // can leave the median where it should be and still wipe a tail of tiles, where the
            // 8-bit plateau of section 1 is then measured.
            string because = Tr.Pick($"median sigma {Fmt.F2(n.SigmaMedianLsb)} LSB on {n.SmoothTiles} smooth tiles (10 % {Fmt.F2(n.Sigma10thLsb)}, minimum {Fmt.F2(n.SigmaMinLsb)}; under {Fmt.F1(Thresholds.NoiseBandingLsb)} LSB, {Fmt.Pct(n.PctTilesBelowBanding)} of the tiles); {target}{after}",
                $"sigma median {Fmt.F2(n.SigmaMedianLsb)} LSB pe {n.SmoothTiles} dale netede (10 % {Fmt.F2(n.Sigma10thLsb)}, minim {Fmt.F2(n.SigmaMinLsb)}; sub {Fmt.F1(Thresholds.NoiseBandingLsb)} LSB, {Fmt.Pct(n.PctTilesBelowBanding)} din dale); {target}{after}");

            if (n.BandingRisk)
                o.Add(new Recommendation(Topic.Denoise, Verdict.Attention,
                    Tr.Pick("Denoise is set too strong for print: the noise no longer dithers the quantization. For the print version, a lower setting keeps the texture and the faint stars.", "Denoise-ul e apasat tare pentru print: zgomotul nu mai dither-eaza cuantizarea. Pentru versiunea de print, o valoare mai mica pastreaza textura si stelele slabe."),
                    because));
            else if (n.UnderDenoised)
                o.Add(new Recommendation(Topic.Denoise, Verdict.Attention,
                    Tr.Pick("Noise visible at 100 % on screen; on paper, at the sizes of the table, it does not show - denoise only if you also want a clean screen version.", "Zgomot vizibil la 100 % pe ecran; pe hartie, la marimile din tabel, nu se vede - denoise doar daca vrei si versiunea de ecran curata."),
                    because + Tr.Pick($"; over {Fmt.F1(Thresholds.NoiseUnderDenoisedLsb)} LSB", $"; peste {Fmt.F1(Thresholds.NoiseUnderDenoisedLsb)} LSB")));
            else if (n.GoodForPrint)
                o.Add(new Recommendation(Topic.Denoise, Verdict.Ok, Tr.Pick("The residual noise is exactly where it should be for print.", "Zgomotul rezidual e exact unde trebuie pentru print."), because));
            else
                o.Add(new Recommendation(Topic.Denoise, Verdict.Ok, Tr.Pick("The residual noise is acceptable for print.", "Zgomotul rezidual e acceptabil pentru print."), because));
        }

        // ------------------------------------------------------------ 6. warnings

        /// <summary>
        /// Section 6, the warnings: an 8-bit file in disguise, no ICC profile or one that is not
        /// sRGB, the 16-bit master to keep, and the reader's note.
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="o">The list the recommendations are added to.</param>
        static void Warnings(AnalysisResult r, List<Recommendation> o)
        {
            FileFactsResult f = r.File;
            BitDepthResult b = r.BitDepth;

            if (b.IsUpscaled)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Attention,
                    Tr.Pick("The file is 8-bit disguised as 16: there is nothing to lose at the conversion, and nothing to gain from 16 either.", "Fisierul e pe 8 biti deghizat in 16: nu ai ce pierde la conversie, dar nici ce castiga din 16."),
                    Tr.Pick($"{Fmt.Pct(b.UpscaledFraction * 100)} of the samples with the value = high_byte x 257 (threshold {Fmt.F0(Thresholds.UpscaledFraction * 100)} %); distinct levels: {string.Join(" / ", b.DistinctLevels.Select(n => Fmt.Int(n)))}", $"{Fmt.Pct(b.UpscaledFraction * 100)} din esantioane cu valoarea = octet_inalt x 257 (prag {Fmt.F0(Thresholds.UpscaledFraction * 100)} %); niveluri distincte: {string.Join(" / ", b.DistinctLevels.Select(n => Fmt.Int(n)))}")));

            if (!f.HasIcc)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Attention,
                    Tr.Pick("No ICC profile: the lab will assume sRGB. Export with the sRGB profile embedded, so that nobody assumes anything.", "Fara profil ICC: laboratorul va presupune sRGB. Exporta cu profilul sRGB inglobat, ca sa nu presupuna nimeni nimic."),
                    Tr.Pick("no profile in the file; the tool measured assuming sRGB", "niciun profil in fisier; unealta a masurat presupunand sRGB")));
            else if (f.IccLooksSrgb == false)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Attention,
                    Tr.Pick($"The profile is not sRGB ({f.IccDescription}): tell the lab, and know that the L* measurements here assume sRGB.", $"Profilul nu e sRGB ({f.IccDescription}): spune-i laboratorului, si stii ca masuratorile de L* de aici presupun sRGB."),
                    Tr.Pick("the profile's name does not contain 'sRGB'", "numele profilului nu contine 'sRGB'")));

            if (r.Quantization.Keep16BitMaster)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Attention,
                    Tr.Pick("Keep the 16-bit master: any later edit (a reframing, a retouch, another print) starts from it, not from the print file.", "Pastreaza master-ul de 16 biti: orice editare ulterioara (recadrare, retus, alt print) pleaca din el, nu din fisierul de tipar."),
                    Tr.Pick($"after a shadow-lift (gamma {Fmt.F1(Thresholds.ShadowLiftGamma)}), from 8 bits {r.Quantization.MissingFrom8} shadow levels are missing ({r.Quantization.LevelsUsedFrom8} used), from 16 {r.Quantization.MissingFrom16} ({r.Quantization.LevelsUsedFrom16} used); threshold {Thresholds.MissingLevelsWarn}", $"dupa un shadow-lift (gamma {Fmt.F1(Thresholds.ShadowLiftGamma)}) din 8 biti lipsesc {r.Quantization.MissingFrom8} niveluri din umbre ({r.Quantization.LevelsUsedFrom8} folosite), din 16 lipsesc {r.Quantization.MissingFrom16} ({r.Quantization.LevelsUsedFrom16} folosite); prag {Thresholds.MissingLevelsWarn}")));

            if (f.ReaderNote != null)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Ok, Tr.Pick("Note from reading the file: ", "Nota cititorului: ") + f.ReaderNote + ".", Tr.Pick("an observation made while reading the file", "observatie la citirea fisierului")));
        }
    }
}
