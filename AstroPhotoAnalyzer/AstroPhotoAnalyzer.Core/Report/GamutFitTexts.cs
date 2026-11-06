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

// GamutFitTexts.cs - every sentence of the Culori pentru hartie tab (R-176, R-178, ADR 0082):
// the variants' names and helps, the evidence under each, the recipe for a hand-made mask, the
// proposed file name, the HISTORY cards and the TIFF description of the saved file. In plain
// words (ADR 0076): no median, percentile, chroma or knee; the numbers interpolated.

using System;
using System.Collections.Generic;
using System.IO;

namespace AstroPhotoAnalyzer
{
    /// <summary>The texts of the paper fit, all pairs in the interface language.</summary>
    internal static class GamutFitTexts
    {
        internal static string Computing => Tr.Pick("fitting the colours to the paper...", "se potrivesc culorile pentru hartie...");

        /// <summary>The name of a variant, as the gallery shows it.</summary>
        /// <param name="knee">The variant's knee: 1, GamutFitKneeSoft or GamutFitKneeSofter.</param>
        /// <returns>The name.</returns>
        internal static string Name(double knee)
            => knee >= 1 ? Tr.Pick("Exact", "Exacta")
             : knee >= Thresholds.GamutFitKneeSoft ? Tr.Pick("Soft", "Lina")
             : Tr.Pick("Softer", "Foarte lina");

        /// <summary>One line on what a variant does, for the evidence's first row.</summary>
        /// <param name="knee">The variant's knee.</param>
        /// <returns>The sentence.</returns>
        internal static string Help(double knee)
            => knee >= 1
                ? Tr.Pick("only the colours the paper cannot hold are changed: each loses just enough saturation to fit, its tone and its hue kept",
                          "se schimba doar culorile pe care hartia nu le tine: fiecare pierde exact atata saturatie cat sa incapa, cu tonul si nuanta pastrate")
                : Tr.Pick($"the colours near the limit of the paper are eased too, from {Fmt.F0(100 * knee)} % of it, so that a run of growing saturation keeps its steps instead of ending flat; tone and hue kept",
                          $"se indulcesc si culorile de langa limita hartiei, de la {Fmt.F0(100 * knee)} % din ea, ca o trecere cu saturatie crescatoare sa-si pastreze treptele in loc sa se termine plat; tonul si nuanta raman");

        /// <summary>
        /// The evidence of a variant, line by line, the warnings marked (a tone out of reach):
        /// the shares before and after, the pixels changed and how much they lose, the dark
        /// share, the recipe for a hand-made mask.
        /// </summary>
        /// <param name="v">The variant.</param>
        /// <param name="recipe">The recipe of the image, the same under every variant.</param>
        /// <returns>The rows.</returns>
        internal static IReadOnlyList<(string Text, bool Warning)> EvidenceRows(GamutFitVariant v, GamutFitRecipe recipe)
        {
            GamutFitEvidence e = v.Evidence;
            var o = new List<(string, bool)>
            {
                (Help(v.Knee), false),
                (Tr.Pick($"colours the paper cannot reproduce, before: {Fmt.Pct(e.PctOutsideBefore)} of the pixels", $"culori pe care hartia nu le poate reda, inainte: {Fmt.Pct(e.PctOutsideBefore)} din pixeli"), false),
                (Tr.Pick($"colours the paper cannot reproduce, after: {Fmt.Pct(e.PctOutsideAfter)}", $"culori pe care hartia nu le poate reda, dupa: {Fmt.Pct(e.PctOutsideAfter)}"), false),
                (Tr.Pick($"pixels changed: {Fmt.Pct(e.PctTouched)}; half of them lose at most {Fmt.F1(e.ChromaLostMedian)} of saturation, 99 of 100 at most {Fmt.F1(e.ChromaLostP99)} (on a scale where 3 is just visible)",
                         $"pixeli schimbati: {Fmt.Pct(e.PctTouched)}; jumatate din ei pierd cel mult {Fmt.F1(e.ChromaLostMedian)} din saturatie, 99 din 100 cel mult {Fmt.F1(e.ChromaLostP99)} (pe o scara pe care 3 abia se vede)"), false),
                (Tr.Pick($"of the colours the paper cannot reproduce, {Fmt.Pct(e.PctOutsideUnderLowSeparation)} are dark tones, under L* {Fmt.F0(Thresholds.LowSeparationLstar)}",
                         $"din culorile pe care hartia nu le poate reda, {Fmt.Pct(e.PctOutsideUnderLowSeparation)} sunt tonuri inchise, sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}"), false),
            };
            if (e.PctUnreachable > 0)
                o.Add((Tr.Pick($"{Fmt.Pct(e.PctUnreachable)} of the pixels have a tone the paper cannot make however little saturation they keep: they stay as they are; lift the background when you redo the stretch (the Tones tab)",
                               $"{Fmt.Pct(e.PctUnreachable)} din pixeli au un ton pe care hartia nu-l poate face oricat de putina saturatie ar pastra: raman cum sunt; ridica fundalul cand refaci stretch-ul (tabul Tonuri)"), true));
            o.Add((RecipeLine(recipe), false));
            return o;
        }

        /// <summary>
        /// The recipe for a hand-made mask in another program: the tone band and the saturation
        /// factor, with what it would leave.
        /// </summary>
        /// <param name="r">The recipe.</param>
        /// <returns>The sentence.</returns>
        internal static string RecipeLine(GamutFitRecipe r)
        {
            if (r.LstarCeiling <= 0)
                return Tr.Pick("by hand, in another program: nothing to do, the paper holds every colour", "de mana, in alt program: nimic de facut, hartia tine toate culorile");
            return Tr.Pick($"by hand, in another program: a mask on the tones under L* {Fmt.F0(r.LstarCeiling)} ({Fmt.Pct(r.PctInBand)} of the pixels) with the saturation at {Fmt.F0(100 * r.Factor)} %; it would leave {Fmt.Pct(r.PctLeft)} of the colours the paper cannot reproduce",
                           $"de mana, in alt program: o masca pe tonurile sub L* {Fmt.F0(r.LstarCeiling)} ({Fmt.Pct(r.PctInBand)} din pixeli) cu saturatia la {Fmt.F0(100 * r.Factor)} %; ar lasa {Fmt.Pct(r.PctLeft)} din culorile pe care hartia nu le poate reda");
        }

        /// <summary>
        /// The file name proposed for a variant: the input's name, the variant, the input's
        /// kind (TIFF for a TIFF input, FITS for any other).
        /// </summary>
        /// <param name="inputPath">The input's path or name.</param>
        /// <param name="variant">The variant's name, as the gallery shows it.</param>
        /// <returns>The file name alone.</returns>
        internal static string ProposedFileName(string inputPath, string variant)
        {
            string ext = Path.GetExtension(inputPath);
            bool tiff = ext.Equals(".tif", StringComparison.OrdinalIgnoreCase) || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
            return Path.GetFileNameWithoutExtension(inputPath) + "_" + variant.Replace(' ', '-') + (tiff ? ".tif" : ".fit");
        }

        /// <summary>Whether a target's extension asks for a TIFF (otherwise a FITS is written).</summary>
        /// <param name="target">The file to write.</param>
        /// <returns>True for .tif or .tiff, in any case.</returns>
        internal static bool IsTiff(string target)
        {
            string ext = Path.GetExtension(target);
            return ext.Equals(".tif", StringComparison.OrdinalIgnoreCase) || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The HISTORY cards of a saved FITS: the program, the variant, the rule in words, the profile.</summary>
        /// <param name="v">The variant saved.</param>
        /// <param name="profile">The profile's name.</param>
        /// <returns>The texts, one card each, each under 72 characters.</returns>
        internal static IReadOnlyList<string> History(GamutFitVariant v, string profile)
            => new[]
            {
                Tr.Pick($"{VersionInfo.Banner}: colours fitted to the paper, {v.Name}", $"{VersionInfo.Banner}: culori potrivite pentru hartie, {v.Name}"),
                Tr.Pick("the colours the paper cannot hold lose saturation; tone and hue kept", "culorile pe care hartia nu le tine pierd saturatie; ton si nuanta pastrate"),
                Tr.Pick($"eased from {Fmt.F0(100 * v.Knee)} % of the paper's limit", $"indulcite de la {Fmt.F0(100 * v.Knee)} % din limita hartiei"),
                Tr.Pick("paper: ", "hartie: ") + profile,
            };

        /// <summary>The description tag of a saved TIFF.</summary>
        /// <param name="v">The variant saved.</param>
        /// <param name="profile">The profile's name.</param>
        /// <returns>One line, ASCII.</returns>
        internal static string Description(GamutFitVariant v, string profile)
            => Tr.Pick($"{VersionInfo.Banner}: colours fitted to the paper ({v.Name}, {profile})", $"{VersionInfo.Banner}: culori potrivite pentru hartie ({v.Name}, {profile})");

        /// <summary>The note under the gallery: what the fit does and does not do.</summary>
        internal static string Note => Tr.Pick(
            "The fit lowers the saturation of the colours the paper cannot reproduce, each along its own hue and at its own tone; it never changes a tone or a hue. A tone too dark for the paper stays dark: lift the background when you redo the stretch (the Tones tab). The image here is a reduced copy; the program assumes the pixels are sRGB.",
            "Potrivirea scade saturatia culorilor pe care hartia nu le poate reda, fiecare pe nuanta ei si la tonul ei; nu schimba niciodata un ton sau o nuanta. Un ton prea inchis pentru hartie ramane inchis: ridica fundalul cand refaci stretch-ul (tabul Tonuri). Imaginea de aici e o copie micsorata; programul presupune ca pixelii sunt sRGB.");
    }
}
