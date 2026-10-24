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

// SmoothReport.cs - SmoothResult -> the console's text. Formatting only.
//
// The mode has no verdict: the file is written anyway. What the report says is WHAT WAS REMOVED -
// the only figure by which the person can decide whether the file can be trusted - and the
// structure battery before and after, so that it shows that what belonged to the lens stayed.
// The screen texts (Caveat, SirilNote, the refusals) sit here too, as values, by the usual rule:
// no sentence for the user is written in a ViewModel.

using System.IO;
using System.Linq;

namespace FlatCheck
{
    /// <summary>
    /// The words of <c>--smooth</c>: the console report of a <see cref="SmoothResult"/>, the
    /// refusals, the scale proposal's report, and the texts the window shows beside them.
    /// </summary>
    internal static class SmoothReport
    {
        /// <summary>
        /// Writes the console report: the frame, the scale, the tiles rejected, the texture,
        /// what was removed, the five structure tests before and after, and the two notes.
        /// </summary>
        /// <param name="r">The smoothing result.</param>
        /// <param name="path">The input's path, as the header prints it.</param>
        /// <param name="w">Where to write.</param>
        internal static void Write(SmoothResult r, string path, TextWriter w)
        {
            w.WriteLine($"\n=== NETEZIRE: {path} ===");
            w.WriteLine($"    cadru              : {FrameLine(r)}");
            w.WriteLine($"    scara de netezire  : {ScaleLine(r)}");
            w.WriteLine($"    dale respinse      : {RejectedLine(r)}");
            w.WriteLine($"    textura fina       : {TextureLine(r)}");

            w.WriteLine($"\n  CE S-A SCOS (original / netezit, la rezolutia de analiza {r.Width} x {r.Height})");
            foreach (string line in RemovedLines(r)) w.WriteLine("    " + line);
            string warning = Warning(r);
            if (warning != null) Indent(warning, w);

            w.WriteLine("\n  INAINTE / DUPA - cele cinci teste de structura");
            foreach (string key in StructureReport.TestKeys)
            {
                var (vb, bb) = StructureReport.Test(r.Before, key);
                var (va, ba) = StructureReport.Test(r.After, key);
                w.WriteLine($"    {StructureReport.TestLabel(key),-24}: {vb,5:F2} % [{bb}]  ->  {va,5:F2} % [{ba}]");
            }
            w.WriteLine($"    {"VERDICT",-24}: {r.Before.Verdict}  ->  {r.After.Verdict}");

            w.WriteLine();
            Indent("INFORMATIV: " + Caveat(), w);
            Indent(SirilNote(), w);
        }

        /// <summary>
        /// "6016 x 4016, CFA (4 pozitii Bayer netezite separat)" / "..., mono/procesat".
        /// </summary>
        /// <param name="r">The smoothing result.</param>
        /// <returns>The input's size and kind.</returns>
        internal static string FrameLine(SmoothResult r)
            => $"{r.SourceW} x {r.SourceH}, " + (r.IsCfa
                ? "CFA (cele 4 pozitii Bayer netezite separat)"
                : "mono/procesat (fara mozaic CFA)");

        /// <summary>"300 px pe senzor (dale de 150 px, nucleu gaussian sigma 2.0 dale)".</summary>
        /// <param name="r">The smoothing result.</param>
        /// <returns>The scale, the tile side and the kernel's sigma.</returns>
        internal static string ScaleLine(SmoothResult r)
            => $"{r.ScalePx:F0} px pe senzor (dale de {r.TilePx} px, nucleu gaussian sigma {r.SigmaTiles:F1} dale)";

        /// <summary>
        /// "1.19 % inainte -> 0.11 % dupa (structura sub ~128 px, fara zgomotul de pixel)".
        /// </summary>
        /// <param name="r">The smoothing result.</param>
        /// <returns>The fine texture before and after, in percent.</returns>
        internal static string TextureLine(SmoothResult r)
            => $"{r.InputTexturePct:F2} % inainte -> {r.OutputTexturePct:F2} % dupa (structura sub ~128 px pe senzor, fara zgomotul de pixel)";

        /// <summary>"niciuna din 1107 pe pozitie" / "66, 58, 57, 69 din 1107 pe pozitie".</summary>
        /// <param name="r">The smoothing result.</param>
        /// <returns>The rejected tiles per position, the clip floor, and the tiles left without a
        /// fit when there are any.</returns>
        internal static string RejectedLine(SmoothResult r)
        {
            int tiles = r.Positions[0].Tiles;
            string counts = r.Positions.All(p => p.Rejected == 0)
                ? "niciuna"
                : string.Join(", ", r.Positions.Select(p => p.Rejected.ToString()));
            string per = r.IsCfa ? " pe pozitie" : "";
            string failed = r.Positions.Sum(p => p.FitFailed) > 0
                ? $"; {r.Positions.Sum(p => p.FitFailed)} dale fara fit (au ramas la mediana lor)"
                : "";
            return $"{counts} din {tiles}{per} (praf, pete, textura peste {SmoothAnalyzer.ClipFloor * 100:F0} %){failed}";
        }

        /// <summary>The four figures about the removed component, one row each.</summary>
        /// <param name="r">The smoothing result.</param>
        /// <returns>Four rows: p1/p99 per pixel, the large-scale ptp, the worst tile, the deepest
        /// spot.</returns>
        internal static string[] RemovedLines(SmoothResult r) => new[]
        {
            $"p1 / p99 pe pixel        : {r.RemovedP1Pct:+0.00;-0.00} % / {r.RemovedP99Pct:+0.00;-0.00} %  (zgomotul de pixel intra aici)",
            $"structura la scara mare  : {r.RemovedPtpPct,5:F2} %  (p95 - p5 pe dale)",
            $"dala cea mai abatuta     : {r.RemovedMaxTilePct,5:F2} %",
            $"pata cea mai adanca      : {StructureReport.LocalSpotValue(r.RemovedSpotPct, r.RemovedSpotX, r.RemovedSpotY)}",
        };

        /// <summary>
        /// The warning when more than the texture was removed, or null. The threshold is read
        /// from Th, not written, so that the text does not fall behind a recalibration.
        /// </summary>
        /// <param name="r">The smoothing result.</param>
        /// <returns>Four lines joined by '\n' when <see cref="SmoothResult.RemovedFlagged"/>, else
        /// null.</returns>
        internal static string Warning(SmoothResult r)
            => !r.RemovedFlagged ? null
             : $"ATENTIE: dala cea mai abatuta a componentei scoase are {r.RemovedMaxTilePct:F2} %, peste {Th.SmoothKeptWarn:F0} %.\n"
             + "Netezirea a luat si ceva de scara mare, nu doar textura: praf adanc, o pata, sau o\n"
             + "structura reala mai ingusta decat scara aleasa. Uita-te in harta componentei scoase\n"
             + "inainte sa folosesti fisierul; o scara mai mica pastreaza mai mult.";

        /// <summary>
        /// What the file means, and what it CANNOT do. It sits in the report and under the map.
        /// </summary>
        /// <returns>Six lines joined by '\n'.</returns>
        internal static string Caveat()
            => "fisierul netezit pastreaza TOT ce e neted in master si scoate tot ce nu e. Nu stie\n"
             + "ce e al obiectivului si ce e al sursei: petele unui monitor, gradientul cerului sau\n"
             + "colturile inegale ale unui panou raman, fiindca sunt netede; doar --ratio le desparte.\n"
             + "Ce scoate - textura, praful, petele compacte - poate fi si ceva dorit: o umbra de\n"
             + "praf e corecta pentru sesiunea ale carei lights o au si ele. O structura reala mai\n"
             + "ingusta decat scara (~300 px) e luata partial; scara e singurul buton.";

        /// <summary>
        /// How the file gets into Siril: the scripts do not take it by themselves.
        /// </summary>
        /// <returns>Two lines joined by '\n'.</returns>
        internal static string SirilNote()
            => "Scripturile Siril numesc pp_flat_stacked explicit, deci nu iau fisierul netezit singure:\n"
             + "redenumeste-l in locul masterului (pastreaza originalul) sau da-l la calibrate cu -flat=.";

        /// <summary>The refusal for a camera raw.</summary>
        /// <param name="path">The refused file's path, as printed.</param>
        /// <returns>Three lines joined by '\n'.</returns>
        internal static string RefusalRaw(string path)
            => $"NETEZIRE: {path} e un raw de aparat - un singur cadru, necalibrat.\n"
             + "Modul vrea masterul de flat stivuit si calibrat (pp_flat_stacked.fit). Pentru un\n"
             + "raw exista --expo.";

        /// <summary>The refusal for a colour cube (NAXIS3 &gt; 1).</summary>
        /// <param name="path">The refused file's path, as printed.</param>
        /// <param name="planes">How many colour planes the file has.</param>
        /// <returns>Three lines joined by '\n'.</returns>
        internal static string RefusalCube(string path, int planes)
            => $"NETEZIRE: {path} are {planes} planuri de culoare (debayerizat).\n"
             + "Cititorul le-ar media intr-un singur raster, si scris inapoi ca mono fisierul ar\n"
             + "pierde doua canale. Stivuieste flat-urile ca CFA (fara debayerizare) si reia.";

        // ------------------------------------------------------- the scale proposal

        /// <summary>
        /// Writes the report of <c>--scale auto</c>: the input's texture, one row per step tried,
        /// the conclusion, why, and what the rule cannot know.
        /// </summary>
        /// <param name="a">The proposal.</param>
        /// <param name="path">The input's path, as the header prints it.</param>
        /// <param name="w">Where to write.</param>
        internal static void WriteAdvice(SmoothScaleAdvice a, string path, TextWriter w)
        {
            w.WriteLine($"\n=== NETEZIRE, propunere de scara: {path} ===");
            w.WriteLine($"    textura fina intrare : {InputTextureLine(a)}");
            if (a.Trials.Count > 0)
            {
                w.WriteLine("\n  INCERCARI (cate o netezire pe treapta)");
                foreach (SmoothScaleTrial t in a.Trials)
                    w.WriteLine("    " + TrialLine(t));
            }
            w.WriteLine();
            w.WriteLine("  " + AdviceVerdict(a));
            Indent(AdviceNote(a), w);
            w.WriteLine();
            Indent(AdviceCaveat(), w);
        }

        /// <summary>
        /// "0.69 % (peste 0.20 %: e de netezit)" / "0.05 % (sub 0.20 %: nimic de netezit)".
        /// </summary>
        /// <param name="a">The proposal.</param>
        /// <returns>The input's texture against <see cref="Th.SmoothTextureMin"/>.</returns>
        internal static string InputTextureLine(SmoothScaleAdvice a)
            => $"{a.InputTexturePct:F2} % " + (a.Kind == SmoothAdviceKind.NoTexture
                ? $"(sub {Th.SmoothTextureMin:F2} %: nimic de netezit)"
                : $"(peste {Th.SmoothTextureMin:F2} %: e de netezit)");

        /// <summary>One row per step: its figures and its conclusion.</summary>
        /// <param name="t">The step tried.</param>
        /// <returns>The row.</returns>
        internal static string TrialLine(SmoothScaleTrial t)
            => $"scara {t.ScalePx,4:F0} px : textura ramasa {t.TextureAfterPct:F2} %, "
             + $"asimetrie {t.AzimuthalDelta:+0.00;-0.00}, inclinare {t.TiltDelta:+0.00;-0.00}, "
             + $"scos {t.RemovedPtpPct:F2} % ptp  -> {TrialVerdict(t)}";

        /// <summary>A step's conclusion in a few words.</summary>
        /// <param name="t">The step tried.</param>
        /// <returns>That it takes real structure, that the texture stays, or that it
        /// fits.</returns>
        internal static string TrialVerdict(SmoothScaleTrial t)
            => t.StructureLost ? "ia si structura reala"
             : !t.TextureGone ? "textura ramane"
             : "potrivita";

        /// <summary>The conclusion line, for the three answers.</summary>
        /// <param name="a">The proposal.</param>
        /// <returns>The proposed scale, "nothing to smooth", or undetermined.</returns>
        internal static string AdviceVerdict(SmoothScaleAdvice a) => a.Kind switch
        {
            SmoothAdviceKind.Proposed => $"SCARA PROPUSA: {a.ScalePx:F0} px",
            SmoothAdviceKind.NoTexture => "NIMIC DE NETEZIT",
            _ => "NEDETERMINAT: textura si structura reala au aceeasi marime",
        };

        /// <summary>Why, in two sentences.</summary>
        /// <param name="a">The proposal.</param>
        /// <returns>Three or four lines joined by '\n', with the thresholds read from Th.</returns>
        internal static string AdviceNote(SmoothScaleAdvice a) => a.Kind switch
        {
            SmoothAdviceKind.Proposed =>
                $"Cea mai mica treapta la care textura ramasa scade sub {Th.SmoothTextureGone:F2} % fara ca\n"
              + $"asimetria azimutala sau inclinarea sa scada cu peste {Th.SmoothScaleLoss:F1} puncte - cele doua\n"
              + "teste pe care textura nu le atinge, deci o scadere acolo e structura reala care pleaca.",
            SmoothAdviceKind.NoTexture =>
                $"Textura fina a masterului e sub {Th.SmoothTextureMin:F2} %: nu exista celule de difuzor de scos.\n"
              + "O netezire ar lua doar praful, si daca vrei asta (flat de biblioteca) alege scara de mana;\n"
              + "pentru sesiunea ale carei lights au acelasi praf, flat-ul e mai bun asa cum e.",
            _ =>
                "Orice treapta care scoate textura scade si asimetria azimutala sau inclinarea cu peste\n"
              + $"{Th.SmoothScaleLoss:F1} puncte: textura difuzorului si o structura reala a obiectivului au aceeasi\n"
              + "marime, si niciun nucleu nu le desparte. Refa flat-urile cu difuzorul mai aproape de\n"
              + "lentila (celulele se defocalizeaza) sau alege scara de mana, stiind ce pierzi.",
        };

        /// <summary>
        /// What the rule cannot know. It sits under the conclusion in the report and on the tab.
        /// </summary>
        /// <returns>Three lines joined by '\n'.</returns>
        internal static string AdviceCaveat()
            => "Regula propune, nu decide: nu stie ce e al obiectivului si ce e al sursei - punctul cald al\n"
             + "unui LCD arata la fel ca un inel al lentilei - si e oarba la o structura reala care sta doar\n"
             + "in colturi, in afara cercului inscris. Uita-te in harta componentei scoase inainte sa apesi.";

        /// <summary>
        /// "-> fisier salvat: ...", the counterpart of RunReport.WriteMapSaved for the FITS.
        /// </summary>
        /// <param name="path">The path the smoothed flat was written to, as printed.</param>
        /// <returns>The line, indented by four spaces.</returns>
        internal static string SavedLine(string path) => $"    -> flat netezit salvat: {path}";

        /// <summary>Writes each line of a text indented by four spaces.</summary>
        /// <param name="text">The lines, joined by '\n'.</param>
        /// <param name="w">Where to write.</param>
        static void Indent(string text, TextWriter w)
        {
            foreach (string line in text.Split('\n')) w.WriteLine("    " + line);
        }
    }
}
