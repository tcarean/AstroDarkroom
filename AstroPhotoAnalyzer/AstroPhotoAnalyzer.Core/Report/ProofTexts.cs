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

// ProofTexts.cs - the Soft proof tab's sentences (R-161 to R-163): the problems, the profile's
// facts, the legend of the paper's gamut warning with its share, the names and the one-line help of
// the intents, the caption of the window.
//
// The window writes none of them: it shows them. Every number in them is the profile's or a
// threshold's, formatted by Fmt (ADR 0012, ADR 0048).

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The sentences the Soft proof tab and its window show, in the interface language (ADR
    /// 0066, ADR 0048); the console prints none of them.
    /// </summary>
    internal static class ProofTexts
    {
        /// <summary>Without a profile behind the Dmax the tab shows this alone (R-162).</summary>
        internal static string NoProfile => Tr.Pick(
            "the paper's ICC profile is missing: choose it with the \"From ICC profile...\" button beside Dmax; without it, the program does not know how the colours would come out on paper",
            "lipseste profilul ICC al hartiei: alege-l cu butonul \"Din profil ICC...\" de langa Dmax; fara el, programul nu stie cum ar iesi culorile pe hartie");

        internal static string Computing => Tr.Pick("computing the soft proof...", "se calculeaza soft proof-ul...");

        /// <summary>The profile's name (R-162).</summary>
        /// <param name="p">The profile.</param>
        /// <returns>The sentence.</returns>
        internal static string ProfileLine(ProofProfile p)
            => Tr.Pick($"the paper's profile: {p.Description}", $"profilul hartiei: {p.Description}");

        /// <summary>The paper's white in L*a*b*, the media white of the profile (R-162).</summary>
        /// <param name="p">The profile.</param>
        /// <returns>The sentence.</returns>
        internal static string PaperWhiteLine(ProofProfile p)
        {
            var (l, a, b) = p.PaperWhiteLab;
            // A chroma that rounds to zero is written 0.0, not -0.0 (the baryta's a* is -0.01).
            a = Math.Abs(a) < 0.05 ? 0 : a;
            b = Math.Abs(b) < 0.05 ? 0 : b;
            return Tr.Pick($"paper white: L* {Fmt.F1(l)} of 100, a* {Fmt.F1(a)}, b* {Fmt.F1(b)} (for a* and b*, 0 means no tint); this is what the image's white becomes on paper",
                $"albul hartiei: L* {Fmt.F1(l)} din 100, a* {Fmt.F1(a)}, b* {Fmt.F1(b)} (la a* si b*, 0 inseamna fara nuanta); asa iese pe hartie albul imaginii");
        }

        /// <summary>The paper's black in L* and Dmax, the figures of R-028 (R-162).</summary>
        /// <param name="p">The profile.</param>
        /// <returns>The sentence.</returns>
        internal static string PaperBlackLine(ProofProfile p)
            => Tr.Pick($"paper black: L* {Fmt.F1(p.PaperBlackLstar)} of 100, at Dmax {Fmt.Dmax(p.Dmax)}: the darkest tone the paper can print, the same black as in the Tones tab",
                $"negrul hartiei: L* {Fmt.F1(p.PaperBlackLstar)} din 100, la Dmax {Fmt.Dmax(p.Dmax)}: cel mai inchis ton pe care il poate tipari hartia, acelasi negru ca in tabul Tonuri");

        /// <summary>The legend of the paper's gamut warning, with its share (R-161).</summary>
        /// <param name="pct">The share of the proof's pixels outside the paper's gamut, in percent.</param>
        /// <returns>The sentence.</returns>
        internal static string PaperGamutLegend(double pct)
            => Tr.Pick($"colours the paper cannot reproduce (the gamut): {Fmt.Pct(pct)} of the pixels (on paper they would come out another tone or colour, by over {Fmt.F0(Thresholds.CurveGamutDeltaE)}; the same rule as in the Curves Editing tab)",
                $"culori pe care hartia nu le poate reda (gamutul): {Fmt.Pct(pct)} din pixeli (pe hartie ar iesi alt ton sau alta culoare, cu peste {Fmt.F0(Thresholds.CurveGamutDeltaE)}; aceeasi regula ca in tabul Editare Curbe)");

        /// <summary>The name of an intent, as the radio button shows it (R-163).</summary>
        /// <param name="intent">The intent.</param>
        /// <returns>The name.</returns>
        internal static string IntentName(ProofIntent intent) => intent switch
        {
            ProofIntent.Perceptual => "Perceptual",
            ProofIntent.Relative => Tr.Pick("Relative", "Relativ"),
            _ => Tr.Pick("Relative, no black point compensation", "Relativ, fara compensarea negrului"),
        };

        /// <summary>One line on what an intent does, the radio button's tooltip (R-163).</summary>
        /// <param name="intent">The intent.</param>
        /// <returns>The sentence.</returns>
        internal static string IntentHelp(ProofIntent intent) => intent switch
        {
            ProofIntent.Perceptual => Tr.Pick("Every colour moves a little, so that the ones the paper cannot reproduce fit too. The usual choice for a photograph.",
                "Toate culorile se muta putin, ca sa incapa si cele pe care hartia nu le poate reda. Alegerea obisnuita pentru o fotografie."),
            ProofIntent.Relative => Tr.Pick("The colours the paper can reproduce stay as they are; the others move to the nearest colour it can reproduce. The image's black becomes the paper's black (black point compensation).",
                "Culorile pe care hartia le poate reda raman cum sunt; celelalte trec la cea mai apropiata culoare pe care o poate reda. Negrul imaginii devine negrul hartiei (compensarea negrului)."),
            _ => Tr.Pick("Like Relative, but without black point compensation: everything darker than the paper's black prints as that same black, and the detail there is lost. This is how the Tones tab counts what is lost in the black.",
                "Ca Relativ, dar fara compensarea negrului: tot ce e mai intunecat decat negrul hartiei iese la fel de negru, iar detaliile de acolo se pierd. Asa socoteste si tabul Tonuri ce se pierde in negru."),
        };

        /// <summary>The caption of the proof: the profile and the intent, the paper and ink simulated (R-163).</summary>
        /// <param name="p">The profile.</param>
        /// <param name="intent">The intent shown.</param>
        /// <returns>The sentence.</returns>
        internal static string Caption(ProofProfile p, ProofIntent intent)
            => $"{p.Description}: {IntentName(intent)}; " + Tr.Pick("paper and ink simulated", "hartia si cerneala simulate");
    }
}
