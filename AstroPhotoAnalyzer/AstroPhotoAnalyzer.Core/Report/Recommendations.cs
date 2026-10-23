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
    /// Turns an analysis's results into the report's recommendations, each with its "fiindca"
    /// (ADR 0011), Romanian.
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
                    $"Trimite TIFF 8-bit LZW ({Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}). Conversia 16 -> 8 biti e rotunjire pura, nu pierdere.",
                    $"eroare de rotunjire medie {Fmt.F2(q.RoundingMeanAbs)}, maxima {Fmt.F2(q.RoundingMax)} niveluri (jumatate de nivel e maximul posibil)"));
            else
                o.Add(new Recommendation(Topic.Format, Verdict.Ok,
                    $"Trimite TIFF 8-bit LZW ({Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}). Sursa e deja pe 8 biti; nu e nimic de convertit.",
                    $"sursa are {r.File.BitDepth} biti pe esantion"));

            if (q100 != null)
            {
                Verdict v = q100.Acceptable ? (q100.PctOverThreshold > Thresholds.JpegOverThresholdWarnPct ? Verdict.Attention : Verdict.Ok) : Verdict.Attention;
                o.Add(new Recommendation(Topic.Format, v,
                    $"Daca laboratorul accepta doar JPEG: q100, 4:4:4 ({Fmt.Mb(q100.Megabytes)}).",
                    $"eroare medie {Fmt.F2(q100.MeanAbsError)}, maxima {Fmt.F1(q100.MaxError)} niveluri; {Fmt.Pct(q100.PctOverThreshold)} din esantioane cu eroare > {Fmt.F0(Thresholds.JpegErrorCountLevels)}, la nivelul mediu {Fmt.F0(q100.MeanLevelOverThreshold)}"
                    + (q100.PctOverThreshold > 0 && q100.MeanLevelOverThreshold < Thresholds.DarkLevelB ? " (in cerul intunecat, unde JPEG-ul lucreaza cel mai prost)" : "")));

                JpegRow worst = r.Jpeg.Rows.Where(x => x.Quality < 100 && !x.Acceptable).OrderByDescending(x => x.Quality).FirstOrDefault();
                if (worst != null)
                    o.Add(new Recommendation(Topic.Format, Verdict.Attention,
                        $"Nu cobori sub q100.",
                        $"q{worst.Quality} are eroare maxima {Fmt.F1(worst.MaxError)} niveluri (prag {Fmt.F0(Thresholds.JpegMaxErrorReject)}), pe stele si pe muchii"));
            }

            if (q.PlateauKnown && size != null)
            {
                double mm = q.PlateauMmAt(size.Dpi);
                if (q.PlateauVisibleAt(size.Dpi))
                    o.Add(new Recommendation(Topic.Format, Verdict.Attention,
                        "Risc de banding pe 8 biti: trimite 16-bit daca laboratorul accepta, sau adauga dither la export.",
                        $"palier de {Fmt.F1(q.PlateauPx)} px = {Fmt.F2(mm)} mm la {Fmt.F0(size.Dpi)} dpi, peste {Fmt.F1(Thresholds.PlateauVisibleMm)} mm"));
                else
                    o.Add(new Recommendation(Topic.Format, Verdict.Ok,
                        "Fara banding vizibil pe 8 biti: zgomotul rezidual dither-eaza cuantizarea.",
                        $"palier de {Fmt.F1(q.PlateauPx)} px = {Fmt.F2(mm)} mm la {Fmt.F0(size.Dpi)} dpi, sub {Fmt.F1(Thresholds.PlateauVisibleMm)} mm"));
            }
            else if (r.Noise.BandingRisk)
                o.Add(new Recommendation(Topic.Format, Verdict.Attention,
                    "Risc de banding pe 8 biti (nu s-a putut masura palierul): trimite 16-bit daca laboratorul accepta.",
                    $"zgomot rezidual sigma {Fmt.F2(r.Noise.SigmaMedianLsb)} LSB, sub {Fmt.F1(Thresholds.NoiseBandingLsb)}"));
        }

        // ------------------------------------------------------------ 2. background

        /// <summary>
        /// Section 2, the background and the paper. The verdict: the dust (the darkest extended
        /// area: the dust class or the darkest sky, ADR 0033) against paper black, and the sky
        /// against it (ADR 0025); then the sky against the visibility threshold (ADR 0029). The
        /// "fiindca" says what the dust is, which are the darkest clouds, where the black is, from
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
            string clouds = s.Clouds.Count == 0 ? "" : "; cei mai intunecati: " + CloudList(s);
            string because = $"praful ({s.DustDescription}{(s.MethodNote == null ? "" : "; " + s.MethodNote)}) la L* {Fmt.F1(asIs.LstarDust)}{clouds}; "
                + $"negrul hartiei L* {Fmt.F1(s.PaperBlackLstar)} la Dmax {Fmt.Dmax(s.Dmax)}; "
                + $"cerul ({s.SkyDescription}) la L* {Fmt.F1(asIs.LstarSky)}; nivelul fundalului {Fmt.Level(s.BackgroundMeasured)}; {pass}; "
                + $"pragul de vizibilitate L* {Fmt.F0(Thresholds.LowSeparationLstar)}, sub el {Fmt.Pct(asIs.PctBelowLowSeparation)} din pixeli";
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
                        ? "Fundalul e print-safe asa cum e; grila nu coboara sub nivelul lui."
                        : !lower.DustOnPaperBlack
                            ? $"Fundalul e print-safe asa cum e; la {Fmt.Level(lower.Target)} praful ar cobori la L* {Fmt.F1(lower.LstarDust)}, sub negrul hartiei."
                            : $"Fundalul e print-safe asa cum e; la {Fmt.Level(lower.Target)} cerul ar cobori la L* {Fmt.F1(lower.LstarSky)}, sub pragul de vizibilitate {threshold}.";
                }
                else
                {
                    bool floor = ReferenceEquals(rec, s.Rows[0]);
                    text = $"Fundalul e print-safe asa cum e; poti cobori pana la {Fmt.Level(rec.Target)} (praful la L* {Fmt.F1(rec.LstarDust)}, cerul la L* {Fmt.F1(rec.LstarSky)})"
                        + (floor ? "; grila nu coboara mai jos." : ".");
                }
                o.Add(new Recommendation(Topic.Background, Verdict.Ok, text, because));
                return;
            }

            Verdict v = s.SkyBelowPaperBlack ? Verdict.Problem : Verdict.Attention;
            string what = s.SkyBelowPaperBlack
                ? "Cerul e sub negrul hartiei: cerul si praful s-ar tipari cu ACELASI negru."
                : s.DustBelowPaperBlack
                    ? "Praful e sub negrul hartiei: norii de praf s-ar tipari negru pur."
                    : $"Cerul e sub {threshold}: hartia il separa de negru, dar in lumina de camera s-ar vedea aproape negru.";
            string fix;
            if (s.Recommended != null)
            {
                string exact = s.DustBelowPaperBlack
                    ? (s.ExactTarget.HasValue ? $"; exact pe negru la {ReportWriter.TargetText(s.ExactTarget)}" : "")
                    : $"; cerul la {threshold} de la {ReportWriter.TargetText(s.VisibilityTarget)}";
                fix = $" Re-stretch din liniar la fundal {Fmt.Level(s.Recommended.Target)} (simulat: praful la L* {Fmt.F1(s.Recommended.LstarDust)}, cerul la L* {Fmt.F1(s.Recommended.LstarSky)}{exact}).";
            }
            else if (!s.DustBelowPaperBlack || s.Rows.Any(x => x.DustOnPaperBlack))
            {
                // The dust reaches black on the grid, but the sky does not reach the threshold on
                // it: a sky much darker than the measured background, beside a bright nebula.
                string top = Fmt.Level(s.Rows.Where(x => !x.AsIs).Select(x => x.Target).DefaultIfEmpty(s.AsIs.Target).Max());
                fix = $" Nicio tinta pana la {top} nu aduce cerul la {threshold}; ar trebui {ReportWriter.TargetText(s.VisibilityTarget)}.";
            }
            else
            {
                string top = Fmt.Level(s.Rows.Where(x => !x.AsIs).Select(x => x.Target).DefaultIfEmpty(s.AsIs.Target).Max());
                fix = s.ExactTarget.HasValue
                    ? $" Nicio tinta pana la {top} nu aduce praful pe negrul hartiei; ar trebui ~{Fmt.Level(s.ExactTarget.Value)}, prea sus pentru un cer de noapte: accepta norii cei mai intunecati negri (Anexa B) sau alege alt mediu."
                    : $" Nicio tinta pana la {top} nu aduce praful pe negrul hartiei, nici {Fmt.Level(Thresholds.ExactTargetHi)}: praful e practic negru; accepta norii cei mai intunecati negri (Anexa B) sau alege alt mediu.";
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
                o.Add(new Recommendation(Topic.Colour, Verdict.Ok, "Imagine mono: nu exista tenta de masurat.", "un singur plan"));
            }
            else if (b.HasCast)
            {
                string kind = Math.Abs(b.GreenExcess) >= Math.Abs(b.RedMinusBlue)
                    ? (b.GreenExcess > 0 ? "verde" : "magenta")
                    : (b.RedMinusBlue > 0 ? "calda" : "rece");
                o.Add(new Recommendation(Topic.Colour, Verdict.Attention,
                    $"Tenta {kind} pe fundal: neutralizeaza (SCNR / BackgroundNeutralization) inainte de print - la fundal ridicat se va vedea.",
                    $"fundal R {Fmt.F1(b.Levels[0])} G {Fmt.F1(b.Levels[1])} B {Fmt.F1(b.Levels[2])}; G - (R+B)/2 = {Fmt.S1(b.GreenExcess)}, R - B = {Fmt.S1(b.RedMinusBlue)} niveluri (prag {Fmt.F1(Thresholds.CastWarnLevels)})"));
            }
            else
            {
                o.Add(new Recommendation(Topic.Colour, Verdict.Ok, "Fundalul e neutru.",
                    $"fundal R {Fmt.F1(b.Levels[0])} G {Fmt.F1(b.Levels[1])} B {Fmt.F1(b.Levels[2])}; G - (R+B)/2 = {Fmt.S1(b.GreenExcess)}, R - B = {Fmt.S1(b.RedMinusBlue)} niveluri"));
            }

            // The COLOURED deviations, each with its own recommendation: light pollution, the
            // horizon, a tinted gradient - things to correct on linear data.
            foreach (EdgeRegion e in b.FlaggedRegions.Where(x => x.Coloured))
            {
                string deltas = $"R {Fmt.S1(e.Deltas[0])}, G {Fmt.S1(e.Deltas[1])}, B {Fmt.S1(e.Deltas[2])}";
                string kind = e.Deltas[0] > e.Deltas[2] + Thresholds.CornerGradientLevels ? "cald (lumina de orizont / poluare)"
                    : e.Deltas[2] > e.Deltas[0] + Thresholds.CornerGradientLevels ? "rece (albastrui)"
                    : e.Deltas[1] > (e.Deltas[0] + e.Deltas[2]) / 2 ? "verzui" : "cu alta culoare decat fundalul";
                o.Add(new Recommendation(Topic.Colour, Verdict.Attention,
                    $"{Cap(e.Name)}: {kind}. Corecteaza pe liniar (GraXpert) sau accepta ca element de compozitie - nu cu o curba locala pe imaginea stretch-uita.",
                    $"fata de fundal: {deltas} niveluri (prag {Fmt.F1(Thresholds.CornerGradientLevels)}); canalele se misca diferit"));
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
                    (brighter ? "Margini mai luminoase decat fundalul, fara tenta: " : "Margini care difera de fundal, fara tenta: ")
                    + "cel mai probabil obiectul insusi, nu un gradient - verifica in Preview inainte sa corectezi ceva.",
                    $"fata de fundal, in niveluri: {list} (prag {Fmt.F1(Thresholds.CornerGradientLevels)}); canalele se misca impreuna"));
            }
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
            string dist = ps.DistanceIsExplicit ? $"distanta data {Fmt.F1(row.ViewingDistanceM)} m" : $"distanta naturala {Fmt.F2(row.NaturalDistanceM)} m (1.5 x diagonala)";

            if (st.Insufficient)
            {
                // Without a FWHM the detail is not measured, only the pixel grid - a limit of the
                // file. It says how far the file reaches; no row is recommended.
                string rule = ps.DistanceIsExplicit
                    ? $"de la {Fmt.F1(row.ViewingDistanceM)} m grila dispare de la {Fmt.F0(ps.FallbackDpi)} dpi in sus"
                    : $"fara distanta data se cere conventia de {Fmt.F0(ps.FallbackDpi)} dpi (grila invizibila de la ~0.5 m, cand te apropii)";
                string reach = row.PointAtViewingDistance
                    ? $"fisierul ajunge pana la {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm ({Fmt.F0(row.Dpi)} dpi)"
                    : $"nici cea mai mica latime din lista ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm, {Fmt.F0(row.Dpi)} dpi) nu atinge pragul";
                string threshold = ps.DistanceIsExplicit
                    ? $"prag {Fmt.F0(ps.FallbackDpi)} dpi = 25.4 / (0.29 mm/m x {Fmt.F1(row.ViewingDistanceM)} m)"
                    : $"prag {Fmt.F0(ps.FallbackDpi)} dpi (conventie: pas {Fmt.F2(25.4 / ps.FallbackDpi)} mm, invizibil de la {Fmt.F2(25.4 / ps.FallbackDpi / Thresholds.AcuityMmAt1m)} m)";
                o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                    $"Fara stele rezolutia reala nu se masoara; se judeca doar grila de pixeli: {rule}, si {reach}. E o limita a fisierului, nu a detaliului - o nebuloasa moale poate cere mult mai putin - deci nu se recomanda o marime."
                    + (ps.DistanceIsExplicit ? "" : " Da distanta de privire ca pragul sa fie cel fizic, nu conventia."),
                    $"{st.Count} stele masurate (minim {Thresholds.StarMinCount}); la {Fmt.F0(row.WidthCm)} cm pasul pixelului e {Fmt.F2(row.PitchMm)} mm, invizibil de la {Fmt.F2(row.PixelDistanceM)} m; {threshold}"));
            }
            else if (ps.DistanceIsExplicit)
            {
                string evidence = $"FWHM median {Fmt.F2(st.FwhmMedian)} px (Q1 {Fmt.F2(st.FwhmQ1)}, Q3 {Fmt.F2(st.FwhmQ3)}, {st.Count} stele) = detaliu real ~{Fmt.F0(ps.EffectiveDetailPx)} px pe latime; la {Fmt.F0(row.WidthCm)} cm steaua are {Fmt.F2(row.StarMm)} mm si e punct de la {Fmt.F2(row.MinDistanceM)} m; {dist}";
                if (row.PointAtViewingDistance)
                    o.Add(new Recommendation(Topic.Size, Verdict.Ok,
                        $"Recomandat: {Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm imagine, ~{Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)} cm cu passepartout si rama.",
                        evidence));
                else
                    o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                        $"De la {Fmt.F1(row.ViewingDistanceM)} m stelele sunt discuri la orice marime din lista; cea mai mica ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm) minimizeaza efectul. Incearca latimi mai mici, sau o distanta mai mare daca privitorul chiar sta mai departe.",
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
                        ? $"Fara distanta de privire nu se recomanda o marime: pana la {Fmt.F0(row.WidthCm)} cm ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm imagine, ~{Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)} cm cu passepartout si rama) imaginea e la fel de clara de la distanta ei naturala - alege marimea dupa perete. Daca se priveste de aproape (hol, birou), da distanta: atunci marimile se despart."
                        : $"De la distanta naturala stelele sunt usor discuri la orice marime - imaginea e moale pentru rezolutia ei; cea mai mica ({Fmt.F0(row.WidthCm)} x {Fmt.F0(row.HeightCm)} cm) minimizeaza efectul. Da distanta de privire ca sa se judece pe ea.",
                    $"FWHM median {Fmt.F2(st.FwhmMedian)} px (Q1 {Fmt.F2(st.FwhmQ1)}, Q3 {Fmt.F2(st.FwhmQ3)}, {st.Count} stele) = detaliu real ~{Fmt.F0(ps.EffectiveDetailPx)} px pe latime; la {Fmt.F0(row.WidthCm)} cm steaua are {Fmt.F2(row.StarMm)} mm si e punct de la {Fmt.F2(row.MinDistanceM)} m fata de {dist} (toleranta {Fmt.F0((Thresholds.NaturalDistanceTolerance - 1) * 100)} %); raportul e acelasi la orice marime"));
            }

            if (st.SoftCorners)
                o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                    "Colturile sunt mai moi decat centrul: nu mari cadrul intreg la maximum; considera un decupaj care le lasa afara.",
                    $"FWHM colt / centru = {Fmt.F2(st.CornerToCenterRatio)} (prag {Fmt.F1(Thresholds.CornerSoftRatio)}); centru {Fmt.F2(st.Regions[0].FwhmMedian)} px"));

            if (r.Crop.Needed)
                o.Add(new Recommendation(Topic.Size, Verdict.Attention,
                    $"Raportul tinta cere decupaj: se taie {ReportWriter.CropCutText(r.Crop)} ({Fmt.Pct(r.Crop.CutPct)}); alege tu cadrul, nu laboratorul.",
                    $"nativ {Fmt.F3(r.Crop.NativeAspect)}, tinta {Fmt.F3(r.Crop.TargetAspect.Value)}; ramane {Fmt.Dim(r.Crop.W, r.Crop.H)} px"));

            if (ps.Rows.Any(x => x.Oversize && x.PointAtViewingDistance))
                o.Add(new Recommendation(Topic.Size, Verdict.Ok,
                    $"Peste {Fmt.F0(Thresholds.OversizeCm)} cm nu fisierul e limita, ci sticla, greutatea si transportul.",
                    $"randurile peste {Fmt.F0(Thresholds.OversizeCm)} cm trec testul de rezolutie dar nu se recomanda"));
        }

        // ------------------------------------------------------------ 5. denoise

        /// <summary>
        /// Section 5, the denoise for the print version: too little smooth sky, or the noise
        /// against the print target (pressed too hard, visible on screen only, right, or
        /// acceptable), with the noise after the recommended re-stretch in the "fiindca".
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="o">The list the one recommendation is added to.</param>
        static void Denoise(AnalysisResult r, List<Recommendation> o)
        {
            NoiseResult n = r.Noise;
            if (n.Insufficient)
            {
                o.Add(new Recommendation(Topic.Denoise, Verdict.Attention,
                    "Prea putin cer neted pentru a masura zgomotul rezidual.",
                    $"{n.SmoothTiles} dale netede din {n.Candidates} incercate (minim {Thresholds.NoiseMinTiles})"));
                return;
            }

            string target = $"tinta pentru versiunea de print: {Fmt.F1(Thresholds.NoisePrintLoLsb)}-{Fmt.F1(Thresholds.NoisePrintHiLsb)} LSB";
            string after = "";
            SimulationRow row = r.Simulation.Recommended;
            if (row != null && !row.AsIs)
            {
                // The gamma curve's slope at the background: what the noise is multiplied by when
                // the background rises (or falls: the recommendation goes both ways, ADR 0025).
                double bg = r.Simulation.BackgroundMeasured;
                double slope = row.Gamma * Math.Pow(bg, row.Gamma - 1);
                after = $"; dupa re-stretch la {Fmt.Level(row.Target)} zgomotul {(slope >= 1 ? "creste" : "scade")} de ~{Fmt.F2(slope)} x (panta curbei la fundal), la ~{Fmt.F2(n.SigmaMedianLsb * slope)} LSB";
            }
            string because = $"sigma median {Fmt.F2(n.SigmaMedianLsb)} LSB pe {n.SmoothTiles} dale netede (10 % {Fmt.F2(n.Sigma10thLsb)}, minim {Fmt.F2(n.SigmaMinLsb)}); {target}{after}";

            if (n.BandingRisk)
                o.Add(new Recommendation(Topic.Denoise, Verdict.Attention,
                    "Denoise-ul e apasat tare pentru print: zgomotul nu mai dither-eaza cuantizarea. Pentru versiunea de print, o valoare mai mica pastreaza textura si stelele slabe.",
                    because + $"; sub {Fmt.F1(Thresholds.NoiseBandingLsb)} LSB, {Fmt.Pct(n.PctTilesBelowBanding)} din dale"));
            else if (n.UnderDenoised)
                o.Add(new Recommendation(Topic.Denoise, Verdict.Attention,
                    "Zgomot vizibil la 100 % pe ecran; pe hartie, la marimile din tabel, nu se vede - denoise doar daca vrei si versiunea de ecran curata.",
                    because + $"; peste {Fmt.F1(Thresholds.NoiseUnderDenoisedLsb)} LSB"));
            else if (n.GoodForPrint)
                o.Add(new Recommendation(Topic.Denoise, Verdict.Ok, "Zgomotul rezidual e exact unde trebuie pentru print.", because));
            else
                o.Add(new Recommendation(Topic.Denoise, Verdict.Ok, "Zgomotul rezidual e acceptabil pentru print.", because));
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
                    "Fisierul e pe 8 biti deghizat in 16: nu ai ce pierde la conversie, dar nici ce castiga din 16.",
                    $"{Fmt.Pct(b.UpscaledFraction * 100)} din esantioane cu valoarea = octet_inalt x 257 (prag {Fmt.F0(Thresholds.UpscaledFraction * 100)} %); niveluri distincte: {string.Join(" / ", b.DistinctLevels.Select(n => Fmt.Int(n)))}"));

            if (!f.HasIcc)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Attention,
                    "Fara profil ICC: laboratorul va presupune sRGB. Exporta cu profilul sRGB inglobat, ca sa nu presupuna nimeni nimic.",
                    "niciun profil in fisier; unealta a masurat presupunand sRGB"));
            else if (f.IccLooksSrgb == false)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Attention,
                    $"Profilul nu e sRGB ({f.IccDescription}): spune-i laboratorului, si stii ca masuratorile de L* de aici presupun sRGB.",
                    "numele profilului nu contine 'sRGB'"));

            if (r.Quantization.Keep16BitMaster)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Attention,
                    "Pastreaza master-ul de 16 biti: orice editare ulterioara (recadrare, retus, alt print) pleaca din el, nu din fisierul de tipar.",
                    $"dupa un shadow-lift (gamma {Fmt.F1(Thresholds.ShadowLiftGamma)}) din 8 biti lipsesc {r.Quantization.MissingFrom8} niveluri din umbre ({r.Quantization.LevelsUsedFrom8} folosite), din 16 lipsesc {r.Quantization.MissingFrom16} ({r.Quantization.LevelsUsedFrom16} folosite); prag {Thresholds.MissingLevelsWarn}"));

            if (f.ReaderNote != null)
                o.Add(new Recommendation(Topic.Warnings, Verdict.Ok, "Nota cititorului: " + f.ReaderNote + ".", "observatie la citirea fisierului"));
        }
    }
}
