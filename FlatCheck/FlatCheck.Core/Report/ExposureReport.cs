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
// BandLabel - and neither are the three of the camera's preview (ScreenComparison,
// ScreenReading, NoPreviewReason). They are the text that goes with the window's histograms
// and its preview comparison, and they sit here for the same reason as RunReport.MapLegend: the
// wording has a single place, even if today it has a single reader. In the console they would
// have no business - a paragraph about the shape of a curve the console does not draw.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            w.WriteLine($"\n=== EXPUNERE: {path} ===");
            w.WriteLine($"    saturatie (white) : {r.White:F0}   black : {r.Black:F0}   sursa: {r.ScaleSource}");

            if (r.IsCfa)
            {
                // The colours are written only when the file really said the order. Without the
                // card, the line stays exactly the one from before - the anonymous positions -
                // because the order differs from camera to camera and a guessed label would look
                // measured.
                w.WriteLine(r.Cfa != null
                    ? $"    nivel median GLOBAL per canal CFA (ordinea R/G/B din {r.Cfa.Source}):"
                    : "    nivel median GLOBAL per canal CFA (informativ; nu stiu care e R/G/B):");
                for (int i = 0; i < 4; i++)
                {
                    string mk = i == r.BrightestChannel ? "  <- cel mai luminos" : "";
                    w.WriteLine($"        {ChannelColumn(r, i)}: {(r.ChannelMedians[i] - r.Black) / r.Range * 100,5:F1}%{mk}");
                }
            }
            else
            {
                w.WriteLine("    imagine mono/procesata (fara CFA)");
            }

            w.WriteLine($"    canal cel mai luminos, mediana GLOBALA : {r.GlobalMedianPct,5:F1}%  (trasa in jos de colturi)");
            w.WriteLine($"    canal cel mai luminos, CENTRU 20%      : {r.CentreMedianPct,5:F1}%  <- ASTA decide expunerea");
            w.WriteLine($"    clipping in centru (>=98% white)       : {r.CentreClipPct:F3}%");

            if (!r.IsRaw)
            {
                WriteNotRaw(r, w);
                return;
            }

            w.WriteLine();

            if (r.IsSky) WriteSky(r, w);
            else WritePanel(r, w);
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
            w.WriteLine("    !! FISIERUL NU PARE UN FLAT BRUT !!");
            if (r.LevelTooLow)
            {
                w.WriteLine($"    Nivelul e ~{r.CentreMedianPct:F1}% (langa zero/negativ) -> flat-dark deja scazut,");
                w.WriteLine("    sau imagine normalizata. --expo are nevoie de nivelul ADU original.");
            }
            if (r.LooksDebayered)
            {
                w.WriteLine($"    Cele 4 canale CFA sunt quasi-identice (spread {r.CfaSpread:F3}) -> fisierul");
                w.WriteLine("    e DEBAYERIZAT. --expo are nevoie de mozaicul Bayer intact.");
            }
            w.WriteLine();
            w.WriteLine("    Cauza tipica: ai deschis NEF-ul in Siril si ai dat Save (asta debayerizeaza).");
            w.WriteLine("    SOLUTII:");
            w.WriteLine("      - ruleaza direct pe NEF (flatcheck citeste brutul exact):");
            w.WriteLine("          flatcheck flat.NEF --expo");
            w.WriteLine("      - SAU produ un FITS brut cu tab-ul Conversion din Siril (NU Open+Save),");
            w.WriteLine("        care pastreaza mozaicul CFA nedebayerizat.");
        }

        /// <summary>The verdict and its advice on the sky scale (<c>--sky</c>).</summary>
        /// <param name="r">The result of a sky flat.</param>
        /// <param name="w">Where to write.</param>
        static void WriteSky(ExposureResult r, TextWriter w)
        {
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            ExposureBands b = r.Bands;
            w.WriteLine("    [mod --sky: praguri pt flats pe cer, nu pe panou]");
            switch (r.Verdict)
            {
                case ExposureVerdict.TooHigh:
                    w.WriteLine("    VERDICT: PREA SUS" + (clipping ? " (CLIPPING in centru!)" : " (risc stele in cadru)"));
                    w.WriteLine("    Scade expunerea.");
                    break;
                case ExposureVerdict.TooLow:
                    w.WriteLine("    VERDICT: PREA JOS pentru sky flat");
                    w.WriteLine($"    Creste cu ~{Math.Log2(b.Target / r.CentreMedianPct):F1} stopuri (dar repede, cerul se stinge).");
                    break;
                case ExposureVerdict.Ok:
                    w.WriteLine($"    VERDICT: OK pentru sky flat  (centru {b.Lo:F0}-{b.Hi:F0}%)");
                    w.WriteLine("    La sky flat nivelul modest e corect. SNR-ul vine din numarul de cadre,");
                    w.WriteLine("    nu din a urca expunerea (ai risca stele). NU forta mai sus.");
                    break;
                default:
                    w.WriteLine("    VERDICT: cam sus, dar acceptabil");
                    w.WriteLine("    Sub 60% e ok; peste, risti stele. Nu urca mai mult.");
                    break;
            }
        }

        /// <summary>The verdict and its correction in stops on the panel scale.</summary>
        /// <param name="r">The result of a panel flat.</param>
        /// <param name="w">Where to write.</param>
        static void WritePanel(ExposureResult r, TextWriter w)
        {
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            ExposureBands b = r.Bands;
            double cen = r.CentreMedianPct;
            switch (r.Verdict)
            {
                case ExposureVerdict.TooHigh:
                    {
                        double stops = cen > 0 ? Math.Log2(b.Target / cen) : -1;
                        w.WriteLine("    VERDICT: PREA SUS" + (clipping ? " (CLIPPING in centru!)" : ""));
                        w.WriteLine($"    Scade expunerea cu ~{-stops:F1} stopuri.");
                        if (clipping)
                            w.WriteLine("    Centrul clipeaza -> pierzi info de vignetare exact unde semnalul e maxim.");
                        break;
                    }
                case ExposureVerdict.TooLow:
                    w.WriteLine("    VERDICT: PREA JOS");
                    w.WriteLine($"    Creste expunerea cu ~{Math.Log2(b.Target / cen):F1} stopuri.");
                    w.WriteLine("    Sub ~30% in centru masterul iese zgomotos (zgomot fixed-pattern).");
                    break;
                case ExposureVerdict.AcceptableLow:
                    w.WriteLine("    VERDICT: ACCEPTABIL, dar cam jos");
                    w.WriteLine($"    Ai putea creste cu ~{Math.Log2(b.Target / cen):F1} stopuri.");
                    break;
                case ExposureVerdict.Ok:
                    w.WriteLine($"    VERDICT: OK  (centrul in tinta {b.Lo:F0}-{b.Hi:F0}%)");
                    break;
                default:
                    w.WriteLine("    VERDICT: ACCEPTABIL, dar cam sus");
                    w.WriteLine($"    Ai putea scadea cu ~{-Math.Log2(b.Target / cen):F1} stopuri.");
                    break;
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
            => b.Sky ? $"tinta sky {b.Lo:F0}-{b.Hi:F0} %" : $"tinta {b.Lo:F0}-{b.Hi:F0} %";

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
            if (c.IsBrightest && r.IsCfa) parts.Add("cel mai luminos - decide expunerea");
            if (c.ClipPct > Th.ExpoClip) parts.Add($"clipping {c.ClipPct:F2} % pe tot canalul");
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
                return "Fisierul nu e un flat brut - e deja calibrat sau debayerizat - deci ce se "
                     + "vede mai jos e distributia lui de niveluri, nu o expunere. Cifrele n-au "
                     + "cum sa insemne ceva pana nu rulezi pe cadrul brut de la aparat.";

            ChannelLevels c = r.Channels[r.IsCfa ? r.BrightestChannel : 0];
            ExposureBands b = r.Bands;
            var s = new List<string>();

            // 1. who decides
            if (!r.IsCfa)
                s.Add("Cadrul e mono sau deja procesat, deci se masoara pe tot, nu pe canale.");
            else if (c.Colour != '\0')
                s.Add($"Canalul cel mai luminos e {c.Label}, deci el decide: el clipeaza primul.");
            else
                s.Add($"Canalul cel mai luminos e {c.Label} - nu stiu ce culoare are - deci el "
                    + "decide: el clipeaza primul.");

            // 2. where the peak sits against the band
            double cen = r.CentreMedianPct;
            if (cen < b.Lo)
                s.Add($"Varful ferestrei centrale e la {cen:F1} %, cu {b.Lo - cen:F1} puncte sub "
                    + $"{BandLabel(b)}.");
            else if (cen > b.Hi)
                s.Add($"Varful ferestrei centrale e la {cen:F1} %, cu {cen - b.Hi:F1} puncte peste "
                    + $"{BandLabel(b)}.");
            else
                s.Add($"Varful ferestrei centrale e la {cen:F1} %, in {BandLabel(b)}.");

            // 3. how far it still is to the top - or that there is no room left at all
            if (r.CentreClipPct > Th.ExpoClip)
                s.Add($"Centrul atinge deja saturatia ({r.CentreClipPct:F3} % din pixelii lui), "
                    + "deci PREA SUS oricare ar fi mediana - acolo se pierde exact informatia "
                    + "de vignetare, unde semnalul e maxim.");
            else if (c.CentreP99Pct > 0)
                s.Add($"Cel mai luminos 1 % din centru ajunge la {c.CentreP99Pct:F1} % din white, "
                    + $"deci mai sunt ~{Math.Log2(100.0 / c.CentreP99Pct):F1} stopuri pana la saturatie.");

            // 4. why the curve in the background is wide
            double drop = cen - c.MedianPct;
            if (drop > 0.5)
                s.Add($"Mediana intregului canal e cu {drop:F1} puncte sub centru - aia e "
                    + "vignetarea, nu subexpunere, si curba lata din fundal o arata. Daca "
                    + "expui dupa ea, clipezi centrul.");

            // 5. whether anyone else clips too
            if (r.IsCfa)
            {
                var others = r.Channels
                    .Where(x => x.Position != c.Position && x.ClipPct > Th.ExpoClip)
                    .Select(x => x.Label)
                    .ToList();
                if (others.Count > 0)
                    s.Add($"Are pixeli saturati si pe tot cadrul, nu doar in centru: "
                        + $"{string.Join(", ", others)}.");
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
                        : $"{p.Label} (media {string.Join(", ", raws.Select(c => c.Label))})";
                    rows.Add((label, p.Colour, raws.Average(c => c.CentreMedianPct), p.CentreMedianPct));
                }
                return rows;
            }

            ChannelLevels rawBest = r.Channels[r.IsCfa ? r.BrightestChannel : 0];
            ChannelLevels prevBest = r.Preview.Channels.OrderByDescending(c => c.MedianPct).First();
            rows.Add(($"cel mai luminos: {rawBest.Label} in raw, {prevBest.Label} pe ecran",
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
                s.Add("Fisierul nu e un flat brut - e deja calibrat sau debayerizat - deci niciuna "
                    + "din cele doua curbe nu e o expunere. Comparatia de mai jos ramane adevarata "
                    + "despre cele doua developari, atat.");

            ChannelLevels raw = r.Channels[r.IsCfa ? r.BrightestChannel : 0];
            ChannelLevels screen = raw.Colour != '\0'
                ? r.Preview.Channels.FirstOrDefault(c => c.Colour == raw.Colour)
                : null;
            screen ??= r.Preview.Channels.OrderByDescending(c => c.MedianPct).First();

            double gap = screen.CentreMedianPct - raw.CentreMedianPct;
            s.Add($"Pe canalul care decide, senzorul e la {raw.CentreMedianPct:F1} % si ecranul "
                + $"aparatului arata {screen.CentreMedianPct:F1} %: {gap:+0.0;-0.0} puncte, in "
                + "aceeasi fereastra centrala de 20 %.");

            s.Add("Cea mai mare parte e gama: un JPEG e codificat gamma prin definitie, ca sa "
                + "imparta 256 de trepte dupa cum vede ochiul, nu dupa cat primeste senzorul. "
                + "Nicio setare din aparat n-o scoate - Picture Control Neutral si contrastul "
                + "la minim taie curba SUPLIMENTARA, nu codificarea. Restul e curba de ton si "
                + "balansul de alb, care e diferit pe fiecare canal.");

            s.Add("O curba compresiva intinde cel mai tare nivelurile de jos si de mijloc, "
                + "adica exact unde sta un flat. De aia un flat pare alarmant pe ecran cand nu e, "
                + "si de aia diferenta nu e o constanta de tinut minte: se schimba cu aparatul, "
                + "cu Picture Control si cu lumina.");

            s.Add("La ce mai e bun ecranul: cat timp balansul de alb amplifica un canal si curba "
                + "de ton ajunge la 255, JPEG-ul se satureaza INAINTEA senzorului - pe aparatele "
                + "masurate aici, cu o treime de stop pana la un stop. Deci \"nu e lipit de "
                + "margine\" e o garantie solida ca n-ai ars nimic. \"E lipit\" nu e informatie: de "
                + "acolo incolo ecranul sta pe loc, oricat ai depasi. E o alarma intr-un singur "
                + "sens, nu un instrument de masura.");

            return string.Join(" ", s);
        }

        /// <summary>Why the comparison cannot be shown on this file.</summary>
        /// <param name="r">The result; not read, the reason is the same for every file.</param>
        /// <returns>One paragraph.</returns>
        internal static string NoPreviewReason(ExposureResult r)
            => "Fisierul asta nu poarta o previzualizare a aparatului - un FITS n-are niciodata, "
             + "iar unele raw-uri convertite o pierd - sau ea nu s-a putut decoda. Nu am ce "
             + "pune langa nivelul real, deci se arata doar datele brute. Verdictul nu e afectat: "
             + "el se da oricum numai pe ele.";
    }
}
