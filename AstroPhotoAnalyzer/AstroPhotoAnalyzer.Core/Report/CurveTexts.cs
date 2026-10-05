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

// CurveTexts.cs - propozitiile tabului Curbe (R-143, R-144, R-145): dovada unei curbe,
// verificarile de hartie, de unde vin ancorele, textul HISTORY al fisierului salvat.
//
// Fereastra nu scrie niciuna: le arata. Verificarile de hartie sunt Recommendation, cu
// "fiindca" obligatoriu, ca orice recomandare a raportului (ADR 0011).

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal static class CurveTexts
    {
        internal const string Original = "Originalul";

        /// <summary>Cum se obtine cifra de gamut cand lipseste profilul (R-144).</summary>
        internal const string GamutNeedsProfile =
            "gamutul hartiei: necunoscut - alege profilul ICC al hartiei cu butonul \"Din profil ICC...\" de langa Dmax si ruleaza din nou";

        internal const string NoMonotoneCurve = "Nu se poate sugera o curba: niciuna din variante nu ramane crescatoare pe ancorele acestei imagini.";

        internal const string SaveOverInput = "Nu se salveaza peste imaginea de intrare: alege alt nume sau alt folder. Imaginea de intrare nu se modifica niciodata.";

        internal static string InputGone(string path)
            => "Imaginea de intrare nu mai e unde era la analiza (" + path + "): salvarea are nevoie de ea, ca sa-i pastreze antetul. Deschide imaginea din nou.";

        internal static string ValuesDoNotFit(long count, int bitpix, double bzero)
            => $"{Fmt.Int(count)} valori nu incap in codarea FITS-ului sursa (BITPIX {bitpix}, BZERO {Fmt.F0(bzero)}): curba le ridica peste capatul ei. "
               + "Nu s-a salvat nimic. Salveaza sursa din Siril pe 32 de biti si analizeaz-o pe aceea.";

        /// <summary>Ce spune fereastra curbei deasupra tabelului (R-147): cum se reface curba in Siril.</summary>
        internal static string CurveCaption(bool colour)
            => "Spline cubic natural prin puncte"
               + (colour ? ", aceeasi curba pe R, G si B" : "")
               + ": in Siril, Curves Transformation, modul cubic spline, cu punctele din tabel.";

        /// <summary>Ce inseamna coloanele tabelului de puncte, dupa sursa ancorelor.</summary>
        internal static string PointsNote(CurveAnchorSource source, bool colour)
            => "Intrarea si iesirea sunt valori 0..1 ale pixelului" + (colour ? ", aceleasi pe R, G si B" : "") + ". Primul si ultimul rand sunt capetele curbei. "
               + (source == CurveAnchorSource.Classes
                   ? "L* e al dalelor clasei, masurat inainte si dupa curba."
                   : "L* e masurat inainte si dupa curba: praful si cerul pe dale, banda, norii luminosi si varfurile ca percentile ale histogramei cadrului.");

        internal static string ShadowHeading(double limit) => $"UMBRELE MARITE (0 - {Fmt.F3(limit)}): unde stau praful si cerul";

        internal const string Computing = "se calculeaza curbele...";

        internal static string AnchorsNotIncreasing(string lower, double lowerValue, string upper, double upperValue)
            => $"Nu se poate sugera o curba: ancora \"{upper}\" ({Fmt.F3(upperValue)}) nu e peste ancora \"{lower}\" ({Fmt.F3(lowerValue)}). "
               + "Curbele au nevoie de cinci trepte crescatoare: praf, cer, banda, nori luminosi, varfuri.";

        internal static string AnchorAtEnd(string label, double value, bool atBlack)
            => $"Nu se poate sugera o curba: ancora \"{label}\" ({Fmt.F3(value)}) e la {(atBlack ? "negru" : "alb")}, nu mai are loc de un punct de control dincolo de ea.";

        internal static string GamutUnreadable(string reason) => "gamutul hartiei: necunoscut - " + reason;

        /// <summary>De unde vin ancorele, sub galerie (R-146).</summary>
        internal static string AnchorSource(CurveAnchorSource source, string methodNote)
            => source == CurveAnchorSource.Classes
                ? $"Ancorele vin din clasele tabului Tonuri: praful si cerul ca acolo, banda la percentila {Fmt.F0(Thresholds.CurveBandClassPercentile)} % a clasei ei, "
                  + $"norii luminosi la {Fmt.F0(Thresholds.CurveCloudClassPercentile)} %, varfurile la mediana."
                : $"Ancorele de sus vin din histograma cadrului ({methodNote ?? "fara toate clasele"}): banda, norii luminosi si varfurile sunt cei mai luminosi "
                  + $"{Fmt.F1(100 - Thresholds.CurveBandPercentile)} %, {Fmt.F1(100 - Thresholds.CurveCloudPercentile)} % si {Fmt.F1(100 - Thresholds.CurvePeakPercentile)} % din pixeli; praful si cerul, ca in tabul Tonuri.";

        /// <summary>Verificarile de hartie ale unei curbe, pentru print (R-143).</summary>
        internal static IReadOnlyList<Recommendation> PaperChecks(SimulationResult sim, NoiseResult noise, CurveEvidence e)
        {
            var o = new List<Recommendation>();
            if (sim.SkyBelowVisibility || sim.DustBelowPaperBlack)
                o.Add(new Recommendation(Topic.Background, Verdict.Attention,
                    "Fundalul e prea jos pentru hartie: ridica-l la stretch, cum spune tabul Tonuri, apoi revino aici. Curba nu muta cerul si praful.",
                    $"cerul la L* {Fmt.F1(sim.AsIs.LstarSky)} (pragul de vizibilitate L* {Fmt.F0(Thresholds.LowSeparationLstar)}), praful la L* {Fmt.F1(sim.AsIs.LstarDust)}, "
                    + $"negrul hartiei L* {Fmt.F1(sim.PaperBlackLstar)} la Dmax {Fmt.Dmax(sim.Dmax)}"));
            if (!double.IsNaN(e.NoiseAfterLsb) && e.NoiseAfterLsb > Thresholds.NoisePrintHiLsb)
                o.Add(new Recommendation(Topic.Denoise, Verdict.Attention,
                    "Dupa curba zgomotul din cer trece de tinta de print: aplica denoise inainte, sau alege o varianta mai blanda.",
                    $"sigma {Fmt.F2(noise.SigmaMedianLsb)} LSB x {Fmt.F2(e.NoiseGain)} (panta curbei la cer) = {Fmt.F2(e.NoiseAfterLsb)} LSB; "
                    + $"tinta de print {Fmt.F1(Thresholds.NoisePrintLoLsb)}-{Fmt.F1(Thresholds.NoisePrintHiLsb)} LSB"));
            return o;
        }

        static string Levels(IReadOnlyList<double> l)
            => string.Join(", ", Enumerable.Range(0, 5).Select(i => $"{CurveAdvisor.Labels[i]} {Fmt.F1(l[i])}"));

        static string GamutLine(string what, GamutFigure g)
            => $"{what}: {Fmt.F2(g.PctAll)} % din pixeli"
               + (g.PctByClass == null ? "" : " (" + string.Join(", ", Enumerable.Range(0, 5).Select(i => $"{CurveAdvisor.Labels[i]} {(double.IsNaN(g.PctByClass[i]) ? "n/a" : Fmt.Pct(g.PctByClass[i]))}")) + ")");

        /// <summary>Dovada unei curbe, rand cu rand, cum o arata tabul sub imagine.</summary>
        internal static IReadOnlyList<string> EvidenceLines(CurveSuggestion s) => EvidenceRows(s).Select(r => r.Text).ToArray();

        /// <summary>
        /// Aceleasi randuri, fiecare cu semnul lui: verificarile de hartie sunt avertismente
        /// si fereastra le arata cu rosu (R-146). Ce e avertisment se decide aici, nu in fereastra.
        /// </summary>
        internal static IReadOnlyList<(string Text, bool Warning)> EvidenceRows(CurveSuggestion s)
        {
            CurveEvidence e = s.Evidence;
            var o = new List<(string, bool)>();
            foreach (string line in Plain(s)) o.Add((line, false));
            foreach (Recommendation r in e.PaperChecks) o.Add(($"! {r.Text} (fiindca: {r.Because})", true));
            if (e.Gamut != null)
            {
                o.Add((GamutLine($"in afara gamutului hartiei (croma mutata cu peste {Fmt.F0(Thresholds.CurveGamutDeltaAb)}), inainte", e.GamutBefore), false));
                o.Add((GamutLine("in afara gamutului hartiei, dupa", e.Gamut), false));
            }
            else if (e.GamutNote != null) o.Add((e.GamutNote, false));
            return o;
        }

        static IReadOnlyList<string> Plain(CurveSuggestion s)
        {
            CurveEvidence e = s.Evidence;
            return new List<string>
            {
                "puncte (intrare > iesire): " + string.Join(", ", s.Curve.Points.Where(p => p.Label != null).Select(p => $"{p.Label} {Fmt.F3(p.X)} > {Fmt.F3(p.Y)}")),
                "L* inainte: " + Levels(e.LstarBefore),
                "L* dupa: " + Levels(e.LstarAfter),
                double.IsNaN(e.NoiseAfterLsb)
                    ? $"zgomotul din cer creste de {Fmt.F2(e.NoiseGain)} x (panta curbei la cer)"
                    : $"zgomotul din cer creste de {Fmt.F2(e.NoiseGain)} x (panta curbei la cer), la {Fmt.F2(e.NoiseAfterLsb)} LSB",
                $"pixeli arsi (un canal la sau peste {Fmt.F3(Thresholds.CurveClipLevel)}): {Fmt.F2(e.PctClippedBefore)} % inainte, {Fmt.F2(e.PctClippedAfter)} % dupa",
            };
        }

        /// <summary>Numele propus fisierului salvat: numele imaginii, sugestia, .fit (R-146).</summary>
        internal static string ProposedFileName(string inputPath, string suggestion)
            => Path.GetFileNameWithoutExtension(inputPath) + "_" + suggestion + ".fit";

        /// <summary>Textul cardurilor HISTORY ale fisierului salvat, o bucata pe card, fiecare de cel mult 70 de caractere (R-145).</summary>
        internal static IReadOnlyList<string> History(string suggestion, ToneCurve curve)
        {
            var o = new List<string>
            {
                $"{VersionInfo.Banner}: curba {suggestion}, pe fiecare canal",
                "spline cubic natural (Siril: Curves Transformation, cubic spline)",
                "puncte de control, intrare iesire:",
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

        internal static string Saved(string path) => "salvat: " + path;
    }
}
