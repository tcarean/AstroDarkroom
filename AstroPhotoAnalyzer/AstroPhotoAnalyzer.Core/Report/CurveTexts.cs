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

// CurveTexts.cs - the Editare Curbe tab's sentences (R-143, R-144, R-145): a curve's evidence, the
// paper checks, where the anchors come from, the HISTORY text of the saved file.
//
// The window writes none of them: it shows them. The paper checks are Recommendations, with a
// mandatory "because", like any recommendation of the report (ADR 0011). The texts that were
// constants are properties, so that they are in the interface language in use (ADR 0048).

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The sentences of diagnosis and evidence the Editare Curbe tab and the curve window show (the
    /// reasons, the evidence lines, the paper checks, the captions), and the HISTORY text of a
    /// saved curve, in the interface language (ADR 0032, ADR 0048); the console prints none of
    /// them.
    /// </summary>
    internal static class CurveTexts
    {
        internal static string Original => Tr.Pick("The original", "Originalul");

        /// <summary>How to get the gamut figure when the profile is missing (R-144).</summary>
        internal static string GamutNeedsProfile => Tr.Pick(
            "paper gamut: unknown - choose the paper's ICC profile with the \"From ICC profile...\" button beside Dmax and run again",
            "gamutul hartiei: necunoscut - alege profilul ICC al hartiei cu butonul \"Din profil ICC...\" de langa Dmax si ruleaza din nou");

        internal static string NoMonotoneCurve => Tr.Pick(
            "No curve can be suggested: none of the variants stays increasing on the anchors of this image.",
            "Nu se poate sugera o curba: niciuna din variante nu ramane crescatoare pe ancorele acestei imagini.");

        internal static string SaveOverInput => Tr.Pick(
            "Not saving over the input image: choose another name or another folder. The input image is never changed.",
            "Nu se salveaza peste imaginea de intrare: alege alt nume sau alt folder. Imaginea de intrare nu se modifica niciodata.");

        /// <summary>
        /// The refusal of a save whose input image is no longer where it was (R-145).
        /// </summary>
        /// <param name="path">The input's full path at the time of the analysis.</param>
        /// <returns>The message, which names the path and asks to open the image again.</returns>
        internal static string InputGone(string path)
            => Tr.Pick("The input image is no longer where it was at the analysis (" + path + "): the save needs it, to keep its header. Open the image again.",
                       "Imaginea de intrare nu mai e unde era la analiza (" + path + "): salvarea are nevoie de ea, ca sa-i pastreze antetul. Deschide imaginea din nou.");

        /// <summary>
        /// The refusal of a save whose curved values the source FITS's integer encoding cannot
        /// hold (R-145).
        /// </summary>
        /// <param name="count">How many samples fall outside the encoding's range.</param>
        /// <param name="bitpix">The source's BITPIX.</param>
        /// <param name="bzero">The source's BZERO, 0 when it has none.</param>
        /// <returns>
        /// The message, which says nothing was saved and how to save a 32-bit source.
        /// </returns>
        internal static string ValuesDoNotFit(long count, int bitpix, double bzero)
            => Tr.Pick($"{Fmt.Int(count)} values do not fit the encoding of the source FITS (BITPIX {bitpix}, BZERO {Fmt.F0(bzero)}): the curve lifts them over its end. "
                       + "Nothing was saved. Save the source from Siril as 32 bits and analyse that one.",
                       $"{Fmt.Int(count)} valori nu incap in codarea FITS-ului sursa (BITPIX {bitpix}, BZERO {Fmt.F0(bzero)}): curba le ridica peste capatul ei. "
                       + "Nu s-a salvat nimic. Salveaza sursa din Siril pe 32 de biti si analizeaz-o pe aceea.");

        /// <summary>
        /// What the curve window says above the table (R-147): how to redo the curve in Siril.
        /// </summary>
        /// <param name="colour">
        /// <c>true</c> for a colour image, which adds that the curve is the same on R, G and B.
        /// </param>
        /// <returns>The caption.</returns>
        internal static string CurveCaption(bool colour)
            => Tr.Pick("A natural cubic spline through points"
                       + (colour ? ", the same curve on R, G and B" : "")
                       + ": in Siril, Curves Transformation, the cubic spline mode, with the points of the table.",
                       "Spline cubic natural prin puncte"
                       + (colour ? ", aceeasi curba pe R, G si B" : "")
                       + ": in Siril, Curves Transformation, modul cubic spline, cu punctele din tabel.");

        /// <summary>What the columns of the points table mean, by the classes' path.</summary>
        /// <param name="method">The path of the classes, which names them in the note.</param>
        /// <param name="colour">
        /// <c>true</c> for a colour image, which adds that the values are the same on R, G and B.
        /// </param>
        /// <returns>The note under the table.</returns>
        internal static string PointsNote(DustMethod method, bool colour)
            => Tr.Pick("The input and the output are pixel values 0..1" + (colour ? ", the same on R, G and B" : "") + ". The first and the last row are the ends of the curve. "
                       + "L* is that of the class's patches, measured before and after the curve"
                       + (method == DustMethod.Band ? " (the classes of the Milky Way band)." : " (classes relative to the modelled sky)."),
                       "Intrarea si iesirea sunt valori 0..1 ale pixelului" + (colour ? ", aceleasi pe R, G si B" : "") + ". Primul si ultimul rand sunt capetele curbei. "
                       + "L* e al dalelor clasei, masurat inainte si dupa curba"
                       + (method == DustMethod.Band ? " (clasele benzii Caii Lactee)." : " (clase relative la cerul modelat)."));

        /// <summary>The heading of the curve window's shadow chart (R-147).</summary>
        /// <param name="limit">
        /// The chart's upper input, <see cref="Thresholds.CurveShadowChartFactor"/> times the
        /// band's input (<see cref="CurveAdvisor.ShadowChartLimit"/>); over 1 when that input is
        /// over 0.8.
        /// </param>
        /// <returns>The heading, with the limit at three decimals.</returns>
        internal static string ShadowHeading(double limit) => Tr.Pick($"THE SHADOWS ENLARGED (0 - {Fmt.F3(limit)}): where the dust and the sky lie", $"UMBRELE MARITE (0 - {Fmt.F3(limit)}): unde stau praful si cerul");

        internal static string Computing => Tr.Pick("computing the curves...", "se calculeaza curbele...");

        /// <summary>
        /// Why no curve is suggested when two neighbouring anchors do not increase (R-141).
        /// </summary>
        /// <param name="lower">The lower class's name.</param>
        /// <param name="lowerValue">The lower class's anchor, 0..1.</param>
        /// <param name="upper">The upper class's name.</param>
        /// <param name="upperValue">The upper class's anchor, 0..1, not over the lower one.</param>
        /// <returns>The reason, which names both anchors.</returns>
        internal static string AnchorsNotIncreasing(string lower, double lowerValue, string upper, double upperValue)
            => Tr.Pick($"No curve can be suggested: the anchor \"{upper}\" ({Fmt.F3(upperValue)}) is not over the anchor \"{lower}\" ({Fmt.F3(lowerValue)}). "
                       + "The curves need five increasing steps: dust, sky, band, bright clouds, peaks.",
                       $"Nu se poate sugera o curba: ancora \"{upper}\" ({Fmt.F3(upperValue)}) nu e peste ancora \"{lower}\" ({Fmt.F3(lowerValue)}). "
                       + "Curbele au nevoie de cinci trepte crescatoare: praf, cer, banda, nori luminosi, varfuri.");

        /// <summary>
        /// Why no curve is suggested when a class has no patch, so no anchor (R-141).
        /// </summary>
        /// <param name="label">The empty class's name.</param>
        /// <returns>The reason, which names the class.</returns>
        internal static string ClassEmpty(string label)
            => Tr.Pick($"No curve can be suggested: the class \"{label}\" has no patch on this image, and the curves need five steps: dust, sky, "
                       + "band or nebula, bright clouds or bright nebula, peaks.",
                       $"Nu se poate sugera o curba: clasa \"{label}\" nu are nicio dala pe imaginea asta, si curbele au nevoie de cinci trepte: praf, cer, "
                       + "banda sau nebuloasa, nori luminosi sau nebuloasa stralucitoare, varfuri.");

        /// <summary>
        /// The peaks already at or over the target (R-142): the image is at the ceiling, and the
        /// curves here lower nothing.
        /// </summary>
        /// <param name="label">The peaks class's name.</param>
        /// <param name="value">
        /// The peaks' anchor, 0..1, at or over <see cref="Thresholds.CurvePeakTarget"/>.
        /// </param>
        /// <returns>The reason, which names the anchor and the target.</returns>
        internal static string PeaksOverTarget(string label, double value)
            => Tr.Pick($"No curve is suggested: the anchor \"{label}\" is already at {Fmt.F3(value)}, at or over the target {Fmt.F2(Thresholds.CurvePeakTarget)} the peaks reach "
                       + "after a curve. There is no room left for contrast without lowering something, and the suggested curves lower no point: the image is already at the ceiling.",
                       $"Nu se sugereaza nicio curba: ancora \"{label}\" e deja la {Fmt.F3(value)}, la sau peste tinta {Fmt.F2(Thresholds.CurvePeakTarget)} la care ajung varfurile "
                       + "dupa o curba. Nu mai e loc de contrast fara sa cobori ceva, iar curbele sugerate nu coboara niciun punct: imaginea e deja la plafon.");

        /// <summary>The evidence line for a profile that gives no gamut figure (R-144).</summary>
        /// <param name="reason">
        /// Why the profile cannot be used, from <see cref="GamutTable.Problem"/>.
        /// </param>
        /// <returns>The line, which ends with the reason.</returns>
        internal static string GamutUnreadable(string reason) => Tr.Pick("paper gamut: unknown - ", "gamutul hartiei: necunoscut - ") + reason;

        /// <summary>
        /// Where the anchors come from, under the gallery (R-146): the Tonuri tab's classes, on
        /// the band path or on the sky path, with its reason.
        /// </summary>
        /// <param name="map">The patch map whose classes gave the anchors.</param>
        /// <param name="methodNote">
        /// Why the band classes were not made (<see cref="SimulationResult.MethodNote"/>), shown on
        /// the sky path; null on the band path.
        /// </param>
        /// <returns>
        /// The sentence, with the percentiles of the band's and the clouds' anchors.
        /// </returns>
        internal static string AnchorSource(DustMap map, string methodNote)
        {
            IReadOnlyList<string> n = map.ClassNames;
            string path = map.Method == DustMethod.Band
                ? Tr.Pick("the classes of the Milky Way band", "clasele benzii Caii Lactee")
                : Tr.Pick($"classes relative to the modelled sky ({methodNote})", $"clase relative la cerul modelat ({methodNote})");
            return Tr.Pick($"The anchors come from the classes of the Tones tab ({path}): the dust and the sky as there, the {n[2]} at the {Fmt.F0(Thresholds.CurveBandClassPercentile)} % percentile of its class, "
                           + $"the {n[3]} at {Fmt.F0(Thresholds.CurveCloudClassPercentile)} %, the {n[4]} at the median.",
                           $"Ancorele vin din clasele tabului Tonuri ({path}): praful si cerul ca acolo, {n[2]} la percentila {Fmt.F0(Thresholds.CurveBandClassPercentile)} % a clasei ei, "
                           + $"{n[3]} la {Fmt.F0(Thresholds.CurveCloudClassPercentile)} %, {n[4]} la mediana.");
        }

        /// <summary>
        /// A curve's paper checks, for print (R-143): an Attention when the sky as it is is under
        /// the visibility threshold or the dust under paper black, and one when the noise after
        /// the curve passes <see cref="Thresholds.NoisePrintHiLsb"/>.
        /// </summary>
        /// <param name="sim">
        /// The analysis's simulation: the "as is" sky and dust, the paper black.
        /// </param>
        /// <param name="noise">
        /// The analysis's noise, whose median sigma the evidence multiplied.
        /// </param>
        /// <param name="e">
        /// The curve's evidence: its noise gain and the noise after it (NaN skips the check).
        /// </param>
        /// <returns>The checks that fail, in that order; empty when both pass.</returns>
        internal static IReadOnlyList<Recommendation> PaperChecks(SimulationResult sim, NoiseResult noise, CurveEvidence e)
        {
            var o = new List<Recommendation>();
            if (sim.SkyBelowVisibility || sim.DustBelowPaperBlack)
                o.Add(new Recommendation(Topic.Background, Verdict.Attention,
                    Tr.Pick("The background is too low for paper: lift it at the stretch, as the Tones tab says, then come back here. The curve does not move the sky and the dust.", "Fundalul e prea jos pentru hartie: ridica-l la stretch, cum spune tabul Tonuri, apoi revino aici. Curba nu muta cerul si praful."),
                    Tr.Pick($"the sky at L* {Fmt.F1(sim.AsIs.LstarSky)} (the visibility threshold L* {Fmt.F0(Thresholds.LowSeparationLstar)}), the dust at L* {Fmt.F1(sim.AsIs.LstarDust)}, "
                            + $"paper black L* {Fmt.F1(sim.PaperBlackLstar)} at Dmax {Fmt.Dmax(sim.Dmax)}",
                            $"cerul la L* {Fmt.F1(sim.AsIs.LstarSky)} (pragul de vizibilitate L* {Fmt.F0(Thresholds.LowSeparationLstar)}), praful la L* {Fmt.F1(sim.AsIs.LstarDust)}, "
                            + $"negrul hartiei L* {Fmt.F1(sim.PaperBlackLstar)} la Dmax {Fmt.Dmax(sim.Dmax)}")));
            if (!double.IsNaN(e.NoiseAfterLsb) && e.NoiseAfterLsb > Thresholds.NoisePrintHiLsb)
                o.Add(new Recommendation(Topic.Denoise, Verdict.Attention,
                    Tr.Pick("After the curve the noise in the sky passes the print target: apply denoise before, or choose a gentler variant.", "Dupa curba zgomotul din cer trece de tinta de print: aplica denoise inainte, sau alege o varianta mai blanda."),
                    Tr.Pick($"sigma {Fmt.F2(noise.SigmaMedianLsb)} LSB x {Fmt.F2(e.NoiseGain)} (the slope of the curve at the sky) = {Fmt.F2(e.NoiseAfterLsb)} LSB; "
                            + $"the print target {Fmt.F1(Thresholds.NoisePrintLoLsb)}-{Fmt.F1(Thresholds.NoisePrintHiLsb)} LSB",
                            $"sigma {Fmt.F2(noise.SigmaMedianLsb)} LSB x {Fmt.F2(e.NoiseGain)} (panta curbei la cer) = {Fmt.F2(e.NoiseAfterLsb)} LSB; "
                            + $"tinta de print {Fmt.F1(Thresholds.NoisePrintLoLsb)}-{Fmt.F1(Thresholds.NoisePrintHiLsb)} LSB")));
            return o;
        }

        /// <summary>The five classes with their L*: "dust 7.9, sky 17.6, ...".</summary>
        /// <param name="labels">The five class names, in the map's order.</param>
        /// <param name="l">The five L*, in the same order; NaN prints as "n/a".</param>
        /// <returns>The pairs, joined by ", ".</returns>
        static string Levels(IReadOnlyList<string> labels, IReadOnlyList<double> l)
            => string.Join(", ", Enumerable.Range(0, 5).Select(i => $"{labels[i]} {Fmt.F1(l[i])}"));

        /// <summary>
        /// A gamut figure as an evidence line (R-144): the share of all the pixels, then per class.
        /// </summary>
        /// <param name="labels">The five class names, in the map's order.</param>
        /// <param name="what">
        /// The line's opening words, which say before or after the curve.
        /// </param>
        /// <param name="g">
        /// The figure; its per-class list may be null, which drops the brackets.
        /// </param>
        /// <returns>The line; a class whose share is NaN prints "n/a".</returns>
        static string GamutLine(IReadOnlyList<string> labels, string what, GamutFigure g)
            => $"{what}: {Fmt.F2(g.PctAll)} % " + Tr.Pick("of the pixels", "din pixeli")
               + (g.PctByClass == null ? "" : " (" + string.Join(", ", Enumerable.Range(0, 5).Select(i => $"{labels[i]} {(double.IsNaN(g.PctByClass[i]) ? "n/a" : Fmt.Pct(g.PctByClass[i]))}")) + ")");

        /// <summary>
        /// A curve's evidence, line by line, as the tab shows it under the image.
        /// </summary>
        /// <param name="s">The suggestion.</param>
        /// <returns>The texts of <see cref="EvidenceRows"/>, without their marks.</returns>
        internal static IReadOnlyList<string> EvidenceLines(CurveSuggestion s) => EvidenceRows(s).Select(r => r.Text).ToArray();

        /// <summary>
        /// The same lines, each with its mark: the paper checks are warnings and the window shows
        /// them in red (R-146). What is a warning is decided here, not in the window.
        /// </summary>
        /// <param name="s">The suggestion.</param>
        /// <returns>
        /// The points, the L* before and after, the noise gain and the clipped share; then each
        /// paper check, marked as a warning; then the gamut before and after, or the note on why
        /// it is missing. The paper checks and the gamut appear only in an evidence made for print.
        /// </returns>
        internal static IReadOnlyList<(string Text, bool Warning)> EvidenceRows(CurveSuggestion s)
        {
            CurveEvidence e = s.Evidence;
            var o = new List<(string, bool)>();
            foreach (string line in Plain(s)) o.Add((line, false));
            foreach (Recommendation r in e.PaperChecks) o.Add(($"! {r.Text} ({Tr.Pick("because", "fiindca")}: {r.Because})", true));
            if (e.Gamut != null)
            {
                o.Add((GamutLine(e.Labels, Tr.Pick($"outside the paper gamut (the chroma moved by over {Fmt.F0(Thresholds.CurveGamutDeltaAb)}), before", $"in afara gamutului hartiei (croma mutata cu peste {Fmt.F0(Thresholds.CurveGamutDeltaAb)}), inainte"), e.GamutBefore), false));
                o.Add((GamutLine(e.Labels, Tr.Pick("outside the paper gamut, after", "in afara gamutului hartiei, dupa"), e.Gamut), false));
            }
            else if (e.GamutNote != null) o.Add((e.GamutNote, false));
            return o;
        }

        /// <summary>
        /// The evidence lines every suggestion has, for print and for the screen.
        /// </summary>
        /// <param name="s">The suggestion.</param>
        /// <returns>
        /// Five lines: the labelled points (input &gt; output), the L* before, the L* after, the
        /// noise gain (with the noise after when it is known) and the clipped share before and
        /// after.
        /// </returns>
        static IReadOnlyList<string> Plain(CurveSuggestion s)
        {
            CurveEvidence e = s.Evidence;
            return new List<string>
            {
                Tr.Pick("points (input > output): ", "puncte (intrare > iesire): ") + string.Join(", ", s.Curve.Points.Where(p => p.Label != null).Select(p => $"{p.Label} {Fmt.F3(p.X)} > {Fmt.F3(p.Y)}")),
                Tr.Pick("L* before: ", "L* inainte: ") + Levels(e.Labels, e.LstarBefore),
                Tr.Pick("L* after: ", "L* dupa: ") + Levels(e.Labels, e.LstarAfter),
                double.IsNaN(e.NoiseAfterLsb)
                    ? Tr.Pick($"the noise in the sky grows by {Fmt.F2(e.NoiseGain)} x (the slope of the curve at the sky)", $"zgomotul din cer creste de {Fmt.F2(e.NoiseGain)} x (panta curbei la cer)")
                    : Tr.Pick($"the noise in the sky grows by {Fmt.F2(e.NoiseGain)} x (the slope of the curve at the sky), to {Fmt.F2(e.NoiseAfterLsb)} LSB", $"zgomotul din cer creste de {Fmt.F2(e.NoiseGain)} x (panta curbei la cer), la {Fmt.F2(e.NoiseAfterLsb)} LSB"),
                Tr.Pick($"clipped pixels (a channel at or over {Fmt.F3(Thresholds.CurveClipLevel)}): {Fmt.F2(e.PctClippedBefore)} % before, {Fmt.F2(e.PctClippedAfter)} % after", $"pixeli arsi (un canal la sau peste {Fmt.F3(Thresholds.CurveClipLevel)}): {Fmt.F2(e.PctClippedBefore)} % inainte, {Fmt.F2(e.PctClippedAfter)} % dupa"),
            };
        }

        /// <summary>
        /// The name proposed for the saved file: the image's name, the suggestion, .fit (R-146).
        /// </summary>
        /// <param name="inputPath">The analysed image's path, as it was given.</param>
        /// <param name="suggestion">
        /// The suggestion's name: "Gentle", "Moderate", "Strong" or "Maximum" (Romanian "Blanda",
        /// "Medie", "Puternica" or "Maxima").
        /// </param>
        /// <returns>A file name without a folder, for example "de_printat_Moderate.fit".</returns>
        internal static string ProposedFileName(string inputPath, string suggestion)
            => Path.GetFileNameWithoutExtension(inputPath) + "_" + suggestion + ".fit";

        /// <summary>
        /// The text of the saved file's HISTORY cards, one piece per card (R-145): the program,
        /// its version and the suggestion, the kind of curve, then the control points as
        /// "x y" pairs at three decimals, each with its label when it has one, as many per piece
        /// as fit in 70 characters; a pair is never split.
        /// </summary>
        /// <param name="suggestion">The suggestion's name.</param>
        /// <param name="curve">The suggestion's curve, whose points are listed.</param>
        /// <returns>The pieces, in order, ASCII.</returns>
        internal static IReadOnlyList<string> History(string suggestion, ToneCurve curve)
        {
            var o = new List<string>
            {
                Tr.Pick($"{VersionInfo.Banner}: curve {suggestion}, on every channel", $"{VersionInfo.Banner}: curba {suggestion}, pe fiecare canal"),
                Tr.Pick("a natural cubic spline (Siril: Curves Transformation, cubic spline)", "spline cubic natural (Siril: Curves Transformation, cubic spline)"),
                Tr.Pick("control points, input output:", "puncte de control, intrare iesire:"),
            };
            string line = "";
            foreach (CurvePoint p in curve.Points)
            {
                string pair = $"{Fmt.F3(p.X)} {Fmt.F3(p.Y)}" + (p.Label != null ? $" ({p.Label})" : "");
                if (line.Length > 0 && line.Length + 2 + pair.Length > 70) { o.Add(line); line = ""; }
                line = line.Length == 0 ? pair : line + "; " + pair;
            }
            if (line.Length > 0) o.Add(line);
            return o;
        }

        /// <summary>The tab's message after a save.</summary>
        /// <param name="path">The written file's path.</param>
        /// <returns>"saved: " ("salvat: ") followed by the path.</returns>
        internal static string Saved(string path) => Tr.Pick("saved: ", "salvat: ") + path;
    }
}
