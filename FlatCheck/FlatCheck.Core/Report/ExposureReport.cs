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

// ExposureReport.cs - ExposureResult -> the console's text. Formatting only.
//
// The suggested corrections are log2(target/centre) stops, where the target is the middle of the
// band - 57 on a panel, 30 on the sky. The choice between the two is no longer made here: it
// comes from r.Bands, that is from exactly the scale the verdict was given on.
//
// Three pieces here are NOT printed in the console - HistogramReading, ChannelNote and
// BandLabel - and neither are the five of the camera's preview (ScreenComparison,
// ScreenReading, NoPreviewReason, PreviewChannelNote, ScreenGap) nor the window's verdict card
// and caption (VerdictWord, Advice, ChannelsSummary, ChannelOrderNote, HistogramCaption). They
// are the text that goes with the window's histograms and its preview comparison, and they sit
// here for the same reason as RunReport.MapLegend: the wording has a single place, even if today
// it has a single reader. In the console they would have no business - a paragraph about the
// shape of a curve the console does not draw. Advice says what Write says, laid out for the
// window.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// The words of <c>--expo</c>: the console report of an <see cref="ExposureResult"/>, and
    /// the sentences the window puts beside its histograms and the camera's preview.
    /// </summary>
    internal static class ExposureReport
    {
        /// <summary>
        /// Writes the console report: the scale, the per-channel medians, the deciding central
        /// median, then either why the file is not a raw flat or the verdict with its
        /// suggested correction in stops.
        /// </summary>
        /// <param name="r">The measured result.</param>
        /// <param name="path">The file's path, as the header prints it.</param>
        /// <param name="w">Where to write.</param>
        internal static void Write(ExposureResult r, string path, TextWriter w)
        {
            w.WriteLine(T($"\n=== EXPOSURE: {path} ===", $"\n=== EXPUNERE: {path} ==="));
            w.WriteLine(T($"    saturation (white): {r.White:F0}   black : {r.Black:F0}   source: {r.ScaleSource}",
                          $"    saturatie (white) : {r.White:F0}   black : {r.Black:F0}   sursa: {r.ScaleSource}"));

            if (r.IsCfa)
            {
                // The colours are written only when the file really said the order. Without the
                // card, the line stays exactly the one from before - the anonymous positions -
                // because the order differs from camera to camera and a guessed label would look
                // measured.
                w.WriteLine(r.Cfa != null
                    ? T($"    GLOBAL median level per CFA channel (the R/G/B order from {r.Cfa.Source}):",
                        $"    nivel median GLOBAL per canal CFA (ordinea R/G/B din {r.Cfa.Source}):")
                    : T("    GLOBAL median level per CFA channel (informative; which is R/G/B is not known):",
                        "    nivel median GLOBAL per canal CFA (informativ; nu stiu care e R/G/B):"));
                for (int i = 0; i < 4; i++)
                {
                    string mk = i == r.BrightestChannel ? T("  <- the brightest", "  <- cel mai luminos") : "";
                    w.WriteLine($"        {ChannelColumn(r, i)}: {ChannelMedianPct(r, i),5:F1}%{mk}");
                }
            }
            else
            {
                w.WriteLine(T("    mono/processed image (no CFA)", "    imagine mono/procesata (fara CFA)"));
            }

            w.WriteLine(T($"    brightest channel, GLOBAL median      : {r.GlobalMedianPct,5:F1}%  (pulled down by the corners)",
                          $"    canal cel mai luminos, mediana GLOBALA : {r.GlobalMedianPct,5:F1}%  (trasa in jos de colturi)"));
            w.WriteLine(T($"    brightest channel, CENTRE 20%         : {r.CentreMedianPct,5:F1}%  <- THIS decides the exposure",
                          $"    canal cel mai luminos, CENTRU 20%      : {r.CentreMedianPct,5:F1}%  <- ASTA decide expunerea"));
            w.WriteLine(T($"    clipping in the centre (>={Th.ExpoClipWhite * 100:F0}% white)  : {r.CentreClipPct:F3}%",
                          $"    clipping in centru (>={Th.ExpoClipWhite * 100:F0}% white)       : {r.CentreClipPct:F3}%"));

            if (!r.IsRaw)
            {
                WriteNotRaw(r, w);
                return;
            }

            w.WriteLine();

            if (r.IsSky) w.WriteLine(T("    [--sky mode: thresholds for sky flats, not for a panel]", "    [mod --sky: praguri pt flats pe cer, nu pe panou]"));
            var (head, lines) = VerdictLines(r);
            w.WriteLine("    VERDICT: " + head);
            foreach (string line in lines) w.WriteLine("    " + line);
        }

        /// <summary>
        /// Why the file does not look like a raw flat (level near zero, or a debayered mosaic),
        /// and how to produce one that does.
        /// </summary>
        /// <param name="r">The result, whose sanity gate failed.</param>
        /// <param name="w">Where to write.</param>
        static void WriteNotRaw(ExposureResult r, TextWriter w)
        {
            w.WriteLine();
            w.WriteLine(T("    !! THE FILE DOES NOT LOOK LIKE A RAW FLAT !!", "    !! FISIERUL NU PARE UN FLAT BRUT !!"));
            foreach (string[] reason in NotRawReasons(r))
                foreach (string line in reason) w.WriteLine("    " + line);
            w.WriteLine();
            w.WriteLine("    " + NotRawTypicalCause);
            w.WriteLine(T("    SOLUTIONS:", "    SOLUTII:"));
            w.WriteLine(T("      - run directly on the NEF (flatcheck reads the raw exactly):",
                          "      - ruleaza direct pe NEF (flatcheck citeste brutul exact):"));
            w.WriteLine("          flatcheck flat.NEF --expo");
            w.WriteLine(T("      - OR make a raw FITS with Siril's Conversion tab (NOT Open+Save),",
                          "      - SAU produ un FITS brut cu tab-ul Conversion din Siril (NU Open+Save),"));
            w.WriteLine(T("        which keeps the CFA mosaic undebayered.",
                          "        care pastreaza mozaicul CFA nedebayerizat."));
        }

        /// <summary>
        /// The sanity gate's reasons, in the console's lines, one entry per reason that fired:
        /// the level near zero (a flat-dark already subtracted, or a normalised image) and the
        /// four CFA channels being alike (a debayered file). The console indents the lines; the
        /// window (<see cref="NotRawAdvice"/>) joins each reason into one paragraph. Shared so the
        /// two cannot drift, as <see cref="VerdictLines"/> does for the verdict.
        /// </summary>
        /// <param name="r">The result, whose sanity gate failed.</param>
        /// <returns>Zero to two reasons, each as its console lines.</returns>
        static List<string[]> NotRawReasons(ExposureResult r)
        {
            var reasons = new List<string[]>();
            if (r.LevelTooLow)
                reasons.Add(new[]
                {
                    T($"The level is ~{r.CentreMedianPct:F1} % (near zero or negative): the flat-dark was already subtracted,",
                      $"Nivelul e ~{r.CentreMedianPct:F1} % (aproape de zero sau negativ): flat-dark-ul a fost deja scazut,"),
                    T("or the image is normalised. The exposure check needs the original level from the camera.",
                      "sau imaginea e normalizata. Verificarea expunerii are nevoie de nivelul original din aparat."),
                });
            if (r.LooksDebayered)
                reasons.Add(new[]
                {
                    T($"The 4 colour channels are almost identical (difference {r.CfaSpread:F3}): the file was",
                      $"Cele 4 canale de culoare sunt aproape identice (diferenta {r.CfaSpread:F3}): fisierul a fost"),
                    T("already processed (debayered). The exposure check needs the raw file, with the channels apart.",
                      "deja prelucrat (debayerizat). Verificarea expunerii are nevoie de fisierul brut, cu canalele separate."),
                });
            return reasons;
        }

        /// <summary>The usual way a flat stops being raw, said in both applications: it was
        /// opened in Siril and saved, which debayers.</summary>
        static string NotRawTypicalCause
            => T("Typical cause: you opened the NEF in Siril and pressed Save (that debayers).",
                 "Cauza tipica: ai deschis NEF-ul in Siril si ai dat Save (asta debayerizeaza).");

        /// <summary>A channel's global median as a percentage of the useful range (black to
        /// white), the figure of the console's per-channel rows and of the window's
        /// <see cref="ChannelsSummary"/>.</summary>
        /// <param name="r">The result.</param>
        /// <param name="i">The Bayer position, 0..3.</param>
        /// <returns>The percentage; 0 at black, 100 at white.</returns>
        static double ChannelMedianPct(ExposureResult r, int i) => (r.ChannelMedians[i] - r.Black) / r.Range * 100;

        /// <summary>
        /// The verdict and what to do next, in the console's words: the one decision tree both
        /// applications print. The console writes the head after "VERDICT:" and each line
        /// indented; the window (<see cref="Advice"/>) puts the head on a line and the rest in one
        /// paragraph. The correction is in stops toward the bands' target, so it comes from the
        /// scale the verdict was given on.
        /// </summary>
        /// <param name="r">The result of a raw flat.</param>
        /// <returns>The verdict's head and the lines that follow it (none on a panel
        /// OK).</returns>
        static (string Head, string[] Lines) VerdictLines(ExposureResult r)
        {
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            ExposureBands b = r.Bands;
            double cen = r.CentreMedianPct;

            if (r.IsSky)
                return r.Verdict switch
                {
                    ExposureVerdict.TooHigh =>
                        (T("TOO HIGH", "PREA SUS") + (clipping ? T(" (the centre is burnt!)", " (centrul e ars!)") : T(" (risk of stars in the frame)", " (risc stele in cadru)")),
                         new[] { T("Lower the exposure.", "Scade expunerea.") }),
                    ExposureVerdict.TooLow =>
                        (T("TOO LOW for a sky flat", "PREA JOS pentru sky flat"),
                         new[] { T($"Raise it by ~{Math.Log2(b.Target / cen):F1} stops (but quickly, the sky is fading).",
                                   $"Creste cu ~{Math.Log2(b.Target / cen):F1} stopuri (dar repede, cerul se stinge).") }),
                    ExposureVerdict.Ok =>
                        (T($"OK for a sky flat  (centre {b.Lo:F0}-{b.Hi:F0}%)", $"OK pentru sky flat  (centru {b.Lo:F0}-{b.Hi:F0}%)"),
                         new[]
                         {
                             T("On a sky flat a modest level is right. The master comes out clean from the number of frames,",
                               "La flat pe cer nivelul modest e corect. Masterul iese curat din numarul de cadre,"),
                             T("not from raising the exposure (you would risk stars). Do NOT push higher.",
                               "nu din a urca expunerea (ai risca stele). NU forta mai sus."),
                         }),
                    _ =>
                        (T("rather high, but acceptable", "cam sus, dar acceptabil"),
                         new[] { T($"Below {Th.ExpoSkyTooHigh:F0}% it is fine; above, you risk stars. Do not go higher.",
                                   $"Sub {Th.ExpoSkyTooHigh:F0}% e ok; peste, risti stele. Nu urca mai mult.") }),
                };

            switch (r.Verdict)
            {
                case ExposureVerdict.TooHigh:
                    {
                        double stops = cen > 0 ? Math.Log2(b.Target / cen) : -1;
                        var lines = new List<string> { T($"Lower the exposure by ~{-stops:F1} stops.", $"Scade expunerea cu ~{-stops:F1} stopuri.") };
                        if (clipping)
                            lines.Add(T("The centre is burnt: you lose the vignetting information exactly where the signal is largest.",
                                        "Centrul e ars: pierzi informatia de vignetare exact unde semnalul e cel mai mare."));
                        return (T("TOO HIGH", "PREA SUS") + (clipping ? T(" (the centre is burnt!)", " (centrul e ars!)") : ""), lines.ToArray());
                    }
                case ExposureVerdict.TooLow:
                    return (T("TOO LOW", "PREA JOS"), new[]
                    {
                        T($"Raise the exposure by ~{Math.Log2(b.Target / cen):F1} stops.",
                          $"Creste expunerea cu ~{Math.Log2(b.Target / cen):F1} stopuri."),
                        T($"Below ~{Th.ExpoTooLow:F0} % in the centre the master comes out noisy.",
                          $"Sub ~{Th.ExpoTooLow:F0} % in centru masterul iese zgomotos."),
                    });
                case ExposureVerdict.AcceptableLow:
                    return (T("ACCEPTABLE, but rather low", "ACCEPTABIL, dar cam jos"),
                            new[] { T($"You could raise it by ~{Math.Log2(b.Target / cen):F1} stops.", $"Ai putea creste cu ~{Math.Log2(b.Target / cen):F1} stopuri.") });
                case ExposureVerdict.Ok:
                    return (T($"OK  (the centre in the target {b.Lo:F0}-{b.Hi:F0}%)", $"OK  (centrul in tinta {b.Lo:F0}-{b.Hi:F0}%)"), Array.Empty<string>());
                default:
                    return (T("ACCEPTABLE, but rather high", "ACCEPTABIL, dar cam sus"),
                            new[] { T($"You could lower it by ~{-Math.Log2(b.Target / cen):F1} stops.", $"Ai putea scadea cu ~{-Math.Log2(b.Target / cen):F1} stopuri.") });
            }
        }

        // =============================================================== for the window
        //
        // What follows is printed nowhere in the console, but ChannelColumn, which Write uses
        // for its per-channel rows. See the note at the head of the file.

        /// <summary>
        /// The left column of a channel: the colour's name when known, plus the position -
        /// which stays visible anyway, so that the line can be tied to the mosaic.
        /// </summary>
        /// <param name="r">The result, with its CFA order.</param>
        /// <param name="i">The Bayer position, 0..3.</param>
        /// <returns>"R  (CFA[0,0])" when the order is known, else "CFA[y,x]".</returns>
        static string ChannelColumn(ExposureResult r, int i)
            => r.Cfa != null
                ? $"{r.ChannelLabel(i),-2} (CFA[{i / 2},{i % 2}])"
                : r.ChannelLabel(i);

        /// <summary>The label of the target band, for the drawing: "tinta 50-65 %".</summary>
        /// <param name="b">The bands the verdict was given on.</param>
        /// <returns>The label, with "sky" in it on the sky scale.</returns>
        internal static string BandLabel(ExposureBands b)
            => b.Sky ? T($"sky target {b.Lo:F0}-{b.Hi:F0} %", $"tinta sky {b.Lo:F0}-{b.Hi:F0} %")
                     : T($"target {b.Lo:F0}-{b.Hi:F0} %", $"tinta {b.Lo:F0}-{b.Hi:F0} %");

        /// <summary>
        /// The short note under a channel's label, or "" when it has nothing to say.
        /// </summary>
        /// <param name="r">The result.</param>
        /// <param name="c">The channel.</param>
        /// <returns>Whether it decides and whether it clips over <see cref="Th.ExpoClip"/>,
        /// joined with "; "; "" for neither.</returns>
        internal static string ChannelNote(ExposureResult r, ChannelLevels c)
        {
            var parts = new List<string>();
            if (c.IsBrightest && r.IsCfa) parts.Add(T("the brightest - decides the exposure", "cel mai luminos - decide expunerea"));
            if (c.ClipPct > Th.ExpoClip) parts.Add(T($"clipping {c.ClipPct:F2} % over the whole channel", $"clipping {c.ClipPct:F2} % pe tot canalul"));
            return string.Join("; ", parts);
        }

        /// <summary>
        /// How to read this file's exposure, in words. It adds no measurement and changes no
        /// verdict: it puts into sentences the figures already on screen, because the link
        /// between them - why the wide curve does NOT mean underexposure, how far it still is to
        /// saturation - does not show from the drawing alone.
        /// </summary>
        /// <param name="r">The result.</param>
        /// <returns>One paragraph, its sentences joined with spaces.</returns>
        internal static string HistogramReading(ExposureResult r)
        {
            if (!r.IsRaw)
                return T("The file is not a raw flat (it is already calibrated or processed), so what "
                         + "shows below is only how its levels are spread, not an exposure. The figures "
                         + "cannot mean anything until you run on the camera's raw file.",
                         "Fisierul nu e un flat brut (e deja calibrat sau prelucrat), deci ce se vede "
                         + "mai jos e doar cum sunt impartite nivelurile lui, nu o expunere. Cifrele nu "
                         + "pot insemna ceva pana nu rulezi pe fisierul brut de la aparat.");

            ChannelLevels c = r.Channels[r.IsCfa ? r.BrightestChannel : 0];
            ExposureBands b = r.Bands;
            var s = new List<string>();

            // 1. who decides
            if (!r.IsCfa)
                s.Add(T("The frame is mono or already processed, so it is measured as a whole, not per channel.",
                        "Cadrul e mono sau deja procesat, deci se masoara pe tot, nu pe canale."));
            else if (c.Colour != '\0')
                s.Add(T($"The brightest channel is {c.Label}, so it decides: it reaches saturation first.",
                        $"Canalul cel mai luminos e {c.Label}, deci el decide: el ajunge primul la saturatie."));
            else
                s.Add(T($"The brightest channel is {c.Label} - its colour is not known - so it "
                        + "decides: it reaches saturation first.",
                        $"Canalul cel mai luminos e {c.Label} - nu se stie ce culoare are - deci el "
                        + "decide: el ajunge primul la saturatie."));

            // 2. where the peak sits against the band
            double cen = r.CentreMedianPct;
            if (cen < b.Lo)
                s.Add(T($"The level at the middle of the frame is at {cen:F1} %, {b.Lo - cen:F1} points below the "
                        + $"{BandLabel(b)}.",
                        $"Nivelul din mijlocul cadrului e la {cen:F1} %, cu {b.Lo - cen:F1} puncte sub "
                        + $"{BandLabel(b)}."));
            else if (cen > b.Hi)
                s.Add(T($"The level at the middle of the frame is at {cen:F1} %, {cen - b.Hi:F1} points above the "
                        + $"{BandLabel(b)}.",
                        $"Nivelul din mijlocul cadrului e la {cen:F1} %, cu {cen - b.Hi:F1} puncte peste "
                        + $"{BandLabel(b)}."));
            else
                s.Add(T($"The level at the middle of the frame is at {cen:F1} %, in the {BandLabel(b)}.",
                        $"Nivelul din mijlocul cadrului e la {cen:F1} %, in {BandLabel(b)}."));

            // 3. how far it still is to the top - or that there is no room left at all
            if (r.CentreClipPct > Th.ExpoClip)
                s.Add(T($"The centre already reaches saturation ({r.CentreClipPct:F3} % of its pixels), "
                        + "so TOO HIGH whatever the average level: that is exactly where the vignetting "
                        + "information is lost, where the signal is largest.",
                        $"Centrul atinge deja saturatia ({r.CentreClipPct:F3} % din pixelii lui), "
                        + "deci PREA SUS oricare ar fi nivelul mediu: acolo se pierde exact informatia "
                        + "de vignetare, unde semnalul e cel mai mare."));
            else if (c.CentreP99Pct > 0)
                s.Add(T($"The brightest 1 % of the centre reaches {c.CentreP99Pct:F1} % of saturation, "
                        + $"so there are ~{Math.Log2(100.0 / c.CentreP99Pct):F1} stops left to it.",
                        $"Cel mai luminos 1 % din centru ajunge la {c.CentreP99Pct:F1} % din saturatie, "
                        + $"deci mai sunt ~{Math.Log2(100.0 / c.CentreP99Pct):F1} stopuri pana la ea."));

            // 4. why the curve in the background is wide
            double drop = cen - c.MedianPct;
            if (drop > 0.5)
                s.Add(T($"The average level over the whole frame is {drop:F1} points below the centre: that is "
                        + "the vignetting, not underexposure, and the wide curve in the background shows it. If "
                        + "you expose by it, you burn the centre.",
                        $"Nivelul mediu pe tot cadrul e cu {drop:F1} puncte sub centru: aceea e "
                        + "vignetarea, nu subexpunere, si curba lata din fundal o arata. Daca "
                        + "expui dupa ea, arzi centrul."));

            // 5. whether anyone else clips too
            if (r.IsCfa)
            {
                var others = r.Channels
                    .Where(x => x.Position != c.Position && x.ClipPct > Th.ExpoClip)
                    .Select(x => x.Label)
                    .ToList();
                if (others.Count > 0)
                    s.Add(T($"Saturated pixels over the whole frame too, not only in the centre: "
                            + $"{string.Join(", ", others)}.",
                            $"Are pixeli saturati si pe tot cadrul, nu doar in centru: "
                            + $"{string.Join(", ", others)}."));
            }

            return string.Join(" ", s);
        }

        // ================================================= the camera's preview
        //
        // For the window too, not printed in the console either. Here the two figures are put
        // side by side - what the sensor measured and what the screen showed - and it says why
        // they differ. Nothing here votes.

        /// <summary>
        /// The raw -> screen pair on each channel, CENTRE against CENTRE: the median of the raw's
        /// 20 % window beside the median of the same window in the preview. The medians of the
        /// whole frame are not compared - the raw's is pulled down by vignetting in a way the
        /// preview's is not, and the difference would partly be the lens.
        ///
        /// The pairing is by the COLOUR of the raw channel, not by its index and not by the
        /// header's mosaic: a raw judged mono has a single channel with no colour, even if the
        /// file said the order. The two greens are averaged against the single G.
        /// With no colour at all on the raw side, a single pair: the brightest with the
        /// brightest, and the label says so.
        /// </summary>
        /// <param name="r">The result, with its preview levels.</param>
        /// <returns>One row per pair: its label, its colour ('\0' for the brightest pair), the
        /// raw's and the screen's central median in percent; empty without a preview.</returns>
        internal static IReadOnlyList<(string Channel, char Colour, double RawPct, double ScreenPct)>
            ScreenComparison(ExposureResult r)
        {
            var rows = new List<(string, char, double, double)>();
            if (r.Preview == null) return rows;

            bool anyColour = r.Channels.Any(c => c.Colour != '\0');
            if (anyColour)
            {
                foreach (ChannelLevels p in r.Preview.Channels)
                {
                    var raws = r.Channels.Where(c => c.Colour == p.Colour).ToList();
                    if (raws.Count == 0) continue;
                    string label = raws.Count == 1
                        ? p.Label
                        : T($"{p.Label} (mean of {string.Join(", ", raws.Select(c => c.Label))})",
                            $"{p.Label} (media {string.Join(", ", raws.Select(c => c.Label))})");
                    rows.Add((label, p.Colour, raws.Average(c => c.CentreMedianPct), p.CentreMedianPct));
                }
                return rows;
            }

            ChannelLevels rawBest = r.Channels[r.IsCfa ? r.BrightestChannel : 0];
            ChannelLevels prevBest = r.Preview.Channels.OrderByDescending(c => c.MedianPct).First();
            rows.Add((T($"the brightest: {rawBest.Label} in the raw, {prevBest.Label} on the screen",
                        $"cel mai luminos: {rawBest.Label} in raw, {prevBest.Label} pe ecran"),
                      '\0', rawBest.CentreMedianPct, prevBest.CentreMedianPct));
            return rows;
        }

        /// <summary>
        /// Why the two differ so much, and what the screen is still good for. It says only what
        /// the result supports: the difference measured on the deciding channel, the mechanism
        /// (gamma plus the tone curve, which no setting removes), why a flat is the least
        /// favourable case, and the useful rule - the screen saturates before the sensor when the
        /// white balance amplifies and the curve reaches 255, so it is a one-way alarm, not a
        /// measuring instrument. Worded as mechanism and observation, not as a law.
        /// </summary>
        /// <param name="r">The result, with its preview levels.</param>
        /// <returns>One paragraph; "" without a preview.</returns>
        internal static string ScreenReading(ExposureResult r)
        {
            if (r.Preview == null) return "";

            var s = new List<string>();
            if (!r.IsRaw)
                s.Add(T("The file is not a raw flat (it is already calibrated or processed), so neither "
                        + "of the two curves is an exposure. The comparison below stays true "
                        + "about the two renderings, nothing more.",
                        "Fisierul nu e un flat brut (e deja calibrat sau prelucrat), deci niciuna "
                        + "din cele doua curbe nu e o expunere. Comparatia de mai jos ramane adevarata "
                        + "despre cele doua prelucrari, atat."));

            ChannelLevels raw = r.Channels[r.IsCfa ? r.BrightestChannel : 0];
            ChannelLevels screen = raw.Colour != '\0'
                ? r.Preview.Channels.FirstOrDefault(c => c.Colour == raw.Colour)
                : null;
            screen ??= r.Preview.Channels.OrderByDescending(c => c.MedianPct).First();

            double gap = screen.CentreMedianPct - raw.CentreMedianPct;
            s.Add(T($"On the deciding channel, the sensor is at {raw.CentreMedianPct:F1} % and the camera's "
                    + $"screen shows {screen.CentreMedianPct:F1} %: {Numbers.Signed(gap, 1)} points, in "
                    + "the same middle of the frame (20 %).",
                    $"Pe canalul care decide, senzorul e la {raw.CentreMedianPct:F1} % si ecranul "
                    + $"aparatului arata {screen.CentreMedianPct:F1} %: {Numbers.Signed(gap, 1)} puncte, in "
                    + "acelasi mijloc al cadrului (20 %)."));

            s.Add(T("Most of it comes from the way the camera makes the JPEG: the brightness steps are "
                    + "spread the way the eye sees, not by how much the sensor receives, and that "
                    + "brightens the low tones. No camera setting removes it; Picture Control Neutral and "
                    + "the contrast at minimum cut only the ADDITIONAL curve. The rest is the tone curve "
                    + "and the white balance, different on each channel.",
                    "Cea mai mare parte vine din felul in care aparatul face JPEG-ul: treptele de "
                    + "luminozitate sunt impartite dupa cum vede ochiul, nu dupa cat primeste senzorul, "
                    + "si asta lumineaza tonurile de jos. Nicio setare din aparat nu scoate asta; Picture "
                    + "Control Neutral si contrastul la minim taie doar curba SUPLIMENTARA. Restul e "
                    + "curba de ton si balansul de alb, diferit pe fiecare canal."));

            s.Add(T("The low and middle tones, exactly where a flat sits, are stretched the most. That is "
                    + "why a flat looks alarming on the screen when it is not, and why the difference is "
                    + "not a constant to remember: it changes with the camera, with the Picture Control "
                    + "and with the light.",
                    "Tonurile de jos si de mijloc, exact unde sta un flat, sunt cele mai intinse. De "
                    + "aceea un flat pare alarmant pe ecran cand nu e, si de aceea diferenta nu e o "
                    + "constanta de tinut minte: se schimba cu aparatul, cu Picture Control si cu lumina."));

            s.Add(T("What the screen is still good for: the JPEG saturates BEFORE the sensor, on the "
                    + "cameras measured here by a third of a stop up to a stop. So if the screen's "
                    + "histogram is not touching the right edge, nothing is burnt for sure. If it is "
                    + "touching, you know nothing: from there on the screen stays put, however far you "
                    + "go over. It is a one-way alarm, not a measuring instrument.",
                    "La ce mai e bun ecranul: JPEG-ul se satureaza INAINTEA senzorului, pe aparatele "
                    + "masurate aici cu o treime de stop pana la un stop. Deci daca histograma de pe "
                    + "ecran nu e lipita de dreapta, sigur nu ai ars nimic. Daca e lipita, nu stii "
                    + "nimic: de acolo incolo ecranul sta pe loc, oricat ai depasi. E o alarma "
                    + "intr-un singur sens, nu un instrument de masura."));

            return string.Join(" ", s);
        }

        /// <summary>Why the comparison cannot be shown on this file.</summary>
        /// <param name="r">The result; not read, the reason is the same for every file.</param>
        /// <returns>One paragraph.</returns>
        internal static string NoPreviewReason(ExposureResult r)
            => T("This file carries no camera preview - a FITS never has one, "
                 + "and some converted raws lose it - or it could not be decoded. There is nothing "
                 + "to put beside the real level, so only the raw data are shown. The verdict is not affected: "
                 + "it is given on them alone anyway.",
                 "Fisierul asta nu poarta o previzualizare a aparatului - un FITS n-are niciodata, "
                 + "iar unele raw-uri convertite o pierd - sau ea nu s-a putut decoda. Nu am ce "
                 + "pune langa nivelul real, deci se arata doar datele brute. Verdictul nu e afectat: "
                 + "el se da oricum numai pe ele.");

        // The verdict card and the drawing's captions of the window. The same verdicts and the
        // same corrections as Write, in the window's layout: paragraphs instead of indented
        // console lines.

        /// <summary>
        /// The verdict in one word, for the window's coloured pill. The strings are those
        /// VerdictPalette colours through Stats.Severity, so they stay in the same vocabulary as
        /// the rest of the application.
        /// </summary>
        /// <param name="r">The result.</param>
        /// <returns>INAPLICABIL, OK, ACCEPTABIL or PROBLEMA.</returns>
        internal static string VerdictWord(ExposureResult r) => r.Verdict switch
        {
            ExposureVerdict.NotRaw => Stats.NotApplicable,
            ExposureVerdict.Ok => "OK",
            ExposureVerdict.AcceptableLow => "ACCEPTABIL",
            ExposureVerdict.AcceptableHigh => "ACCEPTABIL",
            _ => "PROBLEMA",
        };

        /// <summary>
        /// What to do next, for the window's verdict card: the console's verdict and correction
        /// (<see cref="VerdictLines"/>), the head on its own line and the rest as one paragraph;
        /// for a file that is not a raw flat, the sanity gate's explanation in the window's
        /// words, whose way out is the application's, not a console command.
        /// </summary>
        /// <param name="r">The result.</param>
        /// <returns>The advice, on one or more lines.</returns>
        internal static string Advice(ExposureResult r)
        {
            if (!r.IsRaw) return NotRawAdvice(r);

            var (head, lines) = VerdictLines(r);
            head = char.ToUpperInvariant(head[0]) + head.Substring(1);
            return lines.Length == 0 ? head : head + "\n" + string.Join(" ", lines);
        }

        /// <summary>
        /// The sanity gate, for the window: the console's reasons (<see cref="NotRawReasons"/>),
        /// each as one paragraph, and its typical cause - "I opened the NEF in Siril and pressed
        /// Save", which debayers, said exactly so and not only that the file is not good. Only
        /// the way out is the window's: the application in place of the console command.
        /// </summary>
        /// <param name="r">The result, with the gate's two reasons.</param>
        /// <returns>The explanation, in paragraphs.</returns>
        static string NotRawAdvice(ExposureResult r)
        {
            string why = string.Concat(NotRawReasons(r).Select(lines => string.Join(" ", lines) + "\n\n"));

            return T("THE FILE DOES NOT LOOK LIKE A RAW FLAT\n\n", "FISIERUL NU PARE UN FLAT BRUT\n\n") + why
                 + NotRawTypicalCause + "\n\n"
                 + T("Solutions: run directly on the NEF - the application reads the raw exactly - or make a "
                     + "raw FITS with Siril's Conversion tab (NOT Open + Save), which keeps "
                     + "the raw file unprocessed.",
                     "Solutii: ruleaza direct pe NEF - aplicatia citeste brutul exact - sau produ un "
                     + "FITS brut cu tab-ul Conversion din Siril (NU Open + Save), care pastreaza "
                     + "fisierul brut, neprelucrat.");
        }

        /// <summary>The four CFA channels' global medians as a percentage of the useful range,
        /// one per line, the brightest marked - the window's version of Write's channel
        /// rows.</summary>
        /// <param name="r">The result.</param>
        /// <returns>Four lines on a CFA frame; one sentence on a mono or processed one.</returns>
        internal static string ChannelsSummary(ExposureResult r)
        {
            if (!r.IsCfa) return T("mono/processed image (no CFA)", "imagine mono/procesata (fara CFA)");

            var parts = new string[4];
            for (int i = 0; i < 4; i++)
                parts[i] = $"{r.ChannelLabel(i),-8} {ChannelMedianPct(r, i):F1} %"
                         + (i == r.BrightestChannel ? T("  (the brightest)", "  (cel mai luminos)") : "");
            return string.Join("\n", parts);
        }

        /// <summary>
        /// Where the colour order comes from. It is read from the file or not known at all - it
        /// is not guessed. When it is not known, the sentence says it does not even matter for
        /// the verdict, because the limiting one is the brightest channel whatever its colour.
        /// </summary>
        /// <param name="r">The result.</param>
        /// <returns>One or two sentences.</returns>
        internal static string ChannelOrderNote(ExposureResult r)
        {
            if (!r.IsCfa)
                return T("The frame no longer has its colour channels apart, so there is nothing to name.",
                         "Cadrul nu mai are canalele de culoare separate, deci nu e nimic de numit.");

            return r.Cfa != null
                ? T($"The order of the colours is read from the file ({r.Cfa.Source}: {r.Cfa.Letters}), not guessed. "
                    + "The brightest channel reaches saturation first, so it limits the exposure.",
                    $"Ordinea culorilor e citita din fisier ({r.Cfa.Source}: {r.Cfa.Letters}), nu ghicita. "
                    + "Canalul cel mai luminos ajunge primul la saturatie, deci el limiteaza expunerea.")
                : T("Which channel is red, green or blue is not known: the file does not say, and the order "
                    + "differs from camera to camera. Nor does it matter for the verdict: the brightest "
                    + "channel reaches saturation first, so it limits the exposure.",
                    "Nu se stie care canal e rosu, verde sau albastru: fisierul nu o spune, iar ordinea "
                    + "difera de la aparat la aparat. Nici nu conteaza pentru verdict: canalul cel mai "
                    + "luminos ajunge primul la saturatie, deci el limiteaza expunerea.");
        }

        /// <summary>The caption under the window's histograms, for the raw data or for the
        /// camera's preview, which has no target band.</summary>
        /// <param name="preview">True when the preview's histograms are shown.</param>
        /// <returns>One paragraph.</returns>
        internal static string HistogramCaption(bool preview)
            => preview
                ? T("The horizontal axis is in % of 255. Pale curve = the whole frame, solid curve = "
                    + "the centre (20 %), vertical line = the centre's average level. NO target band: on "
                    + "the camera's screen there is no target, nor could there be one. The red "
                    + "stripe on the right = pixels at 255, that is where the SCREEN saturates, not "
                    + "the sensor.",
                    "Axa orizontala e in % din 255. Curba palida = tot cadrul, curba plina = "
                    + "centrul (20 %), linia verticala = nivelul mediu al centrului. FARA banda tinta: pe "
                    + "ecranul aparatului nu exista o tinta, si nici n-ar putea exista. Dunga "
                    + "rosie din dreapta = pixeli la 255, adica aici se satureaza ECRANUL, nu "
                    + "senzorul.")
                : T("The horizontal axis is in percent of saturation (the sensor's useful range). Pale "
                    + "curve = the whole channel, solid curve = the centre (20 %), vertical line = the centre's "
                    + "average level, that is the figure that decides. Green band = the target, red stripe on the "
                    + $"right = burnt pixels, above {Th.ExpoClipWhite * 100:F0} % of saturation.",
                    "Axa orizontala e in procente din saturatie (intervalul util al senzorului). Curba "
                    + "palida = tot canalul, curba plina = centrul (20 %), linia verticala = nivelul mediu "
                    + "al centrului, adica cifra care decide. Banda verde = tinta, dunga rosie din "
                    + $"dreapta = pixeli arsi, peste {Th.ExpoClipWhite * 100:F0} % din saturatie.");

        /// <summary>The note under a channel of the camera's preview: its share of saturated
        /// pixels, when it is over <see cref="Th.ExpoClip"/>. Nothing else - on the preview
        /// nothing decides.</summary>
        /// <param name="c">A channel of the preview.</param>
        /// <returns>The note, or "" when the channel does not clip.</returns>
        internal static string PreviewChannelNote(ChannelLevels c)
            => c.ClipPct > Th.ExpoClip ? T($"pixels at 255: {c.ClipPct:F2} % of the frame", $"pixeli la 255: {c.ClipPct:F2} % din cadru") : "";

        /// <summary>The difference between the screen and the raw on a channel, for the raw /
        /// screen table.</summary>
        /// <param name="rawPct">The raw's central median, in percent of white.</param>
        /// <param name="screenPct">The preview's central median, in percent of 255.</param>
        /// <returns>The signed difference in whole points: "+52 puncte"; "+0 puncte" for what
        /// rounds to zero (R-129).</returns>
        internal static string ScreenGap(double rawPct, double screenPct)
            => T($"{Numbers.Signed(screenPct - rawPct, 0)} points", $"{Numbers.Signed(screenPct - rawPct, 0)} puncte");
    }
}
