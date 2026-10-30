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
// facts, the legends of the two warnings with their shares, the names and the one-line help of
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
            "no ICC profile: choose the paper's profile with the \"From ICC profile...\" button beside Dmax; the soft proof needs its tables",
            "fara profil ICC: alege profilul hartiei cu butonul \"Din profil ICC...\" de langa Dmax; soft proof-ul are nevoie de tabelele lui");

        internal static string Computing => Tr.Pick("computing the soft proof...", "se calculeaza soft proof-ul...");

        /// <summary>The profile's name, with its tables named (R-162).</summary>
        /// <param name="p">The profile.</param>
        /// <returns>The sentence.</returns>
        internal static string ProfileLine(ProofProfile p)
            => Tr.Pick($"profile: {p.Description} (its perceptual table B2A0, its colorimetric tables B2A1 and A2B1)", $"profil: {p.Description} (tabela lui perceptuala B2A0, tabelele lui colorimetrice B2A1 si A2B1)");

        /// <summary>The paper's white in L*a*b*, the media white of the profile (R-162).</summary>
        /// <param name="p">The profile.</param>
        /// <returns>The sentence.</returns>
        internal static string PaperWhiteLine(ProofProfile p)
        {
            var (l, a, b) = p.PaperWhiteLab;
            // A chroma that rounds to zero is written 0.0, not -0.0 (the baryta's a* is -0.01).
            a = Math.Abs(a) < 0.05 ? 0 : a;
            b = Math.Abs(b) < 0.05 ? 0 : b;
            return Tr.Pick($"paper white L* {Fmt.F1(l)}, a* {Fmt.F1(a)}, b* {Fmt.F1(b)} (the profile's media white; what the image's white becomes with the simulation on)",
                $"albul hartiei L* {Fmt.F1(l)}, a* {Fmt.F1(a)}, b* {Fmt.F1(b)} (albul mediului din profil; ce devine albul imaginii cu simularea pornita)");
        }

        /// <summary>The paper's black in L* and Dmax, the figures of R-028 (R-162).</summary>
        /// <param name="p">The profile.</param>
        /// <returns>The sentence.</returns>
        internal static string PaperBlackLine(ProofProfile p)
            => Tr.Pick($"paper black L* {Fmt.F1(p.PaperBlackLstar)} at Dmax {Fmt.Dmax(p.Dmax)} (the device black through the colorimetric table, the black of the Tones tab)",
                $"negrul hartiei L* {Fmt.F1(p.PaperBlackLstar)} la Dmax {Fmt.Dmax(p.Dmax)} (negrul dispozitivului prin tabela colorimetrica, negrul din tabul Tonuri)");

        /// <summary>The legend of the paper's gamut warning, with its share (R-161).</summary>
        /// <param name="pct">The share of the proof's pixels outside the paper's gamut, in percent.</param>
        /// <returns>The sentence.</returns>
        internal static string PaperGamutLegend(double pct)
            => Tr.Pick($"outside the paper's gamut: {Fmt.Pct(pct)} of the pixels (a*, b* moved by more than {Fmt.F0(Thresholds.CurveGamutDeltaAb)} on the round trip through the profile, the rule of Curves Editing)",
                $"in afara gamutului hartiei: {Fmt.Pct(pct)} din pixeli (a*, b* mutate cu peste {Fmt.F0(Thresholds.CurveGamutDeltaAb)} la drumul dus-intors prin profil, regula din Editare Curbe)");

        /// <summary>The legend of the screen's gamut warning, with its share (R-161).</summary>
        /// <param name="pct">The share of the proof's pixels outside the screen's gamut, in percent.</param>
        /// <returns>The sentence.</returns>
        internal static string ScreenGamutLegend(double pct)
            => Tr.Pick($"outside the screen's gamut: {Fmt.Pct(pct)} of the pixels (the paper shows them, an sRGB screen cannot: the clipped colour differs by more than Delta E {Fmt.F1(Thresholds.ProofScreenGamutDeltaE)})",
                $"in afara gamutului ecranului: {Fmt.Pct(pct)} din pixeli (hartia le reda, un ecran sRGB nu: culoarea taiata difera cu peste Delta E {Fmt.F1(Thresholds.ProofScreenGamutDeltaE)})");

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
            ProofIntent.Perceptual => Tr.Pick("The profile's own perceptual table (B2A0): every colour moved so that the ones outside the gamut fit; Lightroom's default",
                "Tabela perceptuala a profilului (B2A0): toate culorile mutate ca sa incapa cele din afara gamutului; implicitul din Lightroom"),
            ProofIntent.Relative => Tr.Pick("The colorimetric table (B2A1) with black point compensation: the image's black lands on the paper's, the colours inside the gamut stay; Lightroom's Relative",
                "Tabela colorimetrica (B2A1) cu compensarea negrului: negrul imaginii ajunge pe al hartiei, culorile din gamut raman; Relative din Lightroom"),
            _ => Tr.Pick("The colorimetric table without compensation: every tone darker than the paper's black prints as that black, what the Tones tab counts",
                "Tabela colorimetrica fara compensare: orice ton mai intunecat decat negrul hartiei se tipareste cu acel negru, ce numara tabul Tonuri"),
        };

        /// <summary>The caption of the proof: the profile, the intent, the state of the simulation (R-163).</summary>
        /// <param name="p">The profile.</param>
        /// <param name="intent">The intent shown.</param>
        /// <param name="simulate">Whether the paper's white and black are shown as they are.</param>
        /// <returns>The sentence.</returns>
        internal static string Caption(ProofProfile p, ProofIntent intent, bool simulate)
            => $"{p.Description}: {IntentName(intent)}; "
               + (simulate ? Tr.Pick("paper and ink simulated", "hartia si cerneala simulate") : Tr.Pick("the paper's white and black as the screen's", "albul si negrul hartiei ca ale ecranului"));
    }
}
