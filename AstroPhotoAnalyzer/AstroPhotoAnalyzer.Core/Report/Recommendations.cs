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

// Recommendations.cs - motorul: din rezultate, o lista de recomandari in ordinea
// deciziilor. Fiecare regula e o functie mica peste un record; pragurile sunt in
// Thresholds, formularile sunt aici. Nimic din ce e aici nu se rescrie in fereastra:
// fereastra afiseaza lista, nu o deduce.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal static class Recommendations
    {
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
                Verdict v = q100.Acceptable ? (q100.PctOverThreshold > 5 ? Verdict.Attention : Verdict.Ok) : Verdict.Attention;
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

        // ------------------------------------------------------------ 2. fundal

        static void Background(AnalysisResult r, List<Recommendation> o)
        {
            TonalResult t = r.Tonal;
            SimulationResult s = r.Simulation;
            string paper = $"negrul hartiei = L* {Fmt.F1(t.PaperBlackLstar)} la Dmax {Fmt.F1(t.Dmax)}";

            if (t.PrintSafe)
            {
                o.Add(new Recommendation(Topic.Background, Verdict.Ok,
                    "Fundalul e print-safe: se poate trimite asa cum e.",
                    $"{Fmt.Pct(t.PctBelowPaperBlack)} din pixeli sub negrul hartiei (prag {Fmt.F0(Thresholds.PaperBlackOkPct)} %), L* median {Fmt.F1(t.LstarMedian)}; {paper}"));
                return;
            }

            Verdict v = t.BackgroundTooLowForPaper ? Verdict.Problem : Verdict.Attention;
            string what = t.BackgroundTooLowForPaper
                ? "Fundalul e prea jos pentru hartie: tot ce e sub negrul ei se printeaza cu ACELASI negru."
                : "Fundalul e la limita pentru hartie.";
            string fix = s.RecommendedTarget.HasValue
                ? $" Re-stretch din liniar la fundal {Fmt.F2(s.RecommendedTarget.Value)} (simulat: {Fmt.Pct(RowFor(s, s.RecommendedTarget.Value).PctBelowPaperBlack)} sub negru, L* median {Fmt.F1(RowFor(s, s.RecommendedTarget.Value).LstarMedian)})."
                : $" Nicio tinta din lista ({string.Join(", ", s.Rows.Skip(1).Select(x => Fmt.F2(x.Target)))}) nu ajunge print-safe: incearca una mai sus.";
            o.Add(new Recommendation(Topic.Background, v, what + fix,
                $"{Fmt.Pct(t.PctBelowPaperBlack)} din pixeli sub negrul hartiei (prag {Fmt.F0(Thresholds.PaperBlackWarnPct)} %), {Fmt.Pct(t.PctBelowLowSeparation)} sub L* {Fmt.F0(Thresholds.LowSeparationLstar)} (separare slaba); fundal masurat {Fmt.F3(s.BackgroundMeasured)}; {paper}"));
        }

        static SimulationRow RowFor(SimulationResult s, double target)
            => s.Rows.First(x => !x.AsIs && Math.Abs(x.Target - target) < 1e-9);

        // ------------------------------------------------------------ 3. culoare

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

            // Abaterile COLORATE, fiecare cu recomandarea ei: poluare, orizont, gradient
            // cu tenta - lucruri de corectat pe liniar.
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

            // Abaterile NEUTRE, toate intr-o singura nota: o margine mai luminoasa la fel pe
            // toate canalele e de cele mai multe ori obiectul insusi (banda Caii Lactee
            // intrand in colt), nu un gradient. Unealta nu poate deosebi; o spune si lasa
            // omul sa se uite in Preview.
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

        static string Cap(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ------------------------------------------------------------ 4. marime

        static void Size(AnalysisResult r, List<Recommendation> o)
        {
            StarResult st = r.Stars;
            PrintSizeResult ps = r.Size;
            SizeRow row = ps.ReferenceRow;
            string dist = ps.DistanceIsExplicit ? $"distanta data {Fmt.F1(row.ViewingDistanceM)} m" : $"distanta naturala {Fmt.F2(row.NaturalDistanceM)} m (1.5 x diagonala)";

            if (st.Insufficient)
            {
                // Fara FWHM nu se masoara detaliul, ci doar grila de pixeli - o limita a
                // fisierului. Se spune pana unde ajunge fisierul; nu se recomanda un rand.
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
                // Fara distanta, claritatea relativa e aceeasi la orice marime (vezi
                // PrintSizeAnalyzer): raspunsul e da/nu pentru toata imaginea, iar
                // marimea o alege peretele. Se spune ca atare, nu se pretinde o alegere.
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
                    $"Raportul tinta cere decupaj: se taie {Fmt.Int(r.Crop.CutPerSidePx)} px pe fiecare parte din {r.Crop.Axis} ({Fmt.Pct(r.Crop.CutPct)}); alege tu cadrul, nu laboratorul.",
                    $"nativ {Fmt.F3(r.Crop.NativeAspect)}, tinta {Fmt.F3(r.Crop.TargetAspect.Value)}; ramane {Fmt.Dim(r.Crop.W, r.Crop.H)} px"));

            if (ps.Rows.Any(x => x.Oversize && x.PointAtViewingDistance))
                o.Add(new Recommendation(Topic.Size, Verdict.Ok,
                    $"Peste {Fmt.F0(Thresholds.OversizeCm)} cm nu fisierul e limita, ci sticla, greutatea si transportul.",
                    $"randurile peste {Fmt.F0(Thresholds.OversizeCm)} cm trec testul de rezolutie dar nu se recomanda"));
        }

        // ------------------------------------------------------------ 5. denoise

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
            if (r.Simulation.RecommendedTarget.HasValue)
            {
                SimulationRow row = RowFor(r.Simulation, r.Simulation.RecommendedTarget.Value);
                double bg = r.Simulation.BackgroundMeasured;
                double slope = row.Gamma * Math.Pow(bg, row.Gamma - 1);
                after = $"; dupa re-stretch la {Fmt.F2(row.Target)} zgomotul creste de ~{Fmt.F2(slope)} x (panta curbei la fundal), la ~{Fmt.F2(n.SigmaMedianLsb * slope)} LSB";
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

        // ------------------------------------------------------------ 6. avertismente

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
